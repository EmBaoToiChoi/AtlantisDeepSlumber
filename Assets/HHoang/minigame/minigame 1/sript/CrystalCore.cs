using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class CrystalCore : NetworkBehaviour
{
    public int crystalID;
    public NetworkList<ulong> holders;
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);

    private Rigidbody rb;

    void Awake() 
    { 
        holders = new NetworkList<ulong>(); 
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate() // Dùng FixedUpdate cho các thao tác vật lý/di chuyển
    {
        if (!IsServer || isSnapped.Value) return;

        CleanupDisconnectedHolders();

        if (holders.Count > 0)
        {
            Vector3 targetPos = Vector3.zero;
            int activeHolders = 0;

            foreach (ulong clientId in holders)
            {
                if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) && 
                    client.PlayerObject != null)
                {
                    if (client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.holdPoint != null)
                    {
                        targetPos += pInt.holdPoint.position;
                        activeHolders++;
                    }
                }
            }

            if (activeHolders > 0)
            {
                // Dùng MovePosition thay vì set thẳng transform để tránh lỗi xuyên vật thể
                rb.MovePosition(targetPos / activeHolders);
                rb.isKinematic = true;
            }
        }
        else
        {
            if (rb.isKinematic) 
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
    }

    private void CleanupDisconnectedHolders()
    {
        for (int i = holders.Count - 1; i >= 0; i--)
        {
            // Kiểm tra an toàn xem client còn tồn tại không
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(holders[i]))
            {
                holders.RemoveAt(i);
            }
        }
    }

    public void RequestPickup(ulong playerId) => RequestPickupServerRpc(playerId);
    public void RequestDrop(ulong playerId) => RequestDropServerRpc(playerId);

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerId) 
    { 
        // 1. Kiểm tra xem người chơi này đã cầm ngọc chưa
        if (!holders.Contains(playerId)) 
        {
            holders.Add(playerId);
            
            // 2. CHUYỂN QUYỀN SỞ HỮU cho người chơi mới nhặt
            // Netcode sẽ cho phép Client này gửi dữ liệu vị trí vật thể lên Server
            var netObj = GetComponent<NetworkObject>();
            if (netObj.OwnerClientId != playerId)
            {
                netObj.ChangeOwnership(playerId);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc(ulong playerId) 
    { 
        if (holders.Contains(playerId)) holders.Remove(playerId); 
    }
}