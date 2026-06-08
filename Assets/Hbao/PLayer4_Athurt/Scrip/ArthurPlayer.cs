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
            return 1;
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

        if (Input.GetMouseButtonDown(0))
        {
            if (!IsUIBlockingInput() && !isRollingStandalone && !IsPlayingActionAnimation())
            {
                PerformComboAttack(false);
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
            if (IsSpawned && !IsUIBlockingInput() && !isRollingStandalone && !IsPlayingActionAnimation())
            {
                PerformComboAttack(true);
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

        int nextStep = comboStep;
        if (currentTime - lastAttackTime > comboWindow)
        {
            nextStep = 0;
        }
        nextStep++;

        if (weapon == 1)
        {
            if (nextStep > 2) nextStep = 1;
        }
        else if (weapon == 2)
        {
            if (nextStep > 3) nextStep = 1;
        }

        if (comboStep > 0 && currentTime - lastAttackTime <= comboWindow)
        {
            float prevDuration = GetAttackDuration(weapon, comboStep);
            if (currentTime - lastAttackTime < prevDuration * comboTransitionThreshold)
            {
                return; 
            }
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

        string animToPlay = "";
        if (weapon == 1) 
        {
            animToPlay = comboStep == 1 ? "Punch1" : "Punch2";
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
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3" ||
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
            anim.ResetTrigger("Slash1");
            anim.ResetTrigger("Slash2");
            anim.ResetTrigger("Slash3");
            if (!string.IsNullOrEmpty(drawWeaponTrigger)) anim.ResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) anim.ResetTrigger(sheathWeaponTrigger);
        }

        anim.SetTrigger(animName);

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

    public override void OnDestroy()
    {
        if (PlayerHUDManager.ActivePlayers != null)
        {
            PlayerHUDManager.ActivePlayers.Remove(this);
        }
        base.OnDestroy();
    }
}
