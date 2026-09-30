using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Cowboy lasso (Tool item): yanks loose rigidbodies into the thrower's hands and ragdolls players toward
// them. The owner aims and draws the rope; the host validates item, range, target and cooldown.
public class LassoTool : NetworkBehaviour
{
    [Header("Gameplay")]
    [SerializeField, Min(1f)] private float range = 15f;
    [SerializeField, Min(0f)] private float cooldown = 1.5f;
    [SerializeField, Min(0f)] private float playerPullForce = 12f;
    [SerializeField, Min(0f)] private float playerKnockdownDuration = 2.5f;
    [Tooltip("Seconds a lassoed object takes to fly into the thrower's hands.")]
    [SerializeField, Min(0.1f)] private float objectFlightTime = 0.6f;
    [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
    [Tooltip("How far a reported hit point may be from the target's colliders on the host (interpolation lag).")]
    [SerializeField, Min(0f)] private float hitPointTolerance = 1.5f;

    [Header("Rope")]
    [Tooltip("Optional; defaults to an unlit material tinted with ropeColor.")]
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private Color ropeColor = new Color(0.55f, 0.4f, 0.22f);
    [SerializeField, Min(0.001f)] private float ropeWidth = 0.03f;
    [Tooltip("Where the rope leaves the hand; defaults to the ItemHolder throw point.")]
    [SerializeField] private Transform ropeOrigin;
    [SerializeField, Range(4, 64)] private int ropeSegments = 20;
    [SerializeField, Min(0f)] private float ropeSag = 1.5f;
    [SerializeField, Min(0.05f)] private float ropeDuration = 0.6f;
    [SerializeField, Min(0.05f)] private float loopRadius = 0.45f;
    [SerializeField, Range(6, 48)] private int loopSegments = 20;

    private PlayerInventory inventory;
    private LineRenderer rope;
    private LineRenderer loop;
    private float lastThrowTime = float.NegativeInfinity;
    private float lastServerThrowTime = float.NegativeInfinity;
    private Coroutine ropeRoutine;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();

        if (ropeOrigin == null && TryGetComponent(out ItemHolder holder))
            ropeOrigin = holder.ThrowPoint;

        Material material = ropeMaterial != null ? ropeMaterial : new Material(Shader.Find("Sprites/Default"));
        rope = CreateLine("LassoRope", material, ropeSegments + 1, false);
        loop = CreateLine("LassoLoop", material, loopSegments, true);
    }

    private LineRenderer CreateLine(string lineName, Material material, int points, bool closed)
    {
        var lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.startColor = ropeColor;
        line.endColor = ropeColor;
        line.widthMultiplier = ropeWidth;
        line.useWorldSpace = true;
        line.loop = closed;
        line.positionCount = points;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.enabled = false;
        return line;
    }

    public void TryThrow(Ray aim)
    {
        if (Time.time - lastThrowTime < cooldown)
            return;

        lastThrowTime = Time.time;

        Vector3 endPoint = aim.origin + aim.direction * range;
        Transform endTarget = null;
        if (TryFindTarget(aim, out RaycastHit hit))
        {
            endPoint = hit.point;
            endTarget = hit.collider.transform;
            RequestPull(hit);
        }

        PlayRope(endPoint, endTarget);
        if (IsSpawned)
            ShowRopeRpc(endPoint);
    }

    private bool TryFindTarget(Ray aim, out RaycastHit result)
    {
        RaycastHit[] hits = Physics.RaycastAll(aim, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            result = hit;
            return true;
        }

        result = default;
        return false;
    }

    private void RequestPull(RaycastHit hit)
    {
        PlayerKnockdown targetPlayer = hit.collider.GetComponentInParent<PlayerKnockdown>();
        Rigidbody targetBody = targetPlayer == null ? hit.rigidbody : null;
        if (targetPlayer == null && !IsPullableBody(targetBody))
            return;

        if (!NetworkRole.IsClientOnly)
        {
            ApplyPull(targetPlayer, targetBody);
            return;
        }

        NetworkObject targetObject = targetPlayer != null ? targetPlayer.NetworkObject : targetBody.GetComponent<NetworkObject>();
        if (targetObject != null && targetObject.IsSpawned)
            RequestPullServerRpc(targetObject, hit.point);
    }

    // In a session only bodies that own a NetworkObject are pulled, so the host's push replicates.
    private static bool IsPullableBody(Rigidbody body)
    {
        if (body == null || body.isKinematic)
            return false;

        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        return !isNetworked || body.TryGetComponent(out NetworkObject _);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestPullServerRpc(NetworkObjectReference targetReference, Vector3 hitPoint, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId ||
            !targetReference.TryGet(out NetworkObject targetObject) || targetObject == null)
            return;

        if (!IsHoldingLasso() || Time.time - lastServerThrowTime < cooldown * 0.8f)
            return;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        if (Vector3.Distance(eye, hitPoint) > range + hitPointTolerance ||
            !NetworkWeaponAuthority.IsNearAnyCollider(targetObject, hitPoint, hitPointTolerance))
            return;

        targetObject.TryGetComponent(out PlayerKnockdown targetPlayer);
        Rigidbody targetBody = null;
        if (targetPlayer == null && (!targetObject.TryGetComponent(out targetBody) || !IsPullableBody(targetBody)))
            return;

        lastServerThrowTime = Time.time;
        ApplyPull(targetPlayer, targetBody);
    }

    private bool IsHoldingLasso()
    {
        return inventory != null && inventory.ActiveItem != null &&
            inventory.ActiveItem.ItemType == InventoryItemType.Tool;
    }

    private void ApplyPull(PlayerKnockdown targetPlayer, Rigidbody targetBody)
    {
        if (targetPlayer != null)
        {
            if (targetPlayer.gameObject == gameObject)
                return;

            // Knockdown pushes away from its origin, so mirror the thrower behind the target to pull it in.
            Vector3 targetPosition = targetPlayer.transform.position;
            Vector3 mirroredOrigin = targetPosition + (targetPosition - transform.position);
            targetPlayer.KnockDownFromServer(mirroredOrigin, playerPullForce, playerKnockdownDuration);
            return;
        }

        // Ballistic launch that lands in front of the thrower's chest after objectFlightTime.
        Vector3 catchPoint = transform.position + transform.forward * 1.2f + Vector3.up * 1.2f;
        Vector3 delta = catchPoint - targetBody.position;
        targetBody.linearVelocity = delta / objectFlightTime - 0.5f * objectFlightTime * Physics.gravity;
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]
    private void ShowRopeRpc(Vector3 endPoint)
    {
        PlayRope(endPoint, null);
    }

    private void PlayRope(Vector3 endPoint, Transform endTarget)
    {
        if (ropeOrigin == null)
            return;

        if (ropeRoutine != null)
            StopCoroutine(ropeRoutine);

        Vector3 localEnd = endTarget != null ? endTarget.InverseTransformPoint(endPoint) : endPoint;
        ropeRoutine = StartCoroutine(AnimateRope(endTarget, localEnd, endPoint));
    }

    // Flies out over the first third, then reels back in while the slack and the loop tighten.
    private IEnumerator AnimateRope(Transform endTarget, Vector3 localEnd, Vector3 worldEnd)
    {
        rope.enabled = true;
        loop.enabled = true;

        for (float elapsed = 0f; elapsed < ropeDuration; elapsed += Time.deltaTime)
        {
            float progress = elapsed / ropeDuration;
            float reach = progress < 0.3f ? progress / 0.3f : 1f - (progress - 0.3f) / 0.7f;
            float slack = progress < 0.3f ? 1f : reach;

            Vector3 start = ropeOrigin.position;
            Vector3 target = endTarget != null ? endTarget.TransformPoint(localEnd) : worldEnd;
            DrawRope(start, Vector3.Lerp(start, target, reach), slack);
            yield return null;
        }

        rope.enabled = false;
        loop.enabled = false;
        ropeRoutine = null;
    }

    private void DrawRope(Vector3 start, Vector3 end, float slack)
    {
        float sag = ropeSag * slack * Vector3.Distance(start, end) / range;
        Vector3 control = (start + end) * 0.5f + Vector3.down * sag;

        for (int i = 0; i <= ropeSegments; i++)
        {
            float t = (float)i / ropeSegments;
            float u = 1f - t;
            rope.SetPosition(i, u * u * start + 2f * u * t * control + t * t * end);
        }

        DrawLoop(end, end - control, Mathf.Lerp(0.3f, 1f, slack) * loopRadius);
    }

    // The loop hangs off the rope's tip, facing along the rope's final direction.
    private void DrawLoop(Vector3 tip, Vector3 tangent, float radius)
    {
        Vector3 forward = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.forward;
        Vector3 side = Vector3.Cross(forward, Vector3.up);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(forward, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, forward);
        Vector3 center = tip - up * radius;

        for (int i = 0; i < loopSegments; i++)
        {
            float angle = (float)i / loopSegments * Mathf.PI * 2f;
            loop.SetPosition(i, center + (up * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * radius);
        }
    }
}
