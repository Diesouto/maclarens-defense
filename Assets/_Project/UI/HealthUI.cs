using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Image healthBar;

    private void OnEnable()
    {
        if (health != null)
            health.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= HandleHealthChanged;
    }

    private void Start()
    {
        if (health != null)
            HandleHealthChanged(health.CurrentHealth);
    }

    public void Bind(Health playerHealth)
    {
        if (health != null)
            health.OnHealthChanged -= HandleHealthChanged;

        health = playerHealth;

        if (health != null && isActiveAndEnabled)
            health.OnHealthChanged += HandleHealthChanged;

        if (health != null)
            HandleHealthChanged(health.CurrentHealth);
    }

    private void HandleHealthChanged(float currentHealth)
    {
        if (healthBar == null || health == null)
            return;

        healthBar.fillAmount = currentHealth / health.MaxHealth;
    }
}
