using UnityEngine;
using Unity.Netcode;

public class BridgeCollapseTrigger : NetworkBehaviour
{
    [Header("Bridge Components Configuration")]
    [Tooltip("Các mảng cầu nguyên vẹn ban đầu (Sẽ bị tắt khi sập, bật lại khi sửa)")]
    public GameObject[] stableBridgeSegments;

    [Tooltip("Các mảng cầu sập/gãy (Sẽ được bật lên khi sập, tắt đi khi sửa)")]
    public GameObject[] brokenBridgeSegments;

    [Tooltip("Animator để chạy hoạt ảnh sập/sửa (nếu có)")]
    public Animator bridgeAnimator;
    public string collapseTriggerName = "Collapse";
    public string repairTriggerName = "Repair";

    [Tooltip("Cây cầu nguyên khối chính (Sẽ ẩn đi hoàn toàn khi sập, hiện lại khi được sửa đủ 16 gỗ)")]
    public GameObject mainBridgeObject;

    [Header("Quest Target Trees")]
    [Tooltip("Danh sách 4 cây gỗ nhiệm vụ tương ứng với 4 player (Leo=0, Maya=1, Elena=2, Arthur=3)")]
    public ChoppableTree[] targetTrees = new ChoppableTree[4];

    private Vector3 originalBridgePos;
    private Quaternion originalBridgeRot;
    private GameObject ghostBridgeObject;

    [Header("Wood Quest Spawning Configuration")]
    [Tooltip("Prefab gỗ để người chơi thu thập (CollectibleItemDrop với itemName = 'WoodLog' hoặc 'ThanhGo')")]
    public GameObject woodLogPrefab;

    [Tooltip("Các vị trí spawn gỗ. Nếu bỏ trống sẽ tự động spawn ngẫu nhiên quanh trigger trên mặt đất.")]
    public Transform[] woodLogSpawnPoints;
    public float spawnRadius = 10f;
    public int logsToSpawn = 16;

    [Header("Quest & Interaction Settings")]
    public int requiredLogsToRepair = 16;
    public float repairInteractRadius = 4f;

    // Trạng thái mạng đồng bộ
    public NetworkVariable<bool> hasCollapsed = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> hasBeenRepaired = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> logsSubmitted = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool localCollapseTriggered = false;
    private IPlayerHUDTarget localPlayer;
    private PlayerHUDController hud;
    private bool isPlayerInRange = false;
    private float nextPlayerSearchTime = 0f;

    private void Awake()
    {
        // Lưu lại vị trí, góc xoay ban đầu của cây cầu nguyên khối
        if (mainBridgeObject != null)
        {
            originalBridgePos = mainBridgeObject.transform.position;
            originalBridgeRot = mainBridgeObject.transform.rotation;
        }

        // Đảm bảo visual ban đầu khớp với trạng thái chưa sập
        SetStableSegmentsActive(true);
        SetBrokenSegmentsActive(false);
    }

    private void Start()
    {
        hud = FindAnyObjectByType<PlayerHUDController>();
        ResolveWoodLogPrefab();
    }

    private void ResolveWoodLogPrefab()
    {
        if (woodLogPrefab == null || woodLogPrefab == gameObject || woodLogPrefab.GetComponent<BridgeCollapseTrigger>() != null)
        {
            Debug.LogWarning("[BridgeCollapseTrigger] Phát hiện woodLogPrefab chưa được cấu hình đúng. Đang tự động tìm kiếm...");
            
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
                            Debug.Log($"[BridgeCollapseTrigger] Tự động sửa cấu hình woodLogPrefab thành công qua CollectibleItemDrop: {woodLogPrefab.name}");
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
                            Debug.Log($"[BridgeCollapseTrigger] Tự động sửa cấu hình woodLogPrefab thành công qua tên prefab: {woodLogPrefab.name}");
                            return;
                        }
                    }
                }
            }

            GameObject loaded = Resources.Load<GameObject>("firewood_single");
            if (loaded != null)
            {
                woodLogPrefab = loaded;
                Debug.Log($"[BridgeCollapseTrigger] Tự động sửa cấu hình woodLogPrefab thành công từ Resources: {woodLogPrefab.name}");
                return;
            }

            loaded = Resources.Load<GameObject>("WoodLog");
            if (loaded != null)
            {
                woodLogPrefab = loaded;
                Debug.Log($"[BridgeCollapseTrigger] Tự động sửa cấu hình woodLogPrefab thành công từ Resources: {woodLogPrefab.name}");
                return;
            }

            Debug.LogError("[BridgeCollapseTrigger] Không thể tìm thấy WoodLog prefab hợp lệ!");
        }
    }

    public override void OnNetworkSpawn()
    {
        // Đăng ký nhận sự kiện thay đổi trạng thái từ Server
        hasCollapsed.OnValueChanged += OnCollapseStateChanged;
        hasBeenRepaired.OnValueChanged += OnRepairStateChanged;
        logsSubmitted.OnValueChanged += OnLogsSubmittedChanged;

        // Cập nhật trạng thái hiển thị cho người vào trễ
        ApplyBridgeVisualState(hasCollapsed.Value, hasBeenRepaired.Value, logsSubmitted.Value);
    }

    public override void OnNetworkDespawn()
    {
        hasCollapsed.OnValueChanged -= OnCollapseStateChanged;
        hasBeenRepaired.OnValueChanged -= OnRepairStateChanged;
        logsSubmitted.OnValueChanged -= OnLogsSubmittedChanged;
    }

    private void Update()
    {
        // 1. Tìm local player nếu chưa có
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // 1.5. Đảm bảo gắn chỉ đường cho mọi người chơi hoạt động nếu cầu sập và chưa sửa xong
        if (hasCollapsed.Value && !hasBeenRepaired.Value)
        {
            var activePlayers = PlayerHUDManager.ActivePlayers;
            foreach (var p in activePlayers)
            {
                if (p != null && targetTrees != null && targetTrees.Length > 0)
                {
                    var indicator = p.gameObject.GetComponent<TreeGuidanceIndicator>();
                    if (indicator == null)
                    {
                        int classIndex = p.CharacterClassIndex;
                        ChoppableTree myTree = targetTrees[classIndex % targetTrees.Length];
                        if (myTree != null)
                        {
                            indicator = p.gameObject.AddComponent<TreeGuidanceIndicator>();
                            indicator.targetTree = myTree;
                            indicator.reachDistance = 4f;
                            Debug.Log($"[BridgeCollapseTrigger] Gắn chỉ đường cho Player {p.DisplayName} tới cây: {myTree.name}");
                        }
                    }
                }
            }
        }

        // 2. Xử lý tương tác sửa cầu khi cầu đã sập và chưa được sửa hoàn tất
        if (hasCollapsed.Value && !hasBeenRepaired.Value && localPlayer != null)
        {
            float distance = Vector3.Distance(transform.position, localPlayer.transform.position);

            if (distance <= repairInteractRadius)
            {
                if (!isPlayerInRange)
                {
                    isPlayerInRange = true;
                }

                if (hud != null)
                {
                    var carrier = localPlayer.gameObject.GetComponent<PlayerLogCarrier>();
                    bool isCarrying = carrier != null && carrier.isCarrying;

                    int remaining = requiredLogsToRepair - logsSubmitted.Value;
                    if (remaining > 0)
                    {
                        if (isCarrying)
                        {
                            hud.ShowInteractionPrompt(true, $"Ấn [F] để góp gỗ sửa cầu ({logsSubmitted.Value}/{requiredLogsToRepair})");
                            
                            if (Input.GetKeyDown(KeyCode.F))
                            {
                                RequestSubmitCarriedLog();
                            }
                        }
                        else
                        {
                            hud.ShowInteractionPrompt(true, $"Hãy tìm và bưng gỗ đến đây để sửa cầu ({logsSubmitted.Value}/{requiredLogsToRepair})");
                        }
                    }
                }
            }
            else if (isPlayerInRange)
            {
                isPlayerInRange = false;
                if (hud != null)
                {
                    hud.ShowInteractionPrompt(false, "");
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Bỏ qua nếu cầu đã sập hoặc đã được sửa
        if (hasCollapsed.Value || localCollapseTriggered) return;

        // 2. Kiểm tra xem có phải là người chơi chạm vào không
        if (IsPlayer(other.gameObject))
        {
            Debug.Log($"[BridgeCollapseTrigger] Người chơi '{other.gameObject.name}' đi vào vùng kích hoạt. Kích hoạt ẩn cầu!");
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (IsServer)
                {
                    TriggerBridgeCollapseServer();
                }
            }
            else
            {
                // Fallback chơi đơn (Offline/Standalone)
                localCollapseTriggered = true;
                CollapseBridgeLocal();
            }
        }
    }

    #region Collapse Logic

    private void TriggerBridgeCollapseServer()
    {
        if (!IsServer) return;
        
        hasCollapsed.Value = true;
        
        // Server thực hiện spawn gỗ xung quanh cầu
        SpawnWoodLogsServer();
    }

    private void CollapseBridgeLocal()
    {
        // 1. Ẩn cây cầu nguyên khối chính đi
        if (mainBridgeObject != null)
        {
            mainBridgeObject.SetActive(false);
        }

        SetStableSegmentsActive(false);
        SetBrokenSegmentsActive(true);

        // 2. Tạo bóng cây cầu (ghost bridge) màu trắng ở vị trí ban đầu
        if (ghostBridgeObject == null)
        {
            CreateGhostBridge();
        }

        // Thêm chỉ đường cho tất cả các người chơi hoạt động dựa theo class của họ
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && targetTrees != null && targetTrees.Length > 0)
            {
                int classIndex = p.CharacterClassIndex;
                ChoppableTree myTree = targetTrees[classIndex % targetTrees.Length];
                if (myTree != null)
                {
                    var indicator = p.gameObject.GetComponent<TreeGuidanceIndicator>();
                    if (indicator == null)
                    {
                        indicator = p.gameObject.AddComponent<TreeGuidanceIndicator>();
                    }
                    indicator.targetTree = myTree;
                    indicator.reachDistance = 4f;
                    Debug.Log($"[BridgeCollapseTrigger] Gắn mũi tên chỉ đường cho Player {p.DisplayName} (class {classIndex}) tới cây: {myTree.name}");
                }
            }
        }

        // 3. Chạy Animator (nếu có)
        if (bridgeAnimator != null && !string.IsNullOrEmpty(collapseTriggerName))
        {
            bridgeAnimator.SetTrigger(collapseTriggerName);
        }

        // 4. Hiển thị UI Quest trên Client
        if (hud != null)
        {
            hud.ShowQuest(true);
            hud.UpdateQuestProgress(logsSubmitted.Value, requiredLogsToRepair);
            hud.ShowMissionAlert("CẦU ĐÃ BỊ SẬP! HÃY TÌM 16 THANH GỖ ĐỂ SỬA LẠI CẦU!", 4.0f);
        }

        // 5. Nếu là Standalone thì tự động spawn gỗ cục bộ
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            SpawnWoodLogsLocal();
        }
    }

    private void OnCollapseStateChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            Debug.Log("[BridgeCollapseTrigger] Server báo trạng thái Cầu Đã Sập.");
            CollapseBridgeLocal();
        }
    }

    #endregion

    #region Repair & Log Submission Logic

    private void RequestSubmitCarriedLog()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            var netPlayer = localPlayer.gameObject.GetComponent<NetworkObject>();
            if (netPlayer != null)
            {
                SubmitCarriedLogServerRpc(netPlayer.NetworkObjectId);
            }
        }
        else
        {
            // Chế độ offline/standalone
            var carrier = localPlayer.gameObject.GetComponent<PlayerLogCarrier>();
            if (carrier != null)
            {
                carrier.DropLog();
            }

            int remaining = requiredLogsToRepair - logsSubmitted.Value;
            if (remaining > 0)
            {
                logsSubmitted.Value += 1;
                if (logsSubmitted.Value >= requiredLogsToRepair)
                {
                    hasBeenRepaired.Value = true; // Sẽ kích hoạt OnRepairStateChanged
                    RepairBridgeLocal();
                }
                else
                {
                    if (hud != null)
                    {
                        hud.UpdateQuestProgress(logsSubmitted.Value, requiredLogsToRepair);
                    }
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitCarriedLogServerRpc(ulong playerNetObjectId)
    {
        if (!IsServer) return;

        SubmitCarriedLogClientRpc(playerNetObjectId);

        int remaining = requiredLogsToRepair - logsSubmitted.Value;
        if (remaining > 0)
        {
            logsSubmitted.Value += 1;
            if (logsSubmitted.Value >= requiredLogsToRepair)
            {
                hasBeenRepaired.Value = true;
                Debug.Log("[BridgeCollapseTrigger] Server: Cầu đã được sửa hoàn toàn!");
            }
        }
    }

    [ClientRpc]
    private void SubmitCarriedLogClientRpc(ulong playerNetObjectId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNetObj))
        {
            var playerObj = playerNetObj.gameObject;
            var carrier = playerObj.GetComponent<PlayerLogCarrier>();
            if (carrier != null)
            {
                carrier.DropLog();
            }
        }
    }

    private void RequestSubmitLogs(int amount)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Chế độ online: Gửi ServerRpc
            SubmitLogsServerRpc(amount);
        }
        else
        {
            // Chế độ offline/standalone
            ConsumePlayerWood(localPlayer, amount);
            logsSubmitted.Value += amount;
            
            if (logsSubmitted.Value >= requiredLogsToRepair)
            {
                hasBeenRepaired.Value = true; // Sẽ kích hoạt OnRepairStateChanged
                RepairBridgeLocal();
            }
            else
            {
                if (hud != null)
                {
                    hud.UpdateQuestProgress(logsSubmitted.Value, requiredLogsToRepair);
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitLogsServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        IPlayerHUDTarget playerTarget = FindPlayerByClientId(clientId);
        
        if (playerTarget != null)
        {
            int woodCount = GetPlayerWoodCount(playerTarget);
            int remaining = requiredLogsToRepair - logsSubmitted.Value;
            int actualToSubmit = Mathf.Min(amount, Mathf.Min(woodCount, remaining));
            
            if (actualToSubmit > 0)
            {
                // Trừ gỗ trên server
                ConsumePlayerWood(playerTarget, actualToSubmit);
                
                // Cập nhật tiến trình
                logsSubmitted.Value += actualToSubmit;
                
                Debug.Log($"[BridgeCollapseTrigger] Server: Client {clientId} đã gửi {actualToSubmit} thanh gỗ. Tiến trình: {logsSubmitted.Value}/{requiredLogsToRepair}");
                
                if (logsSubmitted.Value >= requiredLogsToRepair)
                {
                    hasBeenRepaired.Value = true;
                    Debug.Log("[BridgeCollapseTrigger] Server: Cầu đã được sửa hoàn toàn!");
                }
            }
        }
    }

    private void RepairBridgeLocal()
    {
        // 1. Tắt nhắc nhở tương tác
        if (hud != null)
        {
            hud.ShowInteractionPrompt(false, "");
            hud.ShowQuest(false);
            hud.ShowMissionAlert("CẦU ĐÃ ĐƯỢC SỬA CHỮA THÀNH CÔNG!", 4.0f);
        }

        // Dọn dẹp mũi tên chỉ đường trên tất cả các người chơi hoạt động
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null)
            {
                var indicator = p.gameObject.GetComponent<TreeGuidanceIndicator>();
                if (indicator != null)
                {
                    Destroy(indicator);
                }
            }
        }

        // 2. Hiện lại và khôi phục cây cầu nguyên vẹn
        if (mainBridgeObject != null)
        {
            mainBridgeObject.SetActive(true);
            mainBridgeObject.transform.position = originalBridgePos;
            mainBridgeObject.transform.rotation = originalBridgeRot;
        }

        SetStableSegmentsActive(true);
        SetBrokenSegmentsActive(false);

        // 3. Dọn dẹp bóng cầu (ghost)
        if (ghostBridgeObject != null)
        {
            Destroy(ghostBridgeObject);
            ghostBridgeObject = null;
        }

        // 4. Chạy Animator sửa cầu (nếu có)
        if (bridgeAnimator != null && !string.IsNullOrEmpty(repairTriggerName))
        {
            bridgeAnimator.SetTrigger(repairTriggerName);
        }
    }

    private void OnRepairStateChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            Debug.Log("[BridgeCollapseTrigger] Cầu đã được sửa hoàn tất.");
            RepairBridgeLocal();
        }
    }

    private void OnLogsSubmittedChanged(int oldVal, int newVal)
    {
        Debug.Log($"[BridgeCollapseTrigger] Tiến trình xây cầu thay đổi: {newVal}/{requiredLogsToRepair}");
        
        // Cập nhật UI
        if (hud != null)
        {
            hud.UpdateQuestProgress(newVal, requiredLogsToRepair);
        }
    }

    #endregion

    #region Blueprint & Wood Spawning Effects

    private void CreateGhostBridge()
    {
        if (mainBridgeObject == null) return;
        
        // 1. Nhân bản cây cầu làm bóng/ghost
        ghostBridgeObject = Instantiate(mainBridgeObject, originalBridgePos, originalBridgeRot);
        ghostBridgeObject.name = "GhostBridge";
        ghostBridgeObject.SetActive(true);
        
        // 2. Loại bỏ tất cả Collider và Rigidbody để người chơi không bị va chạm
        foreach (var col in ghostBridgeObject.GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }
        var rb = ghostBridgeObject.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        foreach (var rbs in ghostBridgeObject.GetComponentsInChildren<Rigidbody>())
        {
            Destroy(rbs);
        }
        
        // Loại bỏ các scripts để tránh chạy logic trùng lặp
        foreach (var comp in ghostBridgeObject.GetComponentsInChildren<MonoBehaviour>())
        {
            if (comp != null) Destroy(comp);
        }
        
        // 3. Đổi chất liệu sang màu trắng bán trong suốt (Sprites/Default shader rất mượt)
        Shader ghostShader = Shader.Find("Sprites/Default");
        if (ghostShader != null)
        {
            Material ghostMat = new Material(ghostShader);
            ghostMat.color = new Color(1f, 1f, 1f, 0.3f); // Màu trắng bán trong suốt
            
            foreach (var renderer in ghostBridgeObject.GetComponentsInChildren<Renderer>())
            {
                renderer.material = ghostMat;
            }
        }
    }

    private void SpawnWoodLogsServer()
    {
        if (!IsServer || woodLogPrefab == null) return;

        if (woodLogPrefab == gameObject || woodLogPrefab.GetComponent<BridgeCollapseTrigger>() != null)
        {
            Debug.LogError("[BridgeCollapseTrigger] Infinite loop prevented in SpawnWoodLogsServer! woodLogPrefab is misconfigured.");
            return;
        }

        Debug.Log("[BridgeCollapseTrigger] Server: Đang spawn 16 thanh gỗ...");

        for (int i = 0; i < logsToSpawn; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            GameObject wood = Instantiate(woodLogPrefab, spawnPos, Quaternion.identity);
            
            var netObj = wood.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
        }
    }

    private void SpawnWoodLogsLocal()
    {
        if (woodLogPrefab == null) return;

        if (woodLogPrefab == gameObject || woodLogPrefab.GetComponent<BridgeCollapseTrigger>() != null)
        {
            Debug.LogError("[BridgeCollapseTrigger] Infinite loop prevented in SpawnWoodLogsLocal! woodLogPrefab is misconfigured.");
            return;
        }

        Debug.Log("[BridgeCollapseTrigger] Standalone: Đang spawn 16 thanh gỗ...");

        for (int i = 0; i < logsToSpawn; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            Instantiate(woodLogPrefab, spawnPos, Quaternion.identity);
        }
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (woodLogSpawnPoints != null && woodLogSpawnPoints.Length > 0)
        {
            return woodLogSpawnPoints[index % woodLogSpawnPoints.Length].position;
        }

        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 2.0f, randomCircle.y);

        if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 15.0f))
        {
            spawnPos.y = hit.point.y + 0.3f;
        }
        else
        {
            spawnPos.y = transform.position.y;
        }

        return spawnPos;
    }

    #endregion

    #region Helper Methods

    private void ApplyBridgeVisualState(bool collapsed, bool repaired, int logsProgress)
    {
        if (repaired)
        {
            if (mainBridgeObject != null)
            {
                mainBridgeObject.SetActive(true);
                mainBridgeObject.transform.position = originalBridgePos;
                mainBridgeObject.transform.rotation = originalBridgeRot;
            }

            SetStableSegmentsActive(true);
            SetBrokenSegmentsActive(false);

            if (ghostBridgeObject != null)
            {
                Destroy(ghostBridgeObject);
                ghostBridgeObject = null;
            }



            var activePlayers = PlayerHUDManager.ActivePlayers;
            foreach (var p in activePlayers)
            {
                if (p != null)
                {
                    var indicator = p.gameObject.GetComponent<TreeGuidanceIndicator>();
                    if (indicator != null)
                    {
                        Destroy(indicator);
                    }
                }
            }

            if (hud != null) hud.ShowQuest(false);
        }
        else if (collapsed)
        {
            if (mainBridgeObject != null)
            {
                mainBridgeObject.SetActive(false);
            }

            SetStableSegmentsActive(false);
            SetBrokenSegmentsActive(true);

            // Tạo bóng cây cầu (ghost) màu trắng
            if (ghostBridgeObject == null)
            {
                CreateGhostBridge();
            }

            // Gắn chỉ đường cho mọi người chơi hoạt động dựa theo class của họ
            var activePlayers = PlayerHUDManager.ActivePlayers;
            foreach (var p in activePlayers)
            {
                if (p != null && targetTrees != null && targetTrees.Length > 0)
                {
                    int classIndex = p.CharacterClassIndex;
                    ChoppableTree myTree = targetTrees[classIndex % targetTrees.Length];
                    if (myTree != null)
                    {
                        var indicator = p.gameObject.GetComponent<TreeGuidanceIndicator>();
                        if (indicator == null)
                        {
                            indicator = p.gameObject.AddComponent<TreeGuidanceIndicator>();
                        }
                        indicator.targetTree = myTree;
                        indicator.reachDistance = 4f;
                    }
                }
            }

            if (hud != null)
            {
                hud.ShowQuest(true);
                hud.UpdateQuestProgress(logsProgress, requiredLogsToRepair);
            }
        }
        else
        {
            if (mainBridgeObject != null)
            {
                mainBridgeObject.SetActive(true);
            }
            SetStableSegmentsActive(true);
            SetBrokenSegmentsActive(false);

            if (ghostBridgeObject != null)
            {
                Destroy(ghostBridgeObject);
                ghostBridgeObject = null;
            }

            var activePlayers = PlayerHUDManager.ActivePlayers;
            foreach (var p in activePlayers)
            {
                if (p != null)
                {
                    var indicator = p.gameObject.GetComponent<TreeGuidanceIndicator>();
                    if (indicator != null)
                    {
                        Destroy(indicator);
                    }
                }
            }
        }
    }

    private void SetStableSegmentsActive(bool active)
    {
        if (stableBridgeSegments != null)
        {
            foreach (var go in stableBridgeSegments)
            {
                if (go != null) go.SetActive(active);
            }
        }
    }

    private void SetBrokenSegmentsActive(bool active)
    {
        if (brokenBridgeSegments != null)
        {
            foreach (var go in brokenBridgeSegments)
            {
                if (go != null) go.SetActive(active);
            }
        }
    }

    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;

        if (go.GetComponent<LeoPlayer>() != null || go.GetComponentInParent<LeoPlayer>() != null) return true;
        if (go.GetComponent<ArthurPlayer>() != null || go.GetComponentInParent<ArthurPlayer>() != null) return true;
        if (go.GetComponent<ElenaPlayer>() != null || go.GetComponentInParent<ElenaPlayer>() != null) return true;
        if (go.GetComponent<MayaPlayer>() != null || go.GetComponentInParent<MayaPlayer>() != null) return true;

        if (go.CompareTag("Player") || (go.transform.parent != null && go.transform.parent.CompareTag("Player"))) return true;

        return false;
    }

    private void FindLocalPlayer()
    {
        // 1. Try static cache first (zero overhead)
        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            var p = PlayerHUDController.LocalPlayerTarget;
            if (p != null && (p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        // 2. Try PlayerHUDManager active players cache next
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && (p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        // 3. Fallback to FindObjectsOfType, rate-limited to once every 2 seconds
        if (Time.time >= nextPlayerSearchTime)
        {
            nextPlayerSearchTime = Time.time + 2f;

            LeoPlayer[] leos = FindObjectsByType<LeoPlayer>(FindObjectsSortMode.None);
            foreach (var p in leos) { if (p.isStandaloneMode || p.IsOwner) { localPlayer = p; return; } }

            ArthurPlayer[] arthurs = FindObjectsByType<ArthurPlayer>(FindObjectsSortMode.None);
            foreach (var p in arthurs) { if (p.isStandaloneMode || p.IsOwner) { localPlayer = p; return; } }

            ElenaPlayer[] elenas = FindObjectsByType<ElenaPlayer>(FindObjectsSortMode.None);
            foreach (var p in elenas) { if (p.isStandaloneMode || p.IsOwner) { localPlayer = p; return; } }

            MayaPlayer[] mayas = FindObjectsByType<MayaPlayer>(FindObjectsSortMode.None);
            foreach (var p in mayas) { if (p.isStandaloneMode || p.IsOwner) { localPlayer = p; return; } }
        }
    }

    private IPlayerHUDTarget FindPlayerByClientId(ulong clientId)
    {
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && p.IsSpawned && p.OwnerClientId == clientId)
            {
                return p;
            }
        }
        return null;
    }

    private int GetPlayerWoodCount(IPlayerHUDTarget player)
    {
        string[] slots = player.InventorySlots;
        if (slots == null) return 0;

        int woodCount = 0;
        foreach (var slotVal in slots)
        {
            if (string.IsNullOrEmpty(slotVal)) continue;
            string itemName = slotVal;
            int count = 1;
            if (slotVal.Contains(":"))
            {
                var parts = slotVal.Split(':');
                itemName = parts[0];
                int.TryParse(parts[1], out count);
            }
            if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
            {
                woodCount += count;
            }
        }
        return woodCount;
    }

    private void ConsumePlayerWood(IPlayerHUDTarget player, int amountNeeded)
    {
        string[] slots = player.InventorySlots;
        if (slots == null) return;

        int remainingNeeded = amountNeeded;
        for (int i = 0; i < slots.Length; i++)
        {
            if (string.IsNullOrEmpty(slots[i])) continue;
            string itemName = slots[i];
            int count = 1;
            if (slots[i].Contains(":"))
            {
                var parts = slots[i].Split(':');
                itemName = parts[0];
                int.TryParse(parts[1], out count);
            }

            if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
            {
                if (count <= remainingNeeded)
                {
                    remainingNeeded -= count;
                    slots[i] = "";
                }
                else
                {
                    slots[i] = itemName + ":" + (count - remainingNeeded);
                    remainingNeeded = 0;
                }

                if (remainingNeeded <= 0) break;
            }
        }

        if (player.IsStandaloneMode || player.IsOwner)
        {
            if (hud != null)
            {
                hud.SetInventorySlots(slots);
            }
        }

        player.SavePlayerStateToDatabase();
    }

    #endregion
}
