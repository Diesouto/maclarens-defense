using UnityEngine;

// Wagon that trails the lead car at a fixed arc-length offset, so it stays on the rails through curves.
public class TrainCarFollower : MonoBehaviour, ITrainMotion
{
    [SerializeField] private TrainSpline spline;
    [SerializeField] private TrainSplineFollower leadFollower;
    [SerializeField] private Vector3 up = Vector3.up;
    [SerializeField] private bool invertForward;

    private float followOffset;
    private float previousDistance;
    private bool initialized;

    public float CurrentSpeed { get; private set; }

    private void Start()
    {
        if (spline == null || leadFollower == null)
            return;

        // Lock in whatever spacing the wagon was placed at in the editor.
        previousDistance = spline.GetNearestDistance(transform.position);
        followOffset = spline.WrapDistance(leadFollower.CurrentDistance - previousDistance);
        initialized = true;

        SnapToSpline(previousDistance);
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        float distance = spline.WrapDistance(leadFollower.CurrentDistance - followOffset);
        // Wrap the delta too, otherwise crossing the loop seam reads as a huge speed spike.
        CurrentSpeed = spline.WrapDistance(distance - previousDistance) / Mathf.Max(Time.deltaTime, 0.0001f);
        previousDistance = distance;

        SnapToSpline(distance);
    }

    private void SnapToSpline(float distance)
    {
        transform.position = spline.GetPointAtDistance(distance);

        Quaternion rotation = spline.GetRotationAtDistance(distance, up);
        transform.rotation = invertForward ? rotation * Quaternion.Euler(0f, 180f, 0f) : rotation;
    }
}
