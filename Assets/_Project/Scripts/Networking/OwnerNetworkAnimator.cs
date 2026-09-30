using Unity.Netcode.Components;

// Player animator params are written by the owning client (PlayerController only runs there),
// so the owner must be the authority, matching the owner-authoritative NetworkTransform.
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
