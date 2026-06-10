using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    public static System.Collections.Generic.Dictionary<ulong, string> PendingPlayerNames = new System.Collections.Generic.Dictionary<ulong, string>();
    public static string ServerRoomName = "Atlantis Lobby";
    public static string ServerRoomId = "000000";

    // Quản lý trạng thái phòng và người chơi
    public static string CurrentActiveRoomId = "";
    public static bool IsGameStarted = false;
    public static System.Collections.Generic.HashSet<string> ActivePlayerNames = new System.Collections.Generic.HashSet<string>();

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
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEventReceived;
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEventReceived;
            NetworkManager.Singleton.SceneManager.LoadScene("Waiting hall", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Waiting hall");
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
            Debug.Log("[SERVER] Không còn người chơi nào! Đang tự động reset VPS về cảnh 'Waiting hall'...");
            
            // Xóa sạch dữ liệu chờ và đưa về mặc định
            PendingPlayerNames.Clear();
            ServerRoomName = "Atlantis Lobby";
            ServerRoomId = "000000";
            CurrentActiveRoomId = "";
            IsGameStarted = false;
            ActivePlayerNames.Clear();

            // Đưa VPS quay về cảnh Waiting hall đón lượt chơi mới
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.LoadScene("Waiting hall", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = false;

        string playerName = "Explorer";
        string roomId = "000000";
        string roomName = "Atlantis Lobby";
        if (request.Payload != null && request.Payload.Length > 0)
        {
            try {
                string json = System.Text.Encoding.UTF8.GetString(request.Payload);
                var data = JsonUtility.FromJson<ConnectionPayload>(json);
                playerName = data.playerName;
                roomName = data.roomName;
                roomId = data.roomId;
                Debug.Log($"[SERVER] Nhận dữ liệu JSON kết nối: name={playerName} | roomName={roomName} | roomId={roomId}");
            } catch {
                Debug.LogError("[SERVER] Lỗi phân giải JSON kết nối!");
            }
        }
        
        PendingPlayerNames[request.ClientNetworkId] = playerName;

        // XỬ LÝ CHUYỂN CẢNH KHI CÓ PHÒNG MỚI HOẶC REJOIN
        if (NetworkManager.Singleton.IsServer)
        {
            // Nếu đây là phòng mới (roomId khác với phòng đang chạy hoặc chưa có phòng nào)
            if (string.IsNullOrEmpty(CurrentActiveRoomId) || CurrentActiveRoomId == "000000" || roomId != CurrentActiveRoomId)
            {
                Debug.Log($"[SERVER] Phát hiện phòng mới được tạo/kết nối. Chuyển từ Room ID '{CurrentActiveRoomId}' sang '{roomId}'. Reset trạng thái game.");
                CurrentActiveRoomId = roomId;
                ServerRoomId = roomId;
                ServerRoomName = roomName;
                IsGameStarted = false;
                ActivePlayerNames.Clear();

                // Đưa VPS quay về cảnh Waiting hall
                StartLoadWaitingHall();
            }
            else
            {
                // Nếu trùng roomId (cùng phòng)
                if (IsGameStarted)
                {
                    // Nếu game đã bắt đầu
                    if (ActivePlayerNames.Contains(playerName))
                    {
                        Debug.Log($"[SERVER] Player '{playerName}' rejoin vào phòng đang chạy game. Cho phép vào thẳng scene.");
                        // Tự động đồng bộ sang Waiting hall/Waiting hall2 thông qua NetworkSceneManager
                    }
                    else
                    {
                        Debug.LogWarning($"[SERVER] Player '{playerName}' không nằm trong danh sách game đã start của phòng này. Từ chối kết nối.");
                        response.Approved = false;
                        response.Reason = "Game already in progress.";
                    }
                }
            }
        }
    }

    private void StartLoadWaitingHall()
    {
        StartCoroutine(LoadWaitingHallCoroutine());
    }

    private System.Collections.IEnumerator LoadWaitingHallCoroutine()
    {
        yield return null; // Đợi 1 frame
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            Debug.Log("[SERVER] Đang chuyển cảnh về Waiting hall...");
            NetworkManager.Singleton.SceneManager.LoadScene("Waiting hall", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
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
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEventReceived;
            }
        };

        NetworkManager.Singleton.StartClient();
        Debug.Log($"[NETWORK] Đang thử kết nối tới 165.99.14.40:7777... (Tên: {data.playerName})");

        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEventReceived;
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEventReceived;
        }
    }

    public void StartServerAsHost()
    {
        // TRƯỜNG HỢP DÙNG VPS: Cả người tạo phòng cũng là Client
        // Vì Server thực sự đã chạy sẵn trên VPS rồi.
        StartClientAsPlayer();
    }

    private void OnSceneEventReceived(SceneEvent sceneEvent)
    {
        if (!NetworkManager.Singleton.IsClient) return;
        if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId) return;

        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Load:
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.ShowLoading($"LOADING {sceneEvent.SceneName.ToUpper()}...");
                    if (sceneEvent.AsyncOperation != null)
                    {
                        StartCoroutine(TrackSceneLoadProgress(sceneEvent.AsyncOperation));
                    }
                }
                break;
            case SceneEventType.LoadComplete:
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.HideLoading();
                }
                break;
        }
    }

    private System.Collections.IEnumerator TrackSceneLoadProgress(AsyncOperation op)
    {
        while (op != null && !op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.SetProgress(progress * 100f);
            }
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEventReceived;
        }
    }


}
