using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkGameState : NetworkBehaviour
{
    public NetworkVariable<GameState> CurrentState = new(
        GameState.Menu,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<FailCause> LastFailCause = new(
        FailCause.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private GameStateManager gameStateManager;

    public override void OnNetworkSpawn()
    {
        gameStateManager = GameStateManager.Instance;
        if (gameStateManager == null)
        {
            Debug.LogError("NetworkGameState: no GameStateManager in the scene.", this);
            return;
        }

        CurrentState.OnValueChanged += HandleStateChanged;
        LastFailCause.OnValueChanged += HandleFailCauseChanged;

        if (IsServer)
            SyncFromManager();
        else
            ApplyState();
    }

    public override void OnNetworkDespawn()
    {
        CurrentState.OnValueChanged -= HandleStateChanged;
        LastFailCause.OnValueChanged -= HandleFailCauseChanged;
    }

    private void Update()
    {
        if (IsServer && gameStateManager != null)
            SyncFromManager();
    }

    private void SyncFromManager()
    {
        if (CurrentState.Value != gameStateManager.CurrentState)
            CurrentState.Value = gameStateManager.CurrentState;
        if (LastFailCause.Value != gameStateManager.LastFailCause)
            LastFailCause.Value = gameStateManager.LastFailCause;
    }

    private void HandleStateChanged(GameState previous, GameState current)
    {
        if (!IsServer)
            ApplyState();
    }

    private void HandleFailCauseChanged(FailCause previous, FailCause current)
    {
        if (!IsServer)
            ApplyState();
    }

    private void ApplyState()
    {
        gameStateManager.ApplyReplicatedState(CurrentState.Value, LastFailCause.Value);
    }
}