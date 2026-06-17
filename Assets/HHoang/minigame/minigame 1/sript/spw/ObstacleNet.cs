using UnityEngine;
using Unity.Netcode;

public class ObstacleNet : MonoBehaviour
{
    // ĐỔI THÀNH ONTRIGGERENTER (HỆ 3D)
    private void OnTriggerEnter(Collider collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerControllerNet player = collision.GetComponent<PlayerControllerNet>();
            
            // Chỉ có máy của chính người chơi đó (Owner) mới có quyền gửi lệnh đòi hồi sinh
            if (player != null && player.IsOwner)
            {
                player.RequestRespawnServerRpc();
            }
        }
    }
}