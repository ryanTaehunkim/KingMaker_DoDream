using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuNetworkUI : MonoBehaviour
{
    [Header("Session")]
    [SerializeField] private MultiplayerSessionManager sessionManager;

    [Header("Buttons")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button startGameButton;

    [Header("Input")]
    [SerializeField] private TMP_InputField joinCodeInput;

    [Header("Texts")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text statusText;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        Debug.Log("[MainMenuUI] Awake 실행");

        // -----------------------------------------------------
        // 버튼 이벤트 연결
        // -----------------------------------------------------

        if (createRoomButton != null)
        {
            createRoomButton.onClick.RemoveListener(
                OnCreateRoomButtonClicked
            );

            createRoomButton.onClick.AddListener(
                OnCreateRoomButtonClicked
            );
        }
        else
        {
            Debug.LogError(
                "[MainMenuUI] Create Room Button이 연결되지 않았습니다."
            );
        }


        if (joinRoomButton != null)
        {
            joinRoomButton.onClick.RemoveListener(
                OnJoinRoomButtonClicked
            );

            joinRoomButton.onClick.AddListener(
                OnJoinRoomButtonClicked
            );
        }
        else
        {
            Debug.LogError(
                "[MainMenuUI] Join Room Button이 연결되지 않았습니다."
            );
        }


        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(
                OnStartGameButtonClicked
            );

            startGameButton.onClick.AddListener(
                OnStartGameButtonClicked
            );
        }
        else
        {
            Debug.LogError(
                "[MainMenuUI] Start Game Button이 연결되지 않았습니다."
            );
        }


        // -----------------------------------------------------
        // 초기 UI
        // -----------------------------------------------------

        SetRoomCodeText("");
        SetStatusText("Waiting...");

        if (startGameButton != null)
        {
            startGameButton.interactable = false;
        }
    }


    private void OnEnable()
    {
        SubscribeToSessionManager();
    }


    private void OnDisable()
    {
        UnsubscribeFromSessionManager();
    }


    // =========================================================
    // Session Manager 연결
    // =========================================================

    private void SubscribeToSessionManager()
    {
        if (sessionManager == null)
        {
            Debug.LogError(
                "[MainMenuUI] Session Manager가 연결되지 않았습니다."
            );

            return;
        }

        sessionManager.OnStatusChanged += HandleStatusChanged;
        sessionManager.OnJoinCodeCreated += HandleJoinCodeCreated;
        sessionManager.OnSessionConnected += HandleSessionConnected;
        sessionManager.OnError += HandleError;

        Debug.Log(
            "[MainMenuUI] Session Manager 이벤트 연결 완료"
        );
    }


    private void UnsubscribeFromSessionManager()
    {
        if (sessionManager == null)
            return;

        sessionManager.OnStatusChanged -= HandleStatusChanged;
        sessionManager.OnJoinCodeCreated -= HandleJoinCodeCreated;
        sessionManager.OnSessionConnected -= HandleSessionConnected;
        sessionManager.OnError -= HandleError;
    }


    // =========================================================
    // 방 만들기
    // =========================================================

    private async void OnCreateRoomButtonClicked()
    {
        Debug.Log(
            "[MainMenuUI] Create Room 버튼 클릭"
        );

        if (sessionManager == null)
        {
            Debug.LogError(
                "[MainMenuUI] Session Manager가 없습니다."
            );

            SetStatusText(
                "Session Manager not found."
            );

            return;
        }

        SetButtonsInteractable(false);

        await sessionManager.CreateRoomAsync();

        UpdateButtonsAfterOperation();
    }


    // =========================================================
    // 방 참가
    // =========================================================

    private async void OnJoinRoomButtonClicked()
    {
        Debug.Log(
            "[MainMenuUI] Join Room 버튼 클릭"
        );

        if (sessionManager == null)
        {
            Debug.LogError(
                "[MainMenuUI] Session Manager가 없습니다."
            );

            SetStatusText(
                "Session Manager not found."
            );

            return;
        }


        string joinCode = "";

        if (joinCodeInput != null)
        {
            joinCode = joinCodeInput.text;
        }


        if (string.IsNullOrWhiteSpace(joinCode))
        {
            SetStatusText(
                "Please enter a room code."
            );

            return;
        }


        SetButtonsInteractable(false);

        await sessionManager.JoinRoomAsync(
            joinCode
        );

        UpdateButtonsAfterOperation();
    }


    // =========================================================
    // 게임 시작
    // =========================================================

    private void OnStartGameButtonClicked()
    {
        Debug.Log(
            "[MainMenuUI] ========================="
        );

        Debug.Log(
            "[MainMenuUI] Start Game 버튼 클릭됨"
        );


        if (sessionManager == null)
        {
            Debug.LogError(
                "[MainMenuUI] Session Manager가 없습니다."
            );

            SetStatusText(
                "Session Manager not found."
            );

            return;
        }


        Debug.Log(
            $"[MainMenuUI] SessionManager 확인 - " +
            $"IsHost: {sessionManager.IsHost}"
        );


        Debug.Log(
            "[MainMenuUI] SessionManager.StartGame() 호출"
        );


        sessionManager.StartGame();


        Debug.Log(
            "[MainMenuUI] SessionManager.StartGame() 호출 완료"
        );

        Debug.Log(
            "[MainMenuUI] ========================="
        );
    }


    // =========================================================
    // Session 이벤트
    // =========================================================

    private void HandleStatusChanged(
        string message
    )
    {
        Debug.Log(
            $"[MainMenuUI] Status: {message}"
        );

        SetStatusText(message);
    }


    private void HandleJoinCodeCreated(
        string joinCode
    )
    {
        Debug.Log(
            $"[MainMenuUI] Join Code: {joinCode}"
        );

        if (roomCodeText == null)
            return;

        roomCodeText.text =
            $"Room Code: {joinCode}";
    }


    private void HandleSessionConnected()
    {
        Debug.Log(
            "[MainMenuUI] Session Connected"
        );

        UpdateButtonsAfterOperation();
    }


    private void HandleError(
        string message
    )
    {
        Debug.LogError(
            $"[MainMenuUI] Error: {message}"
        );

        SetStatusText(message);

        UpdateButtonsAfterOperation();
    }


    // =========================================================
    // UI 상태
    // =========================================================

    private void UpdateStartGameButton()
    {
        if (startGameButton == null)
            return;


        if (sessionManager == null)
        {
            startGameButton.interactable = false;
            return;
        }


        // Host만 게임 시작 가능
        bool canStart =
            sessionManager.IsHost;


        startGameButton.interactable =
            canStart;


        Debug.Log(
            $"[MainMenuUI] Start Game Button = {canStart}"
        );
    }


    private void UpdateButtonsAfterOperation()
    {
        if (sessionManager == null)
            return;


        bool isInSession =
            sessionManager.IsInSession;


        // -----------------------------------------------------
        // 방 생성
        // -----------------------------------------------------

        if (createRoomButton != null)
        {
            createRoomButton.interactable =
                !isInSession;
        }


        // -----------------------------------------------------
        // 방 참가
        // -----------------------------------------------------

        if (joinRoomButton != null)
        {
            joinRoomButton.interactable =
                !isInSession;
        }


        // -----------------------------------------------------
        // Join Code 입력
        // -----------------------------------------------------

        if (joinCodeInput != null)
        {
            joinCodeInput.interactable =
                !isInSession;
        }


        // -----------------------------------------------------
        // Start Game
        // -----------------------------------------------------

        UpdateStartGameButton();
    }


    private void SetButtonsInteractable(
        bool interactable
    )
    {
        if (createRoomButton != null)
        {
            createRoomButton.interactable =
                interactable;
        }


        if (joinRoomButton != null)
        {
            joinRoomButton.interactable =
                interactable;
        }


        if (joinCodeInput != null)
        {
            joinCodeInput.interactable =
                interactable;
        }


        if (startGameButton != null)
        {
            startGameButton.interactable = false;
        }
    }


    // =========================================================
    // Text
    // =========================================================

    private void SetRoomCodeText(
        string message
    )
    {
        if (roomCodeText == null)
            return;


        if (string.IsNullOrEmpty(message))
        {
            roomCodeText.text =
                "Room Code:";
        }
        else
        {
            roomCodeText.text =
                message;
        }
    }


    private void SetStatusText(
        string message
    )
    {
        if (statusText == null)
            return;


        statusText.text =
            $"Status: {message}";
    }
}