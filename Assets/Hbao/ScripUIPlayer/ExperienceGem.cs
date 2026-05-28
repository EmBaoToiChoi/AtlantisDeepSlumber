using UnityEngine;
using Unity.Netcode;

public class ExperienceGem : NetworkBehaviour
{
    [Header("Gem Settings")]
    public float expAmount = 25f;
    public float attractionRadius = 6f;
    public float moveSpeed = 2f;
    public float acceleration = 8f;

    [Header("Sync Group ID")]
    public NetworkVariable<Unity.Collections.FixedString64Bytes> networkDropGroupId = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private string localDropGroupId = "";

    public string DropGroupId
    {
        get { return (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? networkDropGroupId.Value.ToString() : localDropGroupId; }
        set {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (IsServer)
                {
                    networkDropGroupId.Value = value;
                }
            }
            else
            {
                localDropGroupId = value;
            }
        }
    }

    private Transform targetPlayer;
    private bool isAttracted = false;

    private void Update()
    {
        // 1. Tìm player gần nhất
        if (targetPlayer == null)
        {
            FindClosestPlayer();
        }

        if (targetPlayer != null)
        {
            SimplePlayerTest ps = targetPlayer.GetComponentInParent<SimplePlayerTest>();
            // Nếu người chơi đã chết hoặc đã ăn một ngọc từ nhóm này, hủy mục tiêu và tìm người khác
            if (ps == null || ps.CurrentHealth <= 0 || ps.HasCollectedFromDropGroup(DropGroupId))
            {
                targetPlayer = null;
                isAttracted = false;
                return;
            }

            float distance = Vector3.Distance(transform.position, targetPlayer.position);
            
            // Nếu đã bị hút hoặc player đi vào vùng hút
            if (isAttracted || distance <= attractionRadius)
            {
                isAttracted = true;
                
                // Di chuyển nhanh dần đều về phía player (nhắm vào ngang người player tầm y + 1.0f)
                moveSpeed += acceleration * Time.deltaTime;
                Vector3 targetPos = targetPlayer.position + Vector3.up * 1f;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

                // Khi chạm vào player
                if (distance <= 0.8f)
                {
                    CollectGem();
                }
            }
        }
    }

    private void FindClosestPlayer()
    {
        float minDistance = float.MaxValue;
        SimplePlayerTest closest = null;
        
        SimplePlayerTest[] players = FindObjectsOfType<SimplePlayerTest>();
        foreach (var p in players)
        {
            if (p.CurrentHealth <= 0) continue;
            
            // Bỏ qua nếu người chơi này đã thu thập ngọc từ nhóm rơi này
            if (p.HasCollectedFromDropGroup(DropGroupId)) continue;

            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = p;
            }
        }

        if (closest != null)
        {
            targetPlayer = closest.transform;
        }
    }

    private void CollectGem()
    {
        SimplePlayerTest playerScript = targetPlayer.GetComponentInParent<SimplePlayerTest>();
        if (playerScript != null)
        {
            // Kiểm tra kỹ lại điều kiện tránh bị đua luồng ăn trùng lặp
            if (playerScript.HasCollectedFromDropGroup(DropGroupId))
            {
                targetPlayer = null;
                isAttracted = false;
                return;
            }

            // Trong chế độ standalone: xử lý cục bộ và hủy
            if (playerScript.isStandaloneMode)
            {
                playerScript.AddCollectedDropGroup(DropGroupId);
                playerScript.AddExperience(expAmount);
                Destroy(gameObject);
            }
            // Trong chế độ Netcode: chỉ Server mới xử lý cộng EXP và Despawn
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                playerScript.AddCollectedDropGroup(DropGroupId);
                playerScript.AddExperience(expAmount);
                playerScript.OnCollectGemClientRpc(DropGroupId); // Thông báo cho client đã thu thập
                
                if (GetComponent<NetworkObject>() != null && GetComponent<NetworkObject>().IsSpawned)
                {
                    GetComponent<NetworkObject>().Despawn();
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
