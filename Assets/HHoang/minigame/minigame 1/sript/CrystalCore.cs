using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class CrystalCore : NetworkBehaviour
{
    public int crystalID;
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);

    private Rigidbody rb;
    private Vector3 originalScale;

    void Awake() 
    { 
        rb = GetComponent<Rigidbody>();
        originalScale = transform.localScale; 
    }

    void FixedUpdate() 
    {
        if (!IsServer) return;

        // 1. LUÔN LUÔN NỘI SUY KÍCH THƯỚC (Dù cầm hay thả đều phóng/thu mượt)
        float flySpeed = 5f; 
        Vector3 targetScale = (holderId.Value != ulong.MaxValue) ? originalScale * 0.3f : originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, flySpeed * Time.fixedDeltaTime);

        // Nếu ngọc đã khóa vào bệ thì xong nhiệm vụ, ngưng xử lý vị trí
        if (isSnapped.Value) return;

        // 2. Logic bay mượt vào tay khi có người cầm
        if (IsSpawned && holderId.Value != ulong.MaxValue)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(holderId.Value, out var client) && client.PlayerObject != null)
            {
                if (client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.holdPoint != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    
                    Vector3 smoothPosition = Vector3.Lerp(transform.position, pInt.holdPoint.position, flySpeed * Time.fixedDeltaTime);
                    rb.MovePosition(smoothPosition);

                    Quaternion smoothRotation = Quaternion.Lerp(transform.rotation, pInt.holdPoint.rotation, flySpeed * Time.fixedDeltaTime);
                    rb.MoveRotation(smoothRotation);

                    return; 
                }
            }
        }

        // 3. Logic rơi tự do khi không ai cầm
        if (rb.isKinematic && holderId.Value == ulong.MaxValue)
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
        if (netObj.OwnerClientId != NetworkManager.ServerClientId)
        {
            netObj.RemoveOwnership(); 
        }
        
        holderId.Value = ulong.MaxValue; 
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void LockToStation()
    {
        if (IsServer)
        {
            // --- FIX LỖI RỚT ĐÁ TRÊN VPS ---
            // Tước quyền sở hữu của Client, giao lại cho Server quản lý vị trí
            var netObj = GetComponent<NetworkObject>();
            if (netObj.OwnerClientId != NetworkManager.ServerClientId)
            {
                netObj.RemoveOwnership(); 
            }
            // -------------------------------

            isSnapped.Value = true;
            holderId.Value = ulong.MaxValue; 
            
            rb.isKinematic = true; 
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero; 
            rb.angularVelocity = Vector3.zero; 
        }
    }
}