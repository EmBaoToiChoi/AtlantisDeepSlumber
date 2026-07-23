using UnityEngine;
using Unity.Netcode;
using System.Collections;

[ExecuteAlways]
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

    [Header("Cấu hình Tia Laser Phản Chiếu")]
    [SerializeField] private bool enableLaser = true;
    [SerializeField] private LineRenderer laserLineRenderer;
    [SerializeField] private Transform laserStartPoint;
    [SerializeField] private int maxReflections = 5;
    [SerializeField] private float maxStepDistance = 50f;
    [SerializeField] private LayerMask mirrorLayerMask;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private LayerMask targetLayerMask;

    [Header("Xem trước trong Editor")]
    [SerializeField] private bool previewLaserInEditor = false; // Tích chọn để vẽ tia laser ngay trong Edit Mode để dễ căn chỉnh gương

    private Material glowMaterial;
    private bool m_IsLaserActive = false;
    
    // Biến mạng đồng bộ trạng thái kích hoạt (Chỉ Server có quyền ghi, Client chỉ đọc)
    private NetworkVariable<bool> m_IsActivated = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    public bool IsActivated => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? m_IsActivated.Value : false;

    private void Awake()
    {
        // Ép buộc giá trị đúng để đồng bộ với logic của Shader mới (1 = Tắt, 0 = Bật)
        // Việc này ghi đè các thiết lập cũ/sai trong Inspector nếu có
        startDissolveValue = 1.0f;
        endDissolveValue = 0.0f;
        m_IsLaserActive = false;

        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (glowMaterial != null) return;
        if (!Application.isPlaying) return;

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
            }
            if (glowChildObject != null)
            {
                targetRenderer = glowChildObject.GetComponent<Renderer>();
            }
        }
        else
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer != null)
        {
            glowMaterial = targetRenderer.material;
        }
    }

    void Start()
    {
        // Bỏ qua khởi chạy Netcode/Material nếu đang trong Edit Mode của Editor
        if (!Application.isPlaying) return;

        Debug.Log($"[FirePillarActivator] Khởi chạy Start trên {gameObject.name} (Chế độ ChildOverlay={useChildOverlayMode})");
        
        InitializeIfNeeded();

        if (glowMaterial != null)
        {
            // Chỉ đặt trạng thái tắt ban đầu nếu chưa được spawn qua mạng hoặc biến mạng chưa được kích hoạt
            // Nếu đã kích hoạt qua mạng, OnNetworkSpawn/ApplyInstantActivatedState đã thiết lập đúng
            if (!IsSpawned || !m_IsActivated.Value)
            {
                // Đặt trạng thái tắt ban đầu trên Local để tránh lỗi hiển thị trước khi spawn mạng
                glowMaterial.SetFloat(dissolvePropertyName, startDissolveValue);
                
                if (useChildOverlayMode && glowChildObject != null)
                {
                    glowChildObject.SetActive(false);
                }
                Debug.Log($"[FirePillarActivator] Đã gán Material thành công. Dissolve ban đầu = {startDissolveValue}");
            }
        }
        else
        {
            Debug.LogError("[FirePillarActivator] Không tìm thấy MeshRenderer hoặc Material!");
        }

        // Tự động cấu hình cơ bản cho LineRenderer để tia sáng nhìn đẹp hơn
        if (laserLineRenderer != null)
        {
            laserLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laserLineRenderer.receiveShadows = false;
            laserLineRenderer.positionCount = 0;
        }
    }

    public override void OnNetworkSpawn()
    {
        InitializeIfNeeded();
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

    void Update()
    {
        if (Application.isPlaying)
        {
            // Vẽ laser thời gian thực khi game đang chạy và cột đá đã được kích hoạt hoàn toàn
            if (m_IsLaserActive && enableLaser && laserLineRenderer != null && laserStartPoint != null)
            {
                DrawLaserReflection();
            }
            else
            {
                if (laserLineRenderer != null && laserLineRenderer.positionCount > 0)
                {
                    laserLineRenderer.positionCount = 0;
                }
            }
        }
        else
        {
            // Chế độ Edit Mode (Khi game KHÔNG chạy)
            if (previewLaserInEditor && laserLineRenderer != null && laserStartPoint != null)
            {
                DrawLaserReflection();
            }
            else
            {
                if (laserLineRenderer != null && laserLineRenderer.positionCount > 0)
                {
                    laserLineRenderer.positionCount = 0;
                }
            }
        }
    }

    private void DrawLaserReflection()
    {
        Vector3 currentOrigin = laserStartPoint.position;
        Vector3 currentDir = laserStartPoint.forward; // Hoặc laserStartPoint.up tùy thiết kế prefab của bạn

        System.Collections.Generic.List<Vector3> laserPoints = new System.Collections.Generic.List<Vector3>();
        laserPoints.Add(currentOrigin);

        bool hitFinalTarget = false;
        GameObject finalTargetObj = null;

        LayerMask combinedMask = mirrorLayerMask | obstacleLayerMask | targetLayerMask;

        for (int i = 0; i < maxReflections; i++)
        {
            Ray ray = new Ray(currentOrigin, currentDir);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, maxStepDistance, combinedMask, QueryTriggerInteraction.Ignore))
            {
                laserPoints.Add(hit.point);

                // 1. Kiểm tra trúng đích Target (Trụ năng lượng cuối cùng)
                if (((1 << hit.collider.gameObject.layer) & targetLayerMask) != 0)
                {
                    hitFinalTarget = true;
                    finalTargetObj = hit.collider.gameObject;
                    break; // Đã trúng đích -> Dừng vẽ tia tại đây
                }

                // 2. Kiểm tra va chạm với Gương phản chiếu (Mirror)
                if (((1 << hit.collider.gameObject.layer) & mirrorLayerMask) != 0)
                {
                    // Chỉ phản chiếu khi đập vào MẶT TRƯỚC của gương (Góc giữa hướng tia và pháp tuyến mặt gương nhỏ hơn 90 độ, tức Dot < 0)
                    if (Vector3.Dot(currentDir, hit.normal) < 0f)
                    {
                        currentDir = Vector3.Reflect(currentDir, hit.normal);
                        currentOrigin = hit.point + currentDir * 0.01f; // Dời điểm gốc tia mới ra ngoài một chút để tránh tự va chạm
                    }
                    else
                    {
                        // Đập vào mặt sau gương -> Bị chặn lại và dừng vẽ
                        break;
                    }
                }
                else
                {
                    // Đập vào chướng ngại vật thông thường -> Bị chặn lại
                    break;
                }
            }
            else
            {
                // Bắn tự do ra xa nếu không va chạm
                laserPoints.Add(currentOrigin + currentDir * maxStepDistance);
                break;
            }
        }

        // Cập nhật tọa độ vẽ LineRenderer
        laserLineRenderer.positionCount = laserPoints.Count;
        laserLineRenderer.SetPositions(laserPoints.ToArray());

        // Chỉ Server chịu trách nhiệm cập nhật trạng thái trúng câu đố (Chỉ kiểm tra khi game đang chạy thực sự)
        if (Application.isPlaying)
        {
            if (IsServer)
            {
                UpdateTargetActivationServer(hitFinalTarget, finalTargetObj);
            }
        }
        else
        {
            // Edit Mode: Kích hoạt preview trực quan nếu tia laser bắn trúng
            if (hitFinalTarget && finalTargetObj != null)
            {
                FinalEnergyPillar targetPillar = finalTargetObj.GetComponent<FinalEnergyPillar>();
                if (targetPillar == null)
                {
                    targetPillar = finalTargetObj.GetComponentInParent<FinalEnergyPillar>();
                }
                if (targetPillar != null)
                {
                    targetPillar.SetLaserHitInEditor();
                }
            }
        }
    }

    private void UpdateTargetActivationServer(bool hitFinalTarget, GameObject finalTargetObj)
    {
        if (!IsServer) return;

        if (hitFinalTarget && finalTargetObj != null)
        {
            FinalEnergyPillar targetPillar = finalTargetObj.GetComponent<FinalEnergyPillar>();
            if (targetPillar == null)
            {
                targetPillar = finalTargetObj.GetComponentInParent<FinalEnergyPillar>();
            }

            if (targetPillar != null)
            {
                targetPillar.SetLaserHitThisFrame();
            }
        }
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
        InitializeIfNeeded();
        m_IsLaserActive = true;
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
        InitializeIfNeeded();
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
            
            // Kích hoạt vẽ laser sau khi hoàn thành 3 giây dâng lửa
            m_IsLaserActive = true;
            Debug.Log("[FirePillarActivator] Hoàn thành chạy hiệu ứng lửa. Bắt đầu bắn tia laser.");
        }
    }
}
