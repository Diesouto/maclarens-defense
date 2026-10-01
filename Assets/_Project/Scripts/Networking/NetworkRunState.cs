using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(RunManager))]
public class NetworkRunState : NetworkBehaviour
{
    // Clients tick the timer locally, so the replicated value only needs coarse corrections.
    private const float TimeSyncThreshold = 0.5f;

    public NetworkVariable<int> QuotaRound = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<RunPhase> CurrentPhase = new(
        RunPhase.MacLarens,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> QuotasCompleted = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> QuotasToWin = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> TimeRemaining = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsTimerRunning = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> HasTimerStarted = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> ElapsedRunSeconds = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkList<PlayerRunStats> PlayerStats;

    private RunManager runManager;

    private void Awake()
    {
        runManager = GetComponent<RunManager>();
        PlayerStats = new NetworkList<PlayerRunStats>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            runManager.Stats.OnStatsChanged += SyncStats;
            SyncFromManager();
            SyncStats();
            return;
        }

        QuotaRound.OnValueChanged += HandleIntChanged;
        CurrentPhase.OnValueChanged += HandlePhaseChanged;
        QuotasCompleted.OnValueChanged += HandleIntChanged;
        QuotasToWin.OnValueChanged += HandleIntChanged;
        TimeRemaining.OnValueChanged += HandleFloatChanged;
        IsTimerRunning.OnValueChanged += HandleBoolChanged;
        HasTimerStarted.OnValueChanged += HandleBoolChanged;
        ElapsedRunSeconds.OnValueChanged += HandleIntChanged;
        PlayerStats.OnListChanged += HandleStatsChanged;
        ApplyState();
        ApplyStats();
    }

    public override void OnNetworkDespawn()
    {
        runManager.Stats.OnStatsChanged -= SyncStats;
        QuotaRound.OnValueChanged -= HandleIntChanged;
        CurrentPhase.OnValueChanged -= HandlePhaseChanged;
        QuotasCompleted.OnValueChanged -= HandleIntChanged;
        QuotasToWin.OnValueChanged -= HandleIntChanged;
        TimeRemaining.OnValueChanged -= HandleFloatChanged;
        IsTimerRunning.OnValueChanged -= HandleBoolChanged;
        HasTimerStarted.OnValueChanged -= HandleBoolChanged;
        ElapsedRunSeconds.OnValueChanged -= HandleIntChanged;
        PlayerStats.OnListChanged -= HandleStatsChanged;
    }

    private void Update()
    {
        if (IsServer && IsSpawned)
            SyncFromManager();
    }

    private void SyncFromManager()
    {
        if (QuotaRound.Value != runManager.CurrentQuotaRound)
            QuotaRound.Value = runManager.CurrentQuotaRound;
        if (CurrentPhase.Value != runManager.CurrentPhase)
            CurrentPhase.Value = runManager.CurrentPhase;
        if (QuotasCompleted.Value != runManager.QuotasCompleted)
            QuotasCompleted.Value = runManager.QuotasCompleted;
        if (QuotasToWin.Value != runManager.QuotasToWin)
            QuotasToWin.Value = runManager.QuotasToWin;

        bool runningChanged = IsTimerRunning.Value != runManager.IsTimerRunning;
        if (runningChanged ||
            !runManager.IsTimerRunning && !Mathf.Approximately(TimeRemaining.Value, runManager.TimeRemaining) ||
            Mathf.Abs(TimeRemaining.Value - runManager.TimeRemaining) >= TimeSyncThreshold)
        {
            TimeRemaining.Value = runManager.TimeRemaining;
        }

        if (runningChanged)
            IsTimerRunning.Value = runManager.IsTimerRunning;
        if (HasTimerStarted.Value != runManager.HasTimerStarted)
            HasTimerStarted.Value = runManager.HasTimerStarted;

        int elapsed = Mathf.FloorToInt(runManager.ElapsedRunSeconds);
        if (ElapsedRunSeconds.Value != elapsed)
            ElapsedRunSeconds.Value = elapsed;
    }

    private void SyncStats()
    {
        if (!IsServer || !IsSpawned)
            return;

        PlayerStats.Clear();
        foreach (PlayerRunStats entry in runManager.Stats.All)
            PlayerStats.Add(entry);
    }

    private void HandleIntChanged(int previous, int current) => ApplyState();

    private void HandleFloatChanged(float previous, float current) => ApplyState();

    private void HandleBoolChanged(bool previous, bool current) => ApplyState();

    private void HandlePhaseChanged(RunPhase previous, RunPhase current) => ApplyState();

    private void HandleStatsChanged(NetworkListEvent<PlayerRunStats> change) => ApplyStats();

    private void ApplyState()
    {
        runManager.ApplyReplicatedRunState(
            QuotaRound.Value,
            CurrentPhase.Value,
            QuotasCompleted.Value,
            QuotasToWin.Value,
            TimeRemaining.Value,
            IsTimerRunning.Value,
            HasTimerStarted.Value,
            ElapsedRunSeconds.Value);
    }

    private void ApplyStats()
    {
        var entries = new List<PlayerRunStats>(PlayerStats.Count);
        foreach (PlayerRunStats entry in PlayerStats)
            entries.Add(entry);

        runManager.Stats.ApplyReplicated(entries);
    }
}