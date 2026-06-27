using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class AxeItem : NetworkBehaviour
{


    [Header("Sprite Icon Settings")]
    public Sprite customAxeIcon; // Nếu trống sẽ tự động tải từ Resources "axe_icon"

    // Đồng bộ ulong NetworkObjectId của Player cầm rìu qua mạng (0 nếu rìu ở đất)
    private NetworkVariable<ulong> carryingPlayerId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Rigidbody rb;
    private Collider[] colliders;
    private bool wasInRange = false;

    // Trạng thái theo dõi cục bộ
    private bool isCarryingLocally = false;
    private GameObject localPlayerCarrier = null;
    private GameObject lastNetworkCarrier = null;
    private Vector3 originalWorldScale = Vector3.one;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        colliders = GetComponents<Collider>();
    }

    private void Start()
    {
        originalWorldScale = transform.localScale;
    }

    public override void OnNetworkSpawn()
    {
        carryingPlayerId.OnValueChanged += OnCarrierChanged;
        // Khởi tạo trạng thái ban đầu cho người vào trễ
        UpdateCarrierState(carryingPlayerId.Value);
    }

    public override void OnNetworkDespawn()
    {
        carryingPlayerId.OnValueChanged -= OnCarrierChanged;
    }

    private void Update()
    {
        // Chỉ chạy cho local player điều khiển giao diện & tương tác nhặt/thả
        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            var playerObj = PlayerHUDController.LocalPlayerTarget as MonoBehaviour;
            if (playerObj != null)
            {
                bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
                ulong currentCarrierId = isNetwork ? carryingPlayerId.Value : (localPlayerCarrier != null ? 1u : 0u);

                float dist = Vector3.Distance(transform.position, playerObj.transform.position);
                bool inRange = (currentCarrierId == 0 && dist <= 3f);

                if (inRange)
                {
                    if (!wasInRange)
                    {
                        wasInRange = true;
                        var hud = FindAnyObjectByType<PlayerHUDController>();
                        if (hud != null)
                        {
                            hud.ShowInteractionPrompt(true, "Ấn [F] để nhặt Rìu");
                        }
                    }

                    if (Input.GetKeyDown(KeyCode.F))
                    {
                        // Ẩn prompt tương tác sau khi nhặt
                        wasInRange = false;
                        var hud = FindAnyObjectByType<PlayerHUDController>();
                        if (hud != null) hud.ShowInteractionPrompt(false, "");

                        SetPendingPickItem(playerObj.gameObject, gameObject);
                        PlayPickupAnimation(playerObj.gameObject);
                    }
                }
                else
                {
                    if (wasInRange)
                    {
                        wasInRange = false;
                        var hud = FindAnyObjectByType<PlayerHUDController>();
                        if (hud != null) hud.ShowInteractionPrompt(false, "");
                    }

                    // Nếu bản thân đang cầm rìu thì cho phép thả bằng phím G
                    if (isCarryingLocally && Input.GetKeyDown(KeyCode.G))
                    {
                        if (isNetwork)
                        {
                            RequestDropServerRpc();
                        }
                        else
                        {
                            DropLocal();
                        }
                    }
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerNetId)
    {
        if (carryingPlayerId.Value != 0) return;

        carryingPlayerId.Value = playerNetId;
        
        // Thực hiện parenting của Netcode
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out NetworkObject playerNetObj))
        {
            Transform parentTarget = null;
            var anchor = playerNetObj.GetComponentInChildren<PlayerAxeAnchor>();
            if (anchor != null && anchor.axeHoldingPoint != null)
            {
                parentTarget = anchor.axeHoldingPoint;
            }
            else
            {
                parentTarget = FindRightHand(playerNetObj.transform);
            }

            if (parentTarget != null)
            {
                NetworkObject.TrySetParent(parentTarget, false);
                
                // Đồng bộ vị trí cục bộ ngay trên Server để truyền xuống Client
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                AdjustScaleToParent(parentTarget, playerNetObj.gameObject);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc()
    {
        if (carryingPlayerId.Value == 0) return;

        ulong lastCarrierId = carryingPlayerId.Value;
        carryingPlayerId.Value = 0;

        // Hủy liên kết parent
        NetworkObject.TryRemoveParent();

        // Đặt lại vị trí rơi
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(lastCarrierId, out NetworkObject playerNetObj))
        {
            transform.position = playerNetObj.transform.position + playerNetObj.transform.forward * 1.2f + Vector3.up * 1.3f;
            transform.rotation = Quaternion.identity;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnCarrierChanged(ulong oldVal, ulong newVal)
    {
        UpdateCarrierState(newVal);
    }

    private void UpdateCarrierState(ulong carrierId)
    {
        // 1. Clean up trạng thái người cũ
        if (lastNetworkCarrier != null)
        {
            var oldCarrier = lastNetworkCarrier.GetComponent<PlayerLogCarrier>();
            if (oldCarrier != null) oldCarrier.TogglePlayerWeapons(true);

            var localMono = PlayerHUDController.LocalPlayerTarget as MonoBehaviour;
            if (localMono != null && lastNetworkCarrier == localMono.gameObject)
            {
                isCarryingLocally = false;
                PlayerHUDController.isCarryingAxe = false;
                if (PlayerHUDController.Instance != null)
                {
                    PlayerHUDController.Instance.SetWeapon1IconOverride(null);
                }
            }
        }

        // 2. Cập nhật trạng thái người nhặt mới
        if (carrierId != 0)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(carrierId, out NetworkObject playerNetObj))
            {
                lastNetworkCarrier = playerNetObj.gameObject;

                // Tắt vật lý của rìu cục bộ
                if (rb != null) rb.isKinematic = true;
                foreach (var col in colliders) if (col != null) col.enabled = false;

                // Tìm xương bàn tay hoặc điểm neo transform và gắn rìu vào
                Transform parentTarget = null;
                var anchor = playerNetObj.GetComponentInChildren<PlayerAxeAnchor>();
                Debug.Log($"[AxeItem] Netcode - Kiem tra anchor tren Player: {anchor != null}, holdingPoint: {anchor?.axeHoldingPoint != null}");
                if (anchor != null && anchor.axeHoldingPoint != null)
                {
                    parentTarget = anchor.axeHoldingPoint;
                }
                else
                {
                    parentTarget = FindRightHand(playerNetObj.transform);
                }

                Debug.Log($"[AxeItem] Netcode - parentTarget: {parentTarget?.name}");
                if (parentTarget != null)
                {
                    transform.SetParent(parentTarget, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    AdjustScaleToParent(parentTarget, playerNetObj.gameObject);
                    Debug.Log($"[AxeItem] Netcode - Da set localPosition = zero. local: {transform.localPosition}, world: {transform.position}");
                }
                else
                {
                    Debug.LogError("[AxeItem] Netcode - parentTarget is null!");
                }

                // Ẩn vũ khí hiện tại của nhân vật
                var carrier = lastNetworkCarrier.GetComponent<PlayerLogCarrier>();
                if (carrier != null) carrier.TogglePlayerWeapons(false);

                // Nếu là local player của máy khách này nhặt
                var localMono = PlayerHUDController.LocalPlayerTarget as MonoBehaviour;
                if (localMono != null && playerNetObj.gameObject == localMono.gameObject)
                {
                    isCarryingLocally = true;
                    PlayerHUDController.isCarryingAxe = true;
                    if (PlayerHUDController.Instance != null)
                    {
                        PlayerHUDController.Instance.SelectWeapon(1);
                        
                        Sprite icon = customAxeIcon;
                        if (icon == null) icon = Resources.Load<Sprite>("axe_icon");
                        PlayerHUDController.Instance.SetWeapon1IconOverride(icon);
                    }
                }
            }
        }
        else
        {
            // Rìu được thả xuống đất
            transform.SetParent(null);
            transform.localScale = originalWorldScale;
            
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            foreach (var col in colliders) if (col != null) col.enabled = true;

            isCarryingLocally = false;
            lastNetworkCarrier = null;
        }
    }

    // --- LOGIC NHẶT/THẢ DÀNH CHO OFFLINE (STANDALONE) ---
    private void PickupLocal(GameObject player)
    {
        Debug.Log($"[AxeItem] PickupLocal bat dau, player: {player.name}");
        // Phá hủy component NetworkObject cục bộ bằng Destroy thay vì DestroyImmediate để tránh lỗi trong Animation Event callback
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log("[AxeItem] Huy NetworkObject offline bang Destroy");
            Destroy(netObj);
        }

        StartCoroutine(DeferredPickupLocal(player));
    }

    private IEnumerator DeferredPickupLocal(GameObject player)
    {
        // Chờ đến cuối khung hình/khung hình tiếp theo để NetworkObject thực sự được dọn dẹp khỏi GameObject
        yield return null;

        localPlayerCarrier = player;
        isCarryingLocally = true;

        if (rb != null) rb.isKinematic = true;
        foreach (var col in colliders) if (col != null) col.enabled = false;

        Transform parentTarget = null;
        var anchor = player.GetComponentInChildren<PlayerAxeAnchor>();
        Debug.Log($"[AxeItem] Kiem tra anchor tren Player: {anchor != null}, holdingPoint: {anchor?.axeHoldingPoint != null}");
        if (anchor != null && anchor.axeHoldingPoint != null)
        {
            parentTarget = anchor.axeHoldingPoint;
        }
        else
        {
            parentTarget = FindRightHand(player.transform);
        }

        Debug.Log($"[AxeItem] parentTarget tim duoc: {parentTarget?.name}");
        if (parentTarget != null)
        {
            transform.SetParent(parentTarget, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            AdjustScaleToParent(parentTarget, player);
            Debug.Log($"[AxeItem] Da SetParent va dat localPosition = zero. vi tri thuc te local: {transform.localPosition}, world: {transform.position}");
        }
        else
        {
            Debug.LogError("[AxeItem] Khong tim thay parent target (RightHand / holdingPoint) de gan!");
        }

        var carrier = player.GetComponent<PlayerLogCarrier>();
        if (carrier != null) carrier.TogglePlayerWeapons(false);

        PlayerHUDController.isCarryingAxe = true;
        if (PlayerHUDController.Instance != null)
        {
            PlayerHUDController.Instance.SelectWeapon(1);
            
            Sprite icon = customAxeIcon;
            if (icon == null) icon = Resources.Load<Sprite>("axe_icon");
            PlayerHUDController.Instance.SetWeapon1IconOverride(icon);
        }
    }

    private void DropLocal()
    {
        if (localPlayerCarrier != null)
        {
            var carrier = localPlayerCarrier.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.TogglePlayerWeapons(true);

            PlayerHUDController.isCarryingAxe = false;
            if (PlayerHUDController.Instance != null)
            {
                PlayerHUDController.Instance.SetWeapon1IconOverride(null);
            }

            transform.SetParent(null);
            transform.position = localPlayerCarrier.transform.position + localPlayerCarrier.transform.forward * 1.2f + Vector3.up * 1.3f;
            transform.rotation = Quaternion.identity;
            transform.localScale = originalWorldScale;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            foreach (var col in colliders) if (col != null) col.enabled = true;

            localPlayerCarrier = null;
            isCarryingLocally = false;
        }
    }

    private Transform FindRightHand(Transform root)
    {
        if (root.name.ToLower().Contains("righthand") || root.name.ToLower().Contains("right_hand"))
        {
            return root;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindRightHand(root.GetChild(i));
            if (found != null) return found;
        }
        return null;
    }



    private void AdjustScaleToParent(Transform parentTarget, GameObject player)
    {
        Transform hand = FindRightHand(player.transform);
        Vector3 parentLossyScale = (hand != null) ? hand.lossyScale : player.transform.lossyScale;

        Vector3 relativeLocalScale = new Vector3(
            originalWorldScale.x / (parentLossyScale.x != 0f ? parentLossyScale.x : 1f),
            originalWorldScale.y / (parentLossyScale.y != 0f ? parentLossyScale.y : 1f),
            originalWorldScale.z / (parentLossyScale.z != 0f ? parentLossyScale.z : 1f)
        );

        if (parentTarget != hand)
        {
            Transform anchorParent = parentTarget.parent;
            if (anchorParent != null)
            {
                Vector3 anchorParentLossyScale = anchorParent.lossyScale;
                transform.localScale = new Vector3(
                    originalWorldScale.x / (anchorParentLossyScale.x != 0f ? anchorParentLossyScale.x : 1f),
                    originalWorldScale.y / (anchorParentLossyScale.y != 0f ? anchorParentLossyScale.y : 1f),
                    originalWorldScale.z / (anchorParentLossyScale.z != 0f ? anchorParentLossyScale.z : 1f)
                );
                transform.localScale = Vector3.Scale(transform.localScale, parentTarget.localScale);
            }
            else
            {
                transform.localScale = relativeLocalScale;
            }
        }
        else
        {
            transform.localScale = relativeLocalScale;
        }
    }

    private void PlayPickupAnimation(GameObject player)
    {
        if (player == null) return;

        var leo = player.GetComponent<LeoPlayer>();
        if (leo != null)
        {
            leo.PlayAnimation("Pick", 0.1f);
            return;
        }

        var arthur = player.GetComponent<ArthurPlayer>();
        if (arthur != null)
        {
            arthur.PlayAnimation("Idle_Pick", 0.1f);
            return;
        }

        var elena = player.GetComponent<ElenaPlayer>();
        if (elena != null)
        {
            elena.PlayAnimation("Idle_Pick", 0.1f);
            return;
        }

        var maya = player.GetComponent<MayaPlayer>();
        if (maya != null)
        {
            maya.PlayAnimation("Idle_Pick", 0.1f);
            return;
        }
    }

    public void ConfirmPickup(GameObject player)
    {
        if (player == null) return;

        bool isNetwork = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;
        if (isNetwork)
        {
            ulong localNetId = player.GetComponent<NetworkObject>().NetworkObjectId;
            RequestPickupServerRpc(localNetId);
        }
        else
        {
            PickupLocal(player);
        }
    }

    private void SetPendingPickItem(GameObject player, GameObject item)
    {
        var leo = player.GetComponent<LeoPlayer>();
        if (leo != null) { leo.pendingPickItem = item; return; }

        var arthur = player.GetComponent<ArthurPlayer>();
        if (arthur != null) { arthur.pendingPickItem = item; return; }

        var elena = player.GetComponent<ElenaPlayer>();
        if (elena != null) { elena.pendingPickItem = item; return; }

        var maya = player.GetComponent<MayaPlayer>();
        if (maya != null) { maya.pendingPickItem = item; return; }
    }
}
