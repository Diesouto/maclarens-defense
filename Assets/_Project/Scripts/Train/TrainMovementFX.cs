using UnityEngine;

// Plays the departure horn once and toggles the rail-motion loop/FX (smoke, dust, wind...) for as
// long as the train is moving, driven by TrainSplineFollower.OnMovementStarted/OnMovementStopped.
public class TrainMovementFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainSplineFollower trainSplineFollower;

    [Header("Horn (plays once on departure)")]
    [SerializeField] private AudioSource hornSource;
    [SerializeField] private AudioClip hornClip;

    [Header("Movement loop (plays while moving)")]
    [SerializeField] private AudioSource movementSource;
    [SerializeField] private AudioClip movementLoopClip;

    [Header("VFX (active while moving)")]
    [SerializeField] private GameObject[] movementFx;

    private void Awake()
    {
        if (trainSplineFollower == null)
            trainSplineFollower = GetComponentInParent<TrainSplineFollower>();
    }

    private void OnEnable()
    {
        if (trainSplineFollower != null)
        {
            trainSplineFollower.OnMovementStarted += HandleMovementStarted;
            trainSplineFollower.OnMovementStopped += HandleMovementStopped;
        }

        SetFxActive(false);
    }

    private void OnDisable()
    {
        if (trainSplineFollower != null)
        {
            trainSplineFollower.OnMovementStarted -= HandleMovementStarted;
            trainSplineFollower.OnMovementStopped -= HandleMovementStopped;
        }
    }

    private void HandleMovementStarted()
    {
        if (hornSource != null && hornClip != null)
            hornSource.PlayOneShot(hornClip);

        SetFxActive(true);

        if (movementSource != null && movementLoopClip != null)
        {
            movementSource.clip = movementLoopClip;
            movementSource.loop = true;
            movementSource.Play();
        }
    }

    private void HandleMovementStopped()
    {
        SetFxActive(false);

        if (movementSource != null)
            movementSource.Stop();
    }

    private void SetFxActive(bool active)
    {
        foreach (GameObject fx in movementFx)
        {
            if (fx != null)
                fx.SetActive(active);
        }
    }
}
