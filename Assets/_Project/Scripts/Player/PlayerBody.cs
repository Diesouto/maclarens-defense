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
        if (IsTeamWiped)
            GameStateManager.Instance?.SetFail(FailCause.TeamWipe);
    }

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
        return "Carry Body";
    }

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

    // Called by whoever resolves Town extraction, before RunManager reaches ResolvingDay.
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

    // Revives this body back into a playable state. A recovered body (never hidden) revives
    // wherever it physically ended up (e.g. aboard the train); an abandoned/lost one (hidden)
    // snaps to the fallback spawn point instead, since its last position is meaningless.
    public void Revive(Transform fallbackSpawnPoint)
    {
        if (!IsDead || NetworkRole.IsClientOnly)
            return;

        bool wasHidden = IsHidden;

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

        if (wasHidden && fallbackSpawnPoint != null)
            TeleportTo(fallbackSpawnPoint);

        health.Revive();
        if (playerController != null)
            playerController.Revive();
    }

    private void TeleportTo(Transform target)
    {
        if (networkPlayer != null && networkPlayer.IsSpawned)
            networkPlayer.TeleportFromServer(target.position, target.rotation);
        else if (TryGetComponent(out PlayerMotor motor))
            motor.Teleport(target.position, target.rotation);
        else
            transform.SetPositionAndRotation(target.position, target.rotation);
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
