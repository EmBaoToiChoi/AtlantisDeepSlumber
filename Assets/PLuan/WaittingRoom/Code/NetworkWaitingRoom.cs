using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class NetworkWaitingRoom : NetworkBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument _uiDocument;
    
    [Header("Slots & Prefabs")]
    public Transform[] slots = new Transform[4];
    public GameObject playerNetworkPrefab; // Default / Atlas
    public GameObject playerNetworkPrefab2; // Nyx
    public GameObject playerNetworkPrefab3; // Aurelia
    public GameObject playerNetworkPrefab4; // Titan

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
            Name = "ATLAS",
            Role = "VANGUARD EXPLORER",
            Description = "A fearless pioneer in trench exploration, Atlas uses customized pressurized gear to survive the deepest oceanic abysses.",
            PortraitStyleClass = "atlas-img",
            Skills = new LobbySkillInfo[]
            {
                new LobbySkillInfo { Name = "Trench Dash", Description = "Dashes forward a short distance, granting pressure immunity for 3 seconds.", IconStyleClass = "skill-icon-img" },
                new LobbySkillInfo { Name = "Sonar Scan", Description = "Emits acoustic pulse that reveals nearby resources and hazards for 8 seconds.", IconStyleClass = "skill-icon-img" },
                new LobbySkillInfo { Name = "Pressure Stabilizer", Description = "Stabilizes pressure resistance, reducing environmental damage by 30%.", IconStyleClass = "skill-icon-img" }
            }
        },
        new LobbyCharacterInfo
        {
            Name = "NYX",
            Role = "ABYSSAL INFILTRATOR",
            Description = "Born in the twilight zones of the ocean, Nyx controls shadows and acoustic frequencies to bypass deep-sea hazards unseen.",
            PortraitStyleClass = "nyx-img",
            Skills = new LobbySkillInfo[]
            {
                new LobbySkillInfo { Name = "Phantom Phase", Description = "Disappears into shadows, lowering aggro and boosting speed by 40%.", IconStyleClass = "skill-icon-img" },
                new LobbySkillInfo { Name = "Sonic Decoy", Description = "Deploys a bioluminescent hologram that attracts all nearby enemies.", IconStyleClass = "skill-icon-img" },
                new LobbySkillInfo { Name = "Shadow Strike", Description = "Strikes with pressurized plasma, dealing critical damage and stunning target.", IconStyleClass = "skill-icon-img" }
            }
        },
        new LobbyCharacterInfo
        {
            Name = "AURELIA",
            Role = "TECH BIOLOGIST",
            Description = "A brilliant scientist dedicated to understanding bioluminescent flora and fauna, Aurelia provides vital scanning and healing support.",
            PortraitStyleClass = "aurelia-img",
            Skills = new LobbySkillInfo[]
            {
                new LobbySkillInfo { Name = "Bio-Pulse", Description = "Releases restorative bioluminescent energy, healing nearby allies.", IconStyleClass = "skill-icon-img" },
                new LobbySkillInfo { Name = "Bioluminescent Shroud", Description = "Blinds all enemies in area, reducing their accuracy for 4 seconds.", IconStyleClass = "skill-icon-img" },
                new LobbySkillInfo { Name = "Nano Recovery", Description = "Injects nanites that restore 10 HP/sec and boost stamina recovery.", IconStyleClass = "skill-icon-img" }
            }
        },
        new LobbyCharacterInfo
        {
            Name = "TITAN",
            Role = "HEAVY GUARDIAN",
            Description = "Equipped with heavy tactical diving armor, Titan is a walking fortress designed to withstand structural attacks and shield the crew.",
            PortraitStyleClass = "titan-img",
            Skills = new LobbySkillInfo[]
            {
                new LobbySkillInfo { Name = "Kinetic Barrier", Description = "Deploys energy shield that blocks projectiles and boosts defense by 20%.", IconStyleClass = "skill-icon-img" },
                new LobbySkillInfo { Name = "Anchor Slam", Description = "Slams thermal anchor, dealing impact damage and slowing enemies.", IconStyleClass = "skill-icon-img" },
                new LobbySkillInfo { Name = "Threat Magnet", Description = "Forces nearby enemies to attack Titan while gaining 50% damage reduction.", IconStyleClass = "skill-icon-img" }
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

        if (_btnCloseDetails != null)
        {
            _btnCloseDetails.clicked += HideCharacterDetails;
        }

        // Toggles cho character selection panel
        _charSelectPanel = _root.Q<VisualElement>("char-select-panel");
        _btnToggleCharPanel = _root.Q<Button>("btn-toggle-char-panel");

        if (_btnToggleCharPanel != null)
        {
            _btnToggleCharPanel.clicked += ToggleCharacterPanel;
        }

        // Ẩn mặc định cho đỡ vướng
        if (_charSelectPanel != null)
        {
            _charSelectPanel.AddToClassList("panel-hidden-state");
            _charSelectPanel.style.display = DisplayStyle.None;
        }
        if (_btnToggleCharPanel != null)
        {
            _btnToggleCharPanel.text = "CHOOSE EXPLORER";
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
            _btnToggleCharPanel.text = "HIDE SELECTION";
        }
        else
        {
            _charPanelAnimCoroutine = StartCoroutine(HideCharPanelCoroutine());
            _btnToggleCharPanel.text = "CHOOSE EXPLORER";
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
        bool isShift = evt.shiftKey || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (isShift)
        {
            ShowCharacterDetails(charId);
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
            var playerUI = client.PlayerObject.GetComponent<PlayerWaitingRoomUI>();
            if (playerUI != null)
            {
                playerUI.NetName.Value = playerName;
                Debug.Log($"[SERVER] Đã cập nhật NetName trực tiếp cho nhân vật của Client {clientId}");
            }
        }
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

    private bool HasDuplicateCharacters()
    {
        if (NetPlayers == null || NetPlayers.Count <= 1) return false;
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            for (int j = i + 1; j < NetPlayers.Count; j++)
            {
                if (NetPlayers[i].CharacterId == NetPlayers[j].CharacterId)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void UpdatePlayerUI()
    {
        if (_lblPlayerCount != null) _lblPlayerCount.text = $"PLAYERS: {NetPlayers.Count}/4";
        
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
                statusLbl.text = "AVAILABLE";
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
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) selectorsPerChar0.Insert(0, "YOU");
                else selectorsPerChar0.Add(p.Name.ToString().ToUpper());
            }
            else if (charId == 1)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) selectorsPerChar1.Insert(0, "YOU");
                else selectorsPerChar1.Add(p.Name.ToString().ToUpper());
            }
            else if (charId == 2)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) selectorsPerChar2.Insert(0, "YOU");
                else selectorsPerChar2.Add(p.Name.ToString().ToUpper());
            }
            else if (charId == 3)
            {
                if (p.ClientId == NetworkManager.Singleton.LocalClientId) selectorsPerChar3.Insert(0, "YOU");
                else selectorsPerChar3.Add(p.Name.ToString().ToUpper());
            }
        }

        // Display selections
        UpdateCardUI(0, selectorsPerChar0);
        UpdateCardUI(1, selectorsPerChar1);
        UpdateCardUI(2, selectorsPerChar2);
        UpdateCardUI(3, selectorsPerChar3);

        // KIỂM TRA ĐIỀU KIỆN READY: Chỉ cần ít nhất 1 người chơi sẵn sàng (Testing)
        // Khi lên sản phẩm thực tế, có thể đổi lại thành NetPlayers.Count == 4 && readyCount == 4
        int readyCount = 0;
        foreach (var p in NetPlayers) if (p.IsReady) readyCount++;
        bool allReady = readyCount >= 1;

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

        // Handle duplicate warning label
        if (_lblWarning != null)
        {
            if (duplicatesExist)
            {
                _lblWarning.RemoveFromClassList("hidden-element");
                _lblWarning.style.display = DisplayStyle.Flex;
            }
            else
            {
                _lblWarning.AddToClassList("hidden-element");
                _lblWarning.style.display = DisplayStyle.None;
            }
        }

        if (_btnStart != null)
        {
            // Chỉ hiển thị nút Start cho Chủ phòng (Slot 0) để bấm bắt đầu
            _btnStart.style.display = isRoomHost ? DisplayStyle.Flex : DisplayStyle.None;
            
            bool canStart = allReady && !duplicatesExist;
            _btnStart.SetEnabled(canStart);
            
            if (isRoomHost)
            {
                _btnStart.RemoveFromClassList("hidden-element");
            }
            else
            {
                _btnStart.AddToClassList("hidden-element");
            }
        }

        // Cập nhật màu nút Ready cho bản thân
        UpdateReadyButtonState();
    }

    private void UpdateCardUI(int charId, List<string> selectors)
    {
        var card = _root.Q<VisualElement>($"char-card-{charId}");
        var statusLbl = _root.Q<Label>($"char-status-{charId}");

        if (selectors.Count > 0)
        {
            if (selectors.Contains("YOU") && card != null)
            {
                card.AddToClassList("select-active");
            }

            if (statusLbl != null)
            {
                statusLbl.text = string.Join(" + ", selectors);
                statusLbl.AddToClassList("active-status");
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
                _btnReady.text = p.IsReady ? "READY!" : "READY?";
                break;
            }
        }
    }

    private void ToggleReady()
    {
        Debug.Log($"[CLIENT] Nút Ready được click! LocalClientId={NetworkManager.Singleton.LocalClientId}");
        ToggleReadyServerRpc();
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
        
        for (int i = 0; i < NetPlayers.Count; i++)
        {
            if (NetPlayers[i].ClientId == clientId)
            {
                var p = NetPlayers[i];
                p.CharacterId = characterId;
                NetPlayers[i] = p;
                Debug.Log($"[SERVER] Đã cập nhật CharacterId={characterId} cho Client {clientId}");
                
                // Respawn mô hình 3D cho client này trên Server
                RespawnPlayerObject(clientId, characterId);
                break;
            }
        }
    }

    private GameObject GetPlayerPrefab(int characterId)
    {
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
            CharacterId = slot // Gán nhân vật mặc định theo Slot của người chơi để tránh trùng lặp ban đầu
        };

        NetPlayers.Add(newData);
        Debug.Log($"[Lobby] Đã thêm {playerName} vào Slot {slot} với nhân vật mặc định {slot}");
        
        SpawnPlayerObject(clientId);
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
        Debug.Log("[CLIENT] Chủ phòng click START EXPEDITION! Đang gửi lệnh ServerRpc khởi động...");
        StartGameServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartGameServerRpc() {
        if (!IsServer) return;

        // Check duplicates on server side too to be absolutely safe
        if (HasDuplicateCharacters())
        {
            Debug.LogError("[SERVER] Không thể bắt đầu game vì có trùng nhân vật!");
            return;
        }

        Debug.Log("--- ĐANG GỌI LOAD SCENE: HBao........................................ ---");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null) {
            //NetworkManager.Singleton.SceneManager.LoadScene("HBao", UnityEngine.SceneManagement.LoadSceneMode.Single);
            NetworkManager.Singleton.SceneManager.LoadScene("HBao", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private async void LeaveRoom()
    {
        string roomId = PlayerPrefs.GetString("CurrentRoomID", "");
        if (!string.IsNullOrEmpty(roomId)) await AuthService.LeaveRoom(roomId);
        
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
        // Đợi 2 frame để đảm bảo Unity dọn dẹp sạch sẽ các thực thể mạng trước khi tải cảnh mới
        yield return null;
        yield return null;
        
        Debug.Log("[Lobby] Đang chuyển sang cảnh MainMenu...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
