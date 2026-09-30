using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private InteractUI interactUI;
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionMask = ~0;

    private PlayerInputHandler input;
    private IInteractable currentInteractable;

    public Camera InteractionCamera => interactionCamera;
    public Transform PlayerTransform => transform;

    public void SetInteractUI(InteractUI ui)
    {
        interactUI = ui;
        RefreshPrompt();
    }

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();

        if (interactionCamera == null)
            interactionCamera = Camera.main;

        if (interactUI == null)
            interactUI = GetComponentInChildren<InteractUI>();
    }

    private void Update()
    {
        UpdateTarget();

        if (currentInteractable != null && input != null && input.InteractPressed)
            currentInteractable.Interact(this);
    }

    private void UpdateTarget()
    {
        IInteractable nextInteractable = FindInteractable();

        if (nextInteractable == currentInteractable)
            return;

        currentInteractable = nextInteractable;
        RefreshPrompt();
    }

    private IInteractable FindInteractable()
    {
        if (interactionCamera == null)
            return null;

        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore))
            return null;

        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable == null || !interactable.CanInteract(this))
            return null;

        return interactable;
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

        interactUI.ShowPrompt(currentInteractable.GetPrompt(this));
    }
}
