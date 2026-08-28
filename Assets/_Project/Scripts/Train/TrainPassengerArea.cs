using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TrainPassengerArea : MonoBehaviour
{
    [SerializeField] private BoxCollider detectionTrigger;
    [SerializeField] private Transform carriageRoot;

    public Transform CarriageRoot => carriageRoot;

    private void Awake()
    {
        if (detectionTrigger == null)
            detectionTrigger = GetComponent<BoxCollider>();

        detectionTrigger.isTrigger = true;

        if (carriageRoot == null)
            carriageRoot = transform.parent;
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