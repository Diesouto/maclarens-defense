using UnityEngine;

// Attach to any world-space Canvas/UI element (nameplates, interact prompts, dialogue bubbles...)
// that should always face whoever is looking at it. Runs in LateUpdate so it reads this frame's
// already-updated camera position (same ordering PlayerController uses for its own camera work).
public class WorldSpaceBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [Tooltip("Ignore the camera's height difference so the element stays upright instead of tilting.")]
    [SerializeField] private bool lockYAxisOnly = true;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    public void SetTargetCamera(Camera cameraToFace)
    {
        targetCamera = cameraToFace;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;

            if (targetCamera == null)
                return;
        }

        Vector3 toCamera = targetCamera.transform.position - transform.position;

        if (lockYAxisOnly)
            toCamera.y = 0f;

        if (toCamera.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(-toCamera);
    }
}
