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
    public float comboWindow = 2.5f;
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

    [Header("Skill R - Nhuộm Đỏ Vũ Khí")]
    [Tooltip("Tên Trigger Animation trong Animator khi khai động Skill R. Để trống nếu không có animation.")]
    public string rSkillAnimTrigger = "SkillR";

    [Header("Skill R Fine Tuning")]
    [Tooltip("Y Offset chỉnh xoay cột sống ngang cho chiêu R")]
    public float rSkillYOffset = 0f;
    [Tooltip("X Offset chỉnh xoay cột sống dọc cho chiêu R")]
    public float rSkillXOffset = 0f;
    [Tooltip("Đảo ngược chiều cúi đầu/ngẩng đầu của xương cột sống")]
    public bool invertSpinePitch = false;

    [Header("Skill R - Bắn Cục Lửa")]
    [Tooltip("Prefab đạn lửa của chiêu R")]
    public GameObject rSkillFirePrefab;
    [Tooltip("Điểm xuất phát bắn đạn lửa của chiêu R")]
    public Transform rSkillFireSpawnPoint;
    [Tooltip("Tốc độ bay của đạn lửa")]
    public float rSkillFireSpeed = 20f;
    [Tooltip("Sát thương của đạn lửa")]
    public float rSkillFireDamage = 40f;

    private bool localIsAimingR = false;
    private GameObject rSkillHandPreviewVisual;
    private bool isRShootPending = false;
    private bool isPendingRShootNetworkMode = false;

    [Header("Skill E - Bất Tử")]
    [Tooltip("Tên Trigger Animation trong Animator khi kích hoạt Skill E.")]
    public string eSkillAnimTrigger = "SkillE";
    [Tooltip("Thời gian hiệu lực Skill E bất tử (giây)")]
    public float eSkillDuration = 5f;
    [HideInInspector]
    public bool isESkillActive = false;
    [HideInInspector]
    public bool isESkillPlayingAnim = false;
    protected float eSkillTimeRemaining = 0f;

    [Header("Skill Q - Dặm Khiên / Vòng Choáng")]
    [Tooltip("Tên Trigger Animation trong Animator khi kích hoạt Skill Q (dặm khiên xuống đất).")]
    public string qSkillAnimTrigger = "SkillQ";
    [Tooltip("Thời gian quái bị choáng (giây)")]
    public float qSkillStunDuration = 5f;
    [Tooltip("Bán kính vòng tròn choáng (đơn vị Unity)")]
    public float qSkillRadius = 6f;
    [Tooltip("Thời gian hồi chiêu Q trên server (giây) - dùng để reset trạng thái sau khi stun xong")]
    public float qSkillDuration = 1.5f;
    [HideInInspector]
    public bool isQSkillActive = false;
    [HideInInspector]
    public bool isQSkillPlayingAnim = false;
    protected float qSkillTimeRemaining = 0f;

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
    public NetworkVariable<bool> isBlockingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    protected bool isBlocking = false;



    public NetworkVariable<bool> isESkillActiveNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> isQSkillActiveNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> isAimingNet = new NetworkVariable<bool>(
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
    public int characterClassIndex = 3;

    [Header("Player Name Sync")]
    public NetworkVariable<Unity.Collections.FixedString64Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Arthur", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

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

    [Header("Hitbox References")]
    public Collider leftHitbox;
    public Collider rightHitbox;
    public Collider leftWeaponHitbox;
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
            if (rootMotionBridge == null)
            {
                rootMotionBridge = anim.gameObject.AddComponent<RootMotionBridge>();
                Debug.Log($"[ArthurPlayer] Dynamically added RootMotionBridge to {anim.gameObject.name} at runtime.");
            }
        }
        return rootMotionBridge;
    }

    protected float localHealth;
    public bool isStandaloneMode = false;
    protected bool isSyncingFromDb = false;

    private float smoothedInputX = 0f;
    private float smoothedInputZ = 0f;
    private Rigidbody rb;

    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public float CurrentHealth => isStandaloneMode ? localHealth : currentHealth.Value;

    bool IPlayerHUDTarget.isStandaloneMode => isStandaloneMode;
    public bool IsStandaloneMode => isStandaloneMode;
    public int CharacterClassIndex => characterClassIndex;
    public bool IsSwitchingWeapon => false;
    public float Weapon1MaxDurability => weapon1MaxDurability;
    public float Weapon2MaxDurability => weapon2MaxDurability;
    public string[] InventorySlots => inventorySlots;
    public float MaxHealth => maxHealth;

    public bool IsInvisible => false;
    public float InvisibilityTimeRemaining => 0f;

    private bool IsSkillsUnlocked => isSkillsUnlocked.Value;

    // Chỉ kích hoạt hoạt ảnh gồng chiêu R ban đầu
    public void TriggerInvisibilitySkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying)
        {
            Debug.LogWarning("[ArthurPlayer] Cannot trigger R Skill while carrying a log!");
            return;
        }

        if (PlayerLevel < 5 && !IsSkillsUnlocked) return;

        int activeWeaponIdx = GetActiveWeaponIndex();
        if (activeWeaponIdx != 0)
        {
            Debug.LogWarning($"[ArthurPlayer] Cannot trigger R Skill because active weapon is {activeWeaponIdx} (must be unarmed!). Please switch to unarmed first.");
            return;
        }
        
        Debug.Log("[ArthurPlayer] Triggering R Skill: Setting localIsAimingR to true.");
        SetAimingR(true);
    }

    private void SetAimingR(bool aiming)
    {
        Debug.Log($"[ArthurPlayer] SetAimingR({aiming}) called. Current: {localIsAimingR}");
        if (localIsAimingR == aiming) return;
        localIsAimingR = aiming;
        
        OnAimStateChanged(localIsAimingR);
        if (!isStandaloneMode && IsOwner)
        {
            SetAimingServerRpc(localIsAimingR);
        }
    }

    private void HandleRAiming()
    {
        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (!hasControl) return;

        if (localIsAimingR)
        {
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
                Debug.Log("[ArthurPlayer] R key released, setting localIsAimingR to false.");
                SetAimingR(false);
            }
            else if (Input.GetMouseButtonDown(0))
            {
                if (!IsUIBlockingInput())
                {
                    isRShootPending = true;
                    isPendingRShootNetworkMode = !isStandaloneMode;
                    
                    if (!string.IsNullOrEmpty(rSkillAnimTrigger))
                    {
                        PlayAnimation(rSkillAnimTrigger, 0.05f);
                    }
                    
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
        bool isPlayingShoot = anim != null && anim.layerCount > 1 && (anim.GetCurrentAnimatorStateInfo(1).IsName(rSkillAnimTrigger) || anim.GetCurrentAnimatorStateInfo(1).IsName("NemRM"));
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
                    if (!string.IsNullOrEmpty(rSkillAnimTrigger))
                    {
                        anim.ResetTrigger(rSkillAnimTrigger);
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

        if (aiming)
        {
            if (rSkillFirePrefab != null && rSkillFireSpawnPoint != null && rSkillHandPreviewVisual == null)
            {
                rSkillHandPreviewVisual = Instantiate(rSkillFirePrefab, rSkillFireSpawnPoint.position, rSkillFireSpawnPoint.rotation, rSkillFireSpawnPoint);
                rSkillHandPreviewVisual.transform.localPosition = Vector3.zero;
                rSkillHandPreviewVisual.transform.localRotation = Quaternion.identity;
                rSkillHandPreviewVisual.transform.localScale = rSkillFirePrefab.transform.localScale;
                
                if (rSkillHandPreviewVisual.TryGetComponent<ArthurFireProjectile>(out var proj))
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
                
                foreach (Transform child in rSkillHandPreviewVisual.transform)
                {
                    if (child.name.ToLower().Contains("hit"))
                    {
                        child.gameObject.SetActive(false);
                    }
                    else
                    {
                        child.gameObject.SetActive(true);
                    }
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
    }

    [ServerRpc]
    private void SetAimingServerRpc(bool aiming)
    {
        isAimingNet.Value = aiming;
    }

    public void OnShootRSkill()
    {
        OnRSkillWeaponGlow();
    }

    // EVENT ĐÓN NHẬN TỪ ANIMATION EVENT KHUNG HÌNH CUỐI (Giải quyết lỗi báo đỏ CS1061)
    public void OnRSkillWeaponGlow()
    {
        if (!isRShootPending) return;
        isRShootPending = false;

        OnAimStateChanged(localIsAimingR);

        // Chỉ Owner hoặc Standalone mới bắn đạn
        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);
        if (!hasControl) return;

        Debug.Log($"[{gameObject.name}] OnRSkillWeaponGlow: Hoạt ảnh kết thúc -> Bắn cục lửa!");

        Vector3 spawnPos = rSkillFireSpawnPoint != null ? rSkillFireSpawnPoint.position : transform.position + transform.forward * 1.5f + Vector3.up * 1f;
        Vector3 shootDirection = targetCamera != null ? targetCamera.transform.forward : transform.forward;

        if (isPendingRShootNetworkMode)
        {
            SpawnFireProjectileServerRpc(spawnPos, shootDirection);
        }
        else
        {
            SpawnFireProjectileLocal(spawnPos, shootDirection);
        }
    }

    private void SpawnFireProjectileLocal(Vector3 spawnPos, Vector3 shootDirection)
    {
        if (rSkillFirePrefab == null)
        {
            Debug.LogError("[ArthurPlayer] rSkillFirePrefab chưa được gán trong Inspector!");
            return;
        }

        GameObject fireObj = Instantiate(rSkillFirePrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        fireObj.transform.localScale = rSkillFirePrefab.transform.localScale;
        fireObj.SetActive(true);

        if (fireObj.TryGetComponent<ArthurFireProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = rSkillFireDamage;
            proj.speed = rSkillFireSpeed;
        }
    }

    [ServerRpc]
    private void SpawnFireProjectileServerRpc(Vector3 spawnPos, Vector3 shootDirection)
    {
        if (rSkillFirePrefab == null)
        {
            Debug.LogError("[ArthurPlayer] rSkillFirePrefab chưa được gán trên Server!");
            return;
        }

        // Kiểm tra hợp lệ khoảng cách trên Server để tránh lag giật tọa độ
        if (Vector3.Distance(spawnPos, transform.position) > 4f)
        {
            spawnPos = rSkillFireSpawnPoint != null ? rSkillFireSpawnPoint.position : transform.position + transform.forward * 1.5f + Vector3.up * 1f;
        }

        GameObject fireObj = Instantiate(rSkillFirePrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        fireObj.transform.localScale = rSkillFirePrefab.transform.localScale;
        fireObj.SetActive(true);

        if (fireObj.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn(true);
        }

        if (fireObj.TryGetComponent<ArthurFireProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = rSkillFireDamage;
            proj.speed = rSkillFireSpeed;
        }
    }

    public bool IsAttackSpeedBoosted => isESkillActive;
    public float AttackSpeedBoostTimeRemaining => eSkillTimeRemaining;

    // ── Skill Q: Dặm Khiên (override stub từ IPlayerHUDTarget) ──
    new public bool IsQSkillActive => isQSkillActive;
    new public float QSkillTimeRemaining => qSkillTimeRemaining;

    /// <summary>Kích hoạt Skill Q - phát hoạt ảnh dặm khiên. Trả về true nếu đã khởi động thành công.</summary>
    new public bool TriggerQSkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return false;

        if (PlayerLevel < 15 && !IsSkillsUnlocked) return false;
        if (isQSkillActive || isQSkillPlayingAnim) return false;

        Debug.Log($"[{gameObject.name}] TriggerQSkill (Skill Q) - Bắt đầu dặm khiên...");

        if (isStandaloneMode)
        {
            if (!string.IsNullOrEmpty(qSkillAnimTrigger))
                PlayAnimation(qSkillAnimTrigger, 0.1f);
            isQSkillPlayingAnim = true;
        }
        else if (IsOwner)
        {
            if (!string.IsNullOrEmpty(qSkillAnimTrigger))
                PlayAnimationLocal(qSkillAnimTrigger, 0.1f);
            isQSkillPlayingAnim = true;
            TriggerQSkillServerRpc();
        }
        return true;
    }

    // EVENT ĐÓN NHẬN TỪ ANIMATION EVENT KHUNG HÌNH DẶM KHIÊN (cuối animation SkillQ)
    public void OnSkillQShieldSlam()
    {
        Debug.Log($"[{gameObject.name}] OnSkillQShieldSlam: Hoạt ảnh dặm khiên kết thúc -> Kích hoạt vòng choáng!");
        isQSkillPlayingAnim = false;

        // Chỉ chạy logic stun trên Owner hoặc Standalone
        if (isStandaloneMode || IsOwner)
        {
            isQSkillActive = true;
            qSkillTimeRemaining = qSkillDuration;

            // Stun tất cả enemy trong bán kính
            ApplyQSkillStunToNearbyEnemies();

            if (!isStandaloneMode)
            {
                StartQSkillBuffServerRpc();
            }
        }
    }

    /// <summary>Dùng OverlapSphere để tìm và choáng tất cả enemy trong bán kính qSkillRadius.</summary>
    private void ApplyQSkillStunToNearbyEnemies()
    {
        bool auth = isStandaloneMode || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer);
        if (!auth) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, qSkillRadius);
        Debug.Log($"[{gameObject.name}] Skill Q: Quét bán kính {qSkillRadius}m -> {hits.Length} collider(s)");

        foreach (var col in hits)
        {
            if (col == null) continue;

            var e1 = col.GetComponentInParent<Enemy1_DapBua>();
            if (e1 != null && !e1.IsDead) { e1.ApplyStun(qSkillStunDuration); continue; }

            var e2 = col.GetComponentInParent<Enemy2_Zombie>();
            if (e2 != null && !e2.IsDead) { e2.ApplyStun(qSkillStunDuration); continue; }

            var e3 = col.GetComponentInParent<Enemy3_Buaa>();
            if (e3 != null && !e3.IsDead) { e3.ApplyStun(qSkillStunDuration); continue; }

            var e4 = col.GetComponentInParent<Enemy4_Bongtoi>();
            if (e4 != null && !e4.IsDead) { e4.ApplyStun(qSkillStunDuration); continue; }

            var e5 = col.GetComponentInParent<Enemy5_PhuThuy>();
            if (e5 != null && !e5.IsDead) { e5.ApplyStun(qSkillStunDuration); continue; }
        }
    }

    public void TriggerAttackSpeedBoostSkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        if (PlayerLevel < 10 && !IsSkillsUnlocked) return;
        if (IsAttackSpeedBoosted || isESkillPlayingAnim) return;

        Debug.Log($"[{gameObject.name}] TriggerAttackSpeedBoostSkill (Skill E) - Bắt đầu gồng...");

        if (isStandaloneMode)
        {
            if (!string.IsNullOrEmpty(eSkillAnimTrigger))
                PlayAnimation(eSkillAnimTrigger, 0.1f);
            isESkillPlayingAnim = true;
        }
        else if (IsOwner)
        {
            if (!string.IsNullOrEmpty(eSkillAnimTrigger))
                PlayAnimationLocal(eSkillAnimTrigger, 0.1f);
            isESkillPlayingAnim = true;
            TriggerESkillServerRpc(true);
        }
    }

    // EVENT ĐÓN NHẬN TỪ ANIMATION EVENT KHUNG HÌNH CUỐI CỦA SKILL E
    public void OnSkillEAnimEnd()
    {
        Debug.Log($"[{gameObject.name}] OnSkillEAnimEnd: Hoạt ảnh kết thúc -> Bắt đầu bất tử 5s!");
        isESkillPlayingAnim = false;

        if (isStandaloneMode || IsOwner)
        {
            isESkillActive = true;
            eSkillTimeRemaining = eSkillDuration;

            if (!isStandaloneMode)
            {
                StartESkillBuffServerRpc();
            }
        }
    }

    // Stub đã được override bởi các property/method cụ thể ở trên
    public event System.Action OnQSkillCancelled;

    [HideInInspector]
    public GameObject pendingPickItem;

    public void SetCursorLock(bool locked)
    {
        isCursorLocked = locked;
        LockCursor(locked);
    }

    public bool IsHoldingAxe()
    {
        return GetComponentInChildren<AxeItem>(true) != null;
    }

    public virtual int GetActiveWeaponIndex()
    {
        int val;
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
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

    private void Awake()
    {
        characterClassIndex = 3;
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
        if (anim != null)
        {
            GetRootMotionBridge();
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
        Debug.Log("[ArthurPlayer] Chạy ở chế độ STANDALONE. Di chuyển và tấn công hoạt động cục bộ.");
        LockCursor(isCursorLocked);

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
        isBlockingNet.OnValueChanged += OnBlockingNetChanged;

        isESkillActiveNet.OnValueChanged += OnESkillNetChanged;
        isQSkillActiveNet.OnValueChanged += OnQSkillNetChanged;
        isAimingNet.OnValueChanged += OnAimingNetChanged;

        upgradePoints.OnValueChanged += OnUpgradePointsChanged;
        hpLevel.OnValueChanged += OnHpLevelChanged;
        mpLevel.OnValueChanged += OnMpLevelChanged;
        cooldownLevel.OnValueChanged += OnCooldownLevelChanged;
        damageLevel.OnValueChanged += OnDamageLevelChanged;

        playerLevel.OnValueChanged += OnLevelOrExpChanged;
        playerExp.OnValueChanged += OnLevelOrExpChanged;

        weapon1Durability.OnValueChanged += OnDurabilityChanged;
        weapon2Durability.OnValueChanged += OnDurabilityChanged;
        isRollingNet.OnValueChanged += OnRollingNetChanged;

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
            LockCursor(isCursorLocked);
        }

        SyncWeaponVisuals(activeWeaponIndex.Value);
    }

    private void OnRollingNetChanged(bool oldVal, bool newVal)
    {
        // Bắt đầu lướt đã được thực hiện đồng bộ qua StartRollClientRpc để đảm bảo chính xác hướng lướt
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
        isBlockingNet.OnValueChanged -= OnBlockingNetChanged;

        isESkillActiveNet.OnValueChanged -= OnESkillNetChanged;
        isQSkillActiveNet.OnValueChanged -= OnQSkillNetChanged;
        isAimingNet.OnValueChanged -= OnAimingNetChanged;

        upgradePoints.OnValueChanged -= OnUpgradePointsChanged;
        hpLevel.OnValueChanged -= OnHpLevelChanged;
        mpLevel.OnValueChanged -= OnMpLevelChanged;
        cooldownLevel.OnValueChanged -= OnCooldownLevelChanged;
        damageLevel.OnValueChanged -= OnDamageLevelChanged;

        playerLevel.OnValueChanged -= OnLevelOrExpChanged;
        playerExp.OnValueChanged -= OnLevelOrExpChanged;

        weapon1Durability.OnValueChanged -= OnDurabilityChanged;
        weapon2Durability.OnValueChanged -= OnDurabilityChanged;
        isRollingNet.OnValueChanged -= OnRollingNetChanged;

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

    private void OnBlockingNetChanged(bool oldVal, bool newVal)
    {
        if (IsOwner) return;
        if (newVal)
        {
            if (anim != null) anim.CrossFadeInFixedTime("AnhitCoVuKhi", 0.1f, 1, 0f);
        }
        else
        {
            if (anim != null) anim.CrossFadeInFixedTime("New State", 0.1f, 1, 0f);
        }
    }

    [ServerRpc]
    private void SetBlockingServerRpc(bool blocking)
    {
        isBlockingNet.Value = blocking;
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        UpdateHealthHUD(newHealth);

        if (IsOwner)
        {
            SavePlayerStateToDatabase();
            if (newHealth <= 0f && oldHealth > 0f)
            {
                PlayerDeathEffectManager.Instance.PlayDeathEffect();
            }
            else if (newHealth > 0f && oldHealth <= 0f)
            {
                PlayerDeathEffectManager.Instance.ResetDeathEffect();
            }
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
        damageAmount = 15f * (1f + dmgLv * 0.15f);

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
            weapon1Durability.Value = Mathf.Min(weapon1Durability.Value + weapon1MaxDurability * 0.5f, weapon1MaxDurability);
        }
        else
        {
            weapon2Durability.Value = Mathf.Min(weapon2Durability.Value + weapon2MaxDurability * 0.5f, weapon2MaxDurability);
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
            }
            SavePlayerStateClientRpc();
        }
    }

    public void StandaloneUpgradeStat(int statType)
    {
        if (localUpgradePoints <= 0)
        {
            return;
        }
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
        if (upgradePoints.Value <= 0)
        {
            return;
        }
        int targetLvl = 0;
        switch (statType)
        {
            case 0: targetLvl = hpLevel.Value; break;
            case 1: targetLvl = mpLevel.Value; break;
            case 2: targetLvl = cooldownLevel.Value; break;
            case 3: targetLvl = damageLevel.Value; break;
        }
        if (targetLvl >= 3) return;

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
        HandleRAiming();

        bool hasControl = isStandaloneMode || (IsSpawned && IsOwner);

        // Đồng bộ di chuyển lướt (Roll) qua network cho cả Client Owner, Server, và các Client khác
        bool isRolling = isStandaloneMode ? isRollingStandalone : (IsSpawned && isRollingNet.Value);
        if (isRolling)
        {
            if (hasControl && rb != null && !rb.isKinematic)
            {
                float currentYVelocity = rb.linearVelocity.y;
                rb.linearVelocity = new Vector3(rollDirection.x * rollSpeed, currentYVelocity, rollDirection.z * rollSpeed);
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

        if (hasControl)
        {
            if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
            {
                isCursorLocked = !isCursorLocked;
                LockCursor(isCursorLocked);
            }

            // Tính toán và đồng bộ góc xoay cột sống (Spine aim angle)
            bool isCurrentlyAttacking = IsPlayingAttackState(out _, out _) || 
                                        (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f);
            
            bool isAimingOrAttacking = IsAiming || isCurrentlyAttacking;
            
            if (isAimingOrAttacking && !isRootedAttack && targetCamera != null)
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

                if (IsAiming && !isStandaloneMode)
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



        if (isESkillActive && (isStandaloneMode || IsOwner))
        {
            eSkillTimeRemaining -= Time.deltaTime;
            if (eSkillTimeRemaining <= 0f)
            {
                eSkillTimeRemaining = 0f;
                isESkillActive = false;

                if (!isStandaloneMode && IsOwner)
                {
                    TriggerESkillServerRpc(false);
                }
            }
        }

        if (isQSkillActive && (isStandaloneMode || IsOwner))
        {
            qSkillTimeRemaining -= Time.deltaTime;
            if (qSkillTimeRemaining <= 0f)
            {
                qSkillTimeRemaining = 0f;
                isQSkillActive = false;

                if (!isStandaloneMode && IsOwner)
                {
                    EndQSkillServerRpc();
                }
            }
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

        if (!IsOwner)
        {
            if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
            {
                anim.SetFloat(inputXParam, netMoveX.Value);
                anim.SetFloat(inputZParam, netMoveZ.Value);
                anim.SetFloat(speedParam, netSpeed.Value);

                bool isArmed = (GetActiveWeaponIndex() == 2 || GetActiveWeaponIndex() == 1);
                anim.SetBool(isArmedParam, isArmed);
            }
            return;
        }
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

            bool isArmed = (GetActiveWeaponIndex() == 2 || GetActiveWeaponIndex() == 1);
            anim.SetBool(isArmedParam, isArmed);

            if (!isStandaloneMode && IsOwner)
            {
                netMoveX.Value = smoothedInputX;
                netMoveZ.Value = smoothedInputZ;
                netSpeed.Value = smoothedSpeed;
            }
        }
    }

    protected virtual void HandleStandaloneUpdate()
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
                if (!useBlendTree) PlayAnimation("Idle", 0.1f);
            }
            return;
        }



        bool canBlock = GetActiveWeaponIndex() == 2 && CurrentHealth > 0 &&
                        !isRollingStandalone && !IsPlayingFullBodyAction();
        bool wantsToBlock = Input.GetMouseButton(1) && canBlock;
        if (wantsToBlock)
        {
            if (!isBlocking)
            {
                isBlocking = true;
                PlayAnimation("AnhitCoVuKhi", 0.1f);
            }
        }
        else
        {
            if (isBlocking)
            {
                isBlocking = false;
                PlayAnimation("New State", 0.1f);
            }
        }

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;
        if (isBlocking)
        {
            currentSpeed *= 0.4f;
        }

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

        bool isArmed = (GetActiveWeaponIndex() == 2 || GetActiveWeaponIndex() == 1);
        bool isMoving = (movementTranslation != Vector3.zero);

        float targetInputX = 0f;
        float targetInputZ = 0f;
        float targetSpeed = 0f;

        bool isAttacking = IsPlayingAttackState(out _, out _);

        // Xoay nhân vật: Luôn xoay theo hướng Camera để hỗ trợ đi ngang/lùi (strafe) mượt mà giống Elena
        // Cho phép xoay cả khi đang tấn công để nhân vật luôn hướng theo camera (chỉ thấy lưng, tránh vặn xương)
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

        if (isBlocking)
        {
            targetInputX *= 0.4f;
            targetInputZ *= 0.4f;
            targetSpeed *= 0.4f;
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

        if (Input.GetMouseButtonDown(0) || (Input.GetMouseButton(0) && !isExecutingAttack && !isBlocking))
        {
            if (localIsAimingR || isRShootPending) return;
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
                if (!useBlendTree) PlayAnimation("Idle", 0.1f);
            }
            return;
        }



        bool canBlock = GetActiveWeaponIndex() == 2 && CurrentHealth > 0 &&
                        (rollTimer <= 0) && !IsPlayingFullBodyAction();
        bool wantsToBlock = Input.GetMouseButton(1) && canBlock;
        if (wantsToBlock)
        {
            if (!isBlocking)
            {
                isBlocking = true;
                SetBlockingServerRpc(true);
                PlayAnimation("AnhitCoVuKhi", 0.1f);
            }
        }
        else
        {
            if (isBlocking)
            {
                isBlocking = false;
                SetBlockingServerRpc(false);
                PlayAnimation("New State", 0.1f);
            }
        }

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;
        if (isBlocking)
        {
            currentSpeed *= 0.4f;
        }

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

        bool isArmed = (GetActiveWeaponIndex() == 2 || GetActiveWeaponIndex() == 1);
        bool isMoving = (movementTranslation != Vector3.zero);

        float targetInputX = 0f;
        float targetInputZ = 0f;
        float targetSpeed = 0f;

        bool isAttacking = IsPlayingAttackState(out _, out _);

        // Xoay nhân vật: Luôn xoay theo hướng Camera để hỗ trợ đi ngang/lùi (strafe) mượt mà giống Elena
        // Cho phép xoay cả khi đang tấn công để nhân vật luôn hướng theo camera (chỉ thấy lưng, tránh vặn xương)
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

        if (isBlocking)
        {
            targetInputX *= 0.4f;
            targetInputZ *= 0.4f;
            targetSpeed *= 0.4f;
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

        if (Input.GetMouseButtonDown(0) || (Input.GetMouseButton(0) && !isExecutingAttack && !isBlocking))
        {
            if (localIsAimingR || isRShootPending) return;
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

        if (anim != null) anim.applyRootMotion = false;
        PlayAnimation("LonVong", 0.05f);
    }

    protected virtual void StartRollOwner(Vector3 moveInput)
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

        if (anim != null) anim.applyRootMotion = false;
        PlayAnimation("LonVong", 0.05f, false);
        StartRollServerRpc(rollDirection, transform.position);
    }

    public void OnRollEnd()
    {
        Debug.Log("[ArthurPlayer] Roll ended.");
        isRollingStandalone = false;
        rollTimer = 0f;

        if (anim != null) anim.applyRootMotion = false;
        if (isStandaloneMode || IsOwner)
        {
            var bridge = GetRootMotionBridge();
            if (bridge != null) bridge.ApplyFinalOffset();

            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

            if (!isStandaloneMode && IsOwner)
            {
                StopRollServerRpc(transform.position, transform.rotation);
            }
        }
    }

    void LateUpdate()
    {
        // Spine bone twist and combo offset
        if (anim != null)
        {
            bool isCurrentlyAttacking = IsPlayingAttackState(out _, out _) || 
                                         (IsAttackAnimationName(lastTriggeredAnimName) && Time.time - lastActionTriggerTime < 0.35f);
            
            float baseAimAngle = isStandaloneMode ? localAimAngle : netAimAngle.Value;
            
            float targetYOffset = 0f;
            float targetXOffset = 0f;

            if (IsAiming)
            {
                targetYOffset = rSkillYOffset;
                float pitchVal = isStandaloneMode ? currentPitch : netAimPitch.Value;
                float pitchFactor = invertSpinePitch ? -0.7f : 0.7f;
                targetXOffset = (pitchVal - 45f) * pitchFactor + rSkillXOffset;
            }
            else if (isCurrentlyAttacking && !isRootedAttack)
            {
                int weapon = GetActiveWeaponIndex();
                if (weapon == 1 || weapon == 2) // Weapon / Slash (Axe or Sword)
                {
                    if (comboStep == 1)
                    {
                        targetYOffset = slash1YOffset;
                        targetXOffset = slash1XOffset;
                    }
                    else if (comboStep == 2)
                    {
                        targetYOffset = slash2YOffset;
                        targetXOffset = slash2XOffset;
                    }
                    else if (comboStep == 3)
                    {
                        targetYOffset = slash3YOffset;
                        targetXOffset = slash3XOffset;
                    }
                }
                else // Unarmed / Punch
                {
                    if (comboStep == 1)
                    {
                        targetYOffset = punch1YOffset;
                        targetXOffset = punch1XOffset;
                    }
                    else if (comboStep == 2)
                    {
                        targetYOffset = punch2YOffset;
                        targetXOffset = punch2XOffset;
                    }
                    else if (comboStep == 3)
                    {
                        targetYOffset = punch3YOffset;
                        targetXOffset = punch3XOffset;
                    }
                }
            }

            smoothedYOffset = Mathf.Lerp(smoothedYOffset, targetYOffset, Time.deltaTime * spineSmoothSpeed);
            smoothedXOffset = Mathf.Lerp(smoothedXOffset, targetXOffset, Time.deltaTime * spineSmoothSpeed);

            // Xoay xương cột sống cho cả Aiming và Combo đánh thường
            if (IsAiming || (isCurrentlyAttacking && !isRootedAttack) || Mathf.Abs(smoothedYOffset) > 0.05f || Mathf.Abs(smoothedXOffset) > 0.05f)
            {
                Transform spine = GetSpineBone();
                if (spine != null)
                {
                    float finalYAngle = baseAimAngle + smoothedYOffset;
                    
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

    protected virtual float GetAttackDuration(int weaponIndex, int step)
    {
        if (weaponIndex == 1)
        {
            string animName = "ChatRiu";
            float duration = GetAnimationClipLength(animName);
            if (duration > 0f) return duration;
            return 2.267f;
        }
        else if (weaponIndex == 2)
        {
            string animName = "";
            if (step == 1) animName = "attack1";
            else if (step == 2) animName = "Attack1combo1";
            else if (step == 3) animName = "Attack2combo1";

            float duration = GetAnimationClipLength(animName);
            if (duration > 0f) return duration;

            if (step == 1) return slash1Duration;
            if (step == 2) return slash2Duration;
            return slash3Duration;
        }
        else // weaponIndex == 0 (Unarmed / Punch)
        {
            string animName = "";
            if (step == 1) animName = "Punch1";
            else if (step == 2) animName = "Punch2";
            else if (step == 3) animName = "Punch3";

            float duration = GetAnimationClipLength(animName);
            if (duration > 0f) return duration;

            if (step == 1) return punch1Duration;
            if (step == 2) return punch2Duration;
            return punch3Duration;
        }
    }

    protected virtual bool CanAttack()
    {
        if (CurrentHealth <= 0) return false;
        if (isBlocking) return false;

        if (isStandaloneMode ? isRollingStandalone : rollTimer > 0) return false;

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

    protected virtual void RequestComboAttack(bool networkMode, bool isContinuation = false)
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        if (isExecutingAttack)
        {
            pendingAttackRequest = true;
            Debug.Log("[ArthurPlayer] Nhấp chuột -> Lưu vào buffer, chờ OnPunchEnd / OnSlashEnd.");
        }
        else
        {
            PerformComboAttack(networkMode, isContinuation);
        }
    }

    private void PerformRaycastAttack()
    {
        if (!isStandaloneMode && !IsOwner) return;

        Vector3 origin = transform.position + Vector3.up * 1f;
        float range = attackRange;
        Collider[] hits = Physics.OverlapSphere(origin, range);
        
        foreach (var col in hits)
        {
            if (IsEnemy(col, out Collider enemyCollider))
            {
                Transform enemyRoot = enemyCollider.transform.root;
                if (!alreadyHitEnemies.Contains(enemyRoot))
                {
                    Vector3 toEnemy = (enemyCollider.bounds.center - origin);
                    toEnemy.y = 0; // Ignore height difference
                    
                    float angle = Vector3.Angle(transform.forward, toEnemy.normalized);
                    if (angle <= 75f)
                    {
                        alreadyHitEnemies.Add(enemyRoot);
                        Debug.Log($"[ArthurPlayer Raycast] HIT: {enemyRoot.name} | Damage: {damageAmount}");
                        
                        if (isStandaloneMode)
                        {
                            TryDamageEnemy(enemyCollider);
                        }
                        else if (IsOwner)
                        {
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
            else
            {
                // Kiểm tra xem có phải cây gỗ (ChoppableTree) hay không
                ChoppableTree tree = col.GetComponentInParent<ChoppableTree>();
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
                        Vector3 toTree = (col.bounds.center - origin);
                        toTree.y = 0; // Ignore height difference
                        
                        float angle = Vector3.Angle(transform.forward, toTree.normalized);
                        if (angle <= 75f)
                        {
                            alreadyHitEnemies.Add(treeRoot);
                            Vector3 hitPos = col.ClosestPoint(origin);
                            int weaponIndex = GetActiveWeaponIndex();
                            
                            Debug.Log($"[ArthurPlayer Raycast] HIT Tree: {tree.name} | WeaponIndex: {weaponIndex}");
                            tree.HitTree(hitPos, weaponIndex);
                        }
                    }
                }
            }
        }
    }

    private System.Collections.IEnumerator ComboChainCoroutine(int weapon, bool networkMode)
    {
        float totalDuration = currentAttackAnimDuration;

        // Chờ hết thời lượng animation thực tế
        yield return new WaitForSeconds(totalDuration);

        // Kết thúc nhịp tấn công
        isExecutingAttack = false;

        // Kiểm tra có buffer click để tiếp tục combo không
        if (pendingAttackRequest)
        {
            pendingAttackRequest = false;
            Debug.Log("[ArthurPlayer] Tiếp tục combo từ buffer sau khi kết thúc đòn cũ.");
            PerformComboAttack(networkMode, true);
        }
        else
        {
            // Không có buffer → reset combo step, mở khóa di chuyển, dọn dẹp Attack Layer
            comboStep = 0;
            isRootedAttack = false;
            ClearAttackLayer();
            Debug.Log("[ArthurPlayer] Kết thúc combo - không có input tiếp theo.");
        }
    }

    protected virtual void PerformComboAttack(bool networkMode, bool isContinuation = false)
    {
        int weapon = GetActiveWeaponIndex();
        if (!networkMode)
        {
            if (weapon == 1) Weapon1Durability = Mathf.Max(Weapon1Durability - 2f, 0f);
            else Weapon2Durability = Mathf.Max(Weapon2Durability - 2f, 0f);
        }
        float currentTime = Time.time;

        int nextStep = comboStep;

        if (!isContinuation && (currentTime - lastAttackTime > comboWindow))
        {
            nextStep = 0;
        }
        nextStep++;

        if (nextStep > 3) nextStep = 1;

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

        alreadyHitEnemies.Clear();
        DisableAllHitboxes();

        string animToPlay = "";
        if (weapon == 1)
        {
            animToPlay = "ChatRiu";
        }
        else if (weapon == 2)
        {
            if (comboStep == 1) animToPlay = "attack1";
            else if (comboStep == 2) animToPlay = "Attack1combo1";
            else if (comboStep == 3) animToPlay = "Attack2combo1";
        }
        else // weapon == 0 (Unarmed)
        {
            if (comboStep == 1) animToPlay = "Punch1";
            else if (comboStep == 2) animToPlay = "Punch2";
            else if (comboStep == 3) animToPlay = "Punch3";
        }

        if (!string.IsNullOrEmpty(animToPlay))
        {
            PlayAnimation(animToPlay, 0.05f, false);
        }

        if (networkMode)
        {
            AttackServerRpc();
        }

        // Thực hiện quét Raycast (OverlapSphere) phát hiện mục tiêu tức thì
        PerformRaycastAttack();

        // Chạy Coroutine tự động kết thúc/nối combo thay vì phụ thuộc Animation Event
        if (comboChainCoroutine != null) StopCoroutine(comboChainCoroutine);
        comboChainCoroutine = StartCoroutine(ComboChainCoroutine(weapon, networkMode));
    }

    private void HandleAttackSequenceEnd(bool isSlash)
    {
        // Hàm này không còn được gọi từ animation event nữa vì đã dùng ComboChainCoroutine, nhưng giữ lại để tránh lỗi compile nếu có chỗ khác dùng.
        isExecutingAttack = false;
        comboStep = 0;
        isRootedAttack = false;
        ClearAttackLayer();
    }

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
        if (isBlocking)
        {
            isBlocking = false;
            if (!isStandaloneMode && IsOwner)
            {
                SetBlockingServerRpc(false);
            }
            PlayAnimation("New State", 0.1f);
        }
        Debug.Log("[ArthurPlayer] Combo bị ngắt (bị hit/chết/lộn vòng).");
    }

    public void Onpunchend()
    {
        // Hàm rỗng để không ảnh hưởng
    }

    protected void TryDamageEnemy(Collider col)
    {
        float actualDamage = damageAmount;

        var e1 = col.GetComponentInParent<Enemy1_DapBua>();
        if (e1 != null) { e1.TakeDamage(actualDamage); return; }

        var e2 = col.GetComponentInParent<Enemy2_Zombie>();
        if (e2 != null) { e2.TakeDamage(actualDamage); return; }

        var e3 = col.GetComponentInParent<Enemy3_Buaa>();
        if (e3 != null) { e3.TakeDamage(actualDamage); return; }

        var e4 = col.GetComponentInParent<Enemy4_Bongtoi>();
        if (e4 != null) { e4.TakeDamage(actualDamage); return; }

        var e5 = col.GetComponentInParent<Enemy5_PhuThuy>();
        if (e5 != null) { e5.TakeDamage(actualDamage); return; }
    }



    [ServerRpc]
    private void TriggerESkillServerRpc(bool state)
    {
        if (state)
        {
            // Chỉ đồng bộ hoạt ảnh gồng chiêu E ban đầu cho các Proxy khác
            TriggerESkillClientRpc(true);
        }
        else
        {
            isESkillActiveNet.Value = false;
            TriggerESkillClientRpc(false);
        }
    }

    [ServerRpc]
    private void StartESkillBuffServerRpc()
    {
        isESkillActiveNet.Value = true;
        StartCoroutine(ServerESkillTimerCoroutine(eSkillDuration));
    }

    [ClientRpc]
    private void TriggerESkillClientRpc(bool state)
    {
        if (state)
        {
            if (!IsOwner && !string.IsNullOrEmpty(eSkillAnimTrigger))
            {
                PlayAnimationLocal(eSkillAnimTrigger, 0.1f);
            }
        }
    }

    private System.Collections.IEnumerator ServerESkillTimerCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (isESkillActiveNet.Value)
        {
            isESkillActiveNet.Value = false;
            TriggerESkillServerRpc(false);
        }
    }

    private void OnESkillNetChanged(bool oldVal, bool newVal)
    {
        isESkillActive = newVal;
        if (newVal)
        {
            eSkillTimeRemaining = eSkillDuration;
        }
        else
        {
            eSkillTimeRemaining = 0f;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  SKILL Q - DẶM KHIÊN: RPCs và Callbacks
    // ══════════════════════════════════════════════════════════════

    /// <summary>Server RPC: Broadcast hoạt ảnh SkillQ đến tất cả client (bao gồm cả proxy)</summary>
    [ServerRpc]
    private void TriggerQSkillServerRpc()
    {
        // Đồng bộ hoạt ảnh dặm khiên cho tất cả client không phải owner
        TriggerQSkillClientRpc();
    }

    [ClientRpc]
    private void TriggerQSkillClientRpc()
    {
        if (!IsOwner && !string.IsNullOrEmpty(qSkillAnimTrigger))
        {
            PlayAnimationLocal(qSkillAnimTrigger, 0.1f);
        }
    }

    /// <summary>Server RPC: Kích hoạt stun toàn map từ Server (authoritative)</summary>
    [ServerRpc]
    private void StartQSkillBuffServerRpc()
    {
        // Server thực thi stun (authoritative)
        ApplyQSkillStunToNearbyEnemies();
        isQSkillActiveNet.Value = true;
        StartCoroutine(ServerQSkillTimerCoroutine(qSkillDuration));
    }

    /// <summary>Server RPC: Reset trạng thái Q skill khi hết thời gian</summary>
    [ServerRpc]
    private void EndQSkillServerRpc()
    {
        isQSkillActiveNet.Value = false;
    }

    private System.Collections.IEnumerator ServerQSkillTimerCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (isQSkillActiveNet.Value)
        {
            isQSkillActiveNet.Value = false;
        }
    }

    private void OnQSkillNetChanged(bool oldVal, bool newVal)
    {
        isQSkillActive = newVal;
        if (newVal)
        {
            qSkillTimeRemaining = qSkillDuration;
        }
        else
        {
            qSkillTimeRemaining = 0f;
        }
    }





    [ServerRpc]
    protected void AttackServerRpc()
    {
        // Trừ độ bền vũ khí trên Server (dù trúng hay trượt)
        int weapon = GetActiveWeaponIndex();
        if (weapon == 1) weapon1Durability.Value = Mathf.Max(weapon1Durability.Value - 2f, 0f);
        else weapon2Durability.Value = Mathf.Max(weapon2Durability.Value - 2f, 0f);

        alreadyHitEnemies.Clear();
    }

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

    public void EnableLeftHitbox() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableLeftHitbox() {}
    public void EnableRightHitbox() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableRightHitbox() {}
    public void EnableBothHitboxes() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableBothHitboxes() {}
    public void EnableLeftWeaponHitbox() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableLeftWeaponHitbox() {}
    public void EnableRightWeaponHitbox() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableRightWeaponHitbox() {}
    public void EnableBothWeaponHitboxes() { alreadyHitEnemies.Clear(); PerformRaycastAttack(); }
    public void DisableBothWeaponHitboxes() {}
    public void DisableAllHitboxes() {}

    public void OnPunchEnd() {}
    public void OnSlashEnd() {}
    public void OnAttackEnd() {}

    private bool CanActivateHitbox()
    {
        return isStandaloneMode || (IsSpawned && IsOwner);
    }

    public void OnHitboxCollision(Collider other)
    {
        if (!CanActivateHitbox()) return;

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

    public void CreateSwordHitboxes()
    {
        Transform leftHand = FindBoneRecursive(transform, "left");
        Transform rightHand = FindBoneRecursive(transform, "right");

        if (leftHand == null) leftHand = transform;
        if (rightHand == null) rightHand = transform;

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

        if (leftHandWeapon != null)
        {
            Collider col = leftHandWeapon.GetComponent<Collider>();
            if (col == null) col = leftHandWeapon.GetComponentInChildren<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                col.enabled = false;
                if (leftHandWeapon.GetComponent<PlayerHitbox>() == null)
                {
                    leftHandWeapon.AddComponent<PlayerHitbox>();
                }
                leftWeaponHitbox = col;
            }
        }
        else
        {
            leftWeaponHitbox = null;
        }

        if (rightHandWeapon != null)
        {
            Collider col = rightHandWeapon.GetComponent<Collider>();
            if (col == null) col = rightHandWeapon.GetComponentInChildren<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                col.enabled = false;
                if (rightHandWeapon.GetComponent<PlayerHitbox>() == null)
                {
                    rightHandWeapon.AddComponent<PlayerHitbox>();
                }
                rightWeaponHitbox = col;
            }
        }
        else
        {
            rightWeaponHitbox = null;
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

    private bool IsPlayingFullBodyAction()
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("LonVong") ||
               stateInfo.IsName("GetHit") ||
               stateInfo.IsName("GeiHit2") ||
               stateInfo.IsName("Idle_Pick") ||
               stateInfo.IsName("Death");
    }

    private Transform FindClosestAttacker()
    {
        Transform closest = null;
        float minDist = float.MaxValue;
        Vector3 myPos = transform.position;

        var e1s = FindObjectsOfType<Enemy1_DapBua>();
        foreach (var e in e1s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(myPos, e.transform.position);
            if (d < minDist) { minDist = d; closest = e.transform; }
        }

        var e2s = FindObjectsOfType<Enemy2_Zombie>();
        foreach (var e in e2s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(myPos, e.transform.position);
            if (d < minDist) { minDist = d; closest = e.transform; }
        }

        var e3s = FindObjectsOfType<Enemy3_Buaa>();
        foreach (var e in e3s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(myPos, e.transform.position);
            if (d < minDist) { minDist = d; closest = e.transform; }
        }

        var e4s = FindObjectsOfType<Enemy4_Bongtoi>();
        foreach (var e in e4s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(myPos, e.transform.position);
            if (d < minDist) { minDist = d; closest = e.transform; }
        }

        var e5s = FindObjectsOfType<Enemy5_PhuThuy>();
        foreach (var e in e5s)
        {
            if (e == null || e.IsDead) continue;
            float d = Vector3.Distance(myPos, e.transform.position);
            if (d < minDist) { minDist = d; closest = e.transform; }
        }

        var spellBalls = FindObjectsOfType<SpellBall>();
        foreach (var sb in spellBalls)
        {
            if (sb == null) continue;
            float d = Vector3.Distance(myPos, sb.transform.position);
            if (d < minDist) { minDist = d; closest = sb.transform; }
        }

        return closest;
    }

    private bool IsAttackerInFront(Transform attacker)
    {
        if (attacker == null) return false;
        Vector3 dirToAttacker = (attacker.position - transform.position);
        dirToAttacker.y = 0;
        dirToAttacker.Normalize();
        float dot = Vector3.Dot(transform.forward, dirToAttacker);
        return dot >= 0.25f;
    }

    public void TakeDamage(float damage)
    {
        // Kiểm tra trạng thái bất tử (Skill E)
        bool invincible = isStandaloneMode ? isESkillActive : isESkillActiveNet.Value;
        if (invincible)
        {
            Debug.Log($"[{gameObject.name}] Arthur đang bất tử (Skill E) -> Bỏ qua sát thương: {damage}");
            return;
        }

        if (isStandaloneMode)
        {
            if (isRollingStandalone)
            {
                return;
            }
        }
        else
        {
            if (isRollingNet.Value)
            {
                return;
            }
        }

        bool blocking = isStandaloneMode ? isBlocking : isBlockingNet.Value;
        if (blocking)
        {
            Transform attacker = FindClosestAttacker();
            if (attacker != null && IsAttackerInFront(attacker))
            {
                PlayAnimation("DoKhienDinhSatThuong", 0.05f);
                return;
            }
        }

        if (isStandaloneMode)
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            UpdateHealthHUD(localHealth);

            InterruptCombo();

            if (localHealth <= 0)
            {
                PlayAnimation("Death", 0.15f);
                PlayerDeathEffectManager.Instance.PlayDeathEffect();
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

        InterruptCombo();

        if (currentHealth.Value <= 0)
        {
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
            Debug.Log($"[ArthurPlayer Standalone] Hồi {amount} máu. Máu hiện tại: {localHealth}");
        }
        else if (IsServer)
        {
            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
            Debug.Log($"[ArthurPlayer Server] Hồi {amount} máu cho {gameObject.name}. Máu hiện tại: {currentHealth.Value}");
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        bool blocking = isStandaloneMode ? isBlocking : isBlockingNet.Value;
        if (blocking)
        {
            Transform attacker = FindClosestAttacker();
            if (attacker != null && IsAttackerInFront(attacker))
            {
                force *= 0.1f;
            }
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

    protected bool HasParameter(string paramName)
    {
        if (anim == null) return false;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    protected System.Collections.IEnumerator ResetTriggerNextFrame(string triggerName)
    {
        yield return null;
        if (anim != null && !string.IsNullOrEmpty(triggerName) && HasParameter(triggerName))
        {
            anim.ResetTrigger(triggerName);
        }
    }

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
               name == "attack1" ||
               name == "Attack1combo1" ||
               name == "Attack2combo1" ||
               name == "ChatRiu" ||
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

        isSwitchingWeapon = true;

        if (newWeapon == 2)
        {
            if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(true);
            if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(true);
            if (leftHandWeapon != null) leftHandWeapon.SetActive(false);
            if (rightHandWeapon != null) rightHandWeapon.SetActive(false);

            StartCoroutine(DrawBothWeaponsSequence());
        }
        else if (newWeapon == 1)
        {
            if (leftHandWeapon != null) leftHandWeapon.SetActive(true);
            if (rightHandWeapon != null) rightHandWeapon.SetActive(true);
            if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(false);
            if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(false);

            StartCoroutine(SheatheBothWeaponsSequence());
        }
    }

    private System.Collections.IEnumerator DrawBothWeaponsSequence()
    {
        if (!string.IsNullOrEmpty(drawLeftTrigger) && anim != null && anim.isActiveAndEnabled)
        {
            PlayAnimation(drawLeftTrigger, 0.1f);

            float drawLeftDuration = GetAnimationClipLength(drawLeftTrigger);
            if (drawLeftDuration <= 0f) drawLeftDuration = 0.8f;
            yield return new WaitForSeconds(drawLeftDuration * 0.85f);

            DrawLeftSword();
        }
        else
        {
            DrawLeftSword();
        }

        if (!string.IsNullOrEmpty(drawRightTrigger) && anim != null && anim.isActiveAndEnabled)
        {
            PlayAnimation(drawRightTrigger, 0.1f);

            float drawRightDuration = GetAnimationClipLength(drawRightTrigger);
            if (drawRightDuration <= 0f) drawRightDuration = 0.8f;
            yield return new WaitForSeconds(drawRightDuration * 0.85f);

            DrawRightSword();
        }
        else
        {
            DrawRightSword();
        }

        SyncWeaponVisuals(2);
        OnWeaponSwitchEnd();
    }

    private System.Collections.IEnumerator SheatheBothWeaponsSequence()
    {
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

        SyncWeaponVisuals(1);
        OnWeaponSwitchEnd();
    }

    private float GetAnimationClipLength(string name)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return 0f;

        string targetName = name.ToLower();

        if (targetName == "drawleft") targetName = "laykhien";
        else if (targetName == "drawright") targetName = "laykiem";
        else if (targetName == "sheatheleft") targetName = "catkhieng";
        else if (targetName == "sheatheright") targetName = "catkiem";
        else if (targetName == "chatriu") targetName = "chat cayy";

        foreach (var clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip != null)
            {
                string clipNameLower = clip.name.ToLower();
                if (clipNameLower == targetName || clipNameLower == name.ToLower() || clipNameLower.Contains(targetName))
                {
                    return clip.length;
                }
            }
        }
        return 0f;
    }

    public void OnDrawLeftEnd() { }

    public void OnSheatheLeftEnd() { }

    public void DrawLeftSword()
    {
        if (leftHandWeapon != null) leftHandWeapon.SetActive(true);
        if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(false);
    }

    public void DrawRightSword()
    {
        if (rightHandWeapon != null) rightHandWeapon.SetActive(true);
        if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(false);
    }

    public void SheatheLeftSword()
    {
        if (leftHandWeapon != null) leftHandWeapon.SetActive(false);
        if (leftShoulderWeapon != null) leftShoulderWeapon.SetActive(true);
    }

    public void SheatheRightSword()
    {
        if (rightHandWeapon != null) rightHandWeapon.SetActive(false);
        if (rightShoulderWeapon != null) rightShoulderWeapon.SetActive(true);
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
    }

    protected virtual bool IsAttackAnimationName(string name)
    {
        return name == "Punch1" ||
               name == "Punch2" ||
               name == "Punch3" ||
               name == "Slash1" ||
               name == "Slash2" ||
               name == "Slash3" ||
               name == "attack1" ||
               name == "Attack1combo1" ||
               name == "Attack2combo1" ||
               name == "ChatRiu";
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
               stateInfo.IsName("Slash3") ||
               stateInfo.IsName("attack1") ||
               stateInfo.IsName("Attack1combo1") ||
               stateInfo.IsName("Attack2combo1") ||
               (!string.IsNullOrEmpty(rSkillAnimTrigger) && stateInfo.IsName(rSkillAnimTrigger));
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
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying && animName != "Death" && animName != "Idle" && animName != "Walk" && animName != "run")
        {
            return;
        }

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

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;

        if (animName == rSkillAnimTrigger || animName == "NemRM")
        {
            isRShootPending = true;
            isPendingRShootNetworkMode = !isStandaloneMode;
            OnAimStateChanged(localIsAimingR);
        }

        bool isLoopingAnim = animName == "Idle" || animName == "Walk" || animName == "run";
        if (isLoopingAnim && currentAnimState == animName) return;

        Debug.Log($"[ArthurPlayer] Kích hoạt Hoạt ảnh: '{animName}'");
        if (animName == "LonVong")
        {
            anim.applyRootMotion = false;
        }

        if (animName == "attack1" || animName == "Attack1combo1" || animName == "Attack2combo1" ||
            animName == "AnhitCoVuKhi" || animName == "DoKhienDinhSatThuong" || animName == "New State" ||
            animName == "ChatRiu")
        {
            if (HasParameter(animName))
            {
                anim.SetTrigger(animName);
                StartCoroutine(ResetTriggerNextFrame(animName));
            }

            if (anim.layerCount > 1)
            {
                // KHẮC PHỤC LỖI GIẬT VÀ KẸT KIẾM: Loại bỏ hoàn toàn anim.SetTrigger(animName). Chỉ giữ độc nhất lệnh CrossFade.
                anim.CrossFadeInFixedTime(animName, fadeTime, 1, 0f);
            }

            bool isAttackState = IsAttackAnimationName(animName);
            if (isAttackState)
            {
                isExecutingAttack = true;
                attackAnimStartTime = Time.time;
                int weapon = GetActiveWeaponIndex();
                currentAttackAnimDuration = GetAttackDuration(weapon, comboStep);
            }
            lastTriggeredAnimName = animName;
            if (IsActionAnimationName(animName))
            {
                lastActionTriggerTime = Time.time;
            }
            return;
        }

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
    private void StartRollServerRpc(Vector3 direction, Vector3 position)
    {
        isRollingNet.Value = true;
        transform.position = position;
        if (rb != null)
        {
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
            PlayAnimationLocal("LonVong", 0.05f);
        }
    }

    [ServerRpc]
    protected void StopRollServerRpc(Vector3 position, Quaternion rotation)
    {
        isRollingNet.Value = false;
        transform.position = position;
        transform.rotation = rotation;
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector3.zero;
        }
    }

    protected virtual void ClearAttackLayer()
    {
        comboStep = 0;
        isRootedAttack = false;
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            anim.CrossFadeInFixedTime("New State", 0.1f, 1, 0f);
        }
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
            return true;
        }
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
        if (PlayerHUDManager.ActivePlayers != null)
        {
            PlayerHUDManager.ActivePlayers.Remove(this);
        }
        base.OnDestroy();
    }
}