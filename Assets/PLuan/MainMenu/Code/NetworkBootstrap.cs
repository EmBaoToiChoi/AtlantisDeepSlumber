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
        
        // Thiết lập địa chỉ IP của VPS trước khi kết nối
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
        NetworkManager.Singleton.StartHost();
        Debug.Log("[NETWORK] Host Started locally...");
    }
}
