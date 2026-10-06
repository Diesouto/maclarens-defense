using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrainCargo : MonoBehaviour
{
    [SerializeField] private Collider cargoTrigger;

    public IReadOnlyCollection<LootItem> ItemsInCargo => itemsInCargo;

    private static readonly List<TrainCargo> allCargo = new();

    private readonly HashSet<LootItem> itemsInCargo = new();
    // Ragdolls have many colliders, so count them per body instead of toggling on first enter/exit.
    private readonly Dictionary<PlayerBody, int> bodyColliderCounts = new();
    private Vector3 lastPosition;
    private Quaternion lastRotation;

    public static bool IsBodyAboard(PlayerBody body)
    {
        foreach (TrainCargo cargo in allCargo)
        {
            if (cargo != null && cargo.bodyColliderCounts.ContainsKey(body))
                return true;
        }

        return false;
    }

    private void OnEnable()
    {
        allCargo.Add(this);
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    // Ragdoll bones are free rigidbodies, so a corpse left on the train must be moved by the train's own delta.
    private void LateUpdate()
    {
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Quaternion deltaRotation = rotation * Quaternion.Inverse(lastRotation);
        Vector3 previousPosition = lastPosition;
        lastPosition = position;
        lastRotation = rotation;

        if (bodyColliderCounts.Count == 0)
            return;

        foreach (PlayerBody body in bodyColliderCounts.Keys)
        {
            if (body == null || !body.IsDead || body.IsHidden || body.IsBeingCarried ||
                !body.TryGetComponent(out CharacterRagdollController ragdoll) || !ragdoll.IsRagdollActive)
                continue;

            Rigidbody root = ragdoll.RootRigidbody;
            float floorY = cargoTrigger.bounds.min.y + 0.1f;
            // Bones sinking through the moving floor are lifted back instead of falling out of the train.
            float lift = root != null && root.position.y < floorY ? floorY + 0.4f - root.position.y : 0f;

            foreach (Rigidbody bone in ragdoll.RagdollRigidbodies)
            {
                if (bone == null)
                    continue;

                bone.position = position + deltaRotation * (bone.position - previousPosition) + Vector3.up * lift;
                bone.rotation = deltaRotation * bone.rotation;
                bone.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                if (lift > 0f)
                {
                    bone.linearVelocity = Vector3.zero;
                    bone.angularVelocity = Vector3.zero;
                }
            }
        }
    }

    private void OnDisable()
    {
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