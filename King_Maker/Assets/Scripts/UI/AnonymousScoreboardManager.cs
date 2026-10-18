
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class AnonymousScoreboardManager : NetworkBehaviour
{
    public static AnonymousScoreboardManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameTimeManager gameTimeManager;

    [Header("Debug Test")]
    [SerializeField] private bool enableDebugHotkeys = true;
    [SerializeField] private KeyCode showBoardKey = KeyCode.F8;
    [SerializeField] private KeyCode hideBoardKey = KeyCode.F9;

    // 이번 일차에 점수판 공개 자격을 받은 플레이어
    private readonly HashSet<ulong> eligibleClients =
        new HashSet<ulong>();

    // 각 클라이언트에서만 관리하는 익명 등급 목록
    private readonly List<int> localAnonymousGrades =
        new List<int>();

    private int expectedEntryCount;

    public bool LocalBoardVisible { get; private set; }
    public int LocalBoardDay { get; private set; }

    public IReadOnlyList<int> LocalAnonymousGrades =>
        localAnonymousGrades;

    public event Action OnLocalBoardChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[AnonymousScoreboardManager] 중복으로 존재합니다."
            );
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (gameTimeManager == null)
        {
            gameTimeManager =
                FindFirstObjectByType<GameTimeManager>();
        }

        if (gameTimeManager != null)
        {
            gameTimeManager.currentDay.OnValueChanged +=
                OnDayChanged;
        }
        else
        {
            Debug.LogError(
                "[AnonymousScoreboardManager] GameTimeManager를 찾지 못했습니다."
            );
        }

        if (IsServer)
        {
            eligibleClients.Clear();
        }

        ClearLocalBoard();
    }

    public override void OnNetworkDespawn()
    {
        if (gameTimeManager != null)
        {
            gameTimeManager.currentDay.OnValueChanged -=
                OnDayChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!enableDebugHotkeys || !IsSpawned)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        // F8: 현재 플레이어에게 테스트 점수판 공개 요청
        if (keyboard.f8Key.wasPressedThisFrame)
        {
            Debug.Log(
                $"[Scoreboard Test] F8 입력 감지 / " +
                $"IsSpawned={IsSpawned} / IsServer={IsServer}"
            );

            RequestTestBoardRpc();
        }

        // F9: 현재 플레이어의 점수판 숨김 요청
        if (keyboard.f9Key.wasPressedThisFrame)
        {
            Debug.Log("[Scoreboard Test] F9 입력 감지");

            RequestHideTestBoardRpc();
        }
    }
    // =====================================================
    // Mission success
    // 서버에서 미션 성공이 확정된 플레이어에게 호출
    // =====================================================

    public void GrantBoardToPlayerServer(ulong clientId)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(
                clientId))
        {
            return;
        }

        if (gameTimeManager == null)
            return;

        // 현재 일차에 공개 자격 등록
        eligibleClients.Add(clientId);

        // 해당 플레이어에게만 익명 점수판 전송
        SendBoardToPlayerServer(clientId);
    }

    // 여러 플레이어가 미션 성공 보상을 받는 경우
    public void GrantBoardToPlayersServer(
        IEnumerable<ulong> clientIds)
    {
        if (!IsServer || !IsSpawned || clientIds == null)
            return;

        foreach (ulong clientId in clientIds)
        {
            GrantBoardToPlayerServer(clientId);
        }
    }

    // 기획상 모든 참가자가 점수판을 볼 수 있는 미션에 사용
    public void GrantBoardToAllConnectedPlayersServer()
    {
        if (!IsServer || !IsSpawned ||
            NetworkManager.Singleton == null)
        {
            return;
        }

        foreach (
            ulong clientId
            in NetworkManager.Singleton.ConnectedClientsIds)
        {
            GrantBoardToPlayerServer(clientId);
        }
    }

    // =====================================================
    // Debug test RPCs
    // =====================================================

    // F8: 요청한 플레이어에게만 점수판 공개
    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTestBoardRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || !IsSpawned)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        Debug.Log(
            $"[Scoreboard Test] Client {senderId} 공개 요청"
        );

        GrantBoardToPlayerServer(senderId);
    }

    // F9: 요청한 플레이어의 공개 자격 해제 및 점수판 숨김
    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestHideTestBoardRpc(
        RpcParams rpcParams = default)
    {
        if (!IsServer || !IsSpawned)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        eligibleClients.Remove(senderId);

        HideBoardRpc(
            RpcTarget.Single(senderId, RpcTargetUse.Temp)
        );

        Debug.Log(
            $"[Scoreboard Test] Client {senderId} 점수판 숨김"
        );
    }

    // =====================================================
    // Anonymous scoreboard data
    // =====================================================

    private List<int> BuildAnonymousGradesServer()
    {
        List<int> grades = new List<int>();

        if (!IsServer || NetworkManager.Singleton == null)
            return grades;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerStateList state =
                client.PlayerObject.GetComponent<PlayerStateList>();

            if (state == null)
                continue;

            // 현재 프로젝트에서는 카드 등급을 순위 기준으로 사용
            grades.Add(state.cardGrade.Value);
        }

        // 높은 등급부터 정렬
        grades.Sort((a, b) => b.CompareTo(a));

        return grades;
    }

    private void SendBoardToPlayerServer(ulong clientId)
    {
        if (!IsServer || !IsSpawned ||
            NetworkManager.Singleton == null ||
            gameTimeManager == null)
        {
            return;
        }

        if (!eligibleClients.Contains(clientId))
            return;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(
                clientId))
        {
            return;
        }

        List<int> grades = BuildAnonymousGradesServer();

        // 해당 클라이언트에게 목록 시작 정보 전송
        BeginBoardRpc(
            gameTimeManager.currentDay.Value,
            grades.Count,
            RpcTarget.Single(clientId, RpcTargetUse.Temp)
        );

        // 이름과 ClientId 없이 등급만 전송
        for (int i = 0; i < grades.Count; i++)
        {
            AddBoardEntryRpc(
                i,
                grades[i],
                RpcTarget.Single(clientId, RpcTargetUse.Temp)
            );
        }
    }

    // 등급 변경 시 공개 자격이 있는 플레이어의 점수판 갱신
    public void RefreshBoardForEligibleClientsServer()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (eligibleClients.Count == 0)
            return;

        List<ulong> clients =
            new List<ulong>(eligibleClients);

        foreach (ulong clientId in clients)
        {
            SendBoardToPlayerServer(clientId);
        }
    }

    // =====================================================
    // Targeted RPCs
    // =====================================================

    [Rpc(SendTo.SpecifiedInParams)]
    private void BeginBoardRpc(
        int day,
        int entryCount,
        RpcParams rpcParams = default)
    {
        LocalBoardVisible = true;
        LocalBoardDay = day;

        localAnonymousGrades.Clear();
        expectedEntryCount = entryCount;

        if (expectedEntryCount <= 0)
        {
            OnLocalBoardChanged?.Invoke();
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AddBoardEntryRpc(
        int index,
        int grade,
        RpcParams rpcParams = default)
    {
        if (!LocalBoardVisible)
            return;

        if (index < 0 || index >= expectedEntryCount)
            return;

        // 중복 항목 방지
        if (index < localAnonymousGrades.Count)
            return;

        // 항목은 순서대로 수신
        if (index != localAnonymousGrades.Count)
            return;

        localAnonymousGrades.Add(grade);

        // 전체 목록 수신이 끝난 뒤 UI 갱신
        if (localAnonymousGrades.Count == expectedEntryCount)
        {
            OnLocalBoardChanged?.Invoke();
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void HideBoardRpc(RpcParams rpcParams = default)
    {
        ClearLocalBoard();
    }

    // =====================================================
    // Day reset
    // =====================================================

    private void OnDayChanged(int oldDay, int newDay)
    {
        // 모든 클라이언트에서 로컬 점수판 초기화
        ClearLocalBoard();

        if (!IsServer)
            return;

        // 새 일차에는 이전 공개 자격 초기화
        eligibleClients.Clear();

        if (NetworkManager.Singleton == null)
            return;

        // 연결된 클라이언트에게 점수판 숨김 전달
        foreach (
            ulong clientId
            in NetworkManager.Singleton.ConnectedClientsIds)
        {
            HideBoardRpc(
                RpcTarget.Single(clientId, RpcTargetUse.Temp)
            );
        }

        Debug.Log(
            $"[Scoreboard] DAY {newDay} 시작: 점수판 초기화"
        );
    }

    private void ClearLocalBoard()
    {
        LocalBoardVisible = false;
        LocalBoardDay = 0;
        expectedEntryCount = 0;

        localAnonymousGrades.Clear();

        OnLocalBoardChanged?.Invoke();
    }
}
