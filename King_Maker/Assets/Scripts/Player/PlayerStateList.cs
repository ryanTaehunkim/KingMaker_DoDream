using UnityEngine;
using Unity.Netcode;
using UnityEditor.Rendering;

public class PlayerStateList : NetworkBehaviour{

    [Header("Stats")]
    public NetworkVariable<float> health = new NetworkVariable<float>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> stamina = new NetworkVariable<float>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> cardGrade = new NetworkVariable<int>(2 , NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Interaction State")]
    public NetworkVariable<bool> isHoldingItem = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> heldItemNetworkId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkList<int> clearedMissionIds;

    //Player 행동 양상
    private bool isRunning;

    private void Awake()
    {
        clearedMissionIds = new NetworkList<int>();
    }

    private void Update()
    {
        if (!IsServer) return;

        if (isRunning)
        {
            stamina.Value -= 20f * Time.deltaTime;
        }
        else
        {
            stamina.Value += 5f * Time.deltaTime;
        }
        stamina.Value = Mathf.Clamp(stamina.Value, 0f, 100f);
    }
    public override void OnNetworkSpawn()
    {
        health.OnValueChanged += OnHealthChanged;
        stamina.OnValueChanged += OnStaminaChanged;
        cardGrade.OnValueChanged += OnCardGradeChanged;
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnHealthChanged;
        stamina.OnValueChanged -= OnStaminaChanged;
        cardGrade.OnValueChanged -= OnCardGradeChanged;
    }
    //체력 변경시 일어날 일들 
    private void OnHealthChanged(float oldValue, float newValue) {
        //체력바 ui 변경
    }

    //스테미나 변경 시 일어날 일들
    private void OnStaminaChanged(float oldValue, float newValue) {
        //스테미나 ui변경
    }
    //카드등급 변경 시 일어날 일들
    private void OnCardGradeChanged(int oldValue, int newValue) {
        //카드 등급 ui변경 등등
    }

    //미션 클리어 여부 판단
    public bool HasClearedMission(int missionId)
    {
        return clearedMissionIds.Contains(missionId);
    }

    //미션 성공시 클리어미션리스트에 추가
    public void AddClearedMission(int missionId)
    {
        if (!IsServer) return;
        if (!clearedMissionIds.Contains(missionId)){
            clearedMissionIds.Add(missionId);
        }

    }

    public void SetRunning(bool running)
    {
        
        if (!IsServer) return;
        isRunning = running;
    }
}
