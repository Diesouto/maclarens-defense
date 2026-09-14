using UnityEngine;

public class DeathFloor : MonoBehaviour
{
    [SerializeField] private BoxCollider deathZone;

    private void Awake()
    {
        if (deathZone == null)
            deathZone = GetComponent<BoxCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // GetComponentInParent, not GetComponent: ragdoll colliders live on child bones, while
        // Health/PlayerBody sit on the root (alive players have their collider on the root too).
        Health health = other.GetComponentInParent<Health>();

        if (health == null)
            return;

        PlayerBody body = other.GetComponentInParent<PlayerBody>();

        // Already a corpse falling into the void: nobody can reach it to carry it out anymore.
        if (body != null && body.IsDead)
        {
            BodyRecoveryManager.Instance?.MarkLost(body);
            return;
        }

        health.TakeDamage(health.MaxHealth, Vector3.up, 0f);

        // A live player who just fell to their death here is equally unreachable.
        if (body != null && body.IsDead)
            BodyRecoveryManager.Instance?.MarkLost(body);
    }
}
