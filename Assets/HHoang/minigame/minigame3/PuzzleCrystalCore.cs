using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PuzzleCrystalCore : NetworkBehaviour // <--- SỬA LẠI THÀNH NETWORKBEHAVIOUR
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
    public List<ulong> historyHolders = new List<ulong>();
    
    public float burnTimeLimit = 11f; 

    [SerializeField] private float burnTimer = 0f;
    [SerializeField] private bool isBurning = false;

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
        }
        else
        {
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

    void FixedUpdate() 
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;

        if (float.IsNaN(transform.position.x)) { ResetToSpawnPosition(); return; }

        // --- CƠ CHẾ ĐẾM NGƯỢC TỪ 11 VỀ 0 ---
        if (isBurning)
        {
            burnTimer -= Time.fixedDeltaTime; 
            if (burnTimer <= 0f) 
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

    public bool CanPickup(ulong playerId)
    {
        if (!IsServer) return false;
        return !historyHolders.Contains(playerId);
    }

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
            
            // 1. Tính toán hướng ngang trước (bỏ qua độ cao hiện tại)
            Vector3 pushDirection = direction;
            pushDirection.y = 0; 
            pushDirection = pushDirection.normalized; // Chuẩn hóa hướng ngang

            // 2. Ép cứng một lực hất bổng cố định tạo hình vòng cung (Góc tầm 45 độ)
            pushDirection.y = 0.5f; 

            // 3. Tống lực. Dùng mức 10f hoặc 15f là cực kỳ an toàn!
            playerRb.AddForce(pushDirection.normalized * 15f, ForceMode.Impulse); 

            if (localPlayer.TryGetComponent<MovementController>(out var mover))
            {
                StartCoroutine(StunPlayerRoutine(mover, 0.5f)); 
            }
        }
    }

    // Coroutine làm choáng nhân vật
    private IEnumerator StunPlayerRoutine(MovementController mover, float stunDuration)
    {
        mover.ToggleMovement(false); // Ngắt điều khiển
        yield return new WaitForSeconds(stunDuration); // Chờ nhân vật văng ra xa
        mover.ToggleMovement(true); // Bật lại điều khiển bình thường
    }

    public void PerformPickup(ulong playerId)
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;
        
        if (!historyHolders.Contains(playerId)) historyHolders.Add(playerId);
        
        isBurning = true;
        burnTimer = burnTimeLimit; 

        var col = GetComponent<Collider>();
        if (col != null) 
        {
            col.enabled = false;
            col.isTrigger = true;
        }

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.ChangeOwnership(playerId);
        }
        holderId.Value = playerId; 

        rb.detectCollisions = false;
        rb.isKinematic = true; 
        rb.useGravity = false;
    }

    public void PerformThrow(Vector3 throwDirection)
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;
        
        isBurning = false; 
        burnTimer = 0f;

        var col = GetComponent<Collider>();
        if (col != null) 
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        }
        holderId.Value = ulong.MaxValue; 

        rb.detectCollisions = true;
        rb.isKinematic = false;
        rb.useGravity = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(throwDirection.normalized * throwForce, ForceMode.Impulse);

        CheckBottomOutRule();
    }

    public void PerformDrop()
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;
        isSnapping.Value = false;
        isBurning = false;
        burnTimer = 0f;
        
        var col = GetComponent<Collider>();
        if (col != null) 
        {
            col.enabled = true;
            col.isTrigger = false;
        }
        
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        }
        holderId.Value = ulong.MaxValue; 
        
        rb.detectCollisions = true;
        rb.isKinematic = false;
        rb.useGravity = true;

        var netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = true;

        CheckBottomOutRule();
    }

    private void CheckBottomOutRule()
    {
        if (historyHolders.Count >= 4 && !isSnapped.Value && !isSnapping.Value)
        {
            ExplodeCore();
        }
    }

    private void ExplodeCore()
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (!IsServer && !isStandalone) return;

        if (!isStandalone)
        {
            ExplodeVisualClientRpc();
        }
        else
        {
            if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        isBurning = false;
        burnTimer = 0f;

        if (holderId.Value != ulong.MaxValue)
        {
            if (!isStandalone)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(holderId.Value, out var client) && client.PlayerObject != null)
                {
                    if (client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt))
                    {
                        pInt.ForceDropFromStation();
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
                        p.ForceDropFromStation();
                    }
                }
            }
        }

        historyHolders.Clear();
        ResetToSpawnPosition();
    }

    [ClientRpc]
    private void ExplodeVisualClientRpc()
    {
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
    }

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
            t += Time.deltaTime * 3f;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }
        isSnapping.Value = false;
    }

    public void LockToStation()
    {
        bool isStandalone = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (IsServer || isStandalone)
        {
            isBurning = false; 
            burnTimer = 0f;
            isSnapping.Value = false;
            isSnapped.Value = true;
            var col = GetComponent<Collider>();
            if (col != null) 
            {
                col.enabled = false;
                col.isTrigger = true;
            }
            holderId.Value = ulong.MaxValue; 
            rb.detectCollisions = false;
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
        isBurning = false;
        burnTimer = 0f;
        isSnapping.Value = false;
        holderId.Value = ulong.MaxValue;
        isSnapped.Value = false;
        
        // Đưa về vị trí gốc
        transform.position = spawnPosition;

        // --- ĐÂY, BẬT LẠI COLLIDER CHỐNG RỚT XUYÊN MAP ---
        var col = GetComponent<Collider>();
        if (col != null) 
        {
            col.enabled = true; 
            col.isTrigger = false;
        }
        // ------------------------------------------------

        // Reset vật lý
        rb.detectCollisions = true;
        rb.linearVelocity = Vector3.zero; 
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; 
        rb.useGravity = true;
    }
}