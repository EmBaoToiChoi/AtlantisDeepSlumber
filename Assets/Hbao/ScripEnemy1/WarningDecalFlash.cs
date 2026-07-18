using UnityEngine;
using System.Collections;

/// <summary>
/// Script tạo hiệu ứng chớp đỏ và nhuộm đỏ cho vòng tròn cảnh báo (Warning Decal) trước khi đá triệu hồi nhô lên.
/// </summary>
public class WarningDecalFlash : MonoBehaviour
{
    private Renderer[] renderers;
    private Projector[] projectors;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        projectors = GetComponentsInChildren<Projector>();
    }

    public void StartFlashing(float warningDuration)
    {
        StartCoroutine(FlashRoutine(warningDuration));
    }

    private IEnumerator FlashRoutine(float duration)
    {
        // Đặt toàn bộ vòng tròn thành màu đỏ ban đầu
        SetColor(Color.red);

        // Giai đoạn 1: Đỏ tĩnh trong 60% thời gian đầu
        float phase1 = duration * 0.6f;
        yield return new WaitForSeconds(phase1);

        // Giai đoạn 2: Chớp đỏ liên tục trong 40% thời gian cuối
        float phase2 = duration - phase1;
        float flashInterval = 0.12f; // Tốc độ chớp tắt
        float flashElapsed = 0f;
        bool visible = true;

        while (flashElapsed < phase2)
        {
            visible = !visible;
            SetVisibility(visible);
            yield return new WaitForSeconds(flashInterval);
            flashElapsed += flashInterval;
        }

        // Đảm bảo hiển thị lại trước khi bị phá hủy
        SetVisibility(true);
    }

    private void SetColor(Color col)
    {
        foreach (var r in renderers)
        {
            if (r != null && r.material != null)
            {
                if (r.material.HasProperty("_Color"))
                {
                    r.material.color = col;
                }
                else if (r.material.HasProperty("_BaseColor"))
                {
                    r.material.SetColor("_BaseColor", col);
                }
            }
        }
        foreach (var p in projectors)
        {
            if (p != null && p.material != null)
            {
                p.material.color = col;
            }
        }

        // Đồng bộ màu cho URP Decal Projector nếu dự án sử dụng URP Decal
        var urpDecalType = System.Type.GetType("UnityEngine.Rendering.Universal.DecalProjector, Unity.RenderPipelines.Universal.Runtime");
        if (urpDecalType != null)
        {
            var decalComponents = GetComponentsInChildren(urpDecalType);
            foreach (var decal in decalComponents)
            {
                var materialProp = urpDecalType.GetProperty("material");
                if (materialProp != null)
                {
                    var mat = materialProp.GetValue(decal) as Material;
                    if (mat != null)
                    {
                        if (mat.HasProperty("_Color")) mat.color = col;
                        else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                    }
                }
            }
        }
    }

    private void SetVisibility(bool visible)
    {
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = visible;
        }
        foreach (var p in projectors)
        {
            if (p != null) p.enabled = visible;
        }

        // Đồng bộ ẩn/hiện cho URP Decal Projector
        var urpDecalType = System.Type.GetType("UnityEngine.Rendering.Universal.DecalProjector, Unity.RenderPipelines.Universal.Runtime");
        if (urpDecalType != null)
        {
            var decalComponents = GetComponentsInChildren(urpDecalType);
            foreach (var decal in decalComponents)
            {
                var behaviour = decal as Behaviour;
                if (behaviour != null) behaviour.enabled = visible;
            }
        }
    }
}
