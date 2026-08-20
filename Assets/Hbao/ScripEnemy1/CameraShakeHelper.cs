using System.Collections;
using UnityEngine;

/// <summary>
/// Quản lý hiệu ứng rung Camera (Camera Shake / Earthquake / Tornado Vortex / Meteor Impact / Sword Rain) dữ dội và sống động cho người chơi.
/// Hỗ trợ cả rung toàn cục lẫn rung suy giảm theo khoảng cách (Distance Attenuation).
/// Sử dụng WaitForEndOfFrame() để áp dụng sau khi LateUpdate của các script Player hoàn tất.
/// </summary>
public class CameraShakeHelper : MonoBehaviour
{
    private static CameraShakeHelper instance;
    private Coroutine activeShakeCoroutine;
    private float currentActiveIntensity = 0f;

    public static CameraShakeHelper Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("CameraShakeManager");
                instance = obj.AddComponent<CameraShakeHelper>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }

    /// <summary>
    /// Rung camera dữ dội toàn cục (động đất, bão tố, lốc cuốn, tử thần bạo nổ)
    /// </summary>
    /// <param name="duration">Thời gian rung (giây)</param>
    /// <param name="intensity">Cường độ rung (0.6f - 1.5f rung rất mạnh và dữ dội)</param>
    public static void Shake(float duration = 0.9f, float intensity = 1.0f)
    {
        Instance.TriggerShake(duration, intensity);
    }

    /// <summary>
    /// Dừng ngay lập tức mọi hiệu ứng rung camera đang hoạt động (ví dụ khi Boss chết hoặc khi bắt đầu Cutscene)
    /// </summary>
    public static void StopShake()
    {
        if (instance != null && instance.activeShakeCoroutine != null)
        {
            instance.StopCoroutine(instance.activeShakeCoroutine);
            instance.activeShakeCoroutine = null;
            instance.currentActiveIntensity = 0f;
        }
    }

    /// <summary>
    /// Rung camera có tính toán suy giảm theo khoảng cách: người đứng gần điểm nổ nhất sẽ rung dữ dội nhất, càng xa giảm dần mượt mà.
    /// </summary>
    /// <param name="impactPos">Tọa độ điểm va chạm/nổ/tiếp đất</param>
    /// <param name="duration">Thời gian rung (giây)</param>
    /// <param name="maxIntensity">Cường độ rung tối đa khi đứng sát điểm nổ (0.9f - 1.6f)</param>
    /// <param name="maxDistance">Khoảng cách tối đa còn cảm nhận được độ rung (m)</param>
    public static void ShakeAtPosition(Vector3 impactPos, float duration = 0.6f, float maxIntensity = 1.2f, float maxDistance = 45f)
    {
        Camera cam = GetActiveCamera();
        if (cam == null) return;

        float dist = Vector3.Distance(cam.transform.position, impactPos);
        if (dist >= maxDistance) return;

        // Tính toán suy giảm êm dịu: Đứng gần rung cực mạnh chấn động, đứng xa vẫn cảm nhận được độ rung rõ rệt
        float factor = Mathf.Clamp01(1.0f - (dist / maxDistance));
        float intensity = maxIntensity * Mathf.Pow(factor, 1.2f);

        if (intensity > 0.05f)
        {
            Instance.TriggerShake(duration, intensity);
        }
    }

    public static Camera GetActiveCamera()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.isActiveAndEnabled) return cam;

        var allCams = Camera.allCameras;
        foreach (var c in allCams)
        {
            if (c != null && c.isActiveAndEnabled) return c;
        }
        return FindFirstObjectByType<Camera>();
    }

    public void TriggerShake(float duration, float intensity)
    {
        if (activeShakeCoroutine != null && intensity <= currentActiveIntensity)
        {
            // Nếu đang rung với cường độ mạnh hơn thì tiếp tục giữ nhịp rung mạnh
            return;
        }

        if (activeShakeCoroutine != null)
        {
            StopCoroutine(activeShakeCoroutine);
        }
        currentActiveIntensity = intensity;
        activeShakeCoroutine = StartCoroutine(ShakeRoutine(duration, intensity));
    }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        float elapsed = 0f;
        float seedX = Random.Range(0f, 100f);
        float seedY = Random.Range(100f, 200f);
        float seedRot = Random.Range(200f, 300f);
        float seedPitch = Random.Range(300f, 400f);

        while (elapsed < duration)
        {
            // Chờ sau khi tất cả Player LateUpdate() đã cập nhật vị trí Camera
            yield return new WaitForEndOfFrame();

            Camera cam = GetActiveCamera();
            if (cam == null) yield break;

            float t = Mathf.Clamp01(elapsed / duration);
            // Giữ độ rung uy lực ở nửa đầu và giảm dần mượt mà về cuối
            float decay = 1.0f - Mathf.Pow(t, 1.3f);
            float currentStrength = intensity * decay;

            // 1. Độ lệch vị trí (Position Shake) - Dao động mạnh mẽ trên mặt phẳng camera (TĂNG MẠNH BIÊN ĐỘ)
            float freq = 52f;
            float offsetX = (Mathf.PerlinNoise(seedX + Time.time * freq, 0f) - 0.5f) * 6.5f * currentStrength;
            float offsetY = (Mathf.PerlinNoise(0f, seedY + Time.time * freq) - 0.5f) * 5.2f * currentStrength;
            float offsetZ = (Mathf.PerlinNoise(seedX, seedY + Time.time * freq) - 0.5f) * 2.5f * currentStrength;

            Vector3 shakeVector = cam.transform.right * offsetX + cam.transform.up * offsetY + cam.transform.forward * offsetZ;
            cam.transform.position += shakeVector;

            // 2. Góc nghiêng lắc lư (Rotational Shake / Roll, Pitch & Yaw) tạo cảm giác chấn động màn hình rung giật cực mạnh
            float rotAngleZ = (Mathf.PerlinNoise(seedRot + Time.time * freq, 0f) - 0.5f) * 15.0f * currentStrength;
            float rotAngleX = (Mathf.PerlinNoise(0f, seedPitch + Time.time * freq) - 0.5f) * 11.0f * currentStrength;
            float rotAngleY = (Mathf.PerlinNoise(seedPitch + Time.time * freq, seedRot) - 0.5f) * 7.5f * currentStrength;

            cam.transform.Rotate(Vector3.forward, rotAngleZ, Space.Self);
            cam.transform.Rotate(Vector3.right, rotAngleX, Space.Self);
            cam.transform.Rotate(Vector3.up, rotAngleY, Space.Self);

            elapsed += Time.deltaTime;
        }

        currentActiveIntensity = 0f;
        activeShakeCoroutine = null;
    }
}
