using UnityEngine;
using Unity.Netcode;

public class CrystalCore : NetworkBehaviour
{
    public int crystalID; 
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> isSnapping = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);

    private Collider[] allColliders;
    private Rigidbody rb;
    private GameObject localCarrierPlayer;
    private Transform currentHoldPoint; 

    private void Awake()
    {
        allColliders = GetComponentsInChildren<Collider>();
        rb = GetComponent<Rigidbody>();
    }

    private void LateUpdate()
    {
        if (currentHoldPoint != null)
        {
            transform.position = currentHoldPoint.position;
            transform.rotation = currentHoldPoint.rotation;
        }
    }

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

    public void PerformPickup(ulong clientId)
    {
        SetCollidersState(false); 
        
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false; 

        GameObject player = FindPlayerByClientId(clientId);
        if (player == null) player = FindAnyObjectByType<PlayerInteraction>()?.gameObject;

        if (player != null)
        {
            localCarrierPlayer = player;
            
            // CHIÊU MỚI: Bật cờ isCarrying để đánh lừa Animator giơ tay lên, 
            // KHÔNG dùng hàm CarryCrystal() nữa để tránh bị hiện ngọc giả!
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.isCarrying = true;

            var pInt = player.GetComponent<PlayerInteraction>();
            if (pInt != null && pInt.holdPoint != null)
            {
                currentHoldPoint = pInt.holdPoint;
            }
        }
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer)
        {
            NotifyPickupClientRpc(clientId);
        }
    }

    public void PerformDrop()
    {
        GameObject player = localCarrierPlayer;
        ulong currentHolderId = holderId.Value;
        if (player == null && currentHolderId != ulong.MaxValue)
        {
            player = FindPlayerByClientId(currentHolderId);
        }

        currentHoldPoint = null; 

        if (player != null)
        {
            // Tắt cờ isCarrying để nhân vật thả tay xuống
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.isCarrying = false;

            transform.position = player.transform.position + Vector3.up * 0.5f + player.transform.forward * 0.6f;
        }

        localCarrierPlayer = null;
        SetCollidersState(true); 

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

        currentHoldPoint = null; 

        if (player != null)
        {
            // Tắt cờ hạ tay
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.isCarrying = false;
        }

        if (IsServer) holderId.Value = ulong.MaxValue;
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

    public void StartSnappingToStation(Transform targetPoint)
    {
        LockToStation();
        transform.position = targetPoint.position;
        transform.rotation = targetPoint.rotation;
        SetRenderersState(true);
    }

    private GameObject FindPlayerByClientId(ulong clientId)
    {
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj != null) return playerObj.gameObject;
        }

        var players = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == clientId) return player.gameObject;
        }

        var hudTargets = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var target in hudTargets)
        {
            if (target is IPlayerHUDTarget)
            {
                var netObj = target.GetComponent<NetworkObject>();
                if (netObj != null && netObj.OwnerClientId == clientId) return target.gameObject;
            }
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
            localCarrierPlayer = player;
            
            // Hack cờ giơ tay cho Client
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.isCarrying = true;

            var pInt = player.GetComponent<PlayerInteraction>();
            if (pInt != null && pInt.holdPoint != null)
            {
                currentHoldPoint = pInt.holdPoint;
            }
        }
    }

    [ClientRpc]
    private void NotifyDropClientRpc(ulong clientId)
    {
        if (IsServer) return;
        
        GameObject player = localCarrierPlayer;
        if (player == null) player = FindPlayerByClientId(clientId);

        currentHoldPoint = null; 

        if (player != null)
        {
            // Tắt cờ hạ tay cho Client
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.isCarrying = false;

            transform.position = player.transform.position + Vector3.up * 0.5f + player.transform.forward * 0.6f;
        }
        
        localCarrierPlayer = null;

        SetCollidersState(true);
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

        currentHoldPoint = null;

        if (player != null)
        {
            // Tắt cờ hạ tay cho Client
            var carrier = player.GetComponent<PlayerLogCarrier>();
            if (carrier != null) carrier.isCarrying = false;

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