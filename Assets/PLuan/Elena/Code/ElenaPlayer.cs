using Unity.Netcode;
using UnityEngine;

public class ElenaPlayer : NetworkBehaviour, IPlayerHUDTarget
{
    [Header("Movement & Attack Settings")]
    public float moveSpeed = 5f;
    public float runSpeedMultiplier = 2.0f;
    public float damageAmount = 20f;
    public float attackRange = 3f;

    [Header("Combo Attack Settings")]
    public float comboWindow = 1.5f;
    public float comboTransitionThreshold = 0.7f;
    public float punch1Duration = 1.2f;
    public float punch2Duration = 1.2f;
    public float punch3Duration = 1.2f;
    public float chem1Duration = 1.2f;
    public float chem2Duration = 1.2f;
    public float chem3Duration = 1.2f;
    private int comboStep = 0;
    private float lastAttackTime = 0f;
    private bool isRootedAttack = false;

    [Header("Weapon Switch Animations")]
    public string drawWeaponTrigger = "LayCung";
    public string sheathWeaponTrigger = "CatCung";

    public GameObject weaponOnBackVisual;
    public GameObject weaponInHandVisual;

    [Header("Player Health Settings")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Network Sync Variables")]
    public NetworkVariable<int> activeWeaponIndex = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isWeapon2Locked = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isSkillsUnlocked = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Upgrade Sync Variables")]
    public NetworkVariable<int> upgradePoints = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> hpLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> mpLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> cooldownLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> damageLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Local State & Inventory")]
    public string[] inventorySlots = new string[10] { "", "", "", "", "", "", "", "", "", "" };
    
    // Biến lưu nâng cấp cho chế độ chơi đơn (Standalone)
    private int localUpgradePoints = 0;
    private int localHpLevel = 0;
    private int localMpLevel = 0;
    private int localCooldownLevel = 0;
    private int localDamageLevel = 0;
    private int localLevel = 0;
    private float localExp = 0f;
    private int localActiveWeaponIndex = 1;

    [Header("Player Experience & Level")]
    public NetworkVariable<int> playerLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<float> playerExp = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Weapon Durability")]
    public float weapon1MaxDurability = 100f;
    public float weapon2MaxDurability = 100f;

    public NetworkVariable<float> weapon1Durability = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<float> weapon2Durability = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private float localWeapon1Durability = 100f;
    private float localWeapon2Durability = 100f;

    [Header("Player Class Settings")]
    [Tooltip("0 = Sát Thủ, 1 = Hỏa Thuật, 2 = Cung Thủ, 3 = Tanker")]
    public int characterClassIndex = 2;

    [Header("Player Name Sync")]
    public NetworkVariable<Unity.Collections.FixedString64Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Elena", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // IPlayerHUDTarget Stats Implementation
    public string DisplayName => string.IsNullOrEmpty(playerName.Value.ToString()) ? "Elena" : playerName.Value.ToString();
    public int PlayerLevel => isStandaloneMode ? localLevel : playerLevel.Value;
    public float PlayerExp => isStandaloneMode ? localExp : playerExp.Value;
    public float MaxExp => 100f + (isStandaloneMode ? localLevel : playerLevel.Value) * 50f;

    [Header("Knockback Settings")]
    private Vector3 knockbackVelocity;

    [Header("Camera Follow Settings")]
    public bool enableCameraFollow = true;
    public Vector3 cameraOffset = new Vector3(0f, 12f, -8f);
    public float cameraSmoothSpeed = 5f;
    public bool cameraLookAtPlayer = true;
    public float cameraPivotHeight = 1.0f;
    private Camera targetCamera;

    [Header("Camera Rotation Settings")]
    public float cameraSensitivity = 2f;
    public float minPitch = 10f;
    public float maxPitch = 80f;
    public float rotationSmoothSpeed = 15f;
    private float currentYaw = 0f;
    private float currentPitch = 45f;
    private float targetYaw = 0f;
    private float targetPitch = 45f;
    private float cameraDistance = 14f;
    private bool isCursorLocked = true;

    [Header("Aiming Settings")]
    public float aimCameraDistance = 4f;
    public float aimShoulderOffset = 0.8f;
    public float aimPivotHeight = 1.3f;
    public float aimCameraSmoothSpeed = 10f;
    public float aimAttackRange = 25f;
    public float aimMinPitch = -80f; // Góc ngước lên tối đa khi ngắm
    public float aimMaxPitch = 80f;  // Góc cúi xuống tối đa khi ngắm

    [Header("Arrow Spawning Settings")]
    public GameObject arrowHandVisual; // Mũi tên trên tay (Visual)
    public GameObject arrowPrefab;     // Prefab mũi tên bay (Projectile)
    public Transform arrowSpawnPoint;   // Điểm xuất phát của mũi tên
    public float arrowSpeed = 30f;      // Tốc độ bay của mũi tên
    public float bowShootCooldown = 1.0f; // Thời gian chờ giữa mỗi lần bắn (giây)
    private float bowShootCooldownTimer = 0f; // Bộ đếm thời gian chờ bắn

    [Header("E Skill (Piercing Arrows) Settings")]
    public float eSkillCooldown = 10f; // Cooldown của kỹ năng E (giây)
    public int eSkillMaxPiercingArrows = 3; // Số lượng mũi tên xuyên thấu tối đa khi kích hoạt kỹ năng E
    private int eSkillRemainingArrows = 0; // Số mũi tên xuyên thấu còn lại (Standalone)
    public NetworkVariable<int> eSkillRemainingArrowsNet = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public int ESkillRemainingArrows => isStandaloneMode ? eSkillRemainingArrows : eSkillRemainingArrowsNet.Value;

    private float eSkillCooldownTimer = 0f; // Bộ đếm cooldown E
    private bool localIsESkillActive = false; // Trạng thái kỹ năng E ở local
    public NetworkVariable<bool> isESkillActiveNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsESkillActive => isStandaloneMode ? localIsESkillActive : isESkillActiveNet.Value;

    [Header("Q Skill (Triple Arrows) Settings")]
    public float qSkillCooldown = 15f; // Cooldown của kỹ năng Q (giây)
    public float qSkillDuration = 10f; // Thời lượng tác dụng kỹ năng Q (giây)
    public float qSkillSpreadAngle = 10f; // Góc lệch của 2 mũi tên bên cạnh
    private float qSkillCooldownTimer = 0f; // Bộ đếm cooldown Q
    private float qSkillDurationTimer = 0f; // Bộ đếm thời lượng Q
    private bool localIsQSkillActive = false; // Trạng thái kỹ năng Q ở local
    public NetworkVariable<bool> isQSkillActiveNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsQSkillActive => isStandaloneMode ? localIsQSkillActive : isQSkillActiveNet.Value;

    [Header("R Skill (Attack Speed Boost) Settings")]
    public float rSkillCooldown = 20f; // Cooldown của kỹ năng R (giây)
    public float rSkillDuration = 5f;  // Thời lượng tác dụng kỹ năng R (giây)
    public float rSkillShootCooldown = 0.3f; // Tốc độ bắn khi bật R (giây chờ giữa các phát bắn)
    private float rSkillCooldownTimer = 0f; // Bộ đếm cooldown R
    private float rSkillDurationTimer = 0f; // Bộ đếm thời lượng R
    private bool localIsRSkillActive = false; // Trạng thái kỹ năng R ở local
    public NetworkVariable<bool> isRSkillActiveNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsRSkillActive => isStandaloneMode ? localIsRSkillActive : isRSkillActiveNet.Value;

    private float defaultCameraDistance;
    private float defaultPivotHeight;
    private float currentShoulderOffset = 0f;
    private bool localIsAiming = false;
    public NetworkVariable<bool> isAimingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsAiming => isStandaloneMode ? localIsAiming : (IsOwner ? localIsAiming : isAimingNet.Value);
    public bool IsBusyOrRolling => (isStandaloneMode ? isRollingStandalone : rollTimer > 0) || IsPlayingActionAnimation() || (CurrentHealth <= 0);

    [Header("Camera Inversion Settings")]
    public bool invertCameraY = false;
    public bool invertSpinePitch = false;

    [Header("Animation Settings")]
    public Animator anim;
    private string currentAnimState;

    [Header("Spine Aim Settings")]
    public float maxSpineTwistAngle = 80f;
    
    [Header("Punch 1 Fine Tuning")]
    public float punch1YOffset = 0f; // Xoay Trái/Phải cho Đấm 1
    public float punch1XOffset = 0f; // Ngửa/Cúi cho Đấm 1 (Fix đấm cao)

    [Header("Punch 2 Fine Tuning")]
    public float punch2YOffset = 0f; // Xoay Trái/Phải cho Đấm 2 (Fix đấm chéo từ phải qua)

    [Header("Punch 3 Fine Tuning")]
    public float punch3YOffset = 0f; // Xoay Trái/Phải cho Đấm 3

    [Header("Aim Fine Tuning")]
    public float aimSpineYOffset = 0f; // Xoay Trái/Phải khi ngắm bắn đứng yên
    public float aimSpineXOffset = 0f; // Ngửa/Cúi khi ngắm bắn đứng yên
    public float aimMovingSpineYOffset = 0f; // Xoay Trái/Phải khi ngắm bắn di chuyển
    public float aimMovingSpineXOffset = 0f; // Ngửa/Cúi khi ngắm bắn di chuyển
    
    private float smoothedYOffset = 0f;
    private float smoothedXOffset = 0f;
    public float spineSmoothSpeed = 15f;
    private Transform spineBone;
    private float localAimAngle = 0f;
    private Vector3 lastPosition;
    public NetworkVariable<float> netAimAngle = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public NetworkVariable<float> netAimPitch = new NetworkVariable<float>(
        45f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private Transform GetSpineBone()
    {
        if (spineBone == null && anim != null)
        {
            spineBone = anim.GetBoneTransform(HumanBodyBones.Spine);
            if (spineBone == null)
            {
                spineBone = anim.GetBoneTransform(HumanBodyBones.Chest);
            }
        }
        return spineBone;
    }

    [Header("Dodge Roll Settings")]
    public float rollSpeed = 15f;
    public float rollDuration = 0.4f;
    public float rollCooldown = 1.2f;
    private float rollCooldownTimer;
    private float rollTimer;
    private Vector3 rollDirection;
    public NetworkVariable<bool> isRollingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private bool isRollingStandalone = false;
    private Rigidbody rb;
    private RootMotionBridge rootMotionBridge;
    private RootMotionBridge GetRootMotionBridge()
    {
        if (rootMotionBridge == null && anim != null)
        {
            rootMotionBridge = anim.GetComponent<RootMotionBridge>();
            if (rootMotionBridge == null)
            {
                rootMotionBridge = anim.gameObject.AddComponent<RootMotionBridge>();
                Debug.Log($"[ElenaPlayer] Dynamically added RootMotionBridge to {anim.gameObject.name} at runtime.");
            }
        }
        return rootMotionBridge;
    }

    // ------------------------------------------------------------------
    //  Biến nội bộ cho chế độ Standalone (không có Netcode)
    // ------------------------------------------------------------------
    private float localHealth;
    public bool isStandaloneMode = false; // true khi chạy đơn lẻ không qua NetworkManager
    private bool isSyncingFromDb = false; // true khi đang đồng bộ dữ liệu ban đầu từ DB tránh hồi máu ảo

    /// <summary>
    /// Trả về true nếu NetworkManager đang hoạt động và đã kết nối/host.
    /// </summary>
    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    /// <summary>
    /// Máu hiện tại: đọc từ NetworkVariable khi online, đọc từ biến local khi standalone.
    /// </summary>
    public float CurrentHealth =>
        isStandaloneMode ? localHealth : currentHealth.Value;

    // IPlayerHUDTarget Implementation
    bool IPlayerHUDTarget.isStandaloneMode => isStandaloneMode;
    public bool IsStandaloneMode => isStandaloneMode;
    public int CharacterClassIndex => characterClassIndex;
    public bool IsSwitchingWeapon => false; // Elena doesn't have draw/sheath lock state
    public float Weapon1MaxDurability => weapon1MaxDurability;
    public float Weapon2MaxDurability => weapon2MaxDurability;
    public string[] InventorySlots => inventorySlots;
    public float MaxHealth => maxHealth;


    // Invisibility Skill R (Elena's Attack Speed Boost)
    public bool IsInvisible => IsRSkillActive;
    public float InvisibilityTimeRemaining => rSkillDurationTimer;
    public void TriggerInvisibilitySkill()
    {
        if (PlayerLevel < 5) return;
        TriggerRSkill();
    }

    // Attack Speed Boost Skill E (Elena's Piercing Arrows)
    public bool IsAttackSpeedBoosted => IsESkillActive;
    public float AttackSpeedBoostTimeRemaining => ESkillRemainingArrows;
    public void TriggerAttackSpeedBoostSkill()
    {
        if (PlayerLevel < 10) return;
        TriggerESkill();
    }

    public void TriggerESkill()
    {
        if (PlayerLevel < 10) return;
        if (eSkillCooldownTimer > 0f || IsESkillActive) return;
        
        if (isStandaloneMode)
        {
            localIsESkillActive = true;
            eSkillRemainingArrows = eSkillMaxPiercingArrows;
        }
        else if (IsOwner)
        {
            SetESkillActiveServerRpc(true);
        }
    }

    private void EndESkill()
    {
        if (isStandaloneMode)
        {
            localIsESkillActive = false;
            eSkillRemainingArrows = 0;
        }
        else if (IsOwner)
        {
            SetESkillActiveServerRpc(false);
        }
    }

    [ServerRpc]
    private void SetESkillActiveServerRpc(bool active)
    {
        isESkillActiveNet.Value = active;
        if (active)
        {
            eSkillRemainingArrowsNet.Value = eSkillMaxPiercingArrows;
        }
        else
        {
            eSkillRemainingArrowsNet.Value = 0;
        }
    }

    public void StartESkillCooldown()
    {
        eSkillCooldownTimer = eSkillCooldown;
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.TriggerElenaCooldownE();
            }
        }
        else
        {
            StartESkillCooldownClientRpc();
        }
    }

    [ClientRpc]
    private void StartESkillCooldownClientRpc()
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.TriggerElenaCooldownE();
            }
        }
        eSkillCooldownTimer = eSkillCooldown;
    }

    // Q Skill
    bool IPlayerHUDTarget.IsQSkillActive => IsQSkillActive;
    public float QSkillTimeRemaining => qSkillDurationTimer;
    
    public bool TriggerQSkill()
    {
        if (PlayerLevel < 15) return false;
        if (qSkillCooldownTimer > 0f || IsQSkillActive) return false;

        qSkillDurationTimer = qSkillDuration;

        if (isStandaloneMode)
        {
            localIsQSkillActive = true;
        }
        else if (IsOwner)
        {
            SetQSkillActiveServerRpc(true);
        }
        return true;
    }

    private void EndQSkill()
    {
        if (isStandaloneMode)
        {
            localIsQSkillActive = false;
        }
        else if (IsOwner)
        {
            SetQSkillActiveServerRpc(false);
        }
        StartQSkillCooldown();
    }

    [ServerRpc]
    private void SetQSkillActiveServerRpc(bool active)
    {
        isQSkillActiveNet.Value = active;
    }

    public void StartQSkillCooldown()
    {
        qSkillCooldownTimer = qSkillCooldown;
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.TriggerElenaCooldownQ();
            }
        }
        else
        {
            StartQSkillCooldownClientRpc();
        }
    }

    [ClientRpc]
    private void StartQSkillCooldownClientRpc()
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.TriggerElenaCooldownQ();
            }
        }
        qSkillCooldownTimer = qSkillCooldown;
    }

    // R Skill
    public void TriggerRSkill()
    {
        if (rSkillCooldownTimer > 0f || IsRSkillActive) return;

        rSkillDurationTimer = rSkillDuration;

        if (isStandaloneMode)
        {
            localIsRSkillActive = true;
        }
        else if (IsOwner)
        {
            SetRSkillActiveServerRpc(true);
        }
    }

    private void EndRSkill()
    {
        if (isStandaloneMode)
        {
            localIsRSkillActive = false;
        }
        else if (IsOwner)
        {
            SetRSkillActiveServerRpc(false);
        }
        StartRSkillCooldown();
    }

    [ServerRpc]
    private void SetRSkillActiveServerRpc(bool active)
    {
        isRSkillActiveNet.Value = active;
    }

    public void StartRSkillCooldown()
    {
        rSkillCooldownTimer = rSkillCooldown;
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.TriggerElenaCooldownR();
            }
        }
        else
        {
            StartRSkillCooldownClientRpc();
        }
    }

    [ClientRpc]
    private void StartRSkillCooldownClientRpc()
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.TriggerElenaCooldownR();
            }
        }
        rSkillCooldownTimer = rSkillCooldown;
    }

    public event System.Action OnQSkillCancelled;


    /// <summary>
    /// Trả về index vũ khí đang chọn: đọc từ HUD khi standalone, đọc từ NetworkVariable khi online.
    /// </summary>
    public int GetActiveWeaponIndex()
    {
        if (isStandaloneMode)
        {
            return localActiveWeaponIndex; // <-- SỬA DÒNG NÀY (Thay vì đọc hud.currentSelectedWeapon)
        }
        return activeWeaponIndex.Value;
    }

    protected virtual void Awake()
    {
        // Ép tên Trigger luôn đúng với Animator tiếng Việt của Elena, bỏ qua giá trị cũ bị lưu ở Inspector
        drawWeaponTrigger = "LayCung";
        sheathWeaponTrigger = "CatCung";

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Mặc định tắt Kinematic để di chuyển được ở chế độ Standalone/Offline
        }

        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }
    }

    private void Start()
    {
        // Đảm bảo khởi tạo Animator cho cả các lớp kế thừa
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        if (anim != null)
        {
            anim.applyRootMotion = false; // Tắt root motion mặc định để tránh ghi đè tốc độ di chuyển của code
            GetRootMotionBridge();
        }

        // Khởi tạo các góc xoay camera từ offset mặc định
        float horizontalDistance = new Vector3(cameraOffset.x, 0f, cameraOffset.z).magnitude;
        currentYaw = Mathf.Atan2(cameraOffset.x, -cameraOffset.z) * Mathf.Rad2Deg;
        currentPitch = Mathf.Atan2(cameraOffset.y, horizontalDistance) * Mathf.Rad2Deg;
        targetYaw = currentYaw;
        targetPitch = currentPitch;
        cameraDistance = cameraOffset.magnitude;
        defaultCameraDistance = cameraDistance;
        defaultPivotHeight = cameraPivotHeight;
        lastPosition = transform.position;

        // Khóa chuột mặc định khi vào game
        LockCursor(isCursorLocked);

        // Nếu không có NetworkManager hoặc chưa listen → chạy đơn lẻ
        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            localHealth = maxHealth;
            InitStandaloneMode();
        }
        // Nếu có Netcode nhưng chưa spawn (vd: đang chờ) thì không làm gì thêm
        // OnNetworkSpawn() sẽ lo phần còn lại
        UpdateWeaponVisualsInstant(GetActiveWeaponIndex());
    }

    /// <summary>
    /// Khởi tạo tất cả chức năng khi chơi đơn lẻ trong Editor mà không cần Host/Server.
    /// </summary>
    private void InitStandaloneMode()
    {
        Debug.Log("[ElenaPlayer] Chạy ở chế độ STANDALONE (không có NetworkManager). " +
                  "Di chuyển và tấn công hoạt động cục bộ.");

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Tắt Kinematic để di chuyển trong chế độ chơi đơn lẻ
        }

        // Tìm camera
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        // Tải nhân vật đã lưu từ PlayerPrefs nếu có
        characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);
        playerName.Value = PlayerPrefs.GetString("AuthDisplayName", "Elena");
        if (PlayerHUDManager.ActivePlayers != null && !PlayerHUDManager.ActivePlayers.Contains(this))
        {
            PlayerHUDManager.ActivePlayers.Add(this);
        }

        // Nạp cấp độ và kinh nghiệm cho chế độ chơi đơn (mặc định về lại 0 theo yêu cầu)
        localLevel = PlayerPrefs.GetInt("SelectedPlayerLevel_" + characterClassIndex, 0);
        localExp = PlayerPrefs.GetFloat("SelectedPlayerExp_" + characterClassIndex, 0f);

        // Register local target
        PlayerHUDController.LocalPlayerTarget = this;
        RakanDialogueController.LocalPlayerTarget = this;
        SilasDialogueController.LocalPlayerTarget = this;

        PlayerHUDController hud = null;
        PlayerHUDManager hudManager = PlayerHUDManager.Instance != null ? PlayerHUDManager.Instance : FindObjectOfType<PlayerHUDManager>();
        if (hudManager != null)
        {
            hud = hudManager.ActivateHUD(characterClassIndex);
        }
        else
        {
            hud = FindObjectOfType<PlayerHUDController>();
        }

        if (hud != null)
        {
            hud.SetupPlayerProfile(characterClassIndex);
            ApplyUpgradedStats();
            UpdateUpgradeHUD();
        }
    }

    // ------------------------------------------------------------------
    //  Netcode Spawn / Despawn
    // ------------------------------------------------------------------
    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = !IsOwner;
        }

        if (!IsOwner)
        {
            if (GetComponent<PlayerNameplate>() == null)
            {
                gameObject.AddComponent<PlayerNameplate>();
            }
        }

        if (PlayerHUDManager.ActivePlayers != null && !PlayerHUDManager.ActivePlayers.Contains(this))
        {
            PlayerHUDManager.ActivePlayers.Add(this);
        }

        // Đăng ký sự kiện đồng bộ Netcode
        activeWeaponIndex.OnValueChanged += OnWeaponIndexChanged;
        isWeapon2Locked.OnValueChanged += OnWeapon2LockedChanged;
        isSkillsUnlocked.OnValueChanged += OnSkillsUnlockedChanged;

        // Đăng ký các sự kiện nâng cấp chỉ số
        upgradePoints.OnValueChanged += OnUpgradePointsChanged;
        hpLevel.OnValueChanged += OnHpLevelChanged;
        mpLevel.OnValueChanged += OnMpLevelChanged;
        cooldownLevel.OnValueChanged += OnCooldownLevelChanged;
        damageLevel.OnValueChanged += OnDamageLevelChanged;

        // Đăng ký sự kiện đồng bộ kinh nghiệm và cấp độ
        playerLevel.OnValueChanged += OnLevelOrExpChanged;
        playerExp.OnValueChanged += OnLevelOrExpChanged;

        // Đăng ký sự kiện đồng bộ độ bền vũ khí
        weapon1Durability.OnValueChanged += OnDurabilityChanged;
        weapon2Durability.OnValueChanged += OnDurabilityChanged;
        currentHealth.OnValueChanged += OnHealthChangedShared;
        isAimingNet.OnValueChanged += OnAimingNetChanged;

        if (IsOwner)
        {
            // Tải nhân vật đã lưu từ PlayerPrefs
            characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);

            // Đồng bộ tên người chơi qua mạng
            string myName = PlayerPrefs.GetString("AuthDisplayName", "Elena");
            SetPlayerNameServerRpc(myName);

            // Register local target
            PlayerHUDController.LocalPlayerTarget = this;
            RakanDialogueController.LocalPlayerTarget = this;
            SilasDialogueController.LocalPlayerTarget = this;

            currentHealth.OnValueChanged += OnHealthChanged;
            UpdateHealthHUD(currentHealth.Value);

            PlayerHUDController hud = null;
            PlayerHUDManager hudManager = PlayerHUDManager.Instance != null ? PlayerHUDManager.Instance : FindObjectOfType<PlayerHUDManager>();
            if (hudManager != null)
            {
                hud = hudManager.ActivateHUD(characterClassIndex);
            }
            else
            {
                hud = FindObjectOfType<PlayerHUDController>();
            }

            if (hud != null)
                hud.SetupPlayerProfile(characterClassIndex);

            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindObjectOfType<Camera>();

            // Tải dữ liệu từ MongoDB Atlas
            LoadPlayerStateFromDatabase();

            // Áp dụng và cập nhật UI chỉ số ban đầu cục bộ
            ApplyUpgradedStats();
            UpdateUpgradeHUD();
            UpdateDurabilityHUD();
            UpdateWeaponVisualsInstant(GetActiveWeaponIndex());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (PlayerHUDManager.ActivePlayers != null)
        {
            PlayerHUDManager.ActivePlayers.Remove(this);
        }

        activeWeaponIndex.OnValueChanged -= OnWeaponIndexChanged;
        isWeapon2Locked.OnValueChanged -= OnWeapon2LockedChanged;
        isSkillsUnlocked.OnValueChanged -= OnSkillsUnlockedChanged;

        // Hủy đăng ký các sự kiện nâng cấp chỉ số
        upgradePoints.OnValueChanged -= OnUpgradePointsChanged;
        hpLevel.OnValueChanged -= OnHpLevelChanged;
        mpLevel.OnValueChanged -= OnMpLevelChanged;
        cooldownLevel.OnValueChanged -= OnCooldownLevelChanged;
        damageLevel.OnValueChanged -= OnDamageLevelChanged;

        // Hủy đăng ký sự kiện kinh nghiệm
        playerLevel.OnValueChanged -= OnLevelOrExpChanged;
        playerExp.OnValueChanged -= OnLevelOrExpChanged;

        // Hủy đăng ký sự kiện độ bền vũ khí
        weapon1Durability.OnValueChanged -= OnDurabilityChanged;
        weapon2Durability.OnValueChanged -= OnDurabilityChanged;
        currentHealth.OnValueChanged -= OnHealthChangedShared;
        isAimingNet.OnValueChanged -= OnAimingNetChanged;

        if (IsOwner)
            currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnWeaponIndexChanged(int oldVal, int newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null) hud.SelectWeapon(newVal);
        }

        PlayWeaponSwitchAnimation(oldVal, newVal);
    }

    private void OnWeapon2LockedChanged(bool oldVal, bool newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null) hud.SetWeapon2Locked(newVal, true);
        }
    }

    private void OnSkillsUnlockedChanged(bool oldVal, bool newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null) hud.SetSkillsUnlocked(newVal, true);
        }
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        UpdateHealthHUD(newHealth);
        
        // Tự động lưu lên DB khi máu thay đổi
        if (IsOwner)
        {
            SavePlayerStateToDatabase();
        }
    }

    private void OnHealthChangedShared(float oldHealth, float newHealth)
    {
        if (newHealth < oldHealth)
        {
            var flash = GetComponent<MaterialFlashBehaviour>();
            if (flash == null) flash = gameObject.AddComponent<MaterialFlashBehaviour>();
            flash.Flash(Color.red, 0.15f);
        }
    }

    private void UpdateHealthHUD(float health)
    {
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
            hud.SetHealth(health / maxHealth);
    }

    // ------------------------------------------------------------------
    //  Bộ lắng nghe sự kiện thay đổi của các NetworkVariable nâng cấp chỉ số
    // ------------------------------------------------------------------
    private void OnUpgradePointsChanged(int oldVal, int newVal)
    {
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    private void OnHpLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner)
        {
            ApplyUpgradedStats();
        }
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    private void OnMpLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner)
        {
            ApplyUpgradedStats();
        }
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    private void OnCooldownLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner)
        {
            ApplyUpgradedStats();
        }
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    private void OnDamageLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner)
        {
            ApplyUpgradedStats();
        }
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    // ------------------------------------------------------------------
    //  Áp dụng chỉ số nâng cấp vào các giá trị thực tế
    // ------------------------------------------------------------------
    private void ApplyUpgradedStats()
    {
        if (isSyncingFromDb) return;

        int hpLv = isStandaloneMode ? localHpLevel : hpLevel.Value;
        int dmgLv = isStandaloneMode ? localDamageLevel : damageLevel.Value;

        float oldMaxHealth = maxHealth;
        maxHealth = 100f + hpLv * 20f;
        damageAmount = 20f + dmgLv * 5f;

        if (isStandaloneMode)
        {
            if (maxHealth > oldMaxHealth)
            {
                localHealth += (maxHealth - oldMaxHealth);
            }
            UpdateHealthHUD(localHealth);
        }
        else if (IsServer)
        {
            if (maxHealth > oldMaxHealth)
            {
                currentHealth.Value += (maxHealth - oldMaxHealth);
            }
        }
    }

    // ------------------------------------------------------------------
    //  Cập nhật UI nâng cấp chỉ số
    // ------------------------------------------------------------------
    private void UpdateUpgradeHUD()
    {
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
        {
            int pts = isStandaloneMode ? localUpgradePoints : upgradePoints.Value;
            int hp = isStandaloneMode ? localHpLevel : hpLevel.Value;
            int mp = isStandaloneMode ? localMpLevel : mpLevel.Value;
            int cd = isStandaloneMode ? localCooldownLevel : cooldownLevel.Value;
            int dmg = isStandaloneMode ? localDamageLevel : damageLevel.Value;

            hud.UpdateUpgradeUI(pts, hp, mp, cd, dmg);
            hud.SetHealth(CurrentHealth / maxHealth);

            // Đồng bộ Cấp độ & Kinh nghiệm lên giao diện HUD chính
            int lv = isStandaloneMode ? localLevel : playerLevel.Value;
            float xp = isStandaloneMode ? localExp : playerExp.Value;
            float needed = 100f + lv * 50f;
            hud.UpdateExperienceUI(lv, xp, needed);
        }
    }

    private void OnLevelOrExpChanged(int oldVal, int newVal)
    {
        if (IsOwner) UpdateUpgradeHUD();
    }

    private void OnLevelOrExpChanged(float oldVal, float newVal)
    {
        if (IsOwner) UpdateUpgradeHUD();
    }

    public float Weapon1Durability
    {
        get { return isStandaloneMode ? localWeapon1Durability : weapon1Durability.Value; }
        set {
            if (isStandaloneMode)
            {
                localWeapon1Durability = Mathf.Clamp(value, 0f, weapon1MaxDurability);
                UpdateDurabilityHUD();
            }
            else if (IsServer)
            {
                weapon1Durability.Value = Mathf.Clamp(value, 0f, weapon1MaxDurability);
            }
        }
    }

    public float Weapon2Durability
    {
        get { return isStandaloneMode ? localWeapon2Durability : weapon2Durability.Value; }
        set {
            if (isStandaloneMode)
            {
                localWeapon2Durability = Mathf.Clamp(value, 0f, weapon2MaxDurability);
                UpdateDurabilityHUD();
            }
            else if (IsServer)
            {
                weapon2Durability.Value = Mathf.Clamp(value, 0f, weapon2MaxDurability);
            }
        }
    }

    public void RepairWeaponFromHUD(int weaponSlotIndex)
    {
        if (isStandaloneMode)
        {
            if (weaponSlotIndex == 1) localWeapon1Durability = weapon1MaxDurability;
            else localWeapon2Durability = weapon2MaxDurability;
            UpdateDurabilityHUD();
        }
        else
        {
            RepairWeaponServerRpc(weaponSlotIndex);
        }
    }

    [ServerRpc]
    private void RepairWeaponServerRpc(int weaponSlotIndex)
    {
        if (weaponSlotIndex == 1)
        {
            weapon1Durability.Value = weapon1MaxDurability;
        }
        else
        {
            weapon2Durability.Value = weapon2MaxDurability;
        }
    }

    private void OnDurabilityChanged(float oldVal, float newVal)
    {
        if (IsOwner) UpdateDurabilityHUD();
    }

    public void UpdateDurabilityHUD()
    {
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
        {
            float percent1 = (isStandaloneMode ? localWeapon1Durability : weapon1Durability.Value) / weapon1MaxDurability;
            float percent2 = (isStandaloneMode ? localWeapon2Durability : weapon2Durability.Value) / weapon2MaxDurability;
            hud.SetWeaponDurability(1, percent1);
            hud.SetWeaponDurability(2, percent2);
        }
    }

    public bool TryAddItem(string itemName)
    {
        // 1. Tìm xem vật phẩm đã tồn tại trong túi đồ để tăng số lượng (Cộng dồn stack)
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            string slotVal = inventorySlots[i];
            if (!string.IsNullOrEmpty(slotVal))
            {
                string name = slotVal;
                int count = 1;
                if (slotVal.Contains(":"))
                {
                    var parts = slotVal.Split(':');
                    name = parts[0];
                    int.TryParse(parts[1], out count);
                }

                if (name == itemName)
                {
                    inventorySlots[i] = name + ":" + (count + 1);
                    
                    // Cập nhật lại giao diện hòm đồ
                    PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                    if (hud != null)
                    {
                        hud.SetInventorySlots(inventorySlots);
                    }
                    
                    // Đồng bộ và lưu trữ lên cơ sở dữ liệu nếu chơi online
                    if (!isStandaloneMode)
                    {
                        SavePlayerStateToDatabase();
                    }
                    
                    PlayAnimation("Idle_Pick", 0.1f);
                    return true;
                }
            }
        }

        // 2. Nếu chưa có stack nào sẵn, đặt vào ô trống đầu tiên
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (string.IsNullOrEmpty(inventorySlots[i]))
            {
                inventorySlots[i] = itemName + ":1";
                
                // Cập nhật lại giao diện hòm đồ
                PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                if (hud != null)
                {
                    hud.SetInventorySlots(inventorySlots);
                }
                
                // Đồng bộ và lưu trữ lên cơ sở dữ liệu nếu chơi online
                if (!isStandaloneMode)
                {
                    SavePlayerStateToDatabase();
                }
                
                PlayAnimation("Idle_Pick", 0.1f);
                return true;
            }
        }
        return false;
    }

    private System.Collections.Generic.HashSet<string> collectedDropGroups = new System.Collections.Generic.HashSet<string>();

    public bool HasCollectedFromDropGroup(string dropGroupId)
    {
        if (string.IsNullOrEmpty(dropGroupId)) return false;
        return collectedDropGroups.Contains(dropGroupId);
    }

    public void AddCollectedDropGroup(string dropGroupId)
    {
        if (string.IsNullOrEmpty(dropGroupId)) return;
        collectedDropGroups.Add(dropGroupId);
    }

    [ClientRpc]
    public void OnCollectGemClientRpc(string dropGroupId)
    {
        if (IsOwner)
        {
            AddCollectedDropGroup(dropGroupId);
        }
    }

    /// <summary>
    /// Cộng điểm kinh nghiệm thu thập được và kiểm tra thăng cấp nhân vật
    /// </summary>
    public void AddExperience(float amount)
    {
        if (isStandaloneMode)
        {
            localExp += amount;
            float needed = 100f + localLevel * 50f;
            while (localExp >= needed)
            {
                localExp -= needed;
                localLevel++;
                needed = 100f + localLevel * 50f;
                // Thăng cấp cộng thêm 1 Điểm Nâng Cấp chỉ số cho người chơi
                localUpgradePoints++;
                Debug.Log($"[Standalone] THĂNG CẤP! Cấp độ mới: {localLevel}. Điểm nâng cấp còn: {localUpgradePoints}");
            }
            // Lưu cấp độ và kinh nghiệm cục bộ của class này
            PlayerPrefs.SetInt("SelectedPlayerLevel_" + characterClassIndex, localLevel);
            PlayerPrefs.SetFloat("SelectedPlayerExp_" + characterClassIndex, localExp);
            PlayerPrefs.Save();
            
            UpdateUpgradeHUD();
        }
        else if (IsServer)
        {
            playerExp.Value += amount;
            float needed = 100f + playerLevel.Value * 50f;
            while (playerExp.Value >= needed)
            {
                playerExp.Value -= needed;
                playerLevel.Value++;
                needed = 100f + playerLevel.Value * 50f;
                // Thăng cấp cộng thêm 1 Điểm Nâng Cấp chỉ số cho người chơi
                upgradePoints.Value++;
                Debug.Log($"[Server] CLIENT {OwnerClientId} THĂNG CẤP! Cấp độ mới: {playerLevel.Value}. Điểm nâng cấp còn: {upgradePoints.Value}");
            }
            SavePlayerStateClientRpc();
        }
    }

    // ------------------------------------------------------------------
    //  Nâng cấp chỉ số cho chơi đơn (Standalone Mode)
    // ------------------------------------------------------------------
    public void StandaloneUpgradeStat(int statType)
    {
        if (localUpgradePoints <= 0)
        {
            Debug.LogWarning("[Standalone] Hết điểm nâng cấp!");
            return;
        }

        localUpgradePoints--;
        switch (statType)
        {
            case 0: localHpLevel++; break;
            case 1: localMpLevel++; break;
            case 2: localCooldownLevel++; break;
            case 3: localDamageLevel++; break;
        }

        ApplyUpgradedStats();
        UpdateUpgradeHUD();
        Debug.Log($"[Standalone] Đã nâng cấp Stat {statType}. Cấp độ mới: HP={localHpLevel}, MP={localMpLevel}, Cooldown={localCooldownLevel}, Damage={localDamageLevel}. Điểm còn: {localUpgradePoints}");
    }

    // ------------------------------------------------------------------
    //  Nâng cấp chỉ số online qua Server RPC (Netcode Mode)
    // ------------------------------------------------------------------
    public void UpgradeStatFromHUD(int statType)
    {
        if (!IsSpawned || !IsOwner) return;
        UpgradeStatServerRpc(statType);
    }

    [ServerRpc]
    private void UpgradeStatServerRpc(int statType)
    {
        if (upgradePoints.Value <= 0)
        {
            Debug.LogWarning("[Server] Người chơi không còn điểm nâng cấp!");
            return;
        }

        upgradePoints.Value--;
        switch (statType)
        {
            case 0: hpLevel.Value++; break;
            case 1: mpLevel.Value++; break;
            case 2: cooldownLevel.Value++; break;
            case 3: damageLevel.Value++; break;
        }

        ApplyUpgradedStats();
        SavePlayerStateClientRpc();
        Debug.Log($"[Server] Đã nâng cấp Stat {statType} cho {gameObject.name}. HP Lv={hpLevel.Value}, MP Lv={mpLevel.Value}, CD Lv={cooldownLevel.Value}, DMG Lv={damageLevel.Value}. Điểm còn lại: {upgradePoints.Value}");
    }

    // ------------------------------------------------------------------
    //  Update (hoạt động cả Standalone lẫn Netcode)
    // ------------------------------------------------------------------
    private void LockCursor(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        LockCursor(locked);
    }

    void Update()
    {
        // Chỉ xử lý phím tắt Alt ẩn hiện chuột nếu là chủ sở hữu hoặc chơi đơn
        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (hasControl)
        {
            if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
            {
                isCursorLocked = !isCursorLocked;
                LockCursor(isCursorLocked);
            }
        }

        // Cập nhật trạng thái ngắm bắn (Aiming)
        if (hasControl)
        {
            int currentWeaponIdx = GetActiveWeaponIndex();
            bool targetAiming = currentWeaponIdx == 2 && Input.GetMouseButton(1) && !IsUIBlockingInput() && !IsBusyOrRolling;
            if (localIsAiming != targetAiming)
            {
                localIsAiming = targetAiming;
                OnAimStateChanged(localIsAiming);
                if (!isStandaloneMode)
                {
                    SetAimingServerRpc(targetAiming);
                }
            }
        }

        // Giảm thời gian cooldown nhào lộn
        if (rollCooldownTimer > 0)
        {
            rollCooldownTimer -= Time.deltaTime;
        }

        // Giảm thời gian cooldown bắn cung
        if (bowShootCooldownTimer > 0)
        {
            bowShootCooldownTimer -= Time.deltaTime;
        }

        // Giảm thời gian cooldown Kỹ năng E
        if (eSkillCooldownTimer > 0)
        {
            eSkillCooldownTimer -= Time.deltaTime;
        }

        // Giảm thời gian cooldown và thời lượng Kỹ năng Q
        if (qSkillCooldownTimer > 0)
        {
            qSkillCooldownTimer -= Time.deltaTime;
        }
        if (qSkillDurationTimer > 0)
        {
            qSkillDurationTimer -= Time.deltaTime;
            if (qSkillDurationTimer <= 0)
            {
                qSkillDurationTimer = 0f;
                EndQSkill();
            }
        }

        // Giảm thời gian cooldown và thời lượng Kỹ năng R
        if (rSkillCooldownTimer > 0)
        {
            rSkillCooldownTimer -= Time.deltaTime;
        }
        if (rSkillDurationTimer > 0)
        {
            rSkillDurationTimer -= Time.deltaTime;
            if (rSkillDurationTimer <= 0)
            {
                rSkillDurationTimer = 0f;
                EndRSkill();
            }
        }

        // Tự động reset combo và dọn dẹp trigger nếu người chơi đã dừng tấn công và hoạt ảnh trở về trạng thái bình thường (Idle/Walk/Run)
        int activeWeapon = GetActiveWeaponIndex();
        float currentAttackDuration = GetAttackDuration(activeWeapon, comboStep);
        if (comboStep > 0 && Time.time - lastAttackTime > currentAttackDuration * comboTransitionThreshold)
        {
            if (!IsPlayingAttackState(out _, out _))
            {
                ClearAttackLayer();
                if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
                {
                    anim.ResetTrigger("Dam1");
                    anim.ResetTrigger("Dam2");
                    anim.ResetTrigger("Dam3");
                }
            }
        }

        if (CurrentHealth <= 0)
        {
            if (anim != null) anim.applyRootMotion = false;
            PlayAnimation("Death", 0.15f);
            return;
        }

        // Tự động tắt weight của Layer 1 và đưa về New State khi kết thúc cất/lấy vũ khí
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            float weight1 = anim.GetLayerWeight(1);
            if (weight1 > 0f && comboStep == 0 && !IsAiming)
            {
                bool isSwitching = IsStatePlayingOnLayer1(drawWeaponTrigger) || IsStatePlayingOnLayer1(sheathWeaponTrigger);
                if (!isSwitching)
                {
                    ClearAttackLayer();
                }
            }
        }

        // Tính toán và đồng bộ góc xoay cột sống (Spine aim angle)
        if (hasControl)
        {
            bool isCurrentlyAttacking = IsPlayingAttackState(out _, out _) || 
                                        (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f);
            
            if (isCurrentlyAttacking && !isRootedAttack && targetCamera != null)
            {
                Vector3 camForward = targetCamera.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();
                if (camForward != Vector3.zero)
                {
                    float angleDiff = Vector3.SignedAngle(transform.forward, camForward, Vector3.up);
                    angleDiff = Mathf.Clamp(angleDiff, -maxSpineTwistAngle, maxSpineTwistAngle);
                    
                    if (isStandaloneMode)
                    {
                        localAimAngle = angleDiff;
                    }
                    else
                    {
                        netAimAngle.Value = angleDiff;
                    }
                    Debug.Log($"[SpineAim_Update] transform.forward={transform.forward}, camForward={camForward}, angleDiff={angleDiff}, localAim={localAimAngle}, netAim={netAimAngle.Value}");
                }
            }
            else
            {
                if (isCurrentlyAttacking)
                {
                    Debug.Log($"[SpineAim_Update_Failed] isRooted={isRootedAttack}, cam={targetCamera != null}");
                }
                if (!IsAiming)
                {
                    if (isStandaloneMode)
                    {
                        localAimAngle = Mathf.Lerp(localAimAngle, 0f, Time.deltaTime * 10f);
                    }
                    else
                    {
                        if (netAimAngle.Value != 0f)
                        {
                            netAimAngle.Value = Mathf.Lerp(netAimAngle.Value, 0f, Time.deltaTime * 10f);
                        }
                        if (netAimPitch.Value != 45f)
                        {
                            netAimPitch.Value = Mathf.Lerp(netAimPitch.Value, 45f, Time.deltaTime * 10f);
                        }
                    }
                }
            }
        }

        // Standalone: xử lý hoàn toàn cục bộ
        if (isStandaloneMode)
        {
            HandleStandaloneUpdate();
        }
        else
        {
            // Netcode: chỉ chủ sở hữu mới điều khiển
            if (IsOwner)
            {
                HandleOwnerUpdate();
            }
        }

        // Cập nhật tham số hoạt ảnh di chuyển cho local client (chủ sở hữu hoặc chơi đơn)
        if (hasControl)
        {
            UpdateAnimatorParameters();
        }
    }

    private void UpdateAnimatorParameters()
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
            return;

        // Cập nhật trạng thái HasWeapon (vũ khí đang cầm cung: activeWeaponIndex == 2)
        bool hasWeapon = GetActiveWeaponIndex() == 2;
        anim.SetBool("HasWeapon", hasWeapon);

        float animMoveX = 0f;
        float animMoveZ = 0f;

        // Lấy đầu vào di chuyển từ phím bấm của người chơi
        float inputX = Input.GetAxis("Horizontal");
        float inputZ = Input.GetAxis("Vertical");
        Vector3 moveInput = new Vector3(inputX, 0f, inputZ);

        // Chuyển đổi hướng di chuyển theo Camera
        if (targetCamera != null && moveInput != Vector3.zero)
        {
            Vector3 camForward = targetCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = targetCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            moveInput = camRight * inputX + camForward * inputZ;
        }

        // Kiểm tra xem có đang mở hội thoại hoặc bị khóa di chuyển do hành động khác không
        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                               (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);
        
        bool isCurrentlyAttacking = IsPlayingAttackState(out _, out _) || 
                                    (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f);

        bool shouldAnimateMovement = !isDialogueOpen && !IsLockingMovementAction();
        // Nếu là đòn tấn công khóa chân (rooted), chân phải đứng yên (idle)
        if (isCurrentlyAttacking && isRootedAttack)
        {
            shouldAnimateMovement = false;
        }

        if (shouldAnimateMovement && moveInput != Vector3.zero)
        {
            // Hướng di chuyển tương quan với hướng hiện tại của nhân vật
            Vector3 localMove = transform.InverseTransformDirection(moveInput.normalized);

            // Xác định xem đang đi hay chạy
            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            float speedFactor = isRunning ? runSpeedMultiplier : 1f;

            // Giới hạn magnitude tối đa là 1.0f để tránh đi chéo bị nhân lên 1.414 trên bàn phím
            float magnitude = Mathf.Clamp01(moveInput.magnitude);
            animMoveX = localMove.x * magnitude * speedFactor;
            animMoveZ = localMove.z * magnitude * speedFactor;
        }

        anim.SetFloat("MoveX", animMoveX);
        anim.SetFloat("MoveZ", animMoveZ);
    }

    private void HandleStandaloneUpdate()
{
    bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                           (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);

    if (isDialogueOpen)
    {
        if (!IsPlayingActionAnimation()) PlayAnimation("Idle", 0.1f);
        return; 
    }

    // XỬ LÝ DI CHUYỂN KHI ĐANG LỘN (STANDALONE)
    if (isRollingStandalone)
    {
        rollTimer -= Time.deltaTime;
        
        // Di chuyển bằng Rigidbody velocity để mượt mà vật lý và camera follow
        if (rb != null)
        {
            Vector3 vel = rollDirection * rollSpeed;
            vel.y = rb.linearVelocity.y; // giữ trọng lực
            rb.linearVelocity = vel;
        }
        else
        {
            // Fallback nếu không có Rigidbody
            transform.Translate(rollDirection * rollSpeed * Time.deltaTime, Space.World);
        }
        
        // Giữ hướng nhìn theo hướng lộn
        if (rollDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(rollDirection);
        }

        if (rollTimer <= 0)
        {
            OnRollEnd();
        }
        return; // Khóa hoàn toàn các input di chuyển khác bên dưới
    }

        // Knockback
        if (knockbackVelocity.magnitude > 0.01f)
        {
            transform.Translate(knockbackVelocity * Time.deltaTime, Space.World);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
        }

        // Tốc độ di chuyển: Giữ Shift Left là chạy (run), thả ra là đi bộ (Walk)
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;

        // Di chuyển (tính theo hướng Camera để tránh bị ngược điều khiển)
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(moveX, 0, moveZ);

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindObjectOfType<Camera>();
        }

        if (targetCamera != null)
        {
            Vector3 camForward = targetCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = targetCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            move = camRight * moveX + camForward * moveZ;
        }

        // Bình thường hóa hướng di chuyển để tránh tăng tốc khi đi chéo
        if (move != Vector3.zero)
        {
            move.Normalize();
        }

        // Tạo bản sao di chuyển vật lý để có thể khóa di chuyển mà không làm mất hướng né đòn (roll direction)
        Vector3 movementTranslation = move;
        bool isCurrentlyAttacking = IsPlayingAttackState(out _, out _) || 
                                    (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f);
        if (IsLockingMovementAction())
        {
            movementTranslation = Vector3.zero;
        }

        transform.Translate(movementTranslation * currentSpeed * Time.deltaTime, Space.World);

        // Xoay nhân vật: Luôn xoay theo hướng Camera để hỗ trợ đi ngang/lùi (strafe) cho cả khi cầm vũ khí và tay không (Chỉ xoay khi không chơi hoạt ảnh hành động như lộn vòng, nhặt đồ, trúng đòn...)
        if (targetCamera != null && !IsPlayingActionAnimation())
        {
            Vector3 camForward = targetCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            if (camForward != Vector3.zero)
            {
                transform.forward = camForward;
            }
        }



        // Kích hoạt kỹ năng E
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!IsUIBlockingInput())
            {
                TriggerESkill();
            }
        }

        // Tấn công đơn lẻ
        if (Input.GetMouseButtonDown(0))
        {
            if (!IsUIBlockingInput())
            {
                if (IsAiming)
                {
                    if (bowShootCooldownTimer <= 0f)
                    {
                        bowShootCooldownTimer = IsRSkillActive ? rSkillShootCooldown : bowShootCooldown;
                        PerformBowShoot(false);
                    }
                }
                else
                {
                    PerformComboAttack(false);
                }
            }
        }

        // Nhấn Ctrl (LeftControl) chơi hoạt ảnh LonVong + Nhào lộn né chiêu
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            Debug.Log($"[Ctrl Debug] Standalone Ctrl pressed! rollCooldownTimer = {rollCooldownTimer}");
            if (rollCooldownTimer <= 0 && !IsUIBlockingInput())
            {
                StartRollStandalone(move);
            }
        }
    }

    private void HandleOwnerUpdate()
{
    bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                           (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);

    if (isDialogueOpen)
    {
        if (!IsPlayingActionAnimation()) PlayAnimation("Idle", 0.1f);
        return; 
    }

    // XỬ LÝ DI CHUYỂN KHI ĐANG LỘN (NETCODE OWNER)
    if (rollTimer > 0)
    {
        rollTimer -= Time.deltaTime;
        
        // Di chuyển bằng Rigidbody velocity để mượt mà vật lý và camera follow
        if (rb != null)
        {
            Vector3 vel = rollDirection * rollSpeed;
            vel.y = rb.linearVelocity.y; // giữ trọng lực
            rb.linearVelocity = vel;
        }
        else
        {
            // Fallback nếu không có Rigidbody
            transform.Translate(rollDirection * rollSpeed * Time.deltaTime, Space.World);
        }
        
        // Giữ hướng nhìn theo hướng lộn
        if (rollDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(rollDirection);
        }

        if (rollTimer <= 0)
        {
            OnRollEnd();
        }
        return; // Khóa hoàn toàn các input di chuyển khác bên dưới
    }

        // Knockback
        if (knockbackVelocity.magnitude > 0.01f)
        {
            transform.Translate(knockbackVelocity * Time.deltaTime, Space.World);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
        }

        // Tốc độ di chuyển: Giữ Shift Left là chạy (run), thả ra là đi bộ (Walk)
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;

        // Di chuyển (tính theo hướng Camera để tránh bị ngược điều khiển)
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(moveX, 0, moveZ);

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindObjectOfType<Camera>();
        }

        if (targetCamera != null)
        {
            Vector3 camForward = targetCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = targetCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            move = camRight * moveX + camForward * moveZ;
        }

        // Bình thường hóa hướng di chuyển để tránh tăng tốc khi đi chéo
        if (move != Vector3.zero)
        {
            move.Normalize();
        }

        // Tạo bản sao di chuyển vật lý để có thể khóa di chuyển mà không làm mất hướng né đòn (roll direction)
        Vector3 movementTranslation = move;
        bool isCurrentlyAttacking = IsPlayingAttackState(out _, out _) || 
                                    (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f);
        if (IsLockingMovementAction())
        {
            movementTranslation = Vector3.zero;
        }

        transform.Translate(movementTranslation * currentSpeed * Time.deltaTime, Space.World);

        // Xoay nhân vật: Luôn xoay theo hướng Camera để hỗ trợ đi ngang/lùi (strafe) cho cả khi cầm vũ khí và tay không (Chỉ xoay khi không chơi hoạt ảnh hành động như lộn vòng, nhặt đồ, trúng đòn...)
        if (targetCamera != null && !IsPlayingActionAnimation())
        {
            Vector3 camForward = targetCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            if (camForward != Vector3.zero)
            {
                transform.forward = camForward;
            }
        }



        // Kích hoạt kỹ năng E
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (IsSpawned && !IsUIBlockingInput())
            {
                TriggerESkill();
            }
        }

        // Tấn công qua RPC (chỉ khi đã spawn trên mạng)
        if (Input.GetMouseButtonDown(0))
        {
            if (IsSpawned && !IsUIBlockingInput())
            {
                if (IsAiming)
                {
                    if (bowShootCooldownTimer <= 0f)
                    {
                        bowShootCooldownTimer = IsRSkillActive ? rSkillShootCooldown : bowShootCooldown;
                        PerformBowShoot(true);
                    }
                }
                else
                {
                    PerformComboAttack(true);
                }
            }
        }

        // Nhấn Ctrl (LeftControl) chơi hoạt ảnh LonVong + Nhào lộn né chiêu đồng bộ mạng
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            Debug.Log($"[Ctrl Debug] Owner Ctrl pressed! rollCooldownTimer = {rollCooldownTimer}, IsSpawned = {IsSpawned}, IsOwner = {IsOwner}");
            if (rollCooldownTimer <= 0 && IsSpawned && !IsUIBlockingInput())
            {
                StartRollOwner(move);
            }
        }
    }

    private void StartRollStandalone(Vector3 moveInput)
{
    isRollingStandalone = true;
    rollTimer = rollDuration;
    rollCooldownTimer = rollCooldown;
    
    ClearAttackLayer(); // Trả Layer 1 về Empty để lộn vòng cả thân người
    
    // Hướng nhào lộn: nếu có di chuyển thì lăn theo hướng WASD theo Camera, ngược lại lăn theo hướng đang nhìn
    if (moveInput != Vector3.zero)
    {
        rollDirection = moveInput.normalized;
    }
    else
    {
        rollDirection = transform.forward;
    }

    // Xoay ngay lập tức về hướng lộn
    if (rollDirection != Vector3.zero)
    {
        transform.rotation = Quaternion.LookRotation(rollDirection);
    }

    if (anim != null) anim.applyRootMotion = false; // TẮT ROOT MOTION
    PlayAnimation("LonVong", 0.05f);
}

    private void StartRollOwner(Vector3 moveInput)
{
    rollTimer = rollDuration;
    rollCooldownTimer = rollCooldown;
    
    ClearAttackLayer(); 
    
    if (moveInput != Vector3.zero)
    {
        rollDirection = moveInput.normalized;
    }
    else
    {
        rollDirection = transform.forward;
    }

    // Xoay ngay lập tức về hướng lộn
    if (rollDirection != Vector3.zero)
    {
        transform.rotation = Quaternion.LookRotation(rollDirection);
    }

    if (anim != null) anim.applyRootMotion = false; // TẮT ROOT MOTION
    PlayAnimation("LonVong", 0.05f, false); 
    StartRollServerRpc(rollDirection);
}

    public void OnRollEnd()
    {
        Debug.Log("[ElenaPlayer] OnRollEnd called.");
        isRollingStandalone = false;
        rollTimer = 0f;

        if (anim != null) anim.applyRootMotion = false;
        var bridge = GetRootMotionBridge();
        if (bridge != null) bridge.ApplyFinalOffset();

        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        if (!isStandaloneMode && IsOwner)
        {
            StopRollServerRpc();
        }
    }

    void LateUpdate()
    {
        float speed = 0f;
        if (Time.deltaTime > 0f)
        {
            speed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
        }
        lastPosition = transform.position;
        bool isMoving = speed > 0.2f;

        // Xoay cột sống (Spine) để hướng cánh tay/vũ khí về phía mục tiêu khi vừa chạy vừa đánh
        if (anim != null)
        {
            bool isCurrentlyAttacking = IsPlayingAttackState(out _, out _) || 
                                        (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f);
            
            float baseAimAngle = isStandaloneMode ? localAimAngle : netAimAngle.Value;
            
            // 1. Tính toán góc ĐÍCH (Target) dựa trên trạng thái hiện tại
            float targetYOffset = 0f;
            float targetXOffset = 0f;

            if (IsAiming)
            {
                float activeYOffset = isMoving ? aimMovingSpineYOffset : aimSpineYOffset;
                float activeXOffset = isMoving ? aimMovingSpineXOffset : aimSpineXOffset;

                if (isStandaloneMode)
                {
                    if (targetCamera != null)
                    {
                        // Twist spine left/right to match camera forward
                        Vector3 camForward = targetCamera.transform.forward;
                        camForward.y = 0f;
                        camForward.Normalize();
                        if (camForward != Vector3.zero)
                        {
                            float angleDiff = Vector3.SignedAngle(transform.forward, camForward, Vector3.up);
                            localAimAngle = Mathf.Clamp(angleDiff, -maxSpineTwistAngle, maxSpineTwistAngle);
                        }
                    }
                    float pitchFactor = invertSpinePitch ? -0.7f : 0.7f;
                    targetXOffset = (currentPitch - 40f) * pitchFactor + activeXOffset;
                    targetYOffset = activeYOffset;
                }
                else
                {
                    // Network Mode
                    if (IsOwner && targetCamera != null)
                    {
                        // Twist spine left/right to match camera forward
                        Vector3 camForward = targetCamera.transform.forward;
                        camForward.y = 0f;
                        camForward.Normalize();
                        if (camForward != Vector3.zero)
                        {
                            float angleDiff = Vector3.SignedAngle(transform.forward, camForward, Vector3.up);
                            netAimAngle.Value = Mathf.Clamp(angleDiff, -maxSpineTwistAngle, maxSpineTwistAngle);
                        }
                        netAimPitch.Value = currentPitch;
                    }

                    float pitchFactor = invertSpinePitch ? -0.7f : 0.7f;
                    targetXOffset = (netAimPitch.Value - 40f) * pitchFactor + activeXOffset;
                    targetYOffset = activeYOffset;
                }
            }
            else if (isCurrentlyAttacking && !isRootedAttack)
            {
                if (comboStep == 1)
                {
                    targetYOffset = punch1YOffset;
                    targetXOffset = punch1XOffset;
                }
                else if (comboStep == 2)
                {
                    targetYOffset = punch2YOffset;
                }
                else if (comboStep == 3)
                {
                    targetYOffset = punch3YOffset;
                }
            }

            // 2. LUÔN LUÔN LERP để các góc dịch chuyển mượt mà, không bao giờ bị khựng đột ngột
            smoothedYOffset = Mathf.Lerp(smoothedYOffset, targetYOffset, Time.deltaTime * spineSmoothSpeed);
            smoothedXOffset = Mathf.Lerp(smoothedXOffset, targetXOffset, Time.deltaTime * spineSmoothSpeed);

            // 3. Áp dụng góc xoay đã được làm mượt vào xương Spine
            // Thêm điều kiện Abs > 0.05f để xương vẫn được mượt mà trả về vị trí cũ sau khi đấm xong (khi isCurrentlyAttacking đã thành false)
            if (IsAiming || (isCurrentlyAttacking && !isRootedAttack) || Mathf.Abs(smoothedYOffset) > 0.05f || Mathf.Abs(smoothedXOffset) > 0.05f)
                {
                Transform spine = GetSpineBone();
                if (spine != null)
                {
                    // Cộng góc ngắm cơ bản với góc Offset Y đã làm mượt
                    float finalYAngle = baseAimAngle + smoothedYOffset;
                    
                    // Xoay Ngang mượt mà
                    spine.rotation = Quaternion.AngleAxis(finalYAngle, Vector3.up) * spine.rotation;

                    // Xoay Dọc mượt mà (chỉ áp dụng khi đòn 1 có tinh chỉnh cao thấp)
                    if (Mathf.Abs(smoothedXOffset) > 0.01f)
                    {
                        spine.rotation = Quaternion.AngleAxis(smoothedXOffset, transform.right) * spine.rotation;
                    }
                }
            }
        }

        // Giữ nguyên hoàn toàn đoạn code Camera follow phía dưới của bạn...

        // Camera follow hoạt động cho cả standalone lẫn Netcode owner
        bool shouldFollow = isStandaloneMode || (IsSpawned && IsOwner);
        if (!shouldFollow || !enableCameraFollow) return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindObjectOfType<Camera>();
        }

        if (targetCamera != null)
        {
            if (isCursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                targetYaw -= mouseX * cameraSensitivity;
                if (invertCameraY)
                {
                    targetPitch -= mouseY * cameraSensitivity;
                }
                else
                {
                    targetPitch += mouseY * cameraSensitivity;
                }
                float currentMinPitch = IsAiming ? aimMinPitch : minPitch;
                float currentMaxPitch = IsAiming ? aimMaxPitch : maxPitch;
                targetPitch = Mathf.Clamp(targetPitch, currentMinPitch, currentMaxPitch);
            }

            // Làm mượt mà các góc xoay (Yaw & Pitch) bằng Lerp để di chuyển chuột vẫn mượt
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * rotationSmoothSpeed);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * rotationSmoothSpeed);

            // Tính toán offset xoay dựa trên góc Yaw và Pitch đã được làm mượt
            float yawRad = currentYaw * Mathf.Deg2Rad;
            float pitchRad = currentPitch * Mathf.Deg2Rad;

            Vector3 rotatedOffset = new Vector3(
                cameraDistance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
                cameraDistance * Mathf.Sin(pitchRad),
                -cameraDistance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
            );

            // Smoothly interpolate shoulder offset, camera distance, and pivot height
            float targetDist = IsAiming ? aimCameraDistance : defaultCameraDistance;
            float targetPivot = IsAiming ? aimPivotHeight : defaultPivotHeight;
            float targetOffset = IsAiming ? aimShoulderOffset : 0f;

            cameraDistance = Mathf.Lerp(cameraDistance, targetDist, Time.deltaTime * aimCameraSmoothSpeed);
            cameraPivotHeight = Mathf.Lerp(cameraPivotHeight, targetPivot, Time.deltaTime * aimCameraSmoothSpeed);
            currentShoulderOffset = Mathf.Lerp(currentShoulderOffset, targetOffset, Time.deltaTime * aimCameraSmoothSpeed);

            Vector3 camRight = new Vector3(Mathf.Cos(yawRad), 0f, Mathf.Sin(yawRad));
            Vector3 rightOffsetVec = camRight * currentShoulderOffset;

            // Gắn cứng camera theo vị trí của nhân vật (Loại bỏ Lerp vị trí để giải quyết triệt để lỗi delay, zoom co giãn, và lệch nhân vật ra rìa)
            Vector3 targetPosition = (transform.position + Vector3.up * cameraPivotHeight) + rotatedOffset + rightOffsetVec;
            targetCamera.transform.position = targetPosition;

            if (cameraLookAtPlayer)
            {
                // Khóa camera luôn nhìn thẳng vào nhân vật (không dùng Slerp rotation) để nhân vật luôn nằm chính giữa màn hình
                targetCamera.transform.rotation = Quaternion.LookRotation(
                    ((transform.position + Vector3.up * cameraPivotHeight) + rightOffsetVec) - targetCamera.transform.position
                );
            }
        }
    }

    private float GetAttackDuration(int weaponIndex, int step)
    {
        if (weaponIndex == 1)
        {
            if (step == 1) return punch1Duration;
            if (step == 2) return punch2Duration;
            return punch3Duration;
        }
        else if (weaponIndex == 2)
        {
            if (step == 1) return chem1Duration;
            if (step == 2) return chem2Duration;
            return chem3Duration;
        }
        return 0.5f;
    }

    private void PerformComboAttack(bool networkMode)
    {
        int weapon = GetActiveWeaponIndex();
        float currentTime = Time.time;

        // Xác định bước combo tiếp theo trước để tính toán thời gian chờ
        int nextStep = comboStep;
        if (currentTime - lastAttackTime > comboWindow)
        {
            nextStep = 0;
        }
        nextStep++;

        // Giới hạn bước combo dựa trên vũ khí (Đấm có 3 combo, Chem có 3 combo)
        if (weapon == 1)
        {
            if (nextStep > 3) nextStep = 1;
        }
        else if (weapon == 2)
        {
            if (nextStep > 3) nextStep = 1;
        }
        else
        {
            nextStep = 1;
        }

        // Kiểm tra xem đòn đánh trước đó đã kết thúc chưa dựa trên thời gian thực tế trôi qua
        // (Sử dụng thời lượng của đòn đánh hiện tại trước khi chuyển sang đòn tiếp theo)
        if (comboStep > 0 && currentTime - lastAttackTime <= comboWindow)
        {
            float prevDuration = GetAttackDuration(weapon, comboStep);
            if (currentTime - lastAttackTime < prevDuration * comboTransitionThreshold)
            {
                return; // Chặn bấm nhanh/click liên tục khi đòn cũ chưa đánh xong
            }
        }

        // Kiểm tra xem người chơi có đang ấn phím di chuyển không để quyết định khóa chân (rooted)
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        bool isMovingInput = (Mathf.Abs(moveX) > 0.01f || Mathf.Abs(moveZ) > 0.01f);

        if (comboStep == 0 || currentTime - lastAttackTime > comboWindow)
        {
            isRootedAttack = !isMovingInput; // Đứng yên đánh thì khóa di chuyển vật lý (rooted), di chuyển/chạy đánh thì không khóa di chuyển (unrooted)
        }

        comboStep = nextStep;
        lastAttackTime = currentTime;

        string animToPlay = "";
        if (weapon == 1) // Unarmed / Fist combo (3 steps)
        {
            if (comboStep == 1) animToPlay = "Dam1";
            else if (comboStep == 2) animToPlay = "Dam2";
            else if (comboStep == 3) animToPlay = "Dam3";
        }
        else if (weapon == 2) // Weapon / Chem combo (3 steps)
        {
            if (comboStep == 1) animToPlay = "Chem1";
            else if (comboStep == 2) animToPlay = "Chem2";
            else if (comboStep == 3) animToPlay = "Chem3";
        }

        Debug.Log($"[Combo Debug] PerformComboAttack: weapon={weapon}, step={comboStep}, animToPlay={animToPlay}, isRooted={isRootedAttack}, isMovingInput={isMovingInput}");

        if (!string.IsNullOrEmpty(animToPlay))
        {
            PlayAnimation(animToPlay, 0.05f, false, isRootedAttack);
        }

        // Xác định hướng ngắm đánh (aim direction) dựa trên camera (nếu có), nếu không có thì dùng hướng transform.forward
        Vector3 aimDir = transform.forward;
        if (targetCamera != null)
        {
            aimDir = targetCamera.transform.forward;
            aimDir.y = 0f;
            aimDir.Normalize();
        }

        if (networkMode)
        {
            AttackServerRpc(aimDir);
        }
        else
        {
            Vector3 rayStart = transform.position + Vector3.up * 0.5f;
            Debug.DrawRay(rayStart, aimDir * attackRange, Color.red, 0.5f);

            if (!Physics.Raycast(rayStart, aimDir, out RaycastHit hit, attackRange)) return;

            TryDamageEnemy(hit.collider);
        }
    }

    private void UpdateWeaponVisualsInstant(int weaponIndex)
    {
        if (weaponOnBackVisual == null || weaponInHandVisual == null) return;

        if (weaponIndex == 2) // Nếu đang chọn Cung (Vũ khí số 2)
        {
            weaponOnBackVisual.SetActive(false);
            weaponInHandVisual.SetActive(true);
        }
        else // Nếu đang đi tay không (Vũ khí số 1)
        {
            weaponOnBackVisual.SetActive(true);
            weaponInHandVisual.SetActive(false);
        }
    }

    public void TryDamageEnemy(Collider col)
    {
        var e1 = col.GetComponentInParent<Enemy1_DapBua>();
        if (e1 != null) { e1.TakeDamage(damageAmount); return; }

        var e2 = col.GetComponentInParent<Enemy2_Zombie>();
        if (e2 != null) { e2.TakeDamage(damageAmount); return; }

        var e3 = col.GetComponentInParent<Enemy3_Buaa>();
        if (e3 != null) { e3.TakeDamage(damageAmount); return; }

        var e4 = col.GetComponentInParent<Enemy4_Bongtoi>();
        if (e4 != null) { e4.TakeDamage(damageAmount); return; }

        var e5 = col.GetComponentInParent<Enemy5_PhuThuy>();
        if (e5 != null) { e5.TakeDamage(damageAmount); return; }
    }

    // ------------------------------------------------------------------
    //  Server RPC Tấn công (chỉ dùng khi Netcode online)
    // ------------------------------------------------------------------
    [ServerRpc]
    void AttackServerRpc(Vector3 aimDir)
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Debug.DrawRay(rayStart, aimDir * attackRange, Color.red, 0.5f);

        if (!Physics.Raycast(rayStart, aimDir, out RaycastHit hit, attackRange)) return;

        var enemy1 = hit.collider.GetComponentInParent<Enemy1_DapBua>();
        if (enemy1 != null) { enemy1.TakeDamage(damageAmount); return; }

        var enemy2 = hit.collider.GetComponentInParent<Enemy2_Zombie>();
        if (enemy2 != null) { enemy2.TakeDamage(damageAmount); return; }

        var enemy3 = hit.collider.GetComponentInParent<Enemy3_Buaa>();
        if (enemy3 != null) { enemy3.TakeDamage(damageAmount); return; }

        var enemy4 = hit.collider.GetComponentInParent<Enemy4_Bongtoi>();
        if (enemy4 != null) { enemy4.TakeDamage(damageAmount); return; }

        var enemy5 = hit.collider.GetComponentInParent<Enemy5_PhuThuy>();
        if (enemy5 != null) { enemy5.TakeDamage(damageAmount); return; }
    }

    public void TakeDamage(float damage)
    {
        // Né chiêu (miễn nhiễm sát thương khi đang lộn vòng)
        if (isStandaloneMode)
        {
            if (isRollingStandalone)
            {
                Debug.Log($"[ElenaPlayer] {gameObject.name} đang né chiêu (lộn vòng), miễn nhiễm sát thương!");
                return;
            }
        }
        else
        {
            if (isRollingNet.Value)
            {
                Debug.Log($"[ElenaPlayer] {gameObject.name} đang né chiêu (lộn vòng), miễn nhiễm sát thương!");
                return;
            }
        }

        if (isStandaloneMode)
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            UpdateHealthHUD(localHealth);
            Debug.Log($"[ElenaPlayer] {gameObject.name} nhận {damage} sát thương. Máu còn: {localHealth}");

            var flash = GetComponent<MaterialFlashBehaviour>();
            if (flash == null) flash = gameObject.AddComponent<MaterialFlashBehaviour>();
            flash.Flash(Color.red, 0.15f);

            if (localHealth <= 0)
            {
                Debug.LogWarning($"[ElenaPlayer] {gameObject.name} đã chết!");
                PlayAnimation("Death", 0.15f);
            }
            else
            {
                string hitAnim = Random.value < 0.5f ? "GetHit" : "GeiHit2";
                PlayAnimation(hitAnim, 0.05f);
            }
            return;
        }

        if (!IsServer) return;

        currentHealth.Value = Mathf.Max(currentHealth.Value - damage, 0f);
        Debug.Log($"[ElenaPlayer] {gameObject.name} nhận {damage} sát thương. Máu còn lại: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
        {
            Debug.LogWarning($"[ElenaPlayer] {gameObject.name} đã chết!");
            PlayAnimation("Death", 0.15f);
        }
        else
        {
            string hitAnim = Random.value < 0.5f ? "GetHit" : "GeiHit2";
            PlayAnimation(hitAnim, 0.05f);
        }
    }

    public void Heal(float amount)
    {
        if (CurrentHealth <= 0) return;

        if (isStandaloneMode)
        {
            localHealth = Mathf.Min(localHealth + amount, maxHealth);
            UpdateHealthHUD(localHealth);
            Debug.Log($"[ElenaPlayer Standalone] Hồi {amount} máu. Máu hiện tại: {localHealth}");
        }
        else if (IsServer)
        {
            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
            Debug.Log($"[ElenaPlayer Server] Hồi {amount} máu cho {gameObject.name}. Máu hiện tại: {currentHealth.Value}");
        }
    }

    // ------------------------------------------------------------------
    //  Knockback
    // ------------------------------------------------------------------
    /// <summary>
    /// Giao diện áp dụng Knockback (Server-authoritative khi online, cục bộ khi standalone).
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        if (isStandaloneMode)
        {
            knockbackVelocity = force;
            return;
        }

        if (!IsServer) return;
        ApplyKnockbackClientRpc(force);
    }

    [ClientRpc]
    public void ApplyKnockbackClientRpc(Vector3 force)
    {
        if (IsOwner)
            knockbackVelocity = force;
    }

    // ==================================================================
    //  MongoDB & Netcode Integration Logic
    // ==================================================================

    public void UpdateStateFromHUD(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
    {
        // Xử lý riêng cho chế độ chơi đơn (Standalone Mode)
        if (isStandaloneMode)
        {
            int oldWeapon = localActiveWeaponIndex;
            localActiveWeaponIndex = weaponIndex;

            // Gọi hoạt ảnh rút/cất vũ khí ngay lập tức tại máy local
            PlayWeaponSwitchAnimation(oldWeapon, weaponIndex);
            return;
        }

        // Nếu chơi Online Netcode thì mới check điều kiện và bắn RPC lên Server
        if (!IsSpawned || !IsOwner) return;
        UpdateStateServerRpc(weaponIndex, weapon2Locked, skillsUnlocked);
    }

    [ServerRpc]
    private void UpdateStateServerRpc(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
    {
        activeWeaponIndex.Value = weaponIndex;
        isWeapon2Locked.Value = weapon2Locked;
        isSkillsUnlocked.Value = skillsUnlocked;
        SavePlayerStateClientRpc();
    }

    private async void LoadPlayerStateFromDatabase()
    {
        Debug.Log("[DB] Bắt đầu tải trạng thái người chơi từ MongoDB Atlas...");
        try
        {
            var res = await AuthService.GetPlayerState();
            if (res != null && res.success && res.playerState != null)
            {
                var state = res.playerState;
                Debug.Log($"[DB] Đã tải thành công trạng thái người chơi từ MongoDB! Máu: {state.health}");
                
                SyncPlayerStateServerRpc(
                    state.health, 
                    state.activeWeaponIndex, 
                    true, // Khóa vũ khí 2 mặc định khi bắt đầu game
                    false, // Khóa kỹ năng mặc định khi bắt đầu game
                    state.upgradePoints,
                    state.hpLevel,
                    state.mpLevel,
                    state.cooldownLevel,
                    state.damageLevel,
                    state.playerLevel,
                    state.playerExp
                );

                if (state.inventorySlots != null)
                {
                    for (int i = 0; i < inventorySlots.Length && i < state.inventorySlots.Length; i++)
                    {
                        inventorySlots[i] = state.inventorySlots[i];
                    }
                }

                PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                if (hud != null)
                {
                    hud.SetInventorySlots(inventorySlots);
                    hud.SetSkillsUnlocked(false, false); // Khóa kỹ năng mặc định
                    hud.SetWeapon2Locked(true, false); // Khóa vũ khí 2 mặc định
                    hud.SelectWeapon(state.activeWeaponIndex);
                    // Cập nhật lại UI sau khi các NetworkVariables được đồng bộ
                    hud.UpdateUpgradeUI(state.upgradePoints, state.hpLevel, state.mpLevel, state.cooldownLevel, state.damageLevel);
                    hud.SetHealth(state.health / (100f + state.hpLevel * 20f));
                    
                    float needed = 100f + state.playerLevel * 50f;
                    hud.UpdateExperienceUI(state.playerLevel, state.playerExp, needed);
                }
            }
            else
            {
                Debug.LogWarning("[DB] Không có dữ liệu cũ hoặc lỗi kết nối. Đồng bộ dữ liệu ban đầu.");
                SyncPlayerStateServerRpc(maxHealth, 1, true, false, 0, 0, 0, 0, 0, 0, 0f);
                SavePlayerStateToDatabase();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Lỗi khi kết nối API tải dữ liệu MongoDB: {ex.Message}");
            SyncPlayerStateServerRpc(maxHealth, 1, true, false, 0, 0, 0, 0, 0, 0, 0f);
        }
    }

    [ServerRpc]
    private void SyncPlayerStateServerRpc(
        float health, 
        int weaponIndex, 
        bool weapon2Locked, 
        bool skillsUnlocked,
        int pts,
        int hp,
        int mp,
        int cd,
        int dmg,
        int levelVal,
        float expVal
    )
    {
        isSyncingFromDb = true;

        upgradePoints.Value = pts;
        hpLevel.Value = hp;
        mpLevel.Value = mp;
        cooldownLevel.Value = cd;
        damageLevel.Value = dmg;
        playerLevel.Value = levelVal;
        playerExp.Value = expVal;

        // Đồng bộ trước maxHealth tác động của chỉ số lên server
        maxHealth = 100f + hp * 20f;
        damageAmount = 20f + dmg * 5f;

        currentHealth.Value = health;
        activeWeaponIndex.Value = weaponIndex;
        isWeapon2Locked.Value = weapon2Locked;
        isSkillsUnlocked.Value = skillsUnlocked;

        isSyncingFromDb = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerNameServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        playerName.Value = name;
    }

    public async void SavePlayerStateToDatabase()
    {
        if (!IsSpawned || !IsOwner) return;

        Debug.Log("[DB] Đang tự động lưu trạng thái nhân vật lên MongoDB...");
        try
        {
            var stateData = new PlayerStateData
            {
                health = CurrentHealth,
                activeWeaponIndex = activeWeaponIndex.Value,
                isWeapon2Locked = isWeapon2Locked.Value,
                isSkillsUnlocked = isSkillsUnlocked.Value,
                inventorySlots = inventorySlots,
                upgradePoints = upgradePoints.Value,
                hpLevel = hpLevel.Value,
                mpLevel = mpLevel.Value,
                cooldownLevel = cooldownLevel.Value,
                damageLevel = damageLevel.Value,
                playerLevel = playerLevel.Value,
                playerExp = playerExp.Value
            };

            var res = await AuthService.SavePlayerState(stateData);
            if (res != null && res.success)
            {
                Debug.Log("[DB] Đã lưu trạng thái nhân vật lên MongoDB Atlas thành công!");
            }
            else
            {
                Debug.LogError($"[DB] Lỗi lưu trữ MongoDB: {res?.message}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Lỗi khi gọi API lưu trạng thái MongoDB: {ex.Message}");
        }
    }

    [ClientRpc]
    private void SavePlayerStateClientRpc()
    {
        if (IsOwner)
        {
            SavePlayerStateToDatabase();
        }
    }

    // ==================================================================
    //  Animation Management System (Supports Direct Play & Triggers)
    // ==================================================================


    private float lastActionTriggerTime = 0f;
    private string lastTriggeredAnimName = "";

    private bool IsActionAnimationName(string name)
    {
        return name == "LonVong" || 
               name == "GetHit" || 
               name == "GeiHit2" || 
               name == "Idle_Pick" || 
               name == "Death" ||
               name == "Dam1" ||
               name == "Dam2" ||
               name == "Dam3" ||
               name == "Chem1" ||
               name == "Chem2" ||
               name == "Chem3" ||
               name == "Bow_Shoot" ||
               (!string.IsNullOrEmpty(drawWeaponTrigger) && name == drawWeaponTrigger) ||
               (!string.IsNullOrEmpty(sheathWeaponTrigger) && name == sheathWeaponTrigger);
    }

    public void PlayWeaponSwitchAnimation(int oldWeapon, int newWeapon)
    {
        if (oldWeapon == newWeapon) return;

        if (newWeapon == 2)
        {
            if (!string.IsNullOrEmpty(drawWeaponTrigger))
            {
                PlayAnimationLocal(drawWeaponTrigger, 0.1f, false);
            }
        }
        else if (newWeapon == 1)
        {
            if (!string.IsNullOrEmpty(sheathWeaponTrigger))
            {
                PlayAnimationLocal(sheathWeaponTrigger, 0.1f, false);
            }
        }
    }

    private bool IsAttackAnimationName(string name)
    {
        return name == "Dam1" || 
               name == "Dam2" || 
               name == "Dam3" || 
               name == "Chem1" ||
               name == "Chem2" ||
               name == "Chem3";
    }

    private bool IsPlayingAttackState(out AnimatorStateInfo activeState, out int layer)
    {
        activeState = default;
        layer = -1;

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
            return false;

        // Ưu tiên check Layer 1 (AttackLayer) trước vì đòn đánh có thể đè ở đây qua Avatar Mask
        if (anim.layerCount > 1)
        {
            AnimatorStateInfo attackLayerStateInfo = anim.GetCurrentAnimatorStateInfo(1);
            if (IsAttackState(attackLayerStateInfo))
            {
                activeState = attackLayerStateInfo;
                layer = 1;
                return true;
            }
            if (anim.IsInTransition(1))
            {
                AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(1);
                if (IsAttackState(nextStateInfo))
                {
                    activeState = nextStateInfo;
                    layer = 1;
                    return true;
                }
            }
        }

        // Check Layer 0 (Base Layer)
        AnimatorStateInfo baseLayerStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (IsAttackState(baseLayerStateInfo))
        {
            activeState = baseLayerStateInfo;
            layer = 0;
            return true;
        }
        if (anim.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(0);
            if (IsAttackState(nextStateInfo))
            {
                activeState = nextStateInfo;
                layer = 0;
                return true;
            }
        }

        return false;
    }

    private bool IsAttackState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("Dam1") || 
               stateInfo.IsName("Dam2") || 
               stateInfo.IsName("Dam3") || 
               stateInfo.IsName("Chem1") ||
               stateInfo.IsName("Chem2") ||
               stateInfo.IsName("Chem3") ||
               stateInfo.IsName("Chem_1") ||
               stateInfo.IsName("Chem_2") ||
               stateInfo.IsName("Chem_3");
    }

    private bool IsStatePlayingOnAnyLayer(string stateName)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
            return false;

        for (int i = 0; i < anim.layerCount; i++)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(i);
            if (stateInfo.IsName(stateName))
                return true;
            if (anim.IsInTransition(i))
            {
                AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(i);
                if (nextStateInfo.IsName(stateName))
                    return true;
            }
        }
        return false;
    }

    private bool IsStatePlayingOnLayer1(string stateName)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null || anim.layerCount <= 1)
            return false;

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(1);
        if (stateInfo.IsName(stateName))
            return true;
        if (anim.IsInTransition(1))
        {
            AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(1);
            if (nextStateInfo.IsName(stateName))
                return true;
        }
        return false;
    }

    private bool IsFullBodyActionAnimation(string name)
    {
        return name == "LonVong" || 
               name == "GetHit" || 
               name == "GeiHit2" || 
               name == "Idle_Pick" || 
               name == "Death";
    }

    private bool IsLockingMovementAction()
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

        // Nhào lộn (rolling) luôn khóa di chuyển tự do
        if (isStandaloneMode ? isRollingStandalone : rollTimer > 0) return true;

        // Nếu đang trong trạng thái tấn công khóa di chuyển (isRootedAttack == true và đang chơi hoặc chuẩn bị chơi đòn đánh)
        bool isCurrentlyAttacking = IsPlayingAttackState(out _, out _) || 
                                    (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f);
        if (isRootedAttack && isCurrentlyAttacking)
        {
            return true;
        }



        // Các trạng thái toàn thân đặc biệt cần khóa di chuyển (trúng đòn, nhặt đồ, chết, nhào lộn)
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool isFullBodyAction = stateInfo.IsName("LonVong") || 
                               stateInfo.IsName("GetHit") || 
                               stateInfo.IsName("GeiHit2") || 
                               stateInfo.IsName("Idle_Pick") || 
                               stateInfo.IsName("Death");

        if (!isFullBodyAction && anim.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(0);
            isFullBodyAction = nextStateInfo.IsName("LonVong") || 
                               nextStateInfo.IsName("GetHit") || 
                               nextStateInfo.IsName("GeiHit2") || 
                               nextStateInfo.IsName("Idle_Pick") || 
                               nextStateInfo.IsName("Death");
            if (isFullBodyAction)
            {
                stateInfo = nextStateInfo;
            }
        }

        return isFullBodyAction && stateInfo.normalizedTime < 0.95f;
    }

    private bool IsPlayingActionAnimation()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

        // Nếu vừa kích hoạt trigger hành động trong vòng 0.35 giây, coi như đang chạy action animation
        if (IsFullBodyActionAnimation(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f) return true;

        // Khi đang nhào lộn (rolling), coi như đang chạy action animation
        if (isStandaloneMode ? isRollingStandalone : rollTimer > 0) return true;

        // Nếu chỉ có 1 layer (Base Layer) HOẶC đòn tấn công đang chạy trên Layer 0 (khi khóa di chuyển - tức là isRootedAttack == true),
        // ta cần chặn các hoạt ảnh di chuyển (Idle, Walk, run) ghi đè lên trên Layer 0.
        bool isAttackOnLayer0 = (anim.layerCount == 1) || (anim.layerCount > 1 && isRootedAttack);
        if (isAttackOnLayer0)
        {
            // Nếu vừa kích hoạt tấn công trong vòng 0.35 giây, coi nó như hành động để tránh ghi đè trước khi animator kịp chuyển trạng thái
            if (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f) return true;

            // 1. Nếu là đòn đánh, ngăn chặn Idle/Walk/run ghi đè lên cho tới khi animator thoát khỏi trạng thái đấm/chém
            if (IsPlayingAttackState(out _, out int attackLayer) && attackLayer == 0)
            {
                return true;
            }

            // 1.5. Nếu đang trong chuỗi combo tấn công và thời gian trôi qua chưa vượt quá thời lượng đòn đánh hiện tại (+ buffer 0.1s), coi như đang chạy action để tránh ghi đè
            int activeWeapon = GetActiveWeaponIndex();
            float currentAttackDuration = GetAttackDuration(activeWeapon, comboStep);
            if (comboStep > 0 && Time.time - lastAttackTime < currentAttackDuration + 0.1f)
            {
                return true;
            }
        }



        // 2. Kiểm tra các hành động toàn thân khác (như lộn vòng, trúng đòn, chết, nhặt đồ) trên Layer 0
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool isFullBodyAction = stateInfo.IsName("LonVong") || 
                               stateInfo.IsName("GetHit") || 
                               stateInfo.IsName("GeiHit2") || 
                               stateInfo.IsName("Idle_Pick") || 
                               stateInfo.IsName("Death");

        if (!isFullBodyAction && anim.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(0);
            isFullBodyAction = nextStateInfo.IsName("LonVong") || 
                               nextStateInfo.IsName("GetHit") || 
                               nextStateInfo.IsName("GeiHit2") || 
                               nextStateInfo.IsName("Idle_Pick") || 
                               nextStateInfo.IsName("Death");
            if (isFullBodyAction)
            {
                stateInfo = nextStateInfo;
            }
        }

        bool isAttackPlaying = IsPlayingAttackState(out _, out _);
        if (isAttackPlaying || IsAttackAnimationName(lastTriggeredAnimName))
        {
            Debug.Log($"[Combo Debug] IsPlayingActionAnimation=false: lastTriggered={lastTriggeredAnimName}, timeDiff={Time.time - lastActionTriggerTime:F2}, isRooted={isRootedAttack}, isAttackPlaying={isAttackPlaying}");
        }

        return isFullBodyAction && stateInfo.normalizedTime < 0.95f;
    }

    private bool HasParameter(string paramName)
    {
        if (anim == null) return false;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    private void SafeResetTrigger(string paramName)
    {
        if (anim != null && HasParameter(paramName))
        {
            anim.ResetTrigger(paramName);
        }
    }

    private void SafeSetTrigger(string paramName)
    {
        if (anim != null && HasParameter(paramName))
        {
            anim.SetTrigger(paramName);
        }
    }

    public void PlayAnimation(string animName, float fadeTime = 0.1f, bool alreadyPlayedLocally = false, bool isRooted = false)
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        
        if (!alreadyPlayedLocally)
        {
            PlayAnimationLocal(animName, fadeTime, isRooted);
        }

        if (!isStandaloneMode)
        {
            if (IsServer)
            {
                PlayAnimationClientRpc(animName, fadeTime, alreadyPlayedLocally, isRooted);
            }
            else if (IsOwner)
            {
                PlayAnimationServerRpc(animName, fadeTime, isRooted);
            }
        }
    }

    private void PlayAnimationLocal(string animName, float fadeTime, bool isRooted)
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        if (anim == null)
        {
            Debug.LogError($"[Animator Debug] KHÔNG tìm thấy component Animator trên {gameObject.name} hoặc các con của nó!");
            return;
        }

        if (!anim.isActiveAndEnabled)
        {
            Debug.LogError($"[Animator Debug] Animator trên {anim.gameObject.name} đang bị VÔ HIỆU HÓA (disabled)!");
            return;
        }

        if (anim.runtimeAnimatorController == null)
        {
            Debug.LogError($"[Animator Debug] Animator trên {anim.gameObject.name} CHƯA ĐƯỢC GÁN Animator Controller!");
            return;
        }
        
        // Tránh lặp lại các hoạt ảnh di chuyển lặp đi lặp lại hàng frame (Idle, run, Walk)
        bool isLoopingAnim = animName == "Idle" || animName == "Walk" || animName == "run";
        if (isLoopingAnim && currentAnimState == animName) return;

        Debug.Log($"[Animator Debug] {gameObject.name} kích hoạt Trigger hoạt ảnh: '{animName}' (isRooted={isRooted}, comboStep={comboStep})");

        // Reset các trigger di chuyển cơ bản để tránh kẹt
        SafeResetTrigger("Idle");
        SafeResetTrigger("Walk");
        SafeResetTrigger("run");
        SafeResetTrigger("Death");
        SafeResetTrigger("GetHit");
        SafeResetTrigger("GeiHit2");
        SafeResetTrigger("LonVong");
        SafeResetTrigger("Idle_Pick");
        SafeResetTrigger("Bow_Shoot");

        // Chỉ dọn dẹp (reset) các trigger combo tấn công khi chuẩn bị kích hoạt một hành động mới
        // (để tránh việc nhân vật di chuyển làm reset mất trigger đòn đấm trên layer Upper Body)
        if (IsActionAnimationName(animName))
        {
            SafeResetTrigger("Dam1");
            SafeResetTrigger("Dam2");
            SafeResetTrigger("Dam3");
            SafeResetTrigger("Bow_Shoot");
            if (!string.IsNullOrEmpty(drawWeaponTrigger)) SafeResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) SafeResetTrigger(sheathWeaponTrigger);
        }

        // Kích hoạt Trigger để chạy dây nối trong Animator
        // Kích hoạt hoạt ảnh: Sử dụng CrossFade cho các đòn đánh để ép buộc chuyển cảnh ngay lập tức, tránh lỗi dây nối Animator
        if (IsAttackAnimationName(animName))
        {
            if (anim.layerCount > 1)
            {
                if (isRooted)
                {
                    // Đứng yên đánh (Idle Attack) -> không sử dụng Avatar Mask: chạy trên Layer 0 (Base Layer) và tắt Weight của Layer 1 về 0
                    anim.SetLayerWeight(1, 0f);
                    anim.CrossFadeInFixedTime(animName, fadeTime, 0);
                }
                else
                {
                    // Di chuyển/chạy đánh (Walk/Run Attack) -> sử dụng Avatar Mask: chạy trên Layer 1 với Weight = 1
                    anim.SetLayerWeight(1, 1f);
                    anim.CrossFadeInFixedTime(animName, fadeTime, 1);
                }
            }
            else
            {
                anim.CrossFadeInFixedTime(animName, fadeTime, 0);
            }
        }
        else
        {
            if (anim.layerCount > 1 && (animName == drawWeaponTrigger || animName == sheathWeaponTrigger || animName == "Bow_Shoot"))
            {
                anim.SetLayerWeight(1, 1f);
            }
            if (animName == "Bow_Shoot" && arrowHandVisual != null)
            {
                arrowHandVisual.SetActive(false);
            }
            SafeSetTrigger(animName);
        }

        // Chỉ cập nhật currentAnimState cho các hoạt ảnh di chuyển hoặc hoạt ảnh hành động toàn thân (không phải đòn đánh di chuyển)
        bool isMovingAttack = IsAttackAnimationName(animName) && !isRooted;
        if (!isMovingAttack)
        {
            currentAnimState = animName;
        }
        lastTriggeredAnimName = animName;

        if (IsActionAnimationName(animName))
        {
            lastActionTriggerTime = Time.time;
        }

        if (IsFullBodyActionAnimation(animName))
        {
            ClearAttackLayer();
        }
    }

    [ServerRpc]
    private void PlayAnimationServerRpc(string animName, float fadeTime, bool isRooted)
    {
        PlayAnimationClientRpc(animName, fadeTime, true, isRooted);
    }

    [ClientRpc]
    private void PlayAnimationClientRpc(string animName, float fadeTime, bool alreadyPlayedLocally, bool isRooted)
    {
        if (alreadyPlayedLocally && IsOwner) return; // Chủ sở hữu đã tự chạy hoạt ảnh local rồi
        PlayAnimationLocal(animName, fadeTime, isRooted);
    }

    [ServerRpc]
private void StartRollServerRpc(Vector3 direction)
{
    isRollingNet.Value = true;
    if (direction != Vector3.zero)
    {
        rollDirection = direction; // Lưu lại biến hướng trên server nếu cần
        transform.rotation = Quaternion.LookRotation(direction);
    }
    // Server phát RPC hoạt ảnh LonVong cho tất cả client khác
    PlayAnimationClientRpc("LonVong", 0.05f, true, false);
}

    [ServerRpc]
    private void StopRollServerRpc()
    {
        isRollingNet.Value = false;
    }

    private void ClearAttackLayer()
    {
        comboStep = 0;
        isRootedAttack = false;
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            anim.SetLayerWeight(1, 0f); // Reset weight của Layer 1 về 0
            // Reset Layer 1 (AttackLayer) về trạng thái Empty/New State mặc định
            anim.Play("New State", 1, 0f);
        }
    }

    // ==================================================================
    //  Animation Events (Được gọi tự động từ Animation Clips)
    // ==================================================================

    // Gọi tại frame tay chạm vào lưng để rút vũ khí
    public void OnWeaponDrawGrab()
    {
        if (weaponOnBackVisual != null) weaponOnBackVisual.SetActive(false);
        if (weaponInHandVisual != null) weaponInHandVisual.SetActive(true);
        Debug.Log("[Animation Event] Đã rút vũ khí lên tay!");
    }

    // Gọi tại frame tay đặt vũ khí vào sau lưng để cất
    public void OnWeaponSheathPlace()
    {
        if (weaponOnBackVisual != null) weaponOnBackVisual.SetActive(true);
        if (weaponInHandVisual != null) weaponInHandVisual.SetActive(false);
        Debug.Log("[Animation Event] Đã cất vũ khí vào lưng!");
    }

    private float lastTimeUIOpen = 0f;

    public bool IsUIBlockingInput()
    {
        bool uiOpen = false;
        if (PlayerHUDController.isAnyUIOpen) uiOpen = true;

        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);
        if (isDialogueOpen) uiOpen = true;

        if (uiOpen)
        {
            lastTimeUIOpen = Time.time;
            return true;
        }
        if (Time.time - lastTimeUIOpen < 0.15f) return true;

        return false;
    }

    private void OnAimingNetChanged(bool oldVal, bool newVal)
    {
        if (!IsOwner)
        {
            OnAimStateChanged(newVal);
        }
    }

    private void OnAimStateChanged(bool aiming)
    {
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            anim.SetBool("IsAiming", aiming);
            if (anim.layerCount > 1)
            {
                if (aiming)
                {
                    anim.SetLayerWeight(1, 1f);
                }
                else if (comboStep == 0)
                {
                    anim.SetLayerWeight(1, 0f);
                    anim.Play("New State", 1, 0f);
                    if (arrowHandVisual != null)
                    {
                        arrowHandVisual.SetActive(false);
                    }
                }
            }
        }

        bool isLocal = isStandaloneMode || (IsSpawned && IsOwner);
        if (isLocal)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.SetCrosshairVisible(aiming);
            }
        }
    }

    [ServerRpc]
    private void SetAimingServerRpc(bool aiming)
    {
        isAimingNet.Value = aiming;
    }

    // Animation Event: Được gọi từ hoạt ảnh LayCung hoặc Bow_Draw để kích hoạt mũi tên trên tay
    public void OnDrawArrow()
    {
        if (arrowHandVisual != null)
        {
            arrowHandVisual.SetActive(true);
        }
    }

    private void PerformBowShoot(bool networkMode)
    {
        PlayAnimation("Bow_Shoot", 0.05f);

        if (arrowHandVisual != null)
        {
            arrowHandVisual.SetActive(false);
        }

        if (targetCamera != null)
        {
            Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 rayOrigin = ray.origin;
            Vector3 rayDirection = ray.direction;

            Vector3 targetPoint = rayOrigin + rayDirection * aimAttackRange;
            if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit cameraHit, aimAttackRange))
            {
                targetPoint = cameraHit.point;
            }
            Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position + Vector3.up * cameraPivotHeight;
            Vector3 shootDirection = (targetPoint - spawnPos).normalized;

            if (networkMode)
            {
                BowShootServerRpc(spawnPos, shootDirection);
            }
            else
            {
                if (arrowPrefab != null)
                {
                    if (IsQSkillActive)
                    {
                        Vector3 dirLeft = Quaternion.Euler(0f, -qSkillSpreadAngle, 0f) * shootDirection;
                        Vector3 dirRight = Quaternion.Euler(0f, qSkillSpreadAngle, 0f) * shootDirection;

                        SpawnArrowLocal(spawnPos, shootDirection);
                        SpawnArrowLocal(spawnPos, dirLeft);
                        SpawnArrowLocal(spawnPos, dirRight);
                    }
                    else
                    {
                        SpawnArrowLocal(spawnPos, shootDirection);
                    }
                }
                else
                {
                    Debug.LogError("[PerformBowShoot] arrowPrefab chưa được gán trong Inspector của ElenaPlayer!");
                }
            }
        }
    }

    private void SpawnArrowLocal(Vector3 spawnPos, Vector3 shootDirection)
    {
        Debug.Log($"[PerformBowShoot] Đang bắn tên ở chế độ Standalone. Vị trí spawn: {spawnPos}, Hướng bắn: {shootDirection}");
        GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        arrowObj.transform.localScale = arrowPrefab.transform.localScale;
        arrowObj.SetActive(true);
        
        if (arrowObj.TryGetComponent<ArrowProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = damageAmount;
            proj.speed = arrowSpeed;
            
            // Xử lý đạn xuyên thấu E-skill
            if (localIsESkillActive && eSkillRemainingArrows > 0)
            {
                proj.isPiercing = true;
                eSkillRemainingArrows--;
                if (eSkillRemainingArrows <= 0)
                {
                    localIsESkillActive = false;
                    StartESkillCooldown();
                }
            }
            else
            {
                proj.isPiercing = false;
            }
        }
    }

    [ServerRpc]
    private void BowShootServerRpc(Vector3 spawnPos, Vector3 shootDirection)
    {
        // Kiểm tra hợp lệ khoảng cách trên Server để tránh lag giật tọa độ
        if (Vector3.Distance(spawnPos, transform.position) > 4f)
        {
            spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position + Vector3.up * cameraPivotHeight;
        }

        if (arrowPrefab != null)
        {
            if (isQSkillActiveNet.Value)
            {
                Vector3 dirLeft = Quaternion.Euler(0f, -qSkillSpreadAngle, 0f) * shootDirection;
                Vector3 dirRight = Quaternion.Euler(0f, qSkillSpreadAngle, 0f) * shootDirection;

                SpawnArrowServer(spawnPos, shootDirection);
                SpawnArrowServer(spawnPos, dirLeft);
                SpawnArrowServer(spawnPos, dirRight);
            }
            else
            {
                SpawnArrowServer(spawnPos, shootDirection);
            }
        }
        else
        {
            Debug.LogError("[BowShootServerRpc] arrowPrefab chưa được gán trên Server!");
        }
    }

    private void SpawnArrowServer(Vector3 spawnPos, Vector3 shootDirection)
    {
        Debug.Log($"[BowShootServerRpc] Server đang spawn tên. Vị trí: {spawnPos}, Hướng bắn: {shootDirection}");
        GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        arrowObj.transform.localScale = arrowPrefab.transform.localScale;
        arrowObj.SetActive(true);
        
        if (arrowObj.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn(true);
        }
        if (arrowObj.TryGetComponent<ArrowProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = damageAmount;
            proj.speed = arrowSpeed;
            
            // Xử lý đạn xuyên thấu E-skill trên Server
            if (isESkillActiveNet.Value && eSkillRemainingArrowsNet.Value > 0)
            {
                proj.isPiercing = true;
                eSkillRemainingArrowsNet.Value--;
                if (eSkillRemainingArrowsNet.Value <= 0)
                {
                    isESkillActiveNet.Value = false;
                    eSkillCooldownTimer = eSkillCooldown;
                    StartESkillCooldown();
                }
            }
            else
            {
                proj.isPiercing = false;
            }
        }
    }

    public override void OnDestroy()
    {
        if (PlayerHUDManager.ActivePlayers != null)
        {
            PlayerHUDManager.ActivePlayers.Remove(this);
        }
        base.OnDestroy();
    }
}
