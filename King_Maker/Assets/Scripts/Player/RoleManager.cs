
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public enum Role
{
    None,
    Citizen,
    Joker
}

public class RoleManager : NetworkBehaviour
{
    [Header("Role Settings")]
    [SerializeField] private int minJokerCount = 1;
    [SerializeField] private bool allowAllJoker = true;

    // 내 역할
    public Role myRole { get; private set; } = Role.None;

    public event Action<Role> OnMyRoleChanged;

    // 서버에서 관리하는 역할 목록
    private readonly Dictionary<ulong, Role> serverRoles =
        new Dictionary<ulong, Role>();

    public bool IsLocalJoker => myRole == Role.Joker;

    // GameFlowManager에서 역할 배정 여부 확인
    private bool rolesAssigned = false;

    public bool RolesAssigned => rolesAssigned;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private float fadeoutTime = 1f;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float holdDuration = 2f;

    private Coroutine revealRoutine;

    private void Awake()
    {
        if (roleText != null)
        {
            roleText.alpha = 0f;
            roleText.text = "";
        }
    }

    private void Update()
    {
        UpGradeBtn();
        // 역할 배정은 서버에서만 가능
        if (!IsServer)
            return;
    }

    public override void OnNetworkSpawn()
    {
        // 테스트용:
        // Host에서 Z를 누르면 역할 배정
        if (Keyboard.current != null &&
            Keyboard.current.zKey.wasPressedThisFrame)
        {
            rolesAssigned = false;
            serverRoles.Clear();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }
    }

    // 기존 등급 상승 버튼에서 호출
    public void UpGradeBtn()
    {
        if (!IsSpawned)
            return;

        if(InputManager.interactPressed)
            UpGradeBtnRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UpGradeBtnRpc(RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton == null)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                senderId, out NetworkClient client))
            return;

        if (client.PlayerObject == null)
            return;

        PlayerStateList state =
            client.PlayerObject.GetComponent<PlayerStateList>();

        if (state == null)
            return;

        int oldGrade = state.cardGrade.Value;
        int newGrade = Mathf.Min(oldGrade + 1, 10);

        if (newGrade == oldGrade)
            return;

        state.cardGrade.Value = newGrade;
    }

    // 서버에서 한 번만 역할 배정
    public void AssignRoles()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (rolesAssigned)
        {
            Debug.LogWarning("[RoleManager] 이미 역할이 배정되었습니다.");
            return;
        }

        if (NetworkManager.Singleton == null)
            return;

        List<ulong> clientIds =
            new List<ulong>(
                NetworkManager.Singleton.ConnectedClientsIds
            );

        int total = clientIds.Count;

        if (total == 0)
        {
            Debug.LogWarning("[RoleManager] 접속한 플레이어가 없습니다.");
            return;
        }

        // 모든 플레이어의 PlayerStateList 확인
        foreach (ulong clientId in clientIds)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                    clientId, out NetworkClient client) ||
                client.PlayerObject == null ||
                client.PlayerObject.GetComponent<PlayerStateList>() == null)
            {
                Debug.LogError(
                    $"[RoleManager] Client {clientId}의 플레이어 상태가 없습니다."
                );
                return;
            }
        }

        // 조커 수 결정
        int jokerCount = Mathf.Clamp(minJokerCount, 1, total);

        if (!allowAllJoker && total > 1)
            jokerCount = Mathf.Min(jokerCount, total - 1);

        // Fisher-Yates Shuffle
        for (int i = clientIds.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);

            (clientIds[i], clientIds[j]) =
                (clientIds[j], clientIds[i]);
        }

        serverRoles.Clear();

        for (int i = 0; i < clientIds.Count; i++)
        {
            Role assignedRole =
                i < jokerCount ? Role.Joker : Role.Citizen;

            serverRoles[clientIds[i]] = assignedRole;
        }

        // 각 플레이어에게 역할 적용
        foreach (var pair in serverRoles)
        {
            ulong clientId = pair.Key;
            Role assignedRole = pair.Value;

            NetworkClient client =
                NetworkManager.Singleton.ConnectedClients[clientId];

            PlayerStateList playerState =
                client.PlayerObject.GetComponent<PlayerStateList>();

            playerState.role.Value = assignedRole;

            AssignRoleRpc(
                assignedRole,
                RpcTarget.Single(clientId, RpcTargetUse.Temp)
            );

            Debug.Log(
                $"[RoleManager] Client {clientId} = {assignedRole}"
            );
        }

        // 모든 역할 배정 절차 완료
        rolesAssigned = true;

        Debug.Log("[RoleManager] 역할 배정 완료");
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AssignRoleRpc(
        Role role,
        RpcParams rpcParams = default)
    {
        myRole = role;

        Debug.Log($"[RoleManager] 내 역할: {myRole}");

        OnMyRoleChanged?.Invoke(myRole);

        ShowRoleUI(role);
    }

    private void ShowRoleUI(Role role)
    {
        if (roleText == null)
            return;

        string label = role switch
        {
            Role.Joker => "JOKER",
            Role.Citizen => "CITIZEN",
            _ => ""
        };

        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        revealRoutine = StartCoroutine(RevealRoutine(label));
    }

    private IEnumerator RevealRoutine(string label)
    {
        yield return new WaitForSecondsRealtime(fadeoutTime);

        roleText.text = label;

        yield return FadeTextRoutine(0f, 1f, fadeDuration);

        yield return new WaitForSecondsRealtime(holdDuration);

        yield return FadeTextRoutine(1f, 0f, fadeDuration);

        revealRoutine = null;
    }

    private IEnumerator FadeTextRoutine(
        float from,
        float to,
        float duration)
    {
        if (duration <= 0f)
        {
            roleText.alpha = to;
            yield break;
        }

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            roleText.alpha = Mathf.Lerp(
                from,
                to,
                Mathf.Clamp01(t / duration)
            );

            yield return null;
        }

        roleText.alpha = to;
    }
}
