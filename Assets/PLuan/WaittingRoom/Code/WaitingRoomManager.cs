using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;

public class WaitingRoomManager : MonoBehaviour
{
    [Header("Room Config")]
    public string currentRoomId;
    public float pollInterval = 2.0f;

    [Header("Slots (Transform points)")]
    public Transform[] slots = new Transform[4];

    [Header("Prefabs")]
    public GameObject playerPrefab; // Kéo thả prefab nhân vật vào đây
    public GameObject nameTagPrefab; // Prefab chứa TextMeshPro (World Space)

    private Dictionary<int, GameObject> _spawnedPlayers = new Dictionary<int, GameObject>();
    private bool _isPolling = false;

    public void StartWaiting(string roomId)
    {
        currentRoomId = roomId;
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
            }

            await Task.Delay((int)(pollInterval * 1000));
        }
    }

    private void UpdatePlayers(RoomPlayer[] players)
    {
        // Danh sách các slot đang có người trong response
        HashSet<int> activeSlots = new HashSet<int>();

        foreach (var p in players)
        {
            activeSlots.Add(p.slot);

            // Nếu chưa có model ở slot này, spawn mới
            if (!_spawnedPlayers.ContainsKey(p.slot))
            {
                SpawnPlayer(p);
            }
            else
            {
                // Cập nhật tên nếu cần (trường hợp đổi tên hoặc logic khác)
                UpdateNameTag(p.slot, p.displayName);
            }
        }

        // Xóa những người không còn trong danh sách (đã thoát)
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

        // Tạo Name Tag
        if (nameTagPrefab != null)
        {
            GameObject nameTag = Instantiate(nameTagPrefab, model.transform);
            // Vị trí trên đầu (giả định y = 2.0f, có thể điều chỉnh tùy model)
            nameTag.transform.localPosition = new Vector3(0, 2.2f, 0);
            
            var tmp = nameTag.GetComponentInChildren<TextMeshPro>();
            if (tmp != null)
            {
                tmp.text = p.displayName;
                // Nếu là slot 1 (chủ phòng), có thể thêm icon hoặc màu khác
                if (p.slot == 1) tmp.color = Color.yellow; 
            }
        }

        Debug.Log($"[Lobby] Spawned {p.displayName} at Slot {p.slot}");
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
            Debug.Log($"[Lobby] Player left Slot {slot}");
        }
    }

    private void ClearAllSlots()
    {
        foreach (var model in _spawnedPlayers.Values)
        {
            Destroy(model);
        }
        _spawnedPlayers.Clear();
    }
}
