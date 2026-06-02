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
    public string slash1Trigger = "Combo1kiem";
    public string slash2Trigger = "Attackdoucombo";
    public string slash3Trigger = "Combo1kiem";

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
    }

    private void Start()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        // Dynamically set the weight of the AttackLayer (layer index 1) to 1.0f so attack animations show!
        if (anim != null && anim.layerCount > 1)
        {
            anim.SetLayerWeight(1, 1.0f);
            Debug.Log("[LeoPlayer] Dynamically set Animator Layer 1 (AttackLayer) weight to 1.0f");
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
        Debug.Log("[LeoPlayer] Starting in STANDALONE mode. Local inputs active.");
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindAnyObjectByType<Camera>();

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
        
        if (anim != null) anim.applyRootMotion = false;
        var bridge = GetRootMotionBridge();
        if (bridge != null) bridge.ApplyFinalOffset();

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

        if (isRollingStandalone || (IsSpawned && isRollingNet.Value))
        {
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0)
            {
                if (isStandaloneMode || IsOwner)
                {
                    OnRollEnd();
                }
                else
                {
                    if (anim != null) anim.applyRootMotion = false;
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

            rb.linearVelocity = new Vector3(targetVelocity.x, currentYVelocity, targetVelocity.z);
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
            if (!isRollingStandalone && !IsPlayingActionAnimation())
            {
                Debug.Log($"[LeoPlayer] Mouse clicked in Standalone. Weapon: {GetActiveWeaponIndex()}");
                PerformComboAttack(false);
            }
        }

        if (Input.GetKeyDown(rollKey))
        {
            if (rollCooldownTimer <= 0 && !IsPlayingAttackState(out _, out _))
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

            rb.linearVelocity = new Vector3(targetVelocity.x, currentYVelocity, targetVelocity.z);
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
            if (IsSpawned)
            {
                Debug.Log($"[LeoPlayer] Mouse clicked in Owner mode. Weapon: {GetActiveWeaponIndex()}");
                PerformComboAttack(true);
            }
        }

        if (Input.GetKeyDown(rollKey))
        {
            if (rollCooldownTimer <= 0 && IsSpawned && !IsPlayingAttackState(out _, out _))
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

    protected void PerformComboAttack(bool networkMode)
    {
        int weapon = GetActiveWeaponIndex();
        float currentTime = Time.time;
        
        if (weapon == 2)
        {
            if (currentTime - lastAttackTime < slash1Duration * comboTransitionThreshold)
            {
                return;
            }
            
            lastAttackTime = currentTime;
            
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");
            bool isMovingInput = (Mathf.Abs(moveX) > 0.01f || Mathf.Abs(moveZ) > 0.01f);
            isRootedAttack = !isMovingInput;
            
            // Random 50-50 choice between Slash1 (Combo1kiem) and Slash2 (Attackdoucombo) as requested
            float rand = Random.value;
            string animToPlay = (rand < 0.5f) ? "Slash1" : "Slash2";
            Debug.Log($"[LeoPlayer] Combo attack (Armed). Random roll: {rand:F2} -> Playing: {animToPlay}");
            
            PlayAnimation(animToPlay, 0.05f, false);
            
            if (networkMode)
            {
                AttackServerRpc();
            }
            else
            {
                Vector3 rayStart = transform.position + Vector3.up * 0.5f;
                if (Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange))
                {
                    TryDamageEnemy(hit.collider);
                }
            }
        }
        else
        {
            // Unarmed combo Sequential punches
            int nextStep = comboStep;
            if (currentTime - lastAttackTime > comboWindow)
            {
                nextStep = 0;
            }
            nextStep++;

            if (nextStep > 2) nextStep = 1;

            if (comboStep > 0 && currentTime - lastAttackTime <= comboWindow)
            {
                float prevDuration = GetAttackDuration(1, comboStep);
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

            string animToPlay = comboStep == 1 ? "Punch1" : "Punch2";
            Debug.Log($"[LeoPlayer] Fist attack (Unarmed). Step: {comboStep} -> Playing: {animToPlay}");
            
            PlayAnimation(animToPlay, 0.05f, false);

            if (networkMode)
            {
                AttackServerRpc();
            }
            else
            {
                Vector3 rayStart = transform.position + Vector3.up * 0.5f;
                if (Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange))
                {
                    TryDamageEnemy(hit.collider);
                }
            }
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

        if (anim != null) anim.applyRootMotion = true;
        PlayAnimation("LonVong", 0.05f);
    }

    protected void StartRollOwner(Vector3 moveInput)
    {
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

        if (anim != null) anim.applyRootMotion = true;
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

    public bool TryAddItem(string itemName)
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
                    PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
                    if (hud != null) hud.SetInventorySlots(inventorySlots);
                    if (!isStandaloneMode) SavePlayerStateToDatabase();
                    PlayAnimation("Idle_Pick", 0.1f);
                    return true;
                }
            }
        }

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (string.IsNullOrEmpty(inventorySlots[i]))
            {
                inventorySlots[i] = itemName + ":1";
                PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
                if (hud != null) hud.SetInventorySlots(inventorySlots);
                if (!isStandaloneMode) SavePlayerStateToDatabase();
                PlayAnimation("Idle_Pick", 0.1f);
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

        if (newWeapon == 2)
        {
            if (!string.IsNullOrEmpty(drawWeaponTrigger))
            {
                PlayAnimation(drawWeaponTrigger, 0.1f);
            }
        }
        else if (newWeapon == 1)
        {
            if (!string.IsNullOrEmpty(sheathWeaponTrigger))
            {
                PlayAnimation(sheathWeaponTrigger, 0.1f);
            }
        }
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
            case "Idle_Pick":
                return pickTrigger;
            case "Punch1":
                return punch1Trigger;
            case "Punch2":
                return punch2Trigger;
            case "Slash1":
                return slash1Trigger;
            case "Slash2":
                return slash2Trigger;
            case "Slash3":
                return slash3Trigger;
            case "GetHit":
                return getHitTrigger;
            case "GeiHit2":
                return getHit2Trigger;
            case "Death":
                return isArmed ? deathArmedTrigger : deathUnarmedTrigger;
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
               name == slash1Trigger ||
               name == slash2Trigger ||
               name == slash3Trigger ||
               name == "LonVong" ||
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

    private bool IsAttackAnimationName(string name)
    {
        return name == punch1Trigger || 
               name == punch2Trigger || 
               name == slash1Trigger || 
               name == slash2Trigger ||
               name == slash3Trigger ||
               name == "Punch1" ||
               name == "Punch2" ||
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3";
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

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController != null)
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
               stateInfo.IsName(slash1Trigger) || 
               stateInfo.IsName(slash2Trigger) ||
               stateInfo.IsName(slash3Trigger) ||
               stateInfo.IsName("Punch1") ||
               stateInfo.IsName("Punch2") ||
               stateInfo.IsName("Slash1") ||
               stateInfo.IsName("Slash2") ||
               stateInfo.IsName("Slash3");
    }

    private bool IsPlayingActionAnimation()
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController != null) return false;

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
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController != null) return false;

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
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController != null) return;

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
            anim.ResetTrigger(slash1Trigger);
            anim.ResetTrigger(slash2Trigger);
            anim.ResetTrigger(slash3Trigger);
            
            anim.ResetTrigger("Punch1");
            anim.ResetTrigger("Punch2");
            anim.ResetTrigger("Slash1");
            anim.ResetTrigger("Slash2");
            anim.ResetTrigger("Slash3");
            if (!string.IsNullOrEmpty(drawWeaponTrigger)) anim.ResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) anim.ResetTrigger(sheathWeaponTrigger);
        }

        if (translatedName == pickTrigger || translatedName == "Idle_Pick")
        {
            anim.ResetTrigger(translatedName);
            anim.CrossFadeInFixedTime(translatedName, fadeTime, -1, 0f);
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
        if (anim != null && anim.applyRootMotion && rb != null)
        {
            Vector3 nextPosition = rb.position + anim.deltaPosition;
            rb.MovePosition(nextPosition);
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
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            anim.Play("New State", 1, 0f);
            anim.Play("Empty", 1, 0f);
        }
    }
}