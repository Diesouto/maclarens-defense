using Unity.Netcode;
using UnityEngine;

// World-space NetworkTransform interpolation renders riders ~100ms in the past, which on a moving
// train leaves them metres behind the carriage. While riding, the owner also publishes its pose
// relative to the carriage and every other peer rebuilds it against its own (current) carriage.
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(TrainPassenger))]
public class NetworkTrainRider : NetworkBehaviour
{
    private readonly NetworkVariable<int> carriageId = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<Vector3> localPosition = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> localYaw = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    [SerializeField, Min(0f)] private float smoothing = 15f;
    [SerializeField, Min(0f)] private float positionThreshold = 0.005f;

    private TrainPassenger passenger;
    private Vector3 smoothedLocalPosition;
    private float smoothedLocalYaw;
    private int appliedCarriageId;

    private void Awake()
    {
        passenger = GetComponent<TrainPassenger>();
    }

    private void LateUpdate()
    {
        if (!IsSpawned)
            return;

        if (IsOwner)
            PublishOwnerPose();
        else
            ApplyRemotePose();
    }

    private void PublishOwnerPose()
    {
        TrainPassengerArea area = passenger.CurrentCarriage;
        if (area == null || area.CarriageRoot == null)
        {
            if (carriageId.Value != 0)
                carriageId.Value = 0;
            return;
        }

        Transform root = area.CarriageRoot;
        Vector3 local = root.InverseTransformPoint(transform.position);
        float yaw = Mathf.DeltaAngle(root.eulerAngles.y, transform.eulerAngles.y);

        if ((localPosition.Value - local).sqrMagnitude > positionThreshold * positionThreshold)
            localPosition.Value = local;

        if (Mathf.Abs(Mathf.DeltaAngle(localYaw.Value, yaw)) > 0.5f)
            localYaw.Value = yaw;

        if (carriageId.Value != area.NetworkId)
            carriageId.Value = area.NetworkId;
    }

    private void ApplyRemotePose()
    {
        int id = carriageId.Value;
        if (id == 0 || !TrainPassengerArea.TryGet(id, out TrainPassengerArea area) || area.CarriageRoot == null)
        {
            appliedCarriageId = 0;
            return;
        }

        if (appliedCarriageId != id)
        {
            appliedCarriageId = id;
            smoothedLocalPosition = localPosition.Value;
            smoothedLocalYaw = localYaw.Value;
        }
        else
        {
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            smoothedLocalPosition = Vector3.Lerp(smoothedLocalPosition, localPosition.Value, t);
            smoothedLocalYaw = Mathf.LerpAngle(smoothedLocalYaw, localYaw.Value, t);
        }

        // Runs after NetworkTransform, so this overrides its lagged world-space pose for this frame.
        Transform root = area.CarriageRoot;
        transform.SetPositionAndRotation(
            root.TransformPoint(smoothedLocalPosition),
            Quaternion.Euler(0f, root.eulerAngles.y + smoothedLocalYaw, 0f));
    }
}
