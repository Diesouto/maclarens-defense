using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool autoSpawnOnClientConnect = true;

    private readonly Dictionary<ulong, GameObject> spawnedPlayers = new();
    private NetworkManager networkManager;
    private GameObject offlinePlayer;
    private bool ownsOfflinePlayer;
    private bool callbacksRegistered;

    private void Start()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("NetworkPlayerSpawner: assign the Player prefab.", this);
            return;
        }

        networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            SpawnOfflinePlayer();
            return;
        }

        if (!networkManager.IsServer)
            return;

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        networkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
        callbacksRegistered = true;

        if (SceneManager.GetActiveScene().name == "MainScene")
            BeginGameplaySpawns();
    }

    private void OnDestroy()
    {
        if (callbacksRegistered && networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            if (networkManager.SceneManager != null)
                networkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
        }

        if (ownsOfflinePlayer && offlinePlayer != null)
            Destroy(offlinePlayer);
    }

    public void SetPlayerPrefab(GameObject prefab)
    {
        playerPrefab = prefab;
    }

    public void SetSpawnPoints(Transform[] points)
    {
        spawnPoints = points;
    }

    private void SpawnOfflinePlayer()
    {
        if (offlinePlayer != null)
            return;

        NetworkPlayer placedPlayer = FindFirstObjectByType<NetworkPlayer>();
        if (placedPlayer != null && placedPlayer.gameObject.scene == gameObject.scene && !placedPlayer.IsSpawned)
        {
            offlinePlayer = placedPlayer.gameObject;
        }
        else
        {
            offlinePlayer = Instantiate(playerPrefab, GetSpawnPosition(0), Quaternion.identity);
            ownsOfflinePlayer = true;
        }

        if (offlinePlayer.TryGetComponent(out NetworkPlayer player))
        {
            player.ConfigureOfflinePlayer(
                PlayerPrefs.GetString("PlayerName", "Player"),
                PlayerPrefs.GetInt("PlayerCharacterIndex", 0));
            if (Camera.main == null)
                Debug.LogError("NetworkPlayerSpawner: offline player spawned, but MainScene has no enabled Camera tagged MainCamera.", offlinePlayer);
            else
                Debug.Log($"NetworkPlayerSpawner: spawned offline Player at {offlinePlayer.transform.position}; output camera is '{Camera.main.name}'.", offlinePlayer);
        }
        else
        {
            Debug.LogError("NetworkPlayerSpawner: Player prefab is missing NetworkPlayer.", offlinePlayer);
        }
    }

    private void BeginGameplaySpawns()
    {
        if (networkManager == null || !networkManager.IsServer)
            return;

        NetworkSessionManager.Instance?.MarkRunInProgress();
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
            SpawnForClient(clientId);
    }

    private void HandleLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (sceneName == "MainScene")
            BeginGameplaySpawns();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!autoSpawnOnClientConnect || SceneManager.GetActiveScene().name != "MainScene")
            return;

        if (NetworkSessionManager.Instance != null &&
            NetworkSessionManager.Instance.SessionState.Value != MultiplayerSessionState.InProgress)
            return;

        SpawnForClient(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!spawnedPlayers.TryGetValue(clientId, out GameObject playerObject))
            return;

        if (playerObject != null)
        {
            PlayerBody body = playerObject.GetComponent<PlayerBody>();
            if (body != null && body.IsDead)
                BodyRecoveryManager.Instance?.MarkLost(body);

            NetworkPlayer networkPlayer = playerObject.GetComponent<NetworkPlayer>();
            networkPlayer?.MarkAbandoned();

            if (playerObject.TryGetComponent(out NetworkObject networkObject) &&
                networkObject.IsSpawned && networkManager != null && networkManager.IsServer)
                networkObject.Despawn();
        }

        spawnedPlayers.Remove(clientId);
    }

    private void SpawnForClient(ulong clientId)
    {
        if (networkManager == null || !networkManager.IsServer || playerPrefab == null || spawnedPlayers.ContainsKey(clientId))
            return;

        if (!networkManager.NetworkConfig.Prefabs.Contains(playerPrefab))
        {
            Debug.LogError($"NetworkPlayerSpawner: Player prefab '{playerPrefab.name}' is not registered in NetworkPrefabs.", playerPrefab);
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(spawnedPlayers.Count);
        GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        if (!playerObject.TryGetComponent(out NetworkObject networkObject))
        {
            Destroy(playerObject);
            Debug.LogError("NetworkPlayerSpawner: Player prefab is missing NetworkObject.", playerPrefab);
            return;
        }

        string playerName = $"Player_{clientId}";
        int characterIndex = 0;
        NetworkSessionManager session = NetworkSessionManager.Instance;
        if (session != null && !session.TryGetPlayerProfile(clientId, out playerName, out characterIndex))
        {
            session.AddOrUpdatePlayer(clientId, playerName, session.HostClientId.Value == clientId, false, characterIndex);
        }

        networkObject.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = playerObject;

        if (playerObject.TryGetComponent(out NetworkPlayer networkPlayer))
            networkPlayer.SetServerProfile(playerName, characterIndex);

        Debug.Log($"NetworkPlayerSpawner: spawned network player for client {clientId} at {spawnPosition}.", playerObject);
    }

    private Vector3 GetSpawnPosition(int playerIndex)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = playerIndex % spawnPoints.Length;
            if (spawnPoints[index] != null)
                return spawnPoints[index].position;
        }

        GameObject respawnPoint = GameObject.Find("RespawnPoint");
        Vector3 origin = respawnPoint == null ? Vector3.zero : respawnPoint.transform.position;
        return origin + Vector3.right * (playerIndex * 2f);
    }
}
