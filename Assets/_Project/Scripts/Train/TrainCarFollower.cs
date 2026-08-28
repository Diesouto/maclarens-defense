using UnityEngine;

// Wagon that trails the lead car at a fixed arc-length offset, so it stays on the rails through curves.
public class TrainCarFollower : MonoBehaviour, ITrainMotion
{
    [SerializeField] private TrainSpline spline;
    [SerializeField] private MonoBehaviour followTargetBehaviour;
    [SerializeField] private float spacingOffset;
    [SerializeField] private Vector3 up = Vector3.up;
    [SerializeField] private bool invertForward;

    private float followOffset;
    private bool initialized;
    private ITrainMotion followTarget;
    private Vector3 previousPosition;

    public float CurrentSpeed { get; private set; }
    public float CurrentDistance { get; private set; }
    public Vector3 CurrentVelocity { get; private set; }
    public Transform MotionTransform => transform;

    private void Awake()
    {
        followTarget = followTargetBehaviour as ITrainMotion;
    }

    private void Start()
    {
        if (spline == null || followTarget == null)
            return;

        // Lock in whatever spacing the wagon was placed at in the editor.
        CurrentDistance = spline.GetNearestDistance(transform.position);
        followOffset = spline.WrapDistance(followTarget.CurrentDistance - CurrentDistance + spacingOffset);
        initialized = true;

        SnapToSpline(CurrentDistance);
        previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        float distance = spline.WrapDistance(followTarget.CurrentDistance - followOffset);
        CurrentDistance = distance;

        SnapToSpline(distance);
        UpdateMotionState();
    }

    private void UpdateMotionState()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        CurrentVelocity = (transform.position - previousPosition) / deltaTime;
        CurrentSpeed = CurrentVelocity.magnitude;
        previousPosition = transform.position;
    }

    private void SnapToSpline(float distance)
    {
        transform.position = spline.GetPointAtDistance(distance);

        Quaternion rotation = spline.GetRotationAtDistance(distance, up);
        transform.rotation = invertForward ? rotation * Quaternion.Euler(0f, 180f, 0f) : rotation;
    }
}
