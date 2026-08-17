using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hệ thống quản lý hiệu ứng nhận sát thương cho quái vật (nháy đỏ, hiển thị số sát thương nổi).
/// </summary>
public static class EnemyDamageEffectHelper
{
    private static AudioClip defaultHitClip;

    public static void PlayDamageEffects(GameObject enemy, float damage, AudioClip customHitClip = null)
    {
        if (enemy == null) return;

        // 1. Nhấp nháy màu đỏ
        var flash = enemy.GetComponent<MaterialFlashBehaviour>();
        if (flash == null) flash = enemy.AddComponent<MaterialFlashBehaviour>();
        flash.Flash(Color.red, 0.15f);

        // 2. Tạo sát thương nổi (Floating Damage Text)
        DamageTextSpawner.Spawn(enemy.transform.position, damage, Color.red);

        // 3. Phát âm thanh khi ăn hit (3D Spatial Audio cho Player ở gần nghe thấy)
        PlayHitSound(enemy.transform.position, customHitClip);
    }

    public static void PlayHitSound(Vector3 position, AudioClip customHitClip = null)
    {
        AudioClip clipToPlay = customHitClip;
        if (clipToPlay == null)
        {
            if (defaultHitClip == null)
            {
                defaultHitClip = Resources.Load<AudioClip>("Audio/ChemHit");
                if (defaultHitClip == null) defaultHitClip = Resources.Load<AudioClip>("Audio/Punch");
            }
            clipToPlay = defaultHitClip;
        }

        if (clipToPlay != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(clipToPlay, position, 0.85f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clipToPlay, position, 0.85f);
            }
        }
    }
}

/// <summary>
/// Component quản lý nháy đỏ vật liệu của model quái vật khi trúng đòn.
/// </summary>
public class MaterialFlashBehaviour : MonoBehaviour
{
    private Renderer[] renderers;
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();
    private Coroutine flashCoroutine;

    private void Awake()
    {
        // Lấy tất cả các renderers của quái vật (bao gồm cả model con)
        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r.material != null && r.material.HasProperty("_Color"))
            {
                Material[] mats = r.materials;
                Color[] colors = new Color[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    colors[i] = mats[i].color;
                }
                originalColors[r] = colors;
            }
        }
    }

    public void Flash(Color flashColor, float duration = 0.15f)
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            RestoreOriginalColors();
        }
        flashCoroutine = StartCoroutine(FlashCoroutine(flashColor, duration));
    }

    private IEnumerator FlashCoroutine(Color color, float duration)
    {
        // Đổi tất cả vật liệu sang màu đỏ chớp nháy
        foreach (var r in renderers)
        {
            if (r == null || !originalColors.ContainsKey(r)) continue;
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].HasProperty("_Color"))
                {
                    mats[i].color = color;
                }
            }
        }

        yield return new WaitForSeconds(duration);

        // Khôi phục lại màu sắc gốc của quái vật
        RestoreOriginalColors();
        flashCoroutine = null;
    }

    private void RestoreOriginalColors()
    {
        foreach (var r in renderers)
        {
            if (r == null || !originalColors.ContainsKey(r)) continue;
            Material[] mats = r.materials;
            Color[] orig = originalColors[r];
            for (int i = 0; i < mats.Length && i < orig.Length; i++)
            {
                if (mats[i].HasProperty("_Color"))
                {
                    mats[i].color = orig[i];
                }
            }
        }
    }
}

/// <summary>
/// Trình tạo 3D TextMesh hiển thị chỉ số sát thương động.
/// </summary>
public static class DamageTextSpawner
{
    public static void Spawn(Vector3 position, float damage, Color color)
    {
        // Tạo GameObject đại diện cho số sát thương
        GameObject go = new GameObject("DamageText");
        // Spawns ở phía trên đầu quái với chút lệch vị trí ngẫu nhiên
        go.transform.position = position + new Vector3(Random.Range(-0.5f, 0.5f), 1.8f, Random.Range(-0.5f, 0.5f));
        
        // Thêm TextMesh của Unity (hiển thị văn bản 3D trực tiếp)
        TextMesh textMesh = go.AddComponent<TextMesh>();
        textMesh.text = damage.ToString("F0");
        textMesh.characterSize = 0.08f;
        textMesh.fontSize = 55;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;

        // Thêm font mặc định của Unity để hiển thị chữ (Khắc phục lỗi TextMesh bị vô hình)
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (defaultFont == null)
        {
            defaultFont = Resources.GetBuiltinResource<Font>("Arial");
        }
        if (defaultFont == null)
        {
            defaultFont = Resources.Load<Font>("Font/Kurale-Regular");
        }
        if (defaultFont == null)
        {
            defaultFont = Resources.Load<Font>("Font/MedievalSharp-Regular");
        }

        if (defaultFont != null)
        {
            textMesh.font = defaultFont;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = defaultFont.material;
                // Tắt bóng đổ để tối ưu hiệu năng
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // Đính kèm component tự di chuyển và tự hủy
        DamageTextBehaviour behaviour = go.AddComponent<DamageTextBehaviour>();
        behaviour.Initialize(color);
    }
}

/// <summary>
/// Xử lý chuyển động bay lên và mờ dần của số sát thương.
/// </summary>
public class DamageTextBehaviour : MonoBehaviour
{
    private TextMesh textMesh;
    private Color baseColor;
    private float lifetime = 0.8f;
    private float timer = 0f;
    private Vector3 moveDirection;

    public void Initialize(Color color)
    {
        textMesh = GetComponent<TextMesh>();
        baseColor = color;
        // Bay lên trên và ngẫu nhiên sang trái/phải một chút
        moveDirection = new Vector3(Random.Range(-0.5f, 0.5f), 2.2f, Random.Range(-0.5f, 0.5f));
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Cập nhật vị trí
        transform.position += moveDirection * Time.deltaTime;

        // Xoay 3D TextMesh luôn hướng về phía Camera chính của người chơi
        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
        }

        // Hiệu ứng mờ dần (Fade out)
        float alpha = Mathf.Lerp(1f, 0f, timer / lifetime);
        textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        
        // Hiệu ứng thu nhỏ dần
        float scale = Mathf.Lerp(1.3f, 0.7f, timer / lifetime);
        transform.localScale = new Vector3(scale, scale, scale);
    }
}
