using System.Collections;
using UnityEngine;

/// <summary>
/// Quản lý hiệu ứng rung Camera (Camera Shake / Earthquake) mượt mà cho toàn bộ người chơi.
/// Sử dụng WaitForEndOfFrame() để áp dụng độ lệch sau khi LateUpdate của các script Player hoàn tất,
/// đảm bảo camera không bị giật hay mất gốc quay.
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
    /// Rung camera nhẹ nhàng kiểu động đất
    /// </summary>
    /// <param name="duration">Thời gian rung (giây, mặc định 0.65s)</param>
    /// <param name="intensity">Cường độ rung (mặc định 0.16f - rung êm ái, không gây chóng mặt)</param>
    public static void Shake(float duration = 0.65f, float intensity = 0.16f)
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

        while (elapsed < duration)
        {
            // Chờ sau khi tất cả Player LateUpdate() đã cập nhật vị trí Camera
            yield return new WaitForEndOfFrame();

            Camera cam = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();
            if (cam == null) yield break;

            float t = Mathf.Clamp01(elapsed / duration);
            float currentStrength = intensity * (1.0f - t); // Giảm dần êm ái theo thời gian

            // Tạo độ lệch rung dạng sóng mượt Perlin Noise
            float offsetX = (Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f) * 2f * currentStrength;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f) * 2f * currentStrength;

            Vector3 shakeVector = cam.transform.right * offsetX + cam.transform.up * offsetY;
            cam.transform.position += shakeVector;

            elapsed += Time.deltaTime;
        }

        activeShakeCoroutine = null;
    }
}
