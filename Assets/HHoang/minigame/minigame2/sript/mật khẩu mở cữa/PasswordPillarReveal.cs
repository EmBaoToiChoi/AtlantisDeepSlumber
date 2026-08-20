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
    [Tooltip("Kéo các Particle System bốc khói tương ứng với từng đốt trụ vào đây")]
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

            // Đảm bảo các object ẩn đi và đặt Alpha = 0 lúc bắt đầu
            if (i < secretObjects.Length && secretObjects[i] != null)
            {
                SetObjectAlpha(secretObjects[i], 0f);
                secretObjects[i].SetActive(false);
            }
        }

        // Đảm bảo khói tắt lúc bắt đầu game
        if (smokeEffects != null)
        {
            foreach (var ps in smokeEffects)
            {
                if (ps != null) ps.Stop();
            }
        }
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

                if (smokeEffects != null && i < smokeEffects.Length && smokeEffects[i] != null)
                {
                    if (!smokeEffects[i].isPlaying)
                    {
                        smokeEffects[i].Play();
                    }
                }
            }
        }
        // TH 2: Khi isSolved được Server đổi thành true (Giải xong) -> Tắt khói ngay và hãm phanh + hiện phần thưởng
        else if (!hasStartedStopping)
        {
            hasStartedStopping = true;

            // Tắt TẤT CẢ khói ngay lập tức khi bắt đầu chậm lại
            if (smokeEffects != null)
            {
                foreach (var ps in smokeEffects)
                {
                    if (ps != null && ps.isPlaying)
                    {
                        ps.Stop();
                    }
                }
            }

            // Bắt đầu hiệu ứng hãm phanh làm chậm trụ VÀ hiện phần thưởng dần ra
            StartCoroutine(BrakeAndSnapToPasswordRoutine());
        }
    }

    private IEnumerator BrakeAndSnapToPasswordRoutine()
    {
        float[] startAngles = new float[pillarSegments.Length];
        float[] targetAngles = new float[pillarSegments.Length];

        // 1. KÍCH HOẠT VÀ ĐẶT ALPHA BAN ĐẦU CHO PHẦN THƯỞNG = 0 (TRONG SUỐT)
        for (int i = 0; i < secretObjects.Length; i++)
        {
            if (secretObjects[i] != null)
            {
                SetObjectAlpha(secretObjects[i], 0f);
                secretObjects[i].SetActive(true); // Bật Object lên ngay lập tức!
            }
        }

        // Tính toán góc đích hoàn hảo để không bị giật
        for (int i = 0; i < pillarSegments.Length; i++)
        {
            if (pillarSegments[i] == null) continue;
            
            // Lấy góc Y hiện tại
            startAngles[i] = pillarSegments[i].localEulerAngles.y;

            float direction = (i % 2 == 0) ? 1f : -1f;
            
            // Dùng Mathf.DeltaAngle để tìm khoảng cách quay ngắn nhất (-180 đến 180)
            float delta = Mathf.DeltaAngle(startAngles[i], correctAngles[i]);

            // Bắt buộc trụ phải quay tiếp theo đúng hướng (không được quay ngược lại)
            if (direction > 0 && delta < 0) delta += 360f;
            if (direction < 0 && delta > 0) delta -= 360f;

            // Cộng thêm số vòng quay biểu diễn (extraSpins)
            delta += direction * 360f * extraSpins;

            // Góc đích cuối cùng = Góc hiện tại + tổng quãng đường góc phải đi
            targetAngles[i] = startAngles[i] + delta;
        }

        float elapsedTime = 0f;

        // Quá trình hãm phanh mượt mà VÀ Fade-in phần thưởng
        while (elapsedTime < stopDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / stopDuration;
            
            // Sử dụng Ease-Out Cubic để tạo cảm giác hãm phanh tự nhiên
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); 

            // A. XOAY TRỤ
            for (int i = 0; i < pillarSegments.Length; i++)
            {
                if (pillarSegments[i] == null) continue;
                
                // Độ trễ nhẹ giữa các đốt trụ (offsetT)
                float offsetT = Mathf.Clamp01(smoothT - (i * 0.05f));
                float currentY = Mathf.Lerp(startAngles[i], targetAngles[i], offsetT);
                
                // Chỉ can thiệp vào góc Y, giữ nguyên X và Z để không lệch trục
                Vector3 currentEuler = pillarSegments[i].localEulerAngles;
                currentEuler.y = currentY;
                pillarSegments[i].localEulerAngles = currentEuler;
            }

            // B. LÀM HIỆN DẦN PHẦN THƯỞNG (FADE IN)
            for (int i = 0; i < secretObjects.Length; i++)
            {
                if (secretObjects[i] != null)
                {
                    SetObjectAlpha(secretObjects[i], t); // t đi từ 0 đến 1
                }
            }

            yield return null;
        }

        // Chốt sổ frame cuối cùng: Đảm bảo dừng chính xác ở góc đã setting (correctAngles)
        for (int i = 0; i < pillarSegments.Length; i++)
        {
            if (pillarSegments[i] == null) continue;
            
            // Ép góc Y về chính xác correctAngle, giữ nguyên X và Z
            Vector3 finalEuler = pillarSegments[i].localEulerAngles;
            finalEuler.y = correctAngles[i];
            pillarSegments[i].localEulerAngles = finalEuler;
        }

        for (int i = 0; i < secretObjects.Length; i++)
        {
            if (secretObjects[i] != null)
            {
                SetObjectAlpha(secretObjects[i], 1f);
                secretObjects[i].SetActive(true);
            }
        }
    }

    /// <summary>
    /// Hàm phụ trợ để thay đổi độ trong suốt (Alpha) của toàn bộ Renderers trên Object
    /// </summary>
    private void SetObjectAlpha(GameObject obj, float alpha)
    {
        if (obj == null) return;

        // Tìm tất cả các MeshRenderer/SkinnedMeshRenderer nằm trên object và con của nó
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            foreach (Material mat in rend.materials)
            {
                // Kiểm tra nếu Material có thuộc tính Color
                if (mat.HasProperty("_Color"))
                {
                    Color color = mat.color;
                    color.a = alpha;
                    mat.color = color;
                }
            }
        }
    }
}