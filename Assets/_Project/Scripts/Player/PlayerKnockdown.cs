using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Temporary ragdoll from explosions (or any future knockback). The host decides, every peer ragdolls
// its own copy (ragdolls are per-peer presentation), and the owner stands back up where the hips
// landed once the timer ends, if still alive.
[RequireComponent(typeof(Health))]
public class PlayerKnockdown : NetworkBehaviour
{
    [SerializeField, Min(0.1f)] private float minimumUpForce = 0.35f;
    [SerializeField] private LayerMask groundMask = ~0;

    private Health health;
    private CharacterRagdollController ragdoll;
    private PlayerController playerController;
    private PlayerMotor motor;
    private Coroutine recoverRoutine;

    public bool IsKnockedDown { get; private set; }

    private void Awake()
    {
        health = GetComponent<Health>();
        ragdoll = GetComponent<CharacterRagdollController>();
        playerController = GetComponent<PlayerController>();
        motor = GetComponent<PlayerMotor>();
    }

    private void OnEnable()
    {
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        health.OnDeath -= HandleDeath;
    }

    public void KnockDownFromServer(Vector3 origin, float force, float duration)
    {
        if (NetworkRole.IsClientOnly || health.IsDead || duration <= 0f)
            return;

        if (IsSpawned)
            KnockDownRpc(origin, force, duration);
        else
            ApplyKnockDown(origin, force, duration);
    }

    [Rpc(SendTo.Everyone)]
    private void KnockDownRpc(Vector3 origin, float force, float duration)
    {
        ApplyKnockDown(origin, force, duration);
    }

    private void ApplyKnockDown(Vector3 origin, float force, float duration)
    {
        if (health.IsDead || ragdoll == null)
            return;

        Vector3 direction = transform.position - origin;
        direction.y = Mathf.Max(direction.y, 0f);
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        direction = (direction + Vector3.up * minimumUpForce).normalized;

        if (!IsKnockedDown)
        {
            IsKnockedDown = true;
            ragdoll.EnableRagdoll(direction, force);
            if (playerController != null)
                playerController.SetKnockedDown(true);
        }
        else if (ragdoll.RootRigidbody != null)
        {
            ragdoll.RootRigidbody.AddForce(direction * force, ForceMode.Impulse);
        }

        if (recoverRoutine != null)
            StopCoroutine(recoverRoutine);
        recoverRoutine = StartCoroutine(RecoverAfter(duration));
    }

    private IEnumerator RecoverAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        recoverRoutine = null;

        if (health.IsDead)
            yield break;

        Vector3 standPosition = ragdoll.GetStandPosition(groundMask);
        Quaternion standRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        ragdoll.DisableRagdoll();
        IsKnockedDown = false;

        // Movement is owner-authoritative: only the owner moves the root; others follow via NetworkTransform.
        bool ownsMovement = !IsSpawned || IsOwner;
        if (ownsMovement && motor != null)
            motor.Teleport(standPosition, standRotation);

        if (playerController != null)
            playerController.SetKnockedDown(false);
    }

    private void HandleDeath()
    {
        if (recoverRoutine != null)
        {
            StopCoroutine(recoverRoutine);
            recoverRoutine = null;
        }

        IsKnockedDown = false;
    }
}
