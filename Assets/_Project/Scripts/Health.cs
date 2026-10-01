using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    public float CurrentHealth { get; private set; }

    public bool IsDead => CurrentHealth <= 0f;

    public float MaxHealth => maxHealth;
    public Vector3 LastHitDirection { get; private set; }
    public float LastHitForce { get; private set; }

    public event Action<float> OnHealthChanged;
    public event Action OnHit;
    public event Action OnDeath;
    public event Action OnRevived;

    private CharacterRagdollController ragdollController;
    private NetworkHealth networkHealth;

    private void Awake()
    {
        CurrentHealth = maxHealth;

        ragdollController = GetComponent<CharacterRagdollController>();
        if (ragdollController == null)
            ragdollController = gameObject.AddComponent<CharacterRagdollController>();

        networkHealth = GetComponent<NetworkHealth>();
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector3.zero, 0f);
    }

    public void TakeDamage(float damage, Vector3 hitDirection, float forceAmount)
    {
        if (networkHealth != null && networkHealth.IsSpawned)
        {
            // Local hazards (DeathFloor, etc.) fire on every machine; only the server or the owner may act on them.
            if (networkHealth.IsServer)
                networkHealth.ApplyServerDamage(damage, hitDirection, forceAmount);
            else if (networkHealth.IsOwner)
                networkHealth.RequestDamageServerRpc(damage, hitDirection, forceAmount);

            return;
        }

        ApplyDamage(damage, hitDirection, forceAmount);
    }

    public void ApplyReplicatedHealth(float health)
    {
        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Clamp(health, 0f, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth);

        if (previousHealth <= 0f && CurrentHealth > 0f)
        {
            OnRevived?.Invoke();
            return;
        }

        if (CurrentHealth >= previousHealth)
            return;

        OnHit?.Invoke();

        if (previousHealth > 0f && CurrentHealth <= 0f)
            Die();
    }

    internal void ApplyDamage(float damage, Vector3 hitDirection, float forceAmount)
    {
        if (IsDead)
            return;

        CurrentHealth -= damage;
        CurrentHealth = Mathf.Max(CurrentHealth, 0f);

        if (hitDirection.sqrMagnitude > 0f)
            LastHitDirection = hitDirection.normalized;
        else
            LastHitDirection = Vector3.zero;

        LastHitForce = Mathf.Max(forceAmount, 0f);

        OnHealthChanged?.Invoke(CurrentHealth);

        if (damage > 0f)
            OnHit?.Invoke();
            
        Debug.Log($"{gameObject.name} took {damage} damage. Current health: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0f)
        {
            Debug.Log($"{gameObject.name} has died.");
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (NetworkRole.IsClientOnly)
            return;

        if (IsDead)
            return;

        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);

        OnHealthChanged?.Invoke(CurrentHealth);
        if (networkHealth != null)
            networkHealth.SyncFromHealth();
    }

    // Brings a dead entity back; callers own repositioning and ragdoll/control reset.
    public void Revive(float healthFraction = 1f)
    {
        if (NetworkRole.IsClientOnly)
            return;

        if (!IsDead)
            return;

        CurrentHealth = Mathf.Max(1f, maxHealth * Mathf.Clamp01(healthFraction));
        OnHealthChanged?.Invoke(CurrentHealth);
        if (networkHealth != null)
            networkHealth.SyncFromHealth();
        OnRevived?.Invoke();
    }

    private void Die()
    {
        if (ragdollController != null)
            ragdollController.EnableRagdoll(LastHitDirection, LastHitForce);

        OnDeath?.Invoke();
    }
}