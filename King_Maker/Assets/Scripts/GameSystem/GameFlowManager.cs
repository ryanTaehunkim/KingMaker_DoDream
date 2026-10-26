
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameFlowManager : NetworkBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    // 게임을 시작한 적이 있는지
    public NetworkVariable<bool> HasGameStarted =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 현재 게임이 진행 중인지
    public NetworkVariable<bool> IsGameInProgress =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("기존 매니저")]
    [SerializeField] private RoleManager roleManager;
    [SerializeField] private GameTimeManager gameTimeManager;
    [SerializeField] private GameCheckManager gameCheckManager;

    [Header("게임 시작 후 필드 위치")]
    [SerializeField] private Transform[] fieldSpawnPoints;

    [Header("블라인드 드롭 위치")]
    [SerializeField] private Transform[] blindDropSpawnPoints;

    private int lastHandledPhase =
        GameTimeManager.PhaseWaiting;

    private struct ReturnPoint
    {
        public Vector3 position;
        public Quaternion rotation;

        public ReturnPoint(
            Vector3 position,
            Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }

    private readonly Dictionary<ulong, ReturnPoint> returnPoints =
        new Dictionary<ulong, ReturnPoint>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[GameFlow] GameFlowManager가 중복으로 존재합니다."
            );
            return;
        }

        Instance = this;

        if (roleManager == null)
            roleManager = GetComponent<RoleManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        HasGameStarted.Value = false;
        IsGameInProgress.Value = false;

        lastHandledPhase = GameTimeManager.PhaseWaiting;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        // 호스트만 Z로 시작
        if (!HasGameStarted.Value)
        {
            if (Keyboard.current != null &&
                Keyboard.current.zKey.wasPressedThisFrame)
            {
                StartGame();
            }

            return;
        }

        if (!IsGameInProgress.Value)
            return;

        // 승패가 결정되면 게임 종료
        if (gameCheckManager != null &&
            gameCheckManager.result.Value != GameResult.None)
        {
            EndGameFromResult();
            return;
        }

        if (gameTimeManager == null)
            return;

        int phase = gameTimeManager.currentPhase.Value;

        if (phase == lastHandledPhase)
            return;

        lastHandledPhase = phase;

        HandlePhaseChanged(phase);
    }

    private void StartGame()
    {
        if (!IsServer || HasGameStarted.Value)
            return;

        if (!ValidateSetup())
            return;

        // 이전 결과 초기화
        gameCheckManager.ResetResultServer();

        // 기존 RoleManager의 역할 배정 기능 사용
        roleManager.AssignRoles();

        if (!roleManager.RolesAssigned)
        {
            Debug.LogError(
                "[GameFlow] 역할 배정에 실패했습니다."
            );
            return;
        }

        HasGameStarted.Value = true;
        IsGameInProgress.Value = true;

        lastHandledPhase = GameTimeManager.PhaseDay1Field;

        // 모든 플레이어를 필드로 이동
        MovePlayersToSpawnPoints(
            fieldSpawnPoints,
            false,
            false
        );

        // 역할 배정 및 이동 이후 타이머 시작
        gameTimeManager.StartGameTimer();

        Debug.Log(
            "[GameFlow] 게임 시작: 1일차 필드 탐색 및 미션"
        );
    }

    private bool ValidateSetup()
    {
        if (roleManager == null ||
            gameTimeManager == null ||
            gameCheckManager == null)
        {
            Debug.LogError(
                "[GameFlow] 기존 매니저 참조가 누락되었습니다."
            );
            return false;
        }

        if (!roleManager.IsSpawned ||
            !gameTimeManager.IsSpawned ||
            !gameCheckManager.IsSpawned)
        {
            Debug.LogError(
                "[GameFlow] 매니저의 NetworkObject가 스폰되지 않았습니다."
            );
            return false;
        }

        if (!HasAnySpawnPoint(fieldSpawnPoints) ||
            !HasAnySpawnPoint(blindDropSpawnPoints))
        {
            Debug.LogError(
                "[GameFlow] 필드 또는 드롭 위치를 설정해 주세요."
            );
            return false;
        }

        if (NetworkManager.Singleton == null)
            return false;

        if (NetworkManager.Singleton.ConnectedClientsIds.Count == 0)
        {
            Debug.LogError("[GameFlow] 접속한 플레이어가 없습니다.");
            return false;
        }

        // 모든 접속자의 플레이어 오브젝트 확인
        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
            {
                Debug.LogError(
                    $"[GameFlow] Client {client.ClientId}의 " +
                    "PlayerObject가 없습니다."
                );
                return false;
            }

            PlayerStateList state =
                client.PlayerObject.GetComponent<PlayerStateList>();

            PlayerMovementLock movementLock =
                client.PlayerObject.GetComponent<PlayerMovementLock>();

            if (state == null || movementLock == null)
            {
                Debug.LogError(
                    $"[GameFlow] Client {client.ClientId}의 " +
                    "PlayerStateList 또는 PlayerMovementLock이 없습니다."
                );
                return false;
            }
        }

        return true;
    }

    private void HandlePhaseChanged(int phase)
    {
        switch (phase)
        {
            case GameTimeManager.PhaseDay1Drop:
                StartBlindDrop();
                Debug.Log("[GameFlow] 1일차 드롭 시작");
                break;

            case GameTimeManager.PhaseDay2Field:
                EndBlindDrop();
                Debug.Log("[GameFlow] 2일차 필드 시작");
                break;

            case GameTimeManager.PhaseDay2Drop:
                StartBlindDrop();
                Debug.Log("[GameFlow] 2일차 드롭 시작");
                break;

            case GameTimeManager.PhaseDay3Field:
                EndBlindDrop();
                Debug.Log("[GameFlow] 3일차 필드 시작");
                break;

            case GameTimeManager.PhaseDay4Final:
                Debug.Log("[GameFlow] 4일차 최종 진행 시작");
                break;

            case GameTimeManager.PhaseFinished:
                IsGameInProgress.Value = false;
                BlindDropManager.Instance?.ServerEndBlindDrop();
                LockAllPlayers(true);

                Debug.Log("[GameFlow] 게임 시간 종료");
                break;
        }
    }

    private void StartBlindDrop()
    {
        returnPoints.Clear();

        int spawnIndex = 0;

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;

            if (playerObject == null)
                continue;

            PlayerStateList state =
                playerObject.GetComponent<PlayerStateList>();

            // 생존자만 이동
            if (state == null || !state.isAlive.Value)
                continue;

            Transform target =
                PickSpawnPoint(blindDropSpawnPoints, spawnIndex);

            if (target == null)
                continue;

            // 미니게임 전 위치를 저장
            returnPoints[playerObject.OwnerClientId] =
                new ReturnPoint(
                    playerObject.transform.position,
                    playerObject.transform.rotation
                );

            // 드롭 장소로 이동
            TeleportPlayer(
                playerObject,
                target.position,
                target.rotation
            );

            // 이동 및 행동 잠금
            PlayerMovementLock movementLock =
                playerObject.GetComponent<PlayerMovementLock>();

            movementLock.SetLockedServer(true);

            spawnIndex++;
        }

        BlindDropManager.Instance?.ServerBeginBlindDrop(gameTimeManager.currentDay.Value);
    }

    private void EndBlindDrop()
    {
        BlindDropManager.Instance?.ServerEndBlindDrop();

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;

            if (playerObject == null)
                continue;

            PlayerStateList state =
                playerObject.GetComponent<PlayerStateList>();

            // 생존자만 복귀
            if (state == null || !state.isAlive.Value)
                continue;

            ulong clientId = playerObject.OwnerClientId;

            if (!returnPoints.TryGetValue(
                    clientId,
                    out ReturnPoint savedPoint))
            {
                continue;
            }

            TeleportPlayer(
                playerObject,
                savedPoint.position,
                savedPoint.rotation
            );

            PlayerMovementLock movementLock =
                playerObject.GetComponent<PlayerMovementLock>();

            if (movementLock != null)
                movementLock.SetLockedServer(false);
        }

        returnPoints.Clear();
    }

    private void MovePlayersToSpawnPoints(
        Transform[] spawnPoints,
        bool aliveOnly,
        bool lockMovement)
    {
        int spawnIndex = 0;

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;

            if (playerObject == null)
                continue;

            PlayerStateList state =
                playerObject.GetComponent<PlayerStateList>();

            if (aliveOnly &&
                (state == null || !state.isAlive.Value))
            {
                continue;
            }

            Transform target =
                PickSpawnPoint(spawnPoints, spawnIndex);

            if (target == null)
                continue;

            TeleportPlayer(
                playerObject,
                target.position,
                target.rotation
            );

            PlayerMovementLock movementLock =
                playerObject.GetComponent<PlayerMovementLock>();

            if (movementLock != null)
                movementLock.SetLockedServer(lockMovement);

            spawnIndex++;
        }
    }


    private void TeleportPlayer(
        NetworkObject playerObject,
        Vector3 position,
        Quaternion rotation)
    {
        if (!IsServer || playerObject == null)
            return;

        // 해당 플레이어를 소유한 클라이언트에 순간이동 요청
        TeleportPlayerClientRpc(
            new NetworkObjectReference(playerObject),
            position,
            rotation,
            playerObject.transform.localScale,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[]
                    {
                    playerObject.OwnerClientId
                    }
                }
            }
        );
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(
        NetworkObjectReference playerReference,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        ClientRpcParams rpcParams = default)
    {
        if (!playerReference.TryGet(out NetworkObject playerObject))
        {
            Debug.LogWarning(
                "[GameFlow] 순간이동할 플레이어를 찾지 못했습니다."
            );
            return;
        }

        // 실제 소유자만 순간이동 처리
        if (!playerObject.IsOwner)
            return;

        NetworkTransform networkTransform =
            playerObject.GetComponent<NetworkTransform>();

        if (networkTransform == null)
        {
            Debug.LogError(
                "[GameFlow] 플레이어의 NetworkTransform이 없습니다."
            );
            return;
        }

        PlayerControlRigid rigidControl =
        playerObject.GetComponent<PlayerControlRigid>();

        if (rigidControl != null)
            rigidControl.ResetPhysicsAt(position, rotation);

        networkTransform.Teleport(
            position,
            rotation,
            scale
        );

        Debug.Log(
            $"[GameFlow] 플레이어 순간이동 완료: " +
            $"ClientId={playerObject.OwnerClientId}"
        );
    }
    

    private void LockAllPlayers(bool locked)
    {
        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerMovementLock movementLock =
                client.PlayerObject.GetComponent<PlayerMovementLock>();

            if (movementLock != null)
                movementLock.SetLockedServer(locked);
        }
    }

    private Transform PickSpawnPoint(
        Transform[] points,
        int startIndex)
    {
        if (points == null || points.Length == 0)
            return null;

        for (int i = 0; i < points.Length; i++)
        {
            int index = (startIndex + i) % points.Length;

            if (points[index] != null)
                return points[index];
        }

        return null;
    }

    private bool HasAnySpawnPoint(Transform[] points)
    {
        if (points == null)
            return false;

        foreach (Transform point in points)
        {
            if (point != null)
                return true;
        }

        return false;
    }

    private void EndGameFromResult()
    {
        IsGameInProgress.Value = false;

        gameTimeManager.FinishGameTimer();
        BlindDropManager.Instance?.ServerEndBlindDrop();
        // 결과 확정 후 추가 행동 방지
        LockAllPlayers(true);

        Debug.Log(
            $"[GameFlow] 게임 종료: {gameCheckManager.result.Value}"
        );
    }
}
