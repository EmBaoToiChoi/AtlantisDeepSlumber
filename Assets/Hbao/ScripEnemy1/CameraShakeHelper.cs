using System.Collections;
using UnityEngine;

/// <summary>
/// Quản lý hiệu ứng rung Camera (Camera Shake / Earthquake / Tornado Vortex) dữ dội và sống động cho người chơi.
/// Kết hợp cả độ lệch vị trí (Position Offset) lẫn góc nghiêng lắc (Rotational Roll/Pitch Shake).
/// Sử dụng WaitForEndOfFrame() để áp dụng sau khi LateUpdate của các script Player hoàn tất.
/// </summary>
public class CameraShakeHelper : MonoBehaviour
{
    private static CameraShakeHelper instance;
    private Coroutine activeShakeCoroutine;

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
    /// Rung camera dữ dội (động đất, bão tố, lốc cuốn, va chạm mạnh)
    /// </summary>
    /// <param name="duration">Thời gian rung (giây)</param>
    /// <param name="intensity">Cường độ rung (0.35f - 0.7f rung rất rõ và mạnh mẽ)</param>
    public static void Shake(float duration = 0.8f, float intensity = 0.55f)
    {
        Instance.TriggerShake(duration, intensity);
    }

    public void TriggerShake(float duration, float intensity)
    {
        if (activeShakeCoroutine != null)
        {
            StopCoroutine(activeShakeCoroutine);
        }
        activeShakeCoroutine = StartCoroutine(ShakeRoutine(duration, intensity));
    }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        float elapsed = 0f;
        float seedX = Random.Range(0f, 100f);
        float seedY = Random.Range(100f, 200f);
        float seedRot = Random.Range(200f, 300f);

        while (elapsed < duration)
        {
            // Chờ sau khi tất cả Player LateUpdate() đã cập nhật vị trí Camera
            yield return new WaitForEndOfFrame();

            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) yield break;

            float t = Mathf.Clamp01(elapsed / duration);
            // Giữ độ rung mạnh mẽ ở nửa đầu và giảm dần về cuối
            float decay = 1.0f - Mathf.Pow(t, 1.5f);
            float currentStrength = intensity * decay;

            // 1. Độ lệch vị trí (Position Shake) - Dao động mạnh mẽ trên mặt phẳng camera
            float freq = 45f;
            float offsetX = (Mathf.PerlinNoise(seedX + Time.time * freq, 0f) - 0.5f) * 2.5f * currentStrength;
            float offsetY = (Mathf.PerlinNoise(0f, seedY + Time.time * freq) - 0.5f) * 2.5f * currentStrength;

            Vector3 shakeVector = cam.transform.right * offsetX + cam.transform.up * offsetY;
            cam.transform.position += shakeVector;

            // 2. Góc nghiêng lắc lư (Rotational Shake / Roll & Pitch) tạo cảm giác chấn động cực mạnh
            float rotAngleZ = (Mathf.PerlinNoise(seedRot + Time.time * freq, 0f) - 0.5f) * 5.0f * currentStrength;
            float rotAngleX = (Mathf.PerlinNoise(0f, seedRot + Time.time * freq) - 0.5f) * 3.5f * currentStrength;

            cam.transform.Rotate(Vector3.forward, rotAngleZ, Space.Self);
            cam.transform.Rotate(Vector3.right, rotAngleX, Space.Self);

            elapsed += Time.deltaTime;
        }

        activeShakeCoroutine = null;
    }
}
