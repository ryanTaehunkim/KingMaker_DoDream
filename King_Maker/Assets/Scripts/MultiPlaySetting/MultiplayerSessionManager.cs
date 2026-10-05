using System;
using System.Threading.Tasks;

using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

public class MultiplayerSessionManager : MonoBehaviour
{
    public static MultiplayerSessionManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private int maxPlayers = 8;

    [SerializeField] private string gameSceneName = "Game";

    // Current active Session
    public ISession CurrentSession { get; private set; }

    // Whether this client is the Host of the current Session
    public bool IsHost =>
        CurrentSession != null &&
        CurrentSession.IsHost;

    // Whether the player is currently in a Session
    public bool IsInSession =>
        CurrentSession != null;

    // Whether the player is actually connected to the NGO network
    public bool IsNetworkConnected =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsConnectedClient;

    // Current Session Join Code
    public string JoinCode =>
        CurrentSession != null
            ? CurrentSession.Code
            : string.Empty;

    // Events used by the UI
    public event Action<string> OnStatusChanged;
    public event Action<string> OnJoinCodeCreated;
    public event Action OnSessionConnected;
    public event Action<string> OnError;

    // Internal state
    private bool isInitializing;
    private bool isBusy;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        // Prevent duplicate Singleton instances
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keep this object when changing scenes
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializeServicesAsync();
    }

    // =========================================================
    // UGS Initialization
    // =========================================================

    public async Task InitializeServicesAsync()
    {
        // Prevent duplicate initialization
        if (isInitializing)
            return;

        isInitializing = true;

        try
        {
            SetStatus("Initializing online services...");

            // -------------------------------------------------
            // Unity Gaming Services Initialization
            // -------------------------------------------------

            if (UnityServices.State !=
                ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            // -------------------------------------------------
            // Anonymous Authentication
            // -------------------------------------------------

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SetStatus("Authenticating player...");

                await AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }

            SetStatus("Online services are ready.");

            Debug.Log(
                $"[Multiplayer] UGS authentication completed.\n" +
                $"Player ID: {AuthenticationService.Instance.PlayerId}"
            );
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            SetError(
                "Failed to initialize online services."
            );
        }
        finally
        {
            isInitializing = false;
        }
    }

    // =========================================================
    // Create Room
    // =========================================================

    public async Task CreateRoomAsync()
    {
        // Do not execute if another operation is in progress
        if (isBusy)
            return;

        // Do not create another room if already in a Session
        if (CurrentSession != null)
        {
            SetError("Already joined a room.");
            return;
        }

        isBusy = true;

        try
        {
            // Initialize UGS
            await InitializeServicesAsync();

            // Check authentication
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SetError("Player authentication failed.");
                return;
            }

            SetStatus("Creating room...");

            // -------------------------------------------------
            // Session Settings
            // -------------------------------------------------

            SessionOptions options =
                new SessionOptions
                {
                    Name = "KINGMAKER ROOM",

                    // Maximum number of players
                    MaxPlayers = maxPlayers,

                    // Use Join Code to allow players to join,
                    // so the room is currently private.
                    IsPrivate = true,

                    // Players can join until the room is locked.
                    IsLocked = false
                }
                .WithRelayNetwork();

            // -------------------------------------------------
            // Create Session
            // -------------------------------------------------

            CurrentSession =
                await MultiplayerService.Instance
                    .CreateSessionAsync(options);

            // -------------------------------------------------
            // Creation Result
            // -------------------------------------------------

            Debug.Log(
                $"[Multiplayer] Room created successfully.\n" +
                $"Session ID: {CurrentSession.Id}\n" +
                $"Join Code: {CurrentSession.Code}"
            );

            SetStatus(
                $"Room created.\n" +
                $"Code: {CurrentSession.Code}"
            );

            // Send Join Code to the UI
            OnJoinCodeCreated?.Invoke(
                CurrentSession.Code
            );

            // Notify that the Session connection is complete
            OnSessionConnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            CurrentSession = null;

            SetError(
                "Failed to create room: " +
                e.Message
            );
        }
        finally
        {
            isBusy = false;
        }
    }

    // =========================================================
    // Join Room
    // =========================================================

    public async Task JoinRoomAsync(string joinCode)
    {
        // Do not execute if another operation is in progress
        if (isBusy)
            return;

        // Do not join another room if already in a Session
        if (CurrentSession != null)
        {
            SetError("Already joined a room.");
            return;
        }

        // Clean up the input code
        string code =
            joinCode.Trim().ToUpperInvariant();

        // Check for an empty code
        if (string.IsNullOrEmpty(code))
        {
            SetError("Please enter a room code.");
            return;
        }

        isBusy = true;

        try
        {
            // Initialize UGS
            await InitializeServicesAsync();

            // Check authentication
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SetError("Player authentication failed.");
                return;
            }

            SetStatus("Joining room...");

            // -------------------------------------------------
            // Join Session using Join Code
            // -------------------------------------------------

            CurrentSession =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(code);

            // -------------------------------------------------
            // Join Result
            // -------------------------------------------------

            Debug.Log(
                $"[Multiplayer] Room joined successfully.\n" +
                $"Session ID: {CurrentSession.Id}\n" +
                $"Join Code: {CurrentSession.Code}"
            );

            SetStatus("Room joined successfully.");

            // Notify that the Session connection is complete
            OnSessionConnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            CurrentSession = null;

            SetError(
                "Failed to join room: " +
                e.Message
            );
        }
        finally
        {
            isBusy = false;
        }
    }

    // =========================================================
    // Start Game
    // =========================================================

    public void StartGame()
    {
        Debug.Log(
            $"[StartGame] " +
            $"Session={CurrentSession != null}, " +
            $"IsHost={CurrentSession?.IsHost}, " +
            $"IsServer={NetworkManager.Singleton?.IsServer}, " +
            $"IsClient={NetworkManager.Singleton?.IsClient}, " +
            $"IsListening={NetworkManager.Singleton?.IsListening}"
        );

        // Check Session
        if (CurrentSession == null)
        {
            SetError("No active room.");
            return;
        }

        if (!CurrentSession.IsHost)
        {
            SetError(
                "Only the Host can start the game."
            );
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            SetError(
                "NetworkManager could not be found."
            );
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            SetError(
                "Network Server is not running."
            );
            return;
        }

        Debug.Log(
            $"[Multiplayer] Starting game: {gameSceneName}"
        );

        NetworkManager.Singleton
            .SceneManager
            .LoadScene(
                gameSceneName,
                LoadSceneMode.Single
            );
    }

    // =========================================================
    // Leave Room
    // =========================================================

    public async Task LeaveRoomAsync()
    {
        // Exit if there is no active Session
        if (CurrentSession == null)
            return;

        try
        {
            SetStatus("Leaving room...");

            // Store the current Session
            ISession session =
                CurrentSession;

            // Clear the reference first
            CurrentSession = null;

            // Leave the Session
            await session.LeaveAsync();

            // Shutdown NGO if the network is still active
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SetStatus("Left the room.");
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            SetError(
                "Failed to leave room: " +
                e.Message
            );
        }
    }

    // =========================================================
    // Status Display
    // =========================================================

    private void SetStatus(string message)
    {
        Debug.Log(
            "[Multiplayer] " +
            message
        );

        OnStatusChanged?.Invoke(
            message
        );
    }

    // =========================================================
    // Error Display
    // =========================================================

    private void SetError(string message)
    {
        Debug.LogError(
            "[Multiplayer] " +
            message
        );

        OnError?.Invoke(
            message
        );
    }
}