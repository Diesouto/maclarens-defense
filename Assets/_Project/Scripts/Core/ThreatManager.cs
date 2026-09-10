using System;
using UnityEngine;

// Threat = pressure the crew is generating by staying and looting in town.
// MVP scope: time in town + first pickup per loot item. No decay from kills, no reset from anything but a new day.
public class ThreatManager : MonoBehaviour
{
    public static ThreatManager Instance { get; private set; }

    [SerializeField] private float threatPerSecond = 0.2f;
    [SerializeField] private float lootPickupThreat = 5f;
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
            RunManager.Instance.OnDayChanged += ResetThreat;
            RunManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnDayChanged -= ResetThreat;
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
        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsRunActive)
            return;

        if (!IsThreatActiveInTown())
            return;

        AddThreat(threatPerSecond * Time.deltaTime);
    }

    private bool IsThreatActiveInTown()
    {
        if (RunManager.Instance != null &&
            RunManager.Instance.CurrentPhase != RunPhase.Town &&
            RunManager.Instance.CurrentPhase != RunPhase.LeavingTown)
        {
            return false;
        }

        return InTownTrigger.Instance == null ||
            InTownTrigger.Instance.AnyPlayerInside;
    }

    // First-time-only pickup threat is enforced by the caller (ItemInstance.HasTriggeredThreat), not here.
    public void RegisterLootPickup()
    {
        AddThreat(lootPickupThreat);
    }

    private void AddThreat(float amount)
    {
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
        if (CurrentThreat == 0f && CurrentLevel == ThreatLevel.Calm)
            return;

        CurrentThreat = 0f;
        OnThreatChanged?.Invoke();
        UpdateLevel();
    }

    private void HandlePhaseChanged(RunPhase phase)
    {
        if (phase != RunPhase.Town)
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
