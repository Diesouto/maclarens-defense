using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

// Thin adapter over a Unity SplineContainer exposing world-space, distance-based queries
// so the authored rail can be edited visually with the Splines tools.
[RequireComponent(typeof(SplineContainer))]
public class TrainSpline : MonoBehaviour
{
    [SerializeField] private int samplesPerMeter = 2;

    private SplineContainer container;
    private readonly List<float> sampleDistances = new();
    private readonly List<float> sampleT = new();
    private bool initialized;

    public float Length { get; private set; }
    public bool IsClosed => container != null && container.Spline != null && container.Spline.Closed;

    private void Awake()
    {
        EnsureInitialized();
    }

    // Wraps around the loop for a closed spline, clamps to the ends for an open one.
    public float WrapDistance(float distance)
    {
        EnsureInitialized();

        if (Length <= 0f)
            return 0f;

        if (!IsClosed)
            return Mathf.Clamp(distance, 0f, Length);

        float wrapped = distance % Length;
        if (wrapped < 0f)
            wrapped += Length;

        return wrapped;
    }

    public Vector3 GetPointAtDistance(float distance)
    {
        EnsureInitialized();
        return container.EvaluatePosition(GetTAtDistance(distance));
    }

    public Quaternion GetRotationAtDistance(float distance, Vector3 upFallback)
    {
        EnsureInitialized();
        float t = GetTAtDistance(distance);
        Vector3 tangent = container.EvaluateTangent(t);
        if (tangent.sqrMagnitude < 0.0001f)
            return transform.rotation;

        Vector3 up = container.EvaluateUpVector(t);
        if (up.sqrMagnitude < 0.0001f)
            up = upFallback;

        return Quaternion.LookRotation(tangent.normalized, up);
    }

    public float GetNearestDistance(Vector3 worldPosition)
    {
        EnsureInitialized();
        Vector3 localPoint = container.transform.InverseTransformPoint(worldPosition);
        // Higher resolution/iterations than the default: on a loop that curves back close to itself
        // (e.g. near the stations) the default search can snap to the wrong branch of the track.
        SplineUtility.GetNearestPoint(container.Spline, localPoint, out _, out float t, resolution: 24, iterations: 4);
        return t * Length;
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        container = GetComponent<SplineContainer>();
        initialized = true;
        BuildSampleTable();
    }

    private void BuildSampleTable()
    {
        sampleDistances.Clear();
        sampleT.Clear();

        Length = container.CalculateLength();
        if (Length <= 0f)
            return;

        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(Length * samplesPerMeter));
        Vector3 previousPoint = container.EvaluatePosition(0f);
        sampleDistances.Add(0f);
        sampleT.Add(0f);

        float accumulated = 0f;
        for (int i = 1; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector3 point = container.EvaluatePosition(t);
            accumulated += Vector3.Distance(previousPoint, point);
            sampleDistances.Add(accumulated);
            sampleT.Add(t);
            previousPoint = point;
        }
    }

    private float GetTAtDistance(float distance)
    {
        if (sampleDistances.Count == 0)
            return 0f;

        distance = WrapDistance(distance);

        int low = 0;
        int high = sampleDistances.Count - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (sampleDistances[mid] <= distance)
                low = mid;
            else
                high = mid - 1;
        }

        if (low >= sampleDistances.Count - 1)
            return sampleT[^1];

        float segmentLength = sampleDistances[low + 1] - sampleDistances[low];
        float fraction = segmentLength > 0f ? (distance - sampleDistances[low]) / segmentLength : 0f;
        return Mathf.Lerp(sampleT[low], sampleT[low + 1], fraction);
    }
}
