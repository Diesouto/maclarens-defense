using UnityEngine;

public class BulletsUI : MonoBehaviour
{
    [SerializeField] private Weapon weapon;
    [SerializeField] private TMPro.TextMeshProUGUI currentBulletsText;
    [SerializeField] private TMPro.TextMeshProUGUI maxBulletsText;

    private void OnEnable()
    {
        if (weapon != null)
            weapon.OnAmmoChanged += HandleAmmoChanged;
    }

    private void OnDisable()
    {
        if (weapon != null)
            weapon.OnAmmoChanged -= HandleAmmoChanged;
    }

    private void Start()
    {
        if (weapon != null)
            HandleAmmoChanged(weapon.CurrentAmmo, weapon.MagazineSize);
    }

    private void HandleAmmoChanged(int currentAmmo, int magazineSize)
    {
        SetCurrentBulletsText(currentAmmo);
        SetMaxBulletsText(magazineSize);
    }

    private void SetCurrentBulletsText(int bullets)
    {
        if (currentBulletsText != null)
            currentBulletsText.text = bullets.ToString();
    }

    private void SetMaxBulletsText(int maxBullets)
    {
        if (maxBulletsText != null)
            maxBulletsText.text = maxBullets.ToString();
    }
}
