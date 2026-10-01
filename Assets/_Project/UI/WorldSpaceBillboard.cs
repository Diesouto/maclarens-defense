using UnityEngine;

// Attach to any world-space Canvas/UI element (nameplates, interact prompts, dialogue bubbles...)
// that should always face whoever is looking at it. Runs in LateUpdate so it reads this frame's
// already-updated camera position (same ordering PlayerController uses for its own camera work).
public class WorldSpaceBillboard : MonoBehaviour
{
    [Tooltip("Optional fixed camera; otherwise faces whatever camera the local player is currently viewing through.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Ignore the camera's height difference so the element stays upright instead of tilting.")]
    [SerializeField] private bool lockYAxisOnly = true;

    public void SetTargetCamera(Camera cameraToFace)
    {
        targetCamera = cameraToFace;
    }

    private void LateUpdate()
    {
        Camera viewer = ResolveViewerCamera();
        if (viewer == null)
            return;

        Vector3 toCamera = viewer.transform.position - transform.position;

        if (lockYAxisOnly)
            toCamera.y = 0f;

        if (toCamera.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(-toCamera);
    }

    // Every player prefab carries its own Camera; remote copies' cameras get disabled, and the death
    // camera swaps the output, so a cached camera goes stale and must be re-resolved.
    private Camera ResolveViewerCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
            return targetCamera;

        NetworkPlayer localPlayer = NetworkPlayer.Local;
        if (localPlayer != null && localPlayer.TryGetComponent(out PlayerController localController) &&
            localController.ViewCamera != null && localController.ViewCamera.isActiveAndEnabled)
        {
            targetCamera = localController.ViewCamera;
            return targetCamera;
        }

        targetCamera = Camera.main;
        return targetCamera;
    }
}
