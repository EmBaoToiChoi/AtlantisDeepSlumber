using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using TMPro;

public class NetworkWaitingRoom : NetworkBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument _uiDocument;
    
    [Header("Slots")]
    public Transform[] slots = new Transform[4];

    [Header("Prefabs")]
    public GameObject playerNetworkPrefab; // Prefab có NetworkObject

    private VisualElement _root;
    private Label _lblRoomName;
    private Label _lblRoomId;
    private Label _lblPlayerCount;
    private Button _btnStart;
    private Button _btnLeave;

    // Đồng bộ danh sách người chơi qua Network
    private NetworkList<PlayerNetData> _players = new NetworkList<PlayerNetData>();

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

        _btnLeave.clicked += LeaveRoom;
        _btnStart.clicked += StartGame;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // Add host
            AddPlayer(NetworkManager.ServerClientId, PlayerPrefs.GetString("AuthDisplayName", "Host"));
        }

        _players.OnListChanged += (changeEvent) => UpdateUI();
        UpdateUI();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        // Trong thực tế, bạn sẽ lấy tên từ Auth hoặc Metadata khi kết nối
        AddPlayer(clientId, $"Player {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].ClientId == clientId)
            {
                _players.RemoveAt(i);
                break;
            }
        }
    }

    private void AddPlayer(ulong clientId, string name)
    {
        int slot = FindEmptySlot();
        if (slot == -1) return;

        _players.Add(new PlayerNetData 
        { 
            Name = name, 
            Slot = slot, 
            ClientId = clientId 
        });

        // Spawn nhân vật tại slot
        GameObject go = Instantiate(playerNetworkPrefab, slots[slot].position, slots[slot].rotation);
        go.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < 4; i++)
        {
            bool occupied = false;
            foreach (var p in _players) if (p.Slot == i) occupied = true;
            if (!occupied) return i;
        }
        return -1;
    }

    private void UpdateUI()
    {
        if (_lblPlayerCount != null) _lblPlayerCount.text = $"PLAYERS: {_players.Count}/4";
        _btnStart.style.display = IsHost ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void StartGame()
    {
        if (!IsHost) return;
        Debug.Log("Starting Game...");
        // NetworkManager.Singleton.SceneManager.LoadScene("MainGame", LoadSceneMode.Single);
    }

    private void LeaveRoom()
    {
        NetworkManager.Singleton.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
