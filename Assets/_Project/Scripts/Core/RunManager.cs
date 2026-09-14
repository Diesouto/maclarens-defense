using System;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [SerializeField] private int[] dayQuotas = { 100, 150, 200 };
    [Tooltip("If enabled, missing the quota fails the run immediately on any day instead of only on the last one.")]
    [SerializeField] private bool failOnAnyMissedQuota = false;

    public int CurrentDay { get; private set; } = 1;
    public RunPhase CurrentPhase { get; private set; } = RunPhase.MacLarens;
    public int TotalDays => dayQuotas != null ? dayQuotas.Length : 0;

    public event Action<int> OnDayChanged;
    public event Action<RunPhase> OnPhaseChanged;

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
        GameStateManager.Instance?.StartRun();
        SetPhase(RunPhase.MacLarens);
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
        if (CurrentDay >= TotalDays)
            return;

        CurrentDay++;
        SetPhase(RunPhase.MacLarens);
        ApplyQuotaForCurrentDay();
        OnDayChanged?.Invoke(CurrentDay);
    }

    public void ResetRun()
    {
        CurrentDay = 1;
        SetPhase(RunPhase.MacLarens);
        GameStateManager.Instance?.StartRun();
        MoneyManager.Instance?.ResetMoney();
        QuotaManager.Instance?.ResetDebt();
        ApplyQuotaForCurrentDay();
        OnDayChanged?.Invoke(CurrentDay);
    }

    public void BeginDeparture(bool returningToMacLarens)
    {
        SetPhase(returningToMacLarens
            ? RunPhase.LeavingTown
            : RunPhase.TravelingToTown);
    }

    public void HandleTrainArrived(TrainDestination destination)
    {
        SetPhase(destination == TrainDestination.Town
            ? RunPhase.Town
            : RunPhase.ResolvingDay);
    }

    public void HandleTownExit()
    {
        if (CurrentPhase == RunPhase.LeavingTown)
            SetPhase(RunPhase.ReturningToMacLarens);
    }

    public void BeginDayResolution()
    {
        SetPhase(RunPhase.ResolvingDay);
    }

    public void FinishDay()
    {
        if (CurrentPhase != RunPhase.ResolvingDay ||
            GameStateManager.Instance?.IsRunActive == false ||
            QuotaManager.Instance == null ||
            MoneyManager.Instance == null)
        {
            return;
        }

        BeginDayResolution();

        if (!QuotaManager.Instance.TryPayCurrentQuota(MoneyManager.Instance))
        {
            if (failOnAnyMissedQuota || CurrentDay >= TotalDays)
                GameStateManager.Instance?.SetFail(FailCause.QuotaFailed);
            else
                AdvanceDay();

            return;
        }

        if (QuotaManager.Instance.DebtRemaining <= 0)
        {
            GameStateManager.Instance?.SetSuccess();
            return;
        }

        AdvanceDay();
    }

    private void SetPhase(RunPhase nextPhase)
    {
        if (CurrentPhase == nextPhase)
            return;

        CurrentPhase = nextPhase;
        OnPhaseChanged?.Invoke(nextPhase);
    }

}
