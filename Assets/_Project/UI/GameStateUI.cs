using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the Success/Fail panel and offers Restart/Main Menu; disappears for Menu/Run.
// Reads GameStateManager and only presents state, same as every other *UI script in this domain.
public class GameStateUI : MonoBehaviour
{
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text causeText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [SerializeField] private string successTitle = "Success";
    [SerializeField] private string failTitle = "Fail";
    [SerializeField] private string teamWipeCauseText = "The whole crew went down.";
    [SerializeField] private string quotaFailedCauseText = "The quota wasn't paid in time.";

    private void Awake()
    {
        if (gameStateManager == null)
            gameStateManager = GameStateManager.Instance;

        if (restartButton != null)
            restartButton.onClick.AddListener(HandleRestart);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(HandleMainMenu);
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

        if (titleText != null)
            titleText.text = state == GameState.Success ? successTitle : failTitle;

        if (causeText != null)
            causeText.text = state == GameState.Fail ? GetCauseText(gameStateManager.LastFailCause) : string.Empty;
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
        gameStateManager?.RestartRun();
    }

    private void HandleMainMenu()
    {
        gameStateManager?.ReturnToMainMenu();
    }
}
