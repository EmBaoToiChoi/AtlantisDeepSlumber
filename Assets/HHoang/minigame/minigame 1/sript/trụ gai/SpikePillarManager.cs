using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class SpikePillarManager : NetworkBehaviour
{
    [Header("Cấu hình Hệ thống")]
    [Tooltip("Kéo bệ ngọc điều khiển cửa vào đây (Crystal Puzzle System). Nếu để trống code tự tìm trong Scene.")]
    public CrystalPuzzleSystem door3Pedestal;
    public SpikePillarPool pool;

    [Header("Cấu hình Spawn")]
    [Tooltip("Kéo danh sách các điểm spawn ngẫu nhiên (ví dụ 2 điểm) vào đây")]
    public Transform[] spawnPoints; 
    public Vector3 rollDirection = new Vector3(0f, 0f, -1f);
    public float spawnInterval = 2.0f; // Thời gian giãn cách giữa các lần rơi trụ mới

    // Trạng thái hoạt động của bẫy được đồng bộ qua mạng
    public NetworkVariable<bool> isSpawningActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float nextSpawnTime;
    private bool doorOpened = false;
    private bool isSpawningActiveOffline = false;

    private void Awake()
    {
        EnsureReferences();
    }

    private void Start()
    {
        EnsureReferences();
    }

    public void EnsureReferences()
    {
        if (pool == null)
        {
            pool = GetComponentInChildren<SpikePillarPool>() ?? FindAnyObjectByType<SpikePillarPool>();
        }

        if (door3Pedestal == null)
        {
            var pedestals = FindObjectsByType<CrystalPuzzleSystem>(FindObjectsSortMode.None);
            if (pedestals != null && pedestals.Length > 0)
            {
                door3Pedestal = pedestals[0];
                Debug.Log($"[SpikePillarManager] Tự động liên kết door3Pedestal: '{door3Pedestal.gameObject.name}'");
            }
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            List<Transform> validPoints = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.ToLower().Contains("gameobject") || child.name.ToLower().Contains("spawn"))
                {
                    validPoints.Add(child);
                }
            }
            if (validPoints.Count > 0)
            {
                spawnPoints = validPoints.ToArray();
                Debug.Log($"[SpikePillarManager] Tự động tìm thấy {spawnPoints.Length} điểm spawn con.");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isSpawningActive.OnValueChanged += OnSpawningActiveChanged;
        if (isSpawningActive.Value)
        {
            doorOpened = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        isSpawningActive.OnValueChanged -= OnSpawningActiveChanged;
    }

    private void OnSpawningActiveChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            doorOpened = true;
        }
        else
        {
            // Khi dừng hoạt động, thu hồi toàn bộ trụ về pool
            if (pool != null)
            {
                pool.RecallAll();
            }
        }
    }

    void Update()
    {
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool isServerInstance = !isNetworkActive || IsServer;

        // 1. Giám sát việc mở cửa (chạy trên Server hoặc Offline)
        if (!doorOpened && isServerInstance)
        {
            EnsureReferences();

            bool shouldOpen = false;

            // Kiểm tra Cách 1: Bệ ngọc được chỉ định trực tiếp
            if (door3Pedestal != null)
            {
                if (door3Pedestal.IsCrystalPlaced)
                {
                    if (door3Pedestal.otherPedestal == null || door3Pedestal.otherPedestal.IsCrystalPlaced)
                    {
                        shouldOpen = true;
                    }
                }
            }

            // Kiểm tra Cách 2: Tất cả bệ ngọc (CrystalPuzzleSystem) trong Scene
            if (!shouldOpen)
            {
                var allPedestals = FindObjectsByType<CrystalPuzzleSystem>(FindObjectsSortMode.None);
                if (allPedestals != null && allPedestals.Length > 0)
                {
                    int placedCount = 0;
                    foreach (var p in allPedestals)
                    {
                        if (p != null && p.IsCrystalPlaced) placedCount++;
                    }
                    if (placedCount >= allPedestals.Length || placedCount >= 2)
                    {
                        shouldOpen = true;
                    }
                }
            }

            // Kiểm tra Cách 3: Quest trigger đặt ngọc đã hoàn thành
            if (!shouldOpen)
            {
                var crystalQuest = FindAnyObjectByType<CrystalPuzzleQuestTrigger>();
                if (crystalQuest != null && crystalQuest.IsQuestCompleted)
                {
                    shouldOpen = true;
                }
            }

            if (shouldOpen)
            {
                ActivateSpawning();
            }
        }

        // 2. Spawn trụ gai theo chu kỳ (Chỉ Server/Host đếm giờ để chọn điểm spawn ngẫu nhiên và đồng bộ qua ClientRpc)
        bool active = isNetworkActive ? isSpawningActive.Value : isSpawningActiveOffline;
        if (active && isServerInstance && pool != null && spawnPoints != null && spawnPoints.Length > 0)
        {
            if (Time.time >= nextSpawnTime)
            {
                nextSpawnTime = Time.time + spawnInterval;
                int randomIndex = Random.Range(0, spawnPoints.Length);

                if (isNetworkActive)
                {
                    SpawnPillarClientRpc(randomIndex);
                }
                else
                {
                    SpawnPillarLocalWithIndex(randomIndex);
                }
            }
        }
    }

    public void ActivateSpawning()
    {
        if (doorOpened && (isSpawningActive.Value || isSpawningActiveOffline)) return;
        doorOpened = true;

        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive)
        {
            if (IsServer)
            {
                isSpawningActive.Value = true;
            }
        }
        else
        {
            isSpawningActiveOffline = true;
        }

        nextSpawnTime = Time.time; // Spawn ngay lập tức
        Debug.Log("[SpikePillarManager] Kích hoạt spawn bánh răng xoay / trụ gai thành công!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive && !IsServer) return;

        // Nếu người chơi đi vào vùng hành lang / trigger của SpikePillarSystem
        if (IsPlayer(other.gameObject))
        {
            if (!doorOpened || !(isNetworkActive ? isSpawningActive.Value : isSpawningActiveOffline))
            {
                Debug.Log($"[SpikePillarManager] Người chơi '{other.gameObject.name}' bước vào hành lang -> Kích hoạt bánh răng xoay!");
                ActivateSpawning();
            }
        }
    }

    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;
        if (go.CompareTag("Player")) return true;
        if (go.GetComponent<IPlayerHUDTarget>() != null || go.GetComponentInParent<IPlayerHUDTarget>() != null) return true;
        string nameLower = go.name.ToLower();
        return nameLower.Contains("player") || nameLower.Contains("leo") || nameLower.Contains("maya") || nameLower.Contains("elena") || nameLower.Contains("arthur");
    }

    [ClientRpc]
    private void SpawnPillarClientRpc(int spawnIndex)
    {
        SpawnPillarLocalWithIndex(spawnIndex);
    }

    private void SpawnPillarLocalWithIndex(int spawnIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (spawnIndex < 0 || spawnIndex >= spawnPoints.Length) return;

        Transform selectedPoint = spawnPoints[spawnIndex];
        if (selectedPoint == null) return;

        if (pool == null)
        {
            pool = GetComponentInChildren<SpikePillarPool>() ?? FindAnyObjectByType<SpikePillarPool>();
        }

        if (pool != null)
        {
            SpikePillarLocal pillar = pool.GetPillar();
            if (pillar != null)
            {
                pillar.Initialize(selectedPoint.position, rollDirection, pool);
            }
        }
    }

    // API để tắt hệ thống spawn (ví dụ khi cả 4 người đã chạm safe box)
    public void DeactivateSpawning()
    {
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive)
        {
            if (IsServer)
            {
                isSpawningActive.Value = false;
            }
        }
        else
        {
            isSpawningActiveOffline = false;
            if (pool != null)
            {
                pool.RecallAll();
            }
        }
        Debug.Log("[SpikePillarManager] Đã dừng spawn bánh răng xoay và thu hồi toàn bộ trụ về pool.");
    }
}
