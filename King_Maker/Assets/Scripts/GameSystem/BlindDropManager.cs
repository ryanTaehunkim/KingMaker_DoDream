using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum BlindDropStep
{
    None,
    Intro,          // 시작 대사
    Meeting,        // 회의 진행 (안내창)
    Choice,         // 참가 / 폴드 버튼
    SeedPoker,      // 시드 포커 (실제 게임은 추후 구현)
    Result,         // 결과창 (안내창)
    Investigation   // 정체 조사권 (최상위 등급자가 한 명을 지목)
}

// 블라인드 드롭 한 번의 진행 흐름을 관리한다.
// 서버가 단계를 진행시키고, 각 클라이언트는 RPC로 받은 단계에 맞는 UI를 보여준다.
//
// GameFlowManager 연결
//   드롭 시작 후:  BlindDropManager.Instance?.ServerBeginBlindDrop(day);
//   드롭 종료 시:  BlindDropManager.Instance?.ServerEndBlindDrop();
public class BlindDropManager : NetworkBehaviour
{
    public static BlindDropManager Instance { get; private set; }

    [Header("단계별 시간(초) - 합계는 GameTimeManager의 드롭 시간(90초)보다 조금 작게")]
    [SerializeField] private float introDuration = 5f;
    [SerializeField] private float meetingDuration = 38f;
    [SerializeField] private float choiceDuration = 10f;
    [SerializeField] private float seedPokerDuration = 10f;
    [SerializeField] private float resultDuration = 7f;
    [SerializeField] private float investigationDuration = 15f;

    [Header("정체 조사권")]
    [SerializeField] private float investigationRange = 20f;     // 지목 가능 거리
    [SerializeField] private float investigationResultHold = 3f; // 결과를 보여주고 기다리는 시간

    [Header("시작 대사")]
    [SerializeField]
    private string[] introLines =
    {
        "일차 필드 탐색이 끝났습니다.",
        "모든 생존자는 회의실로 모입니다.",
        "블라인드 드롭이 곧 시작됩니다."
    };

    [Header("UI - 안내창 (회의 / 결과 공용)")]
    [SerializeField] private GameObject noticePanel;
    [SerializeField] private TMP_Text noticeTitleText;
    [SerializeField] private TMP_Text noticeBodyText;

    [Header("UI - 남은 시간 (선택)")]
    [SerializeField] private TMP_Text timerText;

    [Header("UI - 참가 여부")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button participateButton;
    [SerializeField] private Button foldButton;
    [SerializeField] private TMP_Text choiceStatusText;

    [Header("UI - 시드 포커")]
    [SerializeField] private GameObject seedPokerPanel;
    [SerializeField] private TMP_Text seedPokerText;

    [Header("UI - 정체 조사권 (화면 상단에 작게 배치)")]
    [SerializeField] private GameObject investigationPanel;
    [SerializeField] private TMP_Text investigationText;

    [Header("UI - 조준선 (블라인드 드롭 중에는 숨기고, 정체 조사 때만 표시)")]
    [SerializeField] private GameObject crosshairRoot;
    [SerializeField] private Color crosshairTargetColor = new Color(1f, 0.25f, 0.25f);

    // ---------- 서버 전용 ----------
    private readonly Dictionary<ulong, bool> choices = new Dictionary<ulong, bool>();
    private readonly HashSet<ulong> investigators = new HashSet<ulong>();   // 조사권을 받은 사람
    private readonly HashSet<ulong> investigated = new HashSet<ulong>();    // 이미 사용한 사람
    private BlindDropStep serverStep = BlindDropStep.None;
    private Coroutine flowRoutine;

    // ---------- 클라이언트(로컬) 전용 ----------
    private int myChoice = -1;      // -1 미선택 / 0 폴드 / 1 참가
    private bool timerActive;
    private float timerEndTime;
    private bool canInvestigate;    // 내가 지금 조사권을 쓸 수 있는지
    //



    [System.Serializable]
    public class SeedPokerDayConfig
    {
        public int day = 1;
        public bool enabled = true;
        public int minCard = 1;
        public int maxCard = 5;
    }

    [Header("시드 포커")]
    [SerializeField] private SeedPokerDayConfig[] seedPokerConfigs;
    [SerializeField] private int seedCardCount = 3;
    [SerializeField] private int penaltyPerDay = 2;
    [SerializeField] private int maxGradeFromSeedPoker = 16; // 이 값 이상이면 참가 불가

    private readonly Dictionary<ulong, SeedPokerPlayerResult> seedResults = new();
    private readonly List<ulong> pendingDeaths = new();
    private SeedPokerDayConfig GetConfig(int day)
    {
        if (seedPokerConfigs == null) return null;
        foreach (var c in seedPokerConfigs)
            if (c.day == day) return c;
        return null;
    }

    // GameFlowManager에서도 사용. 설정이 없는 일차는 꺼진 것으로 취급
    public bool IsSeedPokerEnabled(int day)
    {
        var c = GetConfig(day);
        return c != null && c.enabled;
    }

    private List<int> DrawSeedCards(SeedPokerDayConfig cfg)
    {
        var pool = new List<int>();
        for (int v = cfg.minCard; v <= cfg.maxCard; v++) pool.Add(v);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.GetRange(0, Mathf.Min(seedCardCount, pool.Count));
    }

    private Graphic[] crosshairGraphics;
    private Color[] crosshairBaseColors;
    private readonly RaycastHit[] aimHits = new RaycastHit[16];


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[BlindDrop] 중복된 BlindDropManager가 있습니다.");
            return;
        }

        Instance = this;

        if (participateButton != null)
            participateButton.onClick.AddListener(() => SendChoice(true));

        if (foldButton != null)
            foldButton.onClick.AddListener(() => SendChoice(false));

        CacheCrosshair();

        HideAllUI();
    }

    public override void OnNetworkDespawn()
    {
        if (flowRoutine != null)
        {
            StopCoroutine(flowRoutine);
            flowRoutine = null;
        }

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        UpdateTimerText();
        HandleInvestigationInput();
    }

    private void UpdateTimerText()
    {
        if (!timerActive || timerText == null)
            return;

        float remain = Mathf.Max(0f, timerEndTime - Time.time);
        timerText.text = Mathf.CeilToInt(remain).ToString();
    }

    // =========================================================
    // 서버: 시작 / 종료
    // =========================================================

    public void ServerBeginBlindDrop(int day)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (flowRoutine != null)
            StopCoroutine(flowRoutine);

        flowRoutine = StartCoroutine(ServerFlow(day));
    }

    public void ServerEndBlindDrop()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (flowRoutine != null)
        {
            StopCoroutine(flowRoutine);
            flowRoutine = null;
        }

        choices.Clear();
        investigators.Clear();
        investigated.Clear();
        seedResults.Clear();  
        pendingDeaths.Clear();
        serverStep = BlindDropStep.None;

        EndFlowClientRpc();
    }

    // =========================================================
    // 서버: 진행 흐름
    // =========================================================

    private IEnumerator ServerFlow(int day)
    {
        choices.Clear();
        investigators.Clear();
        investigated.Clear();
        seedResults.Clear();
        pendingDeaths.Clear();

        // 1) 시작 대사
        SetStep(BlindDropStep.Intro, introDuration, day);
        yield return new WaitForSeconds(introDuration);

        // 2) 회의 진행 (안내창)
        SetStep(BlindDropStep.Meeting, meetingDuration, day);
        yield return new WaitForSeconds(meetingDuration);

        // 3) 참가 여부 (모두 선택하면 시간이 남아도 바로 넘어감)
        choices.Clear();
        SetStep(BlindDropStep.Choice, choiceDuration, day);

        float choiceEnd = Time.time + choiceDuration;
        while (Time.time < choiceEnd && !AllAliveChose())
            yield return null;

        // 선택하지 않은 사람은 폴드 처리
        List<ulong> participants = new List<ulong>();
        foreach (ulong id in GetAliveClientIds())
        {
            if (choices.TryGetValue(id, out bool join) && join)
                participants.Add(id);
        }

        Debug.Log($"[BlindDrop] 참가 {participants.Count}명 / 생존 {GetAliveClientIds().Count}명");

        // 4) 시드 포커 (참가자가 있을 때만)
        if (participants.Count > 0)
        {
            SetStep(BlindDropStep.SeedPoker, seedPokerDuration, day);
            yield return RunSeedPokerServer(participants, day);
        }

        // 5) 결과창 (안내창)
        serverStep = BlindDropStep.Result;
        foreach (ulong id in GetAliveClientIds())
        {
            ShowResultRpc(
                BuildPersonalResultText(id, participants.Count),
                resultDuration,
                RpcTarget.Single(id, RpcTargetUse.Temp));
        }
        yield return new WaitForSeconds(resultDuration);

        // 사망 처리 (결과창이 끝난 뒤)
        foreach (ulong id in pendingDeaths)
        {
            var ps = GetPlayerState(id);
            if (ps != null && ps.isAlive.Value) ps.isAlive.Value = false;
        }
        pendingDeaths.Clear();

        // 6) 정체 조사권 (드롭 직후, 최상위 등급자)
        yield return RunInvestigationServer(day);

        flowRoutine = null;
    }

    private string BuildPersonalResultText(ulong id, int participantCount)
    {
        if (participantCount == 0)
            return "전원 폴드했습니다.\n블라인드 드롭이 진행되지 않았습니다.";

        if (!seedResults.TryGetValue(id, out var r))
            return "이번 블라인드 드롭에 참가하지 않았습니다.";

        switch (r.outcome)
        {
            case SeedPokerOutcome.Won:
                return $"당신은 이번 결과로 시드카드 {r.cardValue}를 획득하였습니다.";
            case SeedPokerOutcome.Void:
                return "당신은 이번 결과로 시드카드 획득에 실패하였습니다.";
            default:
                return $"당신은 이번 결과로 시드카드 획득에 실패하였습니다.\n추가로 {-r.gradeDelta}등급이 하락하였습니다.";
        }
    }
    [Rpc(SendTo.SpecifiedInParams)]
    private void ShowResultRpc(string body, float duration, RpcParams rpcParams = default)
    {
        HideAllUI();
        if (!IsLocalPlayerAlive()) return;

        SetCursorLocked(true);
        StartLocalTimer(duration);
        ShowNotice("블라인드 드롭 결과", body);
    }
    // 실제 시드 포커 게임은 여기에 구현하면 된다.
    // participants: 참가를 선택한 생존자의 clientId 목록
    private IEnumerator RunSeedPokerServer(List<ulong> participants, int day)
    {
        // TODO: 시드 포커 로직 연결 (카드 배분, 승패 판정, PT 적용 등)
        seedResults.Clear();
        pendingDeaths.Clear();

        var cfg = GetConfig(day);
        if (cfg == null || !cfg.enabled)
        {
            yield return new WaitForSeconds(seedPokerDuration);
            yield break;
        }
        var cards = DrawSeedCards(cfg);

        var list = new List<SeedPokerParticipant>();
        foreach (ulong id in participants)
        {
            var ps = GetPlayerState(id);
            if (ps != null && ps.isAlive.Value)
                list.Add(new SeedPokerParticipant(id, ps.cardGrade.Value));
        }

        var results = SeedPokerResolver.Resolve(list, cards, day, penaltyPerDay, maxGradeFromSeedPoker);

        yield return new WaitForSeconds(seedPokerDuration);

        // 전부 계산한 뒤 한 번에 적용
        foreach (var r in results)
        {
            seedResults[r.clientId] = r;
            var ps = GetPlayerState(r.clientId);
            if (ps == null) continue;

            if (r.gradeDelta != 0) ps.ServerSetCardGradeRaw(r.newGrade);
            if (r.newGrade < 1) pendingDeaths.Add(r.clientId);
        }
    }

    // 결과창에 표시할 문구. 시드 포커 구현 후 실제 결과로 교체하면 된다.
    private string BuildResultText(List<ulong> participants)
    {
        if (participants.Count == 0)
            return "전원 폴드했습니다.\n블라인드 드롭이 진행되지 않았습니다.";

        return "블라인드 드롭이 종료되었습니다.\n(시드 포커 결과는 추후 연결)";
    }

    // 정체 조사권: 생존자 중 등급이 가장 높은 사람(동점이면 전원)이 한 명을 지목한다.
    private IEnumerator RunInvestigationServer(int day)
    {
        investigators.Clear();
        investigated.Clear();

        foreach (ulong id in GetTopGradeAliveClientIds())
            investigators.Add(id);

        if (investigators.Count == 0)
            yield break;

        Debug.Log($"[BlindDrop] 정체 조사권 대상 {investigators.Count}명");

        // 모두에게 단계 알림 → 그 다음 자격자에게만 조사권 지급
        SetStep(BlindDropStep.Investigation, investigationDuration, day);

        foreach (ulong id in investigators)
            GrantInvestigationRpc(RpcTarget.Single(id, RpcTargetUse.Temp));

        // 제한 시간 또는 전원 사용 완료까지 대기
        float end = Time.time + investigationDuration;
        while (Time.time < end && investigated.Count < investigators.Count)
            yield return null;

        // 결과를 읽을 시간을 조금 준다
        if (investigated.Count > 0)
            yield return new WaitForSeconds(investigationResultHold);
    }

    private void SetStep(BlindDropStep step, float duration, int day)
    {
        serverStep = step;
        ShowStepClientRpc(step, duration, day);
    }
    private PlayerStateList GetPlayerState(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
            && client.PlayerObject != null)
        {
            return client.PlayerObject.GetComponent<PlayerStateList>();
        }
        return null;
    }
    // =========================================================
    // 서버: 생존자 / 선택 확인
    // =========================================================

    private List<ulong> GetAliveClientIds()
    {
        List<ulong> result = new List<ulong>();

        if (NetworkManager.Singleton == null)
            return result;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerStateList state = client.PlayerObject.GetComponent<PlayerStateList>();

            if (state != null && state.isAlive.Value)
                result.Add(client.ClientId);
        }

        return result;
    }

    // 생존자 중 카드 등급이 가장 높은 사람들 (동점이면 모두)
    private List<ulong> GetTopGradeAliveClientIds()
    {
        List<ulong> result = new List<ulong>();
        int topGrade = int.MinValue;

        if (NetworkManager.Singleton == null)
            return result;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerStateList state = client.PlayerObject.GetComponent<PlayerStateList>();

            if (state == null || !state.isAlive.Value)
                continue;

            int grade = state.cardGrade.Value;

            if (grade > topGrade)
            {
                topGrade = grade;
                result.Clear();
                result.Add(client.ClientId);
            }
            else if (grade == topGrade)
            {
                result.Add(client.ClientId);
            }
        }

        return result;
    }

    private bool AllAliveChose()
    {
        foreach (ulong id in GetAliveClientIds())
        {
            if (!choices.ContainsKey(id))
                return false;
        }

        return true;
    }

    // =========================================================
    // RPC
    // =========================================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitChoiceServerRpc(bool participate, RpcParams rpcParams = default)
    {
        // 참가 여부 선택 단계에서만 받는다
        if (serverStep != BlindDropStep.Choice)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;

        if (!GetAliveClientIds().Contains(sender))
            return;

        // 한 번 선택하면 변경 불가
        if (choices.ContainsKey(sender))
            return;

        bool join = participate;
        if (join)
        {
            var ps = GetPlayerState(sender);
            if (ps == null || ps.cardGrade.Value >= maxGradeFromSeedPoker)
                join = false;
        }
        choices[sender] = join;
    }

    // 정체 조사 요청 (조사권을 가진 플레이어가 대상을 지목)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestInvestigateServerRpc(ulong targetObjectId, RpcParams rpcParams = default)
    {
        // 정체 조사 단계에서만 받는다
        if (serverStep != BlindDropStep.Investigation)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;

        // 조사권이 없거나 이미 사용했으면 거부
        if (!investigators.Contains(sender)) return;
        if (investigated.Contains(sender)) return;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetObjectId, out NetworkObject targetObj))
            return;

        if (!targetObj.TryGetComponent(out PlayerStateList target))
            return;

        // 자기 자신은 지목할 수 없고, 사망자도 지목할 수 없다
        if (targetObj.OwnerClientId == sender) return;
        if (!target.isAlive.Value) return;

        investigated.Add(sender);

        // 조사한 본인에게만 결과 전송
        SendInvestigationResultRpc(
            target.role.Value,
            RpcTarget.Single(sender, RpcTargetUse.Temp)
        );

        Debug.Log($"[BlindDrop] Client {sender}가 정체 조사 사용");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowStepClientRpc(BlindDropStep step, float duration, int day)
    {
        ApplyStepLocal(step, duration);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowResultClientRpc(string body, float duration)
    {
        HideAllUI();
        SetCrosshairVisible(false);

        if (!IsLocalPlayerAlive())
            return;

        SetCursorLocked(true);
        StartLocalTimer(duration);
        ShowNotice("블라인드 드롭 결과", body);
    }

    // 조사권 지급 (자격자 본인에게만 전송됨)
    [Rpc(SendTo.SpecifiedInParams)]
    private void GrantInvestigationRpc(RpcParams rpcParams = default)
    {
        canInvestigate = true;
        SetCrosshairVisible(true);

        if (investigationText != null)
        {
            investigationText.text =
                "정체 조사권을 얻었습니다.\n조사할 플레이어를 바라보고 좌클릭하세요.";
        }
    }

    // 조사 결과 (조사한 본인에게만 전송됨)
    [Rpc(SendTo.SpecifiedInParams)]
    private void SendInvestigationResultRpc(Role role, RpcParams rpcParams = default)
    {
        canInvestigate = false;
        SetCrosshairVisible(false);

        string roleName = role == Role.Joker ? "조커" : "시민";

        if (investigationText != null)
            investigationText.text = $"조사 결과: 지목한 플레이어는 [{roleName}] 입니다.";
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EndFlowClientRpc()
    {
        HideAllUI();
        SetCursorLocked(true);

        // 필드로 돌아가므로 조준선을 원래대로 표시
        SetCrosshairVisible(true);

        // 화면 대사 + 저장된 대사 기록 삭제
        DialogueManager.Instance?.ClearAll();
    }

    // =========================================================
    // 클라이언트: 정체 조사 입력 (좌클릭으로 지목)
    // =========================================================

    private void HandleInvestigationInput()
    {
        if (!canInvestigate)
            return;

        // 조사 시간이 끝났으면 조준선을 숨기고 종료
        if (timerActive && Time.time > timerEndTime)
        {
            canInvestigate = false;
            SetCrosshairVisible(false);

            if (investigationText != null)
                investigationText.text = "조사 시간이 끝났습니다.";

            return;
        }

        PlayerStateList target = FindAimedPlayer();
        bool valid = target != null && target.isAlive.Value;

        // 지목 가능한 대상 위에 조준선이 있으면 색을 바꿔서 알려준다
        SetCrosshairColor(valid);

        if (valid && InputManager.attackPressed)
            RequestInvestigateServerRpc(target.NetworkObjectId);
    }

    // =========================================================
    // 조준선
    // =========================================================

    // 조준선 오브젝트(자식 포함)의 원래 색을 저장해 둔다
    private void CacheCrosshair()
    {
        if (crosshairRoot == null)
            return;

        crosshairGraphics = crosshairRoot.GetComponentsInChildren<Graphic>(true);
        crosshairBaseColors = new Color[crosshairGraphics.Length];

        for (int i = 0; i < crosshairGraphics.Length; i++)
            crosshairBaseColors[i] = crosshairGraphics[i].color;
    }

    private void SetCrosshairVisible(bool visible)
    {
        if (crosshairRoot == null)
            return;

        crosshairRoot.SetActive(visible);

        // 다시 보일 때는 원래 색으로
        if (visible)
            SetCrosshairColor(false);
    }

    // onTarget이 true면 대상 색, false면 원래 색
    private void SetCrosshairColor(bool onTarget)
    {
        if (crosshairGraphics == null)
            return;

        for (int i = 0; i < crosshairGraphics.Length; i++)
        {
            if (crosshairGraphics[i] == null)
                continue;

            Color baseColor = crosshairBaseColors[i];

            crosshairGraphics[i].color = onTarget
                ? new Color(
                    crosshairTargetColor.r,
                    crosshairTargetColor.g,
                    crosshairTargetColor.b,
                    baseColor.a)
                : baseColor;
        }
    }

    // 조준선이 가리키는 가장 가까운 플레이어 (내 몸은 제외, 벽에 막히면 null)
    private PlayerStateList FindAimedPlayer()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return null;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        int count = Physics.RaycastNonAlloc(
            ray,
            aimHits,
            investigationRange,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float best = float.MaxValue;
        PlayerStateList bestTarget = null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = aimHits[i];
            PlayerStateList target = hit.collider.GetComponentInParent<PlayerStateList>();

            // 내 몸에 맞은 것은 무시
            if (target != null && target.IsOwner)
                continue;

            if (hit.distance >= best)
                continue;

            // 가장 가까운 물체가 플레이어가 아니면(벽 등) null이 들어가 지목 불가 처리
            best = hit.distance;
            bestTarget = target;
        }

        return bestTarget;
    }

    // =========================================================
    // 클라이언트: 단계별 UI
    // =========================================================

    private void ApplyStepLocal(BlindDropStep step, float duration)
    {
        HideAllUI();

        // 블라인드 드롭 중에는 조준선을 숨긴다. (정체 조사권을 받은 사람만 GrantInvestigationRpc에서 다시 표시)
        SetCrosshairVisible(false);

        // 사망자는 드롭에 참여하지 않으므로 UI를 보여주지 않는다
        if (!IsLocalPlayerAlive())
        {
            SetCursorLocked(true);
            return;
        }

        SetCursorLocked(step != BlindDropStep.Choice);

        if (step != BlindDropStep.Intro)
            StartLocalTimer(duration);

        switch (step)
        {
            case BlindDropStep.Intro:
                if (DialogueManager.Instance != null && introLines.Length > 0)
                {
                    float perLine = duration / introLines.Length;
                    DialogueManager.Instance.ShowLines(introLines, perLine);
                }
                else
                {
                    Debug.LogWarning("[BlindDrop] DialogueManager 또는 introLines가 없습니다.");
                }
                break;

            case BlindDropStep.Meeting:
                DialogueManager.Instance?.ClearLine();
                ShowNotice(
                    "회의 진행",
                    "생존자 모두 회의실에 모였습니다.\n자유롭게 토론하며 조커를 추리하세요."
                );
                break;

            //case BlindDropStep.Choice:
            //    myChoice = -1;
            //    SetActiveSafe(choicePanel, true);

            //    if (participateButton != null) participateButton.interactable = true;
            //    if (foldButton != null) foldButton.interactable = true;
            //    if (choiceStatusText != null)
            //        choiceStatusText.text = "블라인드 드롭에 참가할지 선택하세요.";
            //    break;
            case BlindDropStep.Choice:
                myChoice = -1;
                SetActiveSafe(choicePanel, true);

                bool blocked = false;
                var localObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
                if (localObj != null && localObj.TryGetComponent(out PlayerStateList me))
                    blocked = me.cardGrade.Value >= maxGradeFromSeedPoker;

                if (participateButton != null) participateButton.interactable = !blocked;
                if (foldButton != null) foldButton.interactable = true;
                if (choiceStatusText != null)
                    choiceStatusText.text = blocked
                        ? "등급이 너무 높아 참가할 수 없습니다.\n폴드만 선택할 수 있습니다."
                        : "블라인드 드롭에 참가할지 선택하세요.";
                break;
            case BlindDropStep.SeedPoker:
                SetActiveSafe(seedPokerPanel, true);

                if (seedPokerText != null)
                {
                    seedPokerText.text = myChoice == 1
                        ? "시드 포커 진행 중..."
                        : "폴드했습니다.\n시드 포커가 끝날 때까지 기다려주세요.";
                }
                break;
            

            case BlindDropStep.Investigation:
                // 기본 문구. 조사권이 있는 사람은 GrantInvestigationRpc가 문구를 덮어쓴다.
                SetActiveSafe(investigationPanel, true);

                if (investigationText != null)
                    investigationText.text = "최상위 플레이어가 정체를 조사하고 있습니다.";
                break;
        }
    }

    private void SendChoice(bool participate)
    {
        if (myChoice != -1)
            return;

        myChoice = participate ? 1 : 0;

        if (participateButton != null) participateButton.interactable = false;
        if (foldButton != null) foldButton.interactable = false;

        if (choiceStatusText != null)
        {
            choiceStatusText.text = participate
                ? "참가를 선택했습니다.\n다른 플레이어를 기다리는 중..."
                : "폴드를 선택했습니다.\n다른 플레이어를 기다리는 중...";
        }

        SubmitChoiceServerRpc(participate);
    }

    private void ShowNotice(string title, string body)
    {
        SetActiveSafe(noticePanel, true);

        if (noticeTitleText != null) noticeTitleText.text = title;
        if (noticeBodyText != null) noticeBodyText.text = body;
    }

    private void StartLocalTimer(float duration)
    {
        timerEndTime = Time.time + duration;
        timerActive = true;

        if (timerText != null)
            timerText.gameObject.SetActive(true);
    }

    private void HideAllUI()
    {
        SetActiveSafe(noticePanel, false);
        SetActiveSafe(choicePanel, false);
        SetActiveSafe(seedPokerPanel, false);
        SetActiveSafe(investigationPanel, false);

        canInvestigate = false;
        timerActive = false;

        if (timerText != null)
            timerText.gameObject.SetActive(false);
    }

    private void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    // 버튼을 클릭하려면 마우스 커서가 보여야 한다.
    // (PlayerControlRigid는 커서가 잠겨있을 때만 시점이 돌아간다)
    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private bool IsLocalPlayerAlive()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
            return false;

        PlayerStateList state = nm.LocalClient.PlayerObject.GetComponent<PlayerStateList>();

        return state != null && state.isAlive.Value;
    }
}