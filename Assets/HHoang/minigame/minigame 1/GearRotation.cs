using UnityEngine;

public class GearRotation : MonoBehaviour
{
    [Header("Cấu hình Bánh Răng")]
    public float rotationSpeed = 50f;
    public bool clockwise = true;

    private float currentSpeedRatio = 1f; // Mặc định vào game quay bình thường (100% tốc độ)
    
    // Biến đếm số lượng người chơi đang thực hiện QTE thành công ngoài Map (0, 1 hoặc 2)
    private int activeHoldersCount = 0; 

    void Update()
    {
        float direction = clockwise ? 1f : -1f;
        float currentSpeed = rotationSpeed * currentSpeedRatio;

        float rotationAmount = currentSpeed * direction * Time.deltaTime;
        transform.Rotate(0f, rotationAmount, 0f);
    }

    // Hàm nhận thông báo từ các QTEController gửi lên khi trạng thái gõ nhịp thay đổi
    public void ThayDoiSoNguoiGiu(bool dangGiuThanhCong)
    {
        if (dangGiuThanhCong)
        {
            // Tăng số người giữ nhưng giới hạn tối đa là 2
            activeHoldersCount = Mathf.Min(2, activeHoldersCount + 1);
        }
        else
        {
            // Giảm số người giữ nhưng không được tụt xuống dưới 0
            activeHoldersCount = Mathf.Max(0, activeHoldersCount - 1);
        }

        // TỰ ĐỘNG CẬP NHẬT TỶ LỆ TỐC ĐỘ DỰA TRÊN SỐ NGƯỜI ĐANG NHẤN CHUẨN
        if (activeHoldersCount >= 2)
        {
            currentSpeedRatio = 0f; // CẢ 2 NGƯỜI CÙNG NHẤN CHUẨN -> BÁNH RĂNG NGỪNG QUAY
        }
        else if (activeHoldersCount == 1)
        {
            currentSpeedRatio = 0.5f; // Chỉ 1 người nhấn -> Bánh răng quay chậm 50%
        }
        else
        {
            currentSpeedRatio = 1f; // Không ai nhấn nút/Gõ trượt hết -> Bánh răng quay max tốc độ
        }

        Debug.Log($"[Bánh Răng] Số người đang giữ nút thành công: {activeHoldersCount} | Tỷ lệ tốc độ: {currentSpeedRatio * 100}%");
    }

    // Giữ nguyên hàm này để GearPush lấy dữ liệu tính lực đẩy người chơi
    public float GetAverageSpeedRatio()
    {
        return currentSpeedRatio;
    }

    // Hàm cũ (giữ lại phòng hờ nếu các tính năng khác trong game cần gọi ép tốc độ trực tiếp)
    public void SetSpeedRatio(float ratio)
    {
        currentSpeedRatio = Mathf.Clamp01(ratio);
    }
}