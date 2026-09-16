using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkTrainState : NetworkBehaviour
{
    public NetworkVariable<TrainDestination> CurrentStation = new(
        TrainDestination.Town,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<TrainDestination> Destination = new(
        TrainDestination.Town,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsMoving = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> CurrentSpeed = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> CurrentDistance = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private TrainSplineFollower train;
    [SerializeField] private TrainDeparture departure;
    [SerializeField] private RunManager runManager;

    private void Awake()
    {
        if (train == null)
            train = GetComponent<TrainSplineFollower>();

        if (departure == null)
            departure = GetComponentInChildren<TrainDeparture>();

        if (runManager == null)
            runManager = RunManager.Instance;
    }

    public override void OnNetworkSpawn()
    {
        if (train == null)
            return;

        if (IsServer)
        {
            CurrentStation.Value = train.CurrentStation;
            Destination.Value = train.CurrentStation;
            SyncState();
        }
        else
        {
            ApplyState();
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            SyncState();
            return;
        }

        ApplyState();
    }

    public void RequestDeparture()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
            BeginDeparture();
        else
            RequestDepartureServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDepartureServerRpc(ServerRpcParams rpcParams = default)
    {
        BeginDeparture();
    }

    public void RequestFinishDay()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
            ApplyFinishDay();
        else
            RequestFinishDayServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestFinishDayServerRpc(ServerRpcParams rpcParams = default)
    {
        ApplyFinishDay();
    }

    private void BeginDeparture()
    {
        if (!IsServer || departure == null || departure.IsDeparting || departure.HasDeparted)
            return;

        Destination.Value = train.CurrentStation == TrainDestination.Town
            ? TrainDestination.MacLarens
            : TrainDestination.Town;

        departure.BeginAuthoritativeDeparture();
    }

    private void ApplyFinishDay()
    {
        if (!IsServer || runManager == null || runManager.CurrentPhase != RunPhase.ResolvingDay)
            return;

        runManager.FinishDay();
    }

    private void SyncState()
    {
        CurrentStation.Value = train.CurrentStation;
        IsMoving.Value = train.IsMoving;
        CurrentSpeed.Value = train.CurrentSpeed;
        CurrentDistance.Value = train.CurrentDistance;
    }

    private void ApplyState()
    {
        // NetworkTransform carries the authoritative transform. These values remain public
        // state for UI, audio and future deterministic presentation code.
    }
}