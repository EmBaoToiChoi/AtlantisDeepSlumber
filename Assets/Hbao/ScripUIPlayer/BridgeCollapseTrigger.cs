using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BridgeCollapseTrigger : NetworkBehaviour
{
    [Header("Bridge Components Configuration")]
    [Tooltip("Các mảng cầu nguyên vẹn ban đầu (Sẽ bị tắt khi sập, bật lại khi sửa)")]
    public GameObject[] stableBridgeSegments;

    [Tooltip("Các mảng cầu sập/gãy (Sẽ được bật lên khi sập, tắt đi khi sửa)")]
    public GameObject[] brokenBridgeSegments;

    [Tooltip("Các Rigidbody để tạo hiệu ứng rớt cầu vật lý tự do")]
    public Rigidbody[] fallingRigidbodies;

    [Tooltip("Animator để chạy hoạt ảnh sập/sửa (nếu có)")]
    public Animator bridgeAnimator;
    public string collapseTriggerName = "Collapse";
    public string repairTriggerName = "Repair";

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

    private bool localCollapseTriggered = false;
    private IPlayerHUDTarget localPlayer;
    private PlayerHUDController hud;
    private bool isPlayerInRange = false;

    private void Awake()
    {
        // Đảm bảo visual ban đầu khớp với trạng thái chưa sập
        SetStableSegmentsActive(true);
        SetBrokenSegmentsActive(false);
        SetRigidbodiesKinematic(true);
    }

    private void Start()
    {
        hud = FindObjectOfType<PlayerHUDController>();
    }

    public override void OnNetworkSpawn()
    {
        // Đăng ký nhận sự kiện thay đổi trạng thái từ Server
        hasCollapsed.OnValueChanged += OnCollapseStateChanged;
        hasBeenRepaired.OnValueChanged += OnRepairStateChanged;

        // Cập nhật trạng thái hiển thị cho người vào trễ
        ApplyBridgeVisualState(hasCollapsed.Value, hasBeenRepaired.Value);
    }

    public override void OnNetworkDespawn()
    {
        hasCollapsed.OnValueChanged -= OnCollapseStateChanged;
        hasBeenRepaired.OnValueChanged -= OnRepairStateChanged;
    }

    private void Update()
    {
        // 1. Tìm local player nếu chưa có
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // 2. Xử lý tương tác sửa cầu khi cầu đã sập và chưa được sửa
        if (hasCollapsed.Value && !hasBeenRepaired.Value && localPlayer != null)
        {
            float distance = Vector3.Distance(transform.position, localPlayer.transform.position);

            if (distance <= repairInteractRadius)
            {
                int currentWood = GetPlayerWoodCount(localPlayer);
                
                if (!isPlayerInRange)
                {
                    isPlayerInRange = true;
                }

                if (hud != null)
                {
                    if (currentWood >= requiredLogsToRepair)
                    {
                        hud.ShowInteractionPrompt(true, $"Ấn [E] để sửa cầu ({currentWood}/{requiredLogsToRepair} thanh gỗ)");
                        
                        if (Input.GetKeyDown(KeyCode.E))
                        {
                            RequestRepairBridge();
                        }
                    }
                    else
                    {
                        hud.ShowInteractionPrompt(true, $"Cần {requiredLogsToRepair} thanh gỗ để sửa cầu ({currentWood}/{requiredLogsToRepair})");
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
            Debug.Log($"[BridgeCollapseTrigger] Người chơi '{other.gameObject.name}' đi vào vùng kích hoạt. Tiến hành sập cầu!");
            
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
        // 1. Đổi hiển thị mảng cầu
        SetStableSegmentsActive(false);
        SetBrokenSegmentsActive(true);

        // 2. Kích hoạt hiệu ứng vật lý rơi tự do
        SetRigidbodiesKinematic(false);
        ApplyFallForces();

        // 3. Chạy Animator (nếu có)
        if (bridgeAnimator != null && !string.IsNullOrEmpty(collapseTriggerName))
        {
            bridgeAnimator.SetTrigger(collapseTriggerName);
        }

        // 4. Hiển thị UI Quest trên Client hiện tại
        if (hud != null)
        {
            hud.ShowQuest(true);
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

    #region Repair Logic

    private void RequestRepairBridge()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Chế độ online: Gọi ServerRpc để Server thực thi và trừ gỗ
            RepairBridgeServerRpc();
        }
        else
        {
            // Chế độ offline: Thực hiện sửa cục bộ ngay
            ConsumePlayerWood(localPlayer, requiredLogsToRepair);
            hasBeenRepaired.Value = true; // Sẽ kích hoạt OnRepairStateChanged
            RepairBridgeLocal();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RepairBridgeServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Tìm player component thuộc sở hữu của clientId này
        IPlayerHUDTarget playerTarget = FindPlayerByClientId(clientId);
        if (playerTarget != null)
        {
            int woodCount = GetPlayerWoodCount(playerTarget);
            if (woodCount >= requiredLogsToRepair)
            {
                // Trừ gỗ trên Server (sẽ lưu DB và đồng bộ Client)
                ConsumePlayerWood(playerTarget, requiredLogsToRepair);
                hasBeenRepaired.Value = true;
                Debug.Log($"[BridgeCollapseTrigger] Server: Đã sửa cầu thành công cho Client {clientId}.");
            }
            else
            {
                Debug.LogWarning($"[BridgeCollapseTrigger] Server: Client {clientId} yêu cầu sửa cầu nhưng không đủ gỗ ({woodCount}/{requiredLogsToRepair}).");
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

        // 2. Khôi phục visual cầu nguyên vẹn
        SetStableSegmentsActive(true);
        SetBrokenSegmentsActive(false);
        SetRigidbodiesKinematic(true);

        // 3. Chạy Animator sửa cầu
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

    #endregion

    #region Wood Spawning

    private void SpawnWoodLogsServer()
    {
        if (!IsServer || woodLogPrefab == null) return;

        Debug.Log("[BridgeCollapseTrigger] Server: Đang spawn 16 thanh gỗ...");

        for (int i = 0; i < logsToSpawn; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            GameObject wood = Instantiate(woodLogPrefab, spawnPos, Quaternion.identity);
            
            // Đồng bộ Network Object
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

        // Tự động spawn ngẫu nhiên hình tròn xung quanh trigger trên mặt đất
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 2.0f, randomCircle.y);

        // Snap xuống mặt đất bằng Raycast
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

    private void ApplyBridgeVisualState(bool collapsed, bool repaired)
    {
        if (repaired)
        {
            SetStableSegmentsActive(true);
            SetBrokenSegmentsActive(false);
            SetRigidbodiesKinematic(true);
            if (hud != null) hud.ShowQuest(false);
        }
        else if (collapsed)
        {
            SetStableSegmentsActive(false);
            SetBrokenSegmentsActive(true);
            SetRigidbodiesKinematic(false);
            if (hud != null)
            {
                hud.ShowQuest(true);
                // Cập nhật lại số lượng gỗ hiện tại cho người vào sau
                if (localPlayer != null)
                {
                    int woodCount = GetPlayerWoodCount(localPlayer);
                    hud.UpdateQuestProgress(woodCount, requiredLogsToRepair);
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

    private void SetRigidbodiesKinematic(bool kinematic)
    {
        if (fallingRigidbodies != null)
        {
            foreach (var rb in fallingRigidbodies)
            {
                if (rb != null) rb.isKinematic = kinematic;
            }
        }
    }

    private void ApplyFallForces()
    {
        if (fallingRigidbodies != null)
        {
            foreach (var rb in fallingRigidbodies)
            {
                if (rb != null && !rb.isKinematic)
                {
                    // Tạo một lực đẩy nhẹ xuống và xoay ngẫu nhiên cho tự nhiên
                    rb.AddForce(Vector3.down * 1.5f + Random.insideUnitSphere * 0.3f, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
                }
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
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && (p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        // Fallback: Tìm qua Object Type nếu list ActivePlayers trống
        LeoPlayer[] leos = FindObjectsOfType<LeoPlayer>();
        foreach (var p in leos) { if (p.isStandaloneMode || p.IsOwner) { localPlayer = p; return; } }

        ArthurPlayer[] arthurs = FindObjectsOfType<ArthurPlayer>();
        foreach (var p in arthurs) { if (p.isStandaloneMode || p.IsOwner) { localPlayer = p; return; } }

        ElenaPlayer[] elenas = FindObjectsOfType<ElenaPlayer>();
        foreach (var p in elenas) { if (p.isStandaloneMode || p.IsOwner) { localPlayer = p; return; } }

        MayaPlayer[] mayas = FindObjectsOfType<MayaPlayer>();
        foreach (var p in mayas) { if (p.isStandaloneMode || p.IsOwner) { localPlayer = p; return; } }
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

        // Chỉ Client sở hữu mới cập nhật local UI hòm đồ
        if (player.IsStandaloneMode || player.IsOwner)
        {
            if (hud != null)
            {
                hud.SetInventorySlots(slots);
            }
        }

        // Lưu trạng thái của người chơi
        player.SavePlayerStateToDatabase();
    }

    #endregion
}
