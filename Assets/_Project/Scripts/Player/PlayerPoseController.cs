using UnityEngine;

public class PlayerPoseController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Transform itemHoldPointPistol;
    [SerializeField] private Transform itemHoldPointShotgun;
    [SerializeField] private Transform itemHoldPointOther;

    private static readonly int ItemAnimationProfileHash = Animator.StringToHash("ItemAnimationProfile");
    private static readonly int IsWeaponEquippedHash = Animator.StringToHash("IsWeaponEquipped");
    private static readonly int IsCarryingPistolHash = Animator.StringToHash("IsCarryingPistol");
    private static readonly int IsCarryingShotgunHash = Animator.StringToHash("IsCarryingShotgun");
    private static readonly int IsCarryingItemHash = Animator.StringToHash("IsCarryingItem");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        itemHoldPointPistol = EnsureHoldPoint(itemHoldPointPistol, "ItemHoldPointPistol", new Vector3(0.25f, 1.35f, 0.45f));
        itemHoldPointShotgun = EnsureHoldPoint(itemHoldPointShotgun, "ItemHoldPointShotgun", new Vector3(0.25f, 1.3f, 0.55f), itemHoldPointPistol);
        itemHoldPointOther = EnsureHoldPoint(itemHoldPointOther, "ItemHoldPointOther", new Vector3(0.2f, 1.2f, 0.4f), itemHoldPointPistol, itemHoldPointShotgun);
    }

    private void Update()
    {
        if (animator == null || inventory == null)
            return;

        LootDataSO activeItem = inventory.ActiveItem;
        ItemAnimationProfile profile = inventory.CurrentAnimationProfile;
        animator.SetInteger(ItemAnimationProfileHash, (int)profile);
        animator.SetBool(IsCarryingPistolHash, activeItem != null && profile == ItemAnimationProfile.Pistol);
        animator.SetBool(IsCarryingShotgunHash, activeItem != null && profile == ItemAnimationProfile.Shotgun);
        animator.SetBool(IsCarryingItemHash, activeItem != null && profile == ItemAnimationProfile.Carry);
    }

    public Transform GetItemHoldPoint(ItemAnimationProfile profile)
    {
        return profile switch
        {
            ItemAnimationProfile.Pistol => itemHoldPointPistol,
            ItemAnimationProfile.Shotgun => itemHoldPointShotgun,
            _ => itemHoldPointOther
        };
    }

    private Transform EnsureHoldPoint(Transform holdPoint, string pointName, Vector3 localPosition, params Transform[] existingPoints)
    {
        if (holdPoint != null)
        {
            foreach (Transform existingPoint in existingPoints)
            {
                if (holdPoint == existingPoint)
                    holdPoint = null;
            }
        }

        if (holdPoint == null)
        {
            GameObject pointObject = new GameObject(pointName);
            pointObject.transform.SetParent(transform, false);
            pointObject.transform.localPosition = localPosition;
            return pointObject.transform;
        }

        return holdPoint;
    }
}
