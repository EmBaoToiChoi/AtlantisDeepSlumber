using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class BalanceManager : NetworkBehaviour
{
    public ZoneTrigger nw;
    public ZoneTrigger ne;
    public ZoneTrigger sw;
    public ZoneTrigger se;

    // Thay đổi từ Transform thành Rigidbody để xử lý vật lý chính xác
    public Rigidbody diskRigidbody;

    public float CurrentAngle { get; private set; }

    // Biến mạng lưu trữ góc xoay đồng bộ từ Server xuống các Client
    private NetworkVariable<Quaternion> targetRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private bool isFlipping = false;

    public void FlipDisk()
    {
        if(isFlipping) return;

        StartCoroutine(FlipCoroutine());
    }

    IEnumerator FlipCoroutine()
    {
        isFlipping = true;

        Quaternion startRot = diskRigidbody.rotation;
        Quaternion targetRot =
            startRot * Quaternion.Euler(180f, 0f, 0f);

        float timer = 0f;
        float duration = 1f;

        while(timer < duration)
        {
            timer += Time.deltaTime;

            Quaternion rot =
                Quaternion.Slerp(
                    startRot,
                    targetRot,
                    timer / duration
                );

            diskRigidbody.MoveRotation(rot);

            yield return null;
        }
    }

    // Sử dụng FixedUpdate thay vì Update để đồng bộ hoàn hảo với chu kỳ vật lý của nhân vật
    void FixedUpdate()
    {
        // 1. Chỉ Server thực hiện tính toán trọng lượng và góc nghiêng mục tiêu
    if (IsServer)
    {
        float NW = GetWeight(nw);
        float NE = GetWeight(ne);
        float SW = GetWeight(sw);
        float SE = GetWeight(se);

        // Tính độ lệch trái phải và trước sau
        float tiltX = (NE + SE) - (NW + SW);
        float tiltZ = (SW + SE) - (NW + NE);

        CurrentAngle =
            new Vector2(
                tiltX,
                tiltZ
            ).magnitude;
        // XÓA DÒNG CŨ NÀY:
        // targetRotation.Value = Quaternion.Euler(tiltZ * 3f, 0, -tiltX * 3f);

        // THAY BẰNG DÒNG DƯỚI ĐÂY (Đã đảo ngược dấu để bên nặng chìm xuống):
        targetRotation.Value =
    Quaternion.Euler(
        -tiltZ * 3f,
        0,
        tiltX * 3f
    );
            Debug.Log(
            $"NW:{NW} NE:{NE} SW:{SW} SE:{SE}"
        );
    }

        // 2. Cả Server và Client đều dùng MoveRotation để xoay mâm mượt mà, giữ chặt chân nhân vật bằng ma sát
        if (diskRigidbody != null)
        {
            Quaternion nextRotation = Quaternion.Lerp(
                diskRigidbody.rotation,
                targetRotation.Value,
                Time.fixedDeltaTime * 2f // Dùng fixedDeltaTime trong FixedUpdate
            );
            
            diskRigidbody.MoveRotation(nextRotation);
        }
    }

    float GetWeight(ZoneTrigger zone)
    {
        float total = 0;
        if (zone == null || zone.playersInside == null) return total;

        foreach (var player in zone.playersInside)
        {
            if (player == null) continue;

            switch (player.characterType.Value)
            {
                case CharacterType.Arthur:
                    total += 2f;
                    break;
                case CharacterType.Leo:
                    total += 1f;
                    break;
                case CharacterType.Maya:
                    total += 0.8f;
                    break;
                case CharacterType.Elena:
                    total += 0.5f;
                    break;
            }
        }
        return total;
    }
    // public void FlipDisk()
    // {
    //     diskRigidbody.MoveRotation(
    //         Quaternion.Euler(
    //             180,
    //             0,
    //             0
    //         )
    //     );
    // }
}