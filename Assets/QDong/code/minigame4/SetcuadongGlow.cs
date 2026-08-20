using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class SetcuadongGlow : MonoBehaviour
{
    [Header("Glow Settings")]
    [Tooltip("Màu vàng đậm khi nạp đầy (Bật HDR để phát sáng)")]
    [ColorUsage(true, true)] 
    public Color fullChargeColor = new Color(2f, 1.5f, 0f, 1f); // Màu vàng chói HDR
    
    private Renderer rend;
    private MaterialPropertyBlock propBlock;
    private EnergyColumn energyColumn;

    void Start()
    {
        rend = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        
        // Tìm EnergyColumn ở object cha để biết đang nạp đến đâu
        energyColumn = GetComponentInParent<EnergyColumn>();
    }

    void Update()
    {
        if (energyColumn != null && rend != null)
        {
            // Tiến độ nạp từ 0 đến 1
            float progress = energyColumn.maxCharge > 0 ? Mathf.Clamp01(energyColumn.charge.Value / energyColumn.maxCharge) : 0f;

            // Lấy dữ liệu Material hiện tại (bao gồm cả hiệu ứng chạy từ dưới lên của EnergyColumn)
            rend.GetPropertyBlock(propBlock);
            
            // Tính toán màu: 
            // - Mặc định (0%): Trắng (giữ nguyên màu vân đá gốc)
            // - Nạp đầy (100%): Vàng đậm (fullChargeColor)
            Color currentColor = Color.Lerp(Color.white, fullChargeColor, progress);
            
            // Ghi đè màu gốc của Shader
            propBlock.SetColor("_Color", currentColor);
            
            // Áp dụng thay đổi
            rend.SetPropertyBlock(propBlock);
        }
    }
}
