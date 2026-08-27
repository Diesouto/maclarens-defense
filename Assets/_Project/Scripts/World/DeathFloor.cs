using UnityEngine;

public class DeathFloor : MonoBehaviour
{
    [SerializeField] BoxCollider deathZone;

    private void Awake()
    {
        if (deathZone == null)
            deathZone = GetComponent<BoxCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Health>() != null)
        {
            var health = other.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(health.MaxHealth, Vector3.up, 0f);
            }
        }
    }
}
