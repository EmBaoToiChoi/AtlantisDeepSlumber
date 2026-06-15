using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class CrystalCore : NetworkBehaviour
{
    [Header("Cấu hình hiển thị")]
    [Range(0.1f, 1.0f)]
    public float holdScaleMultiplier = 0.3f; 
    public int crystalID;
    
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);
    
    // Thêm biến này để quản lý trạng thái đang bay vào trụ
    public NetworkVariable<bool> isSnapping = new NetworkVariable<bool>(false);

    private Vector3 spawnPosition; 
    private Rigidbody rb;
    private Vector3 originalScale;
    
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

        // 1. Chống lỗi tọa độ
        if (float.IsNaN(transform.position.x)) { ResetToSpawnPosition(); return; }

        // 2. Nội suy kích thước
        float flySpeed = 5f; 
        Vector3 targetScale = (holderId.Value != ulong.MaxValue) ? originalScale * holdScaleMultiplier : originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, flySpeed * Time.fixedDeltaTime);

        // --- ĐÂY LÀ ĐOẠN SỬA MỚI ---
        // Nếu viên ngọc KHÔNG có chủ VÀ KHÔNG bị khóa/bay vào trụ
        if (holderId.Value == ulong.MaxValue && !isSnapped.Value && !isSnapping.Value)
        {
            // Ép nó bật vật lý để nó rơi tự do hoặc văng ra theo lực đẩy
            if (rb.isKinematic) 
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            return; // Chỉ trả về, không chạy logic bay vào tay nữa
        }

        // Nếu nó đang bị khóa hoặc bay vào trụ thì cũng dừng lại, không cần tính toán gì thêm
        if (isSnapped.Value || isSnapping.Value) return;

        // 3. Logic bay vào tay (Chỉ chạy khi có holderId)
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
    }

    // GỌI HÀM NÀY TỪ PILLARSTATION ĐỂ BẮT ĐẦU BAY VÀO TRỤ
    public void StartSnappingToStation(Transform target)
    {
        if (IsServer)
        {
            isSnapping.Value = true;
            StartCoroutine(SnapLerpRoutine(target.position, target.rotation));
        }
    }

    private IEnumerator SnapLerpRoutine(Vector3 targetPos, Quaternion targetRot)
    {
        float t = 0;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        
        while (t < 1)
        {
            t += Time.deltaTime * 3f; // Tốc độ bay (chỉnh 3f để nhanh/chậm)
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }
        isSnapping.Value = false;
    }

    public void PerformPickup(ulong playerId)
    {
        if (!IsServer) return;
        
        // 1. Tắt va chạm của viên ngọc
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false; 

        // 2. Thiết lập quyền sở hữu
        GetComponent<NetworkObject>().ChangeOwnership(playerId);
        holderId.Value = playerId; 

        // 3. Tắt vật lý để không bị "đẩy" nhân vật
        rb.isKinematic = true; 
        rb.useGravity = false;
    }

    public void PerformDrop()
    {
        if (!IsServer) return;
        isSnapping.Value = false;
        
        // Bật lại Collider
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true; 
        
        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        holderId.Value = ulong.MaxValue; 
        
        // Bật lại vật lý để nó rơi xuống đất
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    public void LockToStation()
    {
        if (IsServer)
        {
            isSnapping.Value = false;
            isSnapped.Value = true;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            holderId.Value = ulong.MaxValue; 
            rb.isKinematic = true; 
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero; 
            rb.angularVelocity = Vector3.zero; 
        }
    }

    public void ResetToSpawnPosition()
    {
        if (!IsServer) return;
        isSnapping.Value = false;
        holderId.Value = ulong.MaxValue;
        isSnapped.Value = false;
        transform.position = spawnPosition;
        rb.linearVelocity = Vector3.zero; 
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; 
        rb.useGravity = true;
    }
}