using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Health))]
public class NetworkHealth : NetworkBehaviour
{
    public NetworkVariable<float> CurrentHealth = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    public override void OnNetworkSpawn()
    {
        CurrentHealth.OnValueChanged += HandleHealthChanged;

        if (IsServer)
            CurrentHealth.Value = health.MaxHealth;
        else
            health.ApplyReplicatedHealth(CurrentHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -= HandleHealthChanged;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestDamageServerRpc(float damage, Vector3 hitDirection, float forceAmount,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (damage <= 0f || health.IsDead)
            return;

        health.ApplyDamage(damage, hitDirection, forceAmount);
        CurrentHealth.Value = health.CurrentHealth;
    }

    public bool ApplyServerDamage(float damage, Vector3 hitDirection, float forceAmount)
    {
        if (!IsServer || damage <= 0f || health.IsDead)
            return false;

        health.ApplyDamage(damage, hitDirection, forceAmount);
        CurrentHealth.Value = health.CurrentHealth;
        return true;
    }

    public void SetAuthoritativeHealth(float value)
    {
        if (!IsServer)
            return;

        health.ApplyReplicatedHealth(value);
        CurrentHealth.Value = health.CurrentHealth;
    }

    private void HandleHealthChanged(float previous, float current)
    {
        if (!IsServer)
            health.ApplyReplicatedHealth(current);
    }
}