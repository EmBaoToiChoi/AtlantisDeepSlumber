using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BalanceMeterUI : MonoBehaviour
{
    public BalanceManager balanceManager;

    public RectTransform fillRect;

    public Image fillImage;

    public TMP_Text angleText;

    void Update()
    {
        float angle = balanceManager.CurrentAngle;

        angleText.text =
            angle.ToString("F1") + "°";

        float percent =
            Mathf.Clamp01(
                angle / 20f
            );

        fillRect.localScale =
            new Vector3(
                percent,
                1,
                1
            );

        if(angle < 10)
        {
            fillImage.color = Color.green;
        }
        else if(angle < 15)
        {
            fillImage.color = Color.yellow;
        }
        else
        {
            fillImage.color = Color.red;
        }
    }
}