using UnityEngine;

// Tracks the carriage the player currently rides without reparenting the player transform:
// CharacterController fights transform-hierarchy jumps from a moving/rotating parent, so instead
// we compute the carriage's per-frame delta and let PlayerMotor apply it through controller.Move().
public class TrainPassenger : MonoBehaviour
{
    [SerializeField, Tooltip("How fast carried train momentum bleeds off once grounded away from the train (m/s per second).")]
    private float groundedMomentumDecay = 20f;

    public TrainPassengerArea CurrentCarriage { get; private set; }

    private Vector3 lastCarriagePosition;
    private Quaternion lastCarriageRotation;
    private Vector3 carriedVelocity;

    public void SetCarriage(TrainPassengerArea carriage)
    {
        if (CurrentCarriage == carriage)
            return;

        CurrentCarriage = carriage;

        if (carriage == null)
        {
            Debug.Log($"{name} left the train.");
            return;
        }

        lastCarriagePosition = carriage.CarriageRoot.position;
        lastCarriageRotation = carriage.CarriageRoot.rotation;
        Debug.Log($"{name} boarded {carriage.name}.");
    }

    // While aboard, returns the carriage's world-space displacement and yaw (degrees) since the last
    // call. Once the player leaves (e.g. jumping to the next carriage or off the train), keeps
    // applying the last known train velocity so momentum carries through the jump instead of the
    // player being left behind mid-air; that carried velocity only bleeds off once grounded off-train.
    public bool TryGetCarriageDelta(Vector3 worldPosition, bool grounded, out Vector3 deltaPosition, out float deltaYaw)
    {
        deltaYaw = 0f;

        if (CurrentCarriage != null)
        {
            Transform carriageRoot = CurrentCarriage.CarriageRoot;
            Quaternion currentRotation = carriageRoot.rotation;
            Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastCarriageRotation);

            Vector3 pivotOffset = worldPosition - lastCarriagePosition;
            Vector3 targetPosition = carriageRoot.position + deltaRotation * pivotOffset;
            deltaPosition = targetPosition - worldPosition;

            deltaYaw = deltaRotation.eulerAngles.y;
            if (deltaYaw > 180f)
                deltaYaw -= 360f;

            lastCarriagePosition = carriageRoot.position;
            lastCarriageRotation = currentRotation;

            carriedVelocity = Time.deltaTime > 0f ? deltaPosition / Time.deltaTime : Vector3.zero;
            return true;
        }

        if (grounded)
            carriedVelocity = Vector3.MoveTowards(carriedVelocity, Vector3.zero, groundedMomentumDecay * Time.deltaTime);

        if (carriedVelocity.sqrMagnitude <= 0.0001f)
        {
            deltaPosition = Vector3.zero;
            return false;
        }

        deltaPosition = carriedVelocity * Time.deltaTime;
        return true;
    }
}