using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MayaPlayer : NetworkBehaviour, IPlayerHUDTarget
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
        playerAudioSource.spatialBlend = 1.0f; // 3D Sound for distance attenuation
        playerAudioSource.minDistance = 2.0f;
        playerAudioSource.maxDistance = 25.0f;
        playerAudioSource.rolloffMode = AudioRolloffMode.Linear;
        playerAudioSource.playOnAwake = false;
        playerAudioSource.mute = false;
        playerAudioSource.volume = 1.0f;

        if (footstepClip == null) footstepClip = Resources.Load<AudioClip>("Audio/Footstep");
        if (footstepClip2 == null) footstepClip2 = Resources.Load<AudioClip>("Audio/Footstep2");
        // Tạm thời comment các âm thanh chưa có để tránh loạn âm thanh
        /*
        if (attackClip == null) attackClip = Resources.Load<AudioClip>("Audio/SwordSlash");
        if (hitClip == null) hitClip = Resources.Load<AudioClip>("Audio/HitHurt");
        if (deathClip == null) deathClip = Resources.Load<AudioClip>("Audio/Death");
        */
        
        // Tải âm thanh Skill mới thêm (Skill R nguyên tố)
        if (skillRClip == null) skillRClip = Resources.Load<AudioClip>("Audio/Fireball");

        // Log warnings if audio files fail to load
        if (footstepClip == null) Debug.LogWarning($"[Audio Debug] MayaPlayer: Failed to load Resources/Audio/Footstep");
        else Debug.Log($"[Audio Debug] MayaPlayer: Successfully loaded Resources/Audio/Footstep");
        if (footstepClip2 == null) Debug.LogWarning($"[Audio Debug] MayaPlayer: Failed to load Resources/Audio/Footstep2");
        else Debug.Log($"[Audio Debug] MayaPlayer: Successfully loaded Resources/Audio/Footstep2");
        /*
        if (attackClip == null) Debug.LogWarning($"[Audio Debug] MayaPlayer: Failed to load Resources/Audio/SwordSlash");
        else Debug.Log($"[Audio Debug] MayaPlayer: Successfully loaded Resources/Audio/SwordSlash");
        if (hitClip == null) Debug.LogWarning($"[Audio Debug] MayaPlayer: Failed to load Resources/Audio/HitHurt");
        if (deathClip == null) Debug.LogWarning($"[Audio Debug] MayaPlayer: Failed to load Resources/Audio/Death");
        */
        if (skillRClip == null) Debug.LogWarning($"[Audio Debug] MayaPlayer: Failed to load Resources/Audio/Fireball (Skill R)");
        else Debug.Log($"[Audio Debug] MayaPlayer: Successfully loaded Resources/Audio/Fireball (Skill R)");
    }

    private void PlayPlayerSFX(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[Audio Debug] MayaPlayer: Attempted to play a NULL AudioClip!");
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
        Debug.Log($"[Audio Debug] MayaPlayer: Playing SFX '{clip.name}' at volume {finalVolume} (scale={volumeScale}, sfxVol={sfxVol}, masterVol={masterVol})");
        playerAudioSource.PlayOneShot(clip, finalVolume);
    }

    [Header("Movement & Attack Settings")]
    public float moveSpeed = 5f;
    public float runSpeedMultiplier = 2.0f;
    public float damageAmount = 20f;
    public float attackRange = 3f;
    private List<Transform> alreadyHitEnemies = new List<Transform>();

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
    public string drawWeaponTrigger = "LayVuKhi";
    public string sheathWeaponTrigger = "CatVuKhi";

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
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isSkillsUnlocked = new NetworkVariable<bool>(
        true,
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
    public int characterClassIndex = 1;

    [Header("Player Name Sync")]
    public NetworkVariable<Unity.Collections.FixedString64Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Maya", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // IPlayerHUDTarget Stats Implementation
    public string DisplayName => string.IsNullOrEmpty(playerName.Value.ToString()) ? "Maya" : playerName.Value.ToString();
    public int PlayerLevel => isStandaloneMode ? localLevel : playerLevel.Value;
    public float PlayerExp => isStandaloneMode ? localExp : playerExp.Value;
    public float MaxExp => 100f + (isStandaloneMode ? localLevel : playerLevel.Value) * 50f;

    [Header("Knockback Settings")]
    private Vector3 knockbackVelocity;
    private Vector3 targetMoveVelocity;

    [Header("Gravity Settings")]
    public float gravityMultiplier = 2.5f;

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

    [Header("Normal Attack Spawning Settings")]
    public GameObject normalAttackPrefab;     // Prefab đòn đánh thường (Projectile)
    public Transform normalAttackSpawnPoint;   // Điểm xuất phát đòn đánh thường
    public float normalAttackSpeed = 30f;      // Tốc độ bay đòn đánh thường
    public float ShootingCooldown = 1.0f; // Thời gian chờ giữa mỗi lần bắn (giây)
    private float ShootingCooldownTimer = 0f; // Bộ đếm thời gian chờ bắn
    private bool isShootPending = false;       // Đánh dấu chuẩn bị bắn từ animation event
    private bool isPendingShootNetworkMode = false; // Đánh dấu chế độ bắn mạng hay local
    private bool isRShootPending = false;       // Đánh dấu chuẩn bị bắn R từ animation event
    private bool isPendingRShootNetworkMode = false; // Đánh dấu chế độ bắn R mạng hay local

    [Header("E Skill Healing Zone Settings")]
    public float eSkillCooldown = 10f; // Cooldown của kỹ năng E (giây)
    public float eSkillHealRadius = 5f;
    public float eSkillHealDuration = 5f;
    public float eSkillHealAmount = 10f;
    public GameObject eSkillVfxPrefab;

    private float eSkillCooldownTimer = 0f; // Bộ đếm cooldown E
    private float eSkillActiveTimer = 0f;   // Bộ đếm thời lượng kích hoạt E
    private bool isETargeting = false;      // Đang trong trạng thái nhắm E
    private GameObject eTargetingIndicator; // Vùng sáng chọn vị trí E

    // Giữ các biến cũ để tránh lỗi biên dịch ở các chỗ khác
    private int eSkillRemainingNormalAttacks = 0;
    public int eSkillMaxPiercingNormalAttacks = 3;
    public NetworkVariable<int> eSkillRemainingNormalAttacksNet = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public int ESkillRemainingNormalAttacks => 0;
    private bool localIsESkillActive = false;
    public NetworkVariable<bool> isESkillActiveNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsESkillActive => eSkillActiveTimer > 0f;

    [Header("Q Skill (Triple Attacks) Settings")]
    public float qSkillCooldown = 15f; // Cooldown của kỹ năng Q (giây)
    public float qSkillDuration = 10f; // Thời lượng tác dụng kỹ năng Q (giây)
    public float qSkillSpreadAngle = 10f; // Góc lệch của các tia bên cạnh
    public GameObject qSkillSkeletonPrefab; // Prefab con Skeleton đệ triệu hồi
    private float qSkillCooldownTimer = 0f; // Bộ đếm cooldown Q
    private float qSkillDurationTimer = 0f; // Bộ đếm thời lượng Q
    private bool localIsQSkillActive = false; // Trạng thái kỹ năng Q ở local
    public NetworkVariable<bool> isQSkillActiveNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsQSkillActive => isStandaloneMode ? localIsQSkillActive : isQSkillActiveNet.Value;



    [Header("Skill R - Bắn Cục Nước")]
    [Tooltip("Prefab đạn nước của chiêu R")]
    public GameObject rSkillWaterPrefab;
    [Tooltip("Điểm xuất phát bắn đạn nước của chiêu R")]
    public Transform rSkillWaterSpawnPoint;
    [Tooltip("Tốc độ bay của đạn nước")]
    public float rSkillWaterSpeed = 20f;
    [Tooltip("Sát thương của đạn nước")]
    public float rSkillWaterDamage = 40f;

    private bool localIsAimingR = false;
    private GameObject rSkillHandPreviewVisual;

    private float defaultCameraDistance;
    private float defaultPivotHeight;
    private float currentShoulderOffset = 0f;
    private bool localIsAiming = false;
    public NetworkVariable<bool> isAimingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isAimingRNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public bool IsAiming => isStandaloneMode ? (localIsAiming || localIsAimingR || isRShootPending) : (IsOwner ? (localIsAiming || localIsAimingR || isRShootPending) : (isAimingNet.Value || isAimingRNet.Value));
    public bool IsAimingR => isStandaloneMode ? localIsAimingR : (IsOwner ? localIsAimingR : isAimingRNet.Value);
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
                Debug.Log($"[MayaPlayer] Dynamically added RootMotionBridge to {anim.gameObject.name} at runtime.");
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
    public bool IsSwitchingWeapon => false; // Maya doesn't have draw/sheath lock state
    public float Weapon1MaxDurability => weapon1MaxDurability;
    public float Weapon2MaxDurability => weapon2MaxDurability;
    public string[] InventorySlots => inventorySlots;
    public float MaxHealth => maxHealth;

    private bool isDeathAnimFinished = false;
    public bool IsDeathAnimationFinished => isDeathAnimFinished;
    protected float respawnImmunityTimer = 0f;

    public void ResetDeathState()
    {
        StopAllCoroutines();
        localHealth = maxHealth;
        isDeathAnimFinished = false;
        currentAnimState = "Idle";
        lastTriggeredAnimName = "Idle";
        enabled = true;
        respawnImmunityTimer = 2.0f; // 2 giây bất tử khi vừa hồi sinh ở Checkpoint

        if (rb != null)
        {
            rb.isKinematic = isStandaloneMode ? false : !IsOwner;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            SafeResetTrigger("Death");
            SafeResetTrigger("GetHit");
            SafeResetTrigger("GeiHit2");

            if (anim.layerCount > 1)
            {
                anim.SetLayerWeight(1, 0f);
                try { anim.Play("New State", 1, 0f); } catch {}
            }

            try { anim.Rebind(); } catch {}
            anim.Play("Idle", 0, 0f);
            anim.Update(0f);
        }
        var netAnim = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (netAnim != null)
        {
            try { netAnim.ResetTrigger("Death"); } catch {}
        }
    }

    public void OnDeathAnimationEnd()
    {
        if (localHealth > 0f || CurrentHealth > 0f) return;
        if (IsOwner || isStandaloneMode)
        {
            StartCoroutine(DeathEyelidsSequenceCoroutine());
        }
    }

    private System.Collections.IEnumerator DeathEyelidsSequenceCoroutine()
    {
        if (localHealth > 0f || CurrentHealth > 0f)
        {
            PlayerDeathEffectManager.Instance.ResetDeathEffect();
            yield break;
        }
        float duration = 1.5f;
        PlayerDeathEffectManager.Instance.PlayDeathEffect(duration);
        yield return new WaitForSeconds(duration);
        if (localHealth > 0f || CurrentHealth > 0f)
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

    private System.Collections.IEnumerator FallbackDeathSequenceCoroutine()
    {
        yield return new WaitForSeconds(1.2f);
        if ((localHealth <= 0f || CurrentHealth <= 0f) && !isDeathAnimFinished)
        {
            Debug.Log($"[MayaPlayer] Kích hoạt quy trình chớp mắt dự phòng cho {gameObject.name}");
            OnDeathAnimationEnd();
        }
    }

    private void PlayDeathAnimationSafely(float fadeTime)
    {
        isDeathAnimFinished = false; // Đảm bảo trạng thái chưa xong để không bị Server hồi sinh vội

        // 1. Triệt tiêu vận tốc & khóa vật lý ngay lập tức để không bị trượt đi khi chết
        targetMoveVelocity = Vector3.zero;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (anim == null) return;

        if (anim.layerCount > 1)
        {
            anim.SetLayerWeight(1, 0f);
        }

        SafeResetTrigger("GetHit");
        SafeResetTrigger("GeiHit2");
        SafeSetTrigger("Death");
        SafeSetTrigger("die");
        SafeSetTrigger("DeathTrigger");

        string[] candidateStates = { "Death", "death", "Die", "Dead", "die" };
        bool crossFaded = false;
        foreach (var state in candidateStates)
        {
            int hash = Animator.StringToHash(state);
            if (anim.HasState(0, hash))
            {
                anim.CrossFadeInFixedTime(state, fadeTime, 0, 0f);
                crossFaded = true;
                break;
            }
        }

        if (!crossFaded)
        {
            try { anim.CrossFadeInFixedTime("Death", fadeTime, 0, 0f); } catch {}
        }

        // Kích hoạt ngay lập tức quy trình nhắm mắt UI
        if (IsOwner || isStandaloneMode)
        {
            StartCoroutine(DeathEyelidsSequenceCoroutine());
        }
        else
        {
            StartCoroutine(FallbackDeathSequenceCoroutine());
        }
    }

    [ServerRpc]
    private void NotifyDeathAnimFinishedServerRpc()
    {
        isDeathAnimFinished = true;
    }


    // Invisibility Skill R (stub - legacy removed)
    public bool IsInvisible => false;
    public float InvisibilityTimeRemaining => 0f;
    private bool IsSkillsUnlocked => isSkillsUnlocked.Value;

    public void TriggerInvisibilitySkill()
    {
        if (PlayerLevel < 5 && !IsSkillsUnlocked) return;
        PlayPlayerSFX(skillQClip); // SmokeBomb SFX
        TriggerRSkill();
    }

    // Attack Speed Boost Skill E (Maya's Piercing Normal Attacks)
    public bool IsAttackSpeedBoosted => IsESkillActive;
    public float AttackSpeedBoostTimeRemaining => ESkillRemainingNormalAttacks;
    public void TriggerAttackSpeedBoostSkill()
    {
        if (PlayerLevel < 10 && !IsSkillsUnlocked) return;
        TriggerESkill();
    }

    public void TriggerESkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        if (PlayerLevel < 10 && !IsSkillsUnlocked) return;
        if (GetActiveWeaponIndex() == 1) return;
        if (eSkillCooldownTimer > 0f) return;
        isETargeting = true;
    }

    private void UpdateETargetingIndicator()
    {
        if (isETargeting)
        {
            if (eTargetingIndicator == null)
            {
                eTargetingIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var indicatorCol = eTargetingIndicator.GetComponent<Collider>();
                if (indicatorCol != null)
                {
                    Destroy(indicatorCol);
                }
                
                var renderer = eTargetingIndicator.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader transparentShader = Shader.Find("Sprites/Default");
                    if (transparentShader != null)
                    {
                        Material mat = new Material(transparentShader);
                        mat.color = new Color(0.2f, 1f, 0.3f, 0.3f);
                        renderer.material = mat;
                    }
                }
            }

            eTargetingIndicator.transform.localScale = new Vector3(eSkillHealRadius * 2f, 0.02f, eSkillHealRadius * 2f);

            if (targetCamera != null)
            {
                Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
                if (Physics.Raycast(ray, out RaycastHit hit, 25f, layerMask))
                {
                    eTargetingIndicator.transform.position = hit.point + new Vector3(0f, 0.05f, 0f);
                    eTargetingIndicator.SetActive(true);
                }
                else
                {
                    Vector3 defaultPoint = transform.position + transform.forward * 12f;
                    defaultPoint.y = transform.position.y;
                    eTargetingIndicator.transform.position = defaultPoint + new Vector3(0f, 0.05f, 0f);
                    eTargetingIndicator.SetActive(true);
                }
            }
        }
        else
        {
            if (eTargetingIndicator != null)
            {
                Destroy(eTargetingIndicator);
                eTargetingIndicator = null;
            }
        }
    }

    private void CastEHealingZone(bool networkMode)
    {
        if (eTargetingIndicator == null) return;

        Vector3 targetPos = eTargetingIndicator.transform.position;

        // Play the casting/shooting animation without triggering a normal projectile attack
        PlayAnimation("Shooting", 0.05f);

        if (networkMode)
        {
            SpawnEHealingZoneServerRpc(targetPos);
        }
        else
        {
            SpawnEHealingZoneLocal(targetPos);
        }

        StartESkillCooldown();
        eSkillActiveTimer = eSkillHealDuration;

        isETargeting = false;
        UpdateETargetingIndicator();
    }

    private void SpawnEHealingZoneLocal(Vector3 position)
    {
        GameObject zoneObj = new GameObject("MayaHealingZone_Local");
        zoneObj.transform.position = position;
        var zone = zoneObj.AddComponent<MayaHealingZone>();
        zone.radius = eSkillHealRadius;
        zone.duration = eSkillHealDuration;
        zone.healAmount = eSkillHealAmount;

        // Always spawn the green cylinder visual for demo purposes
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var col = cylinder.GetComponent<Collider>();
        if (col != null) Destroy(col);

        cylinder.transform.SetParent(zoneObj.transform);
        cylinder.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        cylinder.transform.localScale = new Vector3(eSkillHealRadius * 2f, 0.01f, eSkillHealRadius * 2f);
        cylinder.transform.localRotation = Quaternion.identity;

        var renderer = cylinder.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader transparentShader = Shader.Find("Sprites/Default");
            if (transparentShader != null)
            {
                Material mat = new Material(transparentShader);
                mat.color = new Color(0.2f, 0.8f, 0.3f, 0.25f);
                renderer.material = mat;
            }
        }

        // Optionally spawn custom VFX prefab if assigned
        if (eSkillVfxPrefab != null)
        {
            GameObject vfxObj = Instantiate(eSkillVfxPrefab, position, Quaternion.identity);
            vfxObj.transform.SetParent(zoneObj.transform);
            Destroy(vfxObj, eSkillHealDuration);
        }
    }

    [ServerRpc]
    private void SpawnEHealingZoneServerRpc(Vector3 position)
    {
        GameObject zoneObj = new GameObject("MayaHealingZone_ServerAuthoritative");
        zoneObj.transform.position = position;
        var zone = zoneObj.AddComponent<MayaHealingZone>();
        zone.radius = eSkillHealRadius;
        zone.duration = eSkillHealDuration;
        zone.healAmount = eSkillHealAmount;

        SpawnHealingZoneVisualClientRpc(position);
    }

    [ClientRpc]
    private void SpawnHealingZoneVisualClientRpc(Vector3 position)
    {
        GameObject visualObj = new GameObject("MayaHealingZone_VisualClient");
        visualObj.transform.position = position;

        // Always spawn the green cylinder visual for demo purposes
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var col = cylinder.GetComponent<Collider>();
        if (col != null) Destroy(col);

        cylinder.transform.SetParent(visualObj.transform);
        cylinder.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        cylinder.transform.localScale = new Vector3(eSkillHealRadius * 2f, 0.01f, eSkillHealRadius * 2f);
        cylinder.transform.localRotation = Quaternion.identity;

        var renderer = cylinder.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader transparentShader = Shader.Find("Sprites/Default");
            if (transparentShader != null)
            {
                Material mat = new Material(transparentShader);
                mat.color = new Color(0.2f, 0.8f, 0.3f, 0.25f);
                renderer.material = mat;
            }
        }

        // Optionally spawn custom VFX prefab if assigned
        if (eSkillVfxPrefab != null)
        {
            GameObject vfx = Instantiate(eSkillVfxPrefab, position, Quaternion.identity);
            vfx.transform.SetParent(visualObj.transform);
        }

        Destroy(visualObj, eSkillHealDuration);
    }

    private void EndESkill()
    {
        eSkillActiveTimer = 0f;
    }

    public void StartESkillCooldown()
    {
        eSkillCooldownTimer = eSkillCooldown;
        if (isStandaloneMode)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.TriggerMayaCooldownE();
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
                hud.TriggerMayaCooldownE();
            }
        }
        eSkillCooldownTimer = eSkillCooldown;
    }

    // Q Skill
    bool IPlayerHUDTarget.IsQSkillActive => IsQSkillActive;
    public float QSkillTimeRemaining => qSkillDurationTimer;
    
    public bool TriggerQSkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return false;

        if (PlayerLevel < 15 && !IsSkillsUnlocked) return false;
        if (GetActiveWeaponIndex() == 1)
        {
            Debug.Log("[MayaPlayer] Không thể sử dụng kỹ năng Q khi đang cầm rìu!");
            return false;
        }
        if (qSkillCooldownTimer > 0f || IsQSkillActive) return false;

        // Set duration timer for HUD visual bar (15s duration)
        qSkillDurationTimer = 15f;

        if (isStandaloneMode)
        {
            localIsQSkillActive = true;
            SpawnSkeletonLocal();
        }
        else if (IsOwner)
        {
            SpawnSkeletonServerRpc();
        }

        return true;
    }

    private void SpawnSkeletonLocal()
    {
        if (qSkillSkeletonPrefab == null)
        {
            Debug.LogWarning("[MayaPlayer] qSkillSkeletonPrefab chưa được gán!");
            return;
        }

        Vector3 spawnPos = transform.position + transform.forward * 2f;
        spawnPos.y = transform.position.y;
        Quaternion spawnRot = Quaternion.LookRotation(transform.forward);

        GameObject skeleton = Instantiate(qSkillSkeletonPrefab, spawnPos, spawnRot);
        skeleton.SetActive(true);

        StartCoroutine(DespawnSkeletonAfterTime(skeleton, 15f));
    }

    [ServerRpc]
    private void SpawnSkeletonServerRpc()
    {
        if (qSkillSkeletonPrefab == null)
        {
            Debug.LogWarning("[MayaPlayer Server] qSkillSkeletonPrefab chưa được gán trên Server!");
            return;
        }

        isQSkillActiveNet.Value = true;

        Vector3 spawnPos = transform.position + transform.forward * 2f;
        spawnPos.y = transform.position.y;
        Quaternion spawnRot = Quaternion.LookRotation(transform.forward);

        GameObject skeleton = Instantiate(qSkillSkeletonPrefab, spawnPos, spawnRot);
        skeleton.SetActive(true);

        if (skeleton.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn();
        }

        StartCoroutine(DespawnSkeletonAfterTime(skeleton, 15f));
    }

    private System.Collections.IEnumerator DespawnSkeletonAfterTime(GameObject skeleton, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isStandaloneMode)
        {
            if (skeleton != null)
            {
                Destroy(skeleton);
            }
            localIsQSkillActive = false;
            StartQSkillCooldown();
        }
        else if (IsServer)
        {
            if (skeleton != null)
            {
                if (skeleton.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(skeleton);
                }
            }
            isQSkillActiveNet.Value = false;
            StartQSkillCooldown();
        }
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
                hud.TriggerMayaCooldownQ();
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
                hud.TriggerMayaCooldownQ();
            }
        }
        qSkillCooldownTimer = qSkillCooldown;
    }

    public void TriggerRSkill()
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        if (PlayerLevel < 5 && !IsSkillsUnlocked) return;

        if (GetActiveWeaponIndex() != 0) return;

        SetAimingR(true);
    }

    private void SetAimingR(bool aiming)
    {
        if (localIsAimingR == aiming) return;
        localIsAimingR = aiming;
        
        OnAimRStateChanged(localIsAimingR);
        if (!isStandaloneMode && IsOwner)
        {
            SetAimingRServerRpc(localIsAimingR);
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

            if (!Input.GetKey(KeyCode.R) && !isRShootPending)
            {
                SetAimingR(false);
            }
        }
    }

    public event System.Action OnQSkillCancelled;


    public bool IsHoldingAxe()
    {
        return GetComponentInChildren<AxeItem>(true) != null;
    }

    public int GetActiveWeaponIndex()
    {
        int val = isStandaloneMode ? localActiveWeaponIndex : activeWeaponIndex.Value;
        if (val == 1 && !IsHoldingAxe())
        {
            return 0;
        }
        return val;
    }

    protected virtual void Awake()
    {
        maxHealth = 110f;
        localHealth = 110f;
        currentAnimState = "Idle";
        lastTriggeredAnimName = "Idle";
        isDeathAnimFinished = false;

        // Ép tên Trigger luôn đúng với Animator tiếng Việt của Maya, bỏ qua giá trị cũ bị lưu ở Inspector
        drawWeaponTrigger = "LayVuKhi";
        sheathWeaponTrigger = "CatVuKhi";

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Mặc định tắt Kinematic để di chuyển được ở chế độ Standalone/Offline
            // Khóa xoay trục X, Y và Z để tránh nhân vật bị đổ hoặc xoay tròn nghiêng ngả khi va chạm vật lý
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
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
        InitializeAudio();
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
        if (isStandaloneMode || IsOwner)
        {
            PlayerDeathEffectManager.Instance.ResetDeathEffect();
        }
    }

    /// <summary>
    /// Khởi tạo tất cả chức năng khi chơi đơn lẻ trong Editor mà không cần Host/Server.
    /// </summary>
    private void InitStandaloneMode()
    {
        Debug.Log("[MayaPlayer] Chạy ở chế độ STANDALONE (không có NetworkManager). " +
                  "Di chuyển và tấn công hoạt động cục bộ.");
        LockCursor(isCursorLocked);

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Tắt Kinematic để di chuyển trong chế độ chơi đơn lẻ
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        // Tìm camera
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        // Tải nhân vật đã lưu từ PlayerPrefs nếu có
        characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);
        playerName.Value = PlayerPrefs.GetString("AuthDisplayName", "Maya");
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
        localHealth = currentHealth.Value > 0f ? currentHealth.Value : maxHealth;
        currentAnimState = "Idle";
        lastTriggeredAnimName = "Idle";
        isDeathAnimFinished = false;

        var rb = GetComponent<Rigidbody>();
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
        isAimingRNet.OnValueChanged += OnAimingRNetChanged;

        if (IsOwner)
        {
            // Tải nhân vật đã lưu từ PlayerPrefs
            characterClassIndex = PlayerPrefs.GetInt("SelectedCharacterId", characterClassIndex);

            // Đồng bộ tên người chơi qua mạng
            string myName = PlayerPrefs.GetString("AuthDisplayName", "Maya");
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
            LockCursor(isCursorLocked);
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
        isAimingRNet.OnValueChanged -= OnAimingRNetChanged;

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
        localHealth = newHealth;
        UpdateHealthHUD(newHealth);

        if (newHealth <= 0f)
        {
            targetMoveVelocity = Vector3.zero;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                rb.angularVelocity = Vector3.zero;
            }

            if (currentAnimState != "Death")
            {
                PlayAnimation("Death", 0.15f);
            }
        }
        else if (newHealth > 0f && oldHealth <= 0f)
        {
            PlayerDeathEffectManager.Instance.ResetDeathEffect();
            ResetDeathState();
        }

        if (IsOwner)
        {
            SavePlayerStateToDatabase();
        }
    }

    private void OnHealthChangedShared(float oldHealth, float newHealth)
    {
        localHealth = newHealth;

        if (newHealth < oldHealth)
        {
            var flash = GetComponent<MaterialFlashBehaviour>();
            if (flash == null) flash = gameObject.AddComponent<MaterialFlashBehaviour>();
            flash.Flash(Color.red, 0.15f);
        }

        if (newHealth <= 0f)
        {
            targetMoveVelocity = Vector3.zero;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                rb.angularVelocity = Vector3.zero;
            }

            if (currentAnimState != "Death")
            {
                PlayAnimation("Death", 0.15f);
            }
        }
        else if (newHealth > 0f && oldHealth <= 0f)
        {
            ResetDeathState();
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
        damageAmount = 20f * (1f + dmgLv * 0.15f);

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
        if (respawnImmunityTimer > 0f)
        {
            respawnImmunityTimer -= Time.deltaTime;
        }

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
            HandleRAiming();
            int currentWeaponIdx = GetActiveWeaponIndex();

            // Vào chế độ ngắm E khi giữ phím E, có cầm vũ khí (cung) và skill E không ở trạng thái cooldown
            bool targetETargeting = currentWeaponIdx == 2 && eSkillCooldownTimer <= 0f && Input.GetKey(KeyCode.E) && !IsUIBlockingInput() && !IsBusyOrRolling;
            if (isETargeting != targetETargeting)
            {
                isETargeting = targetETargeting;
            }

            UpdateETargetingIndicator();

            bool targetAiming = (currentWeaponIdx == 2 && Input.GetMouseButton(1) && !IsUIBlockingInput() && !IsBusyOrRolling) || isETargeting || isShootPending;
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
        if (ShootingCooldownTimer > 0)
        {
            ShootingCooldownTimer -= Time.deltaTime;
        }

        // Giảm thời gian cooldown Kỹ năng E
        if (eSkillCooldownTimer > 0)
        {
            eSkillCooldownTimer -= Time.deltaTime;
        }

        // Giảm thời gian tác dụng Kỹ năng E
        if (eSkillActiveTimer > 0)
        {
            eSkillActiveTimer -= Time.deltaTime;
            if (eSkillActiveTimer <= 0)
            {
                eSkillActiveTimer = 0f;
            }
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
            if (rb != null) rb.linearVelocity = Vector3.zero;
            if (currentAnimState != "Death")
            {
                PlayAnimation("Death", 0.15f);
            }
            return;
        }

        // Tự động tắt weight của Layer 1 và đưa về New State khi kết thúc cất/lấy vũ khí
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            bool isPushing = false;
            try { isPushing = anim.GetBool("IsPushing"); } catch (System.Exception) {}

            if (isPushing)
            {
                if (anim.GetLayerWeight(1) < 0.99f) anim.SetLayerWeight(1, 1f);
            }
            else
            {
                float weight1 = anim.GetLayerWeight(1);
                if (weight1 > 0f && comboStep == 0 && !IsAiming)
                {
                    var carrier = GetComponent<PlayerLogCarrier>();
                    bool isCarrying = carrier != null && carrier.isCarrying;
                    if (!isCarrying)
                    {
                        bool isSwitching = IsStatePlayingOnLayer1(drawWeaponTrigger) || IsStatePlayingOnLayer1(sheathWeaponTrigger);
                        if (!isSwitching && !IsPlayingAttackState(out _, out _))
                        {
                            ClearAttackLayer();
                        }
                    }
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
                    // Debug.Log($"[SpineAim_Update] transform.forward={transform.forward}, camForward={camForward}, angleDiff={angleDiff}, localAim={localAimAngle}, netAim={netAimAngle.Value}");
                }
            }
            else
            {
                if (isCurrentlyAttacking)
                {
                    // Debug.Log($"[SpineAim_Update_Failed] isRooted={isRootedAttack}, cam={targetCamera != null}");
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

        // Cập nhật tham số hoạt ảnh di chuyển cho local client (chủ sở hữu hoặc chơi đơn) hoặc đồng bộ từ mạng
        if (hasControl)
        {
            UpdateAnimatorParameters();
        }
        else
        {
            if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
            {
                anim.SetFloat("MoveX", netMoveX.Value);
                anim.SetFloat("MoveZ", netMoveZ.Value);
                bool hasWeapon = GetActiveWeaponIndex() == 2;
                anim.SetBool("HasWeapon", hasWeapon);
            }
        }

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
                               (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive) ||
                               (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive) ||
                               SeagullController.ActiveSeagull != null;
        
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

        if (!isStandaloneMode && IsOwner)
        {
            netMoveX.Value = animMoveX;
            netMoveZ.Value = animMoveZ;
        }
    }

    private void HandleStandaloneUpdate()
    {
        if (CurrentHealth <= 0)
        {
            targetMoveVelocity = Vector3.zero;
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            return;
        }
    bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                           (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive) ||
                           (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive) ||
                           PlayerHUDController.isCoopBuildingUIOpen ||
                           SeagullController.ActiveSeagull != null;

    if (isDialogueOpen)
    {
        if (!IsPlayingActionAnimation()) PlayAnimation("Idle", 0.1f);
        targetMoveVelocity = Vector3.zero;
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
            targetMoveVelocity = vel;
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

        // Knockback (Đã chuyển đổi sang tính toán cùng vận tốc di chuyển ở dưới để chạy bằng Rigidbody)
        Vector3 currentKnockback = Vector3.zero;
        if (knockbackVelocity.magnitude > 0.01f)
        {
            currentKnockback = knockbackVelocity;
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
        if (IsLockingMovementAction())
        {
            movementTranslation = Vector3.zero;
        }

        Vector3 finalVelocity = movementTranslation * currentSpeed + currentKnockback;

        targetMoveVelocity = finalVelocity;

        if (rb == null)
        {
            transform.Translate(finalVelocity * Time.deltaTime, Space.World);
        }

        // Xoay nhân vật chuẩn Genshin Impact: Xoay mặt về hướng di chuyển khi chạy, xoay theo Camera khi ngắm/bắn
        bool isHitState = IsPlayingHitAnimation();
        bool isMoving = (movementTranslation != Vector3.zero);
        if (targetCamera != null && (!IsPlayingActionAnimation() || isHitState) && !IsLockingMovementAction())
        {
            Vector3 turnDir = Vector3.zero;
            if (IsAiming || IsPlayingAttackState(out _, out _))
            {
                turnDir = targetCamera.transform.forward;
            }
            else if (isMoving)
            {
                turnDir = movementTranslation;
            }

            turnDir.y = 0f;
            turnDir.Normalize();
            if (turnDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(turnDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 15f);
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

        // Kích hoạt kỹ năng R
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!IsUIBlockingInput())
            {
                TriggerRSkill();
            }
        }

        // Tấn công đơn lẻ
        if (Input.GetMouseButtonDown(0))
        {
            if (!IsUIBlockingInput())
            {
                if (localIsAimingR)
                {
                    isRShootPending = true;
                    isPendingRShootNetworkMode = false;
                    PlayAnimation("Shooting", 0.05f);
                    SetAimingR(false);

                    PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                    if (hud != null)
                    {
                        hud.TriggerCooldownR();
                    }
                }
                else if (isETargeting)
                {
                    CastEHealingZone(false);
                }

                else if (IsAiming)
                {
                    if (ShootingCooldownTimer <= 0f)
                    {
                        ShootingCooldownTimer = ShootingCooldown;
                        PerformShooting(false);
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
                           (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive) ||
                           (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive) ||
                           PlayerHUDController.isCoopBuildingUIOpen ||
                           SeagullController.ActiveSeagull != null;

    if (isDialogueOpen)
    {
        if (!IsPlayingActionAnimation()) PlayAnimation("Idle", 0.1f);
        targetMoveVelocity = Vector3.zero;
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
            targetMoveVelocity = vel;
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

        // Knockback (Đã chuyển đổi sang tính toán cùng vận tốc di chuyển ở dưới để chạy bằng Rigidbody)
        Vector3 currentKnockback = Vector3.zero;
        if (knockbackVelocity.magnitude > 0.01f)
        {
            currentKnockback = knockbackVelocity;
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
        if (IsLockingMovementAction())
        {
            movementTranslation = Vector3.zero;
        }

        Vector3 finalVelocity = movementTranslation * currentSpeed + currentKnockback;

        targetMoveVelocity = finalVelocity;

        if (rb == null)
        {
            transform.Translate(finalVelocity * Time.deltaTime, Space.World);
        }

        // Xoay nhân vật chuẩn Genshin Impact: Xoay mặt về hướng di chuyển khi chạy, xoay theo Camera khi ngắm/bắn
        bool isHitState = IsPlayingHitAnimation();
        bool isMoving = (movementTranslation != Vector3.zero);
        if (targetCamera != null && (!IsPlayingActionAnimation() || isHitState) && !IsLockingMovementAction())
        {
            Vector3 turnDir = Vector3.zero;
            if (IsAiming || IsPlayingAttackState(out _, out _))
            {
                turnDir = targetCamera.transform.forward;
            }
            else if (isMoving)
            {
                turnDir = movementTranslation;
            }

            turnDir.y = 0f;
            turnDir.Normalize();
            if (turnDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(turnDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 15f);
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

        // Kích hoạt kỹ năng R
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (IsSpawned && !IsUIBlockingInput())
            {
                TriggerRSkill();
            }
        }

        // Tấn công qua RPC (chỉ khi đã spawn trên mạng)
        if (Input.GetMouseButtonDown(0))
        {
            if (IsSpawned && !IsUIBlockingInput())
            {
                if (localIsAimingR)
                {
                    isRShootPending = true;
                    isPendingRShootNetworkMode = !isStandaloneMode;
                    PlayAnimation("Shooting", 0.05f);
                    SetAimingR(false);

                    PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                    if (hud != null)
                    {
                        hud.TriggerCooldownR();
                    }
                }
                else if (isETargeting)
                {
                    CastEHealingZone(true);
                }

                else if (IsAiming)
                {
                    if (ShootingCooldownTimer <= 0f)
                    {
                        ShootingCooldownTimer = ShootingCooldown;
                        PerformShooting(true);
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
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

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
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

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
        Debug.Log("[MayaPlayer] OnRollEnd called.");
        isRollingStandalone = false;
        rollTimer = 0f;

        if (anim != null) anim.applyRootMotion = false;
        var bridge = GetRootMotionBridge();
        if (bridge != null) bridge.ApplyFinalOffset();

        targetMoveVelocity = Vector3.zero;
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        if (!isStandaloneMode && IsOwner)
        {
            StopRollServerRpc();
        }
    }

    void FixedUpdate()
    {
        if (CurrentHealth <= 0)
        {
            targetMoveVelocity = Vector3.zero;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                rb.angularVelocity = Vector3.zero;
            }
            return;
        }

        if (rb != null && !rb.isKinematic)
        {
            Vector3 extraGravityForce = Physics.gravity * (gravityMultiplier - 1f);
            rb.AddForce(extraGravityForce, ForceMode.Acceleration);

            // Cập nhật vận tốc di chuyển vật lý của nhân vật một cách mượt mà và đồng bộ trong FixedUpdate
            Vector3 vel = targetMoveVelocity;
            vel.y = rb.linearVelocity.y; // Giữ nguyên trọng lực vật lý
            rb.linearVelocity = vel;
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

    private float GetAnimationClipLength(string triggerName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return 0f;
        string searchName = triggerName;
        if (triggerName == "ChatRiu") searchName = "Chat Cayy";
        foreach (var clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip != null && (clip.name == searchName || clip.name.ToLower() == searchName.ToLower() || clip.name.ToLower().Contains(searchName.ToLower())))
            {
                return clip.length;
            }
        }
        return 0f;
    }

    private float GetAttackDuration(int weaponIndex, int step)
    {
        if (weaponIndex == 1)
        {
            float duration = GetAnimationClipLength("ChatRiu");
            if (duration > 0f) return duration;
            return 2.267f;
        }
        else if (weaponIndex == 2)
        {
            if (step == 1) return chem1Duration;
            if (step == 2) return chem2Duration;
            return chem3Duration;
        }
        else
        {
            if (step == 1) return punch1Duration;
            if (step == 2) return punch2Duration;
            return punch3Duration;
        }
    }

    private void PerformComboAttack(bool networkMode)
    {
        var carrier = GetComponent<PlayerLogCarrier>();
        if (carrier != null && carrier.isCarrying) return;

        int weapon = GetActiveWeaponIndex();
        if (!networkMode)
        {
            if (weapon == 1) Weapon1Durability = Mathf.Max(Weapon1Durability - 2f, 0f);
            else Weapon2Durability = Mathf.Max(Weapon2Durability - 2f, 0f);
        }
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
            nextStep = 1;
        }
        else if (weapon == 2)
        {
            if (nextStep > 3) nextStep = 1;
        }
        else // weapon == 0 (Unarmed)
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
            isRootedAttack = !isMovingInput; // Đứng yên đánh thì khóa di chuyển vật lý (rooted), di chuyển/chạy đánh thì không khóa di chuyển (unrooted)
        }

        comboStep = nextStep;
        lastAttackTime = currentTime;

        string animToPlay = "";
        if (weapon == 1) // Axe / ChatRiu
        {
            animToPlay = "ChatRiu";
        }
        else if (weapon == 2) // Weapon / Chem combo (3 steps)
        {
            if (comboStep == 1) animToPlay = "Chem1";
            else if (comboStep == 2) animToPlay = "Chem2";
            else if (comboStep == 3) animToPlay = "Chem3";
        }
        else // Unarmed / Fist combo (3 steps)
        {
            if (comboStep == 1) animToPlay = "Dam1";
            else if (comboStep == 2) animToPlay = "Dam2";
            else if (comboStep == 3) animToPlay = "Dam3";
        }

        Debug.Log($"[Combo Debug] PerformComboAttack: weapon={weapon}, step={comboStep}, animToPlay={animToPlay}, isRooted={isRootedAttack}, isMovingInput={isMovingInput}");

        if (!string.IsNullOrEmpty(animToPlay))
        {
            PlayAnimation(animToPlay, 0.05f, false, isRootedAttack);
        }

        alreadyHitEnemies.Clear();
        PerformMeleeRaycastAttack();
        StartCoroutine(DelayedRaycastAttackCoroutine(0.15f));
    }

    private System.Collections.IEnumerator DelayedRaycastAttackCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        PerformMeleeRaycastAttack();
    }

    public void PerformMeleeRaycastAttack()
    {
        bool hasControl = isStandaloneMode || !IsSpawned || IsOwner || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        if (!hasControl) return;

        Vector3 aimDir = transform.forward;
        if (targetCamera != null)
        {
            aimDir = targetCamera.transform.forward;
            aimDir.y = 0f;
            aimDir.Normalize();
        }

        Vector3 rayStartClient = transform.position + Vector3.up * 0.8f;
        float range = Mathf.Max(attackRange, 3.0f);
        RaycastHit[] hits = Physics.SphereCastAll(rayStartClient, 1.2f, aimDir, range);

        foreach (var hitClient in hits)
        {
            if (hitClient.collider == null || hitClient.collider.transform.root == transform.root) continue;

            // Kiểm tra xem đòn chém có bị tường/vật cản che chắn không (Line of Sight)
            Vector3 targetCenter = hitClient.collider.bounds.center;
            Vector3 dirToTarget = targetCenter - rayStartClient;
            float distToTarget = dirToTarget.magnitude;

            if (distToTarget > 0.1f)
            {
                RaycastHit[] losHits = Physics.RaycastAll(rayStartClient, dirToTarget.normalized, distToTarget);
                bool blockedByWall = false;
                foreach (var losHit in losHits)
                {
                    if (losHit.collider == null) continue;
                    if (losHit.collider.transform.root == transform.root) continue;
                    if (losHit.collider == hitClient.collider || losHit.collider.transform.IsChildOf(hitClient.collider.transform) || hitClient.collider.transform.IsChildOf(losHit.collider.transform)) continue;

                    if (!losHit.collider.isTrigger && !IsEnemy(losHit.collider, out _) && losHit.collider.GetComponentInParent<ChoppableTree>() == null)
                    {
                        blockedByWall = true;
                        break;
                    }
                }
                if (blockedByWall) continue; // Đòn chém bị tường chặn!
            }

            Transform root = hitClient.collider.transform.root;
            if (alreadyHitEnemies.Contains(root)) continue;

            if (IsEnemy(hitClient.collider, out Collider enemyCollider))
            {
                alreadyHitEnemies.Add(root);
                TryDamageEnemy(enemyCollider);

                var netObj = enemyCollider.transform.root.GetComponent<NetworkObject>() ?? enemyCollider.GetComponentInParent<NetworkObject>() ?? enemyCollider.GetComponentInChildren<NetworkObject>();

                if (!isStandaloneMode && IsSpawned && netObj != null && !IsServer)
                {
                    DamageEnemyServerRpc(netObj);
                }
            }
            else
            {
                alreadyHitEnemies.Add(root);
                // Chém cây gỗ (ChoppableTree) cho Maya
                ChoppableTree tree = hitClient.collider.GetComponentInParent<ChoppableTree>() ?? hitClient.collider.transform.root.GetComponentInChildren<ChoppableTree>();
                if (tree == null)
                {
                    var forwarder = hitClient.collider.GetComponent<TreeColliderForwarder>();
                    if (forwarder != null) tree = forwarder.mainTree;
                }
                if (tree != null)
                {
                    Vector3 hitPos = hitClient.point;
                    int weaponIndex = GetActiveWeaponIndex();
                    tree.HitTree(hitPos, weaponIndex);
                }
            }
        }
    }

    public void EnableLeftHitbox() { PerformMeleeRaycastAttack(); }
    public void DisableLeftHitbox() {}
    public void EnableRightHitbox() { PerformMeleeRaycastAttack(); }
    public void DisableRightHitbox() {}
    public void EnableBothHitboxes() { PerformMeleeRaycastAttack(); }
    public void DisableBothHitboxes() {}
    public void EnableLeftWeaponHitbox() { PerformMeleeRaycastAttack(); }
    public void DisableLeftWeaponHitbox() {}
    public void EnableRightWeaponHitbox() { PerformMeleeRaycastAttack(); }
    public void DisableRightWeaponHitbox() {}
    public void EnableBothWeaponHitboxes() { PerformMeleeRaycastAttack(); }
    public void DisableBothWeaponHitboxes() {}

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
        if (col == null) return;
        float actualDamage = damageAmount;
        Debug.Log($"[MayaPlayer] TryDamageEnemy: col='{col.name}', parent='{col.transform.parent?.name}', damage={actualDamage}");

        var e1 = col.GetComponentInParent<Enemy1_DapBua>() ?? col.GetComponentInChildren<Enemy1_DapBua>();
        if (e1 != null) { Debug.Log($"[MayaPlayer] Found Enemy1_DapBua on {e1.name}"); e1.TakeDamage(actualDamage); return; }

        var e2 = col.GetComponentInParent<Enemy2_Zombie>() ?? col.GetComponentInChildren<Enemy2_Zombie>();
        if (e2 != null) { Debug.Log($"[MayaPlayer] Found Enemy2_Zombie on {e2.name}"); e2.TakeDamage(actualDamage); return; }

        var e3 = col.GetComponentInParent<Enemy3_Buaa>() ?? col.GetComponentInChildren<Enemy3_Buaa>();
        if (e3 != null) { Debug.Log($"[MayaPlayer] Found Enemy3_Buaa on {e3.name}"); e3.TakeDamage(actualDamage); return; }

        var e4 = col.GetComponentInParent<Enemy4_Bongtoi>() ?? col.GetComponentInChildren<Enemy4_Bongtoi>();
        if (e4 != null) { Debug.Log($"[MayaPlayer] Found Enemy4_Bongtoi on {e4.name}"); e4.TakeDamage(actualDamage); return; }

        var e5 = col.GetComponentInParent<Enemy5_PhuThuy>() ?? col.GetComponentInChildren<Enemy5_PhuThuy>();
        if (e5 != null) { Debug.Log($"[MayaPlayer] Found Enemy5_PhuThuy on {e5.name}"); e5.TakeDamage(actualDamage); return; }

        var mb = col.GetComponentInParent<MiniBossAI>() ?? col.GetComponentInChildren<MiniBossAI>();
        if (mb != null) { Debug.Log($"[MayaPlayer] Found MiniBossAI on {mb.name}"); mb.TakeDamage(actualDamage); return; }

        var fb = col.GetComponentInParent<FinalBossAI>() ?? col.GetComponentInChildren<FinalBossAI>();
        if (fb != null) { Debug.Log($"[MayaPlayer] Found FinalBossAI on {fb.name}"); fb.TakeDamage(actualDamage); return; }

        var b = col.GetComponentInParent<BossAI>() ?? col.GetComponentInChildren<BossAI>();
        if (b != null) { Debug.Log($"[MayaPlayer] Found BossAI on {b.name}"); b.TakeDamage(actualDamage); return; }

        Debug.LogWarning($"[MayaPlayer] TryDamageEnemy: No enemy AI script found on collider {col.name} or its hierarchy!");
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

    // ------------------------------------------------------------------
    //  Server RPC Tấn công (chỉ dùng khi Netcode online)
    // ------------------------------------------------------------------
    [ServerRpc]
    void AttackServerRpc(Vector3 aimDir)
    {
        // Trừ độ bền vũ khí trên Server (dù trúng hay trượt)
        int weapon = GetActiveWeaponIndex();
        if (weapon == 1) weapon1Durability.Value = Mathf.Max(weapon1Durability.Value - 2f, 0f);
        else weapon2Durability.Value = Mathf.Max(weapon2Durability.Value - 2f, 0f);

        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Debug.DrawRay(rayStart, aimDir * attackRange, Color.red, 0.5f);

        if (!Physics.Raycast(rayStart, aimDir, out RaycastHit hit, attackRange)) return;

        // Chém cây gỗ (ChoppableTree) cho Maya trên Server
        ChoppableTree tree = hit.collider.GetComponentInParent<ChoppableTree>();
        if (tree == null)
        {
            var forwarder = hit.collider.GetComponent<TreeColliderForwarder>();
            if (forwarder != null) tree = forwarder.mainTree;
        }
        if (tree != null)
        {
            Vector3 hitPos = hit.point;
            int weaponIndex = GetActiveWeaponIndex();
            tree.HitTree(hitPos, weaponIndex);
        }
    }

    [ServerRpc]
    private void DamageEnemyServerRpc(NetworkObjectReference enemyRef)
    {
        Debug.Log($"[MayaPlayer Server] DamageEnemyServerRpc called by client {OwnerClientId}");
        if (enemyRef.TryGet(out NetworkObject netObj))
        {
            Debug.Log($"[MayaPlayer Server] Successfully retrieved NetworkObject '{netObj.name}', ID: {netObj.NetworkObjectId}");
            var col = netObj.GetComponent<Collider>() ?? netObj.GetComponentInChildren<Collider>();
            if (col != null)
            {
                Debug.Log($"[MayaPlayer Server] Found enemy collider: '{col.name}'. Calling TryDamageEnemy.");
                TryDamageEnemy(col);
            }
            else
            {
                float actualDamage = damageAmount;
                Debug.Log($"[MayaPlayer Server] No collider found on NetworkObject '{netObj.name}'. Direct component check with damage={actualDamage}");
                var e1 = netObj.GetComponent<Enemy1_DapBua>() ?? netObj.GetComponentInChildren<Enemy1_DapBua>();
                if (e1 != null) { Debug.Log("[MayaPlayer Server] Direct e1 hit"); e1.TakeDamage(actualDamage); return; }
                var e2 = netObj.GetComponent<Enemy2_Zombie>() ?? netObj.GetComponentInChildren<Enemy2_Zombie>();
                if (e2 != null) { Debug.Log("[MayaPlayer Server] Direct e2 hit"); e2.TakeDamage(actualDamage); return; }
                var e3 = netObj.GetComponent<Enemy3_Buaa>() ?? netObj.GetComponentInChildren<Enemy3_Buaa>();
                if (e3 != null) { Debug.Log("[MayaPlayer Server] Direct e3 hit"); e3.TakeDamage(actualDamage); return; }
                var e4 = netObj.GetComponent<Enemy4_Bongtoi>() ?? netObj.GetComponentInChildren<Enemy4_Bongtoi>();
                if (e4 != null) { Debug.Log("[MayaPlayer Server] Direct e4 hit"); e4.TakeDamage(actualDamage); return; }
                var e5 = netObj.GetComponent<Enemy5_PhuThuy>() ?? netObj.GetComponentInChildren<Enemy5_PhuThuy>();
                if (e5 != null) { Debug.Log("[MayaPlayer Server] Direct e5 hit"); e5.TakeDamage(actualDamage); return; }
                var mb = netObj.GetComponent<MiniBossAI>() ?? netObj.GetComponentInChildren<MiniBossAI>();
                if (mb != null) { Debug.Log("[MayaPlayer Server] Direct mb hit"); mb.TakeDamage(actualDamage); return; }
                var fb = netObj.GetComponent<FinalBossAI>() ?? netObj.GetComponentInChildren<FinalBossAI>();
                if (fb != null) { Debug.Log("[MayaPlayer Server] Direct fb hit"); fb.TakeDamage(actualDamage); return; }
                var b = netObj.GetComponent<BossAI>() ?? netObj.GetComponentInChildren<BossAI>();
                if (b != null) { Debug.Log("[MayaPlayer Server] Direct b hit"); b.TakeDamage(actualDamage); return; }
                Debug.LogWarning("[MayaPlayer Server] Direct check: No enemy AI components found!");
            }
        }
        else
        {
            Debug.LogError("[MayaPlayer Server] Failed to retrieve NetworkObject from NetworkObjectReference!");
        }
    }

    public void TakeDamage(float damage)
    {
        // Nếu vừa hồi sinh ở Checkpoint hoặc đã chết, không nhận thêm sát thương
        if (respawnImmunityTimer > 0f || CurrentHealth <= 0 || localHealth <= 0f) return;

        // Né chiêu (miễn nhiễm sát thương khi đang lộn vòng)
        if (isStandaloneMode)
        {
            if (isRollingStandalone)
            {
                Debug.Log($"[MayaPlayer] {gameObject.name} đang né chiêu (lộn vòng), miễn nhiễm sát thương!");
                return;
            }
        }
        else
        {
            if (isRollingNet.Value)
            {
                Debug.Log($"[MayaPlayer] {gameObject.name} đang né chiêu (lộn vòng), miễn nhiễm sát thương!");
                return;
            }
        }

        if (isStandaloneMode)
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            UpdateHealthHUD(localHealth);
            Debug.Log($"[MayaPlayer] {gameObject.name} nhận {damage} sát thương. Máu còn: {localHealth}");

            var flash = GetComponent<MaterialFlashBehaviour>();
            if (flash == null) flash = gameObject.AddComponent<MaterialFlashBehaviour>();
            flash.Flash(Color.red, 0.15f);

            if (localHealth <= 0)
            {
                targetMoveVelocity = Vector3.zero;
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                Debug.LogWarning($"[MayaPlayer] {gameObject.name} đã chết!");
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
        Debug.Log($"[MayaPlayer] {gameObject.name} nhận {damage} sát thương. Máu còn lại: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
        {
            targetMoveVelocity = Vector3.zero;
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Debug.LogWarning($"[MayaPlayer] {gameObject.name} đã chết!");
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
            Debug.Log($"[MayaPlayer Standalone] Hồi {amount} máu. Máu hiện tại: {localHealth}");
        }
        else if (IsServer)
        {
            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
            Debug.Log($"[MayaPlayer Server] Hồi {amount} máu cho {gameObject.name}. Máu hiện tại: {currentHealth.Value}");
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
                    false, // Khóa vũ khí 2 mặc định khi bắt đầu game
                    true, // Khóa kỹ năng mặc định khi bắt đầu game
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
                    hud.SetSkillsUnlocked(true, false); // Khóa kỹ năng mặc định
                    hud.SetWeapon2Locked(false, false); // Khóa vũ khí 2 mặc định
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
                SyncPlayerStateServerRpc(maxHealth, 1, false, true, 0, 0, 0, 0, 0, 0, 0f);
                SavePlayerStateToDatabase();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Lỗi khi kết nối API tải dữ liệu MongoDB: {ex.Message}");
            SyncPlayerStateServerRpc(maxHealth, 1, false, true, 0, 0, 0, 0, 0, 0, 0f);
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
               name == "ChatRiu" ||
               name == "Shooting" ||
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
               name == "Chem3" ||
               name == "ChatRiu";
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
               stateInfo.IsName("Chem_3") ||
               stateInfo.IsName("ChatRiu");
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
        if (CurrentHealth <= 0) return true;
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

        // Khóa di chuyển khi đang nhặt đồ
        bool isPicking = (lastTriggeredAnimName == "Idle_Pick" || lastTriggeredAnimName == "Pick") && (Time.time - lastActionTriggerTime < 1.2f);

        // Các trạng thái toàn thân đặc biệt cần khóa di chuyển (trúng đòn, nhặt đồ, chết, nhào lộn)
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool isFullBodyAction = stateInfo.IsName("LonVong") || 
                               stateInfo.IsName("GetHit") || 
                               stateInfo.IsName("GeiHit2") || 
                               stateInfo.IsName("Idle_Pick") || 
                               stateInfo.IsName("Death") ||
                               isPicking;

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

        return isFullBodyAction && (isPicking || stateInfo.normalizedTime < 0.95f);
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

        return isFullBodyAction && stateInfo.normalizedTime < 0.95f;
    }

    private bool IsPlayingHitAnimation()
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool isHitState = stateInfo.IsName("GetHit") || stateInfo.IsName("GeiHit2");
        if (!isHitState && anim.layerCount > 1)
        {
            AnimatorStateInfo stateInfoLayer1 = anim.GetCurrentAnimatorStateInfo(1);
            isHitState = stateInfoLayer1.IsName("GetHit") || stateInfoLayer1.IsName("GeiHit2");
        }
        return isHitState && stateInfo.normalizedTime < 0.95f;
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
        var netAnim = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (netAnim != null)
        {
            try { netAnim.ResetTrigger(paramName); } catch {}
        }
    }

    private System.Collections.IEnumerator ResetTriggerNextFrame(string triggerName)
    {
        yield return null;
        if (anim != null && !string.IsNullOrEmpty(triggerName))
        {
            SafeResetTrigger(triggerName);
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

        // Không phát hoạt ảnh Death nếu người chơi đang sống (máu > 0)
        if (animName == "Death" && CurrentHealth > 0)
        {
            return;
        }

        // Nếu đang chết, từ chối tất cả các lệnh hoạt ảnh ngoại trừ "Death", "New State" hoặc "Empty"
        if (currentAnimState == "Death" || localHealth <= 0f || CurrentHealth <= 0f)
        {
            if (CurrentHealth <= 0f || localHealth <= 0f)
            {
                if (animName != "Death" && animName != "New State" && animName != "Empty")
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
        if (animName == "Death" && (localHealth > 0f || CurrentHealth > 0f))
        {
            return;
        }

        // Play action sound effects
        string animLower = animName.ToLower();
        bool isSlash = animLower.Contains("attack") || animLower.Contains("slash") || animLower.Contains("chem") || animLower.Contains("chatriu") || animName == "Shooting";
        bool isPunch = animLower.Contains("punch") || animLower.Contains("dam");

        if (animName == "Shooting")
        {
            PlayPlayerSFX(skillRClip); // Water projectile
        }
        else if (isSlash && !isPunch)
        {
            AudioClip swingClip = Resources.Load<AudioClip>("Audio/ChemChuaHit");
            PlayPlayerSFX(swingClip, 0.8f);
        }
        else if (isPunch)
        {
            AudioClip punchClip = Resources.Load<AudioClip>("Audio/Punch");
            PlayPlayerSFX(punchClip);
        }
        else if (animName == "Death")
        {
            PlayPlayerSFX(deathClip);
        }
        else if (animName == "GetHit" || animName == "GeiHit2")
        {
            PlayPlayerSFX(hitClip);
        }
        else if (animName == "LonVong")
        {
            PlayPlayerSFX(Resources.Load<AudioClip>("Audio/SmokeBomb"), 0.5f); // Roll Whoosh
        }

        this.isRootedAttack = isRooted;
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

        if (animName == "LonVong" || animName == "GetHit" || animName == "GeiHit2" || animName.Contains("Hit"))
        {
            anim.applyRootMotion = false;
        }

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
        SafeResetTrigger("Shooting");

        // Chỉ dọn dẹp (reset) các trigger combo tấn công khi chuẩn bị kích hoạt một hành động mới
        // (để tránh việc nhân vật di chuyển làm reset mất trigger đòn đấm trên layer Upper Body)
        if (IsActionAnimationName(animName))
        {
            SafeResetTrigger("Dam1");
            SafeResetTrigger("Dam2");
            SafeResetTrigger("Dam3");
            SafeResetTrigger("Shooting");
            if (!string.IsNullOrEmpty(drawWeaponTrigger)) SafeResetTrigger(drawWeaponTrigger);
            if (!string.IsNullOrEmpty(sheathWeaponTrigger)) SafeResetTrigger(sheathWeaponTrigger);
        }

        // Kích hoạt Trigger để chạy dây nối trong Animator
        // Kích hoạt hoạt ảnh: Sử dụng CrossFade cho các đòn đánh để ép buộc chuyển cảnh ngay lập tức, tránh lỗi dây nối Animator
        if (IsAttackAnimationName(animName))
        {
            SafeSetTrigger(animName);
            StartCoroutine(ResetTriggerNextFrame(animName));

            if (anim.layerCount > 1)
            {
                int stateHash = Animator.StringToHash(animName);
                bool hasLayer1 = anim.HasState(1, stateHash);
                bool hasLayer0 = anim.HasState(0, stateHash);

                int targetLayer = 0;
                if (hasLayer1 && (!isRooted || !hasLayer0))
                {
                    targetLayer = 1;
                    anim.SetLayerWeight(1, 1f);
                }
                else
                {
                    targetLayer = 0;
                    anim.SetLayerWeight(1, 0f);
                }

                anim.CrossFadeInFixedTime(animName, fadeTime, targetLayer, 0f);
            }
            else
            {
                anim.CrossFadeInFixedTime(animName, fadeTime, 0, 0f);
            }
        }
        else
        {
            if (animName == "Death")
            {
                PlayDeathAnimationSafely(fadeTime);
            }
            else
            {
                if (anim.layerCount > 1 && (animName == drawWeaponTrigger || animName == sheathWeaponTrigger || animName == "Shooting"))
                {
                    anim.SetLayerWeight(1, 1f);
                }
                SafeSetTrigger(animName);
            }
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
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null && anim.layerCount > 1)
        {
            var carrier = GetComponent<PlayerLogCarrier>();
            bool isCarrying = carrier != null && carrier.isCarrying;
            anim.SetLayerWeight(1, isCarrying ? 1f : 0f); // Reset weight của Layer 1 về 0, trừ phi đang bưng gỗ
            // Reset Layer 1 (AttackLayer) về trạng thái Empty/New State mặc định
            if (!isCarrying)
            {
                anim.Play("New State", 1, 0f);
            }
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
                else if (comboStep == 0 && !localIsAimingR)
                {
                    var carrier = GetComponent<PlayerLogCarrier>();
                    bool isCarrying = carrier != null && carrier.isCarrying;
                    if (!isCarrying)
                    {
                        anim.SetLayerWeight(1, 0f);
                        anim.Play("New State", 1, 0f);
                    }
                    // Reset shooting trigger and pending shoot flag to avoid stuck animation states/double arrows on next aim
                    SafeResetTrigger("Shooting");
                    isShootPending = false;
                    isRShootPending = false;
                }
            }
        }

        bool isLocal = isStandaloneMode || (IsSpawned && IsOwner);
        if (isLocal)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.SetCrosshairVisible(aiming || localIsAimingR);
            }
        }
    }

    [ServerRpc]
    private void SetAimingServerRpc(bool aiming)
    {
        isAimingNet.Value = aiming;
    }

    private void OnAimingRNetChanged(bool oldVal, bool newVal)
    {
        if (!IsOwner)
        {
            OnAimRStateChanged(newVal);
        }
    }

    private void OnAimRStateChanged(bool aiming)
    {
        bool isPlayingShoot = anim != null && anim.layerCount > 1 && (anim.GetCurrentAnimatorStateInfo(1).IsName("NemRFM") || anim.GetCurrentAnimatorStateInfo(1).IsName("Bow_Shoot"));
        bool keepWeight = aiming || isRShootPending || isPlayingShoot;

        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            anim.SetBool("IsAimingR", aiming || isRShootPending);
            if (anim.layerCount > 1)
            {
                if (keepWeight)
                {
                    anim.SetLayerWeight(1, 1f);
                }
                else if (comboStep == 0 && !localIsAiming)
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

        bool isLocal = isStandaloneMode || (IsSpawned && IsOwner);
        if (isLocal)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
            {
                hud.SetCrosshairVisible(localIsAiming || aiming);
            }
        }

        if (aiming)
        {
            if (rSkillWaterPrefab != null && rSkillWaterSpawnPoint != null && rSkillHandPreviewVisual == null)
            {
                rSkillHandPreviewVisual = Instantiate(rSkillWaterPrefab, rSkillWaterSpawnPoint.position, rSkillWaterSpawnPoint.rotation, rSkillWaterSpawnPoint);
                rSkillHandPreviewVisual.transform.localPosition = Vector3.zero;
                rSkillHandPreviewVisual.transform.localRotation = Quaternion.identity;
                Vector3 parentLossyScale = rSkillWaterSpawnPoint.lossyScale;
                rSkillHandPreviewVisual.transform.localScale = new Vector3(
                    rSkillWaterPrefab.transform.localScale.x / (parentLossyScale.x != 0 ? parentLossyScale.x : 1f),
                    rSkillWaterPrefab.transform.localScale.y / (parentLossyScale.y != 0 ? parentLossyScale.y : 1f),
                    rSkillWaterPrefab.transform.localScale.z / (parentLossyScale.z != 0 ? parentLossyScale.z : 1f)
                );
                
                if (rSkillHandPreviewVisual.TryGetComponent<MayaWaterProjectile>(out var proj))
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
    private void SetAimingRServerRpc(bool aiming)
    {
        isAimingRNet.Value = aiming;
    }

    // Animation Event: Được gọi từ hoạt ảnh LayVuKhi hoặc Bow_Draw (bỏ qua vì không dùng hand visual)
    public void OnDrawNormalAttack()
    {
        // Để trống vì Maya không dùng visual đòn đánh thường trên tay
    }

    // Animation Event: Được gọi từ hoạt ảnh bắn/tấn công (Shooting) tại frame phóng đạn để bắn
    public void OnShootNormalAttack()
    {
        if (isShootPending)
        {
            isShootPending = false;
            FireNormalAttackProjectile();
            OnAimStateChanged(localIsAiming);
        }
        else if (isRShootPending)
        {
            isRShootPending = false;
            FireRHealProjectile();
            OnAimRStateChanged(localIsAimingR);
        }
    }

    // Animation Event: Được gọi từ hoạt ảnh thi triển chiêu R riêng biệt tại frame phóng đạn/VFX R
    public void OnShootRSkill()
    {
        bool isLocal = isStandaloneMode || (IsSpawned && IsOwner);
        if (isLocal && !isRShootPending)
        {
            Debug.LogWarning($"[{gameObject.name}] OnShootRSkill called on Owner, but isRShootPending is false!");
        }

        if (!isRShootPending) return;
        isRShootPending = false;

        OnAimRStateChanged(localIsAimingR);

        if (!isLocal) return;

        FireRHealProjectile();
    }

    private void FireRHealProjectile()
    {
        Vector3 spawnPos = rSkillWaterSpawnPoint != null ? rSkillWaterSpawnPoint.position : (normalAttackSpawnPoint != null ? normalAttackSpawnPoint.position : transform.position + transform.forward * 1.5f + Vector3.up * 1f);
        Vector3 shootDir = targetCamera != null ? targetCamera.transform.forward : transform.forward;

        if (isPendingRShootNetworkMode)
        {
            SpawnWaterProjectileServerRpc(spawnPos, shootDir);
        }
        else
        {
            SpawnWaterProjectileLocal(spawnPos, shootDir);
        }
    }

    private void SpawnWaterProjectileLocal(Vector3 spawnPos, Vector3 shootDirection)
    {
        if (rSkillWaterPrefab == null)
        {
            Debug.LogError("[MayaPlayer] rSkillWaterPrefab chưa được gán trong Inspector!");
            return;
        }

        GameObject waterObj = Instantiate(rSkillWaterPrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        waterObj.transform.localScale = rSkillWaterPrefab.transform.localScale;
        waterObj.SetActive(true);

        if (waterObj.TryGetComponent<MayaWaterProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = rSkillWaterDamage;
            proj.speed = rSkillWaterSpeed;
        }
    }

    [ServerRpc]
    private void SpawnWaterProjectileServerRpc(Vector3 spawnPos, Vector3 shootDirection)
    {
        if (rSkillWaterPrefab == null)
        {
            Debug.LogError("[MayaPlayer] rSkillWaterPrefab chưa được gán trên Server!");
            return;
        }

        if (Vector3.Distance(spawnPos, transform.position) > 4f)
        {
            spawnPos = rSkillWaterSpawnPoint != null ? rSkillWaterSpawnPoint.position : (normalAttackSpawnPoint != null ? normalAttackSpawnPoint.position : transform.position + transform.forward * 1.5f + Vector3.up * 1f);
        }

        GameObject waterObj = Instantiate(rSkillWaterPrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        waterObj.transform.localScale = rSkillWaterPrefab.transform.localScale;
        waterObj.SetActive(true);

        if (waterObj.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn(true);
        }

        if (waterObj.TryGetComponent<MayaWaterProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = rSkillWaterDamage;
            proj.speed = rSkillWaterSpeed;
        }
    }

    private void PerformShooting(bool networkMode)
    {
        if (!networkMode)
        {
            Weapon2Durability = Mathf.Max(Weapon2Durability - 2f, 0f);
        }
        isShootPending = true;
        isPendingShootNetworkMode = networkMode;
        PlayAnimation("Shooting", 0.05f);
    }

    private void FireNormalAttackProjectile()
    {
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
            Vector3 spawnPos = normalAttackSpawnPoint != null ? normalAttackSpawnPoint.position : transform.position + Vector3.up * cameraPivotHeight;
            Vector3 shootDirection = (targetPoint - spawnPos).normalized;

            if (isPendingShootNetworkMode)
            {
                if (normalAttackPrefab != null)
                {
                    ShootingServerRpc(spawnPos, shootDirection);
                }
                else
                {
                    Debug.LogWarning("[FireNormalAttackProjectile] normalAttackPrefab chưa được gán trên Server! Không thể sinh đạn mạng.");
                }
            }
            else
            {
                if (normalAttackPrefab != null)
                {
                    SpawnNormalAttackLocal(spawnPos, shootDirection);
                }
                else
                {
                    // Fallback Standalone: tự động sinh Sphere khi chưa có prefab
                    Debug.LogWarning("[FireNormalAttackProjectile] normalAttackPrefab chưa được gán! Đã tự động tạo một primitive Sphere tạm thời.");
                    GameObject sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphereObj.transform.position = spawnPos;
                    sphereObj.transform.rotation = Quaternion.LookRotation(shootDirection);
                    sphereObj.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    
                    var proj = sphereObj.AddComponent<MayaProjectile>();
                    proj.owner = this;
                    proj.damage = damageAmount;
                    proj.speed = normalAttackSpeed;
                    
                    var sRb = sphereObj.GetComponent<Rigidbody>();
                    if (sRb == null) sRb = sphereObj.AddComponent<Rigidbody>();
                    sRb.useGravity = false;
                    sRb.isKinematic = true;

                    var sCol = sphereObj.GetComponent<Collider>();
                    if (sCol != null) sCol.isTrigger = true;
                }
            }
        }
    }

    private void SpawnNormalAttackLocal(Vector3 spawnPos, Vector3 shootDirection)
    {
        Debug.Log($"[PerformShooting] Đang bắn đòn đánh thường ở chế độ Standalone. Vị trí spawn: {spawnPos}, Hướng bắn: {shootDirection}");
        GameObject normalAttackObj = Instantiate(normalAttackPrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        normalAttackObj.transform.localScale = normalAttackPrefab.transform.localScale;
        normalAttackObj.SetActive(true);
        
        if (normalAttackObj.TryGetComponent<MayaProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = damageAmount;
            proj.speed = normalAttackSpeed;
            
            // Xử lý đạn xuyên thấu E-skill
            if (localIsESkillActive && eSkillRemainingNormalAttacks > 0)
            {
                proj.isPiercing = true;
                eSkillRemainingNormalAttacks--;
                if (eSkillRemainingNormalAttacks <= 0)
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
    private void ShootingServerRpc(Vector3 spawnPos, Vector3 shootDirection)
    {
        // Trừ độ bền vũ khí trên Server (dù trúng hay trượt)
        weapon2Durability.Value = Mathf.Max(weapon2Durability.Value - 2f, 0f);

        // Kiểm tra hợp lệ khoảng cách trên Server để tránh lag giật tọa độ
        if (Vector3.Distance(spawnPos, transform.position) > 4f)
        {
            spawnPos = normalAttackSpawnPoint != null ? normalAttackSpawnPoint.position : transform.position + Vector3.up * cameraPivotHeight;
        }

        if (normalAttackPrefab != null)
        {
            SpawnNormalAttackServer(spawnPos, shootDirection);
        }
        else
        {
            Debug.LogError("[ShootingServerRpc] normalAttackPrefab chưa được gán trên Server!");
        }
    }

    private void SpawnNormalAttackServer(Vector3 spawnPos, Vector3 shootDirection)
    {
        Debug.Log($"[ShootingServerRpc] Server đang spawn đòn đánh thường. Vị trí: {spawnPos}, Hướng bắn: {shootDirection}");
        GameObject normalAttackObj = Instantiate(normalAttackPrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        normalAttackObj.transform.localScale = normalAttackPrefab.transform.localScale;
        normalAttackObj.SetActive(true);
        
        if (normalAttackObj.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn(true);
        }
        if (normalAttackObj.TryGetComponent<MayaProjectile>(out var proj))
        {
            proj.owner = this;
            proj.damage = damageAmount;
            proj.speed = normalAttackSpeed;
            
            // Xử lý đạn xuyên thấu E-skill trên Server
            if (isESkillActiveNet.Value && eSkillRemainingNormalAttacksNet.Value > 0)
            {
                proj.isPiercing = true;
                eSkillRemainingNormalAttacksNet.Value--;
                if (eSkillRemainingNormalAttacksNet.Value <= 0)
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
        if (eTargetingIndicator != null)
        {
            Destroy(eTargetingIndicator);
        }
        if (PlayerHUDManager.ActivePlayers != null)
        {
            PlayerHUDManager.ActivePlayers.Remove(this);
        }
        base.OnDestroy();
    }

    [HideInInspector]
    public GameObject pendingPickItem;

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
}
