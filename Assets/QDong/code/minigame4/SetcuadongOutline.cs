using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class SetcuadongOutline : MonoBehaviour
{
    [Header("Outline Settings")]
    public Color outlineColor = new Color(1f, 0.8f, 0f, 1f); // Màu vàng
    public float outlineThickness = 1.05f; // Độ dày viền (Phóng to mesh)
    public float maxEmissionIntensity = 2.5f; // Độ chói khi đầy

    private GameObject outlineObject;
    private Material outlineMaterial;
    private EnergyColumn energyColumn;

    void Start()
    {
        // Cố gắng tìm EnergyColumn từ object cha để biết tiến trình nạp
        energyColumn = GetComponentInParent<EnergyColumn>();

        // 1. Tạo một object con để chứa mesh viền
        outlineObject = new GameObject(gameObject.name + "_Outline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one * outlineThickness;

        // 2. Copy Mesh từ object thật sang object viền
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = meshFilter.sharedMesh;

        // 3. Cài đặt Renderer cho viền
        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;

        // 4. Tạo Material phát sáng cho viền (Dùng Standard shader nhưng đổi sang chế độ trong suốt)
        outlineMaterial = new Material(Shader.Find("Standard"));
        
        // Đổi chế độ render sang Transparent để có thể làm mờ/ẩn hiện
        outlineMaterial.SetFloat("_Mode", 3); 
        outlineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        outlineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        outlineMaterial.SetInt("_ZWrite", 0);
        outlineMaterial.DisableKeyword("_ALPHATEST_ON");
        outlineMaterial.EnableKeyword("_ALPHABLEND_ON");
        outlineMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        outlineMaterial.renderQueue = 3000;

        // Bật phát sáng (Emission)
        outlineMaterial.EnableKeyword("_EMISSION");
        outlineMaterial.color = new Color(0, 0, 0, 0); // Ẩn màu gốc, chỉ dùng màu phát sáng
        outlineMaterial.SetColor("_EmissionColor", Color.black);

        outlineRenderer.material = outlineMaterial;
    }

    void Update()
    {
        if (energyColumn != null && outlineMaterial != null)
        {
            // Lấy tiến trình nạp từ 0 đến 1
            float progress = energyColumn.maxCharge > 0 ? Mathf.Clamp01(energyColumn.charge.Value / energyColumn.maxCharge) : 0f;

            // Tính toán màu phát sáng dựa trên tiến trình nạp
            // Khi nạp đầy (progress = 1) sẽ là độ chói tối đa và luôn giữ nguyên màu đó
            float currentIntensity = progress * maxEmissionIntensity;
            
            // Set màu viền (Chỉ hiện khi progress > 0)
            if (progress > 0)
            {
                // Thay đổi alpha và độ chói đồng thời
                Color finalColor = outlineColor * currentIntensity;
                outlineMaterial.SetColor("_EmissionColor", finalColor);
                outlineMaterial.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, progress * 0.5f);
            }
            else
            {
                // Khi chưa nạp gì thì tắt hẳn viền
                outlineMaterial.SetColor("_EmissionColor", Color.black);
                outlineMaterial.color = new Color(0, 0, 0, 0);
            }
        }
    }
}
