using UnityEngine;

public class NetworkMonsterAI : MonoBehaviour 
{
    public float hearingRadius = 15f; // Bán kính nghe thấy
    public Transform currentTarget;   // Mục tiêu hiện tại của quái
    
    void Update()
    {
        // BẮT BUỘC: Chỉ có Server mới được chạy logic tìm mục tiêu này
        // Nếu là Photon Fusion: if (!Object.HasStateAuthority) return;
        // Nếu là Netcode: if (!IsServer) return;
        
        if (currentTarget == null)
        {
            LookForNoise();
        }
        else
        {
            // Logic đuổi theo target...
            // Nếu target ngồi xuống hoặc chạy thoát quá xa -> Mất dấu -> currentTarget = null;
        }
    }

    void LookForNoise()
    {
        // 1. Tìm tất cả các object có tag "Player" trong phòng
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);

            // 2. Nếu player ở trong tầm nghe
            if (distance <= hearingRadius)
            {
                // Lấy script trạng thái mạng của Player đó
                PlayerMovementState playerState = player.GetComponent<PlayerMovementState>();

                if (playerState != null && playerState.IsMakingNoise())
                {
                    // Quái nghe thấy tiếng động! Chọn Player này làm mục tiêu đổi theo
                    currentTarget = player.transform;
                    Debug.Log($"Server: Quái phát hiện ra Player {player.name} làm ồn!");
                    break; 
                }
            }
        }
    }
}