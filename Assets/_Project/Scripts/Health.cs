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

    private CharacterRagdollController ragdollController;

    private void Awake()
    {
        CurrentHealth = maxHealth;

        ragdollController = GetComponent<CharacterRagdollController>();
        if (ragdollController == null)
            ragdollController = gameObject.AddComponent<CharacterRagdollController>();
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector3.zero, 0f);
    }

    public void TakeDamage(float damage, Vector3 hitDirection, float forceAmount)
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
        if (IsDead)
            return;

        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);

        OnHealthChanged?.Invoke(CurrentHealth);
    }

    // Brings a dead entity fully back; callers own repositioning and ragdoll/control reset.
    public void Revive()
    {
        if (!IsDead)
            return;

        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth);
    }

    private void Die()
    {
        if (ragdollController != null)
            ragdollController.EnableRagdoll(LastHitDirection, LastHitForce);

        OnDeath?.Invoke();
    }
}