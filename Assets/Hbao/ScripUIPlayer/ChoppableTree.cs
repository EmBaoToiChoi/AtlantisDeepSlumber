using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class ChoppableTree : NetworkBehaviour
{
    [Header("Tree Settings")]
    [Tooltip("Model hiển thị của cây (sẽ dùng để lắc khi bị chém)")]
    public GameObject visualModel;
    
    [Tooltip("Số lượt chém để cây đổ/biến mất")]
    public int requiredHits = 3;
    
    [Tooltip("Prefab thanh gỗ thu thập (gắn CollectibleItemDrop)")]
    public GameObject woodLogPrefab;

    // Trạng thái mạng đồng bộ
    public NetworkVariable<bool> isCutDown = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private int currentHits = 0;
    private bool isShaking = false;
    private Vector3 originalLocalPos;

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
        if (woodLogPrefab == null || woodLogPrefab == gameObject || woodLogPrefab.GetComponent<ChoppableTree>() != null)
        {
            Debug.LogWarning($"[ChoppableTree] {name}: woodLogPrefab chưa được cấu hình đúng. Đang tự động tìm kiếm...");
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
            {
                foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (networkPrefab.Prefab != null)
                    {
                        var col = networkPrefab.Prefab.GetComponent<CollectibleItemDrop>();
                        if (col != null && (col.itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
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
                        if (pName.Contains("firewood") || pName.Contains("woodlog") || pName.Contains("thanhgo"))
                        {
                            woodLogPrefab = networkPrefab.Prefab;
                            Debug.Log($"[ChoppableTree] {name}: Tự động cấu hình woodLogPrefab thành công qua tên prefab: {woodLogPrefab.name}");
                            return;
                        }
                    }
                }
            }

            GameObject loaded = Resources.Load<GameObject>("firewood_single");
            if (loaded != null)
            {
                woodLogPrefab = loaded;
                Debug.Log($"[ChoppableTree] {name}: Tự động cấu hình woodLogPrefab thành công từ Resources: {woodLogPrefab.name}");
                return;
            }

            loaded = Resources.Load<GameObject>("WoodLog");
            if (loaded != null)
            {
                woodLogPrefab = loaded;
                Debug.Log($"[ChoppableTree] {name}: Tự động cấu hình woodLogPrefab thành công từ Resources: {woodLogPrefab.name}");
                return;
            }

            Debug.LogError($"[ChoppableTree] {name}: Không thể tìm thấy WoodLog prefab hợp lệ!");
        }
    }

    public override void OnNetworkSpawn()
    {
        isCutDown.OnValueChanged += OnCutDownChanged;
        
        if (isCutDown.Value)
        {
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
                // Cận chiến: Bắt buộc phải rút vũ khí (phím 2 -> WeaponIndex == 2)
                if (player.GetActiveWeaponIndex() == 2)
                {
                    OnTreeHit(hitPos);
                }
                else
                {
                    Debug.Log($"[ChoppableTree] Player '{player.DisplayName}' chém bằng tay không, cần trang bị vũ khí để chặt cây.");
                }
            }
            else
            {
                // Đạn bắn: Luôn hợp lệ
                OnTreeHit(hitPos);
            }
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

        currentHits++;
        PlayHitEffectsClientRpc(hitPos, hitterClientId);

        if (currentHits >= requiredHits)
        {
            isCutDown.Value = true;
            int logsCount = Random.Range(1, 5); // Random 1-4
            SpawnWoodLogs(logsCount);
        }
    }

    private void ProcessHitOffline()
    {
        currentHits++;

        if (currentHits >= requiredHits)
        {
            gameObject.SetActive(false);
            int logsCount = Random.Range(1, 5);
            for (int i = 0; i < logsCount; i++)
            {
                SpawnCollectibleLogLocal();
            }
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
        CreateCutMark(hitPos);
    }

    private Material FindURPLitMaterial()
    {
        var allRends = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in allRends)
        {
            if (r != null && r.sharedMaterial != null)
            {
                Shader s = r.sharedMaterial.shader;
                if (s != null)
                {
                    string shaderName = s.name.ToLower();
                    if (shaderName.Contains("universal render pipeline/lit") || 
                        shaderName.Contains("urp/lit") || 
                        (shaderName.Contains("lit") && !shaderName.Contains("speedtree") && !shaderName.Contains("nature") && !shaderName.Contains("terrain")))
                    {
                        return new Material(r.sharedMaterial);
                    }
                }
            }
        }
        
        // Fallback to tree's own renderer
        var treeRends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in treeRends)
        {
            if (r != null && r.sharedMaterial != null)
            {
                return new Material(r.sharedMaterial);
            }
        }

        foreach (var r in allRends)
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

        // Kích thước vết chém dẹt và mỏng sâu vào thân cây
        cutMark.transform.localScale = new Vector3(
            Random.Range(0.22f, 0.32f),  // Độ rộng vết chém
            Random.Range(0.04f, 0.08f), // Độ dày vết chém
            Random.Range(0.08f, 0.14f)  // Chiều sâu vết chém
        );

        // Tô màu lòng gỗ sáng (Dùng vật liệu URP Lit tìm được để tránh lỗi màu tím)
        Renderer rend = cutMark.GetComponent<Renderer>();
        if (rend != null)
        {
            Material cutMat = FindURPLitMaterial();
            if (cutMat != null)
            {
                // Clear textures to make it a solid wood color
                if (cutMat.HasProperty("_BaseMap")) cutMat.SetTexture("_BaseMap", null);
                if (cutMat.HasProperty("_MainTex")) cutMat.SetTexture("_MainTex", null);
                if (cutMat.HasProperty("_BumpMap")) cutMat.SetTexture("_BumpMap", null);
                if (cutMat.HasProperty("_MetallicGlossMap")) cutMat.SetTexture("_MetallicGlossMap", null);
                if (cutMat.HasProperty("_OcclusionMap")) cutMat.SetTexture("_OcclusionMap", null);
                if (cutMat.HasProperty("_EmissionMap")) cutMat.SetTexture("_EmissionMap", null);

                Color woodColor = new Color(0.88f, 0.76f, 0.55f); // Màu lòng gỗ kem nhạt sáng
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
                Color woodColor = new Color(0.88f, 0.76f, 0.55f);
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
            gameObject.SetActive(false);
        }
    }

    private void SpawnWoodLogs(int count)
    {
        if (woodLogPrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + new Vector3(
                Random.Range(-1.2f, 1.2f),
                0.5f,
                Random.Range(-1.2f, 1.2f)
            );

            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 5f))
            {
                spawnPos.y = hit.point.y + 0.3f;
            }

            GameObject log = WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, spawnPos, Quaternion.identity);
            var netObj = log.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
        }
    }

    private void SpawnCollectibleLogLocal()
    {
        if (woodLogPrefab == null) return;

        Vector3 spawnPos = transform.position + new Vector3(
            Random.Range(-1.2f, 1.2f),
            0.5f,
            Random.Range(-1.2f, 1.2f)
        );

        if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 5f))
        {
            spawnPos.y = hit.point.y + 0.3f;
        }

        WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, spawnPos, Quaternion.identity);
    }

    private void SpawnWoodSplinters()
    {
        int count = Random.Range(4, 7);
        Vector3 spawnOrigin = transform.position + Vector3.up * 1.5f;

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
                rend.material.color = new Color(0.42f, 0.26f, 0.1f);
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

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float z = Random.Range(-1f, 1f) * magnitude;

            visualModel.transform.localPosition = new Vector3(originalLocalPos.x + x, originalLocalPos.y, originalLocalPos.z + z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        visualModel.transform.localPosition = originalLocalPos;
        isShaking = false;
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
