// using UnityEngine;
// using Unity.Netcode;

// /// <summary>
// /// Independent custom player controller for Leo Assassin, inheriting directly from NetworkBehaviour.
// /// Operates seamlessly with the SimplePlayerTest proxy component to maintain complete compatibility
// /// with enemy AI, NPC dialog, and UI HUD systems.
// /// Supports smooth 8-directional Blend Tree movement using actual user fbx filenames.
// /// </summary>
// public class LeoPlayer : NetworkBehaviour
// {
//     [Header("Input Keys Configuration")]
//     [Tooltip("Key to trigger roll/dodge.")]
//     public KeyCode rollKey = KeyCode.LeftControl;

//     [Header("Blend Tree vs Trigger Mode")]
//     [Tooltip("If true, movement is animated smoothly using float parameters (InputX, InputZ, Speed, IsArmed) and Blend Trees. If false, it triggers separate animation states directly.")]
//     public bool useBlendTree = true;

//     [Header("Animator Parameter Names")]
//     public string inputXParam = "InputX";
//     public string inputZParam = "InputZ";
//     public string speedParam = "Speed";
//     public string isArmedParam = "IsArmed";

//     [Header("Smooth Movement Settings")]
//     public float inputFilterSpeed = 8f;
//     public float rotationSmoothSpeedArmed = 10f;
//     public float rotationSmoothSpeedUnarmed = 12f;
//     [Tooltip("If true, the character rotates to face the camera direction when Armed, enabling backpedaling and strafing.")]
//     public bool rotateToCameraWhenArmed = true;
//     [Tooltip("If true, the character rotates to face the camera direction when Unarmed, enabling backpedaling and strafing without weapons.")]
//     public bool rotateToCameraWhenUnarmed = false;

//     [Header("Player Settings & Stats")]
//     public float moveSpeed = 3.5f;
//     public float runSpeedMultiplier = 3.5f;
//     public float damageAmount = 25f;
//     public float attackRange = 2f;

//     [Header("Combo Attack Settings")]
//     public float comboWindow = 1.0f;
//     public float comboTransitionThreshold = 0.5f;
//     public float punch1Duration = 0.5f;
//     public float punch2Duration = 0.5f;
//     public float punch3Duration = 0.5f;
//     public float slash1Duration = 0.6f;
//     public float slash2Duration = 0.6f;
//     public float slash3Duration = 0.7f;
//     protected int comboStep = 0;
//     protected float lastAttackTime = 0f;
//     protected bool isRootedAttack = false;

//     [Header("Hitbox References")]
//     [Tooltip("Left hand hitbox collider.")]
//     public Collider leftHitbox;
//     [Tooltip("Right hand hitbox collider.")]
//     public Collider rightHitbox;

//     [Tooltip("Left weapon/sword hitbox collider.")]
//     public Collider leftWeaponHitbox;
//     [Tooltip("Right weapon/sword hitbox collider.")]
//     public Collider rightWeaponHitbox;

//     [Header("Weapon Visual References")]
//     [Tooltip("Thanh kiếm trên tay trái")]
//     public GameObject leftHandSword;
//     [Tooltip("Thanh kiếm trên tay phải")]
//     public GameObject rightHandSword;
//     [Tooltip("Thanh kiếm giắt trên vai/lưng trái")]
//     public GameObject leftShoulderSword;
//     [Tooltip("Thanh kiếm giắt trên vai/lưng phải")]
//     public GameObject rightShoulderSword;

//     private System.Collections.Generic.List<Transform> alreadyHitEnemies = new System.Collections.Generic.List<Transform>();

//     [Header("Movement Lock State")]
//     [Tooltip("If true, movement inputs are ignored and Rigidbody XZ velocity is locked to 0.")]
//     public bool isMovementLocked = false;

//     private float targetAttackLayerWeight = 0f;
//     private float currentAttackLayerWeight = 0f;

//     [Header("Attack Dash Settings")]
//     [Tooltip("Speed of the automatic forward dash during combo attacks.")]
//     public float attackDashSpeed = 4.0f;
//     [Tooltip("Duration of the automatic forward dash during combo attacks.")]
//     public float attackDashDuration = 0.25f;
//     private float attackDashTimer = 0f;
//     private Vector3 attackDashDirection;

//     [Header("Weapon Switch Animations")]
//     public string drawWeaponTrigger = "DrawWeapon";
//     public string sheathWeaponTrigger = "SheathWeapon";
//     public string drawLeftTrigger = "DrawLeft";
//     public string drawRightTrigger = "DrawRight";
//     public string sheatheLeftTrigger = "SheatheLeft";
//     public string sheatheRightTrigger = "SheatheRight";
//     [HideInInspector]
//     public bool isSwitchingWeapon = false; // Chống spam phím khi đổi vũ khí

//     [Header("Player Health Settings")]
//     public float maxHealth = 85f;

//     [Header("Player Class Settings")]
//     [Tooltip("0 = Sát Thủ, 1 = Hỏa Thuật, 2 = Cung Thủ, 3 = Tanker")]
//     public int characterClassIndex = 0;

//     [Header("Knockback Settings")]
//     protected Vector3 knockbackVelocity;

//     [Header("Camera Follow Settings")]
//     public bool enableCameraFollow = true;
//     public Vector3 cameraOffset = new Vector3(0f, 10f, -6.5f);
//     public float cameraSmoothSpeed = 5f;
//     public bool cameraLookAtPlayer = true;
//     public float cameraPivotHeight = 3.5f;
//     protected Camera targetCamera;

//     [Header("Camera Rotation Settings")]
//     public float cameraSensitivity = 3f;
//     public float minPitch = 10f;
//     public float maxPitch = 80f;
//     public float rotationSmoothSpeed = 15f;
//     protected float currentYaw = 0f;
//     protected float currentPitch = 45f;
//     protected float targetYaw = 0f;
//     protected float targetPitch = 45f;
//     protected float cameraDistance = 14f;
//     protected bool isCursorLocked = true;

//     [Header("Animation Settings")]
//     public Animator anim;
//     protected string currentAnimState;

//     [Header("Dodge Roll Settings")]
//     public float rollSpeed = 10f;
//     public float rollDuration = 0.4f;
//     public float rollCooldown = 1.2f;
//     protected float rollCooldownTimer;
//     protected float rollTimer;
//     protected Vector3 rollDirection;
//     protected bool isRollingStandalone = false;

//     // ------------------------------------------------------------------
//     //  Network Sync Variables
//     // ------------------------------------------------------------------
//     public NetworkVariable<float> currentHealth = new NetworkVariable<float>(85f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<int> activeWeaponIndex = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<bool> isWeapon2Locked = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<bool> isSkillsUnlocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<int> upgradePoints = new NetworkVariable<int>(5, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<int> hpLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<int> mpLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<int> cooldownLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<int> damageLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<int> playerLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<float> playerExp = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<float> weapon1Durability = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<float> weapon2Durability = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<bool> isRollingNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     public NetworkVariable<bool> isMovementLockedNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     [HideInInspector]
//     public GameObject pendingPickItem;

//     [Header("Local State & Inventory")]
//     public string[] inventorySlots = new string[10] { "", "", "", "", "", "", "", "", "", "" };
//     protected int localUpgradePoints = 5;
//     protected int localHpLevel = 0;
//     protected int localMpLevel = 0;
//     protected int localCooldownLevel = 0;
//     protected int localDamageLevel = 0;
//     protected int localLevel = 0;
//     protected float localExp = 0f;
//     protected float localHealth;
//     protected float localWeapon1Durability = 100f;
//     protected float localWeapon2Durability = 100f;

//     public bool isStandaloneMode = false;
//     protected bool isSyncingFromDb = false;

//     [Header("Weapon Durability Config")]
//     public float weapon1MaxDurability = 100f;
//     public float weapon2MaxDurability = 100f;

//     // Trigger lists (Unarmed fallback)
//     public string idleUnarmed = "Idle";
//     public string walkUnarmed = "Walk";
//     public string runUnarmed = "run";
//     public string walkBackwardUnarmed = "WalkBackward";
//     public string runBackwardUnarmed = "RunBackward";
//     public string walkLeftUnarmed = "walkleftkhongvukhi";
//     public string walkRightUnarmed = "walkrightkhongvukhi";
//     public string runLeftUnarmed = "Runleftkhongvukhi";
//     public string runRightUnarmed = "Rủnightnovukhi"; // Note: Vietnamese accent 'ủ'

//     // Trigger lists (Armed fallback)
//     public string idleArmed = "IdleArmed";
//     public string walkForwardArmed = "WalkForwardArmed";
//     public string walkBackwardArmed = "WalkBackwardArmed";
//     public string runForwardArmed = "RunForwardArmed";
//     public string runBackwardArmed = "RunBackwardArmed";
//     public string walkLeftArmed = "walkleftcovukhi";
//     public string walkRightArmed = "walkrightcovukhi";
//     public string runLeftArmed = "Runleftcovukhi";
//     public string runRightArmed = "Rủnightcovukhi"; // Note: Vietnamese accent 'ủ'

//     // Action and attack triggers
//     public string rollTrigger = "Lonmeo";
//     public string pickTrigger = "Pick";
//     public string punch1Trigger = "DamTrai";
//     public string punch2Trigger = "DamPhai";
//     public string punch3Trigger = "DamCombo";
//     public string slash1Trigger = "Combo1kiem";
//     public string slash2Trigger = "Attackdoucombo";

//     // Death and hit
//     public string deathUnarmedTrigger = "Death";
//     public string deathArmedTrigger = "DeathArmed";
//     public string getHitTrigger = "GetHit";
//     public string getHit2Trigger = "GeiHit2";

//     // Smooth inputs
//     private float smoothedInputX = 0f;
//     private float smoothedInputZ = 0f;
//     private int fallbackWeaponIndex = 1;
//     private RootMotionBridge rootMotionBridge;
//     private SimplePlayerTest proxyPlayerTest;

//     // Rigidbody component phục vụ tính toán vật lý, khắc phục lỗi đi xuyên
//     private Rigidbody rb;

//     private bool IsNetworkActive =>
//         NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

//     public float CurrentHealth =>
//         isStandaloneMode ? localHealth : currentHealth.Value;

//     protected RootMotionBridge GetRootMotionBridge()
//     {
//         if (rootMotionBridge == null && anim != null)
//         {
//             rootMotionBridge = anim.GetComponent<RootMotionBridge>();
//         }
//         return rootMotionBridge;
//     }

//     private void Awake()
//     {
//         rollKey = KeyCode.LeftControl;
//         proxyPlayerTest = GetComponent<SimplePlayerTest>();

//         characterClassIndex = 0;
//         maxHealth = 85f;
//         moveSpeed = 3.5f;
//         runSpeedMultiplier = 3.5f;
//         damageAmount = 25f;
//         attackRange = 2f;
//         cameraOffset = new Vector3(0f, 10f, -6.5f);
//         cameraSensitivity = 3f;
//         cameraPivotHeight = 3.5f;

//         if (anim == null)
//         {
//             anim = GetComponent<Animator>();
//             if (anim == null)
//                 anim = GetComponentInChildren<Animator>(true);
//         }

//         // Lấy Rigidbody và khóa các trục trục xoay tự do do va chạm vật lý đem lại
//         rb = GetComponent<Rigidbody>();
//         if (rb != null)
//         {
//             rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
//             rb.interpolation = RigidbodyInterpolation.Interpolate; // Giúp Player di chuyển mượt mà không bị khựng hình
//         }

//         // Tự động tạo và liên kết hitbox kiếm/tay nếu bị null
//         CreateSwordHitboxes();
//     }

//     private void Start()
//     {
//         if (anim == null)
//         {
//             anim = GetComponent<Animator>();
//             if (anim == null)
//                 anim = GetComponentInChildren<Animator>(true);
//         }

//         // Khởi tạo weight của AttackLayer ở 0
//         if (anim != null && anim.layerCount > 1)
//         {
//             anim.SetLayerWeight(1, 0f);
//         }

//         float horizontalDistance = new Vector3(cameraOffset.x, 0f, cameraOffset.z).magnitude;
//         currentYaw = Mathf.Atan2(cameraOffset.x, -cameraOffset.z) * Mathf.Rad2Deg;
//         currentPitch = Mathf.Atan2(cameraOffset.y, horizontalDistance) * Mathf.Rad2Deg;
//         targetYaw = currentYaw;
//         targetPitch = currentPitch;
//         cameraDistance = cameraOffset.magnitude;

//         LockCursor(isCursorLocked);

//         if (!IsNetworkActive)
//         {
//             isStandaloneMode = true;
//             localHealth = maxHealth;
//             InitStandaloneMode();
//         }

//         // Khởi tạo hiển thị vũ khí ban đầu phù hợp với trạng thái Armed/Unarmed
//         SyncWeaponVisuals(GetActiveWeaponIndex());
//     }

//     private void InitStandaloneMode()
//     {
//         Debug.Log("[LeoPlayer] Starting in STANDALONE mode. Local inputs active.");
//         targetCamera = Camera.main;
//         if (targetCamera == null)
//             targetCamera = FindObjectOfType<Camera>();

//         characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);
//         localLevel = PlayerPrefs.GetInt("SelectedPlayerLevel_" + characterClassIndex, 0);
//         localExp = PlayerPrefs.GetFloat("SelectedPlayerExp_" + characterClassIndex, 0f);

//         PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//         if (hud != null)
//         {
//             hud.SetupPlayerProfile(characterClassIndex);
//             ApplyUpgradedStats();
//             UpdateUpgradeHUD();
//         }
//     }

//     public override void OnNetworkSpawn()
//     {
//         isStandaloneMode = false;

//         activeWeaponIndex.OnValueChanged += OnWeaponIndexChanged;
//         isWeapon2Locked.OnValueChanged += OnWeapon2LockedChanged;
//         isSkillsUnlocked.OnValueChanged += OnSkillsUnlockedChanged;
//         upgradePoints.OnValueChanged += OnUpgradePointsChanged;
//         hpLevel.OnValueChanged += OnHpLevelChanged;
//         mpLevel.OnValueChanged += OnMpLevelChanged;
//         cooldownLevel.OnValueChanged += OnCooldownLevelChanged;
//         damageLevel.OnValueChanged += OnDamageLevelChanged;
//         playerLevel.OnValueChanged += OnLevelOrExpChanged;
//         playerExp.OnValueChanged += OnLevelOrExpChanged;
//         weapon1Durability.OnValueChanged += OnDurabilityChanged;
//         weapon2Durability.OnValueChanged += OnDurabilityChanged;
//         isMovementLockedNet.OnValueChanged += OnMovementLockedNetChanged;

//         if (IsOwner)
//         {
//             currentHealth.OnValueChanged += OnHealthChanged;
//             UpdateHealthHUD(currentHealth.Value);

//             PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//             if (hud != null)
//                 hud.SetupPlayerProfile(characterClassIndex);

//             targetCamera = Camera.main;
//             if (targetCamera == null)
//                 targetCamera = FindAnyObjectByType<Camera>();

//             LoadPlayerStateFromDatabase();
//             ApplyUpgradedStats();
//             UpdateUpgradeHUD();
//             UpdateDurabilityHUD();
//         }

//         // Đồng bộ hiển thị vũ khí ban đầu cho tất cả người chơi trên mạng
//         SyncWeaponVisuals(activeWeaponIndex.Value);
//     }

//     public override void OnNetworkDespawn()
//     {
//         activeWeaponIndex.OnValueChanged -= OnWeaponIndexChanged;
//         isWeapon2Locked.OnValueChanged -= OnWeapon2LockedChanged;
//         isSkillsUnlocked.OnValueChanged -= OnSkillsUnlockedChanged;
//         upgradePoints.OnValueChanged -= OnUpgradePointsChanged;
//         hpLevel.OnValueChanged -= OnHpLevelChanged;
//         mpLevel.OnValueChanged -= OnMpLevelChanged;
//         cooldownLevel.OnValueChanged -= OnCooldownLevelChanged;
//         damageLevel.OnValueChanged -= OnDamageLevelChanged;
//         playerLevel.OnValueChanged -= OnLevelOrExpChanged;
//         playerExp.OnValueChanged -= OnLevelOrExpChanged;
//         weapon1Durability.OnValueChanged -= OnDurabilityChanged;
//         weapon2Durability.OnValueChanged -= OnDurabilityChanged;
//         isMovementLockedNet.OnValueChanged -= OnMovementLockedNetChanged;

//         if (IsOwner)
//             currentHealth.OnValueChanged -= OnHealthChanged;
//     }

//     // Helper functions to sync NetworkVariables on the Server to proxy component
//     private void SyncNetVarFloat(NetworkVariable<float> myVar, NetworkVariable<float> proxyVar, float value)
//     {
//         if (IsServer)
//         {
//             myVar.Value = value;
//             if (proxyVar != null) proxyVar.Value = value;
//         }
//     }

//     private void SyncNetVarInt(NetworkVariable<int> myVar, NetworkVariable<int> proxyVar, int value)
//     {
//         if (IsServer)
//         {
//             myVar.Value = value;
//             if (proxyVar != null) proxyVar.Value = value;
//         }
//     }

//     private void SyncNetVarBool(NetworkVariable<bool> myVar, NetworkVariable<bool> proxyVar, bool value)
//     {
//         if (IsServer)
//         {
//             myVar.Value = value;
//             if (proxyVar != null) proxyVar.Value = value;
//         }
//     }

//     public int GetActiveWeaponIndex()
//     {
//         if (isStandaloneMode)
//         {
//             PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//             if (hud != null) return hud.currentSelectedWeapon;
//             return fallbackWeaponIndex;
//         }
//         return activeWeaponIndex.Value;
//     }

//     public void OnRollEnd()
//     {
//         Debug.Log("[LeoPlayer] Roll ended via Animation Event.");
//         isRollingStandalone = false;

//         if (anim != null) anim.applyRootMotion = false; // Tắt applyRootMotion
//         var bridge = GetRootMotionBridge();
//         if (bridge != null) bridge.ApplyFinalOffset();

//         if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); // Dừng lực lộn

//         if (!isStandaloneMode && IsOwner)
//         {
//             StopRollServerRpc();
//         }
//     }

//     private void LockCursor(bool locked)
//     {
//         if (locked)
//         {
//             Cursor.lockState = CursorLockMode.Locked;
//             Cursor.visible = false;
//         }
//         else
//         {
//             Cursor.lockState = CursorLockMode.None;
//             Cursor.visible = true;
//         }
//     }

//     private void Update()
//     {
//         // Cập nhật Layer Weight động cho Attack Layer (Layer 1)
//         UpdateAttackLayerWeight();

//         bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
//         if (hasControl)
//         {
//             if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
//             {
//                 isCursorLocked = !isCursorLocked;
//                 LockCursor(isCursorLocked);
//             }
//         }

//         if (rollCooldownTimer > 0)
//         {
//             rollCooldownTimer -= Time.deltaTime;
//         }

//         if (attackDashTimer > 0)
//         {
//             attackDashTimer -= Time.deltaTime;
//         }

//         if (isRollingStandalone || (IsSpawned && isRollingNet.Value))
//         {
//             rollTimer -= Time.deltaTime;

//             if (rollTimer <= 0)
//             {
//                 if (isStandaloneMode || IsOwner)
//                 {
//                     OnRollEnd();
//                 }
//             }
//         }

//         if (CurrentHealth <= 0)
//         {
//             if (anim != null) anim.applyRootMotion = false;
//             if (rb != null) rb.linearVelocity = Vector3.zero; // Dừng lực khi chết
//             PlayAnimation("Death", 0.15f);
//             return;
//         }

//         // Standalone weapon switching when no HUD
//         if (isStandaloneMode && FindAnyObjectByType<PlayerHUDController>() == null)
//         {
//             if (isSwitchingWeapon) return;
//             if (Input.GetKeyDown(KeyCode.Alpha1))
//             {
//                 int oldW = fallbackWeaponIndex;
//                 fallbackWeaponIndex = 1;
//                 PlayWeaponSwitchAnimation(oldW, 1);
//             }
//             else if (Input.GetKeyDown(KeyCode.Alpha2))
//             {
//                 int oldW = fallbackWeaponIndex;
//                 fallbackWeaponIndex = 2;
//                 PlayWeaponSwitchAnimation(oldW, 2);
//             }
//         }

//         if (isStandaloneMode)
//         {
//             HandleStandaloneUpdate();
//             return;
//         }

//         if (!IsOwner) return;
//         HandleOwnerUpdate();
//     }

//     private void HandleStandaloneUpdate()
//     {
//         bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
//                               (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);

//         if (isDialogueOpen)
//         {
//             smoothedInputX = Mathf.MoveTowards(smoothedInputX, 0f, Time.deltaTime * inputFilterSpeed);
//             smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, 0f, Time.deltaTime * inputFilterSpeed);
//             UpdateAnimatorParams(0f);
//             if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
//             if (!IsPlayingActionAnimation())
//             {
//                 if (!useBlendTree) PlayAnimationLocal("Idle", 0.1f);
//             }
//             return;
//         }

//         if (IsPlayingPickAnimation())
//         {
//             smoothedInputX = Mathf.MoveTowards(smoothedInputX, 0f, Time.deltaTime * inputFilterSpeed);
//             smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, 0f, Time.deltaTime * inputFilterSpeed);
//             UpdateAnimatorParams(0f);
//             if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
//             return;
//         }

//         if (isRollingStandalone)
//         {
//             if (rb != null)
//             {
//                 float currentYVelocity = rb.linearVelocity.y;
//                 rb.linearVelocity = new Vector3(rollDirection.x * rollSpeed, currentYVelocity, rollDirection.z * rollSpeed);
//             }
//             return;
//         }

//         bool isRunning = Input.GetKey(KeyCode.LeftShift);
//         float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;

//         float moveX = isMovementLocked ? 0f : Input.GetAxis("Horizontal");
//         float moveZ = isMovementLocked ? 0f : Input.GetAxis("Vertical");
//         Vector3 move = new Vector3(moveX, 0, moveZ);

//         if (targetCamera == null)
//         {
//             targetCamera = Camera.main;
//             if (targetCamera == null)
//                 targetCamera = FindAnyObjectByType<Camera>();
//         }

//         if (targetCamera != null)
//         {
//             Vector3 camForward = targetCamera.transform.forward;
//             camForward.y = 0f;
//             camForward.Normalize();
//             Vector3 camRight = targetCamera.transform.right;
//             camRight.y = 0f;
//             camRight.Normalize();
//             move = camRight * moveX + camForward * moveZ;
//         }

//         Vector3 movementTranslation = move;
//         if (isRootedAttack && IsPlayingActionAnimation())
//         {
//             movementTranslation = Vector3.zero;
//         }

//         // --- ĐÃ ĐỔI THÀNH RIGIDBODY TÍNH VẬN TỐC THAY VÌ TRANSLATE ---
//         if (rb != null)
//         {
//             Vector3 targetVelocity = movementTranslation * currentSpeed;
//             float currentYVelocity = rb.linearVelocity.y;

//             // Tính hợp lực Knockback trực tiếp vào vận tốc Rigidbody tại đây
//             if (knockbackVelocity.magnitude > 0.01f)
//             {
//                 targetVelocity += knockbackVelocity;
//                 knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
//             }

//             // Ưu tiên lực Dash đòn đánh
//             if (attackDashTimer > 0)
//             {
//                 rb.linearVelocity = new Vector3(attackDashDirection.x * attackDashSpeed, currentYVelocity, attackDashDirection.z * attackDashSpeed);
//             }
//             // Nếu khóa di chuyển, đứng im
//             else if (isMovementLocked && knockbackVelocity.magnitude <= 0.01f)
//             {
//                 rb.linearVelocity = new Vector3(0f, currentYVelocity, 0f);
//             }
//             else
//             {
//                 rb.linearVelocity = new Vector3(targetVelocity.x, currentYVelocity, targetVelocity.z);
//             }
//         }

//         bool isArmed = (GetActiveWeaponIndex() == 2);
//         bool isMoving = (movementTranslation != Vector3.zero);

//         float targetInputX = 0f;
//         float targetInputZ = 0f;
//         float targetSpeed = 0f;

//         // Kiểm tra xem nhân vật có đang trong trạng thái đấm/chém hay không
//         bool isAttacking = IsPlayingAttackState(out _, out _);

//         // Nếu đang tấn công, ép buộc nhân vật phải xoay mặt về hướng Camera
//         bool shouldAlignToCamera = (isArmed && rotateToCameraWhenArmed) ||
//                                    (!isArmed && rotateToCameraWhenUnarmed) ||
//                                    isAttacking;

//         if (shouldAlignToCamera)
//         {
//             if (targetCamera != null)
//             {
//                 Vector3 camForward = targetCamera.transform.forward;
//                 camForward.y = 0f;
//                 if (camForward.sqrMagnitude > 0.001f)
//                 {
//                     Quaternion targetRot = Quaternion.LookRotation(camForward.normalized);
//                     transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeedArmed);
//                 }
//             }

//             if (isMoving)
//             {
//                 targetInputX = moveX * (isRunning ? 1.0f : 0.5f);
//                 targetInputZ = moveZ * (isRunning ? 1.0f : 0.5f);
//                 targetSpeed = new Vector2(targetInputX, targetInputZ).magnitude;
//             }
//         }
//         else
//         {
//             if (isMoving)
//             {
//                 Quaternion targetRot = Quaternion.LookRotation(movementTranslation.normalized);
//                 transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeedUnarmed);

//                 targetInputX = 0f;
//                 targetInputZ = isRunning ? 1.0f : 0.5f;
//                 targetSpeed = targetInputZ;
//             }
//         }

//         smoothedInputX = Mathf.MoveTowards(smoothedInputX, targetInputX, Time.deltaTime * inputFilterSpeed);
//         smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, targetInputZ, Time.deltaTime * inputFilterSpeed);
//         UpdateAnimatorParams(targetSpeed);

//         if (!useBlendTree && !IsPlayingActionAnimation())
//         {
//             if (isMoving)
//             {
//                 string moveAnim = isRunning ? "run" : "Walk";
//                 PlayAnimationLocal(moveAnim, 0.1f);
//             }
//             else
//             {
//                 PlayAnimationLocal("Idle", 0.1f);
//             }
//         }

//         // Action Inputs with click attacking vs roll blocking
//         if (Input.GetMouseButtonDown(0))
//         {
//             if (!IsUIBlockingInput() && !isRollingStandalone && !IsPlayingActionAnimation())
//             {
//                 Debug.Log($"[LeoPlayer] Mouse clicked in Standalone. Weapon: {GetActiveWeaponIndex()}");
//                 PerformComboAttack(false);
//             }
//         }

//         if (Input.GetKeyDown(rollKey))
//         {
//             if (!IsUIBlockingInput() && rollCooldownTimer <= 0 && !IsPlayingAttackState(out _, out _))
//             {
//                 StartRollStandalone(move);
//             }
//         }
//     }

//     private void HandleOwnerUpdate()
//     {
//         bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
//                               (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);

//         if (isDialogueOpen)
//         {
//             smoothedInputX = Mathf.MoveTowards(smoothedInputX, 0f, Time.deltaTime * inputFilterSpeed);
//             smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, 0f, Time.deltaTime * inputFilterSpeed);
//             UpdateAnimatorParams(0f);
//             if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
//             if (!IsPlayingActionAnimation())
//             {
//                 if (!useBlendTree) PlayAnimationLocal("Idle", 0.1f);
//             }
//             return;
//         }

//         if (IsPlayingPickAnimation())
//         {
//             smoothedInputX = Mathf.MoveTowards(smoothedInputX, 0f, Time.deltaTime * inputFilterSpeed);
//             smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, 0f, Time.deltaTime * inputFilterSpeed);
//             UpdateAnimatorParams(0f);
//             if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
//             return;
//         }

//         if (isRollingStandalone || (IsSpawned && isRollingNet.Value))
//         {
//             if (rb != null)
//             {
//                 float currentYVelocity = rb.linearVelocity.y;
//                 rb.linearVelocity = new Vector3(rollDirection.x * rollSpeed, currentYVelocity, rollDirection.z * rollSpeed);
//             }
//             return;
//         }

//         bool isRunning = Input.GetKey(KeyCode.LeftShift);
//         float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;

//         float moveX = isMovementLocked ? 0f : Input.GetAxis("Horizontal");
//         float moveZ = isMovementLocked ? 0f : Input.GetAxis("Vertical");
//         Vector3 move = new Vector3(moveX, 0, moveZ);

//         if (targetCamera == null)
//         {
//             targetCamera = Camera.main;
//             if (targetCamera == null)
//                 targetCamera = FindAnyObjectByType<Camera>();
//         }

//         if (targetCamera != null)
//         {
//             Vector3 camForward = targetCamera.transform.forward;
//             camForward.y = 0f;
//             camForward.Normalize();
//             Vector3 camRight = targetCamera.transform.right;
//             camRight.y = 0f;
//             camRight.Normalize();
//             move = camRight * moveX + camForward * moveZ;
//         }

//         Vector3 movementTranslation = move;
//         if (isRootedAttack && IsPlayingActionAnimation())
//         {
//             movementTranslation = Vector3.zero;
//         }

//         // --- ĐÃ ĐỔI THÀNH RIGIDBODY TÍNH VẬN TỐC CHO ĐỒNG BỘ MULTIPLAYER ---
//         if (rb != null)
//         {
//             Vector3 targetVelocity = movementTranslation * currentSpeed;
//             float currentYVelocity = rb.linearVelocity.y;

//             if (knockbackVelocity.magnitude > 0.01f)
//             {
//                 targetVelocity += knockbackVelocity;
//                 knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
//             }

//             // Ưu tiên lực Dash đòn đánh
//             if (attackDashTimer > 0)
//             {
//                 rb.linearVelocity = new Vector3(attackDashDirection.x * attackDashSpeed, currentYVelocity, attackDashDirection.z * attackDashSpeed);
//             }
//             // Nếu khóa di chuyển, đứng im
//             else if (isMovementLocked && knockbackVelocity.magnitude <= 0.01f)
//             {
//                 rb.linearVelocity = new Vector3(0f, currentYVelocity, 0f);
//             }
//             else
//             {
//                 rb.linearVelocity = new Vector3(targetVelocity.x, currentYVelocity, targetVelocity.z);
//             }
//         }

//         bool isArmed = (GetActiveWeaponIndex() == 2);
//         bool isMoving = (movementTranslation != Vector3.zero);

//         float targetInputX = 0f;
//         float targetInputZ = 0f;
//         float targetSpeed = 0f;

//         bool shouldAlignToCamera = (isArmed && rotateToCameraWhenArmed) || (!isArmed && rotateToCameraWhenUnarmed);

//         if (shouldAlignToCamera)
//         {
//             if (targetCamera != null)
//             {
//                 Vector3 camForward = targetCamera.transform.forward;
//                 camForward.y = 0f;
//                 if (camForward.sqrMagnitude > 0.001f)
//                 {
//                     Quaternion targetRot = Quaternion.LookRotation(camForward.normalized);
//                     transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeedArmed);
//                 }
//             }

//             if (isMoving)
//             {
//                 targetInputX = moveX * (isRunning ? 1.0f : 0.5f);
//                 targetInputZ = moveZ * (isRunning ? 1.0f : 0.5f);
//                 targetSpeed = new Vector2(targetInputX, targetInputZ).magnitude;
//             }
//         }
//         else
//         {
//             if (isMoving)
//             {
//                 Quaternion targetRot = Quaternion.LookRotation(movementTranslation.normalized);
//                 transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeedUnarmed);

//                 targetInputX = 0f;
//                 targetInputZ = isRunning ? 1.0f : 0.5f;
//                 targetSpeed = targetInputZ;
//             }
//         }

//         smoothedInputX = Mathf.MoveTowards(smoothedInputX, targetInputX, Time.deltaTime * inputFilterSpeed);
//         smoothedInputZ = Mathf.MoveTowards(smoothedInputZ, targetInputZ, Time.deltaTime * inputFilterSpeed);
//         UpdateAnimatorParams(targetSpeed);

//         if (!useBlendTree && !IsPlayingActionAnimation())
//         {
//             if (isMoving)
//             {
//                 string moveAnim = isRunning ? "run" : "Walk";
//                 PlayAnimationLocal(moveAnim, 0.1f);
//             }
//             else
//             {
//                 PlayAnimationLocal("Idle", 0.1f);
//             }
//         }

//         if (Input.GetMouseButtonDown(0))
//         {
//             if (!IsUIBlockingInput() && IsSpawned)
//             {
//                 Debug.Log($"[LeoPlayer] Mouse clicked in Owner mode. Weapon: {GetActiveWeaponIndex()}");
//                 PerformComboAttack(true);
//             }
//         }

//         if (Input.GetKeyDown(rollKey))
//         {
//             if (!IsUIBlockingInput() && rollCooldownTimer <= 0 && IsSpawned && !IsPlayingAttackState(out _, out _))
//             {
//                 StartRollOwner(move);
//             }
//         }
//     }

//     private void UpdateAnimatorParams(float targetSpeed)
//     {
//         if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
//         {
//             anim.SetFloat(inputXParam, smoothedInputX);
//             anim.SetFloat(inputZParam, smoothedInputZ);

//             float currentSpeedVal = anim.GetFloat(speedParam);
//             float smoothedSpeed = Mathf.MoveTowards(currentSpeedVal, targetSpeed, Time.deltaTime * inputFilterSpeed);
//             anim.SetFloat(speedParam, smoothedSpeed);

//             bool isArmed = (GetActiveWeaponIndex() == 2);
//             anim.SetBool(isArmedParam, isArmed);
//         }
//     }

//     private float GetAttackDuration(int weaponIndex, int step)
//     {
//         if (weaponIndex == 1)
//         {
//             if (step == 1) return punch1Duration;
//             if (step == 2) return punch2Duration;
//             return punch3Duration;
//         }
//         else if (weaponIndex == 2)
//         {
//             if (step == 1) return slash1Duration;
//             if (step == 2) return slash2Duration;
//             return slash3Duration;
//         }
//         return 0.5f;
//     }

//     protected void PerformComboAttack(bool networkMode)
//     {
//         int weapon = GetActiveWeaponIndex();
//         float currentTime = Time.time;

//         // Reset hitbox states and clear target list for the new swing
//         alreadyHitEnemies.Clear();
//         DisableLeftHitbox();
//         DisableRightHitbox();
//         DisableLeftWeaponHitbox();
//         DisableRightWeaponHitbox();

//         if (weapon == 2)
//         {
//             // Armed sequential 3-step combo
//             int nextStep = comboStep;
//             if (currentTime - lastAttackTime > comboWindow)
//             {
//                 nextStep = 0;
//             }
//             nextStep++;

//             if (nextStep > 3) nextStep = 1;

//             if (comboStep > 0 && currentTime - lastAttackTime <= comboWindow)
//             {
//                 float prevDuration = GetAttackDuration(2, comboStep);
//                 if (currentTime - lastAttackTime < prevDuration * comboTransitionThreshold)
//                 {
//                     return;
//                 }
//             }

//             float moveX = Input.GetAxis("Horizontal");
//             float moveZ = Input.GetAxis("Vertical");
//             bool isMovingInput = (Mathf.Abs(moveX) > 0.01f || Mathf.Abs(moveZ) > 0.01f);

//             if (comboStep == 0 || currentTime - lastAttackTime > comboWindow)
//             {
//                 isRootedAttack = !isMovingInput;
//             }

//             comboStep = nextStep;
//             lastAttackTime = currentTime;

//             string animToPlay = "Slash1";
//             if (comboStep == 2) animToPlay = "Slash2";
//             else if (comboStep == 3) animToPlay = "Slash3";

//             Debug.Log($"[LeoPlayer] Sword attack (Armed). Step: {comboStep} -> Playing: {animToPlay}");

//             if (anim != null) anim.applyRootMotion = false;

//             // Kích hoạt Dash tiến lên bằng C# cho cả 3 đòn chém (Chỉ khi đứng im chém)
//             if (isRootedAttack)
//             {
//                 attackDashTimer = attackDashDuration;
//                 attackDashDirection = transform.forward;
//                 Debug.Log($"[LeoPlayer] Triggered slash {comboStep} forward dash by code.");
//             }

//             PlayAnimation(animToPlay, 0.05f, false);
//             // Damage is now processed through animation events enabling left/right hitboxes
//         }
//         else
//         {
//             // Unarmed combo Sequential punches - 3 steps
//             int nextStep = comboStep;
//             if (currentTime - lastAttackTime > comboWindow)
//             {
//                 nextStep = 0;
//             }
//             nextStep++;

//             if (nextStep > 3) nextStep = 1;

//             if (comboStep > 0 && currentTime - lastAttackTime <= comboWindow)
//             {
//                 float prevDuration = GetAttackDuration(1, comboStep);
//                 if (currentTime - lastAttackTime < prevDuration * comboTransitionThreshold)
//                 {
//                     return;
//                 }
//             }

//             // --- ĐÃ SỬA: Cho phép vừa di chuyển vừa đấm ---
//             isRootedAttack = false;

//             comboStep = nextStep;
//             lastAttackTime = currentTime;

//             // --- ĐÃ SỬA: Gỡ bỏ việc khóa di chuyển ngay lập tức ---
//             SetMovementLock(false);
//             Debug.Log("[LeoPlayer] Punch attack started: Movement ALLOWED.");

//             string animToPlay = "Punch1";
//             if (comboStep == 2) animToPlay = "Punch2";
//             else if (comboStep == 3) animToPlay = "Punch3";

//             Debug.Log($"[LeoPlayer] Fist attack (Unarmed). Step: {comboStep} -> Playing: {animToPlay}");

//             PlayAnimation(animToPlay, 0.05f, false);
//         }
//     }

//     private void TryDamageEnemy(Collider col)
//     {
//         var e1 = col.GetComponentInParent<Enemy1_DapBua>();
//         if (e1 != null) { e1.TakeDamage(damageAmount); return; }

//         var e2 = col.GetComponentInParent<Enemy2_Zombie>();
//         if (e2 != null) { e2.TakeDamage(damageAmount); return; }

//         var e3 = col.GetComponentInParent<Enemy3_Buaa>();
//         if (e3 != null) { e3.TakeDamage(damageAmount); return; }

//         var e4 = col.GetComponentInParent<Enemy4_Bongtoi>();
//         if (e4 != null) { e4.TakeDamage(damageAmount); return; }

//         var e5 = col.GetComponentInParent<Enemy5_PhuThuy>();
//         if (e5 != null) { e5.TakeDamage(damageAmount); return; }
//     }

//     [ServerRpc]
//     protected void AttackServerRpc()
//     {
//         Vector3 rayStart = transform.position + Vector3.up * 0.5f;
//         if (Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange))
//         {
//             var e1 = hit.collider.GetComponentInParent<Enemy1_DapBua>();
//             if (e1 != null) { e1.TakeDamage(damageAmount); return; }

//             var e2 = hit.collider.GetComponentInParent<Enemy2_Zombie>();
//             if (e2 != null) { e2.TakeDamage(damageAmount); return; }

//             var e3 = hit.collider.GetComponentInParent<Enemy3_Buaa>();
//             if (e3 != null) { e3.TakeDamage(damageAmount); return; }

//             var e4 = hit.collider.GetComponentInParent<Enemy4_Bongtoi>();
//             if (e4 != null) { e4.TakeDamage(damageAmount); return; }

//             var e5 = hit.collider.GetComponentInParent<Enemy5_PhuThuy>();
//             if (e5 != null) { e5.TakeDamage(damageAmount); return; }
//         }
//     }

//     protected void StartRollStandalone(Vector3 moveInput)
//     {
//         isRollingStandalone = true;
//         rollTimer = rollDuration;
//         rollCooldownTimer = rollCooldown;

//         ClearAttackLayer();

//         if (moveInput != Vector3.zero)
//         {
//             rollDirection = moveInput.normalized;
//             transform.forward = rollDirection;
//         }
//         else
//         {
//             rollDirection = transform.forward;
//         }

//         if (anim != null) anim.applyRootMotion = false;
//         PlayAnimation("LonVong", 0.05f);
//     }

//     protected void StartRollOwner(Vector3 moveInput)
//     {
//         isRollingStandalone = true; // Kích hoạt di chuyển tức thời trên client chủ sở hữu (Client-side Prediction)
//         rollTimer = rollDuration;
//         rollCooldownTimer = rollCooldown;

//         ClearAttackLayer();

//         if (moveInput != Vector3.zero)
//         {
//             rollDirection = moveInput.normalized;
//             transform.forward = rollDirection;
//         }
//         else
//         {
//             rollDirection = transform.forward;
//         }

//         if (anim != null) anim.applyRootMotion = false;
//         PlayAnimation("LonVong", 0.05f, false);
//         StartRollServerRpc(rollDirection);
//     }

//     [ServerRpc]
//     private void StartRollServerRpc(Vector3 direction)
//     {
//         SyncNetVarBool(isRollingNet, proxyPlayerTest != null ? proxyPlayerTest.isRollingNet : null, true);
//         PlayAnimationClientRpc("LonVong", 0.05f, true);
//     }

//     [ServerRpc]
//     protected void StopRollServerRpc()
//     {
//         SyncNetVarBool(isRollingNet, proxyPlayerTest != null ? proxyPlayerTest.isRollingNet : null, false);
//     }

//     private void LateUpdate()
//     {
//         bool shouldFollow = isStandaloneMode || (IsSpawned && IsOwner);
//         if (!shouldFollow || !enableCameraFollow) return;

//         if (targetCamera == null)
//         {
//             targetCamera = Camera.main;
//             if (targetCamera == null)
//                 targetCamera = FindAnyObjectByType<Camera>();
//         }

//         if (targetCamera != null)
//         {
//             if (isCursorLocked)
//             {
//                 float mouseX = Input.GetAxis("Mouse X");
//                 float mouseY = Input.GetAxis("Mouse Y");
//                 targetYaw -= mouseX * cameraSensitivity;
//                 targetPitch += mouseY * cameraSensitivity;
//                 targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
//             }

//             currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * rotationSmoothSpeed);
//             currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * rotationSmoothSpeed);

//             float yawRad = currentYaw * Mathf.Deg2Rad;
//             float pitchRad = currentPitch * Mathf.Deg2Rad;

//             Vector3 rotatedOffset = new Vector3(
//                 cameraDistance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
//                 cameraDistance * Mathf.Sin(pitchRad),
//                 -cameraDistance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
//             );

//             Vector3 targetPosition = (transform.position + Vector3.up * cameraPivotHeight) + rotatedOffset;
//             targetCamera.transform.position = targetPosition;

//             if (cameraLookAtPlayer)
//             {
//                 targetCamera.transform.rotation = Quaternion.LookRotation(
//                     (transform.position + Vector3.up * cameraPivotHeight) - targetCamera.transform.position
//                 );
//             }
//         }
//     }

//     // Health, DB, stats and durability
//     public float Weapon1Durability
//     {
//         get { return isStandaloneMode ? localWeapon1Durability : weapon1Durability.Value; }
//         set
//         {
//             float clamped = Mathf.Clamp(value, 0f, weapon1MaxDurability);
//             if (isStandaloneMode)
//             {
//                 localWeapon1Durability = clamped;
//                 UpdateDurabilityHUD();
//             }
//             else if (IsServer)
//             {
//                 SyncNetVarFloat(weapon1Durability, proxyPlayerTest != null ? proxyPlayerTest.weapon1Durability : null, clamped);
//             }
//         }
//     }

//     public float Weapon2Durability
//     {
//         get { return isStandaloneMode ? localWeapon2Durability : weapon2Durability.Value; }
//         set
//         {
//             float clamped = Mathf.Clamp(value, 0f, weapon2MaxDurability);
//             if (isStandaloneMode)
//             {
//                 localWeapon2Durability = clamped;
//                 UpdateDurabilityHUD();
//             }
//             else if (IsServer)
//             {
//                 SyncNetVarFloat(weapon2Durability, proxyPlayerTest != null ? proxyPlayerTest.weapon2Durability : null, clamped);
//             }
//         }
//     }

//     public void TakeDamage(float damage)
//     {
//         bool isRolling = isStandaloneMode ? isRollingStandalone : isRollingNet.Value;
//         if (isRolling)
//         {
//             Debug.Log($"[LeoPlayer] {gameObject.name} is dodging/rolling, immune to damage!");
//             return;
//         }

//         SetMovementLock(false); // Giải phóng khóa di chuyển khi bị trúng đòn

//         if (isStandaloneMode)
//         {
//             localHealth = Mathf.Max(localHealth - damage, 0f);
//             UpdateHealthHUD(localHealth);
//             Debug.Log($"[LeoPlayer Standalone] Recieved {damage} DMG. Health: {localHealth}");
//             if (localHealth <= 0)
//             {
//                 if (rb != null) rb.linearVelocity = Vector3.zero;
//                 PlayAnimation("Death", 0.15f);
//             }
//             else
//             {
//                 string hitAnim = Random.value < 0.5f ? "GetHit" : "GeiHit2";
//                 PlayAnimation(hitAnim, 0.05f);
//             }
//             return;
//         }

//         if (!IsServer) return;

//         float finalHp = Mathf.Max(currentHealth.Value - damage, 0f);
//         SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, finalHp);
//         Debug.Log($"[LeoPlayer Server] Client {OwnerClientId} recieved {damage} DMG. Health: {currentHealth.Value}");

//         if (currentHealth.Value <= 0)
//         {
//             if (rb != null) rb.linearVelocity = Vector3.zero;
//             PlayAnimation("Death", 0.15f);
//         }
//         else
//         {
//             string hitAnim = Random.value < 0.5f ? "GetHit" : "GeiHit2";
//             PlayAnimation(hitAnim, 0.05f);
//         }
//     }

//     public void ApplyKnockback(Vector3 force)
//     {
//         if (isStandaloneMode)
//         {
//             knockbackVelocity = force;
//             return;
//         }

//         if (!IsServer) return;
//         ApplyKnockbackClientRpc(force);
//     }

//     [ClientRpc]
//     public void ApplyKnockbackClientRpc(Vector3 force)
//     {
//         if (IsOwner)
//             knockbackVelocity = force;
//     }

//     public bool TryAddItem(string itemName, bool playAnimation = true)
//     {
//         for (int i = 0; i < inventorySlots.Length; i++)
//         {
//             string slotVal = inventorySlots[i];
//             if (!string.IsNullOrEmpty(slotVal))
//             {
//                 string name = slotVal;
//                 int count = 1;
//                 if (slotVal.Contains(":"))
//                 {
//                     var parts = slotVal.Split(':');
//                     name = parts[0];
//                     int.TryParse(parts[1], out count);
//                 }

//                 if (name == itemName)
//                 {
//                     inventorySlots[i] = name + ":" + (count + 1);
//                     PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
//                     if (hud != null) hud.SetInventorySlots(inventorySlots);
//                     if (!isStandaloneMode) SavePlayerStateToDatabase();
//                     if (playAnimation) PlayAnimation("Idle_Pick", 0.1f);
//                     return true;
//                 }
//             }
//         }

//         for (int i = 0; i < inventorySlots.Length; i++)
//         {
//             if (string.IsNullOrEmpty(inventorySlots[i]))
//             {
//                 inventorySlots[i] = itemName + ":1";
//                 PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
//                 if (hud != null) hud.SetInventorySlots(inventorySlots);
//                 if (!isStandaloneMode) SavePlayerStateToDatabase();
//                 if (playAnimation) PlayAnimation("Idle_Pick", 0.1f);
//                 return true;
//             }
//         }
//         return false;
//     }

//     public void AddExperience(float amount)
//     {
//         if (isStandaloneMode)
//         {
//             localExp += amount;
//             float needed = 100f + localLevel * 50f;
//             while (localExp >= needed)
//             {
//                 localExp -= needed;
//                 localLevel++;
//                 needed = 100f + localLevel * 50f;
//                 localUpgradePoints++;
//             }
//             PlayerPrefs.SetInt("SelectedPlayerLevel_" + characterClassIndex, localLevel);
//             PlayerPrefs.SetFloat("SelectedPlayerExp_" + characterClassIndex, localExp);
//             PlayerPrefs.Save();
//             UpdateUpgradeHUD();
//         }
//         else if (IsServer)
//         {
//             float finalExp = playerExp.Value + amount;
//             int finalLevel = playerLevel.Value;
//             int finalPoints = upgradePoints.Value;
//             float needed = 100f + finalLevel * 50f;

//             while (finalExp >= needed)
//             {
//                 finalExp -= needed;
//                 finalLevel++;
//                 needed = 100f + finalLevel * 50f;
//                 finalPoints++;
//             }

//             SyncNetVarFloat(playerExp, proxyPlayerTest != null ? proxyPlayerTest.playerExp : null, finalExp);
//             SyncNetVarInt(playerLevel, proxyPlayerTest != null ? proxyPlayerTest.playerLevel : null, finalLevel);
//             SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, finalPoints);
//             SavePlayerStateClientRpc();
//         }
//     }

//     public void StandaloneUpgradeStat(int statType)
//     {
//         if (localUpgradePoints <= 0) return;
//         localUpgradePoints--;
//         switch (statType)
//         {
//             case 0: localHpLevel++; break;
//             case 1: localMpLevel++; break;
//             case 2: localCooldownLevel++; break;
//             case 3: localDamageLevel++; break;
//         }
//         ApplyUpgradedStats();
//         UpdateUpgradeHUD();
//     }

//     public void UpgradeStatFromHUD(int statType)
//     {
//         if (!IsSpawned || !IsOwner) return;
//         UpgradeStatServerRpc(statType);
//     }

//     [ServerRpc]
//     private void UpgradeStatServerRpc(int statType)
//     {
//         if (upgradePoints.Value <= 0) return;

//         int pts = upgradePoints.Value - 1;
//         SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, pts);

//         switch (statType)
//         {
//             case 0: SyncNetVarInt(hpLevel, proxyPlayerTest != null ? proxyPlayerTest.hpLevel : null, hpLevel.Value + 1); break;
//             case 1: SyncNetVarInt(mpLevel, proxyPlayerTest != null ? proxyPlayerTest.mpLevel : null, mpLevel.Value + 1); break;
//             case 2: SyncNetVarInt(cooldownLevel, proxyPlayerTest != null ? proxyPlayerTest.cooldownLevel : null, cooldownLevel.Value + 1); break;
//             case 3: SyncNetVarInt(damageLevel, proxyPlayerTest != null ? proxyPlayerTest.damageLevel : null, damageLevel.Value + 1); break;
//         }

//         ApplyUpgradedStats();
//         SavePlayerStateClientRpc();
//     }

//     private void ApplyUpgradedStats()
//     {
//         if (isSyncingFromDb) return;

//         int hpLv = isStandaloneMode ? localHpLevel : hpLevel.Value;
//         int dmgLv = isStandaloneMode ? localDamageLevel : damageLevel.Value;

//         float oldMaxHealth = maxHealth;
//         maxHealth = 85f + hpLv * 20f;
//         damageAmount = 25f + dmgLv * 5f;

//         if (isStandaloneMode)
//         {
//             if (maxHealth > oldMaxHealth) localHealth += (maxHealth - oldMaxHealth);
//             UpdateHealthHUD(localHealth);
//         }
//         else if (IsServer)
//         {
//             if (maxHealth > oldMaxHealth)
//             {
//                 float finalHp = currentHealth.Value + (maxHealth - oldMaxHealth);
//                 SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, finalHp);
//             }
//         }
//     }

//     private void UpdateUpgradeHUD()
//     {
//         PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//         if (hud != null)
//         {
//             int pts = isStandaloneMode ? localUpgradePoints : upgradePoints.Value;
//             int hp = isStandaloneMode ? localHpLevel : hpLevel.Value;
//             int mp = isStandaloneMode ? localMpLevel : mpLevel.Value;
//             int cd = isStandaloneMode ? localCooldownLevel : cooldownLevel.Value;
//             int dmg = isStandaloneMode ? localDamageLevel : damageLevel.Value;

//             hud.UpdateUpgradeUI(pts, hp, mp, cd, dmg);
//             hud.SetHealth(CurrentHealth / maxHealth);

//             int lv = isStandaloneMode ? localLevel : playerLevel.Value;
//             float xp = isStandaloneMode ? localExp : playerExp.Value;
//             float needed = 100f + lv * 50f;
//             hud.UpdateExperienceUI(lv, xp, needed);
//         }
//     }

//     public void UpdateDurabilityHUD()
//     {
//         PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//         if (hud != null)
//         {
//             float percent1 = (isStandaloneMode ? localWeapon1Durability : weapon1Durability.Value) / weapon1MaxDurability;
//             float percent2 = (isStandaloneMode ? localWeapon2Durability : weapon2Durability.Value) / weapon2MaxDurability;
//             hud.SetWeaponDurability(1, percent1);
//             hud.SetWeaponDurability(2, percent2);
//         }
//     }

//     public void UpdateStateFromHUD(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
//     {
//         if (!IsSpawned || !IsOwner) return;
//         UpdateStateServerRpc(weaponIndex, weapon2Locked, skillsUnlocked);
//     }

//     [ServerRpc]
//     private void UpdateStateServerRpc(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
//     {
//         SyncNetVarInt(activeWeaponIndex, proxyPlayerTest != null ? proxyPlayerTest.activeWeaponIndex : null, weaponIndex);
//         SyncNetVarBool(isWeapon2Locked, proxyPlayerTest != null ? proxyPlayerTest.isWeapon2Locked : null, weapon2Locked);
//         SyncNetVarBool(isSkillsUnlocked, proxyPlayerTest != null ? proxyPlayerTest.isSkillsUnlocked : null, skillsUnlocked);
//         SavePlayerStateClientRpc();
//     }

//     public async void SavePlayerStateToDatabase()
//     {
//         if (!IsSpawned || !IsOwner) return;

//         Debug.Log("[LeoPlayer DB] Saving character state to MongoDB...");
//         try
//         {
//             var stateData = new PlayerStateData
//             {
//                 health = CurrentHealth,
//                 activeWeaponIndex = activeWeaponIndex.Value,
//                 isWeapon2Locked = isWeapon2Locked.Value,
//                 isSkillsUnlocked = isSkillsUnlocked.Value,
//                 inventorySlots = inventorySlots,
//                 upgradePoints = upgradePoints.Value,
//                 hpLevel = hpLevel.Value,
//                 mpLevel = mpLevel.Value,
//                 cooldownLevel = cooldownLevel.Value,
//                 damageLevel = damageLevel.Value,
//                 playerLevel = playerLevel.Value,
//                 playerExp = playerExp.Value
//             };

//             var res = await AuthService.SavePlayerState(stateData);
//             if (res != null && res.success)
//             {
//                 Debug.Log("[LeoPlayer DB] Saved state to MongoDB successfully!");
//             }
//         }
//         catch (System.Exception ex)
//         {
//             Debug.LogError($"[LeoPlayer DB] Error saving MongoDB state: {ex.Message}");
//         }
//     }

//     private async void LoadPlayerStateFromDatabase()
//     {
//         Debug.Log("[LeoPlayer DB] Loading player state from MongoDB Atlas...");
//         try
//         {
//             var res = await AuthService.GetPlayerState();
//             if (res != null && res.success && res.playerState != null)
//             {
//                 var state = res.playerState;
//                 isSyncingFromDb = true;

//                 SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, state.upgradePoints);
//                 SyncNetVarInt(hpLevel, proxyPlayerTest != null ? proxyPlayerTest.hpLevel : null, state.hpLevel);
//                 SyncNetVarInt(mpLevel, proxyPlayerTest != null ? proxyPlayerTest.mpLevel : null, state.mpLevel);
//                 SyncNetVarInt(cooldownLevel, proxyPlayerTest != null ? proxyPlayerTest.cooldownLevel : null, state.cooldownLevel);
//                 SyncNetVarInt(damageLevel, proxyPlayerTest != null ? proxyPlayerTest.damageLevel : null, state.damageLevel);
//                 SyncNetVarInt(playerLevel, proxyPlayerTest != null ? proxyPlayerTest.playerLevel : null, state.playerLevel);
//                 SyncNetVarFloat(playerExp, proxyPlayerTest != null ? proxyPlayerTest.playerExp : null, state.playerExp);

//                 maxHealth = 85f + state.hpLevel * 20f;
//                 damageAmount = 25f + state.damageLevel * 5f;

//                 SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, state.health);
//                 SyncNetVarInt(activeWeaponIndex, proxyPlayerTest != null ? proxyPlayerTest.activeWeaponIndex : null, state.activeWeaponIndex);
//                 SyncNetVarBool(isWeapon2Locked, proxyPlayerTest != null ? proxyPlayerTest.isWeapon2Locked : null, true);
//                 SyncNetVarBool(isSkillsUnlocked, proxyPlayerTest != null ? proxyPlayerTest.isSkillsUnlocked : null, false);

//                 isSyncingFromDb = false;

//                 if (state.inventorySlots != null)
//                 {
//                     for (int i = 0; i < inventorySlots.Length && i < state.inventorySlots.Length; i++)
//                     {
//                         inventorySlots[i] = state.inventorySlots[i];
//                     }
//                 }

//                 PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//                 if (hud != null)
//                 {
//                     hud.SetInventorySlots(inventorySlots);
//                     hud.SetSkillsUnlocked(false, false);
//                     hud.SetWeapon2Locked(true, false);
//                     hud.SelectWeapon(state.activeWeaponIndex);
//                     hud.UpdateUpgradeUI(state.upgradePoints, state.hpLevel, state.mpLevel, state.cooldownLevel, state.damageLevel);
//                     hud.SetHealth(state.health / (85f + state.hpLevel * 20f));

//                     float needed = 100f + state.playerLevel * 50f;
//                     hud.UpdateExperienceUI(state.playerLevel, state.playerExp, needed);
//                 }
//             }
//             else
//             {
//                 SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, maxHealth);
//                 SyncNetVarInt(activeWeaponIndex, proxyPlayerTest != null ? proxyPlayerTest.activeWeaponIndex : null, 1);
//                 SyncNetVarBool(isWeapon2Locked, proxyPlayerTest != null ? proxyPlayerTest.isWeapon2Locked : null, true);
//                 SyncNetVarBool(isSkillsUnlocked, proxyPlayerTest != null ? proxyPlayerTest.isSkillsUnlocked : null, false);
//                 SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, 5);
//                 SyncNetVarInt(hpLevel, proxyPlayerTest != null ? proxyPlayerTest.hpLevel : null, 0);
//                 SyncNetVarInt(mpLevel, proxyPlayerTest != null ? proxyPlayerTest.mpLevel : null, 0);
//                 SyncNetVarInt(cooldownLevel, proxyPlayerTest != null ? proxyPlayerTest.cooldownLevel : null, 0);
//                 SyncNetVarInt(damageLevel, proxyPlayerTest != null ? proxyPlayerTest.damageLevel : null, 0);
//                 SyncNetVarInt(playerLevel, proxyPlayerTest != null ? proxyPlayerTest.playerLevel : null, 0);
//                 SyncNetVarFloat(playerExp, proxyPlayerTest != null ? proxyPlayerTest.playerExp : null, 0f);
//                 SavePlayerStateToDatabase();
//             }
//         }
//         catch (System.Exception ex)
//         {
//             Debug.LogError($"[LeoPlayer DB] Error loading state: {ex.Message}");
//         }
//     }

//     [ClientRpc]
//     private void SavePlayerStateClientRpc()
//     {
//         if (IsOwner)
//         {
//             SavePlayerStateToDatabase();
//         }
//     }

//     // Callbacks for local HUD state changes when NetworkVariables sync from Server to Owner Client
//     private void OnWeaponIndexChanged(int oldVal, int newVal)
//     {
//         if (IsOwner)
//         {
//             PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//             if (hud != null) hud.SelectWeapon(newVal);
//         }
//         PlayWeaponSwitchAnimation(oldVal, newVal);
//     }

//     private void OnWeapon2LockedChanged(bool oldVal, bool newVal)
//     {
//         if (IsOwner)
//         {
//             PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//             if (hud != null) hud.SetWeapon2Locked(newVal, true);
//         }
//     }

//     private void OnSkillsUnlockedChanged(bool oldVal, bool newVal)
//     {
//         if (IsOwner)
//         {
//             PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//             if (hud != null) hud.SetSkillsUnlocked(newVal, true);
//         }
//     }

//     private void OnHealthChanged(float oldHealth, float newHealth)
//     {
//         UpdateHealthHUD(newHealth);
//         if (IsOwner)
//         {
//             SavePlayerStateToDatabase();
//         }
//     }

//     private void UpdateHealthHUD(float health)
//     {
//         PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
//         if (hud != null)
//             hud.SetHealth(health / maxHealth);
//     }

//     private void OnUpgradePointsChanged(int oldVal, int newVal)
//     {
//         if (IsOwner) UpdateUpgradeHUD();
//     }

//     private void OnHpLevelChanged(int oldVal, int newVal)
//     {
//         if (IsServer || IsOwner) ApplyUpgradedStats();
//         if (IsOwner) UpdateUpgradeHUD();
//     }

//     private void OnMpLevelChanged(int oldVal, int newVal)
//     {
//         if (IsServer || IsOwner) ApplyUpgradedStats();
//         if (IsOwner) UpdateUpgradeHUD();
//     }

//     private void OnCooldownLevelChanged(int oldVal, int newVal)
//     {
//         if (IsServer || IsOwner) ApplyUpgradedStats();
//         if (IsOwner) UpdateUpgradeHUD();
//     }

//     private void OnDamageLevelChanged(int oldVal, int newVal)
//     {
//         if (IsServer || IsOwner) ApplyUpgradedStats();
//         if (IsOwner) UpdateUpgradeHUD();
//     }

//     private void OnLevelOrExpChanged(int oldVal, int newVal)
//     {
//         if (IsOwner) UpdateUpgradeHUD();
//     }

//     private void OnLevelOrExpChanged(float oldVal, float newVal)
//     {
//         if (IsOwner) UpdateUpgradeHUD();
//     }

//     private void OnDurabilityChanged(float oldVal, float newVal)
//     {
//         if (IsOwner) UpdateDurabilityHUD();
//     }

//     // Animation Management
//     public void PlayWeaponSwitchAnimation(int oldWeapon, int newWeapon)
//     {
//         if (oldWeapon == newWeapon) return;

//         isSwitchingWeapon = true; // Khóa chống spam phím khi đổi vũ khí

//         if (newWeapon == 2)
//         {
//             // Bắt đầu rút kiếm: lúc này kiếm vẫn ở trên vai, tay chưa cầm kiếm
//             if (leftShoulderSword != null) leftShoulderSword.SetActive(true);
//             if (rightShoulderSword != null) rightShoulderSword.SetActive(true);
//             if (leftHandSword != null) leftHandSword.SetActive(false);
//             if (rightHandSword != null) rightHandSword.SetActive(false);

//             if (!string.IsNullOrEmpty(drawLeftTrigger))
//             {
//                 PlayAnimation(drawLeftTrigger, 0.1f);
//             }
//             else if (!string.IsNullOrEmpty(drawWeaponTrigger))
//             {
//                 PlayAnimation(drawWeaponTrigger, 0.1f);
//             }
//             else
//             {
//                 SyncWeaponVisuals(newWeapon);
//                 OnWeaponSwitchEnd();
//             }
//         }
//         else if (newWeapon == 1)
//         {
//             // Bắt đầu cất kiếm: lúc này kiếm vẫn ở trên tay, chưa cất lên vai
//             if (leftHandSword != null) leftHandSword.SetActive(true);
//             if (rightHandSword != null) rightHandSword.SetActive(true);
//             if (leftShoulderSword != null) leftShoulderSword.SetActive(false);
//             if (rightShoulderSword != null) rightShoulderSword.SetActive(false);

//             if (!string.IsNullOrEmpty(sheatheLeftTrigger))
//             {
//                 PlayAnimation(sheatheLeftTrigger, 0.1f);
//             }
//             else if (!string.IsNullOrEmpty(sheathWeaponTrigger))
//             {
//                 PlayAnimation(sheathWeaponTrigger, 0.1f);
//             }
//             else
//             {
//                 SyncWeaponVisuals(newWeapon);
//                 OnWeaponSwitchEnd();
//             }
//         }
//     }

//     public void OnDrawLeftEnd()
//     {
//         // Để Animator tự động chuyển sang hoạt ảnh tay phải bằng mũi tên transition (Has Exit Time)
//         Debug.Log("[LeoPlayer] Draw Left finished. Letting Animator transition natively to Draw Right.");
//     }

//     public void OnSheatheLeftEnd()
//     {
//         // Để Animator tự động chuyển sang hoạt ảnh tay phải bằng mũi tên transition (Has Exit Time)
//         Debug.Log("[LeoPlayer] Sheathe Left finished. Letting Animator transition natively to Sheathe Right.");
//     }

//     public void DrawLeftSword()
//     {
//         if (leftHandSword != null) leftHandSword.SetActive(true);
//         if (leftShoulderSword != null) leftShoulderSword.SetActive(false);
//         Debug.Log("[LeoPlayer] Left sword DRAWN.");
//     }

//     public void DrawRightSword()
//     {
//         if (rightHandSword != null) rightHandSword.SetActive(true);
//         if (rightShoulderSword != null) rightShoulderSword.SetActive(false);
//         Debug.Log("[LeoPlayer] Right sword DRAWN.");
//     }

//     public void SheatheLeftSword()
//     {
//         if (leftHandSword != null) leftHandSword.SetActive(false);
//         if (leftShoulderSword != null) leftShoulderSword.SetActive(true);
//         Debug.Log("[LeoPlayer] Left sword SHEATHED.");
//     }

//     public void SheatheRightSword()
//     {
//         if (rightHandSword != null) rightHandSword.SetActive(false);
//         if (rightShoulderSword != null) rightShoulderSword.SetActive(true);
//         Debug.Log("[LeoPlayer] Right sword SHEATHED.");
//     }

//     public void SyncWeaponVisuals(int activeWeapon)
//     {
//         bool isArmed = (activeWeapon == 2);

//         if (leftHandSword != null) leftHandSword.SetActive(isArmed);
//         if (rightHandSword != null) rightHandSword.SetActive(isArmed);

//         if (leftShoulderSword != null) leftShoulderSword.SetActive(!isArmed);
//         if (rightShoulderSword != null) rightShoulderSword.SetActive(!isArmed);
//     }

//     public void OnWeaponSwitchEnd()
//     {
//         isSwitchingWeapon = false;
//         Debug.Log("[LeoPlayer] Weapon switch animation finished. Lock released.");
//     }

//     [ContextMenu("Auto Find Sword Meshes")]
//     public void AutoFindSwordMeshes()
//     {
//         // 1. Tìm trên tay
//         Transform leftHand = FindBoneRecursive(transform, "left");
//         Transform rightHand = FindBoneRecursive(transform, "right");

//         if (leftHand != null)
//         {
//             Transform leftSword = FindWeaponTransform(leftHand);
//             if (leftSword != null) leftHandSword = leftSword.gameObject;
//         }
//         if (rightHand != null)
//         {
//             Transform rightSword = FindWeaponTransform(rightHand);
//             if (rightSword != null) rightHandSword = rightSword.gameObject;
//         }

//         // 2. Tìm trên vai (Spine, Chest, Shoulder)
//         Transform spine = FindBoneRecursiveLower(transform, "spine");
//         Transform chest = FindBoneRecursiveLower(transform, "chest");
//         Transform upperBody = chest ?? spine ?? transform;

//         Transform leftSh = FindShoulderSwordRecursive(upperBody, "left");
//         Transform rightSh = FindShoulderSwordRecursive(upperBody, "right");

//         if (leftSh != null) leftShoulderSword = leftSh.gameObject;
//         if (rightSh != null) rightShoulderSword = rightSh.gameObject;

//         Debug.Log($"[LeoPlayer Editor] Auto Find Results -> Hand L: {leftHandSword?.name}, Hand R: {rightHandSword?.name}, Shoulder L: {leftShoulderSword?.name}, Shoulder R: {rightShoulderSword?.name}");
//     }

//     private Transform FindBoneRecursiveLower(Transform current, string keyword)
//     {
//         string nameLower = current.name.ToLower();
//         if (nameLower.Contains(keyword))
//         {
//             return current;
//         }
//         for (int i = 0; i < current.childCount; i++)
//         {
//             Transform found = FindBoneRecursiveLower(current.GetChild(i), keyword);
//             if (found != null) return found;
//         }
//         return null;
//     }

//     private Transform FindShoulderSwordRecursive(Transform current, string side)
//     {
//         string nameLower = current.name.ToLower();
//         if ((nameLower.Contains("sword") || nameLower.Contains("blade") || nameLower.Contains("kiem") || nameLower.Contains("dao") || nameLower.Contains("katana") || nameLower.Contains("weapon")) &&
//             nameLower.Contains(side) &&
//             (nameLower.Contains("shoulder") || nameLower.Contains("back") || nameLower.Contains("sheath") || nameLower.Contains("mount") || nameLower.Contains("holder") || nameLower.Contains("holster")))
//         {
//             return current;
//         }
//         if (nameLower.Contains(side) && (nameLower.Contains("sheath") || nameLower.Contains("mount") || nameLower.Contains("holder") || nameLower.Contains("holster")))
//         {
//             return current;
//         }
//         for (int i = 0; i < current.childCount; i++)
//         {
//             Transform found = FindShoulderSwordRecursive(current.GetChild(i), side);
//             if (found != null) return found;
//         }
//         return null;
//     }

//     public void PlayAnimation(string animName, float fadeTime = 0.1f, bool alreadyPlayedLocally = false)
//     {
//         if (anim == null) return;

//         if (!alreadyPlayedLocally)
//         {
//             PlayAnimationLocal(animName, fadeTime);
//         }

//         if (!isStandaloneMode)
//         {
//             if (IsServer)
//             {
//                 PlayAnimationClientRpc(animName, fadeTime, alreadyPlayedLocally);
//             }
//             else if (IsOwner)
//             {
//                 PlayAnimationServerRpc(animName, fadeTime);
//             }
//         }
//     }

//     private string TranslateAnimName(string animName)
//     {
//         int weapon = GetActiveWeaponIndex();
//         bool isArmed = (weapon == 2);

//         float verticalInput = Input.GetAxis("Vertical");
//         float horizontalInput = Input.GetAxis("Horizontal");

//         bool isMovingBackward = (verticalInput < -0.1f);
//         bool isMovingLeft = (horizontalInput < -0.1f);
//         bool isMovingRight = (horizontalInput > 0.1f);

//         switch (animName)
//         {
//             case "Idle":
//                 return isArmed ? idleArmed : idleUnarmed;
//             case "Walk":
//                 if (isArmed)
//                 {
//                     if (isMovingBackward) return walkBackwardArmed;
//                     if (isMovingLeft) return walkLeftArmed;
//                     if (isMovingRight) return walkRightArmed;
//                     return walkForwardArmed;
//                 }
//                 else
//                 {
//                     if (isMovingBackward) return walkBackwardUnarmed;
//                     if (isMovingLeft) return walkLeftUnarmed;
//                     if (isMovingRight) return walkRightUnarmed;
//                     return walkUnarmed;
//                 }
//             case "run":
//                 if (isArmed)
//                 {
//                     if (isMovingBackward) return runBackwardArmed;
//                     if (isMovingLeft) return runLeftArmed;
//                     if (isMovingRight) return runRightArmed;
//                     return runForwardArmed;
//                 }
//                 else
//                 {
//                     if (isMovingBackward) return runBackwardUnarmed;
//                     if (isMovingLeft) return runLeftUnarmed;
//                     if (isMovingRight) return runRightUnarmed;
//                     return runUnarmed;
//                 }
//             case "LonVong":
//                 return rollTrigger;
//             case "Punch1":
//                 return punch1Trigger;
//             case "Punch2":
//                 return punch2Trigger;
//             case "Punch3":
//                 return punch3Trigger;
//             case "Slash1":
//                 return slash1Trigger;
//             case "Slash2":
//                 return slash2Trigger;
//             case "GetHit":
//                 return getHitTrigger;
//             case "GeiHit2":
//                 return getHit2Trigger;
//             case "Death":
//                 return isArmed ? deathArmedTrigger : deathUnarmedTrigger;
//             case "Idle_Pick":
//             case "Pick":
//                 return pickTrigger;
//             default:
//                 return animName;
//         }
//     }

//     private bool IsActionAnimationName(string name)
//     {
//         return name == rollTrigger ||
//                name == getHitTrigger ||
//                name == getHit2Trigger ||
//                name == pickTrigger ||
//                name == deathUnarmedTrigger ||
//                name == deathArmedTrigger ||
//                name == punch1Trigger ||
//                name == punch2Trigger ||
//                name == punch3Trigger ||
//                name == slash1Trigger ||
//                name == slash2Trigger ||
//                name == "LonVong" ||
//                name == "GetHit" ||
//                name == "GeiHit2" ||
//                name == "Idle_Pick" ||
//                name == "Death" ||
//                name == "Punch1" ||
//                name == "Punch2" ||
//                name == "Punch3" ||
//                name == "Slash1" ||
//                name == "Slash2" ||
//                (!string.IsNullOrEmpty(drawWeaponTrigger) && name == drawWeaponTrigger) ||
//                (!string.IsNullOrEmpty(sheathWeaponTrigger) && name == sheathWeaponTrigger) ||
//                (!string.IsNullOrEmpty(drawLeftTrigger) && name == drawLeftTrigger) ||
//                (!string.IsNullOrEmpty(drawRightTrigger) && name == drawRightTrigger) ||
//                (!string.IsNullOrEmpty(sheatheLeftTrigger) && name == sheatheLeftTrigger) ||
//                (!string.IsNullOrEmpty(sheatheRightTrigger) && name == sheatheRightTrigger);
//     }

//     private bool IsAttackAnimationName(string name)
//     {
//         return name == punch1Trigger ||
//                name == punch2Trigger ||
//                name == punch3Trigger ||
//                name == slash1Trigger ||
//                name == slash2Trigger ||
//                name == "Punch1" ||
//                name == "Punch2" ||
//                name == "Punch3" ||
//                name == "Slash1" ||
//                name == "Slash2";
//     }

//     private bool IsFullBodyActionAnimation(string name)
//     {
//         return name == rollTrigger ||
//                name == getHitTrigger ||
//                name == getHit2Trigger ||
//                name == pickTrigger ||
//                name == deathUnarmedTrigger ||
//                name == deathArmedTrigger ||
//                name == "LonVong" ||
//                name == "GetHit" ||
//                name == "GeiHit2" ||
//                name == "Idle_Pick" ||
//                name == "Death";
//     }

//     private bool IsPlayingAttackState(out AnimatorStateInfo activeState, out int layer)
//     {
//         activeState = default;
//         layer = -1;

//         if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
//             return false;

//         if (anim.layerCount > 1)
//         {
//             AnimatorStateInfo attackLayerStateInfo = anim.GetCurrentAnimatorStateInfo(1);
//             if (IsAttackState(attackLayerStateInfo))
//             {
//                 activeState = attackLayerStateInfo;
//                 layer = 1;
//                 return true;
//             }
//         }

//         AnimatorStateInfo baseLayerStateInfo = anim.GetCurrentAnimatorStateInfo(0);
//         if (IsAttackState(baseLayerStateInfo))
//         {
//             activeState = baseLayerStateInfo;
//             layer = 0;
//             return true;
//         }

//         return false;
//     }

//     private bool IsAttackState(AnimatorStateInfo stateInfo)
//     {
//         return stateInfo.IsName(punch1Trigger) ||
//                stateInfo.IsName(punch2Trigger) ||
//                stateInfo.IsName(punch3Trigger) ||
//                stateInfo.IsName(slash1Trigger) ||
//                stateInfo.IsName(slash2Trigger) ||
//                stateInfo.IsName("Punch1") ||
//                stateInfo.IsName("Punch2") ||
//                stateInfo.IsName("Punch3") ||
//                stateInfo.IsName("Slash1") ||
//                stateInfo.IsName("Slash2");
//     }

//     private bool IsPlayingActionAnimation()
//     {
//         if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

//         if (IsFullBodyActionAnimation(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.15f) return true;

//         if (isRootedAttack && IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.15f) return true;

//         if (isStandaloneMode ? isRollingStandalone : rollTimer > 0) return true;

//         if (isRootedAttack && IsPlayingAttackState(out _, out _))
//         {
//             return true;
//         }

//         AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
//         bool isFullBodyAction = stateInfo.IsName(rollTrigger) ||
//                                stateInfo.IsName(getHitTrigger) ||
//                                stateInfo.IsName(getHit2Trigger) ||
//                                stateInfo.IsName(pickTrigger) ||
//                                stateInfo.IsName(deathUnarmedTrigger) ||
//                                stateInfo.IsName(deathArmedTrigger) ||
//                                stateInfo.IsName("LonVong") ||
//                                stateInfo.IsName("GetHit") ||
//                                stateInfo.IsName("GeiHit2") ||
//                                stateInfo.IsName("Idle_Pick") ||
//                                stateInfo.IsName("Death");

//         return isFullBodyAction && stateInfo.normalizedTime < 0.95f;
//     }

//     private bool IsPlayingPickAnimation()
//     {
//         if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

//         if ((lastTriggeredAnimName == pickTrigger || lastTriggeredAnimName == "Idle_Pick" || lastTriggeredAnimName == "Pick")
//             && Time.time - lastActionTriggerTime < 0.15f)
//         {
//             return true;
//         }

//         AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
//         bool isInPickState = stateInfo.IsName(pickTrigger) || stateInfo.IsName("Idle_Pick") || stateInfo.IsName("Pick");
//         return isInPickState && stateInfo.normalizedTime < 0.95f;
//     }

//     private float lastActionTriggerTime = 0f;
//     private string lastTriggeredAnimName = "";

//     private void PlayAnimationLocal(string animName, float fadeTime)
//     {
//         if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;

//         if (useBlendTree && (animName == "Idle" || animName == "Walk" || animName == "run"))
//         {
//             return;
//         }

//         string translatedName = TranslateAnimName(animName);

//         bool isLoopingAnim = translatedName == idleUnarmed || translatedName == walkUnarmed || translatedName == runUnarmed ||
//                              translatedName == idleArmed || translatedName == walkForwardArmed || translatedName == walkBackwardArmed ||
//                              translatedName == runForwardArmed || translatedName == runBackwardArmed || translatedName == walkBackwardUnarmed ||
//                              translatedName == walkLeftUnarmed || translatedName == walkRightUnarmed || translatedName == runLeftUnarmed ||
//                              translatedName == runRightUnarmed || translatedName == walkLeftArmed || translatedName == walkRightArmed ||
//                              translatedName == runLeftArmed || translatedName == runRightArmed;
//         if (isLoopingAnim && currentAnimState == translatedName) return;

//         Debug.Log($"[LeoPlayer Animator] Triggering Anim: '{translatedName}' (source: '{animName}')");

//         anim.ResetTrigger(idleUnarmed);
//         anim.ResetTrigger(walkUnarmed);
//         anim.ResetTrigger(runUnarmed);
//         anim.ResetTrigger(walkBackwardUnarmed);
//         anim.ResetTrigger(runBackwardUnarmed);
//         anim.ResetTrigger(walkLeftUnarmed);
//         anim.ResetTrigger(walkRightUnarmed);
//         anim.ResetTrigger(runLeftUnarmed);
//         anim.ResetTrigger(runRightUnarmed);

//         anim.ResetTrigger(idleArmed);
//         anim.ResetTrigger(walkForwardArmed);
//         anim.ResetTrigger(walkBackwardArmed);
//         anim.ResetTrigger(runForwardArmed);
//         anim.ResetTrigger(runBackwardArmed);
//         anim.ResetTrigger(walkLeftArmed);
//         anim.ResetTrigger(walkRightArmed);
//         anim.ResetTrigger(runLeftArmed);
//         anim.ResetTrigger(runRightArmed);

//         anim.ResetTrigger(deathUnarmedTrigger);
//         anim.ResetTrigger(deathArmedTrigger);
//         anim.ResetTrigger(getHitTrigger);
//         anim.ResetTrigger(getHit2Trigger);
//         anim.ResetTrigger(rollTrigger);
//         anim.ResetTrigger(pickTrigger);

//         anim.ResetTrigger("Idle");
//         anim.ResetTrigger("Walk");
//         anim.ResetTrigger("run");
//         anim.ResetTrigger("Death");
//         anim.ResetTrigger("GetHit");
//         anim.ResetTrigger("GeiHit2");
//         anim.ResetTrigger("LonVong");
//         anim.ResetTrigger("Idle_Pick");

//         if (IsActionAnimationName(translatedName))
//         {
//             anim.ResetTrigger(punch1Trigger);
//             anim.ResetTrigger(punch2Trigger);
//             anim.ResetTrigger(punch3Trigger);
//             anim.ResetTrigger(slash1Trigger);
//             anim.ResetTrigger(slash2Trigger);

//             anim.ResetTrigger("Punch1");
//             anim.ResetTrigger("Punch2");
//             anim.ResetTrigger("Punch3");
//             anim.ResetTrigger("Slash1");
//             anim.ResetTrigger("Slash2");
//             if (!string.IsNullOrEmpty(drawWeaponTrigger)) anim.ResetTrigger(drawWeaponTrigger);
//             if (!string.IsNullOrEmpty(sheathWeaponTrigger)) anim.ResetTrigger(sheathWeaponTrigger);
//             if (!string.IsNullOrEmpty(drawLeftTrigger)) anim.ResetTrigger(drawLeftTrigger);
//             if (!string.IsNullOrEmpty(drawRightTrigger)) anim.ResetTrigger(drawRightTrigger);
//             if (!string.IsNullOrEmpty(sheatheLeftTrigger)) anim.ResetTrigger(sheatheLeftTrigger);
//             if (!string.IsNullOrEmpty(sheatheRightTrigger)) anim.ResetTrigger(sheatheRightTrigger);
//         }

//         if (IsActionAnimationName(translatedName))
//         {
//             anim.SetTrigger(translatedName);

//             // --- ĐÃ SỬA: Tối ưu hóa CrossFade tương thích ngược danh xưng State ---
//             int targetLayer = IsAttackAnimationName(translatedName) ? 1 : 0;

//             // Thử ép trạng thái theo tên gốc hệ thống trước (Punch1, Punch2, Punch3...)
//             anim.CrossFadeInFixedTime(animName, fadeTime, targetLayer, 0f);

//             // Nếu không tìm thấy tên gốc, cơ chế Animator của Unity sẽ tự giữ nguyên 
//             // hoặc bạn có thể cấu hình ô State trùng khớp với Trigger bên dưới:
//             // anim.CrossFadeInFixedTime(translatedName, fadeTime, targetLayer, 0f);
//         }
//         else
//         {
//             anim.SetTrigger(translatedName);
//         }
//         bool isMovingAttack = IsAttackAnimationName(translatedName) && !isRootedAttack;
//         if (!isMovingAttack)
//         {
//             currentAnimState = translatedName;
//         }
//         lastTriggeredAnimName = translatedName;

//         if (IsActionAnimationName(translatedName))
//         {
//             lastActionTriggerTime = Time.time;
//         }

//         if (IsFullBodyActionAnimation(translatedName))
//         {
//             ClearAttackLayer();
//         }
//     }

//     // --- BẮT ÉP ROOT MOTION (Lúc lộn vòng) PHẢI CHẠY QUA HỆ THỐNG VẬT LÝ RIGIDBODY ĐỂ CHẶN XUYÊN TƯỜNG ---
//     private void OnAnimatorMove()
//     {
//         if (anim != null && rb != null)
//         {
//             if (anim.applyRootMotion)
//             {
//                 Vector3 nextPosition = rb.position + anim.deltaPosition;
//                 rb.MovePosition(nextPosition);
//             }
//             else
//             {
//                 // Giải phóng chuyển động vật lý mặc định khi không dùng Root Motion
//                 anim.ApplyBuiltinRootMotion();
//             }
//         }
//     }

//     [ServerRpc]
//     private void PlayAnimationServerRpc(string animName, float fadeTime)
//     {
//         PlayAnimationClientRpc(animName, fadeTime, true);
//     }

//     [ClientRpc]
//     private void PlayAnimationClientRpc(string animName, float fadeTime, bool alreadyPlayedLocally)
//     {
//         if (alreadyPlayedLocally && IsOwner) return;
//         PlayAnimationLocal(animName, fadeTime);
//     }

//     private void ClearAttackLayer()
//     {
//         comboStep = 0;
//         isRootedAttack = false;
//         SetMovementLock(false); // Giải phóng khóa di chuyển nếu đang đấm nửa chừng
//         if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
//         {
//             anim.Play("New State", 1, 0f);
//             anim.Play("Empty", 1, 0f);
//         }
//     }

//     // ------------------------------------------------------------------
//     //  Hitbox Combat System & Animation Event Receivers
//     // ------------------------------------------------------------------
//     public void EnableLeftHitbox()
//     {
//         if (leftHitbox != null)
//         {
//             leftHitbox.enabled = true;
//             Debug.Log("[LeoPlayer] Left hitbox ENABLED.");
//         }
//     }

//     public void DisableLeftHitbox()
//     {
//         if (leftHitbox != null)
//         {
//             leftHitbox.enabled = false;
//             Debug.Log("[LeoPlayer] Left hitbox DISABLED.");
//         }
//     }

//     public void EnableRightHitbox()
//     {
//         if (rightHitbox != null)
//         {
//             rightHitbox.enabled = true;
//             Debug.Log("[LeoPlayer] Right hitbox ENABLED.");
//         }
//     }

//     public void DisableRightHitbox()
//     {
//         if (rightHitbox != null)
//         {
//             rightHitbox.enabled = false;
//             Debug.Log("[LeoPlayer] Right hitbox DISABLED.");
//         }
//     }

//     public void EnableBothHitboxes()
//     {
//         EnableLeftHitbox();
//         EnableRightHitbox();
//         Debug.Log("[LeoPlayer] Both hitboxes ENABLED.");
//     }

//     public void DisableBothHitboxes()
//     {
//         DisableLeftHitbox();
//         DisableRightHitbox();
//         Debug.Log("[LeoPlayer] Both hitboxes DISABLED.");
//     }

//     public void UnlockMovement()
//     {
//         SetMovementLock(false);
//         Debug.Log("[LeoPlayer] Movement UNLOCKED.");
//     }

//     /// <summary>
//     /// Event receiver được gọi bởi Animation Event tại thời điểm cúi xuống trong hoạt ảnh Pick.
//     /// </summary>
//     public void OnPickItemEvent()
//     {
//         Debug.Log("[LeoPlayer] OnPickItemEvent triggered via Animation Event.");
//         if (pendingPickItem != null)
//         {
//             var collectible = pendingPickItem.GetComponent<CollectibleItemDrop>();
//             if (collectible != null)
//             {
//                 collectible.ConfirmCollect();
//             }
//             else
//             {
//                 var repair = pendingPickItem.GetComponent<RepairItemDrop>();
//                 if (repair != null)
//                 {
//                     repair.ConfirmCollect();
//                 }
//             }
//             pendingPickItem = null;
//         }
//     }

//     /// <summary>
//     /// Thay đổi trạng thái hiển thị/khóa con trỏ chuột.
//     /// </summary>
//     public void SetCursorLock(bool locked)
//     {
//         isCursorLocked = locked;
//         LockCursor(locked);
//     }

//     private PlayerHUDController hudControllerCache;
//     private float lastTimeUIOpen = 0f;

//     private PlayerHUDController GetHUDController()
//     {
//         if (hudControllerCache == null)
//         {
//             hudControllerCache = FindObjectOfType<PlayerHUDController>();
//         }
//         return hudControllerCache;
//     }

//     /// <summary>
//     /// Kiểm tra xem UI (Hành trang, Bản đồ, Đối thoại) có đang mở chặn input hay không.
//     /// </summary>
//     public bool IsUIBlockingInput()
//     {
//         bool uiOpen = false;

//         // Kiểm tra biến static cực nhanh và chính xác 100% không lo null hay sai lệch frame
//         if (PlayerHUDController.isAnyUIOpen)
//         {
//             uiOpen = true;
//         }

//         bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
//                               (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive);
//         if (isDialogueOpen)
//         {
//             uiOpen = true;
//         }

//         if (uiOpen)
//         {
//             lastTimeUIOpen = Time.time;
//             return true;
//         }

//         // Chặn click chuột thêm 0.15 giây sau khi đóng UI để tránh click đóng UI bị đi xuyên làm nhân vật chém
//         if (Time.time - lastTimeUIOpen < 0.15f)
//         {
//             return true;
//         }

//         return false;
//     }

//     /// <summary>
//     /// Thiết lập trạng thái khóa di chuyển local, triệt tiêu vận tốc ngay lập tức và đồng bộ lên server.
//     /// </summary>
//     public void SetMovementLock(bool locked)
//     {
//         isMovementLocked = locked;
//         if (locked)
//         {
//             attackDashTimer = 0f; // Hủy bỏ Dash đang hoạt động nếu có
//             if (rb != null)
//             {
//                 rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
//             }
//         }

//         if (!isStandaloneMode && IsOwner)
//         {
//             SetMovementLockServerRpc(locked);
//         }
//     }

//     [ServerRpc]
//     private void SetMovementLockServerRpc(bool locked)
//     {
//         if (IsServer)
//         {
//             isMovementLockedNet.Value = locked;
//         }
//     }

//     private void OnMovementLockedNetChanged(bool oldVal, bool newVal)
//     {
//         if (!IsOwner)
//         {
//             isMovementLocked = newVal;
//             if (newVal && rb != null)
//             {
//                 rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
//             }
//         }
//     }

//     public void EnableLeftWeaponHitbox()
//     {
//         if (leftWeaponHitbox != null)
//         {
//             leftWeaponHitbox.enabled = true;
//             Debug.Log("[LeoPlayer] Left weapon hitbox ENABLED.");
//         }
//     }

//     public void DisableLeftWeaponHitbox()
//     {
//         if (leftWeaponHitbox != null)
//         {
//             leftWeaponHitbox.enabled = false;
//             Debug.Log("[LeoPlayer] Left weapon hitbox DISABLED.");
//         }
//     }

//     public void EnableRightWeaponHitbox()
//     {
//         if (rightWeaponHitbox != null)
//         {
//             rightWeaponHitbox.enabled = true;
//             Debug.Log("[LeoPlayer] Right weapon hitbox ENABLED.");
//         }
//     }

//     public void DisableRightWeaponHitbox()
//     {
//         if (rightWeaponHitbox != null)
//         {
//             rightWeaponHitbox.enabled = false;
//             Debug.Log("[LeoPlayer] Right weapon hitbox DISABLED.");
//         }
//     }

//     public void EnableBothWeaponHitboxes()
//     {
//         EnableLeftWeaponHitbox();
//         EnableRightWeaponHitbox();
//         Debug.Log("[LeoPlayer] Both weapon hitboxes ENABLED.");
//     }

//     public void DisableBothWeaponHitboxes()
//     {
//         DisableLeftWeaponHitbox();
//         DisableRightWeaponHitbox();
//         Debug.Log("[LeoPlayer] Both weapon hitboxes DISABLED.");
//     }

//     public void OnSlashEnd()
//     {
//         Debug.Log("[LeoPlayer] Slash ended.");
//         if (anim != null) anim.applyRootMotion = false;

//         if (isRootedAttack)
//         {
//             var bridge = GetRootMotionBridge();
//             if (bridge != null) bridge.ApplyFinalOffset();
//         }
//     }

//     public void OnHitboxCollision(Collider other)
//     {
//         if (IsEnemy(other, out Collider enemyCollider))
//         {
//             Transform enemyRoot = enemyCollider.transform.root;
//             if (!alreadyHitEnemies.Contains(enemyRoot))
//             {
//                 alreadyHitEnemies.Add(enemyRoot);
//                 Debug.Log($"[LeoPlayer Combat] Hitbox collided with enemy root: {enemyRoot.name}. Dealing damage: {damageAmount}");

//                 if (isStandaloneMode)
//                 {
//                     TryDamageEnemy(enemyCollider);
//                 }
//                 else if (IsOwner)
//                 {
//                     // Network Mode: Tell server to apply damage
//                     var netObj = enemyCollider.GetComponentInParent<NetworkObject>();
//                     if (netObj != null)
//                     {
//                         DamageEnemyServerRpc(netObj);
//                     }
//                     else
//                     {
//                         TryDamageEnemy(enemyCollider);
//                     }
//                 }
//             }
//         }
//     }

//     private bool IsEnemy(Collider col, out Collider enemyCollider)
//     {
//         enemyCollider = null;
//         if (col == null) return false;

//         if (col.GetComponentInParent<Enemy1_DapBua>() != null ||
//             col.GetComponentInParent<Enemy2_Zombie>() != null ||
//             col.GetComponentInParent<Enemy3_Buaa>() != null ||
//             col.GetComponentInParent<Enemy4_Bongtoi>() != null ||
//             col.GetComponentInParent<Enemy5_PhuThuy>() != null)
//         {
//             enemyCollider = col;
//             return true;
//         }
//         return false;
//     }

//     [ServerRpc]
//     private void DamageEnemyServerRpc(NetworkObjectReference enemyRef)
//     {
//         if (enemyRef.TryGet(out NetworkObject netObj))
//         {
//             var col = netObj.GetComponent<Collider>();
//             if (col != null)
//             {
//                 TryDamageEnemy(col);
//             }
//             else
//             {
//                 var e1 = netObj.GetComponentInChildren<Enemy1_DapBua>();
//                 if (e1 != null) { e1.TakeDamage(damageAmount); return; }
//                 var e2 = netObj.GetComponentInChildren<Enemy2_Zombie>();
//                 if (e2 != null) { e2.TakeDamage(damageAmount); return; }
//                 var e3 = netObj.GetComponentInChildren<Enemy3_Buaa>();
//                 if (e3 != null) { e3.TakeDamage(damageAmount); return; }
//                 var e4 = netObj.GetComponentInChildren<Enemy4_Bongtoi>();
//                 if (e4 != null) { e4.TakeDamage(damageAmount); return; }
//                 var e5 = netObj.GetComponentInChildren<Enemy5_PhuThuy>();
//                 if (e5 != null) { e5.TakeDamage(damageAmount); return; }
//             }
//         }
//     }

//     /// <summary>
//     /// Context menu option and runtime helper to automatically locate hands, 
//     /// create hand and sword hitboxes (LeftHitbox, RightHitbox, LeftWeaponHitbox, RightWeaponHitbox),
//     /// attach colliders & PlayerHitbox scripts, and link them to LeoPlayer.
//     /// </summary>
//     [ContextMenu("Create Sword Hitboxes")]
//     public void CreateSwordHitboxes()
//     {
//         Transform leftHand = FindBoneRecursive(transform, "left");
//         Transform rightHand = FindBoneRecursive(transform, "right");

//         if (leftHand == null) leftHand = transform;
//         if (rightHand == null) rightHand = transform;

//         // --- 1. Tạo Hitbox cho Tay không (Punch) ---
//         // Left Punch Hitbox
//         if (leftHitbox == null)
//         {
//             Transform existingLeft = leftHand.Find("LeftHitbox");
//             GameObject leftObj;
//             if (existingLeft != null)
//             {
//                 leftObj = existingLeft.gameObject;
//             }
//             else
//             {
//                 leftObj = new GameObject("LeftHitbox");
//                 leftObj.transform.SetParent(leftHand);
//                 leftObj.transform.localPosition = Vector3.zero;
//                 leftObj.transform.localRotation = Quaternion.identity;
//                 leftObj.transform.localScale = Vector3.one;
//             }

//             BoxCollider col = leftObj.GetComponent<BoxCollider>();
//             if (col == null) col = leftObj.AddComponent<BoxCollider>();
//             col.isTrigger = true;
//             col.size = new Vector3(0.2f, 0.2f, 0.2f); // Nhỏ, nằm ở nắm đấm
//             col.center = Vector3.zero;
//             col.enabled = false;

//             PlayerHitbox ph = leftObj.GetComponent<PlayerHitbox>();
//             if (ph == null) ph = leftObj.AddComponent<PlayerHitbox>();

//             leftHitbox = col;
//             Debug.Log("[LeoPlayer Editor] Created and linked LeftHitbox under " + leftHand.name);
//         }

//         // Right Punch Hitbox
//         if (rightHitbox == null)
//         {
//             Transform existingRight = rightHand.Find("RightHitbox");
//             GameObject rightObj;
//             if (existingRight != null)
//             {
//                 rightObj = existingRight.gameObject;
//             }
//             else
//             {
//                 rightObj = new GameObject("RightHitbox");
//                 rightObj.transform.SetParent(rightHand);
//                 rightObj.transform.localPosition = Vector3.zero;
//                 rightObj.transform.localRotation = Quaternion.identity;
//                 rightObj.transform.localScale = Vector3.one;
//             }

//             BoxCollider col = rightObj.GetComponent<BoxCollider>();
//             if (col == null) col = rightObj.AddComponent<BoxCollider>();
//             col.isTrigger = true;
//             col.size = new Vector3(0.2f, 0.2f, 0.2f); // Nhỏ, nằm ở nắm đấm
//             col.center = Vector3.zero;
//             col.enabled = false;

//             PlayerHitbox ph = rightObj.GetComponent<PlayerHitbox>();
//             if (ph == null) ph = rightObj.AddComponent<PlayerHitbox>();

//             rightHitbox = col;
//             Debug.Log("[LeoPlayer Editor] Created and linked RightHitbox under " + rightHand.name);
//         }

//         // --- 2. Tạo Hitbox cho Kiếm (Sword/Weapon) ---
//         Transform leftSwordParent = FindWeaponTransform(leftHand) ?? leftHand;
//         Transform rightSwordParent = FindWeaponTransform(rightHand) ?? rightHand;

//         // Left Sword Hitbox
//         if (leftWeaponHitbox == null)
//         {
//             Transform existingLeftW = leftSwordParent.Find("LeftWeaponHitbox");
//             GameObject leftWObj;
//             if (existingLeftW != null)
//             {
//                 leftWObj = existingLeftW.gameObject;
//             }
//             else
//             {
//                 leftWObj = new GameObject("LeftWeaponHitbox");
//                 leftWObj.transform.SetParent(leftSwordParent);
//                 leftWObj.transform.localPosition = Vector3.zero;
//                 leftWObj.transform.localRotation = Quaternion.identity;
//                 leftWObj.transform.localScale = Vector3.one;
//             }

//             BoxCollider col = leftWObj.GetComponent<BoxCollider>();
//             if (col == null) col = leftWObj.AddComponent<BoxCollider>();
//             col.isTrigger = true;
//             col.size = new Vector3(0.2f, 0.2f, 1.2f); // To và dài hơn dọc theo thanh kiếm
//             col.center = new Vector3(0f, 0f, 0.6f);
//             col.enabled = false;

//             PlayerHitbox ph = leftWObj.GetComponent<PlayerHitbox>();
//             if (ph == null) ph = leftWObj.AddComponent<PlayerHitbox>();

//             leftWeaponHitbox = col;
//             Debug.Log("[LeoPlayer Editor] Created and linked LeftWeaponHitbox under " + leftSwordParent.name);
//         }

//         // Right Sword Hitbox
//         if (rightWeaponHitbox == null)
//         {
//             Transform existingRightW = rightSwordParent.Find("RightWeaponHitbox");
//             GameObject rightWObj;
//             if (existingRightW != null)
//             {
//                 rightWObj = existingRightW.gameObject;
//             }
//             else
//             {
//                 rightWObj = new GameObject("RightWeaponHitbox");
//                 rightWObj.transform.SetParent(rightSwordParent);
//                 rightWObj.transform.localPosition = Vector3.zero;
//                 rightWObj.transform.localRotation = Quaternion.identity;
//                 rightWObj.transform.localScale = Vector3.one;
//             }

//             BoxCollider col = rightWObj.GetComponent<BoxCollider>();
//             if (col == null) col = rightWObj.AddComponent<BoxCollider>();
//             col.isTrigger = true;
//             col.size = new Vector3(0.2f, 0.2f, 1.2f); // To và dài hơn dọc theo thanh kiếm
//             col.center = new Vector3(0f, 0f, 0.6f);
//             col.enabled = false;

//             PlayerHitbox ph = rightWObj.GetComponent<PlayerHitbox>();
//             if (ph == null) ph = rightWObj.AddComponent<PlayerHitbox>();

//             rightWeaponHitbox = col;
//             Debug.Log("[LeoPlayer Editor] Created and linked RightWeaponHitbox under " + rightSwordParent.name);
//         }
//     }

//     private Transform FindWeaponTransform(Transform hand)
//     {
//         for (int i = 0; i < hand.childCount; i++)
//         {
//             Transform child = hand.GetChild(i);
//             string nameLower = child.name.ToLower();
//             if (nameLower.Contains("sword") || nameLower.Contains("blade") || nameLower.Contains("weapon") ||
//                 nameLower.Contains("kiem") || nameLower.Contains("dao") || nameLower.Contains("katana") || nameLower.Contains("weapon_r") || nameLower.Contains("weapon_l"))
//             {
//                 return child;
//             }
//             Transform subChild = FindWeaponTransform(child);
//             if (subChild != null) return subChild;
//         }
//         return null;
//     }

//     private Transform FindBoneRecursive(Transform current, string keyword)
//     {
//         string nameLower = current.name.ToLower();
//         if (nameLower.Contains(keyword) && (nameLower.Contains("hand") || nameLower.Contains("wrist") || nameLower.Contains("palm") || nameLower.Contains("finger")))
//         {
//             if (nameLower.Contains("hand"))
//             {
//                 return current;
//             }
//         }

//         for (int i = 0; i < current.childCount; i++)
//         {
//             Transform found = FindBoneRecursive(current.GetChild(i), keyword);
//             if (found != null) return found;
//         }

//         if (current.name.ToLower().Contains(keyword) && current.name.ToLower().Contains("hand"))
//         {
//             return current;
//         }

//         return null;
//     }

//     private void UpdateAttackLayerWeight()
//     {
//         if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
//         {
//             AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(1);
//             // Nếu layer 1 đang chạy hoạt ảnh chém (khác New State và Empty), đặt weight = 1
//             bool isSlashActive = !stateInfo.IsName("New State") && !stateInfo.IsName("Empty");
//             targetAttackLayerWeight = isSlashActive ? 1f : 0f;

//             // Lerp mượt mà weight để tránh chuyển đổi giật cục dáng đi
//             currentAttackLayerWeight = Mathf.MoveTowards(currentAttackLayerWeight, targetAttackLayerWeight, Time.deltaTime * 10f);
//             anim.SetLayerWeight(1, currentAttackLayerWeight);
//         }
//     }
// }
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public struct SlashParticleConfig
{
    [Tooltip("Particle Prefab to instantiate.")]
    public GameObject particlePrefab;
    [Tooltip("Delay before spawning/playing the particle (in seconds).")]
    public float delay;
    [Tooltip("Position offset relative to the player or parent override.")]
    public Vector3 positionOffset;
    [Tooltip("Rotation offset relative to the player or parent override.")]
    public Vector3 rotationOffset;
    [Tooltip("If true, the particle will be parented to the player/parentOverride. If false, it spawns at the offset but remains independent in world space.")]
    public bool parentToPlayer;
    [Tooltip("Parent of the particle. If null and Parent To Player is true, it defaults to the player transform.")]
    public Transform parentOverride;
}

[System.Serializable]
public struct ComboParticleGroup
{
    [Tooltip("Label for this combo step (e.g. Slash 1)")]
    public string label;
    [Tooltip("List of particles to spawn for this combo step.")]
    public SlashParticleConfig[] particles;
}

/// <summary>
/// Independent custom player controller for Leo Assassin, inheriting directly from NetworkBehaviour.
/// Operates seamlessly with the SimplePlayerTest proxy component to maintain complete compatibility
/// with enemy AI, NPC dialog, and UI HUD systems.
/// Supports smooth 8-directional Blend Tree movement using actual user fbx filenames.
/// </summary>
public class LeoPlayer : NetworkBehaviour, IPlayerHUDTarget
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip footstepClip2;
    [SerializeField] private AudioClip attackClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private AudioClip skillQClip;
    [SerializeField] private AudioClip skillEClip;
    [SerializeField] private AudioClip skillRClip;
    private float footstepTimer = 0f;
    private bool playSecondFootstep = false;

    private void InitializeAudio()
    {
        if (playerAudioSource == null)
        {
            Transform child = transform.Find("PlayerSFXSource");
            GameObject sfxObj;
            if (child == null)
            {
                sfxObj = new GameObject("PlayerSFXSource");
                sfxObj.transform.SetParent(transform);
                sfxObj.transform.localPosition = Vector3.zero;
            }
            else
            {
                sfxObj = child.gameObject;
            }
            
            playerAudioSource = sfxObj.GetComponent<AudioSource>();
            if (playerAudioSource == null)
            {
                playerAudioSource = sfxObj.AddComponent<AudioSource>();
            }
        }
        playerAudioSource.spatialBlend = 0.0f; // 2D Sound for absolute audibility
        playerAudioSource.playOnAwake = false;
        playerAudioSource.mute = false;
        playerAudioSource.volume = 1.0f;

        if (footstepClip == null) footstepClip = Resources.Load<AudioClip>("Audio/Footstep");
        if (footstepClip2 == null) footstepClip2 = Resources.Load<AudioClip>("Audio/Footstep2");
        // Tạm thời comment các âm thanh chưa có để tránh loạn âm thanh
        /*
        if (attackClip == null) attackClip = Resources.Load<AudioClip>("Audio/HeavySwing");
        if (hitClip == null) hitClip = Resources.Load<AudioClip>("Audio/HitHurt");
        if (deathClip == null) deathClip = Resources.Load<AudioClip>("Audio/Death");
        */
        
        // Tải âm thanh Skill mới thêm (Leo dùng ThunderSkill cho Skill R, không có BreakSkill)
        if (skillRClip == null) skillRClip = Resources.Load<AudioClip>("Audio/ThunderSkill");

        // Log warnings if audio files fail to load
        if (footstepClip == null) Debug.LogWarning($"[Audio Debug] LeoPlayer: Failed to load Resources/Audio/Footstep");
        else Debug.Log($"[Audio Debug] LeoPlayer: Successfully loaded Resources/Audio/Footstep");
        if (footstepClip2 == null) Debug.LogWarning($"[Audio Debug] LeoPlayer: Failed to load Resources/Audio/Footstep2");
        else Debug.Log($"[Audio Debug] LeoPlayer: Successfully loaded Resources/Audio/Footstep2");
        /*
        if (attackClip == null) Debug.LogWarning($"[Audio Debug] LeoPlayer: Failed to load Resources/Audio/HeavySwing");
        else Debug.Log($"[Audio Debug] LeoPlayer: Successfully loaded Resources/Audio/HeavySwing");
        if (hitClip == null) Debug.LogWarning($"[Audio Debug] LeoPlayer: Failed to load Resources/Audio/HitHurt");
        if (deathClip == null) Debug.LogWarning($"[Audio Debug] LeoPlayer: Failed to load Resources/Audio/Death");
        */
        if (skillRClip == null) Debug.LogWarning($"[Audio Debug] LeoPlayer: Failed to load Resources/Audio/ThunderSkill (Skill R)");
        else Debug.Log($"[Audio Debug] LeoPlayer: Successfully loaded Resources/Audio/ThunderSkill (Skill R)");
    }

    private void PlayPlayerSFX(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[Audio Debug] LeoPlayer: Attempted to play a NULL AudioClip!");
            return;
        }
        if (playerAudioSource == null)
        {
            InitializeAudio();
        }
        float sfxVol = 0.9f;
        float masterVol = 1.0f;
        if (AudioManager.Instance != null)
        {
            sfxVol = AudioManager.Instance.SFXVolume;
            masterVol = AudioManager.Instance.MasterVolume;
        }
        else
        {
            sfxVol = PlayerPrefs.GetFloat("SFXVolume", 90f) / 100f;
            masterVol = PlayerPrefs.GetFloat("MasterVolume", 100f) / 100f;
        }
        float finalVolume = volumeScale * sfxVol * masterVol;
        Debug.Log($"[Audio Debug] LeoPlayer: Playing SFX '{clip.name}' at volume {finalVolume} (scale={volumeScale}, sfxVol={sfxVol}, masterVol={masterVol})");
        playerAudioSource.PlayOneShot(clip, finalVolume);
    }

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
    public bool rotateToCameraWhenUnarmed = true;

    [Header("Player Settings & Stats")]
    public float moveSpeed = 3.5f;
    public float runSpeedMultiplier = 3.5f;
    public float damageAmount = 25f;
    public float attackRange = 2f;

    [Header("Gravity & Physics")]
    [Tooltip("Hệ số nhân gravity thêm vào. 1 = giữ nguyên, 2 = nặng gấp đôi, 3 = nặng gấp 3...")]
    public float extraGravityMultiplier = 2.5f;
    [Tooltip("Tốc độ rơi xuống tối đa (m/s). Đặt cao hơn để rơi nhanh hơn.")]
    public float maxFallSpeed = 20f;
    [Tooltip("Chỉ áp extra gravity khi player đang trên không (false = luôn áp).")]
    public bool onlyExtraGravityWhenAirborne = false;

    [Header("Combo Attack Settings")]

    protected int comboStep = 0;
    protected bool isRootedAttack = false;

    [Header("Sword Combo Particles Settings")]
    [Tooltip("Configure particles for each sword combo step. Element 0 = Slash 1, Element 1 = Slash 2, Element 2 = Slash 3.")]
    public ComboParticleGroup[] swordComboParticles = new ComboParticleGroup[3]
    {
        new ComboParticleGroup { label = "Slash 1 (comboStep = 1)", particles = new SlashParticleConfig[0] },
        new ComboParticleGroup { label = "Slash 2 (comboStep = 2)", particles = new SlashParticleConfig[0] },
        new ComboParticleGroup { label = "Slash 3 (comboStep = 3)", particles = new SlashParticleConfig[0] }
    };
    // Offset xoay root cũ đã bị xóa - xem LeoBoneCorrector.cs để hiệu chỉnh xương đúng cách

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

    [Header("Player Name Sync")]
    public NetworkVariable<Unity.Collections.FixedString64Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Leo", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // IPlayerHUDTarget Stats Implementation
    public string DisplayName => string.IsNullOrEmpty(playerName.Value.ToString()) ? "Leo" : playerName.Value.ToString();
    public int PlayerLevel => isStandaloneMode ? localLevel : playerLevel.Value;
    public float PlayerExp => isStandaloneMode ? localExp : playerExp.Value;
    public float MaxExp => 100f + (isStandaloneMode ? localLevel : playerLevel.Value) * 50f;

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

    [Header("Camera Inversion Settings")]
    public bool invertCameraY = false;

    [Header("Aiming Settings")]
    public float aimCameraDistance = 4f;
    public float aimShoulderOffset = 0.8f;
    public float aimPivotHeight = 1.3f;
    public float aimCameraSmoothSpeed = 10f;
    public float aimMinPitch = -80f;
    public float aimMaxPitch = 80f;

    private float defaultCameraDistance;
    private float defaultPivotHeight;
    private float currentShoulderOffset = 0f;
    public bool IsAiming => isStandaloneMode ? (localIsAimingR || isRShootPending) : (IsOwner ? (localIsAimingR || isRShootPending) : isAimingNet.Value);

    [Header("Spine Aim Settings")]
    public float maxSpineTwistAngle = 80f;
    
    [Header("Punch 1 Fine Tuning")]
    public float punch1YOffset = 0f;
    public float punch1XOffset = 0f;

    [Header("Punch 2 Fine Tuning")]
    public float punch2YOffset = 0f;
    public float punch2XOffset = 0f;

    [Header("Punch 3 Fine Tuning")]
    public float punch3YOffset = 0f;
    public float punch3XOffset = 0f;

    [Header("Slash 1 Fine Tuning")]
    public float slash1YOffset = 0f;
    public float slash1XOffset = 0f;

    [Header("Slash 2 Fine Tuning")]
    public float slash2YOffset = 0f;
    public float slash2XOffset = 0f;

    [Header("Slash 3 Fine Tuning")]
    public float slash3YOffset = 0f;
    public float slash3XOffset = 0f;

    private float smoothedYOffset = 0f;
    private float smoothedXOffset = 0f;
    public float spineSmoothSpeed = 15f;
    private Transform spineBone;
    private float localAimAngle = 0f;
    private Vector3 lastPositionForSpine;

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

    [Header("Network Movement Sync")]
    public NetworkVariable<float> netMoveX = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<float> netMoveZ = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f,
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
    public NetworkVariable<int> upgradePoints = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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

    public NetworkVariable<bool> isAttackSpeedBoostedNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isQSkillActiveNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isAimingNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [HideInInspector]
    public GameObject pendingPickItem;

    [Header("Local State & Inventory")]
    public string[] inventorySlots = new string[10] { "", "", "", "", "", "", "", "", "", "" };
    protected int localUpgradePoints = 0;
    protected int localHpLevel = 0;
    protected int localMpLevel = 0;
    protected int localCooldownLevel = 0;
    protected int localDamageLevel = 0;
    protected int localLevel = 0;
    protected float localExp = 0f;
    protected float localHealth;
    protected float localWeapon1Durability = 100f;
    protected float localWeapon2Durability = 100f;
    protected bool localWeapon2Locked = true;
    protected bool localSkillsUnlocked = false;
    protected int localActiveWeaponIndex = 1;



    [Header("Skill R - Bắn Cục Sét")]
    [Tooltip("Prefab đạn sét của chiêu R")]
    public GameObject rSkillLightningPrefab;

    [Tooltip("Prefab hiển thị trên tay khi đang giữ phím R để ngắm")]
    public GameObject rSkillHandPreviewPrefab;

    [Header("Skill R Fine Tuning")]
    [Tooltip("Y Offset chỉnh xoay cột sống ngang cho chiêu R")]
    public float rSkillYOffset = 0f;
    [Tooltip("X Offset chỉnh xoay cột sống dọc cho chiêu R")]
    public float rSkillXOffset = 0f;
    [Tooltip("Đảo ngược chiều cúi đầu/ngẩng đầu của xương cột sống")]
    public bool invertSpinePitch = false;
    [Tooltip("Điểm xuất phát bắn đạn sét của chiêu R")]
    public Transform rSkillLightningSpawnPoint;
    [Tooltip("Tốc độ bay của đạn sét")]
    public float rSkillLightningSpeed = 20f;
    [Tooltip("Sát thương của đạn sét")]
    public float rSkillLightningDamage = 40f;

    private bool localIsAimingR = false;
    private GameObject rSkillHandPreviewVisual;
    private bool isRShootPending = false;
    private bool isPendingRShootNetworkMode = false;

    [Tooltip("Bán kính vòng tròn định vị chiêu R")]
    public float rSkillAoeRadius = 3f;

    private LineRenderer aoeIndicatorLine;
    private Vector3 aoeTargetPosition;
    private Vector3 pendingRShootPosition;

    [Header("Attack Speed Boost Skill E Settings")]
    public Material redSwordMaterial;
    public Material ghostBodyMaterial;
    [Tooltip("Gán texture 'sword_Emissive' ở đây để chỉ nhuộm đỏ phần lưỡi/đường vân kiếm mà giữ nguyên chuôi kiếm.")]
    public Texture2D swordEmissiveMap;
    [ColorUsage(true, true)]
    [Tooltip("Màu phát sáng HDR đỏ cho kiếm.")]
    public Color redEmissiveColor = new Color(3.5f, 0f, 0f, 1f);
    private float attackSpeedBoostTimeRemaining = 0f;
    private System.Collections.Generic.Dictionary<Renderer, Material[]> originalSwordMaterials = new System.Collections.Generic.Dictionary<Renderer, Material[]>();
    private System.Collections.Generic.Dictionary<Renderer, Material[]> originalBodyMaterials = new System.Collections.Generic.Dictionary<Renderer, Material[]>();

    [Header("Ghost Slash Skill Q Settings")]
    [Tooltip("Particle prefab riêng cho hiệu ứng Ảo ảnh Chém. Nếu để trống sẽ dùng pool VFX cũ.")]
    public GameObject qSkillParticlePrefab;
    public float qSkillDuration = 1.5f;
    public int qSkillSlashCount = 5;
    public float qSkillDamagePerSlash = 15f;
    public float qSkillSearchRadius = 8f;
    private float qSkillTimeRemaining = 0f;
    private bool isQSkillActiveLocal = false;

    /// <summary>Sự kiện bắt đầu khi Skill Q bị hủy (server xác nhận không có enemy) — để HUD reset cooldown.</summary>
    public event System.Action OnQSkillCancelled;

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
    public string runRightUnarmed = "Rủnightnovukhi";

    // Trigger lists (Armed fallback)
    public string idleArmed = "IdleArmed";
    public string walkForwardArmed = "WalkForwardArmed";
    public string walkBackwardArmed = "WalkBackwardArmed";
    public string runForwardArmed = "RunForwardArmed";
    public string runBackwardArmed = "RunBackwardArmed";
    public string walkLeftArmed = "walkleftcovukhi";
    public string walkRightArmed = "walkrightcovukhi";
    public string runLeftArmed = "Runleftcovukhi";
    public string runRightArmed = "Rủnightcovukhi";

    // Action and attack triggers
    public string rollTrigger = "Lonmeo";
    public string pickTrigger = "Pick";
    public string punch1Trigger = "DamTrai";
    public string punch2Trigger = "DamPhai";
    public string punch3Trigger = "DamCombo";
    public string slash1Trigger = "Combo1kiem";
    public string slash2Trigger = "Attackdoucombo";
    public string slash3Trigger = "Slash3"; // Tên trigger Slash3 trong Animator (có thể cấu hình lại)

    // Death and hit
    public string deathUnarmedTrigger = "Death";
    public string deathArmedTrigger = "Death";
    public string getHitTrigger = "GetHit";
    public string getHit2Trigger = "GeiHit2";

    // Smooth inputs
    private float smoothedInputX = 0f;
    private float smoothedInputZ = 0f;
    private int fallbackWeaponIndex = 1;
    private RootMotionBridge rootMotionBridge;
    private SimplePlayerTest proxyPlayerTest;

    private Rigidbody rb;

    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public float CurrentHealth =>
        isStandaloneMode ? localHealth : currentHealth.Value;

    // IPlayerHUDTarget Implementation
    bool IPlayerHUDTarget.isStandaloneMode => isStandaloneMode;
    public bool IsStandaloneMode => isStandaloneMode;
    public int CharacterClassIndex => characterClassIndex;
    public bool IsSwitchingWeapon => isSwitchingWeapon;
    public float Weapon1MaxDurability => weapon1MaxDurability;
    public float Weapon2MaxDurability => weapon2MaxDurability;
    public string[] InventorySlots => inventorySlots;

    private bool isDeathAnimFinished = false;
    public bool IsDeathAnimationFinished => isDeathAnimFinished;
    public void ResetDeathState() => isDeathAnimFinished = false;

    public void OnDeathAnimationEnd()
    {
        if (IsOwner || isStandaloneMode)
        {
            StartCoroutine(DeathEyelidsSequenceCoroutine());
        }
    }

    private System.Collections.IEnumerator DeathEyelidsSequenceCoroutine()
    {
        if (CurrentHealth > 0f) yield break;
        float duration = 1.5f;
        PlayerDeathEffectManager.Instance.PlayDeathEffect(duration);
        yield return new WaitForSeconds(duration);
        if (CurrentHealth > 0f)
        {
            PlayerDeathEffectManager.Instance.ResetDeathEffect();
            yield break;
        }
        isDeathAnimFinished = true;
        if (!isStandaloneMode)
        {
            NotifyDeathAnimFinishedServerRpc();
        }
    }

    [ServerRpc]
    private void NotifyDeathAnimFinishedServerRpc()
    {
        isDeathAnimFinished = true;
    }
    public float MaxHealth => maxHealth;

    // Invisibility Skill R (stub - legacy removed)
    public bool IsInvisible => false;
    public float InvisibilityTimeRemaining => 0f;

    // Attack Speed Boost Skill E
    public bool IsAttackSpeedBoosted => isStandaloneMode ? (attackSpeedBoostTimeRemaining > 0f) : isAttackSpeedBoostedNet.Value;
    public float AttackSpeedBoostTimeRemaining => attackSpeedBoostTimeRemaining;

    // Ghost Slash Skill Q
    public bool IsQSkillActive => isStandaloneMode ? isQSkillActiveLocal : isQSkillActiveNet.Value;
    public float QSkillTimeRemaining => qSkillTimeRemaining;

    protected RootMotionBridge GetRootMotionBridge()
    {
        if (rootMotionBridge == null && anim != null)
        {
            rootMotionBridge = anim.GetComponent<RootMotionBridge>();
            if (rootMotionBridge == null)
            {
                rootMotionBridge = anim.gameObject.AddComponent<RootMotionBridge>();
                Debug.Log($"[LeoPlayer] Dynamically added RootMotionBridge to {anim.gameObject.name} at runtime.");
            }
        }
        return rootMotionBridge;
    }

    private void ResetKinematicState()
    {
        if (rb != null)
        {
            rb.isKinematic = isStandaloneMode ? false : !IsOwner;
        }
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

        rotationSmoothSpeedArmed = 10f;
        rotationSmoothSpeedUnarmed = 12f;
        rotateToCameraWhenUnarmed = true;
        rotateToCameraWhenArmed = true;

        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Mặc định tắt Kinematic để di chuyển được ở chế độ Standalone/Offline
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        CreateSwordHitboxes();
    }

    private void Start()
    {
        InitializeAudio();
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>(true);
        }
        if (anim != null)
        {
            GetRootMotionBridge();
            if (anim.layerCount > 1)
            {
                anim.SetLayerWeight(1, 0f);
            }
        }

        float horizontalDistance = new Vector3(cameraOffset.x, 0f, cameraOffset.z).magnitude;
        currentYaw = Mathf.Atan2(cameraOffset.x, -cameraOffset.z) * Mathf.Rad2Deg;
        currentPitch = Mathf.Atan2(cameraOffset.y, horizontalDistance) * Mathf.Rad2Deg;
        targetYaw = currentYaw;
        targetPitch = currentPitch;
        cameraDistance = cameraOffset.magnitude;
        defaultCameraDistance = cameraDistance;
        defaultPivotHeight = cameraPivotHeight;
        lastPositionForSpine = transform.position;

        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            localHealth = maxHealth;
            InitStandaloneMode();
        }

        SyncWeaponVisuals(GetActiveWeaponIndex());
        if (isStandaloneMode || IsOwner)
        {
            PlayerDeathEffectManager.Instance.ResetDeathEffect();
        }
    }

    private void InitStandaloneMode()
    {
        Debug.Log("[LeoPlayer] Starting in STANDALONE mode. Local inputs active.");
        LockCursor(isCursorLocked);
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Tắt Kinematic để di chuyển trong chế độ chơi đơn lẻ
        }
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);
        playerName.Value = PlayerPrefs.GetString("AuthDisplayName", "Leo");
        if (PlayerHUDManager.ActivePlayers != null && !PlayerHUDManager.ActivePlayers.Contains(this))
        {
            PlayerHUDManager.ActivePlayers.Add(this);
        }
        localLevel = PlayerPrefs.GetInt("SelectedPlayerLevel_" + characterClassIndex, 0);
        localExp = PlayerPrefs.GetFloat("SelectedPlayerExp_" + characterClassIndex, 0f);

        // Register local target
        PlayerHUDController.LocalPlayerTarget = this;
        RakanDialogueController.LocalPlayerTarget = this;
        SilasDialogueController.LocalPlayerTarget = this;

        PlayerHUDController hud = null;
        PlayerHUDManager hudManager = PlayerHUDManager.Instance != null ? PlayerHUDManager.Instance : FindAnyObjectByType<PlayerHUDManager>();
        if (hudManager != null)
        {
            hud = hudManager.ActivateHUD(characterClassIndex);
        }
        else
        {
            hud = FindAnyObjectByType<PlayerHUDController>();
        }

        if (hud != null)
        {
            hud.SetupPlayerProfile(characterClassIndex);
            ApplyUpgradedStats();
            UpdateUpgradeHUD();
        }
        LoadPlayerStateFromDatabase();
        UpdateDurabilityHUD();
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = !IsOwner;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        if (!IsOwner)
        {
            if (GetComponentInChildren<PlayerNameplate>(true) == null)
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
        isMovementLockedNet.OnValueChanged += OnMovementLockedNetChanged;
        currentHealth.OnValueChanged += OnHealthChangedShared;

        isAttackSpeedBoostedNet.OnValueChanged += OnAttackSpeedBoostedChanged;
        isQSkillActiveNet.OnValueChanged += OnQSkillActiveChanged;
        isRollingNet.OnValueChanged += OnRollingNetChanged;
        isAimingNet.OnValueChanged += OnAimingNetChanged;

        if (IsOwner)
        {
            // Tải nhân vật đã lưu từ PlayerPrefs
            characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);

            // Đồng bộ tên người chơi qua mạng
            string myName = PlayerPrefs.GetString("AuthDisplayName", "Leo");
            SetPlayerNameServerRpc(myName);

            // Register local target
            PlayerHUDController.LocalPlayerTarget = this;
            RakanDialogueController.LocalPlayerTarget = this;
            SilasDialogueController.LocalPlayerTarget = this;

            currentHealth.OnValueChanged += OnHealthChanged;
            UpdateHealthHUD(currentHealth.Value);

            PlayerHUDController hud = null;
            PlayerHUDManager hudManager = PlayerHUDManager.Instance != null ? PlayerHUDManager.Instance : FindAnyObjectByType<PlayerHUDManager>();
            if (hudManager != null)
            {
                hud = hudManager.ActivateHUD(characterClassIndex);
            }
            else
            {
                hud = FindAnyObjectByType<PlayerHUDController>();
            }

            if (hud != null)
                hud.SetupPlayerProfile(characterClassIndex);

            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindAnyObjectByType<Camera>();

            LoadPlayerStateFromDatabase();
            ApplyUpgradedStats();
            UpdateUpgradeHUD();
            UpdateDurabilityHUD();
            LockCursor(isCursorLocked);
        }

        SyncWeaponVisuals(activeWeaponIndex.Value);


        // Apply initial visual states for network variables
        if (!isStandaloneMode)
        {
            if (isQSkillActiveNet.Value)
            {
                SetLeoRenderersActive(false);
            }
        }
    }

    private void OnRollingNetChanged(bool oldVal, bool newVal)
    {
        if (!newVal)
        {
            ResetKinematicState();
            var bridge = GetRootMotionBridge();
            if (bridge != null)
            {
                bridge.EndRoll();
                bridge.enabled = true;
            }
        }
        else
        {
            var bridge = GetRootMotionBridge();
            if (bridge != null)
            {
                bridge.BeginRoll();
                bridge.enabled = false;
            }
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
        isMovementLockedNet.OnValueChanged -= OnMovementLockedNetChanged;
        currentHealth.OnValueChanged -= OnHealthChangedShared;

        isAttackSpeedBoostedNet.OnValueChanged -= OnAttackSpeedBoostedChanged;
        isQSkillActiveNet.OnValueChanged -= OnQSkillActiveChanged;
        isRollingNet.OnValueChanged -= OnRollingNetChanged;
        isAimingNet.OnValueChanged -= OnAimingNetChanged;

        if (IsOwner)
            currentHealth.OnValueChanged -= OnHealthChanged;
    }

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

    public bool IsHoldingAxe()
    {
        return GetComponentInChildren<AxeItem>(true) != null;
    }

    public int GetActiveWeaponIndex()
    {
        int val;
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
            val = (hud != null) ? hud.currentSelectedWeapon : fallbackWeaponIndex;
        }
        else
        {
            val = activeWeaponIndex.Value;
        }

        if (val == 1 && !IsHoldingAxe())
        {
            return 0;
        }
        return val;
    }

    public void OnRollEnd()
    {
        Debug.Log("[LeoPlayer] Roll ended.");
        isRollingStandalone = false;
        rollTimer = 0f;

        if (anim != null) anim.applyRootMotion = false;

        // Bật lại RootMotionBridge sau khi lộn xong
        var bridge = GetRootMotionBridge();
        if (bridge != null)
        {
            bridge.EndRoll();
            bridge.enabled = true;
        }

        ResetKinematicState();

        if (isStandaloneMode || IsOwner)
        {
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

            if (!isStandaloneMode)
            {
                StopRollServerRpc(transform.position, transform.rotation);
            }
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


        if (attackSpeedBoostTimeRemaining > 0f)
        {
            attackSpeedBoostTimeRemaining -= Time.deltaTime;
            if (attackSpeedBoostTimeRemaining <= 0f)
            {
                attackSpeedBoostTimeRemaining = 0f;
                if (isStandaloneMode)
                {
                    SetGhostVisuals(false);
                    PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
                    if (hud != null) hud.TriggerCooldownE();
                }
                else if (IsServer)
                {
                    isAttackSpeedBoostedNet.Value = false;
                    TriggerAttackSpeedBoostClientRpc(false);
                }
            }
        }

        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            anim.speed = 1.0f;
        }

        UpdateAttackLayerWeight();

        // Đồng bộ di chuyển lướng (Roll) qua network cho cả Client Owner, Server, và các Client khác — giống Arthur
        bool isRolling = isStandaloneMode ? isRollingStandalone : (IsSpawned && isRollingNet.Value);
        if (isRolling)
        {
            if (rb != null && !rb.isKinematic)
            {
                float currentYVelocity = rb.linearVelocity.y;
                rb.linearVelocity = new Vector3(rollDirection.x * rollSpeed, currentYVelocity, rollDirection.z * rollSpeed);
            }
            else
            {
                transform.Translate(rollDirection * rollSpeed * Time.deltaTime, Space.World);
            }

            if (rollDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(rollDirection);
            }

            if (isStandaloneMode || IsOwner)
            {
                rollTimer -= Time.deltaTime;
                if (rollTimer <= 0)
                {
                    OnRollEnd();
                }
            }
            return;
        }

        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (hasControl)
        {
            HandleRAiming();
            if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
            {
                isCursorLocked = !isCursorLocked;
                LockCursor(isCursorLocked);
            }

            // Tính toán và đồng bộ góc xoay cột sống (Spine aim angle) - CHỈ dành cho trạng thái ngắm bắn (Aiming)
            // Khi chém/đấm thường, không xoay Spine theo camera để tránh vặn xoắn mesh ở đòn chém sâu
            bool isAimingActive = IsAiming;
            
            if (isAimingActive && !isRootedAttack && targetCamera != null)
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
                }

                if (!isStandaloneMode)
                {
                    netAimPitch.Value = currentPitch;
                }
            }
            else
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

        if (rollCooldownTimer > 0)
        {
            rollCooldownTimer -= Time.deltaTime;
        }

        if (attackDashTimer > 0)
        {
            attackDashTimer -= Time.deltaTime;
        }



        if (CurrentHealth <= 0)
        {
            if (anim != null) anim.applyRootMotion = false;
            if (rb != null) rb.linearVelocity = Vector3.zero;
            if (currentAnimState != "Death")
            {
                PlayAnimation("Death", 0.15f);
            }
            return;
        }

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

        if (!IsOwner)
        {
            if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
            {
                anim.SetFloat(inputXParam, netMoveX.Value);
                anim.SetFloat(inputZParam, netMoveZ.Value);
                anim.SetFloat(speedParam, netSpeed.Value);

                bool isArmed = (GetActiveWeaponIndex() == 2);
                anim.SetBool(isArmedParam, isArmed);
            }
            return;
        }
        HandleOwnerUpdate();

        // Footstep logic in Update()
        bool isMoving = false;
        bool isRunning = false;
        if (isStandaloneMode || (IsSpawned && IsOwner))
        {
            float inputX = Input.GetAxis("Horizontal");
            float inputZ = Input.GetAxis("Vertical");
            isMoving = (inputX * inputX + inputZ * inputZ) > 0.01f;
            isRunning = Input.GetKey(KeyCode.LeftShift);
        }
        else if (IsSpawned)
        {
            float netX = netMoveX.Value;
            float netZ = netMoveZ.Value;
            isMoving = (netX * netX + netZ * netZ) > 0.01f;
            isRunning = (netX * netX + netZ * netZ) > 1.5f;
        }

        if (isMoving && currentAnimState != "Death" && (isStandaloneMode ? localHealth : currentHealth.Value) > 0)
        {
            bool isDialogue = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive) ||
                              (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive);
            if (!isDialogue)
            {
                float delay = isRunning ? 0.3f : 0.5f;
                footstepTimer += Time.deltaTime;
                if (footstepTimer >= delay)
                {
                    footstepTimer = 0f;
                    
                    // Alternating footsteps: Footstep 1 then Footstep 2
                    AudioClip clipToPlay = (playSecondFootstep && footstepClip2 != null) ? footstepClip2 : footstepClip;
                    PlayPlayerSFX(clipToPlay, isRunning ? 0.5f : 0.35f);
                    playSecondFootstep = !playSecondFootstep;
                }
            }
        }
        else
        {
            footstepTimer = 0f;
            playSecondFootstep = false; // Reset to start with the first clip next time
        }
    }

    private void HandleStandaloneUpdate()
    {
        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive) ||
                              (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive) ||
                              PlayerHUDController.isCoopBuildingUIOpen ||
                              SeagullController.ActiveSeagull != null;

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



        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;
        if (IsAttackSpeedBoosted)
        {
            currentSpeed *= 2f;
        }

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

        if (move.magnitude > 1f)
        {
            move.Normalize();
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

            if (attackDashTimer > 0)
            {
                rb.linearVelocity = new Vector3(attackDashDirection.x * attackDashSpeed, currentYVelocity, attackDashDirection.z * attackDashSpeed);
            }
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

        // Quét trạng thái tấn công chuẩn xác
        bool isAttacking = IsPlayingAttackState(out _, out _);

        // Xoay nhân vật: Luôn xoay theo hướng Camera — giống Arthur
        bool isAttackingState = isAttacking || isExecutingAttack;
        if (targetCamera != null && (!IsPlayingActionAnimation() || isAttackingState))
        {
            Vector3 camForward = targetCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            if (camForward != Vector3.zero)
            {
                transform.forward = camForward;
            }
        }

        if (isMoving)
        {
            targetInputX = moveX * (isRunning ? 1.0f : 0.5f);
            targetInputZ = moveZ * (isRunning ? 1.0f : 0.5f);
            targetSpeed = new Vector2(targetInputX, targetInputZ).magnitude;
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
            if (localIsAimingR || isRShootPending) return;
            if (!IsUIBlockingInput() && !isRollingStandalone)
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

    private void HandleOwnerUpdate()
    {
        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive) ||
                              (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive) ||
                              PlayerHUDController.isCoopBuildingUIOpen ||
                              SeagullController.ActiveSeagull != null;

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



        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;
        if (IsAttackSpeedBoosted)
        {
            currentSpeed *= 2f;
        }

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

        if (move.magnitude > 1f)
        {
            move.Normalize();
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

            if (attackDashTimer > 0)
            {
                rb.linearVelocity = new Vector3(attackDashDirection.x * attackDashSpeed, currentYVelocity, attackDashDirection.z * attackDashSpeed);
            }
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

        // --- ĐÃ SỬA: Đồng bộ kiểm tra trạng thái tấn công trên mạng cho chế độ Multiplayer ---
        bool isAttacking = IsPlayingAttackState(out _, out _);

        // Xoay nhân vật: Luôn xoay theo hướng Camera — giống Arthur
        bool isAttackingState = isAttacking || isExecutingAttack;
        if (targetCamera != null && (!IsPlayingActionAnimation() || isAttackingState))
        {
            Vector3 camForward = targetCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            if (camForward != Vector3.zero)
            {
                transform.forward = camForward;
            }
        }

        if (isMoving)
        {
            targetInputX = moveX * (isRunning ? 1.0f : 0.5f);
            targetInputZ = moveZ * (isRunning ? 1.0f : 0.5f);
            targetSpeed = new Vector2(targetInputX, targetInputZ).magnitude;
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
            if (localIsAimingR || isRShootPending) return;
            if (!IsUIBlockingInput() && IsSpawned)
            {
                RequestComboAttack(true);
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

            if (!isStandaloneMode && IsOwner)
            {
                netMoveX.Value = smoothedInputX;
                netMoveZ.Value = smoothedInputZ;
                netSpeed.Value = smoothedSpeed;
            }
        }
    }

    private float GetAttackDuration(int weaponIndex, int step)
    {
        if (weaponIndex == 1)
        {
            float duration = GetAnimationClipLength("ChatRiu");
            if (duration > 0f) return duration;
            return 2.267f;
        }
        return 0.5f;
    }

    // ======================================================
    // HỆ THỐNG COMBO TẤN CÔNG VỚI BUFFER & CHỐNG CLICK THỪA
    // ======================================================

    // Combo Settings
    [Header("Combo Attack Settings")]
    [Tooltip("Thời gian animation tấn công (giây). Dùng để tính combo window.")]
    public float punchAnimDuration = 0.5f;
    public float slashAnimDuration = 0.6f;
    // Offset xoay xương đã bị xóa hoàn toàn. Góc quay được điều khiển thuần túy bởi camera.

    [Header("Sword Combo Durations (New FBX Anim clips)")]
    public float attacktaytraiDuration = 0.5f;
    public float attacktayphaiDuration = 0.5f;
    public float slash1Combo2Duration = 0.6f;
    public float slash2combo2Duration = 0.6f;
    public float slash3combo2Duration = 0.6f;

    [Header("Sword Combo VFX (Option B - Spawning Prefabs)")]
    [Tooltip("VFX Prefab cho nhát chém tay trái.")]
    public GameObject leftSlashVfxPrefab;
    [Tooltip("VFX Prefab cho nhát chém tay phải.")]
    public GameObject rightSlashVfxPrefab;
    [Tooltip("VFX Prefab cho nhát chém song kiếm.")]
    public GameObject dualSlashVfxPrefab;
    [Tooltip("VFX Prefab cho nhát chém thứ nhất của combo song kiếm (Slash1Combo2).")]
    public GameObject dualSlash1VfxPrefab;
    [Tooltip("VFX Prefab cho nhát chém thứ hai của combo song kiếm (Slash2combo2).")]
    public GameObject dualSlash2VfxPrefab;
    [Tooltip("Vị trí để spawn VFX trên tay/kiếm trái.")]
    public Transform leftSlashSpawnPoint;
    [Tooltip("Vị trí để spawn VFX trên tay/kiếm phải.")]
    public Transform rightSlashSpawnPoint;
    [Tooltip("Phần trăm animation còn lại cho phép chuyển nhịp combo (0.0 - 1.0).")]
    [Range(0f, 1f)]
    public float comboChainWindowPct = 0.45f;  // Khi anim đã qua 45%, nhấp tiếp sẽ kích hoạt đòn tiếp theo ngay lập tức

    private bool pendingAttackRequest = false;   // Buffer click chuột trong combo window
    private bool isExecutingAttack = false;       // Đang trong nhịp tấn công
    private float attackAnimStartTime = 0f;       // Thời điểm bắt đầu animation tấn công
    private float currentAttackAnimDuration = 0f; // Thời lượng animation tấn công hiện tại
    private int currentWeaponTypeAttacking = 1;  // Loại vũ khí đang dùng khi tấn công
    private Coroutine comboChainCoroutine;

    /// <summary>
    /// Ghi nhận yêu cầu tấn công từ input. Nếu đang đánh thì kích hoạt ngay lập tức đòn tiếp theo khi qua combo window.
    /// </summary>
    private void RequestComboAttack(bool networkMode)
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        if (isExecutingAttack)
        {
            // Đang đánh: kiểm tra xem có đang trong combo window không
            float elapsed = Time.time - attackAnimStartTime;
            float progress = currentAttackAnimDuration > 0 ? elapsed / currentAttackAnimDuration : 1f;
            if (progress >= comboChainWindowPct)
            {
                // Nằm trong combo window -> kích hoạt đòn tiếp theo NGAY LẬP TỨC (hủy các frame thừa của đòn cũ)
                Debug.Log("[LeoPlayer] Nhấp chuột trong Combo Window -> Chuyển sang đòn tiếp theo ngay lập tức!");
                PerformComboAttack(networkMode);
            }
            // Ngoài window (quá sớm) -> bỏ qua click
        }
        else
        {
            // Chưa đang đánh -> bắt đầu tấn công ngay
            PerformComboAttack(networkMode);
        }
    }

    protected void PerformComboAttack(bool networkMode)
    {
        int weapon = GetActiveWeaponIndex();
        if (!networkMode)
        {
            if (weapon == 1) Weapon1Durability = Mathf.Max(Weapon1Durability - 2f, 0f);
            else Weapon2Durability = Mathf.Max(Weapon2Durability - 2f, 0f);
        }

        // Di chuyển tự do khi đấm/chém - không bao giờ khóa movement
        isRootedAttack = false;
        SetMovementLock(false);

        attackAnimStartTime = Time.time;
        isExecutingAttack = true;
        pendingAttackRequest = false;

        alreadyHitEnemies.Clear();
        DisableAllHitboxes();

        string animToPlay;
        if (weapon == 1)
        {
            // --- RÌU (1): CHỈ CHƠI HOẠT ẢNH CHẶT RÌU ---
            animToPlay = "ChatRiu";
            currentAttackAnimDuration = GetAnimationClipLength("ChatRiu");
            if (currentAttackAnimDuration <= 0f) currentAttackAnimDuration = 0.8f;

            if (anim != null) anim.applyRootMotion = isRootedAttack;
            PlayAnimation(animToPlay, 0.05f, false, isRootedAttack);
        }
        else if (weapon == 2)
        {
            // --- KIẾM (ARMED): ĐÚNG CHUẨN 5 CLICK TUẦN TỰ ---
            comboStep++;
            if (comboStep > 5) comboStep = 1;

            animToPlay = "attacktaytrai"; // Đòn 1: Tay trái
            if (comboStep == 2) animToPlay = "attacktayphai"; // Đòn 2: Tay phải
            else if (comboStep == 3) animToPlay = "Slash1Combo2"; // Đòn 3: Song kiếm
            else if (comboStep == 4) animToPlay = "Slash2combo2"; // Đòn 4: Song kiếm
            else if (comboStep == 5) animToPlay = "Slash3combo2"; // Đòn 5: Song kiếm

            if (comboStep == 1) currentAttackAnimDuration = attacktaytraiDuration;
            else if (comboStep == 2) currentAttackAnimDuration = attacktayphaiDuration;
            else if (comboStep == 3) currentAttackAnimDuration = slash1Combo2Duration;
            else if (comboStep == 4) currentAttackAnimDuration = slash2combo2Duration;
            else if (comboStep == 5) currentAttackAnimDuration = slash3combo2Duration;
            else currentAttackAnimDuration = slashAnimDuration;

            if (anim != null) anim.applyRootMotion = isRootedAttack;
            PlayAnimation(animToPlay, 0.05f, false, isRootedAttack);
        }
        else
        {
            // --- ĐẤM TAY (UNARMED): ĐÚNG CHUẨN 3 CLICK TUẦN TỰ ---
            comboStep++;
            if (comboStep > 3) comboStep = 1; // <-- ĐÃ SỬA: Giới hạn chuẩn 3 đòn đấm tuần tự

            animToPlay = "Punch1";
            if (comboStep == 2) animToPlay = "Punch2";
            else if (comboStep == 3) animToPlay = "Punch3";

            currentAttackAnimDuration = punchAnimDuration;

            PlayAnimation(animToPlay, 0.05f, false, isRootedAttack);
        }

        currentWeaponTypeAttacking = weapon;

        PerformRaycastAttack();
        StartCoroutine(DelayedRaycastAttackCoroutine(0.15f));

        if (comboChainCoroutine != null) StopCoroutine(comboChainCoroutine);
        comboChainCoroutine = StartCoroutine(ComboChainCoroutine(weapon, animToPlay, networkMode));
    }

    private System.Collections.IEnumerator DelayedRaycastAttackCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        PerformRaycastAttack();
    }

    private System.Collections.IEnumerator ComboChainCoroutine(int weapon, string animToPlay, bool networkMode)
    {
        // ================================================================
        // HITBOX được điều khiển hoàn toàn bởi ANIMATION EVENT.
        // Coroutine này CHỈ quản lý combo chain (chờ hết animation
        // để xử lý buffer click → tiếp tục combo hay reset).
        // ================================================================
        float totalDuration = currentAttackAnimDuration;

        // Chờ hết thời lượng animation thực tế
        yield return new WaitForSeconds(totalDuration);

        // Đảm bảo tắt hết hitbox khi animation kết thúc
        DisableAllHitboxes();

        // Kết thúc nhịp tấn công
        isExecutingAttack = false;

        // Kiểm tra có buffer click để tiếp tục combo không
        if (pendingAttackRequest)
        {
            pendingAttackRequest = false;
            Debug.Log("[LeoPlayer] Tiếp tục combo từ buffer sau khi kết thúc đòn cũ.");
            PerformComboAttack(networkMode);
        }
        else
        {
            // Không có buffer → reset combo step, mở khóa di chuyển, dọn dẹp Attack Layer
            comboStep = 0;
            isRootedAttack = false;
            SetMovementLock(false);
            ClearAttackLayer();
            Debug.Log("[LeoPlayer] Kết thúc combo - không có input tiếp theo.");
        }
    }

    /// <summary>
    /// Tắt tất cả hitbox ngay lập tức và clear danh sách đã hit.
    /// Có thể gọi từ Animation Event hoặc code.
    /// </summary>
    private void DisableAllHitboxes()
    {
        // Hàm rỗng để không ảnh hưởng
    }

    private void PerformRaycastAttack()
    {
        Debug.Log($"[LeoPlayer Debug] PerformRaycastAttack called (Raycast Mode). isStandaloneMode={isStandaloneMode}, IsOwner={IsOwner}");
        bool hasControl = isStandaloneMode || !IsSpawned || IsOwner || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        if (!hasControl) return;

        // Điểm xuất phát của tia quét ở độ cao ngang ngực (0.8m)
        Vector3 rayStart = transform.position + Vector3.up * 0.8f;
        Vector3 rayDir = transform.forward;
        float range = Mathf.Max(attackRange, 3.0f);

        // Vẽ tia debug trong Unity Editor
        Debug.DrawRay(rayStart, rayDir * range, Color.red, 1f);
        Debug.Log($"[LeoPlayer Debug] Casting SphereCast from {rayStart} in direction {rayDir} with range {range}");

        RaycastHit[] hits = Physics.SphereCastAll(rayStart, 1.2f, rayDir, range);
        foreach (var hit in hits)
        {
            Collider col = hit.collider;
            if (col == null) continue;
            if (col.transform.root == transform.root) continue; // Bỏ qua chính mình

            if (IsEnemy(col, out Collider enemyCollider))
            {
                Transform enemyRoot = enemyCollider.transform.root;
                if (!alreadyHitEnemies.Contains(enemyRoot))
                {
                    alreadyHitEnemies.Add(enemyRoot);
                    Debug.Log($"[LeoPlayer Raycast] HIT ENEMY: {enemyRoot.name} | Sát thương: {damageAmount}");
                    
                    var netObj = enemyCollider.transform.root.GetComponent<NetworkObject>() ?? enemyCollider.GetComponentInParent<NetworkObject>() ?? enemyCollider.GetComponentInChildren<NetworkObject>();

                    if (!isStandaloneMode && IsSpawned && netObj != null)
                    {
                        DamageEnemyServerRpc(netObj);
                    }
                    else
                    {
                        TryDamageEnemy(enemyCollider);
                    }
                }
            }
            else
            {
                // Kiểm tra xem có phải cây gỗ (ChoppableTree) hay không
                ChoppableTree tree = col.GetComponentInParent<ChoppableTree>() ?? col.transform.root.GetComponentInChildren<ChoppableTree>();
                if (tree == null)
                {
                    var forwarder = col.GetComponent<TreeColliderForwarder>();
                    if (forwarder != null)
                    {
                        tree = forwarder.mainTree;
                    }
                }

                if (tree != null)
                {
                    Transform treeRoot = tree.transform;
                    if (!alreadyHitEnemies.Contains(treeRoot))
                    {
                        alreadyHitEnemies.Add(treeRoot);
                        Vector3 hitPos = hit.point;
                        int weaponIndex = GetActiveWeaponIndex();
                        
                        Debug.Log($"[LeoPlayer Raycast] HIT TREE: {tree.name} | WeaponIndex: {weaponIndex}");
                        tree.HitTree(hitPos, weaponIndex);
                    }
                }
            }
        }
    }

    private void TryDamageEnemy(Collider col)
    {
        if (col == null) return;
        float actualDamage = damageAmount;

        var e1 = col.GetComponentInParent<Enemy1_DapBua>() ?? col.GetComponentInChildren<Enemy1_DapBua>() ?? col.transform.root.GetComponentInChildren<Enemy1_DapBua>();
        if (e1 != null) { e1.TakeDamage(actualDamage); return; }

        var e2 = col.GetComponentInParent<Enemy2_Zombie>() ?? col.GetComponentInChildren<Enemy2_Zombie>() ?? col.transform.root.GetComponentInChildren<Enemy2_Zombie>();
        if (e2 != null) { e2.TakeDamage(actualDamage); return; }

        var e3 = col.GetComponentInParent<Enemy3_Buaa>() ?? col.GetComponentInChildren<Enemy3_Buaa>() ?? col.transform.root.GetComponentInChildren<Enemy3_Buaa>();
        if (e3 != null) { e3.TakeDamage(actualDamage); return; }

        var e4 = col.GetComponentInParent<Enemy4_Bongtoi>() ?? col.GetComponentInChildren<Enemy4_Bongtoi>() ?? col.transform.root.GetComponentInChildren<Enemy4_Bongtoi>();
        if (e4 != null) { e4.TakeDamage(actualDamage); return; }

        var e5 = col.GetComponentInParent<Enemy5_PhuThuy>() ?? col.GetComponentInChildren<Enemy5_PhuThuy>() ?? col.transform.root.GetComponentInChildren<Enemy5_PhuThuy>();
        if (e5 != null) { e5.TakeDamage(actualDamage); return; }

        var mb = col.GetComponentInParent<MiniBossAI>() ?? col.GetComponentInChildren<MiniBossAI>() ?? col.transform.root.GetComponentInChildren<MiniBossAI>();
        if (mb != null) { mb.TakeDamage(actualDamage); return; }

        var fb = col.GetComponentInParent<FinalBossAI>() ?? col.GetComponentInChildren<FinalBossAI>() ?? col.transform.root.GetComponentInChildren<FinalBossAI>();
        if (fb != null) { fb.TakeDamage(actualDamage); return; }

        var b = col.GetComponentInParent<BossAI>() ?? col.GetComponentInChildren<BossAI>() ?? col.transform.root.GetComponentInChildren<BossAI>();
        if (b != null) { b.TakeDamage(actualDamage); return; }
    }

    [ServerRpc]
    protected void AttackServerRpc()
    {
        // Trừ độ bền vũ khí trên Server (dù trúng hay trượt)
        int weapon = GetActiveWeaponIndex();
        if (weapon == 1)
        {
            float newVal = Mathf.Max(weapon1Durability.Value - 2f, 0f);
            SyncNetVarFloat(weapon1Durability, proxyPlayerTest != null ? proxyPlayerTest.weapon1Durability : null, newVal);
        }
        else
        {
            float newVal = Mathf.Max(weapon2Durability.Value - 2f, 0f);
            SyncNetVarFloat(weapon2Durability, proxyPlayerTest != null ? proxyPlayerTest.weapon2Durability : null, newVal);
        }

        alreadyHitEnemies.Clear();
    }

    protected void StartRollStandalone(Vector3 moveInput)
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        isRollingStandalone = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;

        ClearAttackLayer();
        InterruptCombo();

        if (moveInput != Vector3.zero)
        {
            rollDirection = moveInput.normalized;
        }
        else
        {
            rollDirection = transform.forward;
        }

        if (rollDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(rollDirection);
        }

        // Tắt RootMotionBridge trong suốt thời gian lộn để tránh lệch trái / giật Hips
        var bridge = GetRootMotionBridge();
        if (bridge != null)
        {
            bridge.BeginRoll();
            bridge.enabled = false;
        }

        // Không đặt rb.isKinematic = true để di chuyển bằng velocity vật lý thuần túy

        if (anim != null) anim.applyRootMotion = false;
        PlayAnimation("LonVong", 0.05f);
    }

    protected void StartRollOwner(Vector3 moveInput)
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;

        ClearAttackLayer();
        InterruptCombo();

        if (moveInput != Vector3.zero)
        {
            rollDirection = moveInput.normalized;
        }
        else
        {
            rollDirection = transform.forward;
        }

        if (rollDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(rollDirection);
        }

        // Tắt RootMotionBridge trong suốt thời gian lộn để tránh lệch trái / giật Hips
        var bridge = GetRootMotionBridge();
        if (bridge != null)
        {
            bridge.BeginRoll();
            bridge.enabled = false;
        }

        // Không đặt rb.isKinematic = true để di chuyển bằng velocity vật lý thuần túy

        if (anim != null) anim.applyRootMotion = false;
        PlayAnimation("LonVong", 0.05f, false);
        StartRollServerRpc(rollDirection, transform.position);
    }

    [ServerRpc]
    private void StartRollServerRpc(Vector3 direction, Vector3 position)
    {
        // Giống Arthur: Server cập nhật isRollingNet và vị trí khởi đầu lộn
        SyncNetVarBool(isRollingNet, proxyPlayerTest != null ? proxyPlayerTest.isRollingNet : null, true);
        transform.position = position;
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.position = position;
            rb.linearVelocity = Vector3.zero;
        }

        if (direction != Vector3.zero)
        {
            rollDirection = direction;
            transform.rotation = Quaternion.LookRotation(direction);
        }
        StartRollClientRpc(direction);
    }

    [ClientRpc]
    private void StartRollClientRpc(Vector3 direction)
    {
        if (!IsOwner)
        {
            rollDirection = direction;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            PlayAnimationLocal("LonVong", 0.05f);
        }
    }

    [ServerRpc]
    protected void StopRollServerRpc(Vector3 position, Quaternion rotation)
    {
        SyncNetVarBool(isRollingNet, proxyPlayerTest != null ? proxyPlayerTest.isRollingNet : null, false);
        transform.position = position;
        transform.rotation = rotation;
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector3.zero;
        }
        ResetKinematicState();
    }

    void LateUpdate()
    {
        // Spine bone twist and combo offset
        if (anim != null)
        {
            // Chỉ thực hiện xoay cột sống Spine khi đang ngắm bắn (IsAiming)
            // Khi chém/đấm thường, cột sống hoàn toàn chạy theo hoạt ảnh tự nhiên để tránh vặn xoắn mesh ở đòn chém sâu
            float targetYOffset = 0f;
            float targetXOffset = 0f;

            if (IsAiming)
            {
                targetYOffset = rSkillYOffset;
                float pitchVal = isStandaloneMode ? currentPitch : netAimPitch.Value;
                float pitchFactor = invertSpinePitch ? -0.7f : 0.7f;
                targetXOffset = (pitchVal - 45f) * pitchFactor + rSkillXOffset;
            }

            smoothedYOffset = Mathf.Lerp(smoothedYOffset, targetYOffset, Time.deltaTime * spineSmoothSpeed);
            smoothedXOffset = Mathf.Lerp(smoothedXOffset, targetXOffset, Time.deltaTime * spineSmoothSpeed);

            // Xoay xương cột sống cho Aiming
            if (IsAiming || Mathf.Abs(smoothedYOffset) > 0.05f || Mathf.Abs(smoothedXOffset) > 0.05f)
            {
                Transform spine = GetSpineBone();
                if (spine != null)
                {
                    float baseAimAngle = isStandaloneMode ? localAimAngle : netAimAngle.Value;
                    float finalYAngle = (IsAiming ? baseAimAngle : 0f) + smoothedYOffset;
                    
                    spine.rotation = Quaternion.AngleAxis(finalYAngle, Vector3.up) * spine.rotation;
            
                    if (Mathf.Abs(smoothedXOffset) > 0.01f)
                    {
                        spine.rotation = Quaternion.AngleAxis(smoothedXOffset, transform.right) * spine.rotation;
                    }
                }
            }
        }

        bool shouldFollow = isStandaloneMode || (IsSpawned && IsOwner);
        if (!shouldFollow || !enableCameraFollow || SeagullController.ActiveSeagull != null) return;

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
            Vector3 pivotPosition = (transform.position + Vector3.up * cameraPivotHeight) + rightOffsetVec;
            Vector3 targetPosition = pivotPosition + rotatedOffset;

            // Thực hiện kiểm tra va chạm của camera với tường/vật cản bằng SphereCast
            float collisionSafetyDistance = 0.4f; // Khoảng cách an toàn để tránh camera sát tường gây lỗi clipping plane
            int cameraLayerMask = ~LayerMask.GetMask("Player", "Ignore Raycast"); // Bỏ qua người chơi và các vật thể Ignore Raycast
            Vector3 rayDirection = rotatedOffset.normalized;
            float maxRayDistance = rotatedOffset.magnitude;

            if (Physics.SphereCast(pivotPosition, 0.2f, rayDirection, out RaycastHit hit, maxRayDistance, cameraLayerMask))
            {
                // Thu nhỏ khoảng cách nếu va chạm với tường
                float clampedDistance = Mathf.Max(0.5f, hit.distance - collisionSafetyDistance);
                targetPosition = pivotPosition + rayDirection * clampedDistance;
            }

            targetCamera.transform.position = targetPosition;

            if (cameraLookAtPlayer)
            {
                // Khóa camera luôn nhìn thẳng vào nhân vật (không dùng Slerp rotation) để nhân vật luôn nằm chính giữa màn hình
                targetCamera.transform.rotation = Quaternion.LookRotation(
                    pivotPosition - targetCamera.transform.position
                );
            }
        }
    }

    public float Weapon1Durability
    {
        get { return isStandaloneMode ? localWeapon1Durability : weapon1Durability.Value; }
        set
        {
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
        set
        {
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

    public void RepairWeaponFromHUD(int weaponSlotIndex)
    {
        if (isStandaloneMode)
        {
            if (weaponSlotIndex == 1)
                localWeapon1Durability = Mathf.Min(localWeapon1Durability + weapon1MaxDurability * 0.5f, weapon1MaxDurability);
            else
                localWeapon2Durability = Mathf.Min(localWeapon2Durability + weapon2MaxDurability * 0.5f, weapon2MaxDurability);
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
            float newVal = Mathf.Min(weapon1Durability.Value + weapon1MaxDurability * 0.5f, weapon1MaxDurability);
            SyncNetVarFloat(weapon1Durability, proxyPlayerTest != null ? proxyPlayerTest.weapon1Durability : null, newVal);
        }
        else
        {
            float newVal = Mathf.Min(weapon2Durability.Value + weapon2MaxDurability * 0.5f, weapon2MaxDurability);
            SyncNetVarFloat(weapon2Durability, proxyPlayerTest != null ? proxyPlayerTest.weapon2Durability : null, newVal);
        }
    }

    public void TakeDamage(float damage)
    {
        // Miễn nhiễm sát thương hoàn toàn khi đang dùng Skill Q
        if (IsQSkillActive)
        {
            Debug.Log("[LeoPlayer] Q Skill active - immune to damage!");
            return;
        }

        bool isRolling = isStandaloneMode ? isRollingStandalone : isRollingNet.Value;
        if (isRolling)
        {
            Debug.Log($"[LeoPlayer] {gameObject.name} is dodging/rolling, immune to damage!");
            return;
        }

        SetMovementLock(false);
        InterruptCombo(); // Ng\u1eaft combo khi b\u1ecb tr\u00fang \u0111\u00f2n

        if (isStandaloneMode)
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            UpdateHealthHUD(localHealth);
            Debug.Log($"[LeoPlayer Standalone] Recieved {damage} DMG. Health: {localHealth}");

            var flash = GetComponent<MaterialFlashBehaviour>();
            if (flash == null) flash = gameObject.AddComponent<MaterialFlashBehaviour>();
            flash.Flash(Color.red, 0.15f);

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

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage)
    {
        TakeDamage(damage);
    }

    public void RequestTakeDamage(float damage)
    {
        if (isStandaloneMode || IsServer)
        {
            TakeDamage(damage);
        }
        else
        {
            TakeDamageServerRpc(damage);
        }
    }

    public void Heal(float amount)
    {
        if (CurrentHealth <= 0) return;

        if (isStandaloneMode)
        {
            localHealth = Mathf.Min(localHealth + amount, maxHealth);
            UpdateHealthHUD(localHealth);
            if (localHealth > 0f)
            {
                PlayerDeathEffectManager.Instance.ResetDeathEffect();
            }
            Debug.Log($"[LeoPlayer Standalone] Hồi {amount} máu. Máu hiện tại: {localHealth}");
        }
        else if (IsServer)
        {
            float finalHp = Mathf.Min(currentHealth.Value + amount, maxHealth);
            SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, finalHp);
            Debug.Log($"[LeoPlayer Server] Hồi {amount} máu cho {gameObject.name}. Máu hiện tại: {currentHealth.Value}");
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
        if (!IsServer)
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
        int targetLvl = 0;
        switch (statType)
        {
            case 0: targetLvl = localHpLevel; break;
            case 1: targetLvl = localMpLevel; break;
            case 2: targetLvl = localCooldownLevel; break;
            case 3: targetLvl = localDamageLevel; break;
        }
        if (targetLvl >= 3) return;

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
        int targetLvl = 0;
        switch (statType)
        {
            case 0: targetLvl = hpLevel.Value; break;
            case 1: targetLvl = mpLevel.Value; break;
            case 2: targetLvl = cooldownLevel.Value; break;
            case 3: targetLvl = damageLevel.Value; break;
        }
        if (targetLvl >= 3) return;

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
        damageAmount = 25f * (1f + dmgLv * 0.15f);

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

    // ======================================================
    // LOGIC TÀNG HÌNH (SKILL R)
    // ======================================================
    private bool IsSkillsUnlocked => isStandaloneMode ? localSkillsUnlocked : isSkillsUnlocked.Value;

    public void TriggerInvisibilitySkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying)
        {
            Debug.LogWarning("[LeoPlayer] Cannot trigger R Skill while carrying a log!");
            return;
        }

        if (PlayerLevel < 5 && !IsSkillsUnlocked) return;

        int activeWeaponIdx = GetActiveWeaponIndex();
        if (activeWeaponIdx != 0)
        {
            Debug.LogWarning($"[LeoPlayer] Cannot trigger R Skill because active weapon is {activeWeaponIdx} (must be unarmed!). Please switch to unarmed first.");
            return;
        }

        Debug.Log("[LeoPlayer] Triggering R Skill: Setting localIsAimingR to true.");
        SetAimingR(true);
    }

    private void CreateAoeIndicator()
    {
        if (aoeIndicatorLine != null) return;

        GameObject indicatorObj = new GameObject("LeoR_AoeIndicator");
        aoeIndicatorLine = indicatorObj.AddComponent<LineRenderer>();
        aoeIndicatorLine.useWorldSpace = true;
        aoeIndicatorLine.loop = true;
        aoeIndicatorLine.startWidth = 0.15f;
        aoeIndicatorLine.endWidth = 0.15f;

        Shader defaultShader = Shader.Find("Sprites/Default");
        if (defaultShader == null) defaultShader = Shader.Find("Unlit/Color");
        if (defaultShader == null) defaultShader = Shader.Find("Standard");

        Material mat = new Material(defaultShader);
        mat.color = Color.green;
        aoeIndicatorLine.material = mat;
        aoeIndicatorLine.startColor = Color.green;
        aoeIndicatorLine.endColor = Color.green;

        aoeIndicatorLine.positionCount = 36;
    }

    private void UpdateAoeIndicatorPosition()
    {
        if (aoeIndicatorLine == null) return;
        if (targetCamera == null) return;

        Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
        Vector3 targetPoint = transform.position + transform.forward * 10f;
        targetPoint.y = transform.position.y;

        if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit cameraHit, 50f, layerMask))
        {
            targetPoint = cameraHit.point;
        }
        else
        {
            if (ray.direction.y < -0.01f)
            {
                float playerFeetY = transform.position.y;
                float t = (playerFeetY - ray.origin.y) / ray.direction.y;
                if (t > 0f && t < 50f)
                {
                    targetPoint = ray.origin + ray.direction * t;
                }
            }
        }

        aoeTargetPosition = targetPoint;

        int segments = aoeIndicatorLine.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (2f * Mathf.PI / segments);
            float x = Mathf.Cos(angle) * rSkillAoeRadius;
            float z = Mathf.Sin(angle) * rSkillAoeRadius;
            Vector3 pointPos = targetPoint + new Vector3(x, 0f, z);

            Vector3 rayStart = pointPos + Vector3.up * 5f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit groundHit, 10f, layerMask))
            {
                pointPos.y = groundHit.point.y + 0.05f;
            }
            else
            {
                pointPos.y = targetPoint.y + 0.05f;
            }

            aoeIndicatorLine.SetPosition(i, pointPos);
        }

        aoeIndicatorLine.enabled = true;
    }

    private void HideAoeIndicator()
    {
        if (aoeIndicatorLine != null)
        {
            if (aoeIndicatorLine.gameObject != null)
            {
                Destroy(aoeIndicatorLine.gameObject);
            }
            aoeIndicatorLine = null;
        }
    }

    private void SetAimingR(bool aiming)
    {
        Debug.Log($"[LeoPlayer] SetAimingR({aiming}) called. Current: {localIsAimingR}");
        if (localIsAimingR == aiming) return;
        localIsAimingR = aiming;
        
        OnAimStateChanged(localIsAimingR);
        if (!isStandaloneMode && IsOwner)
        {
            SetAimingServerRpc(localIsAimingR);
        }

        if (aiming)
        {
            CreateAoeIndicator();
        }
        else
        {
            HideAoeIndicator();
        }
    }

    private void HandleRAiming()
    {
        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (!hasControl) return;

        if (localIsAimingR)
        {
            UpdateAoeIndicatorPosition();

            if (targetCamera != null)
            {
                Vector3 camForward = targetCamera.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();
                if (camForward != Vector3.zero)
                {
                    transform.forward = camForward;
                }
            }

            bool rKeyPressed = false;
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                rKeyPressed = UnityEngine.InputSystem.Keyboard.current.rKey.isPressed;
            }
            else
            {
                rKeyPressed = Input.GetKey(KeyCode.R);
            }

            if (!rKeyPressed && !isRShootPending)
            {
                Debug.Log("[LeoPlayer] R key released, setting localIsAimingR to false.");
                SetAimingR(false);
            }
            else if (Input.GetMouseButtonDown(0))
            {
                if (!IsUIBlockingInput())
                {
                    isRShootPending = true;
                    isPendingRShootNetworkMode = !isStandaloneMode;
                    
                    pendingRShootPosition = aoeTargetPosition;
                    
                    string attackAnim = "SamSet";
                    PlayAnimation(attackAnim, 0.05f);
                    
                    SetAimingR(false);

                    PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                    if (hud != null)
                    {
                        hud.TriggerCooldownR();
                    }
                }
            }
        }
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
        bool isPlayingShoot = anim != null && anim.layerCount > 1 && (anim.GetCurrentAnimatorStateInfo(1).IsName("Attack1combo1") || anim.GetCurrentAnimatorStateInfo(1).IsName("Attack2combo1") || anim.GetCurrentAnimatorStateInfo(1).IsName("SamSet"));
        bool keepWeight = aiming || isRShootPending || isPlayingShoot;
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            anim.SetBool("IsAiming", aiming || isRShootPending);
            if (anim.layerCount > 1)
            {
                if (keepWeight)
                {
                    anim.SetLayerWeight(1, 1f);
                }
                else if (comboStep == 0)
                {
                    var carrier = GetComponent<PlayerLogCarrier>();
                    bool isCarrying = carrier != null && carrier.isCarrying;
                    if (!isCarrying)
                    {
                        anim.SetLayerWeight(1, 0f);
                        anim.Play("New State", 1, 0f);
                    }
                }
            }
        }

        if (aiming)
        {
            GameObject previewPrefab = rSkillHandPreviewPrefab != null ? rSkillHandPreviewPrefab : rSkillLightningPrefab;
            if (previewPrefab != null && rSkillLightningSpawnPoint != null && rSkillHandPreviewVisual == null)
            {
                rSkillHandPreviewVisual = Instantiate(previewPrefab, rSkillLightningSpawnPoint.position, rSkillLightningSpawnPoint.rotation, rSkillLightningSpawnPoint);
                rSkillHandPreviewVisual.transform.localPosition = Vector3.zero;
                rSkillHandPreviewVisual.transform.localRotation = Quaternion.identity;
                rSkillHandPreviewVisual.transform.localScale = previewPrefab.transform.localScale;
                
                if (rSkillHandPreviewVisual.TryGetComponent<LeoLightningProjectile>(out var proj))
                {
                    proj.enabled = false;
                }
                if (rSkillHandPreviewVisual.TryGetComponent<Collider>(out var col))
                {
                    col.enabled = false;
                }
                if (rSkillHandPreviewVisual.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.isKinematic = true;
                }
            }
        }
        else
        {
            if (rSkillHandPreviewVisual != null)
            {
                Destroy(rSkillHandPreviewVisual);
                rSkillHandPreviewVisual = null;
            }
        }

        bool isLocal = isStandaloneMode || (IsSpawned && IsOwner);
        if (isLocal)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.SetCrosshairVisible(false);
            }
        }
    }

    [ServerRpc]
    private void SetAimingServerRpc(bool aiming)
    {
        isAimingNet.Value = aiming;
    }

    public void OnRSkillWeaponGlow()
    {
        OnShootRSkill();
    }

    public void OnShootRSkill()
    {
        if (!isRShootPending) return;
        isRShootPending = false;

        OnAimStateChanged(localIsAimingR);

        // Chỉ Owner hoặc Standalone mới bắn đạn
        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (!hasControl) return;

        Debug.Log($"[{gameObject.name}] OnShootRSkill: Hoạt ảnh bắn sét -> Spawn tại mục tiêu dưới đất!");

        Vector3 spawnPos = pendingRShootPosition + Vector3.up * 0.1f;
        Vector3 shootDirection = transform.forward;

        if (isPendingRShootNetworkMode)
        {
            SpawnLightningProjectileServerRpc(spawnPos, shootDirection);
        }
        else
        {
            SpawnLightningProjectileLocal(spawnPos, shootDirection);
        }
    }

    private void SpawnLightningProjectileLocal(Vector3 spawnPos, Vector3 shootDirection)
    {
        if (rSkillLightningPrefab == null)
        {
            Debug.LogError("[LeoPlayer] rSkillLightningPrefab chưa được gán trong Inspector!");
            return;
        }

        GameObject lightningObj = Instantiate(rSkillLightningPrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        lightningObj.transform.localScale = rSkillLightningPrefab.transform.localScale;
        lightningObj.SetActive(true);

        if (lightningObj.TryGetComponent<LeoLightningProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = rSkillLightningDamage;
        }
    }

    [ServerRpc]
    private void SpawnLightningProjectileServerRpc(Vector3 spawnPos, Vector3 shootDirection)
    {
        if (rSkillLightningPrefab == null)
        {
            Debug.LogError("[LeoPlayer] rSkillLightningPrefab chưa được gán trên Server!");
            return;
        }

        float horizontalDist = Vector3.Distance(new Vector3(spawnPos.x, 0f, spawnPos.z), new Vector3(transform.position.x, 0f, transform.position.z));
        if (horizontalDist > 55f)
        {
            spawnPos = rSkillLightningSpawnPoint != null ? rSkillLightningSpawnPoint.position : transform.position + transform.forward * 1.5f + Vector3.up * 1.0f;
        }

        GameObject lightningObj = Instantiate(rSkillLightningPrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        lightningObj.transform.localScale = rSkillLightningPrefab.transform.localScale;
        lightningObj.SetActive(true);

        if (lightningObj.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn(true);
        }

        if (lightningObj.TryGetComponent<LeoLightningProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = rSkillLightningDamage;
        }
    }



    // ======================================================
    // LOGIC TĂNG TỐC ĐỘ CHÉM & NHUỘM ĐỎ KIẾM (SKILL E)
    // ======================================================
    public void TriggerAttackSpeedBoostSkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        if (PlayerLevel < 10 && !IsSkillsUnlocked) return;
        if (IsAttackSpeedBoosted) return;

        if (isStandaloneMode)
        {
            attackSpeedBoostTimeRemaining = 5f;
            SetGhostVisuals(true);
        }
        else if (IsOwner)
        {
            TriggerAttackSpeedBoostServerRpc(true);
        }
    }

    [ServerRpc]
    private void TriggerAttackSpeedBoostServerRpc(bool state)
    {
        isAttackSpeedBoostedNet.Value = state;
        TriggerAttackSpeedBoostClientRpc(state);
        if (state)
        {
            attackSpeedBoostTimeRemaining = 5f;
        }
    }

    [ClientRpc]
    private void TriggerAttackSpeedBoostClientRpc(bool state)
    {
        SetGhostVisuals(state);
    }



    private void OnAttackSpeedBoostedChanged(bool oldVal, bool newVal)
    {
        SetGhostVisuals(newVal);
        if (newVal)
        {
            attackSpeedBoostTimeRemaining = 5f;
        }
        else
        {
            attackSpeedBoostTimeRemaining = 0f;
            if (IsOwner)
            {
                PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
                if (hud != null) hud.TriggerCooldownE();
            }
        }
    }

    private void SetSwordRedVisuals(bool active)
    {
        if (active)
        {
            if (originalSwordMaterials.Count > 0) return; // Đã nhuộm đỏ rồi

            GameObject[] swords = { leftHandSword, rightHandSword, leftShoulderSword, rightShoulderSword };
            foreach (var sword in swords)
            {
                if (sword == null) continue;
                Renderer[] renderers = sword.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r is SkinnedMeshRenderer || r is MeshRenderer)
                    {
                        originalSwordMaterials[r] = r.sharedMaterials;

                        if (redSwordMaterial != null)
                        {
                            Material[] newMats = new Material[r.sharedMaterials.Length];
                            for (int i = 0; i < newMats.Length; i++)
                            {
                                newMats[i] = redSwordMaterial;
                            }
                            r.materials = newMats;
                        }
                        else
                        {
                            Material[] newMats = new Material[r.sharedMaterials.Length];
                            for (int i = 0; i < newMats.Length; i++)
                            {
                                Material originalMat = r.sharedMaterials[i];
                                if (originalMat != null)
                                {
                                    Material tempMat = new Material(originalMat);
                                    tempMat.EnableKeyword("_EMISSION");
                                    if (swordEmissiveMap != null)
                                    {
                                        tempMat.SetTexture("_EmissionMap", swordEmissiveMap);
                                    }
                                    tempMat.SetColor("_EmissionColor", redEmissiveColor);
                                    newMats[i] = tempMat;
                                }
                                else
                                {
                                    newMats[i] = null;
                                }
                            }
                            r.materials = newMats;
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var kvp in originalSwordMaterials)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.materials = kvp.Value;
                }
            }
            originalSwordMaterials.Clear();
        }
    }

    private void SetGhostVisuals(bool active)
    {
        if (active)
        {
            if (originalBodyMaterials.Count > 0) return; // Đã nhuộm ghost rồi

            Material ghostMat = ghostBodyMaterial;
            if (ghostMat == null)
            {
                Shader ghostShader = Shader.Find("Sprites/Default");
                if (ghostShader == null) ghostShader = Shader.Find("GUI/Text Shader");
                if (ghostShader == null) ghostShader = Shader.Find("Universal Render Pipeline/Lit");
                if (ghostShader == null) ghostShader = Shader.Find("Standard");
                
                ghostMat = new Material(ghostShader);
                ghostMat.name = "DynamicWhiteGhostMaterial";
                
                // Đặt màu trắng mờ (transparent alpha)
                ghostMat.color = new Color(1f, 1f, 1f, 0.6f);
                
                // Cấu hình Standard Shader để hỗ trợ Transparent
                if (ghostMat.HasProperty("_Mode")) ghostMat.SetFloat("_Mode", 3f); // Transparent
                if (ghostMat.HasProperty("_SrcBlend")) ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (ghostMat.HasProperty("_DstBlend")) ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (ghostMat.HasProperty("_ZWrite")) ghostMat.SetInt("_ZWrite", 0);
                ghostMat.DisableKeyword("_ALPHATEST_ON");
                ghostMat.EnableKeyword("_ALPHABLEND_ON");
                ghostMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                ghostMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                
                // Cấu hình URP Lit Shader để hỗ trợ Transparent
                if (ghostMat.HasProperty("_Surface")) ghostMat.SetFloat("_Surface", 1f); // Transparent
                if (ghostMat.HasProperty("_Blend")) ghostMat.SetFloat("_Blend", 0f);   // Alpha
                if (ghostMat.HasProperty("_SrcBlend")) ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (ghostMat.HasProperty("_DstBlend")) ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (ghostMat.HasProperty("_ZWrite")) ghostMat.SetInt("_ZWrite", 0);
                
                // Kích hoạt Emission màu trắng rực rỡ để tạo hiệu ứng "hồn ma phát sáng"
                ghostMat.EnableKeyword("_EMISSION");
                if (ghostMat.HasProperty("_EmissionColor")) ghostMat.SetColor("_EmissionColor", new Color(1.5f, 1.5f, 1.5f, 1.0f));
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // Bỏ qua particle system, các indicator chỉ hướng hoặc line vẽ AOE
                if (r is ParticleSystemRenderer || r.gameObject.name.Contains("VFX") || r.gameObject.name.Contains("Indicator") || r.gameObject.name.Contains("Line"))
                    continue;

                if (r is SkinnedMeshRenderer || r is MeshRenderer)
                {
                    if (!originalBodyMaterials.ContainsKey(r))
                    {
                        originalBodyMaterials[r] = r.sharedMaterials;
                    }

                    Material[] newMats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < newMats.Length; i++)
                    {
                        newMats[i] = ghostMat;
                    }
                    r.materials = newMats;
                }
            }
        }
        else
        {
            foreach (var kvp in originalBodyMaterials)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.materials = kvp.Value;
                }
            }
            originalBodyMaterials.Clear();
        }
    }

    // ======================================================
    // LOGIC ẢO ẢNH CHÉM (SKILL Q)
    // ======================================================

    /// <summary>
    /// Ẩn/Hiện toàn bộ renderer của Leo (cơ thể + kiếm) trừ particle systems.
    /// Được gọi khi bắt đầu/kết thúc Skill Q.
    /// </summary>
    private void SetLeoRenderersActive(bool active)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            // Bỏ qua ParticleSystemRenderer để giữ lại VFX chém
            if (r is ParticleSystemRenderer) continue;
            r.enabled = active;
        }
    }

    /// <summary>
    /// Tìm mục tiêu gần nhất trong bán kính qSkillSearchRadius, không kể các Enemy đã chết.
    /// Trả về Transform của mục tiêu hoặc null nếu không tìm thấy.
    /// </summary>
    private Transform FindNearestAliveEnemy()
    {
        float minDist = float.MaxValue;
        Transform nearest = null;

        // Kiểm tra từng loại Enemy
        Enemy1_DapBua[] e1s = FindObjectsByType<Enemy1_DapBua>(FindObjectsSortMode.None);
        foreach (var e in e1s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d <= qSkillSearchRadius && d < minDist) { minDist = d; nearest = e.transform; }
        }
        Enemy2_Zombie[] e2s = FindObjectsByType<Enemy2_Zombie>(FindObjectsSortMode.None);
        foreach (var e in e2s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d <= qSkillSearchRadius && d < minDist) { minDist = d; nearest = e.transform; }
        }
        Enemy3_Buaa[] e3s = FindObjectsByType<Enemy3_Buaa>(FindObjectsSortMode.None);
        foreach (var e in e3s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d <= qSkillSearchRadius && d < minDist) { minDist = d; nearest = e.transform; }
        }
        Enemy4_Bongtoi[] e4s = FindObjectsByType<Enemy4_Bongtoi>(FindObjectsSortMode.None);
        foreach (var e in e4s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d <= qSkillSearchRadius && d < minDist) { minDist = d; nearest = e.transform; }
        }
        Enemy5_PhuThuy[] e5s = FindObjectsByType<Enemy5_PhuThuy>(FindObjectsSortMode.None);
        foreach (var e in e5s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d <= qSkillSearchRadius && d < minDist) { minDist = d; nearest = e.transform; }
        }
        return nearest;
    }

    /// <summary>
    /// Gây sát thương lên một Enemy cụ thể dựa trên Transform của nó.
    /// </summary>
    private void TryDamageSpecificEnemy(Transform enemyTransform, float damage)
    {
        if (enemyTransform == null) return;
        var e1 = enemyTransform.GetComponentInParent<Enemy1_DapBua>() ?? enemyTransform.GetComponentInChildren<Enemy1_DapBua>();
        if (e1 != null) { e1.TakeDamage(damage); return; }
        var e2 = enemyTransform.GetComponentInParent<Enemy2_Zombie>() ?? enemyTransform.GetComponentInChildren<Enemy2_Zombie>();
        if (e2 != null) { e2.TakeDamage(damage); return; }
        var e3 = enemyTransform.GetComponentInParent<Enemy3_Buaa>() ?? enemyTransform.GetComponentInChildren<Enemy3_Buaa>();
        if (e3 != null) { e3.TakeDamage(damage); return; }
        var e4 = enemyTransform.GetComponentInParent<Enemy4_Bongtoi>() ?? enemyTransform.GetComponentInChildren<Enemy4_Bongtoi>();
        if (e4 != null) { e4.TakeDamage(damage); return; }
        var e5 = enemyTransform.GetComponentInParent<Enemy5_PhuThuy>() ?? enemyTransform.GetComponentInChildren<Enemy5_PhuThuy>();
        if (e5 != null) { e5.TakeDamage(damage); return; }
    }

    /// <summary>
    /// Kiểm tra xem một Transform Enemy có còn sống không.
    /// </summary>
    private bool IsEnemyDead(Transform enemyTransform)
    {
        if (enemyTransform == null) return true;
        var e1 = enemyTransform.GetComponentInParent<Enemy1_DapBua>() ?? enemyTransform.GetComponentInChildren<Enemy1_DapBua>();
        if (e1 != null) return e1.IsDead;
        var e2 = enemyTransform.GetComponentInParent<Enemy2_Zombie>() ?? enemyTransform.GetComponentInChildren<Enemy2_Zombie>();
        if (e2 != null) return e2.IsDead;
        var e3 = enemyTransform.GetComponentInParent<Enemy3_Buaa>() ?? enemyTransform.GetComponentInChildren<Enemy3_Buaa>();
        if (e3 != null) return e3.IsDead;
        var e4 = enemyTransform.GetComponentInParent<Enemy4_Bongtoi>() ?? enemyTransform.GetComponentInChildren<Enemy4_Bongtoi>();
        if (e4 != null) return e4.IsDead;
        var e5 = enemyTransform.GetComponentInParent<Enemy5_PhuThuy>() ?? enemyTransform.GetComponentInChildren<Enemy5_PhuThuy>();
        if (e5 != null) return e5.IsDead;
        return true; // Không tìm thấy component = coi như chết
    }

    /// <summary>
    /// Kích hoạt Skill Q. Trả về true nếu kích hoạt thành công, false nếu không có mục tiêu.
    /// </summary>
    public bool TriggerQSkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return false;

        if (PlayerLevel < 15 && !IsSkillsUnlocked) return false;
        if (IsQSkillActive) return false;

        if (isStandaloneMode)
        {
            Transform target = FindNearestAliveEnemy();
            if (target == null)
            {
                Debug.Log("[LeoPlayer] Q Skill: Không có enemy trong tầm, hủy kỹ năng.");
                return false;
            }
            StartCoroutine(QSkillCoroutineStandalone(target));
            return true;
        }
        else if (IsOwner)
        {
            TriggerQSkillServerRpc();
            return true; // Lạc quan, server sẽ xác nhận lại
        }
        return false;
    }

    [ServerRpc]
    private void TriggerQSkillServerRpc()
    {
        Transform target = FindNearestAliveEnemy();
        if (target == null)
        {
            Debug.Log("[LeoPlayer Server] Q Skill: Không có enemy trong tầm, hủy kỹ năng.");
            // Báo lại client để không tính hồi chiêu - thông qua ClientRpc đặc biệt
            QSkillCancelledClientRpc();
            return;
        }
        StartCoroutine(QSkillCoroutineServer(target));
    }

    [ClientRpc]
    private void QSkillCancelledClientRpc()
    {
        Debug.Log("[LeoPlayer Client] Q Skill bị hủy vì không có enemy.");
        // Thông báo cho HUD reset cooldown
        OnQSkillCancelled?.Invoke();
    }

    /// <summary>
    /// Coroutine chạy Skill Q trong chế độ Standalone.
    /// </summary>
    private System.Collections.IEnumerator QSkillCoroutineStandalone(Transform initialTarget)
    {
        isQSkillActiveLocal = true;
        qSkillTimeRemaining = qSkillDuration;
        SetLeoRenderersActive(false);
        isMovementLocked = true;

        if (rb != null) rb.linearVelocity = Vector3.zero;

        Transform currentTarget = initialTarget;
        float interval = qSkillDuration / Mathf.Max(qSkillSlashCount, 1);

        for (int i = 0; i < qSkillSlashCount; i++)
        {
            // Kiểm tra mục tiêu hiện tại
            if (currentTarget == null || IsEnemyDead(currentTarget))
            {
                currentTarget = FindNearestAliveEnemy();
                if (currentTarget == null)
                {
                    Debug.Log("[LeoPlayer] Q Skill: Không còn enemy, kết thúc sớm.");
                    break;
                }
            }

            // Dịch chuyển xung quanh mục tiêu (vị trí ngẫu nhiên bán kính 1.5m)
            Vector2 offset2D = UnityEngine.Random.insideUnitCircle.normalized * 1.5f;
            Vector3 slashPos = currentTarget.position + new Vector3(offset2D.x, 0f, offset2D.y);
            slashPos.y = transform.position.y;
            transform.position = slashPos;

            // Xoay mặt về phía mục tiêu
            Vector3 dir = (currentTarget.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir.normalized);

            // Gây sát thương
            TryDamageSpecificEnemy(currentTarget, qSkillDamagePerSlash);

            // Phát VFX chém cục bộ ở tâm mục tiêu
            Vector3 targetCenter = GetTargetCenterPosition(currentTarget);
            SpawnQSlashVfxLocal(targetCenter);

            qSkillTimeRemaining -= interval;
            yield return new WaitForSeconds(interval);
        }

        // Kết thúc Skill Q
        isQSkillActiveLocal = false;
        qSkillTimeRemaining = 0f;
        isMovementLocked = false;
        SetLeoRenderersActive(true);
    }

    /// <summary>
    /// Coroutine chạy Skill Q trên Server (Multiplayer).
    /// </summary>
    private System.Collections.IEnumerator QSkillCoroutineServer(Transform initialTarget)
    {
        isQSkillActiveNet.Value = true;
        SetQSkillStateClientRpc(true);
        SetMovementLockServerSide(true);

        float interval = qSkillDuration / Mathf.Max(qSkillSlashCount, 1);
        float remaining = qSkillDuration;

        // Đồng bộ timer về client owner
        UpdateQTimerClientRpc(remaining);

        Transform currentTarget = initialTarget;

        for (int i = 0; i < qSkillSlashCount; i++)
        {
            // Kiểm tra mục tiêu hiện tại
            if (currentTarget == null || IsEnemyDead(currentTarget))
            {
                currentTarget = FindNearestAliveEnemy();
                if (currentTarget == null)
                {
                    Debug.Log("[LeoPlayer Server] Q Skill: Không còn enemy, kết thúc sớm.");
                    break;
                }
            }

            // Dịch chuyển xung quanh mục tiêu
            Vector2 offset2D = UnityEngine.Random.insideUnitCircle.normalized * 1.5f;
            Vector3 slashPos = currentTarget.position + new Vector3(offset2D.x, 0f, offset2D.y);
            slashPos.y = transform.position.y;
            transform.position = slashPos;

            // Xoay mặt về phía mục tiêu
            Vector3 dir = (currentTarget.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir.normalized);

            // Gây sát thương
            TryDamageSpecificEnemy(currentTarget, qSkillDamagePerSlash);

            // Gọi ClientRpc để phát VFX chém ở tất cả client tại tâm mục tiêu
            Vector3 targetCenter = GetTargetCenterPosition(currentTarget);
            PlayQSlashVfxClientRpc(targetCenter);

            remaining -= interval;
            UpdateQTimerClientRpc(Mathf.Max(0f, remaining));
            yield return new WaitForSeconds(interval);
        }

        // Kết thúc Skill Q
        isQSkillActiveNet.Value = false;
        SetQSkillStateClientRpc(false);
        SetMovementLockServerSide(false);
        UpdateQTimerClientRpc(0f);
    }

    private void SetMovementLockServerSide(bool locked)
    {
        if (IsServer)
        {
            isMovementLockedNet.Value = locked;
        }
    }

    [ClientRpc]
    private void SetQSkillStateClientRpc(bool active)
    {
        qSkillTimeRemaining = active ? qSkillDuration : 0f;
        SetLeoRenderersActive(!active);
    }

    [ClientRpc]
    private void UpdateQTimerClientRpc(float remaining)
    {
        qSkillTimeRemaining = remaining;
    }

    [ClientRpc]
    private void PlayQSlashVfxClientRpc(Vector3 targetPos)
    {
        SpawnQSlashVfxLocal(targetPos);
    }

    private Vector3 GetTargetCenterPosition(Transform target)
    {
        if (target == null) return Vector3.zero;

        // Thử tìm CapsuleCollider hoặc CharacterController trước
        var cc = target.GetComponent<CharacterController>();
        if (cc == null) cc = target.GetComponentInChildren<CharacterController>();
        if (cc != null)
        {
            return target.position + Vector3.up * (cc.height * 0.5f);
        }

        var capsule = target.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = target.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            return target.position + Vector3.up * (capsule.height * 0.5f);
        }

        var box = target.GetComponent<BoxCollider>();
        if (box == null) box = target.GetComponentInChildren<BoxCollider>();
        if (box != null)
        {
            return target.position + Vector3.up * (box.size.y * 0.5f);
        }

        // Nếu không có collider, thử tìm renderer bounds
        var renderer = target.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds.center;
        }

        // Fallback mặc định
        return target.position + Vector3.up * 1.0f;
    }

    /// <summary>
    /// Phát VFX vết chém ảo ảnh tại vị trí mục tiêu.
    /// Ưu tiên dùng qSkillParticlePrefab, fallback về pool VFX cũ.
    /// </summary>
    private void SpawnQSlashVfxLocal(Vector3 targetPos)
    {
        Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        Vector3 spawnPos = targetPos; // Đã là tâm mục tiêu được tính từ trước

        // Tăng transform to lên (Scale 1.8f) per user request
        Vector3 qScale = new Vector3(1.8f, 1.8f, 1.8f);

        // Dùng particle riêng nếu đã gán trong Inspector
        if (qSkillParticlePrefab != null)
        {
            GetPooledVFX(qSkillParticlePrefab, spawnPos, rot, qScale);
            return;
        }

        // Fallback: chọn ngẫu nhiên trong pool VFX cũ
        GameObject[] vfxOptions = {
            leftSlashVfxPrefab,
            rightSlashVfxPrefab,
            dualSlashVfxPrefab
        };
        var validVfx = System.Array.FindAll(vfxOptions, v => v != null);
        if (validVfx.Length == 0) return;

        GameObject chosenPrefab = validVfx[UnityEngine.Random.Range(0, validVfx.Length)];
        GetPooledVFX(chosenPrefab, spawnPos, rot, qScale);
    }

    private void OnQSkillActiveChanged(bool oldVal, bool newVal)
    {
        // Cập nhật visual khi nhận tín hiệu từ network (không phải owner chạy coroutine)
        if (!IsOwner)
        {
            SetLeoRenderersActive(!newVal);
        }
        if (newVal)
        {
            qSkillTimeRemaining = qSkillDuration;
        }
        else
        {
            qSkillTimeRemaining = 0f;
        }
    }

    public void UpdateStateFromHUD(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
    {
        if (isStandaloneMode)
        {
            localActiveWeaponIndex = weaponIndex;
            localWeapon2Locked = weapon2Locked;
            localSkillsUnlocked = skillsUnlocked;

            int oldWeapon = GetActiveWeaponIndex();
            PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
            if (hud != null)
            {
                hud.currentSelectedWeapon = weaponIndex;
                hud.SetWeapon2Locked(weapon2Locked, true);
                hud.SetSkillsUnlocked(skillsUnlocked, true);
            }
            PlayWeaponSwitchAnimation(oldWeapon, weaponIndex);
            return;
        }

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
        if (!isStandaloneMode && (!IsSpawned || !IsOwner)) return;

        Debug.Log("[LeoPlayer DB] Saving character state to MongoDB...");
        try
        {
            bool weapon2LockedVal = isWeapon2Locked.Value;
            bool skillsUnlockedVal = isSkillsUnlocked.Value;
            int weaponIndexVal = activeWeaponIndex.Value;
            int upgradePtsVal = upgradePoints.Value;
            int hpLvVal = hpLevel.Value;
            int mpLvVal = mpLevel.Value;
            int cdLvVal = cooldownLevel.Value;
            int dmgLvVal = damageLevel.Value;
            int plLvVal = playerLevel.Value;
            float plExpVal = playerExp.Value;
            float hpVal = CurrentHealth;

            if (isStandaloneMode)
            {
                upgradePtsVal = localUpgradePoints;
                hpLvVal = localHpLevel;
                mpLvVal = localMpLevel;
                cdLvVal = localCooldownLevel;
                dmgLvVal = localDamageLevel;
                plLvVal = localLevel;
                plExpVal = localExp;
                hpVal = localHealth;
                weapon2LockedVal = localWeapon2Locked;
                skillsUnlockedVal = localSkillsUnlocked;
                weaponIndexVal = localActiveWeaponIndex;
            }

            var stateData = new PlayerStateData
            {
                health = hpVal,
                activeWeaponIndex = weaponIndexVal,
                isWeapon2Locked = weapon2LockedVal,
                isSkillsUnlocked = skillsUnlockedVal,
                inventorySlots = inventorySlots,
                upgradePoints = upgradePtsVal,
                hpLevel = hpLvVal,
                mpLevel = mpLvVal,
                cooldownLevel = cdLvVal,
                damageLevel = dmgLvVal,
                playerLevel = plLvVal,
                playerExp = plExpVal
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

    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerNameServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        playerName.Value = name;
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

                if (isStandaloneMode)
                {
                    localUpgradePoints = state.upgradePoints;
                    localHpLevel = state.hpLevel;
                    localMpLevel = state.mpLevel;
                    localCooldownLevel = state.cooldownLevel;
                    localDamageLevel = state.damageLevel;
                    localLevel = state.playerLevel;
                    localExp = state.playerExp;
                    localHealth = state.health;
                    localWeapon2Locked = state.isWeapon2Locked;
                    localSkillsUnlocked = state.isSkillsUnlocked;
                    localActiveWeaponIndex = state.activeWeaponIndex;
                }
                else
                {
                    SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, state.upgradePoints);
                    SyncNetVarInt(hpLevel, proxyPlayerTest != null ? proxyPlayerTest.hpLevel : null, state.hpLevel);
                    SyncNetVarInt(mpLevel, proxyPlayerTest != null ? proxyPlayerTest.mpLevel : null, state.mpLevel);
                    SyncNetVarInt(cooldownLevel, proxyPlayerTest != null ? proxyPlayerTest.cooldownLevel : null, state.cooldownLevel);
                    SyncNetVarInt(damageLevel, proxyPlayerTest != null ? proxyPlayerTest.damageLevel : null, state.damageLevel);
                    SyncNetVarInt(playerLevel, proxyPlayerTest != null ? proxyPlayerTest.playerLevel : null, state.playerLevel);
                    SyncNetVarFloat(playerExp, proxyPlayerTest != null ? proxyPlayerTest.playerExp : null, state.playerExp);
                    SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, state.health);
                    SyncNetVarInt(activeWeaponIndex, proxyPlayerTest != null ? proxyPlayerTest.activeWeaponIndex : null, state.activeWeaponIndex);
                    SyncNetVarBool(isWeapon2Locked, proxyPlayerTest != null ? proxyPlayerTest.isWeapon2Locked : null, state.isWeapon2Locked);
                    SyncNetVarBool(isSkillsUnlocked, proxyPlayerTest != null ? proxyPlayerTest.isSkillsUnlocked : null, state.isSkillsUnlocked);
                }

                maxHealth = 85f + state.hpLevel * 20f;
                damageAmount = 25f + state.damageLevel * 5f;

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
                    hud.SetSkillsUnlocked(state.isSkillsUnlocked, false);
                    hud.SetWeapon2Locked(state.isWeapon2Locked, false);
                    hud.SelectWeapon(state.activeWeaponIndex);
                    hud.UpdateUpgradeUI(state.upgradePoints, state.hpLevel, state.mpLevel, state.cooldownLevel, state.damageLevel);
                    hud.SetHealth(state.health / (85f + state.hpLevel * 20f));

                    float needed = 100f + state.playerLevel * 50f;
                    hud.UpdateExperienceUI(state.playerLevel, state.playerExp, needed);
                }
            }
            else
            {
                if (isStandaloneMode)
                {
                    localHealth = maxHealth;
                    localActiveWeaponIndex = 1;
                    localWeapon2Locked = true;
                    localSkillsUnlocked = false;
                    localUpgradePoints = 0;
                    localHpLevel = 0;
                    localMpLevel = 0;
                    localCooldownLevel = 0;
                    localDamageLevel = 0;
                    localLevel = 0;
                    localExp = 0f;
                }
                else
                {
                    SyncNetVarFloat(currentHealth, proxyPlayerTest != null ? proxyPlayerTest.currentHealth : null, maxHealth);
                    SyncNetVarInt(activeWeaponIndex, proxyPlayerTest != null ? proxyPlayerTest.activeWeaponIndex : null, 1);
                    SyncNetVarBool(isWeapon2Locked, proxyPlayerTest != null ? proxyPlayerTest.isWeapon2Locked : null, true);
                    SyncNetVarBool(isSkillsUnlocked, proxyPlayerTest != null ? proxyPlayerTest.isSkillsUnlocked : null, false);
                    SyncNetVarInt(upgradePoints, proxyPlayerTest != null ? proxyPlayerTest.upgradePoints : null, 0);
                    SyncNetVarInt(hpLevel, proxyPlayerTest != null ? proxyPlayerTest.hpLevel : null, 0);
                    SyncNetVarInt(mpLevel, proxyPlayerTest != null ? proxyPlayerTest.mpLevel : null, 0);
                    SyncNetVarInt(cooldownLevel, proxyPlayerTest != null ? proxyPlayerTest.cooldownLevel : null, 0);
                    SyncNetVarInt(damageLevel, proxyPlayerTest != null ? proxyPlayerTest.damageLevel : null, 0);
                    SyncNetVarInt(playerLevel, proxyPlayerTest != null ? proxyPlayerTest.playerLevel : null, 0);
                    SyncNetVarFloat(playerExp, proxyPlayerTest != null ? proxyPlayerTest.playerExp : null, 0f);
                }
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
            if (newHealth <= 0f && oldHealth > 0f)
            {
                // Defer death effect to OnDeathAnimationEnd
            }
            else if (newHealth > 0f && oldHealth <= 0f)
            {
                PlayerDeathEffectManager.Instance.ResetDeathEffect();
            }
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

    public void PlayWeaponSwitchAnimation(int oldWeapon, int newWeapon)
    {
        if (oldWeapon == newWeapon) return;

        isSwitchingWeapon = true;

        if (newWeapon == 2)
        {
            // Bước 1: Hiện kiếm trên vai, ẩn kiếm trên tay (chuẩn bị rút)
            if (leftShoulderSword != null) leftShoulderSword.SetActive(true);
            if (rightShoulderSword != null) rightShoulderSword.SetActive(true);
            if (leftHandSword != null) leftHandSword.SetActive(false);
            if (rightHandSword != null) rightHandSword.SetActive(false);

            // Dùng Coroutine để rút 2 kiếm tuần tự, đảm bảo cả hai đều được rút ra
            StartCoroutine(DrawBothSwordsSequence());
        }
        else if (newWeapon == 1)
        {
            // Bước 1: Hiện kiếm trên tay, ẩn kiếm trên vai (chuẩn bị cất)
            if (leftHandSword != null) leftHandSword.SetActive(true);
            if (rightHandSword != null) rightHandSword.SetActive(true);
            if (leftShoulderSword != null) leftShoulderSword.SetActive(false);
            if (rightShoulderSword != null) rightShoulderSword.SetActive(false);

            // Dùng Coroutine để cất 2 kiếm tuần tự
            StartCoroutine(SheatheBothSwordsSequence());
        }
    }

    private System.Collections.IEnumerator DrawBothSwordsSequence()
    {
        // Bước 1: Chơi animation Rút Kiếm Trái
        if (!string.IsNullOrEmpty(drawLeftTrigger) && anim != null && anim.isActiveAndEnabled)
        {
            PlayAnimation(drawLeftTrigger, 0.1f);

            // Đợi cho animation DrawLeft chạy xong
            float drawLeftDuration = GetAnimationClipLength(drawLeftTrigger);
            if (drawLeftDuration <= 0f) drawLeftDuration = 0.8f; // fallback
            yield return new WaitForSeconds(drawLeftDuration * 0.85f);

            // Kiếm trái xuất hiện trên tay
            DrawLeftSword();
        }
        else
        {
            DrawLeftSword();
        }

        // Bước 2: Chơi animation Rút Kiếm Phải
        if (!string.IsNullOrEmpty(drawRightTrigger) && anim != null && anim.isActiveAndEnabled)
        {
            PlayAnimation(drawRightTrigger, 0.1f);

            float drawRightDuration = GetAnimationClipLength(drawRightTrigger);
            if (drawRightDuration <= 0f) drawRightDuration = 0.8f;
            yield return new WaitForSeconds(drawRightDuration * 0.85f);

            // Kiếm phải xuất hiện trên tay
            DrawRightSword();
        }
        else
        {
            DrawRightSword();
        }

        // Hàn thành: Đảm bảo cả 2 kiếm đều được hiển thị đúng
        SyncWeaponVisuals(2);
        OnWeaponSwitchEnd();
    }

    private System.Collections.IEnumerator SheatheBothSwordsSequence()
    {
        // Bước 1: Cất kiếm trái
        if (!string.IsNullOrEmpty(sheatheLeftTrigger) && anim != null && anim.isActiveAndEnabled)
        {
            PlayAnimation(sheatheLeftTrigger, 0.1f);

            float duration = GetAnimationClipLength(sheatheLeftTrigger);
            if (duration <= 0f) duration = 0.8f;
            yield return new WaitForSeconds(duration * 0.85f);

            SheatheLeftSword();
        }
        else
        {
            SheatheLeftSword();
        }

        // Bước 2: Cất kiếm phải
        if (!string.IsNullOrEmpty(sheatheRightTrigger) && anim != null && anim.isActiveAndEnabled)
        {
            PlayAnimation(sheatheRightTrigger, 0.1f);

            float duration = GetAnimationClipLength(sheatheRightTrigger);
            if (duration <= 0f) duration = 0.8f;
            yield return new WaitForSeconds(duration * 0.85f);

            SheatheRightSword();
        }
        else
        {
            SheatheRightSword();
        }

        // Hàn thành
        SyncWeaponVisuals(1);
        OnWeaponSwitchEnd();
    }

    /// <summary>
    /// Lấy thời lượng (giây) của animation clip theo tên trigger trong Animator.
    /// Nếu không tìm thấy, trả về 0.
    /// </summary>
    private float GetAnimationClipLength(string triggerName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return 0f;
        string searchName = triggerName;
        if (string.Equals(triggerName, "ChatRiu", System.StringComparison.OrdinalIgnoreCase)) searchName = "Chat Cayy";
        else if (string.Equals(triggerName, "DrawLeft", System.StringComparison.OrdinalIgnoreCase)) searchName = "laykiemtaytrai";
        else if (string.Equals(triggerName, "DrawRight", System.StringComparison.OrdinalIgnoreCase)) searchName = "Laykiemtayphai";
        else if (string.Equals(triggerName, "SheatheLeft", System.StringComparison.OrdinalIgnoreCase)) searchName = "catkiemtaytrai";
        else if (string.Equals(triggerName, "SheatheRight", System.StringComparison.OrdinalIgnoreCase)) searchName = "catkiemtayphai";

        foreach (var clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip != null && (clip.name == searchName || clip.name.ToLower() == searchName.ToLower()))
            {
                return clip.length;
            }
        }
        return 0f;
    }

    public void OnDrawLeftEnd()
    {
        // Không phụ thuộc Animator native transition nữa.
        // Coroutine DrawBothSwordsSequence tự quản lý luồng rút kiếm.
        Debug.Log("[LeoPlayer] OnDrawLeftEnd (Animation Event) - Coroutine đang xử lý DrawRight.");
    }

    public void OnSheatheLeftEnd()
    {
        // Không phụ thuộc Animator native transition nữa.
        // Coroutine SheatheBothSwordsSequence tự quản lý luồng cất kiếm.
        Debug.Log("[LeoPlayer] OnSheatheLeftEnd (Animation Event) - Coroutine đang xử lý SheatheRight.");
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

    public void PlayAnimation(string animName, float fadeTime = 0.1f, bool alreadyPlayedLocally = false, bool isRooted = false)
    {
        if (anim == null) return;

        // Nếu đang chết, chỉ cho phép nhận các lệnh hồi sinh hoặc đưa về trạng thái rỗng/New State
        if (currentAnimState == "Death")
        {
            if (CurrentHealth <= 0)
            {
                if (animName != "Idle" && animName != "Walk" && animName != "run" && animName != "New State" && animName != "Empty")
                {
                    return;
                }
            }
            else
            {
                currentAnimState = "";
            }
        }

        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying && animName != "Death" && animName != "Idle" && animName != "Walk" && animName != "run")
        {
            return;
        }

        if (anim == null) return;

        if (!alreadyPlayedLocally)
        {
            this.isRootedAttack = isRooted;
            PlayAnimationLocal(animName, fadeTime);
        }

        if (!isStandaloneMode)
        {
            if (IsServer)
            {
                PlayAnimationClientRpc(animName, fadeTime, alreadyPlayedLocally || IsOwner, isRooted);
            }
            else if (IsOwner)
            {
                PlayAnimationServerRpc(animName, fadeTime, isRooted);
            }
        }
    }

    private string TranslateAnimName(string animName)
    {
        int weapon = GetActiveWeaponIndex();
        bool isArmed = (weapon == 2 || weapon == 1);

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
            case "Slash3":
                return slash3Trigger;
            case "GetHit":
                return getHitTrigger;
            case "GeiHit2":
                return getHit2Trigger;
            case "Death":
                return "Death";
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
               name == slash3Trigger ||
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
               name == "Slash3" ||
               name == "attacktaytrai" ||
               name == "attacktayphai" ||
               name == "Slash1Combo2" ||
               name == "Slash2combo2" ||
               name == "Slash3combo2" || name == "ChatRiu" ||
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
               name == slash3Trigger ||
               name == "Punch1" ||
               name == "Punch2" ||
               name == "Punch3" ||
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3" ||
               name == "attacktaytrai" ||
               name == "attacktayphai" ||
               name == "Slash1Combo2" ||
               name == "Slash2combo2" ||
               name == "Slash3combo2" ||
               name == "ChatRiu";
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

        if (anim == null || !anim.isActiveAndEnabled)
            return false;

        // --- ĐÃ SỬA: Tự động quét Layer 1 (Attack), cứ thoát khỏi New State/Empty là tính đang tấn công ---
        if (anim.layerCount > 1)
        {
            AnimatorStateInfo attackLayerStateInfo = anim.GetCurrentAnimatorStateInfo(1);
            if (!attackLayerStateInfo.IsName("New State") && !attackLayerStateInfo.IsName("Empty") && !attackLayerStateInfo.IsName("Idle"))
            {
                activeState = attackLayerStateInfo;
                layer = 1;
                return true;
            }
        }

        // Dự phòng cho Base Layer
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
               stateInfo.IsName(slash3Trigger) ||
               stateInfo.IsName("Punch1") ||
               stateInfo.IsName("Punch2") ||
               stateInfo.IsName("Punch3") ||
               stateInfo.IsName("Slash1") ||
               stateInfo.IsName("Slash2") ||
               stateInfo.IsName("Slash3") ||
               stateInfo.IsName("attacktaytrai") ||
               stateInfo.IsName("attacktayphai") ||
               stateInfo.IsName("Slash1Combo2") ||
               stateInfo.IsName("Slash2combo2") ||
               stateInfo.IsName("Slash3combo2") ||
               stateInfo.IsName("ChatRiu");
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

    private System.Collections.IEnumerator ResetTriggerNextFrame(string triggerName)
    {
        yield return null;
        if (anim != null && !string.IsNullOrEmpty(triggerName))
        {
            anim.ResetTrigger(triggerName);
        }
    }

    private bool IsLeftoverEvent()
    {
        return !isExecutingAttack || (Time.time - attackAnimStartTime < 0.15f);
    }

    private void PlayAnimationLocal(string animName, float fadeTime)
    {
        // Play action sound effects
        string translatedNameForAudio = TranslateAnimName(animName);
        if (translatedNameForAudio == "Attack1combo1" || translatedNameForAudio == "Attack2combo1" || 
            animName == "Attack1combo1" || animName == "Attack2combo1" || 
            translatedNameForAudio.StartsWith("Chem") || animName.StartsWith("Chem"))
        {
            AudioClip swingClip = Resources.Load<AudioClip>("Audio/ChemChuaHit");
            PlayPlayerSFX(swingClip, 0.8f);
        }
        else if (translatedNameForAudio.StartsWith("Dam") || animName.StartsWith("Dam") || 
                 translatedNameForAudio.StartsWith("Punch") || animName.StartsWith("Punch"))
        {
            AudioClip punchClip = Resources.Load<AudioClip>("Audio/Punch");
            PlayPlayerSFX(punchClip);
        }
        else if (translatedNameForAudio == "SamSet" || animName == "SamSet")
        {
            PlayPlayerSFX(skillRClip); // Fireball / SamSet
        }
        else if (translatedNameForAudio == "Death" || animName == "Death")
        {
            PlayPlayerSFX(deathClip);
        }
        else if (translatedNameForAudio == "GetHit" || animName == "GetHit" || translatedNameForAudio == "GeiHit2" || animName == "GeiHit2")
        {
            PlayPlayerSFX(hitClip);
        }
        else if (translatedNameForAudio == "LonVong" || animName == "LonVong")
        {
            PlayPlayerSFX(Resources.Load<AudioClip>("Audio/SmokeBomb"), 0.5f); // Roll Whoosh
        }

        if (anim == null) return;

        if (useBlendTree && (animName == "Idle" || animName == "Walk" || animName == "run"))
        {
            return;
        }

        string translatedName = TranslateAnimName(animName);

        if (translatedName == "Attack1combo1" || translatedName == "Attack2combo1" || animName == "Attack1combo1" || animName == "Attack2combo1" || translatedName == "SamSet" || animName == "SamSet")
        {
            isRShootPending = true;
            isPendingRShootNetworkMode = !isStandaloneMode;
            OnAimStateChanged(localIsAimingR);
        }

        if (translatedName == rollTrigger || translatedName == "LonVong")
        {
            anim.applyRootMotion = false;
        }

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
            anim.ResetTrigger(slash3Trigger);

            anim.ResetTrigger("Punch1");
            anim.ResetTrigger("Punch2");
            anim.ResetTrigger("Punch3");
            anim.ResetTrigger("Slash1");
            anim.ResetTrigger("Slash2");
            anim.ResetTrigger("Slash3");

            anim.ResetTrigger("attacktaytrai");
            anim.ResetTrigger("attacktayphai");
            anim.ResetTrigger("Slash1Combo2");
            anim.ResetTrigger("Slash2combo2");
            anim.ResetTrigger("Slash3combo2");

            if (!string.IsNullOrEmpty(drawWeaponTrigger)) anim.ResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) anim.ResetTrigger(sheathWeaponTrigger);
            if (!string.IsNullOrEmpty(drawLeftTrigger)) anim.ResetTrigger(drawLeftTrigger);
            if (!string.IsNullOrEmpty(drawRightTrigger)) anim.ResetTrigger(drawRightTrigger);
            if (!string.IsNullOrEmpty(sheatheLeftTrigger)) anim.ResetTrigger(sheatheLeftTrigger);
            if (!string.IsNullOrEmpty(sheatheRightTrigger)) anim.ResetTrigger(sheatheRightTrigger);
        }

        // Đồng bộ hóa trạng thái tấn công trên tất cả clients
        bool isAttack = IsAttackAnimationName(translatedName);
        if (isAttack)
        {
            isExecutingAttack = true;
            attackAnimStartTime = Time.time;

            if (translatedName == "attacktaytrai") currentAttackAnimDuration = attacktaytraiDuration;
            else if (translatedName == "attacktayphai") currentAttackAnimDuration = attacktayphaiDuration;
            else if (translatedName == "Slash1Combo2") currentAttackAnimDuration = slash1Combo2Duration;
            else if (translatedName == "Slash2combo2") currentAttackAnimDuration = slash2combo2Duration;
            else if (translatedName == "Slash3combo2") currentAttackAnimDuration = slash3combo2Duration;
            else if (translatedName.Contains("Punch")) currentAttackAnimDuration = punchAnimDuration;
            else currentAttackAnimDuration = slashAnimDuration;

        }
        else
        {
            // Nếu chuyển sang di chuyển, Idle, chết, lộn, trúng đòn... thì tắt cờ tấn công
            if (isLoopingAnim || translatedName == "LonVong" || translatedName == rollTrigger ||
                translatedName == "Death" || translatedName == deathUnarmedTrigger ||
                translatedName == deathArmedTrigger || translatedName.Contains("Hit") ||
                translatedName == getHitTrigger || translatedName == getHit2Trigger)
            {
                isExecutingAttack = false;
            }
        }

        if (IsActionAnimationName(translatedName))
        {
            string stateName = translatedName;
            if (string.Equals(translatedName, "DrawLeft", System.StringComparison.OrdinalIgnoreCase)) stateName = "laykiemtaytrai";
            else if (string.Equals(translatedName, "DrawRight", System.StringComparison.OrdinalIgnoreCase)) stateName = "Laykiemtayphai";
            else if (string.Equals(translatedName, "SheatheLeft", System.StringComparison.OrdinalIgnoreCase)) stateName = "catkiemtaytrai";
            else if (string.Equals(translatedName, "SheatheRight", System.StringComparison.OrdinalIgnoreCase)) stateName = "catkiemtayphai";

            anim.SetTrigger(translatedName);
            StartCoroutine(ResetTriggerNextFrame(translatedName));

            int targetLayer = IsAttackAnimationName(translatedName) && !isRootedAttack ? 1 : 0;
            anim.CrossFadeInFixedTime(stateName, fadeTime, targetLayer, 0f);

            // Force evaluation to query the exact animation clip duration
            anim.Update(0f);
            float actualLength = 0f;
            if (anim.IsInTransition(targetLayer))
            {
                actualLength = anim.GetNextAnimatorStateInfo(targetLayer).length;
            }
            else
            {
                actualLength = anim.GetCurrentAnimatorStateInfo(targetLayer).length;
            }

            if (isAttack && actualLength > 0.05f)
            {
                currentAttackAnimDuration = actualLength;
                Debug.Log($"[LeoPlayer] PlayAnimationLocal: '{translatedName}' dynamic duration set to {currentAttackAnimDuration}s (actual clip: {actualLength}s)");
            }
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

    /// <summary>
    /// Kích hoạt hiệu ứng particle chém kiếm từ Animation Event.
    /// Hỗ trợ cả 2 chế độ:
    /// - Nhập số < 100 (ví dụ 1, 2, 3): Chạy đồng thời toàn bộ particle của nhịp combo đó.
    /// - Nhập số >= 100 (ví dụ 101, 102, 201, 206): Nhịp combo là trăm (1, 2, 3), chỉ số particle là chục/đơn vị (1-based).
    ///   Ví dụ: 101 = Nhịp chém 1, particle 1; 206 = Nhịp chém 2, particle 6.
    /// </summary>
    public void TriggerSlashParticle(int parameter)
    {
        if (parameter >= 100)
        {
            int comboStep = parameter / 100;
            int particleIndex = (parameter % 100) - 1;

            int groupIndex = comboStep - 1;
            if (groupIndex >= 0 && groupIndex < swordComboParticles.Length)
            {
                ComboParticleGroup group = swordComboParticles[groupIndex];
                if (group.particles != null && particleIndex >= 0 && particleIndex < group.particles.Length)
                {
                    var config = group.particles[particleIndex];
                    if (config.particlePrefab != null)
                    {
                        StartCoroutine(SpawnParticleCoroutine(config));
                    }
                }
            }
        }
        else
        {
            int index = parameter - 1;
            if (index >= 0 && index < swordComboParticles.Length)
            {
                PlaySwordComboParticles(index);
            }
        }
    }

    private void PlaySwordComboParticles(int index)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

        if (swordComboParticles == null || index < 0 || index >= swordComboParticles.Length) return;

        ComboParticleGroup group = swordComboParticles[index];
        if (group.particles != null)
        {
            foreach (var config in group.particles)
            {
                if (config.particlePrefab != null)
                {
                    StartCoroutine(SpawnParticleCoroutine(config));
                }
            }
        }
    }

    private System.Collections.IEnumerator SpawnParticleCoroutine(SlashParticleConfig config)
    {
        if (config.delay > 0f)
        {
            yield return new WaitForSeconds(config.delay);
        }

        if (config.particlePrefab == null) yield break;

        // Determine parent
        Transform parentTransform = config.parentToPlayer ? (config.parentOverride != null ? config.parentOverride : this.transform) : null;

        if (config.parentToPlayer)
        {
            GameObject pObj = Instantiate(config.particlePrefab, parentTransform);
            pObj.transform.localPosition = config.positionOffset;
            pObj.transform.localRotation = Quaternion.Euler(config.rotationOffset);
        }
        else
        {
            // If parentOverride is specified but we don't parent, compute relative to it, otherwise relative to this transform
            Transform referenceTransform = config.parentOverride != null ? config.parentOverride : this.transform;
            Vector3 worldPos = referenceTransform.TransformPoint(config.positionOffset);
            Quaternion worldRot = referenceTransform.rotation * Quaternion.Euler(config.rotationOffset);
            Instantiate(config.particlePrefab, worldPos, worldRot);
        }
    }

    private void OnAnimatorMove()
    {
        if (anim != null && rb != null && anim.applyRootMotion)
        {
            // Chỉ lấy khoảng cách di chuyển tịnh tiến (deltaPosition) để đẩy nhân vật về trước
            Vector3 nextPosition = rb.position + anim.deltaPosition;
            rb.MovePosition(nextPosition);

            // TUYỆT ĐỐI KHÔNG sử dụng anim.deltaRotation. Góc xoay cơ thể sẽ bị khóa cứng 
            // theo hướng Camera điều khiển từ C#, triệt tiêu hoàn toàn lỗi xoắn xẹo xương sườn.
        }
    }
    private void FixedUpdate()
    {
        ApplyExtraGravity();
    }

    /// <summary>
    /// Áp thêm lực kéo xuống để player luôn nặng và bám đất.
    /// Chỉ chạy trên Owner (Standalone hoặc NetworkOwner) — physics sẽ được
    /// đồng bộ qua NetworkTransform/NetworkRigidbody tự động.
    /// </summary>
    private void ApplyExtraGravity()
    {
        // Chỉ xử lý trên Owner hoặc Standalone
        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (!hasControl) return;

        if (rb == null) return;

        // Kiểm tra xem có đang trên không không (dựa vào vậnl tốc dương Y)
        bool isAirborne = rb.linearVelocity.y > 0.01f || rb.linearVelocity.y < -0.01f;

        if (onlyExtraGravityWhenAirborne && !isAirborne) return;

        // Áp thêm lực kéo xuống: F = m * g * (multiplier - 1)
        // Unity đã tự áp 1x gravity qua Rigidbody, ta chỉ cần thêm phần dư
        float extraGravity = rb.mass * Physics.gravity.magnitude * (extraGravityMultiplier - 1f);
        rb.AddForce(Vector3.down * extraGravity, ForceMode.Force);

        // Giới hạn tốc độ rơi tối đa (chống rơi vồ vật)
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                -maxFallSpeed,
                rb.linearVelocity.z
            );
        }
    }

    [ServerRpc]
    private void PlayAnimationServerRpc(string animName, float fadeTime, bool isRooted)
    {
        PlayAnimationClientRpc(animName, fadeTime, true, isRooted);
    }

    [ClientRpc]
    private void PlayAnimationClientRpc(string animName, float fadeTime, bool alreadyPlayedLocally, bool isRooted = false)
    {
        if (alreadyPlayedLocally && IsOwner) return;
        this.isRootedAttack = isRooted;
        PlayAnimationLocal(animName, fadeTime);
    }

    private void ClearAttackLayer()
    {
        try
        {
            if (anim != null && anim.isActiveAndEnabled && anim.GetBool("IsPushing"))
            {
                return;
            }
        }
        catch (System.Exception) {}

        comboStep = 0;
        isRootedAttack = false;
        SetMovementLock(false);
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            anim.CrossFadeInFixedTime("New State", 0.1f, 1, 0f);
        }

        // Network sync layer clearing
        if (!isStandaloneMode && IsSpawned && IsOwner)
        {
            ClearAttackLayerServerRpc();
        }
    }

    [ServerRpc]
    private void ClearAttackLayerServerRpc()
    {
        ClearAttackLayerClientRpc();
    }

    [ClientRpc]
    private void ClearAttackLayerClientRpc()
    {
        if (!IsOwner)
        {
            comboStep = 0;
            isExecutingAttack = false;
            isRootedAttack = false;
            if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
            {
                anim.CrossFadeInFixedTime("New State", 0.1f, 1, 0f);
            }
        }
    }

    /// <summary>
    /// Ng\u1eaft combo ngay l\u1eadp t\u1ee9c: d\u1eebng coroutine, t\u1eaft hitbox, reset tr\u1ea1ng th\u00e1i t\u1ea5n c\u00f4ng.
    /// G\u1ecdi khi b\u1ecb \u0111\u00e1nh, ch\u1ebft, ho\u1eb7c l\u1ed9n vòng.
    /// </summary>
    private void InterruptCombo()
    {
        if (comboChainCoroutine != null)
        {
            StopCoroutine(comboChainCoroutine);
            comboChainCoroutine = null;
        }
        pendingAttackRequest = false;
        isExecutingAttack = false;
        comboStep = 0;
        DisableAllHitboxes();
        alreadyHitEnemies.Clear();
        Debug.Log("[LeoPlayer] Combo b\u1ecb ng\u1eaft (b\u1ecb hit/ch\u1ebft/l\u1ed9n v\u00f2ng).");
    }

    // ======================================================
    // UTILITY METHODS
    // ======================================================

    public void UnlockMovement()
    {
        SetMovementLock(false);
    }

    public void OnPickItemEvent()
    {
        if (pendingPickItem != null)
        {
            var collectible = pendingPickItem.GetComponent<CollectibleItemDrop>();
            if (collectible != null) collectible.ConfirmCollect();
            else
            {
                var axe = pendingPickItem.GetComponent<AxeItem>();
                if (axe != null) axe.ConfirmPickup(gameObject);
                else
                {
                    var repair = pendingPickItem.GetComponent<RepairItemDrop>();
                    if (repair != null) repair.ConfirmCollect();
                    else
                    {
                        var crystal = pendingPickItem.GetComponent<CrystalCore>();
                        if (crystal != null)
                        {
                            var interaction = GetComponent<PlayerInteraction>();
                            if (interaction != null) interaction.ConfirmPickupCrystal(crystal);
                        }
                    }
                }
            }
            pendingPickItem = null;
        }
    }

    public void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        LockCursor(locked);
    }

    public bool IsUIBlockingInput()
    {
        bool uiOpen = false;
        if (PlayerHUDController.isAnyUIOpen) uiOpen = true;

        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive) ||
                              (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive) ||
                              SeagullController.ActiveSeagull != null;
        if (isDialogueOpen) uiOpen = true;

        if (uiOpen)
        {
            lastTimeUIOpen = Time.time;
            return true;
        }
        if (Time.time - lastTimeUIOpen < 0.15f) return true;

        return false;
    }

    private float lastTimeUIOpen = 0f;

    public void SetMovementLock(bool locked)
    {
        isMovementLocked = locked;
        if (locked)
        {
            attackDashTimer = 0f;
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        if (!isStandaloneMode && IsOwner)
        {
            SetMovementLockServerRpc(locked);
        }
    }

    [ServerRpc]
    private void SetMovementLockServerRpc(bool locked)
    {
        if (IsServer) isMovementLockedNet.Value = locked;
    }

    private void OnMovementLockedNetChanged(bool oldVal, bool newVal)
    {
        if (!IsOwner)
        {
            isMovementLocked = newVal;
            if (newVal && rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    // ======================================================
    // HITBOX SYSTEM - Animation Event Receivers
    // ======================================================
    // Quy tắc:
    //   - Chỉ Owner hoặc Standalone mới được bật hitbox & tính damage.
    //   - Khi BẬT hitbox → clear alreadyHitEnemies (đòn mới, tính damage lại từ đầu).
    //   - Khi TẮT hitbox → clear alreadyHitEnemies (chuẩn bị cho đòn tiếp theo).
    //   - OnHitboxCollision() đảm bảo mỗi enemy chỉ bị damage 1 lần/đòn.
    //
    // Cách dùng trong Animation Event:
    //   Đấm (Punch):
    //     Frame bắt đầu vung tay trái  → EnableLeftHitbox()
    //     Frame kết thúc vung tay trái → DisableLeftHitbox()
    //     Frame bắt đầu vung tay phải → EnableRightHitbox()
    //     Frame kết thúc vung tay phải → DisableRightHitbox()
    //
    //   Chém (Slash):
    //     Frame bắt đầu vung kiếm  → EnableBothWeaponHitboxes()
    //     Frame kết thúc vung kiếm → DisableBothWeaponHitboxes()
    //
    //   Cuối mỗi animation: OnAttackEnd() hoặc OnPunchEnd() / OnSlashEnd()

    private bool CanActivateHitbox()
    {
        return isStandaloneMode || (IsSpawned && IsOwner);
    }

    // --- Đấm tay: Tay Trái ---
    public void EnableLeftHitbox() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableLeftHitbox() {}

    // --- Đấm tay: Tay Phải ---
    public void EnableRightHitbox() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableRightHitbox() {}

    // --- Đấm tay: Cả hai tay ---
    public void EnableBothHitboxes() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableBothHitboxes() {}

    // --- Kiếm: Tay Trái ---
    public void EnableLeftWeaponHitbox() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableLeftWeaponHitbox() {}

    // --- Kiếm: Tay Phải ---
    public void EnableRightWeaponHitbox() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableRightWeaponHitbox() {}

    // --- Kiếm: Cả hai tay (Slash chính) ---
    public void EnableBothWeaponHitboxes() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableBothWeaponHitboxes() {}

    // --- VFX Spawn Animation Events với Object Pooling ---
    private System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.List<GameObject>> vfxPools =
        new System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.List<GameObject>>();

    private GameObject GetPooledVFX(GameObject prefab, Transform parent)
    {
        return GetPooledVFX(prefab, parent, new Vector3(0.5f, 0.5f, 0.5f));
    }

    private GameObject GetPooledVFX(GameObject prefab, Transform parent, Vector3 scale)
    {
        if (prefab == null) return null;

        if (!vfxPools.ContainsKey(prefab))
        {
            vfxPools[prefab] = new System.Collections.Generic.List<GameObject>();
        }

        System.Collections.Generic.List<GameObject> pool = vfxPools[prefab];

        GameObject obj = null;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] == null)
            {
                pool.RemoveAt(i);
                i--;
                continue;
            }

            if (!pool[i].activeSelf)
            {
                obj = pool[i];
                break;
            }
        }

        if (obj == null)
        {
            obj = Instantiate(prefab);
            pool.Add(obj);
        }

        if (parent != null)
        {
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            obj.transform.localScale = scale;
        }
        else
        {
            obj.transform.SetParent(null);
            obj.transform.localScale = scale;
        }

        obj.SetActive(true);
        return obj;
    }

    private GameObject GetPooledVFX(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return GetPooledVFX(prefab, position, rotation, new Vector3(0.5f, 0.5f, 0.5f));
    }

    private GameObject GetPooledVFX(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (prefab == null) return null;

        if (!vfxPools.ContainsKey(prefab))
        {
            vfxPools[prefab] = new System.Collections.Generic.List<GameObject>();
        }

        System.Collections.Generic.List<GameObject> pool = vfxPools[prefab];

        GameObject obj = null;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] == null)
            {
                pool.RemoveAt(i);
                i--;
                continue;
            }

            if (!pool[i].activeSelf)
            {
                obj = pool[i];
                break;
            }
        }

        if (obj == null)
        {
            obj = Instantiate(prefab, position, rotation);
            pool.Add(obj);
        }
        else
        {
            obj.transform.SetParent(null);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
        }

        obj.transform.localScale = scale;
        obj.SetActive(true);
        return obj;
    }

    private System.Collections.IEnumerator DeactivateAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
        {
            obj.SetActive(false);
        }
    }

    public void PlayLeftSlashVFX()
    {
        if (IsLeftoverEvent()) return;
        if (leftSlashVfxPrefab != null && leftSlashSpawnPoint != null)
        {
            GameObject vfx = GetPooledVFX(leftSlashVfxPrefab, leftSlashSpawnPoint);
            if (vfx != null)
            {
                ParticleSystem[] ps = vfx.GetComponentsInChildren<ParticleSystem>();
                for (int i = 0; i < ps.Length; i++)
                {
                    ps[i].Clear();
                    ps[i].Play();
                }
                StartCoroutine(DeactivateAfterDelay(vfx, 2f));
            }
        }
        else
        {
            Debug.LogWarning("[LeoPlayer VFX] Không thể spawn Left Slash VFX vì thiếu Prefab hoặc SpawnPoint.");
        }
    }

    public void PlayRightSlashVFX()
    {
        if (IsLeftoverEvent()) return;
        if (rightSlashVfxPrefab != null && rightSlashSpawnPoint != null)
        {
            GameObject vfx = GetPooledVFX(rightSlashVfxPrefab, rightSlashSpawnPoint);
            if (vfx != null)
            {
                ParticleSystem[] ps = vfx.GetComponentsInChildren<ParticleSystem>();
                for (int i = 0; i < ps.Length; i++)
                {
                    ps[i].Clear();
                    ps[i].Play();
                }
                StartCoroutine(DeactivateAfterDelay(vfx, 2f));
            }
        }
        else
        {
            Debug.LogWarning("[LeoPlayer VFX] Không thể spawn Right Slash VFX vì thiếu Prefab hoặc SpawnPoint.");
        }
    }

    public void PlayDualSlashVFX()
    {
        if (IsLeftoverEvent()) return;
        if (dualSlashVfxPrefab != null)
        {
            if (leftSlashSpawnPoint != null)
            {
                GameObject vfxL = GetPooledVFX(dualSlashVfxPrefab, leftSlashSpawnPoint);
                if (vfxL != null)
                {
                    ParticleSystem[] ps = vfxL.GetComponentsInChildren<ParticleSystem>();
                    for (int i = 0; i < ps.Length; i++)
                    {
                        ps[i].Clear();
                        ps[i].Play();
                    }
                    StartCoroutine(DeactivateAfterDelay(vfxL, 2f));
                }
            }
            if (rightSlashSpawnPoint != null)
            {
                GameObject vfxR = GetPooledVFX(dualSlashVfxPrefab, rightSlashSpawnPoint);
                if (vfxR != null)
                {
                    ParticleSystem[] ps = vfxR.GetComponentsInChildren<ParticleSystem>();
                    for (int i = 0; i < ps.Length; i++)
                    {
                        ps[i].Clear();
                        ps[i].Play();
                    }
                    StartCoroutine(DeactivateAfterDelay(vfxR, 2f));
                }
            }
        }
        else
        {
            // Dự phòng: Phát cả VFX trái và phải cùng lúc
            PlayLeftSlashVFX();
            PlayRightSlashVFX();
        }
    }

    public void PlayDualSlash1VFX()
    {
        if (IsLeftoverEvent()) return;
        if (dualSlash1VfxPrefab != null)
        {
            if (leftSlashSpawnPoint != null)
            {
                GameObject vfx = GetPooledVFX(dualSlash1VfxPrefab, leftSlashSpawnPoint);
                if (vfx != null)
                {
                    ParticleSystem[] ps = vfx.GetComponentsInChildren<ParticleSystem>();
                    for (int i = 0; i < ps.Length; i++)
                    {
                        ps[i].Clear();
                        ps[i].Play();
                    }
                    StartCoroutine(DeactivateAfterDelay(vfx, 2f));
                }
            }
            else
            {
                Debug.LogWarning("[LeoPlayer VFX] Không thể spawn Dual Slash 1 VFX vì thiếu SpawnPoint.");
            }
        }
        else
        {
            // Dự phòng: Chém bằng tay trái
            PlayLeftSlashVFX();
        }
    }

    public void PlayDualSlash2VFX()
    {
        if (IsLeftoverEvent()) return;
        if (dualSlash2VfxPrefab != null)
        {
            if (rightSlashSpawnPoint != null)
            {
                GameObject vfx = GetPooledVFX(dualSlash2VfxPrefab, rightSlashSpawnPoint);
                if (vfx != null)
                {
                    ParticleSystem[] ps = vfx.GetComponentsInChildren<ParticleSystem>();
                    for (int i = 0; i < ps.Length; i++)
                    {
                        ps[i].Clear();
                        ps[i].Play();
                    }
                    StartCoroutine(DeactivateAfterDelay(vfx, 2f));
                }
            }
            else
            {
                Debug.LogWarning("[LeoPlayer VFX] Không thể spawn Dual Slash 2 VFX vì thiếu SpawnPoint.");
            }
        }
        else
        {
            // Dự phòng: Chém bằng tay phải
            PlayRightSlashVFX();
        }
    }

    // --- Animation Event: Kết thúc đòn đánh ---
    /// <summary>
    /// Gọi từ Animation Event ở FRAME CUỐI của mỗi animation đấm/chém.
    /// Đảm bảo tắt hitbox và đánh dấu kết thúc nhịp tấn công.
    /// </summary>
    public void OnAttackEnd() {}
    public void OnSlashEnd() {}
    public void OnPunchEnd() {}

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
                Debug.Log($"[LeoPlayer] 💥 HIT: {enemyRoot.name} | Damage: {damageAmount}");

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
        if (col.transform.root == transform.root) return false;
        if (col.CompareTag("Player") || col.gameObject.layer == LayerMask.NameToLayer("Player")) return false;

        bool isEnemyHit = col.CompareTag("Enemy") ||
                          col.gameObject.layer == LayerMask.NameToLayer("Enemy") ||
                          col.name.ToLower().Contains("enemy") ||
                          col.name.ToLower().Contains("boss") ||
                          col.GetComponentInParent<Enemy1_DapBua>() != null || col.transform.root.GetComponentInChildren<Enemy1_DapBua>() != null ||
                          col.GetComponentInParent<Enemy2_Zombie>() != null || col.transform.root.GetComponentInChildren<Enemy2_Zombie>() != null ||
                          col.GetComponentInParent<Enemy3_Buaa>() != null || col.transform.root.GetComponentInChildren<Enemy3_Buaa>() != null ||
                          col.GetComponentInParent<Enemy4_Bongtoi>() != null || col.transform.root.GetComponentInChildren<Enemy4_Bongtoi>() != null ||
                          col.GetComponentInParent<Enemy5_PhuThuy>() != null || col.transform.root.GetComponentInChildren<Enemy5_PhuThuy>() != null ||
                          col.GetComponentInParent<MiniBossAI>() != null || col.transform.root.GetComponentInChildren<MiniBossAI>() != null ||
                          col.GetComponentInParent<FinalBossAI>() != null || col.transform.root.GetComponentInChildren<FinalBossAI>() != null ||
                          col.GetComponentInParent<BossAI>() != null || col.transform.root.GetComponentInChildren<BossAI>() != null;

        if (isEnemyHit)
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
            var col = netObj.GetComponent<Collider>() ?? netObj.GetComponentInChildren<Collider>();
            if (col != null) TryDamageEnemy(col);
            else
            {
                float actualDamage = damageAmount;
                var e1 = netObj.GetComponent<Enemy1_DapBua>() ?? netObj.GetComponentInChildren<Enemy1_DapBua>();
                if (e1 != null) { e1.TakeDamage(actualDamage); return; }
                var e2 = netObj.GetComponent<Enemy2_Zombie>() ?? netObj.GetComponentInChildren<Enemy2_Zombie>();
                if (e2 != null) { e2.TakeDamage(actualDamage); return; }
                var e3 = netObj.GetComponent<Enemy3_Buaa>() ?? netObj.GetComponentInChildren<Enemy3_Buaa>();
                if (e3 != null) { e3.TakeDamage(actualDamage); return; }
                var e4 = netObj.GetComponent<Enemy4_Bongtoi>() ?? netObj.GetComponentInChildren<Enemy4_Bongtoi>();
                if (e4 != null) { e4.TakeDamage(actualDamage); return; }
                var e5 = netObj.GetComponent<Enemy5_PhuThuy>() ?? netObj.GetComponentInChildren<Enemy5_PhuThuy>();
                if (e5 != null) { e5.TakeDamage(actualDamage); return; }
                var mb = netObj.GetComponent<MiniBossAI>() ?? netObj.GetComponentInChildren<MiniBossAI>();
                if (mb != null) { mb.TakeDamage(actualDamage); return; }
                var fb = netObj.GetComponent<FinalBossAI>() ?? netObj.GetComponentInChildren<FinalBossAI>();
                if (fb != null) { fb.TakeDamage(actualDamage); return; }
                var b = netObj.GetComponent<BossAI>() ?? netObj.GetComponentInChildren<BossAI>();
                if (b != null) { b.TakeDamage(actualDamage); return; }
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

        if (leftHitbox == null)
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
            BoxCollider col = leftObj.GetComponent<BoxCollider>() ?? leftObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.6f, 0.6f, 0.6f);
            col.center = new Vector3(0f, 0f, 0.2f);
            col.enabled = false;
            if (leftObj.GetComponent<PlayerHitbox>() == null) leftObj.AddComponent<PlayerHitbox>();
            leftHitbox = col;
        }

        if (rightHitbox == null)
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
            BoxCollider col = rightObj.GetComponent<BoxCollider>() ?? rightObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.6f, 0.6f, 0.6f);
            col.center = new Vector3(0f, 0f, 0.2f);
            col.enabled = false;
            if (rightObj.GetComponent<PlayerHitbox>() == null) rightObj.AddComponent<PlayerHitbox>();
            rightHitbox = col;
        }

        Transform leftSwordParent = FindWeaponTransform(leftHand) ?? leftHand;
        Transform rightSwordParent = FindWeaponTransform(rightHand) ?? rightHand;

        if (leftWeaponHitbox == null)
        {
            Transform existingLeftW = leftSwordParent.Find("LeftWeaponHitbox");
            GameObject leftWObj = existingLeftW != null ? existingLeftW.gameObject : new GameObject("LeftWeaponHitbox");
            if (existingLeftW == null)
            {
                leftWObj.transform.SetParent(leftSwordParent);
                leftWObj.transform.localPosition = Vector3.zero;
                leftWObj.transform.localRotation = Quaternion.identity;
                leftWObj.transform.localScale = Vector3.one;
            }
            BoxCollider col = leftWObj.GetComponent<BoxCollider>() ?? leftWObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.2f, 0.2f, 1.2f);
            col.center = new Vector3(0f, 0f, 0.6f);
            col.enabled = false;
            if (leftWObj.GetComponent<PlayerHitbox>() == null) leftWObj.AddComponent<PlayerHitbox>();
            leftWeaponHitbox = col;
        }

        if (rightWeaponHitbox == null)
        {
            Transform existingRightW = rightSwordParent.Find("RightWeaponHitbox");
            GameObject rightWObj = existingRightW != null ? existingRightW.gameObject : new GameObject("RightWeaponHitbox");
            if (existingRightW == null)
            {
                rightWObj.transform.SetParent(rightSwordParent);
                rightWObj.transform.localPosition = Vector3.zero;
                rightWObj.transform.localRotation = Quaternion.identity;
                rightWObj.transform.localScale = Vector3.one;
            }
            BoxCollider col = rightWObj.GetComponent<BoxCollider>() ?? rightWObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.2f, 0.2f, 1.2f);
            col.center = new Vector3(0f, 0f, 0.6f);
            col.enabled = false;
            if (rightWObj.GetComponent<PlayerHitbox>() == null) rightWObj.AddComponent<PlayerHitbox>();
            rightWeaponHitbox = col;
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

    private void UpdateAttackLayerWeight()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying)
        {
            return;
        }

        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            try
            {
                if (anim.GetBool("IsPushing"))
                {
                    anim.SetLayerWeight(1, 1f);
                    return;
                }
            }
            catch (System.Exception) {}

            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(1);
            
            bool isDrawOrSheatheActive = stateInfo.IsName("laykiemtaytrai") || stateInfo.IsName("Laykiemtayphai") ||
                                         stateInfo.IsName("catkiemtaytrai") || stateInfo.IsName("catkiemtayphai");
            
            bool shouldBeActive = IsAiming || isRShootPending || (isExecutingAttack && !isRootedAttack) || isSwitchingWeapon || isDrawOrSheatheActive;

            targetAttackLayerWeight = shouldBeActive ? 1f : 0f;

            currentAttackLayerWeight = Mathf.MoveTowards(currentAttackLayerWeight, targetAttackLayerWeight, Time.deltaTime * 10f);
            anim.SetLayerWeight(1, currentAttackLayerWeight);
        }
    }

    public void RequestDropWoodLog()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        string carriedPrefab = carrier != null ? carrier.carriedLogPrefabName : "";
        int amount = carrier != null ? carrier.carriedLogCount : 1;

        float groundY = transform.position.y;
        Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 1.2f;
        Vector3 rayStart = new Vector3(spawnPos.x, transform.position.y + 3f, spawnPos.z);
        int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 6f, layerMask))
        {
            if (hit.collider.gameObject != gameObject && !hit.collider.name.ToLower().Contains("player"))
            {
                groundY = hit.point.y;
            }
        }
        spawnPos.y = groundY + 0.6f;

        if (isStandaloneMode)
        {
            GameObject logPrefab = null;
            if (!string.IsNullOrEmpty(carriedPrefab)) logPrefab = Resources.Load<GameObject>(carriedPrefab);
            if (logPrefab == null && WoodLogObjectPool.Instance != null && WoodLogObjectPool.Instance.WoodPrefab != null)
            {
                logPrefab = WoodLogObjectPool.Instance.WoodPrefab;
            }
            if (logPrefab == null) logPrefab = Resources.Load<GameObject>("wood_stack");
            if (logPrefab == null) logPrefab = Resources.Load<GameObject>("firewood_single");
            if (logPrefab == null) logPrefab = Resources.Load<GameObject>("WoodLog");

            if (logPrefab != null)
            {
                GameObject wood = WoodLogObjectPool.Instance.GetOrCreate(logPrefab, spawnPos, Quaternion.identity);
                var cid = wood.GetComponent<CollectibleItemDrop>();
                if (cid != null)
                {
                    cid.localWoodAmount = amount;
                }
            }
            if (carrier != null) carrier.DropLog();
        }
        else if (IsOwner)
        {
            if (carrier != null) carrier.DropLog();
            DropWoodLogServerRpc(spawnPos, amount, carriedPrefab);
        }
    }

    [ServerRpc]
    private void DropWoodLogServerRpc(Vector3 position, int amount, string prefabName)
    {
        if (!IsServer) return;

        GameObject logPrefab = null;
        if (!string.IsNullOrEmpty(prefabName)) logPrefab = Resources.Load<GameObject>(prefabName);
        if (logPrefab == null && WoodLogObjectPool.Instance != null && WoodLogObjectPool.Instance.WoodPrefab != null)
        {
            logPrefab = WoodLogObjectPool.Instance.WoodPrefab;
        }
        if (logPrefab == null) logPrefab = Resources.Load<GameObject>("wood_stack");
        if (logPrefab == null) logPrefab = Resources.Load<GameObject>("firewood_single");
        if (logPrefab == null) logPrefab = Resources.Load<GameObject>("WoodLog");

        if (logPrefab != null)
        {
            GameObject wood = WoodLogObjectPool.Instance.GetOrCreate(logPrefab, position, Quaternion.identity);
            var cid = wood.GetComponent<CollectibleItemDrop>();
            if (cid != null)
            {
                cid.woodAmount.Value = amount;
            }
            var netObj = wood.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
        }

        DropWoodLogClientRpc();
    }

    [ClientRpc]
    private void DropWoodLogClientRpc()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null)
        {
            carrier.DropLog();
        }
    }

    public override void OnDestroy()
    {
        if (originalBodyMaterials != null)
        {
            foreach (var kvp in originalBodyMaterials)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.materials = kvp.Value;
                }
            }
            originalBodyMaterials.Clear();
        }

        if (originalSwordMaterials != null)
        {
            foreach (var kvp in originalSwordMaterials)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.materials = kvp.Value;
                }
            }
            originalSwordMaterials.Clear();
        }

        HideAoeIndicator();
        if (PlayerHUDManager.ActivePlayers != null)
        {
            PlayerHUDManager.ActivePlayers.Remove(this);
        }
        base.OnDestroy();
    }
}