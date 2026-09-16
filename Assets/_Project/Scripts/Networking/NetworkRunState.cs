using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(RunManager))]
public class NetworkRunState : NetworkBehaviour
{
    public NetworkVariable<int> CurrentDay = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<RunPhase> CurrentPhase = new(
        RunPhase.MacLarens,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private RunManager runManager;

    private void Awake()
    {
        runManager = GetComponent<RunManager>();
    }

    public override void OnNetworkSpawn()
    {
        CurrentDay.OnValueChanged += HandleDayChanged;
        CurrentPhase.OnValueChanged += HandlePhaseChanged;

        if (IsServer)
            SyncFromManager();
        else
            ApplyState();
    }

    public override void OnNetworkDespawn()
    {
        CurrentDay.OnValueChanged -= HandleDayChanged;
        CurrentPhase.OnValueChanged -= HandlePhaseChanged;
    }

    private void Update()
    {
        if (IsServer)
            SyncFromManager();
    }

    private void SyncFromManager()
    {
        CurrentDay.Value = runManager.CurrentDay;
        CurrentPhase.Value = runManager.CurrentPhase;
    }

    private void HandleDayChanged(int previous, int current)
    {
        if (!IsServer)
            ApplyState();
    }

    private void HandlePhaseChanged(RunPhase previous, RunPhase current)
    {
        if (!IsServer)
            ApplyState();
    }

    private void ApplyState()
    {
        runManager.ApplyReplicatedRunState(CurrentDay.Value, CurrentPhase.Value);
    }
}