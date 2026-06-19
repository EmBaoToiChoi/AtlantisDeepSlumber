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
            if (col != null) 
            {
                col.enabled = false;
                col.isTrigger = true; // Chuyển thành Trigger phòng hờ va chạm
            }
            if (rb != null)
            {
                rb.detectCollisions = false; // Tắt hoàn toàn va chạm vật lý
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            AttachToCarrier(newVal);
        }
        else
        {
            DetachFromCarrier();

            // Bật lại NetworkTransform và Collider khi được thả ra
            if (netTransform != null) netTransform.enabled = true;
            if (col != null) 
            {
                col.enabled = !isSnapped.Value;
                col.isTrigger = false;
            }
            if (rb != null)
            {
                rb.detectCollisions = true; // Bật lại va chạm vật lý
            }
        }
    }

    private void AttachToCarrier(ulong clientId)
    {
        GameObject player = null;
        
        // 1. Tìm nhân vật đang nhặt ngọc
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.IsPlayerObject && netObj.OwnerClientId == clientId)
                {
                    player = netObj.gameObject;
                    break;
                }
            }
        }
        else
        {
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

        // 2. Gắn viên ngọc vào tay nhân vật
        if (player != null)
        {
            // QUAN TRỌNG: Quét tìm script PlayerInteraction ở cả Object cha lẫn Object con
            var pInt = player.GetComponentInChildren<PlayerInteraction>();
            if (pInt == null) pInt = player.GetComponentInParent<PlayerInteraction>();

            // Lấy cái Box "Hold Point" mà bạn đã gắn
            Transform targetParent = (pInt != null && pInt.holdPoint != null) ? pInt.holdPoint : player.transform;
            
            transform.SetParent(targetParent, false);

            if (targetParent != player.transform)
            {
                // Nếu đã tìm thấy Box Hold Point -> Ép tọa độ viên ngọc về đúng tâm (0,0,0) của cái Box
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Nếu quên gắn Box trong Unity thì nó mới nằm lơ lửng ở ngực
                transform.localPosition = new Vector3(0f, 0.95f, 0.42f);
                transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
        }
    }
    private void DetachFromCarrier()
    {
        transform.SetParent(null, true);
    }

    void FixedUpdate() 
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;

        // 1. Chống lỗi tọa độ
        if (float.IsNaN(transform.position.x)) { ResetToSpawnPosition(); return; }

        // 2. Nội suy kích thước và bù trừ tỷ lệ co giãn của cha
        float flySpeed = 5f; 
        Vector3 targetScale = (holderId.Value != ulong.MaxValue) ? originalScale * holdScaleMultiplier : originalScale;
        
        if (holderId.Value != ulong.MaxValue && transform.parent != null)
        {
            Vector3 parentLossyScale = transform.parent.lossyScale;
            Vector3 targetCompensated = new Vector3(
                targetScale.x / (parentLossyScale.x != 0 ? parentLossyScale.x : 1f),
                targetScale.y / (parentLossyScale.y != 0 ? parentLossyScale.y : 1f),
                targetScale.z / (parentLossyScale.z != 0 ? parentLossyScale.z : 1f)
            );
            transform.localScale = Vector3.Lerp(transform.localScale, targetCompensated, flySpeed * Time.fixedDeltaTime);
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, flySpeed * Time.fixedDeltaTime);
        }

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
            return; 
        }

        // Nếu nó đang bị khóa hoặc bay vào trụ thì cũng dừng lại, không cần tính toán gì thêm
        if (isSnapped.Value || isSnapping.Value) return;
    }

    void LateUpdate()
    {
        // Tự phục hồi parent nếu có chủ nhưng bị mất liên kết parent
        if (holderId.Value != ulong.MaxValue && transform.parent == null)
        {
            AttachToCarrier(holderId.Value);
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
            DetachFromCarrier();
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
        DetachFromCarrier();
        isSnapping.Value = false;
        holderId.Value = ulong.MaxValue;
        isSnapped.Value = false;
        transform.position = spawnPosition;
        rb.linearVelocity = Vector3.zero; 
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; 
    }
}