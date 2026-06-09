using Unity.Netcode;
using UnityEngine;

public class ArthurPlayer : NetworkBehaviour, IPlayerHUDTarget
{
    [Header("Input Keys Configuration")]
    public KeyCode rollKey = KeyCode.LeftControl;

    [Header("Blend Tree vs Trigger Mode")]
    [Tooltip("If true, movement is animated smoothly using float parameters (InputX, InputZ, Speed, IsArmed) and Blend Trees. If false, it triggers separate animation states directly.")]
    public bool useBlendTree = true;

    [Header("Animator Parameter Names")]
    public string inputXParam = "InputX";
    public string inputZParam = "InputZ";
    public string speedParam = "Speed";
    public string isArmedParam = "IsArmed";

    [Header("Smooth Movement Settings")]
    public float inputFilterSpeed = 8f;
    public float rotationSmoothSpeedArmed = 10f;
    public float rotationSmoothSpeedUnarmed = 12f;
    [Tooltip("If true, the character rotates to face the camera direction when Armed, enabling backpedaling and strafing.")]
    public bool rotateToCameraWhenArmed = true;
    [Tooltip("If true, the character rotates to face the camera direction when Unarmed, enabling backpedaling and strafing without weapons.")]
    public bool rotateToCameraWhenUnarmed = true;

    [Header("Player Settings & Stats")]
    public float moveSpeed = 4f;
    public float runSpeedMultiplier = 2.0f;
    public float damageAmount = 15f;
    public float attackRange = 3f;

    [Header("Combo Attack Settings")]
    public float comboWindow = 1.0f;
    public float comboTransitionThreshold = 0.5f;
    public float punch1Duration = 1.033f;
    public float punch2Duration = 1.033f;
    public float punch3Duration = 2.167f;
    public float slash1Duration = 0.6f;
    public float slash2Duration = 0.6f;
    public float slash3Duration = 0.7f;
    protected int comboStep = 0;
    protected float lastAttackTime = 0f;
    protected bool isRootedAttack = false;

    [Header("Combo Chain Buffer Settings")]
    [Range(0f, 1f)]
    public float comboChainWindowPct = 0.9f;
    protected bool pendingAttackRequest = false;
    protected bool isExecutingAttack = false;
    protected float attackAnimStartTime = 0f;
    protected float currentAttackAnimDuration = 0f;
    protected Coroutine comboChainCoroutine;
    protected float earliestValidEventTime = 0f;

    [Header("Weapon Switch Animations")]
    public string drawWeaponTrigger = "DrawWeapon";
    public string sheathWeaponTrigger = "SheathWeapon";
    public string drawLeftTrigger = "DrawLeft";
    public string drawRightTrigger = "DrawRight";
    public string sheatheLeftTrigger = "SheatheLeft";
    public string sheatheRightTrigger = "SheatheRight";
    [HideInInspector]
    public bool isSwitchingWeapon = false;

    [Header("Weapon Visual References")]
    [Tooltip("Vũ khí/Khiên trên tay trái")]
    public GameObject leftHandWeapon;
    [Tooltip("Vũ khí/Kiếm trên tay phải")]
    public GameObject rightHandWeapon;
    [Tooltip("Vũ khí/Khiên giắt sau lưng/vai trái")]
    public GameObject leftShoulderWeapon;
    [Tooltip("Vũ khí/Kiếm giắt sau lưng/vai phải")]
    public GameObject rightShoulderWeapon;

    [Header("Player Health Settings")]
    public float maxHealth = 150f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        150f,
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
    protected int fallbackWeaponIndex = 1;

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
    public int characterClassIndex = 3;

    [Header("Player Name Sync")]
    public NetworkVariable<Unity.Collections.FixedString64Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Arthur", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // IPlayerHUDTarget Stats Implementation
    public string DisplayName => string.IsNullOrEmpty(playerName.Value.ToString()) ? "Arthur" : playerName.Value.ToString();
    public int PlayerLevel => isStandaloneMode ? localLevel : playerLevel.Value;
    public float PlayerExp => isStandaloneMode ? localExp : playerExp.Value;
    public float MaxExp => 100f + (isStandaloneMode ? localLevel : playerLevel.Value) * 50f;

    [Header("Knockback Settings")]
    protected Vector3 knockbackVelocity;

    [Header("Camera Follow Settings")]
    public bool enableCameraFollow = true;
    public Vector3 cameraOffset = new Vector3(0f, 10f, -6f);
    public float cameraSmoothSpeed = 5f;
    public bool cameraLookAtPlayer = true;
    public float cameraPivotHeight = 1.5f;
    protected Camera targetCamera;

    [Header("Camera Rotation Settings")]
    public float cameraSensitivity = 3f;
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

    [Header("Hitbox References")]
    [Tooltip("Left hand hitbox collider.")]
    public Collider leftHitbox;
    [Tooltip("Right hand hitbox collider.")]
    public Collider rightHitbox;
    [Tooltip("Left weapon/sword hitbox collider.")]
    public Collider leftWeaponHitbox;
    [Tooltip("Right weapon/sword hitbox collider.")]
    public Collider rightWeaponHitbox;

    private System.Collections.Generic.List<Transform> alreadyHitEnemies = new System.Collections.Generic.List<Transform>();

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

    protected float localHealth;
    public bool isStandaloneMode = false;
    protected bool isSyncingFromDb = false;

    // Smooth inputs for 2D Blend Tree
    private float smoothedInputX = 0f;
    private float smoothedInputZ = 0f;
    private Rigidbody rb;

    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public float CurrentHealth => isStandaloneMode ? localHealth : currentHealth.Value;

    // IPlayerHUDTarget Implementation
    bool IPlayerHUDTarget.isStandaloneMode => isStandaloneMode;
    public bool IsStandaloneMode => isStandaloneMode;
    public int CharacterClassIndex => characterClassIndex;
    public bool IsSwitchingWeapon => false;
    public float Weapon1MaxDurability => weapon1MaxDurability;
    public float Weapon2MaxDurability => weapon2MaxDurability;
    public string[] InventorySlots => inventorySlots;
    public float MaxHealth => maxHealth;

    // Invisibility Skill R stub implementation (Leo-only skill)
    public bool IsInvisible => false;
    public float InvisibilityTimeRemaining => 0f;
    public void TriggerInvisibilitySkill() { }

    // Attack Speed Boost Skill E stub implementation (Leo-only skill)
    public bool IsAttackSpeedBoosted => false;
    public float AttackSpeedBoostTimeRemaining => 0f;
    public void TriggerAttackSpeedBoostSkill() { }

    // Q Skill support stub implementation (Leo-only skill)
    public bool IsQSkillActive => false;
    public float QSkillTimeRemaining => 0f;
    public bool TriggerQSkill() => false;
    public event System.Action OnQSkillCancelled;

    [HideInInspector]
    public GameObject pendingPickItem;

    public void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        LockCursor(locked);
    }

    public virtual int GetActiveWeaponIndex()
    {
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null) return hud.currentSelectedWeapon;
            return fallbackWeaponIndex;
        }
        return activeWeaponIndex.Value;
    }

    private void Awake()
    {
        characterClassIndex = 3; // Arthur Tanker
        maxHealth = 150f;
        moveSpeed = 4f;
        runSpeedMultiplier = 2.0f;
        damageAmount = 15f;
        cameraOffset = new Vector3(0f, 10f, -6f);
        cameraSensitivity = 3f;
        cameraPivotHeight = 1.5f;

        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void Start()
    {
        // Enforce a high percentage for combo transition so animations play almost fully before transitioning
        if (comboChainWindowPct < 0.9f)
        {
            comboChainWindowPct = 0.9f;
        }

        CreateSwordHitboxes();
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        float horizontalDistance = new Vector3(cameraOffset.x, 0f, cameraOffset.z).magnitude;
        currentYaw = Mathf.Atan2(cameraOffset.x, -cameraOffset.z) * Mathf.Rad2Deg;
        currentPitch = Mathf.Atan2(cameraOffset.y, horizontalDistance) * Mathf.Rad2Deg;
        targetYaw = currentYaw;
        targetPitch = currentPitch;
        cameraDistance = cameraOffset.magnitude;

        LockCursor(isCursorLocked);

        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            localHealth = maxHealth;
            InitStandaloneMode();
        }

        SyncWeaponVisuals(GetActiveWeaponIndex());
    }

    private void InitStandaloneMode()
    {
        Debug.Log("[ArthurPlayer] Chạy ở chế độ STANDALONE. Di chuyển và tấn công hoạt động cục bộ.");

        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);
        playerName.Value = PlayerPrefs.GetString("AuthDisplayName", "Arthur");
        if (PlayerHUDManager.ActivePlayers != null && !PlayerHUDManager.ActivePlayers.Contains(this))
        {
            PlayerHUDManager.ActivePlayers.Add(this);
        }

        localLevel = PlayerPrefs.GetInt("SelectedPlayerLevel_" + characterClassIndex, 0);
        localExp = PlayerPrefs.GetFloat("SelectedPlayerExp_" + characterClassIndex, 0f);

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

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        var rbComp = GetComponent<Rigidbody>();
        if (rbComp != null)
        {
            rbComp.isKinematic = !IsOwner;
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

        activeWeaponIndex.OnValueChanged += OnWeaponIndexChanged;
        isWeapon2Locked.OnValueChanged += OnWeapon2LockedChanged;
        isSkillsUnlocked.OnValueChanged += OnSkillsUnlockedChanged;

        upgradePoints.OnValueChanged += OnUpgradePointsChanged;
        hpLevel.OnValueChanged += OnHpLevelChanged;
        mpLevel.OnValueChanged += OnMpLevelChanged;
        cooldownLevel.OnValueChanged += OnCooldownLevelChanged;
        damageLevel.OnValueChanged += OnDamageLevelChanged;

        playerLevel.OnValueChanged += OnLevelOrExpChanged;
        playerExp.OnValueChanged += OnLevelOrExpChanged;

        weapon1Durability.OnValueChanged += OnDurabilityChanged;
        weapon2Durability.OnValueChanged += OnDurabilityChanged;

        if (IsOwner)
        {
            characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);

            string myName = PlayerPrefs.GetString("AuthDisplayName", "Arthur");
            SetPlayerNameServerRpc(myName);

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

            LoadPlayerStateFromDatabase();

            ApplyUpgradedStats();
            UpdateUpgradeHUD();
            UpdateDurabilityHUD();
        }

        SyncWeaponVisuals(activeWeaponIndex.Value);
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

        upgradePoints.OnValueChanged -= OnUpgradePointsChanged;
        hpLevel.OnValueChanged -= OnHpLevelChanged;
        mpLevel.OnValueChanged -= OnMpLevelChanged;
        cooldownLevel.OnValueChanged -= OnCooldownLevelChanged;
        damageLevel.OnValueChanged -= OnDamageLevelChanged;

        playerLevel.OnValueChanged -= OnLevelOrExpChanged;
        playerExp.OnValueChanged -= OnLevelOrExpChanged;

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

    private void ApplyUpgradedStats()
    {
        if (isSyncingFromDb) return;

        int hpLv = isStandaloneMode ? localHpLevel : hpLevel.Value;
        int dmgLv = isStandaloneMode ? localDamageLevel : damageLevel.Value;

        float oldMaxHealth = maxHealth;
        maxHealth = 150f + hpLv * 20f;
        damageAmount = 15f + dmgLv * 5f;

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
        set
        {
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
        set
        {
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

    public bool TryAddItem(string itemName, bool playPickupAnimation = true)
    {
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

                    PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                    if (hud != null)
                    {
                        hud.SetInventorySlots(inventorySlots);
                    }

                    if (!isStandaloneMode)
                    {
                        SavePlayerStateToDatabase();
                    }

                    if (playPickupAnimation)
                    {
                        PlayAnimation("Idle_Pick", 0.1f);
                    }
                    return true;
                }
            }
        }

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (string.IsNullOrEmpty(inventorySlots[i]))
            {
                inventorySlots[i] = itemName + ":1";

                PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                if (hud != null)
                {
                    hud.SetInventorySlots(inventorySlots);
                }

                if (!isStandaloneMode)
                {
                    SavePlayerStateToDatabase();
                }

                if (playPickupAnimation)
                {
                    PlayAnimation("Idle_Pick", 0.1f);
                }
                return true;
            }
        }
        return false;
    }

    public bool TryAddItem(string itemName)
    {
        return TryAddItem(itemName, true);
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
                localUpgradePoints++;
                Debug.Log($"[Standalone] THĂNG CẤP! Cấp độ mới: {localLevel}. Điểm nâng cấp còn: {localUpgradePoints}");
            }
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
                upgradePoints.Value++;
                Debug.Log($"[Server] CLIENT {OwnerClientId} THĂNG CẤP! Cấp độ mới: {playerLevel.Value}. Điểm nâng cấp còn: {upgradePoints.Value}");
            }
            SavePlayerStateClientRpc();
        }
    }

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
    }

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
    }

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
        UpdateAttackLayerWeight();
        UpdateComboChain();

        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (hasControl)
        {
            if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
            {
                isCursorLocked = !isCursorLocked;
                LockCursor(isCursorLocked);
            }
        }

        if (isRollingStandalone || (IsSpawned && isRollingNet.Value))
        {
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0)
            {
                if (isStandaloneMode || IsOwner)
                {
                    OnRollEnd();
                }
            }
        }

        if (rollCooldownTimer > 0)
        {
            rollCooldownTimer -= Time.deltaTime;
        }

        if (CurrentHealth <= 0)
        {
            if (anim != null) anim.applyRootMotion = false;
            if (rb != null) rb.linearVelocity = Vector3.zero;
            PlayAnimation("Death", 0.15f);
            return;
        }

        if (isStandaloneMode)
        {
            HandleStandaloneUpdate();
            return;
        }

        if (!IsOwner) return;
        HandleOwnerUpdate();
    }

    private void UpdateAnimatorParams(float targetSpeed)
    {
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            anim.SetFloat(inputXParam, smoothedInputX);
            anim.SetFloat(inputZParam, smoothedInputZ);

            float currentSpeedVal = anim.GetFloat(speedParam);
            float smoothedSpeed = Mathf.MoveTowards(currentSpeedVal, targetSpeed, Time.deltaTime * inputFilterSpeed);
            anim.SetFloat(speedParam, smoothedSpeed);

            bool isArmed = (GetActiveWeaponIndex() == 2);
            anim.SetBool(isArmedParam, isArmed);

            if (Mathf.Abs(smoothedInputX) > 0.01f || Mathf.Abs(smoothedInputZ) > 0.01f)
            {
                Debug.Log($"[ArthurAnimator] InputX: {smoothedInputX:F2}, InputZ: {smoothedInputZ:F2}, Speed: {smoothedSpeed:F2}, IsArmed: {isArmed}");
            }
        }
    }

    protected virtual void HandleStandaloneUpdate()
    {
        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                               (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);

        if (isDialogueOpen)
        {
            smoothedInputX = Mathf.MoveTowards(smoothedInputX, 0f, Time.deltaTime * inputFilterSpeed);
            smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, 0f, Time.deltaTime * inputFilterSpeed);
            UpdateAnimatorParams(0f);
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            if (!IsPlayingActionAnimation())
            {
                if (!useBlendTree) PlayAnimation("Idle", 0.1f);
            }
            return;
        }

        if (isRollingStandalone)
        {
            if (rb != null)
            {
                float currentYVelocity = rb.linearVelocity.y;
                rb.linearVelocity = new Vector3(rollDirection.x * rollSpeed, currentYVelocity, rollDirection.z * rollSpeed);
            }
            return;
        }

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;

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

        Vector3 movementTranslation = move;
        if (isRootedAttack && IsPlayingActionAnimation())
        {
            movementTranslation = Vector3.zero;
        }

        if (rb != null)
        {
            Vector3 targetVelocity = movementTranslation * currentSpeed;
            float currentYVelocity = rb.linearVelocity.y;

            if (knockbackVelocity.magnitude > 0.01f)
            {
                targetVelocity += knockbackVelocity;
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
            }

            rb.linearVelocity = new Vector3(targetVelocity.x, currentYVelocity, targetVelocity.z);
        }

        bool isArmed = (GetActiveWeaponIndex() == 2);
        bool isMoving = (movementTranslation != Vector3.zero);

        float targetInputX = 0f;
        float targetInputZ = 0f;
        float targetSpeed = 0f;

        bool isAttacking = IsPlayingAttackState(out _, out _);

        bool shouldAlignToCamera = (isArmed && rotateToCameraWhenArmed) ||
                                   (!isArmed && rotateToCameraWhenUnarmed) ||
                                   isAttacking;

        if (shouldAlignToCamera)
        {
            if (targetCamera != null)
            {
                Vector3 camForward = targetCamera.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(camForward.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeedArmed);
                }
            }

            if (isMoving)
            {
                targetInputX = moveX * (isRunning ? 1.0f : 0.5f);
                targetInputZ = moveZ * (isRunning ? 1.0f : 0.5f);
                targetSpeed = new Vector2(targetInputX, targetInputZ).magnitude;
            }
        }
        else
        {
            if (isMoving)
            {
                Quaternion targetRot = Quaternion.LookRotation(movementTranslation.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeedUnarmed);

                targetInputX = 0f;
                targetInputZ = isRunning ? 1.0f : 0.5f;
                targetSpeed = targetInputZ;
            }
        }

        smoothedInputX = Mathf.MoveTowards(smoothedInputX, targetInputX, Time.deltaTime * inputFilterSpeed);
        smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, targetInputZ, Time.deltaTime * inputFilterSpeed);
        UpdateAnimatorParams(targetSpeed);

        if (!useBlendTree && !IsPlayingActionAnimation())
        {
            if (isMoving)
            {
                string moveAnim = isRunning ? "run" : "Walk";
                PlayAnimation(moveAnim, 0.1f);
            }
            else
            {
                PlayAnimation("Idle", 0.1f);
            }
        }

        // Standalone weapon switching when no HUD
        if (isStandaloneMode && FindObjectOfType<PlayerHUDController>() == null)
        {
            if (!isSwitchingWeapon)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    int oldW = fallbackWeaponIndex;
                    fallbackWeaponIndex = 1;
                    PlayWeaponSwitchAnimation(oldW, 1);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    int oldW = fallbackWeaponIndex;
                    fallbackWeaponIndex = 2;
                    PlayWeaponSwitchAnimation(oldW, 2);
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!IsUIBlockingInput() && CanAttack())
            {
                RequestComboAttack(false);
            }
        }

        if (Input.GetKeyDown(rollKey))
        {
            if (!IsUIBlockingInput() && rollCooldownTimer <= 0 && !IsPlayingAttackState(out _, out _))
            {
                StartRollStandalone(move);
            }
        }
    }

    protected virtual void HandleOwnerUpdate()
    {
        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                               (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);

        if (isDialogueOpen)
        {
            smoothedInputX = Mathf.MoveTowards(smoothedInputX, 0f, Time.deltaTime * inputFilterSpeed);
            smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, 0f, Time.deltaTime * inputFilterSpeed);
            UpdateAnimatorParams(0f);
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            if (!IsPlayingActionAnimation())
            {
                if (!useBlendTree) PlayAnimation("Idle", 0.1f);
            }
            return;
        }

        if (isRollingStandalone || (IsSpawned && isRollingNet.Value))
        {
            if (rb != null)
            {
                float currentYVelocity = rb.linearVelocity.y;
                rb.linearVelocity = new Vector3(rollDirection.x * rollSpeed, currentYVelocity, rollDirection.z * rollSpeed);
            }
            return;
        }

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;

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

        Vector3 movementTranslation = move;
        if (isRootedAttack && IsPlayingActionAnimation())
        {
            movementTranslation = Vector3.zero;
        }

        if (rb != null)
        {
            Vector3 targetVelocity = movementTranslation * currentSpeed;
            float currentYVelocity = rb.linearVelocity.y;

            if (knockbackVelocity.magnitude > 0.01f)
            {
                targetVelocity += knockbackVelocity;
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
            }

            rb.linearVelocity = new Vector3(targetVelocity.x, currentYVelocity, targetVelocity.z);
        }

        bool isArmed = (GetActiveWeaponIndex() == 2);
        bool isMoving = (movementTranslation != Vector3.zero);

        float targetInputX = 0f;
        float targetInputZ = 0f;
        float targetSpeed = 0f;

        bool isAttacking = IsPlayingAttackState(out _, out _);

        bool shouldAlignToCamera = (isArmed && rotateToCameraWhenArmed) ||
                                   (!isArmed && rotateToCameraWhenUnarmed) ||
                                   isAttacking;

        if (shouldAlignToCamera)
        {
            if (targetCamera != null)
            {
                Vector3 camForward = targetCamera.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(camForward.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeedArmed);
                }
            }

            if (isMoving)
            {
                targetInputX = moveX * (isRunning ? 1.0f : 0.5f);
                targetInputZ = moveZ * (isRunning ? 1.0f : 0.5f);
                targetSpeed = new Vector2(targetInputX, targetInputZ).magnitude;
            }
        }
        else
        {
            if (isMoving)
            {
                Quaternion targetRot = Quaternion.LookRotation(movementTranslation.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeedUnarmed);

                targetInputX = 0f;
                targetInputZ = isRunning ? 1.0f : 0.5f;
                targetSpeed = targetInputZ;
            }
        }

        smoothedInputX = Mathf.MoveTowards(smoothedInputX, targetInputX, Time.deltaTime * inputFilterSpeed);
        smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, targetInputZ, Time.deltaTime * inputFilterSpeed);
        UpdateAnimatorParams(targetSpeed);

        if (!useBlendTree && !IsPlayingActionAnimation())
        {
            if (isMoving)
            {
                string moveAnim = isRunning ? "run" : "Walk";
                PlayAnimation(moveAnim, 0.1f);
            }
            else
            {
                PlayAnimation("Idle", 0.1f);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsSpawned && !IsUIBlockingInput() && CanAttack())
            {
                RequestComboAttack(true);
            }
        }

        if (Input.GetKeyDown(rollKey))
        {
            if (!IsUIBlockingInput() && rollCooldownTimer <= 0 && !IsPlayingAttackState(out _, out _))
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

        ClearAttackLayer();
        InterruptCombo();

        if (moveInput != Vector3.zero)
        {
            rollDirection = moveInput.normalized;
            transform.forward = rollDirection;
        }
        else
        {
            rollDirection = transform.forward;
        }

        if (anim != null) anim.applyRootMotion = false;
        PlayAnimation("LonVong", 0.05f);
    }

    protected virtual void StartRollOwner(Vector3 moveInput)
    {
        isRollingStandalone = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;

        ClearAttackLayer();
        InterruptCombo();

        if (moveInput != Vector3.zero)
        {
            rollDirection = moveInput.normalized;
            transform.forward = rollDirection;
        }
        else
        {
            rollDirection = transform.forward;
        }

        if (anim != null) anim.applyRootMotion = false;
        PlayAnimation("LonVong", 0.05f, false);
        StartRollServerRpc(rollDirection);
    }

    public void OnRollEnd()
    {
        Debug.Log("[ArthurPlayer] Roll ended.");
        isRollingStandalone = false;

        if (anim != null) anim.applyRootMotion = false;
        var bridge = GetRootMotionBridge();
        if (bridge != null) bridge.ApplyFinalOffset();

        if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        if (!isStandaloneMode && IsOwner)
        {
            StopRollServerRpc();
        }
    }

    void LateUpdate()
    {
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

            currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * rotationSmoothSpeed);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * rotationSmoothSpeed);

            float yawRad = currentYaw * Mathf.Deg2Rad;
            float pitchRad = currentPitch * Mathf.Deg2Rad;

            Vector3 rotatedOffset = new Vector3(
                cameraDistance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
                cameraDistance * Mathf.Sin(pitchRad),
                -cameraDistance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
            );

            Vector3 targetPosition = (transform.position + Vector3.up * cameraPivotHeight) + rotatedOffset;
            targetCamera.transform.position = targetPosition;

            if (cameraLookAtPlayer)
            {
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
            if (step == 1) return punch1Duration;
            if (step == 2) return punch2Duration;
            return punch3Duration;
        }
        else if (weaponIndex == 2)
        {
            if (step == 1) return slash1Duration;
            if (step == 2) return slash2Duration;
            return slash3Duration;
        }
        return 0.5f;
    }

    protected virtual bool CanAttack()
    {
        if (CurrentHealth <= 0) return false;

        // Cannot attack if rolling
        if (isStandaloneMode ? isRollingStandalone : rollTimer > 0) return false;

        // Cannot attack if hit, picking, or dead
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("LonVong") ||
                stateInfo.IsName("GetHit") ||
                stateInfo.IsName("GeiHit2") ||
                stateInfo.IsName("Idle_Pick") ||
                stateInfo.IsName("Death"))
            {
                if (stateInfo.normalizedTime < 0.95f)
                    return false;
            }
        }
        return true;
    }

    protected virtual void RequestComboAttack(bool networkMode)
    {
        if (isExecutingAttack)
        {
            // Always buffer the request - NEVER interrupt a running animation directly.
            // HandleAttackSequenceEnd (OnPunchEnd event) will pick it up when the animation finishes.
            pendingAttackRequest = true;
            Debug.Log("[ArthurPlayer] Nhấp chuột -> Lưu vào buffer, chờ OnPunchEnd.");
        }
        else
        {
            // Not attacking: start combo immediately
            PerformComboAttack(networkMode);
        }
    }

    protected virtual void PerformComboAttack(bool networkMode)
    {
        int weapon = GetActiveWeaponIndex();
        float currentTime = Time.time;

        int nextStep = comboStep;
        if (currentTime - lastAttackTime > comboWindow)
        {
            nextStep = 0;
        }
        nextStep++;

        if (weapon == 1)
        {
            if (nextStep > 3) nextStep = 1;
        }
        else if (weapon == 2)
        {
            if (nextStep > 3) nextStep = 1;
        }

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        bool isMovingInput = (Mathf.Abs(moveX) > 0.01f || Mathf.Abs(moveZ) > 0.01f);

        if (comboStep == 0 || currentTime - lastAttackTime > comboWindow)
        {
            isRootedAttack = !isMovingInput;
        }

        comboStep = nextStep;
        lastAttackTime = currentTime;
        attackAnimStartTime = currentTime;
        isExecutingAttack = true;
        pendingAttackRequest = false;

        currentAttackAnimDuration = GetAttackDuration(weapon, comboStep);
        earliestValidEventTime = currentTime + 0.15f;

        // Clear hit enemies list for this new swing and disable hitboxes
        alreadyHitEnemies.Clear();
        DisableAllHitboxes();

        string animToPlay = "";
        if (weapon == 1)
        {
            if (comboStep == 1) animToPlay = "Punch1";
            else if (comboStep == 2) animToPlay = "Punch2";
            else if (comboStep == 3) animToPlay = "Punch3";
        }
        else if (weapon == 2)
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
    }

    private void HandleAttackSequenceEnd(bool isSlash)
    {
        if (isSlash)
        {
            DisableBothWeaponHitboxes();
        }
        else
        {
            DisableBothHitboxes();
        }

        // End active attack window
        isExecutingAttack = false;

        // Check if there is a buffered click OR if the mouse button is currently held down
        bool shouldContinue = pendingAttackRequest || (!IsUIBlockingInput() && CanAttack() && Input.GetMouseButton(0));

        if (shouldContinue)
        {
            pendingAttackRequest = false;
            bool networkMode = !isStandaloneMode && IsOwner;
            Debug.Log("[ArthurPlayer] Tiếp tục combo từ buffer hoặc đè chuột sau khi kết thúc đòn cũ.");
            PerformComboAttack(networkMode);
        }
        else
        {
            // No buffered request and mouse not held: reset combo step and clean layer
            comboStep = 0;
            isRootedAttack = false;
            ClearAttackLayer();
            Debug.Log($"[ArthurPlayer] Kết thúc chuỗi {(isSlash ? "chém" : "đấm")} - không có input tiếp theo.");
        }
    }

    private void InterruptCombo()
    {
        pendingAttackRequest = false;
        isExecutingAttack = false;
        comboStep = 0;
        DisableAllHitboxes();
        alreadyHitEnemies.Clear();
        Debug.Log("[ArthurPlayer] Combo bị ngắt (bị hit/chết/lộn vòng).");
    }

    /// <summary>Lowercase alias for OnPunchEnd - Unity Animation Events are case-sensitive.</summary>
    public void Onpunchend()
    {
        OnPunchEnd();
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

    [ServerRpc]
    protected void AttackServerRpc()
    {
        // Server clears its own hit list for the new swing
        alreadyHitEnemies.Clear();
    }

    // ------------------------------------------------------------------
    //  Hitbox Combat System & Animation Event Receivers (Arthur)
    // ------------------------------------------------------------------
    private bool IsColliderValid(Collider col)
    {
        if (col == null) return false;
        try
        {
            var test = col.enabled;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void EnableLeftHitbox()
    {
        if (!CanActivateHitbox()) return;
        alreadyHitEnemies.Clear();
        if (IsColliderValid(leftHitbox))
        {
            leftHitbox.enabled = true;
            Debug.Log("[ArthurPlayer] Left hitbox ENABLED.");
        }
    }

    public void DisableLeftHitbox()
    {
        if (IsColliderValid(leftHitbox))
        {
            leftHitbox.enabled = false;
            Debug.Log("[ArthurPlayer] Left hitbox DISABLED.");
        }
        alreadyHitEnemies.Clear();
    }

    public void EnableRightHitbox()
    {
        if (!CanActivateHitbox()) return;
        alreadyHitEnemies.Clear();
        if (IsColliderValid(rightHitbox))
        {
            rightHitbox.enabled = true;
            Debug.Log("[ArthurPlayer] Right hitbox ENABLED.");
        }
    }

    public void DisableRightHitbox()
    {
        if (IsColliderValid(rightHitbox))
        {
            rightHitbox.enabled = false;
            Debug.Log("[ArthurPlayer] Right hitbox DISABLED.");
        }
        alreadyHitEnemies.Clear();
    }

    public void EnableBothHitboxes()
    {
        if (!CanActivateHitbox()) return;
        alreadyHitEnemies.Clear();
        if (IsColliderValid(leftHitbox)) leftHitbox.enabled = true;
        if (IsColliderValid(rightHitbox)) rightHitbox.enabled = true;
        Debug.Log("[ArthurPlayer] Both hitboxes ENABLED.");
    }

    public void DisableBothHitboxes()
    {
        if (IsColliderValid(leftHitbox)) leftHitbox.enabled = false;
        if (IsColliderValid(rightHitbox)) rightHitbox.enabled = false;
        alreadyHitEnemies.Clear();
        Debug.Log("[ArthurPlayer] Both hitboxes DISABLED.");
    }

    public void EnableLeftWeaponHitbox()
    {
        if (!CanActivateHitbox()) return;
        alreadyHitEnemies.Clear();
        if (IsColliderValid(leftWeaponHitbox))
        {
            leftWeaponHitbox.enabled = true;
            Debug.Log("[ArthurPlayer] Left Weapon hitbox ENABLED.");
        }
    }

    public void DisableLeftWeaponHitbox()
    {
        if (IsColliderValid(leftWeaponHitbox)) leftWeaponHitbox.enabled = false;
        alreadyHitEnemies.Clear();
        Debug.Log("[ArthurPlayer] Left Weapon hitbox DISABLED.");
    }

    public void EnableRightWeaponHitbox()
    {
        if (!CanActivateHitbox()) return;
        alreadyHitEnemies.Clear();
        if (IsColliderValid(rightWeaponHitbox))
        {
            rightWeaponHitbox.enabled = true;
            Debug.Log("[ArthurPlayer] Right Weapon hitbox ENABLED.");
        }
    }

    public void DisableRightWeaponHitbox()
    {
        if (IsColliderValid(rightWeaponHitbox)) rightWeaponHitbox.enabled = false;
        alreadyHitEnemies.Clear();
        Debug.Log("[ArthurPlayer] Right Weapon hitbox DISABLED.");
    }

    public void EnableBothWeaponHitboxes()
    {
        if (!CanActivateHitbox()) return;
        alreadyHitEnemies.Clear();
        if (IsColliderValid(leftWeaponHitbox)) leftWeaponHitbox.enabled = true;
        if (IsColliderValid(rightWeaponHitbox)) rightWeaponHitbox.enabled = true;
        Debug.Log("[ArthurPlayer] Both Weapon hitboxes ENABLED.");
    }

    public void DisableBothWeaponHitboxes()
    {
        if (IsColliderValid(leftWeaponHitbox)) leftWeaponHitbox.enabled = false;
        if (IsColliderValid(rightWeaponHitbox)) rightWeaponHitbox.enabled = false;
        alreadyHitEnemies.Clear();
        Debug.Log("[ArthurPlayer] Both Weapon hitboxes DISABLED.");
    }

    public void DisableAllHitboxes()
    {
        DisableBothHitboxes();
        DisableBothWeaponHitboxes();
    }

    public void OnPunchEnd()
    {
        if (Time.time < earliestValidEventTime)
        {
            Debug.Log("[ArthurPlayer] OnPunchEnd ignored (stale event).");
            return;
        }
        HandleAttackSequenceEnd(false);
        Debug.Log("[ArthurPlayer] OnPunchEnd - Animation Event.");
    }

    public void OnSlashEnd()
    {
        if (Time.time < earliestValidEventTime)
        {
            Debug.Log("[ArthurPlayer] OnSlashEnd ignored (stale event).");
            return;
        }
        HandleAttackSequenceEnd(true);
        Debug.Log("[ArthurPlayer] OnSlashEnd - Animation Event.");
    }

    public void OnAttackEnd()
    {
        if (Time.time < earliestValidEventTime)
        {
            Debug.Log("[ArthurPlayer] OnAttackEnd ignored (stale event).");
            return;
        }
        HandleAttackSequenceEnd(false);
        Debug.Log("[ArthurPlayer] OnAttackEnd - Animation Event.");
    }

    private bool CanActivateHitbox()
    {
        return isStandaloneMode || (IsSpawned && IsOwner);
    }

    /// <summary>
    /// Nhận va chạm từ PlayerHitbox khi enemy đi vào hitbox.
    /// Tính damage 1 lần duy nhất mỗi enemy trong mỗi đòn đánh.
    /// </summary>
    public void OnHitboxCollision(Collider other)
    {
        if (!CanActivateHitbox()) return; // Chỉ Owner/Standalone xử lý damage

        if (IsEnemy(other, out Collider enemyCollider))
        {
            Transform enemyRoot = enemyCollider.transform.root;
            if (!alreadyHitEnemies.Contains(enemyRoot))
            {
                alreadyHitEnemies.Add(enemyRoot);
                Debug.Log($"[ArthurPlayer] 💥 HIT: {enemyRoot.name} | Damage: {damageAmount}");

                if (isStandaloneMode)
                {
                    TryDamageEnemy(enemyCollider);
                }
                else if (IsOwner)
                {
                    var netObj = enemyCollider.GetComponentInParent<NetworkObject>();
                    if (netObj != null) DamageEnemyServerRpc(netObj);
                    else TryDamageEnemy(enemyCollider);
                }
            }
        }
    }

    private bool IsEnemy(Collider col, out Collider enemyCollider)
    {
        enemyCollider = null;
        if (col == null) return false;

        if (col.GetComponentInParent<Enemy1_DapBua>() != null ||
            col.GetComponentInParent<Enemy2_Zombie>() != null ||
            col.GetComponentInParent<Enemy3_Buaa>() != null ||
            col.GetComponentInParent<Enemy4_Bongtoi>() != null ||
            col.GetComponentInParent<Enemy5_PhuThuy>() != null)
        {
            enemyCollider = col;
            return true;
        }
        return false;
    }

    [ServerRpc]
    private void DamageEnemyServerRpc(NetworkObjectReference enemyRef)
    {
        if (enemyRef.TryGet(out NetworkObject netObj))
        {
            var col = netObj.GetComponent<Collider>();
            if (col != null) TryDamageEnemy(col);
            else
            {
                var e1 = netObj.GetComponentInChildren<Enemy1_DapBua>();
                if (e1 != null) { e1.TakeDamage(damageAmount); return; }
                var e2 = netObj.GetComponentInChildren<Enemy2_Zombie>();
                if (e2 != null) { e2.TakeDamage(damageAmount); return; }
                var e3 = netObj.GetComponentInChildren<Enemy3_Buaa>();
                if (e3 != null) { e3.TakeDamage(damageAmount); return; }
                var e4 = netObj.GetComponentInChildren<Enemy4_Bongtoi>();
                if (e4 != null) { e4.TakeDamage(damageAmount); return; }
                var e5 = netObj.GetComponentInChildren<Enemy5_PhuThuy>();
                if (e5 != null) { e5.TakeDamage(damageAmount); return; }
            }
        }
    }

    [ContextMenu("Create Sword Hitboxes")]
    public void CreateSwordHitboxes()
    {
        Transform leftHand = FindBoneRecursive(transform, "left");
        Transform rightHand = FindBoneRecursive(transform, "right");

        if (leftHand == null) leftHand = transform;
        if (rightHand == null) rightHand = transform;

        // --- Left Hand Hitbox ---
        {
            Transform existingLeft = leftHand.Find("LeftHitbox");
            GameObject leftObj = existingLeft != null ? existingLeft.gameObject : new GameObject("LeftHitbox");
            if (existingLeft == null)
            {
                leftObj.transform.SetParent(leftHand);
                leftObj.transform.localPosition = Vector3.zero;
                leftObj.transform.localRotation = Quaternion.identity;
                leftObj.transform.localScale = Vector3.one;
            }
            BoxCollider col = leftObj.GetComponent<BoxCollider>();
            if (col == null) col = leftObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.6f, 0.6f, 0.6f);
            col.center = new Vector3(0f, 0f, 0.2f);
            col.enabled = false;
            if (leftObj.GetComponent<PlayerHitbox>() == null) leftObj.AddComponent<PlayerHitbox>();
            leftHitbox = col;
        }

        // --- Right Hand Hitbox ---
        {
            Transform existingRight = rightHand.Find("RightHitbox");
            GameObject rightObj = existingRight != null ? existingRight.gameObject : new GameObject("RightHitbox");
            if (existingRight == null)
            {
                rightObj.transform.SetParent(rightHand);
                rightObj.transform.localPosition = Vector3.zero;
                rightObj.transform.localRotation = Quaternion.identity;
                rightObj.transform.localScale = Vector3.one;
            }
            BoxCollider col = rightObj.GetComponent<BoxCollider>();
            if (col == null) col = rightObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.6f, 0.6f, 0.6f);
            col.center = new Vector3(0f, 0f, 0.2f);
            col.enabled = false;
            if (rightObj.GetComponent<PlayerHitbox>() == null) rightObj.AddComponent<PlayerHitbox>();
            rightHitbox = col;
        }

        // Kiếm chém tạm thời bỏ qua, sẽ làm sau
        leftWeaponHitbox = null;
        rightWeaponHitbox = null;
    }

    private Transform FindWeaponTransform(Transform hand)
    {
        for (int i = 0; i < hand.childCount; i++)
        {
            Transform child = hand.GetChild(i);
            string nameLower = child.name.ToLower();
            if (nameLower.Contains("sword") || nameLower.Contains("blade") || nameLower.Contains("weapon") ||
                nameLower.Contains("kiem") || nameLower.Contains("dao") || nameLower.Contains("katana") || nameLower.Contains("weapon_r") || nameLower.Contains("weapon_l"))
            {
                return child;
            }
            Transform subChild = FindWeaponTransform(child);
            if (subChild != null) return subChild;
        }
        return null;
    }

    private Transform FindBoneRecursive(Transform current, string keyword)
    {
        string nameLower = current.name.ToLower();
        if (nameLower.Contains(keyword) && (nameLower.Contains("hand") || nameLower.Contains("wrist") || nameLower.Contains("palm") || nameLower.Contains("finger")))
        {
            if (nameLower.Contains("hand")) return current;
        }
        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindBoneRecursive(current.GetChild(i), keyword);
            if (found != null) return found;
        }
        if (current.name.ToLower().Contains(keyword) && current.name.ToLower().Contains("hand")) return current;
        return null;
    }

    public void TakeDamage(float damage)
    {
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

            InterruptCombo();

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

        InterruptCombo();

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

    public void UpdateStateFromHUD(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
    {
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
                    true,
                    false,
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
                    hud.SetSkillsUnlocked(false, false);
                    hud.SetWeapon2Locked(true, false);
                    hud.SelectWeapon(state.activeWeaponIndex);
                    hud.UpdateUpgradeUI(state.upgradePoints, state.hpLevel, state.mpLevel, state.cooldownLevel, state.damageLevel);
                    hud.SetHealth(state.health / (150f + state.hpLevel * 20f));

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

        maxHealth = 150f + hp * 20f;
        damageAmount = 15f + dmg * 5f;

        currentHealth.Value = health;
        activeWeaponIndex.Value = weaponIndex;
        isWeapon2Locked.Value = weapon2Locked;
        isSkillsUnlocked.Value = skillsUnlocked;

        isSyncingFromDb = false;
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
               name == "Punch3" ||
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3" ||
               (!string.IsNullOrEmpty(drawWeaponTrigger) && name == drawWeaponTrigger) ||
               (!string.IsNullOrEmpty(sheathWeaponTrigger) && name == sheathWeaponTrigger) ||
               (!string.IsNullOrEmpty(drawLeftTrigger) && name == drawLeftTrigger) ||
               (!string.IsNullOrEmpty(drawRightTrigger) && name == drawRightTrigger) ||
               (!string.IsNullOrEmpty(sheatheLeftTrigger) && name == sheatheLeftTrigger) ||
               (!string.IsNullOrEmpty(sheatheRightTrigger) && name == sheatheRightTrigger);
    }

    public void PlayWeaponSwitchAnimation(int oldWeapon, int newWeapon)
    {
        if (oldWeapon == newWeapon) return;

        isSwitchingWeapon = true; // Khóa chống spam phím khi đổi vũ khí

        if (newWeapon == 2)
        {
            // Bắt đầu rút vũ khí: lúc này vũ khí vẫn ở trên vai/lưng, tay chưa cầm
            if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(true);
            if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(true);
            if (leftHandWeapon != null) leftHandWeapon.SetActive(false);
            if (rightHandWeapon != null) rightHandWeapon.SetActive(false);

            if (!string.IsNullOrEmpty(drawLeftTrigger))
            {
                PlayAnimation(drawLeftTrigger, 0.1f);
            }
            else if (!string.IsNullOrEmpty(drawWeaponTrigger))
            {
                PlayAnimation(drawWeaponTrigger, 0.1f);
            }
            else
            {
                SyncWeaponVisuals(newWeapon);
                OnWeaponSwitchEnd();
            }
        }
        else if (newWeapon == 1)
        {
            // Bắt đầu cất vũ khí: lúc này vũ khí vẫn ở trên tay, chưa cất lên vai/lưng
            if (leftHandWeapon != null) leftHandWeapon.SetActive(true);
            if (rightHandWeapon != null) rightHandWeapon.SetActive(true);
            if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(false);
            if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(false);

            if (!string.IsNullOrEmpty(sheatheLeftTrigger))
            {
                PlayAnimation(sheatheLeftTrigger, 0.1f);
            }
            else if (!string.IsNullOrEmpty(sheathWeaponTrigger))
            {
                PlayAnimation(sheathWeaponTrigger, 0.1f);
            }
            else
            {
                SyncWeaponVisuals(newWeapon);
                OnWeaponSwitchEnd();
            }
        }
    }

    public void OnDrawLeftEnd()
    {
        Debug.Log("[ArthurPlayer] Draw Left finished. Letting Animator transition natively to Draw Right.");
    }

    public void OnSheatheLeftEnd()
    {
        Debug.Log("[ArthurPlayer] Sheathe Left finished. Letting Animator transition natively to Sheathe Right.");
    }

    public void DrawLeftSword()
    {
        if (leftHandWeapon != null) leftHandWeapon.SetActive(true);
        if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(false);
        Debug.Log("[ArthurPlayer] Left weapon DRAWN.");
    }

    public void DrawRightSword()
    {
        if (rightHandWeapon != null) rightHandWeapon.SetActive(true);
        if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(false);
        Debug.Log("[ArthurPlayer] Right weapon DRAWN.");
    }

    public void SheatheLeftSword()
    {
        if (leftHandWeapon != null) leftHandWeapon.SetActive(false);
        if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(true);
        Debug.Log("[ArthurPlayer] Left weapon SHEATHED.");
    }

    public void SheatheRightSword()
    {
        if (rightHandWeapon != null) rightHandWeapon.SetActive(false);
        if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(true);
        Debug.Log("[ArthurPlayer] Right weapon SHEATHED.");
    }

    public void SyncWeaponVisuals(int activeWeapon)
    {
        bool isArmed = (activeWeapon == 2);

        if (leftHandWeapon != null) leftHandWeapon.SetActive(isArmed);
        if (rightHandWeapon != null) rightHandWeapon.SetActive(isArmed);

        if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(!isArmed);
        if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(!isArmed);
    }

    public void OnWeaponSwitchEnd()
    {
        isSwitchingWeapon = false;
        Debug.Log("[ArthurPlayer] Weapon switch animation finished. Lock released.");
    }

    protected virtual bool IsAttackAnimationName(string name)
    {
        return name == "Punch1" ||
               name == "Punch2" ||
               name == "Punch3" ||
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3";
    }

    protected virtual bool IsPlayingAttackState(out AnimatorStateInfo activeState, out int layer)
    {
        activeState = default;
        layer = -1;

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
            return false;

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
               stateInfo.IsName("Punch3") ||
               stateInfo.IsName("Damtrai2") ||
               stateInfo.IsName("DamPhai2") ||
               stateInfo.IsName("Damcombo2") ||
               stateInfo.IsName("Slash1") ||
               stateInfo.IsName("Slash2") ||
               stateInfo.IsName("Slash3");
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

        if (IsFullBodyActionAnimation(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.15f) return true;

        if (isRootedAttack && IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.15f) return true;

        if (isStandaloneMode ? isRollingStandalone : rollTimer > 0) return true;

        if (isRootedAttack && IsPlayingAttackState(out _, out _))
        {
            return true;
        }

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

        if (anim == null) return;

        if (!anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;

        bool isLoopingAnim = animName == "Idle" || animName == "Walk" || animName == "run";
        if (isLoopingAnim && currentAnimState == animName) return;

        Debug.Log($"[ArthurPlayer] Kích hoạt Trigger hoạt ảnh: '{animName}'");

        anim.ResetTrigger("Idle");
        anim.ResetTrigger("Walk");
        anim.ResetTrigger("run");
        anim.ResetTrigger("Death");
        anim.ResetTrigger("GetHit");
        anim.ResetTrigger("GeiHit2");
        anim.ResetTrigger("LonVong");
        anim.ResetTrigger("Idle_Pick");

        if (IsActionAnimationName(animName))
        {
            anim.ResetTrigger("Punch1");
            anim.ResetTrigger("Punch2");
            anim.ResetTrigger("Punch3");
            anim.ResetTrigger("Slash1");
            anim.ResetTrigger("Slash2");
            anim.ResetTrigger("Slash3");
            if (!string.IsNullOrEmpty(drawWeaponTrigger)) anim.ResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) anim.ResetTrigger(sheathWeaponTrigger);
            if (!string.IsNullOrEmpty(drawLeftTrigger)) anim.ResetTrigger(drawLeftTrigger);
            if (!string.IsNullOrEmpty(drawRightTrigger)) anim.ResetTrigger(drawRightTrigger);
            if (!string.IsNullOrEmpty(sheatheLeftTrigger)) anim.ResetTrigger(sheatheLeftTrigger);
            if (!string.IsNullOrEmpty(sheatheRightTrigger)) anim.ResetTrigger(sheatheRightTrigger);
        }

        anim.SetTrigger(animName);

        bool isAttack = IsAttackAnimationName(animName);
        if (isAttack)
        {
            isExecutingAttack = true;
            attackAnimStartTime = Time.time;
            int weapon = GetActiveWeaponIndex();
            currentAttackAnimDuration = GetAttackDuration(weapon, comboStep);
        }
        else
        {
            if (isLoopingAnim || animName == "LonVong" || animName == "Death" || animName.Contains("Hit") || animName == "Idle_Pick")
            {
                if (!IsPlayingAttackState(out _, out _))
                {
                    isExecutingAttack = false;
                }
            }
        }

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
        if (alreadyPlayedLocally && IsOwner) return;
        PlayAnimationLocal(animName, fadeTime);
    }

    [ServerRpc]
    private void StartRollServerRpc(Vector3 direction)
    {
        isRollingNet.Value = true;
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

    public void OnPickItemEvent()
    {
        if (pendingPickItem != null)
        {
            var collectible = pendingPickItem.GetComponent<CollectibleItemDrop>();
            if (collectible != null) collectible.ConfirmCollect();
            else
            {
                var repair = pendingPickItem.GetComponent<RepairItemDrop>();
                if (repair != null) repair.ConfirmCollect();
            }
            pendingPickItem = null;
        }
    }

    private void UpdateAttackLayerWeight()
    {
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(1);
            bool isSlashActive = !stateInfo.IsName("New State") && !stateInfo.IsName("Empty");
            float targetAttackLayerWeight = isSlashActive ? 1f : 0f;

            float currentWeight = anim.GetLayerWeight(1);
            float smoothedWeight = Mathf.MoveTowards(currentWeight, targetAttackLayerWeight, Time.deltaTime * 10f);
            anim.SetLayerWeight(1, smoothedWeight);
        }
    }

    protected void UpdateComboChain()
    {
        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (!hasControl) return;

        if (isExecutingAttack)
        {
            float elapsed = Time.time - attackAnimStartTime;

            // --- ĐÃ XÓA: Phần tự động kích hoạt đòn sớm (Early combo chain) để đợi hoạt ảnh chạy xong hẳn ---

            // --- SỬA LẠI: Bộ bảo hiểm Fallback (Chỉ reset khi đòn đánh đã quá thời gian thực tế + không còn transition) ---
            if (elapsed > currentAttackAnimDuration + 0.3f)
            {
                if (!IsPlayingAttackState(out _, out _) && !anim.IsInTransition(0) && !anim.IsInTransition(1))
                {
                    isExecutingAttack = false;
                    comboStep = 0;
                    pendingAttackRequest = false;
                    DisableAllHitboxes();
                }
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
