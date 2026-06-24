using UnityEngine;
using Unity.Netcode;

public class CrystalCore : NetworkBehaviour
{
    // CÁC BIẾN CHO ASCENSION MANAGER (HỆ THỐNG GIẢI ĐỐ)
    public int crystalID; 
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> isSnapping = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);

    private Collider[] allColliders;
    private Rigidbody rb;
    private GameObject localCarrierPlayer;

    private void Awake()
    {
        allColliders = GetComponentsInChildren<Collider>();
        rb = GetComponent<Rigidbody>();
    }

    // ĐÃ XÓA LateUpdate() bám vị trí cũ theo yêu cầu[cite: 1]

    private void SetCollidersState(bool state)
    {
        if (allColliders == null) return;
        foreach (var col in allColliders)
        {
            if (col != null) col.enabled = state;
        }
    }

    private void SetRenderersState(bool state)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = state;
        }
    }

    // --- HÀM THỰC HIỆN NHẶT ---
    public void PerformPickup(ulong clientId)
    {
        SetCollidersState(false);
        SetRenderersState(false);
        
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false;

        // Gọi cơ chế ôm đồ của Script 5 áp dụng cho Tinh thể
        GameObject player = FindPlayerByClientId(clientId);
        if (player == null)
        {
            // Dự phòng offline
            player = FindAnyObjectByType<PlayerInteraction>()?.gameObject;
        }

        if (player != null)
        {
            localCarrierPlayer = player;
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null)
            {
                carrier.CarryCrystal(crystalID, true);
            }
        }
        
        // Kiểm tra an toàn trước khi gọi RPC tránh lỗi chưa bật NetworkManager khi test tại Scene
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer)
        {
            NotifyPickupClientRpc(clientId);
        }
    }

    // --- HÀM THỰC HIỆN THẢ ---
    public void PerformDrop()
    {
        GameObject player = localCarrierPlayer;
        ulong currentHolderId = holderId.Value;
        if (player == null && currentHolderId != ulong.MaxValue)
        {
            player = FindPlayerByClientId(currentHolderId);
        }

        if (player != null)
        {
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.DropCrystal();

            // Teleport original crystal to player position
            transform.position = player.transform.position;
            transform.rotation = player.transform.rotation;
        }

        localCarrierPlayer = null;

        transform.position += Vector3.up * 0.5f + transform.forward * 0.6f;
        SetCollidersState(true);
        SetRenderersState(true);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForce(transform.forward * 2f, ForceMode.Impulse);
        }

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer)
        {
            NotifyDropClientRpc(currentHolderId);
        }
    }

    // --- HÀM KHÓA VÀO TRẠM ---
    public void LockToStation()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer)
        {
            isSnapped.Value = true;
        }

        GameObject player = localCarrierPlayer;
        ulong currentHolderId = holderId.Value;
        if (player == null && currentHolderId != ulong.MaxValue)
        {
            player = FindPlayerByClientId(currentHolderId);
        }

        if (player != null)
        {
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.DropCrystal();
        }

        if (IsServer)
        {
            holderId.Value = ulong.MaxValue;
        }
        localCarrierPlayer = null;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        
        SetCollidersState(false); 
        SetRenderersState(true);

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer)
        {
            NotifyLockToStationClientRpc(currentHolderId);
        }
    }

    // --- HÀM BẮT ĐẦU BAY VÀO TRỤ (Đã xóa tính năng follow của script 2) ---
    public void StartSnappingToStation(Transform targetPoint)
    {
        LockToStation();
        // Xóa tính năng follow nên vật thể sẽ dịch chuyển tức thời vào bệ
        transform.position = targetPoint.position;
        transform.rotation = targetPoint.rotation;
        SetRenderersState(true);
    }

    private GameObject FindPlayerByClientId(ulong clientId)
    {
        // 1. Try server spawn manager (CHỈ CHO PHÉP SERVER CHẠY ĐOẠN NÀY)
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj != null) return playerObj.gameObject;
        }

        // 2. Client-side fallback: check PlayerInteraction scripts (CLIENT SẼ CHẠY XUỐNG ĐÂY)
        var players = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == clientId)
            {
                return player.gameObject;
            }
        }

        // 3. Fallback: check all components implementing IPlayerHUDTarget
        var hudTargets = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var target in hudTargets)
        {
            if (target is IPlayerHUDTarget)
            {
                var netObj = target.GetComponent<NetworkObject>();
                if (netObj != null && netObj.OwnerClientId == clientId)
                {
                    return target.gameObject;
                }
            }
        }
        return null;
    }

    [ClientRpc]
    private void NotifyPickupClientRpc(ulong clientId)
    {
        if (IsServer) return; 
        
        SetCollidersState(false);
        SetRenderersState(false);
        if (rb != null) rb.isKinematic = true;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false;

        GameObject player = FindPlayerByClientId(clientId);
        if (player != null)
        {
            localCarrierPlayer = player;
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.CarryCrystal(crystalID, true);
        }
    }

    [ClientRpc]
    private void NotifyDropClientRpc(ulong clientId)
    {
        if (IsServer) return;
        
        GameObject player = localCarrierPlayer;
        if (player == null) player = FindPlayerByClientId(clientId);

        if (player != null)
        {
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.DropCrystal();

            // Teleport original crystal to player position instantly on clients
            transform.position = player.transform.position;
            transform.rotation = player.transform.rotation;
        }
        
        localCarrierPlayer = null;

        transform.position += Vector3.up * 0.5f + transform.forward * 0.6f;

        SetCollidersState(true);
        SetRenderersState(true);
        if (rb != null) rb.isKinematic = false;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;
    }

    [ClientRpc]
    private void NotifyLockToStationClientRpc(ulong clientId)
    {
        if (IsServer) return;
        
        GameObject player = localCarrierPlayer;
        if (player == null) player = FindPlayerByClientId(clientId);

        if (player != null)
        {
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.DropCrystal();

            var pInt = player.GetComponent<PlayerInteraction>();
            if (pInt != null && pInt.currentInteractBox != null && pInt.currentInteractBox.crystalSnapPoint != null)
            {
                transform.position = pInt.currentInteractBox.crystalSnapPoint.position;
                transform.rotation = pInt.currentInteractBox.crystalSnapPoint.rotation;
            }
        }

        localCarrierPlayer = null;

        if (rb != null) rb.isKinematic = true;
        SetCollidersState(false);
        SetRenderersState(true);

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;
    }
}