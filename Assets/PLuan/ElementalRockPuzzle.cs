using UnityEngine;
using Unity.Netcode;

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

    // Cache các đối tượng sinh ra để quản lý UI
    private GameObject uiRootObj;
    private GameObject[] iconObjects;
    private SpriteRenderer[] iconRenderers;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private void Start()
    {
        orderedTags = new string[] { fireTag, waterTag, iceTag, lightningTag };

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

        ResetPuzzle();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsNetworkActive)
        {
            // Đăng ký sự kiện đồng bộ khi biến mạng thay đổi
            netCurrentStep.OnValueChanged += OnPuzzleStateChanged;
            netIsTimerRunning.OnValueChanged += OnTimerStateChanged;

            // Lấy trạng thái ban đầu của mạng
            currentStep = netCurrentStep.Value;
            isTimerRunning = netIsTimerRunning.Value;
            if (isTimerRunning)
            {
                timeRemaining = netTimeRemaining.Value;
            }

            UpdateVisualStates();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsNetworkActive)
        {
            netCurrentStep.OnValueChanged -= OnPuzzleStateChanged;
            netIsTimerRunning.OnValueChanged -= OnTimerStateChanged;
        }
    }

    private void Update()
    {
        // Cập nhật vị trí UI bám theo viên đá mỗi frame
        UpdateUIPosition();

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
        UpdateEditorRealtimeUI();
        #endif

        // Tạo hiệu ứng nhấp nháy/phóng to thu nhỏ cho Icon hiện tại cần bắn
        if (iconObjects != null && iconObjects.Length == 4)
        {
            AnimateCurrentIcon();
        }

        // Tự động xoay hàng Icon về hướng Camera chính (Billboarding)
        if (faceCamera)
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
        HandleElementHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleElementHit(collision.gameObject);
    }

    private void HandleElementHit(GameObject hitObj)
    {
        if (hitObj == null) return;

        // Tránh nhận liên tiếp nhiều sự kiện va chạm từ cùng một viên đạn
        if (hitObj == lastHitObject) return;

        string hitTag = hitObj.tag;

        // Kiểm tra xem đối tượng va chạm có tag thuộc một trong các nguyên tố không
        if (hitTag == fireTag || hitTag == waterTag || hitTag == iceTag || hitTag == lightningTag)
        {
            lastHitObject = hitObj;

            if (IsNetworkActive)
            {
                // Nếu đang chơi mạng, gửi RPC để Server kiểm tra và đồng bộ
                SubmitElementHitServerRpc(hitTag);
            }
            else
            {
                // Nếu chơi offline, tự xử lý cục bộ
                ProcessElementHit(hitTag);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitElementHitServerRpc(string hitTag)
    {
        ProcessElementHit(hitTag);
    }

    private void ProcessElementHit(string hitTag)
    {
        int activeStep = IsNetworkActive ? netCurrentStep.Value : currentStep;
        string expectedTag = orderedTags[activeStep];

        if (hitTag == expectedTag)
        {
            // Đúng nguyên tố tiếp theo
            if (activeStep == 0)
            {
                // Bắt đầu đếm ngược
                if (IsNetworkActive)
                {
                    netIsTimerRunning.Value = true;
                    netTimeRemaining.Value = timeLimit;
                }
                else
                {
                    isTimerRunning = true;
                    timeRemaining = timeLimit;
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
        // Cập nhật giao diện hình ảnh của 4 Icon
        if (iconRenderers != null && iconRenderers.Length == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                if (iconRenderers[i] == null) continue;

                if (i < currentStep)
                {
                    // Các bước đã hoàn thành: Sáng rõ (Full màu)
                    iconRenderers[i].color = Color.white;
                    if (iconObjects[i] != null && (i != currentStep || !isTimerRunning))
                    {
                        iconObjects[i].transform.localScale = Vector3.one * iconScale;
                    }
                }
                else if (i == currentStep)
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
        currentStep = newVal;
        lastHitObject = null; // Reset đạn khi chuyển bước
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
            lastHitObject = null;
            UpdateVisualStates();
        }
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
