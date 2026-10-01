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
    // The countdown bar is local to whoever pulled the lever, so everyone else needs this to stay blocked.
    public NetworkVariable<bool> IsCountdownActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private TrainSplineFollower train;
    [SerializeField] private TrainDeparture departure;
    [SerializeField] private RunManager runManager;

    private ulong departureRequesterId;

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
            CurrentDistance.OnValueChanged += HandleDistanceChanged;
            IsMoving.OnValueChanged += HandleMovingChanged;
            CurrentStation.OnValueChanged += HandleStationChanged;
            ApplyState(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        CurrentDistance.OnValueChanged -= HandleDistanceChanged;
        IsMoving.OnValueChanged -= HandleMovingChanged;
        CurrentStation.OnValueChanged -= HandleStationChanged;
    }

    private void Update()
    {
        if (IsServer && train != null)
            SyncState();
    }

    private void HandleDistanceChanged(float previousValue, float newValue) => ApplyState(false);

    private void HandleMovingChanged(bool previousValue, bool newValue) => ApplyState(false);

    private void HandleStationChanged(TrainDestination previousValue, TrainDestination newValue) => ApplyState(false);

    public void RequestDeparture()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            departureRequesterId = NetworkManager.LocalClientId;
            BeginDeparture();
        }
        else
        {
            RequestDepartureServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDepartureServerRpc(RpcParams rpcParams = default)
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (runManager == null || departure == null ||
            !NetworkManager.ConnectedClients.TryGetValue(senderId, out NetworkClient client) ||
            client.PlayerObject == null)
        {
            Debug.LogWarning($"NetworkTrainState: departure from client {senderId} rejected (missing RunManager, departure or player object).", this);
            return;
        }

        Health playerHealth = client.PlayerObject.GetComponent<Health>();
        if (playerHealth != null && playerHealth.IsDead)
            return;

        departureRequesterId = senderId;

        float distance = Vector3.Distance(client.PlayerObject.transform.position, departure.transform.position);
        if (distance > 4f)
        {
            Debug.LogWarning($"NetworkTrainState: departure from client {senderId} rejected, {distance:F1}m from the lever (host view).", this);
            return;
        }

        if (runManager.CurrentPhase != RunPhase.Town &&
            runManager.CurrentPhase != RunPhase.MacLarens)
        {
            Debug.LogWarning($"NetworkTrainState: departure from client {senderId} rejected in phase {runManager.CurrentPhase}.", this);
            return;
        }

        BeginDeparture();
    }

    public void BroadcastDepartureCountdown(float duration)
    {
        if (!IsServer)
            return;

        IsCountdownActive.Value = true;
        DepartureCountdownRpc(duration, RpcTarget.Single(departureRequesterId, RpcTargetUse.Temp));
    }

    public void ClearDepartureCountdown()
    {
        if (IsServer)
            IsCountdownActive.Value = false;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void DepartureCountdownRpc(float duration, RpcParams rpcParams)
    {
        departure?.ShowDepartureCountdown(duration);
    }

    public void RequestPayQuota()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
            ApplyPayQuota();
        else
            RequestPayQuotaServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPayQuotaServerRpc(RpcParams rpcParams = default)
    {
        if (!NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out NetworkClient client) ||
            client.PlayerObject == null ||
            (client.PlayerObject.TryGetComponent(out Health playerHealth) && playerHealth.IsDead))
            return;

        ApplyPayQuota();
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

    private void ApplyPayQuota()
    {
        if (!IsServer || runManager == null)
            return;

        runManager.PayQuota();
    }

    private void SyncState()
    {
        if (CurrentStation.Value != train.CurrentStation)
            CurrentStation.Value = train.CurrentStation;
        if (IsMoving.Value != train.IsMoving)
            IsMoving.Value = train.IsMoving;
        if (!Mathf.Approximately(CurrentSpeed.Value, train.CurrentSpeed))
            CurrentSpeed.Value = train.CurrentSpeed;
        if (!Mathf.Approximately(CurrentDistance.Value, train.CurrentDistance))
            CurrentDistance.Value = train.CurrentDistance;
    }

    private void ApplyState(bool snap)
    {
        if (train == null)
            return;

        train.SetReplicatedState(
            CurrentDistance.Value,
            CurrentSpeed.Value,
            CurrentStation.Value,
            IsMoving.Value,
            snap);
    }
}