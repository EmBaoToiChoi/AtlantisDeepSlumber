using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerWaitingRoomUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text _nameTag; 
    
    [Header("3D Character Models")]
    [Tooltip("Gán 4 GameObject/Mesh của 4 nhân vật tương ứng: 0=Atlas, 1=Nyx, 2=Aurelia, 3=Titan")]
    [SerializeField] private GameObject[] _characterModels = new GameObject[4];

    private NetworkWaitingRoom _manager;
    private int _lastCharId = -1;
    private PlayerVoicePlayback _playback;
    private SpriteRenderer _micSpriteRenderer;

    [Header("Microphone Sprites")]
    [SerializeField] private Sprite _micOnSprite;
    [SerializeField] private Sprite _micOffSprite;

    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "Guest", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> NetIsMicOn = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Start()
    {
        _manager = FindFirstObjectByType<NetworkWaitingRoom>();
        if (_nameTag == null) _nameTag = GetComponentInChildren<TMP_Text>();

        // Tự động tìm kiếm các model/mesh trong con nếu chưa được gán trong Inspector
        if (_characterModels == null || _characterModels.Length == 0 || (_characterModels.Length == 4 && _characterModels[0] == null))
        {
            _characterModels = new GameObject[4];
            Transform childAtlas = transform.Find("Atlas");
            if (childAtlas == null) childAtlas = transform.Find("atlas");
            if (childAtlas != null) _characterModels[0] = childAtlas.gameObject;

            Transform childNyx = transform.Find("Nyx");
            if (childNyx == null) childNyx = transform.Find("nyx");
            if (childNyx != null) _characterModels[1] = childNyx.gameObject;

            Transform childAurelia = transform.Find("Aurelia");
            if (childAurelia == null) childAurelia = transform.Find("aurelia");
            if (childAurelia != null) _characterModels[2] = childAurelia.gameObject;

            Transform childTitan = transform.Find("Titan");
            if (childTitan == null) childTitan = transform.Find("titan");
            if (childTitan != null) _characterModels[3] = childTitan.gameObject;
        }

        // Tự động thêm CapsuleCollider nếu chưa có để hỗ trợ tính năng Raycast Shift + Left Click
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1f, 0);
            col.radius = 0.5f;
            col.height = 2f;
            Debug.Log($"[PlayerUI] Đã tự động thêm CapsuleCollider cho nhân vật ClientId={OwnerClientId} để phục vụ Raycast.");
        }

        // Tự động thêm PlayerVoicePlayback nếu chưa có để hỗ trợ Voice Chat / Mic Game
        _playback = GetComponent<PlayerVoicePlayback>();
        if (_playback == null)
        {
            _playback = gameObject.AddComponent<PlayerVoicePlayback>();
        }
        _playback.ownerClientId = OwnerClientId;
        _playback.isLocalPlayer = (NetworkManager.Singleton != null && OwnerClientId == NetworkManager.Singleton.LocalClientId);
        Debug.Log($"[PlayerUI] Đã tự động cấu hình PlayerVoicePlayback cho ClientId={OwnerClientId}, isLocal={_playback.isLocalPlayer}.");

        // Tạo floating 3D mic icon nếu chưa có
        Transform micIconTransform = _nameTag != null ? _nameTag.transform.Find("MicIcon") : null;
        if (micIconTransform == null && _nameTag != null)
        {
            GameObject micObj = new GameObject("MicIcon");
            micObj.transform.SetParent(_nameTag.transform);
            micObj.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            micObj.transform.localRotation = Quaternion.identity;
            micObj.transform.localScale = new Vector3(0.07f, 0.07f, 1f);
            _micSpriteRenderer = micObj.AddComponent<SpriteRenderer>();
        }
        else if (micIconTransform != null)
        {
            _micSpriteRenderer = micIconTransform.GetComponent<SpriteRenderer>();
        }
    }

    private void Update()
    {
        if (_manager == null || _nameTag == null) return;

        // Tìm trạng thái Ready và nhân vật đã chọn
        int currentCharId = -1;
        string status = "<b><size=70%><color=#ff3860>NOT READY</color></size></b>";
        foreach (var p in _manager.NetPlayers)
        {
            if (p.ClientId == OwnerClientId)
            {
                currentCharId = p.CharacterId;
                if (p.IsReady) status = "<b><size=70%><color=#23d160>READY</color></size></b>";
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

        // Cập nhật NetworkVariable mic cho chính mình
        if (IsOwner)
        {
            bool micOn = MicManager.Instance != null && !MicManager.Instance.IsMuted;
            if (NetIsMicOn.Value != micOn)
            {
                NetIsMicOn.Value = micOn;
            }
        }

        bool isMicOn = NetIsMicOn.Value;
        bool isSpeakingNow = _playback != null && _playback.IsSpeaking;

        // Cập nhật Sprite của mic trên billboard 3D
        if (_micSpriteRenderer != null)
        {
            if (_nameTag != null)
            {
                _nameTag.ForceMeshUpdate();
                var textInfo = _nameTag.textInfo;
                float firstLineHalfWidth = 0.5f;
                float lineY = 0f;
                if (textInfo != null && textInfo.lineCount > 0)
                {
                    firstLineHalfWidth = textInfo.lineInfo[0].length * 0.5f;
                    lineY = (textInfo.lineInfo[0].ascender + textInfo.lineInfo[0].descender) * 0.5f;
                }
                // Đặt vị trí lệch bên phải của dòng tên đầu tiên (tăng khoảng cách margin-left lên 0.22f)
                _micSpriteRenderer.transform.localPosition = new Vector3(firstLineHalfWidth + 0.22f, lineY, 0f);
            }

            // Tính toán localScale động để đảm bảo icon mic có cùng kích thước thế giới (world scale) trên mọi nhân vật
            float baseScale = (currentCharId == 0) ? 0.045f : 0.07f; // Cho riêng LEO (ID 0) icon nhỏ hơn một tí
            float targetWorldScale = baseScale;
            if (isSpeakingNow)
            {
                targetWorldScale = baseScale + Mathf.PingPong(Time.time * 0.5f, baseScale * 0.2f);
            }

            float parentScaleX = _nameTag != null ? _nameTag.transform.lossyScale.x : 1f;
            float parentScaleY = _nameTag != null ? _nameTag.transform.lossyScale.y : 1f;
            float localScaleX = parentScaleX > 0 ? (targetWorldScale / parentScaleX) : targetWorldScale;
            float localScaleY = parentScaleY > 0 ? (targetWorldScale / parentScaleY) : targetWorldScale;

            if (isSpeakingNow)
            {
                _micSpriteRenderer.sprite = _micOnSprite;
                _micSpriteRenderer.color = new Color(0f, 1f, 0f, 1f); // Màu xanh lá khi nói
                _micSpriteRenderer.transform.localScale = new Vector3(localScaleX, localScaleY, 1f);
            }
            else if (isMicOn)
            {
                _micSpriteRenderer.sprite = _micOnSprite;
                _micSpriteRenderer.color = new Color(1f, 1f, 1f, 0.8f); // Màu bình thường khi mở mic
                _micSpriteRenderer.transform.localScale = new Vector3(localScaleX, localScaleY, 1f);
            }
            else
            {
                _micSpriteRenderer.sprite = _micOffSprite;
                _micSpriteRenderer.color = new Color(1f, 1f, 1f, 0.4f); // Làm mờ khi tắt mic
                _micSpriteRenderer.transform.localScale = new Vector3(localScaleX, localScaleY, 1f);
            }
        }

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
            case 0: return "LEO";
            case 1: return "MAYA";
            case 2: return "ELENA";
            case 3: return "ARTHUR";
            default: return "SELECTING...";
        }
    }

    private void OnCharacterChanged(int newCharId)
    {
        Debug.Log($"[PlayerUI] ClientId={OwnerClientId} đã chuyển sang nhân vật {GetCharacterName(newCharId)} (ID: {newCharId})");
        
        if (_characterModels == null || _characterModels.Length == 0) return;

        // Bật GameObject của nhân vật được chọn và tắt tất cả các nhân vật khác
        for (int i = 0; i < _characterModels.Length; i++)
        {
            if (_characterModels[i] != null)
            {
                _characterModels[i].SetActive(i == newCharId);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_micOnSprite == null)
        {
            _micOnSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hbao/Image/Mic 1.png");
        }
        if (_micOffSprite == null)
        {
            _micOffSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hbao/Image/Mic - Copy.png");
        }
    }
#endif
}
