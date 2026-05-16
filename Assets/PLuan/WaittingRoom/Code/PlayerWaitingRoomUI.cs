using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerWaitingRoomUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text _nameTag; 
    private NetworkWaitingRoom _manager;

    private void Start()
    {
        _manager = FindFirstObjectByType<NetworkWaitingRoom>();
        if (_nameTag == null) _nameTag = GetComponentInChildren<TMP_Text>();
    }

    private void Update()
    {
        if (_manager == null || _nameTag == null) return;

        // Tìm dữ liệu của mình trong danh sách mạng
        foreach (var p in _manager.NetPlayers)
        {
            if (p.ClientId == OwnerClientId)
            {
                string status = p.IsReady ? "<color=#32ff7e>[READY]</color>" : "<color=#ff4d4d>[NOT READY]</color>";
                _nameTag.text = $"{p.Name}\n{status}";
                break;
            }
        }

        // Cách xoay Billboard chuẩn nhất: Xoay cùng hướng với Camera
        if (Camera.main != null)
        {
            _nameTag.transform.rotation = Camera.main.transform.rotation;
        }
    }

}
