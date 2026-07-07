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

    // ─── Đồng bộ trạng thái FSM qua mạng ───────────────────────
    [Header("Network State Sync")]
    public NetworkVariable<BossState> currentState = new NetworkVariable<BossState>(
        BossState.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isPhase2Network = new NetworkVariable<bool>(
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

    // ─── Biến Fallback dùng khi chạy Offline/Editor ──────────────
    private float localHealth;
    private BossState localState = BossState.Idle;
    private bool localIsPhase2 = false;
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
    public bool IsDead => CurrentStateValue == BossState.Dead;

    /// <summary>HP hiện tại đúng trong cả Standalone lẫn Network mode — dùng cho HP bar polling.</summary>
    public float ActualCurrentHealth => isStandaloneMode ? localHealth : currentHealth.Value;

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
            if (anim == null || anim.runtimeAnimatorController == null) na.enabled = false;
            else na.Animator = anim;
        }

        // Khởi tạo các State cho FSM
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
        enrageCounter.OnValueChanged += (_, _) => { if (anim != null) anim.SetTrigger(enrageTrigger); };
        currentHealth.OnValueChanged += OnHealthNetChanged;

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
        enrageCounter.OnValueChanged -= (_, _) => { if (anim != null) anim.SetTrigger(enrageTrigger); };
        currentHealth.OnValueChanged -= OnHealthNetChanged;
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

        // Giảm thời gian hồi chiêu
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (kickCooldownTimer > 0) kickCooldownTimer -= Time.deltaTime;

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
            currentFSMState.Update();
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
        //    -> Di chuyển vòng quanh mục tiêu (Orbiting) bằng hoạt ảnh WALK (0.5f) thay vì đứng yên hoặc lao đầu vô nghĩa.
        if (attackCooldownTimer > 0 && dist <= attackRange + 3f && !isDodging)
        {
            if (!isOrbiting || orbitTimer <= 0)
            {
                // Chọn một góc ngẫu nhiên bên trái hoặc phải xung quanh player
                orbitDirection = Random.value < 0.5f ? -1f : 1f;
                orbitTimer = Random.Range(1f, 2.5f);

                // Tính toán điểm di chuyển tiếp tuyến xung quanh player
                Vector3 toBossDir = (transform.position - targetPlayer.position).normalized;
                Vector3 tangent = new Vector3(-toBossDir.z, 0, toBossDir.x) * orbitDirection;
                Vector3 targetOrbitPos = targetPlayer.position + (toBossDir * (attackRange + 1f)) + (tangent * 3f);

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
            
            // Luôn hướng mặt về phía player kể cả khi đang di chuyển vòng quanh
            RotateTowards(targetPlayer.position);
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
    private void DetectAndSwitchTarget()
    {
        if (IsDead) return;

        int num = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, playerLayer);
        if (num == 0) num = FallbackDetect();

        Transform closest = null;
        float minD = float.MaxValue;
        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        for (int i = 0; i < num; i++)
        {
            if (detectionResults[i] == null) continue;
            Transform pTrans = detectionResults[i].transform;

            if (IsPlayerDeadOrInvisible(pTrans)) continue;

            float d = Vector3.Distance(eyePos, pTrans.position);
            Vector3 dir = (pTrans.position - eyePos).normalized;
            
            // Tầm quét cực nhạy: 360 độ cự ly gần (3m) hoặc góc quạt FOV rộng ở cự ly xa
            bool inFOV = Vector3.Angle(transform.forward, dir) < fieldOfView / 2f || d <= 4f;

            if (inFOV && !Physics.Raycast(eyePos, dir, d, obstacleLayer))
            {
                if (d < minD)
                {
                    minD = d;
                    closest = pTrans;
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

    private int FallbackDetect()
    {
        int c = 0;
        foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (c >= detectionResults.Length) break;
            if (Vector3.Distance(transform.position, p.transform.position) <= sightRange)
            {
                var col = p.GetComponent<Collider>();
                if (col != null) detectionResults[c++] = col;
            }
        }
        return c;
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

            EnemyDamageHelper.DealDamage(targetToKick, finalKickDamage, knockbackForceVector);
            Debug.Log($"[BossAI] ĐÁ VĂNG Player: {targetToKick.name}, lực đẩy = {finalKickForce}, gây {finalKickDamage} sát thương.");
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
        if (!isStandaloneMode && !IsServer) return;
        if (CurrentStateValue == BossState.Enrage) return; // Bất tử khi đang gồng nộ

        CurrentHealthValue -= damage;

        if (isStandaloneMode)
        {
            EnemyDamageEffectHelper.PlayDamageEffects(gameObject, damage);
            if (anim != null) anim.SetTrigger(hitTrigger);
        }
        else
        {
            hitCounter.Value++;
        }

        // Xử lý khi hết máu lần đầu (Chuyển sang Phase 2 Gồng Cuồng Nộ)
        if (CurrentHealthValue <= 0)
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

        // Phản xạ nhảy né đòn (Dodge) nhanh nhạy khi bị tấn công (40% cơ hội né đòn ra sau/bên cạnh)
        if (!isDodging && Random.value < 0.4f && CurrentStateValue == BossState.Chase)
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

            // Sinh hiệu ứng gồng nộ cuồng nộ (VFX) nếu có thiết lập
            if (boss.enrageVFXPrefab != null)
            {
                Vector3 spawnPos = boss.enrageVFXSpawnPoint != null ? boss.enrageVFXSpawnPoint.position : boss.transform.position;
                GameObject vfx = Instantiate(boss.enrageVFXPrefab, spawnPos, boss.transform.rotation);
                if (!boss.isStandaloneMode && boss.IsServer)
                {
                    var no = vfx.GetComponent<NetworkObject>();
                    if (no != null) no.Spawn();
                }
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
