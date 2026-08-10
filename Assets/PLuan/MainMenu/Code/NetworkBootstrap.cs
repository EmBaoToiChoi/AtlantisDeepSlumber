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

            // Tự động thêm HUD hiển thị FPS và Ping vào đối tượng này
            gameObject.AddComponent<FPSPingDisplay>();

            // Tối ưu hóa tốc độ nạp tài nguyên bất đồng bộ trong nền và đưa lên GPU nhanh hơn
            QualitySettings.asyncUploadTimeSlice = 4; // Tăng từ mặc định 2ms lên 4ms để nạp nhanh hơn mỗi frame
            QualitySettings.asyncUploadBufferSize = 64; // Tăng kích thước bộ đệm tải lên GPU từ mặc định lên 64MB
            Application.backgroundLoadingPriority = ThreadPriority.High; // Tăng quyền ưu tiên cho luồng tải trong nền
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
            // Tối ưu hóa CPU cho VPS: Giới hạn 30 FPS giúp CPU giảm từ 100% xuống còn 2-5%
            UnityEngine.Application.targetFrameRate = 30;
            UnityEngine.QualitySettings.vSyncCount = 0;

            Debug.Log("[SERVER] Phát hiện đang chạy trên VPS. Đang tự động khởi động Server (FrameRate: 30 FPS)...");
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
            transport.DisconnectTimeoutMS = 300000; // Tăng timeout kết nối cấp thấp lên 300 giây (5 phút) tránh ngắt kết nối do nạp cảnh đơ
            transport.HeartbeatTimeoutMS = 3000;    // 3 giây gửi 1 lần
            transport.ConnectTimeoutMS = 15000;     // 15 giây kết nối ban đầu
        }

        // ĐĂNG KÝ TRƯỚC KHI BẬT SERVER (RẤT QUAN TRỌNG)
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

        // ĐĂNG KÝ THEO DÕI NGẮT KẾT NỐI ĐỂ TỰ ĐỘNG RESET KHI PHÒNG TRỐNG
        NetworkManager.Singleton.OnClientDisconnectCallback += OnServerClientDisconnected;

        // Tăng timeout tải cảnh để tránh ngắt kết nối khi load map chậm (tăng từ 120 lên 300)
        NetworkManager.Singleton.NetworkConfig.LoadSceneTimeOut = 300;

        NetworkManager.Singleton.StartServer();
        Debug.Log("[SERVER] Dedicated Server đã bắt đầu lắng nghe tại cổng 7777...");

        // SỬ DỤNG NETWORK SCENE MANAGER ĐỂ ĐỒNG BỘ CẢNH CHUẨN
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEventReceived;
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEventReceived;
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            if (SceneLoader.Instance != null)
            {
                _ = SceneLoader.Instance.LoadSceneAsync("Lobby", "LOADING Lobby...");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
            }
        }
    }

    private void OnServerClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        Debug.Log($"[SERVER] Client {clientId} đã ngắt kết nối.");
        // Chờ 1 giây để danh sách ConnectedClientsList được cập nhật chính xác
        StartCoroutine(CheckServerEmptyDelayed());
    }

    private System.Collections.IEnumerator CheckServerEmptyDelayed()
    {
        yield return new WaitForSeconds(1.0f);
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsList.Count == 0)
        {
            Debug.Log("[SERVER] Không còn người chơi nào! Đang tự động reset VPS về cảnh 'Lobby'...");
            
            // Xóa sạch dữ liệu chờ và đưa về mặc định
            PendingPlayerNames.Clear();
            ServerRoomName = "Atlantis Lobby";
            ServerRoomId = "000000";
            CurrentActiveRoomId = "";
            IsGameStarted = false;
            ActivePlayerNames.Clear();

            // Đưa VPS quay về cảnh Lobby đón lượt chơi mới
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
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

                // Đưa VPS quay về cảnh Lobby
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
                        // Tự động đồng bộ sang Lobby/Lobby2 thông qua NetworkSceneManager
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
            Debug.Log("[SERVER] Đang chuyển cảnh về Lobby...");
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
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
        string targetAddress = "165.99.14.40";
        if (transport != null)
        {
            // Ưu tiên đọc IP cấu hình từ Inspector/Prefab nếu không phải localhost/127.0.0.1
            targetAddress = transport.ConnectionData.Address;
            if (string.IsNullOrEmpty(targetAddress) || targetAddress == "127.0.0.1" || targetAddress == "localhost")
            {
                targetAddress = "165.99.14.40"; // Fallback IP VPS chính xác
            }

            transport.ConnectionData.Address = targetAddress;
            transport.ConnectionData.Port = 7777;
            NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;
            
            // Tối ưu hóa các cài đặt timeout kết nối UTP cấp thấp
            transport.DisconnectTimeoutMS = 300000; // 300 giây (5 phút) chống ngắt kết nối khi đơ nạp cảnh
            transport.HeartbeatTimeoutMS = 3000;    // 3 giây gửi 1 lần
            transport.ConnectTimeoutMS = 15000;     // 15 giây kết nối ban đầu
        }

        NetworkManager.Singleton.OnClientConnectedCallback += (id) => {
            Debug.Log("<color=green>[NETWORK] KẾT NỐI VPS THÀNH CÔNG!</color>");
        };
        NetworkManager.Singleton.OnClientDisconnectCallback += (id) => {
            string reason = NetworkManager.Singleton.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
            {
                Debug.Log("<color=red>[NETWORK] KẾT NỐI THẤT BẠI HOẶC BỊ NGẮT! Hãy kiểm tra Port 7777 trên VPS.</color>");
            }
            else
            {
                Debug.Log($"<color=red>[NETWORK] KẾT NỐI THẤT BẠI HOẶC BỊ NGẮT! Lý do: {reason}</color>");
            }
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEventReceived;
            }
        };

        // Tăng timeout tải cảnh để tránh ngắt kết nối khi load map chậm (tăng từ 120 lên 300)
        NetworkManager.Singleton.NetworkConfig.LoadSceneTimeOut = 300;

        NetworkManager.Singleton.StartClient();
        Debug.Log($"[NETWORK] Đang thử kết nối tới {targetAddress}:7777... (Tên: {data.playerName})");

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
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.SetStatusText("LOADING GAMEPLAY MAP...");
        }

        while (op != null && !op.isDone)
        {
            // Ánh xạ mượt mà op.progress lên thanh tiến trình từ 0% đến 100%
            float progressPercent;
            if (op.progress < 0.9f)
            {
                progressPercent = (op.progress / 0.9f) * 90f;
            }
            else
            {
                // Khi nạp xong phần thô (>= 0.9f) và chuẩn bị kích hoạt cảnh
                progressPercent = 90f + ((op.progress - 0.9f) / 0.1f) * 10f;
            }

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.SetProgress(progressPercent);
            }
            yield return null;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.SetProgress(100f);
            SceneLoader.Instance.SetStatusText("READY TO DESCEND");
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
