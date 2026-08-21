using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BreakableOnImpact : MonoBehaviour
{
    [SerializeField] private float breakVelocity = 6f;
    [SerializeField] private GameObject brokenPrefab;
    [SerializeField] private AudioClip breakSound;

    private bool hasBroken;

    public void CopySettingsFrom(BreakableOnImpact source)
    {
        if (source == null)
            return;

        breakVelocity = source.breakVelocity;
        brokenPrefab = source.brokenPrefab;
        breakSound = source.breakSound;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasBroken || collision.relativeVelocity.magnitude < breakVelocity)
            return;

        Break();
    }

    private void Break()
    {
        hasBroken = true;

        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, transform.position);

        if (brokenPrefab != null)
            Instantiate(brokenPrefab, transform.position, transform.rotation);

        Destroy(gameObject);
    }
}
