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

    private NetworkManager networkManager;

    public bool IsRunning => networkManager != null && networkManager.IsListening;
    public bool IsServer => networkManager != null && networkManager.IsServer;
    public bool IsClient => networkManager != null && networkManager.IsClient;

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

        UnityTransport transport = GetComponent<UnityTransport>();
        if (transport == null)
            transport = gameObject.AddComponent<UnityTransport>();

        networkManager = GetComponent<NetworkManager>();
        if (networkManager == null)
            networkManager = gameObject.AddComponent<NetworkManager>();

        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.NetworkConfig.ConnectionApproval = true;
        if (playerPrefab != null)
            networkManager.AddNetworkPrefab(playerPrefab);

        networkManager.ConnectionApprovalCallback = ApprovalCheck;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void Start()
    {
        if (autoStartServer)
            StartHost();
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        if (Instance == this)
            Instance = null;
    }

    public bool StartHost()
    {
        if (networkManager == null)
            return false;

        bool started = networkManager.StartHost();
        if (started)
            OnConnectionChanged?.Invoke(true);
        return started;
    }

    public bool StartClient()
    {
        if (networkManager == null)
            return false;

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
