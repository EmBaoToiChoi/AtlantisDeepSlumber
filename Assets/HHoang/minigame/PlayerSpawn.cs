using Unity.Netcode;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject hostPlayerPrefab;   // Kéo con playerrrrrrr vào đây
    public GameObject clientPlayerPrefab; // Kéo con playerrrrrrr 1 vào đây

    void Start()
    {
        // Chỉ Server mới có quyền điều khiển spawn
        NetworkManager.Singleton.OnServerStarted += () => {
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        };
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Khi client kết nối, chúng ta spawn nhân vật tương ứng
        ulong clientId = request.ClientNetworkId;
        
        // Nếu là Host (ID 0) thì spawn con host, còn lại spawn con client
        GameObject prefabToSpawn = (clientId == 0) ? hostPlayerPrefab : clientPlayerPrefab;
        
        GameObject playerInstance = Instantiate(prefabToSpawn);
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        
        response.Approved = true;
        response.CreatePlayerObject = false; // Chúng ta đã tự spawn ở trên rồi
    }
}