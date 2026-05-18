using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Enemy3_Buaa : NetworkBehaviour
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Run,
        Search,    // Săn tìm Player tại tọa độ cuối cùng nhìn thấy
        Stagger,   // Bị khựng / choáng khi nhận sát thương lớn
        Attack,
        Dead
    }

    [Header("Health Settings")]
    public float maxHealth = 120f; // Boss/Enemy nặng có lượng HP nhiều hơn 1 chút
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        120f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<EnemyState> currentState = new NetworkVariable<EnemyState>(
        EnemyState.Idle, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    [Header("Advanced AI Sync")]
    // Đồng bộ hit để mọi người thấy quái bị khựng và chơi hoạt ảnh dính đòn
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Kiểu tấn công đồng bộ (0: Attack thường, 1: Combo Skill 2, 2: RunLumpAttack Skill 3)
    public NetworkVariable<int> attackType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform; // Vị trí mắt để dò quét Player bằng Raycast
    public Renderer[] modelRenderers; // Dùng để đổi màu sắc cảnh báo khi quái cuồng bạo

    [Header("AI Vision & Combat Settings")]
    public float sightRange = 15f;      // Tầm nhìn xa
    public float fieldOfView = 100f;    // Góc nhìn rộng
    public float attackRange = 2.2f;    // Khoảng cách ra đòn búa cận chiến
    public float walkRadius = 9f;       // Bán kính đi dạo ngẫu nhiên
    public float idleTimeMax = 4.5f;    // Thời gian đứng im nghỉ ngơi tối đa
    
    [Header("Layers")]
    public LayerMask playerLayer;       // Layer của Player
    public LayerMask obstacleLayer;     // Layer của vật cản địa hình (tường, đá...)

    [Header("Animator Custom Triggers")]
    public string idleTriggerName = "Idle";
    public string walkTriggerName = "Walk";
    public string runTriggerName = "Run";
    public string hitTriggerName = "Anhit";   // Khớp với state quai3Anhit ở layer anhit
    public string dieTriggerName = "Die";     // Khớp với state quai3 die ở base layer
    public string attack1TriggerName = "Attack";   // Khớp với quai3attack
    public string attack2TriggerName = "Combo";    // Khớp với quai3combo
    public string attack3TriggerName = "RunLumpAttack"; // Khớp với quai3runlumpattack

    // Biến điều khiển Pro AI (Chỉ tính toán trên Server)
    private Transform targetPlayer;
    private float stateTimer;
    private bool hasDestination;

    private Vector3 lastKnownPlayerPosition;
    private float searchTimer;
    private float searchLookTimer;
    private float searchLookDirection = 1f;
    
    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f; // Chu kỳ quét 0.15s một lần tối ưu hiệu năng CPU

    private float staggerTimer;
    private float attackCooldownTimer;
    private float tacticalTimer;
    private int tacticalState = 0; // 0: Lao thẳng, 1: Né tránh bo sườn trái, 2: Né tránh bo sườn phải

    private bool isDodging = false;
    private float dodgeTimer;

    private float lastDamageTime;
    private int recentHitCount;

    // Trạng thái phẫn nộ (Enrage) và Cuồng bạo (Frenzy)
    private bool isEnraged = false;   // <= 70% HP
    private bool isFrenzied = false;  // <= 40% HP
    private bool hasRoared70 = false;
    private bool hasRoared40 = false;
    private Vector3 originalScale;

    // Quản lý sát thương của hoạt ảnh tấn công nhiều nhịp
    private bool hasDealtDamage1;
    private bool hasDealtDamage2;
    private float attackDuration;

    public override void OnNetworkSpawn()
    {
        // Đồng bộ hóa trạng thái di chuyển và các trigger trên Client
        currentState.OnValueChanged += OnStateChanged;
        attackType.OnValueChanged += OnAttackTypeChanged;

        // Lưu kích thước ban đầu để phóng to khi phẫn nộ
        originalScale = transform.localScale;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            ChangeState(EnemyState.Idle);
        }

        // Lắng nghe hitCounter để chơi hoạt ảnh dính đòn (Hit/Anhit) trên mọi Client
        hitCounter.OnValueChanged += (oldVal, newVal) => {
            if (anim != null)
            {
                anim.ResetTrigger(hitTriggerName);
                anim.SetTrigger(hitTriggerName);
            }
        };
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
        attackType.OnValueChanged -= OnAttackTypeChanged;
    }

    private void Update()
    {
        // Chỉ Server mới xử lý bộ não quyết định AI
        if (!IsServer) return;

        // Xử lý hồi thời gian đòn đánh
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        // Xử lý hiệu ứng màu sắc cuồng bạo nhấp nháy đỏ trên Server/Client (nếu có mesh)
        UpdateFrenzyVisuals();

        // Xử lý lướt né tránh (Dodge)
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

        // Dò tìm người chơi theo chu kỳ tối ưu CPU
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0)
        {
            detectionTimer = DETECTION_INTERVAL;
            DetectPlayer();
        }

        // Thực thi hành vi theo State Machine
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

    #region AI Logic (Server Side)

    private void DetectPlayer()
    {
        if (currentState.Value == EnemyState.Dead || currentState.Value == EnemyState.Stagger) return;
        if (currentState.Value == EnemyState.Attack) return;

        Collider[] playersInSight = Physics.OverlapSphere(transform.position, sightRange, playerLayer);
        bool playerFound = false;

        foreach (Collider p in playersInSight)
        {
            Transform potentialTarget = p.transform;

            // Bỏ qua nếu Player đã chết
            SimplePlayerTest playerScript = potentialTarget.GetComponentInParent<SimplePlayerTest>();
            if (playerScript != null && playerScript.currentHealth.Value <= 0)
            {
                continue;
            }

            Vector3 directionToTarget = (potentialTarget.position - eyeTransform.position).normalized;

            // Kiểm tra xem góc giữa hướng quái nhìn và player có nhỏ hơn góc FOV quy định không
            if (Vector3.Angle(transform.forward, directionToTarget) < fieldOfView / 2)
            {
                float distanceToTarget = Vector3.Distance(eyeTransform.position, potentialTarget.position);

                // Bắn tia Raycast để chắc chắn không có tường cản giữa quái và Player
                if (!Physics.Raycast(eyeTransform.position, directionToTarget, distanceToTarget, obstacleLayer))
                {
                    targetPlayer = potentialTarget;
                    playerFound = true;
                    if (currentState.Value != EnemyState.Run)
                    {
                        ChangeState(EnemyState.Run);
                    }
                    break; 
                }
            }
        }

        // Mất dấu Player khi đang truy đuổi -> Chuyển sang tìm kiếm thông minh tại vị trí cuối cùng
        if (!playerFound && currentState.Value == EnemyState.Run)
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
            // Đi tuần ngẫu nhiên (50% tiếp tục đứng im nghỉ ngơi, 50% đi dạo chơi tuần tra)
            int rand = Random.Range(0, 2);
            if (rand == 0)
            {
                ChangeState(EnemyState.Walk);
            }
            else
            {
                stateTimer = Random.Range(2f, idleTimeMax);
            }
        }
    }

    private void HandleWalk()
    {
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = 2.0f; // Đi dạo tuần tra chậm rãi
        }

        if (!hasDestination)
        {
            Vector3 randomDirection = Random.insideUnitSphere * walkRadius;
            randomDirection += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, walkRadius, 1))
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(hit.position);
                hasDestination = true;
            }
        }

        // Nếu đã tuần tra tới đích
        if (hasDestination && agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance)
        {
            hasDestination = false;
            ChangeState(EnemyState.Idle);
        }
    }

    private void HandleRun()
    {
        if (targetPlayer == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            UpdateAgentSpeed();
        }

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // Kỹ thuật bo sườn đỉnh cao (Circle Flanking):
        // Khi tiếp cận gần (<= 7m), AI chuyển động xiên trái/phải để né tránh đạn bắn trực diện của Player
        if (distanceToPlayer <= 7f)
        {
            tacticalTimer -= Time.deltaTime;
            if (tacticalTimer <= 0)
            {
                float rand = Random.value;
                if (rand < 0.4f) tacticalState = 0; // Lao thẳng
                else if (rand < 0.7f) tacticalState = 1; // Bo sườn trái
                else tacticalState = 2; // Bo sườn phải

                tacticalTimer = Random.Range(1.0f, 2.0f);
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

                // Điểm đích bo sườn chếch xiên góc
                Vector3 targetOffset = targetPlayer.position - toPlayer * 2.0f + tangent * sideDir * 2.8f;
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetOffset, out hit, 3.5f, NavMesh.AllAreas))
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
            // Ở cự ly xa -> Đuổi trực diện
            if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
        }

        // Vào tầm đánh búa cận chiến và hết thời gian hồi chiêu
        if (distanceToPlayer <= attackRange && attackCooldownTimer <= 0)
        {
            ChangeState(EnemyState.Attack);
        }
    }

    private void HandleSearch()
    {
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = isFrenzied ? 5.5f : (isEnraged ? 4.5f : 3.5f);
            agent.SetDestination(lastKnownPlayerPosition);
        }

        // Đã đến vị trí cuối cùng nhìn thấy Player
        if (agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.isStopped = true;
            searchTimer -= Time.deltaTime;

            // Xoay đầu láo liên quét tìm kiếm qua lại
            searchLookTimer -= Time.deltaTime;
            if (searchLookTimer <= 0)
            {
                searchLookDirection = -searchLookDirection;
                searchLookTimer = 0.7f;
            }

            transform.Rotate(Vector3.up, searchLookDirection * 140f * Time.deltaTime);

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

        // Xử lý tăng trưởng kích thước hoành tráng dần khi phẫn nộ trên Server
        if (isFrenzied && !hasRoared40)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, originalScale * 1.22f, Time.deltaTime * 2.5f);
        }
        else if (isEnraged && !hasRoared70)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, originalScale * 1.12f, Time.deltaTime * 2.5f);
        }

        if (staggerTimer <= 0)
        {
            if (isFrenzied && !hasRoared40) hasRoared40 = true;
            else if (isEnraged && !hasRoared70) hasRoared70 = true;

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

        // Khóa hướng xoay ở 35% đầu của hoạt ảnh vung búa để người chơi có thể lướt né sang sườn
        if (elapsed < attackDuration * 0.35f)
        {
            Vector3 lookDir = (targetPlayer.position - transform.position);
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 12f);
            }
        }

        // TÍNH TOÁN SÁT THƯƠNG DIỆN RỘNG HÌNH NÓN (Khớp hoàn hảo với nhịp hạ búa từng Animation)
        if (attackType.Value == 2) // Skill 3: RunLumpAttack (Jump Slam cực mạnh)
        {
            // Điểm chạm búa ở giây thứ 0.9s của hoạt ảnh
            if (elapsed >= 0.9f && !hasDealtDamage1)
            {
                hasDealtDamage1 = true;
                // Sát thương 35 HP, cự ly rộng 3.7m, góc 110 độ, đẩy lùi cực mạnh 18f
                DealConeDamage(35f, attackRange + 1.5f, 110f, 18f);
            }
        }
        else if (attackType.Value == 1) // Skill 2: Combo (2 Hit đập búa)
        {
            // Nhịp vung đập 1: ở giây thứ 0.5s
            if (elapsed >= 0.5f && !hasDealtDamage1)
            {
                hasDealtDamage1 = true;
                DealConeDamage(12f, attackRange + 0.4f, 80f, 5f);
            }
            // Nhịp vung đập Slam 2: ở giây thứ 1.1s
            if (elapsed >= 1.1f && !hasDealtDamage2)
            {
                hasDealtDamage2 = true;
                DealConeDamage(22f, attackRange + 1.0f, 90f, 12f);
            }
        }
        else // Attack 1: Đòn thường (quai3attack)
        {
            // Nhịp đập búa cơ bản ở giây thứ 0.45s
            if (elapsed >= 0.45f && !hasDealtDamage1)
            {
                hasDealtDamage1 = true;
                DealConeDamage(15f, attackRange + 0.5f, 80f, 7f);
            }
        }

        // Đòn đánh kết thúc hoàn chỉnh
        if (stateTimer <= 0)
        {
            // Hồi chiêu: cuồng bạo hồi chiêu siêu tốc 0.15s, phẫn nộ hồi 0.4s, bình thường hồi 0.8s
            attackCooldownTimer = isFrenzied ? 0.15f : (isEnraged ? 0.4f : 0.8f);
            ChangeState(EnemyState.Run);
        }
    }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, range, playerLayer);
        foreach (var col in hitColliders)
        {
            Transform player = col.transform;
            Vector3 dirToPlayer = (player.position - transform.position).normalized;

            // Kiểm tra góc nêm hình nón phía trước mặt quái
            if (Vector3.Angle(transform.forward, dirToPlayer) <= angle / 2f)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                // Tránh cào/đập xuyên tường cản
                if (!Physics.Raycast(eyeTransform.position, dirToPlayer, distanceToPlayer, obstacleLayer))
                {
                    SimplePlayerTest playerScript = player.GetComponentInParent<SimplePlayerTest>();
                    if (playerScript != null)
                    {
                        playerScript.TakeDamage(damage);
                        
                        // Đẩy lùi Player mượt mà vật lý
                        Vector3 knockbackDir = dirToPlayer;
                        knockbackDir.y = 0;
                        knockbackDir.Normalize();
                        playerScript.ApplyKnockback(knockbackDir * knockback);
                    }
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer || currentState.Value == EnemyState.Dead) return;

        currentHealth.Value -= damage;
        hitCounter.Value++; // Tăng counter đồng bộ client chạy animation "Anhit"

        if (currentHealth.Value <= 0)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        float hpPercent = currentHealth.Value / maxHealth;

        // KIỂM TRA NGƯỠNG PHẪN NỘ
        if (hpPercent <= 0.4f && !isFrenzied)
        {
            // Bước vào Cuồng bạo (Frenzy Phase 3)
            isEnraged = true;
            isFrenzied = true;
            staggerTimer = 1.3f; // Khóa di chuyển 1.3s để gầm rú cuồng bạo
            ChangeState(EnemyState.Stagger);
            
            // Đồng bộ kích hoạt kỹ thuật thét lớn bằng cách kích hoạt trigger skill 3
            attackType.Value = 2;
            return;
        }
        else if (hpPercent <= 0.7f && !isEnraged)
        {
            // Bước vào Phẫn nộ (Enraged Phase 2)
            isEnraged = true;
            staggerTimer = 1.1f; // Khóa di chuyển 1.1s gầm thét phẫn nộ
            ChangeState(EnemyState.Stagger);
            
            // Kích hoạt thét bằng cách chạy trigger Combo
            attackType.Value = 1;
            return;
        }

        // Tính toán sát thương nhận dồn dập
        float now = Time.time;
        if (now - lastDamageTime > 3f)
        {
            recentHitCount = 0;
        }
        recentHitCount++;
        lastDamageTime = now;

        // Nhận sát thương đơn >= 25 HP hoặc dính 3 phát liên tục -> Khựng stagger ăn hit
        bool shouldStagger = (damage >= 25f || recentHitCount >= 3) && (currentState.Value != EnemyState.Stagger);

        if (shouldStagger)
        {
            recentHitCount = 0;
            staggerTimer = 0.55f;
            ChangeState(EnemyState.Stagger);
        }
        else
        {
            // Phản xạ Lướt né tránh (Dodge Reflex) ngẫu nhiên 30% khi bị người chơi tấn công lúc đang đuổi
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
        
        // Ngẫu nhiên chọn né trái hay phải
        if (Random.value < 0.5f) perpendicular = -perpendicular;

        Vector3 dodgeTarget = transform.position + perpendicular * 3.2f;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(dodgeTarget, out hit, 3.5f, NavMesh.AllAreas))
        {
            isDodging = true;
            dodgeTimer = 0.35f; // Lướt nhanh trong 0.35s
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = isFrenzied ? 16f : (isEnraged ? 13f : 10f); // Tốc độ lướt siêu tốc độ
                agent.SetDestination(hit.position);
            }
        }
    }

    private void UpdateAgentSpeed()
    {
        if (!agent.isActiveAndEnabled) return;

        // Tốc chạy: cuồng bạo chạy cực nhanh 7.5f, phẫn nộ chạy 6.0f, bình thường chạy 5.0f
        if (isFrenzied) agent.speed = 7.5f;
        else if (isEnraged) agent.speed = 6.0f;
        else agent.speed = 5.0f;
    }

    private void UpdateFrenzyVisuals()
    {
        if (modelRenderers == null || modelRenderers.Length == 0) return;

        if (isFrenzied)
        {
            // Nhấp nháy màu đỏ cam rực lửa biểu thị cuồng bạo tột đỉnh
            float pingPong = Mathf.PingPong(Time.time * 4f, 1f);
            Color frenzyColor = Color.Lerp(Color.white, new Color(1f, 0.25f, 0f), pingPong);
            
            foreach (var r in modelRenderers)
            {
                if (r != null && r.material != null)
                {
                    r.material.color = frenzyColor;
                }
            }
        }
        else if (isEnraged)
        {
            // Phát ra ánh vàng cam ấm của sự tức giận
            Color angerColor = new Color(1f, 0.75f, 0.5f);
            foreach (var r in modelRenderers)
            {
                if (r != null && r.material != null)
                {
                    r.material.color = angerColor;
                }
            }
        }
    }

    private void Die()
    {
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        
        // Hủy quái sau 2.5 giây chơi hoàn tất hoạt ảnh nằm xuống chết
        Invoke(nameof(DespawnEnemy), 2.5f);
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
        currentState.Value = newState;

        if (newState == EnemyState.Idle) 
        {
            stateTimer = Random.Range(2f, idleTimeMax);
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
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Search)
        {
            searchTimer = 3.5f; // Thời gian tuần tra tìm kiếm player trong 3.5 giây
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
            hasDealtDamage1 = false;
            hasDealtDamage2 = false;

            float hpPercent = currentHealth.Value / maxHealth;

            // QUYẾT ĐỊNH ĐÒN ĐÁNH DỰA TRÊN LƯỢNG HP
            if (hpPercent <= 0.4f)
            {
                // Dùng Skill 3: RunLumpAttack
                attackType.Value = 2;
                attackDuration = 1.9f; 
            }
            else if (hpPercent <= 0.7f)
            {
                // Dùng Skill 2: Combo 2 Hit đập búa
                attackType.Value = 1;
                attackDuration = 1.6f;
            }
            else
            {
                // Dùng Attack 1: Đòn đập búa cơ bản
                attackType.Value = 0;
                attackDuration = 1.0f;
            }

            stateTimer = attackDuration;

            // Kích hoạt đồng bộ hóa hoạt ảnh tấn công
            OnAttackTypeChanged(0, attackType.Value);
        }
        if (newState == EnemyState.Dead) Die();
    }

    #endregion

    #region Client Animation Sync

    // Chạy trên TẤT CẢ các Client khi currentState thay đổi để đồng bộ hoạt ảnh của quái
    private void OnStateChanged(EnemyState previousValue, EnemyState newValue)
    {
        if (anim == null) return;

        // Reset các trigger để đảm bảo không bị kẹt hay giật lặp hoạt ảnh
        anim.ResetTrigger(idleTriggerName);
        anim.ResetTrigger(walkTriggerName);
        anim.ResetTrigger(runTriggerName);
        anim.ResetTrigger(hitTriggerName);
        anim.ResetTrigger(dieTriggerName);

        switch (newValue)
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
                anim.SetTrigger(hitTriggerName); // Hoạt ảnh dính đòn quai3Anhit
                break;
            case EnemyState.Dead:
                anim.SetTrigger(dieTriggerName); // Hoạt ảnh nằm xuống chết quai3 die
                break;
        }
    }

    // Chạy trên TẤT CẢ Client khi kiểu tấn công thay đổi để vung búa đồng bộ
    private void OnAttackTypeChanged(int oldVal, int newVal)
    {
        if (anim == null) return;

        anim.ResetTrigger(attack1TriggerName);
        anim.ResetTrigger(attack2TriggerName);
        anim.ResetTrigger(attack3TriggerName);

        if (newVal == 0) anim.SetTrigger(attack1TriggerName);      // Kích hoạt quai3attack
        else if (newVal == 1) anim.SetTrigger(attack2TriggerName); // Kích hoạt quai3combo
        else if (newVal == 2) anim.SetTrigger(attack3TriggerName); // Kích hoạt quai3runlumpattack
    }

    #endregion

    // Vẽ công cụ Visual Debug hỗ trợ lập trình viên điều chỉnh khoảng cách chuẩn xác tuyệt đối trên Scene Editor
    private void OnDrawGizmosSelected()
    {
        if (eyeTransform != null)
        {
            // Vùng tầm nhìn quét phát hiện Player (Màu Vàng Hổ Phách)
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
            Gizmos.DrawWireSphere(eyeTransform.position, sightRange);

            // Hai vạch thể hiện góc nhìn FOV của mắt quái
            Vector3 leftLimit = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
            Vector3 rightLimit = Quaternion.AngleAxis(fieldOfView / 2f, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eyeTransform.position, leftLimit * sightRange);
            Gizmos.DrawRay(eyeTransform.position, rightLimit * sightRange);

            // Vòng tròn cự ly ra đòn búa cận chiến (Màu Đỏ Neon)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            
            // Vẽ góc quét sát thương thực tế hình nêm nón lúc ra đòn tấn công
            if (currentState.Value == EnemyState.Attack)
            {
                float currentAngle = (attackType.Value == 2) ? 110f : ((attackType.Value == 1) ? 90f : 80f);
                float currentRange = (attackType.Value == 2) ? attackRange + 1.5f : ((attackType.Value == 1) ? attackRange + 1.0f : attackRange + 0.5f);
                
                Vector3 attackLeft = Quaternion.AngleAxis(-currentAngle / 2f, Vector3.up) * transform.forward;
                Vector3 attackRight = Quaternion.AngleAxis(currentAngle / 2f, Vector3.up) * transform.forward;
                
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
                Gizmos.DrawRay(transform.position, attackLeft * currentRange);
                Gizmos.DrawRay(transform.position, attackRight * currentRange);
            }
        }
    }
}
