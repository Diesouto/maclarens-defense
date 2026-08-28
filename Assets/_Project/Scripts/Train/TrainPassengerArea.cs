using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TrainPassengerArea : MonoBehaviour
{
    [SerializeField] private BoxCollider detectionTrigger;
    [SerializeField] private Transform carriageRoot;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float rayStartHeight = 1f;
    [SerializeField] private float rayDistance = 3f;

    public Transform CarriageRoot => carriageRoot;

    private void Awake()
    {
        if (detectionTrigger == null)
            detectionTrigger = GetComponent<BoxCollider>();

        detectionTrigger.isTrigger = true;

        if (carriageRoot == null)
            carriageRoot = transform.parent;
    }

    private void OnTriggerStay(Collider other)
    {
        TrainPassenger passenger =
            other.GetComponentInParent<TrainPassenger>();

        if (passenger == null)
            return;

        if (IsStandingOnThisCarriage(passenger))
        {
            passenger.SetCarriage(this);
        }
        else if (passenger.CurrentCarriage == this)
        {
            passenger.SetCarriage(null);
        }

        Debug.Log($"{passenger.name} is standing on {name}: {IsStandingOnThisCarriage(passenger)}");
    }

    private void OnTriggerExit(Collider other)
    {
        TrainPassenger passenger =
            other.GetComponentInParent<TrainPassenger>();

        if (passenger == null)
            return;

        if (passenger.CurrentCarriage == this)
            passenger.SetCarriage(null);
    }

    private bool IsStandingOnThisCarriage(TrainPassenger passenger)
    {
        Vector3 origin =
            passenger.transform.position +
            Vector3.up * rayStartHeight;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                rayStartHeight + rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.collider.transform.IsChildOf(carriageRoot);
    }
}