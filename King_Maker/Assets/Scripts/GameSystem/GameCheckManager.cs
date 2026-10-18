
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

            if (role == Role.Joker)
            {
                result.Value = GameResult.JokerWin;
            }
            else if (role == Role.Citizen)
            {
                result.Value = GameResult.CitizenWin;
            }

            if (result.Value != GameResult.None)
            {
                Debug.Log(
                    $"[GameCheckManager] 승리 조건 충족: " +
                    $"Client {client.ClientId}, Role {role}, " +
                    $"Grade {state.cardGrade.Value}"
                );

                return;
            }
        }
    }
}
