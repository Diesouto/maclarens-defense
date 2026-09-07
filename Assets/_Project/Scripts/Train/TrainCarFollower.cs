using UnityEngine;

/// <summary>
/// Train carriage that follows the lead train at a fixed spacing.
///
/// Every carriage references the same TrainSplineFollower.
/// Position is calculated as:
///
/// Lead distance - (spacing × car index)
/// </summary>
// Runs before default-order scripts (e.g. PlayerController) so riders read this frame's pose, not last frame's.
[DefaultExecutionOrder(-100)]
public class TrainCarFollower : MonoBehaviour, ITrainMotion
{
    [Header("References")]
    [SerializeField] private TrainSpline spline;
    [SerializeField] private TrainSplineFollower leadTrain;

    [Header("Car Spacing")]
    [Tooltip("First carriage = 1, second = 2, etc.")]
    [SerializeField, Min(1)] private int carIndex = 1;

    [Tooltip("Distance between train cars.")]
    [SerializeField, Min(0f)] private float spacing = 6f;

    [Header("Orientation")]
    [SerializeField] private Vector3 up = Vector3.up;
    [SerializeField] private bool invertForward;

    public float CurrentSpeed { get; private set; }

    public float CurrentDistance { get; private set; }

    public Vector3 CurrentVelocity { get; private set; }

    public Transform MotionTransform => transform;

    private Vector3 previousPosition;

    private void Start()
    {
        if (spline == null || leadTrain == null)
            return;

        previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (spline == null || leadTrain == null)
            return;

        UpdatePosition();
        UpdateMotionState();
    }

    private void UpdatePosition()
    {
        float offset =
            spacing * carIndex;

        CurrentDistance =
            spline.WrapDistance(
                leadTrain.CurrentDistance - offset
            );

        SnapToSpline();
    }

    private void UpdateMotionState()
    {
        float deltaTime =
            Mathf.Max(Time.deltaTime, 0.0001f);

        CurrentVelocity =
            (transform.position - previousPosition) /
            deltaTime;

        CurrentSpeed =
            CurrentVelocity.magnitude;

        previousPosition =
            transform.position;
    }

    private void SnapToSpline()
    {
        transform.position =
            spline.GetPointAtDistance(
                CurrentDistance
            );

        Quaternion rotation =
            spline.GetRotationAtDistance(
                CurrentDistance,
                up
            );

        transform.rotation =
            invertForward
                ? rotation *
                  Quaternion.Euler(0f, 180f, 0f)
                : rotation;
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (spline == null || leadTrain == null)
            return;

        float distance =
            spline.WrapDistance(
                leadTrain.CurrentDistance -
                (spacing * carIndex)
            );

        Vector3 position =
            spline.GetPointAtDistance(distance);

        Gizmos.color = Color.yellow;

        Gizmos.DrawSphere(
            position,
            1.5f
        );

        Gizmos.DrawWireSphere(
            position,
            2f
        );

        UnityEditor.Handles.color =
            Color.yellow;

        UnityEditor.Handles.Label(
            position + Vector3.up * 3f,
            $"CAR {carIndex}\n{distance:F1}m"
        );
    }

#endif
}