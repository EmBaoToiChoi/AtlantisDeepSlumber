using UnityEngine;
using Unity.Netcode;

public class RepairItemDrop : NetworkBehaviour
{
    [Header("Floating Animation Settings")]
    public float rotationSpeed = 60f;
    public float bobSpeed = 2f;
    public float bobRange = 0.12f;
    public float interactRadius = 2.5f;

    private SimplePlayerTest localPlayer;
    private PlayerHUDController hud;
    private bool isWithinRange = false;
    private float startY;

    private void Start()
    {
        startY = transform.position.y;
    }

    private void Update()
    {
        // 1. Hiệu ứng xoay tròn và nhấp nhô nhè nhẹ để vật phẩm trông sống động
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

            // 2. Nếu người chơi đi vào vùng tương tác
            if (distance <= interactRadius)
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
                        hud.ShowInteractionPrompt(true, "Ấn [F] để nhặt");
                    }
                }

                // 3. Lắng nghe phím F để nhặt
                if (Input.GetKeyDown(KeyCode.F))
                {
                    CollectItem();
                }
            }
            // 4. Nếu người chơi đi ra khỏi vùng tương tác
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

    private void FindLocalPlayer()
    {
        SimplePlayerTest[] players = FindObjectsOfType<SimplePlayerTest>();
        foreach (var p in players)
        {
            if (p.isStandaloneMode || p.IsOwner)
            {
                localPlayer = p;
                break;
            }
        }
    }

    private void CollectItem()
    {
        if (localPlayer == null) return;

        // Thử thêm vật phẩm Búa Rèn vào hòm đồ
        bool added = localPlayer.TryAddItem("RepairHammer");
        if (added)
        {
            // Tắt nhắc nhở tương tác ngay lập tức
            if (hud != null)
            {
                hud.ShowInteractionPrompt(false, "");
            }

            // Hủy/Despawn object
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
            Debug.LogWarning("[RepairItemDrop] Hành trang đầy, không thể nhặt vật phẩm!");
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
