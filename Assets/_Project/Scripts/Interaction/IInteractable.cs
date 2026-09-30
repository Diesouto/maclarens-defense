public interface IInteractable
{
    bool CanInteract(PlayerInteractor interactor);
    string GetPrompt(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);

    // Seconds the interact key must be held; 0 = instant press.
    float HoldDuration => 0f;
}
