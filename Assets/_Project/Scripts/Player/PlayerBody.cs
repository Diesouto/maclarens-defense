using System.Collections.Generic;
using UnityEngine;

// Owns the "recoverable corpse" state of a dead player: whether the body is being carried,
// whether it made it out of Town, and reviving it back into a controllable player.
// Health/ragdoll stay dumb (they don't know about carrying, quota penalties or MacLarens);
// this component is the only one that reacts to death with run-specific rules.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerController))]
public class PlayerBody : MonoBehaviour, IInteractable
{
    private static readonly List<PlayerBody> allBodies = new();
    public static IReadOnlyList<PlayerBody> AllBodies => allBodies;

    [SerializeField, Min(0f)] private float carryHoldDuration = 2f;
    [SerializeField] private LayerMask respawnGroundMask = ~0;
    [SerializeField, Min(0.1f)] private float groundSnapHeight = 3f;
    [SerializeField, Min(0f)] private float respawnGroundOffset = 0.1f;

    // A wipe only makes sense once at least one player has actually spawned.
    public static bool IsTeamWiped
    {
        get
        {
            if (allBodies.Count == 0)
                return false;

            foreach (PlayerBody body in allBodies)
            {
                if (body != null && !body.IsDead)
                    return false;
            }

            return true;
        }
    }

    private Health health;
    private PlayerController playerController;
    private CharacterRagdollController ragdollController;
    private NetworkPlayer networkPlayer;
    private readonly List<Renderer> hiddenRenderers = new();
    private readonly List<Collider> hiddenColliders = new();

    public bool IsDead => health != null && health.IsDead;
    public bool IsHidden { get; private set; }
    public BodyCarrier Carrier { get; private set; }
    public bool IsBeingCarried => Carrier != null;

    // Where the corpse actually is: the ragdoll's hips, not the root it died on.
    public Vector3 BodyPosition => ragdollController != null && ragdollController.IsRagdollActive &&
        ragdollController.RootRigidbody != null
            ? ragdollController.RootRigidbody.position
            : transform.position;

    // Set once a manager has queued this body for a MacLarens respawn, so it isn't queued twice
    // (e.g. lost down a DeathFloor mid-day, then still found "inside Town" at extraction).
    public bool IsPendingRespawn { get; private set; }

    private void Awake()
    {
        health = GetComponent<Health>();
        playerController = GetComponent<PlayerController>();
        ragdollController = GetComponent<CharacterRagdollController>();
        networkPlayer = GetComponent<NetworkPlayer>();

        allBodies.Add(this);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += HandleDeath;
            health.OnRevived += HandleRevived;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
            health.OnRevived -= HandleRevived;
        }
    }

    private void OnDestroy()
    {
        allBodies.Remove(this);
    }

    private void HandleDeath()
    {
        // A dead carrier must not keep dragging a body around (or take it into Town extraction).
        if (!NetworkRole.IsClientOnly && TryGetComponent(out BodyCarrier carrier) && carrier.IsCarryingBody)
            carrier.Drop();

        BodyRecoveryManager.Instance?.NotifyDeath(this);

        if (IsRunActive && !NetworkRole.IsClientOnly)
            RunManager.Instance?.Stats.RecordDeath(StatsClientId, StatsPlayerName);

        if (IsTeamWiped)
            GameStateManager.Instance?.SetFail(FailCause.TeamWipe);
    }

    public ulong StatsClientId => networkPlayer != null ? networkPlayer.StatsClientId : 0;
    public string StatsPlayerName => networkPlayer != null ? networkPlayer.PlayerName : name;

    // Deaths caused by the run ending (time out) must not count against the crew.
    private static bool IsRunActive => GameStateManager.Instance == null || GameStateManager.Instance.IsRunActive;

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (!IsDead || IsHidden || IsBeingCarried || interactor == null)
            return false;

        Health interactorHealth = interactor.GetComponent<Health>();
        if (interactorHealth != null && interactorHealth.IsDead)
            return false;

        BodyCarrier carrier = interactor.GetComponent<BodyCarrier>();
        return carrier != null && !carrier.IsCarryingBody;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        return "Cargar Cuerpo";
    }

    float IInteractable.HoldDuration => carryHoldDuration;

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        BodyCarrier carrier = interactor.GetComponent<BodyCarrier>();
        carrier?.TryPickUp(this);
    }

    public void AttachTo(BodyCarrier carrier)
    {
        Carrier = carrier;
    }

    public void Detach()
    {
        Carrier = null;
    }

    public void MarkPendingRespawn()
    {
        IsPendingRespawn = true;
    }

    // Called by whoever resolves Town extraction, before the train is back at MacLarens.
    public void Hide()
    {
        if (Carrier != null)
            Carrier.Drop();

        ApplyHidden(true);
        if (networkPlayer != null)
            networkPlayer.SetBodyHiddenOnServer(true);
    }

    // Hides visuals/colliders instead of deactivating: a disabled NetworkObject stops replicating.
    public void ApplyHidden(bool hidden)
    {
        if (IsHidden == hidden)
            return;

        IsHidden = hidden;

        if (hidden)
        {
            hiddenRenderers.Clear();
            foreach (Renderer bodyRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!bodyRenderer.enabled)
                    continue;

                hiddenRenderers.Add(bodyRenderer);
                bodyRenderer.enabled = false;
            }

            hiddenColliders.Clear();
            foreach (Collider bodyCollider in GetComponentsInChildren<Collider>(true))
            {
                if (!bodyCollider.enabled)
                    continue;

                hiddenColliders.Add(bodyCollider);
                bodyCollider.enabled = false;
            }

            return;
        }

        foreach (Renderer bodyRenderer in hiddenRenderers)
        {
            if (bodyRenderer != null)
                bodyRenderer.enabled = true;
        }

        foreach (Collider bodyCollider in hiddenColliders)
        {
            if (bodyCollider != null)
                bodyCollider.enabled = true;
        }

        hiddenRenderers.Clear();
        hiddenColliders.Clear();
    }

    // Revives this body back into a playable state. A recovered body (never hidden) stands up where
    // its ragdoll lies (e.g. aboard the train, or where a potion hit it); an abandoned/lost one
    // (hidden) snaps to the fallback spawn point instead, since its last position is meaningless.
    // respawnAtSpawn forces the spawn pose even for a recovered body (used for the MacLarens return).
    public void Revive(Transform fallbackSpawnPoint, float healthFraction = 1f, bool respawnAtSpawn = false)
    {
        if (!IsDead || NetworkRole.IsClientOnly)
            return;

        bool wasHidden = IsHidden;
        Vector3 standPosition = ragdollController != null ? ragdollController.GetStandPosition(~0) : transform.position;
        Quaternion standRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (Carrier != null)
            Carrier.Drop();

        if (wasHidden)
        {
            ApplyHidden(false);
            if (networkPlayer != null)
                networkPlayer.SetBodyHiddenOnServer(false);
        }

        IsPendingRespawn = false;
        if (ragdollController != null)
            ragdollController.DisableRagdoll();

        if (wasHidden || respawnAtSpawn)
        {
            // An abandoned body must never keep the ragdoll's last position: it died under the map or off-limits.
            ResolveRespawnPose(fallbackSpawnPoint, out Vector3 respawnPosition, out Quaternion respawnRotation);
            TeleportTo(respawnPosition, respawnRotation);
        }
        else
        {
            TeleportTo(standPosition, standRotation);
        }

        health.Revive(healthFraction);
        if (playerController != null)
            playerController.Revive();

        if (IsRunActive)
            RunManager.Instance?.Stats.RecordRevive(StatsClientId, StatsPlayerName);
    }

    private void ResolveRespawnPose(Transform fallbackSpawnPoint, out Vector3 position, out Quaternion rotation)
    {
        if (fallbackSpawnPoint != null)
        {
            position = fallbackSpawnPoint.position;
            rotation = fallbackSpawnPoint.rotation;
        }
        else if (networkPlayer != null && networkPlayer.HasInitialSpawnPose)
        {
            position = networkPlayer.InitialSpawnPosition;
            rotation = networkPlayer.InitialSpawnRotation;
        }
        else
        {
            Debug.LogWarning($"{nameof(PlayerBody)}: no respawn point and no cached spawn pose; reviving in place.", this);
            position = transform.position;
            rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            return;
        }

        if (Physics.Raycast(position + Vector3.up * groundSnapHeight, Vector3.down,
                out RaycastHit hit, groundSnapHeight * 2f, respawnGroundMask, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * respawnGroundOffset;
        }
    }

    private void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (networkPlayer != null && networkPlayer.IsSpawned)
            networkPlayer.TeleportFromServer(position, rotation);
        else if (TryGetComponent(out PlayerMotor motor))
            motor.Teleport(position, rotation);
        else
            transform.SetPositionAndRotation(position, rotation);
    }

    // Remote peers only see the replicated health come back; mirror the host-side revive locally.
    private void HandleRevived()
    {
        if (!NetworkRole.IsClientOnly)
            return;

        IsPendingRespawn = false;
        if (ragdollController != null)
            ragdollController.DisableRagdoll();
        if (playerController != null)
            playerController.Revive();
    }
}
