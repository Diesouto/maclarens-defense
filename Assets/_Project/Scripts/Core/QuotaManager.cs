using System;
using UnityEngine;

public class QuotaManager : MonoBehaviour
{
    public static QuotaManager Instance { get; private set; }

    [SerializeField] private int currentQuota = 100;

    public int CurrentQuota => currentQuota;

    // Value currently stored across ALL train cargos.
    public int CurrentCargoValue { get; private set; }

    // Value already delivered to the delivery point.
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
        currentQuota = Mathf.Max(currentQuota, 0);
    }

    private void Start()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.ApplyQuotaForCurrentDay();
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
        CurrentCargoValue = 0;
        quotaWasMet = false;

        OnQuotaProgressChanged?.Invoke();

        if (currentQuota == 0)
            SetQuotaMet();
    }

    public void AddCargoValue(int value)
    {
        if (value == 0)
            return;

        CurrentCargoValue = Mathf.Max(CurrentCargoValue + value, 0);

        OnQuotaProgressChanged?.Invoke();
    }

    public void AddDeliveredValue(int value)
    {
        if (value <= 0)
            return;

        DeliveredValue += value;

        OnQuotaProgressChanged?.Invoke();

        if (!quotaWasMet && DeliveredValue >= CurrentQuota)
            SetQuotaMet();
    }

    private void SetQuotaMet()
    {
        if (quotaWasMet)
            return;

        quotaWasMet = true;
        OnQuotaMet?.Invoke();
    }
}