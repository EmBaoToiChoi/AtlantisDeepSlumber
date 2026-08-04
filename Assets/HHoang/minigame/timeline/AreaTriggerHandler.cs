using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class AreaTriggerHandler : NetworkBehaviour
{
    [Header("List Objects To Toggle")]
    [Tooltip("Kéo các GameObject muốn ĐẢO TRẠNG THÁI (bật -> tắt, tắt -> bật) khi đủ người vào đây")]
    public List<GameObject> objectsToToggle = new List<GameObject>();

    [Header("Settings")]
    [Tooltip("Chỉ kích hoạt đúng 1 lần duy nhất trong cả trận")]
    public bool triggerOnlyOnce = true;

    // Biến đồng bộ trạng thái đã kích hoạt chưa
    private NetworkVariable<bool> hasTriggered = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    // Danh sách lưu trữ ClientId của những người chơi ĐÃ CHECK-IN (Chạm vào là lưu luôn)
    private HashSet<ulong> checkedInPlayers = new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        hasTriggered.OnValueChanged += OnHasTriggeredChanged;
    }

    public override void OnNetworkDespawn()
    {
        hasTriggered.OnValueChanged -= OnHasTriggeredChanged;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ Server/Host mới xử lý logic tính toán
        if (!IsServer || (triggerOnlyOnce && hasTriggered.Value)) return;

        if (IsPlayerObject(other, out ulong clientId))
        {
            // Thêm người chơi vào danh sách điểm danh (HashSet tự bỏ qua nếu đã có sẵn)
            if (checkedInPlayers.Add(clientId))
            {
                Debug.Log($"[AreaTrigger] Player ID {clientId} đã check-in chạm Box!");
                CheckTriggerConditionServer();
            }
        }
    }

    // KHÔNG CÓ OnTriggerExit Ở ĐÂY NỮA
    // Để khi người chơi đi ra ngoài, ID của họ vẫn nằm trong checkedInPlayers

    private void CheckTriggerConditionServer()
    {
        // Dọn dẹp những ClientId đã disconnect/thoát phòng
        checkedInPlayers.RemoveWhere(id => NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));

        // Lấy tổng số người chơi hiện tại trong Phòng
        int totalPlayersInRoom = NetworkManager.Singleton.ConnectedClientsList.Count;

        // Công thức: 4 người cần 3, 3 cần 2, 2 cần 1, 1 cần 1
        int requiredPlayers = Mathf.Max(1, totalPlayersInRoom - 1);

        Debug.Log($"[AreaTrigger] Đã check-in: {checkedInPlayers.Count}/{requiredPlayers} (Tổng user trong phòng: {totalPlayersInRoom})");

        if (checkedInPlayers.Count >= requiredPlayers)
        {
            // Gán Value = true sẽ tự động kích hoạt sự kiện OnHasTriggeredChanged đồng bộ cho tất cả Client
            hasTriggered.Value = true;
        }
    }

    private void OnHasTriggeredChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            ApplyToggleObjects();
        }
    }

    private void ApplyToggleObjects()
    {
        foreach (var obj in objectsToToggle)
        {
            if (obj != null)
            {
                // Đảo ngược trạng thái: Đang Bật -> Tắt, Đang Tắt -> Bật
                obj.SetActive(!obj.activeSelf);
            }
        }
    }

    // Kiểm tra collider va chạm có phải là Player do Netcode quản lý hay không
    private bool IsPlayerObject(Collider col, out ulong clientId)
    {
        clientId = 0;
        if (col == null) return false;

        var netObj = col.GetComponentInParent<NetworkObject>();
        if (netObj != null && netObj.IsPlayerObject)
        {
            clientId = netObj.OwnerClientId;
            return true;
        }

        return false;
    }
}