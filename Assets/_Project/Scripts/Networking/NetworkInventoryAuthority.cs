using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerInventory))]
public class NetworkInventoryAuthority : NetworkBehaviour
{
    private PlayerInventory inventory;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    [ServerRpc]
    public void RequestSelectSlotServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        inventory.TrySelectSlot(slotIndex);
    }

    [ServerRpc]
    public void RequestDropServerRpc(Vector3 throwForce, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        inventory.TryThrowSelected(
            inventory.transform.position + inventory.transform.forward * 1.5f,
            throwForce);
    }
}