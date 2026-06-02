using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))] // Yêu cầu Rigidbody trên chính cổng check để kích hoạt va chạm Trigger cực kỳ chính xác
public class GateEnemySpawner : NetworkBehaviour
{
    [Header("Spawner Configuration")]
    [Tooltip("Kéo thả Prefab của Kẻ Địch (Enemy) bạn muốn sinh ra tại đây. Chú ý: Prefab phải có component NetworkObject để đồng bộ chơi mạng.")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("Số lượng Kẻ Địch muốn sinh ra")]
    [SerializeField] private int spawnCount = 3;

    [Tooltip("Kéo thả các GameObject vị trí Spawn (SpawnPoints) trong Scene vào đây. Nếu trống, quái sẽ tự spawn ngẫu nhiên quanh cổng.")]
    [SerializeField] private Transform[] spawnPositions;

    [Tooltip("Tự hủy Vùng Kích Hoạt này sau khi đã sinh quái thành công")]
    [SerializeField] private bool triggerOnlyOnce = true;

    private BoxCollider triggerCollider;
    private Rigidbody rb;
    private bool hasSpawned = false;

    private void Awake()
    {
        // Tự động reset trạng thái đối thoại về ban đầu khi bắt đầu Game/Lượt chơi mới
        PlayerPrefs.SetInt("RakanDialogueFinished", 0);
        PlayerPrefs.Save();
        RakanDialogueController.HasFinishedStoryOnce = false;

        // Thiết lập BoxCollider làm Trigger
        triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null) triggerCollider = gameObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;

        // Tự động cấu hình Rigidbody vật lý trên chính hộp check va chạm
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;  // Tránh việc hộp va chạm bị đẩy hoặc rơi bởi lực vật lý
        rb.useGravity = false;  // Tránh việc hộp va chạm rơi tự do do trọng lực
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Chỉ kích hoạt nếu chưa sinh quái lần nào
        if (hasSpawned) return;

        // 2. Kiểm tra nếu đối tượng va chạm là người chơi (Player)
        LeoPlayer player = other.GetComponentInParent<LeoPlayer>();
        if (player == null) player = other.GetComponentInChildren<LeoPlayer>();
        if (player == null) player = other.GetComponent<LeoPlayer>();

        if (player != null)
        {
            // Chỉ chạy kiểm tra đối thoại trên Client của chính người chơi đi qua cửa (Local Player)
            bool isLocalPlayer = player.isStandaloneMode || player.IsOwner;
            if (isLocalPlayer)
            {
                // Kiểm tra xem đã hoàn thành cuộc đối thoại Atlantis với Rakan chưa
                bool hasFinishedDialogue = PlayerPrefs.GetInt("RakanDialogueFinished", 0) == 1 
                                           || RakanDialogueController.HasFinishedStoryOnce;

                if (hasFinishedDialogue)
                {
                    Debug.Log("[GateEnemySpawner] Người chơi đi qua cổng sau khi nghe Rakan kể chuyện! Tiến hành sinh quái.");
                    
                    // Đánh dấu đã kích hoạt
                    hasSpawned = true;

                    // Thực thi cơ chế sinh quái
                    if (player.isStandaloneMode)
                    {
                        // Chơi Offline: Sinh quái trực tiếp cục bộ
                        ExecuteLocalSpawn();
                    }
                    else
                    {
                        // Chơi Mạng: Gửi yêu cầu lên Server để Server sinh quái đồng bộ cho cả phòng
                        RequestSpawnEnemiesServerRpc();
                    }

                    // Tự hủy trigger nếu chọn chỉ kích hoạt 1 lần
                    if (triggerOnlyOnce)
                    {
                        // Trì hoãn 1 chút để các gói tin RPC kịp gửi đi trước khi hủy object
                        if (IsServer)
                        {
                            GetComponent<NetworkObject>().Despawn(true);
                        }
                        else
                        {
                            Destroy(gameObject, 0.5f);
                        }
                    }
                }
                else
                {
                    Debug.Log("[GateEnemySpawner] Người chơi đi qua cổng nhưng chưa hoàn thành cuộc trò chuyện với Rakan. Chưa sinh quái.");
                }
            }
        }
    }

    /// <summary>
    /// Gửi yêu cầu lên Server để thực thi sinh quái đồng bộ mạng
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnEnemiesServerRpc()
    {
        ExecuteNetworkSpawn();
    }

    /// <summary>
    /// Thực thi sinh quái cục bộ (Dành cho chơi đơn)
    /// </summary>
    private void ExecuteLocalSpawn()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[GateEnemySpawner] Chưa kéo thả enemyPrefab vào Inspector!");
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            Quaternion spawnRot = Quaternion.identity;
            
            Instantiate(enemyPrefab, spawnPos, spawnRot);
            Debug.Log($"[GateEnemySpawner - Offline] Đã sinh quái {enemyPrefab.name} thành công tại vị trí {spawnPos}");
        }
    }

    /// <summary>
    /// Thực thi sinh quái đồng bộ qua mạng (Chỉ Server có quyền chạy)
    /// </summary>
    private void ExecuteNetworkSpawn()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[GateEnemySpawner - Netcode] Chưa kéo thả enemyPrefab vào Inspector!");
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            Quaternion spawnRot = Quaternion.identity;

            GameObject enemyInstance = Instantiate(enemyPrefab, spawnPos, spawnRot);
            NetworkObject netObj = enemyInstance.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                // Gọi Spawn của Netcode để đồng bộ con quái này lên tất cả các Client khác
                netObj.Spawn(true);
                Debug.Log($"[GateEnemySpawner - Netcode] Server đã sinh quái & đồng bộ {enemyPrefab.name} tại {spawnPos}");
            }
            else
            {
                Debug.LogError($"[GateEnemySpawner - Netcode] Prefab Kẻ Địch '{enemyPrefab.name}' KHÔNG có component NetworkObject! Không thể đồng bộ qua mạng co-op.");
            }
        }
    }

    /// <summary>
    /// Lấy tọa độ spawn từ mảng cấu hình (nếu trống sẽ tự động random xung quanh cổng)
    /// </summary>
    private Vector3 GetSpawnPosition(int index)
    {
        if (spawnPositions != null && spawnPositions.Length > 0)
        {
            int posIndex = index % spawnPositions.Length;
            if (spawnPositions[posIndex] != null)
            {
                return spawnPositions[posIndex].position;
            }
        }

        // Tự động random lệch xung quanh tâm của Cổng
        return transform.position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
    }
}
