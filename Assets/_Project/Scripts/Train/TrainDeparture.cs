using System;
using System.Collections;
using UnityEngine;

public class TrainDeparture : MonoBehaviour, IInteractable
{
    [SerializeField] private float departurePrepDuration = 5f;
    [SerializeField] private InteractUI interactUI;
    [SerializeField] private RunManager runManager;

    public bool IsDeparting { get; private set; }
    public bool HasDeparted { get; private set; }

    // Hook for the future rail/animation system: the train can start accelerating away.
    public event Action OnTrainDeparted;

    private void Awake()
    {
        if (interactUI == null)
            interactUI = FindFirstObjectByType<InteractUI>();

        if (runManager == null)
            runManager = RunManager.Instance;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !IsDeparting && !HasDeparted;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        return "Return to MacLarens";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        // Departure does not require the quota to be met; the crew can choose to leave early.
        StartCoroutine(DepartureRoutine());
    }

    private IEnumerator DepartureRoutine()
    {
        IsDeparting = true;

        interactUI?.StartProgress(departurePrepDuration, "Starting train...");
        yield return new WaitForSeconds(departurePrepDuration);

        IsDeparting = false;
        HasDeparted = true;

        if (runManager == null)
            runManager = RunManager.Instance;
        runManager?.AdvanceDay();

        OnTrainDeparted?.Invoke();
    }

    // Called by the future rail/arrival system once the train reaches the next stop.
    public void OnArrived()
    {
        HasDeparted = false;
    }
}
