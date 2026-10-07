using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[DefaultExecutionOrder(-60)]
public class TrainCargo : MonoBehaviour
{
    [SerializeField] private Collider cargoTrigger;
    [SerializeField, Min(0f)] private float throwReleaseGraceDuration = 0.3f;

    public IReadOnlyCollection<LootItem> ItemsInCargo => itemsInCargo;

    private static readonly List<TrainCargo> allCargo = new();

    private readonly HashSet<LootItem> itemsInCargo = new();
    private readonly Dictionary<PlayerBody, HashSet<Collider>> bodyCollidersInCargo = new();
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
        if (body != null && captureCooldowns.TryGetValue(body, out float captureTime) && Time.time < captureTime)
            return false;

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
        foreach (KeyValuePair<PlayerBody, HashSet<Collider>> entry in bodyCollidersInCargo)
        {
            PlayerBody body = entry.Key;
            if (entry.Value.Count == 0 || body == null)
                continue;

            if (captureCooldowns.TryGetValue(body, out float captureTime))
            {
                if (Time.time < captureTime)
                    continue;

                captureCooldowns.Remove(body);
            }

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
        captureCooldowns.Clear();
        allCargo.Remove(this);
        bodyCollidersInCargo.Clear();
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
        if (TrackBodyCollider(other))
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

    private void OnTriggerStay(Collider other)
    {
        TrackBodyCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerBody body = other.GetComponentInParent<PlayerBody>();
        if (body != null)
        {
            if (bodyCollidersInCargo.TryGetValue(body, out HashSet<Collider> colliders))
            {
                colliders.Remove(other);
                if (colliders.Count == 0)
                    bodyCollidersInCargo.Remove(body);
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

    private bool TrackBodyCollider(Collider other)
    {
        PlayerBody body = other.GetComponentInParent<PlayerBody>();
        if (body == null)
            return false;

        if (!bodyCollidersInCargo.TryGetValue(body, out HashSet<Collider> colliders))
        {
            colliders = new HashSet<Collider>();
            bodyCollidersInCargo.Add(body, colliders);
        }

        colliders.Add(other);
        return true;
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