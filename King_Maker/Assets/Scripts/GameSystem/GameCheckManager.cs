using Unity.Netcode;
using UnityEngine;

public enum GameResult { None, CitizenWin, JokerWin}
public class GameCheckManager : NetworkBehaviour
{
    public static GameCheckManager Instance { get; private set; }

    public NetworkVariable<GameResult> result = new NetworkVariable<GameResult>(GameResult.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField] private int kingGrade;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        
    }
    public override void OnNetworkSpawn()
    {
        result.OnValueChanged += OnResultChanged;
    }
    public override void OnNetworkDespawn()
    {
        result.OnValueChanged -= OnResultChanged;
    }
    private void OnResultChanged(GameResult oldV, GameResult newV)
    {
        if (newV == GameResult.None) return;

        //모든 클라이언트 결과 UI
        Debug.Log(result.Value);
    }

    public void CheckAllPlayersGrade()
    {
        if (!IsServer) return;
        if (result.Value != GameResult.None) return;

        foreach(var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var state = client.PlayerObject.GetComponent<PlayerStateList>();
            if (state.cardGrade.Value < kingGrade) continue;

            Role role = state.role.Value;

            result.Value = role == Role.Joker ? GameResult.JokerWin : GameResult.CitizenWin;
            return;
        }
    }
}
