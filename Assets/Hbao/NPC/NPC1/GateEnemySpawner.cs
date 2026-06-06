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

    private System.Collections.Generic.List<GameObject> spawnedEnemies = new System.Collections.Generic.List<GameObject>();
    private bool monitoringEnemies = false;
    private RakanNPC rakanNPC;

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

        // Tắt BoxCollider ban đầu, chỉ bật lên khi Rakan nói chuyện xong
        triggerCollider.enabled = false;

        // Tự động cấu hình Rigidbody vật lý trên chính hộp check va chạm
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;  // Tránh việc hộp va chạm bị đẩy hoặc rơi bởi lực vật lý
        rb.useGravity = false;  // Tránh việc hộp va chạm rơi tự do do trọng lực
    }

    private void Start()
    {
        rakanNPC = FindAnyObjectByType<RakanNPC>();
    }

    /// <summary>
    /// Được gọi từ RakanNPC khi kết thúc cuộc đối thoại cốt truyện thành công
    /// </summary>
    public void EnableTriggerBox()
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
            Debug.Log("[GateEnemySpawner] Box Trigger đã được bật! Sẵn sàng sinh quái khi có bất kỳ Player nào đi vào.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Chỉ kích hoạt nếu chưa sinh quái lần nào
        if (hasSpawned) return;

        // 2. Kiểm tra nếu đối tượng va chạm là bất kỳ người chơi nào (Leo, Elena, Arthur, Maya...)
        IPlayerHUDTarget player = other.GetComponentInParent<IPlayerHUDTarget>();
        if (player == null) player = other.GetComponentInChildren<IPlayerHUDTarget>();
        if (player == null) player = other.GetComponent<IPlayerHUDTarget>();

        if (player != null)
        {
            // Chỉ chạy kiểm tra đối thoại trên Client của chính người chơi đi qua cửa (Local Player)
            bool isLocalPlayer = player.IsStandaloneMode || player.IsOwner;
            if (isLocalPlayer)
            {
                // Kiểm tra xem đã hoàn thành cuộc đối thoại Atlantis với Rakan chưa
                bool hasFinishedDialogue = PlayerPrefs.GetInt("RakanDialogueFinished", 0) == 1 
                                           || RakanDialogueController.HasFinishedStoryOnce;

                if (hasFinishedDialogue)
                {
                    Debug.Log($"[GateEnemySpawner] Player '{player.gameObject.name}' chạm vào cổng trigger! Tiến hành sinh quái ngay lập tức.");
                    
                    // Đánh dấu đã kích hoạt
                    hasSpawned = true;

                    if (triggerCollider != null)
                    {
                        triggerCollider.enabled = false; // Tắt va chạm để tránh kích hoạt lại
                    }

                    // Thực thi cơ chế sinh quái
                    if (player.IsStandaloneMode)
                    {
                        // Chơi Offline: Sinh quái trực tiếp cục bộ
                        ExecuteLocalSpawn();
                    }
                    else
                    {
                        // Chơi Mạng: Gửi yêu cầu lên Server để Server sinh quái đồng bộ cho cả phòng
                        RequestSpawnEnemiesServerRpc();
                    }
                }
                else
                {
                    Debug.Log("[GateEnemySpawner] Có người chơi chạm vào cổng nhưng Rakan chưa kể xong chuyện. Không sinh quái.");
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
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
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

        spawnedEnemies.Clear();
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            Quaternion spawnRot = Quaternion.identity;
            
            GameObject enemyInstance = Instantiate(enemyPrefab, spawnPos, spawnRot);
            spawnedEnemies.Add(enemyInstance);
            Debug.Log($"[GateEnemySpawner - Offline] Đã sinh quái {enemyPrefab.name} thành công tại vị trí {spawnPos}");
        }
        monitoringEnemies = true;
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

        spawnedEnemies.Clear();
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            Quaternion spawnRot = Quaternion.identity;

            GameObject enemyInstance = Instantiate(enemyPrefab, spawnPos, spawnRot);
            NetworkObject netObj = enemyInstance.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn(true);
                spawnedEnemies.Add(enemyInstance);
                Debug.Log($"[GateEnemySpawner - Netcode] Server đã sinh quái & đồng bộ {enemyPrefab.name} tại {spawnPos}");
            }
            else
            {
                Debug.LogError($"[GateEnemySpawner - Netcode] Prefab Kẻ Địch '{enemyPrefab.name}' KHÔNG có component NetworkObject! Không thể đồng bộ qua mạng co-op.");
            }
        }
        monitoringEnemies = true;
    }

    private void Update()
    {
        if (!monitoringEnemies) return;

        bool allDead = true;
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemyGo = spawnedEnemies[i];
            if (enemyGo == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }

            bool isEnemyDead = false;
            var e1 = enemyGo.GetComponent<Enemy1_DapBua>();
            if (e1 != null && e1.IsDead) isEnemyDead = true;
            
            var e2 = enemyGo.GetComponent<Enemy2_Zombie>();
            if (e2 != null && e2.IsDead) isEnemyDead = true;

            var e3 = enemyGo.GetComponent<Enemy3_Buaa>();
            if (e3 != null && e3.IsDead) isEnemyDead = true;

            var e4 = enemyGo.GetComponent<Enemy4_Bongtoi>();
            if (e4 != null && e4.IsDead) isEnemyDead = true;

            var e5 = enemyGo.GetComponent<Enemy5_PhuThuy>();
            if (e5 != null && e5.IsDead) isEnemyDead = true;

            if (!isEnemyDead)
            {
                allDead = false;
            }
            else
            {
                spawnedEnemies.RemoveAt(i);
            }
        }

        if (allDead && spawnedEnemies.Count == 0)
        {
            monitoringEnemies = false;
            OnAllEnemiesDefeated();
        }
    }

    private void OnAllEnemiesDefeated()
    {
        Debug.Log("[GateEnemySpawner] Tất cả kẻ địch sinh ra từ Cổng đã bị tiêu diệt hoàn toàn!");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                // Thông báo tới tất cả client và cập nhật trạng thái Rakan NPC
                OnAllEnemiesDefeatedClientRpc();
                
                // Hủy Spawner trên server sau khi đã phát ClientRpc thành công
                Invoke(nameof(DespawnSpawner), 0.5f);
            }
        }
        else
        {
            // Offline mode: Cập nhật trực tiếp cục bộ
            if (rakanNPC != null)
            {
                rakanNPC.SetEnemiesDefeatedLocal();
            }

            // Hiển thị thông báo trên HUD cục bộ
            var hud = FindAnyObjectByType<PlayerHUDController>();
            if (hud != null)
            {
                hud.ShowMissionAlert("Hãy đi gặp lão Rakan. Ông ấy có điều gì đó muốn nói với bạn.");
            }

            // Tự hủy local spawner
            if (triggerOnlyOnce)
            {
                Destroy(gameObject, 0.5f);
            }
        }
    }

    private void DespawnSpawner()
    {
        if (IsSpawned && triggerOnlyOnce)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    [ClientRpc]
    private void OnAllEnemiesDefeatedClientRpc()
    {
        // Standalone hoặc Client: cập nhật NPC Rakan
        if (rakanNPC == null)
        {
            rakanNPC = FindAnyObjectByType<RakanNPC>();
        }
        if (rakanNPC != null)
        {
            rakanNPC.SetEnemiesDefeatedLocal();
        }

        // Hiển thị thông báo trên HUD
        var hud = FindAnyObjectByType<PlayerHUDController>();
        if (hud != null)
        {
            hud.ShowMissionAlert("Hãy đi gặp lão Rakan. Ông ấy có điều gì đó muốn nói với bạn.");
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
