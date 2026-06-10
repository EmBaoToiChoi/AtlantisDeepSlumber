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
    }

    void FixedUpdate() 
    {
        if (!IsServer) return;

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
            playerRb.AddForce(direction * 18f, ForceMode.Impulse);
        }
    }

    public void PerformPickup(ulong playerId)
    {
        if (!IsServer) return;
        
        if (!historyHolders.Contains(playerId)) historyHolders.Add(playerId);
        
        isBurning = true;
        burnTimer = burnTimeLimit; 

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false; 

        GetComponent<NetworkObject>().ChangeOwnership(playerId);
        holderId.Value = playerId; 

        rb.isKinematic = true; 
        rb.useGravity = false;
    }

    public void PerformThrow(Vector3 throwDirection)
    {
        if (!IsServer) return;
        
        isBurning = false; 
        burnTimer = 0f;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true; 

        var netObj = GetComponent<NetworkObject>();
        if (netObj.OwnerClientId != NetworkManager.ServerClientId) netObj.RemoveOwnership();
        holderId.Value = ulong.MaxValue; 

        rb.isKinematic = false;
        rb.useGravity = true;

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

    private void CheckBottomOutRule()
    {
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

        if (holderId.Value != ulong.MaxValue && NetworkManager.Singleton.ConnectedClients.TryGetValue(holderId.Value, out var client))
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerInteraction>(out var pInt))
            {
                pInt.ForceDropFromStation();
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
            isBurning = false; 
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
        
        // Đưa về vị trí gốc
        transform.position = spawnPosition;

        // --- ĐÂY, BẬT LẠI COLLIDER CHỐNG RỚT XUYÊN MAP ---
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true; 
        // ------------------------------------------------

        // Reset vật lý
        rb.linearVelocity = Vector3.zero; 
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; 
        rb.useGravity = true;
    }
}