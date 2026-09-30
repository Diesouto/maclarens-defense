using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        if (NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
        spawnedPlayers.Clear();
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
        if (!autoSpawnOnClientConnect || NetworkSessionManager.Instance == null ||
            NetworkSessionManager.Instance.SessionState.Value != MultiplayerSessionState.InProgress ||
            SceneManager.GetActiveScene().name != "MainScene")
            return;

        SpawnForClient(clientId);
    }

    private void HandleLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer || sceneName != "MainScene")
            return;

        NetworkSessionManager.Instance?.MarkRunInProgress();
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
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
                    if (networkObject.IsSpawned && NetworkManager.Singleton != null &&
                        NetworkManager.Singleton.IsServer)
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

        Vector3 spawnPosition = GetSpawnPosition(spawnedPlayers.Count);
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Destroy(playerInstance);
            Debug.LogWarning("NetworkPlayerSpawner: prefab is missing NetworkObject.");
            return;
        }

        string playerName = $"Player_{clientId}";
        int characterIndex = 0;
        NetworkSessionManager sessionManager = NetworkSessionManager.Instance;
        if (sessionManager != null && !sessionManager.TryGetPlayerProfile(clientId, out playerName, out characterIndex))
        {
            sessionManager.AddOrUpdatePlayer(
                clientId,
                playerName,
                sessionManager.HostClientId.Value == clientId,
                false,
                characterIndex);
        }

        if (playerInstance.TryGetComponent(out NetworkPlayer networkPlayer))
            networkPlayer.SetServerProfile(playerName, characterIndex);

        if (!NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(playerPrefab))
        {
            Destroy(playerInstance);
            Debug.LogError($"NetworkPlayerSpawner: prefab '{playerPrefab.name}' is not registered in NetworkPrefabs.", playerPrefab);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = playerInstance;
    }

    private Vector3 GetSpawnPosition(int playerIndex)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = playerIndex % spawnPoints.Length;
            return spawnPoints[index].position;
        }

        GameObject respawnPoint = GameObject.Find("RespawnPoint");
        Vector3 origin = respawnPoint == null ? Vector3.zero : respawnPoint.transform.position;
        return origin + Vector3.right * (playerIndex * 2f);
    }
}
