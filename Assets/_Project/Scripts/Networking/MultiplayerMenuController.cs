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
    [Tooltip("Layer applied to the whole preview so a dedicated preview camera can render it alone. -1 keeps the prefab layers.")]
    [SerializeField] private int previewLayer = -1;

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

    [Header("Match Settings")]
    [Tooltip("Shared setup field; editable offline and by the multiplayer host.")]
    [SerializeField] private TMP_InputField lobbyQuotasInput;
    [SerializeField] private TMP_Text lobbyQuotasHintText;
    [SerializeField] private string quotasHintFormat = "Cuotas para ganar (0 = infinito): {0}";

    private const string PlayerNameKey = "PlayerName";
    private const string CharacterIndexKey = "PlayerCharacterIndex";
    private const int MaximumNameLength = 24;
    private bool isConnecting;
    private bool networkCallbacksBound;
    private bool isOfflineSetup;
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
        ConfigureQuotasInput(lobbyQuotasInput, HandleQuotasEdited);
        ShowQuotas(lobbyQuotasInput, lobbyQuotasHintText, RunSettings.SavedQuotasToWin);
        ValidateReferences();
        BindButtons();
    }

    private void ConfigureQuotasInput(TMP_InputField input, UnityEngine.Events.UnityAction<string> onEndEdit)
    {
        if (input == null)
            return;

        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 2;
        // Replaces the built-in Integer validation, which would still accept a leading '-'.
        input.onValidateInput = (_, _, character) => character >= '0' && character <= '9' ? character : '\0';
        input.onEndEdit.AddListener(onEndEdit);
    }

    private void HandleQuotasEdited(string text)
    {
        int fallback = RunSettings.SavedQuotasToWin;
        int value = RunSettings.TryParse(text, out int parsed) ? parsed : fallback;
        RunSettings.SetQuotasToWin(value);

        NetworkSessionManager session = NetworkSessionManager.Instance;
        if (!isOfflineSetup && session != null && session.IsServer)
            session.SetQuotasToWin(value);

        ShowQuotas(lobbyQuotasInput, lobbyQuotasHintText, value);
    }

    private void ShowQuotas(TMP_InputField input, TMP_Text hint, int value)
    {
        if (input != null && !input.isFocused)
            input.SetTextWithoutNotify(value.ToString());

        if (hint != null)
            hint.text = string.Format(quotasHintFormat, RunSettings.Describe(value));
    }

    private void Update()
    {
        if (lobbyScreen != null && lobbyScreen.activeSelf)
            RefreshLobby();
    }

    private void BindButtons()
    {
        AddListener(singlePlayerButton, ShowSinglePlayerSetup);
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

    private void ShowSinglePlayerSetup()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            SetLobbyStatus("Leave the multiplayer session before starting a single-player run.");
            isOfflineSetup = false;
            ShowLobbyScreen();
            return;
        }

        isOfflineSetup = true;
        ShowQuotas(lobbyQuotasInput, lobbyQuotasHintText, RunSettings.SavedQuotasToWin);
        SetLobbyStatus("Single-player setup");
        RefreshLobby();
        ShowLobbyScreen();
    }

    private void StartSinglePlayerRun()
    {
        if (NetworkBootstrapper.Instance != null)
        {
            GameObject servicesRoot = NetworkBootstrapper.Instance.gameObject;
            NetworkBootstrapper.Instance.Shutdown();
            Destroy(servicesRoot);
        }

        // Commits a value still being typed when Play is clicked without leaving the field.
        if (lobbyQuotasInput != null && RunSettings.TryParse(lobbyQuotasInput.text, out int typedQuotas))
            RunSettings.SetQuotasToWin(typedQuotas);
        else
            RunSettings.SetQuotasToWin(RunSettings.SavedQuotasToWin);

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
        RefreshLobby();
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

        isOfflineSetup = false;
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

        isOfflineSetup = false;
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
        // Camera, Light and AudioListener derive from Behaviour, not MonoBehaviour, so the loop above misses them.
        foreach (Behaviour behaviour in previewInstance.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour is Camera || behaviour is Light || behaviour is AudioListener)
                behaviour.enabled = false;
        }
        // Destroyed rather than disabled: Awake still runs on a disabled component when previewRoot is
        // activated, and this one would re-layer the model and strip that layer from a live camera.
        foreach (PlayerBodyVisibility visibility in previewInstance.GetComponentsInChildren<PlayerBodyVisibility>(true))
            Destroy(visibility);
        foreach (Collider previewCollider in previewInstance.GetComponentsInChildren<Collider>(true))
            previewCollider.enabled = false;
        foreach (Rigidbody previewRigidbody in previewInstance.GetComponentsInChildren<Rigidbody>(true))
            previewRigidbody.isKinematic = true;
        foreach (Canvas previewCanvas in previewInstance.GetComponentsInChildren<Canvas>(true))
            previewCanvas.enabled = false;

        if (previewLayer >= 0 && previewLayer < 32)
            SetLayerRecursively(previewInstance.transform, previewLayer);

        previewModels = FindCharacterModels(previewInstance.transform);
        if (previewModels.Length == 0)
            Debug.LogError("MultiplayerMenuController: Player prefab contains no Character_* models.", playerPreviewPrefab);

        previewRoot.SetActive(false);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
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
        if (isOfflineSetup)
        {
            StartSinglePlayerRun();
            return;
        }

        NetworkSessionManager session = NetworkSessionManager.Instance;
        if (session != null && session.IsServer && session.CanStartRun())
            session.StartRun();
    }

    private void LeaveLobby()
    {
        if (isOfflineSetup)
        {
            isOfflineSetup = false;
            ShowMainMenuScreen();
            return;
        }

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
        if (isOfflineSetup)
        {
            SetOfflineSetupPresentation();
            return;
        }

        BindSession();
        NetworkSessionManager session = NetworkSessionManager.Instance;
        if (session == null || !session.IsSpawned)
        {
            SetOnlineSetupPresentation(false);
            if (lobbyPlayerCountText != null)
                lobbyPlayerCountText.text = "Jugadores: conectando...";
            return;
        }

        SetOnlineSetupPresentation(true);

        if (lobbyPlayerCountText != null)
            lobbyPlayerCountText.text = $"Jugadores: {session.Players.Count}/{Mathf.Max(1, session.MaxPlayers.Value)}";

        StringBuilder playersText = new StringBuilder();
        ulong localClientId = NetworkManager.Singleton == null ? ulong.MaxValue : NetworkManager.Singleton.LocalClientId;
        bool localIsHost = false;
        bool localIsReady = false;
        foreach (LobbyPlayerEntry player in session.Players)
        {
            if (playersText.Length > 0)
                playersText.AppendLine();
            playersText.Append(player.PlayerName.ToString());
            playersText.Append(player.IsHost ? "  (ANFITRIÓN)" : "");
            playersText.Append(player.IsReady ? "  LISTO" : "  NO LISTO");
            playersText.Append("  [").Append(player.CharacterIndex).Append(']');

            if (player.ClientId == localClientId)
            {
                localIsHost = player.IsHost;
                localIsReady = player.IsReady;
            }
        }

        if (lobbyPlayerListText != null)
            lobbyPlayerListText.text = playersText.Length == 0 ? "Esperando jugadores..." : playersText.ToString();
        if (readyButton != null)
            readyButton.gameObject.SetActive(!localIsHost);
        if (readyButtonText != null)
            readyButtonText.text = localIsReady ? "Cancelar" : "Listo";
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(localIsHost);
            startGameButton.interactable = localIsHost && session.CanStartRun();
        }

        if (lobbyQuotasInput != null)
            lobbyQuotasInput.interactable = localIsHost;
        ShowQuotas(lobbyQuotasInput, lobbyQuotasHintText, session.QuotasToWin.Value);
    }

    private void SetOfflineSetupPresentation()
    {
        SetLobbyTitle("Ajustes partida");
        SetActive(lobbyJoinCodeText != null ? lobbyJoinCodeText.gameObject : null, false);
        SetActive(lobbyPlayerCountText != null ? lobbyPlayerCountText.gameObject : null, false);
        SetActive(lobbyPlayerListText != null ? lobbyPlayerListText.gameObject : null, false);
        SetActive(readyButton != null ? readyButton.gameObject : null, false);
        if (lobbyStatusText != null)
        {
            lobbyStatusText.gameObject.SetActive(true);
            lobbyStatusText.text = "Ajustes singleplayer";
        }

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(true);
            startGameButton.interactable = true;
        }

        if (leaveLobbyButton != null)
        {
            leaveLobbyButton.gameObject.SetActive(true);
            SetButtonLabel(leaveLobbyButton, "Volver");
        }

        if (lobbyQuotasInput != null)
            lobbyQuotasInput.interactable = true;
        ShowQuotas(lobbyQuotasInput, lobbyQuotasHintText, RunSettings.SavedQuotasToWin);
    }

    private void SetOnlineSetupPresentation(bool connected)
    {
        SetLobbyTitle("Lobby");
        SetActive(lobbyJoinCodeText != null ? lobbyJoinCodeText.gameObject : null, true);
        SetActive(lobbyPlayerCountText != null ? lobbyPlayerCountText.gameObject : null, true);
        SetActive(lobbyPlayerListText != null ? lobbyPlayerListText.gameObject : null, true);
        SetActive(readyButton != null ? readyButton.gameObject : null, connected);
        if (!connected)
        {
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(false);
                startGameButton.interactable = false;
            }

            if (lobbyQuotasInput != null)
                lobbyQuotasInput.interactable = false;
        }

        if (leaveLobbyButton != null)
        {
            leaveLobbyButton.gameObject.SetActive(true);
            SetButtonLabel(leaveLobbyButton, "Salir del Lobby");
        }
    }

    private static void SetButtonLabel(Button button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;
    }

    private void SetLobbyTitle(string title)
    {
        if (lobbyScreen == null)
            return;

        foreach (TMP_Text text in lobbyScreen.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.text == "Lobby" || text.gameObject.name == "LobbyTitle")
            {
                text.text = title;
                return;
            }
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId)
            return;

        isConnecting = false;
        SetLobbyStatus("Conectado al lobby.");
        BindSession();
        RefreshLobby();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer ||
            clientId != NetworkManager.Singleton.LocalClientId)
            return;

        isConnecting = false;
        SetLobbyStatus("Desconectado del anfitrión.");
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