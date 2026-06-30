using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class BalanceMeterUIToolkit : NetworkBehaviour
{
    public BalanceManager balanceManager;
    
    [Tooltip("Kéo thả thành phần UIDocument vào đây")]
    public UIDocument uiDocument;

    // Các thành phần UI bên trong UI Toolkit
    private VisualElement fillElement;
    private Label angleLabel;

    void OnEnable()
    {
        // Lấy UIDocument từ GameObject nếu chưa gán
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        // Lấy tham chiếu đến các element trong giao diện bằng tên ID của chúng
        if (uiDocument != null)
        {
            var root = uiDocument.rootVisualElement;
            
            // Lưu ý: Đảm bảo trong file UXML của bạn, thanh máu (fill) có tên (Name) là "FillElement"
            // và phần hiển thị chữ có tên là "AngleText"
            fillElement = root.Q<VisualElement>("FillElement");
            angleLabel = root.Q<Label>("AngleText");
        }
    }

    void Update()
    {
        /*
        // Tránh chạy tiếp nếu thiếu thành phần hoặc UI chưa sẵn sàng
        if (balanceManager == null || fillElement == null || angleLabel == null)
            return;

        float angle = balanceManager.CurrentAngle;

        // Cập nhật text
        angleLabel.text = angle.ToString("F1") + "°";

        // Tính toán phần trăm (từ 0 -> 1)
        float percent = Mathf.Clamp01(angle / 20f);

        // Với UI Toolkit, thay vì dùng localScale, cách chuẩn nhất là thay đổi width (độ rộng) theo %
        fillElement.style.width = new StyleLength(new Length(percent * 100f, LengthUnit.Percent));

        // Cập nhật màu sắc (đổi màu Background của element)
        if (angle < 10)
        {
            fillElement.style.backgroundColor = new StyleColor(Color.green);
        }
        else if (angle < 15)
        {
            fillElement.style.backgroundColor = new StyleColor(Color.yellow);
        }
        else
        {
            fillElement.style.backgroundColor = new StyleColor(Color.red);
        }
        */
    }
}
