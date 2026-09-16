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
        {
            int teamMoney = Mathf.Max(MoneyManager.Instance.TeamMoney, 0);
            if (TeamMoney.Value != teamMoney)
                TeamMoney.Value = teamMoney;
        }

        if (QuotaManager.Instance == null)
            return;

        if (DebtPaid.Value != Mathf.Max(QuotaManager.Instance.DebtPaid, 0))
            DebtPaid.Value = Mathf.Max(QuotaManager.Instance.DebtPaid, 0);
        if (DebtRemaining.Value != Mathf.Max(QuotaManager.Instance.DebtRemaining, 0))
            DebtRemaining.Value = Mathf.Max(QuotaManager.Instance.DebtRemaining, 0);
        if (CurrentQuota.Value != Mathf.Max(QuotaManager.Instance.EffectiveQuota, 0))
            CurrentQuota.Value = Mathf.Max(QuotaManager.Instance.EffectiveQuota, 0);
        if (CurrentCargoValue.Value != Mathf.Max(QuotaManager.Instance.CurrentCargoValue, 0))
            CurrentCargoValue.Value = Mathf.Max(QuotaManager.Instance.CurrentCargoValue, 0);
        if (DeliveredValue.Value != Mathf.Max(QuotaManager.Instance.DeliveredValue, 0))
            DeliveredValue.Value = Mathf.Max(QuotaManager.Instance.DeliveredValue, 0);
    }
}
