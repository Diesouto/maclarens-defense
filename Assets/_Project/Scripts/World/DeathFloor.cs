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
        Health health = other.GetComponent<Health>();

        if (health == null)
            return;

        health.TakeDamage(health.MaxHealth, Vector3.up, 0f);
    }
}
