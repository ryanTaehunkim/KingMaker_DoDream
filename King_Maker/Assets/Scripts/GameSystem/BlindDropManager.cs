using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum BlindDropStep
{
    None,
    Intro,      // 시작 대사
    Meeting,    // 회의 진행 (안내창)
    Choice,     // 참가 / 폴드 버튼
    SeedPoker,  // 시드 포커 (실제 게임은 추후 구현)
    Result      // 결과창 (안내창)
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

    [Header("단계별 시간(초) - 합계는 GameTimeManager의 드롭 시간(90초) 이하")]
    [SerializeField] private float introDuration = 5f;
    [SerializeField] private float meetingDuration = 45f;
    [SerializeField] private float choiceDuration = 15f;
    [SerializeField] private float seedPokerDuration = 10f;
    [SerializeField] private float resultDuration = 10f;

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

    // ---------- 서버 전용 ----------
    private readonly Dictionary<ulong, bool> choices = new Dictionary<ulong, bool>();
    private BlindDropStep serverStep = BlindDropStep.None;
    private Coroutine flowRoutine;

    // ---------- 클라이언트(로컬) 전용 ----------
    private int myChoice = -1;      // -1 미선택 / 0 폴드 / 1 참가
    private bool timerActive;
    private float timerEndTime;

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
        serverStep = BlindDropStep.None;

        EndFlowClientRpc();
    }

    // =========================================================
    // 서버: 진행 흐름
    // =========================================================

    private IEnumerator ServerFlow(int day)
    {
        choices.Clear();

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
            yield return RunSeedPokerServer(participants);
        }

        // 5) 결과창 (안내창)
        serverStep = BlindDropStep.Result;
        ShowResultClientRpc(BuildResultText(participants), resultDuration);
        yield return new WaitForSeconds(resultDuration);

        flowRoutine = null;
    }

    // 실제 시드 포커 게임은 여기에 구현하면 된다.
    // participants: 참가를 선택한 생존자의 clientId 목록
    private IEnumerator RunSeedPokerServer(List<ulong> participants)
    {
        // TODO: 시드 포커 로직 연결 (카드 배분, 승패 판정, PT 적용 등)
        yield return new WaitForSeconds(seedPokerDuration);
    }

    // 결과창에 표시할 문구. 시드 포커 구현 후 실제 결과로 교체하면 된다.
    private string BuildResultText(List<ulong> participants)
    {
        if (participants.Count == 0)
            return "전원 폴드했습니다.\n블라인드 드롭이 진행되지 않았습니다.";

        return "블라인드 드롭이 종료되었습니다.\n(시드 포커 결과는 추후 연결)";
    }

    private void SetStep(BlindDropStep step, float duration, int day)
    {
        serverStep = step;
        ShowStepClientRpc(step, duration, day);
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

        choices[sender] = participate;
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

        if (!IsLocalPlayerAlive())
            return;

        SetCursorLocked(true);
        StartLocalTimer(duration);
        ShowNotice("블라인드 드롭 결과", body);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EndFlowClientRpc()
    {
        HideAllUI();
        SetCursorLocked(true);

        // 화면 대사 + 저장된 대사 기록 삭제
        DialogueManager.Instance?.ClearAll();
    }

    // =========================================================
    // 클라이언트: 단계별 UI
    // =========================================================

    private void ApplyStepLocal(BlindDropStep step, float duration)
    {
        HideAllUI();

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

            case BlindDropStep.Choice:
                myChoice = -1;
                SetActiveSafe(choicePanel, true);

                if (participateButton != null) participateButton.interactable = true;
                if (foldButton != null) foldButton.interactable = true;
                if (choiceStatusText != null)
                    choiceStatusText.text = "블라인드 드롭에 참가할지 선택하세요.";
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