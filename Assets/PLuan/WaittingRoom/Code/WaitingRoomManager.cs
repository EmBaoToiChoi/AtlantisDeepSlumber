using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using TMPro;

public class WaitingRoomManager : NetworkBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument _uiDocument;
    
    [Header("Slots (Transform points)")]
    public Transform[] slots = new Transform[4];

    [Header("Prefabs")]
    public GameObject playerNetworkPrefab; 

    // NetworkVariables để đồng bộ thông tin phòng
    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetRoomName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetRoomId = new NetworkVariable<Unity.Collections.FixedString64Bytes>(writePerm: NetworkVariableWritePermission.Server);

    private VisualElement _root;
    private Label _lblRoomName;
    private Label _lblRoomId;
    private Label _lblPlayerCount;
    private Button _btnStart;
    private Button _btnLeave;

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

    private void OnEnable()
    {
        if (_uiDocument == null) return;
        _root = _uiDocument.rootVisualElement;
        _lblRoomName = _root.Q<Label>("lbl-room-name");
        _lblRoomId = _root.Q<Label>("lbl-room-id");
        _lblPlayerCount = _root.Q<Label>("lbl-player-count");
        _btnStart = _root.Q<Button>("btn-start");
        _btnLeave = _root.Q<Button>("btn-leave");

        if (_btnLeave != null) _btnLeave.clicked += LeaveRoom;
        if (_btnStart != null) _btnStart.clicked += StartGame;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // Server (VPS) nên lấy thông tin từ DB hoặc truyền qua ConnectionData
            // Tạm thời set demo nếu chưa có hệ thống truyền dữ liệu phức tạp
            if (string.IsNullOrEmpty(NetRoomName.Value.ToString()))
            {
                NetRoomName.Value = "ATLANTIS EXPEDITION";
                NetRoomId.Value = "VPSDEDICATED";
            }
        }

        // Đăng ký callback khi NetworkVariable thay đổi (cho Client cập nhật UI)
        NetRoomName.OnValueChanged += (oldVal, newVal) => UpdateRoomUI();
        NetRoomId.OnValueChanged += (oldVal, newVal) => UpdateRoomUI();
        
        _netPlayers.OnListChanged += (changeEvent) => UpdateUI();
        
        UpdateRoomUI();
        UpdateUI();
    }

    private void UpdateRoomUI()
    {
        if (_lblRoomName != null) _lblRoomName.text = $"SESSION: {NetRoomName.Value.ToString().ToUpper()}";
        if (_lblRoomId != null) _lblRoomId.text = $"ID: #{NetRoomId.Value.ToString()}";
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        string pName = (clientId == NetworkManager.ServerClientId) ? PlayerPrefs.GetString("AuthDisplayName", "Host") : $"Explorer_{clientId}";
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

    private void UpdateUI()
    {
        if (_lblPlayerCount != null) _lblPlayerCount.text = $"PLAYERS: {_netPlayers.Count}/4";
        if (_btnStart != null) _btnStart.style.display = IsHost ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void StartGame()
    {
        if (!IsHost) return;
        Debug.Log("[Lobby] Starting Game Expedition...");
    }

    private void LeaveRoom()
    {
        NetworkManager.Singleton.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
