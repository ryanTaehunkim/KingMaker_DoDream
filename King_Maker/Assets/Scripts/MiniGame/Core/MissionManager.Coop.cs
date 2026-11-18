using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 협동 미션(CoopSession) — 서버 권한 판정
// - 같은 CoopMissionData를 가진 스테이션들이 "자리"다. 플레이어가 자리에서 E를 누르면 서버에 참가 요청.
// - 자리 상태(대기/진행/성공, 마감 시각, 올린 값)는 NetworkList로 모든 클라이언트에 동기화 (라벨·라디오 표시용).
// - SyncAction(C1): 첫 자리 후 syncWindow초 안에 나머지 자리를 서로 다른 플레이어가 수행하면 성공.
// - SumItems(C2): 각자 숫자 패널을 들고 자리에 올림 → 다 차면 합이 목표와 같은지 판정.
// - SharedMinigame(C3~C5, G3, Q): 자리가 다 차면 서버 로직(CoopGameLogic)을 만들고 두 명에게 같은 미니게임을 연다.
//   클라이언트는 입력만 보내고, 서버가 상태와 결과를 보낸다.
// - 협동(Coop 타입) 성공 시 참여자 전원 클리어 + 보상 + MissionEvents.CoopMissionCleared 발행.
public partial class MissionManager
{
    [Header("협동 미션")]
    [Tooltip("테스트용: 한 명이 여러 자리를 수행해도 인정 (혼자 테스트할 때만 켜기)")]
    [SerializeField] private bool coopAllowSameClient = false;

    public enum CoopSlotPhase : byte { Waiting, Playing, Done }

    public struct CoopSlotState : INetworkSerializable, IEquatable<CoopSlotState>
    {
        public int stationId;
        public int missionId;
        public double deadline; // 서버 시각 기준 (Waiting일 때만 의미)
        public CoopSlotPhase phase;
        public int value;       // SumItems: 올린 숫자

        public bool Cleared => phase == CoopSlotPhase.Done;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref stationId);
            serializer.SerializeValue(ref missionId);
            serializer.SerializeValue(ref deadline);
            serializer.SerializeValue(ref phase);
            serializer.SerializeValue(ref value);
        }

        public bool Equals(CoopSlotState o) =>
            stationId == o.stationId && missionId == o.missionId && deadline.Equals(o.deadline) && phase == o.phase && value == o.value;
    }

    public enum CoopNotice : byte { Joined, Success, Failed, Expired, WrongSum }

    private class CoopRun
    {
        public CoopMissionData mission;
        public readonly Dictionary<int, ulong> slots = new Dictionary<int, ulong>(); // stationId → clientId
        public readonly Dictionary<int, int> slotValues = new Dictionary<int, int>(); // stationId → 값 (SumItems)
        public readonly List<int> joinOrder = new List<int>();
        public double startTime;
        public double deadline;
        public double resetAt;
        public CoopSlotPhase phase;

        // SharedMinigame
        public CoopGameLogic logic;
        public ulong[] roleClients;
        public int[] roleStations;
    }

    private class CoopHost : ICoopHost
    {
        private readonly MissionManager owner;
        private readonly CoopRun run;
        public CoopHost(MissionManager owner, CoopRun run) { this.owner = owner; this.run = run; }

        public int Day => owner.CurrentDay;
        public int GetGrade(int role) => owner.ServerGetGrade(run.roleClients[role]);
        public int GetPersonalClearCount(int role) => owner.ServerPersonalClearCount(run.roleClients[role]);
        public void Broadcast(CoopMsg msg) { for (int r = 0; r < run.roleClients.Length; r++) SendTo(r, msg); }
        public void SendTo(int role, CoopMsg msg) => owner.SendCoopGameMsg(run.roleClients[role], msg);
    }

    private readonly NetworkList<CoopSlotState> netCoopSlots = new NetworkList<CoopSlotState>();
    private readonly List<CoopSlotState> offlineCoopSlots = new List<CoopSlotState>();
    private readonly Dictionary<int, CoopRun> coopRuns = new Dictionary<int, CoopRun>(); // missionId → run
    private readonly Dictionary<ulong, CoopRun> coopGameByClient = new Dictionary<ulong, CoopRun>();

    // 클라이언트: 지금 내가 하고 있는 공유 미니게임
    private int localCoopStationId = -1;

    private double CoopNow => IsOnline ? NetworkManager.ServerTime.Time : Time.unscaledTimeAsDouble;

    private bool IsInCoopGame(ulong clientId) => coopGameByClient.ContainsKey(clientId);

    // =====================================================================
    // 클라이언트 API
    // =====================================================================

    // 스테이션 표시용: 이 자리가 참가/수행된 상태인지. remaining = Waiting일 때 마감까지 남은 초
    public bool TryGetCoopSlot(int stationId, out CoopSlotState slot, out float remaining)
    {
        remaining = 0f;
        int count = IsOnline ? netCoopSlots.Count : offlineCoopSlots.Count;
        for (int i = 0; i < count; i++)
        {
            slot = IsOnline ? netCoopSlots[i] : offlineCoopSlots[i];
            if (slot.stationId != stationId) continue;
            if (slot.phase == CoopSlotPhase.Waiting) remaining = Mathf.Max(0f, (float)(slot.deadline - CoopNow));
            return true;
        }
        slot = default;
        return false;
    }

    private void RequestCoopAction(MissionStation station)
    {
        if (!IsDayActive) { ShowDenied(station, MissionDenyReason.DayNotActive); return; }
        if (IsClearedLocally(station.Mission.missionId)) { ShowDenied(station, MissionDenyReason.AlreadyCleared); return; }
        if (TryGetCoopSlot(station.StationId, out _, out _)) { ShowDenied(station, MissionDenyReason.CoopSlotTaken); return; }

        if (IsOnline)
        {
            CoopActionRpc(station.StationId);
        }
        else
        {
            var reason = ServerTryCoopAction(OfflineClientId, station.StationId);
            if (reason != MissionDenyReason.None) ShowDenied(station, reason);
        }
    }

    // 공유 미니게임 입력 (CoopMinigameBase.Send)
    public void SendCoopInput(CoopMsg msg)
    {
        if (localCoopStationId < 0) return;
        if (IsOnline) CoopInputRpc(msg);
        else ServerCoopInput(OfflineClientId, msg);
    }

    private void OnCoopNotice(int stationId, CoopNotice notice, float value)
    {
        MissionStation.TryGet(stationId, out var station);
        var mission = station != null ? station.Mission as CoopMissionData : null;
        var controller = MinigameController.GetOrCreate();

        switch (notice)
        {
            case CoopNotice.Joined:
                controller.ShowToast(mission != null ? string.Format(mission.joinedMessage, value) : "상대가 와야 합니다!", 3f);
                break;
            case CoopNotice.Success:
                int delta = Mathf.RoundToInt(value);
                controller.ShowToast(delta != 0 ? $"협동 미션 성공! 등급 +{delta}" : "협동 미션 성공!", 3f);
                MissionEvents.RaiseLocalResultConfirmed(mission, MinigameResult.Success, delta);
                break;
            case CoopNotice.Failed:
                controller.ShowToast(mission != null ? mission.failMessage : "협동 미션 실패!", 3f);
                MissionEvents.RaiseLocalResultConfirmed(mission, MinigameResult.Fail, 0);
                break;
            case CoopNotice.WrongSum:
                controller.ShowToast($"합이 {value:0}! 목표와 달라요. 패널이 다시 흩어졌습니다.", 3f);
                MissionEvents.RaiseLocalResultConfirmed(mission, MinigameResult.Fail, 0);
                break;
            case CoopNotice.Expired:
                controller.ShowToast("상대가 오지 않아 취소되었습니다.", 3f);
                break;
        }
    }

    private void OnCoopGameBegin(int stationId, int role)
    {
        if (!MissionStation.TryGet(stationId, out var station) || !(station.Mission is CoopMissionData mission))
        {
            CoopLeave();
            return;
        }

        localCoopStationId = stationId;
        CoopMinigameBase.PendingRole = role;
        var controller = MinigameController.GetOrCreate();
        controller.HideToast(); // "상대 대기" 안내가 미니게임 하단 문구를 가리지 않게
        bool opened = controller.Open(mission, GetLocalPlayerState(), (result, elapsed) =>
        {
            // 서버가 끝낸 게 아니라 ESC/피격으로 닫힌 경우만 서버에 알린다
            var current = controller.Current as CoopMinigameBase;
            if (result == MinigameResult.Cancelled && (current == null || !current.IsServerEnded)) CoopLeave();
            localCoopStationId = -1;
        });
        if (!opened) CoopLeave();
    }

    private void OnCoopGameMsg(CoopMsg msg)
    {
        if (MinigameController.Instance != null && MinigameController.Instance.Current is CoopMinigameBase game)
            game.OnServerMessage(msg);
    }

    private void OnCoopGameEnd(int stationId, MinigameResult result, int delta)
    {
        MissionStation.TryGet(stationId, out var station);
        var mission = station != null ? station.Mission : null;

        if (MinigameController.Instance != null && MinigameController.Instance.Current is CoopMinigameBase game)
            game.OnServerEnded(result);
        localCoopStationId = -1;

        string msg;
        if (result == MinigameResult.Cancelled) msg = "상대가 나가서 미션이 취소되었습니다.";
        else if (delta > 0 && mission is CardDuelMissionData) msg = $"승리! 등급 +{delta}";
        else if (delta > 0) msg = result == MinigameResult.Success ? $"미션 성공! 등급 +{delta}" : $"등급 +{delta}";
        else if (delta < 0) msg = $"패배... 등급 {delta}";
        else msg = result == MinigameResult.Success ? "미션 성공!" : "미션 실패!";
        MinigameController.GetOrCreate().ShowToast(msg, 3f);

        if (result != MinigameResult.Cancelled) MissionEvents.RaiseLocalResultConfirmed(mission, result, delta);
    }

    private void CoopLeave()
    {
        localCoopStationId = -1;
        if (IsOnline) CoopLeaveRpc();
        else ServerCoopClientLeft(OfflineClientId);
    }

    // =====================================================================
    // RPC
    // =====================================================================

    [Rpc(SendTo.Server)]
    private void CoopActionRpc(int stationId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        var reason = ServerTryCoopAction(sender, stationId);
        if (reason != MissionDenyReason.None)
            StartDeniedRpc(stationId, reason, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server)]
    private void CoopInputRpc(CoopMsg msg, RpcParams rpcParams = default) => ServerCoopInput(rpcParams.Receive.SenderClientId, msg);

    [Rpc(SendTo.Server)]
    private void CoopLeaveRpc(RpcParams rpcParams = default) => ServerCoopClientLeft(rpcParams.Receive.SenderClientId);

    [Rpc(SendTo.SpecifiedInParams)]
    private void CoopNoticeRpc(int stationId, CoopNotice notice, float value, RpcParams rpcParams) => OnCoopNotice(stationId, notice, value);

    [Rpc(SendTo.SpecifiedInParams)]
    private void CoopGameBeginRpc(int stationId, int role, RpcParams rpcParams) => OnCoopGameBegin(stationId, role);

    [Rpc(SendTo.SpecifiedInParams)]
    private void CoopGameMsgRpc(CoopMsg msg, RpcParams rpcParams) => OnCoopGameMsg(msg);

    [Rpc(SendTo.SpecifiedInParams)]
    private void CoopGameEndRpc(int stationId, MinigameResult result, int delta, RpcParams rpcParams) => OnCoopGameEnd(stationId, result, delta);

    // 접속 중인 클라이언트에게만 보낸다 (오프라인이면 로컬 처리)
    private bool CanSendTo(ulong clientId) =>
        IsOnline ? NetworkManager.ConnectedClients.ContainsKey(clientId) : clientId == OfflineClientId;

    private void SendCoopNotice(ulong clientId, int stationId, CoopNotice notice, float value)
    {
        if (!CanSendTo(clientId)) return;
        if (IsOnline) CoopNoticeRpc(stationId, notice, value, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnCoopNotice(stationId, notice, value);
    }

    private void SendCoopGameBegin(ulong clientId, int stationId, int role)
    {
        if (!CanSendTo(clientId)) return;
        if (IsOnline) CoopGameBeginRpc(stationId, role, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnCoopGameBegin(stationId, role);
    }

    private void SendCoopGameMsg(ulong clientId, CoopMsg msg)
    {
        if (!CanSendTo(clientId)) return;
        if (IsOnline) CoopGameMsgRpc(msg, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnCoopGameMsg(msg);
    }

    private void SendCoopGameEnd(ulong clientId, int stationId, MinigameResult result, int delta)
    {
        if (!CanSendTo(clientId)) return;
        if (IsOnline) CoopGameEndRpc(stationId, result, delta, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else OnCoopGameEnd(stationId, result, delta);
    }

    // =====================================================================
    // 서버: 참가
    // =====================================================================

    private MissionDenyReason ServerTryCoopAction(ulong clientId, int stationId)
    {
        if (!MissionStation.TryGet(stationId, out var station) || !(station.Mission is CoopMissionData mission))
            return MissionDenyReason.InvalidStation;
        if (mission.mode == CoopMode.SharedMinigame && mission.minigamePrefab == null)
        {
            Debug.LogWarning($"[MissionManager] {mission.displayName}: minigamePrefab이 없습니다.");
            return MissionDenyReason.InvalidStation;
        }

        if (!IsDayActive) return MissionDenyReason.DayNotActive;
        if (!station.IsActivated) return MissionDenyReason.NotActivated;
        if (sessionsByClient.ContainsKey(clientId) || IsInCoopGame(clientId)) return MissionDenyReason.AlreadyInMission;
        if (IsCleared(clientId, mission.missionId)) return MissionDenyReason.AlreadyCleared;
        if (retryAvailableAt.TryGetValue((clientId, mission.missionId), out double t) && Now < t)
            return MissionDenyReason.OnCooldown;

        var player = GetPlayerObject(clientId);
        PlayerStateList ps = player != null ? player.GetComponent<PlayerStateList>() : null;
        if (ps != null && !ps.isAlive.Value) return MissionDenyReason.InvalidStation;
        if (player != null && station.TryGetComponent<Collider>(out var col))
        {
            Vector3 closest = col.ClosestPoint(player.transform.position);
            if (Vector3.Distance(closest, player.transform.position) > maxInteractDistance)
                return MissionDenyReason.TooFar;
        }

        double now = CoopNow;
        coopRuns.TryGetValue(mission.missionId, out var run);
        if (run != null)
        {
            if (run.phase != CoopSlotPhase.Waiting || run.slots.ContainsKey(stationId)) return MissionDenyReason.CoopSlotTaken;
            if (!coopAllowSameClient && run.slots.ContainsValue(clientId)) return MissionDenyReason.CoopAlreadyJoined;
        }

        // 자리(역할) 조건: 자리 번호가 정해진 스테이션이면 그 번호, 아니면 참가 순서
        int role = station.CoopSeat >= 0 ? station.CoopSeat : (run != null ? run.joinOrder.Count : 0);
        if (!mission.CanTakeRole(role, ServerGetGrade(clientId))) return MissionDenyReason.RoleNotAllowed;

        // C2: 들고 있는 숫자 패널
        MissionItem heldItem = null;
        if (mission.mode == CoopMode.SumItems)
        {
            heldItem = GetHeldMissionItem(ps);
            if (heldItem == null || heldItem.ItemId != mission.sumItemId) return MissionDenyReason.NeedItem;
        }

        if (run == null)
        {
            run = new CoopRun { mission = mission, startTime = now, deadline = now + mission.syncWindow, phase = CoopSlotPhase.Waiting };
            coopRuns[mission.missionId] = run;
        }

        int value = 0;
        if (heldItem != null)
        {
            value = heldItem.Value;
            ConsumeHeldItem(ps, heldItem);
        }

        run.slots[stationId] = clientId;
        run.slotValues[stationId] = value;
        run.joinOrder.Add(stationId);
        SetCoopSlot(new CoopSlotState { stationId = stationId, missionId = mission.missionId, deadline = run.deadline, value = value });
        MissionEvents.RaiseMissionStarted(clientId, station);

        if (run.slots.Count < mission.RequiredSlots)
        {
            SendCoopNotice(clientId, stationId, CoopNotice.Joined, (float)(run.deadline - now));
            return MissionDenyReason.None;
        }

        // 자리가 다 찼다
        switch (mission.mode)
        {
            case CoopMode.SyncAction:
                ServerCompleteCoop(run);
                break;
            case CoopMode.SumItems:
                int sum = 0;
                foreach (int v in run.slotValues.Values) sum += v;
                int target = CoopSumBoard.TryGetTarget(mission.missionId, out int tg) ? tg : -1;
                if (sum == target) ServerCompleteCoop(run);
                else ServerFailCoop(run, CoopNotice.WrongSum, sum);
                ServerReturnSumPanels(run);
                if (run.phase != CoopSlotPhase.Done) ServerRemoveRun(run);
                break;
            case CoopMode.SharedMinigame:
                ServerStartCoopGame(run);
                break;
        }
        return MissionDenyReason.None;
    }

    // =====================================================================
    // 서버: SyncAction / SumItems 결과
    // =====================================================================

    private void ServerCompleteCoop(CoopRun run)
    {
        var mission = run.mission;
        float elapsed = (float)(CoopNow - run.startTime);
        var participants = new List<ulong>();

        foreach (var kv in run.slots)
        {
            ulong clientId = kv.Value;
            if (participants.Contains(clientId)) continue;
            participants.Add(clientId);

            MarkCleared(clientId, mission.missionId);
            int delta = ServerApplyReward(clientId, mission);
            if (writeCsvLog) MissionLogger.Append(CurrentDay, clientId, mission, MinigameResult.Success, MinigameResult.Success, elapsed, elapsed);
            MissionStation.TryGet(kv.Key, out var station);
            MissionEvents.RaiseMissionFinished(clientId, station, MinigameResult.Success, elapsed);
            SendCoopNotice(clientId, kv.Key, CoopNotice.Success, delta);
        }

        ServerMarkRunDone(run);
        Debug.Log($"[MissionManager] 협동 미션 성공: {mission.displayName}, 참여 {string.Join(",", participants)}, {elapsed:0.00}s");
        if (mission.type == MissionType.Coop) MissionEvents.RaiseCoopMissionCleared(CurrentDay, participants.ToArray());
        foreach (ulong clientId in participants) ServerCheckProgress(clientId);
    }

    private void ServerFailCoop(CoopRun run, CoopNotice notice, float value)
    {
        var mission = run.mission;
        float elapsed = (float)(CoopNow - run.startTime);
        var notified = new HashSet<ulong>();

        foreach (var kv in run.slots)
        {
            ulong clientId = kv.Value;
            if (!notified.Add(clientId)) continue;

            retryAvailableAt[(clientId, mission.missionId)] =
                mission.allowRetryOnFail ? Now + mission.failRetryCooldown : double.MaxValue;
            if (writeCsvLog) MissionLogger.Append(CurrentDay, clientId, mission, MinigameResult.Fail, MinigameResult.Fail, elapsed, elapsed);
            MissionStation.TryGet(kv.Key, out var station);
            MissionEvents.RaiseMissionFinished(clientId, station, MinigameResult.Fail, elapsed);
            SendCoopNotice(clientId, kv.Key, notice, value);
        }
    }

    private void ServerMarkRunDone(CoopRun run)
    {
        run.phase = CoopSlotPhase.Done;
        run.resetAt = CoopNow + run.mission.resetDelay;
        foreach (int stationId in run.slots.Keys)
            SetCoopSlot(new CoopSlotState { stationId = stationId, missionId = run.mission.missionId, phase = CoopSlotPhase.Done, value = run.slotValues[stationId] });
    }

    private void ServerRemoveRun(CoopRun run)
    {
        coopRuns.Remove(run.mission.missionId);
        foreach (int stationId in run.slots.Keys) RemoveCoopSlot(stationId);
        if (run.roleClients != null)
            foreach (ulong c in run.roleClients)
                if (coopGameByClient.TryGetValue(c, out var r) && r == run) coopGameByClient.Remove(c);
    }

    // C2: 소모한 숫자 패널을 맵에 다시 흩어 놓는다 (다른 조가 계속 쓸 수 있게)
    private void ServerReturnSumPanels(CoopRun run)
    {
        if (run.mission.mode != CoopMode.SumItems || !CoopSumBoard.TryGet(run.mission.missionId, out var board)) return;
        foreach (int v in run.slotValues.Values)
            if (v > 0) board.ServerSpawnPanel(v);
    }

    // =====================================================================
    // 서버: 공유 미니게임
    // =====================================================================

    private void ServerStartCoopGame(CoopRun run)
    {
        var mission = run.mission;
        int n = run.joinOrder.Count;
        run.roleClients = new ulong[n];
        run.roleStations = new int[n];

        // 자리 번호가 정해진 스테이션은 그 역할, 나머지는 참가 순서대로 빈 역할에 배정
        var assigned = new bool[n];
        var pending = new List<int>();
        foreach (int sid in run.joinOrder)
        {
            MissionStation.TryGet(sid, out var st);
            int seat = st != null ? st.CoopSeat : -1;
            if (seat >= 0 && seat < n && !assigned[seat]) { assigned[seat] = true; run.roleStations[seat] = sid; }
            else pending.Add(sid);
        }
        int next = 0;
        foreach (int sid in pending)
        {
            while (assigned[next]) next++;
            assigned[next] = true;
            run.roleStations[next] = sid;
        }
        for (int r = 0; r < n; r++)
        {
            run.roleClients[r] = run.slots[run.roleStations[r]];
            coopGameByClient[run.roleClients[r]] = run;
        }

        run.phase = CoopSlotPhase.Playing;
        run.startTime = CoopNow;
        foreach (int sid in run.joinOrder)
            SetCoopSlot(new CoopSlotState { stationId = sid, missionId = mission.missionId, phase = CoopSlotPhase.Playing });

        // 화면을 먼저 열고(Begin) 그 다음 로직 시작 메시지를 보낸다 (RPC 순서 보장)
        for (int r = 0; r < n; r++) SendCoopGameBegin(run.roleClients[r], run.roleStations[r], r);

        run.logic = mission.CreateLogic();
        if (run.logic == null)
        {
            Debug.LogError($"[MissionManager] {mission.displayName}: CreateLogic()이 null입니다.");
            ServerAbortCoopGame(run);
            return;
        }
        run.logic.Init(new CoopHost(this, run), mission, n, UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        Debug.Log($"[MissionManager] 공유 미니게임 시작: {mission.displayName}, 역할 {string.Join(",", run.roleClients)}");
        if (run.logic.Ended) ServerFinishCoopGame(run);
    }

    private void ServerCoopInput(ulong clientId, CoopMsg msg)
    {
        if (!coopGameByClient.TryGetValue(clientId, out var run) || run.logic == null) return;
        int role = Array.IndexOf(run.roleClients, clientId);
        if (role < 0) return;
        run.logic.Input(role, msg);
        if (run.logic.Ended) ServerFinishCoopGame(run);
    }

    // 참가자가 나감(ESC/피격/접속 끊김): 대기 중이면 자리 반환, 진행 중이면 게임 중단
    private void ServerCoopClientLeft(ulong clientId)
    {
        if (coopGameByClient.TryGetValue(clientId, out var run))
        {
            ServerAbortCoopGame(run);
            return;
        }
    }

    private void ServerAbortCoopGame(CoopRun run)
    {
        if (run.logic != null && !run.logic.Ended) run.logic.Abort();
        if (run.logic != null) ServerFinishCoopGame(run);
        else ServerRemoveRun(run);
    }

    private void ServerFinishCoopGame(CoopRun run)
    {
        if (run.phase != CoopSlotPhase.Playing) return;
        var mission = run.mission;
        var logic = run.logic;
        float elapsed = logic.Elapsed;
        var successful = new List<ulong>();

        for (int r = 0; r < run.roleClients.Length; r++)
        {
            ulong clientId = run.roleClients[r];
            var result = logic.RoleResults[r];
            int delta = logic.Deltas[r];

            if (result != MinigameResult.Cancelled)
            {
                if (delta != 0) ServerApplyGradeDelta(clientId, delta, mission);
                if (logic.MarkCleared[r]) MarkCleared(clientId, mission.missionId);
                else if (result == MinigameResult.Fail)
                    retryAvailableAt[(clientId, mission.missionId)] = mission.allowRetryOnFail ? Now + mission.failRetryCooldown : double.MaxValue;
                if (writeCsvLog) MissionLogger.Append(CurrentDay, clientId, mission, result, result, elapsed, elapsed);
            }
            if (result == MinigameResult.Success) successful.Add(clientId);

            MissionStation.TryGet(run.roleStations[r], out var station);
            MissionEvents.RaiseMissionFinished(clientId, station, result, elapsed);
            SendCoopGameEnd(clientId, run.roleStations[r], result, delta);
        }

        Debug.Log($"[MissionManager] 공유 미니게임 종료: {mission.displayName}, {logic.Result}, 변화량 {string.Join(",", logic.Deltas)}");

        foreach (ulong c in run.roleClients) coopGameByClient.Remove(c);
        if (logic.Result == MinigameResult.Success)
        {
            ServerMarkRunDone(run);
            if (mission.type == MissionType.Coop) MissionEvents.RaiseCoopMissionCleared(CurrentDay, successful.ToArray());
            foreach (ulong c in successful) ServerCheckProgress(c);
        }
        else
        {
            ServerRemoveRun(run);
        }
    }

    // =====================================================================
    // 서버: 시간 경과 / 리셋
    // =====================================================================

    private void ServerUpdateCoop()
    {
        if (coopRuns.Count == 0) return;

        double now = CoopNow;
        var runs = new List<CoopRun>(coopRuns.Values);
        foreach (var run in runs)
        {
            switch (run.phase)
            {
                case CoopSlotPhase.Waiting:
                    if (now <= run.deadline) break;
                    if (run.mission.mode == CoopMode.SyncAction) ServerFailCoop(run, CoopNotice.Failed, 0f);
                    else
                    {
                        foreach (var kv in run.slots) SendCoopNotice(kv.Value, kv.Key, CoopNotice.Expired, 0f);
                        ServerReturnSumPanels(run);
                    }
                    ServerRemoveRun(run);
                    break;
                case CoopSlotPhase.Playing:
                    run.logic.Tick(Time.unscaledDeltaTime);
                    if (run.logic.Ended) ServerFinishCoopGame(run);
                    break;
                case CoopSlotPhase.Done:
                    if (now >= run.resetAt) ServerRemoveRun(run);
                    break;
            }
        }
    }

    // 일차 시작/종료: 진행 중인 협동 시도 전부 초기화 (공유 미니게임은 강제 종료)
    private void ServerResetCoop()
    {
        foreach (var run in coopRuns.Values)
            if (run.phase == CoopSlotPhase.Playing && run.roleClients != null)
                foreach (ulong c in run.roleClients) SendForceCancel(c, MinigameCancelReason.DayEnded);

        coopRuns.Clear();
        coopGameByClient.Clear();
        offlineCoopSlots.Clear();
        if (IsOnline && IsServer) netCoopSlots.Clear();
    }

    private void SetCoopSlot(CoopSlotState state)
    {
        if (IsOnline)
        {
            for (int i = 0; i < netCoopSlots.Count; i++)
                if (netCoopSlots[i].stationId == state.stationId) { netCoopSlots[i] = state; return; }
            netCoopSlots.Add(state);
        }
        else
        {
            int i = offlineCoopSlots.FindIndex(s => s.stationId == state.stationId);
            if (i >= 0) offlineCoopSlots[i] = state;
            else offlineCoopSlots.Add(state);
        }
    }

    private void RemoveCoopSlot(int stationId)
    {
        if (IsOnline)
        {
            for (int i = netCoopSlots.Count - 1; i >= 0; i--)
                if (netCoopSlots[i].stationId == stationId) netCoopSlots.RemoveAt(i);
        }
        else
        {
            offlineCoopSlots.RemoveAll(s => s.stationId == stationId);
        }
    }

    // =====================================================================
    // 서버: 공통 헬퍼
    // =====================================================================

    // 등급 (오프라인/플레이어 없음이면 -1)
    private int ServerGetGrade(ulong clientId)
    {
        var ps = GetPlayerState(clientId);
        return ps != null && ps.IsSpawned ? ps.cardGrade.Value : -1;
    }

    // 오늘 클리어한 개인 미션 수 (도박 확률 보정)
    private int ServerPersonalClearCount(ulong clientId)
    {
        if (!clearedByClient.TryGetValue(clientId, out var set)) return 0;
        int count = 0;
        foreach (var station in MissionStation.All.Values)
        {
            var m = station.Mission;
            if (m != null && m.type == MissionType.Personal && set.Contains(m.missionId)) count++;
        }
        // 같은 미션의 스테이션이 여러 개일 수 있으므로 상한
        return Mathf.Min(count, set.Count);
    }

    private void ServerApplyGradeDelta(ulong clientId, int delta, MissionData mission)
    {
        if (delta == 0) return;
        MissionEvents.RaiseGradeChangeRequested(clientId, delta, false, mission);
        if (!applyGradeToPlayerState) return;
        var ps = GetPlayerState(clientId);
        if (ps != null && ps.IsSpawned) ps.AddCardGrade(delta);
    }

    private static MissionItem GetHeldMissionItem(PlayerStateList ps)
    {
        if (ps == null || !ps.IsSpawned || !ps.isHoldingItem.Value) return null;
        var spawn = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager : null;
        if (spawn == null || !spawn.SpawnedObjects.TryGetValue(ps.heldItemNetworkId.Value, out var obj)) return null;
        return obj.GetComponent<MissionItem>();
    }

    private static void ConsumeHeldItem(PlayerStateList ps, MissionItem item)
    {
        if (ps != null && ps.IsSpawned)
        {
            ps.isHoldingItem.Value = false;
            ps.heldItemNetworkId.Value = 0;
        }
        if (item.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned) netObj.Despawn(true);
        else Destroy(item.gameObject);
    }
}
