using System;
using UnityEngine;

public class QuotaManager : MonoBehaviour
{
    public static QuotaManager Instance { get; private set; }

    [SerializeField] private int currentQuota = 100;

    public int CurrentQuota => currentQuota;
    public int DeliveredValue { get; private set; }

    public bool QuotaMet => DeliveredValue >= CurrentQuota;

    public event Action OnQuotaProgressChanged;
    public event Action OnQuotaMet;

    private bool quotaWasMet;

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
        DeliveredValue = 0;
        quotaWasMet = false;

        OnQuotaProgressChanged?.Invoke();

        if (currentQuota == 0)
            SetQuotaMet();
    }

    public void AddDeliveredValue(int value)
    {
        if (value <= 0)
            return;

        DeliveredValue += value;

        OnQuotaProgressChanged?.Invoke();

        if (!quotaWasMet && DeliveredValue >= currentQuota)
            SetQuotaMet();
    }

    private void SetQuotaMet()
    {
        quotaWasMet = true;
        OnQuotaMet?.Invoke();
    }
}