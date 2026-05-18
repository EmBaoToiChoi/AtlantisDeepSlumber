using UnityEngine;
using UnityEngine.AI; // Dùng NavMesh
using Unity.Netcode;  // Dùng Netcode mạng

public class MonsterAI : NetworkBehaviour
{
    // Tạo các trạng thái cho Quái
    public enum MonsterState { Idle, Chasing, Patrolling }

    private NavMeshAgent agent;

    [Header("Cấu hình Trạng Thái & Tốc Độ")]
    public MonsterState currentState = MonsterState.Idle;
    public float chaseSpeed = 8f;
    public float patrolSpeed = 2.5f; // Đi tuần chậm rãi, rón rén

    [Header("Cấu hình Tầm Nghe")]
    public float hearingRadius = 20f;

    [Header("Cấu hình Đi Tuần (Patrol)")]
    [Tooltip("Bán kính tối đa quái có thể chọn điểm để đi tuần xung quanh nó")]
    public float patrolRadius = 15f; 
    [Tooltip("Thời gian đứng im ngơ ngác CHỈ KHI chạy tới chỗ tiếng động xong (giây)")]
    public float idleDuration = 3f; 

    private Vector3 lastNoisePosition;
    private float checkTimer = 0f;
    private float checkInterval = 0.2f; // Quét tiếng động 5 lần/giây cho nhẹ Server

    private float idleTimer = 0f; // Bộ đếm thời gian đứng im
    private bool arrivedAtNoise = false; // Đánh dấu đã đến điểm tiếng động chưa để tránh lặp trạng thái

    // Hàm này chạy khi con quái xuất hiện trên mạng
    public override void OnNetworkSpawn()
    {
        agent = GetComponent<NavMeshAgent>();

        // NẾU LÀ CLIENT: Tắt luôn NavMeshAgent để tránh giật lag đồng bộ vị trí với Server
        if (!IsServer)
        {
            agent.enabled = false; 
            return;
        }

        // CHỈ CHẠY TRÊN SERVER: Cấu hình Rigidbody để chống lỗi quái xoay vòng vòng hoặc bay lên trời
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.freezeRotation = true; // Khóa xoay vật lý hoàn toàn
            rb.isKinematic = true;    // Biến quái thành vật thể vững chắc, player không đẩy lệch hướng được
        }

        // Vừa vào game một cái là bắt nó vô trạng thái đi tuần liền
        arrivedAtNoise = false;
        SwitchState(MonsterState.Patrolling);
    }

    void Update()
    {
        // QUY TẮC VÀNG: Chỉ Server mới xử lý logic AI
        if (!IsServer) return;

        // 1. LIÊN TỤC NGHE NGÓNG: Dù đang làm gì, hễ nghe tiếng động là bỏ hết để đi đuổi theo liền
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            ListenForPlayers();
        }

        // 2. XỬ LÝ LOGIC THEO TỪNG TRẠNG THÁI
        switch (currentState)
        {
            case MonsterState.Idle:
                HandleIdleState();
                break;

            case MonsterState.Chasing:
                HandleChasingState();
                break;

            case MonsterState.Patrolling:
                HandlePatrollingState();
                break;
        }
    }

    // --- HÀM CHUYỂN TRẠNG THÁI ---
    private void SwitchState(MonsterState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case MonsterState.Idle:
                if (agent.gameObject.activeInHierarchy && agent.enabled)
                {
                    agent.ResetPath(); // Ra lệnh dừng lại lập tức khi tới nơi có tiếng động
                }
                idleTimer = 0f; // Reset bộ đếm giây đứng im
                break;

            case MonsterState.Chasing:
                arrivedAtNoise = false;   // Reset đánh dấu khi có mục tiêu đuổi theo mới
                agent.speed = chaseSpeed; // Tăng tốc chạy đi săn
                agent.SetDestination(lastNoisePosition); // Đặt điểm đến ngay lập tức
                break;

            case MonsterState.Patrolling:
                arrivedAtNoise = false;
                agent.speed = patrolSpeed; // Đi tuần chậm rãi
                MoveToRandomPatrolPoint(); // Tìm điểm tuần ngẫu nhiên và di chuyển liền
                break;
        }
    }

    // --- 1. XỬ LÝ NGHE TIẾNG ĐỘNG CỦA PLAYER ---
    private void ListenForPlayers()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, hearingRadius);
        foreach (var col in hitColliders)
        {
            if (col.CompareTag("Player"))
            {
                PlayerMovement player = col.GetComponent<PlayerMovement>();
                
                if (player != null && player.IsMakingNoise())
                {
                    // Phát hiện tiếng động! Lưu vị trí mới nhất
                    lastNoisePosition = player.transform.position;
                    
                    if (currentState != MonsterState.Chasing)
                    {
                        SwitchState(MonsterState.Chasing);
                    }
                    else
                    {
                        // Nếu đang trong trạng thái dí rồi mà nghe tiếng động mới thì cập nhật tiếp tọa độ
                        agent.SetDestination(lastNoisePosition);
                    }
                    break; 
                }
            }
        }
    }

    // --- 2. LOGIC TRẠNG THÁI ĐỨNG IM (IDLE) ---
    private void HandleIdleState()
    {
        idleTimer += Time.deltaTime;

        // Đứng im tại chỗ tiếng động đủ 3 giây thì tự chuyển sang đi tuần tiếp
        if (idleTimer >= idleDuration)
        {
            SwitchState(MonsterState.Patrolling);
        }
    }

    // --- 3. LOGIC TRẠNG THÁI ĐUỔI THEO TIẾNG ĐỘNG (CHASING) ---
    private void HandleChasingState()
    {
        if (arrivedAtNoise) return;

        // Chỉ kiểm tra khoảng cách khi NavMesh đã tính xong đường đi
        if (!agent.pathPending)
        {
            // Kiểm tra xem quái đã chạy tới sát điểm phát ra tiếng động chưa
            if (agent.remainingDistance <= agent.stoppingDistance + 0.5f)
            {
                arrivedAtNoise = true; // Đánh dấu đã đến nơi
                SwitchState(MonsterState.Idle); // CHỈ CHỖ NÀY mới được đứng im 3s theo ý ông
            }
            else if (!agent.hasPath)
            {
                SwitchState(MonsterState.Patrolling); // Nếu mất đường đột ngột thì đi tuần luôn
            }
        }
    }

    // --- 4. LOGIC TRẠNG THÁI ĐI TUẦN (PATROLLING) (Đã sửa đổi đi tuần liên tục không nghỉ) ---
    private void HandlePatrollingState()
    {
        // Chỉ check khoảng cách khi hệ thống đã load xong đường đi mới
        if (!agent.pathPending && agent.hasPath)
        {
            // Kiểm tra xem quái đã đi bộ tới cái điểm tuần ngẫu nhiên đó chưa
            if (agent.remainingDistance <= agent.stoppingDistance + 0.5f)
            {
                // ĐÃ SỬA: Tới nơi phát là tự động tìm điểm tuần khác để đi tiếp luôn, không chuyển sang Idle nữa!
                MoveToRandomPatrolPoint();
            }
        }
        else if (!agent.hasPath && !agent.pathPending)
        {
            // Nếu bị kẹt đường hoặc điểm chọn lỗi, tự động đổi sang điểm tuần khác liền
            MoveToRandomPatrolPoint();
        }
    }

    // Hàm tự động tìm 1 tọa độ ngẫu nhiên nằm TRÊN LƯỚI NAVMESH để quái đi tuần
    private void MoveToRandomPatrolPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += transform.position; 
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, 3.0f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return; 
            }
        }
    }

    // --- HÀM MỚI TÍCH HỢP: TIẾP NHẬN VỊ TRÍ KHI PLAYER BỊ VÙNG NHÌN PHÁT HIỆN ---
    public void SpottedPlayerByVision(Vector3 playerPosition)
    {
        if (!IsServer) return;

        // Lưu lại vị trí nhìn thấy người chơi làm mục tiêu dí theo
        lastNoisePosition = playerPosition;

        // Nếu quái đang đi tuần hoặc đứng im mà nhìn thấy Player lọt vào quạt đỏ, lập tức dí ngay!
        if (currentState != MonsterState.Chasing)
        {
            SwitchState(MonsterState.Chasing);
        }
        else
        {
            // Nếu đang dí sẵn rồi thì liên tục cập nhật đường chạy bám đuôi theo Player
            agent.SetDestination(lastNoisePosition);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}