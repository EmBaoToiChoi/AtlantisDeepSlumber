using UnityEngine;
using Unity.Netcode; // Đảm bảo có thư viện Netcode

public class GearRotator : NetworkBehaviour 
{
    [Header("Cấu hình quay")]
    [Tooltip("Tốc độ quay của bánh răng (độ/giây)")]
    public float rotationSpeed = 50f;

    [Tooltip("Tích chọn nếu muốn quay ngược chiều kim đồng hồ")]
    public bool reverseDirection = false;

    // Biến mạng đồng bộ tốc độ thực tế (Server quản lý)
    // Mặc định bằng 0, khi game chạy Server sẽ cấp phát tốc độ cho Client
    private NetworkVariable<float> currentSpeed = new NetworkVariable<float>(0f, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // Khi object mạng khởi tạo, Server sẽ gán tốc độ dựa trên cấu hình Inspector
        if (IsServer)
        {
            float direction = reverseDirection ? -1f : 1f;
            currentSpeed.Value = rotationSpeed * direction;
        }
    }

    void Update()
    {
        // Sử dụng phương pháp xoay bằng Rotate + Space.Self để không bao giờ bị lỗi trục X = -90
        // Cả Server và Client đều tự xoay dựa trên vận tốc đồng bộ từ Server
        if (currentSpeed.Value != 0)
        {
            transform.Rotate(Vector3.up * currentSpeed.Value * Time.deltaTime, Space.Self);
        }
    }
    // COPY ĐOẠN NÀY DÁN VÀO CUỐI FILE GEARROTATOR.CS
    public void UpdateSpeedFromServer(float newSpeed)
    {
        if (IsServer)
        {
            // Nếu muốn giữ đúng hướng quay ban đầu (thuận/ngược chiều kim đồng hồ)
            float direction = reverseDirection ? -1f : 1f;
            currentSpeed.Value = newSpeed == 0f ? 0f : newSpeed * direction;
        }
    }
}