using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class SpikePillarManager : NetworkBehaviour
{
    public enum DirectionMode { WorldSpace, LocalSpace }

    [Header("Cấu hình Hệ thống")]
    [Tooltip("Kéo bệ ngọc điều khiển cửa vào đây (Crystal Puzzle System). Nếu để trống code tự tìm trong Scene.")]
    public CrystalPuzzleSystem door3Pedestal;
    public SpikePillarPool pool;

    [Header("Cấu hình Spawn")]
    [Tooltip("Kéo danh sách các điểm spawn ngẫu nhiên (ví dụ 2 điểm) vào đây")]
    public Transform[] spawnPoints; 
    public DirectionMode directionMode = DirectionMode.WorldSpace;
    [Tooltip("Hướng lăn của trụ gai (Ví dụ: (0,0,-1) hoặc (1,0,0))")]
    public Vector3 rollDirection = new Vector3(0f, 0f, -1f);
    public float spawnInterval = 2.0f; // Thời gian giãn cách giữa các lần rơi trụ mới

    [Header("=== PREVIEW TRONG SCENE (EDIT MODE) ===")]
    [Tooltip("Bật hiển thị đường đi và điểm chạm đất trong Scene")]
    public bool showSceneGizmos = true;
    [Tooltip("Bật xem trước 3D con lăn chuyển động trực tiếp trong Edit Mode không cần Play")]
    public bool previewInEditMode = false;
    [Range(0f, 1f)]
    [Tooltip("Kéo thanh này từ 0 đến 1 để tua xem chuyển động rơi và lăn của trụ trong Scene")]
    public float previewTimeline = 0f;
    [Tooltip("Tự động chạy animation mượt mà trong Edit Scene")]
    public bool autoAnimatePreview = false;
    public float previewPillarRadius = 1.0f;
    public float previewPillarLength = 6.0f;
    public float previewTrajectoryLength = 50f;
    public bool previewReverseRotation = false;

    // Trạng thái hoạt động của bẫy được đồng bộ qua mạng
    public NetworkVariable<bool> isSpawningActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float nextSpawnTime;
    private bool doorOpened = false;
    private bool isSpawningActiveOffline = false;

    public Vector3 GetEffectiveRollDirection()
    {
        Vector3 dir = (directionMode == DirectionMode.LocalSpace) 
            ? transform.TransformDirection(rollDirection) 
            : rollDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        return dir.normalized;
    }

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
                pillar.Initialize(selectedPoint.position, GetEffectiveRollDirection(), pool);
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

    private void OnDrawGizmos()
    {
        if (!showSceneGizmos) return;
        DrawGizmoVisuals(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSceneGizmos) return;
        DrawGizmoVisuals(true);
    }

    private void DrawGizmoVisuals(bool isSelected)
    {
        Vector3 effDir = GetEffectiveRollDirection();
        Vector3 rollAxis = Vector3.Cross(Vector3.up, effDir).normalized;

        // Tự động tìm lại spawn points nếu mảng rỗng
        Transform[] points = spawnPoints;
        if (points == null || points.Length == 0)
        {
            List<Transform> valid = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);
                if (c.name.ToLower().Contains("gameobject") || c.name.ToLower().Contains("spawn"))
                {
                    valid.Add(c);
                }
            }
            points = valid.ToArray();
        }

        if (points == null || points.Length == 0) return;

        float animProgress = previewTimeline;
        if (autoAnimatePreview && !Application.isPlaying)
        {
#if UNITY_EDITOR
            animProgress = (float)((UnityEditor.EditorApplication.timeSinceStartup * 0.35f) % 1.0);
#endif
        }

        for (int i = 0; i < points.Length; i++)
        {
            Transform pt = points[i];
            if (pt == null) continue;

            Vector3 spawnPos = pt.position;

            // 1. Điểm spawn trên cao
            Gizmos.color = isSelected ? new Color(0f, 1f, 1f, 0.9f) : new Color(0f, 0.8f, 0.8f, 0.5f);
            Gizmos.DrawSphere(spawnPos, 0.5f);
            Gizmos.DrawWireSphere(spawnPos, previewPillarRadius);

            // 2. Tìm điểm chạm đất dưới điểm spawn
            Vector3 groundHitPos = spawnPos + Vector3.down * 15f;
            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 60f, ~LayerMask.GetMask("Player", "Ignore Raycast")))
            {
                groundHitPos = hit.point + Vector3.up * previewPillarRadius;
            }

            // Đường rơi thẳng đứng
            Gizmos.color = isSelected ? new Color(1f, 0.9f, 0.2f, 0.8f) : new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawLine(spawnPos, groundHitPos);

            // Vòng tròn đáp đất
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(groundHitPos, previewPillarRadius);

            // 3. Đường lăn trên mặt đất
            Vector3 rollEndPos = groundHitPos + effDir * previewTrajectoryLength;
            Gizmos.color = isSelected ? new Color(0f, 1f, 0.4f, 0.9f) : new Color(0f, 0.8f, 0.3f, 0.4f);
            Gizmos.DrawLine(groundHitPos, rollEndPos);

            // Mũi tên chỉ hướng lăn
            Vector3 arrowLeft = Quaternion.Euler(0, 150, 0) * effDir * 2f;
            Vector3 arrowRight = Quaternion.Euler(0, -150, 0) * effDir * 2f;
            Gizmos.DrawLine(rollEndPos, rollEndPos + arrowLeft);
            Gizmos.DrawLine(rollEndPos, rollEndPos + arrowRight);

            // Các mốc khoảng cách mỗi 10m
            for (float dist = 10f; dist < previewTrajectoryLength; dist += 10f)
            {
                Vector3 markerPos = groundHitPos + effDir * dist;
                Vector3 markerLeft = markerPos + rollAxis * (previewPillarLength * 0.5f);
                Vector3 markerRight = markerPos - rollAxis * (previewPillarLength * 0.5f);
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
                Gizmos.DrawLine(markerLeft, markerRight);
            }

            // 4. Vẽ 3D Preview con lăn mô phỏng
            if (previewInEditMode || autoAnimatePreview || isSelected)
            {
                Vector3 currentPillarPos;
                float currentRollAngle;

                // 0.0 -> 0.25: Giai đoạn rơi
                // 0.25 -> 1.0: Giai đoạn lăn
                if (animProgress <= 0.25f)
                {
                    float t = animProgress / 0.25f;
                    currentPillarPos = Vector3.Lerp(spawnPos, groundHitPos, t);
                    currentRollAngle = 0f;
                }
                else
                {
                    float t = (animProgress - 0.25f) / 0.75f;
                    float rollDist = t * previewTrajectoryLength;
                    currentPillarPos = groundHitPos + effDir * rollDist;
                    float rollRotSpeed = (rollDist / Mathf.Max(previewPillarRadius, 0.1f)) * Mathf.Rad2Deg;
                    if (previewReverseRotation) rollRotSpeed = -rollRotSpeed;
                    currentRollAngle = rollRotSpeed % 360f;
                }

                DrawWireCylinderGizmo(currentPillarPos, effDir, rollAxis, previewPillarRadius, previewPillarLength, currentRollAngle);
            }

#if UNITY_EDITOR
            if (isSelected)
            {
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(spawnPos + Vector3.up * 1f, $"[Spawn {i + 1}]\nDir: {effDir}\nY: {spawnPos.y:F1}m");
                UnityEditor.Handles.Label(groundHitPos + Vector3.up * 0.5f, $"[Landing {i + 1}]\nY: {groundHitPos.y:F1}m");
            }
#endif
        }
    }

    private void DrawWireCylinderGizmo(Vector3 center, Vector3 forwardDir, Vector3 axis, float radius, float length, float rollAngle)
    {
        Vector3 halfLength = axis * (length * 0.5f);
        Vector3 leftCap = center + halfLength;
        Vector3 rightCap = center - halfLength;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);

        // Vẽ 2 nắp tròn hai bên đầu trụ
        DrawWireCircle(leftCap, axis, radius, forwardDir, rollAngle);
        DrawWireCircle(rightCap, axis, radius, forwardDir, rollAngle);

        // Vẽ các đường thân trụ kết nối 2 đầu
        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i * 360f / segments) + rollAngle;
            Vector3 radial = Quaternion.AngleAxis(angle, axis) * Vector3.up * radius;
            Gizmos.color = (i == 0) ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(0.2f, 0.8f, 1f, 0.7f);
            Gizmos.DrawLine(leftCap + radial, rightCap + radial);
        }

        // Vẽ trục tâm
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(leftCap, rightCap);
        Gizmos.DrawSphere(center, 0.15f);

        // Vẽ mũi tên hướng lăn trên đỉnh con lăn
        Gizmos.color = Color.green;
        Vector3 top = center + Vector3.up * (radius + 0.3f);
        Gizmos.DrawLine(top, top + forwardDir * 1.5f);
    }

    private void DrawWireCircle(Vector3 center, Vector3 normal, float radius, Vector3 forwardRef, float rollAngle)
    {
        int segments = 16;
        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (i * 360f / segments) + rollAngle;
            Vector3 radial = Quaternion.AngleAxis(angle, normal) * Vector3.up * radius;
            Vector3 point = center + radial;

            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, point);
            }
            prevPoint = point;
        }

        // Vẽ nan hoa bên trong để thấy rõ bánh xe đang xoay chiều nào
        for (int i = 0; i < 4; i++)
        {
            float angle = (i * 90f) + rollAngle;
            Vector3 spoke = Quaternion.AngleAxis(angle, normal) * Vector3.up * radius;
            Gizmos.DrawLine(center, center + spoke);
        }
    }
}
