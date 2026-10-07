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
    private List<Transform> ragdollTransforms = new List<Transform>();
    private List<Vector3> bindLocalPositions = new List<Vector3>();
    private List<Quaternion> bindLocalRotations = new List<Quaternion>();
    private bool isRagdollActive;
    private bool isPhysicsSuspended;
    private Vector3[] bodyPositions;
    private Quaternion[] bodyRotations;

    public bool IsRagdollActive => isRagdollActive;
    public bool IsPhysicsSuspended => isPhysicsSuspended;
    public Rigidbody RootRigidbody { get; private set; }
    public IReadOnlyList<Collider> RagdollColliders => ragdollColliders;
    public IReadOnlyList<Rigidbody> RagdollRigidbodies => ragdollRigidbodies;

    private void Awake()
    {
        animator = GetChildComponent<Animator>();
        CacheRagdollParts();
        PrepareAliveBones();
    }

    // Bone colliders stay enabled (hitboxes) but must not simulate or push the root CharacterController.
    private void PrepareAliveBones()
    {
        Rigidbody rootRb = GetComponent<Rigidbody>();
        Collider rootCollider = GetComponent<Collider>();

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            if (rb != null && rb != rootRb)
                rb.isKinematic = true;
        }

        if (rootCollider == null)
            return;

        foreach (Collider boneCollider in ragdollColliders)
        {
            if (boneCollider != null && boneCollider != rootCollider)
                Physics.IgnoreCollision(rootCollider, boneCollider, true);
        }
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
        SetSkinnedBoundsAlwaysUpdated(true);
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

    // Restores the bind pose and hands animation/collision back to the animator; used on revive.
    public void DisableRagdoll()
    {
        if (!isRagdollActive)
            return;

        isRagdollActive = false;
        isPhysicsSuspended = false;
        SetSkinnedBoundsAlwaysUpdated(false);
        StopAllCoroutines();

        // Kinematic bodies reject velocity writes, so clear motion while they are still dynamic.
        foreach (var rb in ragdollRigidbodies)
        {
            if (rb == null || rb.isKinematic)
                continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        SetRagdollState(false);

        for (int i = 0; i < ragdollTransforms.Count; i++)
        {
            Transform ragdollTransform = ragdollTransforms[i];
            if (ragdollTransform == null)
                continue;

            ragdollTransform.localPosition = bindLocalPositions[i];
            ragdollTransform.localRotation = bindLocalRotations[i];
        }

        if (animator != null)
            animator.enabled = true;
    }

    public void SetPhysicsSuspended(bool suspended)
    {
        if (!isRagdollActive || isPhysicsSuspended == suspended)
            return;

        isPhysicsSuspended = suspended;
        if (suspended && animator != null)
            animator.enabled = false;
        foreach (Rigidbody bone in ragdollRigidbodies)
        {
            if (bone == null)
                continue;

            if (!bone.isKinematic)
            {
                bone.linearVelocity = Vector3.zero;
                bone.angularVelocity = Vector3.zero;
            }

            bone.isKinematic = suspended;
        }
    }

    public void SetBodyPose(Vector3 position, Quaternion rotation)
    {
        if (!isRagdollActive || RootRigidbody == null)
            return;

        Vector3 previousPosition = RootRigidbody.position;
        Quaternion deltaRotation = rotation * Quaternion.Inverse(RootRigidbody.rotation);
        for (int index = 0; index < ragdollRigidbodies.Count; index++)
        {
            Rigidbody bone = ragdollRigidbodies[index];
            if (bone == null)
                continue;

            bodyPositions[index] = bone.position;
            bodyRotations[index] = bone.rotation;
        }

        for (int index = 0; index < ragdollRigidbodies.Count; index++)
        {
            Rigidbody bone = ragdollRigidbodies[index];
            if (bone == null)
                continue;

            Vector3 bonePosition = position + deltaRotation * (bodyPositions[index] - previousPosition);
            Quaternion boneRotation = deltaRotation * bodyRotations[index];
            // Rigidbody writes alone only show up after the next physics step, so the pose stutters at framerate.
            bone.transform.SetPositionAndRotation(bonePosition, boneRotation);
            bone.position = bonePosition;
            bone.rotation = boneRotation;
        }
    }

    // The root stays where the body fell; the ragdoll may have been carried or blown elsewhere.
    public Vector3 GetStandPosition(LayerMask groundMask)
    {
        if (RootRigidbody == null)
            return transform.position;

        Vector3 origin = RootRigidbody.position + Vector3.up;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 5f, groundMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.transform.IsChildOf(transform))
                return hit.point;
        }

        return RootRigidbody.position;
    }

    // Bounds follow the root bone: once the ragdoll is dragged away they go stale and the mesh gets culled.
    private void SetSkinnedBoundsAlwaysUpdated(bool enabled)
    {
        foreach (SkinnedMeshRenderer skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skinned.updateWhenOffscreen = enabled;
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
        ragdollTransforms.Clear();
        bindLocalPositions.Clear();
        bindLocalRotations.Clear();

        Rigidbody ownRigidbody = GetComponent<Rigidbody>();
        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (var rigidbody in rigidbodies)
        {
            if (rigidbody == null || rigidbody == ownRigidbody)
                continue;

            ragdollRigidbodies.Add(rigidbody);
            ragdollTransforms.Add(rigidbody.transform);
            bindLocalPositions.Add(rigidbody.transform.localPosition);
            bindLocalRotations.Add(rigidbody.transform.localRotation);

            Collider collider = rigidbody.GetComponent<Collider>();
            if (collider != null)
                ragdollColliders.Add(collider);
        }

        if (ragdollRigidbodies.Count == 0)
        {
            Rigidbody rootRb = GetComponent<Rigidbody>();
            if (rootRb != null)
            {
                ragdollRigidbodies.Add(rootRb);
                ragdollTransforms.Add(rootRb.transform);
                bindLocalPositions.Add(rootRb.transform.localPosition);
                bindLocalRotations.Add(rootRb.transform.localRotation);
            }

            Collider rootCollider = GetComponent<Collider>();
            if (rootCollider != null)
                ragdollColliders.Add(rootCollider);
        }

        // Classic ragdoll rigs join every bone back to its parent except the root (e.g. hips/pelvis).
        RootRigidbody = null;
        foreach (Rigidbody rigidbody in ragdollRigidbodies)
        {
            if (rigidbody != null && rigidbody.GetComponent<Joint>() == null)
            {
                RootRigidbody = rigidbody;
                break;
            }
        }

        if (RootRigidbody == null && ragdollRigidbodies.Count > 0)
            RootRigidbody = ragdollRigidbodies[0];

        bodyPositions = new Vector3[ragdollRigidbodies.Count];
        bodyRotations = new Quaternion[ragdollRigidbodies.Count];
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
