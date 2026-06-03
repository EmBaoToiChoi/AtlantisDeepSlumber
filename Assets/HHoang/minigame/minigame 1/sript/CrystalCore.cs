using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class CrystalCore : NetworkBehaviour
{
    public int crystalID;
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);

    private Rigidbody rb;

    void Awake() 
    { 
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate() 
    {
        // 1. Chỉ Server mới điều khiển vị trí vật lý. Nếu ngọc đã khóa vào trạm thì ngưng xử lý.
        if (!IsServer || isSnapped.Value) return;

        // 2. Tự động bám tay người chơi (Dựa vào Ownership)
        // Nếu ngọc thuộc về một người chơi (Không phải Server)
        if (IsSpawned && OwnerClientId != NetworkManager.ServerClientId)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerClientId, out var client) && client.PlayerObject != null)
            {
                if (client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.holdPoint != null)
                {
                    // Ép ngọc bám theo tay
                    rb.MovePosition(pInt.holdPoint.position);
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    return; // Đang có người cầm, không xử lý rơi tự do
                }
            }
        }

        // 3. Logic rơi tự do (Không ai cầm ngọc)
        if (rb.isKinematic) 
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    // Client gửi lệnh nhặt -> Yêu cầu đổi chủ
    public void RequestPickup(ulong playerId) 
    {
        if (IsServer) RequestPickupServerRpc(playerId);
        else RequestPickupServerRpc(playerId);
    }

    // Client gửi lệnh thả
    public void RequestDrop(ulong playerId) => RequestDropServerRpc(playerId);

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerId) 
    { 
        // Giao quyền kiểm soát viên ngọc cho người chơi
        GetComponent<NetworkObject>().ChangeOwnership(playerId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc(ulong playerId) 
    { 
        // Thu hồi quyền về lại Server để nó rơi xuống đất
        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId)
        {
            netObj.RemoveOwnership();
        }
        
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void LockToStation()
    {
        if (IsServer)
        {
            isSnapped.Value = true;
            
            // Tắt vật lý để nằm im trên bệ
            rb.isKinematic = true; 
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero; 
            rb.angularVelocity = Vector3.zero; 
            
            // Thu hồi quyền
            var netObj = GetComponent<NetworkObject>();
            if (netObj.OwnerClientId != NetworkManager.ServerClientId) 
                netObj.RemoveOwnership();
        }
    }
}