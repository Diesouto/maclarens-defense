using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    public int CharacterIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref IsHost);
        serializer.SerializeValue(ref CharacterIndex);
    }

    public bool Equals(LobbyPlayerEntry other)
    {
        return ClientId == other.ClientId &&
               PlayerName == other.PlayerName &&
               IsReady == other.IsReady &&
               IsHost == other.IsHost &&
               CharacterIndex == other.CharacterIndex;
    }
}

public class NetworkSessionManager : NetworkBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }
    public static string LocalPlayerName => PlayerPrefs.GetString("PlayerName", "Player");
    public static int LocalCharacterIndex => PlayerPrefs.GetInt("PlayerCharacterIndex", 0);

    [SerializeField] private int maxPlayers = 4;

    public NetworkVariable<MultiplayerSessionState> SessionState = new();
    public NetworkVariable<ulong> HostClientId = new();
    public NetworkVariable<int> MaxPlayers = new();
    public NetworkList<LobbyPlayerEntry> Players = new();

    public event Action<ulong> OnPlayerJoined;
    public event Action<ulong> OnPlayerLeft;
    // public event Action OnHostChanged;
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

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        if (!IsServer)
        {
            SubmitLocalProfileServerRpc(
                new FixedString64Bytes(LocalPlayerName),
                LocalCharacterIndex);
            return;
        }

        playerLookup.Clear();
        Players.Clear();
        MaxPlayers.Value = maxPlayers;
        SetSessionState(MultiplayerSessionState.Lobby);
        HostClientId.Value = NetworkManager.Singleton.LocalClientId;
        AddOrUpdatePlayer(HostClientId.Value, "Host", true, true);
        SubmitLocalProfile(LocalPlayerName, LocalCharacterIndex);
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

    public void AddOrUpdatePlayer(ulong clientId, string playerName, bool isHost, bool isReady, int characterIndex = 0)
    {
        if (!IsServer)
            return;

        bool wasPresent = playerLookup.ContainsKey(clientId);

        LobbyPlayerEntry entry = new LobbyPlayerEntry
        {
            ClientId = clientId,
            PlayerName = playerName,
            IsReady = isReady,
            IsHost = isHost,
            CharacterIndex = characterIndex
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

    public bool TryGetPlayerProfile(ulong clientId, out string playerName, out int characterIndex)
    {
        foreach (LobbyPlayerEntry entry in Players)
        {
            if (entry.ClientId != clientId)
                continue;

            playerName = entry.PlayerName.ToString();
            characterIndex = entry.CharacterIndex;
            return true;
        }

        playerName = $"Player_{clientId}";
        characterIndex = 0;
        return false;
    }

    public void SubmitLocalProfile(string playerName, int characterIndex)
    {
        FixedString64Bytes fixedName = new FixedString64Bytes(string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim());
        if (IsServer)
            ApplyPlayerProfile(NetworkManager.Singleton.LocalClientId, fixedName, characterIndex);
        else if (IsSpawned)
            SubmitLocalProfileServerRpc(fixedName, characterIndex);
    }

    public void StartRun()
    {
        if (!CanStartRun())
            return;

        SetSessionState(MultiplayerSessionState.Starting);
        SceneEventProgressStatus loadStatus = NetworkManager.Singleton.SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
        if (loadStatus != SceneEventProgressStatus.Started)
            SetSessionState(MultiplayerSessionState.Lobby);
    }

    public void MarkRunInProgress()
    {
        if (IsServer && SessionState.Value == MultiplayerSessionState.Starting)
            SetSessionState(MultiplayerSessionState.InProgress);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RegisterLocalPlayerServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string playerName = $"Player_{clientId}";
        bool isHost = HostClientId.Value == 0 || clientId == HostClientId.Value;

        if (isHost)
            HostClientId.Value = clientId;

        AddOrUpdatePlayer(clientId, playerName, isHost, false);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetReadyServerRpc(bool isReady, RpcParams rpcParams = default)
    {
        SetReady(rpcParams.Receive.SenderClientId, isReady);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitLocalProfileServerRpc(FixedString64Bytes playerName, int characterIndex, RpcParams rpcParams = default)
    {
        ApplyPlayerProfile(rpcParams.Receive.SenderClientId, playerName, characterIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartRunServerRpc(RpcParams rpcParams = default)
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
        if (playerLookup.ContainsKey(clientId))
            return;

        AddOrUpdatePlayer(clientId, isHost ? "Host" : $"Player_{clientId}", isHost, false);
    }

    private void ApplyPlayerProfile(ulong clientId, FixedString64Bytes playerName, int characterIndex)
    {
        if (!IsServer)
            return;

        // The client's profile RPC can arrive before OnClientConnectedCallback registers it.
        if (!playerLookup.TryGetValue(clientId, out LobbyPlayerEntry entry))
        {
            if (SessionState.Value != MultiplayerSessionState.Lobby)
                return;

            bool isHost = clientId == HostClientId.Value;
            entry = new LobbyPlayerEntry { ClientId = clientId, IsHost = isHost, IsReady = isHost };
        }

        string cleanedName = playerName.ToString().Trim();
        if (string.IsNullOrWhiteSpace(cleanedName))
            cleanedName = $"Player_{clientId}";
        if (cleanedName.Length > 24)
            cleanedName = cleanedName.Substring(0, 24);

        entry.PlayerName = new FixedString64Bytes(cleanedName);
        entry.CharacterIndex = Mathf.Clamp(characterIndex, 0, 31);
        playerLookup[clientId] = entry;
        RebuildNetworkList();
        OnPlayersChanged?.Invoke();
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
