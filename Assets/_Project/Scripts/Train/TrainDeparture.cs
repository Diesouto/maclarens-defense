using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TrainDeparture : MonoBehaviour, IInteractable
{
    [SerializeField] private float departurePrepDuration = 5f;
    [SerializeField] private InteractUI interactUI;
    [SerializeField] private RunManager runManager;
    [SerializeField] private TrainSplineFollower trainSplineFollower;
    [SerializeField] private NetworkTrainState networkTrainState;

    public bool IsDeparting { get; private set; }
    public bool HasDeparted { get; private set; }

    private Coroutine countdownRoutine;
    private bool isCountdownVisible;

    // Hook for the future rail/animation system: the train can start accelerating away.
    public event Action OnTrainDeparted;

    private void Awake()
    {
        if (interactUI == null)
            interactUI = FindFirstObjectByType<InteractUI>();

        if (runManager == null)
            runManager = RunManager.Instance;

        if (trainSplineFollower == null)
            trainSplineFollower = FindFirstObjectByType<TrainSplineFollower>();

        if (networkTrainState == null)
            networkTrainState = FindFirstObjectByType<NetworkTrainState>();
    }

    private void OnEnable()
    {
        if (trainSplineFollower != null)
            trainSplineFollower.OnTownExitReached += HandleTownExitReached;
    }

    private void OnDisable()
    {
        if (trainSplineFollower != null)
            trainSplineFollower.OnTownExitReached -= HandleTownExitReached;
    }

    private void HandleTownExitReached()
    {
        if (runManager == null)
            runManager = RunManager.Instance;

        runManager?.HandleTownExit();
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        bool replicatedBusy = networkTrainState != null && networkTrainState.IsSpawned &&
            (networkTrainState.IsMoving.Value || isCountdownVisible);

        return !IsDeparting &&
               !HasDeparted &&
               !replicatedBusy &&
               (GameStateManager.Instance == null || GameStateManager.Instance.IsRunActive);
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        if (trainSplineFollower == null)
            return "Return to MacLarens";

        return trainSplineFollower.CurrentStation == TrainDestination.Town
            ? "Return to MacLarens"
            : "Depart to Town";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        if (networkTrainState != null && networkTrainState.IsSpawned)
        {
            networkTrainState.RequestDeparture();
            return;
        }

        if (IsNetworkClient())
        {
            Debug.LogError("TrainDeparture: NetworkTrainState is not spawned on this client; departure request dropped.", this);
            return;
        }

        // Departure does not require the quota to be met; the crew can choose to leave early.
        BeginAuthoritativeDeparture();
    }

    // Runs on every machine so each player sees the countdown on their own HUD.
    public void ShowDepartureCountdown(float duration)
    {
        if (countdownRoutine != null)
            StopCoroutine(countdownRoutine);

        countdownRoutine = StartCoroutine(CountdownRoutine(duration));
    }

    private IEnumerator CountdownRoutine(float duration)
    {
        isCountdownVisible = true;
        interactUI?.StartProgress(duration, "Starting train...");
        yield return new WaitForSeconds(duration);
        isCountdownVisible = false;
        countdownRoutine = null;
    }

    private static bool IsNetworkClient() => NetworkRole.IsClientOnly;

    public void BeginAuthoritativeDeparture()
    {
        if (IsDeparting || HasDeparted)
            return;

        StartCoroutine(DepartureRoutine());
    }

    private IEnumerator DepartureRoutine()
    {
        IsDeparting = true;

        if (networkTrainState != null && networkTrainState.IsSpawned)
            networkTrainState.BroadcastDepartureCountdown(departurePrepDuration);
        else
            ShowDepartureCountdown(departurePrepDuration);

        yield return new WaitForSeconds(departurePrepDuration);

        IsDeparting = false;
        HasDeparted = true;

        if (runManager == null)
            runManager = RunManager.Instance;

        bool returningToMacLarens = trainSplineFollower != null &&
            trainSplineFollower.CurrentStation == TrainDestination.Town;

        runManager?.BeginDeparture(returningToMacLarens);

        OnTrainDeparted?.Invoke();
    }

    // Called by the future rail/arrival system once the train reaches the next stop.
    public void OnArrived()
    {
        HasDeparted = false;

        if (runManager == null)
            runManager = RunManager.Instance;

        runManager?.HandleTrainArrived(trainSplineFollower != null
            ? trainSplineFollower.CurrentStation
            : TrainDestination.Town);
    }
}
