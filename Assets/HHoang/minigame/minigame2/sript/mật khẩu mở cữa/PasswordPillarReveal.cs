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

    [Header("Cấu hình Phần Thưởng")]
    [Tooltip("Kéo 4 Object muốn hiện lên khi giải đúng vào đây")]
    public GameObject[] secretObjects; 

    [Header("Cấu hình Xoay Liên Tục")]
    [Tooltip("Tốc độ quay mặc định (độ/giây)")]
    public float spinSpeed = 120f;

    // ===================================================================================
    // Cấu hình VFX Bốc Khói
    // ===================================================================================
    [Header("Cấu hình VFX")]
    [Tooltip("Kéo các Particle System bốc khói tương ứng với từng đốt trụ vào đây (nên cùng kích thước mảng pillarSegments)")]
    public ParticleSystem[] smokeEffects; 
    // ===================================================================================

    [Header("Cấu hình Hãm Phanh (Lúc giải xong)")]
    [Tooltip("Thời gian từ lúc bắt đầu hãm phanh đến khi dừng hẳn (giây)")]
    public float stopDuration = 4f; 
    
    [Tooltip("Số vòng xoay thêm trước khi dừng (để tạo cảm giác trôi từ từ)")]
    public int extraSpins = 2; 

    // Sử dụng NetworkVariable để đồng bộ trạng thái giải đố chính xác giữa Server và các Client
    public NetworkVariable<bool> isSolved = new NetworkVariable<bool>(false);
    private bool hasStartedStopping = false;
    private Quaternion[] initialRotations;

    void Start()
    {
        // Lưu lại rotation ban đầu của từng đốt để đảm bảo không bị lệch trục
        initialRotations = new Quaternion[pillarSegments.Length];
        for (int i = 0; i < pillarSegments.Length; i++)
        {
            if (pillarSegments[i] != null)
                initialRotations[i] = pillarSegments[i].localRotation;

            // Đảm bảo các object ẩn đi lúc bắt đầu
            if (i < secretObjects.Length && secretObjects[i] != null)
                secretObjects[i].SetActive(false);
        }

        // ===================================================================================
        // Đảm bảo khói tắt lúc bắt đầu game
        // ===================================================================================
        if (smokeEffects != null)
        {
            foreach (var ps in smokeEffects)
            {
                if (ps != null) ps.Stop(); // Dùng Stop() để hạt sinh ra trước đó tự biến mất tự nhiên
            }
        }
        // ===================================================================================
    }

    void Update()
    {
        // TH 1: Nếu Server chưa xác nhận giải xong -> Trụ xoay liên tục, Khói bốc liên tục
        if (!isSolved.Value)
        {
            for (int i = 0; i < pillarSegments.Length; i++)
            {
                if (pillarSegments[i] == null) continue;

                float direction = (i % 2 == 0) ? 1f : -1f;
                pillarSegments[i].Rotate(0f, direction * spinSpeed * Time.deltaTime, 0f, Space.Self);

                // ===================================================================================
                // Bật khói khi đang xoay tốc độ cao (Lo vụ khói "mãi mãi" lúc đang xoay)
                // ===================================================================================
                // Kiểm tra xem có VFX cho đốt này không
                if (smokeEffects != null && i < smokeEffects.Length && smokeEffects[i] != null)
                {
                    // Chỉ gọi Play() nếu nó ĐANG KHÔNG chạy, để tránh resta liên tục mỗi frame
                    if (!smokeEffects[i].isPlaying)
                    {
                        smokeEffects[i].Play();
                    }
                }
                // ===================================================================================
            }
        }
        // TH 2: Khi isSolved được Server đổi thành true (Giải xong) -> Tắt khói ngay và hãm phanh
        else if (!hasStartedStopping)
        {
            hasStartedStopping = true; // Đảm bảo khối lệnh này chỉ chạy 1 lần duy nhất trên mỗi máy

            // ===================================================================================
            // Tắt TẤT CẢ khói ngay lập tức khi bắt đầu chậm lại
            // ===================================================================================
            if (smokeEffects != null)
            {
                foreach (var ps in smokeEffects)
                {
                    if (ps != null && ps.isPlaying)
                    {
                        ps.Stop(); // Ngừng tạo hạt mới ngay lập tức. Hạt cũ sẽ trôi nốt rồi mất tự nhiên.
                    }
                }
            }
            // ===================================================================================

            // Bắt đầu hiệu ứng hãm phanh làm chậm trụ
            StartCoroutine(BrakeAndSnapToPasswordRoutine());
        }
    }

    private IEnumerator BrakeAndSnapToPasswordRoutine()
    {
        float[] startAngles = new float[pillarSegments.Length];
        float[] targetAngles = new float[pillarSegments.Length];

        // Tính toán góc đích (bao gồm số vòng quay thêm extraSpins)
        for (int i = 0; i < pillarSegments.Length; i++)
        {
            if (pillarSegments[i] == null) continue;
            float currentY = pillarSegments[i].localEulerAngles.y;
            startAngles[i] = currentY;

            float direction = (i % 2 == 0) ? 1f : -1f;
            float angleDiff = (correctAngles[i] % 360f) - (currentY % 360f);

            // Đảm bảo xoay đúng chiều gốc của đốt đó
            if (direction > 0 && angleDiff < 0) angleDiff += 360f;
            if (direction < 0 && angleDiff > 0) angleDiff -= 360f;

            targetAngles[i] = currentY + angleDiff + (direction * 360f * extraSpins);
        }

        float elapsedTime = 0f;

        // Quá trình hãm phanh mượt mà
        while (elapsedTime < stopDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / stopDuration;
            // Sử dụng Ease-Out Cubic để tạo cảm giác hãm phanh tự nhiên
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); 

            for (int i = 0; i < pillarSegments.Length; i++)
            {
                if (pillarSegments[i] == null) continue;
                // Tạo độ trễ nhẹ giữa các đốt để đẹp hơn (offsetY * 0.05f)
                float offsetT = Mathf.Clamp01(smoothT - (i * 0.05f));
                float currentAngle = Mathf.Lerp(startAngles[i], targetAngles[i], offsetT);
                
                // Áp dụng rotation, nhân với initialRotations để giữ trục chuẩn
                pillarSegments[i].localRotation = initialRotations[i] * Quaternion.Euler(0, currentAngle, 0);
            }
            yield return null;
        }

        // Chốt sổ frame cuối cùng: Ép lại chính xác vào góc đáp án trên toàn bộ các máy Client/Server
        for (int i = 0; i < pillarSegments.Length; i++)
        {
            if (pillarSegments[i] == null) continue;
            pillarSegments[i].localRotation = initialRotations[i] * Quaternion.Euler(0, correctAngles[i], 0);
            
            // Bật object phần thưởng tương ứng
            if (i < secretObjects.Length && secretObjects[i] != null)
            {
                secretObjects[i].SetActive(true);
            }
        }
    }
}