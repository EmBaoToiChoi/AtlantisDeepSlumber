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

    private void Start()
    {
        // TỰ ĐỘNG BẬT SERVER NẾU CHẠY TRÊN VPS (Headless Mode)
        if (UnityEngine.Application.isBatchMode)
        {
            Debug.Log("[SERVER] Phát hiện đang chạy trên VPS. Đang tự động khởi động Server...");
            StartServerOnVPS();
        }
    }

    private void StartServerOnVPS()
    {
        if (NetworkManager.Singleton == null) return;
        
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = "0.0.0.0"; // Lắng nghe mọi kết nối tới
            transport.ConnectionData.Port = 7777;
        }

        NetworkManager.Singleton.StartServer();
        Debug.Log("[SERVER] Dedicated Server đã bắt đầu lắng nghe tại cổng 7777...");
    }

    public void StartClientAsPlayer()
    {
        if (NetworkManager.Singleton == null) return;
        
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
            NetworkManager.Singleton.Shutdown();

        // Gửi tên người chơi kèm theo khi kết nối
        string playerName = PlayerPrefs.GetString("AuthDisplayName", "Explorer");
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(playerName);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = "165.99.14.40"; 
            transport.ConnectionData.Port = 7777;

            NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += (id) => {
            Debug.Log("<color=green>[NETWORK] KẾT NỐI VPS THÀNH CÔNG!</color>");
        };
        NetworkManager.Singleton.OnClientDisconnectCallback += (id) => {
            Debug.Log("<color=red>[NETWORK] KẾT NỐI THẤT BẠI HOẶC BỊ NGẮT! Hãy kiểm tra Port 7777 trên VPS.</color>");
        };

        NetworkManager.Singleton.StartClient();
        Debug.Log($"[NETWORK] Đang thử kết nối tới 165.99.14.40:7777... (Tên: {playerName})");

    }

    public void StartServerAsHost()
    {
        // TRƯỜNG HỢP DÙNG VPS: Cả người tạo phòng cũng là Client
        // Vì Server thực sự đã chạy sẵn trên VPS rồi.
        StartClientAsPlayer();
    }


}
