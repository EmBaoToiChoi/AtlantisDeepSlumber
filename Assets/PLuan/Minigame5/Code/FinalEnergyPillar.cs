using UnityEngine;
using Unity.Netcode;

public class FinalEnergyPillar : NetworkBehaviour
{
    [Header("Cấu hình hiển thị")]
    [SerializeField] private Renderer pillarRenderer;
    [SerializeField] private string activationPropertyName = "_Disolve"; // Thuộc tính shader để kiểm soát phát sáng (Dành cho Custom Shader)
    [SerializeField] private float activeDissolveValue = 0.0f; // Giá trị khi được kích hoạt (sáng)
    [SerializeField] private float inactiveDissolveValue = 1.0f; // Giá trị khi tắt (tối)

    [Header("Cấu hình Phát sáng Màu (Standard Shader)")]
    [SerializeField] private bool useColorEmission = false; // Tích chọn để dùng màu phát sáng của Standard Shader gốc
    [SerializeField] private string colorPropertyName = "_EmissionColor"; // Tên thuộc tính màu phát sáng trong Standard Shader
    [ColorUsage(true, true)] [SerializeField] private Color activeColor = Color.cyan; // Màu HDR phát sáng khi kích hoạt

    [Header("Hiệu ứng đi kèm (Tùy chọn)")]
    [SerializeField] private GameObject activeEffectObject; // Hạt particle hoặc hiệu ứng phụ khi trụ sáng

    // Biến mạng đồng bộ trạng thái kích hoạt của Trụ Năng Lượng Cuối Cùng
    private NetworkVariable<bool> m_IsActivated = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool m_WasHitThisFrame = false;
    private Material m_Material;

    void Start()
    {
        // Tự động tìm Renderer trên chính object này nếu chưa gán
        if (pillarRenderer == null)
        {
            pillarRenderer = GetComponent<Renderer>();
        }

        if (pillarRenderer != null)
        {
            m_Material = pillarRenderer.material;
            // Trạng thái ban đầu: Tắt
            if (useColorEmission)
            {
                m_Material.SetColor(colorPropertyName, Color.black);
            }
            else
            {
                m_Material.SetFloat(activationPropertyName, inactiveDissolveValue);
            }
        }

        if (activeEffectObject != null)
        {
            activeEffectObject.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[FinalEnergyPillar] OnNetworkSpawn kích hoạt. IsServer = {IsServer}, Trạng thái = {m_IsActivated.Value}");
        m_IsActivated.OnValueChanged += OnStateChanged;
        
        // Đặt trạng thái ban đầu cho late joiners
        ApplyActivatedState(m_IsActivated.Value);
    }

    public override void OnNetworkDespawn()
    {
        m_IsActivated.OnValueChanged -= OnStateChanged;
    }

    private void OnStateChanged(bool previousValue, bool newValue)
    {
        Debug.Log($"[FinalEnergyPillar] Trạng thái mạng đổi: {previousValue} -> {newValue}");
        ApplyActivatedState(newValue);
    }

    private void ApplyActivatedState(bool active)
    {
        if (m_Material != null)
        {
            if (useColorEmission)
            {
                if (active)
                {
                    m_Material.EnableKeyword("_EMISSION");
                    m_Material.SetColor(colorPropertyName, activeColor);
                }
                else
                {
                    m_Material.SetColor(colorPropertyName, Color.black);
                }
            }
            else
            {
                // Thiết lập giá trị phát sáng trong shader
                m_Material.SetFloat(activationPropertyName, active ? activeDissolveValue : inactiveDissolveValue);
            }
        }

        if (activeEffectObject != null)
        {
            activeEffectObject.SetActive(active);
        }
    }

    // Hàm gọi từ phía Server khi có tia laser của Emitter chiếu trúng trong frame này
    public void SetLaserHitThisFrame()
    {
        if (!IsServer) return;
        m_WasHitThisFrame = true;
    }

    void LateUpdate()
    {
        // Chỉ Server có quyền thay đổi trạng thái mạng của câu đố
        if (!IsServer) return;

        // Nếu trạng thái kích hoạt hiện tại khác với kết quả kiểm tra tia laser trong frame
        if (m_IsActivated.Value != m_WasHitThisFrame)
        {
            m_IsActivated.Value = m_WasHitThisFrame;
            Debug.Log($"[FinalEnergyPillar] Cập nhật trạng thái mạng: Được kích hoạt = {m_WasHitThisFrame}");
        }

        // Reset lại biến để kiểm tra va chạm ở frame tiếp theo
        m_WasHitThisFrame = false;
    }
}
