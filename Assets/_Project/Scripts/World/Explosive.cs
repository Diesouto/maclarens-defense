using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Host-only detonation: damage with falloff, knockdown for players, impulse for loose rigidbodies,
// chain reactions, then VFX/SFX on every peer. Triggered by gunfire (IDamageable) or by
// BreakableOnImpact when thrown (nitro). Also works offline without a spawned NetworkObject.
public class Explosive : NetworkBehaviour, IDamageable
{
    [SerializeField] private ExplosionEffectSO effect;
    [SerializeField] private bool explodeWhenShot = true;

    private bool hasExploded;

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector3.zero, 0f);
    }

    public void TakeDamage(float damage, Vector3 hitDirection, float forceAmount)
    {
        if (explodeWhenShot)
            Detonate(0f);
    }

    public void Detonate(float delay)
    {
        if (hasExploded || effect == null || NetworkRole.IsClientOnly)
            return;

        hasExploded = true;
        StartCoroutine(ExplodeAfter(delay));
    }

    private IEnumerator ExplodeAfter(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector3 center = transform.position;
        ApplyExplosion(center);
        ThreatManager.Instance?.RegisterExplosion(effect.ThreatAdded);

        if (IsSpawned)
            PlayEffectsRpc(center);
        else
            PlayEffects(center);

        RemoveSelf();
    }

    private void ApplyExplosion(Vector3 center)
    {
        var damaged = new HashSet<IDamageable>();
        var knockedDown = new HashSet<PlayerKnockdown>();
        var pushed = new HashSet<Rigidbody>();

        foreach (Collider hit in Physics.OverlapSphere(center, effect.Radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform))
                continue;

            float distance = Vector3.Distance(center, hit.ClosestPointOnBounds(center));
            float falloff = 1f - Mathf.Clamp01(distance / effect.Radius);
            Vector3 direction = (hit.bounds.center - center).normalized;

            PlayerKnockdown knockdown = hit.GetComponentInParent<PlayerKnockdown>();
            if (knockdown != null && knockedDown.Add(knockdown))
                knockdown.KnockDownFromServer(center, effect.Force * falloff, effect.KnockdownDuration);

            Explosive other = hit.GetComponentInParent<Explosive>();
            if (other != null)
            {
                other.Detonate(effect.ChainDelay);
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable != null && damaged.Add(damageable))
                damageable.TakeDamage(effect.MaxDamage * falloff, direction, effect.Force * falloff);

            // Players are pushed through their knockdown ragdoll; kinematic bones (alive enemies) are skipped.
            Rigidbody body = hit.attachedRigidbody;
            if (knockdown == null && body != null && !body.isKinematic && pushed.Add(body))
                body.AddExplosionForce(effect.Force, center, effect.Radius, effect.UpwardsModifier, ForceMode.Impulse);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlayEffectsRpc(Vector3 center)
    {
        PlayEffects(center);
    }

    private void PlayEffects(Vector3 center)
    {
        if (effect.Sfx != null)
            AudioSource.PlayClipAtPoint(effect.Sfx, center);

        if (effect.VfxPrefab == null)
            return;

        GameObject vfx = Instantiate(effect.VfxPrefab, center, Quaternion.identity);
        if (effect.VfxLifetime > 0f)
            Destroy(vfx, effect.VfxLifetime);
    }

    private void RemoveSelf()
    {
        if (TryGetComponent(out LootItem lootItem))
        {
            lootItem.Cargo?.RemoveItem(lootItem);
            LootRegistry.Instance?.Unregister(lootItem);
        }

        if (IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }
}
