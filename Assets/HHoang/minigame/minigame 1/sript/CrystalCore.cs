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
        if (!IsServer || isSnapped.Value) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (holders.Count > 0)
        {
            Vector3 targetPos = Vector3.zero;
            int activeHolders = 0;

            foreach (ulong clientId in holders)
            {
                // Kiểm tra sự tồn tại của client và PlayerObject
                if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) && 
                    client.PlayerObject != null)
                {
                    // Lấy script tương tác thay vì di chuyển
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
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    public void RequestPickup(ulong playerId) => RequestPickupServerRpc(playerId);
    public void RequestDrop(ulong playerId) => RequestDropServerRpc(playerId);

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerId) { if (!holders.Contains(playerId)) holders.Add(playerId); }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc(ulong playerId) 
    { 
        if (holders.Contains(playerId)) holders.Remove(playerId); 
    }
}