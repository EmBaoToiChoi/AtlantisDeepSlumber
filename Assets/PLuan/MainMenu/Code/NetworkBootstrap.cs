using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    public static System.Collections.Generic.Dictionary<ulong, string> PendingPlayerNames = new System.Collections.Generic.Dictionary<ulong, string>();
    public static string ServerRoomName = "Atlantis Lobby";
    public static string ServerRoomId = "000000";

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

        // ĐĂNG KÝ TRƯỚC KHI BẬT SERVER (RẤT QUAN TRỌNG)
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

        // ĐĂNG KÝ THEO DÕI NGẮT KẾT NỐI ĐỂ TỰ ĐỘNG RESET KHI PHÒNG TRỐNG
        NetworkManager.Singleton.OnClientDisconnectCallback += OnServerClientDisconnected;

        NetworkManager.Singleton.StartServer();
        Debug.Log("[SERVER] Dedicated Server đã bắt đầu lắng nghe tại cổng 7777...");

        // SỬ DỤNG NETWORK SCENE MANAGER ĐỂ ĐỒNG BỘ CẢNH CHUẨN
        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("Map", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Map");
        }
    }

    private void OnServerClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        // Chờ 1 giây để danh sách ConnectedClientsList được cập nhật chính xác
        StartCoroutine(CheckServerEmptyDelayed());
    }

    private System.Collections.IEnumerator CheckServerEmptyDelayed()
    {
        yield return new WaitForSeconds(1.0f);
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsList.Count == 0)
        {
            Debug.Log("[SERVER] Không còn người chơi nào! Đang tự động reset VPS về cảnh 'Map'...");
            
            // Xóa sạch dữ liệu chờ và đưa về mặc định
            PendingPlayerNames.Clear();
            ServerRoomName = "Atlantis Lobby";
            ServerRoomId = "000000";

            // Đưa VPS quay về cảnh Map đón lượt chơi mới
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.LoadScene("Map", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = false;

        string playerName = "Explorer";
        if (request.Payload != null && request.Payload.Length > 0)
        {
            try {
                string json = System.Text.Encoding.UTF8.GetString(request.Payload);
                var data = JsonUtility.FromJson<ConnectionPayload>(json);
                playerName = data.playerName;
                ServerRoomName = data.roomName;
                ServerRoomId = data.roomId;
                Debug.Log($"[SERVER] Nhận dữ liệu JSON: {playerName} | {ServerRoomName}");
            } catch {
                Debug.LogError("[SERVER] Lỗi phân giải JSON kết nối!");
            }
        }
        
        PendingPlayerNames[request.ClientNetworkId] = playerName;
    }

    [System.Serializable]
    public class ConnectionPayload {
        public string playerName;
        public string roomName;
        public string roomId;
    }

    public void StartClientAsPlayer()
    {
        if (NetworkManager.Singleton == null) return;
        
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
            NetworkManager.Singleton.Shutdown();

        // ĐÓNG GÓI JSON CHO CHẮC CHẮN
        var data = new ConnectionPayload {
            playerName = PlayerPrefs.GetString("AuthDisplayName", "Explorer"),
            roomName = PlayerPrefs.GetString("CurrentRoomName", "Atlantis Lobby"),
            roomId = PlayerPrefs.GetString("CurrentRoomID", "000000")
        };
        
        string json = JsonUtility.ToJson(data);
        Debug.Log($"[CLIENT] Đang gửi gói tin JSON: {json}");
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(json);

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
        Debug.Log($"[NETWORK] Đang thử kết nối tới 165.99.14.40:7777... (Tên: {data.playerName})");

    }

    public void StartServerAsHost()
    {
        // TRƯỜNG HỢP DÙNG VPS: Cả người tạo phòng cũng là Client
        // Vì Server thực sự đã chạy sẵn trên VPS rồi.
        StartClientAsPlayer();
    }


}
