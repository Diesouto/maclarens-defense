using System;
using UnityEngine;

// Owns the timed-quota loop: quota rounds, the quota timer, win/lose checks and run stats.
// Server-authoritative; clients only mirror state through ApplyReplicatedRunState.
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Tooltip("Quota amounts and time limits. When empty a default config is used.")]
    [SerializeField] private RunConfigSO runConfig;
    [Tooltip("Pause the quota timer while the train is at MacLarens.")]
    [SerializeField] private bool pauseTimerAtMacLarens = true;

    public int CurrentQuotaRound { get; private set; } = 1;
    public int QuotasCompleted { get; private set; }
    public int QuotasToWin { get; private set; }
    // Locked when the run starts so late joins/leaves don't shift the quota mid-run.
    public int PlayerCount { get; private set; } = 1;
    public RunConfigSO.PlayerScaling Scaling => runConfig.GetPlayerScaling(PlayerCount);
    public bool IsInfinite => QuotasToWin == RunSettings.InfiniteQuotas;
    public RunPhase CurrentPhase { get; private set; } = RunPhase.MacLarens;

    public float TimeRemaining { get; private set; }
    public bool IsTimerRunning { get; private set; }
    // Hidden until the train first leaves for the current quota.
    public bool HasTimerStarted { get; private set; }
    public bool IsInitialized { get; private set; }
    public float ElapsedRunSeconds { get; private set; }

    public RunStatsTracker Stats { get; } = new();

    public event Action<int> OnQuotaRoundChanged;
    public event Action<RunPhase> OnPhaseChanged;
    public event Action OnRunStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (runConfig == null)
            runConfig = ScriptableObject.CreateInstance<RunConfigSO>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
    }

    private void Start()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;

        GameStateManager.Instance?.StartRun();
        SetPhase(RunPhase.MacLarens);

        if (IsNetworkClient())
            return;

        Unity.Netcode.NetworkManager network = Unity.Netcode.NetworkManager.Singleton;
        PlayerCount = network != null && network.IsListening
            ? Mathf.Clamp(network.ConnectedClientsIds.Count, 1, 4)
            : 1;

        QuotasToWin = RunSettings.HasValue ? RunSettings.QuotasToWin : runConfig.DefaultQuotasToWin;
        StartQuotaRound(0f);
    }

    private void Update()
    {
        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsRunActive)
            return;

        float deltaTime = Time.deltaTime;
        ElapsedRunSeconds += deltaTime;

        if (!IsTimerRunning)
            return;

        // Clients count down locally between replicated snapshots so the HUD stays smooth.
        TimeRemaining = Mathf.Max(TimeRemaining - deltaTime, 0f);

        if (TimeRemaining <= 0f && !IsNetworkClient())
            HandleTimeExpired();
    }

    // Pushes the current round's base quota without touching the timer; safe to call repeatedly.
    public void ApplyCurrentQuota()
    {
        if (IsNetworkClient())
            return;

        QuotaManager.Instance?.SetQuota(runConfig.GetQuota(CurrentQuotaRound, PlayerCount));
    }

    private void StartQuotaRound(float carriedOverSeconds)
    {
        ApplyCurrentQuota();
        TimeRemaining = runConfig.GetTimeLimit(CurrentQuotaRound) + Mathf.Max(carriedOverSeconds, 0f);
        IsTimerRunning = false;
        HasTimerStarted = false;
        IsInitialized = true;
        OnQuotaRoundChanged?.Invoke(CurrentQuotaRound);
        OnRunStateChanged?.Invoke();
    }

    public void BeginDeparture(bool returningToMacLarens)
    {
        if (IsNetworkClient())
            return;

        SetPhase(returningToMacLarens
            ? RunPhase.LeavingTown
            : RunPhase.TravelingToTown);

        if (!returningToMacLarens && !IsTimerRunning)
        {
            IsTimerRunning = true;
            HasTimerStarted = true;
            OnRunStateChanged?.Invoke();
        }
    }

    public void HandleTrainArrived(TrainDestination destination)
    {
        if (IsNetworkClient())
            return;

        SetPhase(destination == TrainDestination.Town
            ? RunPhase.Town
            : RunPhase.MacLarens);

        if (destination != TrainDestination.MacLarens || !pauseTimerAtMacLarens || !IsTimerRunning)
            return;

        IsTimerRunning = false;
        OnRunStateChanged?.Invoke();
    }

    public void HandleTownExit()
    {
        if (IsNetworkClient())
            return;

        if (CurrentPhase == RunPhase.LeavingTown)
            SetPhase(RunPhase.ReturningToMacLarens);
    }

    public bool CanPayQuota =>
        CurrentPhase == RunPhase.MacLarens &&
        (GameStateManager.Instance == null || GameStateManager.Instance.IsRunActive) &&
        QuotaManager.Instance != null && QuotaManager.Instance.QuotaMet;

    public bool PayQuota()
    {
        if (IsNetworkClient() || !CanPayQuota || MoneyManager.Instance == null)
            return false;

        float timeLeft = TimeRemaining;
        if (!QuotaManager.Instance.TryPayCurrentQuota(MoneyManager.Instance))
            return false;

        QuotasCompleted++;

        if (!IsInfinite && QuotasCompleted >= QuotasToWin)
        {
            IsTimerRunning = false;
            OnRunStateChanged?.Invoke();
            GameStateManager.Instance?.SetSuccess();
            return true;
        }

        CurrentQuotaRound++;
        StartQuotaRound(timeLeft * runConfig.TimeCarryOverFraction);
        return true;
    }

    private void HandleTimeExpired()
    {
        TimeRemaining = 0f;
        IsTimerRunning = false;
        OnRunStateChanged?.Invoke();

        // Fail first: the resulting wipe must not overwrite the cause with TeamWipe.
        GameStateManager.Instance?.SetFail(FailCause.TimeExpired);
        KillAllPlayers();
    }

    private static void KillAllPlayers()
    {
        foreach (PlayerBody body in PlayerBody.AllBodies)
        {
            if (body == null || body.IsDead || !body.TryGetComponent(out Health health))
                continue;

            health.TakeDamage(health.MaxHealth, Vector3.up, 0f);
        }
    }

    private void HandleGameStateChanged(GameState previousState, GameState nextState)
    {
        if (nextState != GameState.Success && nextState != GameState.Fail)
            return;

        IsTimerRunning = false;

        if (IsNetworkClient())
            return;

        foreach (PlayerBody body in PlayerBody.AllBodies)
        {
            if (body != null)
                Stats.EnsurePlayer(body.StatsClientId, body.StatsPlayerName);
        }

        OnRunStateChanged?.Invoke();
    }

    private void SetPhase(RunPhase nextPhase)
    {
        if (CurrentPhase == nextPhase)
            return;

        CurrentPhase = nextPhase;
        OnPhaseChanged?.Invoke(nextPhase);
    }

    public void ApplyReplicatedRunState(int quotaRound, RunPhase phase, int quotasCompleted, int quotasToWin,
        float timeRemaining, bool timerRunning, bool timerStarted, float elapsedRunSeconds)
    {
        bool roundChanged = CurrentQuotaRound != quotaRound;

        CurrentQuotaRound = Mathf.Max(quotaRound, 1);
        QuotasCompleted = quotasCompleted;
        QuotasToWin = quotasToWin;
        TimeRemaining = timeRemaining;
        IsTimerRunning = timerRunning;
        HasTimerStarted = timerStarted;
        IsInitialized = true;
        ElapsedRunSeconds = elapsedRunSeconds;
        SetPhase(phase);

        if (roundChanged)
            OnQuotaRoundChanged?.Invoke(CurrentQuotaRound);

        OnRunStateChanged?.Invoke();
    }

    private static bool IsNetworkClient() => NetworkRole.IsClientOnly;
}
