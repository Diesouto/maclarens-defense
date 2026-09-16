using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class BreakableOnImpact : MonoBehaviour
{
    [SerializeField] private float breakVelocity = 6f;
    [SerializeField] private GameObject brokenPrefab;
    [SerializeField] private AudioClip breakSound;

    private bool hasBroken;

    public void CopySettingsFrom(BreakableOnImpact source)
    {
        if (source == null)
            return;

        breakVelocity = source.breakVelocity;
        brokenPrefab = source.brokenPrefab;
        breakSound = source.breakSound;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasBroken || collision.relativeVelocity.magnitude < breakVelocity)
            return;

        Break();
    }

    private void Break()
    {
        NetworkBreakable networkBreakable = GetComponent<NetworkBreakable>();
        if (networkBreakable != null && networkBreakable.IsSpawned && !networkBreakable.IsServer)
        {
            networkBreakable.RequestBreakServerRpc();
            return;
        }

        ApplyBreak();
    }

    public void ApplyBreak()
    {
        if (hasBroken)
            return;

        hasBroken = true;

        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, transform.position);

        if (brokenPrefab != null)
        {
            GameObject brokenInstance = Instantiate(brokenPrefab, transform.position, transform.rotation);
            NetworkObject brokenNetworkObject = brokenInstance.GetComponent<NetworkObject>();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
                NetworkManager.Singleton.IsServer)
            {
                if (brokenNetworkObject == null ||
                    !NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(brokenNetworkObject.GlobalObjectIdHash))
                {
                    Destroy(brokenInstance);
                    Debug.LogError($"BreakableOnImpact: broken prefab '{brokenPrefab.name}' is not a registered NetworkObject.", brokenPrefab);
                }
                else
                {
                    brokenNetworkObject.Spawn(true);
                }
            }
        }

        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned && networkObject.IsServer)
            networkObject.Despawn(true);
        else
            Destroy(gameObject);
    }
}
