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

    [Tooltip("Góc xoay bù thêm (X,Y,Z) cho Prefab gốc cây nếu 3D model bị nằm ngang (mặc định X: 0 vì Prefab đã được dựng đứng)")]
    public Vector3 stumpRotationOffset = Vector3.zero;

    [Tooltip("Độ nâng độ cao Y (m) cho gốc cây nhô lên trên mặt đất. Tăng chỉ số này nếu gốc cây bị chìm dưới đất.")]
    public float stumpYOffset = 0.35f;

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
            if (loaded == null)
            {
                loaded = Resources.Load<GameObject>("firewood_single");
            }
            if (loaded == null)
            {
                loaded = Resources.Load<GameObject>("WoodLog");
            }

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
            CreateTreeStump();
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
            StartCoroutine(SpawnLogsAfterDelay(0.2f));
        }
    }

    private void ProcessHitOffline()
    {
        if (!gameObject.activeSelf || currentHits >= requiredHits) return; // Bảo vệ chống va chạm trùng offline

        currentHits++;

        if (currentHits >= requiredHits)
        {
            StartCoroutine(FallDownCoroutine());
            StartCoroutine(SpawnCollectibleLogLocalAfterDelay(0.2f));
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
            StartCoroutine(FallDownCoroutine());
        }
    }

    private void SpawnWoodLogs(int count)
    {
        if (woodLogPrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            float groundY = transform.position.y;

            // Hướng văng ngẫu nhiên ra bên ngoài gốc cây
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 spawnDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;

            // Vị trí xuất hiện ban đầu (startPos) nằm bên ngoài gốc cây 1.8m
            Vector3 startPos = transform.position + spawnDir * 1.8f + Vector3.up * 1.0f;

            // Vị trí đáp đất (targetLandPos) cách tâm gốc cây 2.8m -> 3.6m
            float landDistance = Random.Range(2.8f, 3.6f);
            Vector3 targetLandPos = transform.position + spawnDir * landDistance;

            Vector3 rayStart = new Vector3(targetLandPos.x, transform.position.y + 4f, targetLandPos.z);
            int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 8f, layerMask))
            {
                string hitName = hit.collider.gameObject.name.ToLower();
                if (hit.collider.gameObject != gameObject && !hitName.Contains("player") && !hitName.Contains("stump") && !hitName.Contains("tree"))
                {
                    groundY = hit.point.y;
                }
            }

            targetLandPos.y = groundY + 0.15f;

            // Sinh bó gỗ bên ngoài hẳn gốc cây (startPos) và văng ra đất
            GameObject log = WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, startPos, Quaternion.identity);
            
            var netObj = log.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }

            var cid = log.GetComponent<CollectibleItemDrop>();
            if (cid != null)
            {
                cid.woodAmount.Value = Random.Range(5, 11);
            }

            StartCoroutine(AnimateLogDropFromStump(log, startPos, targetLandPos));
        }
    }

    private void SpawnCollectibleLogLocal(int count)
    {
        if (woodLogPrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            float groundY = transform.position.y;

            // Hướng văng ngẫu nhiên ra bên ngoài gốc cây
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 spawnDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;

            // Vị trí xuất hiện ban đầu (startPos) nằm bên ngoài gốc cây 1.8m
            Vector3 startPos = transform.position + spawnDir * 1.8f + Vector3.up * 1.0f;

            // Vị trí đáp đất (targetLandPos) cách tâm gốc cây 2.8m -> 3.6m
            float landDistance = Random.Range(2.8f, 3.6f);
            Vector3 targetLandPos = transform.position + spawnDir * landDistance;

            Vector3 rayStart = new Vector3(targetLandPos.x, transform.position.y + 4f, targetLandPos.z);
            int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 8f, layerMask))
            {
                string hitName = hit.collider.gameObject.name.ToLower();
                if (hit.collider.gameObject != gameObject && !hitName.Contains("player") && !hitName.Contains("stump") && !hitName.Contains("tree"))
                {
                    groundY = hit.point.y;
                }
            }

            targetLandPos.y = groundY + 0.15f;

            // Sinh bó gỗ bên ngoài hẳn gốc cây (startPos) và văng ra đất
            GameObject log = WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, startPos, Quaternion.identity);
            
            var cid = log.GetComponent<CollectibleItemDrop>();
            if (cid != null)
            {
                cid.localWoodAmount = Random.Range(5, 11);
            }

            StartCoroutine(AnimateLogDropFromStump(log, startPos, targetLandPos));
        }
    }

    private IEnumerator AnimateLogDropFromStump(GameObject logObj, Vector3 startPos, Vector3 targetPos)
    {
        if (logObj == null) yield break;

        float duration = 0.65f;
        float elapsed = 0f;
        Quaternion startRot = logObj.transform.rotation;
        Quaternion targetRot = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));

        while (elapsed < duration)
        {
            if (logObj == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float tArc = Mathf.Sin(t * Mathf.PI);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            currentPos.y += tArc * 0.7f; // Đường cong vồng văng gỗ từ gốc cây rớt xuống đất

            logObj.transform.position = currentPos;
            logObj.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        if (logObj != null)
        {
            logObj.transform.position = targetPos;
            logObj.transform.rotation = targetRot;
        }
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

        // 1. Tắt toàn bộ colliders để người chơi không bị kẹt hoặc va chạm khi cây đang ngã
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = false;
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
        yield return new WaitForSeconds(delay);
        SpawnWoodLogs(1);
    }

    private IEnumerator SpawnCollectibleLogLocalAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnCollectibleLogLocal(1);
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

    private void CreateTreeStump()
    {
        if (spawnedStump != null) return;

        // Bắn Raycast tìm chính xác độ cao mặt đất thực tế
        Vector3 spawnPos = transform.position;
        int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
        if (Physics.Raycast(spawnPos + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 12f, layerMask))
        {
            if (hit.collider.gameObject != gameObject && !hit.collider.name.ToLower().Contains("tree"))
            {
                spawnPos.y = hit.point.y;
            }
        }
        spawnPos.y += stumpYOffset;

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
            Quaternion finalRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Quaternion.Euler(stumpRotationOffset);
            spawnedStump = Instantiate(treeStumpPrefab, spawnPos, finalRotation);
            spawnedStump.name = $"{name}_Stump";
            spawnedStump.transform.rotation = finalRotation;
            if (transform.parent != null && transform.parent.gameObject.activeInHierarchy)
            {
                spawnedStump.transform.SetParent(transform.parent, true);
            }

            // Đảm bảo gốc cây có Collider va chạm để nhân vật không đi xuyên qua
            Collider stumpCol = spawnedStump.GetComponent<Collider>();
            if (stumpCol == null) stumpCol = spawnedStump.GetComponentInChildren<Collider>();
            if (stumpCol == null)
            {
                CapsuleCollider customCapCol = spawnedStump.AddComponent<CapsuleCollider>();
                customCapCol.center = new Vector3(0f, 0.6f, 0f);
                customCapCol.radius = 0.6f;
                customCapCol.height = 1.2f;
            }
            Debug.Log($"[ChoppableTree] Đã tạo thành công gốc cây Prefab '{treeStumpPrefab.name}' cho {name}!");
            return;
        }

        // Tính bán kính & chiều cao gốc cây
        float treeRadius = 0.65f;
        Renderer mainRend = GetComponentInChildren<Renderer>();
        if (mainRend != null)
        {
            treeRadius = Mathf.Clamp(mainRend.bounds.extents.x, 0.55f, 1.2f);
        }

        float stumpHeight = 1.15f; // Chiều cao 1.15m nhô cao rõ ràng trên mặt đất

        // 1. Tạo GameObject phần gốc cây độc lập
        spawnedStump = new GameObject($"{name}_Stump");
        spawnedStump.transform.position = spawnPos;
        spawnedStump.transform.rotation = transform.rotation;
        if (transform.parent != null && transform.parent.gameObject.activeInHierarchy)
        {
            spawnedStump.transform.SetParent(transform.parent, true);
        }

        // 2. Thêm CapsuleCollider tròn mượt màng bao trọn gốc cây (bo tròn XZ, chống kẹt góc và xoay tròn)
        CapsuleCollider capCol = spawnedStump.AddComponent<CapsuleCollider>();
        capCol.center = new Vector3(0f, stumpHeight * 0.5f, 0f);
        capCol.radius = treeRadius * 0.95f;
        capCol.height = stumpHeight * 1.05f;
        capCol.direction = 1; // Hướng trục Y

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
