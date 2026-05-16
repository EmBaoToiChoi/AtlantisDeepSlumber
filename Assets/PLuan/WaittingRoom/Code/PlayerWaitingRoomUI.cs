using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerWaitingRoomUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text _nameTag; 
    private NetworkWaitingRoom _manager;

    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Guest", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Start()
    {
        _manager = FindFirstObjectByType<NetworkWaitingRoom>();
        if (_nameTag == null) _nameTag = GetComponentInChildren<TMP_Text>();
    }

    private void Update()
    {
        if (_manager == null || _nameTag == null) return;

        // Tìm trạng thái Ready trong danh sách mạng
        string status = "<color=#ff4d4d>[NOT READY]</color>";
        foreach (var p in _manager.NetPlayers)
        {
            if (p.ClientId == OwnerClientId)
            {
                if (p.IsReady) status = "<color=#32ff7e>[READY]</color>";
                break;
            }
        }

        // Hiển thị tên từ biến mạng riêng biệt
        _nameTag.text = $"{NetName.Value}\n{status}";

        // Cách xoay Billboard chuẩn nhất: Xoay cùng hướng với Camera
        if (Camera.main != null)
        {
            _nameTag.transform.rotation = Camera.main.transform.rotation;
        }
    }

}
