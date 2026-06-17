using UnityEngine;
using Unity.Netcode;

public class ExperienceGem : NetworkBehaviour
{
    [Header("Gem Settings")]
    public float expAmount = 25f;
    public float attractionRadius = 6f;
    public float moveSpeed = 2f;
    public float acceleration = 8f;

    [Header("Floating Animation Settings")]
    public float rotationSpeed = 60f;
    public float bobSpeed = 2f;
    public float bobRange = 0.12f;

    [Header("Sync Group ID")]
    public NetworkVariable<NetworkString> networkDropGroupId = new NetworkVariable<NetworkString>(
        new NetworkString(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private string localDropGroupId = "";

    public string DropGroupId
    {
        get
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
            {
                string val = networkDropGroupId.Value;
                if (!string.IsNullOrEmpty(val)) return val;
            }
            return localDropGroupId;
        }
        set
        {
            // Sửa lỗi: Tạo hậu tố duy nhất cho mỗi cục EXP để người chơi có thể ăn được tất cả 4 cục
            string uniqueValue = value;
            if (!string.IsNullOrEmpty(value) && !value.Contains("_gem_"))
            {
                uniqueValue = value + "_gem_" + System.Guid.NewGuid().ToString().Substring(0, 8);
            }
            localDropGroupId = uniqueValue;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (IsServer || NetworkManager.Singleton.IsServer)
                {
                    networkDropGroupId.Value = (NetworkString)uniqueValue;
                }
            }
        }
    }

    [Header("Network Sync Target")]
    public NetworkVariable<NetworkObjectReference> networkTargetPlayer = new NetworkVariable<NetworkObjectReference>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Spawn Settings")]
    public float attractionDelay = 1.0f; // Trì hoãn 1.0 giây trước khi bắt đầu bị hút để đồng bộ mạng hiển thị
    private float spawnTimer = 0f;

    private Transform targetPlayer;
    private bool isAttracted = false;
    private float startY;

    private void Start()
    {
        startY = transform.position.y;
        spawnTimer = attractionDelay;
    }

    private void Update()
    {
        // Hiệu ứng xoay tròn 3D để vật phẩm rơi trông sinh động và lấp lánh hơn
        transform.Rotate(new Vector3(0.2f, 1.0f, 0.15f).normalized, rotationSpeed * Time.deltaTime, Space.Self);

        if (spawnTimer > 0f)
        {
            spawnTimer -= Time.deltaTime;
        }

        if (!isAttracted)
        {
            Vector3 currentPos = transform.position;
            currentPos.y = startY + Mathf.Sin(Time.time * bobSpeed) * bobRange;
            transform.position = currentPos;
        }

        bool isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

        if (isMultiplayer)
        {
            if (IsServer)
            {
                if (!isAttracted && spawnTimer <= 0f)
                {
                    FindClosestPlayerInRange();
                    if (isAttracted && targetPlayer != null)
                    {
                        var netObj = targetPlayer.GetComponentInParent<NetworkObject>();
                        if (netObj == null) netObj = targetPlayer.GetComponentInChildren<NetworkObject>();
                        if (netObj != null)
                        {
                            networkTargetPlayer.Value = netObj;
                        }
                    }
                }
            }
            else
            {
                // Client: Nhận mục tiêu đồng bộ từ Server
                if (networkTargetPlayer.Value.TryGet(out NetworkObject netObj))
                {
                    targetPlayer = netObj.transform;
                    isAttracted = true;
                }
                else
                {
                    targetPlayer = null;
                    isAttracted = false;
                }
            }
        }
        else
        {
            // Standalone mode
            if (!isAttracted && spawnTimer <= 0f)
            {
                FindClosestPlayerInRange();
            }
        }

        // Thực hiện di chuyển nếu đã bị hút
        if (isAttracted && targetPlayer != null)
        {
            // Kiểm tra trạng thái của targetPlayer
            IPlayerHUDTarget ps = targetPlayer.GetComponentInParent<IPlayerHUDTarget>();
            if (ps == null) ps = targetPlayer.GetComponentInChildren<IPlayerHUDTarget>();

            if (ps == null || ps.CurrentHealth <= 0 || PlayerGemCollectionHelper.HasCollected(targetPlayer, DropGroupId))
            {
                targetPlayer = null;
                isAttracted = false;
                if (isMultiplayer && IsServer)
                {
                    networkTargetPlayer.Value = default;
                }
                return;
            }

            // Nhắm vào ngang bụng/ngực player tầm y + 1.3f
            Vector3 targetPos = targetPlayer.position + Vector3.up * 1.3f;
            float distance = Vector3.Distance(transform.position, targetPos);
            
            // Di chuyển nhanh dần đều về phía player
            moveSpeed += acceleration * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

            // Khi chạm vào player (khoảng cách tới targetPos <= 0.8f)
            if (distance <= 0.8f)
            {
                CollectGem();
            }
        }
    }

    private void FindClosestPlayerInRange()
    {
        float minDistance = attractionRadius; // Chỉ tìm trong phạm vi hút
        Transform closest = null;

        // Dùng PlayerHUDManager.ActivePlayers thay vì tag "Player" để đảm bảo tìm thấy mọi loại người chơi
        var activePlayers = PlayerHUDManager.ActivePlayers;
        if (activePlayers != null)
        {
            foreach (var p in activePlayers)
            {
                if (p == null || p.gameObject == null) continue;
                if (p.CurrentHealth <= 0) continue;

                // Bỏ qua nếu người chơi này đã thu thập ngọc từ nhóm rơi này
                if (PlayerGemCollectionHelper.HasCollected(p.transform, DropGroupId)) continue;

                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist <= minDistance)
                {
                    minDistance = dist;
                    closest = p.transform;
                }
            }
        }

        if (closest != null)
        {
            targetPlayer = closest;
            isAttracted = true;
        }
    }

    private void CollectGem()
    {
        if (targetPlayer == null) return;

        IPlayerHUDTarget hudTarget = targetPlayer.GetComponentInParent<IPlayerHUDTarget>();
        if (hudTarget == null) hudTarget = targetPlayer.GetComponentInChildren<IPlayerHUDTarget>();
        
        if (hudTarget != null)
        {
            // Kiểm tra kỹ lại điều kiện tránh bị đua luồng ăn trùng lặp
            if (PlayerGemCollectionHelper.HasCollected(targetPlayer, DropGroupId))
            {
                targetPlayer = null;
                isAttracted = false;
                return;
            }

            // Trong chế độ standalone: xử lý cục bộ và hủy
            if (hudTarget.IsStandaloneMode)
            {
                PlayerGemCollectionHelper.AddCollected(targetPlayer, DropGroupId);
                PlayerGemCollectionHelper.AddExperience(targetPlayer, expAmount);
                Destroy(gameObject);
            }
            // Trong chế độ Netcode: chỉ Server mới xử lý cộng EXP và Despawn
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                PlayerGemCollectionHelper.AddCollected(targetPlayer, DropGroupId);
                PlayerGemCollectionHelper.AddExperience(targetPlayer, expAmount);
                PlayerGemCollectionHelper.TriggerOnCollectGemClientRpc(targetPlayer, DropGroupId); // Thông báo cho client đã thu thập
                
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

public struct NetworkString : INetworkSerializable, System.IEquatable<NetworkString>
{
    private string m_Value;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref m_Value);
    }

    public override string ToString() => m_Value ?? string.Empty;

    public static implicit operator string(NetworkString s) => s.ToString();
    public static implicit operator NetworkString(string s) => new NetworkString { m_Value = s };

    public bool Equals(NetworkString other) => m_Value == other.m_Value;
    public override bool Equals(object obj) => obj is NetworkString other && Equals(other);
    public override int GetHashCode() => m_Value != null ? m_Value.GetHashCode() : 0;
}
