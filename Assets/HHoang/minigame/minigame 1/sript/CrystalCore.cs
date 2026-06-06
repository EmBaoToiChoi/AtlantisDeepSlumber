using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class CrystalCore : NetworkBehaviour
{
    [Header("Cấu hình hiển thị")]
    [Range(0.1f, 1.0f)]
    public float holdScaleMultiplier = 0.3f; 
    public int crystalID;
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);

    private Vector3 spawnPosition; 
    private Rigidbody rb;
    private Vector3 originalScale;
    
    // Biến tạm để chặn reset khi vừa văng ngọc
    private float ejectTimer = 0f;

    void Awake() 
    { 
        rb = GetComponent<Rigidbody>();
        originalScale = transform.localScale; 
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) spawnPosition = transform.position; 
    }

    void FixedUpdate() 
    {
        if (!IsServer) return;

        // Giảm timer chặn reset
        if (!isSnapped.Value)
        {
            // Nếu ngọc bay xa quá 80 đơn vị HOẶC rơi sâu xuống dưới 10 đơn vị so với spawn
            if (Vector3.Distance(transform.position, spawnPosition) > 90f || transform.position.y < spawnPosition.y - 10f)
            {
                ResetToSpawnPosition();
                return; 
            }
        }

        // 2. Nội suy kích thước
        float flySpeed = 5f; 
        Vector3 targetScale = (holderId.Value != ulong.MaxValue) ? originalScale * holdScaleMultiplier : originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, flySpeed * Time.fixedDeltaTime);

        if (isSnapped.Value) return;

        // 3. Logic bay vào tay
        if (IsSpawned && holderId.Value != ulong.MaxValue)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(holderId.Value, out var client) && client.PlayerObject != null)
            {
                if (client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.holdPoint != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.MovePosition(Vector3.Lerp(transform.position, pInt.holdPoint.position, flySpeed * Time.fixedDeltaTime));
                    rb.MoveRotation(Quaternion.Lerp(transform.rotation, pInt.holdPoint.rotation, flySpeed * Time.fixedDeltaTime));
                    return; 
                }
            }
            holderId.Value = ulong.MaxValue;
        }
        else if (rb.isKinematic) 
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    // GỌI HÀM NÀY TỪ ASCENSIONMANAGER KHI VĂNG NGỌC
    public void NotifyEjection()
    {
        if (IsServer) ejectTimer = 2.0f; // Chặn reset trong 2 giây
    }

    public void PerformPickup(ulong playerId)
    {
        if (!IsServer) return;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false; 
        GetComponent<NetworkObject>().ChangeOwnership(playerId);
        holderId.Value = playerId; 
    }

    public void PerformDrop()
    {
        if (!IsServer) return;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        holderId.Value = ulong.MaxValue; 
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void LockToStation()
    {
        if (IsServer)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
            isSnapped.Value = true;
            holderId.Value = ulong.MaxValue; 
            rb.isKinematic = true; rb.useGravity = false;
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; 
        }
    }

    public void ResetToSpawnPosition()
    {
        if (!IsServer) return;
        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        holderId.Value = ulong.MaxValue;
        isSnapped.Value = false;
        transform.position = spawnPosition;
        rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; rb.useGravity = true;
    }
}