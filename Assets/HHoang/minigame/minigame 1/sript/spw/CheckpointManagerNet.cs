using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CheckpointManagerNet : NetworkBehaviour
{
    // Kéo thả 4 cái Object vị trí (Transform) vào đây ngoài Inspector
    public List<Transform> spawnPoints; 

    // Danh sách lưu trữ: Điểm số mấy (int) đang thuộc về Player nào (ulong)
    private Dictionary<int, ulong> pointAssignment = new Dictionary<int, ulong>();

    // ĐỔI THÀNH ONTRIGGERENTER (HỆ 3D)
    private void OnTriggerEnter(Collider collision)
    {
        // Chỉ xử lý trên máy Server
        if (!IsServer) return;

        if (collision.CompareTag("Player"))
        {
            PlayerControllerNet player = collision.GetComponent<PlayerControllerNet>();
            if (player != null)
            {
                ulong playerId = player.NetworkObjectId;

                // Nếu ông này đã nhận điểm trước đó rồi thì bỏ qua
                if (pointAssignment.ContainsValue(playerId)) return;

                // Tìm các vị trí còn trống chưa ai nhận
                List<int> availableIndices = new List<int>();
                for (int i = 0; i < spawnPoints.Count; i++)
                {
                    if (!pointAssignment.ContainsKey(i))
                    {
                        availableIndices.Add(i);
                    }
                }

                // Nếu còn điểm trống, bốc ngẫu nhiên 1 điểm gán cho Player này
                if (availableIndices.Count > 0)
                {
                    int randomIndex = availableIndices[Random.Range(0, availableIndices.Count)];
                    
                    pointAssignment.Add(randomIndex, playerId);

                    // Lấy tọa độ 3D (Vector3)
                    Vector3 assignedPosition = spawnPoints[randomIndex].position;

                    // Lưu vào biến của Player đó trên Server
                    player.UpdateCheckpointServer(assignedPosition);
                    
                    Debug.Log($"Server: Đã chia ngẫu nhiên điểm 3D số {randomIndex} cho Player ID: {playerId}");
                }
            }
        }
    }
}