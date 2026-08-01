using System.Collections;
using UnityEngine;

namespace PLuan.Cutscenes
{
    /// <summary>
    /// Script bổ trợ các hiệu ứng Camera Điện ảnh (Cinematic Camera Effects) 
    /// như Rung camera (Shake), Thay đổi FOV (Zoom/Dolly Zoom Vertigo) mượt mà khi quay Cutscene.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CinematicCameraHelper : MonoBehaviour
    {
        private Camera cam;
        private Vector3 originalLocalPos;
        private Coroutine shakeCoroutine;
        private Coroutine fovCoroutine;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            originalLocalPos = transform.localPosition;
        }

        /// <summary>
        /// Rung Camera (gọi qua Timeline Signal hoặc Event khi có tiếng nổ/chém)
        /// </summary>
        /// <param name="duration">Thời gian rung (giây)</param>
        /// <param name="intensity">Độ mạnh của cú rung</param>
        public void DoCameraShake(float duration = 0.5f, float intensity = 0.3f)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeRoutine(duration, intensity));
        }

        /// <summary>
        /// Thay đổi góc nhìn FOV mượt mà (Tạo hiệu ứng Zoom cận cảnh hoặc Dolly Zoom)
        /// </summary>
        /// <param name="targetFOV">Góc FOV đích (ví dụ: 30 cho Close-up, 60 cho Wide)</param>
        /// <param name="duration">Thời gian chuyển FOV</param>
        public void SmoothChangeFOV(float targetFOV, float duration = 1.5f)
        {
            if (fovCoroutine != null) StopCoroutine(fovCoroutine);
            fovCoroutine = StartCoroutine(FOVRoutine(targetFOV, duration));
        }

        private IEnumerator ShakeRoutine(float duration, float intensity)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                Vector3 randomPoint = originalLocalPos + Random.insideUnitSphere * intensity;
                transform.localPosition = randomPoint;
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.localPosition = originalLocalPos;
        }

        private IEnumerator FOVRoutine(float targetFOV, float duration)
        {
            float startFOV = cam.fieldOfView;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                cam.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
                yield return null;
            }

            cam.fieldOfView = targetFOV;
        }
    }
}
