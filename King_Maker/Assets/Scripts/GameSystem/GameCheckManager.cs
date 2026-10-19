
using Unity.Netcode;
using UnityEngine;

public enum GameResult
{
    None,
    CitizenWin,
    JokerWin
}

public class GameCheckManager : NetworkBehaviour
{
    public static GameCheckManager Instance { get; private set; }

    public NetworkVariable<GameResult> result =
        new NetworkVariable<GameResult>(
            GameResult.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("Win Condition")]
    [SerializeField] private int kingGrade = 10;

    public NetworkVariable<GameResult> result = new NetworkVariable<GameResult>(GameResult.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
    }

    public override void OnNetworkDespawn()
    {
        result.OnValueChanged -= OnResultChanged;

        if (Instance == this)
            Instance = null;
    }

    private void OnResultChanged(
        GameResult previousResult,
        GameResult newResult)
    {
        if (newResult == GameResult.None)
            return;

        Debug.Log($"[GameCheckManager] 게임 결과: {newResult}");

        // 추후 이곳에 결과 UI 연결
    }

    // GameFlowManager에서 새 게임 시작 시 호출
    public void ResetResultServer()
    {
        if (!IsServer)
            return;

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

            EndGame(state.role.Value == Role.Joker ? GameResult.JokerWin : GameResult.CitizenWin);
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

           
            if (state.role.Value == Role.Citizen && state.isAlive.Value)
            {
                currentAliveCount++;
            }
        }

        if (currentAliveCount <= citizenLossCount)
        {
            EndGame(GameResult.JokerWin);
        
        }
    
    }


    private void EndGame(GameResult finalResult)
    {
        if (!IsServer || result.Value != GameResult.None) return;

        result.Value = finalResult;
        MissionManager.Instance?.ServerEndDay();
    }
}
