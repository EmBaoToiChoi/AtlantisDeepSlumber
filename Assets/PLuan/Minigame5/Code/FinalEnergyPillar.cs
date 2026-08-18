using UnityEngine;
using Unity.Netcode;
using UnityEngine.Events;

[ExecuteAlways]
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
    [SerializeField] private float transitionDuration = 1.0f; // Thời gian dâng sáng quét từ dưới lên (giây)

    [Header("Sự kiện kích hoạt")]
    public UnityEvent OnActivated; // Kích hoạt khi trụ được sạc đầy
    public UnityEvent OnDeactivated; // Kích hoạt khi trụ bị mất nguồn sáng

    // Biến mạng đồng bộ trạng thái kích hoạt của Trụ Năng Lượng Cuối Cùng
    private NetworkVariable<bool> m_IsActivated = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsActivated => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? m_IsActivated.Value : false;

    private bool m_WasHitThisFrame = false;
    private bool m_WasHitInEditor = false; // Nhận diện trúng laser trong Edit Mode
    private bool m_EditorLastActiveState = false; // Lưu trạng thái trước đó trong Edit Mode
    private Material m_Material;
    private Coroutine m_TransitionCoroutine;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (m_Material != null) return;

        // Tự động tìm Renderer trên chính object này nếu chưa gán
        if (pillarRenderer == null)
        {
            pillarRenderer = GetComponent<Renderer>();
        }

        if (pillarRenderer != null)
        {
            // Trong Edit Mode dùng sharedMaterial để không gây rò rỉ bộ nhớ
            m_Material = Application.isPlaying ? pillarRenderer.material : pillarRenderer.sharedMaterial;
        }
    }

    void Start()
    {
        InitializeIfNeeded();

        // Chỉ đặt trạng thái tắt ban đầu nếu chưa được spawn qua mạng
        // Nếu đã spawn (đối với in-scene objects), OnNetworkSpawn đã thiết lập đúng giá trị đồng bộ
        if (!IsSpawned)
        {
            if (m_Material != null)
            {
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
    }

    public override void OnNetworkSpawn()
    {
        InitializeIfNeeded();
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
        InitializeIfNeeded();

        if (!Application.isPlaying)
        {
            // Edit Mode: cập nhật trực quan ngay lập tức
            Material mat = pillarRenderer != null ? pillarRenderer.sharedMaterial : null;
            if (mat != null)
            {
                if (useColorEmission)
                {
                    if (active)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor(colorPropertyName, activeColor);
                    }
                    else
                    {
                        mat.SetColor(colorPropertyName, Color.black);
                    }
                }
                else
                {
                    mat.SetFloat(activationPropertyName, active ? activeDissolveValue : inactiveDissolveValue);
                }
            }
            if (activeEffectObject != null)
            {
                activeEffectObject.SetActive(active);
            }

            // Kích hoạt sự kiện trong Edit Mode chỉ khi trạng thái thay đổi
            if (m_EditorLastActiveState != active)
            {
                m_EditorLastActiveState = active;
                if (active) OnActivated?.Invoke();
                else OnDeactivated?.Invoke();
            }
            return;
        }

        // Play Mode: Chạy Coroutine dâng sáng mượt mà từ dưới lên
        if (m_TransitionCoroutine != null)
        {
            StopCoroutine(m_TransitionCoroutine);
        }
        m_TransitionCoroutine = StartCoroutine(TransitionGlowCoroutine(active));

        if (activeEffectObject != null)
        {
            activeEffectObject.SetActive(active);
        }

        // Khi TẮT nguồn (active = false): Kích hoạt đóng cửa ngay lập tức để phản hồi nhanh
        if (!active)
        {
            OnDeactivated?.Invoke();
        }
    }

    private System.Collections.IEnumerator TransitionGlowCoroutine(bool active)
    {
        float targetValue = active ? activeDissolveValue : inactiveDissolveValue;
        float startValue = m_Material.GetFloat(activationPropertyName);
        Color startColor = useColorEmission ? m_Material.GetColor(colorPropertyName) : Color.black;
        Color targetColor = active ? activeColor : Color.black;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            if (useColorEmission)
            {
                m_Material.EnableKeyword("_EMISSION");
                m_Material.SetColor(colorPropertyName, Color.Lerp(startColor, targetColor, t));
            }
            else
            {
                m_Material.SetFloat(activationPropertyName, Mathf.Lerp(startValue, targetValue, t));
            }
            yield return null;
        }

        if (useColorEmission)
        {
            m_Material.SetColor(colorPropertyName, targetColor);
            if (!active) m_Material.DisableKeyword("_EMISSION");
        }
        else
        {
            m_Material.SetFloat(activationPropertyName, targetValue);
        }

        // Khi BẬT nguồn (active = true): Chỉ kích hoạt sự kiện mở cửa sau khi ngọc đã dâng sáng 100%
        if (active)
        {
            OnActivated?.Invoke();
        }

        m_TransitionCoroutine = null;
    }

    // Hàm gọi từ phía Server khi có tia laser của Emitter chiếu trúng trong frame này
    public void SetLaserHitThisFrame()
    {
        if (!IsServer) return;
        m_WasHitThisFrame = true;
    }

    // Hàm gọi từ Emitter khi chiếu trúng trong Edit Mode để preview
    public void SetLaserHitInEditor()
    {
        m_WasHitInEditor = true;
    }

    void LateUpdate()
    {
        if (Application.isPlaying)
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
        else
        {
            // Chạy trong Edit Mode để vẽ hiệu ứng trực quan không cần Play game
            ApplyActivatedState(m_WasHitInEditor);
            m_WasHitInEditor = false;
        }
    }
}
