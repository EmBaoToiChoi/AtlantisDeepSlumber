using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Independent custom player controller for Leo Assassin, inheriting directly from NetworkBehaviour.
/// Operates seamlessly with the SimplePlayerTest proxy component to maintain complete compatibility
/// with enemy AI, NPC dialog, and UI HUD systems.
/// Supports smooth 8-directional Blend Tree movement using actual user fbx filenames.
/// </summary>
public class LeoPlayer : NetworkBehaviour
{
    [Header("Input Keys Configuration")]
    [Tooltip("Key to trigger roll/dodge.")]
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
    public bool rotateToCameraWhenUnarmed = false;

    [Header("Player Settings & Stats")]
    public float moveSpeed = 3.5f;
    public float runSpeedMultiplier = 3.5f;
    public float damageAmount = 25f;
    public float attackRange = 2f;

    [Header("Combo Attack Settings")]
    public float comboWindow = 1.0f;
    public float comboTransitionThreshold = 0.5f;
    public float punch1Duration = 0.5f;
    public float punch2Duration = 0.5f;
    public float punch3Duration = 0.5f;
    public float slash1Duration = 0.6f;
    public float slash2Duration = 0.6f;
    public float slash3Duration = 0.7f;
    protected int comboStep = 0;
    protected float lastAttackTime = 0f;
    protected bool isRootedAttack = false;

    [Header("Hitbox References")]
    [Tooltip("Left hand hitbox collider.")]
    public Collider leftHitbox;
    [Tooltip("Right hand hitbox collider.")]
    public Collider rightHitbox;
    
    [Tooltip("Left weapon/sword hitbox collider.")]
    public Collider leftWeaponHitbox;
    [Tooltip("Right weapon/sword hitbox collider.")]
    public Collider rightWeaponHitbox;

    [Header("Weapon Visual References")]
    [Tooltip("Thanh kiếm trên tay trái")]
    public GameObject leftHandSword;
    [Tooltip("Thanh kiếm trên tay phải")]
    public GameObject rightHandSword;
    [Tooltip("Thanh kiếm giắt trên vai/lưng trái")]
    public GameObject leftShoulderSword;
    [Tooltip("Thanh kiếm giắt trên vai/lưng phải")]
    public GameObject rightShoulderSword;

    private System.Collections.Generic.List<Transform> alreadyHitEnemies = new System.Collections.Generic.List<Transform>();

    [Header("Movement Lock State")]
    [Tooltip("If true, movement inputs are ignored and Rigidbody XZ velocity is locked to 0.")]
    public bool isMovementLocked = false;

    private float targetAttackLayerWeight = 0f;
    private float currentAttackLayerWeight = 0f;

    [Header("Attack Dash Settings")]
    [Tooltip("Speed of the automatic forward dash during combo attacks.")]
    public float attackDashSpeed = 4.0f;
    [Tooltip("Duration of the automatic forward dash during combo attacks.")]
    public float attackDashDuration = 0.25f;
    private float attackDashTimer = 0f;
    private Vector3 attackDashDirection;

    [Header("Weapon Switch Animations")]
    public string drawWeaponTrigger = "DrawWeapon";
    public string sheathWeaponTrigger = "SheathWeapon";
    public string drawLeftTrigger = "DrawLeft";
    public string drawRightTrigger = "DrawRight";
    public string sheatheLeftTrigger = "SheatheLeft";
    public string sheatheRightTrigger = "SheatheRight";
    [HideInInspector]
    public bool isSwitchingWeapon = false; // Chống spam phím khi đổi vũ khí

    [Header("Player Health Settings")]
    public float maxHealth = 85f;

    [Header("Player Class Settings")]
    [Tooltip("0 = Sát Thủ, 1 = Hỏa Thuật, 2 = Cung Thủ, 3 = Tanker")]
    public int characterClassIndex = 0;

    [Header("Knockback Settings")]
    protected Vector3 knockbackVelocity;

    [Header("Camera Follow Settings")]
    public bool enableCameraFollow = true;
    public Vector3 cameraOffset = new Vector3(0f, 10f, -6.5f);
    public float cameraSmoothSpeed = 5f;
    public bool cameraLookAtPlayer = true;
    public float cameraPivotHeight = 3.5f;
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
    protected bool isRollingStandalone = false;

    // ------------------------------------------------------------------
    //  Network Sync Variables
    // ------------------------------------------------------------------
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(85f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> activeWeaponIndex = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isWeapon2Locked = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isSkillsUnlocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> upgradePoints = new NetworkVariable<int>(5, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hpLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> mpLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> cooldownLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> damageLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> playerLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> playerExp = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> weapon1Durability = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> weapon2Durability = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isRollingNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isMovementLockedNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [HideInInspector]
    public GameObject pendingPickItem;

    [Header("Local State & Inventory")]
    public string[] inventorySlots = new string[10] { "", "", "", "", "", "", "", "", "", "" };
    protected int localUpgradePoints = 5;
    protected int localHpLevel = 0;
    protected int localMpLevel = 0;
    protected int localCooldownLevel = 0;
    protected int localDamageLevel = 0;
    protected int localLevel = 0;
    protected float localExp = 0f;
    protected float localHealth;
    protected float localWeapon1Durability = 100f;
    protected float localWeapon2Durability = 100f;

    public bool isStandaloneMode = false;
    protected bool isSyncingFromDb = false;

    [Header("Weapon Durability Config")]
    public float weapon1MaxDurability = 100f;
    public float weapon2MaxDurability = 100f;

    // Trigger lists (Unarmed fallback)
    public string idleUnarmed = "Idle";
    public string walkUnarmed = "Walk";
    public string runUnarmed = "run";
    public string walkBackwardUnarmed = "WalkBackward";
    public string runBackwardUnarmed = "RunBackward";
    public string walkLeftUnarmed = "walkleftkhongvukhi";
    public string walkRightUnarmed = "walkrightkhongvukhi";
    public string runLeftUnarmed = "Runleftkhongvukhi";
    public string runRightUnarmed = "Rủnightnovukhi"; // Note: Vietnamese accent 'ủ'

    // Trigger lists (Armed fallback)
    public string idleArmed = "IdleArmed";
    public string walkForwardArmed = "WalkForwardArmed";
    public string walkBackwardArmed = "WalkBackwardArmed";
    public string runForwardArmed = "RunForwardArmed";
    public string runBackwardArmed = "RunBackwardArmed";
    public string walkLeftArmed = "walkleftcovukhi";
    public string walkRightArmed = "walkrightcovukhi";
    public string runLeftArmed = "Runleftcovukhi";
    public string runRightArmed = "Rủnightcovukhi"; // Note: Vietnamese accent 'ủ'

    // Action and attack triggers
    public string rollTrigger = "Lonmeo";
    public string pickTrigger = "Pick";
    public string punch1Trigger = "DamTrai";
    public string punch2Trigger = "DamPhai";
    public string punch3Trigger = "DamCombo";
    public string slash1Trigger = "Combo1kiem";
    public string slash2Trigger = "Attackdoucombo";

    // Death and hit
    public string deathUnarmedTrigger = "Death";
    public string deathArmedTrigger = "DeathArmed";
    public string getHitTrigger = "GetHit";
    public string getHit2Trigger = "GeiHit2";

    // Smooth inputs
    private float smoothedInputX = 0f;
    private float smoothedInputZ = 0f;
    private int fallbackWeaponIndex = 1;
    private RootMotionBridge rootMotionBridge;
    private SimplePlayerTest proxyPlayerTest;
    
    // Rigidbody component phục vụ tính toán vật lý, khắc phục lỗi đi xuyên
    private Rigidbody rb;

    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public float CurrentHealth =>
        isStandaloneMode ? localHealth : currentHealth.Value;

    protected RootMotionBridge GetRootMotionBridge()
    {
        if (rootMotionBridge == null && anim != null)
        {
            rootMotionBridge = anim.GetComponent<RootMotionBridge>();
        }
        return rootMotionBridge;
    }

    private void Awake()
    {
        rollKey = KeyCode.LeftControl;
        proxyPlayerTest = GetComponent<SimplePlayerTest>();
        
        characterClassIndex = 0; 
        maxHealth = 85f;
        moveSpeed = 3.5f;
        runSpeedMultiplier = 3.5f;
        damageAmount = 25f;
        attackRange = 2f;
        cameraOffset = new Vector3(0f, 10f, -6.5f); 
        cameraSensitivity = 3f;  
        cameraPivotHeight = 3.5f; 

        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        // Lấy Rigidbody và khóa các trục trục xoay tự do do va chạm vật lý đem lại
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate; // Giúp Player di chuyển mượt mà không bị khựng hình
        }

        // Tự động tạo và liên kết hitbox kiếm/tay nếu bị null
        CreateSwordHitboxes();
    }

    private void Start()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        // Khởi tạo weight của AttackLayer ở 0
        if (anim != null && anim.layerCount > 1)
        {
            anim.SetLayerWeight(1, 0f);
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

        // Khởi tạo hiển thị vũ khí ban đầu phù hợp với trạng thái Armed/Unarmed
        SyncWeaponVisuals(GetActiveWeaponIndex());
    }

    private void InitStandaloneMode()
    {
        Debug.Log("[LeoPlayer] Starting in STANDALONE mode. Local inputs active.");
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);
        localLevel = PlayerPrefs.GetInt("SelectedPlayerLevel_" + characterClassIndex, 0);
        localExp = PlayerPrefs.GetFloat("SelectedPlayerExp_" + characterClassIndex, 0f);

        PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
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
        isMovementLockedNet.OnValueChanged += OnMovementLockedNetChanged;

        if (IsOwner)
        {
            currentHealth.OnValueChanged += OnHealthChanged;
            UpdateHealthHUD(currentHealth.Value);

            PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
            if (hud != null)
                hud.SetupPlayerProfile(characterClassIndex);

            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindAnyObjectByType<Camera>();

            LoadPlayerStateFromDatabase();
            ApplyUpgradedStats();
            UpdateUpgradeHUD();
            UpdateDurabilityHUD();
        }

        // Đồng bộ hiển thị vũ khí ban đầu cho tất cả người chơi trên mạng
        SyncWeaponVisuals(activeWeaponIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
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
        isMovementLockedNet.OnValueChanged -= OnMovementLockedNetChanged;

        if (IsOwner)
            currentHealth.OnValueChanged -= OnHealthChanged;
    }

    // Helper functions to sync NetworkVariables on the Server to proxy component
    private void SyncNetVarFloat(NetworkVariable<float> myVar, NetworkVariable<float> proxyVar, float value)
    {
        if (IsServer)
        {
            myVar.Value = value;
            if (proxyVar != null) proxyVar.Value = value;
        }
    }

    private void SyncNetVarInt(NetworkVariable<int> myVar, NetworkVariable<int> proxyVar, int value)
    {
        if (IsServer)
        {
            myVar.Value = value;
            if (proxyVar != null) proxyVar.Value = value;
        }
    }

    private void SyncNetVarBool(NetworkVariable<bool> myVar, NetworkVariable<bool> proxyVar, bool value)
    {
        if (IsServer)
        {
            myVar.Value = value;
            if (proxyVar != null) proxyVar.Value = value;
        }
    }

    public int GetActiveWeaponIndex()
    {
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
            if (hud != null) return hud.currentSelectedWeapon;
            return fallbackWeaponIndex;
        }
        return activeWeaponIndex.Value;
    }

    public void OnRollEnd()
    {
        Debug.Log("[LeoPlayer] Roll ended via Animation Event.");
        isRollingStandalone = false;
        
        if (anim != null) anim.applyRootMotion = false; // Tắt applyRootMotion
        var bridge = GetRootMotionBridge();
        if (bridge != null) bridge.ApplyFinalOffset();
        
        if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); // Dừng lực lộn

        if (!isStandaloneMode && IsOwner)
        {
            StopRollServerRpc();
        }
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

    private void Update()
    {
        // Cập nhật Layer Weight động cho Attack Layer (Layer 1)
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

        if (rollCooldownTimer > 0)
        {
            rollCooldownTimer -= Time.deltaTime;
        }

        if (attackDashTimer > 0)
        {
            attackDashTimer -= Time.deltaTime;
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

        if (CurrentHealth <= 0)
        {
            if (anim != null) anim.applyRootMotion = false;
            if (rb != null) rb.linearVelocity = Vector3.zero; // Dừng lực khi chết
            PlayAnimation("Death", 0.15f);
            return;
        }

        // Standalone weapon switching when no HUD
        if (isStandaloneMode && FindAnyObjectByType<PlayerHUDController>() == null)
        {
            if (isSwitchingWeapon) return;
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

        if (isStandaloneMode)
        {
            HandleStandaloneUpdate();
            return;
        }

        if (!IsOwner) return;
        HandleOwnerUpdate();
    }

    private void HandleStandaloneUpdate()
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
                if (!useBlendTree) PlayAnimationLocal("Idle", 0.1f);
            }
            return; 
        }

        if (IsPlayingPickAnimation())
        {
            smoothedInputX = Mathf.MoveTowards(smoothedInputX, 0f, Time.deltaTime * inputFilterSpeed);
            smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, 0f, Time.deltaTime * inputFilterSpeed);
            UpdateAnimatorParams(0f);
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
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

        float moveX = isMovementLocked ? 0f : Input.GetAxis("Horizontal");
        float moveZ = isMovementLocked ? 0f : Input.GetAxis("Vertical");
        Vector3 move = new Vector3(moveX, 0, moveZ);

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindAnyObjectByType<Camera>();
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

        // --- ĐÃ ĐỔI THÀNH RIGIDBODY TÍNH VẬN TỐC THAY VÌ TRANSLATE ---
        if (rb != null)
        {
            Vector3 targetVelocity = movementTranslation * currentSpeed;
            float currentYVelocity = rb.linearVelocity.y;

            // Tính hợp lực Knockback trực tiếp vào vận tốc Rigidbody tại đây
            if (knockbackVelocity.magnitude > 0.01f)
            {
                targetVelocity += knockbackVelocity;
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
            }

            // Ưu tiên lực Dash đòn đánh
            if (attackDashTimer > 0)
            {
                rb.linearVelocity = new Vector3(attackDashDirection.x * attackDashSpeed, currentYVelocity, attackDashDirection.z * attackDashSpeed);
            }
            // Nếu khóa di chuyển, đứng im
            else if (isMovementLocked && knockbackVelocity.magnitude <= 0.01f)
            {
                rb.linearVelocity = new Vector3(0f, currentYVelocity, 0f);
            }
            else
            {
                rb.linearVelocity = new Vector3(targetVelocity.x, currentYVelocity, targetVelocity.z);
            }
        }

        bool isArmed = (GetActiveWeaponIndex() == 2);
        bool isMoving = (movementTranslation != Vector3.zero);

        float targetInputX = 0f;
        float targetInputZ = 0f;
        float targetSpeed = 0f;

        bool shouldAlignToCamera = (isArmed && rotateToCameraWhenArmed) || (!isArmed && rotateToCameraWhenUnarmed);

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
                PlayAnimationLocal(moveAnim, 0.1f);
            }
            else
            {
                PlayAnimationLocal("Idle", 0.1f);
            }
        }

        // Action Inputs with click attacking vs roll blocking
        if (Input.GetMouseButtonDown(0))
        {
            if (!IsUIBlockingInput() && !isRollingStandalone && !IsPlayingActionAnimation())
            {
                Debug.Log($"[LeoPlayer] Mouse clicked in Standalone. Weapon: {GetActiveWeaponIndex()}");
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

    private void HandleOwnerUpdate()
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
                if (!useBlendTree) PlayAnimationLocal("Idle", 0.1f);
            }
            return; 
        }

        if (IsPlayingPickAnimation())
        {
            smoothedInputX = Mathf.MoveTowards(smoothedInputX, 0f, Time.deltaTime * inputFilterSpeed);
            smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, 0f, Time.deltaTime * inputFilterSpeed);
            UpdateAnimatorParams(0f);
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
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

        float moveX = isMovementLocked ? 0f : Input.GetAxis("Horizontal");
        float moveZ = isMovementLocked ? 0f : Input.GetAxis("Vertical");
        Vector3 move = new Vector3(moveX, 0, moveZ);

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindAnyObjectByType<Camera>();
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

        // --- ĐÃ ĐỔI THÀNH RIGIDBODY TÍNH VẬN TỐC CHO ĐỒNG BỘ MULTIPLAYER ---
        if (rb != null)
        {
            Vector3 targetVelocity = movementTranslation * currentSpeed;
            float currentYVelocity = rb.linearVelocity.y;

            if (knockbackVelocity.magnitude > 0.01f)
            {
                targetVelocity += knockbackVelocity;
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
            }

            // Ưu tiên lực Dash đòn đánh
            if (attackDashTimer > 0)
            {
                rb.linearVelocity = new Vector3(attackDashDirection.x * attackDashSpeed, currentYVelocity, attackDashDirection.z * attackDashSpeed);
            }
            // Nếu khóa di chuyển, đứng im
            else if (isMovementLocked && knockbackVelocity.magnitude <= 0.01f)
            {
                rb.linearVelocity = new Vector3(0f, currentYVelocity, 0f);
            }
            else
            {
                rb.linearVelocity = new Vector3(targetVelocity.x, currentYVelocity, targetVelocity.z);
            }
        }

        bool isArmed = (GetActiveWeaponIndex() == 2);
        bool isMoving = (movementTranslation != Vector3.zero);

        float targetInputX = 0f;
        float targetInputZ = 0f;
        float targetSpeed = 0f;

        bool shouldAlignToCamera = (isArmed && rotateToCameraWhenArmed) || (!isArmed && rotateToCameraWhenUnarmed);

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
                PlayAnimationLocal(moveAnim, 0.1f);
            }
            else
            {
                PlayAnimationLocal("Idle", 0.1f);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!IsUIBlockingInput() && IsSpawned)
            {
                Debug.Log($"[LeoPlayer] Mouse clicked in Owner mode. Weapon: {GetActiveWeaponIndex()}");
                PerformComboAttack(true);
            }
        }

        if (Input.GetKeyDown(rollKey))
        {
            if (!IsUIBlockingInput() && rollCooldownTimer <= 0 && IsSpawned && !IsPlayingAttackState(out _, out _))
            {
                StartRollOwner(move);
            }
        }
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
            if (step == 1) return slash1Duration;
            if (step == 2) return slash2Duration;
            return slash3Duration;
        }
        return 0.5f;
    }

    protected void PerformComboAttack(bool networkMode)
    {
        int weapon = GetActiveWeaponIndex();
        float currentTime = Time.time;
        
        // Reset hitbox states and clear target list for the new swing
        alreadyHitEnemies.Clear();
        DisableLeftHitbox();
        DisableRightHitbox();
        DisableLeftWeaponHitbox();
        DisableRightWeaponHitbox();

        if (weapon == 2)
        {
            // Armed sequential 3-step combo
            int nextStep = comboStep;
            if (currentTime - lastAttackTime > comboWindow)
            {
                nextStep = 0;
            }
            nextStep++;

            if (nextStep > 3) nextStep = 1;

            if (comboStep > 0 && currentTime - lastAttackTime <= comboWindow)
            {
                float prevDuration = GetAttackDuration(2, comboStep);
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

            string animToPlay = "Slash1";
            if (comboStep == 2) animToPlay = "Slash2";
            else if (comboStep == 3) animToPlay = "Slash3";
            
            Debug.Log($"[LeoPlayer] Sword attack (Armed). Step: {comboStep} -> Playing: {animToPlay}");

            if (anim != null) anim.applyRootMotion = false;

            // Kích hoạt Dash tiến lên bằng C# cho cả 3 đòn chém (Chỉ khi đứng im chém)
            if (isRootedAttack)
            {
                attackDashTimer = attackDashDuration;
                attackDashDirection = transform.forward;
                Debug.Log($"[LeoPlayer] Triggered slash {comboStep} forward dash by code.");
            }
            
            PlayAnimation(animToPlay, 0.05f, false);
            // Damage is now processed through animation events enabling left/right hitboxes
        }
        else
        {
            // Unarmed combo Sequential punches - 3 steps
            int nextStep = comboStep;
            if (currentTime - lastAttackTime > comboWindow)
            {
                nextStep = 0;
            }
            nextStep++;

            if (nextStep > 3) nextStep = 1;

            if (comboStep > 0 && currentTime - lastAttackTime <= comboWindow)
            {
                float prevDuration = GetAttackDuration(1, comboStep);
                if (currentTime - lastAttackTime < prevDuration * comboTransitionThreshold)
                {
                    return;
                }
            }

            // Đấm thì luôn đứng im hoàn toàn (rooted)
            isRootedAttack = true;

            comboStep = nextStep;
            lastAttackTime = currentTime;

            // Khóa di chuyển khi đấm tay không ngay lập tức
            SetMovementLock(true);
            Debug.Log("[LeoPlayer] Punch attack started: Locking movement immediately.");

            // ĐÃ XÓA lực dash ở đòn đấm combo thứ 3 để đảm bảo đứng im hoàn toàn không di chuyển box

            string animToPlay = "Punch1";
            if (comboStep == 2) animToPlay = "Punch2";
            else if (comboStep == 3) animToPlay = "Punch3";

            Debug.Log($"[LeoPlayer] Fist attack (Unarmed). Step: {comboStep} -> Playing: {animToPlay}");
            
            PlayAnimation(animToPlay, 0.05f, false);
            // Damage is now processed through animation events enabling left/right hitboxes
        }
    }

    private void TryDamageEnemy(Collider col)
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
        if (Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange))
        {
            var e1 = hit.collider.GetComponentInParent<Enemy1_DapBua>();
            if (e1 != null) { e1.TakeDamage(damageAmount); return; }

            var e2 = hit.collider.GetComponentInParent<Enemy2_Zombie>();
            if (e2 != null) { e2.TakeDamage(damageAmount); return; }

            var e3 = hit.collider.GetComponentInParent<Enemy3_Buaa>();
            if (e3 != null) { e3.TakeDamage(damageAmount); return; }

            var e4 = hit.collider.GetComponentInParent<Enemy4_Bongtoi>();
            if (e4 != null) { e4.TakeDamage(damageAmount); return; }

            var e5 = hit.collider.GetComponentInParent<Enemy5_PhuThuy>();
            if (e5 != null) { e5.TakeDamage(damageAmount); return; }
        }
    }

    protected void StartRollStandalone(Vector3 moveInput)
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

    protected void StartRollOwner(Vector3 moveInput)
    {
        isRollingStandalone = true; // Kích hoạt di chuyển tức thời trên client chủ sở hữu (Client-side Prediction)
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

    [ServerRpc]
    private void StartRollServerRpc(Vector3 direction)
    {
        SyncNetVarBool(isRollingNet, proxyPlayerTest != null ? proxyPlayerTest.isRollingNet : null, true);
        PlayAnimationClientRpc("LonVong", 0.05f, true);
    }

    [ServerRpc]
    protected void StopRollServerRpc()
    {
        SyncNetVarBool(isRollingNet, proxyPlayerTest != null ? proxyPlayerTest.isRollingNet : null, false);
    }

    private void LateUpdate()
    {
        bool shouldFollow = isStandaloneMode || (IsSpawned && IsOwner);
        if (!shouldFollow || !enableCameraFollow) return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindAnyObjectByType<Camera>();
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

    // Health, DB, stats and durability
    public float Weapon1Durability
    {
        get { return isStandaloneMode ? localWeapon1Durability : weapon1Durability.Value; }
        set {
            float clamped = Mathf.Clamp(value, 0f, weapon1MaxDurability);
            if (isStandaloneMode)
            {
                localWeapon1Durability = clamped;
                UpdateDurabilityHUD();
            }
            else if (IsServer)
            {
                SyncNetVarFloat(weapon1Durability, proxyPlayerTest != null ? proxyPlayerTest.weapon1Durability : null, clamped);
            }
        }
    }

    public float Weapon2Durability
    {
        get { return isStandaloneMode ? localWeapon2Durability : weapon2Durability.Value; }
        set {
            float clamped = Mathf.Clamp(value, 0f, weapon2MaxDurability);
            if (isStandaloneMode)
            {
                localWeapon2Durability = clamped;
                UpdateDurabilityHUD();
            }
            else if (IsServer)
            {
                SyncNetVarFloat(weapon2Durability, proxyPlayerTest != null ? proxyPlayerTest.weapon2Durability : null, clamped);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        bool isRolling = isStandaloneMode ? isRollingStandalone : isRollingNet.Value;
        if (isRolling)
        {
            Debug.Log($"[LeoPlayer] {gameObject.name} is dodging/rolling, immune to damage!");
            return;
        }

        SetMovementLock(false); // Giải phóng khóa di chuyển khi bị trúng đòn

        if (isStandaloneMode)
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            UpdateHealthHUD(localHealth);
            Debug.Log($"[LeoPlayer Standalone] Recieved {damage} DMG. Health: {localHealth}");
            if (localHealth <= 0)
            {
                if (rb != null) rb.linearVelocity = Vector3.zero;
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

        float finalHp = Mathf.Max(currentHealth.Value - damage, 0f);
        SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, finalHp);
        Debug.Log($"[LeoPlayer Server] Client {OwnerClientId} recieved {damage} DMG. Health: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
        {
            if (rb != null) rb.linearVelocity = Vector3.zero;
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

    public bool TryAddItem(string itemName, bool playAnimation = true)
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
                    if (hud != null) hud.SetInventorySlots(inventorySlots);
                    if (!isStandaloneMode) SavePlayerStateToDatabase();
                    if (playAnimation) PlayAnimation("Idle_Pick", 0.1f);
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
                if (hud != null) hud.SetInventorySlots(inventorySlots);
                if (!isStandaloneMode) SavePlayerStateToDatabase();
                if (playAnimation) PlayAnimation("Idle_Pick", 0.1f);
                return true;
            }
        }
        return false;
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
            }
            PlayerPrefs.SetInt("SelectedPlayerLevel_" + characterClassIndex, localLevel);
            PlayerPrefs.SetFloat("SelectedPlayerExp_" + characterClassIndex, localExp);
            PlayerPrefs.Save();
            UpdateUpgradeHUD();
        }
        else if (IsServer)
        {
            float finalExp = playerExp.Value + amount;
            int finalLevel = playerLevel.Value;
            int finalPoints = upgradePoints.Value;
            float needed = 100f + finalLevel * 50f;

            while (finalExp >= needed)
            {
                finalExp -= needed;
                finalLevel++;
                needed = 100f + finalLevel * 50f;
                finalPoints++;
            }

            SyncNetVarFloat(playerExp, proxyPlayerTest != null ? proxyPlayerTest.playerExp : null, finalExp);
            SyncNetVarInt(playerLevel, proxyPlayerTest != null ? proxyPlayerTest.playerLevel : null, finalLevel);
            SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, finalPoints);
            SavePlayerStateClientRpc();
        }
    }

    public void StandaloneUpgradeStat(int statType)
    {
        if (localUpgradePoints <= 0) return;
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
        if (upgradePoints.Value <= 0) return;
        
        int pts = upgradePoints.Value - 1;
        SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, pts);

        switch (statType)
        {
            case 0: SyncNetVarInt(hpLevel, proxyPlayerTest != null ? proxyPlayerTest.hpLevel : null, hpLevel.Value + 1); break;
            case 1: SyncNetVarInt(mpLevel, proxyPlayerTest != null ? proxyPlayerTest.mpLevel : null, mpLevel.Value + 1); break;
            case 2: SyncNetVarInt(cooldownLevel, proxyPlayerTest != null ? proxyPlayerTest.cooldownLevel : null, cooldownLevel.Value + 1); break;
            case 3: SyncNetVarInt(damageLevel, proxyPlayerTest != null ? proxyPlayerTest.damageLevel : null, damageLevel.Value + 1); break;
        }

        ApplyUpgradedStats();
        SavePlayerStateClientRpc();
    }

    private void ApplyUpgradedStats()
    {
        if (isSyncingFromDb) return;

        int hpLv = isStandaloneMode ? localHpLevel : hpLevel.Value;
        int dmgLv = isStandaloneMode ? localDamageLevel : damageLevel.Value;

        float oldMaxHealth = maxHealth;
        maxHealth = 85f + hpLv * 20f;
        damageAmount = 25f + dmgLv * 5f;

        if (isStandaloneMode)
        {
            if (maxHealth > oldMaxHealth) localHealth += (maxHealth - oldMaxHealth);
            UpdateHealthHUD(localHealth);
        }
        else if (IsServer)
        {
            if (maxHealth > oldMaxHealth)
            {
                float finalHp = currentHealth.Value + (maxHealth - oldMaxHealth);
                SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, finalHp);
            }
        }
    }

    private void UpdateUpgradeHUD()
    {
        PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
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

    public void UpdateDurabilityHUD()
    {
        PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
        if (hud != null)
        {
            float percent1 = (isStandaloneMode ? localWeapon1Durability : weapon1Durability.Value) / weapon1MaxDurability;
            float percent2 = (isStandaloneMode ? localWeapon2Durability : weapon2Durability.Value) / weapon2MaxDurability;
            hud.SetWeaponDurability(1, percent1);
            hud.SetWeaponDurability(2, percent2);
        }
    }

    public void UpdateStateFromHUD(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
    {
        if (!IsSpawned || !IsOwner) return;
        UpdateStateServerRpc(weaponIndex, weapon2Locked, skillsUnlocked);
    }

    [ServerRpc]
    private void UpdateStateServerRpc(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
    {
        SyncNetVarInt(activeWeaponIndex, proxyPlayerTest != null ? proxyPlayerTest.activeWeaponIndex : null, weaponIndex);
        SyncNetVarBool(isWeapon2Locked, proxyPlayerTest != null ? proxyPlayerTest.isWeapon2Locked : null, weapon2Locked);
        SyncNetVarBool(isSkillsUnlocked, proxyPlayerTest != null ? proxyPlayerTest.isSkillsUnlocked : null, skillsUnlocked);
        SavePlayerStateClientRpc();
    }

    public async void SavePlayerStateToDatabase()
    {
        if (!IsSpawned || !IsOwner) return;

        Debug.Log("[LeoPlayer DB] Saving character state to MongoDB...");
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
                Debug.Log("[LeoPlayer DB] Saved state to MongoDB successfully!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LeoPlayer DB] Error saving MongoDB state: {ex.Message}");
        }
    }

    private async void LoadPlayerStateFromDatabase()
    {
        Debug.Log("[LeoPlayer DB] Loading player state from MongoDB Atlas...");
        try
        {
            var res = await AuthService.GetPlayerState();
            if (res != null && res.success && res.playerState != null)
            {
                var state = res.playerState;
                isSyncingFromDb = true;

                SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, state.upgradePoints);
                SyncNetVarInt(hpLevel, proxyPlayerTest != null ? proxyPlayerTest.hpLevel : null, state.hpLevel);
                SyncNetVarInt(mpLevel, proxyPlayerTest != null ? proxyPlayerTest.mpLevel : null, state.mpLevel);
                SyncNetVarInt(cooldownLevel, proxyPlayerTest != null ? proxyPlayerTest.cooldownLevel : null, state.cooldownLevel);
                SyncNetVarInt(damageLevel, proxyPlayerTest != null ? proxyPlayerTest.damageLevel : null, state.damageLevel);
                SyncNetVarInt(playerLevel, proxyPlayerTest != null ? proxyPlayerTest.playerLevel : null, state.playerLevel);
                SyncNetVarFloat(playerExp, proxyPlayerTest != null ? proxyPlayerTest.playerExp : null, state.playerExp);

                maxHealth = 85f + state.hpLevel * 20f;
                damageAmount = 25f + state.damageLevel * 5f;

                SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, state.health);
                SyncNetVarInt(activeWeaponIndex, proxyPlayerTest != null ? proxyPlayerTest.activeWeaponIndex : null, state.activeWeaponIndex);
                SyncNetVarBool(isWeapon2Locked, proxyPlayerTest != null ? proxyPlayerTest.isWeapon2Locked : null, true);
                SyncNetVarBool(isSkillsUnlocked, proxyPlayerTest != null ? proxyPlayerTest.isSkillsUnlocked : null, false);

                isSyncingFromDb = false;

                if (state.inventorySlots != null)
                {
                    for (int i = 0; i < inventorySlots.Length && i < state.inventorySlots.Length; i++)
                    {
                        inventorySlots[i] = state.inventorySlots[i];
                    }
                }

                PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
                if (hud != null)
                {
                    hud.SetInventorySlots(inventorySlots);
                    hud.SetSkillsUnlocked(false, false);
                    hud.SetWeapon2Locked(true, false);
                    hud.SelectWeapon(state.activeWeaponIndex);
                    hud.UpdateUpgradeUI(state.upgradePoints, state.hpLevel, state.mpLevel, state.cooldownLevel, state.damageLevel);
                    hud.SetHealth(state.health / (85f + state.hpLevel * 20f));
                    
                    float needed = 100f + state.playerLevel * 50f;
                    hud.UpdateExperienceUI(state.playerLevel, state.playerExp, needed);
                }
            }
            else
            {
                SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, maxHealth);
                SyncNetVarInt(activeWeaponIndex, proxyPlayerTest != null ? proxyPlayerTest.activeWeaponIndex : null, 1);
                SyncNetVarBool(isWeapon2Locked, proxyPlayerTest != null ? proxyPlayerTest.isWeapon2Locked : null, true);
                SyncNetVarBool(isSkillsUnlocked, proxyPlayerTest != null ? proxyPlayerTest.isSkillsUnlocked : null, false);
                SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, 5);
                SyncNetVarInt(hpLevel, proxyPlayerTest != null ? proxyPlayerTest.hpLevel : null, 0);
                SyncNetVarInt(mpLevel, proxyPlayerTest != null ? proxyPlayerTest.mpLevel : null, 0);
                SyncNetVarInt(cooldownLevel, proxyPlayerTest != null ? proxyPlayerTest.cooldownLevel : null, 0);
                SyncNetVarInt(damageLevel, proxyPlayerTest != null ? proxyPlayerTest.damageLevel : null, 0);
                SyncNetVarInt(playerLevel, proxyPlayerTest != null ? proxyPlayerTest.playerLevel : null, 0);
                SyncNetVarFloat(playerExp, proxyPlayerTest != null ? proxyPlayerTest.playerExp : null, 0f);
                SavePlayerStateToDatabase();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LeoPlayer DB] Error loading state: {ex.Message}");
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

    // Callbacks for local HUD state changes when NetworkVariables sync from Server to Owner Client
    private void OnWeaponIndexChanged(int oldVal, int newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
            if (hud != null) hud.SelectWeapon(newVal);
        }
        PlayWeaponSwitchAnimation(oldVal, newVal);
    }

    private void OnWeapon2LockedChanged(bool oldVal, bool newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
            if (hud != null) hud.SetWeapon2Locked(newVal, true);
        }
    }

    private void OnSkillsUnlockedChanged(bool oldVal, bool newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
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
        PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
        if (hud != null)
            hud.SetHealth(health / maxHealth);
    }

    private void OnUpgradePointsChanged(int oldVal, int newVal)
    {
        if (IsOwner) UpdateUpgradeHUD();
    }

    private void OnHpLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner) ApplyUpgradedStats();
        if (IsOwner) UpdateUpgradeHUD();
    }

    private void OnMpLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner) ApplyUpgradedStats();
        if (IsOwner) UpdateUpgradeHUD();
    }

    private void OnCooldownLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner) ApplyUpgradedStats();
        if (IsOwner) UpdateUpgradeHUD();
    }

    private void OnDamageLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner) ApplyUpgradedStats();
        if (IsOwner) UpdateUpgradeHUD();
    }

    private void OnLevelOrExpChanged(int oldVal, int newVal)
    {
        if (IsOwner) UpdateUpgradeHUD();
    }

    private void OnLevelOrExpChanged(float oldVal, float newVal)
    {
        if (IsOwner) UpdateUpgradeHUD();
    }

    private void OnDurabilityChanged(float oldVal, float newVal)
    {
        if (IsOwner) UpdateDurabilityHUD();
    }

    // Animation Management
    public void PlayWeaponSwitchAnimation(int oldWeapon, int newWeapon)
    {
        if (oldWeapon == newWeapon) return;

        isSwitchingWeapon = true; // Khóa chống spam phím khi đổi vũ khí

        if (newWeapon == 2)
        {
            // Bắt đầu rút kiếm: lúc này kiếm vẫn ở trên vai, tay chưa cầm kiếm
            if (leftShoulderSword != null) leftShoulderSword.SetActive(true);
            if (rightShoulderSword != null) rightShoulderSword.SetActive(true);
            if (leftHandSword != null) leftHandSword.SetActive(false);
            if (rightHandSword != null) rightHandSword.SetActive(false);

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
            // Bắt đầu cất kiếm: lúc này kiếm vẫn ở trên tay, chưa cất lên vai
            if (leftHandSword != null) leftHandSword.SetActive(true);
            if (rightHandSword != null) rightHandSword.SetActive(true);
            if (leftShoulderSword != null) leftShoulderSword.SetActive(false);
            if (rightShoulderSword != null) rightShoulderSword.SetActive(false);

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
        // Để Animator tự động chuyển sang hoạt ảnh tay phải bằng mũi tên transition (Has Exit Time)
        Debug.Log("[LeoPlayer] Draw Left finished. Letting Animator transition natively to Draw Right.");
    }

    public void OnSheatheLeftEnd()
    {
        // Để Animator tự động chuyển sang hoạt ảnh tay phải bằng mũi tên transition (Has Exit Time)
        Debug.Log("[LeoPlayer] Sheathe Left finished. Letting Animator transition natively to Sheathe Right.");
    }

    public void DrawLeftSword()
    {
        if (leftHandSword != null) leftHandSword.SetActive(true);
        if (leftShoulderSword != null) leftShoulderSword.SetActive(false);
        Debug.Log("[LeoPlayer] Left sword DRAWN.");
    }

    public void DrawRightSword()
    {
        if (rightHandSword != null) rightHandSword.SetActive(true);
        if (rightShoulderSword != null) rightShoulderSword.SetActive(false);
        Debug.Log("[LeoPlayer] Right sword DRAWN.");
    }

    public void SheatheLeftSword()
    {
        if (leftHandSword != null) leftHandSword.SetActive(false);
        if (leftShoulderSword != null) leftShoulderSword.SetActive(true);
        Debug.Log("[LeoPlayer] Left sword SHEATHED.");
    }

    public void SheatheRightSword()
    {
        if (rightHandSword != null) rightHandSword.SetActive(false);
        if (rightShoulderSword != null) rightShoulderSword.SetActive(true);
        Debug.Log("[LeoPlayer] Right sword SHEATHED.");
    }

    public void SyncWeaponVisuals(int activeWeapon)
    {
        bool isArmed = (activeWeapon == 2);
        
        if (leftHandSword != null) leftHandSword.SetActive(isArmed);
        if (rightHandSword != null) rightHandSword.SetActive(isArmed);
        
        if (leftShoulderSword != null) leftShoulderSword.SetActive(!isArmed);
        if (rightShoulderSword != null) rightShoulderSword.SetActive(!isArmed);
    }

    public void OnWeaponSwitchEnd()
    {
        isSwitchingWeapon = false;
        Debug.Log("[LeoPlayer] Weapon switch animation finished. Lock released.");
    }

    [ContextMenu("Auto Find Sword Meshes")]
    public void AutoFindSwordMeshes()
    {
        // 1. Tìm trên tay
        Transform leftHand = FindBoneRecursive(transform, "left");
        Transform rightHand = FindBoneRecursive(transform, "right");
        
        if (leftHand != null)
        {
            Transform leftSword = FindWeaponTransform(leftHand);
            if (leftSword != null) leftHandSword = leftSword.gameObject;
        }
        if (rightHand != null)
        {
            Transform rightSword = FindWeaponTransform(rightHand);
            if (rightSword != null) rightHandSword = rightSword.gameObject;
        }
        
        // 2. Tìm trên vai (Spine, Chest, Shoulder)
        Transform spine = FindBoneRecursiveLower(transform, "spine");
        Transform chest = FindBoneRecursiveLower(transform, "chest");
        Transform upperBody = chest ?? spine ?? transform;
        
        Transform leftSh = FindShoulderSwordRecursive(upperBody, "left");
        Transform rightSh = FindShoulderSwordRecursive(upperBody, "right");
        
        if (leftSh != null) leftShoulderSword = leftSh.gameObject;
        if (rightSh != null) rightShoulderSword = rightSh.gameObject;
        
        Debug.Log($"[LeoPlayer Editor] Auto Find Results -> Hand L: {leftHandSword?.name}, Hand R: {rightHandSword?.name}, Shoulder L: {leftShoulderSword?.name}, Shoulder R: {rightShoulderSword?.name}");
    }

    private Transform FindBoneRecursiveLower(Transform current, string keyword)
    {
        string nameLower = current.name.ToLower();
        if (nameLower.Contains(keyword))
        {
            return current;
        }
        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindBoneRecursiveLower(current.GetChild(i), keyword);
            if (found != null) return found;
        }
        return null;
    }

    private Transform FindShoulderSwordRecursive(Transform current, string side)
    {
        string nameLower = current.name.ToLower();
        if ((nameLower.Contains("sword") || nameLower.Contains("blade") || nameLower.Contains("kiem") || nameLower.Contains("dao") || nameLower.Contains("katana") || nameLower.Contains("weapon")) && 
            nameLower.Contains(side) && 
            (nameLower.Contains("shoulder") || nameLower.Contains("back") || nameLower.Contains("sheath") || nameLower.Contains("mount") || nameLower.Contains("holder") || nameLower.Contains("holster")))
        {
            return current;
        }
        if (nameLower.Contains(side) && (nameLower.Contains("sheath") || nameLower.Contains("mount") || nameLower.Contains("holder") || nameLower.Contains("holster")))
        {
            return current;
        }
        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindShoulderSwordRecursive(current.GetChild(i), side);
            if (found != null) return found;
        }
        return null;
    }

    public void PlayAnimation(string animName, float fadeTime = 0.1f, bool alreadyPlayedLocally = false)
    {
        if (anim == null) return;
        
        if (!alreadyPlayedLocally)
        {
            PlayAnimationLocal(animName, fadeTime);
        }

        if (!isStandaloneMode)
        {
            if (IsServer)
            {
                PlayAnimationClientRpc(animName, fadeTime, alreadyPlayedLocally);
            }
            else if (IsOwner)
            {
                PlayAnimationServerRpc(animName, fadeTime);
            }
        }
    }

    private string TranslateAnimName(string animName)
    {
        int weapon = GetActiveWeaponIndex();
        bool isArmed = (weapon == 2);

        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");
        
        bool isMovingBackward = (verticalInput < -0.1f);
        bool isMovingLeft = (horizontalInput < -0.1f);
        bool isMovingRight = (horizontalInput > 0.1f);

        switch (animName)
        {
            case "Idle":
                return isArmed ? idleArmed : idleUnarmed;
            case "Walk":
                if (isArmed)
                {
                    if (isMovingBackward) return walkBackwardArmed;
                    if (isMovingLeft) return walkLeftArmed;
                    if (isMovingRight) return walkRightArmed;
                    return walkForwardArmed;
                }
                else
                {
                    if (isMovingBackward) return walkBackwardUnarmed;
                    if (isMovingLeft) return walkLeftUnarmed;
                    if (isMovingRight) return walkRightUnarmed;
                    return walkUnarmed;
                }
            case "run":
                if (isArmed)
                {
                    if (isMovingBackward) return runBackwardArmed;
                    if (isMovingLeft) return runLeftArmed;
                    if (isMovingRight) return runRightArmed;
                    return runForwardArmed;
                }
                else
                {
                    if (isMovingBackward) return runBackwardUnarmed;
                    if (isMovingLeft) return runLeftUnarmed;
                    if (isMovingRight) return runRightUnarmed;
                    return runUnarmed;
                }
            case "LonVong":
                return rollTrigger;
            case "Punch1":
                return punch1Trigger;
            case "Punch2":
                return punch2Trigger;
            case "Punch3":
                return punch3Trigger;
            case "Slash1":
                return slash1Trigger;
            case "Slash2":
                return slash2Trigger;
            case "GetHit":
                return getHitTrigger;
            case "GeiHit2":
                return getHit2Trigger;
            case "Death":
                return isArmed ? deathArmedTrigger : deathUnarmedTrigger;
            case "Idle_Pick":
            case "Pick":
                return pickTrigger;
            default:
                return animName;
        }
    }

    private bool IsActionAnimationName(string name)
    {
        return name == rollTrigger || 
               name == getHitTrigger || 
               name == getHit2Trigger || 
               name == pickTrigger || 
               name == deathUnarmedTrigger ||
               name == deathArmedTrigger ||
               name == punch1Trigger ||
               name == punch2Trigger ||
               name == punch3Trigger ||
               name == slash1Trigger ||
               name == slash2Trigger ||
               name == "LonVong" ||
               name == "GetHit" ||
               name == "GeiHit2" ||
               name == "Idle_Pick" ||
               name == "Death" ||
               name == "Punch1" ||
               name == "Punch2" ||
               name == "Punch3" ||
               name == "Slash1" ||
               name == "Slash2" ||
               (!string.IsNullOrEmpty(drawWeaponTrigger) && name == drawWeaponTrigger) ||
               (!string.IsNullOrEmpty(sheathWeaponTrigger) && name == sheathWeaponTrigger) ||
               (!string.IsNullOrEmpty(drawLeftTrigger) && name == drawLeftTrigger) ||
               (!string.IsNullOrEmpty(drawRightTrigger) && name == drawRightTrigger) ||
               (!string.IsNullOrEmpty(sheatheLeftTrigger) && name == sheatheLeftTrigger) ||
               (!string.IsNullOrEmpty(sheatheRightTrigger) && name == sheatheRightTrigger);
    }

    private bool IsAttackAnimationName(string name)
    {
        return name == punch1Trigger || 
               name == punch2Trigger || 
               name == punch3Trigger ||
               name == slash1Trigger || 
               name == slash2Trigger ||
               name == "Punch1" ||
               name == "Punch2" ||
               name == "Punch3" ||
               name == "Slash1" ||
               name == "Slash2";
    }

    private bool IsFullBodyActionAnimation(string name)
    {
        return name == rollTrigger || 
               name == getHitTrigger || 
               name == getHit2Trigger || 
               name == pickTrigger || 
               name == deathUnarmedTrigger ||
               name == deathArmedTrigger ||
               name == "LonVong" ||
               name == "GetHit" ||
               name == "GeiHit2" ||
               name == "Idle_Pick" ||
               name == "Death";
    }

    private bool IsPlayingAttackState(out AnimatorStateInfo activeState, out int layer)
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

    private bool IsAttackState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName(punch1Trigger) || 
               stateInfo.IsName(punch2Trigger) || 
               stateInfo.IsName(punch3Trigger) || 
               stateInfo.IsName(slash1Trigger) || 
               stateInfo.IsName(slash2Trigger) ||
               stateInfo.IsName("Punch1") ||
               stateInfo.IsName("Punch2") ||
               stateInfo.IsName("Punch3") ||
               stateInfo.IsName("Slash1") ||
               stateInfo.IsName("Slash2");
    }

    private bool IsPlayingActionAnimation()
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

        if (IsFullBodyActionAnimation(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.15f) return true;
        
        if (isRootedAttack && IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.15f) return true;

        if (isStandaloneMode ? isRollingStandalone : rollTimer > 0) return true;

        if (isRootedAttack && IsPlayingAttackState(out _, out _))
        {
            return true;
        }

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool isFullBodyAction = stateInfo.IsName(rollTrigger) || 
                               stateInfo.IsName(getHitTrigger) || 
                               stateInfo.IsName(getHit2Trigger) || 
                               stateInfo.IsName(pickTrigger) || 
                               stateInfo.IsName(deathUnarmedTrigger) ||
                               stateInfo.IsName(deathArmedTrigger) ||
                               stateInfo.IsName("LonVong") || 
                               stateInfo.IsName("GetHit") || 
                               stateInfo.IsName("GeiHit2") || 
                               stateInfo.IsName("Idle_Pick") || 
                               stateInfo.IsName("Death");

        return isFullBodyAction && stateInfo.normalizedTime < 0.95f;
    }

    private bool IsPlayingPickAnimation()
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

        if ((lastTriggeredAnimName == pickTrigger || lastTriggeredAnimName == "Idle_Pick" || lastTriggeredAnimName == "Pick") 
            && Time.time - lastActionTriggerTime < 0.15f)
        {
            return true;
        }

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool isInPickState = stateInfo.IsName(pickTrigger) || stateInfo.IsName("Idle_Pick") || stateInfo.IsName("Pick");
        return isInPickState && stateInfo.normalizedTime < 0.95f;
    }

    private float lastActionTriggerTime = 0f;
    private string lastTriggeredAnimName = "";

    private void PlayAnimationLocal(string animName, float fadeTime)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;

        if (useBlendTree && (animName == "Idle" || animName == "Walk" || animName == "run"))
        {
            return;
        }

        string translatedName = TranslateAnimName(animName);

        bool isLoopingAnim = translatedName == idleUnarmed || translatedName == walkUnarmed || translatedName == runUnarmed ||
                             translatedName == idleArmed || translatedName == walkForwardArmed || translatedName == walkBackwardArmed ||
                             translatedName == runForwardArmed || translatedName == runBackwardArmed || translatedName == walkBackwardUnarmed ||
                             translatedName == walkLeftUnarmed || translatedName == walkRightUnarmed || translatedName == runLeftUnarmed ||
                             translatedName == runRightUnarmed || translatedName == walkLeftArmed || translatedName == walkRightArmed ||
                             translatedName == runLeftArmed || translatedName == runRightArmed;
        if (isLoopingAnim && currentAnimState == translatedName) return;

        Debug.Log($"[LeoPlayer Animator] Triggering Anim: '{translatedName}' (source: '{animName}')");

        anim.ResetTrigger(idleUnarmed);
        anim.ResetTrigger(walkUnarmed);
        anim.ResetTrigger(runUnarmed);
        anim.ResetTrigger(walkBackwardUnarmed);
        anim.ResetTrigger(runBackwardUnarmed);
        anim.ResetTrigger(walkLeftUnarmed);
        anim.ResetTrigger(walkRightUnarmed);
        anim.ResetTrigger(runLeftUnarmed);
        anim.ResetTrigger(runRightUnarmed);

        anim.ResetTrigger(idleArmed);
        anim.ResetTrigger(walkForwardArmed);
        anim.ResetTrigger(walkBackwardArmed);
        anim.ResetTrigger(runForwardArmed);
        anim.ResetTrigger(runBackwardArmed);
        anim.ResetTrigger(walkLeftArmed);
        anim.ResetTrigger(walkRightArmed);
        anim.ResetTrigger(runLeftArmed);
        anim.ResetTrigger(runRightArmed);

        anim.ResetTrigger(deathUnarmedTrigger);
        anim.ResetTrigger(deathArmedTrigger);
        anim.ResetTrigger(getHitTrigger);
        anim.ResetTrigger(getHit2Trigger);
        anim.ResetTrigger(rollTrigger);
        anim.ResetTrigger(pickTrigger);

        anim.ResetTrigger("Idle");
        anim.ResetTrigger("Walk");
        anim.ResetTrigger("run");
        anim.ResetTrigger("Death");
        anim.ResetTrigger("GetHit");
        anim.ResetTrigger("GeiHit2");
        anim.ResetTrigger("LonVong");
        anim.ResetTrigger("Idle_Pick");

        if (IsActionAnimationName(translatedName))
        {
            anim.ResetTrigger(punch1Trigger);
            anim.ResetTrigger(punch2Trigger);
            anim.ResetTrigger(punch3Trigger);
            anim.ResetTrigger(slash1Trigger);
            anim.ResetTrigger(slash2Trigger);
            
            anim.ResetTrigger("Punch1");
            anim.ResetTrigger("Punch2");
            anim.ResetTrigger("Punch3");
            anim.ResetTrigger("Slash1");
            anim.ResetTrigger("Slash2");
            if (!string.IsNullOrEmpty(drawWeaponTrigger)) anim.ResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) anim.ResetTrigger(sheathWeaponTrigger);
            if (!string.IsNullOrEmpty(drawLeftTrigger)) anim.ResetTrigger(drawLeftTrigger);
            if (!string.IsNullOrEmpty(drawRightTrigger)) anim.ResetTrigger(drawRightTrigger);
            if (!string.IsNullOrEmpty(sheatheLeftTrigger)) anim.ResetTrigger(sheatheLeftTrigger);
            if (!string.IsNullOrEmpty(sheatheRightTrigger)) anim.ResetTrigger(sheatheRightTrigger);
        }

        if (IsActionAnimationName(translatedName))
        {
            anim.SetTrigger(translatedName);
            // Ép Animator CrossFade mượt mà tuyệt đối giữa các hành động (tránh khựng dáng đi)
            anim.CrossFadeInFixedTime(translatedName, fadeTime, 0, 0f);
        }
        else
        {
            anim.SetTrigger(translatedName);
        }

        bool isMovingAttack = IsAttackAnimationName(translatedName) && !isRootedAttack;
        if (!isMovingAttack)
        {
            currentAnimState = translatedName;
        }
        lastTriggeredAnimName = translatedName;

        if (IsActionAnimationName(translatedName))
        {
            lastActionTriggerTime = Time.time;
        }

        if (IsFullBodyActionAnimation(translatedName))
        {
            ClearAttackLayer();
        }
    }

    // --- BẮT ÉP ROOT MOTION (Lúc lộn vòng) PHẢI CHẠY QUA HỆ THỐNG VẬT LÝ RIGIDBODY ĐỂ CHẶN XUYÊN TƯỜNG ---
    private void OnAnimatorMove()
    {
        if (anim != null && rb != null)
        {
            if (anim.applyRootMotion)
            {
                Vector3 nextPosition = rb.position + anim.deltaPosition;
                rb.MovePosition(nextPosition);
            }
            else
            {
                // Giải phóng chuyển động vật lý mặc định khi không dùng Root Motion
                anim.ApplyBuiltinRootMotion();
            }
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

    private void ClearAttackLayer()
    {
        comboStep = 0;
        isRootedAttack = false;
        SetMovementLock(false); // Giải phóng khóa di chuyển nếu đang đấm nửa chừng
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            anim.Play("New State", 1, 0f);
            anim.Play("Empty", 1, 0f);
        }
    }

    // ------------------------------------------------------------------
    //  Hitbox Combat System & Animation Event Receivers
    // ------------------------------------------------------------------
    public void EnableLeftHitbox()
    {
        if (leftHitbox != null)
        {
            leftHitbox.enabled = true;
            Debug.Log("[LeoPlayer] Left hitbox ENABLED.");
        }
    }

    public void DisableLeftHitbox()
    {
        if (leftHitbox != null)
        {
            leftHitbox.enabled = false;
            Debug.Log("[LeoPlayer] Left hitbox DISABLED.");
        }
    }

    public void EnableRightHitbox()
    {
        if (rightHitbox != null)
        {
            rightHitbox.enabled = true;
            Debug.Log("[LeoPlayer] Right hitbox ENABLED.");
        }
    }

    public void DisableRightHitbox()
    {
        if (rightHitbox != null)
        {
            rightHitbox.enabled = false;
            Debug.Log("[LeoPlayer] Right hitbox DISABLED.");
        }
    }

    public void EnableBothHitboxes()
    {
        EnableLeftHitbox();
        EnableRightHitbox();
        Debug.Log("[LeoPlayer] Both hitboxes ENABLED.");
    }

    public void DisableBothHitboxes()
    {
        DisableLeftHitbox();
        DisableRightHitbox();
        Debug.Log("[LeoPlayer] Both hitboxes DISABLED.");
    }

    public void UnlockMovement()
    {
        SetMovementLock(false);
        Debug.Log("[LeoPlayer] Movement UNLOCKED.");
    }

    /// <summary>
    /// Event receiver được gọi bởi Animation Event tại thời điểm cúi xuống trong hoạt ảnh Pick.
    /// </summary>
    public void OnPickItemEvent()
    {
        Debug.Log("[LeoPlayer] OnPickItemEvent triggered via Animation Event.");
        if (pendingPickItem != null)
        {
            var collectible = pendingPickItem.GetComponent<CollectibleItemDrop>();
            if (collectible != null)
            {
                collectible.ConfirmCollect();
            }
            else
            {
                var repair = pendingPickItem.GetComponent<RepairItemDrop>();
                if (repair != null)
                {
                    repair.ConfirmCollect();
                }
            }
            pendingPickItem = null;
        }
    }

    /// <summary>
    /// Thay đổi trạng thái hiển thị/khóa con trỏ chuột.
    /// </summary>
    public void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        LockCursor(locked);
    }

    private PlayerHUDController hudControllerCache;
    private float lastTimeUIOpen = 0f;

    private PlayerHUDController GetHUDController()
    {
        if (hudControllerCache == null)
        {
            hudControllerCache = FindObjectOfType<PlayerHUDController>();
        }
        return hudControllerCache;
    }

    /// <summary>
    /// Kiểm tra xem UI (Hành trang, Bản đồ, Đối thoại) có đang mở chặn input hay không.
    /// </summary>
    public bool IsUIBlockingInput()
    {
        bool uiOpen = false;

        // Kiểm tra biến static cực nhanh và chính xác 100% không lo null hay sai lệch frame
        if (PlayerHUDController.isAnyUIOpen)
        {
            uiOpen = true;
        }

        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);
        if (isDialogueOpen)
        {
            uiOpen = true;
        }

        if (uiOpen)
        {
            lastTimeUIOpen = Time.time;
            return true;
        }

        // Chặn click chuột thêm 0.15 giây sau khi đóng UI để tránh click đóng UI bị đi xuyên làm nhân vật chém
        if (Time.time - lastTimeUIOpen < 0.15f)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Thiết lập trạng thái khóa di chuyển local, triệt tiêu vận tốc ngay lập tức và đồng bộ lên server.
    /// </summary>
    public void SetMovementLock(bool locked)
    {
        isMovementLocked = locked;
        if (locked)
        {
            attackDashTimer = 0f; // Hủy bỏ Dash đang hoạt động nếu có
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
        }

        if (!isStandaloneMode && IsOwner)
        {
            SetMovementLockServerRpc(locked);
        }
    }

    [ServerRpc]
    private void SetMovementLockServerRpc(bool locked)
    {
        if (IsServer)
        {
            isMovementLockedNet.Value = locked;
        }
    }

    private void OnMovementLockedNetChanged(bool oldVal, bool newVal)
    {
        if (!IsOwner)
        {
            isMovementLocked = newVal;
            if (newVal && rb != null)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
        }
    }

    public void EnableLeftWeaponHitbox()
    {
        if (leftWeaponHitbox != null)
        {
            leftWeaponHitbox.enabled = true;
            Debug.Log("[LeoPlayer] Left weapon hitbox ENABLED.");
        }
    }

    public void DisableLeftWeaponHitbox()
    {
        if (leftWeaponHitbox != null)
        {
            leftWeaponHitbox.enabled = false;
            Debug.Log("[LeoPlayer] Left weapon hitbox DISABLED.");
        }
    }

    public void EnableRightWeaponHitbox()
    {
        if (rightWeaponHitbox != null)
        {
            rightWeaponHitbox.enabled = true;
            Debug.Log("[LeoPlayer] Right weapon hitbox ENABLED.");
        }
    }

    public void DisableRightWeaponHitbox()
    {
        if (rightWeaponHitbox != null)
        {
            rightWeaponHitbox.enabled = false;
            Debug.Log("[LeoPlayer] Right weapon hitbox DISABLED.");
        }
    }

    public void EnableBothWeaponHitboxes()
    {
        EnableLeftWeaponHitbox();
        EnableRightWeaponHitbox();
        Debug.Log("[LeoPlayer] Both weapon hitboxes ENABLED.");
    }

    public void DisableBothWeaponHitboxes()
    {
        DisableLeftWeaponHitbox();
        DisableRightWeaponHitbox();
        Debug.Log("[LeoPlayer] Both weapon hitboxes DISABLED.");
    }

    public void OnSlashEnd()
    {
        Debug.Log("[LeoPlayer] Slash ended.");
        if (anim != null) anim.applyRootMotion = false;
        
        if (isRootedAttack)
        {
            var bridge = GetRootMotionBridge();
            if (bridge != null) bridge.ApplyFinalOffset();
        }
    }

    public void OnHitboxCollision(Collider other)
    {
        if (IsEnemy(other, out Collider enemyCollider))
        {
            Transform enemyRoot = enemyCollider.transform.root;
            if (!alreadyHitEnemies.Contains(enemyRoot))
            {
                alreadyHitEnemies.Add(enemyRoot);
                Debug.Log($"[LeoPlayer Combat] Hitbox collided with enemy root: {enemyRoot.name}. Dealing damage: {damageAmount}");
                
                if (isStandaloneMode)
                {
                    TryDamageEnemy(enemyCollider);
                }
                else if (IsOwner)
                {
                    // Network Mode: Tell server to apply damage
                    var netObj = enemyCollider.GetComponentInParent<NetworkObject>();
                    if (netObj != null)
                    {
                        DamageEnemyServerRpc(netObj);
                    }
                    else
                    {
                        TryDamageEnemy(enemyCollider);
                    }
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
            if (col != null)
            {
                TryDamageEnemy(col);
            }
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

    /// <summary>
    /// Context menu option and runtime helper to automatically locate hands, 
    /// create hand and sword hitboxes (LeftHitbox, RightHitbox, LeftWeaponHitbox, RightWeaponHitbox),
    /// attach colliders & PlayerHitbox scripts, and link them to LeoPlayer.
    /// </summary>
    [ContextMenu("Create Sword Hitboxes")]
    public void CreateSwordHitboxes()
    {
        Transform leftHand = FindBoneRecursive(transform, "left");
        Transform rightHand = FindBoneRecursive(transform, "right");

        if (leftHand == null) leftHand = transform;
        if (rightHand == null) rightHand = transform;

        // --- 1. Tạo Hitbox cho Tay không (Punch) ---
        // Left Punch Hitbox
        if (leftHitbox == null)
        {
            Transform existingLeft = leftHand.Find("LeftHitbox");
            GameObject leftObj;
            if (existingLeft != null)
            {
                leftObj = existingLeft.gameObject;
            }
            else
            {
                leftObj = new GameObject("LeftHitbox");
                leftObj.transform.SetParent(leftHand);
                leftObj.transform.localPosition = Vector3.zero;
                leftObj.transform.localRotation = Quaternion.identity;
                leftObj.transform.localScale = Vector3.one;
            }

            BoxCollider col = leftObj.GetComponent<BoxCollider>();
            if (col == null) col = leftObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.2f, 0.2f, 0.2f); // Nhỏ, nằm ở nắm đấm
            col.center = Vector3.zero;
            col.enabled = false;

            PlayerHitbox ph = leftObj.GetComponent<PlayerHitbox>();
            if (ph == null) ph = leftObj.AddComponent<PlayerHitbox>();

            leftHitbox = col;
            Debug.Log("[LeoPlayer Editor] Created and linked LeftHitbox under " + leftHand.name);
        }

        // Right Punch Hitbox
        if (rightHitbox == null)
        {
            Transform existingRight = rightHand.Find("RightHitbox");
            GameObject rightObj;
            if (existingRight != null)
            {
                rightObj = existingRight.gameObject;
            }
            else
            {
                rightObj = new GameObject("RightHitbox");
                rightObj.transform.SetParent(rightHand);
                rightObj.transform.localPosition = Vector3.zero;
                rightObj.transform.localRotation = Quaternion.identity;
                rightObj.transform.localScale = Vector3.one;
            }

            BoxCollider col = rightObj.GetComponent<BoxCollider>();
            if (col == null) col = rightObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.2f, 0.2f, 0.2f); // Nhỏ, nằm ở nắm đấm
            col.center = Vector3.zero;
            col.enabled = false;

            PlayerHitbox ph = rightObj.GetComponent<PlayerHitbox>();
            if (ph == null) ph = rightObj.AddComponent<PlayerHitbox>();

            rightHitbox = col;
            Debug.Log("[LeoPlayer Editor] Created and linked RightHitbox under " + rightHand.name);
        }

        // --- 2. Tạo Hitbox cho Kiếm (Sword/Weapon) ---
        Transform leftSwordParent = FindWeaponTransform(leftHand) ?? leftHand;
        Transform rightSwordParent = FindWeaponTransform(rightHand) ?? rightHand;

        // Left Sword Hitbox
        if (leftWeaponHitbox == null)
        {
            Transform existingLeftW = leftSwordParent.Find("LeftWeaponHitbox");
            GameObject leftWObj;
            if (existingLeftW != null)
            {
                leftWObj = existingLeftW.gameObject;
            }
            else
            {
                leftWObj = new GameObject("LeftWeaponHitbox");
                leftWObj.transform.SetParent(leftSwordParent);
                leftWObj.transform.localPosition = Vector3.zero;
                leftWObj.transform.localRotation = Quaternion.identity;
                leftWObj.transform.localScale = Vector3.one;
            }

            BoxCollider col = leftWObj.GetComponent<BoxCollider>();
            if (col == null) col = leftWObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.2f, 0.2f, 1.2f); // To và dài hơn dọc theo thanh kiếm
            col.center = new Vector3(0f, 0f, 0.6f);
            col.enabled = false;

            PlayerHitbox ph = leftWObj.GetComponent<PlayerHitbox>();
            if (ph == null) ph = leftWObj.AddComponent<PlayerHitbox>();

            leftWeaponHitbox = col;
            Debug.Log("[LeoPlayer Editor] Created and linked LeftWeaponHitbox under " + leftSwordParent.name);
        }

        // Right Sword Hitbox
        if (rightWeaponHitbox == null)
        {
            Transform existingRightW = rightSwordParent.Find("RightWeaponHitbox");
            GameObject rightWObj;
            if (existingRightW != null)
            {
                rightWObj = existingRightW.gameObject;
            }
            else
            {
                rightWObj = new GameObject("RightWeaponHitbox");
                rightWObj.transform.SetParent(rightSwordParent);
                rightWObj.transform.localPosition = Vector3.zero;
                rightWObj.transform.localRotation = Quaternion.identity;
                rightWObj.transform.localScale = Vector3.one;
            }

            BoxCollider col = rightWObj.GetComponent<BoxCollider>();
            if (col == null) col = rightWObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.2f, 0.2f, 1.2f); // To và dài hơn dọc theo thanh kiếm
            col.center = new Vector3(0f, 0f, 0.6f);
            col.enabled = false;

            PlayerHitbox ph = rightWObj.GetComponent<PlayerHitbox>();
            if (ph == null) ph = rightWObj.AddComponent<PlayerHitbox>();

            rightWeaponHitbox = col;
            Debug.Log("[LeoPlayer Editor] Created and linked RightWeaponHitbox under " + rightSwordParent.name);
        }
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
            if (nameLower.Contains("hand"))
            {
                return current;
            }
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindBoneRecursive(current.GetChild(i), keyword);
            if (found != null) return found;
        }

        if (current.name.ToLower().Contains(keyword) && current.name.ToLower().Contains("hand"))
        {
            return current;
        }

        return null;
    }

    private void UpdateAttackLayerWeight()
    {
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(1);
            // Nếu layer 1 đang chạy hoạt ảnh chém (khác New State và Empty), đặt weight = 1
            bool isSlashActive = !stateInfo.IsName("New State") && !stateInfo.IsName("Empty");
            targetAttackLayerWeight = isSlashActive ? 1f : 0f;

            // Lerp mượt mà weight để tránh chuyển đổi giật cục dáng đi
            currentAttackLayerWeight = Mathf.MoveTowards(currentAttackLayerWeight, targetAttackLayerWeight, Time.deltaTime * 10f);
            anim.SetLayerWeight(1, currentAttackLayerWeight);
        }
    }
}