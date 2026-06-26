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

    // Biến kiểm soát trạng thái: true = đang quay vô tận, false = đang hãm phanh để mở
    private bool isEndlessSpinning = true; 

    void Update()
    {
        // Nếu đang ở trạng thái quay vô tận thì cứ mỗi khung hình cho nó xoay
        if (isEndlessSpinning)
        {
            for (int i = 0; i < pillarSegments.Length; i++)
            {
                if (pillarSegments[i] == null) continue;

                // Mẹo: i = 0, 2 (đốt 1 và 3) sẽ xoay chiều dương (1)
                //      i = 1, 3 (đốt 2 và 4) sẽ xoay chiều âm (-1)
                float direction = (i % 2 == 0) ? 1f : -1f;
                
                // Xoay liên tục theo chiều Y
                pillarSegments[i].Rotate(0f, direction * spinSpeed * Time.deltaTime, 0f, Space.Self);
            }
        }
    }

    // Hàm này gọi từ AscensionManager khi 4 viên ngọc đặt đúng
    [ClientRpc]
    public void TriggerRevealClientRpc()
    {
        // Tắt vòng lặp quay vô tận ở Update, chuyển quyền cho Coroutine hãm phanh
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
            // Lấy góc Y hiện tại (luôn nằm trong khoảng 0-360)
            float currentY = pillarSegments[i].localEulerAngles.y;
            startAngles[i] = currentY;

            // Xác định chiều quay hiện tại của đốt này
            float direction = (i % 2 == 0) ? 1f : -1f;

            // --- BẮT ĐẦU TOÁN HỌC TÍNH GÓC DỪNG ---
            // Tìm khoảng cách từ góc hiện tại tới đáp án đúng
            float angleDiff = (correctAngles[i] % 360f) - (currentY % 360f);

            // Ép nó phải đi tiếp theo đúng chiều quay hiện tại, không được quay ngược lại
            if (direction > 0 && angleDiff < 0) angleDiff += 360f;
            if (direction < 0 && angleDiff > 0) angleDiff -= 360f;

            // Góc đích = Góc hiện tại + Khoảng cách bù + (Số vòng xoay thêm * 360)
            targetAngles[i] = currentY + angleDiff + (direction * 360f * extraSpins);
        }

        float elapsedTime = 0f;

        while (elapsedTime < stopDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / stopDuration;

            // Hàm Cubic Ease Out: Khởi đầu giữ tốc độ cũ, sau đó phanh chậm dần rất mượt
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); 

            for (int i = 0; i < pillarSegments.Length; i++)
            {
                // Thêm độ trễ nhẹ giữa các đốt để đốt trên dừng trước, đốt dưới dừng sau nhìn cho "cơ học"
                float offsetT = Mathf.Clamp01(smoothT - (i * 0.05f));

                float currentAngle = Mathf.Lerp(startAngles[i], targetAngles[i], offsetT);
                
                Vector3 currentRot = pillarSegments[i].localEulerAngles;
                pillarSegments[i].localEulerAngles = new Vector3(currentRot.x, currentAngle, currentRot.z);
            }
            yield return null;
        }

        // Chốt sổ: Ép lại chính xác vào số đáp án để loại bỏ sai số nhỏ xíu của float
        for (int i = 0; i < pillarSegments.Length; i++)
        {
            Vector3 finalRot = pillarSegments[i].localEulerAngles;
            pillarSegments[i].localEulerAngles = new Vector3(finalRot.x, correctAngles[i], finalRot.z);
        }
    }
}