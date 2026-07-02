using UnityEngine;
using Unity.Netcode;

public class SpikePillarManager : NetworkBehaviour
{
    [Header("Cấu hình Hệ thống")]
    [Tooltip("Kéo bệ ngọc điều khiển cửa 3 vào đây (Crystal Puzzle System)")]
    public CrystalPuzzleSystem door3Pedestal;
    public SpikePillarPool pool;

    [Header("Cấu hình Spawn")]
    [Tooltip("Kéo danh sách các điểm spawn ngẫu nhiên (ví dụ 2 điểm) vào đây")]
    public Transform[] spawnPoints; 
    public Vector3 rollDirection = Vector3.forward;
    public float spawnInterval = 5.0f; // Thời gian giãn cách giữa các lần rơi trụ mới

    // Trạng thái hoạt động của bẫy được đồng bộ qua mạng
    public NetworkVariable<bool> isSpawningActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float nextSpawnTime;
    private bool doorOpened = false;
    private bool isSpawningActiveOffline = false;

    public override void OnNetworkSpawn()
    {
        isSpawningActive.OnValueChanged += OnSpawningActiveChanged;
    }

    public override void OnNetworkDespawn()
    {
        isSpawningActive.OnValueChanged -= OnSpawningActiveChanged;
    }

    private void OnSpawningActiveChanged(bool oldVal, bool newVal)
    {
        if (!newVal)
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
        bool isNetworkActive = NetworkManager != null && NetworkManager.IsListening;
        bool isServerInstance = IsServer;

        // 1. Giám sát việc mở cửa 3 (chạy trên Server hoặc Offline)
        if (!doorOpened && door3Pedestal != null)
        {
            if (!isNetworkActive || isServerInstance)
            {
                // Kiểm tra xem cả bệ này và bệ đối ứng đều đã đặt ngọc chưa
                if (door3Pedestal.IsCrystalPlaced && 
                    door3Pedestal.otherPedestal != null && 
                    door3Pedestal.otherPedestal.IsCrystalPlaced)
                {
                    doorOpened = true;
                    if (isNetworkActive)
                    {
                        isSpawningActive.Value = true;
                    }
                    else
                    {
                        isSpawningActiveOffline = true;
                    }
                    nextSpawnTime = Time.time; // Spawn ngay lập tức
                }
            }
        }

        // 2. Spawn trụ gai theo chu kỳ (Chạy trên cả Server và Client để tự spawn cục bộ)
        bool active = isNetworkActive ? isSpawningActive.Value : isSpawningActiveOffline;
        if (active && pool != null && spawnPoints != null && spawnPoints.Length > 0)
        {
            if (Time.time >= nextSpawnTime)
            {
                SpawnPillarLocal();
                nextSpawnTime = Time.time + spawnInterval;
            }
        }
    }

    private void SpawnPillarLocal()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        // Chọn ngẫu nhiên 1 trong các điểm spawn được cấu hình
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform selectedPoint = spawnPoints[randomIndex];

        if (selectedPoint == null) return;

        SpikePillarLocal pillar = pool.GetPillar();
        if (pillar != null)
        {
            pillar.Initialize(selectedPoint.position, rollDirection, pool);
        }
    }

    // API để tắt hệ thống spawn (ví dụ khi cả 4 người đã chạm safe box)
    public void DeactivateSpawning()
    {
        bool isNetworkActive = NetworkManager != null && NetworkManager.IsListening;
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
    }
}
