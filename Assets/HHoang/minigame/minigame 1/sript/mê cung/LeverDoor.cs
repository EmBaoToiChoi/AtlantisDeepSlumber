using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class LeverDoor : NetworkBehaviour
{
    // Danh sách toàn cục lưu tất cả các Cần gạt trong game
    public static List<LeverDoor> allLevers = new List<LeverDoor>();
    
    // Chống lỗi cửa di chuyển thừa khi có nhiều cần gạt cùng điều khiển 1 cửa
    private static int lastFrameCount = -1;
    private static HashSet<Transform> updatedDoorsThisFrame = new HashSet<Transform>();

    [Header("--- DANH SÁCH CÁC CỬA ---")]
    public List<DoorConfig> doors = new List<DoorConfig>();

    [Header("--- CẤU HÌNH CẦN GẠT (LEVER) ---")]
    [Tooltip("Kéo object tay cầm cần gạt (Lever) vào đây")]
    public Transform leverHandle; 
    
    [Tooltip("Góc xoay cộng thêm khi giữ F (Ví dụ: X = 90 nghĩa là quay hạ xuống 90 độ)")]
    public Vector3 pulledRotationOffset = new Vector3(90f, 0f, 0f); 
    
    [Tooltip("Tốc độ quay của cần gạt (độ/giây)")]
    public float leverRotateSpeed = 180f; 

    [Header("--- TƯƠNG TÁC PHÍM ---")]
    [Tooltip("Phím cần giữ để gạt cần (Mặc định là F)")]
    public KeyCode interactKey = KeyCode.F;

    [Tooltip("Thông báo UI Toolkit hiển thị ở phía trên giữa màn hình")]
    public string promptMessage = "Giữ F để mở cửa mê cung";
    
    [Tooltip("(Tuỳ chọn) Kéo UI hiển thị gợi ý 'Giữ F' vào đây")]
    public GameObject interactUI; 

    [Header("--- HIỆU ỨNG NỔI BẬT CẦN GẠT (GLOW VFX) ---")]
    [Tooltip("Bật hiệu ứng ánh sáng dịu phát ra từ cần gạt khi gạt xuống")]
    public bool enableGlowEffect = true;

    [Tooltip("Màu quầng sáng dịu khi gạt (Mặc định: Vàng hổ phách / Warm Gold)")]
    public Color glowColor = new Color(1f, 0.8f, 0.35f, 1f);

    [Tooltip("Cường độ ánh sáng tối đa khi gạt cần")]
    public float maxLightIntensity = 2.5f;

    [Tooltip("Bán kính mờ dần xung quanh cần gạt (mét)")]
    public float lightRange = 2.5f;

    [Tooltip("(Tuỳ chọn) PointLight gắn trên cần gạt (Nếu để trống sẽ tự động khởi tạo)")]
    public Light leverGlowLight;

    [Tooltip("(Tuỳ chọn) Kéo Prefab VFX quầng sáng / hạt hiệu ứng tại đây nếu có")]
    public GameObject leverActiveVfxPrefab; 

    [Header("--- ÂM THANH (TUỲ CHỌN) ---")]
    public AudioSource leverAudioSource;
    public AudioClip pullSound;

    [Header("--- TRẠNG THÁI (DEBUG THEO DÕI) ---")]
    public bool isPlayerNearbyDebug = false;
    public bool isPressedDebug = false;

    // Trạng thái mạng: true = đang gạt xuống / mở cửa, false = nhả ra / đóng cửa
    public NetworkVariable<bool> isPressed = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private bool localIsPressed = false;
    private Quaternion initialLeverLocalRot;
    private bool isPlayerNearby = false;
    private bool lastSentState = false;
    private bool isInitialized = false;
    private bool isPromptShowing = false;
    private float currentLightIntensity = 0f;
    private GameObject activeVfxInstance;

    // Thuộc tính kiểm tra trạng thái cần gạt (tự động tương thích cả khi chơi Offline thử nghiệm hoặc Online Netcode)
    public bool IsLeverPressed
    {
        get
        {
            if (IsSpawned) return isPressed.Value;
            return localIsPressed;
        }
    }

    void Awake()
    {
        if (!allLevers.Contains(this)) allLevers.Add(this);
    }

    void OnDisable()
    {
        if (isPromptShowing)
        {
            isPromptShowing = false;
            PlayerHUDController hud = PlayerHUDController.Instance != null 
                ? PlayerHUDController.Instance 
                : FindFirstObjectByType<PlayerHUDController>();
            if (hud != null) hud.ShowInteractionPrompt(false, "");
        }
    }

    void OnDestroy()
    {
        if (isPromptShowing)
        {
            isPromptShowing = false;
            PlayerHUDController hud = PlayerHUDController.Instance != null 
                ? PlayerHUDController.Instance 
                : FindFirstObjectByType<PlayerHUDController>();
            if (hud != null) hud.ShowInteractionPrompt(false, "");
        }
        allLevers.Remove(this);
    }

    void Start()
    {
        foreach (var door in doors)
        {
            if (door != null && door.doorTransform != null)
            {
                door.initialPos = door.doorTransform.localPosition;
            }
        }

        if (leverHandle != null)
        {
            // Tắt Animator và AN_Button có sẵn của Asset Pack để không bị khóa góc xoay tay cầm
            Animator anim = leverHandle.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            AN_Button oldButton = leverHandle.GetComponent<AN_Button>();
            if (oldButton != null) oldButton.enabled = false;

            initialLeverLocalRot = leverHandle.localRotation;
        }

        if (interactUI != null) interactUI.SetActive(false);

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        // 1. Kiểm tra phím F khi Player ở gần cần gạt
        if (isPlayerNearby)
        {
            bool holdingF = Input.GetKey(interactKey);
            if (holdingF != lastSentState)
            {
                lastSentState = holdingF;
                SetPressedState(holdingF);
            }
        }

        // Cập nhật biến Debug xem trong Inspector
        isPlayerNearbyDebug = isPlayerNearby;
        isPressedDebug = IsLeverPressed;

        // Cập nhật UI Toolkit gợi ý tương tác
        UpdatePromptUI();

        // Reset bộ đếm chống lặp cửa mỗi frame
        if (Time.frameCount != lastFrameCount)
        {
            updatedDoorsThisFrame.Clear();
            lastFrameCount = Time.frameCount;
        }

        // 2. Xử lý di chuyển CỬA
        foreach (var door in doors)
        {
            if (door != null && door.doorTransform != null)
            {
                if (updatedDoorsThisFrame.Contains(door.doorTransform)) continue;

                bool shouldOpenDoor = false;

                // Kiểm tra tất cả Cần gạt trong game
                foreach (var lever in allLevers)
                {
                    if (lever != null && lever.IsLeverPressed)
                    {
                        foreach (var pDoor in lever.doors)
                        {
                            if (pDoor != null && pDoor.doorTransform == door.doorTransform)
                            {
                                shouldOpenDoor = true;
                                break;
                            }
                        }
                    }
                    if (shouldOpenDoor) break;
                }

                // Kiểm tra thêm các Nút đạp phiến đá (PressurePlateDoor) nếu có
                if (!shouldOpenDoor && PressurePlateDoor.allPlates != null)
                {
                    foreach (var plate in PressurePlateDoor.allPlates)
                    {
                        if (plate != null && plate.isPressed.Value)
                        {
                            foreach (var pDoor in plate.doors)
                            {
                                if (pDoor != null && pDoor.doorTransform == door.doorTransform)
                                {
                                    shouldOpenDoor = true;
                                    break;
                                }
                            }
                        }
                        if (shouldOpenDoor) break;
                    }
                }

                Vector3 targetDoorPos = shouldOpenDoor ? (door.initialPos + door.slideOffset) : door.initialPos;
                bool isMoving = Vector3.Distance(door.doorTransform.localPosition, targetDoorPos) > 0.001f;

                door.doorTransform.localPosition = Vector3.MoveTowards(
                    door.doorTransform.localPosition, 
                    targetDoorPos, 
                    door.doorSpeed * Time.deltaTime
                );

                if (door.slideAudioSource != null)
                {
                    if (isMoving && !door.slideAudioSource.isPlaying) door.slideAudioSource.Play();
                    else if (!isMoving && door.slideAudioSource.isPlaying) door.slideAudioSource.Stop();
                }

                updatedDoorsThisFrame.Add(door.doorTransform);
            }
        }

        // 3. Xử lý xoay CẦN GẠT (Lever Handle)
        if (leverHandle != null)
        {
            Quaternion targetLeverRot = IsLeverPressed 
                ? (initialLeverLocalRot * Quaternion.Euler(pulledRotationOffset)) 
                : initialLeverLocalRot;

            leverHandle.localRotation = Quaternion.RotateTowards(
                leverHandle.localRotation, 
                targetLeverRot, 
                leverRotateSpeed * Time.deltaTime
            );
        }

        // 4. Xử lý hiệu ứng ánh sáng dịu & VFX nổi bật cần gạt
        UpdateGlowEffect();
    }

    private void SetPressedState(bool pressed)
    {
        if (IsSpawned)
        {
            if (IsServer)
            {
                isPressed.Value = pressed;
            }
            else
            {
                SetPressedServerRpc(pressed);
            }
        }
        else
        {
            // Nếu chạy thử trực tiếp trong Editor không qua NetworkManager Spawn
            localIsPressed = pressed;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPressedServerRpc(bool pressed)
    {
        isPressed.Value = pressed;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.transform.root != null && other.transform.root.CompareTag("Player")) return true;
        if (other.GetComponentInParent<CharacterController>() != null) return true;
        return false;
    }

    private void UpdateGlowEffect()
    {
        if (!enableGlowEffect || leverHandle == null) return;

        bool isPressed = IsLeverPressed;

        // Tự động khởi tạo PointLight nếu chưa có
        if (leverGlowLight == null)
        {
            Transform existingLight = leverHandle.Find("LeverAutoGlowLight");
            if (existingLight != null)
            {
                leverGlowLight = existingLight.GetComponent<Light>();
            }
            else
            {
                GameObject lightObj = new GameObject("LeverAutoGlowLight");
                lightObj.transform.SetParent(leverHandle);
                lightObj.transform.localPosition = Vector3.zero;
                leverGlowLight = lightObj.AddComponent<Light>();
                leverGlowLight.type = LightType.Point;
                leverGlowLight.range = lightRange;
                leverGlowLight.color = glowColor;
                leverGlowLight.intensity = 0f;
                leverGlowLight.enabled = false;
            }
        }

        // Lerp mượt mà cường độ sáng
        float targetIntensity = isPressed ? maxLightIntensity : 0f;
        currentLightIntensity = Mathf.MoveTowards(currentLightIntensity, targetIntensity, maxLightIntensity * 3.5f * Time.deltaTime);

        if (leverGlowLight != null)
        {
            leverGlowLight.color = glowColor;
            leverGlowLight.range = lightRange;
            leverGlowLight.intensity = currentLightIntensity;
            leverGlowLight.enabled = currentLightIntensity > 0.01f;
        }

        // Kích hoạt Prefab VFX bổ sung nếu được gán
        if (leverActiveVfxPrefab != null)
        {
            if (isPressed && activeVfxInstance == null)
            {
                activeVfxInstance = Instantiate(leverActiveVfxPrefab, leverHandle.position, leverHandle.rotation, leverHandle);
            }
            else if (!isPressed && activeVfxInstance != null)
            {
                Destroy(activeVfxInstance);
                activeVfxInstance = null;
            }
        }
    }

    private void UpdatePromptUI()
    {
        bool shouldShow = isPlayerNearby && !IsLeverPressed;

        if (shouldShow != isPromptShowing)
        {
            isPromptShowing = shouldShow;

            PlayerHUDController hud = PlayerHUDController.Instance != null 
                ? PlayerHUDController.Instance 
                : FindFirstObjectByType<PlayerHUDController>();

            if (hud != null)
            {
                hud.ShowInteractionPrompt(shouldShow, shouldShow ? promptMessage : "");
            }

            if (interactUI != null)
            {
                interactUI.SetActive(shouldShow);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (!IsSpawned || netObj == null || netObj.IsLocalPlayer || netObj.IsOwner)
            {
                isPlayerNearby = true;
                UpdatePromptUI();
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isPlayerNearby && IsPlayerCollider(other))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (!IsSpawned || netObj == null || netObj.IsLocalPlayer || netObj.IsOwner)
            {
                isPlayerNearby = true;
                UpdatePromptUI();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (!IsSpawned || netObj == null || netObj.IsLocalPlayer || netObj.IsOwner)
            {
                isPlayerNearby = false;
                UpdatePromptUI();

                if (lastSentState)
                {
                    lastSentState = false;
                    SetPressedState(false);
                }
            }
        }
    }
}
