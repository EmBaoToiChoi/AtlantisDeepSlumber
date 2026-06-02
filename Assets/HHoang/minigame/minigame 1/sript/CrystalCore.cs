using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CrystalCore : NetworkBehaviour
{
    public int crystalID;
    public NetworkList<ulong> holders;
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);

    void Awake() => holders = new NetworkList<ulong>();

    void Update()
    {
        // Chỉ Server thực hiện tính toán vật lý
        if (!IsServer || isSnapped.Value) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        
        // Dọn dẹp danh sách holder nếu có client nào đó đã ngắt kết nối
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
                transform.position = targetPos / activeHolders;
                rb.isKinematic = true;
            }
        }
        else
        {
            // Nếu không có ai giữ, trả về trạng thái vật lý bình thường
            if (rb.isKinematic) 
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
    }

    private void CleanupDisconnectedHolders()
    {
        // Duyệt ngược để an toàn khi remove item khỏi list
        for (int i = holders.Count - 1; i >= 0; i--)
        {
            if (!NetworkManager.ConnectedClients.ContainsKey(holders[i]))
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
        if (!holders.Contains(playerId)) holders.Add(playerId); 
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc(ulong playerId) 
    { 
        if (holders.Contains(playerId)) holders.Remove(playerId); 
    }
}