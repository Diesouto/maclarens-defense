using System.Collections;
using UnityEngine;

public enum TrainDestination
{
    Town,
    MacLarens
}

/// <summary>
/// Drives the lead train car forward around a closed TrainSpline loop
/// between the two MVP destinations.
/// </summary>
// Runs before default-order scripts (e.g. PlayerController) so riders read this frame's pose, not last frame's.
[DefaultExecutionOrder(-100)]
public class TrainSplineFollower : MonoBehaviour, ITrainMotion
{
    [Header("References")]
    [SerializeField] private TrainSpline spline;
    [SerializeField] private TrainDeparture trainDeparture;

    [Header("Stations")]
    [Tooltip("Position of Town along the spline, as a percentage of the total spline length.")]
    [SerializeField, Range(0f, 1f)] private float townPosition = 0f;

    [Tooltip("Position of MacLarens along the spline, as a percentage of the total spline length.")]
    [SerializeField, Range(0f, 1f)] private float macLarensPosition = 0.5f;

    [SerializeField] private TrainDestination startingStation = TrainDestination.Town;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float maxSpeed = 12f;
    [SerializeField, Min(0.01f)] private float accelerationTime = 4f;
    [SerializeField, Min(0f)] private float decelerationDistance = 15f;

    [Header("Orientation")]
    [SerializeField] private Vector3 up = Vector3.up;
    [SerializeField] private bool invertForward;

    public float CurrentDistance { get; private set; }
    public float CurrentSpeed { get; private set; }
    public Vector3 CurrentVelocity { get; private set; }

    public Transform MotionTransform => transform;

    public TrainDestination CurrentStation { get; private set; }

    public bool IsMoving { get; private set; }
    public bool IsInitialized { get; private set; }

    private Coroutine travelRoutine;
    private Vector3 previousPosition;

    /// <summary>
    /// Actual distance along the spline where Town is located.
    /// </summary>
    public float TownDistance
    {
        get
        {
            if (spline == null)
                return 0f;

            return townPosition * spline.Length;
        }
    }

    /// <summary>
    /// Actual distance along the spline where MacLarens is located.
    /// </summary>
    public float MacLarensDistance
    {
        get
        {
            if (spline == null)
                return 0f;

            return macLarensPosition * spline.Length;
        }
    }

    private void Awake()
    {
        if (trainDeparture == null)
            trainDeparture = FindFirstObjectByType<TrainDeparture>();

        CurrentStation = startingStation;

        CurrentDistance =
            startingStation == TrainDestination.Town
                ? TownDistance
                : MacLarensDistance;

        SnapToSpline();

        previousPosition = transform.position;

        IsInitialized = true;
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
        {
            Debug.LogWarning(
                "TrainSplineFollower: No valid spline to follow."
            );

            yield break;
        }

        TrainDestination target =
            CurrentStation == TrainDestination.Town
                ? TrainDestination.MacLarens
                : TrainDestination.Town;

        float targetDistance =
            target == TrainDestination.Town
                ? TownDistance
                : MacLarensDistance;

        // The train always travels forward around the closed spline.
        float startDistance = CurrentDistance;

        float totalDistance =
            targetDistance - startDistance;

        if (totalDistance <= 0f)
            totalDistance += spline.Length;

        IsMoving = true;

        float traveled = 0f;
        float speed = 0f;

        Debug.Log(
            $"TrainSplineFollower: Departing from {CurrentStation} " +
            $"to {target}. Total distance: {totalDistance:F2}."
        );

        while (traveled < totalDistance)
        {
            float remaining = totalDistance - traveled;

            float targetSpeed =
                remaining < decelerationDistance
                    ? Mathf.Lerp(
                        0.5f,
                        maxSpeed,
                        remaining / decelerationDistance
                    )
                    : maxSpeed;

            speed = Mathf.MoveTowards(
                speed,
                targetSpeed,
                (maxSpeed / accelerationTime) * Time.deltaTime
            );

            traveled += Mathf.Min(
                speed * Time.deltaTime,
                remaining
            );

            CurrentDistance =
                spline.WrapDistance(
                    startDistance + traveled
                );

            SnapToSpline();

            UpdateMotionState();

            yield return null;
        }

        // Make absolutely sure we finish exactly at the station.
        CurrentDistance = targetDistance;

        SnapToSpline();

        CurrentSpeed = 0f;
        CurrentVelocity = Vector3.zero;

        CurrentStation = target;
        IsMoving = false;

        travelRoutine = null;

        previousPosition = transform.position;

        trainDeparture?.OnArrived();
    }

    private void UpdateMotionState()
    {
        float deltaTime =
            Mathf.Max(Time.deltaTime, 0.0001f);

        CurrentVelocity =
            (transform.position - previousPosition)
            / deltaTime;

        CurrentSpeed =
            CurrentVelocity.magnitude;

        previousPosition =
            transform.position;
    }

    private void SnapToSpline()
    {
        if (spline == null || spline.Length <= 0f)
            return;

        transform.position =
            spline.GetPointAtDistance(CurrentDistance);

        Quaternion rotation =
            spline.GetRotationAtDistance(
                CurrentDistance,
                up
            );

        transform.rotation =
            invertForward
                ? rotation * Quaternion.Euler(0f, 180f, 0f)
                : rotation;
    }

    private void OnDrawGizmos()
    {
        if (spline == null || spline.Length <= 0f)
            return;

        DrawStationGizmo(
            TownDistance,
            "TOWN",
            Color.green
        );

        DrawStationGizmo(
            MacLarensDistance,
            "MACLARENS",
            Color.red
        );
    }

    private void DrawStationGizmo(
        float distance,
        string label,
        Color color)
    {
        Vector3 position =
            spline.GetPointAtDistance(distance);

        Quaternion rotation =
            spline.GetRotationAtDistance(
                distance,
                up
            );

        Gizmos.color = color;

        // HUGE main marker.
        Gizmos.DrawSphere(
            position,
            3f
        );

        // Even larger outer ring.
        Gizmos.DrawWireSphere(
            position,
            5f
        );

        // Tall vertical marker.
        Gizmos.DrawLine(
            position,
            position + Vector3.up * 10f
        );

        // Forward direction.
        Vector3 forward =
            rotation * Vector3.forward;

        Gizmos.DrawLine(
            position,
            position + forward * 12f
        );

#if UNITY_EDITOR

        UnityEditor.Handles.color = color;

        UnityEditor.Handles.Label(
            position + Vector3.up * 11f,
            $"{label}\n{distance:F1}m"
        );

#endif
    }
}