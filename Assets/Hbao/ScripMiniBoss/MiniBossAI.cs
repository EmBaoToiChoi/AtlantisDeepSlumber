using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Mini Boss AI Script using FSM (Finite State Machine).
/// States: Idle, Chase, Attack, Hit, Enrage, Dead.
/// Features:
///   - Super Armor & Stagger Cooldown to prevent hit-react stun lock loops.
///   - Dynamic target switching to nearest reachable player or weak/low-HP player.
///   - NavMesh reachability & edge stopping (prevents walking off unbaked maps/walls).
///   - Shadow Teleport Blink execution attack behind target players.
///   - 50% HP Shadow Clone Summoning Skill (triệu hồi 2 phân thân đồng chiến đấu).
///   - Alternates between 3 attacks: Nhaychemdat, NhayDanh, and XoayChem.
/// </summary>
public class MiniBossAI : NetworkBehaviour
{
    //cus
    [Header("Death Event Trigger")]
    [Tooltip("Kéo object chứa VideoCutsceneController vào đây và chọn hàm StartCutscene")]
    public UnityEvent onBossDeathEvent;

    public enum MiniBossState { Idle, Chase, Attack, Hit, Enrage, Dead }

    [Header("Health Settings")]
    public float phase1MaxHealth = 500f;
    public float phase2MaxHealth = 700f;
    [HideInInspector] public float maxHealth = 500f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        500f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Phase 2 Settings")]
    public float phase2SpeedMultiplier = 1.3f;
    public float phase2AnimSpeed = 1.35f;
    public float phase2CooldownMultiplier = 0.6f;
    public float enrageDuration = 3f;
    public string enrageTrigger = "Enrage";

    [Header("Phase 2 Enrage Transition Settings")]
    public GameObject enrageVFXPrefab;
    public AudioClip enrageSFXSound;

    [Header("Summon Clones Skill Settings (50% HP)")]
    public bool isClone = false; // Đánh dấu nếu đây là bản sao phân thân
    public GameObject clonePrefab; // Prefab phân thân (nếu null sẽ dùng chính bản thân MiniBoss)
    private bool hasSummonedClones = false;
    public bool isSummonInvulnerable = false; // Trạng thái MIỄN THƯƠNG trong lúc đang gồng triệu hồi
    public NetworkVariable<int> summonCloneCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Furious Charge Skill Settings (Chỉ thỉnh thoảng mới tăng tốc)")]
    public float furiousChargeSpeed = 15.5f;
    public float furiousChargeCooldown = 20.0f; // Cooldown 20 giây lâu lâu mới thực hiện 1 lần
    private float furiousChargeTimer = 0f;
    private bool isFuriousCharging = false;
    private float furiousChargeDurationTimer = 0f;

    [Header("Shadow Teleport Skill Settings (Chỉ dùng khi lỗi địa hình)")]
    public float shadowBlinkCooldown = 18.0f;
    private float shadowBlinkTimer = 0f;
    public GameObject shadowBlinkVFX;
    public AudioClip shadowBlinkSFX;

    [Header("Network State Sync")]
    public NetworkVariable<MiniBossState> currentState = new NetworkVariable<MiniBossState>(
        MiniBossState.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isBossActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Network Animation Sync Counters")]
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackTypeSync = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> dieCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isPhase2Network = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> enrageCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> shadowBlinkCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Standalone fallback variables
    private float localHealth;
    private MiniBossState localState = MiniBossState.Idle;
    private bool localIsBossActive = false;
    private bool localIsPhase2 = false;
    private bool isStandaloneMode = false;
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public bool IsPhase2 => isStandaloneMode ? localIsPhase2 : isPhase2Network.Value;

    public MiniBossState CurrentStateValue
    {
        get => isStandaloneMode ? localState : currentState.Value;
        set { if (isStandaloneMode) localState = value; else currentState.Value = value; }
    }

    public float ActualCurrentHealth => (isStandaloneMode || !IsSpawned) ? localHealth : currentHealth.Value;
    public bool IsBossActive => isStandaloneMode ? localIsBossActive : isBossActive.Value;
    public bool IsDead => CurrentStateValue == MiniBossState.Dead;

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    [Tooltip("The root of the visual mesh child. Leaps/spins will offset this transform Y position to keep NavMesh tracking on the ground.")]
    public Transform visualRoot;
    [Tooltip("Raycast eye height target for detecting players")]
    public Transform eyeTransform;
    public Transform swordBase;
    public Transform swordTip;

    [Header("Movement Speeds")]
    public float walkSpeed = 2.2f;
    public float runSpeed = 5.8f;

    [Header("AI Vision & Attack Ranges")]
    public float sightRange = 22f;
    public float fieldOfView = 140f;
    public float attackRange = 3.5f;
    public float attackCooldown = 2.2f;
    private float attackCooldownTimer;

    [Header("Weapon & Damage Settings")]
    public float attackDamage = 35f;
    public float knockbackForce = 12f;
    public float swordThickness = 0.45f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Phase 2 Death Explosion Settings")]
    public GameObject deathExplosionVFX;
    public AudioClip deathExplosionSound;
    public float explosionRadius = 6f;
    public float explosionDamage = 50f;
    public float explosionKnockback = 20f;
    public NetworkVariable<int> deathExplosionCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Animator Param Names")]
    public string speedParam = "Speed";       // Float: 0 = Idle, 0.5 = Walk, 1.0 = Run
    public string hitTrigger = "Hit";         // Trigger: Zombie Reaction Hit / Hit
    public string dieTrigger = "Die";         // Trigger: Sword and Shield Death / Die
    public string[] attackTriggers = new string[] { "Nhaychemdat", "NhayDanh", "XoayChem" }; // Triggers for the 3 attack animations

    [System.Serializable]
    public struct AttackConfig
    {
        public float duration;             // Duration of the attack state
        public float leapStartPercent;     // Percentage of duration when leap starts (0.0 to 1.0)
        public float leapDurationPercent;  // Percentage of duration when leap occurs
        public float forwardSpeed;         // Speed of movement during leap
        public float peakHeight;           // Height of the Y parabola leap
        public float damageStartPercent;   // Start of continuous damage window (0.0 to 1.0)
        public float damageEndPercent;     // End of continuous damage window (0.0 to 1.0)
    }

    [Header("Attack Configs (0: Nhaychemdat, 1: NhayDanh, 2: XoayChem)")]
    public AttackConfig[] attackConfigs = new AttackConfig[]
    {
        new AttackConfig { duration = 1.8f, leapStartPercent = 0.15f, leapDurationPercent = 0.5f, forwardSpeed = 10f, peakHeight = 3.5f, damageStartPercent = 0.48f, damageEndPercent = 0.72f },  // Nhaychemdat: jump and slam
        new AttackConfig { duration = 1.4f, leapStartPercent = 0.1f, leapDurationPercent = 0.45f, forwardSpeed = 12f, peakHeight = 2.5f, damageStartPercent = 0.28f, damageEndPercent = 0.62f },  // NhayDanh: fast forward leap
        new AttackConfig { duration = 2.0f, leapStartPercent = 0.05f, leapDurationPercent = 0.65f, forwardSpeed = 7f, peakHeight = 1.2f, damageStartPercent = 0.12f, damageEndPercent = 0.72f }   // XoayChem: spin and glide forward
    };

    private IEnemyState currentFSMState;
    private IdleState idleState;
    private ChaseState chaseState;
    private AttackState attackState;
    private HitState hitState;
    private EnrageState enrageState;
    private DeadState deadState;

    private Transform targetPlayer;
    private float stateTimer;
    private float hitStaggerDuration = 0.35f;
    private float hitStaggerCooldownTimer = 0f;
    private bool hasDealtDamage;
    private int currentAttackIndex = 0;

    // Wander patrol state
    private bool hasWanderDestination;
    private Vector3 wanderDestination;
    private float wanderWaitTimer;

    // Leaping tracking
    private bool isLeaping = false;
    private float leapTimer = 0f;
    private float currentLeapDuration = 0f;
    private float currentLeapForwardSpeed = 0f;
    private float currentLeapHeight = 0f;

    // AI scan rate
    private float scanTimer;
    private float scanInterval = 0.15f;

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private void Awake()
    {
        gameObject.tag = "Enemy";
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && !agent.enabled) agent.enabled = true;
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        var na = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (na != null && anim != null)
        {
            na.Animator = anim;
        }

        maxHealth = phase1MaxHealth;
        localHealth = phase1MaxHealth;
        idleState = new IdleState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);
        hitState = new HitState(this);
        enrageState = new EnrageState(this);
        deadState = new DeadState(this);
    }

    private void Start()
    {
        if (agent != null)
        {
            agent.stoppingDistance = 2.4f;
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            InitStandalone();
        }
    }

    private void InitStandalone()
    {
        maxHealth = phase1MaxHealth;
        localHealth = phase1MaxHealth;
        localIsPhase2 = false;
        SnapToNavMesh();
        ApplySpeedAnim(0f);
        ChangeState(MiniBossState.Idle);
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        netSpeed.OnValueChanged += (_, v) => ApplySpeedAnim(v);
        attackCounter.OnValueChanged += (_, _) => {
            if (anim != null && attackTypeSync.Value >= 0 && attackTypeSync.Value < attackTriggers.Length)
                anim.SetTrigger(attackTriggers[attackTypeSync.Value]);
        };
        hitCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        dieCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(dieTrigger); };
        enrageCounter.OnValueChanged += (_, _) => {
            if (anim != null) anim.SetTrigger(enrageTrigger);
            PlayEnrageVFX();
        };
        shadowBlinkCounter.OnValueChanged += (_, _) => PlayShadowBlinkVisuals();
        summonCloneCounter.OnValueChanged += (_, _) => PlaySummonCloneVisuals();
        isPhase2Network.OnValueChanged += (oldVal, newVal) => {
            if (newVal)
            {
                maxHealth = phase2MaxHealth;
                if (anim != null) anim.speed = phase2AnimSpeed;
                Debug.Log("[MiniBossAI Client] Synced to Phase 2!");
            }
        };
        currentHealth.OnValueChanged += OnHealthNetChanged;
        deathExplosionCounter.OnValueChanged += (_, _) => PlayDeathExplosionEffects();

        ApplySpeedAnim(netSpeed.Value);

        if (IsServer)
        {
            if (isClone)
            {
                maxHealth = phase1MaxHealth * 0.45f;
                currentHealth.Value = maxHealth;
                localHealth = maxHealth;
                isBossActive.Value = true;
            }
            else
            {
                maxHealth = phase1MaxHealth;
                currentHealth.Value = phase1MaxHealth;
                ChangeState(MiniBossState.Idle);
            }
            SnapToNavMesh();
        }
        else
        {
            if (agent != null) agent.enabled = false;
        }

        if (isClone)
        {
            EnsureCloneOverheadHealthBar();
        }
    }

    public override void OnNetworkDespawn()
    {
        netSpeed.OnValueChanged -= (_, v) => ApplySpeedAnim(v);
        attackCounter.OnValueChanged -= (_, _) => {
            if (anim != null && attackTypeSync.Value >= 0 && attackTypeSync.Value < attackTriggers.Length)
                anim.SetTrigger(attackTriggers[attackTypeSync.Value]);
        };
        hitCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        dieCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(dieTrigger); };
        enrageCounter.OnValueChanged -= (_, _) => {
            if (anim != null) anim.SetTrigger(enrageTrigger);
            PlayEnrageVFX();
        };
        shadowBlinkCounter.OnValueChanged -= (_, _) => PlayShadowBlinkVisuals();
        summonCloneCounter.OnValueChanged -= (_, _) => PlaySummonCloneVisuals();
        currentHealth.OnValueChanged -= OnHealthNetChanged;
        deathExplosionCounter.OnValueChanged -= (_, _) => PlayDeathExplosionEffects();
    }

    private void OnHealthNetChanged(float oldVal, float newVal)
    {
        localHealth = newVal;
        float diff = oldVal - newVal;
        if (diff > 0)
        {
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, diff);
        }
    }

    public void ActivateBoss()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer) || isClone || !IsSpawned;
        if (!auth) return;

        if (isStandaloneMode || !IsSpawned)
        {
            localIsBossActive = true;
            localHealth = maxHealth > 0 ? maxHealth : phase1MaxHealth * 0.45f;
        }
        
        if (!isStandaloneMode && IsServer && IsSpawned)
        {
            isBossActive.Value = true;
        }

        // BẢO ĐẢM 100%: Lập tức quét tìm Player gần nhất và chuyển sang trạng thái Chase rượt đuổi ngay lập tức!
        DetectAndSwitchTarget();
        if (targetPlayer == null)
        {
            targetPlayer = FindNearestReachablePlayer();
        }
        if (targetPlayer != null)
        {
            ChangeState(MiniBossState.Chase);
        }

        // Tự động khôi phục/gắn Thanh Máu hiển thị trên đầu nếu đây là Phân Thân
        if (isClone)
        {
            EnsureCloneOverheadHealthBar();
        }

        Debug.Log($"[MiniBossAI] {(isClone ? "Phân Thân Mini Boss" : "Mini Boss")} đã KÍCH HOẠT! Nhắm mục tiêu: {(targetPlayer != null ? targetPlayer.name : "None")} - State: {CurrentStateValue}");
    }

    public void EnsureCloneOverheadHealthBar()
    {
        if (!isClone) return;

        // Tính toán tỷ lệ Un-scale để UI không bị méo/phóng to 3x theo parent scale (3,3,3)
        Vector3 unscaleFactor = new Vector3(
            1f / Mathf.Max(transform.localScale.x, 0.001f),
            1f / Mathf.Max(transform.localScale.y, 0.001f),
            1f / Mathf.Max(transform.localScale.z, 0.001f)
        );

        var existingHealthBar = GetComponentInChildren<EnemyHealthBar>(true);
        if (existingHealthBar != null)
        {
            existingHealthBar.gameObject.SetActive(true);
            existingHealthBar.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            existingHealthBar.transform.localScale = unscaleFactor;
            existingHealthBar.enemy = null; existingHealthBar.enemy2 = null; existingHealthBar.enemy3 = null;
            existingHealthBar.enemy4 = null; existingHealthBar.enemy5 = null; existingHealthBar.skeleton = null;
            existingHealthBar.miniBoss = this;
            existingHealthBar.enabled = true;
            return;
        }

        var existingFallback = GetComponentInChildren<CloneWorldHealthBarFallback>(true);
        if (existingFallback != null)
        {
            existingFallback.gameObject.SetActive(true);
            existingFallback.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            existingFallback.transform.localScale = Vector3.Scale(new Vector3(0.015f, 0.015f, 0.015f), unscaleFactor);
            existingFallback.miniBoss = this;
            return;
        }

        // 1. Thử copy mẫu HealthBar World-Space UI Toolkit từ Enemy khác trong Scene
        EnemyHealthBar sample = FindFirstObjectByType<EnemyHealthBar>(FindObjectsInactive.Include);
        if (sample != null && sample.gameObject != null)
        {
            GameObject newHealthBar = Instantiate(sample.gameObject, transform);
            newHealthBar.name = "HealthBar";
            // Đặt vị trí tương đối 0.95m x scale 3.0 = 2.85m (nằm ngay trên đỉnh đầu MiniBoss)
            newHealthBar.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            newHealthBar.transform.localRotation = Quaternion.identity;
            newHealthBar.transform.localScale = unscaleFactor;
            newHealthBar.SetActive(true);

            EnemyHealthBar hpScript = newHealthBar.GetComponent<EnemyHealthBar>();
            if (hpScript != null)
            {
                hpScript.enemy = null; hpScript.enemy2 = null; hpScript.enemy3 = null;
                hpScript.enemy4 = null; hpScript.enemy5 = null; hpScript.skeleton = null;
                hpScript.miniBoss = this;
                hpScript.enabled = true;
            }
            Debug.Log("[MiniBossAI] Đã tự động copy Thanh Máu World-Space trên đầu cho Phân Thân Mini Boss!");
        }
        else
        {
            // 2. Dự phòng: Tự động dựng Canvas Thanh máu World-Space trực tiếp nếu Scene không có quái thường nào khác
            GameObject canvasObj = new GameObject("CloneHealthBarCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.Scale(new Vector3(0.015f, 0.015f, 0.015f), unscaleFactor);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(100f, 14f);

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            var fillImg = fillObj.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = new Color(0.95f, 0.2f, 0.2f, 1f);
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1.5f, 1.5f);
            fillRect.offsetMax = new Vector2(-1.5f, -1.5f);

            var fallbackComp = canvasObj.AddComponent<CloneWorldHealthBarFallback>();
            fallbackComp.miniBoss = this;
            fallbackComp.fillRect = fillRect;

            Debug.Log("[MiniBossAI] Đã tự động dựng Canvas Thanh Máu World-Space dự phòng cho Phân Thân!");
        }
    }

    public void ApplyStun(float duration)
    {
        if (IsDead) return;
        EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, duration, null, 2.8f, 2.2f);
        if (!isStandaloneMode && IsSpawned && IsServer)
        {
            ApplyStunVfxClientRpc(duration);
        }
    }

    [ClientRpc]
    private void ApplyStunVfxClientRpc(float duration)
    {
        if (IsDead) return;
        EnemyStunVfxBehaviour.ApplyStunVfx(gameObject, duration, null, 2.8f, 2.2f);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || CurrentStateValue == MiniBossState.Enrage || isSummonInvulnerable) return;

        localHealth = Mathf.Max(0f, localHealth - damage);
        if (!isStandaloneMode && IsSpawned && IsServer)
        {
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - damage);
            localHealth = currentHealth.Value;
        }

        EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);

        bool isAuth = isStandaloneMode || (IsSpawned && IsServer) || !IsSpawned;
        if (!isAuth) return;

        float activeHp = ActualCurrentHealth;
        if (activeHp <= 0f)
        {
            ChangeState(MiniBossState.Dead);
            return;
        }

        // Tự động kích hoạt khi nhận sát thương nếu chưa active
        if (!IsBossActive) ActivateBoss();

        // Phase 2 Check (chuyển giai đoạn Cuồng Nộ khi mất hết máu Phase 1)
        if (!IsPhase2 && activeHp <= 0f)
        {
            TriggerPhase2Transition();
            return;
        }

        // TRIỆU HỒI 2 BẢN SAO PHÂN THÂN KHI MÁU XUỐNG DƯỚI 50% HP
        if (!isClone && !hasSummonedClones && (activeHp / maxHealth) <= 0.50f)
        {
            hasSummonedClones = true;
            SummonClones();
        }

        // KIỂM TRA TỐC BIẾN NÉ ĐÒN KHI BỊ DỒN SÁT THƯƠNG
        CheckShadowBlinkDodge(damage);

        // HYPER ARMOR FIX: Khi đang tấn công (Attack State), Enrage hoặc Dead -> Không bị hủy đòn chém
        if (CurrentStateValue == MiniBossState.Attack || CurrentStateValue == MiniBossState.Enrage || CurrentStateValue == MiniBossState.Dead)
        {
            return;
        }

        // COOLDOWN HIT STAGGER FIX: Chỉ giật đòn khi sát thương lớn (>= 30 HP) và đã qua thời gian hồi giật đòn (1.6s)
        if (damage >= 30f && hitStaggerCooldownTimer <= 0f)
        {
            hitStaggerCooldownTimer = 1.6f;
            if (!isStandaloneMode && IsSpawned && IsServer)
            {
                hitCounter.Value++;
            }
            else if (anim != null)
            {
                anim.SetTrigger(hitTrigger);
            }
            ChangeState(MiniBossState.Hit);
        }
    }

    private void SummonClones()
    {
        Debug.Log("[MiniBossAI] Máu xuống 50% HP! Triệu hồi 2 phân thân kế bên và tiếp tục combat bình thường!");

        PlaySummonCloneVisuals();

        Vector3 bossScale = transform.localScale;
        if (bossScale.sqrMagnitude < 0.1f) bossScale = new Vector3(3f, 3f, 3f);

        Vector3 targetPosLeft = transform.position - transform.right * 2.5f;
        Vector3 targetPosRight = transform.position + transform.right * 2.5f;

        if (NavMesh.SamplePosition(targetPosLeft, out NavMeshHit hitL, 4.0f, NavMesh.AllAreas)) targetPosLeft = hitL.position;
        if (NavMesh.SamplePosition(targetPosRight, out NavMeshHit hitR, 4.0f, NavMesh.AllAreas)) targetPosRight = hitR.position;

        GameObject prefabToSpawn = clonePrefab;
        bool instantiatedFromSceneObject = false;
        if (prefabToSpawn == null)
        {
            prefabToSpawn = gameObject;
            instantiatedFromSceneObject = true;
        }

        // 1. Sinh 2 phân thân ngay KẾ BÊN MINI BOSS ở vị trí và kích thước bình thường
        GameObject cloneLeft = Instantiate(prefabToSpawn, targetPosLeft, transform.rotation);
        GameObject cloneRight = Instantiate(prefabToSpawn, targetPosRight, transform.rotation);

        // Nếu clone tạo từ gameObject Scene: Xóa NetworkObject trùng lặp trên Clone để NGO không tiêu hủy Boss gốc
        if (instantiatedFromSceneObject)
        {
            var netL = cloneLeft.GetComponent<NetworkObject>();
            if (netL != null) DestroyImmediate(netL);

            var netR = cloneRight.GetComponent<NetworkObject>();
            if (netR != null) DestroyImmediate(netR);
        }

        cloneLeft.transform.position = targetPosLeft;
        cloneRight.transform.position = targetPosRight;
        cloneLeft.transform.localScale = bossScale;
        cloneRight.transform.localScale = bossScale;

        // Bảo đảm 100% kích thước MiniBoss chính không bị ảnh hưởng
        transform.localScale = bossScale;

        MiniBossAI leftAI = cloneLeft.GetComponent<MiniBossAI>();
        MiniBossAI rightAI = cloneRight.GetComponent<MiniBossAI>();

        ConfigureClone(leftAI, targetPosLeft);
        ConfigureClone(rightAI, targetPosRight);

        // Đồng bộ Network nếu dùng clonePrefab hợp lệ
        if (!isStandaloneMode && IsServer && !instantiatedFromSceneObject)
        {
            NetworkObject netL = cloneLeft.GetComponent<NetworkObject>();
            if (netL != null && !netL.IsSpawned) netL.Spawn();

            NetworkObject netR = cloneRight.GetComponent<NetworkObject>();
            if (netR != null && !netR.IsSpawned) netR.Spawn();
        }

        // Tiếp tục combat bình thường ngay lập tức!
        isSummonInvulnerable = false;
        if (targetPlayer != null) ChangeState(MiniBossState.Chase);
    }

    private void ConfigureClone(MiniBossAI cloneAI, Vector3 spawnPos)
    {
        if (cloneAI == null) return;
        cloneAI.isClone = true;
        cloneAI.hasSummonedClones = true; // Khóa không cho phân thân triệu hồi tiếp
        cloneAI.phase1MaxHealth = phase1MaxHealth * 0.45f;
        cloneAI.localHealth = phase1MaxHealth * 0.45f;
        cloneAI.maxHealth = phase1MaxHealth * 0.45f;
        cloneAI.localIsBossActive = true;

        if (!cloneAI.isStandaloneMode && IsServer)
        {
            cloneAI.isBossActive.Value = true;
            cloneAI.currentHealth.Value = cloneAI.maxHealth;
        }

        // Ẩn toàn bộ thanh máu của Phân thân
        var hpBars = cloneAI.GetComponentsInChildren<EnemyHealthBar>();
        foreach (var hp in hpBars) if (hp != null) hp.enabled = false;

        // Bật NavMeshAgent cho phân thân và Warp tới vị trí kế bên Boss
        if (cloneAI.agent != null)
        {
            cloneAI.agent.enabled = true;
            cloneAI.agent.Warp(spawnPos);
        }
        cloneAI.ActivateBoss();
        cloneAI.ChangeState(MiniBossState.Chase);
    }

    private void FinishCloneSetup(GameObject cloneObj, Vector3 finalPos)
    {
        if (cloneObj == null) return;

        MiniBossAI cloneAI = cloneObj.GetComponent<MiniBossAI>();
        if (cloneAI != null)
        {
            if (cloneAI.agent != null)
            {
                cloneAI.agent.enabled = true;
                cloneAI.agent.Warp(finalPos);
            }
            else
            {
                cloneObj.transform.position = finalPos;
            }
            cloneObj.transform.localScale = transform.localScale;
            cloneAI.ActivateBoss();
        }
        else
        {
            cloneObj.transform.position = finalPos;
        }
    }

    private void PlaySummonCloneVisuals()
    {
        if (enrageVFXPrefab != null)
        {
            // Đi theo đúng giữa tâm ngực MiniBoss 100% (cao 0.45m x scale 3.0 = 1.35m giữa ngực)
            GameObject vfx = Instantiate(enrageVFXPrefab, transform.position, transform.rotation, transform);
            vfx.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            vfx.transform.localRotation = Quaternion.identity;
            Destroy(vfx, 3.5f);
        }
        if (enrageSFXSound != null)
        {
            AudioSource.PlayClipAtPoint(enrageSFXSound, transform.position + Vector3.up * 1.35f, 1.0f);
        }
    }

    private void TriggerPhase2Transition()
    {
        if (IsPhase2) return;

        if (isStandaloneMode)
        {
            localIsPhase2 = true;
            localHealth = phase2MaxHealth;
            maxHealth = phase2MaxHealth;
        }
        else if (IsServer)
        {
            isPhase2Network.Value = true;
            currentHealth.Value = phase2MaxHealth;
            maxHealth = phase2MaxHealth;
            enrageCounter.Value++;
        }

        ChangeState(MiniBossState.Enrage);
        Debug.Log("[MiniBossAI] Phase 2 Enrage Triggered!");
    }

    private void Update()
    {
        if (hitStaggerCooldownTimer > 0f) hitStaggerCooldownTimer -= Time.deltaTime;
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (shadowBlinkTimer > 0) shadowBlinkTimer -= Time.deltaTime;
        if (furiousChargeTimer > 0) furiousChargeTimer -= Time.deltaTime;

        if (recentDamageResetTimer > 0f)
        {
            recentDamageResetTimer -= Time.deltaTime;
            if (recentDamageResetTimer <= 0f) recentDamageTaken = 0f;
        }

        bool aiAuth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!aiAuth) return;

        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh) SnapToNavMesh();

        // Wall collision resolver
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            Vector3 moveDir = agent.velocity.normalized;
            if (Physics.Raycast(rayOrigin, moveDir, out RaycastHit hit, 0.8f, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy") && hit.collider.gameObject.layer != LayerMask.NameToLayer("Enemy") && !hit.collider.isTrigger)
                {
                    Vector3 pushBack = hit.normal * 0.15f;
                    agent.Warp(transform.position + pushBack);
                }
            }
        }

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0)
        {
            scanTimer = scanInterval;
            DetectAndSwitchTarget();
        }

        if (currentFSMState != null)
        {
            currentFSMState.Update();
        }
    }

    private void ChangeState(MiniBossState newState)
    {
        if (currentFSMState != null)
        {
            currentFSMState.Exit();
        }

        if (!isStandaloneMode && IsServer)
        {
            currentState.Value = newState;
        }
        else
        {
            localState = newState;
        }

        switch (newState)
        {
            case MiniBossState.Idle:   currentFSMState = idleState;   break;
            case MiniBossState.Chase:  currentFSMState = chaseState;  break;
            case MiniBossState.Attack: currentFSMState = attackState; break;
            case MiniBossState.Hit:    currentFSMState = hitState;    break;
            case MiniBossState.Enrage: currentFSMState = enrageState; break;
            case MiniBossState.Dead:   currentFSMState = deadState;   break;
        }

        if (currentFSMState != null)
        {
            currentFSMState.Enter();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  FSM STATE ACTIONS
    // ══════════════════════════════════════════════════════════

    private void HandleIdle()
    {
        if (IsBossActive && targetPlayer != null)
        {
            hasWanderDestination = false;
            ChangeState(MiniBossState.Chase);
            return;
        }

        if (!hasWanderDestination)
        {
            wanderWaitTimer -= Time.deltaTime;
            if (wanderWaitTimer <= 0)
            {
                Vector3 randomDirection = Random.insideUnitSphere * 8f + transform.position;
                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, 8f, NavMesh.AllAreas))
                {
                    wanderDestination = navHit.position;
                    hasWanderDestination = true;
                    if (AgentReady)
                    {
                        agent.isStopped = false;
                        agent.speed = walkSpeed;
                        agent.SetDestination(wanderDestination);
                    }
                }
            }
            SetSpeedNet(0f);
        }
        else
        {
            SetSpeedNet(0.5f); // Walk animation

            if (AgentReady)
            {
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                {
                    hasWanderDestination = false;
                    wanderWaitTimer = Random.Range(2.0f, 4.0f);
                }
            }
            else
            {
                hasWanderDestination = false;
            }
        }
    }

    private void HandleChase()
    {
        if (targetPlayer == null || IsPlayerDeadOrInvisible(targetPlayer))
        {
            targetPlayer = null;
            isFuriousCharging = false;
            ChangeState(MiniBossState.Idle);
            return;
        }

        // 1. Kiểm tra NavMesh Reachability (Chỉ dùng Tốc Biến khi Player ở vị trí lỗi địa hình/ngoài bản đồ)
        if (!IsTargetReachableOnNavMesh(targetPlayer))
        {
            Transform altTarget = FindNearestReachablePlayer();
            if (altTarget != null)
            {
                targetPlayer = altTarget;
            }
            else
            {
                if (AgentReady) agent.isStopped = true;
                SetSpeedNet(0f);
                
                if (shadowBlinkTimer <= 0f)
                {
                    ExecuteShadowBlink(targetPlayer);
                }
                return;
            }
        }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);

        // 2. Kỹ năng Cuồng Phong Tăng Tốc Lao Tới (Chỉ thỉnh thoảng 20s kích hoạt 1 lần khi Player ở rất xa > 9.5m)
        if (!isFuriousCharging && furiousChargeTimer <= 0f && dist > 9.5f)
        {
            if (Random.value < 0.40f)
            {
                isFuriousCharging = true;
                furiousChargeDurationTimer = 2.2f;
                furiousChargeTimer = IsPhase2 ? 15.0f : furiousChargeCooldown;
                PlayShadowBlinkVisuals();
                Debug.Log($"[MiniBossAI] Lâu lâu Mini Boss gồng nộ KÍCH HOẠT CUỒNG PHONG LAO TỐC ĐỘ CAO (15.5 m/s)!");
            }
            else
            {
                furiousChargeTimer = 3.0f; // Nếu chưa kích hoạt thì chờ thêm 3s mới quét lại
            }
        }

        if (isFuriousCharging)
        {
            furiousChargeDurationTimer -= Time.deltaTime;
            if (furiousChargeDurationTimer <= 0f || dist <= attackRange)
            {
                isFuriousCharging = false;
            }
        }

        if (dist <= attackRange && attackCooldownTimer <= 0)
        {
            isFuriousCharging = false;
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            ChangeState(MiniBossState.Attack);
            return;
        }

        // CHIẾN THUẬT BAO VÂY GỌNG KÌM 3 PHÍA CHO PHÂN THÂN
        Vector3 chaseDestination = targetPlayer.position;
        if (isClone)
        {
            int flankSign = (GetInstanceID() % 2 == 0) ? 1 : -1;
            float angleOffset = flankSign * 60f; // Trái +60 độ, Phải -60 độ
            Vector3 dirFromPlayer = (transform.position - targetPlayer.position).normalized;
            if (dirFromPlayer.sqrMagnitude < 0.01f) dirFromPlayer = -targetPlayer.forward;
            
            Vector3 flankDir = Quaternion.Euler(0, angleOffset, 0) * dirFromPlayer;
            Vector3 desiredFlankPos = targetPlayer.position + flankDir * (attackRange * 0.85f);

            if (NavMesh.SamplePosition(desiredFlankPos, out NavMeshHit flankHit, 3.5f, NavMesh.AllAreas))
            {
                chaseDestination = flankHit.position;
            }
        }

        // Di chuyển áp sát mục tiêu (Tự động tăng tốc cực mạnh nếu đang Cuồng Phong Lao Tới)
        if (AgentReady)
        {
            agent.isStopped = false;
            float baseRun = IsPhase2 ? (runSpeed * phase2SpeedMultiplier) : runSpeed;
            float activeRunSpeed = isFuriousCharging 
                ? (IsPhase2 ? (furiousChargeSpeed * phase2SpeedMultiplier) : furiousChargeSpeed)
                : baseRun;

            agent.speed = activeRunSpeed;
            agent.SetDestination(chaseDestination);
        }
        
        SetSpeedNet(AgentReady && !agent.isStopped ? 1.0f : 0f); // Run animation
        RotateTowards(targetPlayer.position);
    }

    private void ExecuteShadowBlink(Transform target)
    {
        if (target == null) return;

        shadowBlinkTimer = IsPhase2 ? (shadowBlinkCooldown * 0.65f) : shadowBlinkCooldown;

        Vector3 backPos = target.position - target.forward * 1.5f;
        if (!NavMesh.SamplePosition(backPos, out NavMeshHit navHit, 3.5f, NavMesh.AllAreas))
        {
            Vector3 sidePos = target.position + target.right * 1.5f;
            if (!NavMesh.SamplePosition(sidePos, out navHit, 3.5f, NavMesh.AllAreas))
            {
                navHit.position = target.position;
            }
        }

        if (!isStandaloneMode && IsServer)
        {
            shadowBlinkCounter.Value++;
        }
        else
        {
            PlayShadowBlinkVisuals();
        }

        if (AgentReady)
        {
            agent.Warp(navHit.position);
        }
        else
        {
            transform.position = navHit.position;
        }

        FaceTargetImmediately(target.position);
        Debug.Log($"[MiniBossAI] Bí thuật Tốc Biến Bóng Tối xuất hiện đằng sau {target.name}!");

        ChangeState(MiniBossState.Attack);
    }

    private void PlayShadowBlinkVisuals()
    {
        if (shadowBlinkVFX != null)
        {
            // Gán VFX đi theo tâm thân người MiniBoss 100% (cao 0.45m x scale 3.0 = 1.35m giữa ngực)
            GameObject vfx = Instantiate(shadowBlinkVFX, transform.position, transform.rotation, transform);
            vfx.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            vfx.transform.localRotation = Quaternion.identity;
            Destroy(vfx, 2.5f);
        }
        if (shadowBlinkSFX != null)
        {
            AudioSource.PlayClipAtPoint(shadowBlinkSFX, transform.position + Vector3.up * 1.35f, 1.0f);
        }
    }

    private void HandleAttack()
    {
        stateTimer -= Time.deltaTime;

        if (isLeaping && leapTimer >= 0f)
        {
            leapTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(leapTimer / currentLeapDuration);

            if (AgentReady)
            {
                agent.Move(transform.forward * currentLeapForwardSpeed * Time.deltaTime);
            }

            if (visualRoot != null)
            {
                float yOffset = currentLeapHeight * Mathf.Sin(Mathf.PI * progress);
                visualRoot.localPosition = new Vector3(0, yOffset, 0);
            }

            if (leapTimer >= currentLeapDuration)
            {
                isLeaping = false;
                if (visualRoot != null) visualRoot.localPosition = Vector3.zero;
            }
        }
        else if (isLeaping && leapTimer < 0f)
        {
            leapTimer += Time.deltaTime;
        }

        if (targetPlayer != null)
        {
            RotateTowards(targetPlayer.position);
        }

        if (stateTimer <= 0)
        {
            attackCooldownTimer = IsPhase2 ? (attackCooldown * phase2CooldownMultiplier) : attackCooldown;
            if (targetPlayer != null) ChangeState(MiniBossState.Chase);
            else ChangeState(MiniBossState.Idle);
        }
    }

    private void HandleHit()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            if (targetPlayer != null) ChangeState(MiniBossState.Chase);
            else ChangeState(MiniBossState.Idle);
        }
    }

    private void HandleEnrage()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            if (targetPlayer != null) ChangeState(MiniBossState.Chase);
            else ChangeState(MiniBossState.Idle);
        }
    }

private void Die()
    {
        if (AgentReady) agent.isStopped = true;
        if (agent != null) agent.enabled = false;
        SetSpeedNet(0f);

        if (visualRoot != null) visualRoot.localPosition = Vector3.zero;

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c != null && !c.isTrigger) c.enabled = false;
        }

        if (isClone)
        {
            TriggerSoulBurstExplosion();
        }
        else if (IsPhase2)
        {
            TriggerDeathExplosion();
        }

        // --- CODE GỌI CUTSCENE THÊM Ở ĐÂY ---
        // Chỉ kích hoạt sự kiện khi đây là Boss chính (không phải clone) 
        // và lệnh này chỉ được phát đi từ phía Server/Host để đồng bộ cho cả phòng.
        if (!isClone)
        {
            bool isAuth = isStandaloneMode || (IsNetworkActive && IsServer);
            if (isAuth)
            {
                onBossDeathEvent?.Invoke();
            }
        }
        // ------------------------------------

        if (!isStandaloneMode && IsServer)
        {
            dieCounter.Value++;
        }
        else if (anim != null)
        {
            anim.SetTrigger(dieTrigger);
        }

        Debug.Log("[MiniBossAI] Mini Boss is dead!");
        Destroy(gameObject, 5f);
    }

    private void TriggerDeathExplosion()
    {
        if (!isStandaloneMode && IsServer)
        {
            deathExplosionCounter.Value++;
        }
        else if (isStandaloneMode)
        {
            PlayDeathExplosionEffects();
        }

        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            Vector3 explodePos = transform.position + Vector3.up * 1.2f;
            Collider[] hits = Physics.OverlapSphere(explodePos, explosionRadius, playerLayer);
            HashSet<Transform> damagedRoots = new HashSet<Transform>();

            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.transform);
                if (root != null && !damagedRoots.Contains(root))
                {
                    damagedRoots.Add(root);
                    Vector3 knockbackDir = (root.position - transform.position);
                    knockbackDir.y = 0.5f;
                    knockbackDir = knockbackDir.normalized;
                    Vector3 force = knockbackDir * explosionKnockback;

                    EnemyDamageHelper.DealDamage(root, explosionDamage, force);
                }
            }
        }
    }

    public void HealBoss(float amount)
    {
        if (IsDead || ActualCurrentHealth <= 0) return;

        if (isStandaloneMode)
        {
            localHealth = Mathf.Min(localHealth + amount, maxHealth);
        }
        else if (IsServer)
        {
            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
        }

        PlaySummonCloneVisuals();
    }

    private void TriggerSoulBurstExplosion()
    {
        Vector3 explodePos = transform.position + Vector3.up * 1.2f;
        PlayDeathExplosionEffects();

        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (auth)
        {
            // Sát thương bạo nổ linh hồn gây dame cho Player đứng gần
            Collider[] hits = Physics.OverlapSphere(explodePos, 3.5f, playerLayer);
            HashSet<Transform> damagedRoots = new HashSet<Transform>();

            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.transform);
                if (root != null && !damagedRoots.Contains(root))
                {
                    damagedRoots.Add(root);
                    Vector3 kbDir = (root.position - transform.position).normalized + Vector3.up * 0.5f;
                    EnemyDamageHelper.DealDamage(root, 25f, kbDir * 6f);
                }
            }

            // Hồi 10% máu cho Trùm Phụ chính nếu đứng gần dưới 15m
            var allBosses = FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
            foreach (var b in allBosses)
            {
                if (b != null && !b.isClone && !b.IsDead && b.ActualCurrentHealth > 0)
                {
                    if (Vector3.Distance(transform.position, b.transform.position) <= 15.0f)
                    {
                        float healAmount = b.maxHealth * 0.10f;
                        b.HealBoss(healAmount);
                        Debug.Log($"[MiniBossAI] Phân thân hy sinh! Trùm Phụ chính đứng gần được hồi {healAmount} HP!");
                    }
                }
            }
        }
    }

    private float recentDamageTaken = 0f;
    private float recentDamageResetTimer = 0f;

    public void CheckShadowBlinkDodge(float damage)
    {
        // Tắt hoàn toàn tốc biến / dịch chuyển né đòn khi đánh nhau theo yêu cầu!
        return;
    }

    private void ExecuteShadowBlinkDodge()
    {
        if (targetPlayer == null) return;

        Vector3 escapeDir = (transform.position - targetPlayer.position).normalized;
        escapeDir += (Random.insideUnitSphere * 0.4f);
        escapeDir.y = 0;
        escapeDir = escapeDir.normalized;

        Vector3 blinkTarget = transform.position + escapeDir * 4.5f;
        if (NavMesh.SamplePosition(blinkTarget, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
        {
            PlayShadowBlinkVisuals();
            if (AgentReady) agent.Warp(hit.position);
            FaceTargetImmediately(targetPlayer.position);
            Debug.Log($"[MiniBossAI] Mini Boss/Phân Thân bị dồn dame sát thương! KÍCH HOẠT TỐC BIẾN NÉ ĐÒN 4.5M!");
        }
    }

    private void PlayDeathExplosionEffects()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1.2f;
        if (deathExplosionVFX != null)
        {
            GameObject vfx = Instantiate(deathExplosionVFX, spawnPos, Quaternion.identity);
            Destroy(vfx, 4f);
        }
        if (deathExplosionSound != null)
        {
            AudioSource.PlayClipAtPoint(deathExplosionSound, spawnPos, 1.0f);
        }
    }

    public void PlayEnrageVFX()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1f;
        if (enrageVFXPrefab != null)
        {
            GameObject vfx = Instantiate(enrageVFXPrefab, spawnPos, transform.rotation);
            vfx.transform.SetParent(transform);
            Destroy(vfx, 5f);
        }
        if (enrageSFXSound != null)
        {
            AudioSource.PlayClipAtPoint(enrageSFXSound, spawnPos, 1.0f);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  DYNAMIC TARGET DETECTION & NEAREST TARGET SWITCHING
    // ══════════════════════════════════════════════════════════

    private List<Transform> GetAllActivePlayers()
    {
        var list = new List<Transform>();

        var leos = FindObjectsByType<LeoPlayer>(FindObjectsSortMode.None);
        foreach (var p in leos) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var arthurs = FindObjectsByType<ArthurPlayer>(FindObjectsSortMode.None);
        foreach (var p in arthurs) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var elenas = FindObjectsByType<ElenaPlayer>(FindObjectsSortMode.None);
        foreach (var p in elenas) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var elenaArchers = FindObjectsByType<ElenaArcher>(FindObjectsSortMode.None);
        foreach (var p in elenaArchers) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var mayas = FindObjectsByType<MayaPlayer>(FindObjectsSortMode.None);
        foreach (var p in mayas) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var mayaSupports = FindObjectsByType<MayaSupport>(FindObjectsSortMode.None);
        foreach (var p in mayaSupports) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        var simples = FindObjectsByType<SimplePlayerTest>(FindObjectsSortMode.None);
        foreach (var p in simples) if (p != null && !list.Contains(p.transform)) list.Add(p.transform);

        return list;
    }

    private void DetectAndSwitchTarget()
    {
        if (IsDead) return;

        Transform bestTarget = null;
        float minPathDist = float.MaxValue;
        float currentTargetDist = float.MaxValue;

        if (targetPlayer != null && !IsPlayerDeadOrInvisible(targetPlayer) && IsTargetReachableOnNavMesh(targetPlayer))
        {
            currentTargetDist = GetNavMeshPathDistance(transform.position, targetPlayer.position);
        }

        var activePlayers = GetAllActivePlayers();
        foreach (var pTrans in activePlayers)
        {
            if (pTrans == null || IsPlayerDeadOrInvisible(pTrans)) continue;

            if (!IsTargetReachableOnNavMesh(pTrans)) continue;

            float d = GetNavMeshPathDistance(transform.position, pTrans.position);
            float currentSight = IsBossActive ? 50f : sightRange;
            if (d > currentSight) continue;

            float hpRatio = GetPlayerHealthRatio(pTrans);
            if (hpRatio <= 0.35f)
            {
                d *= 0.6f;
            }

            if (d < minPathDist)
            {
                minPathDist = d;
                bestTarget = pTrans;
            }
        }

        if (bestTarget != null)
        {
            bool shouldSwitch = false;
            if (targetPlayer == null)
            {
                shouldSwitch = true;
            }
            else if (bestTarget != targetPlayer)
            {
                if (minPathDist < currentTargetDist - 1.8f || minPathDist < 3.5f)
                {
                    shouldSwitch = true;
                }
            }

            if (shouldSwitch)
            {
                targetPlayer = bestTarget;
            }
        }
        else if (CurrentStateValue == MiniBossState.Chase)
        {
            targetPlayer = null;
        }
    }

    private Transform FindNearestReachablePlayer()
    {
        Transform best = null;
        float minScore = float.MaxValue;
        var players = GetAllActivePlayers();
        foreach (var p in players)
        {
            if (p == null || IsPlayerDeadOrInvisible(p)) continue;
            if (IsTargetReachableOnNavMesh(p))
            {
                float d = GetNavMeshPathDistance(transform.position, p.position);
                float hpRatio = GetPlayerHealthRatio(p);
                float score = d * (0.5f + 0.5f * hpRatio);
                if (hpRatio < 0.35f) score *= 0.6f; // Ưu tiên tập trung săn Player yếu máu (<35% HP)

                if (score < minScore)
                {
                    minScore = score;
                    best = p;
                }
            }
        }
        return best;
    }

    private bool IsTargetReachableOnNavMesh(Transform player)
    {
        if (player == null) return false;
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, player.position, NavMesh.AllAreas, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete) return true;
        }
        return false;
    }

    private float GetNavMeshPathDistance(Vector3 start, Vector3 target)
    {
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(start, target, NavMesh.AllAreas, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial)
            {
                float dist = 0f;
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    dist += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                return dist;
            }
        }
        return Vector3.Distance(start, target);
    }

    private float GetPlayerHealthRatio(Transform player)
    {
        if (player == null) return 1f;
        var ps = player.GetComponentInParent<IPlayerHUDTarget>() ?? player.GetComponentInChildren<IPlayerHUDTarget>();
        if (ps != null && ps.MaxHealth > 0) return ps.CurrentHealth / ps.MaxHealth;

        var sk = player.GetComponentInParent<Skeleton>();
        if (sk != null && sk.maxHealth > 0) return sk.CurrentHealthValue / sk.maxHealth;

        return 1f;
    }

    private bool IsPlayerDeadOrInvisible(Transform player)
    {
        var ps = player.GetComponentInParent<IPlayerHUDTarget>();
        var sk = player.GetComponentInParent<Skeleton>();
        return (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0);
    }

    public void DealSwordDamageContinuously(HashSet<Transform> hitThisAttack)
    {
        Vector3 finalKnockback = Vector3.zero; // Không đẩy Player khi chém
        float finalDamage = IsPhase2 ? (attackDamage * 1.25f) : attackDamage;

        if (swordBase != null && swordTip != null)
        {
            Vector3 start = swordBase.position;
            Vector3 end = swordTip.position;
            Vector3 dir = (end - start).normalized;
            float dist = Vector3.Distance(start, end);

            RaycastHit[] hits = Physics.SphereCastAll(start, swordThickness, dir, dist, playerLayer);
            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.collider.transform);
                if (root != null && !hitThisAttack.Contains(root))
                {
                    hitThisAttack.Add(root);
                    EnemyDamageHelper.DealDamage(root, finalDamage, finalKnockback);
                }
            }
        }
        else
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, swordThickness, transform.forward, attackRange, playerLayer);
            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.collider.transform);
                if (root != null && !hitThisAttack.Contains(root))
                {
                    hitThisAttack.Add(root);
                    EnemyDamageHelper.DealDamage(root, finalDamage, finalKnockback);
                }
            }
        }
    }

    private Transform GetPlayerRoot(Transform t)
    {
        if (t.GetComponentInParent<LeoPlayer>() != null) return t.GetComponentInParent<LeoPlayer>().transform;
        if (t.GetComponentInParent<ArthurPlayer>() != null) return t.GetComponentInParent<ArthurPlayer>().transform;
        if (t.GetComponentInParent<ElenaPlayer>() != null) return t.GetComponentInParent<ElenaPlayer>().transform;
        if (t.GetComponentInParent<ElenaArcher>() != null) return t.GetComponentInParent<ElenaArcher>().transform;
        if (t.GetComponentInParent<MayaPlayer>() != null) return t.GetComponentInParent<MayaPlayer>().transform;
        if (t.GetComponentInParent<MayaSupport>() != null) return t.GetComponentInParent<MayaSupport>().transform;
        if (t.GetComponentInParent<SimplePlayerTest>() != null) return t.GetComponentInParent<SimplePlayerTest>().transform;
        if (t.GetComponentInParent<Skeleton>() != null) return t.GetComponentInParent<Skeleton>().transform;
        return t;
    }

    private void SnapToNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (!agent.isOnNavMesh)
        {
            NavMeshHit h;
            if (NavMesh.SamplePosition(transform.position, out h, 10f, NavMesh.AllAreas))
            {
                agent.Warp(h.position);
            }
        }
    }

    private void RotateTowards(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            float rotSpeed = (CurrentStateValue == MiniBossState.Attack) ? 540f : 360f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
        }
    }

    public void FaceTargetImmediately(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void ApplySpeedAnim(float speed)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        anim.SetFloat(speedParam, speed);
    }

    private void SetSpeedNet(float val)
    {
        if (isStandaloneMode) ApplySpeedAnim(val);
        else if (IsServer && !Mathf.Approximately(netSpeed.Value, val)) netSpeed.Value = val;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (swordBase != null && swordTip != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(swordBase.position, swordTip.position);
            Gizmos.DrawWireSphere(swordBase.position, swordThickness);
            Gizmos.DrawWireSphere(swordTip.position, swordThickness);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  STATE MACHINE INNER CLASSES
    // ══════════════════════════════════════════════════════════

    private class IdleState : IEnemyState
    {
        private MiniBossAI boss;
        public IdleState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.hasWanderDestination = false;
            boss.wanderWaitTimer = Random.Range(1f, 3f);
        }
        public void Update() { boss.HandleIdle(); }
        public void Exit() { }
    }

    private class ChaseState : IEnemyState
    {
        private MiniBossAI boss;
        public ChaseState(MiniBossAI boss) { this.boss = boss; }
        public void Enter() { }
        public void Update() { boss.HandleChase(); }
        public void Exit() { }
    }

    private class AttackState : IEnemyState
    {
        private MiniBossAI boss;
        private AttackConfig config;
        private HashSet<Transform> hitPlayersThisAttack = new HashSet<Transform>();

        public AttackState(MiniBossAI boss) { this.boss = boss; }

        public void Enter()
        {
            hitPlayersThisAttack.Clear();
            boss.hasDealtDamage = false;

            // BẢO ĐẢM 100%: Lập tức quay mặt chính diện thẳng 100% về phía Player đang đuổi theo ngay khi bắt đầu vung kiếm!
            if (boss.targetPlayer != null)
            {
                boss.FaceTargetImmediately(boss.targetPlayer.position);
            }

            // TẢN NHỊP ĐÁNH LIÊN HOÀN 1-2-3 GIỮA TRÙM PHỤ VÀ PHÂN THÂN
            var allBosses = Object.FindObjectsByType<MiniBossAI>(FindObjectsSortMode.None);
            foreach (var b in allBosses)
            {
                if (b != null && b != boss && b.isClone && b.CurrentStateValue == MiniBossState.Chase)
                {
                    b.attackCooldownTimer = Mathf.Max(b.attackCooldownTimer, 0.4f);
                }
            }

            if (!boss.isStandaloneMode)
            {
                boss.attackTypeSync.Value = boss.currentAttackIndex;
                boss.attackCounter.Value++;
            }
            else if (boss.anim != null)
            {
                boss.anim.SetTrigger(boss.attackTriggers[boss.currentAttackIndex]);
            }

            config = boss.attackConfigs[boss.currentAttackIndex];
            boss.stateTimer = config.duration;

            boss.isLeaping = true;
            boss.leapTimer = - (config.duration * config.leapStartPercent);
            boss.currentLeapDuration = config.duration * config.leapDurationPercent;
            boss.currentLeapForwardSpeed = config.forwardSpeed;
            boss.currentLeapHeight = config.peakHeight;

            boss.currentAttackIndex = (boss.currentAttackIndex + 1) % boss.attackConfigs.Length;
        }

        public void Update()
        {
            boss.HandleAttack();

            float elapsed = config.duration - boss.stateTimer;
            float percent = Mathf.Clamp01(elapsed / config.duration);

            if (percent >= config.damageStartPercent && percent <= config.damageEndPercent)
            {
                boss.DealSwordDamageContinuously(hitPlayersThisAttack);
            }
        }

        public void Exit()
        {
            boss.isLeaping = false;
            if (boss.visualRoot != null) boss.visualRoot.localPosition = Vector3.zero;
        }
    }

    private class HitState : IEnemyState
    {
        private MiniBossAI boss;
        public HitState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.stateTimer = boss.hitStaggerDuration;
        }
        public void Update() { boss.HandleHit(); }
        public void Exit() { }
    }

    private class EnrageState : IEnemyState
    {
        private MiniBossAI boss;
        public EnrageState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.stateTimer = boss.enrageDuration;
            if (boss.isStandaloneMode && boss.anim != null)
            {
                boss.anim.SetTrigger(boss.enrageTrigger);
                boss.PlayEnrageVFX();
            }
        }
        public void Update() { boss.HandleEnrage(); }
        public void Exit() { }
    }

    private class DeadState : IEnemyState
    {
        private MiniBossAI boss;
        public DeadState(MiniBossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.Die();
        }
        public void Update() { }
        public void Exit() { }
    }
}

public class CloneWorldHealthBarFallback : MonoBehaviour
{
    public MiniBossAI miniBoss;
    public RectTransform fillRect;
    private Camera mainCam;

    private void Update()
    {
        if (mainCam == null || !mainCam.gameObject.activeInHierarchy) mainCam = Camera.main;
        if (mainCam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
        }

        if (miniBoss != null && fillRect != null)
        {
            float hpRatio = Mathf.Clamp01(miniBoss.ActualCurrentHealth / Mathf.Max(miniBoss.maxHealth, 1f));
            fillRect.anchorMax = new Vector2(hpRatio, 1f);
        }
    }
}
