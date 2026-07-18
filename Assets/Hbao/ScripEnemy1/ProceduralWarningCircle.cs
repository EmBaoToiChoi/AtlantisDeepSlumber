using UnityEngine;
using System.Collections;

/// <summary>
/// Vòng tròn cảnh báo dạng nét vẽ LineRenderer lập trình (Procedural).
/// Đảm bảo luôn hiển thị đỏ rực rỡ và chớp đỏ trong mọi Render Pipeline (Built-in, URP, HDRP) mà không phụ thuộc vào Shader Decal.
/// </summary>
public class ProceduralWarningCircle : MonoBehaviour
{
    private LineRenderer line;
    private Color baseColor = Color.red;
    private float radius = 3.0f;
    private float duration = 1.5f;

    private void Awake()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = 51; // 50 cạnh + 1 điểm nối khép kín vòng tròn
        line.useWorldSpace = false; // Vẽ trong không gian local của GameObject cảnh báo
        line.startWidth = 0.15f;
        line.endWidth = 0.15f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        
        // Tìm Shader mặc định của Sprites hoặc UI để luôn hiển thị Unlit (không bị ảnh hưởng bởi bóng tối)
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        
        line.material = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        line.startColor = baseColor;
        line.endColor = baseColor;
    }

    public void StartWarning(float warningDuration, float targetRadius)
    {
        duration = warningDuration;
        radius = targetRadius;

        // Vẽ tọa độ các điểm trên vòng tròn phẳng XZ
        Vector3[] points = new Vector3[51];
        for (int i = 0; i <= 50; i++)
        {
            float angle = i * (2f * Mathf.PI / 50f);
            points[i] = new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius);
        }
        line.SetPositions(points);

        StartCoroutine(AnimateWarning());
    }

    private IEnumerator AnimateWarning()
    {
        float elapsed = 0f;
        float phase1 = duration * 0.6f;

        // Giai đoạn 1: Mạch đập nhẹ (alpha chạy 0.4 -> 1.0)
        while (elapsed < phase1)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.4f, 1.0f, Mathf.PingPong(elapsed * 4f, 1f));
            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            line.startColor = c;
            line.endColor = c;
            yield return null;
        }

        // Giai đoạn 2: Chớp đỏ khẩn cấp liên tục ở 40% thời gian cuối
        float phase2 = duration - phase1;
        float flashInterval = 0.1f;
        float flashElapsed = 0f;
        bool visible = true;

        while (flashElapsed < phase2)
        {
            visible = !visible;
            line.enabled = visible;
            yield return new WaitForSeconds(flashInterval);
            flashElapsed += flashInterval;
        }

        line.enabled = visible;
    }
}
