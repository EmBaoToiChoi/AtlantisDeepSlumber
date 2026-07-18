using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Boss AI Script sử dụng kĩ thuật FSM (Finite State Machine).
/// Có các trạng thái: Idle (DILE / Tự động đi tuần), Chase (Run / Đuổi theo), Attack (attackbth / Chém thường), Kick (Da / Đá văng), Hit (anhit / Choáng), Enrage (Gồng cuồng nộ), Dead.
/// Tối ưu hóa chuyển động: 
///   - Khi đứng yên (Idle) sẽ tự động đi dạo xung quanh (Walk animation, Speed = 0.5f).
///   - Khi cận chiến đang hồi chiêu (Cooldown), Boss sẽ tự động đi vòng quanh di chuyển liên tục (Strafe/Orbit) xung quanh player để tạo thế trận thông minh.
///   - Có khả năng né đòn (Dodge) phản xạ nhanh khi bị nhận sát thương.
/// Cơ chế Phase 2 (Gồng cuồng nộ):
///   - Khi hết 100% máu lần đầu, Boss chuyển sang trạng thái Enrage (gồng nộ), bất tử trong lúc gồng nộ.
///   - Máu hồi lại 100% theo lượng máu của Phase 2 (tự động cập nhật thanh máu UI).
///   - Tăng tốc độ di chuyển, tăng sát thương và toàn bộ tốc độ đánh hoạt ảnh (anim.speed) nhanh hơn.
///   - Kích hoạt thêm combo chém mới: hoạt ảnh "attack2" (50% tỉ lệ chém combo này trong Phase 2).
/// Hỗ trợ cả chế độ mạng (Netcode) và offline (Standalone).
/// </summary>
public class BossAI : NetworkBehaviour
{
    public enum BossState { Idle, Chase, Attack, Kick, Hit, Enrage, Dead }

    // ─── Máu Boss ──────────────────────────────────────────────
    [Header("Health")]
    public float maxHealth = 1000f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        1000f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ─── Thiết lập Phase 2 ─────────────────────────────────────
    [Header("Phase 2 Settings (Enrage)")]
    [Tooltip("Lượng máu tối đa của Boss ở Phase 2 (có thể tùy chỉnh)")]
    public float phase2MaxHealth = 1500f;
    [Tooltip("Hệ số nhân sát thương ở Phase 2")]
    public float phase2DamageMultiplier = 1.3f;
    [Tooltip("Hệ số nhân tốc độ di chuyển ở Phase 2")]
    public float phase2SpeedMultiplier = 1.25f;
    [Tooltip("Tốc độ hoạt ảnh Animator của Boss ở Phase 2 (giúp đánh nhanh hơn toàn bộ)")]
    public float phase2AnimSpeed = 1.4f;
    [Tooltip("Thời gian gồng nộ cuồng nộ (giây)")]
    public float enrageDuration = 3f;
    [Tooltip("Hiệu ứng kỹ xảo (VFX) khi Boss gồng nộ")]
    public GameObject enrageVFXPrefab;
    public Transform enrageVFXSpawnPoint;

    // ─── Thiết lập Triệu Hồi Đá (Earth Summon) ───────────────────
    [Header("Earth Summon Settings")]
    public GameObject warningDecalPrefab;
    public GameObject earthBlastPrefab;
    public float warningDuration = 1.5f;
    public float earthBlastRadius = 3.0f;
    public float earthBlastDamage = 50f;
    public float earthBlastKnockback = 15f;
    public string earthSummonTrigger = "EarthSummon";
    public float earthSummonInterval = 5f;
    public float earthBlastScale = 5.0f; // Scale đá to hơn (mặc định FinalBoss là 3.0f)
    private float earthSummonCooldownTimer;
    private bool isCastingEarthSummon = false;

    // ─── Thiết lập Triệu Hồi Quái Con (Minion Summon) ────────────
    [Header("Minion Spawning Settings")]
    [Tooltip("Prefab của quái con để triệu hồi. Bắt buộc có NetworkObject.")]
    public GameObject minionPrefab;
    [Tooltip("Bán kính ngẫu nhiên xung quanh Boss AI để sinh quái con")]
    public float minionSpawnRadius = 6.0f;
    [Tooltip("Giản cách thời gian triệu hồi quái con (giây)")]
    public float minionSummonInterval = 10f;
    [Tooltip("Giới hạn số lượng quái con tối đa được sinh ra còn sống đồng thời")]
    public int maxMinionsAlive = 10;
    private float minionSummonTimer;
    private List<GameObject> activeMinions = new List<GameObject>();

    // ─── Đồng bộ trạng thái FSM qua mạng ───────────────────────
    [Header("Network State Sync")]
    public NetworkVariable<BossState> currentState = new NetworkVariable<BossState>(
        BossState.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isPhase2Network = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isBossActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ─── Đồng bộ hoạt ảnh Animator qua mạng ─────────────────────
    [Header("Network Anim Sync")]
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attackCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> attack2Counter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> kickCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> enrageCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> earthSummonCounter = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ─── Biến Fallback dùng khi chạy Offline/Editor ──────────────
    private float localHealth;
    private BossState localState = BossState.Idle;
    private bool localIsPhase2 = false;
    private bool localIsBossActive = false;
    private bool hasEnraged = false; // Tránh gồng nộ nhiều lần
    private bool isStandaloneMode;
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private BossState CurrentStateValue
    {
        get => isStandaloneMode ? localState : currentState.Value;
        set { if (isStandaloneMode) localState = value; else currentState.Value = value; }
    }

    private float CurrentHealthValue
    {
        get => isStandaloneMode ? localHealth : currentHealth.Value;
        set { if (isStandaloneMode) localHealth = value; else currentHealth.Value = value; }
    }

    public bool IsPhase2 => isStandaloneMode ? localIsPhase2 : isPhase2Network.Value;
    public bool IsBossActive => isStandaloneMode ? localIsBossActive : isBossActive.Value;
    public bool IsDead => CurrentStateValue == BossState.Dead;

    /// <summary>HP hiện tại đúng trong cả Standalone lẫn Network mode — dùng cho HP bar polling.</summary>
    public float ActualCurrentHealth => (isStandaloneMode || !IsSpawned || !IsServer) ? localHealth : currentHealth.Value;

    public void ActivateBoss()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        if (isStandaloneMode)
            localIsBossActive = true;
        else
            isBossActive.Value = true;

        Debug.Log("[BossAI] Boss kích hoạt trận chiến!");
    }

    // ─── Thành phần chính (Components) ─────────────────────────
    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;

    // ─── Tốc độ di chuyển ──────────────────────────────────────
    [Header("Movement Speeds")]
    public float patrolWalkSpeed = 2f;    // Tốc độ đi dạo tự động (Walk animation, Speed = 0.5f)
    public float chaseRunSpeed = 5.5f;     // Tốc độ đuổi theo chạy (Run animation, Speed = 1f)
    public float orbitSpeed = 3f;          // Tốc độ di chuyển vòng quanh (Walk animation, Speed = 0.5f)
    public float dodgeSpeed = 12f;         // Tốc độ nhảy né tránh (Dodge, Speed = 1f)

    // ─── Cấu hình phát hiện mục tiêu ──────────────────────────
    [Header("AI Vision Settings")]
    public float sightRange = 18f;
    public float fieldOfView = 140f;
    public float attackRange = 3f;

    // ─── Cấu hình Đá (Da State) ───────────────────────────────
    [Header("Kick Settings (Da State)")]
    [Tooltip("Khoảng cách kích hoạt đá (Da)")]
    public float kickDetectRange = 2f;
    [Tooltip("Góc trước mặt để kích hoạt đá (Da)")]
    public float kickDetectAngle = 60f;
    public float kickDamage = 25f;
    [Tooltip("Lực văng cực mạnh khi bị dính đá")]
    public float kickKnockbackForce = 22f;
    public float kickCooldown = 5f;
    [Tooltip("Thời gian người chơi bị choáng/khóa điều khiển sau khi dính đá")]
    public float kickStunDuration = 1.5f;
    private float kickCooldownTimer;

    // ─── Cấu hình Kiếm và Canh chính xác frame ──────────────────
    [Header("Weapon Settings (Sword Attack)")]
    [Tooltip("Gốc thanh kiếm (thường là tay hoặc chuôi kiếm)")]
    public Transform swordBase;
    [Tooltip("Đỉnh thanh kiếm (mũi kiếm)")]
    public Transform swordTip;
    [Tooltip("Độ rộng của tia Raycast/SphereCast chém")]
    public float swordThickness = 0.45f;
    public float attackDamage = 40f;
    public float attackKnockbackForce = 7f;
    public float attackCooldown = 2.5f;
    private float attackCooldownTimer;

    [Header("Weapon Settings 2 (Attack2 Combo)")]
    [Tooltip("Thời gian hoạt ảnh chém combo 2")]
    public float attack2Duration = 1.6f;

    // ─── Các Layer va chạm ─────────────────────────────────────
    [Header("Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    // ─── Tên Tham Số trong Animator ──────────────────────────────
    [Header("Animator Parameter Names")]
    public string speedParam = "Speed";       // Float: 0 = DILE, 0.5 = Walk, 1 = Run
    public string attackTrigger = "AttackBth"; // Trigger: sang attackbth (chém combo 1)
    public string attack2Trigger = "Attack2";  // Trigger: sang attack2 (chém combo 2)
    public string kickTrigger = "Da";          // Trigger: sang Da
    public string hitTrigger = "AnHit";        // Trigger: sang anhit
    public string enrageTrigger = "Enrage";    // Trigger: sang gồng nộ cuồng nộ
    public string dieTrigger = "Die";          // Trigger: sang Die (nếu có)

    // ─── Quản lý các trạng thái FSM ────────────────────────────
    private IEnemyState currentFSMState;
    private IdleState idleState;
    private ChaseState chaseState;
    private AttackState attackState;
    private KickState kickState;
    private HitState hitState;
    private EnrageState enrageState;
    private DeadState deadState;

    private Transform targetPlayer;
    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] hitResults = new Collider[8];

    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    // ─── Các biến phục vụ độ thông minh và di chuyển ───────────
    private float stateTimer;
    private float hitStaggerDuration = 0.55f;
    private float attackDuration = 1.4f; 
    private float kickDuration = 1.1f;   
    private bool hasDealtAttackDamage;
    private bool hasDealtKickDamage;

    // Tuần tra tự động (Wander)
    private float wanderWaitTimer;
    private Vector3 wanderDestination;
    private bool hasWanderDestination;

    // Di chuyển vòng quanh mục tiêu (Orbiting)
    private float orbitTimer;
    private float orbitDirection = 1f; // 1 = sang phải, -1 = sang trái
    private Vector3 orbitDestination;
    private bool isOrbiting;

    // Nhảy né tránh (Dodge)
    private bool isDodging;
    private float dodgeTimer;

    // Tần suất quét AI nhạy bén
    private float aiUpdateInterval = 0.1f;
    private float aiUpdateTimer;

    private void Awake()
    {
        gameObject.tag = "Enemy";
        if (anim == null) anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        var na = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (na != null)
        {
            if (anim != null) na.Animator = anim;
            if (anim == null || anim.runtimeAnimatorController == null) na.enabled = false;
        }

        // Khởi tạo các State cho FSM
        localHealth = maxHealth;
        idleState = new IdleState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);
        kickState = new KickState(this);
        hitState = new HitState(this);
        enrageState = new EnrageState(this);
        deadState = new DeadState(this);
    }

    private void Start()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        earthSummonCooldownTimer = earthSummonInterval;
        minionSummonTimer = minionSummonInterval;
 
        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            InitStandalone();
        }
    }

    private void InitStandalone()
    {
        localHealth = maxHealth;
        SnapToNavMesh();
        ApplySpeedAnim(0f);
        ChangeState(BossState.Idle);
    }

    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null)
        {
            nt.PositionThreshold = 0.001f;
            nt.RotAngleThreshold = 0.01f;
            nt.ScaleThreshold = 0.01f;
        }

        // Đăng ký đồng bộ hoạt ảnh từ Server xuống Client
        netSpeed.OnValueChanged += (_, v) => ApplySpeedAnim(v);
        hitCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        attackCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(attackTrigger); };
        attack2Counter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(attack2Trigger); };
        kickCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(kickTrigger); };
        enrageCounter.OnValueChanged += OnEnrageCounterChanged;
        currentHealth.OnValueChanged += OnHealthNetChanged;
        earthSummonCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(earthSummonTrigger); };

        // Lắng nghe sự kiện gồng nộ đồng bộ hóa của Client
        isPhase2Network.OnValueChanged += (oldVal, newVal) =>
        {
            if (newVal)
            {
                maxHealth = phase2MaxHealth;
                if (anim != null) anim.speed = phase2AnimSpeed;
                Debug.Log("[BossAI Client] Đã đồng bộ sang Phase 2 (Cuồng Nộ)!");
            }
        };

        ApplySpeedAnim(netSpeed.Value);

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            SnapToNavMesh();
            ChangeState(BossState.Idle);
        }
        else
        {
            if (agent != null) agent.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        netSpeed.OnValueChanged -= (_, v) => ApplySpeedAnim(v);
        hitCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(hitTrigger); };
        attackCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(attackTrigger); };
        attack2Counter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(attack2Trigger); };
        kickCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(kickTrigger); };
        enrageCounter.OnValueChanged -= OnEnrageCounterChanged;
        currentHealth.OnValueChanged -= OnHealthNetChanged;
        earthSummonCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(earthSummonTrigger); };
    }

    private void OnHealthNetChanged(float oldVal, float newVal)
    {
        float diff = oldVal - newVal;
        if (diff > 0)
        {
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, diff);
        }
    }

    private void Update()
    {
        bool aiAuth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!aiAuth) return;

        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh) SnapToNavMesh();

        // --- BỘ GIẢI QUYẾT VA CHẠM TRÁNH XUYÊN TƯỜNG (WALL COLLISION RESOLVER) ---
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            Vector3 moveDir = agent.velocity.normalized;
            if (Physics.Raycast(rayOrigin, moveDir, out RaycastHit hit, 0.8f))
            {
                if (!hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy") && hit.collider.gameObject.layer != LayerMask.NameToLayer("Enemy") && !hit.collider.isTrigger)
                {
                    Vector3 pushBack = hit.normal * 0.15f;
                    agent.Warp(transform.position + pushBack);
                }
            }
        }

        // Giảm thời gian hồi chiêu
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (kickCooldownTimer > 0) kickCooldownTimer -= Time.deltaTime;

        if (IsBossActive && !IsDead && targetPlayer != null)
        {
            earthSummonCooldownTimer -= Time.deltaTime;
            if (earthSummonCooldownTimer <= 0)
            {
                earthSummonCooldownTimer = earthSummonInterval;
                TriggerEarthSummon();
            }
 
            minionSummonTimer -= Time.deltaTime;
            if (minionSummonTimer <= 0)
            {
                minionSummonTimer = minionSummonInterval;
                SummonMinions();
            }
        }

        // Xử lý đếm thời gian né đòn (Dodge)
        if (isDodging)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0)
            {
                isDodging = false;
                if (AgentReady)
                {
                    agent.speed = IsPhase2 ? (chaseRunSpeed * phase2SpeedMultiplier) : chaseRunSpeed;
                }
            }
        }

        // Tần suất quét phát hiện người chơi (nhạy bén hơn)
        aiUpdateTimer -= Time.deltaTime;
        if (aiUpdateTimer <= 0)
        {
            aiUpdateTimer = aiUpdateInterval;
            DetectAndSwitchTarget();
        }

        // Cập nhật FSM hiện tại
        if (currentFSMState != null)
        {
            if (isCastingEarthSummon)
            {
                if (AgentReady)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                SetSpeedNet(0f);
                if (targetPlayer != null)
                {
                    RotateTowards(targetPlayer.position);
                }
            }
            else
            {
                currentFSMState.Update();
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  HÀNH VI CÁC TRẠNG THÁI (FSM ACTIONS)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Trạng thái Idle: Khi không phát hiện player, Boss sẽ đi tuần dạo xung quanh (Walk).
    /// </summary>
    private void HandleIdle()
    {
        if (targetPlayer != null)
        {
            hasWanderDestination = false;
            ChangeState(BossState.Chase);
            return;
        }

        // Tự động di chuyển đi tuần (Wander) xung quanh để tuần tra
        if (!hasWanderDestination)
        {
            wanderWaitTimer -= Time.deltaTime;
            if (wanderWaitTimer <= 0)
            {
                // Chọn ngẫu nhiên một điểm NavMesh trong phạm vi 10m
                Vector3 randomDirection = Random.insideUnitSphere * 10f + transform.position;
                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, 10f, NavMesh.AllAreas))
                {
                    wanderDestination = navHit.position;
                    hasWanderDestination = true;
                    if (AgentReady)
                    {
                        agent.isStopped = false;
                        agent.speed = patrolWalkSpeed;
                        agent.SetDestination(wanderDestination);
                    }
                }
            }
            SetSpeedNet(0f); // Đứng yên
        }
        else
        {
            // Đang đi bộ tuần tra tự động (Walk animation, Speed = 0.5f)
            SetSpeedNet(0.5f);

            if (AgentReady)
            {
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                {
                    hasWanderDestination = false;
                    wanderWaitTimer = Random.Range(1.5f, 3.5f); // Nghỉ một chút rồi đi tiếp
                }
            }
            else
            {
                hasWanderDestination = false;
            }
        }
    }

    /// <summary>
    /// Trạng thái Đuổi theo (Chase):
    ///   - Nếu ở ngoài tầm chém, chạy nhanh tới player (Run, Speed = 1f).
    ///   - Nếu trong tầm chém nhưng chém đang hồi chiêu (Cooldown), tự động đi vòng quanh (Orbit, Speed = 0.5f) để nhấp nhả thông minh.
    /// </summary>
    private void HandleChase()
    {
        if (targetPlayer == null || IsPlayerDeadOrInvisible(targetPlayer))
        {
            targetPlayer = null;
            isOrbiting = false;
            ChangeState(BossState.Idle);
            return;
        }

        // 1. Nếu có người chơi tiếp cận vùng đá (Da) trước mặt và đá đã hồi chiêu -> Đá văng ra xa
        if (kickCooldownTimer <= 0 && IsPlayerInKickZone(targetPlayer))
        {
            isOrbiting = false;
            ChangeState(BossState.Kick);
            return;
        }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);

        // 2. Nếu đã áp sát trong tầm đánh kiếm và chiêu chém sẵn sàng -> Chém ngay
        if (dist <= attackRange && attackCooldownTimer <= 0)
        {
            isOrbiting = false;
            if (AgentReady) agent.isStopped = true;
            SetSpeedNet(0f);
            ChangeState(BossState.Attack);
            return;
        }

        // 3. NẾU chiêu đang hồi chiêu (Attack Cooldown) và Boss đang ở gần Player:
        //    -> Di chuyển vòng quanh mục tiêu (Orbiting) bằng hoạt ảnh WALK (0.5f) hoặc CHẠY ÁP SÁT.
        //    -> Ở Phase 2 (Cuồng Nộ), Boss KHÔNG bao giờ đi bộ vòng quanh đần đần nữa, mà chạy thẳng áp sát áp lực liên tục!
        if (attackCooldownTimer > 0 && dist <= attackRange + 3f && !isDodging)
        {
            bool shouldOrbit = !IsPhase2 && (Random.value < 0.3f || isOrbiting); // Chỉ 30% cơ hội đi bộ vòng quanh ở Phase 1

            if (shouldOrbit)
            {
                if (!isOrbiting || orbitTimer <= 0)
                {
                    // Chọn một góc ngẫu nhiên bên trái hoặc phải xung quanh player
                    orbitDirection = Random.value < 0.5f ? -1f : 1f;
                    orbitTimer = Random.Range(1f, 2.5f);

                    // Tính toán điểm di chuyển tiếp tuyến xung quanh player
                    Vector3 toBossDir = (transform.position - targetPlayer.position).normalized;
                    Vector3 tangent = new Vector3(-toBossDir.z, 0, toBossDir.x) * orbitDirection;
                    Vector3 targetOrbitPos = targetPlayer.position + (toBossDir * (attackRange - 0.5f)) + (tangent * 2f); // Áp sát hơn một chút

                    if (NavMesh.SamplePosition(targetOrbitPos, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
                    {
                        orbitDestination = navHit.position;
                        isOrbiting = true;
                        if (AgentReady)
                        {
                            agent.isStopped = false;
                            agent.speed = orbitSpeed;
                            agent.SetDestination(orbitDestination);
                        }
                    }
                }

                orbitTimer -= Time.deltaTime;

                if (AgentReady)
                {
                    // Vừa di chuyển vòng quanh vừa đi bộ (Walk animation, Speed = 0.5f)
                    SetSpeedNet(0.5f);

                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                    {
                        isOrbiting = false; // Chọn điểm di chuyển vòng quanh mới
                    }
                }
                RotateTowards(targetPlayer.position);
            }
            else
            {
                // Nếu không đi vòng quanh: Chạy thẳng áp sát đè áp lực lên Player!
                isOrbiting = false;
                if (AgentReady)
                {
                    agent.isStopped = false;
                    agent.speed = IsPhase2 ? (chaseRunSpeed * phase2SpeedMultiplier) : chaseRunSpeed;
                    
                    // Điểm áp sát sát sườn player
                    Vector3 targetPos = targetPlayer.position;
                    agent.SetDestination(targetPos);
                    
                    // Nếu đã đứng siêu sát (trong tầm đánh) -> Đứng yên chờ cooldown nhưng vẫn hướng mặt về Player
                    if (dist <= attackRange - 0.2f)
                    {
                        agent.isStopped = true;
                        SetSpeedNet(0f); // Idle
                    }
                    else
                    {
                        SetSpeedNet(IsPhase2 ? 1f : 0.5f); // Phase 2 chạy, Phase 1 đi bộ áp sát
                    }
                }
                RotateTowards(targetPlayer.position);
            }
        }
        else if (!isDodging)
        {
            // 4. Nếu ở xa mục tiêu -> Chạy nhanh đuổi theo (Run animation, Speed = 1f)
            isOrbiting = false;
            if (AgentReady)
            {
                agent.isStopped = false;
                agent.speed = IsPhase2 ? (chaseRunSpeed * phase2SpeedMultiplier) : chaseRunSpeed;
                agent.SetDestination(targetPlayer.position);
            }
            SetSpeedNet(AgentReady && !agent.isStopped ? 1f : 0f);
            RotateTowards(targetPlayer.position);
        }
        else
        {
            // Đang nhảy né đòn (Dodge)
            SetSpeedNet(1f); // Giữ hoạt ảnh chạy nhanh
            RotateTowards(targetPlayer.position);
        }
    }

    private void HandleAttack()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        stateTimer -= Time.deltaTime;

        // Xoay mặt hướng về player ở nửa đầu của hoạt ảnh vung tay để canh chém chính xác hướng
        if (targetPlayer != null && stateTimer > (IsPhase2 ? attack2Duration : attackDuration) * 0.5f)
        {
            RotateTowards(targetPlayer.position);
        }

        if (stateTimer <= 0)
        {
            // Cooldown ở Phase 2 nhanh hơn (giảm thời gian hồi chiêu)
            attackCooldownTimer = IsPhase2 ? (attackCooldown * 0.65f) : attackCooldown;

            if (targetPlayer != null) ChangeState(BossState.Chase);
            else ChangeState(BossState.Idle);
        }
    }

    private void HandleKick()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        stateTimer -= Time.deltaTime;

        // Xoay mặt hướng về player ở nửa đầu của hoạt ảnh đá để canh chuẩn xác hướng
        if (targetPlayer != null && stateTimer > kickDuration * 0.5f)
        {
            RotateTowards(targetPlayer.position);
        }

        if (stateTimer <= 0)
        {
            kickCooldownTimer = IsPhase2 ? (kickCooldown * 0.65f) : kickCooldown;

            if (targetPlayer != null) ChangeState(BossState.Chase);
            else ChangeState(BossState.Idle);
        }
    }

    private void HandleHit()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            if (targetPlayer != null) ChangeState(BossState.Chase);
            else ChangeState(BossState.Idle);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PHÁT HIỆN MỤC TIÊU & KIỂM TRA PHẠM VI (DETECTION)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Quét nhạy bén phát hiện người chơi gần nhất để đuổi theo hoặc đổi mục tiêu nếu có người chơi khác gây nguy hại hơn.
    /// </summary>
    private System.Collections.Generic.List<Transform> GetAllActivePlayers()
    {
        var list = new System.Collections.Generic.List<Transform>();

        // Tìm trực tiếp các Class Player cụ thể kể cả các lớp con (Cực kỳ tối ưu và bỏ qua sai sót về Tag/Layer trên Editor)
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

        Transform closest = null;
        float minD = float.MaxValue;
        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        // Loại trừ Layer "Player" và "Enemy" khỏi bộ lọc vật cản (Raycast) để tránh việc Raycast tự va chạm vào chính thân thể Player/Boss rồi nghĩ là bị che mắt
        int raycastMask = obstacleLayer.value & ~LayerMask.GetMask("Player", "Enemy");

        // KHÓA MỤC TIÊU ƯU TIÊN: Nếu đang có mục tiêu và mục tiêu đó vẫn hợp lệ thì tiếp tục dí mục tiêu đó, không đổi người gần hơn
        if (targetPlayer != null)
        {
            if (!IsPlayerDeadOrInvisible(targetPlayer))
            {
                Vector3 targetCenter = targetPlayer.position + Vector3.up * 1.0f;
                float d = Vector3.Distance(eyePos, targetCenter);
                if (d <= sightRange)
                {
                    Vector3 dir = (targetCenter - eyePos).normalized;
                    if (!Physics.Raycast(eyePos, dir, d, raycastMask))
                    {
                        return; // Khóa mục tiêu thành công!
                    }
                }
            }
        }

        var activePlayers = GetAllActivePlayers();
        for (int i = 0; i < activePlayers.Count; i++)
        {
            Transform pTrans = activePlayers[i];
            if (pTrans == null || pTrans == transform) continue;

            if (IsPlayerDeadOrInvisible(pTrans)) continue;

            // Tính khoảng cách từ mắt Boss tới tâm ngực Player (cao hơn 1m so với chân) để tránh việc Raycast bị chạm đất/nền sàn làm mất mục tiêu ở cự ly gần!
            Vector3 targetCenter = pTrans.position + Vector3.up * 1.0f;
            float d = Vector3.Distance(eyePos, targetCenter);
            
            // Chỉ kiểm tra tầm nhìn khi ở trong khoảng cách sightRange
            if (d <= sightRange)
            {
                Vector3 dir = (targetCenter - eyePos).normalized;
                
                // Tầm quét cực nhạy: 360 độ cự ly gần (4m) hoặc góc quạt FOV rộng ở cự ly xa
                bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f || d <= 4f;

                if (inFOV && !Physics.Raycast(eyePos, dir, d, raycastMask))
                {
                    if (d < minD)
                    {
                        minD = d;
                        closest = pTrans;
                    }
                }
            }
        }

        // Tự động chuyển đổi mục tiêu thông minh sang player gần nhất
        if (closest != null)
        {
            targetPlayer = closest;
        }
        else if (CurrentStateValue == BossState.Chase)
        {
            targetPlayer = null;
            isOrbiting = false;
        }
    }

    private bool IsPlayerDeadOrInvisible(Transform player)
    {
        var ps = player.GetComponentInParent<IPlayerHUDTarget>();
        var sk = player.GetComponentInParent<Skeleton>();
        return (ps != null && (ps.CurrentHealth <= 0 || ps.IsInvisible)) || (sk != null && sk.CurrentHealthValue <= 0);
    }

    private bool IsPlayerInKickZone(Transform player)
    {
        Vector3 diff = player.position - transform.position;
        float d = diff.magnitude;
        if (d > kickDetectRange) return false;

        // Bỏ qua độ cao Y để tính góc nón chính xác trên mặt phẳng ngang
        Vector3 horizDiff = new Vector3(diff.x, 0, diff.z).normalized;
        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        float angle = Vector3.Angle(forward, horizDiff);

        // Nằm trong góc phát hiện (ở trước mặt)
        return angle <= kickDetectAngle / 2f;
    }

    // ══════════════════════════════════════════════════════════
    //  QUÉT RAYCAST VÀ TÍNH DAMAGE CHÍNH XÁC FRAME (DAMAGE)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Hàm này được thiết kế để gọi chính xác tại frame vung tay thông qua Animation Event (tên event: OnSwordSwing).
    /// </summary>
    public void OnSwordSwing()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth || hasDealtAttackDamage) return;
        hasDealtAttackDamage = true;

        DealSwordDamage1();
    }

    private void DealSwordDamage1()
    {
        HashSet<Transform> hitPlayers = new HashSet<Transform>();
        float finalDamage = IsPhase2 ? (attackDamage * phase2DamageMultiplier) : attackDamage;

        // 1. Quét tia Raycast/SphereCast từ gốc kiếm (swordBase) đến đỉnh kiếm (swordTip) để lấy chính xác đường kiếm đi qua
        if (swordBase != null && swordTip != null)
        {
            Vector3 start = swordBase.position;
            Vector3 end = swordTip.position;
            Vector3 dir = (end - start).normalized;
            float dist = Vector3.Distance(start, end);

            RaycastHit[] hits = Physics.SphereCastAll(start, swordThickness, dir, dist, playerLayer);
            foreach (var hit in hits)
            {
                Transform pTrans = hit.collider.transform;
                Transform root = GetPlayerRoot(pTrans);
                if (root != null && !hitPlayers.Contains(root))
                {
                    hitPlayers.Add(root);
                    if (hitPlayers.Count >= 4) break; // Giới hạn tối đa trúng 4 player cùng lúc
                }
            }
        }
        else
        {
            // Fallback nếu không thiết lập kiếm trong Inspector: Quét hình quạt 120 độ trước mặt
            int num = Physics.OverlapSphereNonAlloc(transform.position + transform.forward * (attackRange / 2f), attackRange, hitResults, playerLayer);
            for (int i = 0; i < num; i++)
            {
                if (hitResults[i] == null) continue;
                Transform root = GetPlayerRoot(hitResults[i].transform);
                if (root != null && !hitPlayers.Contains(root))
                {
                    Vector3 diff = root.position - transform.position;
                    float angle = Vector3.Angle(transform.forward, new Vector3(diff.x, 0, diff.z).normalized);
                    if (angle <= 65f)
                    {
                        hitPlayers.Add(root);
                        if (hitPlayers.Count >= 4) break;
                    }
                }
            }
        }

        // 2. Tính damage và đẩy lùi nhẹ lên tối đa 4 player bị dính kiếm
        foreach (var p in hitPlayers)
        {
            Vector3 knockbackDir = (p.position - transform.position);
            knockbackDir.y = 0;
            knockbackDir = knockbackDir.normalized;
            Vector3 knockbackForceVector = knockbackDir * attackKnockbackForce;

            EnemyDamageHelper.DealDamage(p, finalDamage, knockbackForceVector);
            Debug.Log($"[BossAI] Kiếm chém thường trúng player: {p.name}, gây {finalDamage} sát thương.");
        }
    }

    /// <summary>
    /// Kích hoạt sát thương cho đòn chém Combo 2 (đặt trong Animation Event: OnSwordSwing2).
    /// </summary>
    public void OnSwordSwing2()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth || hasDealtAttackDamage) return;
        hasDealtAttackDamage = true;

        DealSwordDamage2();
    }

    private void DealSwordDamage2()
    {
        HashSet<Transform> hitPlayers = new HashSet<Transform>();
        float finalDamage = attackDamage * phase2DamageMultiplier * 1.2f; // Đòn chém combo 2 mạnh hơn nữa!
        float finalKnockback = attackKnockbackForce * 1.5f;

        if (swordBase != null && swordTip != null)
        {
            Vector3 start = swordBase.position;
            Vector3 end = swordTip.position;
            Vector3 dir = (end - start).normalized;
            float dist = Vector3.Distance(start, end);

            // Quét tia chém rộng hơn một chút cho combo 2
            RaycastHit[] hits = Physics.SphereCastAll(start, swordThickness * 1.25f, dir, dist, playerLayer);
            foreach (var hit in hits)
            {
                Transform pTrans = hit.collider.transform;
                Transform root = GetPlayerRoot(pTrans);
                if (root != null && !hitPlayers.Contains(root))
                {
                    hitPlayers.Add(root);
                    if (hitPlayers.Count >= 4) break;
                }
            }
        }
        else
        {
            int num = Physics.OverlapSphereNonAlloc(transform.position + transform.forward * (attackRange / 2f), attackRange + 0.6f, hitResults, playerLayer);
            for (int i = 0; i < num; i++)
            {
                if (hitResults[i] == null) continue;
                Transform root = GetPlayerRoot(hitResults[i].transform);
                if (root != null && !hitPlayers.Contains(root))
                {
                    Vector3 diff = root.position - transform.position;
                    float angle = Vector3.Angle(transform.forward, new Vector3(diff.x, 0, diff.z).normalized);
                    if (angle <= 75f)
                    {
                        hitPlayers.Add(root);
                        if (hitPlayers.Count >= 4) break;
                    }
                }
            }
        }

        foreach (var p in hitPlayers)
        {
            Vector3 knockbackDir = (p.position - transform.position);
            knockbackDir.y = 0.1f; // Chém hất tung nhẹ
            knockbackDir = knockbackDir.normalized;
            Vector3 knockbackForceVector = knockbackDir * finalKnockback;

            EnemyDamageHelper.DealDamage(p, finalDamage, knockbackForceVector);
            Debug.Log($"[BossAI] KIẾM COMBO 2 chém trúng player: {p.name}, gây {finalDamage} sát thương.");
        }
    }

    /// <summary>
    /// Hàm này được thiết kế để gọi chính xác tại frame đá chân chạm mục tiêu bằng Animation Event (tên event: OnKickHit).
    /// </summary>
    public void OnKickHit()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth || hasDealtKickDamage) return;
        hasDealtKickDamage = true;

        DealKickDamage();
    }

    private void DealKickDamage()
    {
        // 1. Quét tìm tất cả player nằm trong vùng đá nhỏ trước mặt
        int num = Physics.OverlapSphereNonAlloc(transform.position + transform.forward * (kickDetectRange / 2f), kickDetectRange, hitResults, playerLayer);
        Transform targetToKick = null;
        float minAngle = float.MaxValue;

        for (int i = 0; i < num; i++)
        {
            if (hitResults[i] == null) continue;
            Transform root = GetPlayerRoot(hitResults[i].transform);
            if (root == null) continue;

            if (IsPlayerDeadOrInvisible(root)) continue;

            Vector3 diff = root.position - transform.position;
            float dist = diff.magnitude;
            if (dist <= kickDetectRange)
            {
                float angle = Vector3.Angle(transform.forward, new Vector3(diff.x, 0, diff.z).normalized);
                if (angle <= kickDetectAngle / 2f)
                {
                    if (angle < minAngle)
                    {
                        minAngle = angle;
                        targetToKick = root; // Ưu tiên đá trúng player nằm ngay trực diện trung tâm
                    }
                }
            }
        }

        // 2. Nếu có player dính đá thì áp dụng damage và lực văng cực mạnh ("văng ra xa")
        if (targetToKick != null)
        {
            float finalKickDamage = IsPhase2 ? (kickDamage * phase2DamageMultiplier) : kickDamage;
            float finalKickForce = IsPhase2 ? (kickKnockbackForce * 1.2f) : kickKnockbackForce;

            Vector3 knockbackDir = (targetToKick.position - transform.position);
            knockbackDir.y = 0.3f; // Đẩy bay hếch lên trời một chút để hiệu ứng văng đẹp mắt hơn
            knockbackDir = knockbackDir.normalized;
            Vector3 knockbackForceVector = knockbackDir * finalKickForce;

            EnemyDamageHelper.DealKickDamageWithStun(targetToKick, finalKickDamage, knockbackForceVector, kickStunDuration);
            Debug.Log($"[BossAI] ĐÁ VĂNG Player: {targetToKick.name}, lực đẩy = {finalKickForce}, gây {finalKickDamage} sát thương, khóa {kickStunDuration}s.");
        }
    }

    private Transform GetPlayerRoot(Transform t)
    {
        if (t == null) return null;
        var ps = t.GetComponentInParent<IPlayerHUDTarget>() ?? t.GetComponentInChildren<IPlayerHUDTarget>();
        if (ps != null) return ps.transform;

        var sk = t.GetComponentInParent<Skeleton>() ?? t.GetComponentInChildren<Skeleton>();
        if (sk != null) return sk.transform;

        return null;
    }

    // ══════════════════════════════════════════════════════════
    //  NHẬN SÁT THƯƠNG & XỬ LÝ CHẾT (TAKE DAMAGE & DEAD)
    // ══════════════════════════════════════════════════════════

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        if (CurrentStateValue == BossState.Enrage) return; // Bất tử khi đang gồng nộ

        localHealth = Mathf.Max(0f, localHealth - damage);
        if (!isStandaloneMode && IsSpawned && IsServer)
        {
            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - damage);
            localHealth = currentHealth.Value;
            hitCounter.Value++;
        }
        else if (anim != null)
        {
            anim.SetTrigger(hitTrigger);
        }

        EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);

        float activeHp = ActualCurrentHealth;

        // Xử lý khi hết máu lần đầu (Chuyển sang Phase 2 Gồng Cuồng Nộ)
        if (activeHp <= 0f)
        {
            if (!IsPhase2 && !hasEnraged)
            {
                hasEnraged = true;
                ChangeState(BossState.Enrage);
                return;
            }

            ChangeState(BossState.Dead);
            return;
        }

        // Phản xạ nhảy né đòn (Dodge) nhanh nhạy khi bị tấn công (Phase 1 né 25%, Phase 2 điên cuồng chỉ né 10%)
        float dodgeChance = IsPhase2 ? 0.1f : 0.25f;
        if (!isDodging && Random.value < dodgeChance && CurrentStateValue == BossState.Chase)
        {
            ExecuteDodge();
        }
        else
        {
            // Khi bị dính sát thương thì chuyển sang trạng thái Hit (chạy hoạt ảnh anhit)
            ChangeState(BossState.Hit);
        }
    }

    /// <summary>
    /// Nhận hiệu ứng choáng từ kỹ năng của người chơi (ví dụ Q của Arthur).
    /// </summary>
    public void ApplyStun(float duration)
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;
        if (IsDead) return;

        stateTimer = duration;
        ChangeState(BossState.Hit);
        Debug.Log($"[BossAI] Boss bị choáng (Stun) trong {duration} giây.");
    }

    public void OnSkillEAnimEnd()
    {
        // Dummy animation event receiver to prevent Silas warning
    }

    public void SpawnEnrageVFXLocally()
    {
        if (enrageVFXPrefab != null)
        {
            Vector3 spawnPos = enrageVFXSpawnPoint != null ? enrageVFXSpawnPoint.position : transform.position;
            GameObject vfx = Instantiate(enrageVFXPrefab, spawnPos, transform.rotation);
            Destroy(vfx, 5f);
        }
    }

    private void OnEnrageCounterChanged(int oldVal, int newVal)
    {
        if (newVal > 0)
        {
            if (anim != null) anim.SetTrigger(enrageTrigger);
            SpawnEnrageVFXLocally();
        }
    }

    /// <summary>
    /// Kỹ năng nhảy tránh né (Dodge) cực kỳ nhạy bén giúp Boss nhảy tránh ra bên cạnh/phía sau.
    /// </summary>
    private void ExecuteDodge()
    {
        if (targetPlayer == null || !AgentReady) return;

        // Lấy phương vuông góc để né sang trái hoặc phải ngẫu nhiên
        Vector3 toPlayerDir = (targetPlayer.position - transform.position).normalized;
        Vector3 dodgeDir = new Vector3(-toPlayerDir.z, 0, toPlayerDir.x) * (Random.value < 0.5f ? 1f : -1f);
        
        // Hoặc né lùi ra sau
        if (Random.value < 0.3f)
        {
            dodgeDir = -toPlayerDir;
        }

        Vector3 targetDodgePos = transform.position + dodgeDir * 4.5f;

        if (NavMesh.SamplePosition(targetDodgePos, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
        {
            isDodging = true;
            isOrbiting = false;
            dodgeTimer = 0.4f; // Né trong 0.4s
            agent.isStopped = false;
            agent.speed = dodgeSpeed;
            agent.SetDestination(navHit.position);

            SetSpeedNet(1f); // Kích hoạt chạy nhanh khi né
            Debug.Log("[BossAI] Kích hoạt phản xạ né đòn (Dodge) nhạy bén!");
        }
    }

    private void Die()
    {
        if (AgentReady) agent.isStopped = true;
        SetSpeedNet(0f);

        if (anim != null)
        {
            anim.ResetTrigger(attackTrigger);
            anim.ResetTrigger(attack2Trigger);
            anim.ResetTrigger(kickTrigger);
            anim.ResetTrigger(hitTrigger);
            anim.ResetTrigger(enrageTrigger);
            if (!string.IsNullOrEmpty(dieTrigger)) anim.SetTrigger(dieTrigger);
        }

        // Vô hiệu hóa va chạm để không cản đường
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Invoke(nameof(DespawnBoss), 3.0f);
    }

    private void DespawnBoss()
    {
        if (isStandaloneMode)
        {
            Destroy(gameObject);
        }
        else if (IsServer && IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  FINITE STATE MACHINE (FSM) CLASSES
    // ══════════════════════════════════════════════════════════

    private void ChangeState(BossState newState)
    {
        if (currentFSMState != null)
        {
            currentFSMState.Exit();
        }

        if (newState == BossState.Hit || newState == BossState.Dead || newState == BossState.Enrage)
        {
            isCastingEarthSummon = false;
        }

        CurrentStateValue = newState;

        switch (newState)
        {
            case BossState.Idle:   currentFSMState = idleState;   break;
            case BossState.Chase:  currentFSMState = chaseState;  break;
            case BossState.Attack: currentFSMState = attackState; break;
            case BossState.Kick:   currentFSMState = kickState;   break;
            case BossState.Hit:    currentFSMState = hitState;    break;
            case BossState.Enrage: currentFSMState = enrageState; break;
            case BossState.Dead:   currentFSMState = deadState;   break;
        }

        if (currentFSMState != null)
        {
            currentFSMState.Enter();
        }
    }

    private class IdleState : IEnemyState
    {
        private BossAI boss;
        public IdleState(BossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.SetSpeedNet(0f);
            boss.hasWanderDestination = false;
            boss.wanderWaitTimer = Random.Range(1f, 3f);
        }
        public void Update() { boss.HandleIdle(); }
        public void Exit() {}
    }

    private class ChaseState : IEnemyState
    {
        private BossAI boss;
        public ChaseState(BossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.isOrbiting = false;
            boss.isDodging = false;
        }
        public void Update() { boss.HandleChase(); }
        public void Exit()
        {
            boss.isOrbiting = false;
        }
    }

    private class AttackState : IEnemyState
    {
        private BossAI boss;
        private float fallbackTimer;
        private bool isUsingAttack2;

        public AttackState(BossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.hasDealtAttackDamage = false;

            // Trong Phase 2, có 50% cơ hội tung chém Combo 2 (attack2)
            isUsingAttack2 = boss.IsPhase2 && Random.value < 0.5f;

            if (isUsingAttack2)
            {
                boss.stateTimer = boss.attack2Duration;
                fallbackTimer = boss.attack2Duration * 0.4f;

                if (boss.anim != null) boss.anim.SetTrigger(boss.attack2Trigger);
                if (!boss.isStandaloneMode) boss.attack2Counter.Value++;
            }
            else
            {
                boss.stateTimer = boss.attackDuration;
                fallbackTimer = boss.attackDuration * 0.4f;

                if (boss.anim != null) boss.anim.SetTrigger(boss.attackTrigger);
                if (!boss.isStandaloneMode) boss.attackCounter.Value++;
            }
        }
        public void Update()
        {
            boss.HandleAttack();

            // Cơ chế dự phòng chạy bằng code nếu Animation Event chưa được kích hoạt
            if (!boss.hasDealtAttackDamage)
            {
                fallbackTimer -= Time.deltaTime;
                if (fallbackTimer <= 0)
                {
                    if (isUsingAttack2)
                        boss.OnSwordSwing2();
                    else
                        boss.OnSwordSwing();
                }
            }
        }
        public void Exit() {}
    }

    private class KickState : IEnemyState
    {
        private BossAI boss;
        private float fallbackTimer;
        public KickState(BossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.hasDealtKickDamage = false;
            boss.stateTimer = boss.kickDuration;
            // Dự phòng (fallback) nếu người chơi quên thiết lập Animation Event, tự động đá ở khoảng 45% thời lượng hoạt ảnh
            fallbackTimer = boss.kickDuration * 0.45f;

            if (boss.anim != null) boss.anim.SetTrigger(boss.kickTrigger);
            if (!boss.isStandaloneMode) boss.kickCounter.Value++;
        }
        public void Update()
        {
            boss.HandleKick();

            // Cơ chế dự phòng chạy bằng code nếu Animation Event chưa được kích hoạt
            if (!boss.hasDealtKickDamage)
            {
                fallbackTimer -= Time.deltaTime;
                if (fallbackTimer <= 0)
                {
                    boss.OnKickHit();
                }
            }
        }
        public void Exit() {}
    }

    private class HitState : IEnemyState
    {
        private BossAI boss;
        public HitState(BossAI boss) { this.boss = boss; }
        public void Enter()
        {
            boss.stateTimer = boss.hitStaggerDuration;
            if (boss.anim != null) boss.anim.SetTrigger(boss.hitTrigger);
            if (!boss.isStandaloneMode) boss.hitCounter.Value++;
        }
        public void Update() { boss.HandleHit(); }
        public void Exit() {}
    }

    private class EnrageState : IEnemyState
    {
        private BossAI boss;
        public EnrageState(BossAI boss) { this.boss = boss; }
        public void Enter()
        {
            if (boss.AgentReady) boss.agent.isStopped = true;
            boss.SetSpeedNet(0f);
            boss.stateTimer = boss.enrageDuration;

            // Kích hoạt hoạt ảnh gồng cuồng nộ
            if (boss.anim != null) boss.anim.SetTrigger(boss.enrageTrigger);
            if (!boss.isStandaloneMode) boss.enrageCounter.Value++;

            // Thay đổi máu tối đa và Hồi đầy 100% máu của Phase 2
            boss.maxHealth = boss.phase2MaxHealth;
            boss.CurrentHealthValue = boss.phase2MaxHealth;

            // Bật cờ Phase 2
            if (!boss.isStandaloneMode)
            {
                boss.isPhase2Network.Value = true;
            }
            else
            {
                boss.localIsPhase2 = true;
            }

            // Tăng tốc độ chạy của NavMeshAgent
            if (boss.AgentReady)
            {
                boss.agent.speed = boss.chaseRunSpeed * boss.phase2SpeedMultiplier;
            }

            // Sinh hiệu ứng gồng nộ cuồng nộ (VFX) cục bộ nếu có thiết lập
            if (boss.isStandaloneMode)
            {
                boss.SpawnEnrageVFXLocally();
            }

            Debug.Log($"[BossAI] BOSS hóa CUỒNG NỘ! Hồi {boss.phase2MaxHealth} HP. Tốc độ di chuyển tăng x{boss.phase2SpeedMultiplier}!");
        }

        public void Update()
        {
            boss.stateTimer -= Time.deltaTime;
            if (boss.stateTimer <= 0)
            {
                if (boss.targetPlayer != null) boss.ChangeState(BossState.Chase);
                else boss.ChangeState(BossState.Idle);
            }
        }

        public void Exit()
        {
            // Tăng toàn bộ tốc độ hoạt ảnh của Animator (Đánh nhanh hơn)
            if (boss.anim != null)
            {
                boss.anim.speed = boss.phase2AnimSpeed;
            }
        }
    }

    private class DeadState : IEnemyState
    {
        private BossAI boss;
        public DeadState(BossAI boss) { this.boss = boss; }
        public void Enter() { boss.Die(); }
        public void Update() {}
        public void Exit() {}
    }

    // ─── Earth Summon (Triệu Hồi Đá) Logic ───────────────────────

    private void TriggerEarthSummon()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        isCastingEarthSummon = true; // Đứng im triệu hồi

        if (isStandaloneMode)
        {
            if (anim != null) anim.SetTrigger(earthSummonTrigger);
        }
        else
        {
            earthSummonCounter.Value++;
        }

        Vector3[] spawnPositions = CalculateEarthBlastPositions();

        if (!isStandaloneMode)
        {
            SpawnWarningIndicatorsClientRpc(spawnPositions);
        }
        else
        {
            SpawnWarningIndicatorsLocal(spawnPositions);
        }

        StartCoroutine(EarthBlastRoutine(spawnPositions));
    }

    private IEnumerator EarthBlastRoutine(Vector3[] positions)
    {
        yield return new WaitForSeconds(warningDuration);

        isCastingEarthSummon = false; // Kết thúc đứng im triệu hồi
        if (AgentReady)
        {
            agent.isStopped = false; // Tiếp tục di chuyển
        }

        if (!isStandaloneMode)
        {
            PlayEarthBlastClientRpc(positions);
        }
        else
        {
            PlayEarthBlastLocal(positions);
        }

        DealEarthBlastDamage(positions);
    }

    [ClientRpc]
    private void SpawnWarningIndicatorsClientRpc(Vector3[] positions)
    {
        SpawnWarningIndicatorsLocal(positions);
    }

    [ClientRpc]
    private void PlayEarthBlastClientRpc(Vector3[] positions)
    {
        PlayEarthBlastLocal(positions);
    }

    private void SpawnWarningIndicatorsLocal(Vector3[] positions)
    {
        foreach (var pos in positions)
        {
            // 1. Tạo vòng tròn cảnh báo decal (nếu hoạt động trên máy người chơi)
            if (warningDecalPrefab != null)
            {
                GameObject warning = Instantiate(warningDecalPrefab, pos + Vector3.up * 0.05f, Quaternion.identity);
                
                // Gắn script chớp đỏ cảnh báo
                var flasher = warning.AddComponent<WarningDecalFlash>();
                if (flasher != null)
                {
                    flasher.StartFlashing(warningDuration);
                }

                Destroy(warning, warningDuration);
            }

            // 2. Tạo vòng viền đỏ lập trình (Procedural) - Failsafe luôn hiển thị đỏ chớp tắt rực rỡ
            GameObject proceduralRing = new GameObject("ProceduralWarningRing");
            proceduralRing.transform.position = pos;
            var ring = proceduralRing.AddComponent<ProceduralWarningCircle>();
            if (ring != null)
            {
                ring.StartWarning(warningDuration, earthBlastRadius);
            }
            Destroy(proceduralRing, warningDuration);
        }
    }

    private void PlayEarthBlastLocal(Vector3[] positions)
    {
        foreach (var pos in positions)
        {
            if (earthBlastPrefab != null)
            {
                GameObject blast = Instantiate(earthBlastPrefab, pos, Quaternion.identity);
                
                // Tăng kích thước transform lên to hơn nữa ( earthBlastScale = 5.0f )
                blast.transform.localScale = Vector3.one * earthBlastScale;

                // Tự động thiết lập Collider toàn diện (cả vòng tròn ngoài lẫn tất cả các mảnh đá visual của Prefab)
                EarthBlastDamageZone.SetupRockColliders(blast, earthBlastRadius, earthBlastScale, 30f);

                // Thử kích hoạt Elemental VFX nếu có
                var locationVfx = blast.GetComponent<PixPlays.ElementalVFX.LocationVfx>();
                if (locationVfx != null)
                {
                    var data = new PixPlays.ElementalVFX.VfxData(pos, pos + Vector3.up, earthBlastScale, earthBlastRadius * earthBlastScale);
                    locationVfx.Play(data);
                }

                Destroy(blast, 4.0f);
            }
        }
    }

    private Vector3[] CalculateEarthBlastPositions()
    {
        var activePlayers = GetAllActivePlayers();
        var spots = new List<Vector3>();

        // Đảm bảo chia đều triệu hồi đá dưới chân TẤT CẢ các Player đang sống
        foreach (var p in activePlayers)
        {
            if (p == null || IsPlayerDeadOrInvisible(p)) continue;
            
            // 1. Triệu hồi ĐẦY ĐỦ chính xác 1 đốm đá ngay dưới chân từng Player
            spots.Add(p.position);
            
            // 2. Triệu hồi thêm 2 đốm đá bẫy ngẫu nhiên xung quanh dưới chân người chơi đó (Chia đều cho cả 4 player)
            for (int k = 0; k < 2; k++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 3.0f; // Bán kính 3m quanh player
                Vector3 offsetPos = p.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
                if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
                {
                    spots.Add(hit.position);
                }
                else
                {
                    spots.Add(offsetPos);
                }
            }
        }

        // Nếu không có người chơi nào hoạt động, triệu hồi ngẫu nhiên xung quanh Boss
        if (spots.Count == 0)
        {
            int targetCount = Random.Range(4, 7);
            for (int i = 0; i < targetCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 8f;
                Vector3 offsetPos = transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    spots.Add(hit.position);
                }
            }
        }

        return spots.ToArray();
    }

    public void DealEarthBlastDamage(Vector3[] positions)
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        foreach (var pos in positions)
        {
            Collider[] hits = Physics.OverlapSphere(pos, earthBlastRadius, playerLayer);
            HashSet<Transform> hitRoots = new HashSet<Transform>();

            foreach (var hit in hits)
            {
                Transform root = GetPlayerRoot(hit.transform);
                if (root != null && !hitRoots.Contains(root))
                {
                    hitRoots.Add(root);
                    Vector3 knockbackDir = (root.position - pos);
                    knockbackDir.y = 1.0f;
                    Vector3 force = knockbackDir.normalized * earthBlastKnockback;

                    EnemyDamageHelper.DealDamage(root, earthBlastDamage, force);
                    Debug.Log($"[BossAI] Earth Blast hit player: {root.name} at {pos}");
                }
            }
        }
    }

    private void SummonMinions()
    {
        bool auth = isStandaloneMode || (IsNetworkActive && IsServer);
        if (!auth) return;

        if (minionPrefab == null)
        {
            Debug.LogWarning("[BossAI] Không có Minion Prefab nào được gán để triệu hồi!");
            return;
        }

        // Dọn dẹp quái con đã chết hoặc biến mất
        for (int i = activeMinions.Count - 1; i >= 0; i--)
        {
            if (activeMinions[i] == null) activeMinions.RemoveAt(i);
        }

        // Giới hạn số lượng quái con đồng thời
        if (activeMinions.Count >= maxMinionsAlive)
        {
            Debug.Log("[BossAI] Đã đạt giới hạn quái con tối đa. Bỏ qua lượt triệu hồi này.");
            return;
        }

        // Xác định tâm sinh quái là vị trí hiện tại của Boss AI để triệu hồi ngẫu nhiên xung quanh Boss
        Vector3 spawnCenter = transform.position;

        int spawnCount = Random.Range(4, 6); // Triệu hồi 4-5 con quái con
        Debug.Log($"[BossAI] Bắt đầu triệu hồi ngẫu nhiên {spawnCount} quái con xung quanh Boss tại: {spawnCenter}");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * minionSpawnRadius;
            Vector3 spawnPos = spawnCenter + new Vector3(randomCircle.x, 0.1f, randomCircle.y);

            // Tìm vị trí hợp lệ trên NavMesh
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, minionSpawnRadius, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            GameObject minion = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
            
            if (isStandaloneMode)
            {
                activeMinions.Add(minion);
            }
            else
            {
                var netObj = minion.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                    activeMinions.Add(minion);
                }
                else
                {
                    Debug.LogError($"[BossAI] Minion Prefab '{minionPrefab.name}' thiếu NetworkObject! Đang tự hủy...");
                    Destroy(minion);
                }
            }
        }
    }

    // ─── Hỗ trợ đồng bộ hóa & Xoay ─────────────────────────────

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
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 15f);
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
        // Vùng phát hiện tuần tra (Vàng)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // Vùng đá Da (Cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, kickDetectRange);
        Vector3 kickLeft = Quaternion.AngleAxis(-kickDetectAngle / 2f, Vector3.up) * transform.forward;
        Vector3 kickRight = Quaternion.AngleAxis(kickDetectAngle / 2f, Vector3.up) * transform.forward;
        Gizmos.DrawRay(transform.position, kickLeft * kickDetectRange);
        Gizmos.DrawRay(transform.position, kickRight * kickDetectRange);

        // Tầm đánh chém thường (Đỏ)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Vẽ tia kiếm chém (Magenta)
        if (swordBase != null && swordTip != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(swordBase.position, swordTip.position);
            Gizmos.DrawWireSphere(swordBase.position, swordThickness);
            Gizmos.DrawWireSphere(swordTip.position, swordThickness);
        }
    }
}
