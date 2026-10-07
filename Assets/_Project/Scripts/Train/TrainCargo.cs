using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[DefaultExecutionOrder(-60)]
public class TrainCargo : MonoBehaviour
{
    [SerializeField] private Collider cargoTrigger;

    public IReadOnlyCollection<LootItem> ItemsInCargo => itemsInCargo;

    private static readonly List<TrainCargo> allCargo = new();

    private readonly HashSet<LootItem> itemsInCargo = new();
    // Ragdolls have many colliders, so count them per body instead of toggling on first enter/exit.
    private readonly Dictionary<PlayerBody, int> bodyColliderCounts = new();
    private readonly Dictionary<PlayerBody, StoredBody> storedBodies = new();
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

    public static bool TryStoreBody(PlayerBody body)
    {
        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo != null && body != null &&
                (cargo.storedBodies.ContainsKey(body) || cargo.ContainsBody(body)) && cargo.AttachBody(body))
                return true;
        }

        return false;
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

    private bool AttachBody(PlayerBody body)
    {
        if (body == null || !body.IsDead || body.IsHidden || body.IsBeingCarried || body.IsBeingPulled ||
            !body.TryGetComponent(out CharacterRagdollController ragdoll) || !ragdoll.IsRagdollActive)
            return false;

        if (storedBodies.ContainsKey(body))
            return true;

        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo != null && cargo != this && cargo.storedBodies.ContainsKey(body))
                return false;
        }

        ragdoll.SetPhysicsSuspended(true);
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
        return true;
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
        foreach (PlayerBody body in bodyColliderCounts.Keys)
        {
            if (ContainsBody(body))
                AttachBody(body);
        }

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

                bone.position = transform.TransformPoint(stored.Positions[index]);
                bone.rotation = transform.rotation * stored.Rotations[index];
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

    private void OnDisable()
    {
        foreach (StoredBody stored in storedBodies.Values)
        {
            if (stored.Ragdoll != null)
                stored.Ragdoll.SetPhysicsSuspended(false);
        }

        storedBodies.Clear();
        allCargo.Remove(this);
        bodyColliderCounts.Clear();
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
        PlayerBody body = other.GetComponentInParent<PlayerBody>();
        if (body != null)
        {
            bodyColliderCounts.TryGetValue(body, out int count);
            bodyColliderCounts[body] = count + 1;
            return;
        }

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
        PlayerBody body = other.GetComponentInParent<PlayerBody>();
        if (body != null)
        {
            if (bodyColliderCounts.TryGetValue(body, out int count))
            {
                if (count <= 1)
                    bodyColliderCounts.Remove(body);
                else
                    bodyColliderCounts[body] = count - 1;
            }

            return;
        }

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