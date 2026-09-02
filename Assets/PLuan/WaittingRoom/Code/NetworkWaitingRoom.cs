using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class NetworkWaitingRoom : NetworkBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument _uiDocument;

    [Header("Gameplay Scene Configuration")]
    [SerializeField] private string gameplaySceneName = "MapFinal";
    
    [Header("Slots & Prefabs")]
    public Transform[] slots = new Transform[4];
    public GameObject playerNetworkPrefab; // Default / Atlas
    public GameObject playerNetworkPrefab2; // Nyx
    public GameObject playerNetworkPrefab3; // Aurelia
    public GameObject playerNetworkPrefab4; // Titan

    [Header("Mr. Bean Overhead Spotlight Beams")]
    [Tooltip("Bật hiệu ứng chùm sáng giáng từ trên đầu xuống khi chọn nhân vật (kiểu Mr. Bean)")]
    [SerializeField] private bool enableSpotlightBeams = true;
    [SerializeField] private float spotlightHeight = 8.5f;
    [SerializeField] private float spotlightIntensity = 1.8f;
    [SerializeField] private bool useCharacterTheming = false;

    private LobbySpotlightBeam[] _slotBeams = new LobbySpotlightBeam[4];
    private int[] _lastSlotCharacterIds = new int[] { -1, -1, -1, -1 };

    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetRoomName = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "ATLANTIS EXPEDITION", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<Unity.Collections.FixedString64Bytes> NetRoomId = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
        "XXXXXX", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private VisualElement _root;
    private Label _lblRoomName;
    private Label _lblRoomId;
    private Label _lblPlayerCount;
    private Label _lblWarning;
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
        public int CharacterId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref CharacterId);
        }
        public bool Equals(PlayerNetData other) 
        {
            return ClientId == other.ClientId && 
                   IsReady == other.IsReady && 
                   Name.Equals(other.Name) && 
                   Slot == other.Slot &&
                   CharacterId == other.CharacterId;
        }
    }

    [System.Serializable]
    public struct LobbySkillInfo
    {
        public string Name;
        public string Description;
        public string IconStyleClass;
    }

    [System.Serializable]
    public struct LobbyCharacterInfo
    {
        public string Name;
        public string Role;
        public string Description;
        public string PortraitStyleClass;
        public LobbySkillInfo[] Skills;
    }

    private readonly LobbyCharacterInfo[] _characters = new LobbyCharacterInfo[]
    {
        new LobbyCharacterInfo
        {
            Name = "LEO",
            Role = "SÁT THỦ BÓNG ĐÊM",
            Description = "Bậc thầy bóng tối và ám sát, Leo có thể hạ gục các mục tiêu quan trọng trước khi chúng kịp nhận ra sự hiện diện của anh.",
            PortraitStyleClass = "leo-img",
            Skills = new LobbySkillInfo[]
            {
                new LobbySkillInfo { Name = "Lướt Nhanh", Description = "Lướt về phía trước nhanh chóng, chém xuyên qua tất cả kẻ địch trên đường đi.", IconStyleClass = "leo-skill-0" },
                new LobbySkillInfo { Name = "Màn Khói Bóng Đêm", Description = "Tạo ra màn khói để tàng hình và tăng tốc độ di chuyển.", IconStyleClass = "leo-skill-1" },
                new LobbySkillInfo { Name = "Ảo Ảnh Chém", Description = "Thực hiện cú chém kết liễu chí mạng, gây sát thương vật lý khổng lồ lên mục tiêu.", IconStyleClass = "leo-skill-2" }
            }
        },
        new LobbyCharacterInfo
        {
            Name = "MAYA",
            Role = "PHÁP SƯ LỬA",
            Description = "Pháp sư hỗ trợ hùng mạnh làm chủ sức mạnh của lửa để thiêu rụi kẻ thù và bảo vệ đồng đội.",
            PortraitStyleClass = "maya-img",
            Skills = new LobbySkillInfo[]
            {
                new LobbySkillInfo { Name = "Cầu Lửa", Description = "Bắn ra một quả cầu lửa rực cháy gây sát thương diện rộng lên kẻ địch.", IconStyleClass = "maya-skill-0" },
                new LobbySkillInfo { Name = "Tăng Cường", Description = "Bao bọc đồng đội bằng lá chắn nhiệt bảo vệ, giảm sát thương nhận vào.", IconStyleClass = "maya-skill-1" },
                new LobbySkillInfo { Name = "Tân Tinh Lửa", Description = "Kích hoạt vụ nổ lửa khổng lồ thiêu rụi các mục tiêu xung quanh và hồi máu cho đồng đội.", IconStyleClass = "maya-skill-2" }
            }
        },
        new LobbyCharacterInfo
        {
            Name = "ELENA",
            Role = "CUNG THỦ TINH LINH",
            Description = "Với độ chính xác vô song, Elena trút mưa tên xuống kẻ thù từ một khoảng cách an toàn.",
            PortraitStyleClass = "elena-img",
            Skills = new LobbySkillInfo[]
            {
                new LobbySkillInfo { Name = "Mưa Tên Liên Hoàn", Description = "Bắn liên tiếp các mũi tên xuyên thấu theo hình nón phía trước.", IconStyleClass = "elena-skill-0" },
                new LobbySkillInfo { Name = "Bắt Tẩy", Description = "Tăng cường năng lượng linh hồn vào mũi tên tiếp theo, làm choáng mục tiêu.", IconStyleClass = "elena-skill-1" },
                new LobbySkillInfo { Name = "Bão Tên Tinh Tú", Description = "Triệu hồi mưa tên tinh tú liên tục gây sát thương lên kẻ địch trong vùng ảnh hưởng.", IconStyleClass = "elena-skill-2" }
            }
        },
        new LobbyCharacterInfo
        {
            Name = "ARTHUR",
            Role = "ĐẤU SĨ HOÀNG GIA",
            Description = "Chiến binh khiên huyền thoại như một pháo đài di động, chống chịu sát thương và bảo vệ đồng đội.",
            PortraitStyleClass = "arthur-img",
            Skills = new LobbySkillInfo[]
            {
                new LobbySkillInfo { Name = "Dặm Khiên", Description = "Đập mạnh khiên hoàng gia về phía trước, làm choáng kẻ địch và gây sát thương va chạm.", IconStyleClass = "arthur-skill-0" },
                new LobbySkillInfo { Name = "Bất Tử", Description = "Tăng mạnh khả năng phòng thủ và kháng sát thương vật lý trong 5 giây.", IconStyleClass = "arthur-skill-1" },
                new LobbySkillInfo { Name = "Pháo Đài Bảo Vệ", Description = "Tạo vùng phòng thủ hấp thụ toàn bộ đạn phản hồi và hồi máu cho đồng đội.", IconStyleClass = "arthur-skill-2" }
            }
        }
    };

    private VisualElement _detailsModal;
    private VisualElement _detailsPortrait;
    private Label _detailsName;
    private Label _detailsRole;
    private Label _detailsDesc;
    private Button _btnCloseDetails;

    private VisualElement _charSelectPanel;
    private Button _btnToggleCharPanel;
    private Button _btnSwapCharacter;

    // Swap Request Modal & Fields
    private VisualElement _swapRequestModal;
    private Label _lblSwapRequestMsg;
    private Button _btnSwapAccept;
    private Button _btnSwapDecline;
    private ulong _pendingSwapSenderClientId = ulong.MaxValue;
    private bool _isSwapModeActive = false;

    // Players Voice Chat Modal & Fields
    private VisualElement _playersVoiceModal;
    private ScrollView _playersVoiceList;
    private Button _btnPlayersVoice;
    private Button _btnClosePlayersVoice;

    private void Awake() 
    { 
        Debug.Log("[EMERGENCY] Awake đã chạy!");
        
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null)
        {
            Debug.LogError("[Lobby] KHÔNG TÌM THẤY UIDocument!");
            return;
        }

        _root = _uiDocument.rootVisualElement;
        _lblRoomName = _root.Q<Label>("lbl-room-name");
        _lblRoomId = _root.Q<Label>("lbl-room-id");
        _lblPlayerCount = _root.Q<Label>("lbl-player-count");
        _lblWarning = _root.Q<Label>("lbl-warning");
        _btnReady = _root.Q<Button>("btn-ready");
        _btnStart = _root.Q<Button>("btn-start");
        _btnLeave = _root.Q<Button>("btn-leave");

        // Các thành phần của chi tiết nhân vật (modal)
        _detailsModal = _root.Q<VisualElement>("char-details-modal");
        _detailsPortrait = _root.Q<VisualElement>("details-portrait");
        _detailsName = _root.Q<Label>("details-name");
        _detailsRole = _root.Q<Label>("details-role");
        _detailsDesc = _root.Q<Label>("details-desc");
        _btnCloseDetails = _root.Q<Button>("btn-close-details");

        if (_detailsModal != null)
        {
            _detailsModal.style.display = DisplayStyle.None;
        }

        if (_btnCloseDetails != null)
        {
            _btnCloseDetails.clicked += HideCharacterDetails;
        }

        // Toggles cho character selection panel
        _charSelectPanel = _root.Q<VisualElement>("char-select-panel");
        _btnToggleCharPanel = _root.Q<Button>("btn-toggle-char-panel");
        _btnSwapCharacter = _root.Q<Button>("btn-swap-character");

        if (_btnToggleCharPanel != null)
        {
            _btnToggleCharPanel.clicked += ToggleCharacterPanel;
        }

        if (_btnSwapCharacter != null)
        {
            _btnSwapCharacter.clicked += ToggleSwapMode;
            _btnSwapCharacter.AddToClassList("hidden-element");
            _btnSwapCharacter.style.display = DisplayStyle.None;
        }

        // Các thành phần của Swap Request Modal
        _swapRequestModal = _root.Q<VisualElement>("swap-request-modal");
        _lblSwapRequestMsg = _root.Q<Label>("lbl-swap-request-msg");
        _btnSwapAccept = _root.Q<Button>("btn-swap-accept");
        _btnSwapDecline = _root.Q<Button>("btn-swap-decline");

        if (_swapRequestModal != null)
        {
            _swapRequestModal.style.display = DisplayStyle.None;
        }

        if (_btnSwapAccept != null)
        {
            _btnSwapAccept.clicked += () => {
                if (_pendingSwapSenderClientId != ulong.MaxValue)
                {
                    RespondToSwapRequestServerRpc(_pendingSwapSenderClientId, true);
                    _pendingSwapSenderClientId = ulong.MaxValue;
                }
                HideSwapRequestModal();
            };
        }

        if (_btnSwapDecline != null)
        {
            _btnSwapDecline.clicked += () => {
                if (_pendingSwapSenderClientId != ulong.MaxValue)
                {
                    RespondToSwapRequestServerRpc(_pendingSwapSenderClientId, false);
                    _pendingSwapSenderClientId = ulong.MaxValue;
                }
                HideSwapRequestModal();
            };
        }

        // Players Voice Chat Modal setup
        _playersVoiceModal = _root.Q<VisualElement>("players-voice-modal");
        _playersVoiceList = _root.Q<ScrollView>("players-voice-list");
        _btnPlayersVoice = _root.Q<Button>("btn-players-voice");
        _btnClosePlayersVoice = _root.Q<Button>("btn-close-players-voice");

        if (_playersVoiceModal != null)
        {
            _playersVoiceModal.style.display = DisplayStyle.None;
        }

        if (_btnPlayersVoice != null)
        {
            _btnPlayersVoice.clicked += ShowPlayersVoiceModal;
        }

        if (_btnClosePlayersVoice != null)
        {
            _btnClosePlayersVoice.clicked += HidePlayersVoiceModal;
        }

        // Ẩn mặc định cho đỡ vướng
        if (_charSelectPanel != null)
        {
            _charSelectPanel.AddToClassList("panel-hidden-state");
            _charSelectPanel.style.display = DisplayStyle.None;
        }
        if (_btnToggleCharPanel != null)
        {
            _btnToggleCharPanel.text = "CHỌN NHÂN VẬT";
        }

        // Đăng ký sự kiện Click cho 4 thẻ nhân vật
        for (int i = 0; i < 4; i++)
        {
            int charId = i;
            var card = _root.Q<VisualElement>($"char-card-{charId}");
            if (card != null)
            {
                card.RegisterCallback<ClickEvent>(evt => OnCharacterCardClicked(charId, evt));
            }
        }

        Debug.Log($"[Lobby] UI Binding: _btnReady={_btnReady!=null}, _btnStart={_btnStart!=null}, _btnLeave={_btnLeave!=null}");

        RefreshLocalUI();

        if (_btnLeave != null) _btnLeave.clicked += LeaveRoom;
        if (_btnReady != null) _btnReady.clicked += ToggleReady;
        if (_btnStart != null) _btnStart.clicked += StartGame;
    }

    private void HideCharacterDetails()
    {
        if (_detailsModal != null)
        {
            _detailsModal.AddToClassList("hidden-element");
            _detailsModal.style.display = DisplayStyle.None;
            Debug.Log("[Lobby] Đã ẩn bảng chi tiết nhân vật.");
        }
        else
        {
            Debug.LogError("[Lobby] _detailsModal bị NULL khi ẩn!");
        }
    }

    private void ShowPlayersVoiceModal()
    {
        if (_playersVoiceModal != null)
        {
            _playersVoiceModal.RemoveFromClassList("hidden-element");
            _playersVoiceModal.style.display = DisplayStyle.Flex;
            PopulatePlayersVoiceList();
            Debug.Log("[Lobby] Đã hiển thị bảng chỉnh âm lượng người chơi.");
        }
    }

    private void HidePlayersVoiceModal()
    {
        if (_playersVoiceModal != null)
        {
            _playersVoiceModal.AddToClassList("hidden-element");
            _playersVoiceModal.style.display = DisplayStyle.None;
            Debug.Log("[Lobby] Đã ẩn bảng chỉnh âm lượng người chơi.");
        }
    }

    private void PopulatePlayersVoiceList()
    {
        if (_playersVoiceList == null) return;
        _playersVoiceList.Clear();

        if (NetworkManager.Singleton == null) return;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        bool hasOtherPlayers = false;

        foreach (var p in NetPlayers)
        {
            if (p.ClientId == localClientId) continue; // Skip self

            hasOtherPlayers = true;
            ulong targetClientId = p.ClientId;
            string playerName = p.Name.ToString();
            string charName = GetCharacterName(p.CharacterId);

            // Container
            var row = new VisualElement();
            row.AddToClassList("voice-player-row");

            // Label
            var nameLbl = new Label($"<b>{playerName}</b> ({charName})");
            nameLbl.AddToClassList("voice-player-name");
            row.Add(nameLbl);

            // Slider
            var volSlider = new Slider(0f, 100f);
            volSlider.AddToClassList("custom-slider");
            volSlider.style.flexGrow = 1f;

            // Get current multiplier
            float currentVol = 100f;
            if (PlayerVoicePlayback.PlayerVolumeMultipliers.TryGetValue(targetClientId, out float mult))
            {
                currentVol = mult * 100f;
            }
            else if (MicManager.Instance != null)
            {
                currentVol = MicManager.Instance.voicePlaybackVolume;
            }
            volSlider.value = currentVol;

            // Numeric Label
            var valLbl = new Label($"{Mathf.RoundToInt(currentVol)}%");
            valLbl.AddToClassList("voice-slider-val");

            volSlider.RegisterValueChangedCallback(evt =>
            {
                float newVolPercent = evt.newValue;
                valLbl.text = $"{Mathf.RoundToInt(newVolPercent)}%";
                PlayerVoicePlayback.PlayerVolumeMultipliers[targetClientId] = newVolPercent / 100f;
            });

            row.Add(volSlider);
            row.Add(valLbl);

            _playersVoiceList.Add(row);
        }

        if (!hasOtherPlayers)
        {
            var noPlayersLbl = new Label("KHÔNG CÓ NGƯỜI CHƠI KHÁC TRONG PHÒNG");
            noPlayersLbl.AddToClassList("voice-empty-hint");
            _playersVoiceList.Add(noPlayersLbl);
        }
    }

    private void ToggleSwapMode()
    {
        _isSwapModeActive = !_isSwapModeActive;
        if (_btnSwapCharacter != null)
        {
            _btnSwapCharacter.text = _isSwapModeActive ? "HỦY ĐỔI" : "ĐỔI NHÂN VẬT";
        }
        UpdatePlayerUI();
    }

    private void HideSwapRequestModal()
    {
        if (_swapRequestModal != null)
        {
            _swapRequestModal.AddToClassList("hidden-element");
            _swapRequestModal.style.display = DisplayStyle.None;
        }
    }

    private Coroutine _charPanelAnimCoroutine;

    private void ToggleCharacterPanel()
    {
        if (_charSelectPanel == null || _btnToggleCharPanel == null) return;
        
        if (_charPanelAnimCoroutine != null) StopCoroutine(_charPanelAnimCoroutine);
        
        bool isHidden = _charSelectPanel.ClassListContains("panel-hidden-state") || _charSelectPanel.style.display == DisplayStyle.None;
        if (isHidden)
        {
            _charSelectPanel.style.display = DisplayStyle.Flex;
            _charPanelAnimCoroutine = StartCoroutine(ShowCharPanelCoroutine());
            _btnToggleCharPanel.text = "ẨN BẢNG CHỌN";
        }
        else
        {
            _charPanelAnimCoroutine = StartCoroutine(HideCharPanelCoroutine());
            _btnToggleCharPanel.text = "CHỌN NHÂN VẬT";
        }
    }

    private IEnumerator ShowCharPanelCoroutine()
    {
        yield return null; // Đợi 1 frame để layout nhận trạng thái display: flex
        _charSelectPanel.RemoveFromClassList("panel-hidden-state");
    }

    private IEnumerator HideCharPanelCoroutine()
    {
        _charSelectPanel.AddToClassList("panel-hidden-state");
        yield return new WaitForSeconds(0.3f); // Đợi kết thúc transition trong USS (0.3s)
        _charSelectPanel.style.display = DisplayStyle.None;
    }

    private void OnEnable()
    {
        Debug.Log("[EMERGENCY] OnEnable đã chạy!");
    }



    private void Start()
    {
        Debug.Log("[Lobby] Script NetworkWaitingRoom đã bắt đầu chạy (Start).");
        
        if (_uiDocument == null) Debug.LogError("[Lobby] THẤT BẠI: Bạn chưa kéo UI Document!");
        if (slots == null || slots.Length == 0) Debug.LogError("[Lobby] THẤT BẠI: Danh sách Slots đang trống!");

        // Khởi tạo các chùm sáng Mr. Bean cho các slots
        InitializeSpotlightBeams();

        // TỰ ĐỘNG KIỂM TRA NẾU VÀO PHÒNG MUỘN
        InvokeRepeating(nameof(CheckForSpawn), 0.5f, 1.0f);
    }

    private void Update()
    {
        // Kiểm tra Shift + Chuột trái để soi nhân vật trong không gian 3D
        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetMouseButtonDown(0))
        {
            Handle3DInspection();
        }
    }

    private void Handle3DInspection()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var playerUI = hit.collider.GetComponent<PlayerWaitingRoomUI>();
            if (playerUI == null)
            {
                playerUI = hit.collider.GetComponentInParent<PlayerWaitingRoomUI>();
            }

            if (playerUI != null)
            {
                ulong targetClientId = playerUI.OwnerClientId;
                Debug.Log($"[CLIENT] Raycast trúng nhân vật của Client {targetClientId}!");

                // Tìm xem Client này đang chọn nhân vật nào
                foreach (var p in NetPlayers)
                {
                    if (p.ClientId == targetClientId)
                    {
                        if (p.CharacterId >= 0 && p.CharacterId < 4)
                        {
                            ShowCharacterDetails(p.CharacterId);
                        }
                        else
                        {
                            Debug.LogWarning($"[CLIENT] Client {targetClientId} chưa chọn nhân vật hợp lệ (CharacterId={p.CharacterId})");
                        }
                        break;
                    }
                }
            }
        }
    }

    private void OnCharacterCardClicked(int charId, ClickEvent evt)
    {
        if (_isSwapModeActive)
        {
            ulong targetClientId = ulong.MaxValue;
            foreach (var p in NetPlayers)
            {
                if (p.CharacterId == charId && p.ClientId != NetworkManager.Singleton.LocalClientId)
                {
                    targetClientId = p.ClientId;
                    break;
                }
            }

            if (targetClientId != ulong.MaxValue)
            {
                Debug.Log($"[CLIENT] Clicked swap on character {charId} owned by Client {targetClientId}");
                RequestSwapCharacterServerRpc(targetClientId);
                ToggleSwapMode(); // Exit swap mode
            }
            return;
        }

        // 1. Kiểm tra xem nhân vật này có bị người khác chọn chưa
        bool isLockedByOther = false;
        if (NetworkManager.Singleton != null)
        {
            foreach (var p in NetPlayers)
            {
                if (p.ClientId != NetworkManager.Singleton.LocalClientId && p.CharacterId == charId)
                {
                    isLockedByOther = true;
                    break;
                }
            }
        }

        // 2. Xử lý Click hoặc Shift + Click
        bool isShift = evt.shiftKey || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (isShift)
        {
            ShowCharacterDetails(charId);
        }
        else if (isLockedByOther)
        {
            // Hiển thị cảnh báo nhân vật đã bị người khác chọn
            if (_lblWarning != null)
            {
                _lblWarning.text = $"{_characters[charId].Name.ToUpper()} ĐÃ ĐƯỢC CHỌN!";
                _lblWarning.RemoveFromClassList("hidden-element");
                _lblWarning.style.display = DisplayStyle.Flex;
                CancelInvoke(nameof(HideWarningLabel));
                Invoke(nameof(HideWarningLabel), 2f);
            }
        }
        else
        {
            SelectCharacter(charId);
        }
    }

    private void ShowCharacterDetails(int charId)
    {
        if (charId < 0 || charId >= _characters.Length) return;

        var info = _characters[charId];
        if (_detailsName != null) _detailsName.text = info.Name;
        if (_detailsRole != null) _detailsRole.text = info.Role;
        if (_detailsDesc != null) _detailsDesc.text = info.Description;

        // BINDING 3 SKILLS
        for (int i = 0; i < 3; i++)
        {
            var skillNameLbl = _root.Q<Label>($"details-skill-name-{i}");
            var skillDescLbl = _root.Q<Label>($"details-skill-desc-{i}");
            var skillImg = _root.Q<VisualElement>($"details-skill-img-{i}");

            if (info.Skills != null && i < info.Skills.Length)
            {
                var skillInfo = info.Skills[i];
                if (skillNameLbl != null) skillNameLbl.text = skillInfo.Name;
                if (skillDescLbl != null) skillDescLbl.text = skillInfo.Description;
                if (skillImg != null)
                {
                    skillImg.ClearClassList();
                    skillImg.AddToClassList("details-skill-icon");
                    if (!string.IsNullOrEmpty(skillInfo.IconStyleClass))
                    {
                        skillImg.AddToClassList(skillInfo.IconStyleClass);
                    }
                }
            }
        }

        if (_detailsPortrait != null)
        {
            _detailsPortrait.ClearClassList();
            _detailsPortrait.AddToClassList("details-large-portrait");
            _detailsPortrait.AddToClassList(info.PortraitStyleClass);
        }

        if (_detailsModal != null)
        {
            _detailsModal.RemoveFromClassList("hidden-element");
            _detailsModal.style.display = DisplayStyle.Flex;
        }
    }

    private void SelectCharacter(int charId)
    {
        Debug.Log($"[CLIENT] Yêu cầu chọn nhân vật: {charId}");
        PlayerPrefs.SetInt("SelectedCharacterId", charId);
        PlayerPrefs.Save();

        // Kích hoạt ngay lập tức chùm sáng trên slot của chính mình để phản hồi tức thì
        if (NetworkManager.Singleton != null && enableSpotlightBeams)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            foreach (var p in NetPlayers)
            {
                if (p.ClientId == myId && p.Slot >= 0 && p.Slot < 4)
                {
                    if (_slotBeams != null && _slotBeams[p.Slot] != null)
                    {
                        _slotBeams[p.Slot].PlayBeamDrop(charId);
                        _lastSlotCharacterIds[p.Slot] = charId;
                    }
                    break;
                }
            }
        }

        ChangeCharacterServerRpc(charId);
    }

    private void CheckForSpawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !IsSpawned)
        {
            if (NetworkManager.Singleton.IsConnectedClient)
            {
                Debug.Log("[Lobby] Đã thấy kết nối mạng, đang thử kích hoạt đồng bộ thủ công...");
                OnNetworkSpawn();
                CancelInvoke(nameof(CheckForSpawn));
            }
        }
        else if (IsSpawned)
        {
            CancelInvoke(nameof(CheckForSpawn));
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("[Lobby] OnNetworkSpawn đã kích hoạt!");

        // Dù là Server hay Client, ta đều đăng ký callback để biết khi có người vào
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            if (IsServer)
            {
                Debug.Log($"[SERVER] Đang chạy trên VPS. Đang có {NetworkManager.Singleton.ConnectedClients.Count} người kết nối.");
                
                // LẤY DỮ LIỆU TỪ BOOTSTRAP (DO VPS KHÔNG CÓ PLAYERPREFS)
                NetRoomName.Value = NetworkBootstrap.ServerRoomName;
                NetRoomId.Value = NetworkBootstrap.ServerRoomId;
                
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    Debug.Log($"[SERVER] Tự động Spawn cho ClientID: {client.ClientId}");
                    OnClientConnected(client.ClientId);
                }
            }
            else
            {
                Debug.Log($"[CLIENT] Đã kết nối thành công với ClientID: {NetworkManager.Singleton.LocalClientId}");
            }
        }
        else
        {
            Debug.LogError("[Lobby] NetworkManager.Singleton bị NULL!");
        }

        NetRoomName.OnValueChanged += (o, n) => {
            Debug.Log($"[Lobby] Tên phòng đổi thành: {n}");
            UpdateRoomUI();
        };
        NetRoomId.OnValueChanged += (o, n) => {
            Debug.Log($"[Lobby] ID phòng đổi thành: {n}");
            UpdateRoomUI();
        };
        NetPlayers.OnListChanged += (e) => {
            Debug.Log($"[Lobby] Danh sách người chơi thay đổi! Số lượng: {NetPlayers.Count}");
            UpdatePlayerUI();
        };


        UpdateRoomUI();
        UpdatePlayerUI();

        // NẾU LÀ MÁY KHÁCH, ĐỢI 1 GIÂY RỒI MỚI GỬI LỆNH ÉP VPS CẬP NHẬT (TRÁNH XUNG ĐỘT)
        if (IsClient && !IsServer)
        {
            StartCoroutine(DelayUpdateRoomRPC());
        }
    }

    public override void OnNetworkDespawn()
    {
        Debug.Log("[Lobby] OnNetworkDespawn đã kích hoạt! Đang hủy đăng ký callback...");
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private IEnumerator DelayUpdateRoomRPC()
    {
        yield return new WaitForSeconds(2.0f);
        string pName = PlayerPrefs.GetString("AuthDisplayName", "Explorer");
        string rName = PlayerPrefs.GetString("CurrentRoomName", "Atlantis Lobby");
        string rId = PlayerPrefs.GetString("CurrentRoomID", "000000");
        Debug.Log($"[CLIENT] Đang gửi lệnh ServerRpc: {pName} | {rName}");
        UpdateRoomInfoServerRpc(pName, rName, rId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateRoomInfoServerRpc(string playerName, string roomName, string roomId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[SERVER] RPC: Cập nhật phòng '{roomName}' và Player '{playerName}' cho Client {clientId}");

        // 1. Cập nhật tên phòng toàn cục
        NetRoomName.Value = roomName;
        NetRoomId.Value = roomId;

        // 2. Cập nhật tên người chơi trong danh sách
        bool found = false;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                var p = NetPlayers[i];
                p.Name = playerName;
                NetPlayers[i] = p;
                found = true;
                Debug.Log($"[SERVER] Đã đổi tên Client {clientId} thành {playerName}");
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[SERVER] Chưa tìm thấy Client {clientId} trong danh sách NetPlayers để đổi tên!");
        }

        // 3. Cập nhật trực tiếp vào biến mạng trên nhân vật (Để đồng bộ tag tên)
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                var playerUI = client.PlayerObject.GetComponent<PlayerWaitingRoomUI>();
                if (playerUI != null)
                {
                    playerUI.NetName.Value = playerName;
                    Debug.Log($"[SERVER] Đã cập nhật NetName trực tiếp cho nhân vật của Client {clientId}");
                }
            }
        }
    }



    private void RefreshLocalUI()
    {
        string localName = PlayerPrefs.GetString("CurrentRoomName", "UNKNOWN");
        string localId = PlayerPrefs.GetString("CurrentRoomID", "XXXXXX");
        if (_lblRoomName != null) _lblRoomName.text = $"PHÒNG: {localName.ToUpper()}";
        if (_lblRoomId != null) _lblRoomId.text = $"ID: #{localId}";
    }


    private void UpdateRoomUI()
    {
        if (_lblRoomName != null) _lblRoomName.text = $"PHÒNG: {NetRoomName.Value.ToString().ToUpper()}";
        if (_lblRoomId != null) _lblRoomId.text = $"ID: #{NetRoomId.Value.ToString()}";
    }

    private bool HasDuplicateCharacters()
    {
        var seen = new HashSet<int>();
        foreach (var p in NetPlayers)
        {
            if (p.CharacterId < 0) continue; // Chưa chọn thì bỏ qua
            if (!seen.Add(p.CharacterId)) return true;
        }
        return false;
    }

    private void UpdatePlayerUI()
    {
        if (_lblPlayerCount != null) _lblPlayerCount.text = $"NGƯỜI CHƠI: {NetPlayers.Count}/4";
        
        // Reset all 4 character cards to default state
        for (int i = 0; i < 4; i++)
        {
            var card = _root.Q<VisualElement>($"char-card-{i}");
            var statusLbl = _root.Q<Label>($"char-status-{i}");
            if (card != null)
            {
                card.RemoveFromClassList("select-active");
            }
            if (statusLbl != null)
            {
                statusLbl.text = "TRỐNG";
                statusLbl.RemoveFromClassList("active-status");
            }
        }

        // Aggregate names for each character selection
        List<string> selectorsPerChar0 = new List<string>();
        List<string> selectorsPerChar1 = new List<string>();
        List<string> selectorsPerChar2 = new List<string>();
        List<string> selectorsPerChar3 = new List<string>();

        foreach (var p in NetPlayers)
        {
            int charId = p.CharacterId;
            if (NetworkManager.Singleton != null && p.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                PlayerPrefs.SetInt("SelectedCharacterId", charId);
                PlayerPrefs.Save();
            }
            if (charId == 0)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) selectorsPerChar0.Insert(0, "BẠN");
                else selectorsPerChar0.Add(p.Name.ToString().ToUpper());
            }
            else if (charId == 1)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) selectorsPerChar1.Insert(0, "BẠN");
                else selectorsPerChar1.Add(p.Name.ToString().ToUpper());
            }
            else if (charId == 2)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) selectorsPerChar2.Insert(0, "BẠN");
                else selectorsPerChar2.Add(p.Name.ToString().ToUpper());
            }
            else if (charId == 3)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) selectorsPerChar3.Insert(0, "BẠN");
                else selectorsPerChar3.Add(p.Name.ToString().ToUpper());
            }
        }

        // Display selections
        UpdateCardUI(0, selectorsPerChar0);
        UpdateCardUI(1, selectorsPerChar1);
        UpdateCardUI(2, selectorsPerChar2);
        UpdateCardUI(3, selectorsPerChar3);

        // Hiển thị nút Swap Explorer nếu đã chọn nhân vật hợp lệ (>= 0)
        if (_btnSwapCharacter != null)
        {
            bool hasSelected = false;
            foreach (var p in NetPlayers)
            {
                if (NetworkManager.Singleton != null && p.ClientId == NetworkManager.Singleton.LocalClientId && p.CharacterId >= 0)
                {
                    hasSelected = true;
                    break;
                }
            }
            if (hasSelected)
            {
                _btnSwapCharacter.RemoveFromClassList("hidden-element");
                _btnSwapCharacter.style.display = DisplayStyle.Flex;
            }
            else
            {
                _btnSwapCharacter.AddToClassList("hidden-element");
                _btnSwapCharacter.style.display = DisplayStyle.None;
            }
        }

        // Cập nhật số lượng người chơi - sẻ ghi đè lại sau khi tính ready count
        if (_lblPlayerCount != null) _lblPlayerCount.text = $"NGƯỜI CHƠI: {NetPlayers.Count}/4";

        // KIỂM TRA ĐIỀU KIỆN: Đủ 1 người VÀ tất cả đều ready
        int readyCount = 0;
        foreach (var p in NetPlayers) if (p.IsReady) readyCount++;
        bool enoughPlayers = NetPlayers.Count >= 1;
        bool allReady = enoughPlayers && NetPlayers.Count > 0 && (readyCount == NetPlayers.Count);

        // Cập nhật text số lượng người chơi
        if (_lblPlayerCount != null)
            _lblPlayerCount.text = $"NGƯỜI CHƠI: {NetPlayers.Count}/4";

        // KIỂM TRA XEM LOCAL CLIENT CÓ PHẢI LÀ CHỦ PHÒNG (SLOT 0) KHÔNG
        bool isRoomHost = false;
        if (NetworkManager.Singleton != null)
        {
            foreach (var p in NetPlayers)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId && p.Slot == 0)
                {
                    isRoomHost = true;
                    break;
                }
            }
        }

        bool duplicatesExist = HasDuplicateCharacters();
        bool canStart = allReady && !duplicatesExist;

        // Hiển cảnh báo nếu có trùng nhân vật
        if (_lblWarning != null && duplicatesExist)
        {
            _lblWarning.text = "PHÁT HIỆN TRÙNG NHÂN VẬT!";
            _lblWarning.RemoveFromClassList("hidden-element");
            _lblWarning.style.display = DisplayStyle.Flex;
        }
        else if (_lblWarning != null && !duplicatesExist && _lblWarning.text == "PHÁT HIỆN TRÙNG NHÂN VẬT!")
        {
            _lblWarning.AddToClassList("hidden-element");
            _lblWarning.style.display = DisplayStyle.None;
        }

        if (_btnStart != null)
        {
            // Chỉ hiển thị nút Start cho Chủ phòng (Slot 0)
            if (isRoomHost)
            {
                _btnStart.RemoveFromClassList("hidden-element");
                _btnStart.style.display = DisplayStyle.Flex;
            }
            else
            {
                _btnStart.AddToClassList("hidden-element");
                _btnStart.style.display = DisplayStyle.None;
            }

            // Chỉ enable START khi đủ điều kiện
            _btnStart.SetEnabled(canStart);
            _btnStart.text = canStart ? "BẮT ĐẦU THÁM HIỂM" :
                             !enoughPlayers ? $"ĐANG CHỜ ({NetPlayers.Count}/4 NGƯỜI CHƠI)" :
                             !allReady ? $"ĐANG CHỜ ({readyCount}/{NetPlayers.Count} SẴN SÀNG)" :
                             "BẮT ĐẦU THÁM HIỂM";
        }

        // Cập nhật màu nút Ready cho bản thân
        UpdateReadyButtonState();

        // Cập nhật chùm sáng Mr. Bean cho các slot
        UpdateSpotlightBeams();

        // Refresh player voice list if open
        if (_playersVoiceModal != null && _playersVoiceModal.style.display == DisplayStyle.Flex)
        {
            PopulatePlayersVoiceList();
        }
    }

    private void InitializeSpotlightBeams()
    {
        if (!enableSpotlightBeams || slots == null) return;

        for (int i = 0; i < slots.Length && i < 4; i++)
        {
            if (slots[i] == null) continue;

            // Tìm hoặc tạo LobbySpotlightBeam cho từng slot
            var existingBeam = slots[i].GetComponentInChildren<LobbySpotlightBeam>();
            if (existingBeam == null)
            {
                GameObject beamObj = new GameObject($"LobbySpotlightBeam_Slot{i}");
                beamObj.transform.SetParent(slots[i]);
                beamObj.transform.localPosition = Vector3.zero;
                beamObj.transform.localRotation = Quaternion.identity;

                existingBeam = beamObj.AddComponent<LobbySpotlightBeam>();
            }

            existingBeam.slotTransform = slots[i];
            existingBeam.beamHeight = spotlightHeight;
            existingBeam.lightIntensity = spotlightIntensity;
            existingBeam.useCharacterTheming = useCharacterTheming;
            existingBeam.BuildComponentsIfNeeded();

            _slotBeams[i] = existingBeam;
        }
    }

    private void UpdateSpotlightBeams()
    {
        if (!enableSpotlightBeams || _slotBeams == null) return;

        // Lưu danh sách characterId hiện tại của từng slot (0..3)
        int[] currentSlotCharIds = new int[] { -1, -1, -1, -1 };

        if (NetPlayers != null)
        {
            foreach (var p in NetPlayers)
            {
                if (p.Slot >= 0 && p.Slot < 4)
                {
                    currentSlotCharIds[p.Slot] = p.CharacterId;
                }
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (_slotBeams[i] == null) continue;

            int charId = currentSlotCharIds[i];
            int lastCharId = _lastSlotCharacterIds[i];

            if (charId >= 0)
            {
                if (lastCharId != charId)
                {
                    // Kích hoạt đợt giáng sáng mới khi mới chọn hoặc đổi nhân vật
                    _slotBeams[i].PlayBeamDrop(charId);
                    _lastSlotCharacterIds[i] = charId;
                }
                else
                {
                    _slotBeams[i].SetBeamActive(true, charId);
                }
            }
            else
            {
                // Chưa chọn hoặc slot trống -> ẩn chùm sáng
                _slotBeams[i].SetBeamActive(false);
                _lastSlotCharacterIds[i] = -1;
            }
        }
    }
    private void UpdateCardUI(int charId, List<string> selectors)
    {
        var card = _root.Q<VisualElement>($"char-card-{charId}");
        var statusLbl = _root.Q<Label>($"char-status-{charId}");

        if (card != null)
        {
            card.RemoveFromClassList("select-active");
            card.RemoveFromClassList("card-locked");
            card.style.opacity = 1f;
        }
        if (statusLbl != null)
        {
            statusLbl.text = "TRỐNG";
            statusLbl.RemoveFromClassList("active-status");
            statusLbl.RemoveFromClassList("swap-status-active");
        }

        if (selectors.Count > 0)
        {
            if (selectors.Contains("BẠN"))
            {
                if (card != null)
                {
                    card.AddToClassList("select-active");
                }
                if (statusLbl != null)
                {
                    statusLbl.text = "BẠN";
                    statusLbl.AddToClassList("active-status");
                }
            }
            else
            {
                if (_isSwapModeActive)
                {
                    if (card != null)
                    {
                        card.style.opacity = 1f;
                        card.AddToClassList("select-active");
                    }
                    if (statusLbl != null)
                    {
                        statusLbl.text = "ĐỔI";
                        statusLbl.AddToClassList("swap-status-active");
                    }
                }
                else
                {
                    // Khóa card vì người khác đã chiếm nhân vật này
                    if (card != null)
                    {
                        card.AddToClassList("card-locked");
                        card.style.opacity = 0.4f;
                    }
                    if (statusLbl != null)
                    {
                        statusLbl.text = "ĐÃ CHỌN";
                        statusLbl.RemoveFromClassList("active-status");
                    }
                }
            }
        }
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
                _btnReady.text = p.IsReady ? "ĐÃ SẴN SÀNG!" : "SẴN SÀNG?";
                break;
            }
        }
    }

    private void ToggleReady()
    {
        Debug.Log($"[CLIENT] Nút Ready được click! LocalClientId={NetworkManager.Singleton.LocalClientId}");
        
        // Kiểm tra xem đã chọn nhân vật chưa
        bool hasSelected = false;
        if (NetworkManager.Singleton != null)
        {
            foreach (var p in NetPlayers)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId)
                {
                    if (p.CharacterId >= 0 && p.CharacterId < 4)
                    {
                        hasSelected = true;
                    }
                    break;
                }
            }
        }

        if (!hasSelected)
        {
            Debug.LogWarning("[CLIENT] Bạn cần phải chọn nhân vật trước khi ấn Ready!");
            if (_lblWarning != null)
            {
                _lblWarning.text = "VUI LÒNG CHỌN NHÂN VẬT TRƯỚC KHI SẴN SÀNG!";
                _lblWarning.RemoveFromClassList("hidden-element");
                _lblWarning.style.display = DisplayStyle.Flex;
                
                // Tự động ẩn cảnh báo sau 3 giây
                CancelInvoke(nameof(HideWarningLabel));
                Invoke(nameof(HideWarningLabel), 3f);
            }
            return;
        }

        ToggleReadyServerRpc();
    }

    private void HideWarningLabel()
    {
        if (_lblWarning != null)
        {
            _lblWarning.AddToClassList("hidden-element");
            _lblWarning.style.display = DisplayStyle.None;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[SERVER] Nhận lệnh ToggleReady từ ClientId={clientId}");
        bool found = false;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                var data = NetPlayers[i];
                data.IsReady = !data.IsReady;
                NetPlayers[i] = data;
                found = true;
                Debug.Log($"[SERVER] Cập nhật trạng thái IsReady của ClientId={clientId} thành: {data.IsReady}");
                break;
            }
        }
        if (!found)
        {
            Debug.LogWarning($"[SERVER] Không tìm thấy ClientId={clientId} trong danh sách NetPlayers để chuyển trạng thái Ready!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChangeCharacterServerRpc(int characterId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[SERVER] Nhận yêu cầu đổi nhân vật thành {characterId} từ Client {clientId}");
        
        // 1. Kiểm tra xem CharacterId này có bị người khác chiếm không
        foreach (var p in NetPlayers)
        {
            if (p.ClientId != clientId && p.CharacterId == characterId)
            {
                Debug.LogWarning($"[SERVER] Nhân vật {characterId} đã bị Client {p.ClientId} chọn! Từ chối yêu cầu của Client {clientId}");
                NotifyCharacterTakenClientRpc(characterId, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
                });
                return;
            }
        }

        // 2. Thực hiện đổi nhân vật cho client gửi yêu cầu
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                var p = NetPlayers[i];
                p.CharacterId = characterId;
                p.IsReady = false; // Bắt buộc hủy ready khi đổi nhân vật
                NetPlayers[i] = p;
                Debug.Log($"[SERVER] Đã cập nhật CharacterId={characterId} cho Client {clientId}");
                
                // Respawn mô hình 3D cho client này trên Server
                RespawnPlayerObject(clientId, characterId);
                break;
            }
        }

        // 3. Tự động gán nhân vật cuối cùng cho người chưa chọn nếu thích hợp
        CheckAndAutoPickLastCharacter();
    }

    private void CheckAndAutoPickLastCharacter()
    {
        if (!IsServer) return;
        if (NetPlayers.Count < 2) return;

        int unselectedCount = 0;
        ulong lastUnselectedClientId = 0;
        
        foreach (var p in NetPlayers)
        {
            if (p.CharacterId == -1)
            {
                unselectedCount++;
                lastUnselectedClientId = p.ClientId;
            }
        }

        if (unselectedCount == 1)
        {
            List<int> availableChars = new List<int> { 0, 1, 2, 3 };
            foreach (var p in NetPlayers)
            {
                if (p.CharacterId != -1)
                {
                    availableChars.Remove(p.CharacterId);
                }
            }

            if (availableChars.Count == 1)
            {
                int autoPickedCharId = availableChars[0];
                Debug.Log($"[SERVER] Tự động gán nhân vật {autoPickedCharId} cho Client {lastUnselectedClientId} (người chơi cuối cùng)");
                
                for (int i = 0; i < NetPlayers.Count; i++)
                {
                    if (NetPlayers[i].ClientId == lastUnselectedClientId)
                    {
                        var p = NetPlayers[i];
                        p.CharacterId = autoPickedCharId;
                        NetPlayers[i] = p;
                        
                        RespawnPlayerObject(lastUnselectedClientId, autoPickedCharId);
                        break;
                    }
                }
            }
        }
    }

    private GameObject GetPlayerPrefab(int characterId)
    {
        if (characterId < 0 || characterId >= 4) return playerNetworkPrefab;
        switch (characterId)
        {
            case 0: return playerNetworkPrefab;
            case 1: return playerNetworkPrefab2 != null ? playerNetworkPrefab2 : playerNetworkPrefab;
            case 2: return playerNetworkPrefab3 != null ? playerNetworkPrefab3 : playerNetworkPrefab;
            case 3: return playerNetworkPrefab4 != null ? playerNetworkPrefab4 : playerNetworkPrefab;
            default: return playerNetworkPrefab;
        }
    }

    private void RespawnPlayerObject(ulong clientId, int characterId)
    {
        if (!IsServer) return;

        Debug.Log($"[SERVER] Đang đổi mô hình 3D cho Client {clientId} sang nhân vật {characterId}");

        string preservedName = "Guest_" + clientId;
        if (NetPlayers != null)
        {
            foreach (var p in NetPlayers)
            {
                if (p.ClientId == clientId)
                {
                    preservedName = p.Name.ToString();
                    break;
                }
            }
        }
        
        // 1. Lưu lại tên hiển thị từ đối tượng cũ nếu có
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientConnection))
        {
            var oldPlayerObj = clientConnection.PlayerObject;
            if (oldPlayerObj != null)
            {
                var oldUI = oldPlayerObj.GetComponent<PlayerWaitingRoomUI>();
                if (oldUI != null)
                {
                    preservedName = oldUI.NetName.Value.ToString();
                }
                
                try
                {
                    oldPlayerObj.Despawn(true); // true = Hủy GameObject luôn
                    Debug.Log($"[SERVER] Đã despawn nhân vật cũ của Client {clientId}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SERVER] Lỗi nhẹ khi despawn: {ex.Message}");
                }
            }
        }

        // Tìm slot của người chơi
        int slotIdx = -1;
        foreach (var p in NetPlayers)
        {
            if (p.ClientId == clientId)
            {
                slotIdx = p.Slot;
                break;
            }
        }

        if (slotIdx == -1)
        {
            Debug.LogError($"[SERVER] Không tìm thấy slot cho Client {clientId}!");
            return;
        }

        // 2. Instantiate và Spawn nhân vật mới tương ứng với Character ID

        GameObject prefabToSpawn = GetPlayerPrefab(characterId);
        if (prefabToSpawn != null)
        {
            Vector3 spawnPos = slots[slotIdx].position;
            Quaternion spawnRot = slots[slotIdx].rotation;

            GameObject go = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnAsPlayerObject(clientId, true);
                Debug.Log($"[SERVER] Đã spawn nhân vật mới cho Client {clientId} tại Slot {slotIdx}");

                // Gán lại tên hiển thị
                var newUI = go.GetComponent<PlayerWaitingRoomUI>();
                if (newUI != null)
                {
                    newUI.NetName.Value = preservedName;
                }
            }
            else
            {
                Debug.LogError("[SERVER] Prefab nhân vật mới không có NetworkObject!");
            }
        }
        else
        {
            Debug.LogError($"[SERVER] Không tìm thấy prefab cho CharacterId={characterId}");
        }
    }

    // Đã chuyển sang NetworkBootstrap.cs
    /*
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    ...
    */

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // CẬP NHẬT LẠI THÔNG TIN PHÒNG TỪ BOOTSTRAP (MỖI KHI CÓ NGƯỜI VÀO CHO CHẮC)
        NetRoomName.Value = NetworkBootstrap.ServerRoomName;
        NetRoomId.Value = NetworkBootstrap.ServerRoomId;

        string playerName = "Guest_" + clientId;
        if (NetworkBootstrap.PendingPlayerNames.ContainsKey(clientId))
        {
            playerName = NetworkBootstrap.PendingPlayerNames[clientId];
            NetworkBootstrap.PendingPlayerNames.Remove(clientId);
        }

        foreach (var p in NetPlayers) if (p.ClientId == clientId) return;

        AddPlayer(clientId, playerName);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                NetPlayers.RemoveAt(i);
                break;
            }
        }
    }

    private void AddPlayer(ulong clientId, string playerName)
    {
        if (!IsServer) return;
        
        int slot = FindEmptySlot();
        if (slot == -1) return;

        var newData = new PlayerNetData
        {
            Name = playerName,
            Slot = slot,
            ClientId = clientId,
            IsReady = false,
            CharacterId = -1 // Gán mặc định là -1 (chưa chọn) để bắt buộc chọn trước khi Ready
        };

        NetPlayers.Add(newData);
        Debug.Log($"[Lobby] Đã thêm {playerName} vào Slot {slot} với trạng thái chưa chọn nhân vật (-1)");
        
        SpawnPlayerObject(clientId);

        // Kiểm tra xem đây có phải là người cuối cùng để auto-pick luôn không
        CheckAndAutoPickLastCharacter();
    }

    private void SpawnPlayerObject(ulong clientId)
    {
        if (!IsServer) return;
        
        // Tìm thông tin player vừa add
        int slotIdx = -1;
        int charId = 0;
        string pName = "Guest_" + clientId;
        foreach (var p in NetPlayers)
        {
            if (p.ClientId == clientId)
            {
                slotIdx = p.Slot;
                charId = p.CharacterId;
                pName = p.Name.ToString();
                break;
            }
        }
        
        if (slotIdx == -1) return;

        // Không spawn mô hình khi chưa chọn nhân vật – sẽ spawn khi chọn lần đầu qua RespawnPlayerObject
        if (charId == -1)
        {
            Debug.Log($"[SPAWN] Client {clientId} chưa chọn nhân vật, bỏ qua spawn ban đầu.");
            return;
        }

        GameObject prefabToSpawn = GetPlayerPrefab(charId);
        if (prefabToSpawn != null)
        {
            Vector3 spawnPos = slots[slotIdx].position;
            Quaternion spawnRot = slots[slotIdx].rotation;

            GameObject go = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            
            if (go == null) {
                Debug.LogError("[SPAWN] THẤT BẠI: Lệnh Instantiate trả về null!");
                return;
            }

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null) {
                Debug.LogError("[SPAWN] THẤT BẠI: Prefab nhân vật thiếu thành phần NetworkObject!");
                return;
            }

            try {
                netObj.SpawnAsPlayerObject(clientId, true);
                Debug.Log($"[SPAWN] Đã spawn nhân vật ban đầu cho Client {clientId} với prefab ID {charId} tại Slot {slotIdx}");
                
                // Gán lại tên hiển thị
                var newUI = go.GetComponent<PlayerWaitingRoomUI>();
                if (newUI != null)
                {
                    newUI.NetName.Value = pName;
                }
            }
            catch (System.Exception e) {
                Debug.LogError($"[SPAWN] LỖI KHI GỌI SPAWN: {e.Message}");
            }
        }
        else {
            Debug.LogError($"[SPAWN] THẤT BẠI: Không tìm thấy prefab cho CharacterId={charId}!");
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
        Debug.Log($"[CLIENT] Chủ phòng click START EXPEDITION! Đang gửi lệnh ServerRpc với cảnh cần load: {gameplaySceneName}");
        StartGameServerRpc(gameplaySceneName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartGameServerRpc(string sceneName, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;

        // Chỉ Slot 0 (chủ phòng) mới được khởi động game
        bool isSenderHost = false;
        foreach (var p in NetPlayers)
        {
            if (p.ClientId == senderClientId && p.Slot == 0) { isSenderHost = true; break; }
        }
        if (!isSenderHost)
        {
            Debug.LogWarning("[SERVER] Chỉ chủ phòng (Slot 0) mới được bắt đầu game!");
            return;
        }

        // Kiểm tra đủ 1 người
        if (NetPlayers.Count < 1)
        {
            Debug.LogWarning($"[SERVER] Chưa đủ người chơi! Hiện tại: {NetPlayers.Count}/1");
            return;
        }

        // Kiểm tra tất cả đều ready
        int readyCount = 0;
        foreach (var p in NetPlayers) if (p.IsReady) readyCount++;
        if (readyCount != NetPlayers.Count)
        {
            Debug.LogWarning($"[SERVER] Chưa đủ người sẵn sàng! {readyCount}/{NetPlayers.Count} ready");
            return;
        }

        // Kiểm tra không trùng nhân vật
        if (HasDuplicateCharacters())
        {
            Debug.LogError("[SERVER] Không thể bắt đầu game vì có trùng nhân vật!");
            return;
        }

        // Lưu trạng thái phòng đã start và danh sách người chơi vào NetworkBootstrap để phục vụ rejoin
        NetworkBootstrap.IsGameStarted = true;
        NetworkBootstrap.CurrentActiveRoomId = NetRoomId.Value.ToString();
        NetworkBootstrap.ActivePlayerNames.Clear();
        foreach (var p in NetPlayers)
        {
            NetworkBootstrap.ActivePlayerNames.Add(p.Name.ToString());
        }

        // Báo cho tất cả clients bật UI Loading cùng lúc
        NotifyStartGameClientRpc();

        Debug.Log($"[SERVER] Tất cả điều kiện thỏa mãn! Đang tải cảnh {sceneName}...");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    [ClientRpc]
    private void NotifyStartGameClientRpc()
    {
        if (_uiDocument != null)
        {
            _uiDocument.enabled = false;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.ShowLoading("ĐANG CHUẨN BỊ THÁM HIỂM...");
        }
    }

    private string GetCharacterName(int charId)
    {
        if (charId >= 0 && charId < _characters.Length)
        {
            return _characters[charId].Name;
        }
        return "UNKNOWN";
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSwapCharacterServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[SERVER] RequestSwapCharacterServerRpc called from {senderClientId} targeting {targetClientId}");

        PlayerNetData senderPlayer = default;
        PlayerNetData targetPlayer = default;
        bool foundSender = false;
        bool foundTarget = false;

        foreach (var p in NetPlayers)
        {
            if (p.ClientId == senderClientId)
            {
                senderPlayer = p;
                foundSender = true;
            }
            else if (p.ClientId == targetClientId)
            {
                targetPlayer = p;
                foundTarget = true;
            }
        }

        if (!foundSender || !foundTarget)
        {
            Debug.LogWarning($"[SERVER] Swap request invalid: sender found={foundSender}, target found={foundTarget}");
            return;
        }

        if (senderPlayer.CharacterId == -1 || targetPlayer.CharacterId == -1)
        {
            Debug.LogWarning("[SERVER] Swap request invalid: one of the players has no character selected.");
            return;
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { targetClientId }
            }
        };

        ReceiveSwapRequestClientRpc(senderClientId, senderPlayer.Name.ToString(), senderPlayer.CharacterId, targetPlayer.CharacterId, clientRpcParams);
    }

    [ClientRpc]
    private void ReceiveSwapRequestClientRpc(ulong senderClientId, string senderName, int senderCharId, int targetCharId, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[CLIENT] Received swap request from {senderName} (ClientId={senderClientId}). Swap {senderCharId} for {targetCharId}");
        
        _pendingSwapSenderClientId = senderClientId;

        if (_swapRequestModal != null && _lblSwapRequestMsg != null)
        {
            string senderCharName = GetCharacterName(senderCharId);
            string targetCharName = GetCharacterName(targetCharId);

            _lblSwapRequestMsg.text = $"{senderName.ToUpper()} MUỐN ĐỔI {senderCharName} LẤY {targetCharName} CỦA BẠN.";

            _swapRequestModal.RemoveFromClassList("hidden-element");
            _swapRequestModal.style.display = DisplayStyle.Flex;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RespondToSwapRequestServerRpc(ulong senderClientId, bool accepted, ServerRpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[SERVER] Target {targetClientId} responded to swap request from sender {senderClientId}: accepted={accepted}");

        int senderIdx = -1;
        int targetIdx = -1;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == senderClientId) senderIdx = i;
            if (NetPlayers[i].ClientId == targetClientId) targetIdx = i;
        }

        if (senderIdx == -1 || targetIdx == -1)
        {
            Debug.LogWarning("[SERVER] Swap response invalid: sender or target is no longer in the lobby.");
            return;
        }

        if (accepted)
        {
            var senderData = NetPlayers[senderIdx];
            var targetData = NetPlayers[targetIdx];

            int tempCharId = senderData.CharacterId;
            senderData.CharacterId = targetData.CharacterId;
            targetData.CharacterId = tempCharId;

            // Force both to NOT READY
            senderData.IsReady = false;
            targetData.IsReady = false;

            NetPlayers[senderIdx] = senderData;
            NetPlayers[targetIdx] = targetData;

            Debug.Log($"[SERVER] Swapped characters: Client {senderClientId} -> {senderData.CharacterId}, Client {targetClientId} -> {targetData.CharacterId}");

            // Respawn both player models
            RespawnPlayerObject(senderClientId, senderData.CharacterId);
            RespawnPlayerObject(targetClientId, targetData.CharacterId);
        }
        else
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { senderClientId }
                }
            };

            string targetPlayerName = NetPlayers[targetIdx].Name.ToString();
            ReceiveSwapDeclinedClientRpc(targetPlayerName, clientRpcParams);
        }
    }

    [ClientRpc]
    private void ReceiveSwapDeclinedClientRpc(string targetPlayerName, ClientRpcParams clientRpcParams = default)
    {
        Debug.LogWarning($"[CLIENT] Swap request declined by {targetPlayerName}");

        if (_lblWarning != null)
        {
            _lblWarning.text = $"{targetPlayerName.ToUpper()} ĐÃ TỪ CHỐI YÊU CẦU ĐỔI NHÂN VẬT!";
            _lblWarning.RemoveFromClassList("hidden-element");
            _lblWarning.style.display = DisplayStyle.Flex;

            CancelInvoke(nameof(HideWarningLabel));
            Invoke(nameof(HideWarningLabel), 3f);
        }
    }

    /// <summary>
    /// Thông báo về cho client khi server từ chối vì nhân vật đã bị người khác chiếm
    /// </summary>
    [ClientRpc]
    private void NotifyCharacterTakenClientRpc(int characterId, ClientRpcParams clientRpcParams = default)
    {
        string charName = (characterId >= 0 && characterId < _characters.Length)
            ? _characters[characterId].Name : "CHARACTER";
        Debug.LogWarning($"[CLIENT] Nhân vật {charName} đã bị người khác chọn!");

        if (_lblWarning != null)
        {
            _lblWarning.text = $"{charName} ĐÃ ĐƯỢC CHỌN!";
            _lblWarning.RemoveFromClassList("hidden-element");
            _lblWarning.style.display = DisplayStyle.Flex;
            CancelInvoke(nameof(HideWarningLabel));
            Invoke(nameof(HideWarningLabel), 2.5f);
        }
    }

    private async void LeaveRoom()
    {
        string roomId = PlayerPrefs.GetString("CurrentRoomID", "");
        if (!string.IsNullOrEmpty(roomId))
        {
            _ = AuthService.LeaveRoom(roomId);
        }

        PlayerPrefs.DeleteKey("CurrentRoomID");
        PlayerPrefs.DeleteKey("CurrentRoomName");
        PlayerPrefs.DeleteKey("IsRoomHost");
        PlayerPrefs.Save();
        
        if (_uiDocument != null)
        {
            _uiDocument.enabled = false;
        }

        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[Lobby] Đang tắt kết nối NetworkManager...");
            NetworkManager.Singleton.Shutdown();
        }
        
        // Bắt đầu Coroutine chờ giải phóng tài nguyên mạng trước khi tải cảnh Menu
        StartCoroutine(SafeLeaveRoomRoutine());
    }

    private IEnumerator SafeLeaveRoomRoutine()
    {
        Debug.Log("[Lobby] Đang chờ giải phóng bộ nhớ Netcode an toàn...");
        if (NetworkManager.Singleton != null)
        {
            while (NetworkManager.Singleton.IsListening)
            {
                yield return null;
            }
        }
        yield return new WaitForSeconds(0.2f);
        
        Debug.Log("[Lobby] Đang chuyển sang cảnh MainMenu...");
        if (SceneLoader.Instance != null)
        {
            _ = SceneLoader.Instance.LoadSceneAsync("MainMenu", "RETURNING TO MAIN MENU...");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    private void OnDestroy()
    {
        // Dọn dẹp khi Stop Play Mode trong Unity Editor hoặc thoát game
        // Tránh player cũ bị giữ lại trong NetworkManager (DontDestroyOnLoad)
        try
        {
            if (NetworkManager.Singleton == null) return;

            if (IsServer && NetPlayers != null)
            {
                Debug.Log("[Lobby] OnDestroy: Dọn dẹp player objects trước khi phá hủy...");

                // Despawn tất cả player objects trước khi dọn
                if (NetworkManager.Singleton.IsListening)
                {
                    foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                    {
                        if (client.PlayerObject != null)
                        {
                            try { client.PlayerObject.Despawn(true); }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"[Lobby] OnDestroy despawn warning: {ex.Message}");
                            }
                        }
                    }
                }

                // Xóa danh sách players
                try { NetPlayers.Clear(); } catch { }
            }

            // Huỷ đăng ký callback để tránh memory leak
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        catch (System.Exception ex)
        {
            // Bỏ qua lỗi khi dòng sự kiện xảy ra trong quá trình Unity Editor stop
            Debug.LogWarning($"[Lobby] OnDestroy warning (bình thường khi dừng Editor): {ex.Message}");
        }
    }
}
