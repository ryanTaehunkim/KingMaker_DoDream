using UnityEngine;
using Unity.Netcode;

public class PlayerStateList : NetworkBehaviour
{
    [Header("Stats")]

    // 생존 여부 (추가)
    public NetworkVariable<bool> isAlive =
        new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 체력
    public NetworkVariable<float> health =
        new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 스태미나
    public NetworkVariable<float> stamina =
        new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 카드 등급
    // 기본적으로 자기 자신만 읽을 수 있도록 설정
    // 조커가 다른 플레이어의 등급을 보는 것은 RoleManager의 Targeted RPC를 사용
    public NetworkVariable<int> cardGrade =
        new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server
        );


    [Header("Role")]

    // 자신의 역할만 읽을 수 있음
    // 서버는 모든 플레이어의 역할을 설정할 수 있음
    public NetworkVariable<Role> role =
        new NetworkVariable<Role>(
            Role.None,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server
        );


    [Header("Interaction State")]

    // 아이템을 들고 있는지
    public NetworkVariable<bool> isHoldingItem =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // 들고 있는 아이템의 NetworkObjectId
    public NetworkVariable<ulong> heldItemNetworkId =
        new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );


    [Header("Mission State")]

    // 클리어한 미션 ID 목록
    public NetworkList<int> clearedMissionIds;


    // 현재 달리는 중인지
    private bool isRunning;


    private void Awake()
    {
        clearedMissionIds = new NetworkList<int>();
    }


    private void Update()
    {
        // 스태미나 계산은 서버에서만
        if (!IsServer)
            return;

        StaminaChanged();
    }


    public override void OnNetworkSpawn()
    {
        health.OnValueChanged += OnHealthChanged;
        stamina.OnValueChanged += OnStaminaChanged;
        cardGrade.OnValueChanged += OnCardGradeChanged;
        role.OnValueChanged += OnRoleChanged;
    }


    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnHealthChanged;
        stamina.OnValueChanged -= OnStaminaChanged;
        cardGrade.OnValueChanged -= OnCardGradeChanged;
        role.OnValueChanged -= OnRoleChanged;
    }


 

    // =========================================================
    // Health
    // =========================================================

    private void OnHealthChanged(
        float oldValue,
        float newValue)
    {
        // 나중에 HUD HP Bar와 연결
        // 예:
        // HPBar.value = newValue;
    }


    // 피해 처리 (추가)
    public void TakeDamage(float damage)
    {
        if (!IsServer || !isAlive.Value)
        {
            return;
        }

        health.Value = Mathf.Max(0f, health.Value - damage);

        if (health.Value <= 0f)
            isAlive.Value = false;

        Debug.Log(health.Value);
    }


    // =========================================================
    // Stamina
    // =========================================================

    private void OnStaminaChanged(
        float oldValue,
        float newValue)
    {
        // 나중에 HUD Stamina Bar와 연결
        // 예:
        // StaminaBar.value = newValue;
    }


    // =========================================================
    // Card Grade
    // =========================================================

    private void OnCardGradeChanged(
        int oldValue,
        int newValue)
    {
        Debug.Log(
            $"[PlayerStateList] " +
            $"Client {OwnerClientId} CardGrade: " +
            $"{oldValue} -> {newValue}"
        );

        // 서버만 게임 결과와 익명 순위판을 갱신
        if (!IsServer)
            return;

        if (GameCheckManager.Instance != null)
        {
            GameCheckManager.Instance.CheckAllPlayersGrade();
        }

        if (AnonymousScoreboardManager.Instance != null)
        {
            AnonymousScoreboardManager.Instance
                .RefreshBoardForEligibleClientsServer();
        }
    }


    // 카드 등급 변경은 서버에서만 가능
    // 미션 성공, 게임 이벤트 등에서 이 함수를 호출해서 사용
    public void SetCardGrade(int newGrade)
    {
        if (!IsServer)
            return;

        // 최소 1등급
        newGrade = Mathf.Max(1, newGrade);

        // 기존 값과 같으면 처리하지 않음
        if (cardGrade.Value == newGrade)
            return;

        cardGrade.Value = newGrade;
    }


    // 등급을 증가시키는 편의 함수
    public void AddCardGrade(int amount)
    {
        if (!IsServer)
            return;

        SetCardGrade(cardGrade.Value + amount);
    }


    // =========================================================
    // Role
    // =========================================================

    private void OnRoleChanged(
        Role oldValue,
        Role newValue)
    {
        Debug.Log(
            $"[PlayerStateList] " +
            $"내 역할 변경: {oldValue} → {newValue}"
        );
    }


    // =========================================================
    // Mission
    // =========================================================

    // 특정 미션을 클리어했는지 확인
    public bool HasClearedMission(int missionId)
    {
        return clearedMissionIds.Contains(missionId);
    }


    // 미션 성공 시 클리어 미션 리스트에 추가
    public void AddClearedMission(int missionId)
    {
        if (!IsServer)
            return;

        if (!clearedMissionIds.Contains(missionId))
        {
            clearedMissionIds.Add(missionId);
        }
    }


    // 일차가 바뀌면 클리어 기록 초기화
    public void ResetClearedMissions()
    {
        if (!IsServer)
            return;

        clearedMissionIds.Clear();
    }


    // =========================================================
    // Running / Stamina
    // =========================================================

    public void SetRunning(bool running)
    {
        if (!IsServer)
            return;

        isRunning = running;
    }


    private void StaminaChanged()
    {
        float newStamina = stamina.Value;

        if (isRunning)
        {
            newStamina -= 20f * Time.deltaTime;
        }
        else
        {
            newStamina += 5f * Time.deltaTime;
        }

        newStamina =
            Mathf.Clamp(
                newStamina,
                0f,
                100f
            );
        stamina.Value = newStamina;
    }
}