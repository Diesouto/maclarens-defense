using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerMenuController : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private GameObject mainMenuScreen;
    [SerializeField] private GameObject multiplayerScreen;
    [SerializeField] private GameObject characterScreen;
    [SerializeField] private GameObject joinScreen;
    [SerializeField] private GameObject lobbyScreen;

    [Header("Main Menu")]
    [SerializeField] private Button singlePlayerButton;
    [SerializeField] private Button openMultiplayerButton;

    [Header("Multiplayer Profile")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button previousCharacterButton;
    [SerializeField] private TMP_Text characterIndexText;
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private GameObject playerPreviewPrefab;
    [SerializeField] private Transform modelPosition;

    [Header("Multiplayer Actions")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button openCharacterScreenButton;
    [SerializeField] private Button multiplayerBackButton;
    [SerializeField] private Button characterBackButton;

    [Header("Join Screen")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button joinBackButton;

    [Header("Lobby")]
    [SerializeField] private TMP_Text lobbyStatusText;
    [SerializeField] private TMP_Text lobbyJoinCodeText;
    [SerializeField] private TMP_Text lobbyPlayerCountText;
    [SerializeField] private TMP_Text lobbyPlayerListText;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;

    [Header("Networking")]
    [SerializeField] private RelayJoinCodeManager relayManager;

    private const string PlayerNameKey = "PlayerName";
    private const string CharacterIndexKey = "PlayerCharacterIndex";
    private const int MaximumNameLength = 24;
    private bool isConnecting;
    private bool networkCallbacksBound;
    private string activeJoinCode = string.Empty;
    private NetworkSessionManager boundSession;
    private GameObject previewRoot;
    private GameObject previewInstance;
    private Transform[] previewModels = System.Array.Empty<Transform>();

    private void Awake()
    {
        if (relayManager == null)
            relayManager = RelayJoinCodeManager.Instance;

        playerNameInput?.SetTextWithoutNotify(PlayerPrefs.GetString(PlayerNameKey, "Player"));
        CreateCharacterPreview();
        ApplyCharacterIndex(PlayerPrefs.GetInt(CharacterIndexKey, 0));
        ValidateReferences();
        BindButtons();
    }

    private void Update()
    {
        if (lobbyScreen != null && lobbyScreen.activeSelf)
            RefreshLobby();
    }

    private void BindButtons()
    {
        AddListener(singlePlayerButton, LoadSinglePlayer);
        AddListener(openMultiplayerButton, ShowMultiplayerScreen);
        AddListener(previousCharacterButton, () => ChangeCharacter(-1));
        AddListener(nextCharacterButton, () => ChangeCharacter(1));
        AddListener(hostButton, () => _ = CreateHostAsync());
        AddListener(joinButton, ShowJoinScreen);
        AddListener(openCharacterScreenButton, ShowCharacterScreen);
        AddListener(multiplayerBackButton, ShowMainMenuScreen);
        AddListener(characterBackButton, ShowMultiplayerScreen);
        AddListener(connectButton, () => _ = JoinHostAsync());
        AddListener(joinBackButton, ShowMultiplayerScreen);
        AddListener(readyButton, ToggleReady);
        AddListener(startGameButton, StartGame);
        AddListener(leaveLobbyButton, LeaveLobby);
        playerNameInput?.onEndEdit.AddListener(SavePlayerName);
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }

    private void ValidateReferences()
    {
        Require(mainMenuScreen, nameof(mainMenuScreen));
        Require(multiplayerScreen, nameof(multiplayerScreen));
        Require(characterScreen, nameof(characterScreen));
        Require(joinScreen, nameof(joinScreen));
        Require(lobbyScreen, nameof(lobbyScreen));
        Require(singlePlayerButton, nameof(singlePlayerButton));
        Require(openMultiplayerButton, nameof(openMultiplayerButton));
        Require(playerNameInput, nameof(playerNameInput));
        Require(previousCharacterButton, nameof(previousCharacterButton));
        Require(characterIndexText, nameof(characterIndexText));
        Require(nextCharacterButton, nameof(nextCharacterButton));
        Require(playerPreviewPrefab, nameof(playerPreviewPrefab));
        Require(modelPosition, nameof(modelPosition));
        Require(hostButton, nameof(hostButton));
        Require(joinButton, nameof(joinButton));
        Require(openCharacterScreenButton, nameof(openCharacterScreenButton));
        Require(multiplayerBackButton, nameof(multiplayerBackButton));
        Require(characterBackButton, nameof(characterBackButton));
        Require(joinCodeInput, nameof(joinCodeInput));
        Require(connectButton, nameof(connectButton));
        Require(joinBackButton, nameof(joinBackButton));
        Require(lobbyStatusText, nameof(lobbyStatusText));
        Require(lobbyJoinCodeText, nameof(lobbyJoinCodeText));
        Require(lobbyPlayerCountText, nameof(lobbyPlayerCountText));
        Require(lobbyPlayerListText, nameof(lobbyPlayerListText));
        Require(readyButton, nameof(readyButton));
        Require(readyButtonText, nameof(readyButtonText));
        Require(startGameButton, nameof(startGameButton));
        Require(leaveLobbyButton, nameof(leaveLobbyButton));
        if (relayManager == null)
            Debug.LogError($"{nameof(MultiplayerMenuController)}: assign {nameof(relayManager)}.", this);
    }

    private void Require(UnityEngine.Object reference, string fieldName)
    {
        if (reference == null)
            Debug.LogError($"{nameof(MultiplayerMenuController)}: assign {fieldName} in the Inspector.", this);
    }

    private void LoadSinglePlayer()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            SetLobbyStatus("Leave the multiplayer session before starting a single-player run.");
            ShowLobbyScreen();
            return;
        }

        if (NetworkBootstrapper.Instance != null)
        {
            GameObject servicesRoot = NetworkBootstrapper.Instance.gameObject;
            NetworkBootstrapper.Instance.Shutdown();
            Destroy(servicesRoot);
        }

        SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
    }

    private void ShowMainMenuScreen()
    {
        SetScreen(mainMenuScreen);
    }

    private void ShowMultiplayerScreen()
    {
        SetScreen(multiplayerScreen);
    }

    private void ShowJoinScreen()
    {
        SetScreen(joinScreen);
    }

    private void ShowCharacterScreen()
    {
        SetScreen(characterScreen);
    }

    private void ShowLobbyScreen()
    {
        SetScreen(lobbyScreen);
    }

    private void SetScreen(GameObject selectedScreen)
    {
        SetActive(mainMenuScreen, selectedScreen == mainMenuScreen);
        SetActive(multiplayerScreen, selectedScreen == multiplayerScreen);
        SetActive(characterScreen, selectedScreen == characterScreen);
        SetActive(joinScreen, selectedScreen == joinScreen);
        SetActive(lobbyScreen, selectedScreen == lobbyScreen);
        if (previewRoot != null)
            previewRoot.SetActive(selectedScreen == characterScreen);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private async Task CreateHostAsync()
    {
        if (isConnecting)
            return;

        if (relayManager == null)
        {
            SetLobbyStatus("RelayManager is not assigned to the menu.");
            ShowLobbyScreen();
            return;
        }

        isConnecting = true;
        activeJoinCode = string.Empty;
        SetLobbyStatus("Creating Relay room...");
        lobbyJoinCodeText.text = string.Empty;
        ShowLobbyScreen();

        try
        {
            activeJoinCode = await relayManager.StartHostAsync(4);
            lobbyJoinCodeText.text = $"Join code: {activeJoinCode}";
            SetLobbyStatus("Room created. Waiting for players.");
            BindSession();
            RefreshLobby();
        }
        catch (Exception exception)
        {
            SetLobbyStatus($"Host failed: {exception.Message}");
            Debug.LogException(exception, this);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private async Task JoinHostAsync()
    {
        if (isConnecting)
            return;

        if (relayManager == null)
        {
            SetLobbyStatus("RelayManager is not assigned to the menu.");
            ShowLobbyScreen();
            return;
        }

        isConnecting = true;
        activeJoinCode = joinCodeInput == null ? string.Empty : joinCodeInput.text.Trim().ToUpperInvariant();
        SetLobbyStatus("Joining Relay room...");
        lobbyJoinCodeText.text = string.IsNullOrEmpty(activeJoinCode) ? "" : $"Join code: {activeJoinCode}";
        ShowLobbyScreen();

        try
        {
            await relayManager.JoinHostAsync(activeJoinCode);
            SetLobbyStatus("Connecting to host...");
        }
        catch (Exception exception)
        {
            SetLobbyStatus($"Join failed: {exception.Message}");
            Debug.LogException(exception, this);
            isConnecting = false;
        }
    }

    private void SavePlayerName(string value)
    {
        string safeName = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        if (safeName.Length > MaximumNameLength)
            safeName = safeName.Substring(0, MaximumNameLength);

        PlayerPrefs.SetString(PlayerNameKey, safeName);
        PlayerPrefs.Save();
        if (playerNameInput != null)
            playerNameInput.SetTextWithoutNotify(safeName);
        NetworkSessionManager.Instance?.SubmitLocalProfile(safeName, SelectedCharacterIndex);
    }

    private int SelectedCharacterIndex
    {
        get
        {
            int count = previewModels == null ? 0 : previewModels.Length;
            return count == 0 ? 0 : Mathf.Clamp(PlayerPrefs.GetInt(CharacterIndexKey, 0), 0, count - 1);
        }
    }

    private void ChangeCharacter(int direction)
    {
        int count = previewModels == null ? 0 : previewModels.Length;
        if (count == 0)
        {
            Debug.LogWarning("MultiplayerMenuController: Player preview has no Character_* model roots.", this);
            return;
        }

        int index = (SelectedCharacterIndex + direction + count) % count;
        PlayerPrefs.SetInt(CharacterIndexKey, index);
        PlayerPrefs.Save();
        ApplyCharacterIndex(index);
        NetworkSessionManager.Instance?.SubmitLocalProfile(NetworkSessionManager.LocalPlayerName, index);
    }

    private void ApplyCharacterIndex(int index)
    {
        if (previewModels == null)
            return;

        if (previewModels.Length > 0)
            index = (index % previewModels.Length + previewModels.Length) % previewModels.Length;

        for (int i = 0; i < previewModels.Length; i++)
        {
            if (previewModels[i] != null)
                previewModels[i].gameObject.SetActive(i == index);
        }

        if (characterIndexText != null)
            characterIndexText.text = index.ToString();
    }

    private void CreateCharacterPreview()
    {
        if (playerPreviewPrefab == null || modelPosition == null)
            return;

        previewRoot = new GameObject("PlayerCharacterPreview");
        previewRoot.transform.SetParent(modelPosition, false);
        previewRoot.transform.localPosition = Vector3.zero;
        previewRoot.transform.localRotation = Quaternion.identity;
        previewRoot.transform.localScale = Vector3.one;
        previewRoot.SetActive(false);

        previewInstance = Instantiate(playerPreviewPrefab, previewRoot.transform, false);
        previewInstance.name = "PlayerCharacterPreviewModel";

        foreach (MonoBehaviour behaviour in previewInstance.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Collider previewCollider in previewInstance.GetComponentsInChildren<Collider>(true))
            previewCollider.enabled = false;
        foreach (Rigidbody previewRigidbody in previewInstance.GetComponentsInChildren<Rigidbody>(true))
            previewRigidbody.isKinematic = true;
        foreach (Canvas previewCanvas in previewInstance.GetComponentsInChildren<Canvas>(true))
            previewCanvas.enabled = false;
        foreach (AudioListener previewListener in previewInstance.GetComponentsInChildren<AudioListener>(true))
            previewListener.enabled = false;

        previewModels = FindCharacterModels(previewInstance.transform);
        if (previewModels.Length == 0)
            Debug.LogError("MultiplayerMenuController: Player prefab contains no Character_* models.", playerPreviewPrefab);

        previewRoot.SetActive(false);
    }

    private static Transform[] FindCharacterModels(Transform root)
    {
        var models = new List<Transform>();
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name.StartsWith("Character_") &&
                candidate.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                models.Add(candidate);
        }

        models.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return models.ToArray();
    }

    private void ToggleReady()
    {
        NetworkSessionManager session = NetworkSessionManager.Instance;
        if (session == null || !session.IsSpawned || NetworkManager.Singleton == null)
            return;

        ulong clientId = NetworkManager.Singleton.LocalClientId;
        bool currentlyReady = false;
        foreach (LobbyPlayerEntry player in session.Players)
        {
            if (player.ClientId == clientId)
            {
                currentlyReady = player.IsReady;
                break;
            }
        }

        session.SetReadyServerRpc(!currentlyReady);
    }

    private void StartGame()
    {
        NetworkSessionManager session = NetworkSessionManager.Instance;
        if (session != null && session.IsServer && session.CanStartRun())
            session.StartRun();
    }

    private void LeaveLobby()
    {
        relayManager?.LeaveSession();
        UnbindSession();
        activeJoinCode = string.Empty;
        isConnecting = false;
        ShowMainMenuScreen();
    }

    private void BindSession()
    {
        NetworkSessionManager current = NetworkSessionManager.Instance;
        if (current == null || current == boundSession)
            return;

        UnbindSession();
        boundSession = current;
        boundSession.Players.OnListChanged += HandlePlayersChanged;
    }

    private void UnbindSession()
    {
        if (boundSession != null)
            boundSession.Players.OnListChanged -= HandlePlayersChanged;
        boundSession = null;
    }

    private void HandlePlayersChanged(NetworkListEvent<LobbyPlayerEntry> changeEvent)
    {
        RefreshLobby();
    }

    private void RefreshLobby()
    {
        BindSession();
        NetworkSessionManager session = NetworkSessionManager.Instance;
        if (session == null || !session.IsSpawned)
        {
            if (lobbyPlayerCountText != null)
                lobbyPlayerCountText.text = "Players: connecting...";
            return;
        }

        if (lobbyPlayerCountText != null)
            lobbyPlayerCountText.text = $"Players: {session.Players.Count}/{Mathf.Max(1, session.MaxPlayers.Value)}";

        StringBuilder playersText = new StringBuilder();
        ulong localClientId = NetworkManager.Singleton == null ? ulong.MaxValue : NetworkManager.Singleton.LocalClientId;
        bool localIsHost = false;
        bool localIsReady = false;
        foreach (LobbyPlayerEntry player in session.Players)
        {
            if (playersText.Length > 0)
                playersText.AppendLine();
            playersText.Append(player.PlayerName.ToString());
            playersText.Append(player.IsHost ? "  (HOST)" : "");
            playersText.Append(player.IsReady ? "  READY" : "  NOT READY");
            playersText.Append("  [").Append(player.CharacterIndex).Append(']');

            if (player.ClientId == localClientId)
            {
                localIsHost = player.IsHost;
                localIsReady = player.IsReady;
            }
        }

        if (lobbyPlayerListText != null)
            lobbyPlayerListText.text = playersText.Length == 0 ? "Waiting for players..." : playersText.ToString();
        if (readyButton != null)
            readyButton.gameObject.SetActive(!localIsHost);
        if (readyButtonText != null)
            readyButtonText.text = localIsReady ? "Cancel ready" : "Ready";
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(localIsHost);
            startGameButton.interactable = localIsHost && session.CanStartRun();
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId)
            return;

        isConnecting = false;
        SetLobbyStatus("Connected to lobby.");
        BindSession();
        RefreshLobby();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer ||
            clientId != NetworkManager.Singleton.LocalClientId)
            return;

        isConnecting = false;
        SetLobbyStatus("Disconnected from host.");
    }

    private void SetLobbyStatus(string value)
    {
        if (lobbyStatusText != null)
            lobbyStatusText.text = value;
    }

    private void OnNetworkClientConnected(ulong clientId)
    {
        HandleClientConnected(clientId);
    }

    private void OnNetworkClientDisconnected(ulong clientId)
    {
        HandleClientDisconnected(clientId);
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnNetworkClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkClientDisconnected;
            networkCallbacksBound = true;
        }
    }

    private void OnDestroy()
    {
        UnbindSession();
        if (!networkCallbacksBound || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkClientDisconnected;
    }
}