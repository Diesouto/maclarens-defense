using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(EnemyController))]
public class NetworkEnemyState : NetworkBehaviour
{
    public NetworkVariable<bool> IsDead = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsAttacking = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private EnemyController enemy;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            enemy.enabled = false;
    }

    public void SetDead(bool value)
    {
        if (IsServer)
            IsDead.Value = value;
    }

    public void SetAttacking(bool value)
    {
        if (IsServer)
            IsAttacking.Value = value;
    }
}