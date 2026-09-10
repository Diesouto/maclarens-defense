using TMPro;
using UnityEngine;

// Pure dialogue: reads nothing beyond cycling lines. Live stats belong to FinishDayStatusUI.
public class MacLarensOwner : MonoBehaviour, IInteractable
{
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private string[] dialogueLines =
    {
        "Bring enough money back to MacLarens.",
        "The debt will not wait for your crew.",
        "Sell the loot, prepare, and finish the day when you are ready."
    };

    private int dialogueIndex;

    public bool CanInteract(PlayerInteractor interactor)
    {
        return GameStateManager.Instance == null || GameStateManager.Instance.IsRunActive;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        return "Talk to Owner";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
            return;

        if (dialogueLines == null || dialogueLines.Length == 0)
            return;

        string line = dialogueLines[dialogueIndex];
        dialogueIndex = (dialogueIndex + 1) % dialogueLines.Length;

        if (dialogueText != null)
            dialogueText.text = line;
        else
            Debug.Log($"MacLarens Owner: {line}", this);
    }
}
