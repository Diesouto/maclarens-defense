using UnityEngine;

public class FinishDayInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private RunManager runManager;

    private void Awake()
    {
        if (runManager == null)
            runManager = RunManager.Instance;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return runManager != null &&
               GameStateManager.Instance != null &&
               GameStateManager.Instance.IsRunActive &&
               runManager.CurrentPhase == RunPhase.ResolvingDay;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        return "Finish Day";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        runManager.FinishDay();
    }
}
