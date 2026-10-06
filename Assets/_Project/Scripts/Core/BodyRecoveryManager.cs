using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Resolves what happens to dead players' bodies once Town is left behind: a body still inside
// Town at that moment is abandoned (quota penalty, respawns later); a body already carried out
// (e.g. aboard the departing train) is recovered (no penalty). Both revive once the train is back
// at MacLarens, never earlier.
public class BodyRecoveryManager : MonoBehaviour
{
    public static BodyRecoveryManager Instance { get; private set; }

    [SerializeField] private Transform macLarensRespawnPoint;
    [SerializeField, Min(0)] private int lostBodyMoneyPenalty = 500;
    [Tooltip("Players who die while the train is at MacLarens (never inside Town) respawn on their own after this delay.")]
    [SerializeField, Min(0f)] private float stationRespawnDelay = 5f;

    private RunManager runManager;
    private bool subscribedToRun;
    private struct PendingRespawn
    {
        public PlayerBody Body;
        public bool WithPenalty;
    }

    private readonly List<PendingRespawn> pendingRespawns = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        runManager = RunManager.Instance;
    }

    private void OnEnable()
    {
        SubscribeToRun();
    }

    // RunManager may not have awoken yet during OnEnable, which silently skipped the arrival revive.
    private void Start()
    {
        SubscribeToRun();
    }

    private void SubscribeToRun()
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        if (runManager == null || subscribedToRun)
            return;

        runManager.OnPhaseChanged += HandlePhaseChanged;
        subscribedToRun = true;
    }

    private void OnDisable()
    {
        if (runManager != null && subscribedToRun)
            runManager.OnPhaseChanged -= HandlePhaseChanged;

        subscribedToRun = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Called once by TownExtractionResolver right after stragglers are abandoned, so freshly
    // killed players are included alongside anyone who died earlier in the day.
    public void ResolveBodiesAtTownExit()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
            return;

        foreach (PlayerBody body in PlayerBody.AllBodies)
        {
            if (body == null || !body.IsDead || body.IsPendingRespawn)
                continue;

            PlayerController owner = body.GetComponent<PlayerController>();
            bool leftBehind = (InTownTrigger.Instance == null || InTownTrigger.Instance.IsPlayerInsideTown(owner)) &&
                !IsRecoverable(body);

            if (leftBehind)
                body.Hide();

            QueueRespawn(body, leftBehind);
        }
    }

    // Called by DeathFloor (or any other hazard) for a corpse that fell somewhere unreachable:
    // always counts as abandoned, regardless of whether Town has been resolved yet.
    public void MarkLost(PlayerBody body)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
            return;

        if (body == null || !body.IsDead || body.IsPendingRespawn)
            return;

        if (runManager == null)
            runManager = RunManager.Instance;

        // At the station nothing would ever flush the pending queue, so respawn right away.
        if (runManager != null && runManager.CurrentPhase == RunPhase.MacLarens)
        {
            body.Revive(macLarensRespawnPoint, 1f, true);
            return;
        }

        body.Hide();
        QueueRespawn(body, withPenalty: true);
    }

    // Deaths at the station never touch Town extraction and the arrival revive already happened,
    // so nothing else would ever bring these players back.
    public void NotifyDeath(PlayerBody body)
    {
        if (NetworkRole.IsClientOnly || body == null)
            return;

        if (runManager == null)
            runManager = RunManager.Instance;

        if (runManager == null || runManager.CurrentPhase != RunPhase.MacLarens)
            return;

        StartCoroutine(RespawnAtStationAfterDelay(body));
    }

    private System.Collections.IEnumerator RespawnAtStationAfterDelay(PlayerBody body)
    {
        yield return new WaitForSeconds(stationRespawnDelay);

        bool runActive = GameStateManager.Instance == null || GameStateManager.Instance.IsRunActive;
        if (body != null && runActive && body.IsDead && !body.IsPendingRespawn &&
            runManager.CurrentPhase == RunPhase.MacLarens)
            body.Revive(macLarensRespawnPoint, 1f, true);
    }

    private void QueueRespawn(PlayerBody body, bool withPenalty)
    {
        body.MarkPendingRespawn();
        pendingRespawns.Add(new PendingRespawn { Body = body, WithPenalty = withPenalty });
    }

    // The only natural ways home: lying on the train or in a living player's hands.
    private static bool IsRecoverable(PlayerBody body)
    {
        return body.IsBeingCarried || TrainCargo.IsBodyAboard(body);
    }

    private void HandlePhaseChanged(RunPhase phase)
    {
        // Arrival back at MacLarens is the only MacLarens transition that happens mid-run.
        if (phase != RunPhase.MacLarens || NetworkRole.IsClientOnly)
            return;

        foreach (PendingRespawn pending in pendingRespawns)
        {
            if (pending.Body == null)
                continue;

            pending.Body.Revive(macLarensRespawnPoint, 1f, pending.WithPenalty);

            if (pending.WithPenalty)
                MoneyManager.Instance?.ApplyPenalty(lostBodyMoneyPenalty);
        }

        pendingRespawns.Clear();

        // Anyone still dead and not hidden: recovered if aboard/carried, otherwise lost with a penalty.
        foreach (PlayerBody body in PlayerBody.AllBodies)
        {
            if (body == null || !body.IsDead || body.IsHidden)
                continue;

            bool recovered = IsRecoverable(body);
            if (!recovered)
                MoneyManager.Instance?.ApplyPenalty(lostBodyMoneyPenalty);

            body.Revive(macLarensRespawnPoint, 1f, !recovered);
        }
    }
}
