using UnityEngine;
using Unity.Netcode;

public class BalanceManager : NetworkBehaviour
{
    public ZoneTrigger nw;
    public ZoneTrigger ne;
    public ZoneTrigger sw;
    public ZoneTrigger se;

    // Thay đổi từ Transform thành Rigidbody để xử lý vật lý chính xác
    public Rigidbody diskRigidbody; 

    // Biến mạng lưu trữ góc xoay đồng bộ từ Server xuống các Client
    private NetworkVariable<Quaternion> targetRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

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

            // Cập nhật giá trị vào biến mạng
            targetRotation.Value = Quaternion.Euler(tiltZ * 3f, 0, -tiltX * 3f);
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
}