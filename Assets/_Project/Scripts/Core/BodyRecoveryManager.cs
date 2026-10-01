using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Resolves what happens to dead players' bodies once Town is left behind: a body still inside
// Town at that moment is abandoned (quota penalty, respawns later); a body already carried out
// (e.g. aboard the departing train) is recovered (no penalty). Both revive once the run reaches
// ResolvingDay (train arrived at MacLarens), never earlier.
public class BodyRecoveryManager : MonoBehaviour
{
    public static BodyRecoveryManager Instance { get; private set; }

    [SerializeField] private Transform macLarensRespawnPoint;
    [SerializeField, Min(0)] private int abandonedBodyQuotaPenalty = 500;

    private RunManager runManager;

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
        if (runManager == null)
            runManager = RunManager.Instance;

        if (runManager != null)
            runManager.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.OnPhaseChanged -= HandlePhaseChanged;
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
            bool leftBehind = InTownTrigger.Instance == null || InTownTrigger.Instance.IsPlayerInsideTown(owner);

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

        body.Hide();
        QueueRespawn(body, withPenalty: true);
    }

    private void QueueRespawn(PlayerBody body, bool withPenalty)
    {
        body.MarkPendingRespawn();
        pendingRespawns.Add(new PendingRespawn { Body = body, WithPenalty = withPenalty });
    }

    private void HandlePhaseChanged(RunPhase phase)
    {
        if (phase != RunPhase.ResolvingDay)
            return;

        foreach (PendingRespawn pending in pendingRespawns)
        {
            if (pending.Body == null)
                continue;

            pending.Body.Revive(macLarensRespawnPoint);

            if (pending.WithPenalty)
                QuotaManager.Instance?.AddQuotaModifier(abandonedBodyQuotaPenalty, "jugador abandonado");
        }

        pendingRespawns.Clear();

        // Anyone who died after leaving Town (on the train, in MacLarens) made it back: free revive.
        foreach (PlayerBody body in PlayerBody.AllBodies)
        {
            if (body != null && body.IsDead && !body.IsHidden)
                body.Revive(macLarensRespawnPoint);
        }
    }
}
