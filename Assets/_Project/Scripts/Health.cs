using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    public float CurrentHealth { get; private set; }

    public bool IsDead => CurrentHealth <= 0f;

    public float MaxHealth => maxHealth;
    public event Action<float> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead)
            return;

        CurrentHealth -= damage;
        CurrentHealth = Mathf.Max(CurrentHealth, 0f);

        OnHealthChanged?.Invoke(CurrentHealth);
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

    private void Die()
    {
        OnDeath?.Invoke();
    }
}