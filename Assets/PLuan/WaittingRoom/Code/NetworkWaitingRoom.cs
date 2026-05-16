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

    private void Awake() { NetPlayers = new NetworkList<PlayerNetData>(); }

    private void OnEnable()
    {
        StartCoroutine(SetupUIWithRetry());
    }

    private IEnumerator SetupUIWithRetry()
    {
        yield return new WaitForSeconds(0.1f);
        if (_uiDocument == null) yield break;

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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            NetRoomName.Value = PlayerPrefs.GetString("CurrentRoomName", "ATLANTIS LOBBY");
            NetRoomId.Value = PlayerPrefs.GetString("CurrentRoomID", "000000");

            // QUAN TRỌNG: Spawn luôn nhân vật cho Host vì Host kết nối trước khi callback kịp đăng ký
            if (!UnityEngine.Application.isBatchMode) // Nếu không phải Dedicated Server (là Host thật)
            {
                OnClientConnected(NetworkManager.ServerClientId);
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
        int slotIdx = FindEmptySlot();
        if (slotIdx == -1) {
            Debug.LogError($"[SPAWN] Không tìm thấy slot trống cho {name}");
            return;
        }

        NetPlayers.Add(new PlayerNetData { Name = name, Slot = slotIdx, ClientId = clientId, IsReady = false });
        Debug.Log($"[SPAWN] Đã thêm {name} vào danh sách NetPlayers. Đang chuẩn bị tạo nhân vật...");

        if (playerNetworkPrefab != null) {
            GameObject go = Instantiate(playerNetworkPrefab, slots[slotIdx].position, slots[slotIdx].rotation);
            go.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
            Debug.Log($"[SPAWN] Thành công! Đã tạo nhân vật cho {name} tại Slot {slotIdx}");
        }
        else {
            Debug.LogError("[SPAWN] THẤT BẠI: Bạn chưa kéo nhân vật vào ô 'Player Network Prefab' trong Inspector!");
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
