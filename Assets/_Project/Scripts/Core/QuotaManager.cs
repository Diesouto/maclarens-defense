using System;
using UnityEngine;

public class QuotaManager : MonoBehaviour
{
    public static QuotaManager Instance { get; private set; }

    [SerializeField] private int currentQuota = 100;

    public int CurrentQuota => currentQuota;
    public int CurrentCargoValue { get; private set; }
    public bool QuotaMet => CurrentCargoValue >= currentQuota;

    public event Action OnQuotaProgressChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetQuota(int quota)
    {
        currentQuota = Mathf.Max(quota, 0);
        OnQuotaProgressChanged?.Invoke();
    }

    public void SetCargoValue(int value)
    {
        value = Mathf.Max(value, 0);
        if (value == CurrentCargoValue)
            return;

        CurrentCargoValue = value;
        OnQuotaProgressChanged?.Invoke();
    }
}
