using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawner sinh quái liên tục tại vị trí chỉ định.
/// Người dùng có thể kéo thả và lựa chọn danh sách nhiều loại quái muốn sinh ra.
/// Chỉ chạy logic spawn trên Server/Host.
/// </summary>
public class ContinuousEnemySpawner : NetworkBehaviour
{
    [Header("Enemy Configurations")]
    [Tooltip("Danh sách các Prefab Kẻ Địch (Enemy) muốn sinh ra. Spawner sẽ chọn ngẫu nhiên một loại trong danh sách này mỗi đợt. Mỗi Prefab bắt buộc phải có NetworkObject.")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Tooltip("Prefab dự phòng (nếu danh sách phía trên trống)")]
    public GameObject fallbackEnemyPrefab;

    [Header("Spawn Time Settings")]
    [Tooltip("Thời gian giãn cách tối thiểu giữa các lần sinh quái (giây)")]
    public float minSpawnInterval = 8f;

    [Tooltip("Thời gian giãn cách tối đa giữa các lần sinh quái (giây)")]
    public float maxSpawnInterval = 15f;

    [Header("Spawner Limits")]
    [Tooltip("Số lượng quái tối đa còn sống đồng thời từ Spawner này")]
    public int maxEnemiesAlive = 5;

    [Tooltip("Bán kính ngẫu nhiên xung quanh Spawner để sinh quái")]
    public float spawnRadius = 3f;

    private List<GameObject> activeEnemies = new List<GameObject>();
    private float spawnTimer;

    private void Start()
    {
        // Khởi tạo thời gian spawn ngẫu nhiên cho lần đầu tiên
        ResetSpawnTimer();
    }

    private void Update()
    {
        // Chỉ Server mới thực thi việc tính toán thời gian và sinh quái
        if (!IsServer) return;

        // Đảm bảo có ít nhất 1 prefab hợp lệ để spawn
        if ((enemyPrefabs == null || enemyPrefabs.Count == 0) && fallbackEnemyPrefab == null) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0)
        {
            ResetSpawnTimer();
            TrySpawnEnemy();
        }
    }

    private void ResetSpawnTimer()
    {
        // Tính toán ngẫu nhiên thời gian cho đợt spawn kế tiếp giữa khoảng min và max
        float minVal = Mathf.Max(0.1f, minSpawnInterval);
        float maxVal = Mathf.Max(minVal, maxSpawnInterval);
        spawnTimer = Random.Range(minVal, maxVal);
    }

    private void TrySpawnEnemy()
    {
        // Dọn dẹp danh sách những quái đã chết hoặc đã biến mất khỏi Scene
        CleanActiveEnemiesList();

        // Nếu vượt quá giới hạn số lượng quái còn sống thì dừng spawn
        if (activeEnemies.Count >= maxEnemiesAlive)
        {
            return;
        }

        // Lựa chọn ngẫu nhiên một Prefab quái từ danh sách
        GameObject selectedPrefab = null;
        if (enemyPrefabs != null && enemyPrefabs.Count > 0)
        {
            // Chọn ngẫu nhiên trong danh sách, bỏ qua phần tử null
            List<GameObject> validPrefabs = new List<GameObject>();
            foreach (var p in enemyPrefabs)
            {
                if (p != null) validPrefabs.Add(p);
            }

            if (validPrefabs.Count > 0)
            {
                selectedPrefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
            }
        }

        // Nếu danh sách không có prefab hợp lệ, dùng fallbackEnemyPrefab làm dự phòng
        if (selectedPrefab == null)
        {
            selectedPrefab = fallbackEnemyPrefab;
        }

        if (selectedPrefab == null) return;

        // Tính vị trí spawn ngẫu nhiên trên mặt phẳng phẳng XZ quanh Spawner
        Vector3 spawnOffset = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            0.1f,
            Random.Range(-spawnRadius, spawnRadius)
        );
        Vector3 spawnPosition = transform.position + spawnOffset;

        // Tiến hành sinh quái
        GameObject enemyInstance = Instantiate(selectedPrefab, spawnPosition, transform.rotation);
        NetworkObject netObj = enemyInstance.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.Spawn(true);
            activeEnemies.Add(enemyInstance);
            Debug.Log($"[ContinuousEnemySpawner] Spawned {selectedPrefab.name} successfully at {spawnPosition}. Active count: {activeEnemies.Count}/{maxEnemiesAlive}. Next spawn in {spawnTimer:F1}s");
        }
        else
        {
            Debug.LogError($"[ContinuousEnemySpawner] Prefab '{selectedPrefab.name}' thiếu thành phần NetworkObject! Không thể sinh quái đồng bộ mạng.");
            Destroy(enemyInstance);
        }
    }

    private void CleanActiveEnemiesList()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemyGo = activeEnemies[i];
            if (enemyGo == null)
            {
                activeEnemies.RemoveAt(i);
                continue;
            }

            if (IsEnemyDead(enemyGo))
            {
                activeEnemies.RemoveAt(i);
            }
        }
    }

    private bool IsEnemyDead(GameObject go)
    {
        if (go == null) return true;

        var e1 = go.GetComponent<Enemy1_DapBua>();
        if (e1 != null && e1.IsDead) return true;

        var e2 = go.GetComponent<Enemy2_Zombie>();
        if (e2 != null && e2.IsDead) return true;

        var e3 = go.GetComponent<Enemy3_Buaa>();
        if (e3 != null && e3.IsDead) return true;

        var e4 = go.GetComponent<Enemy4_Bongtoi>();
        if (e4 != null && e4.IsDead) return true;

        var e5 = go.GetComponent<Enemy5_PhuThuy>();
        if (e5 != null && e5.IsDead) return true;

        return false;
    }

    // Vẽ vùng tròn hiển thị bán kính spawn quái trong Editor để dễ quan sát thiết lập
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
