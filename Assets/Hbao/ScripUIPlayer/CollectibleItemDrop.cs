using UnityEngine;
using Unity.Netcode;

public class CollectibleItemDrop : NetworkBehaviour, IInteractableItem
{
    [Header("Item Settings")]
    [Tooltip("Tên của vật phẩm để phân biệt trong hòm đồ (Ví dụ: Ngoc1, Ngoc2)")]
    public string itemName = "Ngoc1";
    public float interactRadius = 2.5f;

    public float InteractRadius => interactRadius;

    [Header("Floating Animation Settings")]
    public float rotationSpeed = 60f;
    public float bobSpeed = 2f;
    public float bobRange = 0.12f;

    private MonoBehaviour localPlayer;

    private float GetPlayerHealth()
    {
        if (localPlayer is LeoPlayer leo) return leo.CurrentHealth;
        if (localPlayer is ArthurPlayer arthur) return arthur.CurrentHealth;
        if (localPlayer is ElenaPlayer elena) return elena.CurrentHealth;
        if (localPlayer is MayaPlayer maya) return maya.CurrentHealth;
        return 0f;
    }

    private void SetPendingPickItem(GameObject item)
    {
        if (localPlayer is LeoPlayer leo) leo.pendingPickItem = item;
        else if (localPlayer is ArthurPlayer arthur) arthur.pendingPickItem = item;
        else if (localPlayer is ElenaPlayer elena) elena.pendingPickItem = item;
        else if (localPlayer is MayaPlayer maya) maya.pendingPickItem = item;
        else if (localPlayer is SimplePlayerTest spt) spt.pendingPickItem = item;
    }

    private void PlayPickAnimation()
    {
        if (localPlayer is LeoPlayer leo) leo.PlayAnimation("Pick", 0.1f);
        else if (localPlayer is ArthurPlayer arthur) arthur.PlayAnimation("Idle_Pick", 0.1f);
        else if (localPlayer is ElenaPlayer elena) elena.PlayAnimation("Idle_Pick", 0.1f);
        else if (localPlayer is MayaPlayer maya) maya.PlayAnimation("Idle_Pick", 0.1f);
    }

    private bool TryAddItem(string name)
    {
        if (localPlayer is LeoPlayer leo) return leo.TryAddItem(name, false);
        if (localPlayer is ArthurPlayer arthur) return arthur.TryAddItem(name, false);
        if (localPlayer is ElenaPlayer elena) return elena.TryAddItem(name);
        if (localPlayer is MayaPlayer maya) return maya.TryAddItem(name);
        return false;
    }

    private bool IsPlayerStandalone()
    {
        if (localPlayer is LeoPlayer leo) return leo.isStandaloneMode;
        if (localPlayer is ArthurPlayer arthur) return arthur.isStandaloneMode;
        if (localPlayer is ElenaPlayer elena) return elena.isStandaloneMode;
        if (localPlayer is MayaPlayer maya) return maya.isStandaloneMode;
        if (localPlayer is SimplePlayerTest spt) return spt.isStandaloneMode;
        
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return true;
        }
        return false;
    }
    private PlayerHUDController hud;
    private bool isWithinRange = false;
    private float startY;

    private void Start()
    {
        startY = transform.position.y;
    }

    private void OnEnable()
    {
        InteractionRegistry.Register(this);
        // Reset startY cho các vật phẩm lấy từ ObjectPool
        startY = transform.position.y;
    }

    private void OnDisable()
    {
        InteractionRegistry.Unregister(this);
        if (isWithinRange && hud != null)
        {
            hud.ShowInteractionPrompt(false, "");
        }
        isWithinRange = false; // Reset trạng thái range khi trả về pool
    }

    private void Update()
    {
        // Hiệu ứng xoay tròn và nhấp nhô nhè nhẹ để vật phẩm rơi trông sinh động hơn
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        Vector3 currentPos = transform.position;
        currentPos.y = startY + Mathf.Sin(Time.time * bobSpeed) * bobRange;
        transform.position = currentPos;
        // 1. Tìm người chơi cục bộ nếu chưa có
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (localPlayer != null)
        {
            // Kiểm tra nếu người chơi đã chết
            if (GetPlayerHealth() <= 0)
            {
                if (isWithinRange)
                {
                    isWithinRange = false;
                    if (hud != null) hud.ShowInteractionPrompt(false, "");
                }
                return;
            }

            float distance = Vector3.Distance(transform.position, localPlayer.transform.position);

            // 2. Nếu người chơi đi vào vùng tương tác và là vật phẩm gần nhất
            if (distance <= interactRadius && IsClosestItem())
            {
                if (!isWithinRange)
                {
                    isWithinRange = true;
                    if (hud == null)
                    {
                        hud = FindObjectOfType<PlayerHUDController>();
                    }
                    if (hud != null)
                    {
                        // Hiển thị gợi ý nhặt
                        hud.ShowInteractionPrompt(true, "Ấn [F] để nhặt");
                    }
                }

                // 3. Lắng nghe phím F để nhặt
                if (Input.GetKeyDown(KeyCode.F))
                {
                    StartCollecting();
                }
            }
            // 4. Nếu người chơi đi ra khỏi vùng tương tác hoặc không còn là gần nhất
            else if (isWithinRange)
            {
                isWithinRange = false;
                if (hud != null)
                {
                    hud.ShowInteractionPrompt(false, "");
                }
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem vật phẩm này có phải là vật phẩm gần người chơi cục bộ nhất hay không
    /// </summary>
    private bool IsClosestItem()
    {
        if (localPlayer == null) return false;

        float myDist = Vector3.Distance(transform.position, localPlayer.transform.position);
        var activeItems = InteractionRegistry.ActiveItems;

        for (int i = 0; i < activeItems.Count; i++)
        {
            var item = activeItems[i];
            if (ReferenceEquals(item, this) || item == null) continue;

            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.InteractRadius && dist < myDist) return false;
        }

        return true;
    }

    private void FindLocalPlayer()
    {
        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            localPlayer = PlayerHUDController.LocalPlayerTarget as MonoBehaviour;
        }
    }

    private void StartCollecting()
    {
        if (localPlayer == null) return;

        // Check if player is carrying wood
        if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
            itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
        {
            var carrier = localPlayer.GetComponent<PlayerLogCarrier>();
            if (carrier != null && carrier.isCarrying)
            {
                if (hud != null)
                {
                    hud.ShowMissionAlert("Bạn đang bưng một thanh gỗ rồi!", 2.0f);
                }
                return;
            }
        }

        // Tắt nhắc nhở tương tác ngay lập tức
        if (hud != null)
        {
            hud.ShowInteractionPrompt(false, "");
        }

        // Set pending item for animation event pickup flow
        SetPendingPickItem(gameObject);

        // Phát hoạt ảnh nhặt đồ trên Player
        PlayPickAnimation();

        // Safety fallback coroutine: if animation event doesn't trigger within 1.0 seconds, force collect
        StartCoroutine(FallbackCollectSequence());
    }

    private System.Collections.IEnumerator FallbackCollectSequence()
    {
        yield return new WaitForSeconds(1.0f);
        
        if (localPlayer != null)
        {
            GameObject pending = null;
            if (localPlayer is LeoPlayer leo) pending = leo.pendingPickItem;
            else if (localPlayer is ArthurPlayer arthur) pending = arthur.pendingPickItem;
            else if (localPlayer is ElenaPlayer elena) pending = elena.pendingPickItem;
            else if (localPlayer is MayaPlayer maya) pending = maya.pendingPickItem;
            else if (localPlayer is SimplePlayerTest spt) pending = spt.pendingPickItem;

            if (pending == gameObject)
            {
                Debug.LogWarning("[CollectibleItemDrop] Animation Event 'OnPickItemEvent' did not fire. Running safety fallback pickup.");
                ConfirmCollect();
                SetPendingPickItem(null);
            }
        }
    }

    public void ConfirmCollect()
    {
        if (localPlayer == null) return;

        // Nếu là gỗ thì bưng lên tay chứ không cho vào hòm đồ
        if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
            itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
        {
            if (IsPlayerStandalone())
            {
                var carrier = localPlayer.GetComponent<PlayerLogCarrier>();
                if (carrier == null)
                {
                    carrier = localPlayer.gameObject.AddComponent<PlayerLogCarrier>();
                }
                carrier.CarryLog();
                // Trả về pool thay vì hủy
                WoodLogObjectPool.Instance.ReturnToPool(gameObject);
            }
            else
            {
                var netPlayer = localPlayer.GetComponent<NetworkObject>();
                if (netPlayer != null)
                {
                    PickUpWoodLogServerRpc(netPlayer.NetworkObjectId);
                }
            }
            return;
        }

        // Thử thêm vật phẩm vào hòm đồ mà không chạy lại hoạt ảnh
        bool added = TryAddItem(itemName);
        if (added)
        {
            // Hủy/Despawn vật lý
            if (IsPlayerStandalone())
            {
                Destroy(gameObject);
            }
            else
            {
                RequestDespawnServerRpc();
            }
        }
        else
        {
            Debug.LogWarning($"[CollectibleItemDrop] Hành trang đầy, không thể nhặt {itemName}!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickUpWoodLogServerRpc(ulong playerNetObjectId)
    {
        if (!IsServer) return;

        PickUpWoodLogClientRpc(playerNetObjectId);

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else
        {
            if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
            {
                WoodLogObjectPool.Instance.ReturnToPool(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    [ClientRpc]
    private void PickUpWoodLogClientRpc(ulong playerNetObjectId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNetObj))
        {
            var playerObj = playerNetObj.gameObject;
            var carrier = playerObj.GetComponent<PlayerLogCarrier>();
            if (carrier == null)
            {
                carrier = playerObj.AddComponent<PlayerLogCarrier>();
            }
            carrier.CarryLog();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDespawnServerRpc()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else
        {
            if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
            {
                WoodLogObjectPool.Instance.ReturnToPool(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }


}
