using UnityEngine;
using Unity.Netcode;

public class TempPlayerSpawner : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("Kéo thả Prefab Player của bạn vào đây")]
    [SerializeField] private GameObject playerPrefab;
    
    [Tooltip("Điểm xuất hiện của Player (nếu trống sẽ lấy vị trí của Spawner này)")]
    [SerializeField] private Transform spawnPoint;

    [Header("Network Test Settings")]
    [Tooltip("Tự động Start Host nếu có NetworkManager trong Scene và chưa khởi động mạng")]
    [SerializeField] private bool startAsHost = false;

    private void Awake()
    {
        // Kiểm tra xem đã có Player nào thuộc LeoPlayer tồn tại trong Scene chưa để tránh trùng lặp
        LeoPlayer existingPlayer = FindObjectOfType<LeoPlayer>();
        if (existingPlayer != null)
        {
            Debug.Log("[TempPlayerSpawner] Đã phát hiện Player trong Scene. Hủy spawn để tránh trùng lặp.");
            return;
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        spawnPos.y += 0.5f; // Tăng thêm 0.5f trên trục Y để tránh rơi xuyên map
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        if (startAsHost && NetworkManager.Singleton != null)
        {
            if (!NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[TempPlayerSpawner] Khởi động Host cho Netcode để test multiplayer/online...");
                NetworkManager.Singleton.StartHost();
            }
        }
        else
        {
            if (playerPrefab != null)
            {
                Debug.Log("[TempPlayerSpawner] Spawn Player ở chế độ Standalone (Chơi đơn) để test nhanh...");
                GameObject spawnObj = Instantiate(playerPrefab, spawnPos, spawnRot);
                
                // Kích hoạt camera follow cho Player test
                LeoPlayer playerScript = spawnObj.GetComponent<LeoPlayer>();
                if (playerScript != null)
                {
                    playerScript.enableCameraFollow = true;
                }
            }
            else
            {
                Debug.LogError("[TempPlayerSpawner] Vui lòng kéo thả Player Prefab vào Inspector của TempPlayerSpawner!");
            }
        }
    }
}
