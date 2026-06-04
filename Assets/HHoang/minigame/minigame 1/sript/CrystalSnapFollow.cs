using UnityEngine;
using Unity.Netcode;

public class CrystalSnapFollow : NetworkBehaviour
{
    private CrystalCore core;
    public Transform targetSnapPoint; 

    void Awake() => core = GetComponent<CrystalCore>();

    void FixedUpdate() // Đổi thành FixedUpdate để mượt hơn với Rigidbody
    {
        if (core == null) return;

        // Nếu đã khóa (Snapped), vật thể bay từ từ vào trạm thay vì dịch chuyển tức thời
        if (core.isSnapped.Value && targetSnapPoint != null)
        {
            if (IsServer)
            {
                float snapSpeed = 5f; // Tốc độ bay vào bệ (Có thể chỉnh to lên nếu muốn bay nhanh)
                
                Vector3 smoothPos = Vector3.Lerp(transform.position, targetSnapPoint.position, snapSpeed * Time.fixedDeltaTime);
                Quaternion smoothRot = Quaternion.Lerp(transform.rotation, targetSnapPoint.rotation, snapSpeed * Time.fixedDeltaTime);
                
                // Tối ưu vật lý tránh bị giật lag
                var rb = core.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.MovePosition(smoothPos);
                    rb.MoveRotation(smoothRot);
                }
                else 
                {
                    transform.position = smoothPos;
                    transform.rotation = smoothRot;
                }
            }
        }
    }
}