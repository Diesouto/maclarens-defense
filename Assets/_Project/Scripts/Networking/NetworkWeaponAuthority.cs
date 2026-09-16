using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkWeaponAuthority : NetworkBehaviour
{
    private Weapon weapon;

    private void Awake()
    {
        weapon = GetComponentInChildren<Weapon>(true);
    }

    [ServerRpc]
    public void RequestWeaponInputServerRpc(bool firePressed, bool fireHeld, bool reloadPressed,
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || weapon == null)
            return;

        weapon.ApplyServerInput(firePressed, fireHeld, reloadPressed);
    }

    [ServerRpc]
    public void RequestHitServerRpc(NetworkObjectReference targetReference, Vector3 hitDirection,
        float hitForce, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId ||
            !targetReference.TryGet(out NetworkObject targetObject) || targetObject == null)
            return;

        Vector3 origin = transform.position + Vector3.up;
        Vector3 toTarget = targetObject.transform.position - origin;
        float targetDistance = toTarget.magnitude;
        if (targetDistance <= 0.01f || targetDistance > 100f)
            return;

        Vector3 direction = toTarget / targetDistance;
        if (hitDirection.sqrMagnitude > 0.01f && Vector3.Dot(direction, hitDirection.normalized) < 0.5f)
            return;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, targetDistance + 1f,
                ~0, QueryTriggerInteraction.Collide))
            return;

        NetworkHealth targetHealth = targetObject.GetComponent<NetworkHealth>();
        NetworkHealth hitHealth = hit.collider.GetComponentInParent<NetworkHealth>();
        if (targetHealth == null || hitHealth != targetHealth)
            return;

        float damage = weapon.WeaponData != null ? weapon.WeaponData.damage : 0f;
        Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
        if (hitbox != null && hitbox.Type == Hitbox.HitboxType.Head && weapon.WeaponData != null)
            damage *= weapon.WeaponData.headshotMultiplier;
        targetHealth.ApplyServerDamage(damage, hitDirection, Mathf.Clamp(hitForce, 0f, 100f));
    }
}