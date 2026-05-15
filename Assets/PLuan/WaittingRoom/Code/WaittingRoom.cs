using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using TMPro;
using System.Threading.Tasks;



public class WaittingRoom : MonoBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument _uiDocument;
    
    [Header("Room Config")]
    public string currentRoomId;
    public float pollInterval = 2.0f;

    [Header("Slots (Transform points)")]
    public Transform[] slots = new Transform[4];

    [Header("Prefabs")]
    public GameObject playerPrefab; 
    
    private VisualElement _root;
    private Label _lblRoomName;
    private Label _lblRoomId;
    private Label _lblPlayerCount;
    private Button _btnStart;
    private Button _btnLeave;

    private Dictionary<int, GameObject> _spawnedPlayers = new Dictionary<int, GameObject>();
    private bool _isPolling = false;

    private void OnEnable()
    {
        if (_uiDocument == null) return;
        _root = _uiDocument.rootVisualElement;

        _lblRoomName = _root.Q<Label>("lbl-room-name");
        _lblRoomId = _root.Q<Label>("lbl-room-id");
        _lblPlayerCount = _root.Q<Label>("lbl-player-count");
        _btnStart = _root.Q<Button>("btn-start");
        _btnLeave = _root.Q<Button>("btn-leave");

        _btnLeave.clicked += () => FindObjectOfType<AtlantisMenuController>()?.LeaveRoom();
    }

    public void StartWaiting(RoomData room)
    {
        currentRoomId = room.roomId;
        
        // Cập nhật UI ban đầu
        if (_lblRoomName != null) _lblRoomName.text = $"SESSION: {room.roomName.ToUpper()}";
        if (_lblRoomId != null) _lblRoomId.text = $"ID: #{room.roomId}";
        UpdatePlayerCountLabel(room.players.Length, room.maxPlayers);

        // Chỉ host mới thấy nút Start (giả sử host có ID trùng với room.host)
        string myUserId = PlayerPrefs.GetString("AuthToken"); // Hoặc lấy từ một nơi quản lý State
        // _btnStart.ToggleInClassList("hidden-element", room.host != currentUserId);

        gameObject.SetActive(true);
        _isPolling = true;
        PollRoomStatus();
    }


    public void StopWaiting()
    {
        _isPolling = false;
        ClearAllSlots();
    }

    private async void PollRoomStatus()
    {
        while (_isPolling)
        {
            if (string.IsNullOrEmpty(currentRoomId)) break;

            var response = await AuthService.GetRoomStatus(currentRoomId);
            if (response != null && response.success)
            {
                UpdatePlayers(response.room.players);
                UpdatePlayerCountLabel(response.room.players.Length, response.room.maxPlayers);
            }

            await Task.Delay((int)(pollInterval * 1000));
        }
    }

    private void UpdatePlayerCountLabel(int count, int max)
    {
        if (_lblPlayerCount != null) _lblPlayerCount.text = $"PLAYERS: {count}/{max}";
    }


    private void UpdatePlayers(RoomPlayer[] players)
    {
        if (players == null) return;
        
        HashSet<int> activeSlots = new HashSet<int>();

        foreach (var p in players)
        {
            activeSlots.Add(p.slot);

            if (!_spawnedPlayers.ContainsKey(p.slot))
            {
                SpawnPlayer(p);
            }
            else
            {
                UpdateNameTag(p.slot, p.displayName);
            }
        }

        List<int> slotsToRemove = new List<int>();
        foreach (var slot in _spawnedPlayers.Keys)
        {
            if (!activeSlots.Contains(slot))
            {
                slotsToRemove.Add(slot);
            }
        }

        foreach (var slot in slotsToRemove)
        {
            DespawnPlayer(slot);
        }
    }

    private void SpawnPlayer(RoomPlayer p)
    {
        if (p.slot < 1 || p.slot > 4) return;
        Transform slotTransform = slots[p.slot - 1];
        if (slotTransform == null) return;

        GameObject model = Instantiate(playerPrefab, slotTransform.position, slotTransform.rotation, slotTransform);
        _spawnedPlayers[p.slot] = model;

        // Lưu ý: Phần hiển thị tên trên đầu model vẫn có thể dùng TextMeshPro 
        // hoặc dùng UI Toolkit overlay nếu bạn muốn đồng bộ 100%.
        // Hiện tại tôi giữ nguyên model nhưng bạn có thể thêm logic UI Toolkit ở đây.
        
        Debug.Log($"[Lobby] {p.displayName} đã vào Slot {p.slot}");
    }


    private void UpdateNameTag(int slot, string displayName)
    {
        if (_spawnedPlayers.TryGetValue(slot, out GameObject model))
        {
            var tmp = model.GetComponentInChildren<TextMeshPro>();
            if (tmp != null) tmp.text = displayName;
        }
    }

    private void DespawnPlayer(int slot)
    {
        if (_spawnedPlayers.TryGetValue(slot, out GameObject model))
        {
            Destroy(model);
            _spawnedPlayers.Remove(slot);
            Debug.Log($"[Lobby] Player thoát Slot {slot}");
        }
    }

    private void ClearAllSlots()
    {
        foreach (var model in _spawnedPlayers.Values)
        {
            if (model != null) Destroy(model);
        }
        _spawnedPlayers.Clear();
    }
}
