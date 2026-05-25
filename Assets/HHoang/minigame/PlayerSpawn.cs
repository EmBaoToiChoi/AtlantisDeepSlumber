using Unity.Netcode;
using UnityEngine;

public class PlayerSpawn : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Chỉ người sở hữu nhân vật này mới được quyền thay đổi vị trí của nó
        if (IsOwner)
        {
            // Danh sách các vị trí spawn (Bạn có thể thêm nhiều vị trí hơn tùy ý)
            Vector3[] spawnPositions = { 
                new Vector3(0, 1, 0),    // Vị trí cho Client 0
                new Vector3(5, 1, 5),    // Vị trí cho Client 1
                new Vector3(-5, 1, 5),   // Vị trí cho Client 2
                new Vector3(5, 1, -5)    // Vị trí cho Client 3
            };

            // Sử dụng % để đảm bảo không bị lỗi nếu có nhiều hơn số vị trí trong mảng
            int index = (int)OwnerClientId % spawnPositions.Length;
            transform.position = spawnPositions[index];
            
            Debug.Log($"Người chơi {OwnerClientId} đã spawn tại vị trí {index}");
        }
    }
}