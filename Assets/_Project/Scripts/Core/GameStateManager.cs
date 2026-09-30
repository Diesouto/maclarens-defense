using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Menu,
    Run,
    Success,
    Fail
}

// Why the run ended in Fail; UI-facing only, doesn't change any gameplay rule.
public enum FailCause
{
    None,
    TeamWipe,
    QuotaFailed
}

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [SerializeField] private GameState initialState = GameState.Menu;
    [SerializeField] private string mainMenuSceneName = "MenuScene";

    public GameState CurrentState { get; private set; }
    public bool IsRunActive => CurrentState == GameState.Run;
    public FailCause LastFailCause { get; private set; } = FailCause.None;

    public event Action<GameState, GameState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentState = initialState;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TrySetState(GameState nextState)
    {
        if (NetworkRole.IsClientOnly)
            return false;

        if (CurrentState == nextState)
            return false;

        GameState previousState = CurrentState;
        CurrentState = nextState;
        OnStateChanged?.Invoke(previousState, nextState);
        return true;
    }

    public void StartRun()
    {
        TrySetState(GameState.Run);
    }

    public void SetSuccess()
    {
        TrySetState(GameState.Success);
    }

    public void SetFail(FailCause cause)
    {
        if (!TrySetState(GameState.Fail))
            return;

        LastFailCause = cause;
    }

    public void ApplyReplicatedState(GameState state, FailCause cause)
    {
        if (CurrentState == state && LastFailCause == cause)
            return;

        GameState previous = CurrentState;
        CurrentState = state;
        LastFailCause = cause;
        OnStateChanged?.Invoke(previous, state);
    }

    public bool CanRestart => !NetworkRole.IsClientOnly;

    // Reloading the gameplay scene is the reset: every manager, loot, enemy and threat instance
    // is freshly created instead of hand-resetting each system one by one.
    public void RestartRun()
    {
        if (!CanRestart)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            // Networked reload so every client follows the host into the fresh run.
            networkManager.SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
            return;
        }

        SceneManager.LoadScene(activeScene.buildIndex);
    }

    public void ReturnToMainMenu()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}

