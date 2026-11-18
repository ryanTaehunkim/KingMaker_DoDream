using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 게임 결과(승리 / 패배 / 종료) 화면. 네트워크와 무관하게 각 플레이어 PC에서 따로 표시한다.
//
// 흐름
//   GameCheckManager의 result / endReason 값이 바뀌면 → GameResultUI.Instance.Refresh() 호출
//   내 역할과 결과를 비교해서 "승리" 또는 "패배"를 보여준다.
//
// 주의: 이 스크립트는 "항상 켜져 있는" 오브젝트에 붙이고,
//       resultPanel은 그 자식(또는 다른 오브젝트)으로 연결하세요.
public class GameResultUI : MonoBehaviour
{
    public static GameResultUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text titleText;        // 승리 / 패배
    [SerializeField] private TMP_Text descriptionText;  // 시민 팀의 승리 / 조커 팀의 승리
    [SerializeField] private TMP_Text reasonText;       // 종료 사유
    [SerializeField] private Button leaveButton;

    [Header("제목 색")]
    [SerializeField] private Color winColor = new Color(0.45f, 0.85f, 0.55f);
    [SerializeField] private Color loseColor = new Color(0.95f, 0.25f, 0.25f);
    [SerializeField] private Color neutralColor = Color.white;

    [Header("나가기 버튼")]
    [Tooltip("비워두면 게임을 종료합니다. 씬 이름을 적으면 방을 나간 뒤 그 씬으로 이동합니다.")]
    [SerializeField] private string menuSceneName = "";

    private bool shown;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[GameResultUI] 중복된 GameResultUI가 있습니다.");
            return;
        }

        Instance = this;

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);

        HidePanel();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // 결과 화면이 떠 있는 동안에는 버튼을 누를 수 있게 커서를 계속 풀어 둔다.
        // (다른 스크립트가 커서를 다시 잠그는 경우 대비)
        if (!shown)
            return;

        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;

        if (!Cursor.visible)
            Cursor.visible = true;
    }

    // GameCheckManager의 값이 바뀔 때마다 호출된다. 결과가 없으면 화면을 숨긴다.
    public void Refresh()
    {
        GameCheckManager manager = GameCheckManager.Instance;

        if (manager == null || manager.result.Value == GameResult.None)
        {
            HidePanel();
            return;
        }

        ShowPanel(manager.result.Value, manager.endReason.Value);
    }

    private void ShowPanel(GameResult result, GameEndReason reason)
    {
        shown = true;

        if (resultPanel != null)
            resultPanel.SetActive(true);

        Role myRole = GetLocalRole();

        bool hasRole = myRole != Role.None;

        bool isWin =
            (result == GameResult.CitizenWin && myRole == Role.Citizen) ||
            (result == GameResult.JokerWin && myRole == Role.Joker);

        if (titleText != null)
        {
            if (!hasRole)
            {
                titleText.text = "게임 종료";
                titleText.color = neutralColor;
            }
            else
            {
                titleText.text = isWin ? "승리" : "패배";
                titleText.color = isWin ? winColor : loseColor;
            }
        }

        if (descriptionText != null)
        {
            descriptionText.text = result == GameResult.CitizenWin
                ? "시민 팀의 승리"
                : "조커 팀의 승리";
        }

        if (reasonText != null)
            reasonText.text = GetReasonText(reason);

        if (leaveButton != null)
            leaveButton.interactable = true;
    }

    private void HidePanel()
    {
        shown = false;

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    private string GetReasonText(GameEndReason reason)
    {
        switch (reason)
        {
            case GameEndReason.KingAchieved:
                return "K 등급을 달성한 플레이어가 나왔습니다.";

            case GameEndReason.CitizensEliminated:
                return "남은 시민이 부족해졌습니다.";

            case GameEndReason.TimeOver:
                return "제한 시간 안에 시민이 K 등급을 달성하지 못했습니다.";

            default:
                return "";
        }
    }

    // 내 역할 (본인 PlayerStateList의 role은 오너만 읽을 수 있으므로 내 값만 알 수 있다)
    private Role GetLocalRole()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
            return Role.None;

        PlayerStateList state = nm.LocalClient.PlayerObject.GetComponent<PlayerStateList>();

        return state != null ? state.role.Value : Role.None;
    }

    // =========================================================
    // 나가기 / 종료
    // =========================================================

    private async void OnLeaveClicked()
    {
        if (leaveButton != null)
            leaveButton.interactable = false;

        // 씬 이름이 비어 있으면 게임을 종료한다
        if (string.IsNullOrWhiteSpace(menuSceneName))
        {
            QuitGame();
            return;
        }

        // 방(세션)을 나가고 네트워크를 종료한 뒤 메뉴 씬으로 이동한다
        if (MultiplayerSessionManager.Instance != null)
        {
            await MultiplayerSessionManager.Instance.LeaveRoomAsync();
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(menuSceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}