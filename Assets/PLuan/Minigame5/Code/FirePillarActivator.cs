using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class FirePillarActivator : NetworkBehaviour
{
    [Header("Cấu hình hiệu ứng")]
    [SerializeField] private float activationDuration = 3.0f; // Thời gian chạy hết hiệu ứng (giây)
    [SerializeField] private string dissolvePropertyName = "_Disolve"; // Tên thuộc tính trong Shader
    [SerializeField] private float startDissolveValue = 1.0f; // 1.0: Giá trị lúc tắt (lửa ẩn)
    [SerializeField] private float endDissolveValue = 0.0f; // 0.0: Giá trị lúc bật (lửa chạy hết lên trên)
    
    [Header("Chế độ hiển thị")]
    [SerializeField] private bool useChildOverlayMode = false; // TRUE: Dùng Object con đè lên | FALSE: Dùng trực tiếp trên cột đá này
    [SerializeField] private GameObject glowChildObject; // Chỉ cần thiết nếu useChildOverlayMode = true
    
    [Header("Cấu hình va chạm")]
    [SerializeField] private string fireElementTag = "Lua"; // Tag đạn lửa của Arthur là "Lua"

    private Material glowMaterial;
    
    // Biến mạng đồng bộ trạng thái kích hoạt (Chỉ Server có quyền ghi, Client chỉ đọc)
    private NetworkVariable<bool> m_IsActivated = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        // Ép buộc giá trị đúng để đồng bộ với logic của Shader mới (1 = Tắt, 0 = Bật)
        // Việc này ghi đè các thiết lập cũ/sai trong Inspector nếu có
        startDissolveValue = 1.0f;
        endDissolveValue = 0.0f;
    }

    void Start()
    {
        Debug.Log($"[FirePillarActivator] Khởi chạy Start trên {gameObject.name} (Chế độ ChildOverlay={useChildOverlayMode})");
        
        Renderer targetRenderer = null;

        if (useChildOverlayMode)
        {
            if (glowChildObject == null)
            {
                if (transform.childCount > 0)
                {
                    glowChildObject = transform.GetChild(0).gameObject;
                    Debug.Log($"[FirePillarActivator] Tự động tìm thấy object con: {glowChildObject.name}");
                }
                else
                {
                    Debug.LogError("[FirePillarActivator] Chưa gán glowChildObject và không tìm thấy object con nào!");
                    return;
                }
            }
            targetRenderer = glowChildObject.GetComponent<Renderer>();
        }
        else
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer != null)
        {
            glowMaterial = targetRenderer.material;
            // Đặt trạng thái tắt ban đầu trên Local để tránh lỗi hiển thị trước khi spawn mạng
            glowMaterial.SetFloat(dissolvePropertyName, startDissolveValue);
            
            if (useChildOverlayMode && glowChildObject != null)
            {
                glowChildObject.SetActive(false);
            }
            Debug.Log($"[FirePillarActivator] Đã gán Material thành công. Dissolve ban đầu = {startDissolveValue}");
        }
        else
        {
            Debug.LogError("[FirePillarActivator] Không tìm thấy MeshRenderer!");
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[FirePillarActivator] OnNetworkSpawn kích hoạt. IsServer = {IsServer}, Giá trị biến mạng = {m_IsActivated.Value}");
        // Đăng ký sự kiện thay đổi trạng thái của cột đá
        m_IsActivated.OnValueChanged += OnActivationStateChanged;

        // Xử lý trường hợp người chơi vào sau (Late Joiner):
        // Nếu Server đã kích hoạt cột đá trước đó rồi, hiển thị trạng thái bật hoàn toàn ngay lập tức
        if (m_IsActivated.Value)
        {
            Debug.Log("[FirePillarActivator] Phát hiện cột đá đã kích hoạt từ trước (Late Joiner). Hiển thị ngay hiệu ứng.");
            ApplyInstantActivatedState();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Hủy đăng ký sự kiện tránh rò rỉ bộ nhớ
        m_IsActivated.OnValueChanged -= OnActivationStateChanged;
    }

    // Lắng nghe thay đổi biến NetworkVariable từ Server
    private void OnActivationStateChanged(bool previousValue, bool newValue)
    {
        Debug.Log($"[FirePillarActivator] Trạng thái mạng thay đổi: {previousValue} -> {newValue}");
        if (newValue == true)
        {
            StartCoroutine(ActivatePillarCoroutine());
        }
    }

    // Hàm áp dụng trạng thái phát sáng ngay lập tức (không chạy coroutine) cho người chơi vào sau
    private void ApplyInstantActivatedState()
    {
        if (glowMaterial != null)
        {
            if (useChildOverlayMode && glowChildObject != null)
            {
                glowChildObject.SetActive(true);
            }
            glowMaterial.SetFloat(dissolvePropertyName, endDissolveValue);
        }
    }

    // Kiểm tra xem đối tượng va chạm có phải là đạn lửa của Arthur hay không
    private bool IsArthurFireball(GameObject hitObj)
    {
        if (hitObj == null) return false;

        bool hasTag = hitObj.CompareTag(fireElementTag);
        bool hasComponent = hitObj.GetComponentInParent<ArthurFireProjectile>() != null;

        Debug.Log($"[FirePillarActivator Debug] Kiểm tra va chạm vật thể: Name='{hitObj.name}' | Tag='{hitObj.tag}' | Có Tag '{fireElementTag}'={hasTag} | Có ArthurScript={hasComponent}");

        return hasTag || hasComponent;
    }

    // Chỉ thực hiện phát hiện va chạm mạng trên phía SERVER để tránh hack/lỗi không đồng bộ
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[FirePillarActivator] OnTriggerEnter va chạm với '{other.gameObject.name}' (IsServer={IsServer})");
        if (!IsServer) return; // Nếu không phải Server thì bỏ qua

        if (IsArthurFireball(other.gameObject))
        {
            TriggerPillarServer();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[FirePillarActivator] OnCollisionEnter va chạm với '{collision.gameObject.name}' (IsServer={IsServer})");
        if (!IsServer) return; // Nếu không phải Server thì bỏ qua

        if (IsArthurFireball(collision.gameObject))
        {
            TriggerPillarServer();
        }
    }

    // Hàm gọi trên Server để kích hoạt cột đá
    private void TriggerPillarServer()
    {
        if (m_IsActivated.Value) return; // Đã kích hoạt rồi thì bỏ qua
        
        Debug.Log("[FirePillarActivator] Server xác nhận va chạm đạn lửa hợp lệ! Đang đồng bộ kích hoạt sang các máy client...");
        m_IsActivated.Value = true; // Cập nhật NetworkVariable -> Tự động đồng bộ tới tất cả Client
    }

    // Diễn họa hiệu ứng chạy lửa từ dưới lên trên cho các máy client
    IEnumerator ActivatePillarCoroutine()
    {
        if (glowMaterial != null)
        {
            Debug.Log($"[FirePillarActivator] Bắt đầu chạy hiệu ứng lửa chạy từ dưới lên. Thời gian = {activationDuration}s");
            
            // Đặt giá trị ẩn TRƯỚC KHI bật Mesh để tránh hiện tượng nháy sáng (flash) đột ngột
            glowMaterial.SetFloat(dissolvePropertyName, startDissolveValue);
            
            if (useChildOverlayMode && glowChildObject != null)
            {
                glowChildObject.SetActive(true);
            }
            
            float elapsed = 0f;
            while (elapsed < activationDuration)
            {
                elapsed += Time.deltaTime;
                float currentVal = Mathf.Lerp(startDissolveValue, endDissolveValue, elapsed / activationDuration);
                
                glowMaterial.SetFloat(dissolvePropertyName, currentVal);
                yield return null;
            }

            // Đảm bảo dừng ở giá trị cuối cùng
            glowMaterial.SetFloat(dissolvePropertyName, endDissolveValue);
            Debug.Log("[FirePillarActivator] Hoàn thành chạy hiệu ứng lửa.");
        }
    }
}
