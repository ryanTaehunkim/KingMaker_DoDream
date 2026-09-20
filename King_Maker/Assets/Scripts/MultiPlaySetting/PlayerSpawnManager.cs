using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[PlayerSpawn] NetworkManager가 없습니다.");
            return;
        }

        // Host / Server만 Spawn을 담당한다.
        if (!NetworkManager.Singleton.IsServer)
            return;

        SpawnConnectedPlayers();
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
                "[PlayerSpawn] Player Prefab이 지정되지 않았습니다."
            );
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[PlayerSpawn] Spawn Point가 없습니다."
            );
            return;
        }

        // 이미 Player Object가 있는 Client라면 다시 Spawn하지 않는다.
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                Debug.Log(
                    $"[PlayerSpawn] Client {clientId}는 이미 Player가 있습니다."
                );

                return;
            }
        }

        Transform spawnPoint =
            spawnPoints[spawnIndex % spawnPoints.Length];

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
            $"[PlayerSpawn] Player Spawn 완료 - " +
            $"ClientId: {clientId}, " +
            $"Position: {spawnPoint.position}"
        );
    }
}