using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkEconomyState : NetworkBehaviour
{
    public NetworkVariable<int> TeamMoney = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> DebtPaid = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> DebtRemaining = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> CurrentQuota = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> CurrentCargoValue = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> DeliveredValue = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            SyncFromManagers();
    }

    private void LateUpdate()
    {
        if (IsServer && IsSpawned)
            SyncFromManagers();
    }

    private void SyncFromManagers()
    {
        if (MoneyManager.Instance != null)
            TeamMoney.Value = MoneyManager.Instance.TeamMoney;

        if (QuotaManager.Instance == null)
            return;

        DebtPaid.Value = QuotaManager.Instance.DebtPaid;
        DebtRemaining.Value = QuotaManager.Instance.DebtRemaining;
        CurrentQuota.Value = QuotaManager.Instance.EffectiveQuota;
        CurrentCargoValue.Value = QuotaManager.Instance.CurrentCargoValue;
        DeliveredValue.Value = QuotaManager.Instance.DeliveredValue;
    }
}
