using UnityEngine;
using Unity.Netcode;

public class CrystalCore : NetworkBehaviour
{
    // CÁC BIẾN CHO ASCENSION MANAGER (HỆ THỐNG GIẢI ĐỐ)
    public int crystalID; 
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> isSnapping = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);

    // THAY ĐỔI: Dùng mảng để lấy TẤT CẢ Collider (tránh lỗi ngọc có 2-3 cái collider)
    private Collider[] allColliders;
    private Rigidbody rb;
    
    // Lưu vị trí tay cầm
    private Transform currentHoldPoint;

    private void Awake()
    {
        // Lấy tất cả collider gắn trên viên ngọc
        allColliders = GetComponentsInChildren<Collider>();
        
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

    // Hàm hỗ trợ bật/tắt toàn bộ Collider
    private void SetCollidersState(bool state)
    {
        if (allColliders == null) return;
        foreach (var col in allColliders)
        {
            col.enabled = state;
        }
    }

    // --- HÀM THỰC HIỆN NHẶT ---
    public void PerformPickup(ulong clientId)
    {
        // TẮT TOÀN BỘ COLLIDER ĐỂ KHÔNG VA CHẠM VỚI NGƯỜI
        SetCollidersState(false);
        
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

        // THÊM DÒNG NÀY: Đẩy viên ngọc ra trước mặt 0.5 mét và nâng lên một chút 
        // để khi bật Collider nó không bị kẹt vào bụng hoặc cẳng chân của nhân vật (gây lỗi xuyên sàn)
        transform.position += Vector3.up * 0.5f + transform.forward * 0.6f;

        // BẬT LẠI TOÀN BỘ COLLIDER ĐỂ KHÔNG RỚT XUYÊN MAP
        SetCollidersState(true);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            // Đẩy nhẹ ra trước cho tự nhiên
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
        
        SetCollidersState(false); // Khóa vào trạm thì tắt va chạm cho đỡ kẹt đường

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
    // CÁC HÀM MẠNG ĐỒNG BỘ CHO CLIENT
    // ==========================================
    private GameObject FindPlayerByClientId(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj != null) return playerObj.gameObject;
        }
        return null;
    }

    [ClientRpc]
    private void NotifyPickupClientRpc(ulong clientId)
    {
        if (IsServer) return; 
        
        SetCollidersState(false);
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
        
        SetCollidersState(true);
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
        SetCollidersState(false);

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;
    }
}