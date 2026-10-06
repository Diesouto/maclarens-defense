using System.Collections;
using TMPro;
using UnityEngine;

// Pure dialogue: reads nothing beyond cycling lines. Live stats belong to QuotaStatusUI.
public class MacLarensOwner : MonoBehaviour, IInteractable
{
    [SerializeField] private TMP_Text dialogueText;
    [Tooltip("Shown while talking and hidden otherwise; defaults to the dialogue text's canvas.")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField, Min(0f)] private float dialogueVisibleDuration = 10f;
    [SerializeField] private string[] dialogueLines =
    {
        "Bring enough money back to MacLarens.",
        "The clock starts as soon as that train leaves.",
        "Pay the quota here before time runs out."
    };

    private int dialogueIndex;
    private Coroutine hideRoutine;

    private void Awake()
    {
        // Only fall back to the whole canvas when it belongs to the Owner; a shared HUD canvas must stay up.
        if (dialogueRoot == null && dialogueText != null)
        {
            Canvas canvas = dialogueText.canvas;
            dialogueRoot = canvas != null && canvas.gameObject != gameObject && canvas.transform.IsChildOf(transform)
                ? canvas.gameObject
                : dialogueText.gameObject;
        }

        // Hiding our own GameObject would disable this interactable too.
        if (dialogueRoot == gameObject)
            dialogueRoot = dialogueText != null ? dialogueText.gameObject : null;

        SetDialogueVisible(false);
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return GameStateManager.Instance == null || GameStateManager.Instance.IsRunActive;
    }

    public string GetPrompt(PlayerInteractor interactor)
    {
        return "Bobby Conbares";
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

        SetDialogueVisible(true);

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(dialogueVisibleDuration);
        hideRoutine = null;
        SetDialogueVisible(false);
    }

    private void SetDialogueVisible(bool visible)
    {
        if (dialogueRoot != null)
            dialogueRoot.SetActive(visible);
    }
}
