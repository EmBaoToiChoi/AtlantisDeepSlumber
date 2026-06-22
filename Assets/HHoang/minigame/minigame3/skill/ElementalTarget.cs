using UnityEngine;

public class ElementalTarget : MonoBehaviour
{
    [Header("Cấu hình màu (Trắng/Xám/Đỏ/Lục...)")]
    public Color highlightColor = Color.white; 
    
    [Header("Độ sáng lúc bị quét (HDR)")]
    public float emissionIntensity = 0.6f; 

    private Renderer[] renderers;
    private Color[] originalEmissions;
    private bool isHighlighted = false;

    void Awake()
    {
        // Lấy tất cả các bề mặt của vật thể
        renderers = GetComponentsInChildren<Renderer>();
        originalEmissions = new Color[renderers.Length];

        // Lưu lại màu gốc để lúc hết quét nó trả về bình thường
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_EmissionColor"))
            {
                originalEmissions[i] = renderers[i].material.GetColor("_EmissionColor");
            }
        }
    }

    void OnEnable()
    {
        // Đưa vật thể này vào danh sách cho Scanner nó biết
        ElementalScanner.RegisterTarget(this);
    }

    void OnDisable()
    {
        // Xoá vật thể khỏi danh sách (ví dụ lúc quái chết)
        ElementalScanner.UnregisterTarget(this);
    }

    public void SetHighlight(bool state)
    {
        if (isHighlighted == state) return;
        isHighlighted = state;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (state)
            {
                // Bật phát sáng với màu ông đã chọn ở Inspector
                renderers[i].material.EnableKeyword("_EMISSION");
                renderers[i].material.SetColor("_EmissionColor", highlightColor * emissionIntensity); 
            }
            else
            {
                // Trả về màu gốc
                renderers[i].material.SetColor("_EmissionColor", originalEmissions[i]);
            }
        }
    }
}