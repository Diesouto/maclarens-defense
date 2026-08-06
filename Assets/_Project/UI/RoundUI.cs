using UnityEngine;

public class RoundUI : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI roundNumberText;

    private void SetRoundText(int round)
    {
        if (roundNumberText != null)
            roundNumberText.text = round.ToString();
    }
}
