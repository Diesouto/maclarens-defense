using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool autoSpawnOnClientConnect = true;

    private readonly Dictionary<ulong, GameObject> spawnedPlayers = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnForClient(clientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    public void SetPlayerPrefab(GameObject prefab)
    {
        playerPrefab = prefab;
    }

    public void SetSpawnPoints(Transform[] points)
    {
        spawnPoints = points;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!autoSpawnOnClientConnect)
            return;

        SpawnForClient(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (spawnedPlayers.TryGetValue(clientId, out GameObject spawnedPlayer))
        {
            if (spawnedPlayer != null)
            {
                PlayerBody body = spawnedPlayer.GetComponent<PlayerBody>();
                if (body != null && body.IsDead)
                    BodyRecoveryManager.Instance?.MarkLost(body);

                NetworkPlayer networkPlayer = spawnedPlayer.GetComponent<NetworkPlayer>();
                networkPlayer?.MarkAbandoned();

                if (spawnedPlayer.TryGetComponent(out NetworkObject networkObject))
                {
                    if (networkObject.IsSpawned && networkObject.IsServer)
                        networkObject.Despawn();
                }
                else
                {
                    Destroy(spawnedPlayer);
                }
            }

            spawnedPlayers.Remove(clientId);
        }

    }

    private void SpawnForClient(ulong clientId)
    {
        if (!IsServer)
            return;

        if (playerPrefab == null)
        {
            Debug.LogWarning("NetworkPlayerSpawner: missing player prefab.");
            return;
        }

        if (spawnedPlayers.ContainsKey(clientId))
            return;

        Vector3 spawnPosition = GetSpawnPosition(clientId);
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Destroy(playerInstance);
            Debug.LogWarning("NetworkPlayerSpawner: prefab is missing NetworkObject.");
            return;
        }

        if (!NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(networkObject.GlobalObjectIdHash))
        {
            Destroy(playerInstance);
            Debug.LogError($"NetworkPlayerSpawner: prefab '{playerPrefab.name}' is not registered in NetworkPrefabs.", playerPrefab);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = playerInstance;

        if (NetworkSessionManager.Instance != null)
        {
            bool isHost = NetworkSessionManager.Instance.HostClientId.Value == clientId;
            NetworkSessionManager.Instance.AddOrUpdatePlayer(clientId, $"Player_{clientId}", isHost, false);
        }
    }

    private Vector3 GetSpawnPosition(ulong clientId)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = (int)(clientId % (ulong)spawnPoints.Length);
            return spawnPoints[index].position;
        }

        return Vector3.zero;
    }
}
