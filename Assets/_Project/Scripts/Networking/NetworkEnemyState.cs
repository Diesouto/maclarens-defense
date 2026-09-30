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
    private Animator animator;
    private Vector3 lastPosition;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            return;

        // Clients only present the host's enemy: NetworkTransform moves it, the local agent must not.
        enemy.enabled = false;
        if (TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
            agent.enabled = false;

        lastPosition = transform.position;
        IsAttacking.OnValueChanged += HandleAttackingChanged;
    }

    public override void OnNetworkDespawn()
    {
        IsAttacking.OnValueChanged -= HandleAttackingChanged;
    }

    private void Update()
    {
        if (!IsSpawned || IsServer || animator == null)
            return;

        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;

        bool moving = !IsDead.Value && Time.deltaTime > 0f && delta.sqrMagnitude / (Time.deltaTime * Time.deltaTime) > 0.25f;
        animator.SetFloat("Speed", moving ? 1f : 0f);
    }

    private void HandleAttackingChanged(bool previous, bool current)
    {
        if (current && animator != null && !IsDead.Value)
            animator.SetTrigger("HasAttacked");
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