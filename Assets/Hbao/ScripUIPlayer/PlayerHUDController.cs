using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class PlayerHUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [System.Serializable]
    public struct PlayerHUDProfile
    {
        public string className;
        public Sprite avatarSprite;
        public Sprite weapon1Sprite;
        public Sprite weapon2Sprite;
        public Sprite skillQSprite;
        public Sprite skillRSprite;
        public Sprite skillESprite;
    }

    [Header("Player Profiles (4 Players)")]
    public System.Collections.Generic.List<PlayerHUDProfile> hudProfiles;

    [Header("UI Testing")]
    [Tooltip("Chọn index từ 0 đến 3 để test nhanh giao diện lớp nhân vật khi ấn Play")]
    public int testProfileIndex = 0;

    private static IPlayerHUDTarget localPlayerTarget;
    public static IPlayerHUDTarget LocalPlayerTarget
    {
        get
        {
            if (localPlayerTarget != null && localPlayerTarget is UnityEngine.Object obj && obj == null)
            {
                localPlayerTarget = null;
            }
            return localPlayerTarget;
        }
        set
        {
            localPlayerTarget = value;
            if (localPlayerTarget != null)
            {
                int classIdx = localPlayerTarget.CharacterClassIndex;

                // Xác định class index chuẩn xác dựa trên kiểu lớp C# thực tế để tránh ghi đè PlayerPrefs khi test nhiều client trên cùng PC
                if (localPlayerTarget is LeoPlayer || localPlayerTarget is LeoAssassin)
                {
                    classIdx = 0;
                }
                else if (localPlayerTarget is ElenaPlayer || localPlayerTarget is ElenaArcher)
                {
                    classIdx = 2;
                }
                else if (localPlayerTarget is MayaSupport)
                {
                    classIdx = 1;
                }
                else if (localPlayerTarget is ArthurTanker)
                {
                    classIdx = 3;
                }

                Debug.Log($"[PlayerHUDController] LocalPlayerTarget set! Tự động kích hoạt HUD cho classIdx: {classIdx}");

                PlayerHUDManager hudManager = PlayerHUDManager.Instance != null ? PlayerHUDManager.Instance : FindAnyObjectByType<PlayerHUDManager>();
                if (hudManager != null)
                {
                    var hud = hudManager.ActivateHUD(classIdx);
                    if (hud != null)
                    {
                        hud.SetupPlayerProfile(classIdx);
                    }
                }
            }
        }
    }

    private VisualElement hpFill;
    private VisualElement mpFill;
    private VisualElement expFill;

    private VisualElement hpCatchUp;
    private VisualElement screenDamageFlash;
    private float targetHpPercent = 1f;
    private float currentCatchUpPercent = 1f;
    private float catchUpDelayTimer = 0f;
    private float damageFlashOpacity = 0f;
    private bool isFirstHealthSet = true;

    // Tham chiếu trực tiếp tới phần tử chứa icon
    private VisualElement micIcon;
    private VisualElement rawPlayerImage; // Tham chiếu tới Avatar Player để đổi ảnh động
    private VisualElement weaponSlot1;
    private VisualElement weaponSlot2;
    private VisualElement weaponImg1; // Tham chiếu tới ảnh vũ khí 1 để đổi ảnh động

    // Kỹ năng
    private VisualElement cooldownQ;
    private VisualElement cooldownR;
    private VisualElement cooldownE;
    private Label cooldownTextQ;
    private Label cooldownTextR;
    private Label cooldownTextE;
    private float cooldownTimeQ = 10f; // Thời gian hồi chiêu Q
    private float cooldownTimeR = 15f; // Thời gian hồi chiêu R
    private float cooldownTimeE = 12f; // Thời gian hồi chiêu E
    private float currentCooldownQ = 0f;
    private float currentCooldownR = 0f;
    private float currentCooldownE = 0f;

    // Khóa kỹ năng
    private VisualElement lockQ;
    private VisualElement lockR;
    private VisualElement lockE;
    private VisualElement lockIconQ; // Tham chiếu tới icon ổ khóa để rung
    private VisualElement lockIconR; // Tham chiếu tới icon ổ khóa để rung
    private VisualElement lockIconE; // Tham chiếu tới icon ổ khóa để rung
    private VisualElement skillImgQ; // Tham chiếu tới hình ảnh kỹ năng để ẩn
    private VisualElement skillImgR; // Tham chiếu tới hình ảnh kỹ năng để ẩn
    private VisualElement skillImgE; // Tham chiếu tới hình ảnh kỹ năng để ẩn
    private bool isSkillsUnlocked = false; // Trạng thái đã mở khóa kỹ năng hay chưa
    public int currentSelectedWeapon = 1; // Thêm biến lưu vũ khí đang chọn
    public static bool isAnyUIOpen = false; // Trạng thái static báo hiệu bất kỳ UI nào đang mở

    // Mặc định false -> Vào game chưa ấn M sẽ là tắt Mic
    private bool isMicOn = false;

    // Cảnh báo vũ khí và Hành trang (Tab)
    private VisualElement worldMapOverlay;
    private VisualElement inventoryOverlay;
    private Label weaponWarning;

    // UI nâng cấp chỉ số
    private Label upgradePointsText;
    private Label hpLevelText;
    private Label mpLevelText;
    private Label cooldownLevelText;
    private Label damageLevelText;
    private Button btnUpgradeHp;
    private Button btnUpgradeMp;
    private Button btnUpgradeCooldown;
    private Button btnUpgradeDamage;

    private VisualElement weaponLock2; // Tham chiếu tới overlay khóa vũ khí
    private VisualElement lockIcon2;   // Tham chiếu tới icon ổ khóa để rung
    private VisualElement weaponImg2;  // Tham chiếu tới hình ảnh vũ khí để ẩn
    private bool isWeapon2Locked = true;
    private float warningTimer = 0f;
    private const float WARNING_DURATION = 2f;

    private VisualElement weaponDurabilityFill1;
    private VisualElement weaponDurabilityFill2;
    private VisualElement interactionPrompt;
    private Label interactionPromptText;
    private Label interactionPromptKeyText;
    private VisualElement tooltipElement;
    
    // Hệ thống Hướng Dẫn Phím Nóng Động
    private VisualElement hotkeysHintPanel;
    private VisualElement idleHintsGroup;
    private VisualElement actionHintsGroup;
    private VisualElement hintWeapon2;
    private VisualElement hintSkills;
    private Label tooltipTitle;
    private Label tooltipDesc;
    private System.Collections.Generic.List<VisualElement> inventorySlotsUI = new System.Collections.Generic.List<VisualElement>();
    private string[] currentInventoryData;
    private int draggedSlotIndex = -1;
    private bool isCooldownActive = false;
    private VisualElement dragGhost;
    private VisualElement teammatesContainer;
    private System.Collections.Generic.Dictionary<ulong, VisualElement> teammateCards = new System.Collections.Generic.Dictionary<ulong, VisualElement>();
    private bool isUIInitialized = false;
    private int lastSelectedProfileIndex = -1;

    [Header("Item Sprites Settings")]
    public Sprite repairHammerSprite;
    public Sprite ngoc1Sprite;
    public Sprite ngoc2Sprite;

    void OnEnable()
    {
        InitializeUI();
    }

    void OnDisable()
    {
        // QUAN TRỌNG: Reset toàn bộ state khi HUD bị tắt (SetActive false).
        // Khi UI Toolkit rebuild lại visual tree sau lần SetActive(true) tiếp theo,
        // tất cả các tham chiếu element cũ sẽ là dead reference -> phải re-query lại.
        isUIInitialized = false;

        // Reset tất cả tham chiếu VisualElement để InitializeUI() re-query lại từ tree mới
        hpFill = null; mpFill = null; expFill = null;
        hpCatchUp = null;
        screenDamageFlash = null;
        isFirstHealthSet = true;
        micIcon = null; rawPlayerImage = null;
        weaponSlot1 = null; weaponSlot2 = null;
        weaponImg1 = null; weaponImg2 = null;
        weaponLock2 = null; lockIcon2 = null;
        cooldownQ = null; cooldownR = null; cooldownE = null;
        cooldownTextQ = null; cooldownTextR = null; cooldownTextE = null;
        lockQ = null; lockR = null; lockE = null;
        lockIconQ = null; lockIconR = null; lockIconE = null;
        skillImgQ = null; skillImgR = null; skillImgE = null;
        worldMapOverlay = null; inventoryOverlay = null; weaponWarning = null;
        weaponDurabilityFill1 = null; weaponDurabilityFill2 = null;
        interactionPrompt = null; interactionPromptText = null; interactionPromptKeyText = null;
        hotkeysHintPanel = null; idleHintsGroup = null; actionHintsGroup = null;
        hintWeapon2 = null; hintSkills = null;
        upgradePointsText = null; hpLevelText = null; mpLevelText = null;
        cooldownLevelText = null; damageLevelText = null;
        btnUpgradeHp = null; btnUpgradeMp = null; btnUpgradeCooldown = null; btnUpgradeDamage = null;
        tooltipElement = null; tooltipTitle = null; tooltipDesc = null;
        dragGhost = null; teammatesContainer = null;
        inventorySlotsUI = new System.Collections.Generic.List<VisualElement>();
        teammateCards = new System.Collections.Generic.Dictionary<ulong, VisualElement>();

        Debug.Log($"[PlayerHUDController] OnDisable - reset state, sẽ re-init khi Enable lại. ProfileIndex giữ nguyên: {lastSelectedProfileIndex}");
    }


    public void InitializeUI()
    {
        if (isUIInitialized) return;
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        var root = uiDocument.rootVisualElement;

        hpFill = root.Q<VisualElement>("hp-fill");
        mpFill = root.Q<VisualElement>("mp-fill");
        expFill = root.Q<VisualElement>("exp-fill");

        if (hpFill != null && hpCatchUp == null)
        {
            hpCatchUp = new VisualElement();
            hpCatchUp.name = "hp-catchup";
            hpCatchUp.style.position = Position.Absolute;
            hpCatchUp.style.left = 0;
            hpCatchUp.style.top = 0;
            hpCatchUp.style.bottom = 0;
            hpCatchUp.style.backgroundColor = new Color(0.85f, 0.15f, 0.15f, 0.75f);
            hpCatchUp.style.width = Length.Percent(100f);
            
            var hpTrack = hpFill.parent;
            if (hpTrack != null)
            {
                hpTrack.Insert(0, hpCatchUp);
            }
        }

        if (screenDamageFlash == null)
        {
            screenDamageFlash = new VisualElement();
            screenDamageFlash.name = "screen-damage-flash";
            screenDamageFlash.style.position = Position.Absolute;
            screenDamageFlash.style.left = 0;
            screenDamageFlash.style.top = 0;
            screenDamageFlash.style.right = 0;
            screenDamageFlash.style.bottom = 0;
            screenDamageFlash.style.backgroundColor = new Color(1f, 0f, 0f, 0f);
            screenDamageFlash.pickingMode = PickingMode.Ignore;
            
            root.Add(screenDamageFlash);
        }

        weaponDurabilityFill1 = root.Q<VisualElement>("weapon-durability-fill-1");
        weaponDurabilityFill2 = root.Q<VisualElement>("weapon-durability-fill-2");
        interactionPrompt = root.Q<VisualElement>("interaction-prompt");
        interactionPromptText = root.Q<Label>("interaction-prompt-text");
        interactionPromptKeyText = root.Q<Label>(className: "key-badge-f-text");

        // Tìm các phần tử của bảng phím nóng
        hotkeysHintPanel = root.Q<VisualElement>("hotkeys-hint-panel");
        idleHintsGroup = root.Q<VisualElement>("idle-hints-group");
        actionHintsGroup = root.Q<VisualElement>("action-hints-group");
        hintWeapon2 = root.Q<VisualElement>("hint-weapon2");
        hintSkills = root.Q<VisualElement>("hint-skills");

        // Tìm UI Mic Icon trực tiếp
        micIcon = root.Q<VisualElement>("mic-icon");
        rawPlayerImage = root.Q<VisualElement>("raw-player-image");

        weaponSlot1 = root.Q<VisualElement>("weapon-slot-1");
        weaponSlot2 = root.Q<VisualElement>("weapon-slot-2");
        weaponImg1 = root.Q<VisualElement>("weapon-img-1");

        // Tìm UI Kỹ năng
        cooldownQ = root.Q<VisualElement>("skill-cooldown-q");
        cooldownR = root.Q<VisualElement>("skill-cooldown-r");
        cooldownE = root.Q<VisualElement>("skill-cooldown-e");
        cooldownTextQ = root.Q<Label>("skill-cooldown-text-q");
        cooldownTextR = root.Q<Label>("skill-cooldown-text-r");
        cooldownTextE = root.Q<Label>("skill-cooldown-text-e");

        lockQ = root.Q<VisualElement>("skill-lock-q");
        lockR = root.Q<VisualElement>("skill-lock-r");
        lockE = root.Q<VisualElement>("skill-lock-e");

        if (lockQ != null) lockIconQ = lockQ.Q<VisualElement>(null, "skill-lock-icon");
        if (lockR != null) lockIconR = lockR.Q<VisualElement>(null, "skill-lock-icon");
        if (lockE != null) lockIconE = lockE.Q<VisualElement>(null, "skill-lock-icon");

        skillImgQ = root.Q<VisualElement>("skill-img-q");
        skillImgR = root.Q<VisualElement>("skill-img-r");
        skillImgE = root.Q<VisualElement>("skill-img-e");

        // Ẩn kỹ năng ngay từ đầu nếu đang khóa
        if (!isSkillsUnlocked)
        {
            if (skillImgQ != null) skillImgQ.style.visibility = Visibility.Hidden;
            if (skillImgR != null) skillImgR.style.visibility = Visibility.Hidden;
            if (skillImgE != null) skillImgE.style.visibility = Visibility.Hidden;
        }

        // Tìm Label cảnh báo, Overlay bản đồ và Hành trang
        worldMapOverlay = root.Q<VisualElement>("world-map-overlay");
        if (worldMapOverlay != null)
        {
            worldMapOverlay.pickingMode = PickingMode.Ignore; // Mặc định ẩn, bỏ qua cản chuột
        }
        inventoryOverlay = root.Q<VisualElement>("inventory-overlay");
        if (inventoryOverlay != null)
        {
            inventoryOverlay.pickingMode = PickingMode.Ignore; // Mặc định ẩn, bỏ qua cản chuột
            inventoryOverlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == inventoryOverlay)
                {
                    ToggleInventory();
                }
            });
        }
        weaponWarning = root.Q<Label>("weapon-warning");
        weaponLock2 = root.Q<VisualElement>("weapon-lock-2");
        if (weaponLock2 != null) lockIcon2 = weaponLock2.Q<VisualElement>(null, "weapon-lock-icon");
        weaponImg2 = root.Q<VisualElement>("weapon-img-2");

        // Ẩn vũ khí 2 ngay từ đầu nếu đang khóa
        if (isWeapon2Locked && weaponImg2 != null)
        {
            weaponImg2.style.visibility = Visibility.Hidden;
        }

        // Tìm kiếm nhãn tên thực tế của tài khoản từ PlayerPrefs
        var charNameLabel = root.Q<Label>("character-name-label");
        if (charNameLabel != null)
        {
            charNameLabel.text = PlayerPrefs.GetString("AuthDisplayName", "HERO").ToUpper();
        }

        // Tìm kiếm các thành phần giao diện nâng cấp
        upgradePointsText = root.Q<Label>("upgrade-points-text");
        hpLevelText = root.Q<Label>("hp-level-text");
        mpLevelText = root.Q<Label>("mp-level-text");
        cooldownLevelText = root.Q<Label>("cooldown-level-text");
        damageLevelText = root.Q<Label>("damage-level-text");

        btnUpgradeHp = root.Q<Button>("btn-upgrade-hp");
        btnUpgradeMp = root.Q<Button>("btn-upgrade-mp");
        btnUpgradeCooldown = root.Q<Button>("btn-upgrade-cooldown");
        btnUpgradeDamage = root.Q<Button>("btn-upgrade-damage");

        if (btnUpgradeHp != null) btnUpgradeHp.clicked += () => UpgradeStat(0);
        if (btnUpgradeMp != null) btnUpgradeMp.clicked += () => UpgradeStat(1);
        if (btnUpgradeCooldown != null) btnUpgradeCooldown.clicked += () => UpgradeStat(2);
        if (btnUpgradeDamage != null) btnUpgradeDamage.clicked += () => UpgradeStat(3);

        // Khởi tạo và dịch ngôn ngữ giao diện HUD
        LocalizationManager.Initialize();
        ApplyHUDLocalization();

        // Đồng bộ trạng thái UI ngay khi load game (Tắt)
        UpdateMicUI();
        SelectWeapon(1); // Mặc định chọn vũ khí 1 khi vào game

        // Tải nhân vật đã chọn ở lobby nếu chưa được gán động từ LocalPlayerTarget
        if (lastSelectedProfileIndex == -1)
        {
            int selectedChar = PlayerPrefs.GetInt("SelectedCharacterId", testProfileIndex);
            lastSelectedProfileIndex = selectedChar;
        }

        // Tìm và thiết lập danh sách 10 ô Hành Trang (Inventory Slots)
        inventorySlotsUI = root.Query<VisualElement>(className: "inventory-slot").ToList();
        for (int i = 0; i < inventorySlotsUI.Count; i++)
        {
            int index = i;
            VisualElement slot = inventorySlotsUI[i];
            slot.name = $"inventory-slot-{index}";

            // Vẽ nhãn số thứ tự mờ ở góc ô hành trang
            slot.Clear();
            Label indexLabel = new Label((index + 1).ToString());
            indexLabel.AddToClassList("inventory-slot-index");
            slot.Add(indexLabel);

            // Đăng ký các sự kiện Drag & Drop và Hover Tooltip
            slot.RegisterCallback<PointerDownEvent>(evt => OnSlotPointerDown(evt, index));
            slot.RegisterCallback<PointerUpEvent>(evt => OnSlotPointerUp(evt, index));
            slot.RegisterCallback<PointerEnterEvent>(evt => OnSlotPointerEnter(evt, index));
            slot.RegisterCallback<PointerLeaveEvent>(evt => OnSlotPointerLeave(evt, index));
            slot.RegisterCallback<PointerMoveEvent>(evt => OnSlotPointerMove(evt, index));
        }

        // Tạo sẵn phần tử hiển thị mô tả (Tooltip)
        tooltipElement = new VisualElement();
        tooltipElement.AddToClassList("inventory-tooltip");
        tooltipElement.pickingMode = PickingMode.Ignore; // Tránh cản chuột
        tooltipTitle = new Label();
        tooltipTitle.AddToClassList("inventory-tooltip-title");
        tooltipTitle.pickingMode = PickingMode.Ignore;
        tooltipDesc = new Label();
        tooltipDesc.AddToClassList("inventory-tooltip-desc");
        tooltipDesc.pickingMode = PickingMode.Ignore;
        tooltipElement.Add(tooltipTitle);
        tooltipElement.Add(tooltipDesc);
        root.Add(tooltipElement);

        // Tạo sẵn phần tử hiển thị kéo thả (Drag Ghost) theo con trỏ chuột
        dragGhost = new VisualElement();
        dragGhost.AddToClassList("dragged-item-ghost");
        dragGhost.style.position = Position.Absolute;
        dragGhost.style.width = 54f;
        dragGhost.style.height = 54f;
        dragGhost.pickingMode = PickingMode.Ignore; // CỰC KỲ QUAN TRỌNG: để không chặn panel.Pick() khi nhả chuột!
        dragGhost.style.display = DisplayStyle.None;
        root.Add(dragGhost);

        isUIInitialized = true;
        Debug.Log("[PlayerHUDController] UI Toolkit đã được khởi tạo thành công!");

        if (lastSelectedProfileIndex != -1)
        {
            Debug.Log($"[PlayerHUDController] Áp dụng lại profile index {lastSelectedProfileIndex} sau khi khởi tạo UI xong.");
            SetupPlayerProfile(lastSelectedProfileIndex);
        }
    }

    /// <summary>
    /// Áp dụng dịch đa ngôn ngữ cho toàn bộ các nhãn tĩnh của HUD/Hành trang
    /// </summary>
    private void ApplyHUDLocalization()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        var root = uiDocument.rootVisualElement;

        var inventorySubtitle = root.Q<Label>("inventory-subtitle");
        if (inventorySubtitle != null) inventorySubtitle.text = LocalizationManager.Get("hud_tab_close");

        var upgradesTitle = root.Q<Label>("upgrades-title");
        if (upgradesTitle != null) upgradesTitle.text = LocalizationManager.Get("hud_upgrades_title");

        var upgradeNameHp = root.Q<Label>("upgrade-name-hp");
        if (upgradeNameHp != null) upgradeNameHp.text = LocalizationManager.Get("hud_stat_hp");

        var upgradeNameMp = root.Q<Label>("upgrade-name-mp");
        if (upgradeNameMp != null) upgradeNameMp.text = LocalizationManager.Get("hud_stat_mp");

        var upgradeNameCooldown = root.Q<Label>("upgrade-name-cooldown");
        if (upgradeNameCooldown != null) upgradeNameCooldown.text = LocalizationManager.Get("hud_stat_cooldown");

        var upgradeNameDamage = root.Q<Label>("upgrade-name-damage");
        if (upgradeNameDamage != null) upgradeNameDamage.text = LocalizationManager.Get("hud_stat_damage");
    }

    void Update()
    {
        InitializeUI(); // Đảm bảo khởi tạo nếu OnEnable chạy trước khi rootVisualElement sẵn sàng
        UpdateTeammatesHUD();

        if (Keyboard.current != null)
        {
            // Khi ấn T sẽ đổi trạng thái (Sử dụng Input System mới)
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                Debug.Log("Đã ấn T để chuyển đổi trạng thái Mic");
                ToggleMic();
            }

            // Chuyển vũ khí 1 và 2
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                SelectWeapon(1);
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                SelectWeapon(2);
            }

            // Mở khóa vũ khí 2 bằng phím K
            if (Keyboard.current.kKey.wasPressedThisFrame && isWeapon2Locked)
            {
                isWeapon2Locked = false;
                Debug.Log("Đã mở khóa Vũ khí 2!");

                if (weaponLock2 != null)
                {
                    weaponLock2.AddToClassList("unlocked-anim");
                }

                // Hiện lại vũ khí khi mở khóa
                if (weaponImg2 != null)
                {
                    weaponImg2.style.visibility = Visibility.Visible;
                }
                NotifyHUDChange();
            }

            // Mở/đóng Bản đồ thế giới bằng phím M
            if (Keyboard.current.mKey.wasPressedThisFrame)
            {
                if (worldMapOverlay != null)
                {
                    worldMapOverlay.ToggleInClassList("show-map");
                    bool isNowVisible = worldMapOverlay.ClassListContains("show-map");
                    worldMapOverlay.pickingMode = isNowVisible ? PickingMode.Position : PickingMode.Ignore; // Kích hoạt cản/nhận chuột khi hiện
                    isAnyUIOpen = isNowVisible; // Đồng bộ trạng thái UI đang mở

                    // Hiện/ẩn chuột cho Bản đồ
                    if (LocalPlayerTarget != null)
                    {
                        LocalPlayerTarget.SetCursorLock(!isNowVisible);
                    }
                    else
                    {
                        if (isNowVisible)
                        {
                            UnityEngine.Cursor.lockState = CursorLockMode.None;
                            UnityEngine.Cursor.visible = true;
                        }
                        else
                        {
                            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                            UnityEngine.Cursor.visible = false;
                        }
                    }

                    Debug.Log("Đã " + (isNowVisible ? "mở" : "đóng") + " Bản đồ thế giới với hiệu ứng");
                }
            }

            // Mở/đóng Hành trang bằng phím Tab
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleInventory();
            }


            // Mở khóa kỹ năng bằng phím L
            if (Keyboard.current.lKey.wasPressedThisFrame && !isSkillsUnlocked)
            {
                isSkillsUnlocked = true;
                Debug.Log("Đã mở khóa Kỹ năng!");

                // Thêm class để kích hoạt hiệu ứng rớt ổ khóa trong USS
                if (lockQ != null) lockQ.AddToClassList("unlocked-anim");
                if (lockR != null) lockR.AddToClassList("unlocked-anim");
                if (lockE != null) lockE.AddToClassList("unlocked-anim");

                // Hiện lại hình ảnh kỹ năng khi mở khóa
                if (skillImgQ != null) skillImgQ.style.visibility = Visibility.Visible;
                if (skillImgR != null) skillImgR.style.visibility = Visibility.Visible;
                if (skillImgE != null) skillImgE.style.visibility = Visibility.Visible;
                NotifyHUDChange();
            }

            // Kích hoạt Skill Q (chỉ khi đã mở khóa)
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                if (isSkillsUnlocked)
                {
                    if (currentCooldownQ <= 0f)
                    {
                        currentCooldownQ = cooldownTimeQ;
                        Debug.Log("Đã dùng kỹ năng Q");
                    }
                }
                else
                {
                    ShowSkillWarning(lockIconQ);
                }
            }

            // Kích hoạt Skill R (chỉ khi đã mở khóa)
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (isSkillsUnlocked)
                {
                    if (currentCooldownR <= 0f)
                    {
                        currentCooldownR = cooldownTimeR;
                        Debug.Log("Đã dùng kỹ năng R");
                    }
                }
                else
                {
                    ShowSkillWarning(lockIconR);
                }
            }

            // Kích hoạt Skill E (chỉ khi đã mở khóa)
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                bool isPromptingE = interactionPrompt != null &&
                                    interactionPrompt.ClassListContains("show-prompt") &&
                                    interactionPromptText != null &&
                                    interactionPromptText.text.Contains("[E]");

                bool isDialogueOpen = SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive;

                if (!isPromptingE && !isDialogueOpen)
                {
                    if (isSkillsUnlocked)
                    {
                        if (currentCooldownE <= 0f)
                        {
                            currentCooldownE = cooldownTimeE;
                            Debug.Log("Đã dùng kỹ năng E");
                        }
                    }
                    else
                    {
                        ShowSkillWarning(lockIconE);
                    }
                }
            }
        }

        // Cập nhật hiệu ứng hồi chiêu Q
        if (currentCooldownQ > 0f)
        {
            currentCooldownQ -= Time.deltaTime;
            if (cooldownQ != null)
            {
                float percent = Mathf.Clamp01(currentCooldownQ / cooldownTimeQ) * 100f;
                cooldownQ.style.height = Length.Percent(percent);
            }
            if (cooldownTextQ != null)
            {
                cooldownTextQ.text = Mathf.CeilToInt(currentCooldownQ).ToString();
                cooldownTextQ.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            if (cooldownQ != null && cooldownQ.style.height.value.value > 0)
                cooldownQ.style.height = Length.Percent(0);
            if (cooldownTextQ != null && cooldownTextQ.style.display == DisplayStyle.Flex)
                cooldownTextQ.style.display = DisplayStyle.None;
        }

        // Cập nhật hiệu ứng hồi chiêu R
        if (currentCooldownR > 0f)
        {
            currentCooldownR -= Time.deltaTime;
            if (cooldownR != null)
            {
                float percent = Mathf.Clamp01(currentCooldownR / cooldownTimeR) * 100f;
                cooldownR.style.height = Length.Percent(percent);
            }
            if (cooldownTextR != null)
            {
                cooldownTextR.text = Mathf.CeilToInt(currentCooldownR).ToString();
                cooldownTextR.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            if (cooldownR != null && cooldownR.style.height.value.value > 0)
                cooldownR.style.height = Length.Percent(0);
            if (cooldownTextR != null && cooldownTextR.style.display == DisplayStyle.Flex)
                cooldownTextR.style.display = DisplayStyle.None;
        }

        // Cập nhật hiệu ứng hồi chiêu E
        if (currentCooldownE > 0f)
        {
            currentCooldownE -= Time.deltaTime;
            if (cooldownE != null)
            {
                float percent = Mathf.Clamp01(currentCooldownE / cooldownTimeE) * 100f;
                cooldownE.style.height = Length.Percent(percent);
            }
            if (cooldownTextE != null)
            {
                cooldownTextE.text = Mathf.CeilToInt(currentCooldownE).ToString();
                cooldownTextE.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            if (cooldownE != null && cooldownE.style.height.value.value > 0)
                cooldownE.style.height = Length.Percent(0);
            if (cooldownTextE != null && cooldownTextE.style.display == DisplayStyle.Flex)
                cooldownTextE.style.display = DisplayStyle.None;
        }

        // Cập nhật timer cảnh báo
        if (warningTimer > 0f)
        {
            warningTimer -= Time.deltaTime;
            if (warningTimer <= 0f && weaponWarning != null)
            {
                weaponWarning.RemoveFromClassList("show-warning");
            }
        }

        // Animate HP catch-up bar and screen damage flash
        if (catchUpDelayTimer > 0f)
        {
            catchUpDelayTimer -= Time.deltaTime;
        }
        else if (currentCatchUpPercent > targetHpPercent)
        {
            currentCatchUpPercent = Mathf.MoveTowards(currentCatchUpPercent, targetHpPercent, Time.deltaTime * 0.4f);
            if (hpCatchUp != null)
            {
                hpCatchUp.style.width = Length.Percent(currentCatchUpPercent * 100f);
            }
        }

        if (damageFlashOpacity > 0f)
        {
            damageFlashOpacity = Mathf.MoveTowards(damageFlashOpacity, 0f, Time.deltaTime * 1.0f);
            if (screenDamageFlash != null)
            {
                screenDamageFlash.style.backgroundColor = new Color(0.85f, 0.1f, 0.1f, damageFlashOpacity);
            }
        }

        UpdateHotkeysHint();
    }

    public void ToggleMic()
    {
        isMicOn = !isMicOn;
        Debug.Log("Trạng thái Mic hiện tại: " + (isMicOn ? "Mở" : "Tắt"));
        UpdateMicUI();
    }

    public void ToggleInventory()
    {
        if (inventoryOverlay != null)
        {
            inventoryOverlay.ToggleInClassList("show-inventory");
            bool isNowVisible = inventoryOverlay.ClassListContains("show-inventory");
            inventoryOverlay.pickingMode = isNowVisible ? PickingMode.Position : PickingMode.Ignore; // Kích hoạt cản/nhận chuột khi hiện
            Debug.Log("Đã " + (isNowVisible ? "mở" : "đóng") + " hành trang");

            // Đồng bộ trạng thái static UI
            isAnyUIOpen = isNowVisible;

            // Đảm bảo EventSystem được cấu hình đúng khi mở UI
            if (isNowVisible)
            {
                SetupEventSystemForInputSystem();
            }

            // Tự động ẩn/hiện con trỏ chuột phù hợp với trạng thái UI hành trang
            if (LocalPlayerTarget != null)
            {
                LocalPlayerTarget.SetCursorLock(!isNowVisible);
            }
            else
            {
                if (isNowVisible)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                }
                else
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }

            // Ẩn tooltip khi đóng hành trang để tránh tình trạng tooltip hiển thị thừa
            if (!isNowVisible && tooltipElement != null)
            {
                tooltipElement.style.opacity = 0f;
            }

            // Tự động lưu trạng thái người chơi vào MongoDB khi đóng hành trang
            if (!isNowVisible && LocalPlayerTarget != null)
            {
                if (LocalPlayerTarget.IsStandaloneMode || (LocalPlayerTarget.IsSpawned && LocalPlayerTarget.IsOwner))
                {
                    LocalPlayerTarget.SavePlayerStateToDatabase();
                }
            }
        }
    }

    private void UpdateMicUI()
    {
        if (micIcon == null)
        {
            Debug.LogWarning("Không tìm thấy mic-icon trong UIDocument! Hãy kiểm tra lại file UXML.");
            return;
        }

        if (isMicOn)
        {
            micIcon.RemoveFromClassList("mic-off");
            micIcon.AddToClassList("mic-on");
        }
        else
        {
            micIcon.RemoveFromClassList("mic-on");
            micIcon.AddToClassList("mic-off");
        }
    }

    // --- Giữ nguyên các hàm cập nhật HP/MP/EXP của bạn bên dưới ---
    public void SetHealth(float percentage)
    {
        float clamped = Mathf.Clamp01(percentage);
        
        if (isFirstHealthSet)
        {
            isFirstHealthSet = false;
            targetHpPercent = clamped;
            currentCatchUpPercent = clamped;
            if (hpCatchUp != null) hpCatchUp.style.width = Length.Percent(clamped * 100f);
        }
        else
        {
            if (clamped < targetHpPercent)
            {
                catchUpDelayTimer = 0.5f;
                damageFlashOpacity = 0.35f;
            }
            else if (clamped > targetHpPercent)
            {
                currentCatchUpPercent = clamped;
                if (hpCatchUp != null) hpCatchUp.style.width = Length.Percent(clamped * 100f);
            }
            targetHpPercent = clamped;
        }

        if (hpFill != null)
        {
            hpFill.style.width = Length.Percent(clamped * 100f);
        }
    }

    public void SelectWeapon(int index)
    {
        if (weaponSlot1 == null || weaponSlot2 == null) return;

        // Chặn chuyển đổi vũ khí nếu nhân vật đang chạy hoạt ảnh rút/cất kiếm
        if (LocalPlayerTarget != null && LocalPlayerTarget.IsSwitchingWeapon)
        {
            Debug.Log("[PlayerHUDController] Chặn chuyển vũ khí vì đang chạy hoạt ảnh đổi vũ khí.");
            return;
        }

        int oldWeapon = currentSelectedWeapon;

        if (index == 1)
        {
            weaponSlot1.RemoveFromClassList("weapon-inactive");
            weaponSlot1.AddToClassList("weapon-active");

            weaponSlot2.RemoveFromClassList("weapon-active");
            weaponSlot2.AddToClassList("weapon-inactive");
            currentSelectedWeapon = 1;
            NotifyHUDChange();
        }
        else if (index == 2)
        {
            // Kiểm tra nếu vũ khí 2 đang bị khóa
            if (isWeapon2Locked)
            {
                ShowWeaponWarning();
                return; // Không cho phép chọn
            }

            weaponSlot2.RemoveFromClassList("weapon-inactive");
            weaponSlot2.AddToClassList("weapon-active");

            weaponSlot1.RemoveFromClassList("weapon-active");
            weaponSlot1.AddToClassList("weapon-inactive");
            currentSelectedWeapon = 2;
            NotifyHUDChange();
        }

        // Standalone Mode: gọi trực tiếp phương thức chuyển đổi hoạt ảnh
        if (currentSelectedWeapon != oldWeapon && LocalPlayerTarget != null && LocalPlayerTarget.IsStandaloneMode)
        {
            LocalPlayerTarget.PlayWeaponSwitchAnimation(oldWeapon, currentSelectedWeapon);
        }
    }

    private void ShowSkillWarning(VisualElement icon)
    {
        if (weaponWarning == null) return;

        Debug.Log("Kỹ năng đang bị khóa");
        weaponWarning.text = LocalizationManager.Get("hud_warning_skill_locked");
        weaponWarning.AddToClassList("show-warning");
        warningTimer = WARNING_DURATION;

        // Hiệu ứng rung ổ khóa kỹ năng
        if (icon != null)
        {
            icon.RemoveFromClassList("shake-left");
            icon.RemoveFromClassList("shake-right");

            icon.schedule.Execute(() => icon.AddToClassList("shake-left")).StartingIn(0);
            icon.schedule.Execute(() =>
            {
                icon.RemoveFromClassList("shake-left");
                icon.AddToClassList("shake-right");
            }).StartingIn(60);
            icon.schedule.Execute(() =>
            {
                icon.RemoveFromClassList("shake-right");
                icon.AddToClassList("shake-left");
            }).StartingIn(120);
            icon.schedule.Execute(() =>
            {
                icon.RemoveFromClassList("shake-left");
                icon.AddToClassList("shake-right");
            }).StartingIn(180);
            icon.schedule.Execute(() =>
            {
                icon.RemoveFromClassList("shake-right");
            }).StartingIn(240);
        }
    }

    private void ShowWeaponWarning()
    {
        if (weaponWarning == null) return;

        Debug.Log("Vũ khí đang bị khóa");
        weaponWarning.text = LocalizationManager.Get("hud_warning_weapon_locked");
        weaponWarning.AddToClassList("show-warning");
        warningTimer = WARNING_DURATION;

        // Hiệu ứng rung ổ khóa qua lại
        if (lockIcon2 != null)
        {
            // Xóa các class cũ nếu có
            lockIcon2.RemoveFromClassList("shake-left");
            lockIcon2.RemoveFromClassList("shake-right");

            // Chuỗi rung nhanh qua lại
            lockIcon2.schedule.Execute(() => lockIcon2.AddToClassList("shake-left")).StartingIn(0);
            lockIcon2.schedule.Execute(() =>
            {
                lockIcon2.RemoveFromClassList("shake-left");
                lockIcon2.AddToClassList("shake-right");
            }).StartingIn(60);
            lockIcon2.schedule.Execute(() =>
            {
                lockIcon2.RemoveFromClassList("shake-right");
                lockIcon2.AddToClassList("shake-left");
            }).StartingIn(120);
            lockIcon2.schedule.Execute(() =>
            {
                lockIcon2.RemoveFromClassList("shake-left");
                lockIcon2.AddToClassList("shake-right");
            }).StartingIn(180);
            lockIcon2.schedule.Execute(() =>
            {
                lockIcon2.RemoveFromClassList("shake-right");
            }).StartingIn(240);
        }
    }
    private void NotifyHUDChange()
    {
        if (LocalPlayerTarget != null && (LocalPlayerTarget.IsStandaloneMode || (LocalPlayerTarget.IsSpawned && LocalPlayerTarget.IsOwner)))
        {
            LocalPlayerTarget.UpdateStateFromHUD(currentSelectedWeapon, isWeapon2Locked, isSkillsUnlocked);
        }
    }

    public void SetSkillsUnlocked(bool unlocked, bool playAnim = false)
    {
        isSkillsUnlocked = unlocked;
        if (unlocked)
        {
            if (lockQ != null) { if (playAnim) lockQ.AddToClassList("unlocked-anim"); else lockQ.style.display = DisplayStyle.None; }
            if (lockR != null) { if (playAnim) lockR.AddToClassList("unlocked-anim"); else lockR.style.display = DisplayStyle.None; }
            if (lockE != null) { if (playAnim) lockE.AddToClassList("unlocked-anim"); else lockE.style.display = DisplayStyle.None; }

            if (skillImgQ != null) skillImgQ.style.visibility = Visibility.Visible;
            if (skillImgR != null) skillImgR.style.visibility = Visibility.Visible;
            if (skillImgE != null) skillImgE.style.visibility = Visibility.Visible;
        }
        else
        {
            if (lockQ != null) { lockQ.RemoveFromClassList("unlocked-anim"); lockQ.style.display = DisplayStyle.Flex; }
            if (lockR != null) { lockR.RemoveFromClassList("unlocked-anim"); lockR.style.display = DisplayStyle.Flex; }
            if (lockE != null) { lockE.RemoveFromClassList("unlocked-anim"); lockE.style.display = DisplayStyle.Flex; }

            if (skillImgQ != null) skillImgQ.style.visibility = Visibility.Hidden;
            if (skillImgR != null) skillImgR.style.visibility = Visibility.Hidden;
            if (skillImgE != null) skillImgE.style.visibility = Visibility.Hidden;
        }
    }

    public void SetWeapon2Locked(bool locked, bool playAnim = false)
    {
        isWeapon2Locked = locked;
        if (!locked)
        {
            if (weaponLock2 != null) { if (playAnim) weaponLock2.AddToClassList("unlocked-anim"); else weaponLock2.style.display = DisplayStyle.None; }
            if (weaponImg2 != null) weaponImg2.style.visibility = Visibility.Visible;
        }
        else
        {
            if (weaponLock2 != null) { weaponLock2.RemoveFromClassList("unlocked-anim"); weaponLock2.style.display = DisplayStyle.Flex; }
            if (weaponImg2 != null) weaponImg2.style.visibility = Visibility.Hidden;
        }
    }

    public void SetWeaponDurability(int slotIndex, float percent)
    {
        float widthPercent = Mathf.Clamp01(percent) * 100f;
        if (slotIndex == 1 && weaponDurabilityFill1 != null)
        {
            weaponDurabilityFill1.style.width = Length.Percent(widthPercent);
        }
        else if (slotIndex == 2 && weaponDurabilityFill2 != null)
        {
            weaponDurabilityFill2.style.width = Length.Percent(widthPercent);
        }
    }

    public void ShowInteractionPrompt(bool show, string text)
    {
        InitializeUI(); // Đảm bảo khởi tạo khi được gọi

        if (interactionPrompt == null)
        {
            Debug.LogWarning($"[PlayerHUDController] ShowInteractionPrompt({show}, '{text}') được gọi nhưng interactionPrompt là NULL! UIDocument có thể chưa tải xong.");
            return;
        }

        if (show)
        {
            interactionPrompt.AddToClassList("show-prompt");
            Debug.Log($"[PlayerHUDController] Hiển thị gợi ý: {text}");
        }
        else
        {
            interactionPrompt.RemoveFromClassList("show-prompt");
            Debug.Log("[PlayerHUDController] Ẩn gợi ý tương tác.");
        }

        if (interactionPromptText != null && !string.IsNullOrEmpty(text))
        {
            interactionPromptText.text = text;

            // Tự động thay đổi nhãn phím dựa trên nội dung text
            if (interactionPromptKeyText != null)
            {
                if (text.Contains("[E]") || text.Contains("phím E") || text.Contains("phím [E]"))
                {
                    interactionPromptKeyText.text = "E";
                }
                else
                {
                    interactionPromptKeyText.text = "F";
                }
            }
        }
    }

    public void SetInventorySlots(string[] slots)
    {
        currentInventoryData = slots;
        if (inventorySlotsUI == null || inventorySlotsUI.Count == 0) return;

        for (int i = 0; i < inventorySlotsUI.Count && i < slots.Length; i++)
        {
            VisualElement slot = inventorySlotsUI[i];

            // Xóa ảnh item cũ và nhãn stack cũ
            var oldItem = slot.Q<VisualElement>(className: "inventory-item-icon");
            if (oldItem != null) oldItem.RemoveFromHierarchy();

            var oldStack = slot.Q<Label>(className: "inventory-item-stack-count");
            if (oldStack != null) oldStack.RemoveFromHierarchy();

            string slotVal = slots[i];
            if (!string.IsNullOrEmpty(slotVal))
            {
                string itemName = slotVal;
                int count = 1;
                if (slotVal.Contains(":"))
                {
                    var parts = slotVal.Split(':');
                    itemName = parts[0];
                    int.TryParse(parts[1], out count);
                }

                VisualElement itemIcon = new VisualElement();
                itemIcon.AddToClassList("inventory-item-icon");
                itemIcon.style.width = Length.Percent(80);
                itemIcon.style.height = Length.Percent(80);

                if (itemName == "RepairHammer")
                {
                    if (repairHammerSprite != null)
                    {
                        itemIcon.style.backgroundImage = new StyleBackground(repairHammerSprite);
                    }
                }
                else if (itemName == "Ngoc1")
                {
                    if (ngoc1Sprite != null)
                    {
                        itemIcon.style.backgroundImage = new StyleBackground(ngoc1Sprite);
                    }
                }
                else if (itemName == "Ngoc2")
                {
                    if (ngoc2Sprite != null)
                    {
                        itemIcon.style.backgroundImage = new StyleBackground(ngoc2Sprite);
                    }
                }

                slot.Add(itemIcon);

                // Nếu số lượng cộng dồn lớn hơn 1, thêm nhãn x[Count] ở góc dưới bên phải
                if (count > 1)
                {
                    Label stackLabel = new Label("x" + count);
                    stackLabel.AddToClassList("inventory-item-stack-count");
                    slot.Add(stackLabel);
                }
            }
        }
    }

    // =========================================================================
    //  Hành trang: Xử lý Kéo & Thả (Drag and Drop) với Drag Ghost di chuyển theo chuột
    // =========================================================================
    private void OnSlotPointerDown(PointerDownEvent evt, int index)
    {
        // 1. Kiểm tra nếu là Click chuột phải (evt.button == 1) -> Sử dụng vật phẩm
        if (evt.button == 1)
        {
            UseItem(index);
            return;
        }

        // 2. Click chuột trái (evt.button == 0) -> Bắt đầu Kéo thả sắp xếp hành trang
        if (evt.button == 0 && currentInventoryData != null && index < currentInventoryData.Length)
        {
            string slotVal = currentInventoryData[index];
            if (!string.IsNullOrEmpty(slotVal))
            {
                string itemName = slotVal;
                if (slotVal.Contains(":"))
                {
                    itemName = slotVal.Split(':')[0];
                }

                draggedSlotIndex = index;
                VisualElement slot = inventorySlotsUI[index];
                slot.AddToClassList("slot-dragging-source");
                slot.CapturePointer(evt.pointerId);

                // Thiết lập ảnh nền cho dragGhost từ item đang kéo
                if (itemName == "RepairHammer" && dragGhost != null)
                {
                    if (repairHammerSprite != null)
                    {
                        dragGhost.style.backgroundImage = new StyleBackground(repairHammerSprite);
                    }
                }
                else if (itemName == "Ngoc1" && dragGhost != null)
                {
                    if (ngoc1Sprite != null)
                    {
                        dragGhost.style.backgroundImage = new StyleBackground(ngoc1Sprite);
                    }
                }
                else if (itemName == "Ngoc2" && dragGhost != null)
                {
                    if (ngoc2Sprite != null)
                    {
                        dragGhost.style.backgroundImage = new StyleBackground(ngoc2Sprite);
                    }
                }
                else if (dragGhost != null)
                {
                    dragGhost.style.backgroundImage = StyleKeyword.Null;
                }

                // Căn giữa dragGhost dưới con trỏ chuột và hiển thị nó
                if (dragGhost != null)
                {
                    dragGhost.style.left = evt.position.x - 27f;
                    dragGhost.style.top = evt.position.y - 27f;
                    dragGhost.style.display = DisplayStyle.Flex;
                }

                // Ẩn tạm thời tooltip để tránh vướng màn hình khi kéo
                if (tooltipElement != null)
                {
                    tooltipElement.style.opacity = 0f;
                }
            }
        }
    }

    private void OnSlotPointerUp(PointerUpEvent evt, int index)
    {
        if (draggedSlotIndex == index)
        {
            VisualElement slot = inventorySlotsUI[index];
            slot.ReleasePointer(evt.pointerId);
            slot.RemoveFromClassList("slot-dragging-source");

            // Ẩn dragGhost đi ngay lập tức khi thả
            if (dragGhost != null)
            {
                dragGhost.style.display = DisplayStyle.None;
            }

            // Tìm ô đích được thả chuột tại tọa độ thả
            VisualElement targetElement = uiDocument.rootVisualElement.panel.Pick(evt.position);
            if (targetElement != null)
            {
                VisualElement actualSlot = targetElement;
                while (actualSlot != null && !actualSlot.name.StartsWith("inventory-slot-"))
                {
                    actualSlot = actualSlot.parent;
                }

                if (actualSlot != null)
                {
                    int targetIndex = int.Parse(actualSlot.name.Replace("inventory-slot-", ""));
                    if (targetIndex != draggedSlotIndex)
                    {
                        // Thực hiện tráo đổi (Swap) vị trí vật phẩm
                        if (LocalPlayerTarget != null)
                        {
                            string temp = LocalPlayerTarget.InventorySlots[draggedSlotIndex];
                            LocalPlayerTarget.InventorySlots[draggedSlotIndex] = LocalPlayerTarget.InventorySlots[targetIndex];
                            LocalPlayerTarget.InventorySlots[targetIndex] = temp;

                            // Vẽ lại và đồng bộ cơ sở dữ liệu
                            SetInventorySlots(LocalPlayerTarget.InventorySlots);
                            if (!LocalPlayerTarget.IsStandaloneMode) LocalPlayerTarget.SavePlayerStateToDatabase();
                        }
                    }
                }
            }
            draggedSlotIndex = -1;
        }
    }

    private void OnSlotPointerMove(PointerMoveEvent evt, int index)
    {
        // 1. Di chuyển dragGhost theo chuột nếu đang thực hiện kéo thả
        if (draggedSlotIndex != -1 && dragGhost != null)
        {
            dragGhost.style.left = evt.position.x - 27f;
            dragGhost.style.top = evt.position.y - 27f;
        }
        // 2. Ngược lại, nếu đang hover bình thường thì di chuyển Tooltip
        else if (tooltipElement != null && tooltipElement.style.opacity.value > 0f)
        {
            tooltipElement.style.left = evt.position.x + 15f;
            tooltipElement.style.top = evt.position.y + 15f;
        }
    }

    // =========================================================================
    //  Hành trang: Xử lý Hover Hiển thị chú thích (Tooltip)
    // =========================================================================
    private void OnSlotPointerEnter(PointerEnterEvent evt, int index)
    {
        // Chỉ hiển thị tooltip khi KHÔNG đang thực hiện kéo thả vật phẩm
        if (draggedSlotIndex == -1 && currentInventoryData != null && index < currentInventoryData.Length)
        {
            string slotVal = currentInventoryData[index];
            string itemName = slotVal;
            if (slotVal.Contains(":"))
            {
                itemName = slotVal.Split(':')[0];
            }

            if (itemName == "RepairHammer" && tooltipElement != null)
            {
                tooltipTitle.text = "BÚA RÈN MA THUẬT";
                tooltipDesc.text = "Click chuột phải để rèn lại 100% độ bền vũ khí đang trang bị.";
                tooltipElement.style.opacity = 1f;
                tooltipElement.style.left = evt.position.x + 15f;
                tooltipElement.style.top = evt.position.y + 15f;
            }
            else if (itemName == "Ngoc1" && tooltipElement != null)
            {
                tooltipTitle.text = "NGỌC TÍM BÍ ẨN";
                tooltipDesc.text = "Viên ngọc lấp lánh đang cất giấu một bí mật gì đó... Hiện tại chưa thể sử dụng.";
                tooltipElement.style.opacity = 1f;
                tooltipElement.style.left = evt.position.x + 15f;
                tooltipElement.style.top = evt.position.y + 15f;
            }
            else if (itemName == "Ngoc2" && tooltipElement != null)
            {
                tooltipTitle.text = "NGỌC ĐỎ BÍ ẨN";
                tooltipDesc.text = "Viên ngọc rực lửa đang cất giấu một sức mạnh bí mật... Hiện tại chưa thể sử dụng.";
                tooltipElement.style.opacity = 1f;
                tooltipElement.style.left = evt.position.x + 15f;
                tooltipElement.style.top = evt.position.y + 15f;
            }
        }
    }

    private void OnSlotPointerLeave(PointerLeaveEvent evt, int index)
    {
        if (tooltipElement != null)
        {
            tooltipElement.style.opacity = 0f;
        }
    }

    // =========================================================================
    //  Hành trang: Click Chuột phải sử dụng và chạy Cooldown sửa vũ khí
    // =========================================================================
    private void UseItem(int index)
    {
        if (isCooldownActive || currentInventoryData == null || index >= currentInventoryData.Length) return;

        string slotVal = currentInventoryData[index];
        string itemName = slotVal;
        if (slotVal.Contains(":"))
        {
            itemName = slotVal.Split(':')[0];
        }

        if (itemName == "RepairHammer")
        {
            VisualElement slot = inventorySlotsUI[index];
            if (slot != null)
            {
                // Tạo sẵn Overlay Cooldown
                VisualElement cdOverlay = new VisualElement();
                cdOverlay.AddToClassList("slot-cooldown-overlay");
                Label cdText = new Label("1.5s");
                cdText.AddToClassList("slot-cooldown-text");
                cdOverlay.Add(cdText);
                slot.Add(cdOverlay);

                // Ẩn tạm thời tooltip để nhìn rõ cooldown
                if (tooltipElement != null) tooltipElement.style.opacity = 0f;

                StartCoroutine(AnimateCooldown(slot, cdOverlay, cdText, index));
            }
        }
    }

    private System.Collections.IEnumerator AnimateCooldown(VisualElement slot, VisualElement cdOverlay, Label cdText, int slotIndex)
    {
        isCooldownActive = true;
        float duration = 1.5f;
        float timer = duration;

        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            cdText.text = $"{timer:F1}s";
            yield return null;
        }

        cdOverlay.RemoveFromHierarchy();
        isCooldownActive = false;

        // Tiến hành sửa chữa độ bền 100%
        if (LocalPlayerTarget != null)
        {
            int activeWeapon = LocalPlayerTarget.IsStandaloneMode ? currentSelectedWeapon : LocalPlayerTarget.GetActiveWeaponIndex();
            LocalPlayerTarget.RepairWeaponFromHUD(activeWeapon);

            // Tiêu hao vật phẩm (giảm số lượng đi 1 hoặc xóa hoàn toàn nếu là cái cuối)
            string slotVal = LocalPlayerTarget.InventorySlots[slotIndex];
            string baseName = slotVal;
            int count = 1;
            if (slotVal.Contains(":"))
            {
                var parts = slotVal.Split(':');
                baseName = parts[0];
                int.TryParse(parts[1], out count);
            }

            if (count > 1)
            {
                LocalPlayerTarget.InventorySlots[slotIndex] = baseName + ":" + (count - 1);
            }
            else
            {
                LocalPlayerTarget.InventorySlots[slotIndex] = "";
            }

            SetInventorySlots(LocalPlayerTarget.InventorySlots);

            if (!LocalPlayerTarget.IsStandaloneMode) LocalPlayerTarget.SavePlayerStateToDatabase();
        }
    }

    public void UpdateUpgradeUI(int points, int hpLv, int mpLv, int cdLv, int dmgLv)
    {
        LocalizationManager.Initialize();

        if (upgradePointsText != null)
        {
            upgradePointsText.text = string.Format(LocalizationManager.Get("hud_points_format"), points);
        }

        // Chỉ hiện bonus khi đã nâng cấp (level > 0), còn không chỉ hiện "Lv. X"
        if (hpLevelText != null)
        {
            int hpBonus = hpLv * 20;
            hpLevelText.text = hpLv > 0 ? $"Lv. {hpLv}  (+{hpBonus} HP)" : $"Lv. {hpLv}";
        }
        if (mpLevelText != null)
        {
            int mpBonus = mpLv * 10;
            mpLevelText.text = mpLv > 0 ? $"Lv. {mpLv}  (+{mpBonus} Stamina)" : $"Lv. {mpLv}";
        }
        if (cooldownLevelText != null)
        {
            int cdBonus = cdLv * 2;
            cooldownLevelText.text = cdLv > 0 ? $"Lv. {cdLv}  (-{cdBonus}%)" : $"Lv. {cdLv}";
        }
        if (damageLevelText != null)
        {
            int dmgBonus = dmgLv * 5;
            damageLevelText.text = dmgLv > 0 ? $"Lv. {dmgLv}  (+{dmgBonus} DMG)" : $"Lv. {dmgLv}";
        }

        // Giảm thời gian hồi chiêu tương ứng (2% mỗi cấp độ)
        cooldownTimeQ = 10f * (1f - cdLv * 0.02f);
        cooldownTimeR = 15f * (1f - cdLv * 0.02f);
        cooldownTimeE = 12f * (1f - cdLv * 0.02f);
    }

    /// <summary>
    /// Cập nhật cấp độ hiện tại và thanh kinh nghiệm (EXP) của người chơi lên giao diện HUD/Hành trang
    /// </summary>
    public void UpdateExperienceUI(int level, float currentExp, float maxExp)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        var root = uiDocument.rootVisualElement;

        // 1. Cập nhật nhãn Level trên HUD chính
        var hudLevel = root.Q<Label>("hud-level-label");
        if (hudLevel == null) hudLevel = root.Q<Label>(null, "level-label");
        if (hudLevel != null)
        {
            hudLevel.text = $"Lv. {level}";
        }

        // 2. Cập nhật nhãn Level trong thẻ thông tin hành trang
        var levelBadge = root.Q<Label>("character-level-badge");
        if (levelBadge != null)
        {
            levelBadge.text = $"LV. {level}";
        }

        // 3. Cập nhật thanh đầy Kinh nghiệm (Exp Fill)
        if (expFill != null)
        {
            float percentage = maxExp > 0 ? (currentExp / maxExp) * 100f : 0f;
            expFill.style.width = Length.Percent(Mathf.Clamp(percentage, 0f, 100f));
        }
    }

    private void UpgradeStat(int statType)
    {
        if (LocalPlayerTarget != null)
        {
            if (LocalPlayerTarget.IsStandaloneMode)
            {
                LocalPlayerTarget.StandaloneUpgradeStat(statType);
            }
            else if (LocalPlayerTarget.IsSpawned && LocalPlayerTarget.IsOwner)
            {
                LocalPlayerTarget.UpgradeStatFromHUD(statType);
            }
        }
    }

    /// <summary>
    /// Đồng bộ động toàn bộ giao diện (Avatar, Vũ khí, Kỹ năng) theo Player Profile của người chơi
    /// </summary>
    public void SetupPlayerProfile(int profileIndex)
    {
        lastSelectedProfileIndex = profileIndex;
        if (hudProfiles == null || hudProfiles.Count == 0)
        {
            Debug.LogWarning($"[PlayerHUDController] Danh sách Profiles trống!");
            return;
        }

        int targetIndex = profileIndex;
        // Nếu danh sách chỉ có 1 profile (đã được cấu hình riêng cho HUD này), sử dụng luôn profile đó (index 0)
        if (hudProfiles.Count == 1)
        {
            targetIndex = 0;
        }
        else if (profileIndex < 0 || profileIndex >= hudProfiles.Count)
        {
            Debug.LogWarning($"[PlayerHUDController] Index profile {profileIndex} vượt quá giới hạn danh sách Profiles (size={hudProfiles.Count})!");
            return;
        }

        var profile = hudProfiles[targetIndex];

        // 1. Cập nhật Avatar (Hỗ trợ cả VisualElement lẫn Image component)
        if (rawPlayerImage != null && profile.avatarSprite != null)
        {
            if (rawPlayerImage is UnityEngine.UIElements.Image uiImage)
            {
                // Nếu là thẻ <ui:Image>, thay đổi trực tiếp thuộc tính .image của nó
                uiImage.image = profile.avatarSprite.texture;
            }
            else
            {
                // Nếu là thẻ <ui:VisualElement> thông thường
                rawPlayerImage.style.backgroundImage = new StyleBackground(profile.avatarSprite);
            }
        }

        // 2. Cập nhật ảnh Vũ khí
        if (weaponImg1 != null && profile.weapon1Sprite != null)
        {
            weaponImg1.style.backgroundImage = new StyleBackground(profile.weapon1Sprite);
        }
        if (weaponImg2 != null && profile.weapon2Sprite != null)
        {
            weaponImg2.style.backgroundImage = new StyleBackground(profile.weapon2Sprite);
        }

        // 3. Cập nhật ảnh Kỹ năng
        if (skillImgQ != null)
        {
            skillImgQ.RemoveFromClassList("skill-img-q-maya");
            skillImgQ.RemoveFromClassList("skill-img-q-elena");
            if (profile.skillQSprite != null)
            {
                skillImgQ.style.backgroundImage = new StyleBackground(profile.skillQSprite);
                if (profileIndex == 1) // Maya Support
                {
                    skillImgQ.AddToClassList("skill-img-q-maya");
                }
                else if (profileIndex == 2) // Elena Archer
                {
                    skillImgQ.AddToClassList("skill-img-q-elena");
                }
            }
        }
        if (skillImgR != null && profile.skillRSprite != null)
        {
            skillImgR.style.backgroundImage = new StyleBackground(profile.skillRSprite);
        }
        if (skillImgE != null && profile.skillESprite != null)
        {
            skillImgE.style.backgroundImage = new StyleBackground(profile.skillESprite);
        }

        Debug.Log($"[PlayerHUDController] Đã thiết lập thành công giao diện cho lớp nhân vật: {profile.className} (Index {profileIndex})");
    }

    public bool IsInventoryOpen()
    {
        return inventoryOverlay != null && inventoryOverlay.ClassListContains("show-inventory");
    }

    public bool IsMapOpen()
    {
        return worldMapOverlay != null && worldMapOverlay.ClassListContains("show-map");
    }

    private void Start()
    {
        SetupEventSystemForInputSystem();
    }

    /// <summary>
    /// Kiểm tra và tự động cấu hình/sửa chữa EventSystem để tương thích 100% với Input System mới.
    /// Giúp UI Toolkit nhận tương tác click chuột ngay lập tức mà không bị liệt.
    /// </summary>
    public void SetupEventSystemForInputSystem()
    {
        var eventSystem = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            eventSystem = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

            // Sử dụng StandaloneInputModule hoặc InputSystemUIInputModule tùy theo cấu hình hệ thống
#if ENABLE_INPUT_SYSTEM
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            Debug.Log("[PlayerHUDController] Created EventSystem.");
        }
        else
        {
            // CựC KỲ QUAN TRỌNG: Nếu đã có EventSystem trong scene nhưng đang dùng module cũ (StandaloneInputModule)
            // của hệ thống Input cũ, trong khi game đang chạy New Input System, ta cần nâng cấp nó lên InputSystemUIInputModule.
            // Nếu không, UI Toolkit (VisualElement) sẽ không thể nhận được sự kiện click chuột hay kéo thả từ người chơi!
#if ENABLE_INPUT_SYSTEM
            var legacyModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacyModule != null)
            {
                Debug.LogWarning("[PlayerHUDController] Phát hiện EventSystem sử dụng StandaloneInputModule cũ dưới chế độ New Input System. Đang tự động nâng cấp lên InputSystemUIInputModule để hỗ trợ tương tác UI.");
                Destroy(legacyModule);
                
                // Tránh add trùng lặp
                var newModule = eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (newModule == null)
                {
                    eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }
            }
#endif
        }
    }

    private void UpdateTeammatesHUD()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        if (teammatesContainer == null)
        {
            teammatesContainer = uiDocument.rootVisualElement.Q<VisualElement>("teammates-container");
            if (teammatesContainer == null)
            {
                teammatesContainer = new VisualElement();
                teammatesContainer.name = "teammates-container";
                teammatesContainer.AddToClassList("teammates-container");
                uiDocument.rootVisualElement.Add(teammatesContainer);
            }
        }

        var activePlayers = PlayerHUDManager.ActivePlayers;
        System.Collections.Generic.HashSet<ulong> currentKeys = new System.Collections.Generic.HashSet<ulong>();

        foreach (var player in activePlayers)
        {
            if (player == null || player.gameObject == null) continue;

            // Bỏ qua bản thân (LocalPlayerTarget)
            if (player == LocalPlayerTarget) continue;

            ulong key = player.IsSpawned ? player.OwnerClientId : (ulong)player.gameObject.GetInstanceID();
            currentKeys.Add(key);

            if (!teammateCards.TryGetValue(key, out var card))
            {
                card = CreateTeammateCard(player);
                teammatesContainer.Add(card);
                teammateCards[key] = card;
            }

            UpdateTeammateCardValues(card, player);
        }

        System.Collections.Generic.List<ulong> keysToRemove = new System.Collections.Generic.List<ulong>();
        foreach (var existingKey in teammateCards.Keys)
        {
            if (!currentKeys.Contains(existingKey))
            {
                keysToRemove.Add(existingKey);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (teammateCards.TryGetValue(key, out var card))
            {
                card.RemoveFromHierarchy();
                teammateCards.Remove(key);
            }
        }
    }

    private VisualElement CreateTeammateCard(IPlayerHUDTarget player)
    {
        var card = new VisualElement();
        card.AddToClassList("teammate-card");

        var avatarContainer = new VisualElement();
        avatarContainer.AddToClassList("teammate-avatar-container");
        var avatarImg = new VisualElement();
        avatarImg.name = "avatar-image";
        avatarImg.AddToClassList("teammate-avatar-image");
        avatarContainer.Add(avatarImg);
        card.Add(avatarContainer);

        var statsWrapper = new VisualElement();
        statsWrapper.AddToClassList("teammate-stats-wrapper");

        var nameLevelRow = new VisualElement();
        nameLevelRow.AddToClassList("teammate-name-level-row");

        var nameLabel = new Label();
        nameLabel.name = "name-label";
        nameLabel.AddToClassList("teammate-name-label");

        var levelLabel = new Label();
        levelLabel.name = "level-label";
        levelLabel.AddToClassList("teammate-level-label");

        nameLevelRow.Add(nameLabel);
        nameLevelRow.Add(levelLabel);
        statsWrapper.Add(nameLevelRow);

        var hpTrack = new VisualElement();
        hpTrack.AddToClassList("teammate-track-bg");
        hpTrack.AddToClassList("teammate-hp-track");
        var hpFill = new VisualElement();
        hpFill.name = "hp-fill";
        hpFill.AddToClassList("teammate-hp-fill");
        hpTrack.Add(hpFill);
        statsWrapper.Add(hpTrack);

        var mpTrack = new VisualElement();
        mpTrack.AddToClassList("teammate-track-bg");
        mpTrack.AddToClassList("teammate-mp-track");
        var mpFill = new VisualElement();
        mpFill.name = "mp-fill";
        mpFill.AddToClassList("teammate-mp-fill");
        mpTrack.Add(mpFill);
        statsWrapper.Add(mpTrack);

        var expTrack = new VisualElement();
        expTrack.AddToClassList("teammate-track-bg");
        expTrack.AddToClassList("teammate-exp-track");
        var expFill = new VisualElement();
        expFill.name = "exp-fill";
        expFill.AddToClassList("teammate-exp-fill");
        expTrack.Add(expFill);
        statsWrapper.Add(expTrack);

        card.Add(statsWrapper);
        return card;
    }

    private void UpdateTeammateCardValues(VisualElement card, IPlayerHUDTarget player)
    {
        var avatarImg = card.Q<VisualElement>("avatar-image");
        if (avatarImg != null)
        {
            int classIdx = player.CharacterClassIndex;

            // Fallback xác định classIdx dựa trên class type thực tế của Player để tránh lỗi trống avatar
            if (player is LeoPlayer || player is LeoAssassin)
            {
                classIdx = 0;
            }
            else if (player is ElenaPlayer || player is ElenaArcher)
            {
                classIdx = 2;
            }
            else if (player is MayaSupport)
            {
                classIdx = 1;
            }
            else if (player is ArthurTanker)
            {
                classIdx = 3;
            }

            Sprite avatarSprite = null;

            // 1. Thử lấy từ HUDManager (hỗ trợ trường hợp các HUD chỉ cấu hình 1 profile riêng)
            if (PlayerHUDManager.Instance != null)
            {
                avatarSprite = PlayerHUDManager.Instance.GetTeammateAvatar(classIdx);
            }

            // 2. Fallback lấy từ chính HUD hiện tại nếu HUD hiện tại có đầy đủ list profiles
            if (avatarSprite == null && hudProfiles != null && classIdx >= 0 && classIdx < hudProfiles.Count)
            {
                avatarSprite = hudProfiles[classIdx].avatarSprite;
            }

            if (avatarSprite != null)
            {
                avatarImg.style.backgroundImage = new StyleBackground(avatarSprite);
            }
        }

        var nameLabel = card.Q<Label>("name-label");
        if (nameLabel != null)
        {
            nameLabel.text = player.DisplayName;
        }

        var levelLabel = card.Q<Label>("level-label");
        if (levelLabel != null)
        {
            levelLabel.text = $"Lv. {player.PlayerLevel}";
        }

        var hpFill = card.Q<VisualElement>("hp-fill");
        if (hpFill != null)
        {
            float maxHp = player.MaxHealth;
            float currentHp = player.CurrentHealth;
            float hpPct = maxHp > 0 ? (currentHp / maxHp) * 100f : 0f;
            hpFill.style.width = Length.Percent(Mathf.Clamp(hpPct, 0f, 100f));
        }

        var mpFill = card.Q<VisualElement>("mp-fill");
        if (mpFill != null)
        {
            mpFill.style.width = Length.Percent(100f);
        }

        var expFill = card.Q<VisualElement>("exp-fill");
        if (expFill != null)
        {
            float expPct = player.MaxExp > 0 ? (player.PlayerExp / player.MaxExp) * 100f : 0f;
            expFill.style.width = Length.Percent(Mathf.Clamp(expPct, 0f, 100f));
        }
    }

    private void UpdateHotkeysHint()
    {
        // 1. Kiểm tra an toàn: Chỉ hiển thị trên màn hình của chính người chơi đó (Local Player Client-only)
        if (LocalPlayerTarget == null || LocalPlayerTarget.gameObject == null)
        {
            if (hotkeysHintPanel != null)
            {
                hotkeysHintPanel.style.display = DisplayStyle.None;
            }
            return;
        }

        // Hiển thị panel phím nóng
        if (hotkeysHintPanel != null && hotkeysHintPanel.style.display == DisplayStyle.None)
        {
            hotkeysHintPanel.style.display = DisplayStyle.Flex;
        }

        // 2. Kiểm tra xem người chơi có đang bấm phím di chuyển không
        bool isMoving = false;
        if (Keyboard.current != null)
        {
            isMoving = Keyboard.current.wKey.isPressed ||
                       Keyboard.current.aKey.isPressed ||
                       Keyboard.current.sKey.isPressed ||
                       Keyboard.current.dKey.isPressed ||
                       Keyboard.current.upArrowKey.isPressed ||
                       Keyboard.current.leftArrowKey.isPressed ||
                       Keyboard.current.downArrowKey.isPressed ||
                       Keyboard.current.rightArrowKey.isPressed;
        }

        // Lấy thêm vận tốc vật lý từ Rigidbody để nhận diện di chuyển chính xác hơn
        var rb = LocalPlayerTarget.gameObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            isMoving |= new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).sqrMagnitude > 0.05f;
        }

        // 3. Ẩn/Hiện nhóm phím theo trạng thái di chuyển
        if (isMoving)
        {
            if (idleHintsGroup != null) idleHintsGroup.style.display = DisplayStyle.None;
            if (actionHintsGroup != null) actionHintsGroup.style.display = DisplayStyle.Flex;
        }
        else
        {
            if (idleHintsGroup != null) idleHintsGroup.style.display = DisplayStyle.Flex;
            if (actionHintsGroup != null) actionHintsGroup.style.display = DisplayStyle.None;
        }

        // 4. Hiển thị phím Vũ khí 2 (nếu đã được mở khóa)
        if (hintWeapon2 != null)
        {
            hintWeapon2.style.display = isWeapon2Locked ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // 5. Hiển thị các phím kỹ năng Q, E, R (nếu đã được mở khóa)
        if (hintSkills != null)
        {
            hintSkills.style.display = isSkillsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

public class MayaHUDController : PlayerHUDController { }
public class ArthurHUDController : PlayerHUDController { }