using UnityEngine;
using System.Collections;

public class SimpleCameraShake : MonoBehaviour
{
    public float shakeDuration = 3f;
    public float shakeMagnitude = 0.1f;

    void OnEnable()
    {
        // Gọi Coroutine khi Camera được Timeline bật lên
        StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        float elapsed = 0.0f;
        // Lưu lại vị trí ban đầu của Camera
        Vector3 originalPos = transform.localPosition;

        while (elapsed < shakeDuration)
        {
            // TÍNH TOÁN LẠI CHỖ NÀY: Cộng dồn độ rung vào vị trí gốc
            float x = originalPos.x + Random.Range(-1f, 1f) * shakeMagnitude;
            float y = originalPos.y + Random.Range(-1f, 1f) * shakeMagnitude;

            // Cập nhật vị trí mới
            transform.localPosition = new Vector3(x, y, originalPos.z);
            elapsed += Time.deltaTime;

            yield return null;
        }

        // Trả Camera về đúng vị trí cũ sau khi rung xong
        transform.localPosition = originalPos;
    }
}