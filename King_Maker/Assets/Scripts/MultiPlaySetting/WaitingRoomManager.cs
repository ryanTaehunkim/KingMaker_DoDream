using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 대기 씬의 진행 담당. 호스트가 게임 시작 오브젝트에 상호작용하면
// 암전 → 대기 씬 플레이어 제거 → Game 씬 로드 순서로 진행한다. (서버 권한)
// 대기 씬에 NetworkObject와 함께 한 개만 둔다.
public class WaitingRoomManager : NetworkBehaviour
{
    public static WaitingRoomManager Instance { get; private set; }

    [Header("게임 시작 조건")]
    [SerializeField] private int minPlayersToStart = 1;

    [Header("화면 전환(페이드)")]
    [SerializeField] private float startFadeDuration = 1f;

    private bool isStarting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[WaitingRoom] WaitingRoomManager가 중복으로 존재합니다.");
            return;
        }

        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    // 시작 가능 여부 (안내 문구에도 사용)
    public bool CanStart(out string reason)
    {
        reason = "";

        if (NetworkManager == null || !IsSpawned)
        {
            reason = "준비 중입니다...";
            return false;
        }

        int count = NetworkManager.ConnectedClientsIds.Count;

        if (count < minPlayersToStart)
        {
            reason = $"최소 {minPlayersToStart}명이 필요합니다. (현재 {count}명)";
            return false;
        }

        return true;
    }

    // 호스트의 GameStartInteractable이 호출한다
    public void ServerStartGame()
    {
        if (!IsServer || !IsSpawned || isStarting)
            return;

        if (!CanStart(out string reason))
        {
            Debug.LogWarning($"[WaitingRoom] 시작 불가: {reason}");
            return;
        }

        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        isStarting = true;

        // 1) 이동/공격 잠금 + 모든 화면 암전
        LockAllPlayers(true);
        FadeClientRpc(true, startFadeDuration);
        yield return new WaitForSeconds(startFadeDuration);

        if (MultiplayerSessionManager.Instance == null)
        {
            Debug.LogError("[WaitingRoom] MultiplayerSessionManager가 없습니다. MainMenu 씬에서 시작해 주세요.");
            LockAllPlayers(false);
            FadeClientRpc(false, startFadeDuration);
            isStarting = false;
            yield break;
        }

        // 2) 대기 씬 플레이어 제거 (Game 씬에서 PlayerSpawnManager가 새로 스폰한다)
        DespawnAllPlayers();
        yield return null;

        // 3) Game 씬 로드 (Game 씬의 GameFlowManager가 자동으로 게임을 시작한다)
        MultiplayerSessionManager.Instance.StartGameplay();
    }

    private void LockAllPlayers(bool locked)
    {
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerMovementLock movementLock =
                client.PlayerObject.GetComponent<PlayerMovementLock>();

            if (movementLock != null)
                movementLock.SetLockedServer(locked);
        }
    }

    private void DespawnAllPlayers()
    {
        List<NetworkClient> clients = new List<NetworkClient>(NetworkManager.ConnectedClientsList);

        foreach (NetworkClient client in clients)
        {
            NetworkObject playerObject = client.PlayerObject;

            if (playerObject != null && playerObject.IsSpawned)
                playerObject.Despawn(true);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void FadeClientRpc(bool toBlack, float duration)
    {
        if (FadeManager.Instance == null)
            return;

        if (toBlack)
        {
            FadeManager.KeepBlackForNextScene();   // 다음 씬(Game)도 검은 화면으로 시작
            FadeManager.Instance.FadeOut(duration);
        }
        else
        {
            FadeManager.Instance.FadeIn(duration);
        }
    }
}