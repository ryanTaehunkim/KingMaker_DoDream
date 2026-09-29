using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 미션 시작/완료의 서버 권한 처리.
// - 미니게임 플레이 자체는 클라이언트 로컬, 시작 요청과 결과 보고만 서버가 검증한다.
// - 검증 항목: 일차 진행 여부, 일차 중복 클리어, 도구 보유, 스테이션 점유, 실패 쿨다운, 거리, 최소 소요시간
// - 네트워크가 시작되지 않은 상태(오프라인 테스트)에서는 같은 검증을 로컬에서 수행한다.
// 씬에 NetworkObject와 함께 1개 배치한다.
public class MissionManager : NetworkBehaviour
{
    public static MissionManager Instance { get; private set; }

    private const ulong OfflineClientId = 0;
    private const float RequestTimeout = 3f;

    [Header("일차")]
    [Tooltip("일차 시스템이 연결되기 전 테스트용: 시작 시 1일차를 자동으로 연다")]
    [SerializeField] private bool startDayOnLaunch = true;

    [Header("검증")]
    [Tooltip("스테이션과 플레이어 사이 허용 거리(m)")]
    [SerializeField] private float maxInteractDistance = 5f;
    [Tooltip("제한시간 초과 판정 시 네트워크 지연 허용치(초)")]
    [SerializeField] private float resultLatencyGrace = 2f;

    [Header("보상")]
    [Tooltip("등급 시스템이 연결되기 전까지 PlayerStateList.cardGrade에 보상을 직접 반영")]
    [SerializeField] private bool applyGradeToPlayerState = true;

    [Header("로그")]
    [SerializeField] private bool writeCsvLog = true;

    private NetworkVariable<int> netDay = new NetworkVariable<int>(0);
    private NetworkVariable<bool> netDayActive = new NetworkVariable<bool>(false);
    private int offlineDay;
    private bool offlineDayActive;

    // ---- 서버 상태 ----
    private class Session
    {
        public ulong clientId;
        public MissionStation station;
        public double startTime;
    }

    private readonly Dictionary<int, Session> sessionsByStation = new Dictionary<int, Session>();
    private readonly Dictionary<ulong, Session> sessionsByClient = new Dictionary<ulong, Session>();
    private readonly Dictionary<ulong, HashSet<int>> clearedByClient = new Dictionary<ulong, HashSet<int>>();
    private readonly Dictionary<(ulong, int), double> retryAvailableAt = new Dictionary<(ulong, int), double>();
    private readonly MissionProgressTracker tracker = new MissionProgressTracker();

    // ---- 클라이언트 상태 ----
    private bool requestPending;
    private float requestSentTime;
    private PlayerStateList requestingPlayer;
    private MissionStation activeStation;
    private MinigameResult lastReportedResult;

    public bool IsOnline => IsSpawned;
    public bool IsAuthority => !IsOnline || IsServer;
    public int CurrentDay => IsOnline ? netDay.Value : offlineDay;
    public bool IsDayActive => IsOnline ? netDayActive.Value : offlineDayActive;

    private static double Now => Time.unscaledTimeAsDouble;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MissionManager] 중복 MissionManager가 있어 비활성화합니다.", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        bool networkRunning = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (startDayOnLaunch && !networkRunning && !IsOnline)
        {
            ServerStartDay(1);
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        // 오프라인에서 쌓인 상태는 버리고 네트워크 기준으로 다시 시작
        ResetServerState();
        activeStation = null;
        requestPending = false;

        netDayActive.OnValueChanged += OnNetDayActiveChanged;

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            if (startDayOnLaunch) ServerStartDay(1);
        }
    }

    public override void OnNetworkDespawn()
    {
        netDayActive.OnValueChanged -= OnNetDayActiveChanged;
        if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        MinigameController.Instance?.CancelCurrent(MinigameCancelReason.Server);
    }

    private void OnNetDayActiveChanged(bool oldValue, bool newValue)
    {
        if (IsServer) return; // 서버는 ServerStartDay/ServerEndDay에서 직접 발생시킨다
        if (newValue) MissionEvents.RaiseDayStarted(netDay.Value);
        else MissionEvents.RaiseDayEnded(netDay.Value);
    }

    private void Update()
    {
        if (requestPending && Time.unscaledTime - requestSentTime > RequestTimeout)
        {
            requestPending = false;
        }
    }

    // =====================================================================
    // 클라이언트 API
    // =====================================================================

    public void RequestStart(MissionStation station, PlayerStateList playerState = null)
    {
        if (station == null || station.Mission == null) return;
        if (requestPending || activeStation != null || MinigameController.IsBusy) return;

        // 즉시 피드백용 로컬 사전 검사 (최종 판정은 서버)
        if (!IsDayActive) { ShowDenied(station, MissionDenyReason.DayNotActive); return; }
        if (IsClearedLocally(station.Mission.missionId)) { ShowDenied(station, MissionDenyReason.AlreadyCleared); return; }

        requestPending = true;
        requestSentTime = Time.unscaledTime;
        requestingPlayer = playerState;

        if (IsOnline)
        {
            RequestStartRpc(station.StationId);
        }
        else
        {
            var reason = ServerTryStart(OfflineClientId, station.StationId);
            if (reason == MissionDenyReason.None) OnStartApproved(station.StationId);
            else OnStartDenied(station.StationId, reason);
        }
    }

    // 내 클리어 기록 기준 (스테이션 표시용)
    public bool IsClearedLocally(int missionId)
    {
        if (IsOnline)
        {
            var ps = GetLocalPlayerState();
            return ps != null && ps.HasClearedMission(missionId);
        }
        return clearedByClient.TryGetValue(OfflineClientId, out var set) && set.Contains(missionId);
    }

    private void OnStartApproved(int stationId)
    {
        requestPending = false;

        if (!MissionStation.TryGet(stationId, out var station))
        {
            SendResult(stationId, MinigameResult.Cancelled, 0f);
            return;
        }

        activeStation = station;
        var controller = MinigameController.GetOrCreate();
        var player = requestingPlayer != null ? requestingPlayer : GetLocalPlayerState();
        bool opened = controller.Open(station.Mission, player,
            (result, elapsed) => SendResult(stationId, result, elapsed));

        if (!opened) SendResult(stationId, MinigameResult.Cancelled, 0f);
    }

    private void OnStartDenied(int stationId, MissionDenyReason reason)
    {
        requestPending = false;
        MissionStation.TryGet(stationId, out var station);
        ShowDenied(station, reason);
    }

    private void SendResult(int stationId, MinigameResult result, float elapsed)
    {
        activeStation = null;
        lastReportedResult = result;

        if (IsOnline)
        {
            ReportResultRpc(stationId, result, elapsed);
        }
        else
        {
            ServerHandleResult(OfflineClientId, stationId, result, elapsed);
        }
    }

    private void OnResultConfirmed(int stationId, MinigameResult final, int gradeDelta)
    {
        MissionStation.TryGet(stationId, out var station);
        var mission = station != null ? station.Mission : null;

        var controller = MinigameController.Instance;
        if (controller != null)
        {
            if (final == MinigameResult.Success)
                controller.ShowToast(gradeDelta != 0 ? $"미션 성공! 등급 +{gradeDelta}" : "미션 성공!");
            else if (final == MinigameResult.Fail && lastReportedResult == MinigameResult.Success)
                controller.ShowToast("결과가 인정되지 않았습니다.");
            else if (final == MinigameResult.Fail && mission != null && mission.allowRetryOnFail)
                controller.ShowToast($"미션 실패! {mission.failRetryCooldown:0}초 후 재도전 가능");
        }

        MissionEvents.RaiseLocalResultConfirmed(mission, final, gradeDelta);
    }

    private void OnForceCancel(MinigameCancelReason reason)
    {
        requestPending = false;
        MinigameController.Instance?.CancelCurrent(reason);
    }

    private void OnAllPersonalClearedLocal(int day)
    {
        MinigameController.Instance?.ShowToast("오늘의 개인 미션을 모두 완료했습니다! 힌트가 제공됩니다.", 3f);
        MissionEvents.RaiseLocalAllPersonalMissionsCleared(day);
    }

    private static void ShowDenied(MissionStation station, MissionDenyReason reason)
    {
        var mission = station != null ? station.Mission : null;
        string msg;
        switch (reason)
        {
            case MissionDenyReason.DayNotActive: msg = "지금은 미션을 수행할 수 없습니다."; break;
            case MissionDenyReason.AlreadyCleared: msg = "오늘은 이미 완료한 미션입니다."; break;
            case MissionDenyReason.MissingTool:
                string tool = mission != null && !string.IsNullOrEmpty(mission.requiredToolName) ? mission.requiredToolName : "도구";
                msg = $"{tool}{SubjectParticle(tool)} 필요합니다.";
                break;
            case MissionDenyReason.Occupied: msg = "다른 플레이어가 사용 중입니다."; break;
            case MissionDenyReason.OnCooldown: msg = "잠시 후 다시 시도하세요."; break;
            case MissionDenyReason.NotEnoughPlayers: msg = "함께할 플레이어가 필요합니다."; break;
            case MissionDenyReason.TooFar: msg = "너무 멀리 있습니다."; break;
            default: msg = "지금은 시작할 수 없습니다."; break;
        }
        MinigameController.GetOrCreate().ShowToast(msg);
    }

    // 받침 유무에 따라 "이/가"
    private static string SubjectParticle(string word)
    {
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return "이(가)";
        return (last - 0xAC00) % 28 != 0 ? "이" : "가";
    }

    // =====================================================================
    // RPC
    // =====================================================================

    [Rpc(SendTo.Server)]
    private void RequestStartRpc(int stationId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        var reason = ServerTryStart(sender, stationId);
        var target = RpcTarget.Single(sender, RpcTargetUse.Temp);
        if (reason == MissionDenyReason.None) StartApprovedRpc(stationId, target);
        else StartDeniedRpc(stationId, reason, target);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void StartApprovedRpc(int stationId, RpcParams rpcParams) => OnStartApproved(stationId);

    [Rpc(SendTo.SpecifiedInParams)]
    private void StartDeniedRpc(int stationId, MissionDenyReason reason, RpcParams rpcParams) => OnStartDenied(stationId, reason);

    [Rpc(SendTo.Server)]
    private void ReportResultRpc(int stationId, MinigameResult result, float clientElapsed, RpcParams rpcParams = default)
    {
        ServerHandleResult(rpcParams.Receive.SenderClientId, stationId, result, clientElapsed);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ResultConfirmedRpc(int stationId, MinigameResult final, int gradeDelta, RpcParams rpcParams) => OnResultConfirmed(stationId, final, gradeDelta);

    [Rpc(SendTo.SpecifiedInParams)]
    private void ForceCancelRpc(MinigameCancelReason reason, RpcParams rpcParams) => OnForceCancel(reason);

    [Rpc(SendTo.SpecifiedInParams)]
    private void AllPersonalClearedRpc(int day, RpcParams rpcParams) => OnAllPersonalClearedLocal(day);

    // =====================================================================
    // 서버 로직 (오프라인에서는 로컬에서 그대로 실행)
    // =====================================================================

    private MissionDenyReason ServerTryStart(ulong clientId, int stationId)
    {
        if (!MissionStation.TryGet(stationId, out var station) || station.Mission == null)
            return MissionDenyReason.InvalidStation;

        var mission = station.Mission;
        if (!IsDayActive) return MissionDenyReason.DayNotActive;
        if (sessionsByClient.ContainsKey(clientId)) return MissionDenyReason.AlreadyInMission;
        if (IsCleared(clientId, mission.missionId)) return MissionDenyReason.AlreadyCleared;
        if (sessionsByStation.ContainsKey(stationId)) return MissionDenyReason.Occupied;
        if (retryAvailableAt.TryGetValue((clientId, mission.missionId), out double t) && Now < t)
            return MissionDenyReason.OnCooldown;
        // 협동 미션(2인 이상)은 11주차에 별도 흐름으로 구현 예정
        if (mission.requiredPlayers > 1) return MissionDenyReason.NotEnoughPlayers;

        var player = GetPlayerObject(clientId);
        if (mission.RequiresTool)
        {
            var holder = player != null ? player.GetComponent<IMissionToolHolder>() : null;
            // 오프라인 테스트에서 플레이어 오브젝트가 없으면 씬의 인벤토리를 사용
            if (holder == null && !IsOnline) holder = FindFirstObjectByType<MissionToolInventory>();
            if (holder == null || !holder.HasTool(mission.requiredToolId)) return MissionDenyReason.MissingTool;
        }
        if (player != null && station.TryGetComponent<Collider>(out var col))
        {
            Vector3 closest = col.ClosestPoint(player.transform.position);
            if (Vector3.Distance(closest, player.transform.position) > maxInteractDistance)
                return MissionDenyReason.TooFar;
        }

        var session = new Session { clientId = clientId, station = station, startTime = Now };
        sessionsByStation[stationId] = session;
        sessionsByClient[clientId] = session;

        MissionEvents.RaiseMissionStarted(clientId, station);
        return MissionDenyReason.None;
    }

    private void ServerHandleResult(ulong clientId, int stationId, MinigameResult reported, float clientElapsed)
    {
        // 강제 종료 등으로 이미 끝난 세션이면 무시
        if (!sessionsByClient.TryGetValue(clientId, out var session) || session.station.StationId != stationId) return;
        EndSession(session);

        var mission = session.station.Mission;
        float serverElapsed = (float)(Now - session.startTime);
        var final = reported;

        if (final == MinigameResult.Success)
        {
            if (serverElapsed < mission.minClearTime)
            {
                Debug.LogWarning($"[MissionManager] 최소 소요시간 미달로 실패 처리: client {clientId}, {mission.displayName}, {serverElapsed:0.00}s");
                final = MinigameResult.Fail;
            }
            else if (serverElapsed > mission.timeLimit + resultLatencyGrace)
            {
                Debug.LogWarning($"[MissionManager] 제한시간 초과로 실패 처리: client {clientId}, {mission.displayName}, {serverElapsed:0.00}s");
                final = MinigameResult.Fail;
            }
        }

        int gradeDelta = 0;
        switch (final)
        {
            case MinigameResult.Success:
                MarkCleared(clientId, mission.missionId);
                gradeDelta = ServerApplyReward(clientId, mission);
                break;
            case MinigameResult.Fail:
                retryAvailableAt[(clientId, mission.missionId)] =
                    mission.allowRetryOnFail ? Now + mission.failRetryCooldown : double.MaxValue;
                break;
        }

        if (writeCsvLog) MissionLogger.Append(CurrentDay, clientId, mission, reported, final, clientElapsed, serverElapsed);
        MissionEvents.RaiseMissionFinished(clientId, session.station, final, serverElapsed);

        if (IsOnline) ResultConfirmedRpc(stationId, final, gradeDelta, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnResultConfirmed(stationId, final, gradeDelta);

        if (final == MinigameResult.Success) ServerCheckProgress(clientId);
    }

    private int ServerApplyReward(ulong clientId, MissionData mission)
    {
        MissionEvents.RaiseGradeChangeRequested(clientId, mission.gradeReward, mission.suitBonusEligible, mission);

        if (applyGradeToPlayerState)
        {
            var ps = GetPlayerState(clientId);
            if (ps != null && ps.IsSpawned) ps.cardGrade.Value += mission.gradeReward;
        }
        return mission.gradeReward;
    }

    private void ServerCheckProgress(ulong clientId)
    {
        if (clearedByClient.TryGetValue(clientId, out var set) && tracker.CheckAllPersonalCleared(clientId, set))
        {
            MissionEvents.RaiseAllPersonalMissionsCleared(clientId, CurrentDay);
            if (IsOnline) AllPersonalClearedRpc(CurrentDay, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            else OnAllPersonalClearedLocal(CurrentDay);
        }

        if (tracker.CheckAllSharedCleared(clearedByClient.Values))
        {
            MissionEvents.RaiseAllSharedMissionsCleared(CurrentDay);
        }
    }

    // ---- 일차 관리 (일차/컷오프 시스템에서 서버에서 호출) ----

    public void ServerStartDay(int day)
    {
        if (!IsAuthority) return;

        ServerCancelAllSessions(MinigameCancelReason.DayEnded);
        clearedByClient.Clear();
        retryAvailableAt.Clear();
        tracker.ResetDay();

        if (IsOnline)
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerStateList>(out var ps))
                    ps.ResetClearedMissions();
            }
            netDay.Value = day;
            netDayActive.Value = true;
        }
        else
        {
            offlineDay = day;
            offlineDayActive = true;
        }

        MissionEvents.RaiseDayStarted(day);
    }

    // 일차 종료(컷오프): 진행 중인 미니게임 전부 강제 취소
    public void ServerEndDay()
    {
        if (!IsAuthority) return;

        if (IsOnline) netDayActive.Value = false;
        else offlineDayActive = false;

        ServerCancelAllSessions(MinigameCancelReason.DayEnded);
        MissionEvents.RaiseDayEnded(CurrentDay);
    }

    // 특정 플레이어의 미니게임을 강제 종료 (예: 사망 처리 시 서버에서 호출)
    public void ServerCancelFor(ulong clientId, MinigameCancelReason reason)
    {
        if (!IsAuthority) return;
        if (sessionsByClient.TryGetValue(clientId, out var session))
        {
            EndSession(session);
            SendForceCancel(clientId, reason);
        }
    }

    private void ServerCancelAllSessions(MinigameCancelReason reason)
    {
        var sessions = new List<Session>(sessionsByClient.Values);
        foreach (var s in sessions)
        {
            EndSession(s);
            SendForceCancel(s.clientId, reason);
        }
    }

    private void SendForceCancel(ulong clientId, MinigameCancelReason reason)
    {
        if (IsOnline) ForceCancelRpc(reason, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnForceCancel(reason);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (sessionsByClient.TryGetValue(clientId, out var session)) EndSession(session);
    }

    private void EndSession(Session session)
    {
        sessionsByClient.Remove(session.clientId);
        sessionsByStation.Remove(session.station.StationId);
    }

    private void ResetServerState()
    {
        sessionsByClient.Clear();
        sessionsByStation.Clear();
        clearedByClient.Clear();
        retryAvailableAt.Clear();
        tracker.ResetDay();
    }

    private bool IsCleared(ulong clientId, int missionId)
        => clearedByClient.TryGetValue(clientId, out var set) && set.Contains(missionId);

    private void MarkCleared(ulong clientId, int missionId)
    {
        if (!clearedByClient.TryGetValue(clientId, out var set))
        {
            set = new HashSet<int>();
            clearedByClient[clientId] = set;
        }
        set.Add(missionId);

        // 클라이언트 표시용 동기화
        var ps = GetPlayerState(clientId);
        if (ps != null && ps.IsSpawned) ps.AddClearedMission(missionId);
    }

    // ---- 플레이어 조회 ----

    private GameObject GetPlayerObject(ulong clientId)
    {
        if (IsOnline)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
                return client.PlayerObject.gameObject;
            return null;
        }
        var ps = FindFirstObjectByType<PlayerStateList>();
        return ps != null ? ps.gameObject : null;
    }

    private PlayerStateList GetPlayerState(ulong clientId)
    {
        var go = GetPlayerObject(clientId);
        return go != null ? go.GetComponent<PlayerStateList>() : null;
    }

    private PlayerStateList GetLocalPlayerState()
    {
        if (IsOnline)
        {
            var po = NetworkManager.LocalClient?.PlayerObject;
            return po != null ? po.GetComponent<PlayerStateList>() : null;
        }
        return FindFirstObjectByType<PlayerStateList>();
    }
}
