using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class NetworkWaitingRoom : NetworkBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument _uiDocument;
    
    [Header("Slots (Transform points)")]
    public Transform[] slots = new Transform[4];

    [Header("Prefabs")]
    public GameObject playerNetworkPrefab; 

    // NetworkVariables để đồng bộ thông tin phòng từ Server xuống Client
    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetRoomName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "ATLANTIS EXPEDITION", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetRoomId = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "XXXXXX", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private VisualElement _root;
    private Label _lblRoomName;
    private Label _lblRoomId;
    private Label _lblPlayerCount;
    private Button _btnStart;
    private Button _btnLeave;

    // NetworkList để đồng bộ danh sách người chơi
    private NetworkList<PlayerNetData> _netPlayers = new NetworkList<PlayerNetData>();

    public struct PlayerNetData : INetworkSerializable, System.IEquatable<PlayerNetData>
    {
        public Unity.Collections.FixedString64Bytes Name;
        public int Slot;
        public ulong ClientId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref ClientId);
        }

        public bool Equals(PlayerNetData other) => ClientId == other.ClientId;
    }

    private void Awake()
    {
        // NetworkList cần được khởi tạo nếu chưa
        _netPlayers = new NetworkList<PlayerNetData>();
    }

    private void OnEnable()
    {
        if (_uiDocument == null) {
            Debug.LogError("[WAITING ROOM] UI Document is NOT assigned!");
            return;
        }
        _root = _uiDocument.rootVisualElement;
        
        // Tìm theo Name
        _lblRoomName = _root.Q<Label>("lbl-room-name");
        _lblRoomId = _root.Q<Label>("lbl-room-id");
        _lblPlayerCount = _root.Q<Label>("lbl-player-count");
        _btnStart = _root.Q<Button>("btn-start");
        _btnLeave = _root.Q<Button>("btn-leave");

        // Nếu không tìm thấy theo Name, thử tìm theo Class (đề phòng bạn đặt tên khác)
        if (_lblRoomName == null) _lblRoomName = _root.Q<Label>(className: "waiting-room-name");
        if (_lblRoomId == null) _lblRoomId = _root.Q<Label>(className: "waiting-room-id");

        if (_lblRoomName == null) Debug.LogWarning("[WAITING ROOM] Cảnh báo: Không tìm thấy Label tên 'lbl-room-name' hoặc class 'waiting-room-name'!");

        if (_btnLeave != null) _btnLeave.clicked += LeaveRoom;
        if (_btnStart != null) _btnStart.clicked += StartGame;
    }



    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // Ở bản Dedicated Server VPS, ta có thể hardcode hoặc lấy từ một hệ thống quản lý phòng
            // Tạm thời set giá trị demo để bạn thấy nó hiện lên
            NetRoomName.Value = "ATLANTIS DEEP LOBBY";
            NetRoomId.Value = "VPS-7777";

            // Add Host (Nếu Server là Host, nhưng đây là Dedicated Server nên thường không có Host cục bộ)
        }

        // Đăng ký callback cập nhật UI khi biến mạng thay đổi
        NetRoomName.OnValueChanged += (oldV, newV) => UpdateRoomUI();
        NetRoomId.OnValueChanged += (oldV, newV) => UpdateRoomUI();
        _netPlayers.OnListChanged += (changeEvent) => UpdatePlayerUI();

        UpdateRoomUI();
        UpdatePlayerUI();
    }

    private void UpdateRoomUI()
    {
        if (_lblRoomName != null) _lblRoomName.text = $"SESSION: {NetRoomName.Value.ToString().ToUpper()}";
        if (_lblRoomId != null) _lblRoomId.text = $"ID: #{NetRoomId.Value.ToString()}";
    }

    private void UpdatePlayerUI()
    {
        if (_lblPlayerCount != null) _lblPlayerCount.text = $"PLAYERS: {_netPlayers.Count}/4";
        if (_btnStart != null) _btnStart.style.display = IsServer ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        
        // Trên VPS (Dedicated Server) không có PlayerPrefs, nên ta dùng tên mặc định hoặc lấy từ ConnectionData
        string pName = $"Explorer_{clientId}";
        
        // Nếu là Host (Local Host) thì mới có PlayerPrefs
        if (!UnityEngine.Application.isBatchMode && clientId == NetworkManager.ServerClientId) {
            pName = PlayerPrefs.GetString("AuthDisplayName", "Host");
        }

        AddPlayer(clientId, pName);
    }


    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = 0; i < _netPlayers.Count; i++)
        {
            if (_netPlayers[i].ClientId == clientId)
            {
                _netPlayers.RemoveAt(i);
                break;
            }
        }
    }

    private void AddPlayer(ulong clientId, string name)
    {
        int slotIdx = FindEmptySlot();
        if (slotIdx == -1) return;

        _netPlayers.Add(new PlayerNetData 
        { 
            Name = name, 
            Slot = slotIdx, 
            ClientId = clientId 
        });

        GameObject go = Instantiate(playerNetworkPrefab, slots[slotIdx].position, slots[slotIdx].rotation);
        go.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < 4; i++)
        {
            bool occupied = false;
            foreach (var p in _netPlayers) if (p.Slot == i) occupied = true;
            if (!occupied) return i;
        }
        return -1;
    }

    private void StartGame()
    {
        if (!IsServer) return;
        Debug.Log("[Lobby] Starting Game Expedition...");
        // NetworkManager.Singleton.SceneManager.LoadScene("GameplayScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private async void LeaveRoom()
    {
        string roomId = PlayerPrefs.GetString("CurrentRoomID", "");

        if (!string.IsNullOrEmpty(roomId))
        {
            Debug.Log($"[Lobby] Leaving room {roomId}...");
            await AuthService.LeaveRoom(roomId);
        }

        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }


}
