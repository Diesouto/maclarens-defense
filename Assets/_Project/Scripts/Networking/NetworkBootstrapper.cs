using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkBootstrapper : MonoBehaviour
{
    public static NetworkBootstrapper Instance { get; private set; }

    [SerializeField] private bool autoStartServer = false;
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    private bool isConfigured;
    private GameObject runtimeNetworkRoot;

    public bool IsRunning => networkManager != null && networkManager.IsListening;
    public bool IsServer => networkManager != null && networkManager.IsServer;
    public bool IsClient => networkManager != null && networkManager.IsClient;
    public UnityTransport Transport => transport;

    public event Action<bool> OnConnectionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (networkManager == null)
            networkManager = GetComponent<NetworkManager>();
        if (transport == null)
            transport = networkManager != null ? networkManager.GetComponent<UnityTransport>() : GetComponent<UnityTransport>();

        if (networkManager == null)
        {
            runtimeNetworkRoot = new GameObject("NetworkRuntime");
            DontDestroyOnLoad(runtimeNetworkRoot);
            if (transport == null)
                transport = runtimeNetworkRoot.AddComponent<UnityTransport>();
            networkManager = runtimeNetworkRoot.AddComponent<NetworkManager>();
        }

        if (transport == null)
            transport = networkManager.gameObject.AddComponent<UnityTransport>();

        if (networkManager.NetworkConfig == null)
        {
            Debug.LogError("NetworkBootstrapper: NetworkManager has no NetworkConfig.", networkManager);
            enabled = false;
            return;
        }

        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.NetworkConfig.ConnectionApproval = true;
        if (playerPrefab == null)
        {
            Debug.LogError("NetworkBootstrapper: assign the networked player prefab.", this);
            enabled = false;
            return;
        }

        if (networkManager.NetworkConfig.Prefabs == null)
        {
            Debug.LogError("NetworkBootstrapper: NetworkConfig has no prefab registry.", networkManager);
            enabled = false;
            return;
        }

        try
        {
            if (!networkManager.NetworkConfig.Prefabs.Contains(playerPrefab))
                networkManager.AddNetworkPrefab(playerPrefab);
        }
        catch (Exception exception)
        {
            Debug.LogError($"NetworkBootstrapper: could not register player prefab: {exception.Message}", this);
            enabled = false;
            return;
        }

        networkManager.ConnectionApprovalCallback = ApprovalCheck;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        isConfigured = true;
    }

    private void Start()
    {
        if (autoStartServer && isConfigured)
            StartHost();
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            if (networkManager.IsListening)
                networkManager.Shutdown();
        }

        if (runtimeNetworkRoot != null)
            Destroy(runtimeNetworkRoot);

        if (Instance == this)
            Instance = null;
    }

    public bool StartHost()
    {
        if (!isConfigured || networkManager == null)
        {
            Debug.LogError("NetworkBootstrapper: cannot start host because network references are not configured.", this);
            return false;
        }

        bool started = networkManager.StartHost();
        if (started)
            OnConnectionChanged?.Invoke(true);
        return started;
    }

    public bool StartClient()
    {
        if (!isConfigured || networkManager == null)
        {
            Debug.LogError("NetworkBootstrapper: cannot start client because network references are not configured.", this);
            return false;
        }

        bool started = networkManager.StartClient();
        if (started)
            OnConnectionChanged?.Invoke(true);
        return started;
    }

    public void Shutdown()
    {
        if (networkManager == null)
            return;

        if (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
            networkManager.Shutdown();

        OnConnectionChanged?.Invoke(false);
    }

    public void SetMaxPlayers(int requestedMaxPlayers)
    {
        maxPlayers = Mathf.Clamp(requestedMaxPlayers, 1, 4);
        if (networkManager != null)
            networkManager.ConnectionApprovalCallback = ApprovalCheck;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager != null && networkManager.IsServer)
            return;

        OnConnectionChanged?.Invoke(false);
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = false;
        response.PlayerPrefabHash = 0;
        response.Position = Vector3.zero;
        response.Rotation = Quaternion.identity;

        if (NetworkSessionManager.Instance != null)
        {
            response.Approved = NetworkSessionManager.Instance.SessionState.Value == MultiplayerSessionState.Lobby &&
                NetworkSessionManager.Instance.GetPlayerCount() < maxPlayers;
        }
    }
}
