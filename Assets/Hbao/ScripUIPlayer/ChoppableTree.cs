using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class ChoppableTree : NetworkBehaviour
{
    [Header("Tree Settings")]
    [Tooltip("Model hiển thị của cây (sẽ dùng để lắc khi bị chém)")]
    public GameObject visualModel;
    
    [Tooltip("Số lượt chém để cây đổ/biến mất")]
    public int requiredHits = 3;
    
    [Tooltip("Prefab thanh gỗ thu thập (gắn CollectibleItemDrop)")]
    public GameObject woodLogPrefab;

    [Tooltip("Prefab gốc cây custom (ví dụ aspen-stump) xuất hiện bên dưới thân cây khi cây bị chặt")]
    public GameObject treeStumpPrefab;

    [Tooltip("Góc xoay (X, Y, Z) cho gốc cây theo đúng thông số bạn cài đặt trong Inspector")]
    public Vector3 stumpRotationOffset = Vector3.zero;

    [Tooltip("Độ bù tinh chỉnh độ cao Y (m) cho gốc cây")]
    public float stumpYOffset = 0f;

    [Tooltip("Độ bù vị trí (X, Y, Z) cho gốc cây (nếu muốn dịch chuyển thêm)")]
    public Vector3 stumpPositionOffset = Vector3.zero;

    [Header("--- HIỆU ỨNG VĂNG & GỘP MẢNH GỖ NHỎ ---")]
    [Tooltip("Prefab mảnh gỗ đơn lẻ (firewood_single) văng ra trước khi gộp thành bó gỗ. Nếu để trống sẽ tự động tìm kiếm prefab firewood_single.")]
    public GameObject firewoodChipPrefab;

    [Tooltip("Số lượng mảnh gỗ nhỏ văng vãi ra đất trước khi gộp thành 1 bó gỗ (Mặc định 6 mảnh)")]
    public int scatteredChipCount = 6;

    [Tooltip("Thời gian các mảnh gỗ nhỏ văng ra đất (giây)")]
    public float chipScatterDuration = 0.75f;

    [Tooltip("Thời gian các mảnh gỗ nhỏ bay tụ lại gộp thành bó gỗ (giây)")]
    public float chipMergeDuration = 0.45f;

    // Trạng thái mạng đồng bộ
    public NetworkVariable<bool> isCutDown = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private int currentHits = 0;
    private bool isShaking = false;
    private Vector3 originalLocalPos;
    private GameObject spawnedStump;

    private void Awake()
    {
        // Đảm bảo cây có Rigidbody kinematic để Unity đăng ký và gửi sự kiện va chạm đầy đủ
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // Tự động tìm và gắn cầu nối va chạm (bao gồm cả Trigger và Collision) cho toàn bộ Collider con của cây
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            var forwarder = col.gameObject.GetComponent<TreeColliderForwarder>();
            if (forwarder == null)
            {
                forwarder = col.gameObject.AddComponent<TreeColliderForwarder>();
            }
            forwarder.mainTree = this;
        }
    }

    private void Start()
    {
        if (visualModel == null)
        {
            visualModel = gameObject; // Fallback
        }
        originalLocalPos = visualModel.transform.localPosition;
        ResolveWoodLogPrefab();
    }

    private void ResolveWoodLogPrefab()
    {
#if UNITY_EDITOR
        if (firewoodChipPrefab == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("firewood_single t:Prefab");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                firewoodChipPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Debug.Log($"[ChoppableTree] {name}: Editor auto-loaded firewoodChipPrefab: {path}");
            }
        }
        if (woodLogPrefab == null || woodLogPrefab == gameObject || woodLogPrefab.GetComponent<ChoppableTree>() != null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("wood_stack t:Prefab");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                woodLogPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Debug.Log($"[ChoppableTree] {name}: Editor auto-loaded woodLogPrefab: {path}");
            }
        }
#endif

        if (woodLogPrefab == null || woodLogPrefab == gameObject || woodLogPrefab.GetComponent<ChoppableTree>() != null)
        {
            Debug.LogWarning($"[ChoppableTree] {name}: woodLogPrefab chưa được cấu hình đúng. Đang tự động tìm kiếm...");
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null && NetworkManager.Singleton.NetworkConfig.Prefabs != null && NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs != null)
            {
                foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (networkPrefab.Prefab != null)
                    {
                        var col = networkPrefab.Prefab.GetComponent<CollectibleItemDrop>();
                        if (col != null && (col.itemName.Equals("wood_stack", System.StringComparison.OrdinalIgnoreCase) ||
                                            col.itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                                            col.itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase)))
                        {
                            woodLogPrefab = networkPrefab.Prefab;
                            Debug.Log($"[ChoppableTree] {name}: Tự động cấu hình woodLogPrefab thành công qua CollectibleItemDrop: {woodLogPrefab.name}");
                            return;
                        }
                    }
                }

                foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (networkPrefab.Prefab != null)
                    {
                        string pName = networkPrefab.Prefab.name.ToLower();
                        if (pName.Contains("wood_stack") || pName.Contains("firewood") || pName.Contains("woodlog") || pName.Contains("thanhgo"))
                        {
                            woodLogPrefab = networkPrefab.Prefab;
                            Debug.Log($"[ChoppableTree] {name}: Tự động cấu hình woodLogPrefab thành công qua tên prefab: {woodLogPrefab.name}");
                            return;
                        }
                    }
                }
            }

            GameObject loaded = Resources.Load<GameObject>("wood_stack");
            if (loaded == null) loaded = Resources.Load<GameObject>("firewood_single");
            if (loaded == null) loaded = Resources.Load<GameObject>("WoodLog");

            if (loaded != null)
            {
                woodLogPrefab = loaded;
                Debug.Log($"[ChoppableTree] {name}: Tự động cấu hình woodLogPrefab thành công từ Resources: {woodLogPrefab.name}");
                return;
            }

            Debug.LogError($"[ChoppableTree] {name}: Không thể tìm thấy WoodLog prefab hợp lệ!");
        }

        if (firewoodChipPrefab == null)
        {
            firewoodChipPrefab = Resources.Load<GameObject>("firewood_single");
            if (firewoodChipPrefab == null) firewoodChipPrefab = Resources.Load<GameObject>("Prefab/firewood_single");
            if (firewoodChipPrefab == null) firewoodChipPrefab = Resources.Load<GameObject>("Hbao/Prefab/firewood_single");
            if (firewoodChipPrefab != null)
            {
                Debug.Log($"[ChoppableTree] {name}: Tự động cấu hình firewoodChipPrefab từ Resources: {firewoodChipPrefab.name}");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isCutDown.OnValueChanged += OnCutDownChanged;
        
        if (isCutDown.Value)
        {
            CreateTreeStump();
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c != null) c.enabled = false;
            }
            gameObject.SetActive(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        isCutDown.OnValueChanged -= OnCutDownChanged;
    }

    // Nhận diện va chạm từ chính Object cha nếu có Collider
    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleTriggerEnter(collision.collider);
    }

    // Hàm nhận và xử lý va chạm chuyển tiếp từ các Collider con (hoặc cha)
    public void HandleTriggerEnter(Collider other)
    {
        if (isCutDown.Value) return;

        if (other == null) return;
        if (other.GetComponentInParent<PushableStone>() != null) return;

        bool isPlayerAttack = false;
        IPlayerHUDTarget player = other.GetComponentInParent<IPlayerHUDTarget>();
        if (player == null)
        {
            player = other.transform.root.GetComponentInChildren<IPlayerHUDTarget>();
        }

        // 1. Kiểm tra nếu va chạm thuộc về một người chơi
        if (player != null)
        {
            string nameLower = other.name.ToLower();
            
            // Lọc bỏ Body Collider (ví dụ: Capsule Collider bao quanh người chơi để di chuyển)
            // Chỉ nhận diện các va chạm từ vũ khí/hitbox (là trigger hoặc chứa tên vũ khí)
            if (other.isTrigger || 
                nameLower.Contains("hitbox") || 
                nameLower.Contains("weapon") || 
                nameLower.Contains("blade") || 
                nameLower.Contains("sword") || 
                nameLower.Contains("shield") || 
                nameLower.Contains("fist") || 
                nameLower.Contains("hand") ||
                nameLower.Contains("tool"))
            {
                isPlayerAttack = true;
            }
        }
        // 2. Kiểm tra nếu va chạm là đạn bắn từ Elena (Arrow) hoặc Maya (Spell/Projectile)
        else
        {
            string nameLower = other.name.ToLower();
            if (nameLower.Contains("arrow") || nameLower.Contains("projectile") ||
                other.GetComponent<ArrowProjectile>() != null || other.GetComponent<MayaProjectile>() != null)
            {
                isPlayerAttack = true;
            }
        }

        if (isPlayerAttack)
        {
            Vector3 hitPos = other.transform.position;
            if (other != null)
            {
                try { hitPos = other.bounds.center; } catch {}
            }

            if (player != null)
            {
                // Chỉ cho phép chặt cây khi đang sử dụng RÌU (WeaponIndex == 1)
                if (player.GetActiveWeaponIndex() == 1 && player.IsHoldingAxe())
                {
                    OnTreeHit(hitPos);
                }
                else
                {
                    Debug.Log($"[ChoppableTree] Player '{player.DisplayName}' chém bằng vũ khí khác (WeaponIndex={player.GetActiveWeaponIndex()}), cần trang bị Rìu (WeaponIndex=1) để chặt cây.");
                }
            }
        }
    }

    public void HitTree(Vector3 hitPos, int weaponIndex)
    {
        if (isCutDown.Value) return;

        // Chỉ cho phép chặt cây khi đang sử dụng RÌU (WeaponIndex == 1)
        if (weaponIndex == 1)
        {
            OnTreeHit(hitPos);
        }
        else
        {
            Debug.Log($"[ChoppableTree] Player chém bằng vũ khí khác (WeaponIndex={weaponIndex}), cần trang bị Rìu (WeaponIndex=1) để chặt cây.");
        }
    }

    private void OnTreeHit(Vector3 hitPos)
    {
        // Chạy hiệu ứng rung, dăm gỗ và vết chém lập tức cho người chơi vừa chém
        PlayHitEffectsLocal(hitPos);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                ProcessHitServer(hitPos, NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                ReportHitServerRpc(hitPos);
            }
        }
        else
        {
            ProcessHitOffline();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReportHitServerRpc(Vector3 hitPos, ServerRpcParams rpcParams = default)
    {
        ulong hitterClientId = rpcParams.Receive.SenderClientId;
        ProcessHitServer(hitPos, hitterClientId);
    }

    private void ProcessHitServer(Vector3 hitPos, ulong hitterClientId)
    {
        if (isCutDown.Value) return;
        if (currentHits >= requiredHits) return; // Bảo vệ chống nhận RPC trùng trong cùng frame

        currentHits++;
        PlayHitEffectsClientRpc(hitPos, hitterClientId);

        if (currentHits >= requiredHits)
        {
            isCutDown.Value = true;
        }
    }

    private void ProcessHitOffline()
    {
        if (!gameObject.activeSelf || currentHits >= requiredHits) return; // Bảo vệ chống va chạm trùng offline

        currentHits++;

        if (currentHits >= requiredHits)
        {
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c != null) c.enabled = false;
            }
            StartCoroutine(FallDownCoroutine());
        }
    }

    [ClientRpc]
    private void PlayHitEffectsClientRpc(Vector3 hitPos, ulong hitterClientId)
    {
        // Nếu là client đã chém (đã tự tạo hiệu ứng rồi) thì bỏ qua
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == hitterClientId)
        {
            return;
        }

        PlayHitEffectsLocal(hitPos);
    }

    private void PlayHitEffectsLocal(Vector3 hitPos)
    {
        if (!isShaking)
        {
            StartCoroutine(ShakeCoroutine());
        }
        SpawnWoodSplinters();

        // Nâng vị trí hiển thị lên 0.45m để khớp với lưỡi rìu thực tế thay vì tay cầm (pivot)
        Vector3 adjustedHitPos = hitPos + Vector3.up * 0.45f;

        CreateCutMark(adjustedHitPos);

        // Tạo hiệu ứng khói bụi nhỏ giống thật khi chặt cây
        CreateRealisticDustEffect(adjustedHitPos, 0.6f, 15);
    }

    private Shader GetGuaranteedLitShader()
    {
        bool isURP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        Shader s = null;

        if (isURP)
        {
            s = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Universal Render Pipeline/Simple Lit") ??
                Shader.Find("Universal Render Pipeline/Unlit");
        }

        // Built-in Render Pipeline hoặc Fallback
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Legacy Shaders/Diffuse");
        if (s == null) s = Shader.Find("Mobile/Diffuse");
        if (s == null) s = Shader.Find("Bumped Diffuse");
        if (s == null) s = Shader.Find("Sprites/Default");

        if (s == null)
        {
            // Tìm bất kỳ MeshRenderer nào có shader chuẩn trong Scene
            var rends = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var r in rends)
            {
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null)
                {
                    string shaderName = r.sharedMaterial.shader.name;
                    if (!shaderName.Contains("ALP") && !shaderName.Contains("Tree") && !shaderName.Contains("Wind"))
                    {
                        return r.sharedMaterial.shader;
                    }
                }
            }
        }
        return s;
    }

    private Material FindLitMaterial()
    {
        Shader s = GetGuaranteedLitShader();
        if (s != null)
        {
            return new Material(s);
        }

        var treeRends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in treeRends)
        {
            if (r != null && r.sharedMaterial != null)
            {
                return new Material(r.sharedMaterial);
            }
        }
        return null;
    }

    private void CreateCutMark(Vector3 hitPos)
    {
        if (visualModel == null) return;

        // Tính hướng từ tâm cây ra điểm va chạm
        Vector3 dirToHit = hitPos - transform.position;
        dirToHit.y = 0f; // Bỏ trục Y để lấy hướng ngang phẳng
        Vector3 horizontalDir = dirToHit.normalized;

        // Bán kính cây
        float treeRadius = 0.45f;
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap == null) cap = GetComponentInChildren<CapsuleCollider>();
        if (cap != null)
        {
            treeRadius = cap.radius * transform.lossyScale.x;
        }

        // Tính vị trí vết chém nằm trên vỏ thân cây
        Vector3 cutPosition = transform.position + horizontalDir * treeRadius;
        cutPosition.y = hitPos.y; // Chiều cao chính xác của điểm va chạm

        // Giới hạn chiều cao vết chém để không bị lệch quá cao hoặc dưới mặt đất
        float minY = transform.position.y + 0.3f;
        float maxY = transform.position.y + 2.5f;
        cutPosition.y = Mathf.Clamp(cutPosition.y, minY, maxY);

        // Góc xoay của vết chém áp sát vỏ cây
        Quaternion cutRotation = Quaternion.LookRotation(horizontalDir);

        // Tạo mesh vết chém
        GameObject cutMark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cutMark.name = "TreeCutMark";
        
        // Hủy collider của vết chém ngay lập tức
        Destroy(cutMark.GetComponent<Collider>());

        // Gắn vào visualModel làm con để nó tự rung/tự ẩn đi khi cây bị hạ
        cutMark.transform.SetParent(visualModel.transform, true);
        
        cutMark.transform.position = cutPosition;
        // Hơi nghiêng ngẫu nhiên để tạo hiệu ứng chém chéo tự nhiên
        cutMark.transform.rotation = cutRotation * Quaternion.Euler(
            Random.Range(-10f, 10f),
            Random.Range(-5f, 5f),
            Random.Range(-20f, 20f)
        );

        // Kích thước vết chém dẹt và mỏng sâu vào thân cây (lớn hơn để dễ thấy)
        cutMark.transform.localScale = new Vector3(
            Random.Range(0.35f, 0.5f),  // Độ rộng vết chém
            Random.Range(0.08f, 0.14f), // Độ dày vết chém
            Random.Range(0.12f, 0.2f)   // Chiều sâu vết chém
        );

        // Tô màu lòng gỗ sáng (Dùng vật liệu URP Lit tìm được để tránh lỗi màu tím)
        Renderer rend = cutMark.GetComponent<Renderer>();
        if (rend != null)
        {
            Material cutMat = FindLitMaterial();
            if (cutMat != null)
            {
                // Clear textures to make it a solid wood color
                if (cutMat.HasProperty("_BaseMap")) cutMat.SetTexture("_BaseMap", null);
                if (cutMat.HasProperty("_MainTex")) cutMat.SetTexture("_MainTex", null);
                if (cutMat.HasProperty("_BumpMap")) cutMat.SetTexture("_BumpMap", null);
                if (cutMat.HasProperty("_MetallicGlossMap")) cutMat.SetTexture("_MetallicGlossMap", null);
                if (cutMat.HasProperty("_OcclusionMap")) cutMat.SetTexture("_OcclusionMap", null);
                if (cutMat.HasProperty("_EmissionMap")) cutMat.SetTexture("_EmissionMap", null);

                Color woodColor = new Color(0.88f, 0.72f, 0.48f); // Màu lòng gỗ sáng tự nhiên
                if (cutMat.HasProperty("_BaseColor"))
                {
                    cutMat.SetColor("_BaseColor", woodColor);
                }
                else if (cutMat.HasProperty("_Color"))
                {
                    cutMat.SetColor("_Color", woodColor);
                }
                
                // Đảm bảo không bị trong suốt
                if (cutMat.HasProperty("_Surface"))
                {
                    cutMat.SetFloat("_Surface", 0f); // Opaque
                    cutMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    cutMat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                }
                cutMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

                rend.sharedMaterial = cutMat;
            }
            else
            {
                // Fallback cuối cùng nếu không tìm thấy material nào
                Color woodColor = new Color(0.88f, 0.72f, 0.48f); // Màu lòng gỗ sáng tự nhiên
                if (rend.material.HasProperty("_BaseColor"))
                {
                    rend.material.SetColor("_BaseColor", woodColor);
                }
                else
                {
                    rend.material.color = woodColor;
                }
            }
        }
    }

    private void OnCutDownChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            // Tắt ngay lập tức tất cả Collider trên cây để tránh kẹt người chơi
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c != null) c.enabled = false;
            }
            StartCoroutine(FallDownCoroutine());
        }
    }

    private void SpawnWoodLogs(int count)
    {
        if (woodLogPrefab == null) ResolveWoodLogPrefab();
        Debug.Log($"[ChoppableTree] {name}: SpawnWoodLogs gọi với count={count}, woodLogPrefab={(woodLogPrefab != null ? woodLogPrefab.name : "NULL")}");

        for (int i = 0; i < count; i++)
        {
            int woodAmount = Random.Range(5, 11);
            if (WoodLogObjectPool.Instance != null)
            {
                WoodLogObjectPool.Instance.StartCoroutine(AnimateScatteredWoodChipsAndMerge(woodAmount, isNetwork: true));
            }
            else
            {
                StartCoroutine(AnimateScatteredWoodChipsAndMerge(woodAmount, isNetwork: true));
            }
        }
    }

    private bool hasDroppedLogs = false;

    private void TriggerWoodDropAnimation()
    {
        if (hasDroppedLogs) return;
        hasDroppedLogs = true;

        if (woodLogPrefab == null) ResolveWoodLogPrefab();
        int woodAmount = Random.Range(5, 11);
        bool isNet = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        
        Debug.Log($"[ChoppableTree] {name}: TriggerWoodDropAnimation được kích hoạt DUY NHẤT 1 LẦN! isNet={isNet}, IsServer={(isNet ? IsServer : false)}");

        if (WoodLogObjectPool.Instance != null)
        {
            WoodLogObjectPool.Instance.StartCoroutine(AnimateScatteredWoodChipsAndMerge(woodAmount, isNet));
        }
        else
        {
            StartCoroutine(AnimateScatteredWoodChipsAndMerge(woodAmount, isNet));
        }
    }

    private IEnumerator AnimateScatteredWoodChipsAndMerge(int woodAmount, bool isNetwork)
    {
        // 1. Tính toán điểm đáp đất cuối cùng của Bó Gỗ (ngoài hẳn gốc cây 3.5m -> 4.5m)
        // Sử dụng hạt giống tọa độ cây (transform.position) để tạo vị trí chuẩn xác 100% giống nhau giữa Server và tất cả Client!
        float treeHash = Mathf.Abs(Mathf.Sin(Vector3.Dot(transform.position, new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f) % 1.0f;
        float angleMain = (treeHash * 360f) * Mathf.Deg2Rad;
        float bundleDist = 3.5f + (treeHash * 1.0f); // Bán kính nảy 3.5m -> 4.5m
        Vector3 mainDir = new Vector3(Mathf.Cos(angleMain), 0f, Mathf.Sin(angleMain)).normalized;
        Vector3 bundleLandPos = transform.position + mainDir * bundleDist;

        // Bắn Raycast tìm chính xác độ cao đất cho điểm nảy bó gỗ
        float groundY = GetAccurateGroundY(bundleLandPos);
        bundleLandPos.y = groundY + 0.65f;

        // 2. Tạo cụm các mảnh gỗ nhỏ văng ra xung quanh né xa gốc cây (bán kính rộng 3.2m -> 4.8m)
        int chipCount = Mathf.Max(3, scatteredChipCount);
        GameObject[] chips = new GameObject[chipCount];
        Vector3[] chipStartPos = new Vector3[chipCount];
        Vector3[] chipLandPos = new Vector3[chipCount];
        Quaternion[] chipRotations = new Quaternion[chipCount];

        Renderer mainRend = GetComponentInChildren<Renderer>();
        Material treeMat = GetTrunkMaterial();
        if (treeMat == null && mainRend != null && mainRend.sharedMaterial != null) treeMat = mainRend.sharedMaterial;

        for (int i = 0; i < chipCount; i++)
        {
            // Mỗi mảnh gỗ văng ra theo hướng đồng bộ né xa gốc cây (3.2m -> 4.8m)
            float chipSeed = (treeHash + i * 0.173f) % 1.0f;
            float chipAngle = (i * (360f / chipCount) + (chipSeed * 40f - 20f)) * Mathf.Deg2Rad;
            Vector3 chipDir = new Vector3(Mathf.Cos(chipAngle), 0f, Mathf.Sin(chipAngle)).normalized;

            Vector3 startP = transform.position + chipDir * 1.5f + Vector3.up * 1.8f;
            float chipDist = 3.2f + (chipSeed * 1.4f);
            Vector3 landP = transform.position + chipDir * chipDist;
            
            // Tìm mặt đất chính xác cho mảnh gỗ nhỏ
            landP.y = GetAccurateGroundY(landP) + 0.35f;

            chipStartPos[i] = startP;
            chipLandPos[i] = landP;

            GameObject chip;
            GameObject prefabToUse = firewoodChipPrefab != null ? firewoodChipPrefab : woodLogPrefab;
            if (prefabToUse != null)
            {
                chip = Instantiate(prefabToUse, startP, Random.rotation);
                chip.name = $"WoodChip_{i}";

                // Kích hoạt toàn bộ GameObject/Renderer con và RESET localPosition về Vector3.zero
                // (Khắc phục triệt để lỗi Prefab firewood_single bị đặt lệch tâm Z = 4.88m trong Asset!)
                chip.SetActive(true);
                Transform[] allChilds = chip.GetComponentsInChildren<Transform>(true);
                foreach (var ch in allChilds)
                {
                    if (ch != null)
                    {
                        ch.gameObject.SetActive(true);
                        if (ch != chip.transform)
                        {
                            ch.localPosition = Vector3.zero;
                            ch.localRotation = Quaternion.identity;
                        }
                    }
                }

                Renderer[] childRends = chip.GetComponentsInChildren<Renderer>(true);
                foreach (var r in childRends)
                {
                    if (r != null)
                    {
                        r.enabled = true;
                        // Bảo vệ Shader dùng trong Built-in Render Pipeline (Standard Shader / Diffuse)
                        if (r.sharedMaterial == null && r.material == null)
                        {
                            Material defaultMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                            defaultMat.color = new Color(0.55f, 0.35f, 0.18f);
                            r.material = defaultMat;
                        }
                    }
                }

                Debug.Log($"[ChoppableTree] {name}: Đã sinh thành công mảnh gỗ văng #{i} từ Prefab '{prefabToUse.name}' tại vị trí {startP}");

                Vector3 origS = (prefabToUse == firewoodChipPrefab) ? firewoodChipPrefab.transform.localScale * 2.5f : prefabToUse.transform.localScale * 1.5f;
                if (origS.magnitude < 0.8f) origS = new Vector3(0.8f, 0.8f, 1.2f);
                chip.transform.localScale = origS;

                // Tắt Collider & Rigidbody để không gây va chạm vật lý lúc văng
                Collider[] cCols = chip.GetComponentsInChildren<Collider>(true);
                foreach (var c in cCols) if (c != null) c.enabled = false;
                Rigidbody[] rbs = chip.GetComponentsInChildren<Rigidbody>(true);
                foreach (var r in rbs) if (r != null) r.isKinematic = true;
            }
            else
            {
                // Tạo khối hình trụ Cylinder 3D gỗ lớn
                chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chip.name = $"WoodChip_{i}";
                chip.transform.position = startP;
                chip.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
                chip.transform.rotation = Random.rotation;

                Collider cCol = chip.GetComponent<Collider>();
                if (cCol != null) cCol.enabled = false;

                Renderer cRend = chip.GetComponent<Renderer>();
                if (cRend != null)
                {
                    cRend.enabled = true;
                    Material woodMat = FindLitMaterial();
                    if (woodMat != null)
                    {
                        Color woodDarkColor = new Color(0.55f, 0.35f, 0.18f); // Màu thanh gỗ nâu tự nhiên
                        if (woodMat.HasProperty("_BaseColor")) woodMat.SetColor("_BaseColor", woodDarkColor);
                        else if (woodMat.HasProperty("_Color")) woodMat.SetColor("_Color", woodDarkColor);
                        cRend.material = woodMat;
                    }
                    else if (treeMat != null)
                    {
                        cRend.material = treeMat;
                    }
                }
            }

            chipRotations[i] = chip.transform.rotation;
            chips[i] = chip;
        }

        // 3. Hiệu ứng mảnh gỗ nhỏ văng nảy ra đất (Phase 1: Scatter)
        float elapsed = 0f;
        while (elapsed < chipScatterDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / chipScatterDuration;

            for (int i = 0; i < chipCount; i++)
            {
                if (chips[i] == null) continue;
                Vector3 current = Vector3.Lerp(chipStartPos[i], chipLandPos[i], t);
                // Tạo vòm cong parabol văng cao lên không trung (2.2m) rồi rớt xuống
                float arc = Mathf.Sin(t * Mathf.PI) * 2.2f;
                current.y += arc;

                chips[i].transform.position = current;
                chips[i].transform.rotation = Quaternion.Slerp(chipRotations[i], Quaternion.Euler(0f, chipRotations[i].eulerAngles.y + 180f, 0f), t);
            }
            yield return null;
        }

        // Dừng ngắn 0.15s trên mặt đất
        yield return new WaitForSeconds(0.15f);

        // 4. Hiệu ứng các mảnh gỗ nhỏ bay tập trung gộp lại thành Bó Gỗ (Phase 2: Merge)
        elapsed = 0f;
        while (elapsed < chipMergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / chipMergeDuration;
            float smoothT = t * t * (3f - 2f * t);

            for (int i = 0; i < chipCount; i++)
            {
                if (chips[i] == null) continue;
                Vector3 current = Vector3.Lerp(chipLandPos[i], bundleLandPos, smoothT);
                // Vòm cong nhẹ khi hội tụ
                float arc = Mathf.Sin(t * Mathf.PI) * 0.8f;
                current.y += arc;

                chips[i].transform.position = current;
                Vector3 origScale = (firewoodChipPrefab != null) ? firewoodChipPrefab.transform.localScale : new Vector3(0.2f, 0.2f, 0.5f);
                chips[i].transform.localScale = Vector3.Lerp(origScale, Vector3.zero, t);
            }
            yield return null;
        }

        // Hủy toàn bộ mảnh gỗ nhỏ
        for (int i = 0; i < chipCount; i++)
        {
            if (chips[i] != null) Destroy(chips[i]);
        }

        // 5. Sinh ra Bó Gỗ chính (woodLogPrefab / wood_stack) đúng vị trí nảy gộp
        if (woodLogPrefab != null)
        {
            // Trong chế độ Mạng (Network): Chỉ SERVER mới khởi tạo Bó Gỗ và Spawn NetworkObject.
            // Client kết nối sẽ tự động nhận 1 Bó Gỗ duy nhất từ Server qua Netcode!
            if (isNetwork && !IsServer)
            {
                yield break;
            }

            GameObject log = WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, bundleLandPos, Quaternion.identity);
            
            // Đặt tất cả Colliders trên Bó Gỗ thành IsTrigger = true
            // Đảm bảo nhân vật bước tới nhặt đồ mượt mà, KHÔNG bị nhảy đứng lên không trung!
            Collider[] logCols = log.GetComponentsInChildren<Collider>(true);
            foreach (var c in logCols)
            {
                if (c != null) c.isTrigger = true;
            }

            var cid = log.GetComponent<CollectibleItemDrop>();
            if (cid != null)
            {
                if (isNetwork && IsServer) cid.woodAmount.Value = woodAmount;
                else cid.localWoodAmount = woodAmount;
            }

            if (isNetwork && IsServer)
            {
                var netObj = log.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned) netObj.Spawn();
            }

            // Hiệu ứng nảy pop-up nở ra cho bó gỗ chính khi xuất hiện
            StartCoroutine(AnimateBundlePopUp(log, bundleLandPos));
        }
    }

    private IEnumerator AnimateBundlePopUp(GameObject logObj, Vector3 targetPos)
    {
        if (logObj == null) yield break;
        Vector3 baseScale = logObj.transform.localScale;
        if (baseScale == Vector3.zero) baseScale = Vector3.one;

        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Overshoot elastic pop
            float scaleMultiplier = Mathf.Sin(t * Mathf.PI * 0.85f) * 1.25f;
            if (t >= 0.8f) scaleMultiplier = Mathf.Lerp(1.25f, 1f, (t - 0.8f) / 0.2f);

            logObj.transform.localScale = baseScale * scaleMultiplier;
            yield return null;
        }
        logObj.transform.localScale = baseScale;
    }

    private void SpawnWoodSplinters()
    {
        int count = Random.Range(4, 7);
        Vector3 spawnOrigin = transform.position + Vector3.up * 1.5f;
        
        // Chuẩn bị material gỗ URP cho các mảnh gỗ
        Material splinterMat = FindLitMaterial();
        if (splinterMat != null)
        {
            if (splinterMat.HasProperty("_BaseMap")) splinterMat.SetTexture("_BaseMap", null);
            if (splinterMat.HasProperty("_MainTex")) splinterMat.SetTexture("_MainTex", null);
            Color woodDarkColor = new Color(0.42f, 0.26f, 0.1f);
            if (splinterMat.HasProperty("_BaseColor"))
                splinterMat.SetColor("_BaseColor", woodDarkColor);
            else if (splinterMat.HasProperty("_Color"))
                splinterMat.SetColor("_Color", woodDarkColor);
            if (splinterMat.HasProperty("_Surface"))
            {
                splinterMat.SetFloat("_Surface", 0f);
                splinterMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                splinterMat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            }
        }

        for (int i = 0; i < count; i++)
        {
            GameObject splinter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            splinter.name = "WoodSplinter";
            
            splinter.transform.localScale = new Vector3(
                Random.Range(0.05f, 0.15f),
                Random.Range(0.03f, 0.08f),
                Random.Range(0.15f, 0.4f)
            );
            splinter.transform.position = spawnOrigin + Random.insideUnitSphere * 0.5f;
            splinter.transform.rotation = Random.rotation;

            Renderer rend = splinter.GetComponent<Renderer>();
            if (rend != null)
            {
                if (splinterMat != null)
                {
                    rend.sharedMaterial = splinterMat;
                }
                else
                {
                    // Fallback: cố gắng đặt màu trực tiếp
                    rend.material.color = new Color(0.42f, 0.26f, 0.1f);
                }
            }

            Rigidbody rb = splinter.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            
            Vector3 pushForce = new Vector3(
                Random.Range(-2f, 2f),
                Random.Range(1f, 4f),
                Random.Range(-2f, 2f)
            );
            rb.linearVelocity = pushForce;
            rb.angularVelocity = Random.insideUnitSphere * 20f;

            Destroy(splinter, 3f);
        }
    }

    private IEnumerator ShakeCoroutine()
    {
        isShaking = true;
        float elapsed = 0f;
        float duration = 0.25f;
        float magnitude = 0.12f;

        bool shakeChildren = (visualModel == gameObject) || (visualModel.GetComponent<NetworkObject>() != null);
        
        Transform[] shakeTargets;
        Vector3[] originalPoses;

        if (shakeChildren)
        {
            int childCount = visualModel.transform.childCount;
            shakeTargets = new Transform[childCount];
            originalPoses = new Vector3[childCount];
            for (int i = 0; i < childCount; i++)
            {
                shakeTargets[i] = visualModel.transform.GetChild(i);
                originalPoses[i] = shakeTargets[i].localPosition;
            }
        }
        else
        {
            shakeTargets = new Transform[] { visualModel.transform };
            originalPoses = new Vector3[] { originalLocalPos };
        }

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float z = Random.Range(-1f, 1f) * magnitude;
            Vector3 offset = new Vector3(x, 0f, z);

            for (int i = 0; i < shakeTargets.Length; i++)
            {
                if (shakeTargets[i] != null)
                {
                    shakeTargets[i].localPosition = originalPoses[i] + offset;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < shakeTargets.Length; i++)
        {
            if (shakeTargets[i] != null)
            {
                shakeTargets[i].localPosition = originalPoses[i];
            }
        }

        isShaking = false;
    }

    private IEnumerator FallDownCoroutine()
    {
        CreateTreeStump();
        TriggerWoodDropAnimation();

        float stumpHeight = 1.15f;

        // Kích hoạt camera nhìn cây ngã nếu ở gần người chơi
        if (PlayerHUDController.Instance != null && PlayerHUDController.LocalPlayerTarget != null)
        {
            Vector3 playerPos = PlayerHUDController.LocalPlayerTarget.transform.position;
            float dist = Vector3.Distance(playerPos, transform.position);
            if (dist < 18f)
            {
                PlayerHUDController.Instance.TriggerTreeFallCamera(this);
            }
        }

        // 1. Vô hiệu hóa và xóa toàn bộ Collider trên cây gục ngã
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col != null)
            {
                col.enabled = false;
                Destroy(col);
            }
        }

        // Vô hiệu hóa Rigidbody của cây
        Rigidbody treeRb = GetComponent<Rigidbody>();
        if (treeRb != null)
        {
            treeRb.detectCollisions = false;
            Destroy(treeRb);
        }

        // 2. Tạo FallPivot tại đúng vị trí mặt cắt trên đỉnh gốc cây (y = 1.15m)
        Vector3 pivotWorldPos = transform.position + Vector3.up * stumpHeight;
        GameObject fallPivot = new GameObject($"{name}_FallPivot");
        fallPivot.transform.position = pivotWorldPos;
        fallPivot.transform.rotation = transform.rotation;

        if (transform.parent != null && transform.parent.gameObject.activeInHierarchy)
        {
            fallPivot.transform.SetParent(transform.parent, true);
        }

        // Chuyển toàn bộ phần thân trên của cây (Visual Model / Renderers) sang fallPivot
        if (visualModel != null && visualModel != gameObject)
        {
            visualModel.transform.SetParent(fallPivot.transform, true);
        }
        else
        {
            List<Transform> childrenToMove = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (spawnedStump != null && child == spawnedStump.transform) continue;
                if (child == fallPivot.transform) continue;
                childrenToMove.Add(child);
            }
            foreach (var child in childrenToMove)
            {
                child.SetParent(fallPivot.transform, true);
            }
        }

        // Xóa sạch tất cả collider còn sót lại trên fallPivot để đảm bảo thân cây ngã hoàn toàn không có va chạm
        Collider[] pivotCols = fallPivot.GetComponentsInChildren<Collider>(true);
        foreach (var c in pivotCols)
        {
            if (c != null)
            {
                c.enabled = false;
                Destroy(c);
            }
        }

        // 3. Chọn hướng ngã ngẫu nhiên xung quanh trục Y (Đồng bộ giữa Server và tất cả Client bằng vị trí cây làm seed)
        int seed = (int)(transform.position.x * 100f + transform.position.z * 10f);
        Random.State oldState = Random.state;
        Random.InitState(seed);
        float randomAngle = Random.Range(0f, 360f);
        Random.state = oldState;

        Vector3 fallRotationAxis = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.right;

        float duration = 2.0f;
        float elapsed = 0f;

        Quaternion startWorldRot = fallPivot.transform.rotation;
        Quaternion targetWorldRot = Quaternion.AngleAxis(90f, fallRotationAxis) * startWorldRot;

        // Sinh dăm gỗ & khói bụi gãy cây ngay tại mặt cắt gốc
        CreateRealisticDustEffect(pivotWorldPos, 1.2f, 30);
        SpawnWoodSplinters();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float tSmooth = t * t; // Hiệu ứng ngã nhanh dần đều

            fallPivot.transform.rotation = Quaternion.Slerp(startWorldRot, targetWorldRot, tSmooth);
            yield return null;
        }

        fallPivot.transform.rotation = targetWorldRot;

        // Sinh hiệu ứng khói bụi lớn khi ngọn cây đập xuống đất
        Vector3 treeTopPos = pivotWorldPos + (fallPivot.transform.rotation * (Vector3.up * 4.0f));
        CreateRealisticDustEffect(treeTopPos, 2.2f, 50);
        CreateRealisticDustEffect(pivotWorldPos, 2.0f, 40);

        // Chờ một chút ngắn trước khi ẩn hoàn toàn phần thân cây ngã
        yield return new WaitForSeconds(0.2f);
        Destroy(fallPivot);
        gameObject.SetActive(false);
    }

    private void CreateRealisticDustEffect(Vector3 position, float scale, int count)
    {
        // 1. Tạo GameObject mới cho Particle System
        GameObject dustObj = new GameObject("RealisticDustVFX");
        dustObj.transform.position = position;

        ParticleSystem ps = dustObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        // Cấu hình Main Module
        var main = ps.main;
        main.duration = 2.0f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f); // Tăng thời gian sống của bụi
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 2.2f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.2f * scale); // Tăng kích thước hạt bụi rõ nét hơn
        
        // Màu bụi giống thật: màu đất cát xám nâu nhạt pha trộn đậm hơn (Alpha tăng lên 0.5f để rõ nét)
        main.startColor = new Color(0.72f, 0.65f, 0.58f, 0.5f); 
        main.gravityModifier = -0.015f; // Khói bụi bốc nhẹ lên cao
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        // Cấu hình Emission Module
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, (short)count) });

        // Cấu hình Shape Module (Hình nón tỏa góc rộng hướng lên)
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.25f * scale;

        // Cấu hình Size over Lifetime (Hạt khói nở to dần khi tan vào không khí)
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0.0f, 0.3f);
        sizeCurve.AddKey(0.2f, 1.1f);
        sizeCurve.AddKey(1.0f, 1.8f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

        // Cấu hình Color over Lifetime (Bụi mờ và tan biến dần với Alpha đậm nét)
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(new Color(0.72f, 0.65f, 0.58f), 0.0f), 
                new GradientColorKey(new Color(0.68f, 0.61f, 0.54f), 0.7f),
                new GradientColorKey(new Color(0.62f, 0.55f, 0.48f), 1.0f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0.0f, 0.0f), 
                new GradientAlphaKey(0.55f, 0.15f), // Tăng alpha từ 0.28 lên 0.55
                new GradientAlphaKey(0.35f, 0.6f),  // Tăng alpha từ 0.18 lên 0.35
                new GradientAlphaKey(0.0f, 1.0f) 
            }
        );
        colorOverLifetime.color = gradient;

        // Cấu hình Limit Velocity over Lifetime (Không khí cản để bụi chuyển động chậm dần)
        var limitVelocity = ps.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.drag = 1.2f;
        limitVelocity.multiplyDragByParticleSize = true;

        // Cấu hình Renderer
        ParticleSystemRenderer renderer = dustObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            
            // Tìm Shader Unlit Particles để tương thích 100% không bị hồng trên built-in pipeline
            Shader particleShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply") ?? 
                                   Shader.Find("Particles/Standard Unlit") ?? 
                                   Shader.Find("Sprites/Default");
            
            if (particleShader != null)
            {
                Material defaultMat = new Material(particleShader);
                renderer.sharedMaterial = defaultMat;
            }
        }

        ps.Play();
        Destroy(dustObj, 2.5f);
    }

    private IEnumerator SpawnLogsAfterDelay(float delay)
    {
        yield break;
    }

    private IEnumerator SpawnCollectibleLogLocalAfterDelay(float delay)
    {
        yield break;
    }

    private Material GetTrunkMaterial()
    {
        var treeRends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in treeRends)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            foreach (var mat in r.sharedMaterials)
            {
                if (mat != null)
                {
                    string mName = mat.name.ToLower();
                    if (mName.Contains("bark") || mName.Contains("trunk") || mName.Contains("wood") || mName.Contains("oak"))
                    {
                        return mat;
                    }
                }
            }
        }
        foreach (var r in treeRends)
        {
            if (r != null && !(r is ParticleSystemRenderer) && r.sharedMaterial != null)
            {
                return r.sharedMaterial;
            }
        }
        return null;
    }

    private Material GetStumpBarkLitMaterial()
    {
        Material sourceMat = GetTrunkMaterial();
        Shader s = GetGuaranteedLitShader();
        Material litMat = (s != null) ? new Material(s) : FindLitMaterial();
        if (litMat == null && sourceMat != null) litMat = new Material(sourceMat);

        Texture mainTex = null;
        Texture bumpMap = null;
        Color baseColor = new Color(0.45f, 0.32f, 0.20f); // Màu vỏ cây đậm chuẩn tự nhiên

        if (sourceMat != null)
        {
            if (sourceMat.HasProperty("_MainTex") && sourceMat.GetTexture("_MainTex") != null)
                mainTex = sourceMat.GetTexture("_MainTex");
            else if (sourceMat.HasProperty("_BaseMap") && sourceMat.GetTexture("_BaseMap") != null)
                mainTex = sourceMat.GetTexture("_BaseMap");
            else if (sourceMat.HasProperty("_MainAlbedoTex") && sourceMat.GetTexture("_MainAlbedoTex") != null)
                mainTex = sourceMat.GetTexture("_MainAlbedoTex");

            if (sourceMat.HasProperty("_BumpMap") && sourceMat.GetTexture("_BumpMap") != null)
                bumpMap = sourceMat.GetTexture("_BumpMap");
            else if (sourceMat.HasProperty("_MainNormalTex") && sourceMat.GetTexture("_MainNormalTex") != null)
                bumpMap = sourceMat.GetTexture("_MainNormalTex");

            if (sourceMat.HasProperty("_BaseColor")) baseColor = sourceMat.GetColor("_BaseColor");
            else if (sourceMat.HasProperty("_Color")) baseColor = sourceMat.GetColor("_Color");
        }

        if (litMat != null)
        {
            if (mainTex != null)
            {
                if (litMat.HasProperty("_BaseMap"))
                {
                    litMat.SetTexture("_BaseMap", mainTex);
                    litMat.SetTextureScale("_BaseMap", new Vector2(4f, 2f));
                }
                if (litMat.HasProperty("_MainTex"))
                {
                    litMat.SetTexture("_MainTex", mainTex);
                    litMat.SetTextureScale("_MainTex", new Vector2(4f, 2f));
                }
            }
            if (bumpMap != null)
            {
                if (litMat.HasProperty("_BumpMap"))
                {
                    litMat.SetTexture("_BumpMap", bumpMap);
                    litMat.SetTextureScale("_BumpMap", new Vector2(4f, 2f));
                }
            }

            if (litMat.HasProperty("_BaseColor")) litMat.SetColor("_BaseColor", baseColor);
            else if (litMat.HasProperty("_Color")) litMat.SetColor("_Color", baseColor);
        }

        return litMat;
    }

    private float GetAccurateGroundY(Vector3 worldPos)
    {
        // 1. Kiểm tra tất cả Terrain trong Scene để lấy độ cao mặt đất chính xác 100%
        if (Terrain.activeTerrains != null && Terrain.activeTerrains.Length > 0)
        {
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 tPos = terrain.transform.position;
                Vector3 tSize = terrain.terrainData.size;
                if (worldPos.x >= tPos.x && worldPos.x <= tPos.x + tSize.x &&
                    worldPos.z >= tPos.z && worldPos.z <= tPos.z + tSize.z)
                {
                    return terrain.SampleHeight(worldPos) + tPos.y;
                }
            }
        }
        else if (Terrain.activeTerrain != null && Terrain.activeTerrain.terrainData != null)
        {
            Vector3 tPos = Terrain.activeTerrain.transform.position;
            Vector3 tSize = Terrain.activeTerrain.terrainData.size;
            if (worldPos.x >= tPos.x && worldPos.x <= tPos.x + tSize.x &&
                worldPos.z >= tPos.z && worldPos.z <= tPos.z + tSize.z)
            {
                return Terrain.activeTerrain.SampleHeight(worldPos) + tPos.y;
            }
        }

        // 2. Bắn Raycast từ trên cao xuống để tìm sàn/mesh đất/đá (nếu không dùng Terrain)
        Vector3 rayStart = worldPos + Vector3.up * 8.0f;
        float rayDistance = 30f;
        int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayDistance, layerMask, QueryTriggerInteraction.Ignore);

        float bestGroundY = worldPos.y;
        float minDistanceToTreeBase = float.MaxValue;
        bool foundGround = false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.isTrigger) continue;

            // Bỏ qua tuyệt đối toàn bộ collider của chính cây này và các con của cây
            if (h.collider.transform.IsChildOf(transform) || h.collider.gameObject == gameObject)
                continue;

            string colName = h.collider.name.ToLower();
            // Bỏ qua các object không phải địa hình/mặt đất
            if (colName.Contains("stump") || colName.Contains("wood") || colName.Contains("chip") || 
                colName.Contains("log") || colName.Contains("player") || colName.Contains("weapon") || 
                colName.Contains("hitbox") || colName.Contains("projectile"))
                continue;

            float dist = Mathf.Abs(h.point.y - worldPos.y);
            if (dist < minDistanceToTreeBase)
            {
                minDistanceToTreeBase = dist;
                bestGroundY = h.point.y;
                foundGround = true;
            }
        }

        return foundGround ? bestGroundY : worldPos.y;
    }

    private void CreateTreeStump()
    {
        if (spawnedStump != null) return;

        // Tìm chính xác độ cao mặt đất thực tế trực tiếp dưới gốc cây
        float groundY = GetAccurateGroundY(transform.position);
        Vector3 spawnPos = new Vector3(transform.position.x, groundY, transform.position.z);

        // Tính vị trí gốc cây từ mặt đất kết hợp offset cài đặt
        Vector3 finalPos = spawnPos + Vector3.up * stumpYOffset + stumpPositionOffset;

        // Tự động tìm Prefab gốc cây nếu chưa được kéo gán trong Inspector
        if (treeStumpPrefab == null)
        {
            treeStumpPrefab = Resources.Load<GameObject>("Prefab/aspen-stump");
            if (treeStumpPrefab == null) treeStumpPrefab = Resources.Load<GameObject>("aspen-stump");
            if (treeStumpPrefab == null) treeStumpPrefab = Resources.Load<GameObject>("Hbao/Prefab/aspen-stump");
        }

        // Nếu có Prefab gốc cây custom, sinh trực tiếp Prefab đó dưới vị trí thân cây
        if (treeStumpPrefab != null)
        {
            // Áp dụng chính xác góc xoay đã cài đặt trong Inspector (stumpRotationOffset)
            Quaternion finalRotation;
            if (stumpRotationOffset != Vector3.zero)
            {
                finalRotation = Quaternion.Euler(stumpRotationOffset);
            }
            else
            {
                finalRotation = transform.rotation;
            }

            spawnedStump = Instantiate(treeStumpPrefab, finalPos, finalRotation);
            spawnedStump.name = $"{name}_Stump";
            spawnedStump.transform.position = finalPos;
            spawnedStump.transform.rotation = finalRotation;

            if (transform.parent != null && transform.parent.gameObject.activeInHierarchy)
            {
                spawnedStump.transform.SetParent(transform.parent, true);
            }

            // Xóa/tắt toàn bộ Collider trên gốc cây để người chơi hoàn toàn KHÔNG bị kẹt khi đi qua
            Collider[] existingCols = spawnedStump.GetComponentsInChildren<Collider>(true);
            foreach (var c in existingCols)
            {
                if (c != null)
                {
                    c.enabled = false;
                    Destroy(c);
                }
            }

            Debug.Log($"[ChoppableTree] Đã tạo thành công gốc cây Prefab '{treeStumpPrefab.name}' tại vị trí {finalPos} với góc xoay {finalRotation.eulerAngles} cho {name}!");
            return;
        }

        // Tính bán kính & chiều cao gốc cây
        float treeRadius = 0.6f;
        Renderer mainRend = GetComponentInChildren<Renderer>();
        if (mainRend != null)
        {
            treeRadius = Mathf.Clamp(mainRend.bounds.extents.x, 0.5f, 1.1f);
        }

        float stumpHeight = 1.15f; // Chiều cao 1.15m nhô cao rõ ràng trên mặt đất

        // 1. Tạo GameObject phần gốc cây độc lập
        spawnedStump = new GameObject($"{name}_Stump");
        spawnedStump.transform.position = spawnPos - Vector3.up * 0.05f;
        spawnedStump.transform.rotation = transform.rotation;
        if (transform.parent != null && transform.parent.gameObject.activeInHierarchy)
        {
            spawnedStump.transform.SetParent(transform.parent, true);
        }

        // 3. Thân gốc cây thẳng đứng (Stump Body)
        GameObject stumpBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stumpBody.name = "StumpBody";
        stumpBody.transform.SetParent(spawnedStump.transform, false);
        stumpBody.transform.localPosition = new Vector3(0f, stumpHeight * 0.5f, 0f);
        stumpBody.transform.localScale = new Vector3(treeRadius * 2f, stumpHeight * 0.5f, treeRadius * 2f);

        Collider bodyCol = stumpBody.GetComponent<Collider>();
        if (bodyCol != null) DestroyImmediate(bodyCol);

        Renderer bodyRend = stumpBody.GetComponent<Renderer>();
        Material trunkMat = GetStumpBarkLitMaterial();
        if (bodyRend != null && trunkMat != null)
        {
            bodyRend.sharedMaterial = trunkMat;
        }

        // 4. Mặt cắt lòng gỗ dẹt (StumpCutCap) ở đỉnh gốc cây
        GameObject cutCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cutCap.name = "StumpCutCap";
        cutCap.transform.SetParent(spawnedStump.transform, false);
        cutCap.transform.localPosition = new Vector3(0f, stumpHeight + 0.005f, 0f);
        cutCap.transform.localScale = new Vector3(treeRadius * 1.96f, 0.008f, treeRadius * 1.96f);

        Collider capCutCol = cutCap.GetComponent<Collider>();
        if (capCutCol != null) DestroyImmediate(capCutCol);

        Renderer capRend = cutCap.GetComponent<Renderer>();
        if (capRend != null)
        {
            Material cutMat = FindLitMaterial();
            if (cutMat != null)
            {
                if (cutMat.HasProperty("_BaseMap")) cutMat.SetTexture("_BaseMap", null);
                if (cutMat.HasProperty("_MainTex")) cutMat.SetTexture("_MainTex", null);
                if (cutMat.HasProperty("_BumpMap")) cutMat.SetTexture("_BumpMap", null);
                if (cutMat.HasProperty("_MetallicGlossMap")) cutMat.SetTexture("_MetallicGlossMap", null);
                if (cutMat.HasProperty("_OcclusionMap")) cutMat.SetTexture("_OcclusionMap", null);
                if (cutMat.HasProperty("_EmissionMap")) cutMat.SetTexture("_EmissionMap", null);

                Color woodColor = new Color(0.88f, 0.72f, 0.48f); // Màu lòng gỗ tự nhiên
                if (cutMat.HasProperty("_BaseColor")) cutMat.SetColor("_BaseColor", woodColor);
                else if (cutMat.HasProperty("_Color")) cutMat.SetColor("_Color", woodColor);

                if (cutMat.HasProperty("_Surface"))
                {
                    cutMat.SetFloat("_Surface", 0f);
                    cutMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    cutMat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                }
                cutMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                capRend.sharedMaterial = cutMat;
            }
        }
    }
}

// Lớp cầu nối chuyển tiếp va chạm phụ trợ từ các Collider con lên main script
public class TreeColliderForwarder : MonoBehaviour
{
    public ChoppableTree mainTree;

    private void OnTriggerEnter(Collider other)
    {
        if (mainTree != null)
        {
            mainTree.HandleTriggerEnter(other);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (mainTree != null)
        {
            mainTree.HandleTriggerEnter(collision.collider);
        }
    }
}
