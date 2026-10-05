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
            Debug.LogError("[PlayerSpawn] NetworkManager is missing.");
            return;
        }

        // Only Host / Server handles player spawning.
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

        // Do not spawn again if the Client already has a Player Object.
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                Debug.Log(
                    $"[PlayerSpawn] Client {clientId} already has a Player Object."
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
            $"[PlayerSpawn] Player Spawned - " +
            $"ClientId: {clientId}, " +
            $"Position: {spawnPoint.position}"
        );
    }
}