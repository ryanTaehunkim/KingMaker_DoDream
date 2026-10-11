using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerStateListUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform content;
    [SerializeField] private PlayerEntryUI playerEntryPrefab;


    private readonly Dictionary<ulong, PlayerEntryUI> entries =
        new Dictionary<ulong, PlayerEntryUI>();


    private PlayerStateList localPlayerState;
    private RoleManager roleManager;


    private IEnumerator Start()
    {
        // Local Player가 생성될 때까지 기다림
        while (
            NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClient == null ||
            NetworkManager.Singleton.LocalClient.PlayerObject == null
        )
        {
            yield return null;
        }


        // Local Player의 PlayerStateList
        localPlayerState =
            NetworkManager.Singleton
                .LocalClient
                .PlayerObject
                .GetComponent<PlayerStateList>();


        // RoleManager 찾기
        roleManager =
            FindFirstObjectByType<RoleManager>();


        // RoleManager가 준비될 때까지 기다림
        while (
            roleManager == null ||
            roleManager.myRole == Role.None
        )
        {
            roleManager =
                FindFirstObjectByType<RoleManager>();

            yield return null;
        }


        // 최초 목록 생성
        RefreshList();


        // 시민은 자기 등급 변경을 직접 읽을 수 있으므로
        // 자신의 등급 변경 이벤트 연결
        if (localPlayerState != null)
        {
            localPlayerState.cardGrade.OnValueChanged +=
                OnMyGradeChanged;
        }
    }


    private void OnDestroy()
    {
        if (localPlayerState != null)
        {
            localPlayerState.cardGrade.OnValueChanged -=
                OnMyGradeChanged;
        }
    }


    // =========================================================
    // Player List 생성
    // =========================================================

    private void RefreshList()
    {
        // 기존 UI 삭제
        foreach (var entry in entries.Values)
        {
            if (entry != null)
            {
                Destroy(entry.gameObject);
            }
        }

        entries.Clear();


        // 현재 씬의 모든 PlayerStateList 찾기
        PlayerStateList[] playerStates =
            FindObjectsByType<PlayerStateList>(
                FindObjectsSortMode.None
            );


        foreach (PlayerStateList playerState in playerStates)
        {
            NetworkObject networkObject =
                playerState.GetComponent<NetworkObject>();


            if (networkObject == null)
                continue;


            ulong clientId =
                networkObject.OwnerClientId;


            // Player Entry 생성
            PlayerEntryUI entry =
                Instantiate(
                    playerEntryPrefab,
                    content
                );


            // -------------------------------------------------
            // Joker
            // -------------------------------------------------

            if (roleManager.IsLocalJoker)
            {
                // 조커는 모든 플레이어의 등급을 볼 수 있음
                if (roleManager.TryGetJokerGrade(
                        clientId,
                        out int grade))
                {
                    entry.SetData(
                        $"Player {clientId}",
                        true,
                        grade
                    );
                }
                else
                {
                    // 아직 등급 정보를 못 받은 경우
                    entry.SetData(
                        $"Player {clientId}",
                        false,
                        0
                    );
                }
            }


            // -------------------------------------------------
            // Citizen
            // -------------------------------------------------

            else
            {
                // 자기 자신인지 확인
                bool isMine =
                    clientId ==
                    NetworkManager.Singleton.LocalClientId;


                if (isMine)
                {
                    // 시민은 자기 등급만 볼 수 있음
                    entry.SetData(
                        $"Player {clientId}",
                        true,
                        localPlayerState.cardGrade.Value
                    );
                }
                else
                {
                    // 다른 플레이어의 등급은 숨김
                    entry.SetData(
                        $"Player {clientId}",
                        false,
                        0
                    );
                }
            }


            entries[clientId] = entry;
        }
    }


    // =========================================================
    // 내 등급 변경
    // =========================================================

    private void OnMyGradeChanged(
        int oldValue,
        int newValue)
    {
        if (roleManager == null)
            return;


        // 조커는 RoleManager에서 등급 변경을 받음
        if (roleManager.IsLocalJoker)
            return;


        ulong localClientId =
            NetworkManager.Singleton.LocalClientId;


        if (entries.TryGetValue(
                localClientId,
                out PlayerEntryUI entry))
        {
            entry.SetGrade(newValue);
        }
    }
}