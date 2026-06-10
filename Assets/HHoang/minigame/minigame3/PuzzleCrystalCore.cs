using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PuzzleCrystalCore : NetworkBehaviour
{
    [Header("Cấu hình Hiển thị & Vật lý")]
    [Range(0.1f, 1.0f)]
    public float holdScaleMultiplier = 0.3f; 
    public float throwForce = 15f; 
    public GameObject explosionEffectPrefab;
    
    public NetworkVariable<bool> isSnapped = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> holderId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> isSnapping = new NetworkVariable<bool>(false);

    private Vector3 spawnPosition; 
    private Rigidbody rb;
    private Vector3 originalScale;

    [Header("Cấu hình Cơ chế Giải đố")]
    // Lưu lịch sử chạm để xử lý luật "Một Lần Chạm"
    public List<ulong> historyHolders = new List<ulong>();
    private float burnTimer = 0f;
    private bool isBurning = false;

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

        if (float.IsNaN(transform.position.x)) { ResetToSpawnPosition(); return; }

        // --- QUY TẮC "THỜI GIAN ĐỐT CHÁY" (11 giây nổ) ---
        if (isBurning)
        {
            burnTimer += Time.fixedDeltaTime;
            if (burnTimer >= 11f) 
            {
                ExplodeCore();
                return;
            }
        }

        float flySpeed = 5f; 
        Vector3 targetScale = (holderId.Value != ulong.MaxValue) ? originalScale * holdScaleMultiplier : originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, flySpeed * Time.fixedDeltaTime);

        if (holderId.Value == ulong.MaxValue && !isSnapped.Value && !isSnapping.Value)
        {
            if (rb.isKinematic) 
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            return; 
        }

        if (isSnapped.Value || isSnapping.Value) return;

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

    // --- KIỂM TRA QUY TẮC "MỘT LẦN CHẠM" ---
    public bool CanPickup(ulong playerId)
    {
        if (!IsServer) return false;
        return !historyHolders.Contains(playerId);
    }

    // --- HIỆU ỨNG ĐẨY LÙI KHI BỊ TỪ CHỐI ---
    public void RepelPlayer(ulong playerId)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client) && client.PlayerObject != null)
        {
            Vector3 pushDirection = (client.PlayerObject.transform.position - transform.position).normalized;
            pushDirection.y = 0.2f; 

            ClientRpcParams rpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { playerId } } };
            RepelClientRpc(pushDirection, rpcParams);
        }
    }

    [ClientRpc]
    private void RepelClientRpc(Vector3 direction, ClientRpcParams rpcParams = default)
    {
        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (localPlayer != null && localPlayer.TryGetComponent<Rigidbody>(out var playerRb))
        {
            playerRb.isKinematic = false;
            playerRb.AddForce(direction * 18f, ForceMode.Impulse);
        }
    }

    public void PerformPickup(ulong playerId)
    {
        if (!IsServer) return;
        
        // Ghi danh vào lịch sử chạm & kích hoạt bom nổ chậm
        if (!historyHolders.Contains(playerId)) historyHolders.Add(playerId);
        isBurning = true;
        burnTimer = 0f;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false; 

        GetComponent<NetworkObject>().ChangeOwnership(playerId);
        holderId.Value = playerId; 

        rb.isKinematic = true; 
        rb.useGravity = false;
    }

    // --- CƠ CHẾ SINH TỒN: NÉM (PHÍM E) ---
    public void PerformThrow(Vector3 throwDirection)
    {
        if (!IsServer) return;
        
        isBurning = false; // Rời tay là dừng đếm nổ
        burnTimer = 0f;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true; 

        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        holderId.Value = ulong.MaxValue; 

        rb.isKinematic = false;
        rb.useGravity = true;

        // Áp dụng lực ném
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(throwDirection.normalized * throwForce, ForceMode.Impulse);

        CheckBottomOutRule();
    }

    public void PerformDrop()
    {
        if (!IsServer) return;
        isSnapping.Value = false;
        isBurning = false;
        burnTimer = 0f;
        
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true; 
        
        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        holderId.Value = ulong.MaxValue; 
        
        rb.isKinematic = false;
        rb.useGravity = true;

        CheckBottomOutRule();
    }

    // --- QUY TẮC "CHẠM ĐÁY" ---
    private void CheckBottomOutRule()
    {
        // 4 người đã chạm + đang rơi tự do ngoài trung tâm -> Nổ
        if (historyHolders.Count >= 4 && !isSnapped.Value && !isSnapping.Value)
        {
            ExplodeCore();
        }
    }

    private void ExplodeCore()
    {
        if (!IsServer) return;

        ExplodeVisualClientRpc();

        isBurning = false;
        burnTimer = 0f;

        // Ép thả nếu có người đang cố ôm
        if (holderId.Value != ulong.MaxValue && NetworkManager.Singleton.ConnectedClients.TryGetValue(holderId.Value, out var client))
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt))
            {
                pInt.ForceDropFromStation();
            }
        }

        // Reset toàn bộ câu đố
        historyHolders.Clear();
        ResetToSpawnPosition();
    }

    [ClientRpc]
    private void ExplodeVisualClientRpc()
    {
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
    }

    // --- CÁC HÀM CẮM TRỤ CƠ BẢN ---
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
            t += Time.deltaTime * 3f;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }
        isSnapping.Value = false;
    }

    public void LockToStation()
    {
        if (IsServer)
        {
            isBurning = false; // Cắm vào bệ an toàn -> Tắt nổ
            burnTimer = 0f;
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
        isBurning = false;
        burnTimer = 0f;
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