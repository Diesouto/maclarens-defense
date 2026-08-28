using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class CharacterRagdollController : MonoBehaviour
{
    [SerializeField] private bool enableOnStart;
    [SerializeField] private float disableAnimatorDelay = 0.05f;
    [SerializeField] private bool destroyAfterDeath = false;
    [SerializeField] private float destroyDelay = 30f;

    private Animator animator;
    private List<Rigidbody> ragdollRigidbodies = new List<Rigidbody>();
    private List<Collider> ragdollColliders = new List<Collider>();
    private bool isRagdollActive;

    private void Awake()
    {
        animator = GetChildComponent<Animator>();
        CacheRagdollParts();
        // SetRagdollState(false);
    }

    private void Start()
    {
        if (enableOnStart)
            EnableRagdoll(Vector3.zero, 0f);
    }

    public void EnableRagdoll(Vector3 hitDirection, float forceAmount)
    {
        if (isRagdollActive)
            return;

        isRagdollActive = true;

        if (animator != null)
        {
            if (disableAnimatorDelay > 0f)
                StartCoroutine(DisableAnimatorAfterDelay());
            else
                animator.enabled = false;
        }

        SetRagdollState(true);

        if (forceAmount > 0f)
        {
            Vector3 forceDirection = hitDirection.sqrMagnitude > 0f ? hitDirection.normalized : transform.forward;
            Vector3 force = forceDirection * forceAmount;

            foreach (var rb in ragdollRigidbodies)
            {
                if (rb == null)
                    continue;

                rb.AddForce(force, ForceMode.Impulse);
            }
        }

        if (destroyAfterDeath)
            Destroy(gameObject, destroyDelay);
    }

    private T GetChildComponent<T>() where T : Component
    {
        Transform current = transform;
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            T component = child.GetComponent<T>();
            if (component != null)
                return component;

            component = GetChildComponentInChildren<T>(child);
            if (component != null)
                return component;
        }

        return null;
    }

    private T GetChildComponentInChildren<T>(Transform root) where T : Component
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            T component = child.GetComponent<T>();
            if (component != null)
                return component;

            component = GetChildComponentInChildren<T>(child);
            if (component != null)
                return component;
        }

        return null;
    }

    private System.Collections.IEnumerator DisableAnimatorAfterDelay()
    {
        yield return new WaitForSeconds(disableAnimatorDelay);
        if (animator != null)
            animator.enabled = false;
    }

    private void CacheRagdollParts()
    {
        ragdollRigidbodies.Clear();
        ragdollColliders.Clear();

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>();
        foreach (var rigidbody in rigidbodies)
        {
            if (rigidbody == null)
                continue;

            ragdollRigidbodies.Add(rigidbody);

            Collider collider = rigidbody.GetComponent<Collider>();
            if (collider != null)
                ragdollColliders.Add(collider);
        }

        if (ragdollRigidbodies.Count == 0)
        {
            Rigidbody rootRb = GetComponent<Rigidbody>();
            if (rootRb != null)
                ragdollRigidbodies.Add(rootRb);

            Collider rootCollider = GetComponent<Collider>();
            if (rootCollider != null)
                ragdollColliders.Add(rootCollider);
        }
    }

    private void SetRagdollState(bool enabled)
    {
        foreach (var rb in ragdollRigidbodies)
        {
            if (rb == null)
                continue;

            rb.isKinematic = !enabled;
            rb.detectCollisions = enabled;
        }

        foreach (var collider in ragdollColliders)
        {
            if (collider == null)
                continue;

            collider.enabled = enabled;
        }

        var rootCollider = GetComponent<Collider>();
        if (rootCollider != null)
            rootCollider.enabled = !enabled;

        var rootRigidbody = GetComponent<Rigidbody>();
        if (rootRigidbody != null)
            rootRigidbody.isKinematic = enabled;
    }
}
