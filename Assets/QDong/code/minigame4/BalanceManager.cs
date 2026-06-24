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
    private bool isFlipping = false;
    private bool puzzleLocked = false;

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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            playersOnBoard.Add(other);
            Debug.Log($"<color=green>[ĐĨA NGHIÊNG]</color> Phát hiện nhân vật {other.name} ĐẠT CHÂN lên đĩa. Số người hiện tại: {playersOnBoard.Count}");
        }
        else
        {
            // Log này giúp bạn check xem có phải bạn quên chưa đổi Tag của Player không
            Debug.Log($"[ĐĨA NGHIÊNG] Có vật thể chạm vào nhưng bị bỏ qua vì không phải Tag 'Player': {other.name} (Tag hiện tại: {other.tag})");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            playersOnBoard.Remove(other);
            Debug.Log($"<color=red>[ĐĨA NGHIÊNG]</color> Nhân vật {other.name} RỜI KHỎI đĩa. Số người còn lại: {playersOnBoard.Count}");
        }
    }

    void FixedUpdate()
    {
        if (puzzleLocked || isFlipping || diskRigidbody == null)
            return;

        if (IsServer)
        {
            playersOnBoard.RemoveWhere(p => p == null);

            float tiltX = 0f;
            float tiltZ = 0f;

            foreach (var player in playersOnBoard)
            {
                // Tính vị trí tương đối (Local Position) từ Player tới tâm đĩa
                Vector3 localPos = diskRigidbody.transform.InverseTransformPoint(player.transform.position);
                float weight = GetPlayerWeight(player);

                tiltX += localPos.z * weight;
                tiltZ += localPos.x * weight;
            }

            // Cập nhật góc xoay đích dựa trên vị trí người chơi
            // Đổi -tiltX thành tiltX (Trục X: đi tới/lui)
            // Giữ nguyên -tiltZ (Trục Z: đi trái/phải)
            targetRotation.Value = Quaternion.Euler(tiltX * tiltSensitivity, 0f, -tiltZ * tiltSensitivity);
            
            CurrentAngle = Quaternion.Angle(Quaternion.identity, diskRigidbody.rotation);
        }

        // Cả Server và Client cùng thực hiện xoay mâm mượt mà theo biến mạng targetRotation
        diskRigidbody.MoveRotation(Quaternion.Lerp(
            diskRigidbody.rotation,
            targetRotation.Value,
            Time.fixedDeltaTime * 5f
        ));
    }

    float GetPlayerWeight(Collider player)
    {
        CharacterInfo info = player.GetComponent<CharacterInfo>();
        if (info != null)
        {
            switch (info.characterType.Value)
            {
                case CharacterType.Arthur: return 2f;
                case CharacterType.Leo:    return 1f;
                case CharacterType.Maya:   return 0.8f;
                case CharacterType.Elena:  return 0.5f;
            }
        }
        return 1f; 
    }

    public void FlipDisk()
    {
        if (isFlipping) return;
        StartCoroutine(FlipCoroutine());
    }

    IEnumerator FlipCoroutine()
    {
        isFlipping = true;
        Quaternion startRotation = diskRigidbody.rotation;
        Quaternion targetRot = startRotation * Quaternion.Euler(180f, 0f, 0f);
        float duration = 1.2f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            Quaternion newRotation = Quaternion.Slerp(startRotation, targetRot, timer / duration);
            diskRigidbody.MoveRotation(newRotation);
            if (IsServer) targetRotation.Value = newRotation;
            yield return null;
        }
        diskRigidbody.MoveRotation(targetRot);
        if (IsServer) targetRotation.Value = targetRot;
    }

    public void ResetDisk()
    {
        isFlipping = false;
        diskRigidbody.rotation = Quaternion.identity;
        diskRigidbody.linearVelocity = Vector3.zero;
        diskRigidbody.angularVelocity = Vector3.zero;
        if (IsServer) targetRotation.Value = Quaternion.identity;
    }

    public void LockDisk()
    {
        puzzleLocked = true;
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
            yield return null;
        }

        diskRigidbody.MoveRotation(Quaternion.identity);
        CurrentAngle = 0;
        puzzleLocked = true;
    }
}