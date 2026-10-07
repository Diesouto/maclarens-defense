using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[DefaultExecutionOrder(-60)]
public class TrainCargo : MonoBehaviour
{
    [SerializeField] private Collider cargoTrigger;
    [SerializeField, Min(0f)] private float throwReleaseGraceDuration = 0.3f;

    [Header("Corpses")]
    [Tooltip("Dropping a body this close to the cargo volume (body or carrier) stows it inside.")]
    [SerializeField, Min(0f)] private float bodyDropMargin = 1.5f;
    [Tooltip("Loose (thrown, pulled or freshly killed) bodies this close to the cargo volume are stowed automatically.")]
    [SerializeField, Min(0f)] private float bodyCaptureMargin = 0.3f;
    [SerializeField, Min(0f)] private float bodyWallPadding = 0.35f;
    [SerializeField, Min(0f)] private float bodyFloorClearance = 0.25f;

    public IReadOnlyCollection<LootItem> ItemsInCargo => itemsInCargo;

    private static readonly List<TrainCargo> allCargo = new();

    private readonly HashSet<LootItem> itemsInCargo = new();
    private readonly Dictionary<PlayerBody, StoredBody> storedBodies = new();
    private readonly Dictionary<PlayerBody, float> captureCooldowns = new();
    private readonly List<PlayerBody> bodiesToRelease = new();

    private sealed class StoredBody
    {
        public CharacterRagdollController Ragdoll;
        public Vector3[] Positions;
        public Quaternion[] Rotations;
    }

    public static bool IsBodyAboard(PlayerBody body)
    {
        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo != null && body != null &&
                (cargo.storedBodies.ContainsKey(body) || cargo.ContainsBody(body)))
                return true;
        }

        return false;
    }

    private void OnEnable()
    {
        allCargo.Add(this);
    }

    public static void ReleaseBody(PlayerBody body)
    {
        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo != null)
                cargo.DetachBody(body);
        }
    }

    public static void ReleaseBodyForThrow(PlayerBody body)
    {
        if (body == null)
            return;

        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo == null)
                continue;

            cargo.captureCooldowns[body] = Time.time + cargo.throwReleaseGraceDuration;
            cargo.DetachBody(body);
        }
    }

    // Server/offline only: clients receive the result through NetworkBodyCarrier's stow state.
    public static bool TryStowBody(PlayerBody body, Vector3 dropperPosition)
    {
        if (body == null || NetworkRole.IsClientOnly)
            return false;

        TrainCargo best = null;
        float bestDistance = float.MaxValue;
        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo == null)
                continue;

            float distance = Mathf.Min(cargo.DistanceTo(body.BodyPosition), cargo.DistanceTo(dropperPosition));
            if (distance <= cargo.bodyDropMargin && distance < bestDistance)
            {
                best = cargo;
                bestDistance = distance;
            }
        }

        return best != null && best.Stow(body);
    }

    public static TrainCargo GetStowedCargo(PlayerBody body)
    {
        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo != null && body != null && cargo.storedBodies.ContainsKey(body))
                return cargo;
        }

        return null;
    }

    public static TrainCargo GetBodyCargo(PlayerBody body)
    {
        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo != null && body != null && cargo.storedBodies.ContainsKey(body))
                return cargo;
        }

        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo != null && cargo.ContainsBody(body))
                return cargo;
        }

        return null;
    }

    private bool ContainsBody(PlayerBody body)
    {
        return body != null && cargoTrigger != null && cargoTrigger.enabled &&
            (cargoTrigger.ClosestPoint(body.BodyPosition) - body.BodyPosition).sqrMagnitude < 0.0001f;
    }

    private float DistanceTo(Vector3 point)
    {
        if (cargoTrigger == null || !cargoTrigger.enabled)
            return float.MaxValue;

        return Vector3.Distance(cargoTrigger.ClosestPoint(point), point);
    }

    private static bool CanHoldBody(PlayerBody body, out CharacterRagdollController ragdoll)
    {
        ragdoll = null;
        return body != null && body.IsDead && !body.IsHidden && !body.IsBeingCarried && !body.IsBeingPulled &&
            body.TryGetComponent(out ragdoll) && ragdoll.IsRagdollActive && ragdoll.RootRigidbody != null;
    }

    private bool Stow(PlayerBody body)
    {
        if (!CanHoldBody(body, out CharacterRagdollController ragdoll))
            return false;

        return StowAt(body, GetRestingPoint(body), ragdoll.RootRigidbody.rotation);
    }

    // Latches the corpse at a fixed pose inside this carriage: no physics, so it can't slide or fall off.
    public bool StowAt(PlayerBody body, Vector3 rootPosition, Quaternion rootRotation)
    {
        if (!CanHoldBody(body, out CharacterRagdollController ragdoll))
            return false;

        if (storedBodies.ContainsKey(body))
            return true;

        ReleaseBody(body);
        captureCooldowns.Remove(body);
        ragdoll.SetPhysicsSuspended(true);
        ragdoll.SetBodyPose(rootPosition, rootRotation);
        AttachBody(body, ragdoll);
        return true;
    }

    // Keeps the hips inside the cargo volume, away from the walls, resting on the carriage floor.
    private Vector3 GetRestingPoint(PlayerBody body)
    {
        Vector3 point;
        if (cargoTrigger is BoxCollider box)
        {
            Transform boxTransform = box.transform;
            Vector3 scale = boxTransform.lossyScale;
            Vector3 half = box.size * 0.5f;
            Vector3 local = boxTransform.InverseTransformPoint(body.BodyPosition) - box.center;
            float limitX = Mathf.Max(0f, half.x - bodyWallPadding / Mathf.Max(Mathf.Abs(scale.x), 0.0001f));
            float limitZ = Mathf.Max(0f, half.z - bodyWallPadding / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
            local.x = Mathf.Clamp(local.x, -limitX, limitX);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -limitZ, limitZ);
            point = boxTransform.TransformPoint(box.center + local);
        }
        else
        {
            point = cargoTrigger.ClosestPoint(body.BodyPosition);
            Vector3 toCenter = Vector3.ProjectOnPlane(cargoTrigger.bounds.center - point, Vector3.up);
            point += Vector3.ClampMagnitude(toCenter, bodyWallPadding);
        }

        Bounds bounds = cargoTrigger.bounds;
        Vector3 rayStart = new Vector3(point.x, bounds.max.y, point.z);
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, bounds.size.y + 1f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        point.y = bounds.min.y + bodyFloorClearance;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(body.transform) ||
                hit.collider.GetComponentInParent<PlayerController>() != null)
                continue;

            point.y = hit.point.y + bodyFloorClearance;
            break;
        }

        return point;
    }

    private void AttachBody(PlayerBody body, CharacterRagdollController ragdoll)
    {
        var stored = new StoredBody
        {
            Ragdoll = ragdoll,
            Positions = new Vector3[ragdoll.RagdollRigidbodies.Count],
            Rotations = new Quaternion[ragdoll.RagdollRigidbodies.Count]
        };

        for (int index = 0; index < ragdoll.RagdollRigidbodies.Count; index++)
        {
            Rigidbody bone = ragdoll.RagdollRigidbodies[index];
            if (bone == null)
                continue;

            stored.Positions[index] = transform.InverseTransformPoint(bone.position);
            stored.Rotations[index] = Quaternion.Inverse(transform.rotation) * bone.rotation;
        }

        storedBodies.Add(body, stored);
    }

    private void DetachBody(PlayerBody body)
    {
        if (body == null || !storedBodies.TryGetValue(body, out StoredBody stored))
            return;

        storedBodies.Remove(body);
        if (stored.Ragdoll != null)
            stored.Ragdoll.SetPhysicsSuspended(false);
    }

    private void LateUpdate()
    {
        if (!NetworkRole.IsClientOnly)
            CaptureLooseBodies();

        bodiesToRelease.Clear();
        foreach (KeyValuePair<PlayerBody, StoredBody> entry in storedBodies)
        {
            PlayerBody body = entry.Key;
            StoredBody stored = entry.Value;
            if (body == null || !body.IsDead || body.IsHidden || body.IsBeingCarried ||
                stored.Ragdoll == null || !stored.Ragdoll.IsRagdollActive)
            {
                bodiesToRelease.Add(body);
                continue;
            }

            for (int index = 0; index < stored.Positions.Length; index++)
            {
                Rigidbody bone = stored.Ragdoll.RagdollRigidbodies[index];
                if (bone == null)
                    continue;

                Vector3 bonePosition = transform.TransformPoint(stored.Positions[index]);
                Quaternion boneRotation = transform.rotation * stored.Rotations[index];
                // Transform write keeps the corpse visually in lockstep with the carriage this frame.
                bone.transform.SetPositionAndRotation(bonePosition, boneRotation);
                bone.position = bonePosition;
                bone.rotation = boneRotation;
            }
        }

        foreach (PlayerBody body in bodiesToRelease)
        {
            if (body == null)
                storedBodies.Remove(body);
            else
                DetachBody(body);
        }
    }

    // Geometric, not trigger-driven: suspended (kinematic) ragdoll bones don't reliably raise trigger events.
    private void CaptureLooseBodies()
    {
        foreach (PlayerBody body in PlayerBody.AllBodies)
        {
            if (body == null || storedBodies.ContainsKey(body) || !CanHoldBody(body, out _) ||
                GetStowedCargo(body) != null)
                continue;

            if (captureCooldowns.TryGetValue(body, out float captureTime))
            {
                if (Time.time < captureTime)
                    continue;

                captureCooldowns.Remove(body);
            }

            if (DistanceTo(body.BodyPosition) <= bodyCaptureMargin)
                Stow(body);
        }
    }

    private void OnDisable()
    {
        foreach (StoredBody stored in storedBodies.Values)
        {
            if (stored.Ragdoll != null)
                stored.Ragdoll.SetPhysicsSuspended(false);
        }

        storedBodies.Clear();
        captureCooldowns.Clear();
        allCargo.Remove(this);
    }

    private void Awake()
    {
        if (cargoTrigger == null)
            cargoTrigger = GetComponent<Collider>();

        cargoTrigger.isTrigger = true;

        if (GetComponent<TrainSplineFollower>() == null &&
            GetComponent<TrainCarFollower>() == null)
        {
            Debug.LogWarning(
                $"{name}: TrainCargo has no TrainSplineFollower/TrainCarFollower.",
                this
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerBody>() != null)
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
            return;

        LootItem lootItem = other.GetComponentInParent<LootItem>();

        if (lootItem == null)
            return;

        if (!itemsInCargo.Add(lootItem))
            return;

        lootItem.SetCargo(this);
        AttachToCargo(lootItem);

        int value = lootItem.Data != null
            ? lootItem.Data.Value
            : 0;

        QuotaManager.Instance?.AddCargoValue(value);

        Debug.Log(
            $"TrainCargo: Added {lootItem.name} worth ${value}.",
            this
        );
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerBody>() != null)
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
            return;

        LootItem lootItem = other.GetComponentInParent<LootItem>();

        if (lootItem == null)
            return;

        if (!itemsInCargo.Remove(lootItem))
            return;

        lootItem.SetCargo(null);
        DetachFromCargo(lootItem);

        int value = lootItem.Data != null
            ? lootItem.Data.Value
            : 0;

        QuotaManager.Instance?.AddCargoValue(-value);

        Debug.Log(
            $"TrainCargo: Removed {lootItem.name} worth ${value}.",
            this
        );
    }

    public bool RemoveItem(LootItem lootItem)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
            return false;

        if (lootItem == null)
            return false;

        if (!itemsInCargo.Remove(lootItem))
            return false;

        lootItem.SetCargo(null);
        DetachFromCargo(lootItem);

        int value = lootItem.Data != null
            ? lootItem.Data.Value
            : 0;

        QuotaManager.Instance?.AddCargoValue(-value);

        Debug.Log(
            $"TrainCargo: Removed {lootItem.name} worth ${value}.",
            this
        );

        return true;
    }

    // Loot must ride along with the train, mirroring how TrainPassenger attaches players to a carriage.
    private void AttachToCargo(LootItem lootItem)
    {
        lootItem.transform.SetParent(transform, true);
    }

    private void DetachFromCargo(LootItem lootItem)
    {
        if (lootItem.transform.parent == transform)
            lootItem.transform.SetParent(null, true);
    }
}