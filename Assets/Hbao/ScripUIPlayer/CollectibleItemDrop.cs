using UnityEngine;
using Unity.Netcode;

public class CollectibleItemDrop : NetworkBehaviour
{
    [Header("Item Settings")]
    [Tooltip("Tên của vật phẩm để phân biệt trong hòm đồ (Ví dụ: Ngoc1, Ngoc2)")]
    public string itemName = "Ngoc1";
    public float interactRadius = 2.5f;

    [Header("Floating Animation Settings")]
    public float rotationSpeed = 60f;
    public float bobSpeed = 2f;
    public float bobRange = 0.12f;

    private LeoPlayer localPlayer;
    private PlayerHUDController hud;
    private bool isWithinRange = false;
    private float startY;

    private void Start()
    {
        startY = transform.position.y;
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
            if (localPlayer.CurrentHealth <= 0)
            {
                if (isWithinRange)
                {
                    isWithinRange = false;
                    if (hud != null) hud.ShowInteractionPrompt(false, "");
                }
                return;
            }

            float distance = Vector3.Distance(transform.position, localPlayer.transform.position);
            bool isClosest = IsClosestItem();

            // 2. Nếu người chơi đi vào vùng tương tác và là vật phẩm gần nhất
            if (distance <= interactRadius && isClosest)
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

        // Kiểm tra tất cả CollectibleItemDrop
        CollectibleItemDrop[] collectibles = FindObjectsOfType<CollectibleItemDrop>();
        foreach (var item in collectibles)
        {
            if (item == this || item == null) continue;
            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.interactRadius && dist < myDist)
            {
                return false;
            }
        }

        // Kiểm tra tất cả RepairItemDrop
        RepairItemDrop[] repairs = FindObjectsOfType<RepairItemDrop>();
        foreach (var item in repairs)
        {
            if (item == null) continue;
            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.interactRadius && dist < myDist)
            {
                return false;
            }
        }

        return true;
    }

    private void FindLocalPlayer()
    {
        LeoPlayer[] players = FindObjectsOfType<LeoPlayer>();
        foreach (var p in players)
        {
            if (p.isStandaloneMode || p.IsOwner)
            {
                localPlayer = p;
                break;
            }
        }
    }

    private void StartCollecting()
    {
        if (localPlayer == null) return;

        // Gán vật phẩm chờ nhặt cho người chơi
        localPlayer.pendingPickItem = gameObject;

        // Tắt nhắc nhở tương tác ngay lập tức
        if (hud != null)
        {
            hud.ShowInteractionPrompt(false, "");
        }

        // Phát hoạt ảnh nhặt đồ trên Player
        localPlayer.PlayAnimation("Pick", 0.1f);
    }

    public void ConfirmCollect()
    {
        if (localPlayer == null) return;

        // Thử thêm vật phẩm vào hòm đồ mà không chạy lại hoạt ảnh
        bool added = localPlayer.TryAddItem(itemName, false);
        if (added)
        {
            // Hủy/Despawn vật lý
            if (localPlayer.isStandaloneMode)
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
    private void RequestDespawnServerRpc()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // Đảm bảo dọn dẹp prompt tương tác khi vật phẩm bị hủy
        if (isWithinRange && hud != null)
        {
            hud.ShowInteractionPrompt(false, "");
        }
    }
}
