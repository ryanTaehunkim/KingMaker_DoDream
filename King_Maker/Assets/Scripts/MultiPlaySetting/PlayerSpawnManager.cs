using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    [Header("Player")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    // 서버 전용: 각 클라이언트에게 배정한 스폰 위치
    private readonly Dictionary<ulong, Transform> assignedSpawns =
        new Dictionary<ulong, Transform>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[PlayerSpawn] NetworkManager is missing.");
            return;
        }

        // Only Host / Server handles player spawning.
        if (!NetworkManager.Singleton.IsServer)
            return;

        SpawnConnectedPlayers();
    }

    // 플레이어 쪽(PlayerControlForNetwork)에서 "내 스폰 위치"를 물어볼 때 사용 (서버에서만 호출)
    public bool TryGetSpawnPoint(
        ulong clientId,
        out Vector3 position,
        out Quaternion rotation)
    {
        if (assignedSpawns.TryGetValue(clientId, out Transform point) &&
            point != null)
        {
            position = point.position;
            rotation = point.rotation;
            return true;
        }

        position = default;
        rotation = default;
        return false;
    }

    private void SpawnConnectedPlayers()
    {
        var clientIds = NetworkManager.Singleton.ConnectedClientsIds;

        for (int i = 0; i < clientIds.Count; i++)
        {
            SpawnPlayer(clientIds[i], i);
        }
    }

    private void SpawnPlayer(ulong clientId, int spawnIndex)
    {
        if (playerPrefab == null)
        {
            Debug.LogError(
                "[PlayerSpawn] Player Prefab is not assigned."
            );
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[PlayerSpawn] No Spawn Points are assigned."
            );
            return;
        }

        Transform spawnPoint =
            spawnPoints[spawnIndex % spawnPoints.Length];

        // 스폰 위치를 먼저 기록해 둔다. (클라이언트가 스폰 직후 물어볼 수 있도록)
        assignedSpawns[clientId] = spawnPoint;

        // Do not spawn again if the Client already has a Player Object.
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                Debug.Log(
                    $"[PlayerSpawn] Client {clientId} already has a Player Object. " +
                    "스폰 위치로만 이동시킵니다."
                );

                MoveExistingPlayer(client.PlayerObject, clientId, spawnPoint);
                return;
            }
        }

        NetworkObject player =
            Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

        player.SpawnAsPlayerObject(
            clientId,
            true
        );

        Debug.Log(
            $"[PlayerSpawn] Player Spawned - " +
            $"ClientId: {clientId}, " +
            $"Position: {spawnPoint.position}"
        );
    }

    // 이미 플레이어가 있는 경우, 새로 만들지 않고 스폰 위치로만 옮긴다.
    private void MoveExistingPlayer(
        NetworkObject playerObject,
        ulong clientId,
        Transform spawnPoint)
    {
        PlayerControlForNetwork control =
            playerObject.GetComponent<PlayerControlForNetwork>();

        if (control == null)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // 호스트 본인
            control.TeleportTo(spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            control.TeleportOwnerRpc(
                spawnPoint.position,
                spawnPoint.rotation,
                NetworkManager.Singleton.RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }
}