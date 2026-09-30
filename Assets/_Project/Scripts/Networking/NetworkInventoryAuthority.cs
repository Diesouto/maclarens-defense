using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerInventory))]
public class NetworkInventoryAuthority : NetworkBehaviour
{
    [SerializeField, Min(0.5f)] private float maxDropDistance = 3f;

    private PlayerInventory inventory;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestSelectSlotServerRpc(int slotIndex, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        inventory.TrySelectSlot(slotIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestDropServerRpc(Vector3 dropPosition, Vector3 throwForce, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        Vector3 fallback = inventory.transform.position + inventory.transform.forward * 1.5f;
        if (Vector3.Distance(dropPosition, inventory.transform.position) > maxDropDistance)
            dropPosition = fallback;

        inventory.TryThrowSelected(dropPosition, throwForce);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestConsumeServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == OwnerClientId)
            inventory.TryConsumeActive();
    }

    // Drunk wobble is a camera effect, so only the owning client needs to hear about it.
    public void NotifyDrunkToOwner(float duration)
    {
        if (IsServer && IsSpawned)
            DrunkOwnerRpc(duration);
    }

    [Rpc(SendTo.Owner)]
    private void DrunkOwnerRpc(float duration)
    {
        if (TryGetComponent(out PlayerController controller))
            controller.ApplyDrunk(duration);
    }
}