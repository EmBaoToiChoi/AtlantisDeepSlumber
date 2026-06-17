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
        holderId.OnValueChanged += OnHolderIdChanged;
        if (holderId.Value != ulong.MaxValue)
        {
            OnHolderIdChanged(ulong.MaxValue, holderId.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        holderId.OnValueChanged -= OnHolderIdChanged;
    }

    private void OnHolderIdChanged(ulong oldVal, ulong newVal)
    {
        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        var col = GetComponent<Collider>();
        
        if (newVal != ulong.MaxValue)
        {
            // Tắt NetworkTransform và Collider khi đang được nhặt để tránh tranh chấp tọa độ/vật lý
            if (netTransform != null) netTransform.enabled = false;
            if (col != null) col.enabled = false;
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            // Bật lại NetworkTransform và Collider khi được thả ra
            if (netTransform != null) netTransform.enabled = true;
            if (col != null) col.enabled = !isSnapped.Value;
        }
    }

    void FixedUpdate() 
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;

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

        // 3. Logic bay vào tay trên Server/Standalone (Chỉ chạy khi có holderId)
        if (holderId.Value != ulong.MaxValue)
        {
            GameObject player = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(holderId.Value, out var client) && client.PlayerObject != null)
                {
                    player = client.PlayerObject.gameObject;
                }
            }
            
            if (player != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                
                // Di chuyển rigidbody trên Server tới vị trí ngang tay/bụng của player (như thanh gỗ)
                Vector3 targetPos = player.transform.TransformPoint(new Vector3(0f, 0.95f, 0.42f));
                Quaternion targetRot = player.transform.rotation;
                
                rb.MovePosition(Vector3.Lerp(transform.position, targetPos, flySpeed * Time.fixedDeltaTime));
                rb.MoveRotation(Quaternion.Lerp(transform.rotation, targetRot, flySpeed * Time.fixedDeltaTime));
                return; 
            }
            
            if (!isStandalone)
            {
                holderId.Value = ulong.MaxValue;
            }
        }
    }

    void LateUpdate()
    {
        // Cập nhật tọa độ tức thì trên tất cả Client/Standalone để triệt tiêu độ trễ (Zero lag local positioning)
        if (holderId.Value != ulong.MaxValue)
        {
            GameObject player = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(holderId.Value, out var playerNetObj))
                {
                    player = playerNetObj.gameObject;
                }
            }
            else
            {
                // Offline test mode: tìm player gần nhất đang cầm ngọc
                var players = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p.isCarryingCore.Value)
                    {
                        player = p.gameObject;
                        break;
                    }
                }
            }

            if (player != null)
            {
                // Tắt NetworkTransform và Collider thủ công ở offline mode
                var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
                var col = GetComponent<Collider>();
                if (netTransform != null && netTransform.enabled) netTransform.enabled = false;
                if (col != null && col.enabled) col.enabled = false;
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                // Khớp chính xác với vị trí bưng thanh gỗ (offset từ root player)
                transform.position = player.transform.TransformPoint(new Vector3(0f, 0.95f, 0.42f));
                transform.rotation = player.transform.rotation;
                
                // Nội suy scale mượt mà trên client
                float lerpSpeed = 10f;
                Vector3 targetScale = originalScale * holdScaleMultiplier;
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, lerpSpeed * Time.deltaTime);
            }
        }
    }

    // GỌI HÀM NÀY TỪ PILLARSTATION ĐỂ BẮT ĐẦU BAY VÀO TRỤ
    public void StartSnappingToStation(Transform target)
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (IsServer || isStandalone)
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
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;
        
        // 1. Tắt va chạm của viên ngọc
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false; 

        // 2. Thiết lập quyền sở hữu
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.ChangeOwnership(playerId);
        }
        holderId.Value = playerId; 

        // 3. Tắt vật lý để không bị "đẩy" nhân vật
        rb.isKinematic = true; 
        rb.useGravity = false;
    }

    public void PerformDrop()
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;
        isSnapping.Value = false;
        
        // Bật lại Collider
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true; 
        
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        }
        holderId.Value = ulong.MaxValue; 
        
        // Bật lại vật lý để nó rơi xuống đất
        rb.isKinematic = false;
        rb.useGravity = true;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;
    }

    public void LockToStation()
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (IsServer || isStandalone)
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
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;
        isSnapping.Value = false;
        holderId.Value = ulong.MaxValue;
        isSnapped.Value = false;
        transform.position = spawnPosition;
        rb.linearVelocity = Vector3.zero; 
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; 
    }
}