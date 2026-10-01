using System;
using UnityEngine;

// Threat = pressure the crew is generating by staying and looting in town.
// MVP scope: time in town + first pickup per loot item. No decay from kills, no reset from anything but a new day.
public class ThreatManager : MonoBehaviour
{
    public static ThreatManager Instance { get; private set; }

    [SerializeField] private float threatPerSecond = 0.2f;
    [SerializeField] private float lootPickupThreat = 5f;
    [SerializeField] private float enemyKillThreat = 1f;
    [SerializeField] private float maxThreat = 100f;

    [Header("Threat Levels")]
    [SerializeField] private float lowThreshold = 20f;
    [SerializeField] private float mediumThreshold = 40f;
    [SerializeField] private float highThreshold = 60f;
    [SerializeField] private float criticalThreshold = 80f;

    public float CurrentThreat { get; private set; }
    public float MaxThreat => maxThreat;
    public ThreatLevel CurrentLevel { get; private set; } = ThreatLevel.Calm;

    public event Action OnThreatChanged;
    public event Action<ThreatLevel> OnThreatLevelChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnQuotaRoundChanged += ResetThreat;
            RunManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnQuotaRoundChanged -= ResetThreat;
            RunManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (NetworkRole.IsClientOnly)
            return;

        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsRunActive)
            return;

        if (!IsThreatActiveInTown())
            return;

        AddThreat(threatPerSecond * Time.deltaTime);
    }

    private bool IsThreatActiveInTown()
    {
        if (!IsThreatPhase())
            return false;

        return InTownTrigger.Instance == null ||
            InTownTrigger.Instance.AnyPlayerInside;
    }

    private static bool IsThreatPhase()
    {
        if (RunManager.Instance == null)
            return true;

        RunPhase phase = RunManager.Instance.CurrentPhase;
        return phase == RunPhase.TravelingToTown || phase == RunPhase.Town;
    }

    // First-time-only pickup threat is enforced by the caller (ItemInstance.HasTriggeredThreat), not here.
    public void RegisterLootPickup()
    {
        if (IsThreatActiveInTown())
            AddThreat(lootPickupThreat);
    }

    // Gunfire draws attention: killing makes the town angrier, not calmer.
    public void RegisterEnemyKill()
    {
        if (IsThreatActiveInTown())
            AddThreat(enemyKillThreat);
    }

    public void RegisterExplosion(float amount)
    {
        if (IsThreatActiveInTown())
            AddThreat(amount);
    }

    private void AddThreat(float amount)
    {
        if (NetworkRole.IsClientOnly)
            return;

        NetworkThreatState networkState = NetworkThreatState.Instance;
        if (networkState != null && networkState.IsSpawned)
            amount *= networkState.CurrentThreatMultiplier;

        if (amount == 0f)
            return;

        CurrentThreat = Mathf.Clamp(CurrentThreat + amount, 0f, maxThreat);
        OnThreatChanged?.Invoke();
        UpdateLevel();
    }

    private void ResetThreat(int day)
    {
        ResetThreat();
    }

    public void ResetThreat()
    {
        if (NetworkRole.IsClientOnly)
            return;

        if (CurrentThreat == 0f && CurrentLevel == ThreatLevel.Calm)
            return;

        CurrentThreat = 0f;
        OnThreatChanged?.Invoke();
        UpdateLevel();
    }

    public void ApplyReplicatedThreat(float threat, ThreatLevel level)
    {
        if (!Mathf.Approximately(CurrentThreat, threat))
        {
            CurrentThreat = threat;
            OnThreatChanged?.Invoke();
        }

        if (CurrentLevel == level)
            return;

        CurrentLevel = level;
        OnThreatLevelChanged?.Invoke(CurrentLevel);
    }

    private void HandlePhaseChanged(RunPhase phase)
    {
        if (!IsThreatPhase())
            ResetThreat();
    }

    private void UpdateLevel()
    {
        ThreatLevel newLevel = ComputeLevel(CurrentThreat);

        if (newLevel == CurrentLevel)
            return;

        CurrentLevel = newLevel;
        OnThreatLevelChanged?.Invoke(CurrentLevel);
    }

    private ThreatLevel ComputeLevel(float threat)
    {
        if (threat >= criticalThreshold)
            return ThreatLevel.Critical;
        if (threat >= highThreshold)
            return ThreatLevel.High;
        if (threat >= mediumThreshold)
            return ThreatLevel.Medium;
        if (threat >= lowThreshold)
            return ThreatLevel.Low;

        return ThreatLevel.Calm;
    }
}
