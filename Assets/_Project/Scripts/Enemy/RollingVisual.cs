using UnityEngine;

// Presentation only: spins and bounces a child model from the root's movement, so it works on every
// peer from NetworkTransform updates. The visual must be a child; the root's rotation is replicated.
public class RollingVisual : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField, Min(0.01f)] private float radius = 0.5f;
    [SerializeField, Min(0f)] private float bounceHeight = 0.3f;
    [Tooltip("Hops per metre travelled.")]
    [SerializeField, Min(0f)] private float bounceFrequency = 0.6f;

    private Vector3 lastPosition;
    private Vector3 visualRestPosition;
    private float travelled;

    private void Awake()
    {
        lastPosition = transform.position;
        if (visual != null)
            visualRestPosition = visual.localPosition;
    }

    private void Update()
    {
        if (visual == null)
            return;

        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;

        float distance = delta.magnitude;
        if (distance < 0.0001f)
            return;

        Vector3 axis = Vector3.Cross(Vector3.up, delta / distance);
        visual.Rotate(axis, distance / radius * Mathf.Rad2Deg, Space.World);

        travelled += distance;
        float hop = Mathf.Abs(Mathf.Sin(travelled * bounceFrequency * Mathf.PI)) * bounceHeight;
        visual.localPosition = visualRestPosition + Vector3.up * hop;
    }
}
