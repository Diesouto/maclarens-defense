using UnityEngine;

public class Hitbox : MonoBehaviour
{
    public enum HitboxType
    {
        Body,
        Head
    }

    [SerializeField] private HitboxType type;

    public HitboxType Type => type;
}