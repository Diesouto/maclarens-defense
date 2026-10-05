using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private InteractUI interactUI;
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionMask = ~0;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private PlayerInputHandler input;
    private PlayerController playerController;
    private IInteractable currentInteractable;
    private IInteractable holdTarget;
    private float holdStartedAt;
    private bool canInteractWithCurrent;
    private string lastPromptText;

    // Prefer this player's own view camera: Camera.main can resolve to another player's camera in multiplayer.
    public Camera InteractionCamera => playerController != null && playerController.ViewCamera != null
        ? playerController.ViewCamera
        : interactionCamera;
    public Transform PlayerTransform => transform;

    public void SetInteractUI(InteractUI ui)
    {
        interactUI = ui;
        RefreshPrompt();
    }

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        playerController = GetComponent<PlayerController>();

        if (interactionCamera == null)
            interactionCamera = Camera.main;

        if (interactUI == null)
            interactUI = GetComponentInChildren<InteractUI>();
    }

    private void OnDisable()
    {
        CancelHold();
    }

    private void Update()
    {
        UpdateTarget();

        if (currentInteractable == null || input == null || !canInteractWithCurrent)
        {
            CancelHold();
            return;
        }

        float holdDuration = currentInteractable.HoldDuration;
        if (holdDuration <= 0f)
        {
            if (input.InteractPressed)
                currentInteractable.Interact(this);
            return;
        }

        UpdateHold(holdDuration);
    }

    private void UpdateHold(float holdDuration)
    {
        if (input.InteractPressed && holdTarget == null)
        {
            holdTarget = currentInteractable;
            holdStartedAt = Time.time;
            interactUI?.StartHeldProgress(holdDuration, currentInteractable.GetPrompt(this));
        }

        if (holdTarget == null)
            return;

        if (!input.InteractHeld || holdTarget != currentInteractable)
        {
            CancelHold();
            return;
        }

        if (Time.time - holdStartedAt < holdDuration)
            return;

        IInteractable target = holdTarget;
        holdTarget = null;
        interactUI?.FinishProgress();
        target.Interact(this);
        RefreshPrompt();
    }

    private void CancelHold()
    {
        if (holdTarget == null)
            return;

        holdTarget = null;
        interactUI?.CancelProgress();
        RefreshPrompt();
    }

    private void UpdateTarget()
    {
        IInteractable nextInteractable = FindInteractable();
        bool nextCanInteract = nextInteractable != null && nextInteractable.CanInteract(this);
        string nextPrompt = nextInteractable == null ? null : nextInteractable.GetPrompt(this);

        if (nextInteractable == currentInteractable &&
            nextCanInteract == canInteractWithCurrent &&
            nextPrompt == lastPromptText)
        {
            return;
        }

        currentInteractable = nextInteractable;
        canInteractWithCurrent = nextCanInteract;
        lastPromptText = nextPrompt;
        RefreshPrompt();
    }

    // Blocked interactables are still returned so the prompt can explain why (greyed out) instead of vanishing.
    private IInteractable FindInteractable()
    {
        Camera viewCamera = InteractionCamera;
        if (viewCamera == null)
            return null;

        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        int count = Physics.RaycastNonAlloc(ray, hitBuffer, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue;
        Collider closestCollider = null;

        // Own body colliders are see-through so they never block or become the target.
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.distance >= closest || hit.collider.transform.IsChildOf(transform))
                continue;

            closest = hit.distance;
            closestCollider = hit.collider;
        }

        return closestCollider == null ? null : closestCollider.GetComponentInParent<IInteractable>();
    }

    private void RefreshPrompt()
    {
        if (interactUI == null)
            return;

        if (currentInteractable == null)
        {
            interactUI.HidePrompt();
            return;
        }

        interactUI.ShowPrompt(currentInteractable.GetPrompt(this), canInteractWithCurrent);
    }
}
