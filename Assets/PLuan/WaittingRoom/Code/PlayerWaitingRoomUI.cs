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

        // Tìm trạng thái Ready với phong cách Sci-Fi cao cấp có viền khung phát sáng (Capsule Background - dùng tag <mark>)
        string status = "<b><size=70%><mark=#ff386033><color=#ff3860>  ● NOT READY  </color></mark></size></b>";
        foreach (var p in _manager.NetPlayers)
        {
            if (p.ClientId == OwnerClientId)
            {
                if (p.IsReady) status = "<b><size=70%><mark=#23d16033><color=#23d160>  ▲ READY  </color></mark></size></b>";
                break;
            }
        }

        // Tên hiển thị màu Cyan Neon bắt mắt kết hợp với khung trạng thái
        _nameTag.text = $"<color=#00e5ff><b>{NetName.Value}</b></color>\n\n{status}";

        // Cách xoay Billboard chuẩn nhất: Xoay cùng hướng với Camera
        if (Camera.main != null)
        {
            _nameTag.transform.rotation = Camera.main.transform.rotation;
        }
    }

}
