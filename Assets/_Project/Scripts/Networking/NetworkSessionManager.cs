using System;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum MultiplayerSessionState
{
    Lobby,
    Starting,
    InProgress,
    Ended
}

public struct LobbyPlayerEntry : INetworkSerializable, IEquatable<LobbyPlayerEntry>
{
    public ulong ClientId;
    public FixedString64Bytes PlayerName;
    public bool IsReady;
    public bool IsHost;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref IsHost);
    }

    public bool Equals(LobbyPlayerEntry other)
    {
        return ClientId == other.ClientId &&
               PlayerName == other.PlayerName &&
               IsReady == other.IsReady &&
               IsHost == other.IsHost;
    }
}

public class NetworkSessionManager : NetworkBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }

    [SerializeField] private int maxPlayers = 4;

    public NetworkVariable<MultiplayerSessionState> SessionState = new();
    public NetworkVariable<ulong> HostClientId = new();
    public NetworkVariable<int> MaxPlayers = new();
    public NetworkList<LobbyPlayerEntry> Players = new();

    public event Action<ulong> OnPlayerJoined;
    public event Action<ulong> OnPlayerLeft;
    public event Action OnHostChanged;
    public event Action OnSessionClosed;
    public event Action OnPlayersChanged;
    public event Action<MultiplayerSessionState, MultiplayerSessionState> OnSessionStateChanged;

    private readonly Dictionary<ulong, LobbyPlayerEntry> playerLookup = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        if (!IsServer)
            return;

        MaxPlayers.Value = maxPlayers;
        SetSessionState(MultiplayerSessionState.Lobby);
        HostClientId.Value = NetworkManager.Singleton.LocalClientId;
        AddOrUpdatePlayer(NetworkManager.Singleton.LocalClientId, "Host", true, false);
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    public bool IsHostClient(ulong clientId)
    {
        return HostClientId.Value == clientId;
    }

    public int GetPlayerCount()
    {
        return Players.Count;
    }

    public void ConfigureSession(int requestedMaxPlayers)
    {
        if (!IsServer)
            return;

        maxPlayers = Mathf.Clamp(requestedMaxPlayers, 1, 4);
        MaxPlayers.Value = maxPlayers;
    }

    public void AddOrUpdatePlayer(ulong clientId, string playerName, bool isHost, bool isReady)
    {
        if (!IsServer)
            return;

        bool wasPresent = playerLookup.ContainsKey(clientId);

        LobbyPlayerEntry entry = new LobbyPlayerEntry
        {
            ClientId = clientId,
            PlayerName = playerName,
            IsReady = isReady,
            IsHost = isHost
        };

        playerLookup[clientId] = entry;
        RebuildNetworkList();

        if (HostClientId.Value == 0 && isHost)
            HostClientId.Value = clientId;

        if (!wasPresent)
            OnPlayerJoined?.Invoke(clientId);

        OnPlayersChanged?.Invoke();
    }

    public void RemovePlayer(ulong clientId)
    {
        if (!IsServer)
            return;

        if (playerLookup.Remove(clientId))
        {
            RebuildNetworkList();
            OnPlayerLeft?.Invoke(clientId);
            OnPlayersChanged?.Invoke();
        }

        if (HostClientId.Value == clientId)
        {
            ulong nextHost = 0;
            foreach (ulong key in playerLookup.Keys)
            {
                if (key == clientId)
                    continue;

                nextHost = key;
                break;
            }

            if (nextHost != 0)
            {
                SetSessionState(MultiplayerSessionState.Ended);
                OnSessionClosed?.Invoke();
            }
            else
            {
                SetSessionState(MultiplayerSessionState.Ended);
                OnSessionClosed?.Invoke();
            }
        }
    }

    public void SetReady(ulong clientId, bool isReady)
    {
        if (!IsServer || !playerLookup.TryGetValue(clientId, out LobbyPlayerEntry entry))
            return;

        entry.IsReady = isReady;
        playerLookup[clientId] = entry;
        RebuildNetworkList();
        OnPlayersChanged?.Invoke();
    }

    public void StartRun()
    {
        if (!CanStartRun())
            return;

        SetSessionState(MultiplayerSessionState.Starting);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RegisterLocalPlayerServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string playerName = $"Player_{clientId}";
        bool isHost = HostClientId.Value == 0 || clientId == HostClientId.Value;

        if (isHost)
            HostClientId.Value = clientId;

        AddOrUpdatePlayer(clientId, playerName, isHost, false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool isReady, ServerRpcParams rpcParams = default)
    {
        SetReady(rpcParams.Receive.SenderClientId, isReady);
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartRunServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer || !IsHostClient(rpcParams.Receive.SenderClientId))
            return;

        StartRun();
    }

    public bool CanStartRun()
    {
        if (!IsServer || SessionState.Value != MultiplayerSessionState.Lobby || playerLookup.Count == 0)
            return false;

        foreach (LobbyPlayerEntry entry in playerLookup.Values)
        {
            if (!entry.IsReady)
                return false;
        }

        return true;
    }

    public void SetSessionState(MultiplayerSessionState nextState)
    {
        if (!IsServer || SessionState.Value == nextState)
            return;

        MultiplayerSessionState previous = SessionState.Value;
        SessionState.Value = nextState;
        OnSessionStateChanged?.Invoke(previous, nextState);
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer || SessionState.Value != MultiplayerSessionState.Lobby)
            return;

        bool isHost = clientId == HostClientId.Value;
        AddOrUpdatePlayer(clientId, isHost ? "Host" : $"Player_{clientId}", isHost, false);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (IsServer)
            RemovePlayer(clientId);
    }

    private void RebuildNetworkList()
    {
        if (!IsServer)
            return;

        Players.Clear();

        foreach (LobbyPlayerEntry entry in playerLookup.Values)
            Players.Add(entry);
    }
}
