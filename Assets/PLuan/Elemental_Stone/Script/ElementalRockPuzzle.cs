using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class ElementalRockPuzzle : NetworkBehaviour
{
    [Header("Puzzle Settings")]
    [Tooltip("Thời gian tối đa để kích hoạt đủ 4 nguyên tố kể từ nguyên tố đầu tiên (giây)")]
    public float timeLimit = 8f;

    [Header("Element Tags Configuration")]
    [Tooltip("Tag của đạn nguyên tố Lửa")]
    public string fireTag = "Lua";
    [Tooltip("Tag của đạn nguyên tố Nước")]
    public string waterTag = "Nuoc";
    [Tooltip("Tag của đạn nguyên tố Băng")]
    public string iceTag = "Bang";
    [Tooltip("Tag của đạn nguyên tố Sét")]
    public string lightningTag = "Set";

    [Header("Icon Settings")]
    [Tooltip("Icon cho nguyên tố Lửa")]
    public Sprite fireIcon;
    [Tooltip("Icon cho nguyên tố Nước")]
    public Sprite waterIcon;
    [Tooltip("Icon cho nguyên tố Băng")]
    public Sprite iceIcon;
    [Tooltip("Icon cho nguyên tố Sét")]
    public Sprite lightningIcon;

    [Header("UI Transform Settings")]
    [Tooltip("Transform chỉ định vị trí hiện icon. Nếu để trống, sẽ tự động tính toán đỉnh của vật thể.")]
    public Transform uiPivot;
    [Tooltip("Chiều cao bù thêm (offset) khi tự động tính toán vị trí hiển thị phía trên viên đá")]
    public float offsetHeight = 2.5f;
    [Tooltip("Khoảng cách nằm ngang giữa các Icon")]
    public float iconSpacing = 0.6f;
    [Tooltip("Tỉ lệ thu phóng (Scale) của Icon")]
    public float iconScale = 0.5f;
    [Tooltip("Có tự động quay Icon về hướng Camera không")]
    public bool faceCamera = true;
    [Tooltip("Góc xoay bù thêm (offset) cho hàng icon (độ Euler)")]
    public Vector3 uiRotationOffset = Vector3.zero;

    [Header("Status (Read Only - Local State)")]
    [SerializeField] private int currentStep = 0;
    [SerializeField] private float timeRemaining = 0f;
    [SerializeField] private bool isTimerRunning = false;

    // --- CÁC BIẾN ĐỒNG BỘ MẠNG (NETCODE) ---
    private NetworkVariable<int> netCurrentStep = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<float> netTimeRemaining = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private NetworkVariable<bool> netIsTimerRunning = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private string[] orderedTags;
    private GameObject lastHitObject; // Tránh việc một viên đạn va chạm liên tục nhiều lần trong các frame kế tiếp
    private ulong lastProcessedProjectileId = 0; // Tránh va chạm trùng lặp giữa Client RPC và Server Local Trigger

    // Cache các đối tượng sinh ra để quản lý UI
    private GameObject uiRootObj;
    private GameObject[] iconObjects;
    private SpriteRenderer[] iconRenderers;

    [Header("Element 3D Renderers (Model)")]
    public Renderer fireRenderer;
    public Renderer waterRenderer;
    public Renderer iceRenderer;
    public Renderer lightningRenderer;

    [Header("Glow Colors (HDR)")]
    [ColorUsage(true, true)] public Color fireGlowColor = Color.red * 5f;
    [ColorUsage(true, true)] public Color waterGlowColor = Color.blue * 5f;
    [ColorUsage(true, true)] public Color iceGlowColor = Color.cyan * 5f;
    [ColorUsage(true, true)] public Color lightningGlowColor = Color.yellow * 5f;

    [Header("Fade Settings")]
    public float fadeSpeed = 2f;

    private Renderer[] elementRenderers;
    private Color[] elementGlowColors;
    private Coroutine[] fadeCoroutines;

    [Header("Start Hidden Settings")]
    [Tooltip("Nếu tích chọn, đá sẽ tự ẩn Renderer và Collider khi bắt đầu (nhưng GameObject vẫn Active để tránh lỗi Netcode).")]
    public bool startHidden = false;

    private NetworkVariable<bool> netIsShown = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    private bool isShown = false;
    public bool IsShown => !startHidden || (IsNetworkActive ? netIsShown.Value : isShown);

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    private void Start()
    {
        orderedTags = new string[] { fireTag, waterTag, iceTag, lightningTag };

        // ĐẢM BẢO đá không có Rigidbody để đóng vai trò là Static Collider/Trigger.
        // Trong Unity, Kinematic Rigidbody (của đạn) KHÔNG va chạm/trigger với Kinematic Rigidbody khác (của đá).
        // Chúng chỉ va chạm với Static Collider hoặc Dynamic Rigidbody.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Destroy(rb);
            Debug.Log($"[ElementalRockPuzzle] Đã xóa Rigidbody trên '{gameObject.name}' để chuyển thành Static Collider (tránh lỗi Kinematic vs Kinematic).");
        }

        // Đảm bảo tất cả Collider sẵn có trên đối tượng cha đều được thiết lập làm Trigger để bắt va chạm
        Collider[] parentColliders = GetComponents<Collider>();
        foreach (var col in parentColliders)
        {
            col.isTrigger = true;
            Debug.Log($"[ElementalRockPuzzle] '{gameObject.name}' đã tự động cấu hình Collider '{col.GetType().Name}' làm Trigger.");
        }

        // Cấu hình các 3D Renderers nếu được gán
        elementRenderers = new Renderer[] { fireRenderer, waterRenderer, iceRenderer, lightningRenderer };
        elementGlowColors = new Color[] { fireGlowColor, waterGlowColor, iceGlowColor, lightningGlowColor };
        fadeCoroutines = new Coroutine[4];

        bool has3DRenderers = fireRenderer != null || waterRenderer != null || iceRenderer != null || lightningRenderer != null;
        if (has3DRenderers)
        {
            InitializeRenderersToBlack();
        }

        // Chỉ tạo UI Icon 2D cũ nếu KHÔNG có Renderers 3D mới
        if (!has3DRenderers)
        {
            // Tạo root object cho UI độc lập với transform của đá (tránh bị Scale âm / Xoay của đá làm ngược chữ)
            uiRootObj = new GameObject("RockPuzzle_UIRoot");
            UpdateUIPosition();

            // Khởi tạo các SpriteRenderer hiển thị Icon
            bool hasAllIcons = fireIcon != null && waterIcon != null && iceIcon != null && lightningIcon != null;
            if (hasAllIcons)
            {
                CreateIconElements();
            }
            else
            {
                Debug.LogWarning("[ElementalRockPuzzle] Vui lòng gán đầy đủ 4 Sprite Icon nguyên tố trong Inspector để hiển thị.");
            }
        }

        if (startHidden)
        {
            isShown = false;

            // Tắt đệ quy tất cả GameObject con (ẩn hoàn toàn mesh, collider, v.v.)
            // GameObject cha (chính nó) vẫn active để tránh lỗi Netcode xóa đối tượng inactive
            SetChildrenActiveRecursive(transform, false);

            // Tắt Renderer và tất cả Colliders trên chính GameObject này (nếu có)
            var ren = GetComponent<Renderer>();
            if (ren != null) ren.enabled = false;
            foreach (var col in GetComponents<Collider>())
            {
                col.enabled = false;
            }

            // Ẩn UI Icon
            if (uiRootObj != null) uiRootObj.SetActive(false);

            Debug.Log($"[ElementalRockPuzzle] '{gameObject.name}' đã ẩn khi khởi tạo (startHidden = true). Số con: {transform.childCount}");
        }
        else
        {
            isShown = true;
            ResetPuzzle();
        }
        UpdateVisualStates();
    }

    public void ShowRock()
    {
        bool currentShown = IsNetworkActive ? netIsShown.Value : isShown;
        if (currentShown)
        {
            Debug.Log($"[ElementalRockPuzzle] '{gameObject.name}' đã được hiển thị rồi, bỏ qua ShowRock().");
            return;
        }

        if (IsNetworkActive)
        {
            if (IsServer)
            {
                netIsShown.Value = true;
            }
        }
        else
        {
            isShown = true;
        }

        ApplyShowRockVisuals();
    }

    private void ApplyShowRockVisuals()
    {
        // Bật active chính nó (phòng trường hợp bị tắt)
        gameObject.SetActive(true);

        // Kích hoạt đệ quy tất cả GameObject con (từ trên xuống để cha active trước con)
        SetChildrenActiveRecursive(transform, true);

        // Bật Renderer và tất cả Colliders trên chính nó
        var ren = GetComponent<Renderer>();
        if (ren != null) ren.enabled = true;
        foreach (var col in GetComponents<Collider>())
        {
            col.enabled = true;
        }

        // Bật tất cả Renderer con (kể cả vừa mới được kích hoạt lại)
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
        }

        // Bật tất cả Collider con
        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            c.enabled = true;
        }

        // Hiện UI Icon
        if (uiRootObj != null) uiRootObj.SetActive(true);

        bool has3DRenderers = fireRenderer != null || waterRenderer != null || iceRenderer != null || lightningRenderer != null;
        if (has3DRenderers)
        {
            InitializeRenderersToBlack();
        }
        
        ResetPuzzle();

        // Log chi tiết trạng thái collider sau khi hiển thị đá
        Collider selfCol = GetComponent<Collider>();
        Debug.Log($"[ElementalRockPuzzle] '{gameObject.name}' ApplyShowRockVisuals hoàn tất! " +
                  $"Colliders trên self: {GetComponents<Collider>().Length} | " +
                  $"Tổng collider con: {GetComponentsInChildren<Collider>(true).Length} | Số con: {transform.childCount}");
    }

    /// <summary>
    /// Kích hoạt hoặc tắt đệ quy tất cả GameObject con (từ trên xuống dưới).
    /// Hàm này hoạt động đúng cả khi GameObject con đang inactive.
    /// </summary>
    private void SetChildrenActiveRecursive(Transform parent, bool active)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            child.gameObject.SetActive(active);
            // Tiếp tục đệ quy vào con của con
            SetChildrenActiveRecursive(child, active);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsNetworkActive)
        {
            if (IsServer)
            {
                netIsShown.Value = !startHidden;
            }

            // Đăng ký sự kiện đồng bộ khi biến mạng thay đổi
            netCurrentStep.OnValueChanged += OnPuzzleStateChanged;
            netIsTimerRunning.OnValueChanged += OnTimerStateChanged;
            netIsShown.OnValueChanged += OnIsShownChanged;

            // Lấy trạng thái ban đầu của mạng
            currentStep = netCurrentStep.Value;
            isTimerRunning = netIsTimerRunning.Value;
            if (isTimerRunning)
            {
                timeRemaining = netTimeRemaining.Value;
            }

            // Đồng bộ hiển thị ban đầu nếu Server đã kích hoạt hiển thị đá
            if (netIsShown.Value)
            {
                ApplyShowRockVisuals();
            }
            else
            {
                UpdateVisualStates();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsNetworkActive)
        {
            netCurrentStep.OnValueChanged -= OnPuzzleStateChanged;
            netIsTimerRunning.OnValueChanged -= OnTimerStateChanged;
            netIsShown.OnValueChanged -= OnIsShownChanged;
        }
    }

    private void FixedUpdate()
    {
        // Chỉ quét khi đá đang hiển thị
        if (!IsShown) return;

        // Tính tâm và bán kính quét dựa trên kích thước của đá
        Vector3 scanCenter = transform.position;
        float scanRadius = 2.0f; // Bán kính quét mặc định

        BoxCollider boxCol = GetComponent<BoxCollider>();
        SphereCollider sphereCol = GetComponent<SphereCollider>();

        if (boxCol != null)
        {
            scanCenter = transform.TransformPoint(boxCol.center);
            scanRadius = Mathf.Max(boxCol.size.x, Mathf.Max(boxCol.size.y, boxCol.size.z)) * 0.7f;
        }
        else if (sphereCol != null)
        {
            scanCenter = transform.TransformPoint(sphereCol.center);
            scanRadius = sphereCol.radius;
        }

        // Áp dụng scale của Transform cho bán kính quét
        float maxScale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        scanRadius *= maxScale;
        
        // Thêm khoảng đệm quét để bắt đạn trước khi bay xuyên qua hoặc nếu đạn di chuyển nhanh
        scanRadius += 0.8f; 

        // Quét tất cả các collider trong vùng (bao gồm cả Trigger)
        Collider[] hits = Physics.OverlapSphere(scanCenter, scanRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCol = hits[i];
            if (hitCol == null || hitCol.gameObject == gameObject) continue;

            // Tìm root hoặc đối tượng đạn có tag phù hợp
            GameObject hitObj = hitCol.gameObject;
            string hitTag = hitObj.tag;

            // Debug log every object found by the sweep
            if (hitObj.name.Contains("Fireball") || hitObj.name.Contains("Lua") || hitObj.tag == "Lua")
            {
                Debug.Log($"[ElementalRockPuzzle Debug] Sweep found: '{hitObj.name}' | tag='{hitTag}' | layer={LayerMask.LayerToName(hitObj.layer)} | distance={Vector3.Distance(scanCenter, hitCol.transform.position):F2}m (scanRadius={scanRadius:F2}m)");
            }

            // Kiểm tra xem tag trực tiếp có phải nguyên tố mong muốn
            bool isElement = hitTag == fireTag || hitTag == waterTag || hitTag == iceTag || hitTag == lightningTag;
            
            // Nếu không, kiểm tra cha
            if (!isElement && hitObj.transform.parent != null)
            {
                hitObj = hitObj.transform.parent.gameObject;
                hitTag = hitObj.tag;
                isElement = hitTag == fireTag || hitTag == waterTag || hitTag == iceTag || hitTag == lightningTag;
            }

            // Nếu đúng là đạn nguyên tố
            if (isElement)
            {
                float dist = Vector3.Distance(scanCenter, hitCol.transform.position);
                if (dist <= scanRadius)
                {
                    Debug.Log($"[ElementalRockPuzzle] Quét FixedUpdate phát hiện đạn: '{hitObj.name}' (tag='{hitTag}') ở khoảng cách {dist:F2}m (Bán kính quét: {scanRadius:F2}m)");
                    HandleElementHit(hitCol.gameObject);
                }
            }
        }
    }

    private void Update()
    {
        bool has3DRenderers = fireRenderer != null || waterRenderer != null || iceRenderer != null || lightningRenderer != null;

        // Cập nhật vị trí UI bám theo viên đá mỗi frame
        if (!has3DRenderers)
        {
            UpdateUIPosition();
        }

        // Client đếm ngược cục bộ để mượt mà UI
        if (isTimerRunning)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                // Chỉ Server (hoặc ở chế độ Offline) mới có quyền ra lệnh reset khi hết giờ
                if (!IsNetworkActive || IsServer)
                {
                    ResetPuzzle();
                }
            }
            UpdateVisualStates();
        }

        // Cập nhật khoảng cách ngang và kích thước của các Icon tương ứng lúc Play (Editor)
        #if UNITY_EDITOR
        if (!has3DRenderers)
        {
            UpdateEditorRealtimeUI();
        }
        #endif

        // Tạo hiệu ứng nhấp nháy/phóng to thu nhỏ cho Icon hiện tại cần bắn
        if (!has3DRenderers && iconObjects != null && iconObjects.Length == 4)
        {
            AnimateCurrentIcon();
        }

        // Tự động xoay hàng Icon về hướng Camera chính (Billboarding)
        if (!has3DRenderers && faceCamera)
        {
            BillboardUI();
        }
    }

    private void OnDestroy()
    {
        // Vì uiRootObj không còn là con của viên đá, chúng ta cần chủ động xóa nó khi viên đá bị hủy
        if (uiRootObj != null)
        {
            Destroy(uiRootObj);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[ElementalRockPuzzle] OnTriggerEnter: '{other.gameObject.name}' (tag='{other.gameObject.tag}', layer={LayerMask.LayerToName(other.gameObject.layer)})");
        HandleElementHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[ElementalRockPuzzle] OnCollisionEnter: '{collision.gameObject.name}' (tag='{collision.gameObject.tag}', layer={LayerMask.LayerToName(collision.gameObject.layer)})");
        HandleElementHit(collision.gameObject);
    }

    private void HandleElementHit(GameObject hitObj)
    {
        if (hitObj == null) return;

        // Tìm NetworkObject trên đối tượng va chạm hoặc cha của nó để lấy đúng Tag và ID gốc của đạn
        NetworkObject netObj = hitObj.GetComponentInParent<NetworkObject>();
        GameObject rootObj = null;
        if (netObj != null)
        {
            rootObj = netObj.gameObject;
        }
        else
        {
            // Nếu không có NetworkObject, tìm component projectile trên chính nó hoặc cha để lấy root chính xác
            Component projComp = (Component)hitObj.GetComponentInParent<ArthurFireProjectile>() ??
                                 (Component)hitObj.GetComponentInParent<MayaWaterProjectile>() ??
                                 (Component)hitObj.GetComponentInParent<ElenaIceProjectile>() ??
                                 (Component)hitObj.GetComponentInParent<LeoLightningProjectile>();
            rootObj = projComp != null ? projComp.gameObject : hitObj;
        }

        // Tránh nhận liên tiếp nhiều sự kiện va chạm từ cùng một viên đạn
        if (rootObj == lastHitObject) return;

        string hitTag = rootObj.tag;
        int activeStep = IsNetworkActive ? netCurrentStep.Value : currentStep;
        string expectedTag = (orderedTags != null && activeStep >= 0 && activeStep < orderedTags.Length) ? orderedTags[activeStep] : "UNKNOWN";

        Debug.Log($"[ElementalRockPuzzle] VA CHẠM: Đối tượng va chạm='{hitObj.name}', Root='{rootObj.name}', Tag='{hitTag}', Cần nguyên tố='{expectedTag}' (Bước: {activeStep})");

        // Kiểm tra xem đối tượng va chạm có tag thuộc một trong các nguyên tố không
        if (hitTag == fireTag || hitTag == waterTag || hitTag == iceTag || hitTag == lightningTag)
        {
            // Kiểm tra ID mạng để tránh double hit giữa Client RPC và Server local collision
            if (netObj != null)
            {
                if (netObj.NetworkObjectId == lastProcessedProjectileId)
                {
                    Debug.Log($"[ElementalRockPuzzle] Bỏ qua đạn ID {netObj.NetworkObjectId} vì đã được xử lý trước đó.");
                    return;
                }
                lastProcessedProjectileId = netObj.NetworkObjectId;
            }

            lastHitObject = rootObj;

            // VÔ HIỆU HÓA va chạm của đạn ngay lập tức trên cả các bộ phận con của nó
            Collider[] colliders = rootObj.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
            
            // Dừng vận tốc vật lý
            Rigidbody projectileRigidbody = rootObj.GetComponent<Rigidbody>();
            if (projectileRigidbody == null)
            {
                projectileRigidbody = rootObj.GetComponentInChildren<Rigidbody>();
            }
            if (projectileRigidbody != null)
            {
                if (!projectileRigidbody.isKinematic)
                {
                    projectileRigidbody.linearVelocity = Vector3.zero;
                    projectileRigidbody.angularVelocity = Vector3.zero;
                }
                projectileRigidbody.isKinematic = true;
            }

            // Gọi các logic nổ/ẩn của đạn ngay lập tức trên cả Client/Server để đạn dừng di chuyển cục bộ
            if (rootObj.GetComponent<ElenaIceProjectile>() != null || 
                rootObj.GetComponent<MayaWaterProjectile>() != null ||
                rootObj.GetComponent<ArthurFireProjectile>() != null)
            {
                rootObj.SendMessage("HandleHitImpact", SendMessageOptions.DontRequireReceiver);
            }
            else if (rootObj.GetComponent<LeoLightningProjectile>() != null)
            {
                // AOE Sét tự quản lý lifetime của mình, không cần despawn ngay
                Debug.Log($"[ElementalRockPuzzle] Sét AOE chạm đá, không cần despawn.");
            }
            else
            {
                rootObj.SendMessage("DespawnOrDestroy", SendMessageOptions.DontRequireReceiver);
            }

            if (IsNetworkActive)
            {
                if (IsServer)
                {
                    // Nếu là Server/Host, tự xử lý trực tiếp luôn
                    ProcessElementHit(hitTag);
                }
                else
                {
                    // Nếu là Client, gửi RPC lên Server để xử lý kèm theo ulong NetworkObjectId
                    SubmitElementHitServerRpc(hitTag, netObj != null ? netObj.NetworkObjectId : 0);
                }
            }
            else
            {
                // Nếu chơi offline, tự xử lý cục bộ
                ProcessElementHit(hitTag);
            }
        }
    }

    /// <summary>
    /// Cho phép các đạn nguyên tố chủ động thông báo va chạm trực tiếp để tránh lỗi mất sự kiện va chạm vật lý (race condition)
    /// </summary>
    public void NotifyElementHit(GameObject projectileObj)
    {
        HandleElementHit(projectileObj);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitElementHitServerRpc(string hitTag, ulong projectileId)
    {
        // Tránh xử lý trùng lặp nếu Server đã tự phát hiện va chạm cục bộ trước đó
        if (projectileId != 0)
        {
            if (projectileId == lastProcessedProjectileId)
            {
                Debug.Log($"[ElementalRockPuzzle] ServerRpc: Bỏ qua đạn ID {projectileId} vì đã được xử lý trước đó.");
                return;
            }
            lastProcessedProjectileId = projectileId;
        }

        // Tìm và tắt va chạm của đạn trên Server nếu nó vẫn đang tồn tại
        if (projectileId != 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(projectileId, out var netObj))
        {
            if (netObj != null)
            {
                GameObject hitObj = netObj.gameObject;
                Collider[] colliders = hitObj.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    col.enabled = false;
                }

                Rigidbody projectileRigidbody = hitObj.GetComponent<Rigidbody>();
                if (projectileRigidbody != null)
                {
                    if (!projectileRigidbody.isKinematic)
                    {
                        projectileRigidbody.linearVelocity = Vector3.zero;
                        projectileRigidbody.angularVelocity = Vector3.zero;
                    }
                    projectileRigidbody.isKinematic = true;
                }

                if (hitObj.GetComponent<ElenaIceProjectile>() != null || 
                    hitObj.GetComponent<MayaWaterProjectile>() != null ||
                    hitObj.GetComponent<ArthurFireProjectile>() != null)
                {
                    hitObj.SendMessage("HandleHitImpact", SendMessageOptions.DontRequireReceiver);
                }
                else if (hitObj.GetComponent<LeoLightningProjectile>() == null)
                {
                    hitObj.SendMessage("DespawnOrDestroy", SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        // Gọi xử lý nguyên tố
        ProcessElementHit(hitTag);
    }

    private void ProcessElementHit(string hitTag)
    {
        int activeStep = IsNetworkActive ? netCurrentStep.Value : currentStep;
        
        // Tránh lỗi vượt quá chỉ mục của mảng
        if (activeStep < 0 || activeStep >= orderedTags.Length) return;

        string expectedTag = orderedTags[activeStep];

        if (hitTag == expectedTag)
        {
            Debug.Log($"[ElementalRockPuzzle] ĐÚNG nguyên tố! Nhận được: '{hitTag}' == Mong đợi: '{expectedTag}'. Tiến lên bước {activeStep + 1}");
            // Đúng nguyên tố tiếp theo
            if (activeStep == 0)
            {
                // Bắt đầu đếm ngược
                if (IsNetworkActive)
                {
                    netTimeRemaining.Value = timeLimit;
                    netIsTimerRunning.Value = true;
                }
                else
                {
                    timeRemaining = timeLimit;
                    isTimerRunning = true;
                }
            }

            if (IsNetworkActive)
            {
                netCurrentStep.Value++;
                if (netCurrentStep.Value >= orderedTags.Length)
                {
                    ShatterRock();
                }
            }
            else
            {
                currentStep++;
                UpdateVisualStates();
                if (currentStep >= orderedTags.Length)
                {
                    ShatterRock();
                }
            }
        }
        else
        {
            // Kiểm tra xem nguyên tố này có phải của bước ĐÃ HOÀN THÀNH trước đó không
            bool isAlreadyCompleted = false;
            for (int i = 0; i < activeStep; i++)
            {
                if (hitTag == orderedTags[i])
                {
                    isAlreadyCompleted = true;
                    break;
                }
            }

            if (isAlreadyCompleted)
            {
                Debug.Log($"[ElementalRockPuzzle] Nhận nguyên tố đã hoàn thành trước đó: '{hitTag}' (Bước hiện tại: {activeStep}). Bỏ qua để tránh reset do double-hit/lag.");
                return;
            }

            Debug.LogWarning($"[ElementalRockPuzzle] SAI nguyên tố! Nhận được: '{hitTag}', Mong đợi: '{expectedTag}'. Reset câu đố!");
            // Sai nguyên tố -> Reset câu đố
            ResetPuzzle();
        }
    }

    private void UpdateUIPosition()
    {
        if (uiRootObj == null) return;
        uiRootObj.transform.position = transform.TransformPoint(CalculateUIPosition());
    }

    private Vector3 CalculateUIPosition()
    {
        if (uiPivot != null)
        {
            return transform.InverseTransformPoint(uiPivot.position);
        }

        // Tự động tìm đỉnh của mesh hoặc collider
        float calculatedHeight = offsetHeight;
        Renderer ren = GetComponent<Renderer>();
        Collider col = GetComponent<Collider>();

        if (ren != null)
        {
            calculatedHeight = (ren.bounds.max.y - transform.position.y) + 0.5f;
        }
        else if (col != null)
        {
            calculatedHeight = (col.bounds.max.y - transform.position.y) + 0.5f;
        }

        return new Vector3(0f, calculatedHeight, 0f);
    }

    private void CreateIconElements()
    {
        iconObjects = new GameObject[4];
        iconRenderers = new SpriteRenderer[4];
        Sprite[] sprites = new Sprite[] { fireIcon, waterIcon, iceIcon, lightningIcon };

        for (int i = 0; i < 4; i++)
        {
            GameObject iconObj = new GameObject($"Icon_{i}");
            iconObj.transform.SetParent(uiRootObj.transform);
            
            // Xếp hàng ngang đối xứng qua gốc tọa độ của UI root
            float xPos = (i - 1.5f) * iconSpacing;
            iconObj.transform.localPosition = new Vector3(xPos, 0f, 0f);
            iconObj.transform.localScale = Vector3.one * iconScale;

            SpriteRenderer sr = iconObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[i];
            
            iconObjects[i] = iconObj;
            iconRenderers[i] = sr;
        }
    }

    private void UpdateVisualStates()
    {
        int activeStep = IsNetworkActive ? netCurrentStep.Value : currentStep;
        currentStep = activeStep;

        bool has3DRenderers = fireRenderer != null || waterRenderer != null || iceRenderer != null || lightningRenderer != null;

        if (has3DRenderers)
        {
            if (elementRenderers == null || elementRenderers.Length != 4) return;
            for (int i = 0; i < 4; i++)
            {
                if (elementRenderers[i] == null) continue;
                bool shouldBeActive = i < activeStep;
                if (fadeCoroutines[i] != null)
                {
                    StopCoroutine(fadeCoroutines[i]);
                }
                fadeCoroutines[i] = StartCoroutine(FadeElement(i, shouldBeActive));
            }
        }
        else
        {
            // Cập nhật giao diện hình ảnh của 4 Icon
            if (iconRenderers != null && iconRenderers.Length == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (iconRenderers[i] == null) continue;

                    if (i < activeStep)
                    {
                        // Các bước đã hoàn thành: Sáng rõ (Full màu)
                        iconRenderers[i].color = Color.white;
                        if (iconObjects[i] != null && (i != activeStep || !isTimerRunning))
                        {
                            iconObjects[i].transform.localScale = Vector3.one * iconScale;
                        }
                    }
                    else if (i == activeStep)
                    {
                        // Bước hiện tại cần bắn: Sáng rõ
                        iconRenderers[i].color = Color.white;
                    }
                    else
                    {
                        // Các bước chưa tới lượt: Làm mờ/Tối đi
                        iconRenderers[i].color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
                        if (iconObjects[i] != null)
                        {
                            iconObjects[i].transform.localScale = Vector3.one * iconScale;
                        }
                    }
                }
            }
        }
    }

    private void InitializeRenderersToBlack()
    {
        if (elementRenderers == null) return;
        for (int i = 0; i < elementRenderers.Length; i++)
        {
            if (elementRenderers[i] == null) continue;
            Material mat = elementRenderers[i].material;
            if (mat != null)
            {
                mat.SetColor("_Color", Color.black);
                mat.SetColor("_EmissionColor", Color.black);
                mat.EnableKeyword("_EMISSION");
            }
        }
    }

    private IEnumerator FadeElement(int index, bool fadeIn)
    {
        Renderer ren = elementRenderers[index];
        if (ren == null) yield break;

        Material mat = ren.material;
        Color startAlbedo = mat.GetColor("_Color");
        Color startEmission = mat.GetColor("_EmissionColor");

        Color targetAlbedo = fadeIn ? Color.white : Color.black;
        Color targetEmission = fadeIn ? elementGlowColors[index] : Color.black;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * fadeSpeed;
            mat.SetColor("_Color", Color.Lerp(startAlbedo, targetAlbedo, t));
            mat.SetColor("_EmissionColor", Color.Lerp(startEmission, targetEmission, t));
            yield return null;
        }

        mat.SetColor("_Color", targetAlbedo);
        mat.SetColor("_EmissionColor", targetEmission);
    }

    private void AnimateCurrentIcon()
    {
        if (currentStep >= 0 && currentStep < 4)
        {
            GameObject currentIconObj = iconObjects[currentStep];
            if (currentIconObj != null)
            {
                // Hiệu ứng nhịp tim (pulsing) nhẹ để thu hút sự chú ý
                float pulse = 1f + Mathf.PingPong(Time.time * 2.5f, 0.2f);
                currentIconObj.transform.localScale = Vector3.one * iconScale * pulse;
            }
        }
    }

    private void BillboardUI()
    {
        Camera cam = Camera.main;
        
        if (uiRootObj != null)
        {
            if (faceCamera && cam != null)
            {
                // Xoay về phía Camera + 180 độ + góc xoay bù thêm (offset)
                uiRootObj.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(uiRotationOffset);
            }
            else
            {
                // Nếu không xoay theo camera, lấy xoay của đá làm gốc + góc xoay bù thêm
                uiRootObj.transform.rotation = transform.rotation * Quaternion.Euler(uiRotationOffset);
            }
        }
    }

    private void UpdateEditorRealtimeUI()
    {
        if (uiRootObj != null)
        {
            // Cập nhật khoảng cách ngang và kích thước của các Icon tương ứng
            if (iconObjects != null && iconObjects.Length == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (iconObjects[i] != null)
                    {
                        float xPos = (i - 1.5f) * iconSpacing;
                        iconObjects[i].transform.localPosition = new Vector3(xPos, 0f, 0f);

                        // Chỉ áp dụng tỉ lệ mặc định khi icon không bị hiệu ứng phóng to (pulse) lúc đếm ngược
                        if (i != currentStep || !isTimerRunning)
                        {
                            iconObjects[i].transform.localScale = Vector3.one * iconScale;
                        }
                    }
                }
            }
        }
    }

    // --- CÁC HÀM PHẢN HỒI KHI BIẾN MẠNG THAY ĐỔI ---
    private void OnPuzzleStateChanged(int oldVal, int newVal)
    {
        Debug.Log($"[ElementalRockPuzzle Debug] OnPuzzleStateChanged: old={oldVal} => new={newVal}");
        currentStep = newVal;
        UpdateVisualStates();
    }

    private void OnTimerStateChanged(bool oldVal, bool newVal)
    {
        isTimerRunning = newVal;
        if (newVal)
        {
            timeRemaining = netTimeRemaining.Value;
        }
        else
        {
            timeRemaining = 0f;
        }
        UpdateVisualStates();
    }

    private void OnIsShownChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            ApplyShowRockVisuals();
        }
    }

    private void ResetPuzzle()
    {
        if (IsNetworkActive && IsServer)
        {
            netCurrentStep.Value = 0;
            netTimeRemaining.Value = 0f;
            netIsTimerRunning.Value = false;
        }
        else if (!IsNetworkActive)
        {
            currentStep = 0;
            timeRemaining = 0f;
            isTimerRunning = false;
        }
        lastHitObject = null; // Luôn luôn reset lastHitObject khi reset câu đố
        UpdateVisualStates();
    }

    private void ShatterRock()
    {
        Debug.Log("[ElementalRockPuzzle] Kích hoạt thành công cả 4 nguyên tố theo đúng thứ tự! Đá đã bị phá vỡ.");
        
        if (IsNetworkActive)
        {
            if (IsServer)
            {
                // Nếu có NetworkObject và đang chạy server, gọi Despawn để hủy đồng bộ cho mọi người
                if (TryGetComponent<NetworkObject>(out var netObj) && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
        else
        {
            // Offline/Standalone
            Destroy(gameObject);
        }
    }

    // --- VẼ GIZMOS PHỤC VỤ CĂN CHỈNH Ở CHẾ ĐỘ EDIT MODE ---
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 localUIPos = CalculateUIPosition();
        Vector3 worldUIPos = transform.TransformPoint(localUIPos);

        // Vẽ đường nối từ tâm viên đá lên vị trí UI
        Gizmos.DrawLine(transform.position, worldUIPos);
        Gizmos.DrawWireSphere(worldUIPos, 0.2f);

        // Vẽ các vòng tròn tượng trưng cho vị trí của 4 Icon nguyên tố
        for (int i = 0; i < 4; i++)
        {
            float xOffset = (i - 1.5f) * iconSpacing;
            Vector3 localIconOffset = new Vector3(xOffset, 0f, 0f);
            Vector3 worldIconPos = worldUIPos + transform.TransformDirection(localIconOffset);
            
            Gizmos.DrawWireSphere(worldIconPos, 0.15f * (iconScale / 0.5f));
        }
    }
}
