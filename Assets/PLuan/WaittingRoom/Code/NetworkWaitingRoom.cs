using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class NetworkWaitingRoom : NetworkBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument _uiDocument;
    
    [Header("Slots")]
    public Transform[] slots = new Transform[4];
    public GameObject playerNetworkPrefab; 

    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetRoomName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "ATLANTIS EXPEDITION", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetRoomId = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "XXXXXX", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private VisualElement _root;
    private Label _lblRoomName;
    private Label _lblRoomId;
    private Label _lblPlayerCount;
    private Button _btnReady;
    private Button _btnStart;
    private Button _btnLeave;

    public NetworkList<PlayerNetData> NetPlayers = new NetworkList<PlayerNetData>();

    public struct PlayerNetData : INetworkSerializable, System.IEquatable<PlayerNetData>
    {
        public Unity.Collections.FixedString64Bytes Name;
        public int Slot;
        public ulong ClientId;
        public bool IsReady;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref IsReady);
        }
        public bool Equals(PlayerNetData other) 
        {
            return ClientId == other.ClientId && 
                   IsReady == other.IsReady && 
                   Name.Equals(other.Name) && 
                   Slot == other.Slot;
        }
    }

    // private static System.Collections.Generic.Dictionary<ulong, string> _pendingPlayerNames = new System.Collections.Generic.Dictionary<ulong, string>();
    
    private void Awake() 
    { 
        Debug.Log("[EMERGENCY] Awake đã chạy!");
        // Giữ nguyên reference biên dịch gốc của NetworkList để tránh hỏng đồng bộ Netcode
        
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null)
        {
            Debug.LogError("[Lobby] KHÔNG TÌM THẤY UIDocument!");
            return;
        }

        _root = _uiDocument.rootVisualElement;
        _lblRoomName = _root.Q<Label>("lbl-room-name");
        _lblRoomId = _root.Q<Label>("lbl-room-id");
        _lblPlayerCount = _root.Q<Label>("lbl-player-count");
        _btnReady = _root.Q<Button>("btn-ready");
        _btnStart = _root.Q<Button>("btn-start");
        _btnLeave = _root.Q<Button>("btn-leave");

        Debug.Log($"[Lobby] UI Binding: _btnReady={_btnReady!=null}, _btnStart={_btnStart!=null}, _btnLeave={_btnLeave!=null}");

        RefreshLocalUI();

        if (_btnLeave != null) _btnLeave.clicked += LeaveRoom;
        if (_btnReady != null) _btnReady.clicked += ToggleReady;
        if (_btnStart != null) _btnStart.clicked += StartGame;
    }

    private void OnEnable()
    {
        Debug.Log("[EMERGENCY] OnEnable đã chạy!");
    }



    private void Start()
    {
        Debug.Log("[Lobby] Script NetworkWaitingRoom đã bắt đầu chạy (Start).");
        
        if (_uiDocument == null) Debug.LogError("[Lobby] THẤT BẠI: Bạn chưa kéo UI Document!");
        if (slots == null || slots.Length == 0) Debug.LogError("[Lobby] THẤT BẠI: Danh sách Slots đang trống!");

        // TỰ ĐỘNG KIỂM TRA NẾU VÀO PHÒNG MUỘN
        InvokeRepeating(nameof(CheckForSpawn), 0.5f, 1.0f);
    }

    private void CheckForSpawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !IsSpawned)
        {
            if (NetworkManager.Singleton.IsConnectedClient)
            {
                Debug.Log("[Lobby] Đã thấy kết nối mạng, đang thử kích hoạt đồng bộ thủ công...");
                OnNetworkSpawn();
                CancelInvoke(nameof(CheckForSpawn));
            }
        }
        else if (IsSpawned)
        {
            CancelInvoke(nameof(CheckForSpawn));
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("[Lobby] OnNetworkSpawn đã kích hoạt!");

        // Dù là Server hay Client, ta đều đăng ký callback để biết khi có người vào
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            if (IsServer)
            {
                Debug.Log($"[SERVER] Đang chạy trên VPS. Đang có {NetworkManager.Singleton.ConnectedClients.Count} người kết nối.");
                
                // LẤY DỮ LIỆU TỪ BOOTSTRAP (DO VPS KHÔNG CÓ PLAYERPREFS)
                NetRoomName.Value = NetworkBootstrap.ServerRoomName;
                NetRoomId.Value = NetworkBootstrap.ServerRoomId;
                
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    Debug.Log($"[SERVER] Tự động Spawn cho ClientID: {client.ClientId}");
                    OnClientConnected(client.ClientId);
                }
            }
            else
            {
                Debug.Log($"[CLIENT] Đã kết nối thành công với ClientID: {NetworkManager.Singleton.LocalClientId}");
            }
        }
        else
        {
            Debug.LogError("[Lobby] NetworkManager.Singleton bị NULL!");
        }

        NetRoomName.OnValueChanged += (o, n) => {
            Debug.Log($"[Lobby] Tên phòng đổi thành: {n}");
            UpdateRoomUI();
        };
        NetRoomId.OnValueChanged += (o, n) => {
            Debug.Log($"[Lobby] ID phòng đổi thành: {n}");
            UpdateRoomUI();
        };
        NetPlayers.OnListChanged += (e) => {
            Debug.Log($"[Lobby] Danh sách người chơi thay đổi! Số lượng: {NetPlayers.Count}");
            UpdatePlayerUI();
        };


        UpdateRoomUI();
        UpdatePlayerUI();

        // NẾU LÀ MÁY KHÁCH, ĐỢI 1 GIÂY RỒI MỚI GỬI LỆNH ÉP VPS CẬP NHẬT (TRÁNH XUNG ĐỘT)
        if (IsClient && !IsServer)
        {
            StartCoroutine(DelayUpdateRoomRPC());
        }
    }

    public override void OnNetworkDespawn()
    {
        Debug.Log("[Lobby] OnNetworkDespawn đã kích hoạt! Đang hủy đăng ký callback...");
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private IEnumerator DelayUpdateRoomRPC()
    {
        yield return new WaitForSeconds(2.0f);
        string pName = PlayerPrefs.GetString("AuthDisplayName", "Explorer");
        string rName = PlayerPrefs.GetString("CurrentRoomName", "Atlantis Lobby");
        string rId = PlayerPrefs.GetString("CurrentRoomID", "000000");
        Debug.Log($"[CLIENT] Đang gửi lệnh ServerRpc: {pName} | {rName}");
        UpdateRoomInfoServerRpc(pName, rName, rId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateRoomInfoServerRpc(string playerName, string roomName, string roomId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[SERVER] RPC: Cập nhật phòng '{roomName}' và Player '{playerName}' cho Client {clientId}");

        // 1. Cập nhật tên phòng toàn cục
        NetRoomName.Value = roomName;
        NetRoomId.Value = roomId;

        // 2. Cập nhật tên người chơi trong danh sách
        bool found = false;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                var p = NetPlayers[i];
                p.Name = playerName;
                NetPlayers[i] = p;
                found = true;
                Debug.Log($"[SERVER] Đã đổi tên Client {clientId} thành {playerName}");
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[SERVER] Chưa tìm thấy Client {clientId} trong danh sách NetPlayers để đổi tên!");
        }

        // 3. Cập nhật trực tiếp vào biến mạng trên nhân vật (Để đồng bộ tag tên)
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            var playerUI = client.PlayerObject.GetComponent<PlayerWaitingRoomUI>();
            if (playerUI != null)
            {
                playerUI.NetName.Value = playerName;
                Debug.Log($"[SERVER] Đã cập nhật NetName trực tiếp cho nhân vật của Client {clientId}");
            }
        }
    }



    private void RefreshLocalUI()
    {
        string localName = PlayerPrefs.GetString("CurrentRoomName", "UNKNOWN");
        string localId = PlayerPrefs.GetString("CurrentRoomID", "XXXXXX");
        if (_lblRoomName != null) _lblRoomName.text = $"SESSION: {localName.ToUpper()}";
        if (_lblRoomId != null) _lblRoomId.text = $"ID: #{localId}";
    }


    private void UpdateRoomUI()
    {
        if (_lblRoomName != null) _lblRoomName.text = $"SESSION: {NetRoomName.Value.ToString().ToUpper()}";
        if (_lblRoomId != null) _lblRoomId.text = $"ID: #{NetRoomId.Value.ToString()}";
    }

    private void UpdatePlayerUI()
    {
        if (_lblPlayerCount != null) _lblPlayerCount.text = $"PLAYERS: {NetPlayers.Count}/4";
        
        // KIỂM TRA ĐIỀU KIỆN READY: Chỉ cần ít nhất 1 người chơi sẵn sàng (Testing)
        // Khi lên sản phẩm thực tế, có thể đổi lại thành NetPlayers.Count == 4 && readyCount == 4
        int readyCount = 0;
        foreach (var p in NetPlayers) if (p.IsReady) readyCount++;
        bool allReady = readyCount >= 1;

        // KIỂM TRA XEM LOCAL CLIENT CÓ PHẢI LÀ CHỦ PHÒNG (SLOT 0) KHÔNG
        bool isRoomHost = false;
        if (NetworkManager.Singleton != null)
        {
            foreach (var p in NetPlayers)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId && p.Slot == 0)
                {
                    isRoomHost = true;
                    break;
                }
            }
        }

        if (_btnStart != null)
        {
            // Chỉ hiển thị nút Start cho Chủ phòng (Slot 0) để bấm bắt đầu
            _btnStart.style.display = isRoomHost ? DisplayStyle.Flex : DisplayStyle.None;
            _btnStart.SetEnabled(allReady);
            
            if (allReady && isRoomHost)
            {
                _btnStart.RemoveFromClassList("hidden-element");
            }
            else
            {
                _btnStart.AddToClassList("hidden-element");
            }
        }

        // Cập nhật màu nút Ready cho bản thân
        UpdateReadyButtonState();
    }

    private void UpdateReadyButtonState()
    {
        if (_btnReady == null) return;
        foreach (var p in NetPlayers)
        {
            if (p.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                if (p.IsReady) _btnReady.AddToClassList("ready-active");
                else _btnReady.RemoveFromClassList("ready-active");
                _btnReady.text = p.IsReady ? "READY!" : "READY?";
                break;
            }
        }
    }

    private void ToggleReady()
    {
        Debug.Log($"[CLIENT] Nút Ready được click! LocalClientId={NetworkManager.Singleton.LocalClientId}");
        ToggleReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[SERVER] Nhận lệnh ToggleReady từ ClientId={clientId}");
        bool found = false;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                var data = NetPlayers[i];
                data.IsReady = !data.IsReady;
                NetPlayers[i] = data;
                found = true;
                Debug.Log($"[SERVER] Cập nhật trạng thái IsReady của ClientId={clientId} thành: {data.IsReady}");
                break;
            }
        }
        if (!found)
        {
            Debug.LogWarning($"[SERVER] Không tìm thấy ClientId={clientId} trong danh sách NetPlayers để chuyển trạng thái Ready!");
        }
    }

    // Đã chuyển sang NetworkBootstrap.cs
    /*
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    ...
    */

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // CẬP NHẬT LẠI THÔNG TIN PHÒNG TỪ BOOTSTRAP (MỖI KHI CÓ NGƯỜI VÀO CHO CHẮC)
        NetRoomName.Value = NetworkBootstrap.ServerRoomName;
        NetRoomId.Value = NetworkBootstrap.ServerRoomId;

        string playerName = "Guest_" + clientId;
        if (NetworkBootstrap.PendingPlayerNames.ContainsKey(clientId))
        {
            playerName = NetworkBootstrap.PendingPlayerNames[clientId];
            NetworkBootstrap.PendingPlayerNames.Remove(clientId);
        }

        foreach (var p in NetPlayers) if (p.ClientId == clientId) return;

        AddPlayer(clientId, playerName);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                NetPlayers.RemoveAt(i);
                break;
            }
        }
    }

    private void AddPlayer(ulong clientId, string playerName)
    {
        if (!IsServer) return;
        
        int slot = FindEmptySlot();
        if (slot == -1) return;

        var newData = new PlayerNetData
        {
            Name = playerName,
            Slot = slot,
            ClientId = clientId,
            IsReady = false
        };

        NetPlayers.Add(newData);
        Debug.Log($"[Lobby] Đã thêm {playerName} vào Slot {slot}");
        
        SpawnPlayerObject(clientId);
    }

    private void SpawnPlayerObject(ulong clientId)
    {
        if (!IsServer) return;
        
        // Tìm thông tin player vừa add
        int slotIdx = -1;
        foreach (var p in NetPlayers) if (p.ClientId == clientId) slotIdx = p.Slot;
        
        if (slotIdx == -1) return;

        if (playerNetworkPrefab != null)
        {
            Vector3 spawnPos = slots[slotIdx].position;
            Quaternion spawnRot = slots[slotIdx].rotation;
            

            GameObject go = Instantiate(playerNetworkPrefab, spawnPos, spawnRot);
            
            if (go == null) {
                Debug.LogError("[SPAWN] THẤT BẠI: Lệnh Instantiate trả về null!");
                return;
            }

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null) {
                Debug.LogError("[SPAWN] THẤT BẠI: Prefab nhân vật thiếu thành phần NetworkObject!");
                return;
            }

            // Kiểm tra xem đã đăng ký Prefab chưa
            try {
                netObj.SpawnAsPlayerObject(clientId, true);
                Debug.Log($"[SPAWN] ĐÃ GỌI LỆNH SPAWN THÀNH CÔNG cho {name}!");
            }
            catch (System.Exception e) {
                Debug.LogError($"[SPAWN] LỖI KHI GỌI SPAWN: {e.Message}. Hãy kiểm tra xem bạn đã thêm Prefab vào NetworkManager chưa!");
            }
        }
        else {
            Debug.LogError("[SPAWN] THẤT BẠI: Bạn chưa kéo nhân vật vào ô 'Player Network Prefab'!");
        }
    }




    private int FindEmptySlot() {
        for (int i = 0; i < 4; i++) {
            bool occupied = false;
            foreach (var p in NetPlayers) if (p.Slot == i) occupied = true;
            if (!occupied) return i;
        }
        return -1;
    }

    private void StartGame() { 
        Debug.Log("[CLIENT] Chủ phòng click START EXPEDITION! Đang gửi lệnh ServerRpc khởi động...");
        StartGameServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartGameServerRpc() {
        if (IsServer) {
            Debug.Log("[SERVER] Nhận lệnh khởi động game! Đang chuyển tất cả người chơi sang cảnh 'minigame'...");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null) {
                NetworkManager.Singleton.SceneManager.LoadScene("minigame", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }

    private async void LeaveRoom()
    {
        string roomId = PlayerPrefs.GetString("CurrentRoomID", "");
        if (!string.IsNullOrEmpty(roomId)) await AuthService.LeaveRoom(roomId);
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
