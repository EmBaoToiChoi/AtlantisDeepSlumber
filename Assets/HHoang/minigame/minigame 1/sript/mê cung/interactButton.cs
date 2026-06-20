using UnityEngine;

public class ZoneUIManager : MonoBehaviour
{
    public GameObject interactButton; // Kéo cái nút "B" vào đây

    private void Start()
    {
        if (interactButton != null) interactButton.SetActive(false);
    }

    // Khi player chạm vào trigger, nó sẽ gửi tín hiệu tới đây
    public void ShowButton()
    {
        if (interactButton != null) interactButton.SetActive(true);
    }

    public void HideButton()
    {
        if (interactButton != null) interactButton.SetActive(false);
    }
}