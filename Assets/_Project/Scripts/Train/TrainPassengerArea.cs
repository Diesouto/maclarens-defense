using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TrainPassengerArea : MonoBehaviour
{
    private static readonly Dictionary<int, TrainPassengerArea> areasById = new();

    [SerializeField] private BoxCollider detectionTrigger;
    [SerializeField] private Transform carriageRoot;

    public Transform CarriageRoot => carriageRoot;
    // Derived from the scene hierarchy so every peer computes the same id without extra wiring.
    public int NetworkId { get; private set; }

    public static bool TryGet(int id, out TrainPassengerArea area) => areasById.TryGetValue(id, out area);

    // OnTriggerExit is skipped when a rider is teleported or its collider disabled, so riders re-check this.
    public bool IsNear(Vector3 worldPosition, float margin)
    {
        return detectionTrigger != null && detectionTrigger.enabled &&
            (detectionTrigger.ClosestPoint(worldPosition) - worldPosition).sqrMagnitude <= margin * margin;
    }

    private void Awake()
    {
        if (detectionTrigger == null)
            detectionTrigger = GetComponent<BoxCollider>();

        detectionTrigger.isTrigger = true;

        if (carriageRoot == null)
            carriageRoot = transform.parent;

        NetworkId = ComputeHierarchyId(transform);
        if (!areasById.TryAdd(NetworkId, this))
            Debug.LogError($"TrainPassengerArea: duplicate hierarchy id for '{name}'.", this);
    }

    private void OnDestroy()
    {
        if (areasById.TryGetValue(NetworkId, out TrainPassengerArea registered) && registered == this)
            areasById.Remove(NetworkId);
    }

    private static int ComputeHierarchyId(Transform target)
    {
        // FNV-1a over name + sibling index up the hierarchy; never 0 so 0 can mean "not riding".
        unchecked
        {
            uint hash = 2166136261;
            for (Transform current = target; current != null; current = current.parent)
            {
                foreach (char c in current.name)
                    hash = (hash ^ c) * 16777619;
                hash = (hash ^ (uint)current.GetSiblingIndex()) * 16777619;
            }

            return hash == 0 ? 1 : (int)hash;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TrainPassenger passenger = other.GetComponentInParent<TrainPassenger>();

        if (passenger == null)
            return;

        passenger.SetCarriage(this);
    }

    private void OnTriggerExit(Collider other)
    {
        TrainPassenger passenger = other.GetComponentInParent<TrainPassenger>();

        if (passenger == null)
            return;

        if (passenger.CurrentCarriage == this)
            passenger.SetCarriage(null);
    }
}