using UnityEngine;

public class BoozeItem : MonoBehaviour
{
    public int amount = 1;

    public void OnStolen()
    {
        amount--;
        if (amount <= 0)
            Destroy(gameObject);
    }
}
