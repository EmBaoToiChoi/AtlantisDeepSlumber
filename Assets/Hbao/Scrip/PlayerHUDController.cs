using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    // Các tham chiếu đến VisualElement lõi của thanh bar
    private VisualElement hpFill;
    private VisualElement mpFill;
    private VisualElement expFill;

    void OnEnable()
    {
        if (uiDocument == null) return;

        // Lấy gốc (root) của giao diện
        var root = uiDocument.rootVisualElement;

        // Truy xuất các element dựa vào thuộc tính 'name' đã đặt trong UXML
        hpFill = root.Q<VisualElement>("hp-fill");
        mpFill = root.Q<VisualElement>("mp-fill");
        expFill = root.Q<VisualElement>("exp-fill");

        // Test thử đặt giá trị ban đầu
        SetHealth(0.8f); // 80% máu
        SetMana(0.5f);   // 50% mana
        SetExp(0.25f);   // 25% exp
    }

    // Hàm cập nhật độ rộng thanh Máu (giá trị từ 0.0 đến 1.0)
    public void SetHealth(float percentage)
    {
        if (hpFill != null)
        {
            // Thay đổi thuộc tính width theo %
            hpFill.style.width = new Length(percentage * 100, LengthUnit.Percent);
        }
    }

    public void SetMana(float percentage)
    {
        if (mpFill != null)
        {
            mpFill.style.width = new Length(percentage * 100, LengthUnit.Percent);
        }
    }

    public void SetExp(float percentage)
    {
        if (expFill != null)
        {
            expFill.style.width = new Length(percentage * 100, LengthUnit.Percent);
        }
    }
}