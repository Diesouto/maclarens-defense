using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// Singleton: some time after a player dies, takes over rendering with a 3rd-person orbiting
// Cinemachine camera aimed at the corpse by default. Left/right cycles to other alive players,
// same idea as PEAK's spectator camera.
// Requires a CinemachineBrain on the player's FPS Camera: enabling deathCamera is enough for the
// brain to blend control away from PlayerController's manual rotation, no second Camera needed.
[DefaultExecutionOrder(-50)]
public class DeathCameraController : MonoBehaviour
{
    public static DeathCameraController Instance { get; private set; }

    // NetworkPlayer raises the local player's virtual camera to 1000; the death camera must beat it.
    private const int ActivePriority = 2000;

    [SerializeField] private float activationDelay = 3f;
    [SerializeField] private CinemachineCamera deathCamera;
    [SerializeField, Min(0.01f)] private float corpseFollowSmoothTime = 0.4f;

    // Smoothed, rotation-free stand-in for the corpse: a carried/tossed ragdoll jerks around too much to follow directly.
    private Transform corpseProxy;
    private Transform corpseSource;
    private Vector3 corpseProxyVelocity;

    private readonly List<Transform> spectateTargets = new();
    private readonly List<Camera> suppressedCameras = new();
    private int currentTargetIndex;
    private Coroutine activateRoutine;

    private void Awake()
    {
        Instance = this;

        SetActiveCamera(false);
    }

    public void NotifyPlayerDied(PlayerController deadPlayer)
    {
        if (deadPlayer == null)
            return;

        if (activateRoutine != null)
            StopCoroutine(activateRoutine);

        activateRoutine = StartCoroutine(ActivateAfterDelay(deadPlayer));
    }

    private IEnumerator ActivateAfterDelay(PlayerController deadPlayer)
    {
        yield return new WaitForSeconds(activationDelay);

        RefreshTargets(deadPlayer != null ? deadPlayer.transform : null);
        if (deadPlayer != null && deadPlayer.TryGetComponent(out PlayerBodyVisibility bodyVisibility))
            bodyVisibility.ShowBodyToAllCameras();

        SuppressPlayerCameras(deadPlayer);
        SetActiveCamera(true);
        Debug.Log($"DeathCameraController: spectating '{(spectateTargets.Count > 0 ? spectateTargets[0].name : "nothing")}'.", this);

        activateRoutine = null;
    }

    // A Camera living on the player prefab renders over the scene's CinemachineBrain output, which
    // is what the death camera drives; hide it while spectating.
    private void SuppressPlayerCameras(PlayerController deadPlayer)
    {
        RestorePlayerCameras();
        if (deadPlayer == null)
            return;

        foreach (Camera playerCamera in deadPlayer.GetComponentsInChildren<Camera>())
        {
            if (!playerCamera.enabled)
                continue;

            playerCamera.enabled = false;
            suppressedCameras.Add(playerCamera);
        }
    }

    private void RestorePlayerCameras()
    {
        foreach (Camera playerCamera in suppressedCameras)
        {
            if (playerCamera != null)
                playerCamera.enabled = true;
        }

        suppressedCameras.Clear();
    }

    private void RefreshTargets(Transform corpse)
    {
        spectateTargets.Clear();

        corpseSource = null;

        // Follow the ragdoll's hips: the root stays where the player died while the body gets carried away.
        if (corpse != null)
        {
            corpseSource = corpse;
            if (corpse.TryGetComponent(out CharacterRagdollController ragdoll) && ragdoll.RootRigidbody != null)
                corpseSource = ragdoll.RootRigidbody.transform;

            if (corpseProxy == null)
                corpseProxy = new GameObject("DeathCameraCorpseTarget").transform;

            corpseProxy.SetPositionAndRotation(corpseSource.position, Quaternion.identity);
            corpseProxyVelocity = Vector3.zero;
            spectateTargets.Add(corpseProxy);
        }

        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player != null && player.IsAlive && player.transform != corpse)
                spectateTargets.Add(player.transform);
        }

        currentTargetIndex = 0;
        ApplyTarget();
    }

    private void LateUpdate()
    {
        if (corpseProxy == null || corpseSource == null)
            return;

        corpseProxy.position = Vector3.SmoothDamp(corpseProxy.position, corpseSource.position,
            ref corpseProxyVelocity, corpseFollowSmoothTime);
    }

    private void Update()
    {
        if (deathCamera == null || !deathCamera.gameObject.activeSelf || Keyboard.current == null)
            return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame)
            CycleTarget(-1);
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
            CycleTarget(1);
    }

    private void CycleTarget(int direction)
    {
        if (spectateTargets.Count == 0)
            return;

        currentTargetIndex = (currentTargetIndex + direction + spectateTargets.Count) % spectateTargets.Count;
        ApplyTarget();
    }

    private void ApplyTarget()
    {
        if (deathCamera == null || spectateTargets.Count == 0)
            return;

        Transform target = spectateTargets[currentTargetIndex];
        deathCamera.Follow = target;
        deathCamera.LookAt = target;
    }

    // Hook for a future respawn/round-reset flow to hand control back to the player.
    public void Deactivate()
    {
        if (activateRoutine != null)
        {
            StopCoroutine(activateRoutine);
            activateRoutine = null;
        }

        corpseSource = null;
        RestorePlayerCameras();
        SetActiveCamera(false);
    }

    private void OnDestroy()
    {
        if (corpseProxy != null)
            Destroy(corpseProxy.gameObject);
    }

    private void SetActiveCamera(bool active)
    {
        if (deathCamera == null)
            return;

        deathCamera.gameObject.SetActive(active);
        if (!active)
            return;

        deathCamera.Priority = ActivePriority;
        deathCamera.Prioritize();
    }
}
