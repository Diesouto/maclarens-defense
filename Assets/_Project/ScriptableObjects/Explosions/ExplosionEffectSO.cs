using UnityEngine;

// Tuning for one kind of explosion; shared by every explosive of that type (nitro, gunpowder barrel...).
[CreateAssetMenu(fileName = "ExplosionEffect", menuName = "MacLarens/Explosion Effect")]
public class ExplosionEffectSO : ScriptableObject
{
    [Header("Area")]
    [SerializeField, Min(0.1f)] private float radius = 5f;

    [Header("Damage")]
    [Tooltip("Damage at the center; falls off linearly to 0 at the radius edge.")]
    [SerializeField, Min(0f)] private float maxDamage = 60f;

    [Header("Physics")]
    [Tooltip("Impulse on players' ragdolls and on loose rigidbodies at the center.")]
    [SerializeField, Min(0f)] private float force = 15f;
    [SerializeField, Min(0f)] private float upwardsModifier = 1.5f;

    [Header("Players")]
    [Tooltip("Seconds a surviving player stays ragdolled before getting up.")]
    [SerializeField, Min(0f)] private float knockdownDuration = 3f;

    [Header("Chain Reaction")]
    [SerializeField, Min(0f)] private float chainDelay = 0.15f;

    [Header("Threat")]
    [SerializeField, Min(0f)] private float threatAdded = 5f;

    [Header("Presentation")]
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField, Min(0f)] private float vfxLifetime = 5f;
    [SerializeField] private AudioClip sfx;

    public float Radius => radius;
    public float MaxDamage => maxDamage;
    public float Force => force;
    public float UpwardsModifier => upwardsModifier;
    public float KnockdownDuration => knockdownDuration;
    public float ChainDelay => chainDelay;
    public float ThreatAdded => threatAdded;
    public GameObject VfxPrefab => vfxPrefab;
    public float VfxLifetime => vfxLifetime;
    public AudioClip Sfx => sfx;
}
