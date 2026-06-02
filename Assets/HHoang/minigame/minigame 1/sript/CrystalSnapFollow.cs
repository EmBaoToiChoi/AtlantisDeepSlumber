using UnityEngine;
using Unity.Netcode;

public class CrystalSnapFollow : NetworkBehaviour
{
    private CrystalCore core;
    public Transform targetSnapPoint; 

    void Awake() => core = GetComponent<CrystalCore>();

    void LateUpdate()
    {
        // 1. Kiểm tra null core để tránh lỗi
        if (core == null) return;

        // 2. Chỉ chạy logic hút khi core đã được khóa (isSnapped) 
        // 3. Kiểm tra targetSnapPoint tồn tại để tránh NullReferenceException
        if (core.isSnapped.Value && targetSnapPoint != null)
        {
            // Chỉ cập nhật nếu vị trí hiện tại khác với vị trí đích (tối ưu hóa nhỏ)
            if (transform.position != targetSnapPoint.position || transform.rotation != targetSnapPoint.rotation)
            {
                transform.SetPositionAndRotation(targetSnapPoint.position, targetSnapPoint.rotation);
            }
        }
    }
}