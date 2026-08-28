using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Distance-based adapter over a Unity SplineContainer.
///
/// Provides:
/// - Spline length
/// - Position at distance
/// - Rotation at distance
/// - Distance wrapping
/// - Nearest distance
///
/// Works both during play mode and in the Unity Editor.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class TrainSpline : MonoBehaviour
{
    [Header("Sampling")]
    [SerializeField, Min(1)]
    private int samplesPerMeter = 2;

    private SplineContainer container;

    private readonly List<float> sampleDistances = new();
    private readonly List<float> sampleT = new();

    private float length;
    private bool initialized;

    /// <summary>
    /// Total length of the spline in world units.
    /// </summary>
    public float Length
    {
        get
        {
            EnsureInitialized();
            return length;
        }
    }

    public bool IsClosed
    {
        get
        {
            EnsureInitialized();

            return container != null &&
                   container.Spline != null &&
                   container.Spline.Closed;
        }
    }

    private void Awake()
{
    EnsureInitialized();

    Debug.Log(
        $"TRAIN SPLINE | Length = {Length} | " +
        $"Closed = {IsClosed} | " +
        $"Container = {container}"
    );
}

#if UNITY_EDITOR

    private void OnEnable()
    {
        initialized = false;
        EnsureInitialized();
    }

    private void OnValidate()
    {
        initialized = false;
        EnsureInitialized();
    }

#endif

    /// <summary>
    /// Wraps a distance around a closed spline.
    /// Clamps to the ends for an open spline.
    /// </summary>
    public float WrapDistance(float distance)
    {
        EnsureInitialized();

        if (length <= 0f)
            return 0f;

        if (!IsClosed)
            return Mathf.Clamp(distance, 0f, length);

        float wrapped = distance % length;

        if (wrapped < 0f)
            wrapped += length;

        return wrapped;
    }

    /// <summary>
    /// Gets the world-space position at a distance along the spline.
    /// </summary>
    public Vector3 GetPointAtDistance(float distance)
    {
        EnsureInitialized();

        if (container == null || length <= 0f)
            return transform.position;

        return container.EvaluatePosition(
            GetTAtDistance(distance)
        );
    }

    /// <summary>
    /// Gets the world-space rotation at a distance along the spline.
    /// </summary>
    public Quaternion GetRotationAtDistance(
        float distance,
        Vector3 upFallback)
    {
        EnsureInitialized();

        if (container == null || length <= 0f)
            return transform.rotation;

        float t = GetTAtDistance(distance);

        Vector3 tangent =
            container.EvaluateTangent(t);

        if (tangent.sqrMagnitude < 0.0001f)
            return transform.rotation;

        Vector3 up =
            container.EvaluateUpVector(t);

        if (up.sqrMagnitude < 0.0001f)
            up = upFallback;

        return Quaternion.LookRotation(
            tangent.normalized,
            up
        );
    }

    /// <summary>
    /// Finds the nearest position on the spline and returns
    /// its distance from the beginning of the spline.
    /// </summary>
    public float GetNearestDistance(Vector3 worldPosition)
    {
        EnsureInitialized();

        if (container == null || length <= 0f)
            return 0f;

        Vector3 localPoint =
            container.transform.InverseTransformPoint(
                worldPosition
            );

        SplineUtility.GetNearestPoint(
            container.Spline,
            localPoint,
            out _,
            out float t,
            resolution: 24,
            iterations: 4
        );

        return Mathf.Clamp01(t) * length;
    }

    private void EnsureInitialized()
    {
        if (initialized &&
            container != null &&
            sampleDistances.Count > 0)
        {
            return;
        }

        container = GetComponent<SplineContainer>();

        if (container == null ||
            container.Spline == null)
        {
            length = 0f;
            sampleDistances.Clear();
            sampleT.Clear();
            initialized = true;
            return;
        }

        BuildSampleTable();

        initialized = true;
    }

    private void BuildSampleTable()
    {
        sampleDistances.Clear();
        sampleT.Clear();

        if (container == null ||
            container.Spline == null)
        {
            length = 0f;
            return;
        }

        // Calculate actual spline length.
        length = container.CalculateLength();

        if (length <= 0f)
        {
            sampleDistances.Add(0f);
            sampleT.Add(0f);
            return;
        }

        int sampleCount = Mathf.Max(
            2,
            Mathf.CeilToInt(
                length * samplesPerMeter
            )
        );

        Vector3 previousPoint =
            container.EvaluatePosition(0f);

        sampleDistances.Add(0f);
        sampleT.Add(0f);

        float accumulated = 0f;

        for (int i = 1; i <= sampleCount; i++)
        {
            float t =
                i / (float)sampleCount;

            Vector3 point =
                container.EvaluatePosition(t);

            accumulated +=
                Vector3.Distance(
                    previousPoint,
                    point
                );

            sampleDistances.Add(
                accumulated
            );

            sampleT.Add(t);

            previousPoint = point;
        }
    }

    private float GetTAtDistance(float distance)
    {
        EnsureInitialized();

        if (sampleDistances.Count == 0)
            return 0f;

        distance = WrapDistance(distance);

        int low = 0;
        int high = sampleDistances.Count - 1;

        while (low < high)
        {
            int mid =
                (low + high + 1) / 2;

            if (sampleDistances[mid] <= distance)
                low = mid;
            else
                high = mid - 1;
        }

        if (low >= sampleDistances.Count - 1)
            return sampleT[^1];

        float segmentLength =
            sampleDistances[low + 1] -
            sampleDistances[low];

        float fraction =
            segmentLength > 0f
                ? (distance - sampleDistances[low]) /
                  segmentLength
                : 0f;

        return Mathf.Lerp(
            sampleT[low],
            sampleT[low + 1],
            fraction
        );
    }
}