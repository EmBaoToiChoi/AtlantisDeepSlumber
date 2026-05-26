using Unity.Netcode;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject hostPlayerPrefab;
    public GameObject clientPlayerPrefab;

    void Start()
    {
        // Đảm bảo chỉ Server mới thực hiện logic này
        NetworkManager.Singleton.OnServerStarted += () => {
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                
                // Spawn cho Host ngay khi Server vừa start
                SpawnPlayer(0); 
            }
        };
    }

    private void OnClientConnected(ulong clientId)
    {
        // Khi client kết nối, spawn cho client
        if (clientId != 0) // ID 0 là Host, đã spawn ở trên
        {
            SpawnPlayer(clientId);
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        GameObject prefabToSpawn = (clientId == 0) ? hostPlayerPrefab : clientPlayerPrefab;
        GameObject playerInstance = Instantiate(prefabToSpawn);
        
        // SpawnAsPlayerObject gắn quyền điều khiển cho client đó
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}