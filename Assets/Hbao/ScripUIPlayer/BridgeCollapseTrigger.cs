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
    private bool isGhostModeActive = false;
    // Lưu materials gốc của tất cả renderer để khôi phục sau khi sửa cầu
    private System.Collections.Generic.List<Renderer> savedRenderers = new System.Collections.Generic.List<Renderer>();
    private System.Collections.Generic.List<Material[]> savedMaterials = new System.Collections.Generic.List<Material[]>();

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

    public NetworkVariable<bool> isReadyToBuild = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> buildProgress = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Coop Build Config")]
    public float coopBuildDecayRate = 8f;
    public float soloBuildDecayRate = 0.5f;
    public float buildProgressPerClick = 1.5f;
    public ParticleSystem buildProgressParticles;

    [HideInInspector]
    public bool localReadyToBuild = false;
    [HideInInspector]
    public float localBuildProgress = 0f;

    private Material ghostMaterialInstance;
    private float lastProgress = 0f;
    private float particleStopTimer = 0f;

    private bool localCollapseTriggered = false;
    private bool localRepaired = false;
    private int localLogsSubmittedCount = 0;
    private IPlayerHUDTarget localPlayer;
    private PlayerHUDController hud;
    private float nextPlayerSearchTime = 0f;

    public bool IsBridgeCollapsed()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return hasCollapsed.Value;
        }
        return localCollapseTriggered;
    }

    public bool IsBridgeRepaired()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return hasBeenRepaired.Value;
        }
        return localRepaired;
    }

    public int GetLogsSubmittedCount()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return logsSubmitted.Value;
        }
        return localLogsSubmittedCount;
    }

    private void Awake()
    {
        // Fallback nếu mainBridgeObject bị bỏ trống trong Inspector
        if (mainBridgeObject == null && stableBridgeSegments != null && stableBridgeSegments.Length > 0)
        {
            mainBridgeObject = stableBridgeSegments[0];
            Debug.Log("[BridgeCollapseTrigger] Tự động gán mainBridgeObject bằng stableBridgeSegments[0] làm fallback!");
        }

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
        // Đảm bảo WoodLogObjectPool được khởi tạo sớm
        var pool = WoodLogObjectPool.Instance;
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
        // Đồng bộ local với NetworkVariable ban đầu khi spawn trên mạng
        localCollapseTriggered = hasCollapsed.Value;
        localRepaired = hasBeenRepaired.Value;
        localLogsSubmittedCount = logsSubmitted.Value;

        // Đăng ký nhận sự kiện thay đổi trạng thái từ Server
        hasCollapsed.OnValueChanged += OnCollapseStateChanged;
        hasBeenRepaired.OnValueChanged += OnRepairStateChanged;
        logsSubmitted.OnValueChanged += OnLogsSubmittedChanged;
        isReadyToBuild.OnValueChanged += OnReadyToBuildChanged;

        // Cập nhật trạng thái hiển thị cho người vào trễ
        ApplyBridgeVisualState(hasCollapsed.Value, hasBeenRepaired.Value, logsSubmitted.Value);
    }

    public override void OnNetworkDespawn()
    {
        hasCollapsed.OnValueChanged -= OnCollapseStateChanged;
        hasBeenRepaired.OnValueChanged -= OnRepairStateChanged;
        logsSubmitted.OnValueChanged -= OnLogsSubmittedChanged;
        isReadyToBuild.OnValueChanged -= OnReadyToBuildChanged;
    }

    private void OnReadyToBuildChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
            if (localHud != null)
            {
                localHud.ShowMissionAlert("GỖ ĐÃ ĐỦ! HÃY LẠI GẦN CẦU VÀ ẤN F ĐỂ CÙNG XÂY DỰNG!", 6.0f);
            }
        }
    }

    private void Update()
    {
        // 1. Tìm local player nếu chưa có
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // 1.5. Đảm bảo gắn chỉ đường cho local player nếu cầu sập và chưa sửa xong
        if (IsBridgeCollapsed() && !IsBridgeRepaired() && localPlayer != null)
        {
            var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
            if (indicator == null)
            {
                int classIndex = localPlayer.CharacterClassIndex;
                ChoppableTree myTree = ResolveTreeForClass(classIndex);
                bool isTreeCut = myTree != null && ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? myTree.isCutDown.Value : !myTree.gameObject.activeInHierarchy);
                if (myTree != null && !isTreeCut)
                {
                    indicator = localPlayer.gameObject.AddComponent<TreeGuidanceIndicator>();
                    indicator.targetTree = myTree;
                    indicator.reachDistance = 4f;
                    Debug.Log($"[BridgeCollapseTrigger] Gắn chỉ đường cho Local Player {localPlayer.DisplayName} tới cây: {myTree.name}");
                }
            }
        }

        // 2. Coop Building Progress and Decay logic
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool ready = isNetwork ? isReadyToBuild.Value : localReadyToBuild;
        float currentProgress = isNetwork ? buildProgress.Value : localBuildProgress;

        if (ready)
        {
            if (isNetwork && IsServer)
            {
                if (buildProgress.Value > 0f && buildProgress.Value < 100f)
                {
                    buildProgress.Value = Mathf.Clamp(buildProgress.Value - coopBuildDecayRate * Time.deltaTime, 0f, 100f);
                }
            }
            else if (!isNetwork)
            {
                if (localBuildProgress > 0f && localBuildProgress < 100f)
                {
                    localBuildProgress = Mathf.Clamp(localBuildProgress - soloBuildDecayRate * Time.deltaTime, 0f, 100f);
                }
            }

            // Update ghost alpha dynamically as progress increases
            UpdateGhostAlpha(currentProgress);

            // Emit particles when progress increases
            if (currentProgress > lastProgress)
            {
                SetParticlesActive(true);
                particleStopTimer = 0.5f; // keep playing for 0.5s after clicks stop
            }
            lastProgress = currentProgress;

            if (particleStopTimer > 0f)
            {
                particleStopTimer -= Time.deltaTime;
                if (particleStopTimer <= 0f)
                {
                    SetParticlesActive(false);
                }
            }

            // Periodically sync UI alert / quest to say "Bạn cần tương tác để xây cầu"
            PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();
            if (localHudCtl != null)
            {
                localHudCtl.UpdateQuestDescription("Bạn cần tương tác để xây cầu!");
                localHudCtl.UpdateQuestProgress((int)currentProgress, 100);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra sập cầu (nếu chưa sập)
        if (!IsBridgeCollapsed())
        {
            if (IsPlayer(other.gameObject))
            {
                Debug.Log($"[BridgeCollapseTrigger] Người chơi '{other.gameObject.name}' đi vào vùng kích hoạt.");
                
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    if (IsServer)
                    {
                        // Server tự kích hoạt trực tiếp
                        TriggerBridgeCollapseServer();
                    }
                    else
                    {
                        // Client gửi yêu cầu lên server (client không phải host cũng có thể kích hoạt)
                        RequestCollapseServerRpc();
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
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCollapseServerRpc()
    {
        if (!IsServer || IsBridgeCollapsed()) return;
        TriggerBridgeCollapseServer();
    }



    #region Collapse Logic

    private void TriggerBridgeCollapseServer()
    {
        if (!IsServer) return;
        
        hasCollapsed.Value = true;
        
        // Server thực hiện spawn gỗ xung quanh cầu
        SpawnWoodLogsServer();
        
        // Thông báo tất cả client hiển thị quest UI
        ShowCollapseUIClientRpc();
    }

    [ClientRpc]
    private void ShowCollapseUIClientRpc()
    {
        // Mỗi client tự tìm và hiển thị HUD của mình
        PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
        if (localHud != null)
        {
            localHud.ShowQuest(true);
            localHud.UpdateQuestProgress(0, requiredLogsToRepair);
            localHud.ShowMissionAlert("CẦU ĐÃ BỊ SẬP! HÃY TÌM 16 THANH GỖ ĐỂ SỬA LẠI CẦU!", 5.0f);
        }
    }

    [ClientRpc]
    private void ShowRepairCompleteUIClientRpc()
    {
        PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
        if (localHud != null)
        {
            localHud.ShowInteractionPrompt(false, "");
            localHud.ShowQuest(false);
            localHud.ShowMissionAlert("CẦU ĐÃ ĐƯỢC SỬA CHỮA THÀNH CÔNG!", 5.0f);
        }
    }

    private void CollapseBridgeLocal()
    {
        localCollapseTriggered = true;

        // Đảm bảo lưu lại vị trí ban đầu của cầu trước khi bị ẩn đi
        if (mainBridgeObject != null && originalBridgePos == Vector3.zero)
        {
            originalBridgePos = mainBridgeObject.transform.position;
            originalBridgeRot = mainBridgeObject.transform.rotation;
        }

        // Ẩn stableBridgeSegments riêng
        SetStableSegmentsActive(false);
        SetBrokenSegmentsActive(true);

        // 1. Chuyển mainBridgeObject sang chế độ Ghost (giữ active, đổi sang màu trắng trong suốt)
        if (mainBridgeObject != null)
        {
            ApplyGhostMode(mainBridgeObject, true);
        }

        // 2. Ghost bridge hiển thị ngay lập tức (mainBridgeObject vẫn active, chỉ đổi material)

        // Tìm local player để thêm mũi tên hướng dẫn chỉ về cây gỗ nhiệm vụ tương ứng
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (localPlayer != null)
        {
            int classIndex = localPlayer.CharacterClassIndex;
            ChoppableTree myTree = ResolveTreeForClass(classIndex);
            bool isTreeCut = myTree != null && ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? myTree.isCutDown.Value : !myTree.gameObject.activeInHierarchy);
            if (myTree != null && !isTreeCut)
            {
                var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
                if (indicator == null)
                {
                    indicator = localPlayer.gameObject.AddComponent<TreeGuidanceIndicator>();
                }
                indicator.targetTree = myTree;
                indicator.reachDistance = 4f;
                Debug.Log($"[BridgeCollapseTrigger] Gắn mũi tên chỉ đường cho Local Player {localPlayer.DisplayName} (class {classIndex}) tới cây: {myTree.name}");
            }
        }

        // 3. Chạy Animator (nếu có)
        if (bridgeAnimator != null && !string.IsNullOrEmpty(collapseTriggerName))
        {
            bridgeAnimator.SetTrigger(collapseTriggerName);
        }

        // 4. Hiển thị UI Quest trên Client (dùng dynamic lookup để luôn tìm đúng HUD đang active)
        PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        if (localHudCtl != null)
        {
            int currentProgress = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? logsSubmitted.Value : localLogsSubmittedCount;
            localHudCtl.ShowQuest(true);
            localHudCtl.UpdateQuestProgress(currentProgress, requiredLogsToRepair);
            localHudCtl.ShowMissionAlert("CẦU ĐÃ BỊ SẬP! HÃY TÌM 16 THANH GỖ ĐỂ SỬA LẠI CẦU!", 5.0f);
        }

        // 5. Nếu là Standalone thì tự động spawn gỗ cục bộ
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            SpawnWoodLogsLocal();
        }
    }

    private void OnCollapseStateChanged(bool oldVal, bool newVal)
    {
        localCollapseTriggered = newVal;
        if (newVal)
        {
            Debug.Log("[BridgeCollapseTrigger] Server báo trạng thái Cầu Đã Sập.");
            CollapseBridgeLocal();
        }
    }

    #endregion

    #region Repair & Log Submission Logic

    public void RequestSubmitCarriedLog()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            var netPlayer = localPlayer.gameObject.GetComponent<NetworkObject>();
            if (netPlayer != null)
            {
                var carrier = localPlayer.gameObject.GetComponent<PlayerLogCarrier>();
                int amount = carrier != null ? carrier.carriedLogCount : 1;
                if (carrier != null)
                {
                    carrier.DropLog();
                }
                SubmitCarriedLogServerRpc(netPlayer.NetworkObjectId, amount);
            }
        }
        else
        {
            // Chế độ offline/standalone
            var carrier = localPlayer.gameObject.GetComponent<PlayerLogCarrier>();
            int amount = carrier != null ? carrier.carriedLogCount : 1;
            if (carrier != null)
            {
                carrier.DropLog();
            }

            int remaining = requiredLogsToRepair - GetLogsSubmittedCount();
            if (remaining > 0)
            {
                int actualSubmit = Mathf.Min(amount, remaining);
                localLogsSubmittedCount += actualSubmit;
                if (localLogsSubmittedCount >= requiredLogsToRepair)
                {
                    localReadyToBuild = true;
                    PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
                    if (localHud != null)
                    {
                        localHud.ShowMissionAlert("GỖ ĐÃ ĐỦ! HÃY LẠI GẦN CẦU VÀ ẤN F ĐỂ CÙNG XÂY DỰNG!", 6.0f);
                        localHud.UpdateQuestDescription("Bạn cần tương tác để xây cầu!");
                        localHud.UpdateQuestProgress(0, 100);
                    }
                }
                else
                {
                    if (hud != null)
                    {
                        hud.UpdateQuestProgress(localLogsSubmittedCount, requiredLogsToRepair);
                    }
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitCarriedLogServerRpc(ulong playerNetObjectId, int amount)
    {
        if (!IsServer) return;

        SubmitCarriedLogClientRpc(playerNetObjectId);

        int remaining = requiredLogsToRepair - logsSubmitted.Value;
        if (remaining > 0)
        {
            int actualSubmit = Mathf.Min(amount, remaining);
            logsSubmitted.Value += actualSubmit;
            if (logsSubmitted.Value >= requiredLogsToRepair)
            {
                isReadyToBuild.Value = true;
                Debug.Log("[BridgeCollapseTrigger] Server: Cầu đã đủ gỗ, chuyển sang ReadyToBuild!");
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
            localLogsSubmittedCount += amount;
            
            if (localLogsSubmittedCount >= requiredLogsToRepair)
            {
                localReadyToBuild = true;
                PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
                if (localHud != null)
                {
                    localHud.ShowMissionAlert("GỖ ĐÃ ĐỦ! HÃY LẠI GẦN CẦU VÀ ẤN F ĐỂ CÙNG XÂY DỰNG!", 6.0f);
                    localHud.UpdateQuestDescription("Bạn cần tương tác để xây cầu!");
                    localHud.UpdateQuestProgress(0, 100);
                }
            }
            else
            {
                if (hud != null)
                {
                    hud.UpdateQuestProgress(localLogsSubmittedCount, requiredLogsToRepair);
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
                    isReadyToBuild.Value = true;
                    Debug.Log("[BridgeCollapseTrigger] Server: Cầu đã đủ gỗ, chuyển sang ReadyToBuild!");
                }
            }
        }
    }

    private void RepairBridgeLocal()
    {
        localRepaired = true;
        // 1. Tắt nhắc nhở tương tác (dùng dynamic HUD lookup)
        PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        if (localHudCtl != null)
        {
            localHudCtl.ShowInteractionPrompt(false, "");
            localHudCtl.ShowQuest(false);
            localHudCtl.ShowMissionAlert("CẦU ĐÃ ĐƯỢC SỬA CHỮA THÀNH CÔNG!", 5.0f);
        }

        // Dọn dẹp mũi tên chỉ đường nếu còn
        if (localPlayer != null)
        {
            var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }

        // 2. Khôi phục cây cầu gốc (xóa ghost mode, hiện lại và khôi phục materials)
        if (mainBridgeObject != null)
        {
            ApplyGhostMode(mainBridgeObject, false);
            mainBridgeObject.transform.position = originalBridgePos;
            mainBridgeObject.transform.rotation = originalBridgeRot;
        }

        SetStableSegmentsActive(true);
        SetBrokenSegmentsActive(false);

        // 3. Khôi phục tất cả materials đã lưu
        for (int i = 0; i < savedRenderers.Count; i++)
        {
            if (savedRenderers[i] != null)
            {
                savedRenderers[i].materials = savedMaterials[i];
            }
        }
        savedRenderers.Clear();
        savedMaterials.Clear();
        isGhostModeActive = false;

        // 4. Chạy Animator sửa cầu (nếu có)
        if (bridgeAnimator != null && !string.IsNullOrEmpty(repairTriggerName))
        {
            bridgeAnimator.SetTrigger(repairTriggerName);
        }
    }

    private void OnRepairStateChanged(bool oldVal, bool newVal)
    {
        localRepaired = newVal;
        if (newVal)
        {
            Debug.Log("[BridgeCollapseTrigger] Cầu đã được sửa hoàn tất.");
            RepairBridgeLocal();
        }
    }

    private void OnLogsSubmittedChanged(int oldVal, int newVal)
    {
        localLogsSubmittedCount = newVal;
        Debug.Log($"[BridgeCollapseTrigger] Tiến trình xây cầu thay đổi: {newVal}/{requiredLogsToRepair}");
        
        // Cập nhật UI - dùng dynamic lookup để luôn tìm đúng HUD đang active trên màn hình của client này
        PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        if (localHudCtl != null)
        {
            localHudCtl.UpdateQuestProgress(newVal, requiredLogsToRepair);
        }
    }

    #endregion

    #region Blueprint & Wood Spawning Effects

    /// <summary>
    /// Chuyển bridge sang chế độ Ghost (màu trắng trong suốt) hoặc khôi phục lại.
    /// Lưu materials gốc khi bật ghost, khôi phục khi tắt ghost.
    /// </summary>
    private void ApplyGhostMode(GameObject bridgeRoot, bool enableGhost)
    {
        if (bridgeRoot == null) return;

        if (enableGhost && !isGhostModeActive)
        {
            isGhostModeActive = true;
            savedRenderers.Clear();
            savedMaterials.Clear();

            // Đảm bảo bridge vẫn active để render được
            bridgeRoot.SetActive(true);

            // Thu thập tất cả renderers trong bridge (bao gồm cả các inactive children)
            var renderers = bridgeRoot.GetComponentsInChildren<Renderer>(true);
            
            // Tạo 1 vật liệu ghost trắng dùng chung để đảm bảo hiển thị màu sắc trắng và trong suốt hoạt động hoàn hảo trên Standard/URP
            ghostMaterialInstance = CreateGhostMaterial(0.35f);
            Material ghostMat = ghostMaterialInstance;
            
            foreach (var rend in renderers)
            {
                if (rend == null) continue;

                // Lưu materials gốc
                savedRenderers.Add(rend);
                savedMaterials.Add(rend.sharedMaterials);

                // Tạo mảng vật liệu ghost mới cho tất cả các submesh
                Material[] ghostMats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < ghostMats.Length; i++)
                {
                    ghostMats[i] = ghostMat;
                }
                rend.materials = ghostMats;

                // Đảm bảo child này được active để thấy ghost
                rend.gameObject.SetActive(true);
            }

            // Không cho va chạm vậtlý bật lại trên ghost - disable colliders
            foreach (var col in bridgeRoot.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            Debug.Log($"[BridgeCollapseTrigger] Ghost mode BẬT - Đã đổi {renderers.Length} renderer sang trắng trong suốt.");
        }
        else if (!enableGhost && isGhostModeActive)
        {
            isGhostModeActive = false;
            ghostMaterialInstance = null;

            // Khôi phục colliders
            if (bridgeRoot != null)
            {
                foreach (var col in bridgeRoot.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = true;
                }
            }

            // Khôi phục materials gốc
            for (int i = 0; i < savedRenderers.Count; i++)
            {
                if (savedRenderers[i] != null)
                {
                    savedRenderers[i].materials = savedMaterials[i];
                }
            }
            savedRenderers.Clear();
            savedMaterials.Clear();

            Debug.Log("[BridgeCollapseTrigger] Ghost mode TẮT - Đã khôi phục materials gốc.");
        }
    }

    private void UpdateGhostAlpha(float progress)
    {
        if (ghostMaterialInstance != null)
        {
            float alpha = 0.35f + (progress / 100f) * 0.55f;
            Color col = new Color(1f, 1f, 1f, alpha);
            if (ghostMaterialInstance.HasProperty("_BaseColor"))
            {
                ghostMaterialInstance.SetColor("_BaseColor", col);
            }
            else if (ghostMaterialInstance.HasProperty("_Color"))
            {
                ghostMaterialInstance.SetColor("_Color", col);
            }
        }
    }

    private void SetParticlesActive(bool active)
    {
        if (buildProgressParticles == null)
        {
            buildProgressParticles = GetComponentInChildren<ParticleSystem>();
        }

        if (buildProgressParticles != null)
        {
            var emission = buildProgressParticles.emission;
            emission.enabled = active;
            if (active && !buildProgressParticles.isPlaying)
            {
                buildProgressParticles.Play();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClickBuildServerRpc()
    {
        if (!IsServer || !isReadyToBuild.Value) return;

        buildProgress.Value = Mathf.Min(buildProgress.Value + buildProgressPerClick, 100f);
        if (buildProgress.Value >= 100f)
        {
            hasBeenRepaired.Value = true;
            isReadyToBuild.Value = false;
            Debug.Log("[BridgeCollapseTrigger] Server: Cầu đã được xây dựng xong!");
            ShowRepairCompleteUIClientRpc();
        }
    }

    public void ClickBuildLocal()
    {
        if (!localReadyToBuild) return;

        localBuildProgress = Mathf.Min(localBuildProgress + buildProgressPerClick, 100f);
        if (localBuildProgress >= 100f)
        {
            localRepaired = true;
            localReadyToBuild = false;
            Debug.Log("[BridgeCollapseTrigger] Standalone: Cầu đã được xây dựng xong!");
            RepairBridgeLocal();
        }
    }


    private Material CreateGhostMaterial(float alpha)
    {
        bool isURP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        Shader s = null;
        if (isURP)
        {
            s = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Sprites/Default");

        Material mat = new Material(s);

        if (mat.HasProperty("_Cull"))
        {
            mat.SetFloat("_Cull", 0f); // Tắt Cull để vẽ cả 2 mặt
        }

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
            }
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        }
        else
        {
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f); // 3 = Transparent for Built-in Standard shader
            }
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            }
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
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
            GameObject wood = WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, spawnPos, Quaternion.identity);
            
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
            WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, spawnPos, Quaternion.identity);
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
        localCollapseTriggered = collapsed;
        localRepaired = repaired;
        localLogsSubmittedCount = logsProgress;

        PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();

        if (repaired)
        {
            if (mainBridgeObject != null)
            {
                ApplyGhostMode(mainBridgeObject, false);
                mainBridgeObject.transform.position = originalBridgePos;
                mainBridgeObject.transform.rotation = originalBridgeRot;
            }
            SetStableSegmentsActive(true);
            SetBrokenSegmentsActive(false);
            if (localPlayer == null) FindLocalPlayer();
            if (localPlayer != null)
            {
                var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
                if (indicator != null) Destroy(indicator);
            }
            if (localHudCtl != null) localHudCtl.ShowQuest(false);
        }
        else if (collapsed)
        {
            SetStableSegmentsActive(false);
            SetBrokenSegmentsActive(true);
            if (mainBridgeObject != null)
            {
                ApplyGhostMode(mainBridgeObject, true);
            }
            if (localPlayer == null) FindLocalPlayer();
            if (localPlayer != null)
            {
                int classIndex = localPlayer.CharacterClassIndex;
                ChoppableTree myTree = ResolveTreeForClass(classIndex);
                bool isTreeCut = myTree != null && ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? myTree.isCutDown.Value : !myTree.gameObject.activeInHierarchy);
                if (myTree != null && !isTreeCut)
                {
                    var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
                    if (indicator == null)
                        indicator = localPlayer.gameObject.AddComponent<TreeGuidanceIndicator>();
                    indicator.targetTree = myTree;
                    indicator.reachDistance = 4f;
                }
            }
            if (localHudCtl != null)
            {
                localHudCtl.ShowQuest(true);
                localHudCtl.UpdateQuestProgress(logsProgress, requiredLogsToRepair);
            }
        }
        else
        {
            if (mainBridgeObject != null)
                ApplyGhostMode(mainBridgeObject, false);
            SetStableSegmentsActive(true);
            SetBrokenSegmentsActive(false);
            if (localPlayer == null) FindLocalPlayer();
            if (localPlayer != null)
            {
                var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
                if (indicator != null) Destroy(indicator);
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

        if (go.GetComponent<IPlayerHUDTarget>() != null || go.GetComponentInParent<IPlayerHUDTarget>() != null) return true;

        if (go.CompareTag("Player") || (go.transform.parent != null && go.transform.parent.CompareTag("Player"))) return true;

        return false;
    }

    private ChoppableTree ResolveTreeForClass(int classIndex)
    {
        ChoppableTree myTree = null;
        if (targetTrees != null && targetTrees.Length > 0)
        {
            int idx = classIndex % targetTrees.Length;
            if (idx >= 0 && idx < targetTrees.Length)
            {
                myTree = targetTrees[idx];
            }
        }
        
        // Fallback 1: Lấy cây đầu tiên không null trong mảng targetTrees
        if (myTree == null && targetTrees != null)
        {
            foreach (var tree in targetTrees)
            {
                if (tree != null)
                {
                    myTree = tree;
                    break;
                }
            }
        }
        
        // Fallback 2: Lấy bất kỳ cây ChoppableTree nào trong cảnh
        if (myTree == null)
        {
            var allTrees = FindObjectsByType<ChoppableTree>(FindObjectsSortMode.None);
            if (allTrees != null && allTrees.Length > 0)
            {
                myTree = allTrees[0];
            }
        }
        return myTree;
    }

    private void FindLocalPlayer()
    {
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // 1. Try static cache first (zero overhead)
        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            var p = PlayerHUDController.LocalPlayerTarget;
            if (p != null && (!isNetworkActive || p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        // 2. Try PlayerHUDManager active players cache next
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && (!isNetworkActive || p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        // 3. Fallback to finding any IPlayerHUDTarget in the scene, rate-limited to once every 2 seconds
        if (Time.time >= nextPlayerSearchTime)
        {
            nextPlayerSearchTime = Time.time + 2f;

            var allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mono in allComponents)
            {
                if (mono is IPlayerHUDTarget p)
                {
                    if (!isNetworkActive || p.IsStandaloneMode || p.IsOwner)
                    {
                        localPlayer = p;
                        return;
                    }
                }
            }
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
