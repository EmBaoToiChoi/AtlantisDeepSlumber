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
        public bool Equals(PlayerNetData other) => ClientId == other.ClientId;
    }

    private void Awake() 
    { 
        Debug.Log("[EMERGENCY] Awake đã chạy!");
        NetPlayers = new NetworkList<PlayerNetData>(); 
        
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
        
        // Nếu dùng VPS làm Server, thì tại đây ta chỉ cần chờ NetworkManager kết nối xong.
        // Việc Spawn nhân vật sẽ được xử lý khi OnNetworkSpawn kích hoạt.

        if (_uiDocument == null) Debug.LogError("[Lobby] THẤT BẠI: Bạn chưa kéo UI Document!");
        if (slots == null || slots.Length == 0) Debug.LogError("[Lobby] THẤT BẠI: Danh sách Slots đang trống!");
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
                NetRoomName.Value = PlayerPrefs.GetString("CurrentRoomName", "ATLANTIS LOBBY");
                NetRoomId.Value = PlayerPrefs.GetString("CurrentRoomID", "000000");
                
                // QUAN TRỌNG: Kiểm tra xem có ai đã kết nối TRƯỚC KHI script này chạy không
                // (Đặc biệt là người đầu tiên tạo phòng)
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    Debug.Log($"[Lobby] Phát hiện người chơi đã chờ sẵn: {client.ClientId}. Đang tiến hành spawn...");
                    OnClientConnected(client.ClientId);
                }
            }

            else if (IsClient)
            {
                 Debug.Log("[Lobby] Tôi là Client, đang chờ Server xác nhận để hiển thị...");
            }
        }

        NetRoomName.OnValueChanged += (o, n) => UpdateRoomUI();
        NetRoomId.OnValueChanged += (o, n) => UpdateRoomUI();
        NetPlayers.OnListChanged += (e) => UpdatePlayerUI();

        UpdateRoomUI();
        UpdatePlayerUI();
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
        
        bool allReady = NetPlayers.Count == 4;
        foreach (var p in NetPlayers) if (!p.IsReady) allReady = false;

        if (_btnStart != null)
        {
            _btnStart.style.display = IsServer ? DisplayStyle.Flex : DisplayStyle.None;
            _btnStart.SetEnabled(allReady);
            if (allReady) _btnStart.RemoveFromClassList("hidden-element");
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
        ToggleReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                var data = NetPlayers[i];
                data.IsReady = !data.IsReady;
                NetPlayers[i] = data;
                break;
            }
        }
    }

    private void OnClientConnected(ulong clientId) {
        if (!IsServer) return;
        string pName = $"Explorer_{clientId}";
        if (!UnityEngine.Application.isBatchMode && clientId == NetworkManager.ServerClientId) 
            pName = PlayerPrefs.GetString("AuthDisplayName", "Host");
        AddPlayer(clientId, pName);
    }

    private void OnClientDisconnected(ulong clientId) {
        if (!IsServer) return;
        for (int i = 0; i < NetPlayers.Count; i++) {
            if (NetPlayers[i].ClientId == clientId) { NetPlayers.RemoveAt(i); break; }
        }
    }

    private void AddPlayer(ulong clientId, string name) {
        // KIỂM TRA CHỐNG TRÙNG: Nếu Client này đã có trong danh sách thì bỏ qua
        foreach (var p in NetPlayers) {
            if (p.ClientId == clientId) return;
        }

        Debug.Log($"[DEBUG] Bắt đầu AddPlayer cho: {name} (ID: {clientId})");

        
        int slotIdx = FindEmptySlot();
        if (slotIdx == -1) {
            Debug.LogError($"[SPAWN] THẤT BẠI: Không còn slot trống!");
            return;
        }

        NetPlayers.Add(new PlayerNetData { Name = name, Slot = slotIdx, ClientId = clientId, IsReady = false });

        if (playerNetworkPrefab != null) {
            Vector3 spawnPos = slots[slotIdx].position;
            Quaternion spawnRot = slots[slotIdx].rotation;
            
            Debug.Log($"[SPAWN] Đang Instantiate tại vị trí: {spawnPos}");

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
        if (IsServer) {
            Debug.Log("[Lobby] All players ready! Starting expedition...");
            // NetworkManager.Singleton.SceneManager.LoadScene("GameplayScene", LoadSceneMode.Single);
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
