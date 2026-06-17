using UnityEngine;
using Unity.Netcode;

public class PlayerControllerNet : NetworkBehaviour
{
    private Vector3 myRespawnPosition;

    public override void OnNetworkSpawn()
    {
        // Mặc định ban đầu là vị trí xuất phát 3D
        myRespawnPosition = transform.position;
    }

    public void UpdateCheckpointServer(Vector3 newPosition)
    {
        if (IsServer)
        {
            myRespawnPosition = newPosition;
        }
    }

    [ServerRpc]
    public void RequestRespawnServerRpc()
    {
        // Server dịch chuyển vị trí 3D của Player
        transform.position = myRespawnPosition;

        // Reset vật lý 3D (Rigidbody) về 0 để nhân vật đứng im ngay lập tức, không bị trượt tiếp
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero; // Hoặc rb.velocity = Vector3.zero tùy bản Unity
            rb.angularVelocity = Vector3.zero; // Khóa luôn lực xoay vòng vòng nếu có
        }

        Debug.Log($"Server: Đã đưa Player {NetworkObjectId} về điểm hồi sinh 3D.");
    }
}