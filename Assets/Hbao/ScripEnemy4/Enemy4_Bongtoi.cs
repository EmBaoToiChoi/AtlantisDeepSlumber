using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Enemy4_Bongtoi : NetworkBehaviour
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Run,
        Search,    // Tìm kiếm Player tại vị trí cuối cùng được phát hiện
        Stagger,   // Bị khựng / choáng khi nhận sát thương lớn
        Attack,
        Dead
    }

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(
        EnemyState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Advanced AI Multiplayer Sync")]
    // Đồng bộ trigger hit để mọi client thấy hiệu ứng trúng đòn khựng lại
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Đồng bộ kiểu tấn công (0: Attack 1, 1: Attack Combo)
    public NetworkVariable<int> attackType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform;    // Điểm bắn Raycast dò Player (ví dụ: ở đầu/mắt quái)
    public Renderer[] modelRenderers; // Thay đổi sắc thái để tăng hiệu ứng hình ảnh khi yếu máu hoặc cuồng nộ

    [Header("Melee Hitbox Settings")]
    public GameObject clawHitbox;      // Vùng đánh vuốt/cào (kéo Collider ở tay/vuốt vào đây)
    public GameObject weaponHitbox;    // Vùng đánh bằng vũ khí phụ trợ

    // Bộ đệm tránh phân bổ rác (GC Alloc) khi quét va chạm
    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults = new Collider[8];

    [Header("AI Vision & Combat Settings")]
    public float sightRange = 15f;      // Tầm nhìn xa phát hiện Player
    public float fieldOfView = 100f;    // Góc nhìn (độ)
    public float attackRange = 2f;      // Tầm đánh cận chiến
    public float walkRadius = 9f;       // Bán kính đi dạo ngẫu nhiên
    public float idleTimeMax = 4f;      // Thời gian đứng im nghỉ ngơi tối đa

    [Header("Layers")]
    public LayerMask playerLayer;       // Layer của Player
    public LayerMask obstacleLayer;     // Layer của tường / vật cản địa hình

    [Header("Animator Parameter/Trigger Names")]
    public string idleTriggerName = "qual4IDLE";
    public string walkTriggerName = "Qual4walk";
    public string runTriggerName = "qual4RUn";
    public string hitTriggerName = "Qual4Anhit";   // Trigger cho layer Anhit
    public string dieTriggerName = "Qual4Dle";     // Trigger cho layer Base (Chết)
    public string attack1TriggerName = "qual44attack"; // Trigger Attack 1
    public string attackComboTriggerName = "qual4combo"; // Trigger Attack Combo

    // Quản lý trạng thái AI (Chỉ tính toán và xử lý trên Server)
    private Transform targetPlayer;
    private float stateTimer;
    private bool hasDestination;

    private Vector3 lastKnownPlayerPosition;
    private float searchTimer;
    private float searchLookTimer;
    private float searchLookDirection = 1f;

    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f; // Quét 0.15s một lần tiết kiệm tài nguyên CPU

    private float staggerTimer;
    private float attackCooldownTimer;
    private float tacticalTimer;
    private int tacticalState = 0; // 0: Đuổi thẳng, 1: Né bo sườn trái, 2: Né bo sườn phải

    private bool isDodging = false;
    private float dodgeTimer;

    private float lastDamageTime;
    private int recentHitCount;

    private bool hasDealtDamage;
    private float attackDuration;

    private bool wasEnraged = false;
    private MaterialPropertyBlock propBlock;
    private EnemyState clientLocalState = (EnemyState)(-1);

    private void Awake()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
            if (anim == null)
            {
                anim = GetComponentInChildren<Animator>(true);
            }
        }

        var netAnim = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        if (netAnim != null)
        {
            if (anim == null)
            {
                Debug.LogError($"[{gameObject.name}] KHÔNG TÌM THẤY component Animator trên đối tượng này hoặc con của nó! Đang vô hiệu hóa NetworkAnimator để tránh lỗi crash game NullReferenceException.");
                netAnim.enabled = false;
            }
            else if (anim.runtimeAnimatorController == null)
            {
                Debug.LogError($"[{gameObject.name}] PHÁT HIỆN LỖI: Animator tồn tại nhưng CHƯA ĐƯỢC GÁN 'Animator Controller' trong cửa sổ Inspector của Prefab! Vui lòng kéo Animator Controller của quái 4 vào thành phần Animator của nó trong Prefab. Đang vô hiệu hóa NetworkAnimator để tránh crash game NullReferenceException.");
                netAnim.enabled = false;
            }
            else
            {
                netAnim.Animator = anim;
                Debug.Log($"[{gameObject.name}] Đã liên kết tự động thành công Animator '{anim.name}' vào NetworkAnimator.");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        // Đăng ký đồng bộ hóa Animation trên Client khi biến mạng thay đổi
        currentState.OnValueChanged += OnStateChanged;

        // Khởi tạo hoạt ảnh ban đầu khớp với trạng thái hiện tại (đặc biệt cho người chơi vào sau)
        OnStateChanged(currentState.Value, currentState.Value);

        // Tối ưu hóa đồng bộ chuyển động siêu nhỏ cho mọi Client cùng quan sát
        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.PositionThreshold = 0.001f;
            netTransform.RotAngleThreshold = 0.01f;
            netTransform.ScaleThreshold = 0.01f;
        }

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            ChangeState(EnemyState.Idle);

            // Warp snap quái vào NavMesh khi sinh ra để tránh kẹt
            if (agent != null && agent.isActiveAndEnabled)
            {
                if (!agent.isOnNavMesh)
                {
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                        Debug.Log($"[{gameObject.name}] Snapped to NavMesh on spawn at {hit.position}");
                    }
                }
            }
        }
        else // ---> THÊM ĐOẠN NÀY VÀO <---
        {
            // TẮT NavMeshAgent trên Client để NetworkTransform của Server thoải mái cập nhật vị trí
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        // Đảm bảo các hitbox ban đầu được tắt
        if (clawHitbox != null) clawHitbox.SetActive(false);
        if (weaponHitbox != null) weaponHitbox.SetActive(false);

        // Đăng ký hiệu ứng dính đòn (Hit/Anhit) khi hitCounter tăng
        hitCounter.OnValueChanged += OnHitCounterChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
        hitCounter.OnValueChanged -= OnHitCounterChanged;
    }

    private void OnHitCounterChanged(int oldVal, int newVal)
    {
        if (anim != null)
        {
            anim.ResetTrigger(hitTriggerName);
            anim.SetTrigger(hitTriggerName);
        }
    }

    private void Update()
    {
        if (!IsSpawned) return; // Ngăn code chạy khi chưa kết nối mạng hoàn chỉnh

        // Hiệu ứng màu sắc cuồng bạo nhấp nháy tím khi yếu máu (chạy trên cả server/client để mượt mà)
        UpdateEnrageVisuals();

        // Đồng bộ hóa an toàn hoạt ảnh di chuyển trên Client
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            if (clientLocalState != currentState.Value)
            {
                if (SyncAnimationState(currentState.Value))
                {
                    clientLocalState = currentState.Value; // Cập nhật ngay lập tức
                }
            }
        }

        // Chỉ Server mới có quyền tính toán và ra quyết định AI
        if (!IsServer) return;

        // Tự động snap quái lại vào NavMesh nếu vô tình bị đẩy văng ra ngoài
        if (agent != null && agent.isActiveAndEnabled && !agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        // Giảm thời gian hồi chiêu
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        // Xử lý né tránh lướt ngang (Dodge)
        if (isDodging)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0)
            {
                isDodging = false;
                UpdateAgentSpeed();
            }
            return;
        }

        // Quét tìm Player theo chu kỳ tối ưu CPU
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0)
        {
            detectionTimer = DETECTION_INTERVAL;
            DetectPlayer();
        }

        // Xử lý máy trạng thái
        switch (currentState.Value)
        {
            case EnemyState.Idle:
                HandleIdle();
                break;
            case EnemyState.Walk:
                HandleWalk();
                break;
            case EnemyState.Run:
                HandleRun();
                break;
            case EnemyState.Search:
                HandleSearch();
                break;
            case EnemyState.Stagger:
                HandleStagger();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
        }
    }

    #region AI Server Core Logic

    private void DetectPlayer()
    {
        if (currentState.Value == EnemyState.Dead || currentState.Value == EnemyState.Stagger) return;
        if (currentState.Value == EnemyState.Attack) return;

        int numPlayers = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, playerLayer);

        bool found = false;
        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        for (int i = 0; i < numPlayers; i++)
        {
            Collider col = detectionResults[i];
            if (col == null) continue;
            Transform playerTrans = col.transform;

            // Bỏ qua nếu Player đã chết
            SimplePlayerTest playerScript = playerTrans.GetComponentInParent<SimplePlayerTest>();
            if (playerScript != null && playerScript.currentHealth.Value <= 0) continue;

            // Nâng tâm ngắm lên ngực Player (cao 1.0f)
            Vector3 targetCenterPos = playerTrans.position + Vector3.up * 1.0f;
            Vector3 direction = (targetCenterPos - eyePos).normalized;

            // Kiểm tra góc FOV
            if (Vector3.Angle(transform.forward, direction) < fieldOfView / 2f)
            {
                float distance = Vector3.Distance(eyePos, targetCenterPos);

                // Bắn Raycast kiểm tra vật cản (tránh nhìn xuyên tường)
                if (!Physics.Raycast(eyePos, direction, distance, obstacleLayer))
                {
                    targetPlayer = playerTrans;
                    found = true;
                    if (currentState.Value != EnemyState.Run)
                    {
                        ChangeState(EnemyState.Run);
                    }
                    break;
                }
            }
        }

        // Mất dấu -> Chuyển sang tìm kiếm
        if (!found && currentState.Value == EnemyState.Run)
        {
            if (targetPlayer != null)
            {
                lastKnownPlayerPosition = targetPlayer.position;
                targetPlayer = null;
                ChangeState(EnemyState.Search);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    private void HandleIdle()
    {
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            // Tự nhiên ngẫu nhiên: 40% đi dạo, 30% chạy dạo tuần tra, 30% đứng nghỉ ngơi
            float rand = Random.value;
            if (rand < 0.4f)
            {
                ChangeState(EnemyState.Walk);
            }
            else if (rand < 0.7f)
            {
                ChangeState(EnemyState.Run);
            }
            else
            {
                stateTimer = Random.Range(1.5f, idleTimeMax);
            }
        }
    }

    private void HandleWalk()
    {
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = 2.0f; // Đi chậm tuần tra
        }

        if (!hasDestination)
        {
            Vector3 targetPos = GetRandomNavMeshPoint(walkRadius);
            if (targetPos != Vector3.zero)
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(targetPos);
                hasDestination = true;
            }
        }

        if (hasDestination && agent.isActiveAndEnabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            hasDestination = false;
            float rand = Random.value;
            if (rand < 0.4f) ChangeState(EnemyState.Idle);
            else if (rand < 0.8f) ChangeState(EnemyState.Walk);
            else ChangeState(EnemyState.Run);
        }
    }

    private void HandleRun()
    {
        // 1. Trường hợp tuần tra tự do (không có mục tiêu Player)
        if (targetPlayer == null)
        {
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = 4.0f; // Tốc độ chạy tuần tra
            }

            if (!hasDestination)
            {
                Vector3 targetPos = GetRandomNavMeshPoint(walkRadius * 1.5f);
                if (targetPos != Vector3.zero)
                {
                    if (agent.isActiveAndEnabled) agent.SetDestination(targetPos);
                    hasDestination = true;
                }
            }

            if (hasDestination && agent.isActiveAndEnabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                hasDestination = false;
                ChangeState(Random.value < 0.6f ? EnemyState.Idle : EnemyState.Walk);
            }
            return;
        }

        // Quay mặt cực nhanh khóa chặt Player khi phát hiện và truy đuổi
        Vector3 lookDir = (targetPlayer.position - transform.position);
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 15f);
        }

        // 2. Trường hợp Đuổi theo Player (Chase mode)
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            UpdateAgentSpeed();
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.position);

        // Kỹ thuật chiến thuật cao (Flanking AI):
        // Khi tiến sát Player (cự ly <= 6m), AI sẽ chạy chếch xiên bo sườn thay vì lao thẳng
        if (distance <= 6f)
        {
            tacticalTimer -= Time.deltaTime;
            if (tacticalTimer <= 0)
            {
                float rand = Random.value;
                if (rand < 0.35f) tacticalState = 0;      // Đuổi trực diện
                else if (rand < 0.68f) tacticalState = 1; // Bo sườn trái
                else tacticalState = 2;                  // Bo sườn phải

                tacticalTimer = Random.Range(0.8f, 1.8f);
            }

            if (tacticalState == 0)
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
            }
            else
            {
                Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
                Vector3 tangent = new Vector3(-toPlayer.z, 0, toPlayer.x);
                float sideDir = (tacticalState == 1) ? 1f : -1f;

                // Điểm bo sườn chếch xiên một góc đẹp mắt
                Vector3 targetOffset = targetPlayer.position - toPlayer * 1.5f + tangent * sideDir * 2.5f;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetOffset, out hit, 3f, NavMesh.AllAreas))
                {
                    if (agent.isActiveAndEnabled) agent.SetDestination(hit.position);
                }
                else
                {
                    if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
                }
            }
        }
        else
        {
            // Cự ly xa -> Chạy đuổi trực diện
            if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
        }

        // ĐOẠN CODE ĐÚNG SAU KHI SỬA
        if (distance <= attackRange)
        {
            if (attackCooldownTimer <= 0)
            {
                ChangeState(EnemyState.Attack);
            }
            // Bỏ qua bước chuyển sang Idle. Quái sẽ giữ state Run và chạy bám đuôi Player!
        }
    }

    private void HandleSearch()
    {
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = 3.5f;
            agent.SetDestination(lastKnownPlayerPosition);
        }

        if (agent.isActiveAndEnabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            agent.isStopped = true;
            searchTimer -= Time.deltaTime;

            // Xoay đầu láo liên tìm kiếm
            searchLookTimer -= Time.deltaTime;
            if (searchLookTimer <= 0)
            {
                searchLookDirection = -searchLookDirection;
                searchLookTimer = 0.7f;
            }

            transform.Rotate(Vector3.up, searchLookDirection * 130f * Time.deltaTime);

            if (searchTimer <= 0)
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    private void HandleStagger()
    {
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        staggerTimer -= Time.deltaTime;

        if (staggerTimer <= 0)
        {
            if (targetPlayer != null)
            {
                ChangeState(EnemyState.Run);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    private void HandleAttack()
    {
        if (targetPlayer == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        if (agent.isActiveAndEnabled) agent.isStopped = true;

        stateTimer -= Time.deltaTime;
        float elapsed = attackDuration - stateTimer;

        // Khóa hướng xoay ở 30% đầu của hoạt ảnh chuẩn bị đánh để tăng độ chính xác,
        // Nâng tốc độ slerp lên 18f để snappy khóa mục tiêu
        if (elapsed < attackDuration * 0.3f)
        {
            Vector3 lookDir = (targetPlayer.position - transform.position);
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 18f);
            }
        }

        // Tính toán gây sát thương diện rộng hình nón (khớp hoàn hảo với nhịp hạ tay của animation)
        // Attack Combo (2 hit vung tay đập liên hoàn) hoặc Attack 1
        if (attackType.Value == 1) // Combo (Dưới 50% HP)
        {
            // Nhịp vung 1: ở 0.5s
            if (elapsed >= 0.5f && !hasDealtDamage)
            {
                hasDealtDamage = true;
                DealConeDamage(14f, attackRange + 0.4f, 85f, 6f);
            }
            // Nhịp vung Slam mạnh 2: ở 1.1s
            if (elapsed >= 1.1f && hasDealtDamage)
            {
                hasDealtDamage = false; // Mượn biến để đánh dấu hit 2
                DealConeDamage(26f, attackRange + 1.1f, 95f, 13f);
            }
        }
        else // Attack 1 (Thanh máu đầy 100%)
        {
            if (elapsed >= 0.45f && !hasDealtDamage)
            {
                hasDealtDamage = true;
                DealConeDamage(16f, attackRange + 0.5f, 80f, 8f);
            }
        }

        if (stateTimer <= 0)
        {
            // Thời gian hồi chiêu: bình thường hồi 0.7s, khi máu thấp hồi siêu tốc 0.3s cực kỳ thông minh hung hãn
            attackCooldownTimer = (currentHealth.Value < maxHealth * 0.5f) ? 0.3f : 0.7f;
            ChangeState(EnemyState.Run);
        }
    }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int numHits = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);

        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        for (int i = 0; i < numHits; i++)
        {
            Collider col = damageResults[i];
            if (col == null) continue;
            Transform player = col.transform;
            Vector3 dir = (player.position - transform.position).normalized;

            // Kiểm tra góc nêm hình nón phía trước mặt quái
            if (Vector3.Angle(transform.forward, dir) <= angle / 2f)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                // Tránh cào xuyên tường cản
                if (!Physics.Raycast(eyePos, dir, dist, obstacleLayer))
                {
                    SimplePlayerTest playerScript = player.GetComponentInParent<SimplePlayerTest>();
                    if (playerScript != null)
                    {
                        playerScript.TakeDamage(damage);

                        // Đẩy lùi Player mượt mà bằng vật lý
                        Vector3 kbDir = dir;
                        kbDir.y = 0;
                        kbDir.Normalize();
                        playerScript.ApplyKnockback(kbDir * knockback);
                    }
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer || currentState.Value == EnemyState.Dead) return;

        currentHealth.Value -= damage;
        hitCounter.Value++; // Tăng biến mạng để đồng bộ trigger Anhit cho tất cả các Client

        if (currentHealth.Value <= 0)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        // Tính toán sát thương nhận dồn để khựng choáng
        float now = Time.time;
        if (now - lastDamageTime > 3f)
        {
            recentHitCount = 0;
        }
        recentHitCount++;
        lastDamageTime = now;

        // Nếu nhận sát thương lớn >= 25 HP hoặc bị đánh liên tiếp 3 hit -> Khựng stagger ăn hit
        bool shouldStagger = (damage >= 25f || recentHitCount >= 3) && (currentState.Value != EnemyState.Stagger);

        if (shouldStagger)
        {
            recentHitCount = 0;
            staggerTimer = 0.55f;
            ChangeState(EnemyState.Stagger);
        }
        else
        {
            // Phản xạ Lướt né tránh (Dodge Reflex) ngẫu nhiên 30% khi bị người chơi tấn công lúc đang di chuyển
            if (!isDodging && Random.value < 0.3f && (currentState.Value == EnemyState.Run || currentState.Value == EnemyState.Walk))
            {
                ExecuteDodge();
            }
        }
    }

    private void ExecuteDodge()
    {
        if (targetPlayer == null) return;

        Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
        Vector3 perpendicular = new Vector3(-toPlayer.z, 0, toPlayer.x);

        // Ngẫu nhiên chọn né bên trái hay bên phải
        if (Random.value < 0.5f) perpendicular = -perpendicular;

        Vector3 dodgeTarget = transform.position + perpendicular * 3.0f;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(dodgeTarget, out hit, 3f, NavMesh.AllAreas))
        {
            isDodging = true;
            dodgeTimer = 0.35f; // Lướt nhanh trong 0.35 giây
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = (currentHealth.Value < maxHealth * 0.5f) ? 15f : 11f; // Chạy lướt siêu tốc độ khi máu yếu
                agent.SetDestination(hit.position);
            }
        }
    }

    private void UpdateAgentSpeed()
    {
        if (!agent.isActiveAndEnabled) return;

        // Chạy nhanh hơn khi dưới 50% HP biểu thị cuồng bạo
        if (currentHealth.Value < maxHealth * 0.5f)
        {
            agent.speed = 6.2f;
        }
        else
        {
            agent.speed = 4.8f;
        }
    }

    private Vector3 GetRandomNavMeshPoint(float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, 1))
        {
            return hit.position;
        }
        return Vector3.zero;
    }

    private void Die()
    {
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        // Tắt va chạm hoặc thực hiện các hiệu ứng chết khác ở đây nếu cần

        // Hủy đối tượng qua mạng sau 2 giây chơi hoàn tất hoạt ảnh chết
        Invoke(nameof(DespawnEnemy), 2.0f);
    }

    private void DespawnEnemy()
    {
        if (IsServer && IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    private void ChangeState(EnemyState newState)
    {
        // Tự động tắt hitbox vũ khí/vuốt khi chuyển từ Attack sang trạng thái khác để tránh kẹt sát thương
        if (currentState.Value == EnemyState.Attack && newState != EnemyState.Attack)
        {
            DisableClawHitbox();
            DisableWeaponHitbox();
        }

        currentState.Value = newState;

        if (newState == EnemyState.Idle)
        {
            stateTimer = Random.Range(1.5f, idleTimeMax);
            if (agent.isActiveAndEnabled) agent.isStopped = true;
        }
        if (newState == EnemyState.Walk)
        {
            hasDestination = false;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Run)
        {
            isDodging = false;
            hasDestination = false;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Search)
        {
            searchTimer = 3.5f; // Tìm kiếm trong 3.5 giây
            searchLookTimer = 0f;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Stagger)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
            if (staggerTimer <= 0) staggerTimer = 0.55f;
        }
        if (newState == EnemyState.Attack)
        {
            hasDealtDamage = false;

            float hpPercent = currentHealth.Value / maxHealth;

            // QUYẾT ĐỊNH ĐÒN ĐÁNH THEO LƯỢNG MÁU HP:
            // 1. Khi còn đủ 100% HP -> Chắc chắn sử dụng Attack 1 (qual44attack)
            // 2. Khi HP dưới 50% -> Chắc chắn sử dụng Attack Combo (qual4combo)
            // 3. Khi HP ở khoảng giữa (50% <= HP < 100%) -> Ưu tiên Attack 1 hoặc xoay vòng ngẫu nhiên
            if (hpPercent >= 1.0f)
            {
                attackType.Value = 0; // Attack 1
                attackDuration = 1.0f; // Khớp thời lượng anim qual44attack
            }
            else if (hpPercent < 0.5f)
            {
                attackType.Value = 1; // Attack Combo
                attackDuration = 1.7f; // Khớp thời lượng anim qual4combo
            }
            else
            {
                // Khoảng giữa 50% - 99%: sử dụng đòn vung đơn Attack 1 làm mặc định chiến đấu
                attackType.Value = 0;
                attackDuration = 1.0f;
            }

            stateTimer = attackDuration;

            // Kích hoạt đồng bộ hóa hoạt ảnh tấn công qua ClientRpc đến toàn bộ client
            PlayAttackAnimationClientRpc(attackType.Value);
        }
        if (newState == EnemyState.Dead) Die();
    }

    #endregion

    #region Client Animation Synchronization

    // Hàm chạy trên toàn bộ máy Client khi currentState đổi để đồng bộ Animator chuyển động
    private void OnStateChanged(EnemyState previousValue, EnemyState newValue)
    {
        clientLocalState = (EnemyState)(-1);
    }

    private bool SyncAnimationState(EnemyState newState)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

        // Reset toàn bộ trigger cũ để tránh xung đột chuyển động
        anim.ResetTrigger(idleTriggerName);
        anim.ResetTrigger(walkTriggerName);
        anim.ResetTrigger(runTriggerName);
        anim.ResetTrigger(hitTriggerName);
        anim.ResetTrigger(dieTriggerName);

        switch (newState)
        {
            case EnemyState.Idle:
                anim.SetTrigger(idleTriggerName);
                break;
            case EnemyState.Walk:
                anim.SetTrigger(walkTriggerName);
                break;
            case EnemyState.Run:
                anim.SetTrigger(runTriggerName);
                break;
            case EnemyState.Stagger:
                anim.SetTrigger(hitTriggerName); // Chạy hoạt ảnh Qual4Anhit
                break;
            case EnemyState.Dead:
                anim.SetTrigger(dieTriggerName); // Chạy hoạt ảnh Qual4Dle
                break;
        }
        return true;
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc(int type)
    {
        if (anim == null) return;

        anim.ResetTrigger(attack1TriggerName);
        anim.ResetTrigger(attackComboTriggerName);

        if (type == 0)
        {
            anim.SetTrigger(attack1TriggerName); // Kích hoạt qual44attack
        }
        else if (type == 1)
        {
            anim.SetTrigger(attackComboTriggerName); // Kích hoạt qual4combo
        }
    }

    #endregion

    #region Animation Events & Hitbox Control

    // Các hàm này được gọi từ Animation Events tại các keyframe vung tay/vuốt để bật/tắt collider gây sát thương
    public void EnableClawHitbox()
    {
        if (clawHitbox != null)
        {
            clawHitbox.SetActive(true);
        }
    }

    public void DisableClawHitbox()
    {
        if (clawHitbox != null)
        {
            clawHitbox.SetActive(false);
        }
    }

    public void EnableWeaponHitbox()
    {
        if (weaponHitbox != null)
        {
            weaponHitbox.SetActive(true);
        }
    }

    public void DisableWeaponHitbox()
    {
        if (weaponHitbox != null)
        {
            weaponHitbox.SetActive(false);
        }
    }

    #endregion

    #region Visual Effects (Enrage)

    private void UpdateEnrageVisuals()
    {
        if (modelRenderers == null || modelRenderers.Length == 0) return;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        bool isEnraged = currentHealth.Value < maxHealth * 0.5f;

        if (isEnraged)
        {
            wasEnraged = true;
            // Nhấp nháy màu tím huyền bí biểu thị bóng tối thức tỉnh cuồng nộ
            float pingPong = Mathf.PingPong(Time.time * 3f, 1f);
            Color enrageColor = Color.Lerp(Color.white, new Color(0.5f, 0f, 0.8f), pingPong); // Tím huyền ảo

            foreach (var r in modelRenderers)
            {
                if (r != null)
                {
                    r.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_Color", enrageColor);
                    r.SetPropertyBlock(propBlock);
                }
            }
        }
        else if (wasEnraged)
        {
            wasEnraged = false;
            foreach (var r in modelRenderers)
            {
                if (r != null)
                {
                    r.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_Color", Color.white);
                    r.SetPropertyBlock(propBlock);
                }
            }
        }
    }

    #endregion

    // Hiển thị công cụ trực quan hóa tầm nhìn trên Unity Scene Editor hỗ trợ lập trình viên
    private void OnDrawGizmosSelected()
    {
        if (eyeTransform != null)
        {
            // Tầm quét Player (Màu Tím huyền bí)
            Gizmos.color = new Color(0.6f, 0.2f, 0.8f, 0.4f);
            Gizmos.DrawWireSphere(eyeTransform.position, sightRange);

            // Góc nhìn FOV
            Vector3 leftRay = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
            Vector3 rightRay = Quaternion.AngleAxis(fieldOfView / 2f, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eyeTransform.position, leftRay * sightRange);
            Gizmos.DrawRay(eyeTransform.position, rightRay * sightRange);

            // Tầm đòn đánh cận chiến (Màu Đỏ)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}