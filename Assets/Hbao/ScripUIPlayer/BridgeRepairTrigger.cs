using UnityEngine;

public class BridgeRepairTrigger : MonoBehaviour
{
    [Header("Bridge Collapse Trigger Reference")]
    [Tooltip("Reference tới script BridgeCollapseTrigger quản lý trạng thái cầu")]
    public BridgeCollapseTrigger bridgeController;

    private IPlayerHUDTarget localPlayer;
    private Collider triggerCollider;
    private bool wasInRange = false;
    private float nextPlayerSearchTime = 0f;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        if (bridgeController == null)
        {
            bridgeController = FindAnyObjectByType<BridgeCollapseTrigger>();
            if (bridgeController != null)
            {
                Debug.Log("[BridgeRepairTrigger] Tự động tìm thấy BridgeCollapseTrigger trong Start!");
            }
        }
    }

    private void Update()
    {
        if (bridgeController == null)
        {
            bridgeController = FindAnyObjectByType<BridgeCollapseTrigger>();
            if (bridgeController == null) return;
        }

        // Chỉ hoạt động khi cầu đã sập và chưa sửa xong
        if (bridgeController.IsBridgeCollapsed() && !bridgeController.IsBridgeRepaired())
        {
            if (localPlayer == null)
            {
                FindLocalPlayer();
            }

            if (localPlayer != null)
            {
                // Đo khoảng cách từ người chơi đến điểm gần nhất của Collider (phù hợp cho các trigger hình hộp dài)
                // Tránh tình trạng Pivot ở giữa cách xa người chơi khi họ đang đứng ở các góc/đầu trigger.
                bool inRange = false;
                if (triggerCollider != null)
                {
                    Vector3 playerPos = localPlayer.transform.position;
                    Vector3 closestPoint = triggerCollider.bounds.ClosestPoint(playerPos);
                    float distToCollider = Vector3.Distance(playerPos, closestPoint);
                    if (distToCollider <= bridgeController.repairInteractRadius || triggerCollider.bounds.Contains(playerPos))
                    {
                        inRange = true;
                    }
                }
                else
                {
                    float distance = Vector3.Distance(localPlayer.transform.position, transform.position);
                    inRange = distance <= bridgeController.repairInteractRadius;
                }

                bool isReady = (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening) ? bridgeController.isReadyToBuild.Value : bridgeController.localReadyToBuild;

                if (inRange)
                {
                    wasInRange = true;
                    PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
                    if (localHud != null)
                    {
                        if (isReady)
                        {
                            float buildProgressVal = (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening) ? bridgeController.buildProgress.Value : bridgeController.localBuildProgress;
                            if (PlayerHUDController.isCoopBuildingUIOpen)
                            {
                                localHud.ShowInteractionPrompt(true, $"Spam [SPACE] để xây cầu | [F] để thoát (Tiến độ: {(int)buildProgressVal}%)");
                            }
                            else
                            {
                                localHud.ShowInteractionPrompt(true, $"Ấn [SPACE] hoặc [F] để xây cầu (Tiến độ: {(int)buildProgressVal}%)");
                            }

                            if (Input.GetKeyDown(KeyCode.F))
                            {
                                localHud.ToggleCoopBuildUI(bridgeController);
                            }
                            else if (Input.GetKeyDown(KeyCode.Space) && !PlayerHUDController.isCoopBuildingUIOpen)
                            {
                                localHud.OpenCoopBuildUI(bridgeController);
                            }
                        }
                        else
                        {
                            var carrier = localPlayer.gameObject.GetComponent<PlayerLogCarrier>();
                            bool isCarrying = carrier != null && carrier.isCarrying;

                            int logsSubmitted = bridgeController.GetLogsSubmittedCount();
                            int requiredLogsToRepair = bridgeController.requiredLogsToRepair;
                            int remaining = requiredLogsToRepair - logsSubmitted;

                            if (remaining > 0)
                            {
                                if (isCarrying)
                                {
                                    localHud.ShowInteractionPrompt(true, $"Ấn [G] để góp gỗ sửa cầu ({logsSubmitted}/{requiredLogsToRepair})");

                                    if (Input.GetKeyDown(KeyCode.G))
                                    {
                                        bridgeController.RequestSubmitCarriedLog();
                                    }
                                }
                                else
                                {
                                    localHud.ShowInteractionPrompt(true, $"Hãy tìm và bưng gỗ đến đây để sửa cầu ({logsSubmitted}/{requiredLogsToRepair})");
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (wasInRange)
                    {
                        wasInRange = false;
                        PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
                        if (localHud != null)
                        {
                            localHud.ShowInteractionPrompt(false, "");
                            localHud.CloseCoopBuildUI();
                        }
                    }
                }
            }
        }
        else
        {
            // Reset prompt nếu trạng thái cầu thay đổi
            if (wasInRange)
            {
                wasInRange = false;
                PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
                if (localHud != null)
                {
                    localHud.ShowInteractionPrompt(false, "");
                    localHud.CloseCoopBuildUI();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other) { }
    private void OnTriggerStay(Collider other) { }
    private void OnTriggerExit(Collider other) { }

    private bool IsLocalPlayerObject(GameObject go)
    {
        if (localPlayer == null) return IsPlayer(go);

        if (go == localPlayer.gameObject || go.transform.IsChildOf(localPlayer.transform) || localPlayer.transform.IsChildOf(go.transform))
        {
            return true;
        }
        return false;
    }

    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;

        if (go.GetComponent<IPlayerHUDTarget>() != null || go.GetComponentInParent<IPlayerHUDTarget>() != null) return true;

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

        // 3. Fallback to finding any IPlayerHUDTarget in the scene, rate-limited to once every 2 seconds
        if (Time.time >= nextPlayerSearchTime)
        {
            nextPlayerSearchTime = Time.time + 2f;

            var allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mono in allComponents)
            {
                if (mono is IPlayerHUDTarget p)
                {
                    if (p.IsStandaloneMode || p.IsOwner)
                    {
                        localPlayer = p;
                        return;
                    }
                }
            }
        }
    }
}
