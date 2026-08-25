using System.Collections;
using UnityEngine;

public enum TrainDestination
{
    Town,
    MacLarens
}

// Drives the lead train car forward around a closed TrainSpline loop between the two MVP destinations.
public class TrainSplineFollower : MonoBehaviour, ITrainMotion
{
    [SerializeField] private TrainSpline spline;
    [SerializeField] private TrainDeparture trainDeparture;
    [SerializeField] private Transform townStation;
    [SerializeField] private Transform macLarensStation;
    [SerializeField] private TrainDestination startingDestination = TrainDestination.Town;
    [SerializeField] private float maxSpeed = 12f;
    [SerializeField] private float accelerationTime = 4f;
    [SerializeField] private float decelerationDistance = 15f;
    [SerializeField] private Vector3 up = Vector3.up;
    [SerializeField] private bool invertForward;

    public float CurrentDistance { get; private set; }
    public float CurrentSpeed { get; private set; }
    public Vector3 CurrentVelocity { get; private set; }
    public Transform MotionTransform => transform;
    public TrainDestination CurrentDestination { get; private set; }
    public bool IsMoving { get; private set; }

    private float townDistance;
    private float macLarensDistance;
    private Coroutine travelRoutine;
    private Vector3 previousPosition;

    private void Awake()
    {
        if (trainDeparture == null)
            trainDeparture = FindFirstObjectByType<TrainDeparture>();

        if (spline != null)
        {
            townDistance = townStation != null ? spline.GetNearestDistance(townStation.position) : 0f;
            macLarensDistance = macLarensStation != null ? spline.GetNearestDistance(macLarensStation.position) : spline.Length * 0.5f;
        }

        CurrentDestination = startingDestination;
        CurrentDistance = startingDestination == TrainDestination.Town ? townDistance : macLarensDistance;
        SnapToSpline();
        previousPosition = transform.position;
    }

    private void OnEnable()
    {
        if (trainDeparture != null)
            trainDeparture.OnTrainDeparted += HandleDeparted;
    }

    private void OnDisable()
    {
        if (trainDeparture != null)
            trainDeparture.OnTrainDeparted -= HandleDeparted;
    }

    private void HandleDeparted()
    {
        if (travelRoutine != null)
            StopCoroutine(travelRoutine);

        travelRoutine = StartCoroutine(TravelRoutine());
    }

    private IEnumerator TravelRoutine()
    {
        if (spline == null || spline.Length <= 0f)
            yield break;

        TrainDestination target = CurrentDestination == TrainDestination.Town ? TrainDestination.MacLarens : TrainDestination.Town;
        float targetDistance = target == TrainDestination.Town ? townDistance : macLarensDistance;

        // The loop is only ever travelled forward, wrapping past the end back to the start.
        float startDistance = CurrentDistance;
        float totalDistance = targetDistance - startDistance;
        if (totalDistance <= 0f)
            totalDistance += spline.Length;

        IsMoving = true;
        float traveled = 0f;
        float speed = 0f;

        while (traveled < totalDistance)
        {
            float remaining = totalDistance - traveled;
            float targetSpeed = remaining < decelerationDistance
                ? Mathf.Lerp(0.5f, maxSpeed, remaining / decelerationDistance)
                : maxSpeed;

            speed = Mathf.MoveTowards(speed, targetSpeed, (maxSpeed / accelerationTime) * Time.deltaTime);
            CurrentSpeed = speed;

            traveled += Mathf.Min(speed * Time.deltaTime, remaining);
            CurrentDistance = spline.WrapDistance(startDistance + traveled);

            SnapToSpline();
            UpdateMotionState();
            yield return null;
        }

        CurrentSpeed = 0f;
        CurrentVelocity = Vector3.zero;
        CurrentDistance = targetDistance;
        CurrentDestination = target;
        IsMoving = false;
        travelRoutine = null;
        previousPosition = transform.position;

        trainDeparture?.OnArrived();
    }

    private void UpdateMotionState()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        CurrentVelocity = (transform.position - previousPosition) / deltaTime;
        CurrentSpeed = CurrentVelocity.magnitude;
        previousPosition = transform.position;
    }

    private void SnapToSpline()
    {
        if (spline == null)
            return;

        transform.position = spline.GetPointAtDistance(CurrentDistance);

        Quaternion rotation = spline.GetRotationAtDistance(CurrentDistance, up);
        transform.rotation = invertForward ? rotation * Quaternion.Euler(0f, 180f, 0f) : rotation;
    }
}
