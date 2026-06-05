using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class CrystalCore : NetworkBehaviour
{
    public int crystalID;
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);

    private Vector3 spawnPosition; // Điểm gốc
    private Rigidbody rb;
    private Vector3 originalScale;

    void Awake() 
    { 
        rb = GetComponent<Rigidbody>();
        originalScale = transform.localScale; 
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) spawnPosition = transform.position; // Lưu vị trí khi vừa spawn
    }

    void FixedUpdate() 
    {
        if (!IsServer) return;

        // 1. CHỐT CHẶN: Nếu ngọc bay quá xa HOẶC rớt xuống map -> Reset ngay (ƯU TIÊN SỐ 1)
        if (!isSnapped.Value)
        {
            if (Vector3.Distance(transform.position, spawnPosition) > 80f || transform.position.y < spawnPosition.y - 10f)
            {
                ResetToSpawnPosition();
                return; // Thoát hàm, không làm gì thêm
            }
        }

        // 2. Nội suy kích thước
        float flySpeed = 5f; 
        Vector3 targetScale = (holderId.Value != ulong.MaxValue) ? originalScale * 0.3f : originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, flySpeed * Time.fixedDeltaTime);

        if (isSnapped.Value) return;

        // 3. Logic bay vào tay (chỉ chạy nếu không bị reset)
        if (IsSpawned && holderId.Value != ulong.MaxValue)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(holderId.Value, out var client) && client.PlayerObject != null)
            {
                if (client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.holdPoint != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.MovePosition(Vector3.Lerp(transform.position, pInt.holdPoint.position, flySpeed * Time.fixedDeltaTime));
                    rb.MoveRotation(Quaternion.Lerp(transform.rotation, pInt.holdPoint.rotation, flySpeed * Time.fixedDeltaTime));
                    return; 
                }
            }
            // Nếu không tìm thấy player, tự thả ngọc
            holderId.Value = ulong.MaxValue;
        }
        else if (rb.isKinematic) // Logic rơi tự do
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    public void PerformPickup(ulong playerId)
    {
        if (!IsServer) return;
        GetComponent<NetworkObject>().ChangeOwnership(playerId);
        holderId.Value = playerId; 
    }

    public void PerformDrop()
    {
        if (!IsServer) return;
        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        
        holderId.Value = ulong.MaxValue; 
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void LockToStation()
    {
        if (IsServer)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();

            isSnapped.Value = true;
            holderId.Value = ulong.MaxValue; 
            rb.isKinematic = true; rb.useGravity = false;
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; 
        }
    }

    public void ResetToSpawnPosition()
    {
        if (!IsServer) return;
        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();

        holderId.Value = ulong.MaxValue;
        isSnapped.Value = false;
        transform.position = spawnPosition;
        rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; rb.useGravity = true;
    }
}