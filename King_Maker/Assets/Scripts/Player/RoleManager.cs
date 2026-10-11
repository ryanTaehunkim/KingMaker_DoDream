using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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


    // =========================================================
    // 내 역할
    // =========================================================

    public Role myRole { get; private set; } = Role.None;


    // 내 역할이 변경되었을 때 UI 등에 알려주기 위한 이벤트
    public event Action<Role> OnMyRoleChanged;


    // =========================================================
    // 서버 역할 관리
    // =========================================================

    // 서버에서만 전체 플레이어의 역할을 저장
    private readonly Dictionary<ulong, Role> serverRoles =
        new Dictionary<ulong, Role>();


    // =========================================================
    // Joker Grade
    // =========================================================

    // 조커 클라이언트에서만 사용하는
    // "각 플레이어의 카드 등급" 저장소

    private readonly Dictionary<ulong, int> jokerVisibleGrades =
        new Dictionary<ulong, int>();


    // UI에서 조커의 전체 등급 정보를 읽을 수 있도록 제공
    public IReadOnlyDictionary<ulong, int> JokerVisibleGrades
        => jokerVisibleGrades;


    // 현재 내 역할이 조커인지 쉽게 확인하기 위한 프로퍼티
    public bool IsLocalJoker
        => myRole == Role.Joker;


    // =========================================================
    // Role UI
    // =========================================================

    [Header("UI")]

    [SerializeField] private TextMeshProUGUI roleText;

    [SerializeField] private float fadeoutTime = 1f;

    [SerializeField] private float fadeDuration = 1.0f;

    [SerializeField] private float holdDuration = 2f;


    private Coroutine revealRoutine;


    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        if (roleText != null)
        {
            roleText.alpha = 0f;
            roleText.text = "";
        }
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        // 역할 배정은 서버에서만 가능
        if (!IsServer)
            return;

        // 테스트용:
        // Host에서 Z를 누르면 역할 배정
        if (Keyboard.current != null &&
            Keyboard.current.zKey.wasPressedThisFrame)
        {
            AssignRoles();
        }
    }


    // =========================================================
    // Assign Roles
    // =========================================================

    private void AssignRoles()
    {
        // 현재 접속 중인 클라이언트 목록
        List<ulong> clientIds =
            new List<ulong>(
                NetworkManager.Singleton.ConnectedClientsIds
            );

        int total = clientIds.Count;

        if (total == 0)
            return;


        // Fisher-Yates Shuffle
        for (int i = clientIds.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);

            (clientIds[i], clientIds[j]) =
                (clientIds[j], clientIds[i]);
        }


        // 이전 역할 정보 초기화
        serverRoles.Clear();


        // 역할 배정
        for (int i = 0; i < clientIds.Count; i++)
        {
            Role assignedRole =
                i < minJokerCount
                ? Role.Joker
                : Role.Citizen;

            serverRoles[clientIds[i]] =
                assignedRole;
        }


        // 서버 로그
        foreach (var pair in serverRoles)
        {
            Debug.Log(
                $"[RoleManager] " +
                $"ClientId {pair.Key} = {pair.Value}"
            );
        }


        // =====================================================
        // 각 플레이어에게 역할 적용
        // =====================================================

        foreach (var pair in serverRoles)
        {
            ulong clientId = pair.Key;
            Role assignedRole = pair.Value;


            // Client 찾기
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                    clientId,
                    out NetworkClient client))
            {
                Debug.LogWarning(
                    $"[RoleManager] " +
                    $"Client {clientId}를 찾을 수 없습니다."
                );

                continue;
            }


            // PlayerObject 확인
            if (client.PlayerObject == null)
            {
                Debug.LogWarning(
                    $"[RoleManager] " +
                    $"Client {clientId}의 PlayerObject가 없습니다."
                );

                continue;
            }


            // PlayerStateList 찾기
            PlayerStateList playerState =
                client.PlayerObject.GetComponent<PlayerStateList>();


            if (playerState == null)
            {
                Debug.LogError(
                    $"[RoleManager] " +
                    $"Client {clientId}의 PlayerObject에 " +
                    $"PlayerStateList가 없습니다."
                );

                continue;
            }


            // 서버에 역할 저장
            // Owner만 읽을 수 있기 때문에 다른 플레이어에게 공개되지 않음
            playerState.role.Value = assignedRole;


            // 해당 플레이어에게만 역할 전달
            AssignRoleRpc(
                assignedRole,
                RpcTarget.Single(
                    clientId,
                    RpcTargetUse.Temp
                )
            );
        }


        // =====================================================
        // 역할 배정이 끝난 뒤
        // 조커에게 모든 플레이어의 현재 등급 전달
        // =====================================================

        SendAllGradesToJokers();
    }


    // =========================================================
    // Role RPC
    // =========================================================

    [Rpc(SendTo.SpecifiedInParams)]
    private void AssignRoleRpc(
        Role role,
        RpcParams rpcParams = default)
    {
        // 내 RoleManager에 역할 저장
        myRole = role;


        Debug.Log(
            $"[RoleManager] 내 역할: {myRole}"
        );


        // 새로운 역할 배정이면 기존 조커 등급 정보 초기화
        if (role == Role.Joker)
        {
            jokerVisibleGrades.Clear();
        }


        // 역할 변경 이벤트
        OnMyRoleChanged?.Invoke(myRole);


        // 역할 공개 UI
        ShowRoleUI(role);
    }


    // =========================================================
    // Joker : 모든 플레이어 등급 전달
    // =========================================================

    private void SendAllGradesToJokers()
    {
        if (!IsServer)
            return;


        // 서버가 알고 있는 모든 플레이어를 순회
        foreach (var playerPair in serverRoles)
        {
            ulong playerClientId =
                playerPair.Key;


            // 해당 플레이어 찾기
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                    playerClientId,
                    out NetworkClient playerClient))
            {
                continue;
            }


            if (playerClient.PlayerObject == null)
                continue;


            // 해당 플레이어의 PlayerStateList 가져오기
            PlayerStateList playerState =
                playerClient.PlayerObject.GetComponent<PlayerStateList>();


            if (playerState == null)
                continue;


            int grade =
                playerState.cardGrade.Value;


            // 현재 서버의 모든 조커에게 전달
            foreach (var rolePair in serverRoles)
            {
                if (rolePair.Value != Role.Joker)
                    continue;


                ulong jokerClientId =
                    rolePair.Key;


                SendJokerGradeRpc(
                    playerClientId,
                    grade,
                    RpcTarget.Single(
                        jokerClientId,
                        RpcTargetUse.Temp
                    )
                );
            }
        }
    }


    // =========================================================
    // Joker : 등급 변경 실시간 전달
    // =========================================================

    public void NotifyCardGradeChanged(
        ulong playerClientId,
        int newGrade)
    {
        // 서버에서만 실행
        if (!IsServer)
            return;


        // 모든 조커에게 변경된 등급 전송
        foreach (var pair in serverRoles)
        {
            if (pair.Value != Role.Joker)
                continue;


            ulong jokerClientId =
                pair.Key;


            SendJokerGradeRpc(
                playerClientId,
                newGrade,
                RpcTarget.Single(
                    jokerClientId,
                    RpcTargetUse.Temp
                )
            );
        }
    }


    // =========================================================
    // Joker Grade RPC
    // =========================================================

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendJokerGradeRpc(
        ulong playerClientId,
        int grade,
        RpcParams rpcParams = default)
    {
        // 이 RPC를 받은 사람은 조커뿐임

        jokerVisibleGrades[playerClientId] =
            grade;


        Debug.Log(
            $"[RoleManager] " +
            $"Joker에게 전달: " +
            $"Client {playerClientId} = Grade {grade}"
        );
    }


    // =========================================================
    // Joker Grade 조회
    // =========================================================

    public bool TryGetJokerGrade(
        ulong clientId,
        out int grade)
    {
        return jokerVisibleGrades.TryGetValue(
            clientId,
            out grade
        );
    }


    // =========================================================
    // Role UI
    // =========================================================

    private void ShowRoleUI(Role role)
    {
        if (roleText == null)
            return;


        string label =
            role == Role.Joker
            ? "JOKER"
            : "CITIZEN";


        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
        }


        revealRoutine =
            StartCoroutine(
                RevealRoutine(label)
            );
    }


    private IEnumerator RevealRoutine(
        string label)
    {
        // 화면 Fade 시스템 완성 후 연결
        yield return new WaitForSecondsRealtime(
            fadeoutTime
        );


        roleText.text = label;


        yield return FadeTextRoutine(
            0f,
            1f,
            fadeDuration
        );


        yield return new WaitForSecondsRealtime(
            holdDuration
        );


        yield return FadeTextRoutine(
            1f,
            0f,
            fadeDuration
        );
    }


    private IEnumerator FadeTextRoutine(
        float from,
        float to,
        float duration)
    {
        float t = 0f;


        while (t < duration)
        {
            t += Time.unscaledDeltaTime;


            roleText.alpha =
                Mathf.Lerp(
                    from,
                    to,
                    t / duration
                );


            yield return null;
        }


        roleText.alpha = to;
    }
}