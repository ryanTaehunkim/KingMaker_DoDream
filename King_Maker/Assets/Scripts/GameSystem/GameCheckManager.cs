using Unity.Netcode;
using UnityEngine;

public enum GameResult
{
    None,
    CitizenWin,
    JokerWin
}

// 게임이 끝난 이유 (결과 화면에 표시)
public enum GameEndReason
{
    None,
    KingAchieved,        // K 등급 달성
    CitizensEliminated,  // 남은 시민이 기준 이하
    TimeOver             // 제한 시간 종료
}

public class GameCheckManager : NetworkBehaviour
{
    public static GameCheckManager Instance { get; private set; }



    [Header("Win Condition")]


    public NetworkVariable<GameResult> result = new NetworkVariable<GameResult>(GameResult.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 게임이 끝난 이유 (result보다 먼저 서버에서 값을 넣는다)
    public NetworkVariable<GameEndReason> endReason = new NetworkVariable<GameEndReason>(GameEndReason.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private int kingGrade;
    [SerializeField] private int citizenLossCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[GameCheckManager] 중복된 GameCheckManager가 있습니다."
            );
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        result.OnValueChanged += OnResultChanged;
        endReason.OnValueChanged += OnEndReasonChanged;

        // 이미 결과가 나온 상태에서 접속한 경우를 위해 한 번 갱신
        GameResultUI.Instance?.Refresh();
    }

    public override void OnNetworkDespawn()
    {
        result.OnValueChanged -= OnResultChanged;
        endReason.OnValueChanged -= OnEndReasonChanged;

        if (Instance == this)
            Instance = null;
    }

    private void OnResultChanged(
        GameResult previousResult,
        GameResult newResult)
    {
        Debug.Log($"[GameCheckManager] 게임 결과: {newResult}");

        // 결과 화면 표시 (None이면 화면을 숨긴다)
        GameResultUI.Instance?.Refresh();
    }

    private void OnEndReasonChanged(
        GameEndReason previousReason,
        GameEndReason newReason)
    {
        // result와 reason이 도착하는 순서가 바뀔 수 있어서, 둘 중 어느 쪽이 와도 다시 그린다
        GameResultUI.Instance?.Refresh();
    }

    // GameFlowManager에서 새 게임 시작 시 호출
    public void ResetResultServer()
    {
        if (!IsServer)
            return;

        endReason.Value = GameEndReason.None;
        result.Value = GameResult.None;

        Debug.Log("[GameCheckManager] 게임 결과 초기화");
    }

    // 킹 등급 달성 여부 확인
    public void CheckAllPlayersGrade()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (result.Value != GameResult.None)
            return;

        if (NetworkManager.Singleton == null)
            return;

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerStateList state =
                client.PlayerObject.GetComponent<PlayerStateList>();

            if (state == null)
                continue;

            // 사망한 플레이어는 승리 조건에서 제외
            if (!state.isAlive.Value)
                continue;

            Role role = state.role.Value;

            // 역할 미배정 플레이어 제외
            if (role == Role.None)
                continue;

            if (state.cardGrade.Value < kingGrade)
                continue;

            EndGame(
                state.role.Value == Role.Joker ? GameResult.JokerWin : GameResult.CitizenWin,
                GameEndReason.KingAchieved);
            return;
        }
    }

    public void CheckAllLife()
    {
        if (!IsServer) return;
        if (result.Value != GameResult.None) return;

        int currentAliveCount = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var state = client.PlayerObject.GetComponent<PlayerStateList>();

            if (state == null) continue;

            if (state.role.Value == Role.Citizen && state.isAlive.Value)
            {
                currentAliveCount++;
            }
        }

        if (currentAliveCount <= citizenLossCount)
        {
            EndGame(GameResult.JokerWin, GameEndReason.CitizensEliminated);

        }

    }

    // 제한 시간이 끝날 때까지 승패가 나지 않으면 조커 팀 승리 (GameFlowManager가 호출)
    public void ServerEndByTimeout()
    {
        if (!IsServer || result.Value != GameResult.None)
            return;

        EndGame(GameResult.JokerWin, GameEndReason.TimeOver);
    }


    private void EndGame(GameResult finalResult, GameEndReason reason)
    {
        if (!IsServer || result.Value != GameResult.None) return;

        endReason.Value = reason;
        result.Value = finalResult;
        MissionManager.Instance?.ServerEndDay();
    }
}