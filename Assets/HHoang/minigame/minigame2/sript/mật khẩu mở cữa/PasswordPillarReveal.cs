using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PasswordPillarReveal : NetworkBehaviour
{
    [Header("Cấu hình Trụ Mật Khẩu")]
    [Tooltip("Kéo 4 đốt trụ (từ trên xuống dưới) vào mảng này")]
    public Transform[] pillarSegments; 
    
    [Tooltip("Góc Y chính xác của từng đốt để hiện mật khẩu (VD: 0, 90, 180, 270)")]
    public float[] correctAngles = new float[4];

    [Header("Cấu hình Xoay Liên Tục")]
    [Tooltip("Tốc độ quay mặc định (độ/giây)")]
    public float spinSpeed = 120f;

    [Header("Cấu hình Hãm Phanh (Lúc giải xong)")]
    [Tooltip("Thời gian từ lúc bắt đầu hãm phanh đến khi dừng hẳn (giây)")]
    public float stopDuration = 4f; 
    
    [Tooltip("Số vòng xoay thêm trước khi dừng (để tạo cảm giác trôi từ từ)")]
    public int extraSpins = 2; 

    private bool isEndlessSpinning = true;
    // Lưu góc khởi tạo để giữ nguyên trục X, Z không bị thay đổi
    private Quaternion[] initialRotations;

    void Start()
    {
        // Lưu lại rotation ban đầu của từng đốt để đảm bảo không bị lệch trục
        initialRotations = new Quaternion[pillarSegments.Length];
        for (int i = 0; i < pillarSegments.Length; i++)
        {
            if (pillarSegments[i] != null)
                initialRotations[i] = pillarSegments[i].localRotation;
        }
    }

    void Update()
    {
        if (isEndlessSpinning)
        {
            for (int i = 0; i < pillarSegments.Length; i++)
            {
                if (pillarSegments[i] == null) continue;

                float direction = (i % 2 == 0) ? 1f : -1f;
                pillarSegments[i].Rotate(0f, direction * spinSpeed * Time.deltaTime, 0f, Space.Self);
            }
        }
    }

    [ClientRpc]
    public void TriggerRevealClientRpc()
    {
        if (isEndlessSpinning)
        {
            isEndlessSpinning = false;
            StartCoroutine(BrakeAndSnapToPasswordRoutine());
        }
    }

    private IEnumerator BrakeAndSnapToPasswordRoutine()
    {
        float[] startAngles = new float[pillarSegments.Length];
        float[] targetAngles = new float[pillarSegments.Length];

        for (int i = 0; i < pillarSegments.Length; i++)
        {
            // Lấy góc Y hiện tại dựa trên localRotation
            float currentY = pillarSegments[i].localEulerAngles.y;
            startAngles[i] = currentY;

            float direction = (i % 2 == 0) ? 1f : -1f;

            float angleDiff = (correctAngles[i] % 360f) - (currentY % 360f);

            if (direction > 0 && angleDiff < 0) angleDiff += 360f;
            if (direction < 0 && angleDiff > 0) angleDiff -= 360f;

            targetAngles[i] = currentY + angleDiff + (direction * 360f * extraSpins);
        }

        float elapsedTime = 0f;

        while (elapsedTime < stopDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / stopDuration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); 

            for (int i = 0; i < pillarSegments.Length; i++)
            {
                float offsetT = Mathf.Clamp01(smoothT - (i * 0.05f));
                float currentAngle = Mathf.Lerp(startAngles[i], targetAngles[i], offsetT);
                
                // SỬA LỖI: Dùng Quaternion.Euler để đảm bảo chỉ tác động vào trục Y
                // Nhân với initialRotations để giữ nguyên trục X và Z ban đầu
                pillarSegments[i].localRotation = initialRotations[i] * Quaternion.Euler(0, currentAngle, 0);
            }
            yield return null;
        }

        // Chốt sổ: Ép lại chính xác vào số đáp án
        for (int i = 0; i < pillarSegments.Length; i++)
        {
            pillarSegments[i].localRotation = initialRotations[i] * Quaternion.Euler(0, correctAngles[i], 0);
        }
    }
}