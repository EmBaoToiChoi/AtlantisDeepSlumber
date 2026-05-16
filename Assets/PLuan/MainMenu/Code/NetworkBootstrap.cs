using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Giữ NetworkManager xuyên suốt game
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void StartClientAsPlayer()
    {
        if (NetworkManager.Singleton == null) return;
        
        // Đảm bảo dọn sạch kết nối cũ nếu còn sót
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
            NetworkManager.Singleton.Shutdown();

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = "165.99.14.40";
            transport.ConnectionData.Port = 7777;
        }

        NetworkManager.Singleton.StartClient();
        Debug.Log("[NETWORK] Client Started connecting to VPS...");
    }

    public void StartServerAsHost()
    {
        if (NetworkManager.Singleton == null) return;

        // Đảm bảo dọn sạch kết nối cũ nếu còn sót
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
            NetworkManager.Singleton.Shutdown();

        NetworkManager.Singleton.StartHost();
        Debug.Log("[NETWORK] Host Started locally...");
    }

}
