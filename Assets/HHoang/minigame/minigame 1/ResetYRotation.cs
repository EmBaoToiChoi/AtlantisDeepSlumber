using UnityEngine;
using System.Collections.Generic;

public class GearRotation : MonoBehaviour
{
    [Header("Liên kết với DANH SÁCH Nút Bấm")]
    [Tooltip("Kéo cả 2 tấm ván/nút bấm vào danh sách này trong Inspector.")]
    public List<PressurePlate> pressurePlates = new List<PressurePlate>(); 

    [Header("Cấu hình Bánh Răng")]
    public float rotationSpeed = 50f;
    public bool clockwise = true;

    void Update()
    {
        float direction = clockwise ? 1f : -1f;
        float currentSpeed = rotationSpeed;

        // Tính tốc độ bánh răng dựa trên TRUNG BÌNH CỘNG của cả 2 nút
        if (pressurePlates != null && pressurePlates.Count > 0)
        {
            float totalRatio = 0f;
            int validPlatesCount = 0;

            foreach (PressurePlate plate in pressurePlates)
            {
                if (plate != null)
                {
                    // Lấy tỷ lệ tốc độ của từng nút (từ 0 đến 1)
                    float ratio = plate.currentGearSpeed / plate.maxGearSpeed;
                    totalRatio += ratio;
                    validPlatesCount++;
                }
            }

            if (validPlatesCount > 0)
            {
                // Chia trung bình: Nếu 1 nút dẫm (0) + 1 nút thả (1) = 1 -> 1 / 2 = 0.5 (quay chậm một nửa)
                // Nếu cả 2 nút dẫm: (0 + 0) / 2 = 0 (dừng hẳn)
                float averageRatio = totalRatio / validPlatesCount;
                currentSpeed = rotationSpeed * averageRatio;
            }
        }

        float rotationAmount = currentSpeed * direction * Time.deltaTime;
        transform.Rotate(0f, rotationAmount, 0f);
    }

    // Hàm phụ trợ để bên GearPush gọi ké lấy tốc độ chính xác
    public float GetAverageSpeedRatio()
    {
        if (pressurePlates != null && pressurePlates.Count > 0)
        {
            float totalRatio = 0f;
            int validPlatesCount = 0;

            foreach (PressurePlate plate in pressurePlates)
            {
                if (plate != null)
                {
                    totalRatio += (plate.currentGearSpeed / plate.maxGearSpeed);
                    validPlatesCount++;
                }
            }
            return validPlatesCount > 0 ? (totalRatio / validPlatesCount) : 1f;
        }
        return 1f;
    }
}