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

    void FixedUpdate() 
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
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(holders[i]))
            {
                holders.RemoveAt(i);
            }
        }
    }

    // --- CÁC HÀM GỌI TỪ CLIENT ---
    public void RequestPickup(ulong playerId) 
    {
        if (IsServer) RequestPickupServerRpc(playerId); // Gọi trực tiếp nếu là Server
        else RequestPickupServerRpc(playerId); // Netcode tự xử lý gọi từ Client lên Server
    }
    public void RequestDrop(ulong playerId) => RequestDropServerRpc(playerId);

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerId) 
    { 
        if (!holders.Contains(playerId)) 
        {
            holders.Add(playerId);
            // Chuyển quyền để Client cầm ngọc có quyền ghi dữ liệu
            GetComponent<NetworkObject>().ChangeOwnership(playerId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc(ulong playerId) 
    { 
        if (holders.Contains(playerId)) 
        {
            holders.Remove(playerId);
            
            // Chỉ trả quyền về Server nếu Server chưa sở hữu
            var netObj = GetComponent<NetworkObject>();
            if (netObj.OwnerClientId != NetworkManager.ServerClientId)
            {
                netObj.RemoveOwnership();
            }
            
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    public void LockToStation()
    {
        if (IsServer)
        {
            isSnapped.Value = true;
            rb.isKinematic = true;
            rb.useGravity = false;
            holders.Clear();
            
            // Nếu ngọc thuộc quyền sở hữu của ai đó, thu hồi về Server khi gắn vào trạm
            if (GetComponent<NetworkObject>().IsOwner) 
                GetComponent<NetworkObject>().RemoveOwnership();
        }
    }
}