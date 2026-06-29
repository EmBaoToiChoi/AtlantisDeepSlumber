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
            if (IsServer) Debug.Log($"<color=green>[ĐĨA NGHIÊNG]</color> Phát hiện nhân vật {other.name} ĐẠT CHÂN lên đĩa. Số người hiện tại: {playersOnBoard.Count}");
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

        // Cập nhật góc xoay đích dựa trên vị trí người chơi (tính local trên mọi máy để mượt nhất)
        Quaternion desiredRotation = Quaternion.Euler(tiltX * tiltSensitivity, 0f, -tiltZ * tiltSensitivity);
        
        CurrentAngle = Quaternion.Angle(Quaternion.identity, diskRigidbody.rotation);
        
        if (IsServer)
        {
            targetRotation.Value = desiredRotation;
        }

        // Mượt mà hóa vòng xoay (Lerp) kết hợp giới hạn vận tốc góc (RotateTowards)
        // Tránh việc đĩa bật ngược lên quá nhanh hất văng người chơi khi có người trượt ra ngoài
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

        // Áp dụng lực hút nhẹ để nhân vật bám sát đĩa hơn khi đĩa di chuyển
        foreach (var player in playersOnBoard)
        {
            // Bỏ qua lực hút nếu đã rớt khỏi mặt đĩa
            Vector3 localPos = diskRigidbody.transform.InverseTransformPoint(player.transform.position);
            if (localPos.y < -0.5f)
                continue;

            CharacterInfo info = player.GetComponent<CharacterInfo>();
            if (info != null && info.IsOwner)
            {
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(-diskRigidbody.transform.up * 15f, ForceMode.Force);
                }
            }
        }
    }

    float GetPlayerWeight(Collider player)
    {
        CharacterInfo info = player.GetComponent<CharacterInfo>();
        if (info != null)
        {
            switch (info.characterType.Value)
            {
                case CharacterType.Arthur: return 2.5f;
                case CharacterType.Leo:    return 1.8f;
                case CharacterType.Maya:   return 1f;
                case CharacterType.Elena:  return 1f;
            }
        }
        return 1f; 
    }

    public void ResetDisk()
    {
        diskRigidbody.rotation = Quaternion.identity;
        diskRigidbody.linearVelocity = Vector3.zero;
        diskRigidbody.angularVelocity = Vector3.zero;
        if (IsServer) targetRotation.Value = Quaternion.identity;
    }

    public void LockDisk()
    {
        puzzleLocked = true;
    }

    public void UnlockDisk()
    {
        puzzleLocked = false;
    }

    public IEnumerator ReturnToCenterAndLock()
    {
        Quaternion startRot = diskRigidbody.rotation;
        float timer = 0f;
        float duration = 1f;

        while(timer < duration)
        {
            timer += Time.deltaTime;
            Quaternion rot = Quaternion.Slerp(startRot, Quaternion.identity, timer / duration);
            diskRigidbody.MoveRotation(rot);
            if (IsServer) targetRotation.Value = rot;
            yield return null;
        }

        diskRigidbody.MoveRotation(Quaternion.identity);
        if (IsServer) targetRotation.Value = Quaternion.identity;
        CurrentAngle = 0;
        puzzleLocked = true;
    }
}