using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerWaitingRoomUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text _nameTag; 
    private NetworkWaitingRoom _manager;
    private int _lastCharId = -1;

    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Guest", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Start()
    {
        _manager = FindFirstObjectByType<NetworkWaitingRoom>();
        if (_nameTag == null) _nameTag = GetComponentInChildren<TMP_Text>();

        // Tự động thêm CapsuleCollider nếu chưa có để hỗ trợ tính năng Raycast Shift + Left Click
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1f, 0);
            col.radius = 0.5f;
            col.height = 2f;
            Debug.Log($"[PlayerUI] Đã tự động thêm CapsuleCollider cho nhân vật ClientId={OwnerClientId} để phục vụ Raycast.");
        }
    }

    private void Update()
    {
        if (_manager == null || _nameTag == null) return;

        // Tìm trạng thái Ready và nhân vật đã chọn
        int currentCharId = -1;
        string status = "<b><size=70%><mark=#ff386033><color=#ff3860>  ● NOT READY  </color></mark></size></b>";
        foreach (var p in _manager.NetPlayers)
        {
            if (p.ClientId == OwnerClientId)
            {
                currentCharId = p.CharacterId;
                if (p.IsReady) status = "<b><size=70%><mark=#23d16033><color=#23d160>  ▲ READY  </color></mark></size></b>";
                break;
            }
        }

        // Nếu nhân vật thay đổi, kích hoạt hook sự kiện đổi mesh 3D
        if (currentCharId != _lastCharId)
        {
            OnCharacterChanged(currentCharId);
            _lastCharId = currentCharId;
        }

        // Tự động ẩn/hiện Mesh Renderers tùy theo trạng thái đã chọn hay chưa (ẩn khi = -1)
        SetMeshVisibility(currentCharId != -1);

        // Tên hiển thị màu Cyan Neon bắt mắt kết hợp với tên nhân vật trong ngoặc đơn và khung trạng thái
        string charSub = currentCharId >= 0 && currentCharId < 4 ? GetCharacterName(currentCharId) : "SELECTING...";
        _nameTag.text = $"<color=#00e5ff><b>{NetName.Value}</b></color> <size=80%><color=#80c8ff>({charSub})</color></size>\n\n{status}";

        // Cách xoay Billboard chuẩn nhất: Xoay cùng hướng với Camera
        if (Camera.main != null)
        {
            _nameTag.transform.rotation = Camera.main.transform.rotation;
        }
    }

    private void SetMeshVisibility(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            // Không làm ẩn name tag / billboard text
            if (_nameTag != null && (r.gameObject == _nameTag.gameObject || r.transform.IsChildOf(_nameTag.transform)))
            {
                continue;
            }
            r.enabled = visible;
        }
    }

    private string GetCharacterName(int id)
    {
        switch (id)
        {
            case 0: return "ATLAS";
            case 1: return "NYX";
            case 2: return "AURELIA";
            case 3: return "TITAN";
            default: return "SELECTING...";
        }
    }

    private void OnCharacterChanged(int newCharId)
    {
        Debug.Log($"[PlayerUI] ClientId={OwnerClientId} đã chuyển sang nhân vật {GetCharacterName(newCharId)} (ID: {newCharId})");
        
        // HOOK ĐỂ DEV THAY ĐỔI MESH 3D SAU NÀY:
        // switch (newCharId) {
        //     case 0: ActiveAtlasMesh(); break;
        //     case 1: ActiveNyxMesh(); break;
        //     ...
        // }
    }
}
