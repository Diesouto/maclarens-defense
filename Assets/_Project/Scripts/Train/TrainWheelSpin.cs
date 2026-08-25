using UnityEngine;

// Spins a wheel transform to match the speed reported by the car it belongs to.
public class TrainWheelSpin : MonoBehaviour
{
    [SerializeField] private MonoBehaviour motionSource;
    [SerializeField] private float wheelRadius = 0.45f;
    [SerializeField] private Vector3 localRotationAxis = Vector3.right;

    private ITrainMotion motion;

    private void Awake()
    {
        motion = motionSource as ITrainMotion;
        if (motion == null)
            motion = GetComponentInParent<TrainSplineFollower>() as ITrainMotion ?? GetComponentInParent<TrainCarFollower>();
    }

    private void Update()
    {
        if (motion == null || wheelRadius <= 0f)
            return;

        float circumference = 2f * Mathf.PI * wheelRadius;
        float degreesPerSecond = (motion.CurrentSpeed / circumference) * 360f;
        transform.Rotate(localRotationAxis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
