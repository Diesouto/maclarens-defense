using System.Collections.Generic;
using UnityEngine;

// Marks a moving train volume and feeds exact point motion into players and loose rigidbodies inside it.
[RequireComponent(typeof(BoxCollider))]
[DefaultExecutionOrder(100)]
public class TrainPlatform : MonoBehaviour
{
    private sealed class PlayerState
    {
        public PlayerMotor Motor;
        public ITrainMotion MotionSource;
        public Vector3 LocalPoint;
        public Vector3 PreviousWorldPoint;
    }

    private sealed class RigidbodyState
    {
        public Rigidbody Rigidbody;
        public ITrainMotion MotionSource;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public bool WasKinematic;
        public Vector3 LastVelocity;
    }

    [SerializeField] private BoxCollider platformTrigger;
    [SerializeField] private MonoBehaviour motionSourceBehaviour;

    private static readonly Dictionary<Rigidbody, TrainPlatform> rigidbodyOwners = new();

    private readonly Dictionary<PlayerMotor, PlayerState> riderStates = new();
    private readonly Dictionary<Rigidbody, RigidbodyState> rigidbodyStates = new();
    private ITrainMotion motionSource;

    private void Awake()
    {
        if (platformTrigger == null)
            platformTrigger = GetComponent<BoxCollider>();

        platformTrigger.isTrigger = true;

        motionSource = motionSourceBehaviour as ITrainMotion;
        if (motionSource == null)
            motionSource = GetComponent<TrainSplineFollower>() as ITrainMotion
                ?? GetComponent<TrainCarFollower>() as ITrainMotion
                ?? GetComponentInParent<TrainSplineFollower>() as ITrainMotion
                ?? GetComponentInParent<TrainCarFollower>() as ITrainMotion;

        if (motionSource == null)
            Debug.LogWarning($"{name}: TrainPlatform needs a TrainSplineFollower/TrainCarFollower on this object or assigned as Motion Source.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterOccupant(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterOccupant(other);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMotor rider = other.GetComponentInParent<PlayerMotor>();
        if (rider != null)
        {
            riderStates.Remove(rider);
            return;
        }

        Rigidbody rigidbody = other.attachedRigidbody;
        if (rigidbody == null)
            return;

        ReleaseRigidbody(rigidbody);
    }

    private void RegisterOccupant(Collider other)
    {
        if (other.transform.IsChildOf(transform.root))
            return;

        PlayerMotor rider = other.GetComponentInParent<PlayerMotor>();
        if (rider != null)
        {
            RegisterPlayer(rider);
            return;
        }

        Rigidbody rigidbody = other.attachedRigidbody;
        if (rigidbody == null || rigidbody.isKinematic || rigidbody.transform.IsChildOf(transform.root))
            return;

        RegisterRigidbody(rigidbody);
    }

    private void LateUpdate()
    {
        if (motionSource == null)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        CleanupPlayers();
        foreach (PlayerState state in riderStates.Values)
        {
            Vector3 currentWorldPoint = state.MotionSource.MotionTransform.TransformPoint(state.LocalPoint);
            Vector3 pointVelocity = (currentWorldPoint - state.PreviousWorldPoint) / deltaTime;
            float score = (state.Motor.transform.position - state.MotionSource.MotionTransform.position).sqrMagnitude;
            state.Motor.RegisterPlatformVelocity(pointVelocity, score);

            state.LocalPoint = state.MotionSource.MotionTransform.InverseTransformPoint(state.Motor.transform.position);
            state.PreviousWorldPoint = state.MotionSource.MotionTransform.TransformPoint(state.LocalPoint);
        }

        CleanupRigidbodies();
        foreach (RigidbodyState state in rigidbodyStates.Values)
        {
            Vector3 targetPosition = state.MotionSource.MotionTransform.TransformPoint(state.LocalPosition);
            Quaternion targetRotation = state.MotionSource.MotionTransform.rotation * state.LocalRotation;
            state.LastVelocity = (targetPosition - state.Rigidbody.position) / deltaTime;
            state.Rigidbody.MovePosition(targetPosition);
            state.Rigidbody.MoveRotation(targetRotation);
        }
    }

    private void RegisterPlayer(PlayerMotor rider)
    {
        if (rider == null)
            return;

        if (riderStates.ContainsKey(rider))
            return;

        Transform motionTransform = motionSource.MotionTransform;
        Vector3 localPoint = motionTransform.InverseTransformPoint(rider.transform.position);
        riderStates.Add(rider, new PlayerState
        {
            Motor = rider,
            MotionSource = motionSource,
            LocalPoint = localPoint,
            PreviousWorldPoint = motionTransform.TransformPoint(localPoint)
        });
    }

    private void RegisterRigidbody(Rigidbody rigidbody)
    {
        if (rigidbody == null)
            return;

        if (rigidbodyOwners.TryGetValue(rigidbody, out TrainPlatform owner) && owner != this)
            return;

        if (rigidbodyStates.ContainsKey(rigidbody))
            return;

        rigidbodyOwners[rigidbody] = this;

        Transform motionTransform = motionSource.MotionTransform;
        rigidbodyStates.Add(rigidbody, new RigidbodyState
        {
            Rigidbody = rigidbody,
            MotionSource = motionSource,
            LocalPosition = motionTransform.InverseTransformPoint(rigidbody.position),
            LocalRotation = Quaternion.Inverse(motionTransform.rotation) * rigidbody.rotation,
            WasKinematic = rigidbody.isKinematic,
            LastVelocity = motionSource.CurrentVelocity
        });

        rigidbody.isKinematic = true;
    }

    private void ReleaseRigidbody(Rigidbody rigidbody)
    {
        if (rigidbody == null)
            return;

        if (!rigidbodyStates.TryGetValue(rigidbody, out RigidbodyState state))
            return;

        rigidbody.isKinematic = state.WasKinematic;
        if (!rigidbody.isKinematic)
            rigidbody.linearVelocity = state.LastVelocity;

        rigidbodyStates.Remove(rigidbody);
        rigidbodyOwners.Remove(rigidbody);
    }

    private void CleanupPlayers()
    {
        List<PlayerMotor> stalePlayers = null;
        foreach (KeyValuePair<PlayerMotor, PlayerState> pair in riderStates)
        {
            if (pair.Key != null && pair.Key.enabled)
                continue;

            stalePlayers ??= new List<PlayerMotor>();
            stalePlayers.Add(pair.Key);
        }

        if (stalePlayers == null)
            return;

        foreach (PlayerMotor rider in stalePlayers)
            riderStates.Remove(rider);
    }

    private void CleanupRigidbodies()
    {
        List<Rigidbody> staleBodies = null;
        foreach (KeyValuePair<Rigidbody, RigidbodyState> pair in rigidbodyStates)
        {
            if (pair.Key != null)
                continue;

            staleBodies ??= new List<Rigidbody>();
            staleBodies.Add(pair.Key);
        }

        if (staleBodies == null)
            return;

        foreach (Rigidbody rigidbody in staleBodies)
        {
            rigidbodyStates.Remove(rigidbody);
            rigidbodyOwners.Remove(rigidbody);
        }
    }
}
