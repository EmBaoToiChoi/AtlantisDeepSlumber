using UnityEngine;
using Unity.Netcode;

public class CrystalCore : NetworkBehaviour
{
    // CÁC BIẾN CHO ASCENSION MANAGER (HỆ THỐNG GIẢI ĐỐ)
    public int crystalID; 
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> isSnapping = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);

    private Collider crystalCollider;
    private Rigidbody rb;
    
    // Lưu vị trí tay cầm (tránh dùng SetParent gây lỗi Netcode)
    private Transform currentHoldPoint;

    private void Awake()
    {
        crystalCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
    }

    // Cơ chế bám tay mượt mà mỗi khung hình
    private void LateUpdate()
    {
        if (currentHoldPoint != null)
        {
            transform.position = currentHoldPoint.position;
            transform.rotation = currentHoldPoint.rotation;
        }
    }

    // --- HÀM THỰC HIỆN NHẶT ---
    public void PerformPickup(ulong clientId)
    {
        if (crystalCollider != null) crystalCollider.enabled = false;
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false;

        GameObject player = FindPlayerByClientId(clientId);
        if (player != null)
        {
            var pInt = player.GetComponentInChildren<PlayerInteraction>();
            currentHoldPoint = (pInt != null && pInt.holdPoint != null) ? pInt.holdPoint : player.transform;
        }
        
        NotifyPickupClientRpc(clientId);
    }

    // --- HÀM THỰC HIỆN THẢ ---
    public void PerformDrop()
    {
        currentHoldPoint = null; 

        if (crystalCollider != null) crystalCollider.enabled = true;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForce(transform.forward * 2f, ForceMode.Impulse);
        }

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;

        NotifyDropClientRpc();
    }

    // --- HÀM KHÓA VÀO TRẠM ---
    public void LockToStation()
    {
        if (IsServer) isSnapped.Value = true;

        currentHoldPoint = null; 

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        if (crystalCollider != null) crystalCollider.enabled = false;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;

        NotifyLockToStationClientRpc();
    }

    // --- HÀM BẮT ĐẦU BAY VÀO TRỤ ---
    public void StartSnappingToStation(Transform targetPoint)
    {
        LockToStation();
        var snapFollow = GetComponent<CrystalSnapFollow>();
        if (snapFollow != null)
        {
            snapFollow.targetSnapPoint = targetPoint;
        }
    }

    // ==========================================
    // CÁC HÀM MẠNG (ĐÃ FIX LỖI TÌM PLAYER)
    // ==========================================
    private GameObject FindPlayerByClientId(ulong clientId)
    {
        // Sử dụng GetPlayerNetworkObject cực kỳ an toàn cho cả Server và Client
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj != null) 
            {
                return playerObj.gameObject;
            }
        }
        return null;
    }

    [ClientRpc]
    private void NotifyPickupClientRpc(ulong clientId)
    {
        if (IsServer) return; 
        if (crystalCollider != null) crystalCollider.enabled = false;
        if (rb != null) rb.isKinematic = true;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false;

        GameObject player = FindPlayerByClientId(clientId);
        if (player != null)
        {
            var pInt = player.GetComponentInChildren<PlayerInteraction>();
            currentHoldPoint = (pInt != null && pInt.holdPoint != null) ? pInt.holdPoint : player.transform;
        }
    }

    [ClientRpc]
    private void NotifyDropClientRpc()
    {
        if (IsServer) return;
        currentHoldPoint = null;
        
        if (crystalCollider != null) crystalCollider.enabled = true;
        if (rb != null) rb.isKinematic = false;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;
    }

    [ClientRpc]
    private void NotifyLockToStationClientRpc()
    {
        if (IsServer) return;
        currentHoldPoint = null;
        
        if (rb != null) rb.isKinematic = true;
        if (crystalCollider != null) crystalCollider.enabled = false;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;
    }
}