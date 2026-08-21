using UnityEngine;

public class InteractionTestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Interactuar";
    [SerializeField] private bool disableAfterInteraction;

    private int interactionCount;

    public bool CanInteract(PlayerInteractor interactor)
    {
        return isActiveAndEnabled;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        return prompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        interactionCount++;
        Debug.Log($"{name} interactuado. Total: {interactionCount}", this);

        if (disableAfterInteraction)
            gameObject.SetActive(false);
    }
}
