using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Enemy1_DapBua : NetworkBehaviour
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Run,
        Search,    // Trạng thái săn tìm player tại vị trí cuối cùng
        Stagger,   // Trạng thái bị khựng / choáng khi nhận sát thương lớn
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

    public NetworkVariable<bool> isEnraged = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Advanced AI Sync")]
    // Đồng bộ hit để mọi người thấy bị trúng đòn
    public NetworkVariable<int> hitCounter = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Đồng bộ kiểu tấn công (0: Left, 1: Right, 2: Combo)
    public NetworkVariable<int> attackType = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool isNextAttackLeft = true;

    [Header("Components")]
    public NavMeshAgent agent;
    public Animator anim;
    public Transform eyeTransform; // Vị trí mắt của Enemy để bắn Raycast

    [Header("Melee Hitbox Settings")]
    public GameObject hammerHitbox; // Kéo GameObject vùng đánh (box collider ở tay/búa) vào đây (Giữ để tránh mất ref cũ)
    public GameObject hammerHitboxLeft;  // Hitbox búa/vũ khí bên trái
    public GameObject hammerHitboxRight; // Hitbox búa/vũ khí bên phải

    [Header("AI Settings")]
    public float sightRange = 15f;      // Tầm nhìn xa
    public float fieldOfView = 90f;     // Góc nhìn (độ)
    public float attackRange = 2f;      // Khoảng cách ra đòn
    public float walkRadius = 10f;      // Bán kính đi dạo ngẫu nhiên
    public float idleTimeMax = 4f;      // Thời gian đứng im tối đa
    
    [Header("Layers")]
    public LayerMask playerLayer;       // Layer của Player
    public LayerMask obstacleLayer;     // Layer của vật cản (tường, đất...)

    [Header("Pro AI Combat Settings")]
    private Transform targetPlayer;
    private float stateTimer;
    private bool hasDestination;

    // Các biến phụ trợ cho Pro AI
    private Vector3 lastKnownPlayerPosition;
    private float searchTimer;
    private float searchLookTimer;
    private float searchLookDirection = 1f;
    
    private float detectionTimer;
    private const float DETECTION_INTERVAL = 0.15f; // Quét mục tiêu 0.15s một lần để tiết kiệm CPU

    private float staggerTimer;
    private bool hasRoared = false;

    private float attackCooldownTimer;
    private float tacticalTimer;
    private int tacticalState = 0; // 0: Chạy thẳng, 1: Né sang sườn trái, 2: Né sang sườn phải

    private bool isDodging = false;
    private float dodgeTimer;

    private float lastDamageTime;
    private int recentHitCount;

    private bool hasDealtDamage1;
    private bool hasDealtDamage2;
    private float attackDuration;

    // Bộ đệm tránh phân bổ rác (GC Alloc) khi quét va chạm
    private readonly Collider[] detectionResults = new Collider[8];
    private readonly Collider[] damageResults = new Collider[8];

    private EnemyState clientLocalState = (EnemyState)(-1);
    private int framesSinceActive = 0;

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
                Debug.LogError($"[{gameObject.name}] PHÁT HIỆN LỖI: Animator tồn tại nhưng CHƯA ĐƯỢC GÁN 'Animator Controller' trong cửa sổ Inspector của Prefab! Vui lòng kéo Animator Controller của quái 1 vào thành phần Animator của nó trong Prefab. Đang vô hiệu hóa NetworkAnimator để tránh crash game NullReferenceException.");
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
        // Lắng nghe sự thay đổi trạng thái để chạy Animation trên các Client
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
            isEnraged.Value = false;
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

        // Đảm bảo các hitbox búa ban đầu được tắt
        if (hammerHitbox != null)
        {
            hammerHitbox.SetActive(false);
        }
        if (hammerHitboxLeft != null)
        {
            hammerHitboxLeft.SetActive(false);
        }
        if (hammerHitboxRight != null)
        {
            hammerHitboxRight.SetActive(false);
        }

        // Lắng nghe hit để chạy hiệu ứng dính đòn cho mọi Client
        hitCounter.OnValueChanged += (oldVal, newVal) => {
            if (anim != null)
            {
                // Không chạy trigger Hit bình thường nếu đang thực hiện gầm rú Phẫn nộ
                if (currentState.Value == EnemyState.Stagger && isEnraged.Value && !hasRoared)
                {
                    return;
                }
                anim.SetTrigger("Hit");
            }
        };
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= OnStateChanged;
    }

    private void Update()
    {
        // Xử lý phóng to quái khi phẫn nộ (chạy trên cả Server và Client)
        if (isEnraged.Value && !hasRoared)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * 1.15f, Time.deltaTime * 3f);
            if (transform.localScale.x >= 1.14f)
            {
                hasRoared = true;
            }
        }

        // Đồng bộ hóa an toàn hoạt ảnh di chuyển trên Client
        if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
        {
            framesSinceActive++;
            if (clientLocalState != currentState.Value)
            {
                if (SyncAnimationState(currentState.Value))
                {
                    if (framesSinceActive >= 10)
                    {
                        clientLocalState = currentState.Value;
                    }
                }
            }
        }
        else
        {
            framesSinceActive = 0;
        }

        // Chỉ Server mới được phép tính toán AI
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

        // Giảm thời gian hồi đòn đánh
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        // Quét Player theo chu kỳ tối ưu thay vì mỗi frame giúp tăng hiệu năng đáng kể
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0)
        {
            detectionTimer = DETECTION_INTERVAL;
            DetectPlayer();
        }

        // Xử lý logic tùy theo trạng thái hiện tại
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
        // Nếu đã chết hoặc đang bị stagger choáng thì tạm không quét mục tiêu mới
        if (currentState.Value == EnemyState.Dead || currentState.Value == EnemyState.Stagger) return;

        // Nếu đang trong đòn đánh thì không đổi mục tiêu
        if (currentState.Value == EnemyState.Attack) return;

        int numPlayers = Physics.OverlapSphereNonAlloc(transform.position, sightRange, detectionResults, playerLayer);
        
        // Quét dự phòng theo Tag "Player" nếu LayerMask không trả về kết quả
        if (numPlayers == 0)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            int count = 0;
            foreach (var p in players)
            {
                if (count >= detectionResults.Length) break;
                if (Vector3.Distance(transform.position, p.transform.position) <= sightRange)
                {
                    Collider col = p.GetComponent<Collider>();
                    if (col != null)
                    {
                        detectionResults[count++] = col;
                    }
                }
            }
            numPlayers = count;
        }

        bool playerFound = false;
        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        for (int i = 0; i < numPlayers; i++)
        {
            Collider p = detectionResults[i];
            if (p == null) continue;
            Transform potentialTarget = p.transform;

            // Chỉ nhắm vào Player còn sống
            SimplePlayerTest playerScript = potentialTarget.GetComponentInParent<SimplePlayerTest>();
            if (playerScript != null && playerScript.currentHealth.Value <= 0)
            {
                continue;
            }

            Vector3 directionToTarget = (potentialTarget.position - eyePos).normalized;

            // Kiểm tra xem player có nằm trong góc nhìn (Field of View) không
            if (Vector3.Angle(transform.forward, directionToTarget) < fieldOfView / 2)
            {
                float distanceToTarget = Vector3.Distance(eyePos, potentialTarget.position);

                // Bắn Raycast từ mắt tới player xem có bị khuất tường không
                if (!Physics.Raycast(eyePos, directionToTarget, distanceToTarget, obstacleLayer))
                {
                    // Nhìn thấy Player -> Đuổi theo
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

        // Nếu mất dấu mục tiêu khi đang chạy
        if (!playerFound && currentState.Value == EnemyState.Run)
        {
            if (targetPlayer != null)
            {
                // Lưu vị trí cuối cùng để chuyển sang trạng thái Tìm kiếm thông minh
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
            agent.speed = 2.0f; // Tốc độ đi bộ dạo chơi
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

        // Khi đi dạo tới đích
        if (hasDestination && agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance)
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
        // 1. Trường hợp chạy tuần tra dạo chơi không có Player
        if (targetPlayer == null)
        {
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = 4.0f; // Chạy nhanh dạo chơi tuần tra
            }

            if (!hasDestination)
            {
                Vector3 randomDirection = Random.insideUnitSphere * walkRadius * 1.5f;
                randomDirection += transform.position;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, walkRadius * 1.5f, 1))
                {
                    if (agent.isActiveAndEnabled) agent.SetDestination(hit.position);
                    hasDestination = true;
                }
            }

            if (hasDestination && agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance)
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

        // Phản xạ né đòn (Dodge step): Di chuyển theo đích đến né và không đổi đích trong 0.35s
        if (isDodging)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0)
            {
                isDodging = false;
                if (agent.isActiveAndEnabled) agent.speed = isEnraged.Value ? 7.5f : 5f;
            }
            return;
        }

        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = false;
            agent.speed = isEnraged.Value ? 7.5f : 5f; 
        }

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // Cơ chế di chuyển chiến thuật vòng sườn (Circle Chase) ở cự ly gần
        if (distanceToPlayer <= 8f)
        {
            tacticalTimer -= Time.deltaTime;
            if (tacticalTimer <= 0)
            {
                // Thay đổi cách tiếp cận ngẫu nhiên (50% lao trực diện, 50% quành sang sườn trái/phải)
                float rand = Random.value;
                if (rand < 0.5f) tacticalState = 0;
                else if (rand < 0.75f) tacticalState = 1;
                else tacticalState = 2;

                tacticalTimer = Random.Range(1.2f, 2.2f);
            }

            if (tacticalState == 0)
            {
                if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
            }
            else
            {
                // Chạy bo sườn: Tính vector tiếp tuyến vuông góc với hướng tới Player
                Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
                Vector3 tangent = new Vector3(-toPlayer.z, 0, toPlayer.x);
                float sideDir = (tacticalState == 1) ? 1f : -1f;

                // Tạo điểm đích chếch về bên sườn Player
                Vector3 targetOffset = targetPlayer.position - toPlayer * 2.5f + tangent * sideDir * 3f;
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetOffset, out hit, 4f, NavMesh.AllAreas))
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
            // Khoảng cách xa thì cứ lao thẳng trực diện
            if (agent.isActiveAndEnabled) agent.SetDestination(targetPlayer.position);
        }

        // Kiểm tra khoảng cách để tấn công (chỉ đánh khi đã hết hồi chiêu)
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
            agent.speed = isEnraged.Value ? 5.5f : 4f;
            agent.SetDestination(lastKnownPlayerPosition);
        }

        // Đã đến vị trí mất dấu cuối cùng
        if (agent.isActiveAndEnabled && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.isStopped = true;
            searchTimer -= Time.deltaTime;

            // Xoay đầu quét tìm kiếm liên tục qua lại
            searchLookTimer -= Time.deltaTime;
            if (searchLookTimer <= 0)
            {
                searchLookDirection = -searchLookDirection;
                searchLookTimer = 0.8f;
            }

            transform.Rotate(Vector3.up, searchLookDirection * 120f * Time.deltaTime);

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
            if (isEnraged.Value && !hasRoared)
            {
                hasRoared = true; // Kết thúc roar chuyển sang phẫn nộ chiến đấu
            }

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

        // Rotation Lock: Chỉ xoay về hướng player trong 35% thời gian đầu (chuẩn bị vung búa)
        // Khi vung búa đập xuống (từ 35% trở đi), AI khóa hướng xoay giúp Player né được sang bên
        if (elapsed < attackDuration * 0.35f)
        {
            Vector3 lookDir = (targetPlayer.position - transform.position);
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 18f);
            }
        }

        // Tính toán gây sát thương diện rộng hình nón khớp hoàn toàn với thời điểm chạm đất của Animation
        if (attackType.Value == 2) // Combo attack (2 hit)
        {
            // Hit 1: Ở giây thứ 0.5
            if (elapsed >= 0.5f && !hasDealtDamage1)
            {
                hasDealtDamage1 = true;
                DealConeDamage(10f, attackRange + 0.5f, 80f, 6f);
            }
            // Hit 2: Slam cực mạnh ở giây thứ 1.2
            if (elapsed >= 1.2f && !hasDealtDamage2)
            {
                hasDealtDamage2 = true;
                DealConeDamage(25f, attackRange + 1.2f, 95f, 15f);
            }
        }
        else // Đòn thường Left hoặc Right
        {
            // Hit đơn: Ở giây thứ 0.5
            if (elapsed >= 0.5f && !hasDealtDamage1)
            {
                hasDealtDamage1 = true;
                DealConeDamage(15f, attackRange + 0.5f, 80f, 8f);
            }
        }

        // Đòn đánh hoàn tất
        if (stateTimer <= 0)
        {
            // Giãn cách đòn đánh (hồi chiêu) để không bị spam quá nhanh, quái phẫn nộ sẽ hồi chiêu nhanh hơn
            attackCooldownTimer = isEnraged.Value ? 0.4f : 0.8f;
            
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            if (distanceToPlayer > attackRange)
            {
                ChangeState(EnemyState.Run);
            }
            else
            {
                ChangeState(EnemyState.Run); // Chuyển về chạy để tiếp tục tính toán di chuyển sườn
            }
        }
    }

    private void DealConeDamage(float damage, float range, float angle, float knockback)
    {
        int numPlayers = Physics.OverlapSphereNonAlloc(transform.position, range, damageResults, playerLayer);
        
        // Quét dự phòng theo Tag "Player" nếu LayerMask không trả về kết quả
        if (numPlayers == 0)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            int count = 0;
            foreach (var p in players)
            {
                if (count >= damageResults.Length) break;
                if (Vector3.Distance(transform.position, p.transform.position) <= range)
                {
                    Collider col = p.GetComponent<Collider>();
                    if (col != null)
                    {
                        damageResults[count++] = col;
                    }
                }
            }
            numPlayers = count;
        }

        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 1.5f;

        for (int i = 0; i < numPlayers; i++)
        {
            Collider col = damageResults[i];
            if (col == null) continue;
            Transform player = col.transform;
            Vector3 dirToPlayer = (player.position - transform.position).normalized;

            // Kiểm tra góc hình nón phía trước mặt của Enemy
            if (Vector3.Angle(transform.forward, dirToPlayer) <= angle / 2f)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                // Đảm bảo đòn đánh không xuyên tường/vật cản
                if (!Physics.Raycast(eyePos, dirToPlayer, distanceToPlayer, obstacleLayer))
                {
                    SimplePlayerTest playerScript = player.GetComponentInParent<SimplePlayerTest>();
                    if (playerScript != null)
                    {
                        // Gây sát thương thực sự cho Player
                        playerScript.TakeDamage(damage);
                        
                        // Áp dụng lực đẩy lùi vật lý mượt mà (Knockback)
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
        hitCounter.Value++; // Tăng counter để tất cả Client chạy Anim Hit

        if (currentHealth.Value <= 0)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        // Kích hoạt Phase 2 Phẫn nộ gầm rú cực ngầu khi máu dưới 50%
        if (currentHealth.Value <= maxHealth * 0.5f && !isEnraged.Value)
        {
            isEnraged.Value = true;
            staggerTimer = 1.2f; // Khóa di chuyển 1.2s để roar gầm thét
            ChangeState(EnemyState.Stagger);
            
            // Đồng bộ tiếng thét / animation bằng cách gọi ClientRpc
            PlayRoarAnimationClientRpc();
            return;
        }

        // Tính toán sát thương nhận dồn để khựng choáng (Stagger)
        float now = Time.time;
        if (now - lastDamageTime > 3f)
        {
            recentHitCount = 0;
        }
        recentHitCount++;
        lastDamageTime = now;

        // Nếu sát thương lớn >= 25 HP hoặc bị hit 3 lần liên tiếp trong 3s thì khựng choáng (Stagger)
        bool shouldStagger = (damage >= 25f || recentHitCount >= 3) && (currentState.Value != EnemyState.Stagger);

        if (shouldStagger)
        {
            recentHitCount = 0;
            staggerTimer = 0.6f; // Thời gian choáng 0.6s
            ChangeState(EnemyState.Stagger);
        }
        else
        {
            // Phản xạ né đòn (Dodge Step) ngẫu nhiên 30% khi bị đánh lúc đang di chuyển
            if (!isDodging && Random.value < 0.3f && (currentState.Value == EnemyState.Run || currentState.Value == EnemyState.Walk))
            {
                ExecuteDodge();
            }
        }
    }

    private void ExecuteDodge()
    {
        if (targetPlayer == null) return;

        // Tính hướng né vuông góc sang sườn với hướng đối diện Player
        Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
        Vector3 perpendicular = new Vector3(-toPlayer.z, 0, toPlayer.x);
        
        // Ngẫu nhiên chọn né bên trái hay bên phải
        if (Random.value < 0.5f) perpendicular = -perpendicular;

        Vector3 dodgeTarget = transform.position + perpendicular * 3f;
        
        NavMeshHit hit;
        // Kiểm tra xem vị trí lướt né có thuộc NavMesh hợp lệ không để tránh lướt xuyên tường/kẹt
        if (NavMesh.SamplePosition(dodgeTarget, out hit, 3f, NavMesh.AllAreas))
        {
            isDodging = true;
            dodgeTimer = 0.35f; // Lướt nhanh trong 0.35s
            if (agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.speed = isEnraged.Value ? 14f : 10f; // Lướt đi với tốc độ cực cao
                agent.SetDestination(hit.position);
            }
        }
    }

    private void Die()
    {
        // Tắt agent tránh quái chết vẫn cản đường vật lý
        if (agent.isActiveAndEnabled) agent.isStopped = true;
        
        // Despawn Enemy sau 2 giây chơi animation chết
        Invoke(nameof(DespawnEnemy), 2f);
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
        // Tự động tắt hitbox vũ khí nếu trạng thái chuyển từ Attack sang trạng thái khác (tránh lỗi kẹt hitbox khi bị khựng/chết)
        if (currentState.Value == EnemyState.Attack && newState != EnemyState.Attack)
        {
            DisableWeaponHitbox();
        }

        currentState.Value = newState;

        // Reset các biến liên quan khi chuyển trạng thái
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
            searchTimer = 3.0f; // Đứng tìm kiếm 3 giây
            searchLookTimer = 0f;
            if (agent.isActiveAndEnabled) agent.isStopped = false;
        }
        if (newState == EnemyState.Stagger)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
            // Nếu staggerTimer không được gán sẵn (như gầm rú), đặt mặc định choáng 0.6s
            if (staggerTimer <= 0) staggerTimer = 0.6f;
        }
        if (newState == EnemyState.Attack)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
            
            hasDealtDamage1 = false;
            hasDealtDamage2 = false;

            // Lựa chọn kiểu đòn đánh
            if (isEnraged.Value && Random.value < 0.4f)
            {
                attackType.Value = 2; // Combo
                attackDuration = 1.8f;
            }
            else
            {
                attackType.Value = isNextAttackLeft ? 0 : 1;
                isNextAttackLeft = !isNextAttackLeft;
                attackDuration = 1.1f;
            }

            stateTimer = attackDuration;

            // Kích hoạt đồng bộ hóa hoạt ảnh tấn công qua ClientRpc
            PlayAttackAnimationClientRpc(attackType.Value);
        }
        if (newState == EnemyState.Dead) Die();
    }

    #endregion

    #region Client Animation Sync

    // Hàm này chạy trên TẤT CẢ các Client khi NetworkVariable currentState thay đổi
    private void OnStateChanged(EnemyState previousValue, EnemyState newValue)
    {
        // Gán bằng -1 để Update() tự động thực hiện đồng bộ hóa an toàn và liên tục
        clientLocalState = (EnemyState)(-1);
    }

    private bool SyncAnimationState(EnemyState newState)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;

        // Reset các trigger cũ để tránh kẹt
        anim.ResetTrigger("Idle");
        anim.ResetTrigger("Walk");
        anim.ResetTrigger("Run");
        anim.ResetTrigger("Hit");

        switch (newState)
        {
            case EnemyState.Idle:
                anim.SetTrigger("Idle"); 
                break;
            case EnemyState.Walk:
                anim.SetTrigger("Walk"); 
                break;
            case EnemyState.Run:
                anim.SetTrigger("Run");   
                break;
            case EnemyState.Stagger:
                // Nếu là Stagger đặc biệt khi gầm rú phẫn nộ (máu <= 50%), Client tự chạy Combo để gầm rú
                // Ngược lại chạy Hit bình thường
                if (isEnraged.Value && !hasRoared)
                {
                    anim.ResetTrigger("Combo");
                    anim.SetTrigger("Combo");
                }
                else
                {
                    anim.SetTrigger("Hit"); // Đồng bộ khựng choáng dính đòn lên toàn client
                }
                break;
            case EnemyState.Dead:
                anim.SetTrigger("Die");   
                break;
        }
        return true;
    }

    private void OnAttackTypeChanged(int oldVal, int newVal)
    {
        if (anim == null) return;
        
        // Reset các trigger tấn công
        anim.ResetTrigger("AttLeft");
        anim.ResetTrigger("quai1Attackphai");
        anim.ResetTrigger("Combo");

        // Gọi đúng tên trigger được thiết lập trong Animator
        if (newVal == 0) anim.SetTrigger("AttLeft");
        else if (newVal == 1) anim.SetTrigger("quai1Attackphai");
        else if (newVal == 2) anim.SetTrigger("Combo");
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc(int type)
    {
        if (anim == null) return;
        
        anim.ResetTrigger("AttLeft");
        anim.ResetTrigger("quai1Attackphai");
        anim.ResetTrigger("Combo");

        if (type == 0) anim.SetTrigger("AttLeft");
        else if (type == 1) anim.SetTrigger("quai1Attackphai");
        else if (type == 2) anim.SetTrigger("Combo");
    }

    [ClientRpc]
    private void PlayRoarAnimationClientRpc()
    {
        if (anim == null) return;
        anim.ResetTrigger("Hit");
        anim.ResetTrigger("Combo");
        anim.SetTrigger("Combo"); // Combo là hoạt ảnh tiếng thét / gầm rú phẫn nộ
    }

    #region Animation Events & Hitbox Control

    // Hàm này được gọi từ Animation Event tại frame vung búa để bật Box Collider tay/búa (hoặc cả hai)
    public void EnableWeaponHitbox()
    {
        if (hammerHitbox != null)
        {
            hammerHitbox.SetActive(true);
        }
        EnableLeftWeaponHitbox();
        EnableRightWeaponHitbox();
    }

    // Hàm này được gọi từ Animation Event tại frame vung búa xong hoặc kết thúc đòn đánh để tắt Box Collider (hoặc cả hai)
    public void DisableWeaponHitbox()
    {
        if (hammerHitbox != null)
        {
            hammerHitbox.SetActive(false);
        }
        DisableLeftWeaponHitbox();
        DisableRightWeaponHitbox();
    }

    // Bật/tắt riêng biệt cho tay/vũ khí bên trái
    public void EnableLeftWeaponHitbox()
    {
        if (hammerHitboxLeft != null)
        {
            hammerHitboxLeft.SetActive(true);
        }
    }

    public void DisableLeftWeaponHitbox()
    {
        if (hammerHitboxLeft != null)
        {
            hammerHitboxLeft.SetActive(false);
        }
    }

    // Bật/tắt riêng biệt cho tay/vũ khí bên phải
    public void EnableRightWeaponHitbox()
    {
        if (hammerHitboxRight != null)
        {
            hammerHitboxRight.SetActive(true);
        }
    }

    public void DisableRightWeaponHitbox()
    {
        if (hammerHitboxRight != null)
        {
            hammerHitboxRight.SetActive(false);
        }
    }

    #endregion

    #endregion

    // Vẽ vùng nhìn thấy và vùng đập búa trên Editor để dễ dàng debug căn cự ly
    private void OnDrawGizmosSelected()
    {
        if (eyeTransform != null)
        {
            // Vẽ vòng tròn phạm vi nhìn thấy (màu vàng)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eyeTransform.position, sightRange);

            // Vẽ góc FOV nhìn thấy của Enemy (màu vàng nhạt)
            Vector3 leftLimit = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
            Vector3 rightLimit = Quaternion.AngleAxis(fieldOfView / 2f, Vector3.up) * transform.forward;
            Gizmos.DrawRay(eyeTransform.position, leftLimit * sightRange);
            Gizmos.DrawRay(eyeTransform.position, rightLimit * sightRange);

            // Vẽ vòng tròn tầm đánh cơ bản (màu đỏ)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            
            // Vẽ góc tấn công diện rộng hình nón khi đang ở trạng thái Attack
            if (currentState.Value == EnemyState.Attack)
            {
                float currentAngle = (attackType.Value == 2) ? 95f : 80f;
                float currentRange = (attackType.Value == 2) ? attackRange + 1.2f : attackRange + 0.5f;
                Vector3 attackLeft = Quaternion.AngleAxis(-currentAngle / 2f, Vector3.up) * transform.forward;
                Vector3 attackRight = Quaternion.AngleAxis(currentAngle / 2f, Vector3.up) * transform.forward;
                Gizmos.DrawRay(transform.position, attackLeft * currentRange);
                Gizmos.DrawRay(transform.position, attackRight * currentRange);
            }
        }
    }
}