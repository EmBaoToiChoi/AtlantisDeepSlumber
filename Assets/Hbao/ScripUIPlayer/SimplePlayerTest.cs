using Unity.Netcode;
using UnityEngine;

public class SimplePlayerTest : NetworkBehaviour, IPlayerHUDTarget
{
    protected LeoPlayer leoPlayer;
    protected ArthurPlayer arthurPlayer;

    [Header("Movement & Attack Settings")]
    public float moveSpeed = 5f;
    public float runSpeedMultiplier = 1.5f;
    public float damageAmount = 20f;
    public float attackRange = 3f;

    [Header("Combo Attack Settings")]
    public float comboWindow = 1.0f;
    public float comboTransitionThreshold = 0.5f;
    public float punch1Duration = 0.5f;
    public float punch2Duration = 0.5f;
    public float slash1Duration = 0.6f;
    public float slash2Duration = 0.6f;
    public float slash3Duration = 0.7f;
    protected int comboStep = 0;
    protected float lastAttackTime = 0f;
    protected bool isRootedAttack = false;

    [Header("Weapon Switch Animations")]
    public string drawWeaponTrigger = "DrawWeapon";
    public string sheathWeaponTrigger = "SheathWeapon";

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
    protected int localUpgradePoints = 0;
    protected int localHpLevel = 0;
    protected int localMpLevel = 0;
    protected int localCooldownLevel = 0;
    protected int localDamageLevel = 0;
    protected int localLevel = 0;
    protected float localExp = 0f;

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
    protected float localWeapon1Durability = 100f;
    protected float localWeapon2Durability = 100f;

    [Header("Player Class Settings")]
    [Tooltip("0 = Sát Thủ, 1 = Hỏa Thuật, 2 = Cung Thủ, 3 = Tanker")]
    public int characterClassIndex = 0;

    [Header("Player Name Sync")]
    public NetworkVariable<Unity.Collections.FixedString64Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Explorer", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // IPlayerHUDTarget Stats Implementation
    public string DisplayName => string.IsNullOrEmpty(playerName.Value.ToString()) ? "Explorer" : playerName.Value.ToString();
    public int PlayerLevel => isStandaloneMode ? localLevel : playerLevel.Value;
    public float PlayerExp => isStandaloneMode ? localExp : playerExp.Value;
    public float MaxExp => 100f + (isStandaloneMode ? localLevel : playerLevel.Value) * 50f;

    [Header("Knockback Settings")]
    protected Vector3 knockbackVelocity;

    [Header("Camera Follow Settings")]
    public bool enableCameraFollow = true;
    public Vector3 cameraOffset = new Vector3(0f, 12f, -8f);
    public float cameraSmoothSpeed = 5f;
    public bool cameraLookAtPlayer = true;
    public float cameraPivotHeight = 1.0f;
    protected Camera targetCamera;

    [Header("Camera Rotation Settings")]
    public float cameraSensitivity = 2f;
    public float minPitch = 10f;
    public float maxPitch = 80f;
    public float rotationSmoothSpeed = 15f;
    protected float currentYaw = 0f;
    protected float currentPitch = 45f;
    protected float targetYaw = 0f;
    protected float targetPitch = 45f;
    protected float cameraDistance = 14f;
    protected bool isCursorLocked = true;

    [Header("Animation Settings")]
    public Animator anim;
    protected string currentAnimState;

    [Header("Dodge Roll Settings")]
    public float rollSpeed = 10f;
    public float rollDuration = 0.4f;
    public float rollCooldown = 1.2f;
    protected float rollCooldownTimer;
    protected float rollTimer;
    protected Vector3 rollDirection;
    public NetworkVariable<bool> isRollingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    protected bool isRollingStandalone = false;
    private RootMotionBridge rootMotionBridge;
    protected RootMotionBridge GetRootMotionBridge()
    {
        if (rootMotionBridge == null && anim != null)
        {
            rootMotionBridge = anim.GetComponent<RootMotionBridge>();
        }
        return rootMotionBridge;
    }

    // ------------------------------------------------------------------
    //  Biến nội bộ cho chế độ Standalone (không có Netcode)
    // ------------------------------------------------------------------
    protected float localHealth;
    public bool isStandaloneMode = false; // true khi chạy đơn lẻ không qua NetworkManager
    protected bool isSyncingFromDb = false; // true khi đang đồng bộ dữ liệu ban đầu từ DB tránh hồi máu ảo

    /// <summary>
    /// Trả về true nếu NetworkManager đang hoạt động và đã kết nối/host.
    /// </summary>
    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    /// <summary>
    /// Máu hiện tại: đọc từ NetworkVariable khi online, đọc từ biến local khi standalone.
    /// </summary>
    public float CurrentHealth => leoPlayer != null ? leoPlayer.CurrentHealth : (arthurPlayer != null ? arthurPlayer.CurrentHealth : (isStandaloneMode ? localHealth : currentHealth.Value));

    // IPlayerHUDTarget Implementation
    bool IPlayerHUDTarget.isStandaloneMode => isStandaloneMode;
    public bool IsStandaloneMode => isStandaloneMode;
    public int CharacterClassIndex => characterClassIndex;
    public bool IsSwitchingWeapon => false;
    public float Weapon1MaxDurability => weapon1MaxDurability;
    public float Weapon2MaxDurability => weapon2MaxDurability;
    public string[] InventorySlots => inventorySlots;
    public float MaxHealth => maxHealth;

    // Invisibility Skill R proxy
    public bool IsInvisible => leoPlayer != null ? leoPlayer.IsInvisible : false;
    public float InvisibilityTimeRemaining => leoPlayer != null ? leoPlayer.InvisibilityTimeRemaining : 0f;
    public void TriggerInvisibilitySkill()
    {
        if (leoPlayer != null) leoPlayer.TriggerInvisibilitySkill();
    }

    // Attack Speed Boost Skill E proxy
    public bool IsAttackSpeedBoosted => leoPlayer != null ? leoPlayer.IsAttackSpeedBoosted : false;
    public float AttackSpeedBoostTimeRemaining => leoPlayer != null ? leoPlayer.AttackSpeedBoostTimeRemaining : 0f;
    public void TriggerAttackSpeedBoostSkill()
    {
        if (leoPlayer != null) leoPlayer.TriggerAttackSpeedBoostSkill();
    }

    // Q Skill support proxy
    public bool IsQSkillActive => leoPlayer != null ? leoPlayer.IsQSkillActive : false;
    public float QSkillTimeRemaining => leoPlayer != null ? leoPlayer.QSkillTimeRemaining : 0f;
    public bool TriggerQSkill()
    {
        if (leoPlayer != null) return leoPlayer.TriggerQSkill();
        return false;
    }
    public event System.Action OnQSkillCancelled
    {
        add    
        { 
            if (leoPlayer != null) leoPlayer.OnQSkillCancelled += value; 
            if (arthurPlayer != null) arthurPlayer.OnQSkillCancelled += value;
        }
        remove 
        { 
            if (leoPlayer != null) leoPlayer.OnQSkillCancelled -= value; 
            if (arthurPlayer != null) arthurPlayer.OnQSkillCancelled -= value;
        }
    }

    public void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        LockCursor(locked);
    }

    /// <summary>
    /// Trả về index vũ khí đang chọn: đọc từ HUD khi standalone, đọc từ NetworkVariable khi online.
    /// </summary>
    public virtual int GetActiveWeaponIndex()
    {
        if (leoPlayer != null) return leoPlayer.GetActiveWeaponIndex();
        if (arthurPlayer != null) return arthurPlayer.GetActiveWeaponIndex();
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null) return hud.currentSelectedWeapon;
            return 1;
        }
        return activeWeaponIndex.Value;
    }

    protected virtual void Awake()
    {
        leoPlayer = GetComponent<LeoPlayer>();
        arthurPlayer = GetComponent<ArthurPlayer>();
        if (leoPlayer != null)
        {
            inventorySlots = leoPlayer.inventorySlots;
        }
        else if (arthurPlayer != null)
        {
            inventorySlots = arthurPlayer.inventorySlots;
        }
        else if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }
    }

    private void Start()
    {
        if (leoPlayer != null || arthurPlayer != null)
        {
            isStandaloneMode = (leoPlayer != null) ? leoPlayer.isStandaloneMode : arthurPlayer.isStandaloneMode;
            // Giữ enabled = true để các hệ thống FindObjectsOfType<SimplePlayerTest>() vẫn tìm thấy Player.
            // Logic chính sẽ được chặn ở Update và LateUpdate bằng cách return sớm.
            return;
        }

        // Đảm bảo khởi tạo Animator cho cả các lớp kế thừa
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        // Khởi tạo các góc xoay camera từ offset mặc định
        float horizontalDistance = new Vector3(cameraOffset.x, 0f, cameraOffset.z).magnitude;
        currentYaw = Mathf.Atan2(cameraOffset.x, -cameraOffset.z) * Mathf.Rad2Deg;
        currentPitch = Mathf.Atan2(cameraOffset.y, horizontalDistance) * Mathf.Rad2Deg;
        targetYaw = currentYaw;
        targetPitch = currentPitch;
        cameraDistance = cameraOffset.magnitude;

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
    }

    /// <summary>
    /// Khởi tạo tất cả chức năng khi chơi đơn lẻ trong Editor mà không cần Host/Server.
    /// </summary>
    private void InitStandaloneMode()
    {
        Debug.Log("[SimplePlayerTest] Chạy ở chế độ STANDALONE (không có NetworkManager). " +
                  "Di chuyển và tấn công hoạt động cục bộ.");

        // Tìm camera
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        // Tải nhân vật đã lưu từ PlayerPrefs nếu có
        characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);
        playerName.Value = PlayerPrefs.GetString("AuthDisplayName", "Explorer");
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

        if (leoPlayer != null || arthurPlayer != null)
        {
            // LeoPlayer / ArthurPlayer handle their own network registration internally.
            return;
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

        if (IsOwner)
        {
            // Tải nhân vật đã lưu từ PlayerPrefs
            characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);

            // Đồng bộ tên người chơi qua mạng
            string myName = PlayerPrefs.GetString("AuthDisplayName", "Explorer");
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
        }
    }

    public override void OnNetworkDespawn()
    {
        if (PlayerHUDManager.ActivePlayers != null)
        {
            PlayerHUDManager.ActivePlayers.Remove(this);
        }

        if (leoPlayer != null || arthurPlayer != null)
        {
            // LeoPlayer / ArthurPlayer handle their own network despawn internally.
            return;
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
        get { 
            if (leoPlayer != null) return leoPlayer.Weapon1Durability;
            if (arthurPlayer != null) return arthurPlayer.Weapon1Durability;
            return isStandaloneMode ? localWeapon1Durability : weapon1Durability.Value;
        }
        set {
            if (leoPlayer != null)
            {
                leoPlayer.Weapon1Durability = value;
                return;
            }
            if (arthurPlayer != null)
            {
                arthurPlayer.Weapon1Durability = value;
                return;
            }
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
        get { 
            if (leoPlayer != null) return leoPlayer.Weapon2Durability;
            if (arthurPlayer != null) return arthurPlayer.Weapon2Durability;
            return isStandaloneMode ? localWeapon2Durability : weapon2Durability.Value;
        }
        set {
            if (leoPlayer != null)
            {
                leoPlayer.Weapon2Durability = value;
                return;
            }
            if (arthurPlayer != null)
            {
                arthurPlayer.Weapon2Durability = value;
                return;
            }
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
        if (leoPlayer != null)
        {
            leoPlayer.RepairWeaponFromHUD(weaponSlotIndex);
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.RepairWeaponFromHUD(weaponSlotIndex);
            return;
        }

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
        if (leoPlayer != null) return leoPlayer.TryAddItem(itemName);
        if (arthurPlayer != null) return arthurPlayer.TryAddItem(itemName);
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
        if (leoPlayer != null)
        {
            leoPlayer.AddExperience(amount);
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.AddExperience(amount);
            return;
        }
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
        if (leoPlayer != null)
        {
            leoPlayer.StandaloneUpgradeStat(statType);
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.StandaloneUpgradeStat(statType);
            return;
        }
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
        if (leoPlayer != null)
        {
            leoPlayer.UpgradeStatFromHUD(statType);
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.UpgradeStatFromHUD(statType);
            return;
        }
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

    protected virtual void Update()
    {
        if (leoPlayer != null || arthurPlayer != null) return;

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

        // Giảm thời gian cooldown nhào lộn
        if (rollCooldownTimer > 0)
        {
            rollCooldownTimer -= Time.deltaTime;
        }

        if (CurrentHealth <= 0)
        {
            if (anim != null) anim.applyRootMotion = false;
            PlayAnimation("Death", 0.15f);
            return;
        }

        // Standalone: xử lý hoàn toàn cục bộ
        if (isStandaloneMode)
        {
            HandleStandaloneUpdate();
            return;
        }

        // Netcode: chỉ chủ sở hữu mới điều khiển
        if (!IsOwner) return;
        HandleOwnerUpdate();
    }

    protected virtual void HandleStandaloneUpdate()
    {
        // Khóa di chuyển, tấn công, nhào lộn khi đang nói chuyện với Rakan hoặc Silas
        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);

        if (isDialogueOpen)
        {
            if (!IsPlayingActionAnimation())
            {
                PlayAnimation("Idle", 0.1f);
            }
            return; // Khóa hoàn toàn di chuyển, né tránh, tấn công
        }

        // Xử lý di chuyển khi đang nhào lộn
        if (isRollingStandalone)
        {
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0)
            {
                isRollingStandalone = false;
                if (anim != null) anim.applyRootMotion = false;
                var bridge = GetRootMotionBridge();
                if (bridge != null) bridge.ApplyFinalOffset();
            }
            return; // Khóa các đầu vào di chuyển khác khi đang nhào lộn
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

        // Tạo bản sao di chuyển vật lý để có thể khóa di chuyển mà không làm mất hướng né đòn (roll direction)
        Vector3 movementTranslation = move;
        if (isRootedAttack && IsPlayingActionAnimation())
        {
            movementTranslation = Vector3.zero;
        }

        transform.Translate(movementTranslation * currentSpeed * Time.deltaTime, Space.World);
        if (movementTranslation != Vector3.zero)
        {
            transform.forward = movementTranslation;
            if (!IsPlayingActionAnimation())
            {
                string moveAnim = isRunning ? "run" : "Walk";
                PlayAnimation(moveAnim, 0.1f);
            }
        }
        else
        {
            if (!IsPlayingActionAnimation())
            {
                PlayAnimation("Idle", 0.1f);
            }
        }

        // Tấn công đơn lẻ
        if (Input.GetMouseButtonDown(0))
        {
            if (!IsUIBlockingInput())
            {
                PerformComboAttack(false);
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

    protected virtual void HandleOwnerUpdate()
    {
        // Khóa di chuyển, tấn công, nhào lộn khi đang nói chuyện với Rakan hoặc Silas
        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);

        if (isDialogueOpen)
        {
            if (!IsPlayingActionAnimation())
            {
                PlayAnimation("Idle", 0.1f);
            }
            return; // Khóa hoàn toàn di chuyển, né tránh, tấn công
        }

        // Xử lý di chuyển khi đang nhào lộn
        if (rollTimer > 0)
        {
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0)
            {
                if (anim != null) anim.applyRootMotion = false;
                var bridge = GetRootMotionBridge();
                if (bridge != null) bridge.ApplyFinalOffset();
                StopRollServerRpc();
            }
            return; // Khóa các đầu vào di chuyển khác khi đang nhào lộn
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

        // Tạo bản sao di chuyển vật lý để có thể khóa di chuyển mà không làm mất hướng né đòn (roll direction)
        Vector3 movementTranslation = move;
        if (isRootedAttack && IsPlayingActionAnimation())
        {
            movementTranslation = Vector3.zero;
        }

        transform.Translate(movementTranslation * currentSpeed * Time.deltaTime, Space.World);
        if (movementTranslation != Vector3.zero)
        {
            transform.forward = movementTranslation;
            if (!IsPlayingActionAnimation())
            {
                string moveAnim = isRunning ? "run" : "Walk";
                PlayAnimation(moveAnim, 0.1f);
            }
        }
        else
        {
            if (!IsPlayingActionAnimation())
            {
                PlayAnimation("Idle", 0.1f);
            }
        }

        // Tấn công qua RPC (chỉ khi đã spawn trên mạng)
        if (Input.GetMouseButtonDown(0))
        {
            if (IsSpawned && !IsUIBlockingInput())
            {
                PerformComboAttack(true);
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

    protected virtual void StartRollStandalone(Vector3 moveInput)
    {
        isRollingStandalone = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;
        
        ClearAttackLayer(); // Trả Layer 1 về Empty để lộn vòng cả thân người
        
        // Hướng nhào lộn: nếu có di chuyển thì lăn theo hướng WASD, ngược lại lăn theo hướng đang nhìn
        if (moveInput != Vector3.zero)
        {
            rollDirection = moveInput.normalized;
            transform.forward = rollDirection;
        }
        else
        {
            rollDirection = transform.forward;
        }

        if (anim != null) anim.applyRootMotion = true;
        PlayAnimation("LonVong", 0.05f);
    }

    protected virtual void StartRollOwner(Vector3 moveInput)
    {
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;
        
        ClearAttackLayer(); // Trả Layer 1 về Empty để lộn vòng cả thân người
        
        // Hướng nhào lộn: nếu có di chuyển thì lăn theo hướng WASD, ngược lại lăn theo hướng đang nhìn
        if (moveInput != Vector3.zero)
        {
            rollDirection = moveInput.normalized;
            transform.forward = rollDirection;
        }
        else
        {
            rollDirection = transform.forward;
        }

        if (anim != null) anim.applyRootMotion = true;
        PlayAnimation("LonVong", 0.05f, false); // Đặt false để client local thực sự gọi PlayAnimationLocal!
        StartRollServerRpc(rollDirection);
    }

    void LateUpdate()
    {
        if (leoPlayer != null || arthurPlayer != null) return;

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
                targetPitch += mouseY * cameraSensitivity;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
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

            // Gắn cứng camera theo vị trí của nhân vật (Loại bỏ Lerp vị trí để giải quyết triệt để lỗi delay, zoom co giãn, và lệch nhân vật ra rìa)
            Vector3 targetPosition = (transform.position + Vector3.up * cameraPivotHeight) + rotatedOffset;
            targetCamera.transform.position = targetPosition;

            if (cameraLookAtPlayer)
            {
                // Khóa camera luôn nhìn thẳng vào nhân vật (không dùng Slerp rotation) để nhân vật luôn nằm chính giữa màn hình
                targetCamera.transform.rotation = Quaternion.LookRotation(
                    (transform.position + Vector3.up * cameraPivotHeight) - targetCamera.transform.position
                );
            }
        }
    }

    protected virtual float GetAttackDuration(int weaponIndex, int step)
    {
        if (weaponIndex == 1)
        {
            return step == 1 ? punch1Duration : punch2Duration;
        }
        else if (weaponIndex == 2)
        {
            if (step == 1) return slash1Duration;
            if (step == 2) return slash2Duration;
            return slash3Duration;
        }
        return 0.5f;
    }

    protected virtual void PerformComboAttack(bool networkMode)
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

        // Giới hạn bước combo dựa trên vũ khí
        if (weapon == 1)
        {
            if (nextStep > 2) nextStep = 1;
        }
        else if (weapon == 2)
        {
            if (nextStep > 3) nextStep = 1;
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
            isRootedAttack = !isMovingInput; // Đứng yên đánh thì khóa chân (rooted), di chuyển đánh thì không khóa chân
        }

        comboStep = nextStep;
        lastAttackTime = currentTime;

        string animToPlay = "";
        if (weapon == 1) // Unarmed / Fist combo (2 steps)
        {
            animToPlay = comboStep == 1 ? "Punch1" : "Punch2";
        }
        else if (weapon == 2) // Sword combo (3 steps)
        {
            if (comboStep == 1) animToPlay = "Slash1";
            else if (comboStep == 2) animToPlay = "Slash2";
            else if (comboStep == 3) animToPlay = "Slash3";
        }

        if (!string.IsNullOrEmpty(animToPlay))
        {
            PlayAnimation(animToPlay, 0.05f, false);
        }

        if (networkMode)
        {
            AttackServerRpc();
        }
        else
        {
            Vector3 rayStart = transform.position + Vector3.up * 0.5f;
            Debug.DrawRay(rayStart, transform.forward * attackRange, Color.red, 0.5f);

            if (!Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange)) return;

            TryDamageEnemy(hit.collider);
        }
    }

    protected void TryDamageEnemy(Collider col)
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
    protected void AttackServerRpc()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Debug.DrawRay(rayStart, transform.forward * attackRange, Color.red, 0.5f);

        if (!Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange)) return;

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
        if (leoPlayer != null)
        {
            leoPlayer.TakeDamage(damage);
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.TakeDamage(damage);
            return;
        }
        // Né chiêu (miễn nhiễm sát thương khi đang lộn vòng)
        if (isStandaloneMode)
        {
            if (isRollingStandalone)
            {
                Debug.Log($"[Standalone] {gameObject.name} đang né chiêu (lộn vòng), miễn nhiễm sát thương!");
                return;
            }
        }
        else
        {
            if (isRollingNet.Value)
            {
                Debug.Log($"[Netcode] {gameObject.name} đang né chiêu (lộn vòng), miễn nhiễm sát thương!");
                return;
            }
        }

        if (isStandaloneMode)
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            UpdateHealthHUD(localHealth);
            Debug.Log($"[Standalone] {gameObject.name} nhận {damage} sát thương. Máu còn: {localHealth}");
            if (localHealth <= 0)
            {
                Debug.LogWarning($"[Standalone] {gameObject.name} đã chết!");
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
        Debug.Log($"[Server] {gameObject.name} nhận {damage} sát thương. Máu còn lại: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
        {
            Debug.LogWarning($"[Server] {gameObject.name} đã chết!");
            PlayAnimation("Death", 0.15f);
        }
        else
        {
            string hitAnim = Random.value < 0.5f ? "GetHit" : "GeiHit2";
            PlayAnimation(hitAnim, 0.05f);
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
        if (leoPlayer != null)
        {
            leoPlayer.ApplyKnockback(force);
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.ApplyKnockback(force);
            return;
        }
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
        if (leoPlayer != null)
        {
            leoPlayer.UpdateStateFromHUD(weaponIndex, weapon2Locked, skillsUnlocked);
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.UpdateStateFromHUD(weaponIndex, weapon2Locked, skillsUnlocked);
            return;
        }
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

    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerNameServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        playerName.Value = name;
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

    public async void SavePlayerStateToDatabase()
    {
        if (leoPlayer != null)
        {
            leoPlayer.SavePlayerStateToDatabase();
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.SavePlayerStateToDatabase();
            return;
        }
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


    protected float lastActionTriggerTime = 0f;
    protected string lastTriggeredAnimName = "";

    protected virtual bool IsActionAnimationName(string name)
    {
        return name == "LonVong" || 
               name == "GetHit" || 
               name == "GeiHit2" || 
               name == "Idle_Pick" || 
               name == "Death" ||
               name == "Punch1" ||
               name == "Punch2" ||
               name == "Dam1" ||
               name == "Dam2" ||
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3" ||
               name == "Chem1" ||
               name == "Chem2" ||
               name == "Chem3" ||
               (!string.IsNullOrEmpty(drawWeaponTrigger) && name == drawWeaponTrigger) ||
               (!string.IsNullOrEmpty(sheathWeaponTrigger) && name == sheathWeaponTrigger);
    }

    public void PlayWeaponSwitchAnimation(int oldWeapon, int newWeapon)
    {
        if (leoPlayer != null)
        {
            leoPlayer.PlayWeaponSwitchAnimation(oldWeapon, newWeapon);
            return;
        }
        if (arthurPlayer != null)
        {
            arthurPlayer.PlayWeaponSwitchAnimation(oldWeapon, newWeapon);
            return;
        }
        if (oldWeapon == newWeapon) return;

        if (newWeapon == 2)
        {
            if (!string.IsNullOrEmpty(drawWeaponTrigger))
            {
                PlayAnimationLocal(drawWeaponTrigger, 0.1f);
            }
        }
        else if (newWeapon == 1)
        {
            if (!string.IsNullOrEmpty(sheathWeaponTrigger))
            {
                PlayAnimationLocal(sheathWeaponTrigger, 0.1f);
            }
        }
    }

    protected virtual bool IsAttackAnimationName(string name)
    {
        return name == "Punch1" || 
               name == "Punch2" || 
               name == "Dam1" || 
               name == "Dam2" || 
               name == "Slash1" || 
               name == "Slash2" || 
               name == "Slash3" ||
               name == "Chem1" ||
               name == "Chem2" ||
               name == "Chem3";
    }

    protected virtual bool IsPlayingAttackState(out AnimatorStateInfo activeState, out int layer)
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
        }

        // Check Layer 0 (Base Layer)
        AnimatorStateInfo baseLayerStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (IsAttackState(baseLayerStateInfo))
        {
            activeState = baseLayerStateInfo;
            layer = 0;
            return true;
        }

        return false;
    }

    protected virtual bool IsAttackState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("Punch1") || 
               stateInfo.IsName("Punch2") || 
               stateInfo.IsName("Dam1") || 
               stateInfo.IsName("Dam2") || 
               stateInfo.IsName("Slash1") || 
               stateInfo.IsName("Slash2") || 
               stateInfo.IsName("Slash3") ||
               stateInfo.IsName("Slash_1") || 
               stateInfo.IsName("Slash_2") || 
               stateInfo.IsName("Slash_3") ||
               stateInfo.IsName("Chem1") ||
               stateInfo.IsName("Chem2") ||
               stateInfo.IsName("Chem3") ||
               stateInfo.IsName("Chem_1") ||
               stateInfo.IsName("Chem_2") ||
               stateInfo.IsName("Chem_3");
    }

    protected virtual bool IsFullBodyActionAnimation(string name)
    {
        return name == "LonVong" || 
               name == "GetHit" || 
               name == "GeiHit2" || 
               name == "Idle_Pick" || 
               name == "Death";
    }

    protected virtual bool IsPlayingActionAnimation()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

        // Nếu vừa kích hoạt trigger hành động full-body trong vòng 0.15 giây, coi như đang chạy action animation
        if (IsFullBodyActionAnimation(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.15f) return true;
        
        // Nếu vừa kích hoạt tấn công đứng yên (rooted) trong vòng 0.15 giây, coi nó như hành động full-body
        if (isRootedAttack && IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.15f) return true;

        // Khi đang nhào lộn (rolling), coi như đang chạy action animation
        if (isStandaloneMode ? isRollingStandalone : rollTimer > 0) return true;

        // 1. Nếu là đòn đánh đứng yên (rooted), khóa di chuyển hoàn toàn cho tới khi animator thoát khỏi trạng thái đấm/chém
        if (isRootedAttack && IsPlayingAttackState(out _, out _))
        {
            return true;
        }

        // 2. Kiểm tra các hành động toàn thân khác (như lộn vòng, trúng đòn, chết, nhặt đồ) trên Layer 0
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool isFullBodyAction = stateInfo.IsName("LonVong") || 
                               stateInfo.IsName("GetHit") || 
                               stateInfo.IsName("GeiHit2") || 
                               stateInfo.IsName("Idle_Pick") || 
                               stateInfo.IsName("Death");

        return isFullBodyAction && stateInfo.normalizedTime < 0.95f;
    }

    public void PlayAnimation(string animName, float fadeTime = 0.1f, bool alreadyPlayedLocally = false)
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
            PlayAnimationLocal(animName, fadeTime);
        }

        if (!isStandaloneMode)
        {
            if (IsServer)
            {
                PlayAnimationClientRpc(animName, fadeTime, alreadyPlayedLocally || IsOwner);
            }
            else if (IsOwner)
            {
                PlayAnimationServerRpc(animName, fadeTime);
            }
        }
    }

    protected virtual void PlayAnimationLocal(string animName, float fadeTime)
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

        Debug.Log($"[Animator Debug] {gameObject.name} kích hoạt Trigger hoạt ảnh: '{animName}'");

        // Reset các trigger di chuyển cơ bản để tránh kẹt
        anim.ResetTrigger("Idle");
        anim.ResetTrigger("Walk");
        anim.ResetTrigger("run");
        anim.ResetTrigger("Death");
        anim.ResetTrigger("GetHit");
        anim.ResetTrigger("GeiHit2");
        anim.ResetTrigger("LonVong");
        anim.ResetTrigger("Idle_Pick");

        // Chỉ dọn dẹp (reset) các trigger combo tấn công khi chuẩn bị kích hoạt một hành động mới
        // (để tránh việc nhân vật di chuyển làm reset mất trigger đòn đấm trên layer Upper Body)
        if (IsActionAnimationName(animName))
        {
            anim.ResetTrigger("Punch1");
            anim.ResetTrigger("Punch2");
            anim.ResetTrigger("Slash1");
            anim.ResetTrigger("Slash2");
            anim.ResetTrigger("Slash3");
            if (!string.IsNullOrEmpty(drawWeaponTrigger)) anim.ResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) anim.ResetTrigger(sheathWeaponTrigger);
        }

        // Kích hoạt Trigger để chạy dây nối trong Animator
        anim.SetTrigger(animName);

        // Chỉ cập nhật currentAnimState cho các hoạt ảnh di chuyển hoặc hoạt ảnh hành động toàn thân (không phải đòn đánh di chuyển)
        bool isMovingAttack = IsAttackAnimationName(animName) && !isRootedAttack;
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
    private void PlayAnimationServerRpc(string animName, float fadeTime)
    {
        PlayAnimationClientRpc(animName, fadeTime, true);
    }

    [ClientRpc]
    private void PlayAnimationClientRpc(string animName, float fadeTime, bool alreadyPlayedLocally)
    {
        if (alreadyPlayedLocally && IsOwner) return; // Chủ sở hữu đã tự chạy hoạt ảnh local rồi
        PlayAnimationLocal(animName, fadeTime);
    }

    [ServerRpc]
    private void StartRollServerRpc(Vector3 direction)
    {
        isRollingNet.Value = true;
        // Server phát RPC hoạt ảnh LonVong cho tất cả client khác (owner đã tự chạy rồi)
        PlayAnimationClientRpc("LonVong", 0.05f, true);
    }

    [ServerRpc]
    protected void StopRollServerRpc()
    {
        isRollingNet.Value = false;
    }

    protected virtual void ClearAttackLayer()
    {
        comboStep = 0;
        isRootedAttack = false;
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            // Reset Layer 1 (AttackLayer) về trạng thái Empty/New State mặc định
            anim.Play("New State", 1, 0f);
            anim.Play("Empty", 1, 0f);
        }
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

    public override void OnDestroy()
    {
        if (PlayerHUDManager.ActivePlayers != null)
        {
            PlayerHUDManager.ActivePlayers.Remove(this);
        }
        base.OnDestroy();
    }
}
