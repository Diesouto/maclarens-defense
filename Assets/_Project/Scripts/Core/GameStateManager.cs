using System;
using UnityEngine;

public enum GameState
{
    Menu,
    Run,
    Success,
    Fail
}

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [SerializeField] private GameState initialState = GameState.Menu;

    public GameState CurrentState { get; private set; }
    public bool IsRunActive => CurrentState == GameState.Run;

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

    public void SetFail()
    {
        TrySetState(GameState.Fail);
    }
}
