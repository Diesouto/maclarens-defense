using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkEconomyState : NetworkBehaviour
{
    public NetworkVariable<int> TeamMoney = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> TotalEarned = new(
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
        {
            SyncFromManagers();
            return;
        }

        TeamMoney.OnValueChanged += HandleValueChanged;
        TotalEarned.OnValueChanged += HandleValueChanged;
        CurrentQuota.OnValueChanged += HandleValueChanged;
        CurrentCargoValue.OnValueChanged += HandleValueChanged;
        DeliveredValue.OnValueChanged += HandleValueChanged;
        ApplyToManagers();
    }

    public override void OnNetworkDespawn()
    {
        TeamMoney.OnValueChanged -= HandleValueChanged;
        TotalEarned.OnValueChanged -= HandleValueChanged;
        CurrentQuota.OnValueChanged -= HandleValueChanged;
        CurrentCargoValue.OnValueChanged -= HandleValueChanged;
        DeliveredValue.OnValueChanged -= HandleValueChanged;
    }

    private void HandleValueChanged(int previous, int current) => ApplyToManagers();

    private void ApplyToManagers()
    {
        MoneyManager.Instance?.ApplyReplicatedMoney(TeamMoney.Value, TotalEarned.Value);
        QuotaManager.Instance?.ApplyReplicatedState(
            CurrentQuota.Value,
            CurrentCargoValue.Value,
            DeliveredValue.Value);
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
            if (TotalEarned.Value != MoneyManager.Instance.TotalEarned)
                TotalEarned.Value = MoneyManager.Instance.TotalEarned;
        }

        if (QuotaManager.Instance == null)
            return;

        if (CurrentQuota.Value != Mathf.Max(QuotaManager.Instance.EffectiveQuota, 0))
            CurrentQuota.Value = Mathf.Max(QuotaManager.Instance.EffectiveQuota, 0);
        if (CurrentCargoValue.Value != Mathf.Max(QuotaManager.Instance.CurrentCargoValue, 0))
            CurrentCargoValue.Value = Mathf.Max(QuotaManager.Instance.CurrentCargoValue, 0);
        if (DeliveredValue.Value != Mathf.Max(QuotaManager.Instance.DeliveredValue, 0))
            DeliveredValue.Value = Mathf.Max(QuotaManager.Instance.DeliveredValue, 0);
    }
}
