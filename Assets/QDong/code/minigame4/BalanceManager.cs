using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class BalanceManager : NetworkBehaviour
{
    [Header("Cài đặt Độ nhạy nghiêng")]
    [Tooltip("Chỉ số càng cao thì khi ra rìa đĩa càng nghiêng dốc. Thử tăng lên 20-30 nếu đĩa nặng.")]
    public float tiltSensitivity = 15f; 

    [Header("Cấu hình Rigidbody của Đĩa")]
    public Rigidbody diskRigidbody;

    public float CurrentAngle { get; private set; }

    private NetworkVariable<Quaternion> targetRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private HashSet<Collider> playersOnBoard = new HashSet<Collider>();
    public bool puzzleLocked = false;
    private float initialYaw = 0f;

    public override void OnNetworkSpawn()
    {
        if (diskRigidbody == null)
        {
            diskRigidbody = GetComponent<Rigidbody>();
        }

        if (diskRigidbody != null)
        {
            // KHI DÙNG MOVEROTATION: Bắt buộc đĩa phải là Kinematic để script tự điều khiển góc, 
            // tránh bị trọng lực mặc định của Unity ghì chặt đĩa lại không cho xoay.
            diskRigidbody.isKinematic = true; 
            diskRigidbody.useGravity = false;
            
            // LƯU LẠI GÓC XOAY Y BAN ĐẦU ĐỂ KHÔNG BỊ TỰ ĐỘNG QUAY VỀ 0
            initialYaw = diskRigidbody.transform.eulerAngles.y;
        }
        else
        {
            Debug.LogError($"[LỖI NGHIÊNG ĐĨA] Chưa kéo thả Rigidbody của chiếc đĩa vào BalanceManager trên {gameObject.name}!");
        }

        // Tự động nới rộng vùng Trigger để tránh lỗi nhân vật trượt ra mép bị rớt khỏi Trigger
        // Nếu thoát khỏi Trigger quá sớm, đĩa sẽ mất trọng lượng, bật ngược lên và hất văng nhân vật.
        Collider[] cols = GetComponents<Collider>();
        foreach (var col in cols)
        {
            if (col.isTrigger)
            {
                if (col is BoxCollider box)
                {
                    box.size = new Vector3(box.size.x * 1.3f, box.size.y + 10f, box.size.z * 1.3f);
                }
                else if (col is SphereCollider sphere)
                {
                    sphere.radius *= 1.3f;
                }
                else if (col is CapsuleCollider cap)
                {
                    cap.radius *= 1.3f;
                    cap.height += 10f;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playersOnBoard.Add(other);
            if (IsServer) 
            {
                Debug.Log($"<color=green>[ĐĨA NGHIÊNG]</color> Phát hiện nhân vật {other.name} ĐẠT CHÂN lên đĩa. Số người hiện tại: {playersOnBoard.Count}");
                
                // Guard: Chỉ gọi RPC khi NetworkManager đã sẵn sàng
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
                
                Puzzle4Manager p4Manager = FindAnyObjectByType<Puzzle4Manager>();
                if (p4Manager != null && p4Manager.IsSpawned && p4Manager.isMinigameStarted.Value && !p4Manager.puzzleCompleted.Value)
                {
                    NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
                    if (netObj != null)
                    {
                        ClientRpcParams rpcParams = new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { netObj.OwnerClientId } }
                        };
                        p4Manager.ToggleSharedCameraClientRpc(true, rpcParams);
                    }
                }
            }
        }
        else if (IsServer)
        {
            // Log này giúp bạn check xem có phải bạn quên chưa đổi Tag của Player không
            Debug.Log($"[ĐĨA NGHIÊNG] Có vật thể chạm vào nhưng bị bỏ qua vì không phải Tag 'Player': {other.name} (Tag hiện tại: {other.tag})");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playersOnBoard.Remove(other);
            if (IsServer) Debug.Log($"<color=red>[ĐĨA NGHIÊNG]</color> Nhân vật {other.name} RỜI KHỎI đĩa. Số người còn lại: {playersOnBoard.Count}");
        }
    }

    public bool IsPlayerOnBoard(Collider playerCollider)
    {
        return playersOnBoard.Contains(playerCollider);
    }

    void FixedUpdate()
    {
        if (puzzleLocked || diskRigidbody == null)
            return;

        // Xóa các player null (ví dụ khi disconnect) trên cả Server và Client
        playersOnBoard.RemoveWhere(p => p == null);

        float tiltX = 0f;
        float tiltZ = 0f;

        foreach (var player in playersOnBoard)
        {
            // Tính vị trí tương đối (Local Position) từ Player tới tâm đĩa
            Vector3 localPos = diskRigidbody.transform.InverseTransformPoint(player.transform.position);
            
            // Nếu người chơi rớt xuống dưới mặt đĩa thì không tính trọng lượng để tránh đĩa bị nghiêng theo
            if (localPos.y < -0.5f)
                continue;

            float weight = GetPlayerWeight(player);

            tiltX += localPos.z * weight;
            tiltZ += localPos.x * weight;
        }

        // SỬA LỖI TỰ ĐỘNG XOAY VỀ 0 Ở KHÚC ĐẦU GAME:
        // Cần giữ nguyên góc xoay Y ban đầu của đĩa (initialYaw), nếu không mâm sẽ tự động xoay lết về 0.
        Quaternion baseRotation = Quaternion.Euler(0, initialYaw, 0);
        
        float finalTiltX = tiltX * tiltSensitivity;
        float finalTiltZ = -tiltZ * tiltSensitivity;
        
        Vector3 tiltVector = new Vector3(finalTiltX, 0f, finalTiltZ);
        float tiltAngle = tiltVector.magnitude;
        
        Quaternion tiltRotation = Quaternion.identity;
        if (tiltAngle > 0.001f)
        {
            Vector3 rotationAxis = tiltVector.normalized;
            tiltRotation = Quaternion.AngleAxis(tiltAngle, rotationAxis);
        }
        
        // Kết hợp góc xoay ban đầu và góc nghiêng
        Quaternion desiredRotation = baseRotation * tiltRotation;
        
        CurrentAngle = Quaternion.Angle(baseRotation, diskRigidbody.rotation);
        
        if (IsServer)
        {
            targetRotation.Value = desiredRotation;
            
            Quaternion lerpRot = Quaternion.Lerp(
                diskRigidbody.rotation,
                desiredRotation,
                Time.fixedDeltaTime * 5f
            );
            Quaternion nextRot = Quaternion.RotateTowards(
                diskRigidbody.rotation,
                lerpRot,
                40f * Time.fixedDeltaTime // Xoay tối đa 40 độ / giây
            );
            // Dùng transform.rotation thay vì MoveRotation để triệt tiêu hoàn toàn lực hất vật lý (bounce) từ mặt sàn
            diskRigidbody.transform.rotation = nextRot;
        }
        else
        {
            Quaternion lerpRot = Quaternion.Lerp(
                diskRigidbody.rotation,
                targetRotation.Value,
                Time.fixedDeltaTime * 5f
            );
            Quaternion nextRot = Quaternion.RotateTowards(
                diskRigidbody.rotation,
                lerpRot,
                40f * Time.fixedDeltaTime // Xoay tối đa 40 độ / giây
            );
            diskRigidbody.transform.rotation = nextRot;
        }

        // Áp dụng lực hút nhẹ để nhân vật bám sát đĩa hơn khi đĩa di chuyển
        // Dừng khi đĩa đã bị khóa (hoàn thành puzzle) để tránh văng player lên
        if (!puzzleLocked)
        {
            foreach (var player in playersOnBoard)
            {
                // Bỏ qua lực hút nếu đã rớt khỏi mặt đĩa
                Vector3 localPos = diskRigidbody.transform.InverseTransformPoint(player.transform.position);
                if (localPos.y < -0.5f)
                    continue;

                IPlayerHUDTarget info = player.GetComponentInParent<IPlayerHUDTarget>();
                if (info != null && info.IsOwner)
                {
                    Rigidbody rb = player.GetComponentInParent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddForce(-diskRigidbody.transform.up * 15f, ForceMode.Force);
                    }
                }
            }
        }
    }

    float GetPlayerWeight(Collider player)
    {
        IPlayerHUDTarget info = player.GetComponentInParent<IPlayerHUDTarget>();
        if (info != null)
        {
            switch (info.CharacterClassIndex)
            {
                case 3: return 2.5f; // Arthur
                case 0: return 1.8f; // Leo
                case 1: return 1f; // Maya/Elena
                case 2: return 1f; // Elena/Maya
            }
        }
        return 1f; 
    }

    public void ResetDisk()
    {
        if (diskRigidbody == null) return;
        Quaternion baseRot = Quaternion.Euler(0, initialYaw, 0);
        diskRigidbody.rotation = baseRot;
        diskRigidbody.linearVelocity = Vector3.zero;
        diskRigidbody.angularVelocity = Vector3.zero;
        if (IsServer) targetRotation.Value = baseRot;
    }

    public void LockDisk()
    {
        puzzleLocked = true;
        if (IsServer && IsSpawned) LockDiskClientRpc();
    }

    public void UnlockDisk()
    {
        puzzleLocked = false;
        if (IsServer && IsSpawned) UnlockDiskClientRpc();
    }

    [ClientRpc]
    private void LockDiskClientRpc()
    {
        puzzleLocked = true;
    }

    [ClientRpc]
    private void UnlockDiskClientRpc()
    {
        puzzleLocked = false;
    }

    public void TriggerReturnToCenterAndLock()
    {
        if (IsServer)
        {
            ReturnToCenterAndLockClientRpc();
        }
    }

    [ClientRpc]
    private void ReturnToCenterAndLockClientRpc()
    {
        StartCoroutine(ReturnToCenterAndLockCoroutine());
    }

    private IEnumerator ReturnToCenterAndLockCoroutine()
    {
        LockDisk(); // Khoá đĩa ngay để FixedUpdate không đè lại lực nghiêng
        
        Quaternion startRot = diskRigidbody.rotation;
        Quaternion targetRot = Quaternion.Euler(0, initialYaw, 0);
        float timer = 0f;
        float duration = 1f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            Quaternion rot = Quaternion.Slerp(startRot, targetRot, timer / duration);
            diskRigidbody.transform.rotation = rot;
            if (IsServer) targetRotation.Value = rot;
            yield return null;
        }

        diskRigidbody.transform.rotation = targetRot;
        if (IsServer) targetRotation.Value = targetRot;
        CurrentAngle = 0;
    }
}