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
        
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
            NetworkManager.Singleton.Shutdown();

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = "165.99.14.40"; 
            transport.ConnectionData.Port = 7777;

            // Gửi tên người chơi kèm theo khi kết nối
            string playerName = PlayerPrefs.GetString("Username", "Unknown");
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(playerName);
            NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;
        }

        NetworkManager.Singleton.StartClient();
        Debug.Log($"[NETWORK] Connecting to VPS with name: {PlayerPrefs.GetString("Username", "Unknown")}");

    }

    public void StartServerAsHost()
    {
        // TRƯỜNG HỢP DÙNG VPS: Cả người tạo phòng cũng là Client
        // Vì Server thực sự đã chạy sẵn trên VPS rồi.
        StartClientAsPlayer();
    }


}
