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
public class DeathCameraController : MonoBehaviour
{
    public static DeathCameraController Instance { get; private set; }

    // NetworkPlayer raises the local player's virtual camera to 1000; the death camera must beat it.
    private const int ActivePriority = 2000;

    [SerializeField] private float activationDelay = 3f;
    [SerializeField] private CinemachineCamera deathCamera;

    private readonly List<Transform> spectateTargets = new();
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
        SetActiveCamera(true);

        activateRoutine = null;
    }

    private void RefreshTargets(Transform corpse)
    {
        spectateTargets.Clear();

        if (corpse != null)
            spectateTargets.Add(corpse);

        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player != null && player.IsAlive && player.transform != corpse)
                spectateTargets.Add(player.transform);
        }

        currentTargetIndex = 0;
        ApplyTarget();
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

        SetActiveCamera(false);
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
