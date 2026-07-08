using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class FirePillarActivator : NetworkBehaviour
{
    [Header("Cấu hình hiệu ứng")]
    [SerializeField] private float activationDuration = 3.0f; // Thời gian chạy hết hiệu ứng (giây)
    [SerializeField] private string dissolvePropertyName = "_Disolve"; // Tên thuộc tính trong Shader
    [SerializeField] private float startDissolveValue = 0.0f; // Giá trị lúc tắt (lửa ở dưới)
    [SerializeField] private float endDissolveValue = 1.0f; // Giá trị lúc bật (lửa chạy hết lên trên)
    
    [Header("Cấu hình va chạm")]
    [SerializeField] private string fireElementTag = "Lua"; // Tag đạn lửa của Arthur là "Lua"
    
    [Header("Tham chiếu Object con")]
    [SerializeField] private GameObject glowChildObject; // Object con chứa hiệu ứng (trụ_đá_optimized (1))

    private Material glowMaterial;
    
    // Biến mạng đồng bộ trạng thái kích hoạt (Chỉ Server có quyền ghi, Client chỉ đọc)
    private NetworkVariable<bool> m_IsActivated = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    void Start()
    {
        // Tự động tìm object con nếu chưa được kéo vào Inspector
        if (glowChildObject == null)
        {
            if (transform.childCount > 0)
            {
                glowChildObject = transform.GetChild(0).gameObject;
            }
            else
            {
                Debug.LogError("Chưa gán glowChildObject và không tìm thấy object con nào!");
                return;
            }
        }

        // Lấy material từ MeshRenderer của object con
        Renderer childRenderer = glowChildObject.GetComponent<Renderer>();
        if (childRenderer != null)
        {
            glowMaterial = childRenderer.material;
            // Đặt trạng thái tắt ban đầu trên Local để tránh lỗi hiển thị trước khi spawn mạng
            glowMaterial.SetFloat(dissolvePropertyName, startDissolveValue);
            glowChildObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Object con không có MeshRenderer!");
        }
    }

    public override void OnNetworkSpawn()
    {
        // Đăng ký sự kiện thay đổi trạng thái của cột đá
        m_IsActivated.OnValueChanged += OnActivationStateChanged;

        // Xử lý trường hợp người chơi vào sau (Late Joiner):
        // Nếu Server đã kích hoạt cột đá trước đó rồi, hiển thị trạng thái bật hoàn toàn ngay lập tức
        if (m_IsActivated.Value)
        {
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
        if (newValue == true)
        {
            StartCoroutine(ActivatePillarCoroutine());
        }
    }

    // Hàm áp dụng trạng thái phát sáng ngay lập tức (không chạy coroutine) cho người chơi vào sau
    private void ApplyInstantActivatedState()
    {
        if (glowChildObject != null && glowMaterial != null)
        {
            glowChildObject.SetActive(true);
            glowMaterial.SetFloat(dissolvePropertyName, endDissolveValue);
        }
    }

    // Kiểm tra xem đối tượng va chạm có phải là đạn lửa của Arthur hay không
    private bool IsArthurFireball(GameObject hitObj)
    {
        if (hitObj == null) return false;

        // 1. Kiểm tra Tag đạn lửa của Arthur ("Lua")
        if (hitObj.CompareTag(fireElementTag)) return true;

        // 2. Kiểm tra Component ArthurFireProjectile trên chính nó hoặc đối tượng cha
        if (hitObj.GetComponentInParent<ArthurFireProjectile>() != null) return true;

        return false;
    }

    // Chỉ thực hiện phát hiện va chạm mạng trên phía SERVER để tránh hack/lỗi không đồng bộ
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Nếu không phải Server thì bỏ qua

        if (IsArthurFireball(other.gameObject))
        {
            TriggerPillarServer();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
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
        
        m_IsActivated.Value = true; // Cập nhật NetworkVariable -> Tự động đồng bộ tới tất cả Client
    }

    // Diễn họa hiệu ứng chạy lửa từ dưới lên trên cho các máy client
    IEnumerator ActivatePillarCoroutine()
    {
        if (glowChildObject != null && glowMaterial != null)
        {
            // Bật Mesh hiệu ứng
            glowChildObject.SetActive(true);
            
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
        }
    }
}
