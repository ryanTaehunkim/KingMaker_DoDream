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
        Debug.Log("[MainMenuUI] Awake called");

        // -----------------------------------------------------
        // Connect button events
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
                "[MainMenuUI] Create Room Button is not assigned."
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
                "[MainMenuUI] Join Room Button is not assigned."
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
                "[MainMenuUI] Start Game Button is not assigned."
            );
        }


        // -----------------------------------------------------
        // Initial UI
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
    // Session Manager Connection
    // =========================================================

    private void SubscribeToSessionManager()
    {
        if (sessionManager == null)
        {
            Debug.LogError(
                "[MainMenuUI] Session Manager is not assigned."
            );

            return;
        }

        sessionManager.OnStatusChanged += HandleStatusChanged;
        sessionManager.OnJoinCodeCreated += HandleJoinCodeCreated;
        sessionManager.OnSessionConnected += HandleSessionConnected;
        sessionManager.OnError += HandleError;

        Debug.Log(
            "[MainMenuUI] Session Manager event subscription completed."
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
    // Create Room
    // =========================================================

    private async void OnCreateRoomButtonClicked()
    {
        Debug.Log(
            "[MainMenuUI] Create Room button clicked."
        );

        if (sessionManager == null)
        {
            Debug.LogError(
                "[MainMenuUI] Session Manager is missing."
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
    // Join Room
    // =========================================================

    private async void OnJoinRoomButtonClicked()
    {
        Debug.Log(
            "[MainMenuUI] Join Room button clicked."
        );

        if (sessionManager == null)
        {
            Debug.LogError(
                "[MainMenuUI] Session Manager is missing."
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
    // Start Game
    // =========================================================

    private void OnStartGameButtonClicked()
    {
        Debug.Log(
            "[MainMenuUI] ========================="
        );

        Debug.Log(
            "[MainMenuUI] Start Game button clicked."
        );


        if (sessionManager == null)
        {
            Debug.LogError(
                "[MainMenuUI] Session Manager is missing."
            );

            SetStatusText(
                "Session Manager not found."
            );

            return;
        }


        Debug.Log(
            $"[MainMenuUI] SessionManager check - " +
            $"IsHost: {sessionManager.IsHost}"
        );


        Debug.Log(
            "[MainMenuUI] Calling SessionManager.StartGame()."
        );


        sessionManager.StartGame();


        Debug.Log(
            "[MainMenuUI] SessionManager.StartGame() completed."
        );

        Debug.Log(
            "[MainMenuUI] ========================="
        );
    }


    // =========================================================
    // Session Events
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
    // UI State
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


        // Only the Host can start the game.
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
        // Create Room
        // -----------------------------------------------------

        if (createRoomButton != null)
        {
            createRoomButton.interactable =
                !isInSession;
        }


        // -----------------------------------------------------
        // Join Room
        // -----------------------------------------------------

        if (joinRoomButton != null)
        {
            joinRoomButton.interactable =
                !isInSession;
        }


        // -----------------------------------------------------
        // Join Code Input
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