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

    public bool IsDead => health != null && health.IsDead;
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

        allBodies.Add(this);
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
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
        if (!IsDead || IsBeingCarried || interactor == null)
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

        gameObject.SetActive(false);
    }

    // Revives this body back into a playable state. A recovered body (never hidden) revives
    // wherever it physically ended up (e.g. aboard the train); an abandoned/lost one (hidden)
    // snaps to the fallback spawn point instead, since its last position is meaningless.
    public void Revive(Transform fallbackSpawnPoint)
    {
        if (!IsDead)
            return;

        bool wasHidden = !gameObject.activeSelf;

        if (Carrier != null)
            Carrier.Drop();

        if (wasHidden)
        {
            gameObject.SetActive(true);

            if (fallbackSpawnPoint != null)
                transform.SetPositionAndRotation(fallbackSpawnPoint.position, fallbackSpawnPoint.rotation);
        }

        IsPendingRespawn = false;
        ragdollController?.DisableRagdoll();
        health.Revive();
        playerController?.Revive();
    }
}
