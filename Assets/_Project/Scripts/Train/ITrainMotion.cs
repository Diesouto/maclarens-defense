public interface ITrainMotion
{
    float CurrentSpeed { get; }
    float CurrentDistance { get; }
    UnityEngine.Vector3 CurrentVelocity { get; }
    UnityEngine.Transform MotionTransform { get; }
}
