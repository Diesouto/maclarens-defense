public interface IInteractable
{
    bool CanInteract(PlayerInteractor interactor);
    string GetPrompt(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);
}
