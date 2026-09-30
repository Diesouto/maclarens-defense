using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the Success/Fail panel and offers Restart/Main Menu; disappears for Menu/Run.
// Reads GameStateManager and only presents state, same as every other *UI script in this domain.
// If no panel is authored it builds a minimal one at runtime, so the screens work without editor wiring.
public class GameStateUI : MonoBehaviour
{
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text causeText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [SerializeField] private string successTitle = "Debt paid!";
    [SerializeField] private string failTitle = "Defeat";
    [SerializeField] private string successCauseText = "MacLarens is free. The crew made it.";
    [SerializeField] private string teamWipeCauseText = "The whole crew went down.";
    [SerializeField] private string quotaFailedCauseText = "The quota wasn't paid in time.";
    [SerializeField] private string waitingForHostText = "Waiting for the host to restart...";

    private void Awake()
    {
        if (gameStateManager == null)
            gameStateManager = GameStateManager.Instance;

        if (panelRoot == null)
            BuildDefaultPanel();

        if (restartButton != null)
            restartButton.onClick.AddListener(HandleRestart);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(HandleMainMenu);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameStateManager == null)
            gameStateManager = GameStateManager.Instance;

        if (gameStateManager != null)
        {
            gameStateManager.OnStateChanged += HandleStateChanged;
            Refresh(gameStateManager.CurrentState);
        }
    }

    // GameStateManager may not exist yet during OnEnable (Awake order is not guaranteed).
    private void Start()
    {
        if (gameStateManager != null)
            return;

        gameStateManager = GameStateManager.Instance;
        if (gameStateManager == null)
            return;

        gameStateManager.OnStateChanged += HandleStateChanged;
        Refresh(gameStateManager.CurrentState);
    }

    private void OnDisable()
    {
        if (gameStateManager != null)
            gameStateManager.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState previousState, GameState nextState)
    {
        Refresh(nextState);
    }

    private void Refresh(GameState state)
    {
        bool showPanel = state == GameState.Success || state == GameState.Fail;

        if (panelRoot != null)
            panelRoot.SetActive(showPanel);

        if (!showPanel)
            return;

        ReleaseLocalPlayerInput();

        if (titleText != null)
            titleText.text = state == GameState.Success ? successTitle : failTitle;

        if (causeText != null)
        {
            string cause = state == GameState.Fail ? GetCauseText(gameStateManager.LastFailCause) : successCauseText;
            causeText.text = gameStateManager.CanRestart ? cause : $"{cause}\n{waitingForHostText}";
        }

        if (restartButton != null)
            restartButton.gameObject.SetActive(gameStateManager.CanRestart);
    }

    // Disabling the local input handler unlocks the cursor and stops gameplay input behind the panel.
    private static void ReleaseLocalPlayerInput()
    {
        if (NetworkPlayer.Local != null && NetworkPlayer.Local.TryGetComponent(out PlayerInputHandler input))
            input.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private string GetCauseText(FailCause cause)
    {
        return cause switch
        {
            FailCause.TeamWipe => teamWipeCauseText,
            FailCause.QuotaFailed => quotaFailedCauseText,
            _ => string.Empty
        };
    }

    private void HandleRestart()
    {
        if (gameStateManager != null)
            gameStateManager.RestartRun();
    }

    private void HandleMainMenu()
    {
        if (gameStateManager != null)
            gameStateManager.ReturnToMainMenu();
    }

    private void BuildDefaultPanel()
    {
        panelRoot = CreateUIObject("GameStatePanel", transform);
        Stretch((RectTransform)panelRoot.transform);
        Image background = panelRoot.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.8f);

        titleText = CreateText("Title", panelRoot.transform, 96f, new Vector2(0f, 180f), new Vector2(1400f, 140f));
        causeText = CreateText("Cause", panelRoot.transform, 40f, new Vector2(0f, 60f), new Vector2(1400f, 80f));
        restartButton = CreateButton("RestartButton", panelRoot.transform, "Restart", new Vector2(-180f, -120f), out _);
        mainMenuButton = CreateButton("MainMenuButton", panelRoot.transform, "Main Menu", new Vector2(180f, -120f), out _);
        panelRoot.transform.SetAsLastSibling();
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        var uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, float fontSize, Vector2 position, Vector2 size)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        var rect = (RectTransform)textObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string objectName, Transform parent, string label, Vector2 position, out TMP_Text labelText)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        var rect = (RectTransform)buttonObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(320f, 80f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.55f, 0.3f, 0.12f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        labelText = CreateText("Label", buttonObject.transform, 36f, Vector2.zero, rect.sizeDelta);
        labelText.text = label;
        return button;
    }
}
