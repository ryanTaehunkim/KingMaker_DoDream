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

    // 현재 참가 중인 Session
    public ISession CurrentSession { get; private set; }

    // 현재 Session의 Host인지
    public bool IsHost =>
        CurrentSession != null &&
        CurrentSession.IsHost;

    // Session에 참가한 상태인지
    public bool IsInSession =>
        CurrentSession != null;

    // 실제 NGO 네트워크에 연결되어 있는지
    public bool IsNetworkConnected =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsConnectedClient;

    // 현재 Session의 Join Code
    public string JoinCode =>
        CurrentSession != null
            ? CurrentSession.Code
            : string.Empty;

    // UI에서 사용할 이벤트
    public event Action<string> OnStatusChanged;
    public event Action<string> OnJoinCodeCreated;
    public event Action OnSessionConnected;
    public event Action<string> OnError;

    // 내부 상태
    private bool isInitializing;
    private bool isBusy;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        // Singleton 중복 방지
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Scene이 바뀌어도 유지
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializeServicesAsync();
    }

    // =========================================================
    // UGS 초기화
    // =========================================================

    public async Task InitializeServicesAsync()
    {
        // 이미 초기화 중이면 중복 실행하지 않음
        if (isInitializing)
            return;

        isInitializing = true;

        try
        {
            SetStatus("온라인 서비스 초기화 중...");

            // -------------------------------------------------
            // Unity Gaming Services 초기화
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
                SetStatus("플레이어 인증 중...");

                await AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }

            SetStatus("온라인 서비스 준비 완료");

            Debug.Log(
                $"[Multiplayer] UGS 인증 완료\n" +
                $"Player ID: {AuthenticationService.Instance.PlayerId}"
            );
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            SetError(
                "온라인 서비스 초기화에 실패했습니다."
            );
        }
        finally
        {
            isInitializing = false;
        }
    }

    // =========================================================
    // 방 만들기
    // =========================================================

    public async Task CreateRoomAsync()
    {
        // 다른 작업 중이면 실행하지 않음
        if (isBusy)
            return;

        // 이미 방에 들어가 있다면 생성하지 않음
        if (CurrentSession != null)
        {
            SetError("이미 방에 참가한 상태입니다.");
            return;
        }

        isBusy = true;

        try
        {
            // UGS 초기화
            await InitializeServicesAsync();

            // 초기화 실패 등으로 인증되지 않은 경우
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SetError("플레이어 인증에 실패했습니다.");
                return;
            }

            SetStatus("방 생성 중...");

            // -------------------------------------------------
            // Session 설정
            // -------------------------------------------------

            SessionOptions options =
                new SessionOptions
                {
                    Name = "KINGMAKER ROOM",

                    // 최대 플레이어 수
                    MaxPlayers = maxPlayers,

                    // Join Code를 통한 참가를 사용할 것이므로
                    // 현재는 비공개 방으로 설정
                    IsPrivate = true,

                    // 방장에 의해 잠그기 전까지 참가 가능
                    IsLocked = false
                }
                .WithRelayNetwork();

            // -------------------------------------------------
            // Session 생성
            // -------------------------------------------------

            CurrentSession =
                await MultiplayerService.Instance
                    .CreateSessionAsync(options);

            // -------------------------------------------------
            // 생성 결과
            // -------------------------------------------------

            Debug.Log(
                $"[Multiplayer] 방 생성 성공\n" +
                $"Session ID: {CurrentSession.Id}\n" +
                $"Join Code: {CurrentSession.Code}"
            );

            SetStatus(
                $"방 생성 완료\n" +
                $"코드: {CurrentSession.Code}"
            );

            // UI에 Join Code 전달
            OnJoinCodeCreated?.Invoke(
                CurrentSession.Code
            );

            // Session 연결 완료
            OnSessionConnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            CurrentSession = null;

            SetError(
                "방 생성 실패: " +
                e.Message
            );
        }
        finally
        {
            isBusy = false;
        }
    }

    // =========================================================
    // 방 참가
    // =========================================================

    public async Task JoinRoomAsync(string joinCode)
    {
        // 다른 작업 중이면 실행하지 않음
        if (isBusy)
            return;

        // 이미 방에 참가한 경우
        if (CurrentSession != null)
        {
            SetError("이미 방에 참가한 상태입니다.");
            return;
        }

        // 입력된 코드 정리
        string code =
            joinCode.Trim().ToUpperInvariant();

        // 빈 코드 검사
        if (string.IsNullOrEmpty(code))
        {
            SetError("방 코드를 입력해주세요.");
            return;
        }

        isBusy = true;

        try
        {
            // UGS 초기화
            await InitializeServicesAsync();

            // 인증 확인
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SetError("플레이어 인증에 실패했습니다.");
                return;
            }

            SetStatus("방 참가 중...");

            // -------------------------------------------------
            // Join Code를 이용해 Session 참가
            // -------------------------------------------------

            CurrentSession =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(code);

            // -------------------------------------------------
            // 참가 결과
            // -------------------------------------------------

            Debug.Log(
                $"[Multiplayer] 방 참가 성공\n" +
                $"Session ID: {CurrentSession.Id}\n" +
                $"Join Code: {CurrentSession.Code}"
            );

            SetStatus("방 참가 완료");

            // Session 연결 완료
            OnSessionConnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            CurrentSession = null;

            SetError(
                "방 참가 실패: " +
                e.Message
            );
        }
        finally
        {
            isBusy = false;
        }
    }

    // =========================================================
    // 게임 시작
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

        // Session 확인
        if (CurrentSession == null)
        {
            SetError("참가한 방이 없습니다.");
            return;
        }

        if (!CurrentSession.IsHost)
        {
            SetError(
                "게임 시작은 방장만 할 수 있습니다."
            );
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            SetError(
                "NetworkManager를 찾을 수 없습니다."
            );
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            SetError(
                "Network Server가 실행되지 않았습니다."
            );
            return;
        }

        Debug.Log(
            $"[Multiplayer] 게임 시작: {gameSceneName}"
        );

        NetworkManager.Singleton
            .SceneManager
            .LoadScene(
                gameSceneName,
                LoadSceneMode.Single
            );
    }

    // =========================================================
    // 방 나가기
    // =========================================================

    public async Task LeaveRoomAsync()
    {
        // 참가 중인 방이 없으면 종료
        if (CurrentSession == null)
            return;

        try
        {
            SetStatus("방에서 나가는 중...");

            // 현재 Session 저장
            ISession session =
                CurrentSession;

            // 먼저 참조 제거
            CurrentSession = null;

            // Session에서 나가기
            await session.LeaveAsync();

            // NGO 네트워크가 남아 있다면 종료
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SetStatus("방에서 나왔습니다.");
        }
        catch (Exception e)
        {
            Debug.LogException(e);

            SetError(
                "방 나가기 실패: " +
                e.Message
            );
        }
    }

    // =========================================================
    // 상태 표시
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
    // 에러 표시
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