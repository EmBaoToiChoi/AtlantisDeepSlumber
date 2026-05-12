using UnityEngine;
using UnityEngine.UIElements; // Bắt buộc có namespace này

public class PlayerHUD : MonoBehaviour
{
    private VisualElement root;
    private ProgressBar hpBar;
    private ProgressBar mpBar;
    private ProgressBar expBar;
    private VisualElement avatar;

    void OnEnable()
    {
        // 1. Lấy Root của UI Document
        root = GetComponent<UIDocument>().rootVisualElement;

        // 2. Tìm các Element theo tên đã đặt trong UI Builder (Q = Query)
        hpBar = root.Q<ProgressBar>("HPBar");
        mpBar = root.Q<ProgressBar>("MPBar");
        expBar = root.Q<ProgressBar>("ExpBar");
        avatar = root.Q<VisualElement>("Avatar");

        // Thử nghiệm thay đổi giá trị
        UpdateHP(80, 100);
    }

    public void UpdateHP(float current, float max)
    {
        hpBar.value = (current / max) * 100f; // ProgressBar dùng đơn vị %
        hpBar.title = $"{current}/{max}"; // Hiển thị số lên thanh
    }

    public void UpdateAvatar(Texture2D image)
    {
        avatar.style.backgroundImage = new StyleBackground(image);
    }
}