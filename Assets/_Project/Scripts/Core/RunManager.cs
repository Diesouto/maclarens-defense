using System;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [SerializeField] private int[] dayQuotas = { 100, 150, 200 };

    public int CurrentDay { get; private set; } = 1;

    public event Action<int> OnDayChanged;

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

    private void Start()
    {
        ApplyQuotaForCurrentDay();
    }

    public void ApplyQuotaForCurrentDay()
    {
        if (dayQuotas == null || dayQuotas.Length == 0)
            return;

        int index = Mathf.Clamp(CurrentDay - 1, 0, dayQuotas.Length - 1);
        int quota = dayQuotas[index];

        if (QuotaManager.Instance != null)
            QuotaManager.Instance.SetQuota(quota);
    }

    public void AdvanceDay()
    {
        CurrentDay++;
        ApplyQuotaForCurrentDay();
        OnDayChanged?.Invoke(CurrentDay);
    }

    public void ResetRun()
    {
        CurrentDay = 1;
        ApplyQuotaForCurrentDay();
        OnDayChanged?.Invoke(CurrentDay);
    }

}
