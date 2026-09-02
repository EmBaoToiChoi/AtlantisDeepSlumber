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

    public static PlayerHUDController Instance { get; private set; }
    public static bool isCarryingAxe = false;

    private static IPlayerHUDTarget localPlayerTarget;
    public static IPlayerHUDTarget LocalPlayerTarget
    {
        get
        {
            if (localPlayerTarget != null && localPlayerTarget is UnityEngine.Object obj && obj == null)
            {
                localPlayerTarget = null;
            }
            if (localPlayerTarget == null)
            {
                var allMBs = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var mb in allMBs)
                {
                    if (mb is IPlayerHUDTarget target)
                    {
                        localPlayerTarget = target;
                        Debug.Log($"[PlayerHUDController] LocalPlayerTarget tự động tìm thấy: {mb.gameObject.name}");
                        break;
                    }
                }
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
                else if (localPlayerTarget is MayaPlayer || localPlayerTarget is MayaSupport)
                {
                    classIdx = 1;
                }
                else if (localPlayerTarget is ArthurTanker || localPlayerTarget is ArthurPlayer)
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
                        hud.RefreshAllBars();
                    }
                }

                // Đăng ký event hủy Skill Q được thực hiện trong OnEnable() thay vì ở đây
                // (vì đây là static setter, không thể dùng instance method)
            }
        }
    }

    /// <summary>Được gọi khi server xác nhận Skill Q bị hủy (không có enemy). Reset cooldown Q.</summary>
    private void HandleQSkillCancelled()
    {
        currentCooldownQ = 0f;
        Debug.Log("[PlayerHUDController] Q Skill bị hủy - Reset cooldown Q.");
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
    private VisualElement invisibilityIndicator;
    private Label invisibilityTimerLabel;
    private VisualElement speedBoostIndicator;
    private Label speedBoostTimerLabel;
    private VisualElement qSkillIndicator;
    private Label qSkillTimerLabel;

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
    private VisualElement skillSlotQ; // Ô kỹ năng Q
    private VisualElement skillSlotR; // Ô kỹ năng R
    private VisualElement skillSlotE; // Ô kỹ năng E
    private Label skillNameLabelQ; // Label hiển thị tên Skill Q (Ảo Ảnh Chém)
    private Label skillNameLabelR; // Label hiển thị tên Skill R (Chưởng Nguyên Tố)
    private Label skillNameLabelE; // Label hiển thị tên Skill E (Tăng Tốc Chạy)
    private bool isSkillsUnlocked = false; // Trạng thái đã mở khóa kỹ năng hay chưa
    public int currentSelectedWeapon = 1; // Thêm biến lưu vũ khí đang chọn
    public static bool isAnyUIOpen = false; // Trạng thái static báo hiệu bất kỳ UI nào đang mở

    // Mặc định false -> Vào game chưa ấn M sẽ là tắt Mic
    private bool isMicOn = false;
    private int _lastMicUIState = -1; // -1: uninitialized, 0: muted, 1: unmuted silent, 2: unmuted transmitting

    [Header("Minimap Settings")]
    public float minimapZoom = 30f;
    private VisualElement minimapContainer;
    private VisualElement minimapContent;
    private Camera minimapCamera;
    private RenderTexture minimapRenderTexture;

    private struct MinimapIconData
    {
        public VisualElement iconContainer;
        public VisualElement arrowContainer;
        public VisualElement avatarElement;
    }
    private System.Collections.Generic.Dictionary<ulong, MinimapIconData> minimapIcons = new System.Collections.Generic.Dictionary<ulong, MinimapIconData>();

    [Header("World Map Settings")]
    public float worldMapZoom = 55f;
    private VisualElement worldMapFrame;
    private Camera worldMapCamera;
    private RenderTexture worldMapRenderTexture;

    private struct WorldMapIconData
    {
        public VisualElement iconContainer;
        public VisualElement arrowContainer;
        public VisualElement avatarElement;
    }
    private System.Collections.Generic.Dictionary<ulong, WorldMapIconData> worldMapIcons = new System.Collections.Generic.Dictionary<ulong, WorldMapIconData>();

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
    private Label speedLevelText;
    private Button btnUpgradeHp;
    private Button btnUpgradeMp;
    private Button btnUpgradeCooldown;
    private Button btnUpgradeDamage;
    private Button btnUpgradeSpeed;

    private VisualElement weaponLock2; // Tham chiếu tới overlay khóa vũ khí
    private VisualElement lockIcon2;   // Tham chiếu tới icon ổ khóa để rung
    private VisualElement weaponImg2;  // Tham chiếu tới hình ảnh vũ khí để ẩn
    private bool isWeapon2Locked = false;
    public bool Weapon2Locked => false;
    private float warningTimer = 0f;
    private const float WARNING_DURATION = 2f;

    private VisualElement weaponDurabilitySlot1;
    private VisualElement weaponDurabilityFill1;
    private VisualElement weaponDurabilityFill2;
    private VisualElement interactionPrompt;
    private Label interactionPromptText;
    private VisualElement interactionKeyIcon;
    private Label interactionPromptKeyText;
    private VisualElement tooltipElement;
    private VisualElement missionAlertBox;
    private Label missionAlertText;

    // Fantasy Notification Popup References (Popup_06)
    private VisualElement notificationPopup;
    private Label notificationPopupText;
    private VisualElement notificationPopupIcon;
    private IVisualElementScheduledItem hideNotificationScheduledItem;

    // Quest UI References
    private VisualElement questPanel;
    private Label questProgressText;
    private VisualElement questProgressBar;
    private Label questDescriptionText;
    private VisualElement questIcon;
    private Label questTitleText;
    public Sprite defaultQuestIcon;
    private object currentQuestOwner = null;
    private VisualElement questSpotlightBackdrop;
    private VisualElement questPointerLeft;
    private VisualElement questPointerBottom;
    private VisualElement questGlowAura;
    private Coroutine questSpotlightCoroutine;
    private string lastActiveQuestTitle = "";

    // Coop Building UI system
    public static bool isCoopBuildingUIOpen = false;
    private VisualElement coopBuildContainer;
    private VisualElement coopBuildFrameLeft;
    private VisualElement coopBuildFrameRight;
    private VisualElement coopBuildProgressBarFill;
    private Label coopBuildProgressLabel;
    private Button coopBuildClickButton;
    private BridgeCollapseTrigger activeBridgeTrigger;

    // Coop Build Camera States
    private static bool hasTransitionedBuildCameraOnce = false;
    private static BridgeCollapseTrigger lastActiveBridgeTrigger = null;
    private float buildCameraTransitionTimer = 0f;
    private float buildCameraTransitionDuration = 1.2f;
    private Vector3 initialCamPosBeforeBuild;
    private Quaternion initialCamRotBeforeBuild;
    private bool isBuildCameraActive = false;

    [Header("Coop Build Camera Offset Settings")]
    [Tooltip("Khoảng cách kéo lùi camera ra phía sau đầu cầu (dọc theo tim cầu)")]
    public float buildCamBackwardOffset = 10f;
    [Tooltip("Chiều cao camera trên đầu cầu hướng lên trên (Y)")]
    public float buildCamUpwardOffset = 26f;
    [Tooltip("Góc xoay Pitch (X) của camera khi xây cầu")]
    public float buildCamPitch = 40f;

    [Header("Tree Fall Camera Settings")]
    [Tooltip("Khoảng cách từ camera đến cây khi cây ngã")]
    public float treeCamDistance = 14f;
    [Tooltip("Chiều cao camera khi cây ngã")]
    public float treeCamHeight = 7f;
    [Tooltip("Chiều cao điểm nhìn tập trung trên thân cây")]
    public float treeCamLookHeight = 3f;
    [Tooltip("Thời gian hiển thị camera quay cây ngã (giây)")]
    public float treeCamDuration = 3.5f;

    // Trạng thái camera quay cây ngã
    private bool isTreeCameraActive = false;
    private float treeCameraTransitionTimer = 0f;
    private Vector3 initialCamPosBeforeTree;
    private Quaternion initialCamRotBeforeTree;
    private Vector3 normalCamOffsetFromPlayer;
    private Transform activeFallingTree;

    // Cấp độ nâng cấp hiện tại (giới hạn tối đa 3)
    private int currentHpLv = 0;
    private int currentMpLv = 0;
    private int currentCdLv = 0;
    private int currentDmgLv = 0;
    private int currentSpdLv = 0;

    [Header("Quest Settings")]
    public Sprite woodLogSprite;
    
    private VisualElement hotkeysHintPanel;
    private VisualElement f1ToggleHint;
    private VisualElement tutorialDetailsContainer;
    private float tutorialTimer = 0f;
    private const float TUTORIAL_AUTO_HIDE_DURATION = 20f;
    private bool isTutorialManuallyToggled = false;
    private bool isTutorialVisible = true;
    private VisualElement idleHintsGroup;
    private VisualElement actionHintsGroup;
    private VisualElement hintWeapon2;
    private VisualElement hintSkills;
    private VisualElement hintAttack;
    private VisualElement hintAttackIcons;
    private Label hintAttackText;
    private VisualElement hintBlock;
    private Label hintBlockText;
    private VisualElement hintIdleAttack;
    private VisualElement hintIdleAttackIcons;
    private Label hintIdleAttackText;
    private VisualElement hintIdleBlock;
    private Label hintIdleBlockText;
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
    private VisualElement crosshairElement;

    // Fog of War variables
    private Texture2D fogOfWarTexture;
    private Color32[] fogOfWarColors;
    private Material fogOfWarMaterial;
    private GameObject fogOfWarPlane;
    private float fowWorldSize = 1000f; // 2.5km x 2.5km
    private int fowTextureSize = 256;
    private float fowRevealRadius = 15f; // Bán kính sáng xung quanh player
    private Vector2 fowWorldCenter = Vector2.zero; // Tâm map thế giới
    private bool isFowInitialized = false;

    [Header("Item Sprites Settings")]
    public Sprite repairHammerSprite;
    public Sprite ngoc1Sprite;
    public Sprite ngoc2Sprite;

    [Header("Coop Build Frame Sprites")]
    public Sprite coopBuildFrameLeftSprite;
    public Sprite coopBuildFrameRightSprite;

    void OnEnable()
    {
        Instance = this;
        InitializeUI();
        // Đăng ký lắng nghe event hủy Skill Q
        if (LocalPlayerTarget != null)
        {
            LocalPlayerTarget.OnQSkillCancelled -= HandleQSkillCancelled; // tránh duplicate
            LocalPlayerTarget.OnQSkillCancelled += HandleQSkillCancelled;
        }
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        // QUAN TRỌNG: Reset toàn bộ state khi HUD bị tắt (SetActive false).
        // Khi UI Toolkit rebuild lại visual tree sau lần SetActive(true) tiếp theo,
        // tất cả các tham chiếu element cũ sẽ là dead reference -> phải re-query lại.
        isUIInitialized = false;
        _lastMicUIState = -1;

        // Dọn dẹp tài nguyên Fog of War
        if (fogOfWarPlane != null)
        {
            Destroy(fogOfWarPlane);
            fogOfWarPlane = null;
        }
        if (fogOfWarTexture != null)
        {
            Destroy(fogOfWarTexture);
            fogOfWarTexture = null;
        }
        if (fogOfWarMaterial != null)
        {
            Destroy(fogOfWarMaterial);
            fogOfWarMaterial = null;
        }
        isFowInitialized = false;

        // Hủy đăng ký event để tránh memory leak
        if (LocalPlayerTarget != null)
        {
            LocalPlayerTarget.OnQSkillCancelled -= HandleQSkillCancelled;
        }

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
        skillSlotQ = null; skillSlotR = null; skillSlotE = null;
        worldMapOverlay = null; inventoryOverlay = null; weaponWarning = null;
        weaponDurabilityFill1 = null; weaponDurabilityFill2 = null;
        interactionPrompt = null; interactionPromptText = null; interactionKeyIcon = null; interactionPromptKeyText = null;
        missionAlertBox = null; missionAlertText = null;
        notificationPopup = null; notificationPopupText = null; notificationPopupIcon = null;
        if (hideNotificationScheduledItem != null) { hideNotificationScheduledItem.Pause(); hideNotificationScheduledItem = null; }
        questPanel = null; questProgressText = null; questProgressBar = null;
        questDescriptionText = null;
        questIcon = null;
        questTitleText = null;
        questSpotlightBackdrop = null;
        questPointerLeft = null;
        questPointerBottom = null;
        questGlowAura = null;
        lastActiveQuestTitle = "";
        if (coopBuildContainer != null)
        {
            coopBuildContainer.RemoveFromHierarchy();
        }
        coopBuildContainer = null;
        coopBuildFrameLeft = null;
        coopBuildFrameRight = null;
        coopBuildProgressBarFill = null;
        coopBuildProgressLabel = null;
        coopBuildClickButton = null;
        activeBridgeTrigger = null;
        isCoopBuildingUIOpen = false;
        hotkeysHintPanel = null; idleHintsGroup = null; actionHintsGroup = null;
        hintWeapon2 = null; hintSkills = null;
        upgradePointsText = null; hpLevelText = null; mpLevelText = null;
        cooldownLevelText = null; damageLevelText = null;
        btnUpgradeHp = null; btnUpgradeMp = null; btnUpgradeCooldown = null; btnUpgradeDamage = null;
        tooltipElement = null; tooltipTitle = null; tooltipDesc = null;
        dragGhost = null; teammatesContainer = null;
        invisibilityIndicator = null;
        invisibilityTimerLabel = null;
        crosshairElement = null;
        speedBoostIndicator = null;
        speedBoostTimerLabel = null;
        qSkillIndicator = null;
        qSkillTimerLabel = null;
        inventorySlotsUI = new System.Collections.Generic.List<VisualElement>();
        teammateCards = new System.Collections.Generic.Dictionary<ulong, VisualElement>();

        if (minimapCamera != null)
        {
            Destroy(minimapCamera.gameObject);
            minimapCamera = null;
        }
        if (minimapRenderTexture != null)
        {
            minimapRenderTexture.Release();
            Destroy(minimapRenderTexture);
            minimapRenderTexture = null;
        }
        minimapContainer = null;
        minimapContent = null;
        minimapIcons.Clear();

        if (worldMapCamera != null)
        {
            Destroy(worldMapCamera.gameObject);
            worldMapCamera = null;
        }
        if (worldMapRenderTexture != null)
        {
            worldMapRenderTexture.Release();
            Destroy(worldMapRenderTexture);
            worldMapRenderTexture = null;
        }
        worldMapFrame = null;
        worldMapIcons.Clear();

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
            hpCatchUp.style.backgroundColor = new Color(0.96f, 0.96f, 1.0f, 0.88f);
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

        weaponDurabilitySlot1 = root.Q<VisualElement>("weapon-durability-slot-1");
        if (weaponDurabilitySlot1 != null)
        {
            weaponDurabilitySlot1.style.display = isCarryingAxe ? DisplayStyle.Flex : DisplayStyle.None;
        }
        weaponDurabilityFill1 = root.Q<VisualElement>("weapon-durability-fill-1");
        weaponDurabilityFill2 = root.Q<VisualElement>("weapon-durability-fill-2");
        interactionPrompt = root.Q<VisualElement>("interaction-prompt");
        interactionPromptText = root.Q<Label>("interaction-prompt-text");
        interactionKeyIcon = root.Q<VisualElement>("interaction-key-icon");
        if (interactionKeyIcon == null && interactionPrompt != null)
        {
            interactionKeyIcon = interactionPrompt.Q<VisualElement>(className: "interaction-key-icon");
            if (interactionKeyIcon == null) interactionKeyIcon = interactionPrompt.Q<VisualElement>(className: "key-icon-f");
        }
        interactionPromptKeyText = root.Q<Label>(className: "key-badge-f-text");

        // Quest Panel references
        questPanel = root.Q<VisualElement>("quest-panel");
        questProgressText = root.Q<Label>("quest-progress-text");
        questProgressBar = root.Q<VisualElement>("quest-progress-bar");
        questDescriptionText = root.Q<Label>("quest-description");
        questIcon = root.Q<VisualElement>(className: "quest-icon");
        questTitleText = root.Q<Label>(className: "quest-title");
        questSpotlightBackdrop = root.Q<VisualElement>("quest-spotlight-backdrop");
        if (questSpotlightBackdrop != null)
        {
            questSpotlightBackdrop.style.display = DisplayStyle.None;
        }
        questPointerLeft = root.Q<VisualElement>("quest-pointer-left");
        questPointerBottom = root.Q<VisualElement>("quest-pointer-bottom");
        questGlowAura = root.Q<VisualElement>("quest-glow-aura");

        // Tìm các phần tử của bảng phím nóng
        hotkeysHintPanel = root.Q<VisualElement>("hotkeys-hint-panel");
        f1ToggleHint = root.Q<VisualElement>("f1-toggle-hint");
        tutorialDetailsContainer = root.Q<VisualElement>("tutorial-details-container");
        if (f1ToggleHint != null)
        {
            f1ToggleHint.RegisterCallback<PointerDownEvent>(evt =>
            {
                ToggleTutorialPanel();
            });
        }
        idleHintsGroup = root.Q<VisualElement>("idle-hints-group");
        actionHintsGroup = root.Q<VisualElement>("action-hints-group");
        hintWeapon2 = root.Q<VisualElement>("hint-weapon2");
        hintSkills = root.Q<VisualElement>("hint-skills");
        hintAttack = root.Q<VisualElement>("hint-attack");
        hintAttackIcons = root.Q<VisualElement>("hint-attack-icons");
        hintAttackText = root.Q<Label>("hint-attack-text");
        hintBlock = root.Q<VisualElement>("hint-block");
        hintBlockText = root.Q<Label>("hint-block-text");
        hintIdleAttack = root.Q<VisualElement>("hint-idle-attack");
        hintIdleAttackIcons = root.Q<VisualElement>("hint-idle-attack-icons");
        hintIdleAttackText = root.Q<Label>("hint-idle-attack-text");
        hintIdleBlock = root.Q<VisualElement>("hint-idle-block");
        hintIdleBlockText = root.Q<Label>("hint-idle-block-text");
        UpdateWeaponActionHints();

        // Tìm UI Mic Icon trực tiếp
        micIcon = root.Q<VisualElement>("mic-icon");
        if (micIcon != null)
        {
            micIcon.RegisterCallback<PointerDownEvent>(evt =>
            {
                ToggleMic();
            });
        }
        rawPlayerImage = root.Q<VisualElement>("raw-player-image");

        weaponSlot1 = root.Q<VisualElement>("weapon-slot-1");
        weaponSlot2 = root.Q<VisualElement>("weapon-slot-2");
        weaponImg1 = root.Q<VisualElement>("weapon-img-1");

        // Tích hợp động UI Kỹ Năng E & Q Indicator (Menu_Button_05 Frame)
        VisualElement weaponsWrapper = root.Q<VisualElement>(className: "hud-weapons-wrapper");

        // UI Skill E Indicator
        if (speedBoostIndicator == null && weaponsWrapper != null)
        {
            speedBoostIndicator = new VisualElement();
            speedBoostIndicator.name = "speedboost-indicator";
            speedBoostIndicator.AddToClassList("speedboost-indicator");

            Label label = new Label("TĂNG TỐC CHẠY");
            label.name = "speedboost-text";
            label.AddToClassList("skill-active-indicator-title");

            speedBoostTimerLabel = new Label("10.0s");
            speedBoostTimerLabel.name = "speedboost-timer";
            speedBoostTimerLabel.AddToClassList("skill-active-indicator-timer");

            speedBoostIndicator.Add(label);
            speedBoostIndicator.Add(speedBoostTimerLabel);

            weaponsWrapper.Insert(0, speedBoostIndicator);
        }

        // UI Skill Q Indicator
        if (qSkillIndicator == null && weaponsWrapper != null)
        {
            qSkillIndicator = new VisualElement();
            qSkillIndicator.name = "qskill-indicator";
            qSkillIndicator.AddToClassList("qskill-indicator");

            Label labelQ = new Label("ẢO ẢNH CHÉM");
            labelQ.name = "qskill-text";
            labelQ.AddToClassList("skill-active-indicator-title");

            qSkillTimerLabel = new Label("1.5s");
            qSkillTimerLabel.name = "qskill-timer";
            qSkillTimerLabel.AddToClassList("skill-active-indicator-timer");

            qSkillIndicator.Add(labelQ);
            qSkillIndicator.Add(qSkillTimerLabel);

            weaponsWrapper.Insert(0, qSkillIndicator);
        }

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

        if (skillImgQ != null) skillSlotQ = skillImgQ.parent;
        if (skillImgR != null) skillSlotR = skillImgR.parent;
        if (skillImgE != null) skillSlotE = skillImgE.parent;

        skillNameLabelQ = root.Q<Label>("skill-name-label-q");
        skillNameLabelR = root.Q<Label>("skill-name-label-r");
        skillNameLabelE = root.Q<Label>("skill-name-label-e");

        // Tự động tạo Label tên Skill nếu chưa có trong UXML
        if (skillNameLabelQ == null && cooldownQ != null && cooldownQ.parent != null)
        {
            skillNameLabelQ = new Label("Ảo Ảnh Chém");
            skillNameLabelQ.name = "skill-name-label-q";
            skillNameLabelQ.AddToClassList("skill-name-label");
            cooldownQ.parent.Add(skillNameLabelQ);
        }
        if (skillNameLabelR == null && cooldownR != null && cooldownR.parent != null)
        {
            skillNameLabelR = new Label("Chưởng Nguyên Tố");
            skillNameLabelR.name = "skill-name-label-r";
            skillNameLabelR.AddToClassList("skill-name-label");
            cooldownR.parent.Add(skillNameLabelR);
        }
        if (skillNameLabelE == null && cooldownE != null && cooldownE.parent != null)
        {
            skillNameLabelE = new Label("Tăng Tốc Chạy");
            skillNameLabelE.name = "skill-name-label-e";
            skillNameLabelE.AddToClassList("skill-name-label");
            cooldownE.parent.Add(skillNameLabelE);
        }

        // Ẩn kỹ năng ngay từ đầu nếu đang khóa
        if (!isSkillsUnlocked)
        {
            if (skillImgQ != null) skillImgQ.style.visibility = Visibility.Hidden;
            if (skillImgR != null) skillImgR.style.visibility = Visibility.Hidden;
            if (skillImgE != null) skillImgE.style.visibility = Visibility.Hidden;
        }

        // Tìm Minimap
        minimapContainer = root.Q<VisualElement>("minimap-container");
        minimapContent = root.Q<VisualElement>("minimap-content");

        // Tìm Label cảnh báo, Overlay bản đồ và Hành trang
        worldMapOverlay = root.Q<VisualElement>("world-map-overlay");
        if (worldMapOverlay != null)
        {
            worldMapOverlay.pickingMode = PickingMode.Ignore; // Mặc định ẩn, bỏ qua cản chuột
            worldMapFrame = worldMapOverlay.Q<VisualElement>(className: "world-map-frame");
        }
        inventoryOverlay = root.Q<VisualElement>("inventory-overlay");
        if (inventoryOverlay != null)
        {
            inventoryOverlay.pickingMode = PickingMode.Ignore; // Mặc định ẩn, bỏ qua cản chuột
            inventoryOverlay.style.display = DisplayStyle.None;
            inventoryOverlay.style.visibility = Visibility.Hidden;
            inventoryOverlay.style.opacity = 0f;
            inventoryOverlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == inventoryOverlay)
                {
                    ToggleInventory();
                }
            });

            // Đảm bảo inventory-frame và các container con nhận đúng sự kiện Pointer
            var invFrame = inventoryOverlay.Q<VisualElement>(className: "inventory-frame");
            if (invFrame != null) invFrame.pickingMode = PickingMode.Position;

            // Đảm bảo mọi container trung gian đều nhận pointer events
            var invContent = inventoryOverlay.Q<VisualElement>(className: "inventory-content");
            if (invContent != null) invContent.pickingMode = PickingMode.Position;

            var invLeftPanel = inventoryOverlay.Q<VisualElement>(className: "inventory-left-panel");
            if (invLeftPanel != null) invLeftPanel.pickingMode = PickingMode.Position;

            var invHeader = inventoryOverlay.Q<VisualElement>(className: "inventory-header");
            if (invHeader != null) invHeader.pickingMode = PickingMode.Position;

            var slotsContainer = inventoryOverlay.Q<VisualElement>(className: "inventory-slots-container");
            if (slotsContainer != null) slotsContainer.pickingMode = PickingMode.Position;

            var upgradesContainer = inventoryOverlay.Q<VisualElement>(className: "upgrades-container");
            if (upgradesContainer != null) upgradesContainer.pickingMode = PickingMode.Position;

            var upgradeRowContainer = inventoryOverlay.Q<VisualElement>(className: "upgrade-row-container");
            if (upgradeRowContainer != null) upgradeRowContainer.pickingMode = PickingMode.Position;

            // Đảm bảo từng upgrade-row nhận click cho nút upgrade bên trong
            var upgradeRows = inventoryOverlay.Query<VisualElement>(className: "upgrade-row").ToList();
            foreach (var row in upgradeRows)
            {
                row.pickingMode = PickingMode.Position;
            }

            // Đảm bảo preview panel cũng nhận pointer events
            var previewContainer = inventoryOverlay.Q<VisualElement>(className: "inventory-preview-container");
            if (previewContainer != null) previewContainer.pickingMode = PickingMode.Position;

            var previewCard = inventoryOverlay.Q<VisualElement>(className: "inventory-preview-card");
            if (previewCard != null) previewCard.pickingMode = PickingMode.Position;
        }
        weaponWarning = root.Q<Label>("weapon-warning");
        notificationPopup = root.Q<VisualElement>("notification-popup");
        notificationPopupText = root.Q<Label>("notification-popup-text");
        notificationPopupIcon = root.Q<VisualElement>("notification-popup-icon");
        missionAlertBox = notificationPopup;
        missionAlertText = notificationPopupText;
        weaponLock2 = root.Q<VisualElement>("weapon-lock-2");
        if (weaponLock2 != null)
        {
            weaponLock2.style.display = DisplayStyle.None;
        }
        weaponImg2 = root.Q<VisualElement>("weapon-img-2");
        if (weaponImg2 != null)
        {
            weaponImg2.style.visibility = Visibility.Visible;
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
        speedLevelText = root.Q<Label>("speed-level-text");

        btnUpgradeHp = root.Q<Button>("btn-upgrade-hp");
        btnUpgradeMp = root.Q<Button>("btn-upgrade-mp");
        btnUpgradeCooldown = root.Q<Button>("btn-upgrade-cooldown");
        btnUpgradeDamage = root.Q<Button>("btn-upgrade-damage");
        btnUpgradeSpeed = root.Q<Button>("btn-upgrade-speed");

        if (btnUpgradeHp != null) btnUpgradeHp.clicked += () => UpgradeStat(0);
        if (btnUpgradeMp != null) btnUpgradeMp.clicked += () => UpgradeStat(1);
        if (btnUpgradeCooldown != null) btnUpgradeCooldown.clicked += () => UpgradeStat(2);
        if (btnUpgradeDamage != null) btnUpgradeDamage.clicked += () => UpgradeStat(3);
        if (btnUpgradeSpeed != null) btnUpgradeSpeed.clicked += () => UpgradeStat(4);

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
            slot.pickingMode = PickingMode.Position; // Đảm bảo nhận click chuột

            // Vẽ nhãn số thứ tự mờ ở góc ô hành trang
            slot.Clear();
            Label indexLabel = new Label((index + 1).ToString());
            indexLabel.AddToClassList("inventory-slot-index");
            indexLabel.pickingMode = PickingMode.Ignore; // Cho phép click xuyên qua tới slot cha
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

        // Tạo sẵn hồng tâm ngắm bắn
        if (crosshairElement == null)
        {
            crosshairElement = new VisualElement();
            crosshairElement.name = "aim-crosshair";
            crosshairElement.pickingMode = PickingMode.Ignore; // Tránh chặn click chuột trên inventory
            crosshairElement.style.position = Position.Absolute;
            crosshairElement.style.left = Length.Percent(50f);
            crosshairElement.style.top = Length.Percent(50f);
            crosshairElement.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f), 0f);
            crosshairElement.style.width = 40f;
            crosshairElement.style.height = 40f;
            
            // Viền tròn hồng tâm
            crosshairElement.style.borderTopWidth = 2f;
            crosshairElement.style.borderBottomWidth = 2f;
            crosshairElement.style.borderLeftWidth = 2f;
            crosshairElement.style.borderRightWidth = 2f;
            crosshairElement.style.borderTopColor = Color.red;
            crosshairElement.style.borderBottomColor = Color.red;
            crosshairElement.style.borderLeftColor = Color.red;
            crosshairElement.style.borderRightColor = Color.red;
            crosshairElement.style.borderTopLeftRadius = 20f;
            crosshairElement.style.borderTopRightRadius = 20f;
            crosshairElement.style.borderBottomLeftRadius = 20f;
            crosshairElement.style.borderBottomRightRadius = 20f;
            
            // Chấm đỏ chính giữa
            VisualElement centerDot = new VisualElement();
            centerDot.style.position = Position.Absolute;
            centerDot.style.left = Length.Percent(50f);
            centerDot.style.top = Length.Percent(50f);
            centerDot.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f), 0f);
            centerDot.style.width = 6f;
            centerDot.style.height = 6f;
            centerDot.style.backgroundColor = Color.red;
            centerDot.style.borderTopLeftRadius = 3f;
            centerDot.style.borderTopRightRadius = 3f;
            centerDot.style.borderBottomLeftRadius = 3f;
            centerDot.style.borderBottomRightRadius = 3f;
            
            crosshairElement.Add(centerDot);
            crosshairElement.style.display = DisplayStyle.None;
            root.Add(crosshairElement);
        }

        isUIInitialized = true;

        // Đảm bảo UIDocument có PanelEventHandler + PanelRaycaster để nhận pointer events
        EnsurePanelEventComponents();

        // Đăng ký listener cấp root để chẩn đoán xem panel có nhận pointer events hay không
        root.RegisterCallback<PointerDownEvent>(evt =>
        {
            Debug.Log($"[HUD ROOT] PointerDownEvent nhận được! target={evt.target}, button={evt.button}, pos={evt.position}");
        }, TrickleDown.TrickleDown);

        Debug.Log("[PlayerHUDController] UI Toolkit đã được khởi tạo thành công!");

        if (lastSelectedProfileIndex != -1)
        {
            Debug.Log($"[PlayerHUDController] Áp dụng lại profile index {lastSelectedProfileIndex} sau khi khởi tạo UI xong.");
            SetupPlayerProfile(lastSelectedProfileIndex);
        }

        RefreshAllBars();

        // Đăng ký nhận UI hội thoại NPC đang diễn ra (giải quyết đua luồng mạng multiplayer)
        if (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive)
        {
            IntroDialogueController.Instance.RegisterNewHUD(this);
        }
    }

    /// <summary>
    /// Áp dụng dịch đa ngôn ngữ cho toàn bộ các nhãn tĩnh của HUD/Hành trang
    /// </summary>
    private void ApplyHUDLocalization()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        var root = uiDocument.rootVisualElement;

        var inventoryTitle = root.Q<Label>(className: "inventory-title");
        if (inventoryTitle != null) inventoryTitle.text = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Vietnamese ? "HÀNH TRANG" : "INVENTORY";

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

        var upgradeNameSpeed = root.Q<Label>("upgrade-name-speed");
        if (upgradeNameSpeed != null) upgradeNameSpeed.text = LocalizationManager.Get("hud_stat_speed");
    }

    void Update()
    {
        InitializeUI(); // Đảm bảo khởi tạo nếu OnEnable chạy trước khi rootVisualElement sẵn sàng
        HandleManualInventoryClick(); // Hệ thống fallback xử lý click chuột thủ công khi Inventory mở
        UpdateTeammatesHUD();
        UpdateMinimap();
        UpdateWorldMap();
        UpdateFogOfWar();

        if (isCoopBuildingUIOpen && activeBridgeTrigger != null)
        {
            UpdateCoopBuildCamera();
        }
        else if (isTreeCameraActive && activeFallingTree != null)
        {
            UpdateTreeFallCamera();
        }

        if (LocalPlayerTarget != null)
        {
            if (LocalPlayerTarget.MaxMana > 0f)
            {
                SetMana(LocalPlayerTarget.CurrentMana / LocalPlayerTarget.MaxMana);
            }

            if (invisibilityIndicator != null)
            {
                invisibilityIndicator.style.display = DisplayStyle.None;
            }

            if (LocalPlayerTarget.IsAttackSpeedBoosted)
            {
                if (speedBoostIndicator != null)
                {
                    speedBoostIndicator.style.display = DisplayStyle.Flex;
                }
                if (speedBoostTimerLabel != null)
                {
                    speedBoostTimerLabel.text = $"{Mathf.Max(0f, LocalPlayerTarget.AttackSpeedBoostTimeRemaining):F1}s";
                }
            }
            else
            {
                if (speedBoostIndicator != null)
                {
                    speedBoostIndicator.style.display = DisplayStyle.None;
                }
            }

            // Hiển thị/ẩn UI đếm ngược Skill Q
            if (LocalPlayerTarget.IsQSkillActive)
            {
                if (qSkillIndicator != null)
                {
                    qSkillIndicator.style.display = DisplayStyle.Flex;
                }
                if (qSkillTimerLabel != null)
                {
                    qSkillTimerLabel.text = $"{Mathf.Max(0f, LocalPlayerTarget.QSkillTimeRemaining):F1}s";
                }
            }
            else
            {
                if (qSkillIndicator != null)
                {
                    qSkillIndicator.style.display = DisplayStyle.None;
                }
            }
        }

        // Tự động đếm 20 giây để tắt bảng chi tiết và hiển thị nút F1
        if (!isTutorialManuallyToggled && isTutorialVisible)
        {
            tutorialTimer += Time.deltaTime;
            if (tutorialTimer >= TUTORIAL_AUTO_HIDE_DURATION)
            {
                isTutorialVisible = false;
                if (tutorialDetailsContainer != null)
                {
                    tutorialDetailsContainer.style.display = DisplayStyle.None;
                }
                if (f1ToggleHint != null)
                {
                    f1ToggleHint.style.display = DisplayStyle.Flex;
                }
            }
        }

        if (Keyboard.current != null)
        {
            // Phím F1 để Bật/Tắt hướng dẫn
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                ToggleTutorialPanel();
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

            // Mở khóa vũ khí 2 bằng phím K (Test debug)
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


            // Mở khóa vũ khí 2 và kỹ năng bằng phím L (Tạm thời phục vụ test)
            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                isSkillsUnlocked = true;
                isWeapon2Locked = false;
                Debug.Log("Đã tạm thời mở khóa Vũ khí 2 và Kỹ năng!");

                // Thêm class để kích hoạt hiệu ứng rớt ổ khóa trong USS
                if (lockQ != null) lockQ.AddToClassList("unlocked-anim");
                if (lockR != null) lockR.AddToClassList("unlocked-anim");
                if (lockE != null) lockE.AddToClassList("unlocked-anim");
                if (weaponLock2 != null) weaponLock2.AddToClassList("unlocked-anim");

                // Hiện lại hình ảnh kỹ năng & vũ khí khi mở khóa
                if (skillImgQ != null) skillImgQ.style.visibility = Visibility.Visible;
                if (skillImgR != null) skillImgR.style.visibility = Visibility.Visible;
                if (skillImgE != null) skillImgE.style.visibility = Visibility.Visible;
                if (weaponImg2 != null) weaponImg2.style.visibility = Visibility.Visible;

                NotifyHUDChange();
            }

            // Kích hoạt Skill Q (chỉ khi đã mở khóa hoặc đạt Level 15)
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                if (isSkillsUnlocked || (LocalPlayerTarget != null && LocalPlayerTarget.PlayerLevel >= 15))
                {
                    if (LocalPlayerTarget != null && !LocalPlayerTarget.HasEnoughMana(30f))
                    {
                        ShowMissionAlert("Không đủ năng lượng! (Cần 30 năng lượng)", 2.0f);
                        return;
                    }

                    if (currentCooldownQ <= 0f && LocalPlayerTarget != null && !LocalPlayerTarget.IsQSkillActive)
                    {
                        // Kiểm tra bắt buộc phải ở Ô vũ khí 2 (Phím 2) đối với nhân vật Leo
                        if (LocalPlayerTarget is LeoPlayer || LocalPlayerTarget.CharacterClassIndex == 0)
                        {
                            if (currentSelectedWeapon != 2)
                            {
                                ShowMissionAlert("Cần chuyển sang Ô vũ khí 2 (Phím 2) để sử dụng kỹ năng Q!", 2.5f);
                                return;
                            }
                        }

                        bool activated = LocalPlayerTarget.TriggerQSkill();
                        if (activated)
                        {
                            currentCooldownQ = cooldownTimeQ;
                            Debug.Log("Đã dùng kỹ năng Q");
                        }
                        else
                        {
                            Debug.Log("Q Skill không kích hoạt được (không có enemy).");
                        }
                    }
                }
                else
                {
                    ShowSkillWarning(lockIconQ);
                    if (LocalPlayerTarget != null && LocalPlayerTarget.PlayerLevel < 15)
                    {
                        ShowMissionAlert("Cần đạt Level 15 để mở khóa kỹ năng Q!", 2.5f);
                    }
                }
            }

            // Kích hoạt Skill R (chỉ khi đã mở khóa hoặc đạt Level 5)
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (isSkillsUnlocked || (LocalPlayerTarget != null && LocalPlayerTarget.PlayerLevel >= 5))
                {
                    if (LocalPlayerTarget != null && !LocalPlayerTarget.HasEnoughMana(30f))
                    {
                        ShowMissionAlert("Không đủ năng lượng! (Cần 30 năng lượng)", 2.0f);
                        return;
                    }

                    if (currentCooldownR <= 0f && LocalPlayerTarget != null && !LocalPlayerTarget.IsInvisible)
                    {
                        LocalPlayerTarget.TriggerInvisibilitySkill();
                        Debug.Log("Đã dùng kỹ năng R");
                    }
                }
                else
                {
                    ShowSkillWarning(lockIconR);
                    if (LocalPlayerTarget != null && LocalPlayerTarget.PlayerLevel < 5)
                    {
                        ShowMissionAlert("Cần đạt Level 5 để mở khóa kỹ năng R!", 2.5f);
                    }
                }
            }

            // Kích hoạt Skill E (chỉ khi đã mở khóa hoặc đạt Level 10)
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                bool isPromptingE = interactionPrompt != null &&
                                    interactionPrompt.ClassListContains("show-prompt") &&
                                    interactionPromptText != null &&
                                    interactionPromptText.text.Contains("[E]");

                bool isDialogueOpen = SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive;

                if (!isPromptingE && !isDialogueOpen)
                {
                    if (isSkillsUnlocked || (LocalPlayerTarget != null && LocalPlayerTarget.PlayerLevel >= 10))
                    {
                        if (LocalPlayerTarget != null && !LocalPlayerTarget.HasEnoughMana(30f))
                        {
                            ShowMissionAlert("Không đủ năng lượng! (Cần 30 năng lượng)", 2.0f);
                            return;
                        }

                        if (currentCooldownE <= 0f && LocalPlayerTarget != null && !LocalPlayerTarget.IsAttackSpeedBoosted)
                        {
                            LocalPlayerTarget.TriggerAttackSpeedBoostSkill();
                            if (LocalPlayerTarget.CharacterClassIndex != 2 && LocalPlayerTarget.CharacterClassIndex != 1 && LocalPlayerTarget.CharacterClassIndex != 0)
                            {
                                currentCooldownE = cooldownTimeE;
                            }
                            Debug.Log("Đã dùng kỹ năng E");
                        }
                    }
                    else
                    {
                        ShowSkillWarning(lockIconE);
                        if (LocalPlayerTarget != null && LocalPlayerTarget.PlayerLevel < 10)
                        {
                            ShowMissionAlert("Cần đạt Level 10 để mở khóa kỹ năng E!", 2.5f);
                        }
                    }
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ToggleTutorialPanel();
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

        // Cập nhật trạng thái tối đen của ô kỹ năng khi năng lượng < 30
        UpdateSkillEnergyVisuals();

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

        UpdateMicUI();
        UpdateHotkeysHint();

        // Cập nhật trạng thái hiển thị ổ khóa kỹ năng theo cấp độ người chơi
        if (LocalPlayerTarget != null)
        {
            int pLevel = LocalPlayerTarget.PlayerLevel;
            if (isSkillsUnlocked)
            {
                if (lockR != null && !lockR.ClassListContains("unlocked-anim")) lockR.AddToClassList("unlocked-anim");
                if (lockE != null && !lockE.ClassListContains("unlocked-anim")) lockE.AddToClassList("unlocked-anim");
                if (lockQ != null && !lockQ.ClassListContains("unlocked-anim")) lockQ.AddToClassList("unlocked-anim");
            }
            else
            {
                // Skill R (Lv 5)
                if (lockR != null)
                {
                    if (pLevel < 5)
                    {
                        lockR.RemoveFromClassList("unlocked-anim");
                        lockR.style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        if (!lockR.ClassListContains("unlocked-anim"))
                        {
                            lockR.AddToClassList("unlocked-anim");
                        }
                    }
                }

                // Skill E (Lv 10)
                if (lockE != null)
                {
                    if (pLevel < 10)
                    {
                        lockE.RemoveFromClassList("unlocked-anim");
                        lockE.style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        if (!lockE.ClassListContains("unlocked-anim"))
                        {
                            lockE.AddToClassList("unlocked-anim");
                        }
                    }
                }

                // Skill Q (Lv 15)
                if (lockQ != null)
                {
                    if (pLevel < 15)
                    {
                        lockQ.RemoveFromClassList("unlocked-anim");
                        lockQ.style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        if (!lockQ.ClassListContains("unlocked-anim"))
                        {
                            lockQ.AddToClassList("unlocked-anim");
                        }
                    }
                }
            }
        }

        // Update Coop Build UI state
        if (isCoopBuildingUIOpen)
        {
            if (activeBridgeTrigger != null)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    OnBuildButtonClicked();
                }

                bool isNetwork = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;
                bool isRepaired = isNetwork ? activeBridgeTrigger.hasBeenRepaired.Value : activeBridgeTrigger.IsBridgeRepaired();
                if (isRepaired)
                {
                    hasTransitionedBuildCameraOnce = false;
                    CloseCoopBuildUI();
                }
                else
                {
                    float currentProgress = isNetwork ? activeBridgeTrigger.buildProgress.Value : activeBridgeTrigger.localBuildProgress;
                    UpdateCoopBuildProgress(currentProgress);
                }
            }
            else
            {
                CloseCoopBuildUI();
            }
        }
    }

    public void ToggleMic()
    {
        if (MicManager.Instance != null)
        {
            if (MicManager.Instance.transmissionMode == 1) // Chỉ toggle trong chế độ Auto
            {
                MicManager.Instance.IsMuted = !MicManager.Instance.IsMuted;
                isMicOn = !MicManager.Instance.IsMuted;
            }
        }
        else
        {
            isMicOn = !isMicOn;
        }
        Debug.Log("Trạng thái Mic hiện tại: " + (isMicOn ? "Mở" : "Tắt"));
        UpdateMicUI();
    }

    public void ToggleInventory()
    {
        if (inventoryOverlay != null)
        {
            inventoryOverlay.ToggleInClassList("show-inventory");
            bool isNowVisible = inventoryOverlay.ClassListContains("show-inventory");
            inventoryOverlay.pickingMode = isNowVisible ? PickingMode.Position : PickingMode.Ignore;

            // Cưỡng ép visibility/opacity/display trong code để mở/đóng hoàn toàn
            if (isNowVisible)
            {
                inventoryOverlay.style.display = DisplayStyle.Flex;
                inventoryOverlay.style.visibility = Visibility.Visible;
                inventoryOverlay.style.opacity = 1f;
            }
            else
            {
                inventoryOverlay.style.display = DisplayStyle.None;
                inventoryOverlay.style.visibility = Visibility.Hidden;
                inventoryOverlay.style.opacity = 0f;
            }

            Debug.Log("Đã " + (isNowVisible ? "mở" : "đóng") + " hành trang" +
                      $" | pickingMode={inventoryOverlay.pickingMode}" +
                      $" | visibility={inventoryOverlay.resolvedStyle.visibility}" +
                      $" | panel={inventoryOverlay.panel != null}");

            // Đồng bộ trạng thái static UI
            isAnyUIOpen = isNowVisible;

            // Đảm bảo EventSystem và PanelEventHandler được cấu hình đúng khi mở UI
            if (isNowVisible)
            {
                SetupEventSystemForInputSystem();
                EnsurePanelEventComponents();

                // Cập nhật ngay toàn bộ vật phẩm đã lưu và giao diện nâng cấp của người chơi khi mở Tab
                if (LocalPlayerTarget != null)
                {
                    if (LocalPlayerTarget.InventorySlots != null)
                    {
                        SetInventorySlots(LocalPlayerTarget.InventorySlots);
                    }
                    LocalPlayerTarget.RefreshUpgradeHUD();
                }
            }

            // Tự động ẩn/hiện con trỏ chuột phù hợp với trạng thái UI hành trang
            if (isNowVisible)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                if (LocalPlayerTarget != null)
                {
                    LocalPlayerTarget.SetCursorLock(false);
                }
            }
            else
            {
                if (LocalPlayerTarget != null)
                {
                    LocalPlayerTarget.SetCursorLock(true);
                }
                else
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }

            // Ẩn tooltip và drag ghost khi đóng hành trang để tránh tình trạng hiển thị thừa
            if (!isNowVisible)
            {
                if (tooltipElement != null) tooltipElement.style.opacity = 0f;
                if (dragGhost != null) dragGhost.style.display = DisplayStyle.None;
                draggedSlotIndex = -1;
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

        bool isMuted = MicManager.Instance != null ? MicManager.Instance.IsMuted : !isMicOn;
        bool isTransmitting = MicManager.Instance != null && MicManager.Instance.IsLocalTransmitting;

        int targetState = isMuted ? 0 : (isTransmitting ? 2 : 1);
        if (targetState == _lastMicUIState) return;
        _lastMicUIState = targetState;

        if (targetState == 0)
        {
            micIcon.RemoveFromClassList("mic-on");
            micIcon.AddToClassList("mic-off");
            micIcon.style.opacity = 0.4f;
            micIcon.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
        }
        else if (targetState == 2)
        {
            micIcon.RemoveFromClassList("mic-off");
            micIcon.AddToClassList("mic-on");
            micIcon.style.opacity = 1f;
            micIcon.style.scale = new StyleScale(new Scale(new Vector3(1.25f, 1.25f, 1f)));
        }
        else
        {
            micIcon.RemoveFromClassList("mic-off");
            micIcon.AddToClassList("mic-on");
            micIcon.style.opacity = 0.8f;
            micIcon.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
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

    public void SetMana(float percentage)
    {
        float clamped = Mathf.Clamp01(percentage);
        if (mpFill != null)
        {
            mpFill.style.width = Length.Percent(clamped * 100f);
        }
        UpdateSkillEnergyVisuals();
    }

    public void RefreshAllBars()
    {
        if (LocalPlayerTarget != null)
        {
            if (LocalPlayerTarget.MaxHealth > 0)
            {
                SetHealth(LocalPlayerTarget.CurrentHealth / LocalPlayerTarget.MaxHealth);
            }
            if (LocalPlayerTarget.MaxExp > 0)
            {
                UpdateExperienceUI(LocalPlayerTarget.PlayerLevel, LocalPlayerTarget.PlayerExp, LocalPlayerTarget.MaxExp);
            }
            if (LocalPlayerTarget.MaxMana > 0)
            {
                SetMana(LocalPlayerTarget.CurrentMana / LocalPlayerTarget.MaxMana);
            }
            else
            {
                SetMana(1f);
            }
        }
        UpdateSkillEnergyVisuals();
    }

    public void UpdateSkillEnergyVisuals()
    {
        bool hasEnoughEnergy = true;
        if (LocalPlayerTarget != null)
        {
            hasEnoughEnergy = LocalPlayerTarget.CurrentMana >= 30f;
        }

        SetSkillsEnergyState(hasEnoughEnergy);
    }

    public void SetSkillsEnergyState(bool hasEnoughEnergy)
    {
        if (skillSlotQ != null)
        {
            if (hasEnoughEnergy) skillSlotQ.RemoveFromClassList("skill-slot-no-mana");
            else skillSlotQ.AddToClassList("skill-slot-no-mana");
        }
        if (skillSlotR != null)
        {
            if (hasEnoughEnergy) skillSlotR.RemoveFromClassList("skill-slot-no-mana");
            else skillSlotR.AddToClassList("skill-slot-no-mana");
        }
        if (skillSlotE != null)
        {
            if (hasEnoughEnergy) skillSlotE.RemoveFromClassList("skill-slot-no-mana");
            else skillSlotE.AddToClassList("skill-slot-no-mana");
        }

        if (skillImgQ != null)
        {
            if (hasEnoughEnergy) skillImgQ.RemoveFromClassList("skill-no-mana");
            else skillImgQ.AddToClassList("skill-no-mana");
        }
        if (skillImgR != null)
        {
            if (hasEnoughEnergy) skillImgR.RemoveFromClassList("skill-no-mana");
            else skillImgR.AddToClassList("skill-no-mana");
        }
        if (skillImgE != null)
        {
            if (hasEnoughEnergy) skillImgE.RemoveFromClassList("skill-no-mana");
            else skillImgE.AddToClassList("skill-no-mana");
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
            UpdateWeaponActionHints();
            NotifyHUDChange();
        }
        else if (index == 2)
        {
            // Kiểm tra nếu vũ khí 2 đang bị khóa hoặc đang cầm rìu
            if (isWeapon2Locked || isCarryingAxe)
            {
                ShowWeaponWarning();
                return; // Không cho phép chọn
            }

            weaponSlot2.RemoveFromClassList("weapon-inactive");
            weaponSlot2.AddToClassList("weapon-active");

            weaponSlot1.RemoveFromClassList("weapon-active");
            weaponSlot1.AddToClassList("weapon-inactive");
            currentSelectedWeapon = 2;
            UpdateWeaponActionHints();
            NotifyHUDChange();
        }

        // NotifyHUDChange() đã tự động gọi UpdateStateFromHUD -> PlayWeaponSwitchAnimation cho cả Standalone lẫn Netcode.
    }

    private void ShowSkillWarning(VisualElement icon)
    {
        ShowMissionAlert(LocalizationManager.Get("hud_warning_skill_locked"), 2.5f);

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
        ShowMissionAlert(LocalizationManager.Get("hud_warning_weapon_locked"), 2.5f);

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

    public void ShowWeaponWarningCustom(string message)
    {
        ShowMissionAlert(message, 2.5f);
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
        Color fillColor = Color.white;
        if (percent < 0.2f)
        {
            fillColor = new Color(0.9f, 0.1f, 0.1f, 1f); // Màu đỏ
        }
        else if (percent < 0.6f)
        {
            fillColor = new Color(0.9f, 0.8f, 0.1f, 1f); // Màu vàng
        }

        if (slotIndex == 1 && weaponDurabilityFill1 != null)
        {
            if (weaponDurabilitySlot1 != null)
            {
                weaponDurabilitySlot1.style.display = isCarryingAxe ? DisplayStyle.Flex : DisplayStyle.None;
            }
            weaponDurabilityFill1.style.width = Length.Percent(widthPercent);
            weaponDurabilityFill1.style.backgroundColor = fillColor;
        }
        else if (slotIndex == 2 && weaponDurabilityFill2 != null)
        {
            weaponDurabilityFill2.style.width = Length.Percent(widthPercent);
            weaponDurabilityFill2.style.backgroundColor = fillColor;
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

            // Tự động thay đổi icon phím dựa trên nội dung text (F, G, C, E, Space, v.v.)
            if (interactionKeyIcon != null)
            {
                interactionKeyIcon.RemoveFromClassList("key-icon-f");
                interactionKeyIcon.RemoveFromClassList("key-icon-g");
                interactionKeyIcon.RemoveFromClassList("key-icon-c");
                interactionKeyIcon.RemoveFromClassList("key-icon-e");
                interactionKeyIcon.RemoveFromClassList("key-icon-space");

                if (text.Contains("[C]") || text.Contains("phím C") || text.Contains("phím [C]"))
                {
                    interactionKeyIcon.AddToClassList("key-icon-c");
                }
                else if (text.Contains("[G]") || text.Contains("phím G") || text.Contains("phím [G]"))
                {
                    interactionKeyIcon.AddToClassList("key-icon-g");
                }
                else if (text.Contains("[E]") || text.Contains("phím E") || text.Contains("phím [E]"))
                {
                    interactionKeyIcon.AddToClassList("key-icon-e");
                }
                else if (text.Contains("[SPACE]") || text.Contains("phím Space") || text.Contains("phím [Space]"))
                {
                    interactionKeyIcon.AddToClassList("key-icon-space");
                }
                else
                {
                    interactionKeyIcon.AddToClassList("key-icon-f");
                }
            }

            if (interactionPromptKeyText != null)
            {
                if (text.Contains("[C]") || text.Contains("phím C") || text.Contains("phím [C]"))
                {
                    interactionPromptKeyText.text = "C";
                }
                else if (text.Contains("[E]") || text.Contains("phím E") || text.Contains("phím [E]"))
                {
                    interactionPromptKeyText.text = "E";
                }
                else if (text.Contains("[G]") || text.Contains("phím G") || text.Contains("phím [G]"))
                {
                    interactionPromptKeyText.text = "G";
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
                itemIcon.pickingMode = PickingMode.Ignore; // Cho phép click xuyên qua tới slot cha
                itemIcon.style.width = Length.Percent(100);
                itemIcon.style.height = Length.Percent(100);

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
                else if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                         itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase) ||
                         itemName.Equals("wood_stack", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (woodLogSprite != null)
                    {
                        itemIcon.style.backgroundImage = new StyleBackground(woodLogSprite);
                    }
                }

                slot.Add(itemIcon);

                // Nếu số lượng cộng dồn lớn hơn 1, thêm nhãn x[Count] ở góc dưới bên phải
                if (count > 1)
                {
                    Label stackLabel = new Label("x" + count);
                    stackLabel.AddToClassList("inventory-item-stack-count");
                    stackLabel.pickingMode = PickingMode.Ignore; // Cho phép click xuyên qua tới slot cha
                    slot.Add(stackLabel);
                }
            }
        }

        // Count Wood Logs in inventory to update the Quest progress UI automatically
        int woodCount = 0;
        if (slots != null)
        {
            foreach (var slotVal in slots)
            {
                if (string.IsNullOrEmpty(slotVal)) continue;
                string itemN = slotVal;
                int count = 1;
                if (slotVal.Contains(":"))
                {
                    var parts = slotVal.Split(':');
                    itemN = parts[0];
                    int.TryParse(parts[1], out count);
                }
                if (itemN.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                    itemN.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase) ||
                    itemN.Equals("wood_stack", System.StringComparison.OrdinalIgnoreCase))
                {
                    woodCount += count;
                }
            }
        }
        UpdateQuestProgress(woodCount, 16);
    }

    // =========================================================================
    //  Hành trang: Xử lý Kéo & Thả (Drag and Drop) với Drag Ghost di chuyển theo chuột
    // =========================================================================
    private void OnSlotPointerDown(PointerDownEvent evt, int index)
    {
        Debug.Log($"[Inventory] OnSlotPointerDown: slot={index}, button={evt.button}, target={evt.target}");
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
            else
            {
                // Thả ngoài hành trang -> Vứt vật phẩm ra đất!
                DropItemFromInventory(draggedSlotIndex);
            }
            draggedSlotIndex = -1;
        }
    }

    private void DropItemFromInventory(int slotIndex)
    {
        if (LocalPlayerTarget == null || slotIndex < 0 || slotIndex >= LocalPlayerTarget.InventorySlots.Length) return;

        string slotVal = LocalPlayerTarget.InventorySlots[slotIndex];
        if (string.IsNullOrEmpty(slotVal)) return;

        string itemName = slotVal.Split(':')[0];
        int count = 1;
        if (slotVal.Contains(":"))
        {
            int.TryParse(slotVal.Split(':')[1], out count);
        }

        // 1. Giảm hoặc xóa vật phẩm khỏi túi đồ
        if (count > 1)
        {
            LocalPlayerTarget.InventorySlots[slotIndex] = itemName + ":" + (count - 1);
        }
        else
        {
            LocalPlayerTarget.InventorySlots[slotIndex] = "";
        }

        SetInventorySlots(LocalPlayerTarget.InventorySlots);
        if (!LocalPlayerTarget.IsStandaloneMode) LocalPlayerTarget.SavePlayerStateToDatabase();

        // 2. Gọi logic rớt vật phẩm từ PlayerInteraction
        var playerInt = LocalPlayerTarget.gameObject.GetComponent<PlayerInteraction>();
        if (playerInt != null)
        {
            playerInt.RequestDropItem(itemName);
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
            else if ((itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                      itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase) ||
                      itemName.Equals("wood_stack", System.StringComparison.OrdinalIgnoreCase)) && 
                     tooltipElement != null)
            {
                tooltipTitle.text = "THANH GỖ";
                tooltipDesc.text = "Thanh gỗ chắc chắn dùng để sửa cầu.";
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
    private void ConsumeItemInSlot(int slotIndex)
    {
        if (LocalPlayerTarget == null || LocalPlayerTarget.InventorySlots == null) return;
        if (slotIndex < 0 || slotIndex >= LocalPlayerTarget.InventorySlots.Length) return;

        string slotVal = LocalPlayerTarget.InventorySlots[slotIndex];
        if (string.IsNullOrEmpty(slotVal)) return;

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

    private void UseItem(int index)
    {
        if (isCooldownActive || currentInventoryData == null || index >= currentInventoryData.Length) return;

        string slotVal = currentInventoryData[index];
        if (string.IsNullOrEmpty(slotVal)) return;

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
        else if (itemName == "Ngoc1" || itemName == "Ngoc2" || itemName.Contains("Potion") || itemName.Contains("HP") || itemName.Contains("Heal"))
        {
            if (LocalPlayerTarget != null)
            {
                LocalPlayerTarget.Heal(30f);
                ConsumeItemInSlot(index);
                Debug.Log($"[PlayerHUDController] Đã sử dụng {itemName} để hồi 30 HP!");
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

    public void UpdateUpgradeUI(int points, int hpLv, int mpLv, int cdLv, int dmgLv, int spdLv = 0)
    {
        LocalizationManager.Initialize();

        currentHpLv = hpLv;
        currentMpLv = mpLv;
        currentCdLv = cdLv;
        currentDmgLv = dmgLv;
        currentSpdLv = spdLv;

        if (upgradePointsText != null)
        {
            upgradePointsText.text = string.Format(LocalizationManager.Get("hud_points_format"), points);
        }

        // Chỉ hiện bonus khi đã nâng cấp (level > 0), còn không chỉ hiện "Lv. X"
        if (hpLevelText != null)
        {
            int hpBonus = hpLv * 10;
            string maxLabel = hpLv >= 3 ? " (MAX)" : "";
            hpLevelText.text = hpLv > 0 ? $"Lv. {hpLv}  (+{hpBonus} Máu){maxLabel}" : $"Lv. {hpLv}";
        }
        if (mpLevelText != null)
        {
            int mpBonus = mpLv * 10;
            string maxLabel = mpLv >= 3 ? " (MAX)" : "";
            mpLevelText.text = mpLv > 0 ? $"Lv. {mpLv}  (+{mpBonus} Năng lượng){maxLabel}" : $"Lv. {mpLv}";
        }
        if (cooldownLevelText != null)
        {
            float cdBonus = cdLv * 0.5f; // Giảm 0.5s mỗi cấp độ
            string maxLabel = cdLv >= 3 ? " (MAX)" : "";
            cooldownLevelText.text = cdLv > 0 ? $"Lv. {cdLv}  (-{cdBonus:0.0}s){maxLabel}" : $"Lv. {cdLv}";
        }
        if (damageLevelText != null)
        {
            int dmgBonus = dmgLv * 5; // +5 sát thương mỗi cấp độ
            string maxLabel = dmgLv >= 3 ? " (MAX)" : "";
            damageLevelText.text = dmgLv > 0 ? $"Lv. {dmgLv}  (+{dmgBonus}){maxLabel}" : $"Lv. {dmgLv}";
        }
        if (speedLevelText != null)
        {
            float spdBonus = spdLv * 0.5f; // +0.5 tốc độ chạy mỗi cấp độ
            string maxLabel = spdLv >= 3 ? " (MAX)" : "";
            speedLevelText.text = spdLv > 0 ? $"Lv. {spdLv}  (+{spdBonus:0.0}){maxLabel}" : $"Lv. {spdLv}";
        }

        if (btnUpgradeHp != null) btnUpgradeHp.SetEnabled(points > 0 && hpLv < 3);
        if (btnUpgradeMp != null) btnUpgradeMp.SetEnabled(points > 0 && mpLv < 3);
        if (btnUpgradeCooldown != null) btnUpgradeCooldown.SetEnabled(points > 0 && cdLv < 3);
        if (btnUpgradeDamage != null) btnUpgradeDamage.SetEnabled(points > 0 && dmgLv < 3);
        if (btnUpgradeSpeed != null) btnUpgradeSpeed.SetEnabled(points > 0 && spdLv < 3);

        // Giảm thời gian hồi chiêu tương ứng (-0.5s mỗi cấp độ)
        cooldownTimeQ = Mathf.Max(0.5f, 10f - cdLv * 0.5f);
        cooldownTimeR = Mathf.Max(0.5f, 15f - cdLv * 0.5f);
        cooldownTimeE = Mathf.Max(0.5f, 12f - cdLv * 0.5f);
    }

    public void TriggerElenaCooldownE()
    {
        currentCooldownE = cooldownTimeE;
    }

    public void TriggerCooldownE()
    {
        currentCooldownE = cooldownTimeE;
    }

    public void TriggerElenaCooldownQ()
    {
        currentCooldownQ = cooldownTimeQ;
    }

    public void TriggerElenaCooldownR()
    {
        currentCooldownR = cooldownTimeR;
    }

    public void TriggerCooldownR()
    {
        currentCooldownR = cooldownTimeR;
    }

    public void TriggerMayaCooldownE()
    {
        currentCooldownE = cooldownTimeE;
    }

    public void TriggerMayaCooldownQ()
    {
        currentCooldownQ = cooldownTimeQ;
    }

    public void TriggerMayaCooldownR()
    {
        currentCooldownR = cooldownTimeR;
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
            int currentLevel = 0;
            switch (statType)
            {
                case 0: currentLevel = currentHpLv; break;
                case 1: currentLevel = currentMpLv; break;
                case 2: currentLevel = currentCdLv; break;
                case 3: currentLevel = currentDmgLv; break;
                case 4: currentLevel = currentSpdLv; break;
            }

            if (currentLevel >= 3)
            {
                Debug.LogWarning($"[PlayerHUDController] Stat {statType} đã đạt cấp tối đa (3)!");
                return;
            }

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

        // Cập nhật tên Skill tương ứng theo từng nhân vật
        string qName = "Ảo Ảnh Chém";
        string eName = "Tăng Tốc Chạy";
        string rName = "Chưởng Nguyên Tố";

        if (profileIndex == 1) // Maya (Support)
        {
            qName = "Triệu Hồi Người Xương";
            eName = "Hồi Huyết";
            rName = "Chưởng Nguyên Tố";
        }
        else if (profileIndex == 2) // Elena (Archer)
        {
            qName = "Triệu Hồi Dò Đường";
            eName = "Bắn Ra 3 Tên";
            rName = "Chưởng Nguyên Tố";
        }
        else if (profileIndex == 3) // Arthur (Tanker)
        {
            qName = "Làm Choáng Diện Rộng";
            eName = "Miễn Nhiễm";
            rName = "Chưởng Nguyên Tố";
        }

        if (skillNameLabelQ != null) skillNameLabelQ.text = qName;
        if (skillNameLabelE != null) skillNameLabelE.text = eName;
        if (skillNameLabelR != null) skillNameLabelR.text = rName;

        // 4. UI Skill R không cần hiển thị indicator thanh banner đếm ngược (chỉ hồi chiêu trên ô kỹ năng)
        if (invisibilityIndicator != null)
        {
            invisibilityIndicator.style.display = DisplayStyle.None;
        }

        // 5. Tùy chỉnh UI Skill E indicator theo từng nhân vật
        if (speedBoostIndicator != null)
        {
            var skillELabel = speedBoostIndicator.Q<Label>("speedboost-text");
            if (profileIndex == 0) // Leo
            {
                if (skillELabel != null) skillELabel.text = "TĂNG TỐC CHẠY";
            }
            else if (profileIndex == 1) // Maya
            {
                if (skillELabel != null) skillELabel.text = "HỒI HUYẾT";
            }
            else if (profileIndex == 2) // Elena
            {
                if (skillELabel != null) skillELabel.text = "BẮN RA 3 TÊN";
            }
            else if (profileIndex == 3) // Arthur Tanker
            {
                if (skillELabel != null) skillELabel.text = "MIỄN NHIỄM";
            }
            else
            {
                if (skillELabel != null) skillELabel.text = "TĂNG TỐC CHẠY";
            }
        }

        // 6. Tùy chỉnh UI Skill Q indicator theo từng nhân vật
        if (qSkillIndicator != null)
        {
            var skillQLabel = qSkillIndicator.Q<Label>("qskill-text");
            if (profileIndex == 0) // Leo
            {
                if (skillQLabel != null) skillQLabel.text = "ẢO ẢNH CHÉM";
            }
            else if (profileIndex == 1) // Maya
            {
                if (skillQLabel != null) skillQLabel.text = "TRIỆU HỒI NGƯỜI XƯƠNG";
            }
            else if (profileIndex == 2) // Elena
            {
                if (skillQLabel != null) skillQLabel.text = "TRIỆU HỒI DÒ ĐƯỜNG";
            }
            else if (profileIndex == 3) // Arthur Tanker
            {
                if (skillQLabel != null) skillQLabel.text = "LÀM CHOÁNG DIỆN RỘNG";
            }
            else
            {
                if (skillQLabel != null) skillQLabel.text = "ẢO ẢNH CHÉM";
            }
        }

        UpdateWeaponActionHints();
        RefreshAllBars();
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
        if (ngoc1Sprite == null) ngoc1Sprite = Resources.Load<Sprite>("crystal_purple");
        if (ngoc2Sprite == null) ngoc2Sprite = Resources.Load<Sprite>("crystal_red");
        EnsureCoopBuildSpritesLoaded();
        SetupEventSystemForInputSystem();
    }

    private void EnsureCoopBuildSpritesLoaded()
    {
        if (coopBuildFrameLeftSprite == null)
        {
            coopBuildFrameLeftSprite = LoadSpriteAnywhere(
                "SPR_FantasyMenus_Frame_Popup_01_Left",
                "Assets/Synty/InterfaceFantasyMenus/Sprites/FantasyMenus/SPR_FantasyMenus_Frame_Popup_01_Left.png"
            );
        }
        if (coopBuildFrameRightSprite == null)
        {
            coopBuildFrameRightSprite = LoadSpriteAnywhere(
                "SPR_FantasyMenus_Frame_Popup_01_Right",
                "Assets/Synty/InterfaceFantasyMenus/Sprites/FantasyMenus/SPR_FantasyMenus_Frame_Popup_01_Right.png"
            );
        }
    }

    private Sprite LoadSpriteAnywhere(string resourceName, string assetPath)
    {
        Sprite s = Resources.Load<Sprite>(resourceName);
        if (s != null) return s;

        Texture2D tex = Resources.Load<Texture2D>(resourceName);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

#if UNITY_EDITOR
        var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (allAssets != null && allAssets.Length > 0)
        {
            foreach (var obj in allAssets)
            {
                if (obj is Sprite sp) return sp;
            }
            foreach (var obj in allAssets)
            {
                if (obj is Texture2D t)
                {
                    return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
                }
            }
        }
#endif
        return null;
    }

    public void SetupEventSystemForInputSystem()
    {
        var activeES = UnityEngine.EventSystems.EventSystem.current;
        if (activeES == null)
        {
            var allES = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
            if (allES != null && allES.Length > 0)
            {
                activeES = allES[0];
            }
        }

        if (activeES == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            activeES = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        }

        activeES.gameObject.SetActive(true);
        activeES.enabled = true;

#if ENABLE_INPUT_SYSTEM
        // Ưu tiên InputSystemUIInputModule (New Input System) — tạo nếu chưa có
        var inputSystemModule = activeES.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (inputSystemModule == null)
        {
            inputSystemModule = activeES.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        inputSystemModule.enabled = true;
        inputSystemModule.AssignDefaultActions();

        // TẮT StandaloneInputModule vì nó xung đột với InputSystemUIInputModule
        // Khi cả hai cùng enabled, StandaloneInputModule "nuốt" pointer events
        // khiến UI Toolkit không nhận được click chuột
        var standalone = activeES.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone != null)
        {
            standalone.enabled = false;
        }
#else
        // Fallback: Chỉ dùng StandaloneInputModule khi KHÔNG có New Input System
        var standalone = activeES.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone == null)
        {
            standalone = activeES.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        standalone.enabled = true;
#endif
    }

    /// <summary>
    /// Đảm bảo UIDocument có PanelEventHandler và PanelRaycaster để nhận pointer events.
    /// Trong Unity 6, hai component này thường được auto-add nhưng có thể bị thiếu
    /// khi GameObject bị disable/enable lại (HUD switching).
    /// </summary>
    private void EnsurePanelEventComponents()
    {
        if (uiDocument == null) return;
        GameObject go = uiDocument.gameObject;

        // PanelEventHandler: cần thiết để UI Toolkit nhận events từ EventSystem
        var handler = go.GetComponent<UnityEngine.UIElements.PanelEventHandler>();
        if (handler == null)
        {
            handler = go.AddComponent<UnityEngine.UIElements.PanelEventHandler>();
            Debug.Log("[PlayerHUDController] Đã tạo PanelEventHandler trên UIDocument!");
        }
        handler.enabled = true;

        // PanelRaycaster: cần thiết để EventSystem raycast tới UI Toolkit panel
        var raycaster = go.GetComponent<UnityEngine.UIElements.PanelRaycaster>();
        if (raycaster == null)
        {
            raycaster = go.AddComponent<UnityEngine.UIElements.PanelRaycaster>();
            Debug.Log("[PlayerHUDController] Đã tạo PanelRaycaster trên UIDocument!");
        }
        raycaster.enabled = true;

        Debug.Log($"[PlayerHUDController] EnsurePanelEventComponents: handler={handler.enabled}, raycaster={raycaster.enabled}");
    }

    /// <summary>
    /// Hệ thống Fallback: Xử lý click chuột thủ công bằng panel.Pick() khi Inventory mở.
    /// Gọi trong Update() để bypass hoàn toàn EventSystem pipeline nếu pointer events không hoạt động.
    /// </summary>
    /// <summary>
    /// Set sprite background cho dragGhost từ tên vật phẩm
    /// </summary>
    private void SetDragGhostSprite(string itemName)
    {
        if (dragGhost == null) return;
        if (itemName == "RepairHammer" && repairHammerSprite != null)
        {
            dragGhost.style.backgroundImage = new StyleBackground(repairHammerSprite);
        }
        else if (itemName == "Ngoc1" && ngoc1Sprite != null)
        {
            dragGhost.style.backgroundImage = new StyleBackground(ngoc1Sprite);
        }
        else if (itemName == "Ngoc2" && ngoc2Sprite != null)
        {
            dragGhost.style.backgroundImage = new StyleBackground(ngoc2Sprite);
        }
        else if ((itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                  itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase) ||
                  itemName.Equals("wood_stack", System.StringComparison.OrdinalIgnoreCase)) && woodLogSprite != null)
        {
            dragGhost.style.backgroundImage = new StyleBackground(woodLogSprite);
        }
        else
        {
            dragGhost.style.backgroundImage = StyleKeyword.Null;
        }
    }

    /// <summary>
    /// Cập nhật hiển thị Tooltip chú thích vật phẩm theo vị trí con trỏ chuột
    /// </summary>
    private void UpdateItemTooltipInfo(string itemName, Vector2 panelPos)
    {
        if (tooltipElement == null || tooltipTitle == null || tooltipDesc == null) return;

        if (string.IsNullOrEmpty(itemName))
        {
            tooltipElement.style.opacity = 0f;
            return;
        }

        if (itemName == "RepairHammer")
        {
            tooltipTitle.text = "BÚA RÈN MA THUẬT";
            tooltipDesc.text = "Click chuột phải để rèn lại 100% độ bền vũ khí đang trang bị.";
        }
        else if (itemName == "Ngoc1")
        {
            tooltipTitle.text = "NGỌC TÍM BÍ ẨN";
            tooltipDesc.text = "Viên ngọc lấp lánh cất giấu bí thuật... Click chuột phải để sử dụng hồi 30 HP.";
        }
        else if (itemName == "Ngoc2")
        {
            tooltipTitle.text = "NGỌC ĐỎ BÍ ẨN";
            tooltipDesc.text = "Viên ngọc rực lửa mang năng lượng thần bí... Click chuột phải để sử dụng hồi 30 HP.";
        }
        else if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                 itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase) ||
                 itemName.Equals("wood_stack", System.StringComparison.OrdinalIgnoreCase))
        {
            tooltipTitle.text = "THANH GỖ";
            tooltipDesc.text = "Vật liệu chắc chắn dùng để xây dựng/sửa chữa cầu.";
        }
        else if (itemName.Contains("Potion") || itemName.Contains("HP") || itemName.Contains("Heal"))
        {
            tooltipTitle.text = "BÌNH THUỐC MÁU";
            tooltipDesc.text = "Click chuột phải để hồi 30 sinh lực (HP).";
        }
        else
        {
            tooltipTitle.text = itemName.ToUpper();
            tooltipDesc.text = "Vật phẩm trong hành trang.";
        }

        tooltipElement.style.left = panelPos.x + 15f;
        tooltipElement.style.top = panelPos.y + 15f;
        tooltipElement.style.opacity = 1f;
    }

    /// <summary>
    /// Hệ thống Fallback: Xử lý click chuột, kéo thả (Drag & Drop) và chú thích vật phẩm (Tooltip)
    /// </summary>
    private void HandleManualInventoryClick()
    {
        if (!isAnyUIOpen) return;
        if (inventoryOverlay == null || !inventoryOverlay.ClassListContains("show-inventory")) return;
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var panel = uiDocument.rootVisualElement.panel;
        if (panel == null) return;

        // Chuyển đổi tọa độ chuột từ Unity Screen (y=0 ở dưới) sang GUI Screen (y=0 ở trên)
        Vector2 guiMousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, guiMousePos);

        VisualElement picked = panel.Pick(panelPos);

        // 1. Xử lý BẮT ĐẦU KÉO THẢ (Mouse Down 0)
        if (Input.GetMouseButtonDown(0))
        {
            if (picked != null)
            {
                Debug.Log($"[Inventory Fallback] Click trái tại: {picked.name ?? picked.GetType().Name} (class: {string.Join(",", picked.GetClasses())})");

                // Kiểm tra nếu click vào inventory slot (hoặc con của slot)
                VisualElement slotElement = picked;
                while (slotElement != null && !(slotElement.name != null && slotElement.name.StartsWith("inventory-slot-")))
                {
                    slotElement = slotElement.parent;
                }
                if (slotElement != null)
                {
                    int slotIdx = int.Parse(slotElement.name.Replace("inventory-slot-", ""));
                    Debug.Log($"[Inventory Fallback] Bắt đầu kéo slot {slotIdx}!");

                    // Kích hoạt kéo thả nếu ô có đồ
                    string[] invData = LocalPlayerTarget != null && LocalPlayerTarget.InventorySlots != null ? LocalPlayerTarget.InventorySlots : currentInventoryData;
                    if (invData != null && slotIdx >= 0 && slotIdx < invData.Length && !string.IsNullOrEmpty(invData[slotIdx]))
                    {
                        draggedSlotIndex = slotIdx;
                        string itemVal = invData[slotIdx];
                        string itemName = itemVal.Split(':')[0];

                        SetDragGhostSprite(itemName);
                        if (dragGhost != null)
                        {
                            dragGhost.style.left = panelPos.x - 27f;
                            dragGhost.style.top = panelPos.y - 27f;
                            dragGhost.style.display = DisplayStyle.Flex;
                        }
                        if (tooltipElement != null) tooltipElement.style.opacity = 0f;
                    }
                }

                // Kiểm tra nếu click vào nút upgrade (hoặc nhãn chữ bên trong nút)
                VisualElement currElem = picked;
                Button btn = null;
                while (currElem != null)
                {
                    if (currElem is Button b) { btn = b; break; }
                    currElem = currElem.parent;
                }

                if (btn != null)
                {
                    Debug.Log($"[Inventory Fallback] Kích hoạt nâng cấp qua Button: {btn.name}!");
                    if (btn.name == "btn-upgrade-hp") UpgradeStat(0);
                    else if (btn.name == "btn-upgrade-mp") UpgradeStat(1);
                    else if (btn.name == "btn-upgrade-cooldown") UpgradeStat(2);
                    else if (btn.name == "btn-upgrade-damage") UpgradeStat(3);
                    else if (btn.name == "btn-upgrade-speed") UpgradeStat(4);
                }

                // Kiểm tra nếu click vào overlay background ngoài khung (đóng inventory)
                if (picked == inventoryOverlay)
                {
                    ToggleInventory();
                    return;
                }
            }
        }

        // 2. Xử lý DI CHUYỂN KÉO THẢ (Mouse Holding 0)
        if (Input.GetMouseButton(0) && draggedSlotIndex != -1)
        {
            if (dragGhost != null)
            {
                dragGhost.style.left = panelPos.x - 27f;
                dragGhost.style.top = panelPos.y - 27f;
                dragGhost.style.display = DisplayStyle.Flex;
            }
            if (tooltipElement != null) tooltipElement.style.opacity = 0f;
        }

        // 3. Xử lý THẢ CHUỘT (Mouse Up 0)
        if (Input.GetMouseButtonUp(0) && draggedSlotIndex != -1)
        {
            if (dragGhost != null)
            {
                dragGhost.style.display = DisplayStyle.None;
            }

            // Tìm ô đích tại vị trí thả chuột
            VisualElement slotElement = picked;
            while (slotElement != null && !(slotElement.name != null && slotElement.name.StartsWith("inventory-slot-")))
            {
                slotElement = slotElement.parent;
            }

            if (slotElement != null)
            {
                int targetIndex = int.Parse(slotElement.name.Replace("inventory-slot-", ""));
                if (targetIndex != draggedSlotIndex && LocalPlayerTarget != null && LocalPlayerTarget.InventorySlots != null)
                {
                    Debug.Log($"[Inventory Fallback] Tráo đổi vật phẩm từ slot {draggedSlotIndex} sang slot {targetIndex}");
                    string temp = LocalPlayerTarget.InventorySlots[draggedSlotIndex];
                    LocalPlayerTarget.InventorySlots[draggedSlotIndex] = LocalPlayerTarget.InventorySlots[targetIndex];
                    LocalPlayerTarget.InventorySlots[targetIndex] = temp;

                    SetInventorySlots(LocalPlayerTarget.InventorySlots);
                    if (!LocalPlayerTarget.IsStandaloneMode) LocalPlayerTarget.SavePlayerStateToDatabase();
                }
            }
            else if (picked == inventoryOverlay || picked == null)
            {
                // Thả ra ngoài khung hành trang -> Vứt vật phẩm ra đất!
                Debug.Log($"[Inventory Fallback] Vứt vật phẩm ở slot {draggedSlotIndex} ra đất!");
                DropItemFromInventory(draggedSlotIndex);
            }

            draggedSlotIndex = -1;
        }

        // 4. Xử lý CLICK CHUỘT PHẢI (Mouse Down 1) — Sử dụng vật phẩm
        if (Input.GetMouseButtonDown(1))
        {
            if (picked != null)
            {
                Debug.Log($"[Inventory Fallback] Click phải tại: {picked.name ?? picked.GetType().Name}");

                VisualElement slotElement = picked;
                while (slotElement != null && !(slotElement.name != null && slotElement.name.StartsWith("inventory-slot-")))
                {
                    slotElement = slotElement.parent;
                }
                if (slotElement != null)
                {
                    int slotIdx = int.Parse(slotElement.name.Replace("inventory-slot-", ""));
                    Debug.Log($"[Inventory Fallback] Click phải vào slot {slotIdx} → UseItem!");
                    UseItem(slotIdx);
                }
            }
        }

        // 5. Xử lý HOVER HIỂN THỊ CHÚ THÍCH (Tooltip) khi không kéo thả
        if (draggedSlotIndex == -1)
        {
            VisualElement hoverSlot = picked;
            while (hoverSlot != null && !(hoverSlot.name != null && hoverSlot.name.StartsWith("inventory-slot-")))
            {
                hoverSlot = hoverSlot.parent;
            }

            if (hoverSlot != null)
            {
                int slotIdx = int.Parse(hoverSlot.name.Replace("inventory-slot-", ""));
                string[] invData = LocalPlayerTarget != null && LocalPlayerTarget.InventorySlots != null ? LocalPlayerTarget.InventorySlots : currentInventoryData;
                if (invData != null && slotIdx >= 0 && slotIdx < invData.Length && !string.IsNullOrEmpty(invData[slotIdx]))
                {
                    string slotVal = invData[slotIdx];
                    string itemName = slotVal.Split(':')[0];
                    UpdateItemTooltipInfo(itemName, panelPos);
                }
                else
                {
                    if (tooltipElement != null) tooltipElement.style.opacity = 0f;
                }
            }
            else
            {
                if (tooltipElement != null) tooltipElement.style.opacity = 0f;
            }
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
            if (LocalPlayerTarget != null && (player == LocalPlayerTarget || player.gameObject == LocalPlayerTarget.gameObject)) continue;

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
                if (card != null && card.parent != null)
                {
                    try
                    {
                        card.parent.Remove(card);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[PlayerHUDController] Loi khi remove card khoi hierarchy: {ex.Message}");
                    }
                }
                teammateCards.Remove(key);
            }
        }
    }

    void OnDestroy()
    {
        if (minimapRenderTexture != null)
        {
            minimapRenderTexture.Release();
            Destroy(minimapRenderTexture);
            minimapRenderTexture = null;
        }
        if (minimapCamera != null)
        {
            Destroy(minimapCamera.gameObject);
            minimapCamera = null;
        }

        if (worldMapRenderTexture != null)
        {
            worldMapRenderTexture.Release();
            Destroy(worldMapRenderTexture);
            worldMapRenderTexture = null;
        }
        if (worldMapCamera != null)
        {
            Destroy(worldMapCamera.gameObject);
            worldMapCamera = null;
        }
        worldMapFrame = null;
        worldMapIcons.Clear();
    }

    private void UpdateMinimap()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        if (minimapContainer == null)
        {
            minimapContainer = uiDocument.rootVisualElement.Q<VisualElement>("minimap-container");
        }
        if (minimapContent == null)
        {
            minimapContent = uiDocument.rootVisualElement.Q<VisualElement>("minimap-content");
        }

        if (minimapContainer == null || minimapContent == null) return;

        // An minimap neu khong co local player
        if (LocalPlayerTarget == null || LocalPlayerTarget.transform == null)
        {
            minimapContainer.style.display = DisplayStyle.None;
            return;
        }

        minimapContainer.style.display = DisplayStyle.Flex;

        Vector3 localPos = LocalPlayerTarget.transform.position;

        // Khoi tao render texture va camera neu chua co
        if (minimapRenderTexture == null)
        {
            minimapRenderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            minimapRenderTexture.filterMode = FilterMode.Bilinear;
            minimapRenderTexture.Create();
        }

        if (minimapCamera == null)
        {
            GameObject camGo = new GameObject("MinimapCamera_Generated");
            minimapCamera = camGo.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = minimapZoom;
            minimapCamera.targetTexture = minimapRenderTexture;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.04f, 0.06f, 0.12f); // Sleek dark blue
            minimapCamera.cullingMask = ~(1 << LayerMask.NameToLayer("UI")); // Exclude UI
            
            // Camera luon huong thang xuong (North Up)
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // Di chuyen camera theo vi tri local player
        minimapCamera.transform.position = new Vector3(localPos.x, localPos.y + 50f, localPos.z);

        // Gan RenderTexture lam background cho minimap-content
        minimapContent.style.backgroundImage = Background.FromRenderTexture(minimapRenderTexture);

        // Tinh toan kich thuoc thuc te cua minimap de scale toa do dong
        float mapWidth = float.IsNaN(minimapContent.layout.width) || minimapContent.layout.width <= 0 ? 196f : minimapContent.layout.width;
        float mapHeight = float.IsNaN(minimapContent.layout.height) || minimapContent.layout.height <= 0 ? 196f : minimapContent.layout.height;
        float centerX = mapWidth / 2f;
        float centerY = mapHeight / 2f;
        float radius = Mathf.Min(mapWidth, mapHeight) / 2f;

        // Cap nhat cac icon player di chuyen
        var activePlayers = PlayerHUDManager.ActivePlayers;
        System.Collections.Generic.HashSet<ulong> currentKeys = new System.Collections.Generic.HashSet<ulong>();

        foreach (var player in activePlayers)
        {
            if (player == null || player.gameObject == null || player.transform == null) continue;

            ulong key = player.IsSpawned ? player.OwnerClientId : (ulong)player.gameObject.GetInstanceID();
            currentKeys.Add(key);

            bool isOwner = (LocalPlayerTarget != null && (player == LocalPlayerTarget || player.gameObject == LocalPlayerTarget.gameObject));

            if (!minimapIcons.TryGetValue(key, out var iconData))
            {
                iconData = CreateMinimapIcon(player, isOwner);
                minimapContent.Add(iconData.iconContainer);
                minimapIcons[key] = iconData;
            }

            // Tinh toan vi tri relative so voi local player
            Vector3 diff = player.transform.position - localPos;
            float dx = diff.x;
            float dz = diff.z;
            float scale = radius / minimapZoom;

            float rx = dx * scale;
            float ry = dz * scale;
            float dist = Mathf.Sqrt(rx * rx + ry * ry);

            // Gioi han ban kinh clamp de icon khong bi khuat khoi vong tron
            float maxRadius = radius - 12f; // Offset 12px cho icon size 24x24 px
            if (dist > maxRadius)
            {
                float clampScale = maxRadius / dist;
                rx *= clampScale;
                ry *= clampScale;
            }

            // UI coordinates: truc Y huong xuong duoi, trong khi Z huong len tren
            float x_ui = centerX + rx;
            float y_ui = centerY - ry;

            // Offset de can giua icon (size 24x24 px, offset = 12px)
            iconData.iconContainer.style.left = x_ui - 12f;
            iconData.iconContainer.style.top = y_ui - 12f;
            iconData.iconContainer.style.display = DisplayStyle.Flex;

            // Xoay arrow huong di chuyen cua player
            float facingAngle = player.transform.eulerAngles.y;
            iconData.arrowContainer.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(facingAngle)));
        }

        // Don dep cac player da thoat khoi danh sach active
        System.Collections.Generic.List<ulong> keysToRemove = new System.Collections.Generic.List<ulong>();
        foreach (var existingKey in minimapIcons.Keys)
        {
            if (!currentKeys.Contains(existingKey))
            {
                keysToRemove.Add(existingKey);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (minimapIcons.TryGetValue(key, out var iconData))
            {
                if (iconData.iconContainer != null && iconData.iconContainer.parent != null)
                {
                    iconData.iconContainer.parent.Remove(iconData.iconContainer);
                }
                minimapIcons.Remove(key);
            }
        }
    }

    private void UpdateWorldMap()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null || worldMapOverlay == null || worldMapFrame == null) return;

        // Check xem map dang mo khong
        if (!IsMapOpen())
        {
            if (worldMapCamera != null)
            {
                worldMapCamera.enabled = false;
            }
            // Clear cac icon tren world map khi dong
            if (worldMapIcons.Count > 0)
            {
                foreach (var iconData in worldMapIcons.Values)
                {
                    if (iconData.iconContainer != null && iconData.iconContainer.parent != null)
                    {
                        iconData.iconContainer.parent.Remove(iconData.iconContainer);
                    }
                }
                worldMapIcons.Clear();
            }
            return;
        }

        // Neu khong co local player
        if (LocalPlayerTarget == null || LocalPlayerTarget.transform == null)
        {
            return;
        }

        Vector3 localPos = LocalPlayerTarget.transform.position;

        // Khoi tao render texture va camera cho World Map
        if (worldMapRenderTexture == null)
        {
            worldMapRenderTexture = new RenderTexture(1024, 512, 16, RenderTextureFormat.ARGB32);
            worldMapRenderTexture.filterMode = FilterMode.Bilinear;
            worldMapRenderTexture.Create();
        }

        if (worldMapCamera == null)
        {
            GameObject camGo = new GameObject("WorldMapCamera_Generated");
            worldMapCamera = camGo.AddComponent<Camera>();
            worldMapCamera.orthographic = true;
            worldMapCamera.orthographicSize = worldMapZoom;
            worldMapCamera.aspect = 2.0f;
            worldMapCamera.targetTexture = worldMapRenderTexture;
            worldMapCamera.clearFlags = CameraClearFlags.SolidColor;
            worldMapCamera.backgroundColor = new Color(0.04f, 0.06f, 0.12f);
            
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                worldMapCamera.cullingMask = ~(1 << uiLayer);
            }
            else
            {
                worldMapCamera.cullingMask = ~0;
            }
            
            worldMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        worldMapCamera.enabled = true;
        // Camera luon follow player nhung voi tam nhin rong hon nhieu
        worldMapCamera.transform.position = new Vector3(localPos.x, localPos.y + 100f, localPos.z);

        float effectiveZoom = Mathf.Clamp(worldMapZoom <= 40f ? worldMapZoom : 30f, 15f, 40f);
        worldMapCamera.orthographicSize = effectiveZoom;

        // Gan background image cho worldMapViewport (neu co) hoac worldMapFrame
        VisualElement mapRenderElem = worldMapFrame.Q<VisualElement>("world-map-viewport") ?? worldMapFrame;
        mapRenderElem.style.backgroundImage = Background.FromRenderTexture(worldMapRenderTexture);

        // Tinh toan kich thuoc frame de scale toa do
        float frameWidth = float.IsNaN(mapRenderElem.layout.width) || mapRenderElem.layout.width <= 0 ? 920f : mapRenderElem.layout.width;
        float frameHeight = float.IsNaN(mapRenderElem.layout.height) || mapRenderElem.layout.height <= 0 ? 350f : mapRenderElem.layout.height;
        worldMapCamera.aspect = frameWidth / frameHeight;
        float centerX = frameWidth / 2f;
        float centerY = frameHeight / 2f;

        var activePlayers = PlayerHUDManager.ActivePlayers;
        System.Collections.Generic.HashSet<ulong> currentKeys = new System.Collections.Generic.HashSet<ulong>();

        foreach (var player in activePlayers)
        {
            if (player == null || player.gameObject == null || player.transform == null) continue;

            ulong key = player.IsSpawned ? player.OwnerClientId : (ulong)player.gameObject.GetInstanceID();
            currentKeys.Add(key);

            bool isOwner = (LocalPlayerTarget != null && (player == LocalPlayerTarget || player.gameObject == LocalPlayerTarget.gameObject));

            if (!worldMapIcons.TryGetValue(key, out var iconData))
            {
                iconData = CreateWorldMapIcon(player, isOwner);
                mapRenderElem.Add(iconData.iconContainer);
                worldMapIcons[key] = iconData;
            }

            Vector3 diff = player.transform.position - localPos;
            float dx = diff.x;
            float dz = diff.z;

            // Map world space sang frame UI space
            float scale = (frameHeight / 2f) / effectiveZoom;

            float rx = dx * scale;
            float ry = dz * scale;

            // Clamp vao trong khung ban do lon
            float limitX = centerX - 16f; // half size 32px icon
            float limitY = centerY - 16f;
            rx = Mathf.Clamp(rx, -limitX, limitX);
            ry = Mathf.Clamp(ry, -limitY, limitY);

            float x_ui = centerX + rx;
            float y_ui = centerY - ry;

            iconData.iconContainer.style.left = x_ui - 16f;
            iconData.iconContainer.style.top = y_ui - 16f;
            iconData.iconContainer.style.display = DisplayStyle.Flex;

            float facingAngle = player.transform.eulerAngles.y;
            iconData.arrowContainer.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(facingAngle)));
        }

        // Don dep cac player thoat game
        System.Collections.Generic.List<ulong> keysToRemove = new System.Collections.Generic.List<ulong>();
        foreach (var existingKey in worldMapIcons.Keys)
        {
            if (!currentKeys.Contains(existingKey))
            {
                keysToRemove.Add(existingKey);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (worldMapIcons.TryGetValue(key, out var iconData))
            {
                if (iconData.iconContainer != null && iconData.iconContainer.parent != null)
                {
                    iconData.iconContainer.parent.Remove(iconData.iconContainer);
                }
                worldMapIcons.Remove(key);
            }
        }
    }

    private WorldMapIconData CreateWorldMapIcon(IPlayerHUDTarget player, bool isOwner)
    {
        WorldMapIconData data = new WorldMapIconData();

        data.iconContainer = new VisualElement();
        data.iconContainer.AddToClassList("world-map-player-icon");

        Color classColor = GetMinimapClassColor(player.CharacterClassIndex, isOwner);
        data.iconContainer.style.borderTopColor = classColor;
        data.iconContainer.style.borderBottomColor = classColor;
        data.iconContainer.style.borderLeftColor = classColor;
        data.iconContainer.style.borderRightColor = classColor;

        data.avatarElement = new VisualElement();
        data.avatarElement.AddToClassList("world-map-player-avatar");

        Sprite avatarSprite = null;
        if (PlayerHUDManager.Instance != null)
        {
            avatarSprite = PlayerHUDManager.Instance.GetTeammateAvatar(player.CharacterClassIndex);
        }

        if (avatarSprite == null && hudProfiles != null && hudProfiles.Count > 0)
        {
            int idx = player.CharacterClassIndex;
            if (idx >= 0 && idx < hudProfiles.Count)
            {
                avatarSprite = hudProfiles[idx].avatarSprite;
            }
            else
            {
                avatarSprite = hudProfiles[0].avatarSprite;
            }
        }

        if (avatarSprite != null)
        {
            data.avatarElement.style.backgroundImage = new StyleBackground(avatarSprite);
        }
        data.iconContainer.Add(data.avatarElement);

        data.arrowContainer = new VisualElement();
        data.arrowContainer.style.position = Position.Absolute;
        data.arrowContainer.style.width = Length.Percent(100f);
        data.arrowContainer.style.height = Length.Percent(100f);
        data.arrowContainer.pickingMode = PickingMode.Ignore;

        VisualElement arrow = new VisualElement();
        arrow.AddToClassList("world-map-player-arrow");
        arrow.style.backgroundColor = classColor;
        arrow.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(45f)));
        data.arrowContainer.Add(arrow);

        data.iconContainer.Add(data.arrowContainer);

        return data;
    }

    private MinimapIconData CreateMinimapIcon(IPlayerHUDTarget player, bool isOwner)
    {
        MinimapIconData data = new MinimapIconData();

        // 1. Container cho icon
        data.iconContainer = new VisualElement();
        data.iconContainer.AddToClassList("minimap-player-icon");

        Color classColor = GetMinimapClassColor(player.CharacterClassIndex, isOwner);
        data.iconContainer.style.borderTopColor = classColor;
        data.iconContainer.style.borderBottomColor = classColor;
        data.iconContainer.style.borderLeftColor = classColor;
        data.iconContainer.style.borderRightColor = classColor;

        // 2. Avatar cua nhan vat
        data.avatarElement = new VisualElement();
        data.avatarElement.AddToClassList("minimap-player-avatar");

        Sprite avatarSprite = null;
        if (PlayerHUDManager.Instance != null)
        {
            avatarSprite = PlayerHUDManager.Instance.GetTeammateAvatar(player.CharacterClassIndex);
        }

        if (avatarSprite == null && hudProfiles != null && hudProfiles.Count > 0)
        {
            int idx = player.CharacterClassIndex;
            if (idx >= 0 && idx < hudProfiles.Count)
            {
                avatarSprite = hudProfiles[idx].avatarSprite;
            }
            else
            {
                avatarSprite = hudProfiles[0].avatarSprite;
            }
        }

        if (avatarSprite != null)
        {
            data.avatarElement.style.backgroundImage = new StyleBackground(avatarSprite);
        }
        data.iconContainer.Add(data.avatarElement);

        // 3. Arrow Container de xoay doc lap (tranh lam nguoc/xoay anh avatar nhan vat)
        data.arrowContainer = new VisualElement();
        data.arrowContainer.style.position = Position.Absolute;
        data.arrowContainer.style.width = Length.Percent(100f);
        data.arrowContainer.style.height = Length.Percent(100f);
        data.arrowContainer.pickingMode = PickingMode.Ignore;

        // 4. Mui ten huong di chuyen (la 1 diamond xoay 45 do)
        VisualElement arrow = new VisualElement();
        arrow.AddToClassList("minimap-player-arrow");
        arrow.style.backgroundColor = classColor;
        arrow.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(45f)));
        data.arrowContainer.Add(arrow);

        data.iconContainer.Add(data.arrowContainer);

        return data;
    }

    private Color GetMinimapClassColor(int classIdx, bool isOwner)
    {
        if (isOwner)
        {
            return new Color(0.1f, 0.8f, 1f); // Vibrant light blue cho local player
        }
        switch (classIdx)
        {
            case 0: return new Color(0.7f, 0.3f, 1f); // Leo - Assassin (Purple)
            case 1: return new Color(1f, 0.4f, 0.4f); // Maya - Support (Red/Pink)
            case 2: return new Color(0.3f, 0.8f, 0.3f); // Elena - Archer (Green)
            case 3: return new Color(1f, 0.8f, 0.2f); // Arthur - Tanker (Gold/Yellow)
            default: return Color.white;
        }
    }

    private VisualElement CreateTeammateCard(IPlayerHUDTarget player)
    {
        var card = new VisualElement();
        card.AddToClassList("teammate-card");

        // 1. Stats Wrapper (rendered first in DOM so left tips are tucked under avatar)
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

        // HP Container with sword frame
        var hpContainer = new VisualElement();
        hpContainer.AddToClassList("teammate-stat-bar-container");
        hpContainer.AddToClassList("teammate-hp-bar-container");

        var hpTrack = new VisualElement();
        hpTrack.AddToClassList("teammate-track-bg");
        hpTrack.AddToClassList("teammate-hp-track");
        var hpFill = new VisualElement();
        hpFill.name = "hp-fill";
        hpFill.AddToClassList("fill-core");
        hpFill.AddToClassList("hp-fill");
        hpTrack.Add(hpFill);
        hpContainer.Add(hpTrack);

        var hpFrame = new VisualElement();
        hpFrame.AddToClassList("teammate-stat-bar-frame");
        hpContainer.Add(hpFrame);

        statsWrapper.Add(hpContainer);

        // MP Container with sword frame
        var mpContainer = new VisualElement();
        mpContainer.AddToClassList("teammate-stat-bar-container");
        mpContainer.AddToClassList("teammate-mp-bar-container");

        var mpTrack = new VisualElement();
        mpTrack.AddToClassList("teammate-track-bg");
        mpTrack.AddToClassList("teammate-mp-track");
        var mpFill = new VisualElement();
        mpFill.name = "mp-fill";
        mpFill.AddToClassList("fill-core");
        mpFill.AddToClassList("mp-fill");
        mpTrack.Add(mpFill);
        mpContainer.Add(mpTrack);

        var mpFrame = new VisualElement();
        mpFrame.AddToClassList("teammate-stat-bar-frame");
        mpContainer.Add(mpFrame);

        statsWrapper.Add(mpContainer);

        // EXP Container with sword frame
        var expContainer = new VisualElement();
        expContainer.AddToClassList("teammate-stat-bar-container");
        expContainer.AddToClassList("teammate-exp-bar-container");

        var expTrack = new VisualElement();
        expTrack.AddToClassList("teammate-track-bg");
        expTrack.AddToClassList("teammate-exp-track");
        var expFill = new VisualElement();
        expFill.name = "exp-fill";
        expFill.AddToClassList("fill-core");
        expFill.AddToClassList("exp-fill");
        expTrack.Add(expFill);
        expContainer.Add(expTrack);

        var expFrame = new VisualElement();
        expFrame.AddToClassList("teammate-stat-bar-frame");
        expContainer.Add(expFrame);

        statsWrapper.Add(expContainer);

        card.Add(statsWrapper);

        // 2. Avatar Container (rendered second in DOM so it sits on top)
        var avatarContainer = new VisualElement();
        avatarContainer.AddToClassList("teammate-avatar-container");

        var avatarImg = new VisualElement();
        avatarImg.name = "avatar-image";
        avatarImg.AddToClassList("teammate-avatar-image");
        avatarContainer.Add(avatarImg);

        var avatarBorder = new VisualElement();
        avatarBorder.AddToClassList("teammate-avatar-border");
        avatarContainer.Add(avatarBorder);

        card.Add(avatarContainer);

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
            else if (player is MayaPlayer || player is MayaSupport)
            {
                classIdx = 1;
            }
            else if (player is ArthurTanker || player is ArthurPlayer)
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
            float maxMp = player.MaxMana;
            float currentMp = player.CurrentMana;
            float mpPct = maxMp > 0 ? (currentMp / maxMp) * 100f : 0f;
            mpFill.style.width = Length.Percent(Mathf.Clamp(mpPct, 0f, 100f));
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

        // Kiểm tra trạng thái đóng/mở của chi tiết hướng dẫn
        if (!isTutorialVisible)
        {
            if (tutorialDetailsContainer != null)
            {
                tutorialDetailsContainer.style.display = DisplayStyle.None;
            }
            if (f1ToggleHint != null)
            {
                f1ToggleHint.style.display = DisplayStyle.Flex;
            }
            return;
        }
        else
        {
            if (tutorialDetailsContainer != null)
            {
                tutorialDetailsContainer.style.display = DisplayStyle.Flex;
            }
            if (f1ToggleHint != null && isTutorialManuallyToggled)
            {
                f1ToggleHint.style.display = DisplayStyle.Flex;
            }
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

        // 4. Hiển thị phím Vũ khí 2
        if (hintWeapon2 != null)
        {
            hintWeapon2.style.display = DisplayStyle.Flex;
        }

        // 5. Hiển thị các phím kỹ năng Q, E, R (nếu đã được mở khóa)
        if (hintSkills != null)
        {
            hintSkills.style.display = isSkillsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 6. Cập nhật gợi ý tấn công / đỡ khiên theo từng nhân vật và slot vũ khí đang chọn
        UpdateWeaponActionHints();
    }

    private void UpdateWeaponActionHints()
    {
        if (hintAttack == null && uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var root = uiDocument.rootVisualElement;
            hintAttack = root.Q<VisualElement>("hint-attack");
            hintAttackIcons = root.Q<VisualElement>("hint-attack-icons");
            hintAttackText = root.Q<Label>("hint-attack-text");
            hintBlock = root.Q<VisualElement>("hint-block");
            hintBlockText = root.Q<Label>("hint-block-text");

            hintIdleAttack = root.Q<VisualElement>("hint-idle-attack");
            hintIdleAttackIcons = root.Q<VisualElement>("hint-idle-attack-icons");
            hintIdleAttackText = root.Q<Label>("hint-idle-attack-text");
            hintIdleBlock = root.Q<VisualElement>("hint-idle-block");
            hintIdleBlockText = root.Q<Label>("hint-idle-block-text");
        }

        int classIdx = LocalPlayerTarget != null ? LocalPlayerTarget.CharacterClassIndex : (lastSelectedProfileIndex >= 0 ? lastSelectedProfileIndex : 0);
        int currentWeapon = currentSelectedWeapon; // 1 = Tay không, 2 = Vũ khí 2

        ConfigureCombatHints(hintAttack, hintAttackIcons, hintAttackText, hintBlock, hintBlockText, classIdx, currentWeapon, false);
        ConfigureCombatHints(hintIdleAttack, hintIdleAttackIcons, hintIdleAttackText, hintIdleBlock, hintIdleBlockText, classIdx, currentWeapon, true);
    }

    private void ConfigureCombatHints(
        VisualElement attackRow, VisualElement attackIcons, Label attackText,
        VisualElement blockRow, Label blockText,
        int classIdx, int currentWeapon, bool isIdle)
    {
        if (currentWeapon == 1) // Ô vũ khí 1 (Tay không)
        {
            if (isIdle)
            {
                // Khi đứng im ở ô vũ khí 1: ẩn gợi ý combat để bảng phím idle hiển thị gọn gàng
                if (attackRow != null) attackRow.style.display = DisplayStyle.None;
                if (blockRow != null) blockRow.style.display = DisplayStyle.None;
            }
            else
            {
                if (attackRow != null) attackRow.style.display = DisplayStyle.Flex;
                if (attackIcons != null)
                {
                    attackIcons.Clear();
                    var lmb = new VisualElement();
                    lmb.AddToClassList("key-icon");
                    lmb.AddToClassList("key-icon-mouse-l");
                    attackIcons.Add(lmb);
                }
                if (attackText != null) attackText.text = "Đấm";
                if (blockRow != null) blockRow.style.display = DisplayStyle.None;
            }
        }
        else // Ô vũ khí 2 (Vũ khí riêng từng phái) -> Hiển thị cả khi di chuyển lẫn khi đứng im!
        {
            if (attackRow != null) attackRow.style.display = DisplayStyle.Flex;

            if (classIdx == 3) // Arthur (Tanker): Click chuột trái Chém + Giữ chuột phải Đỡ khiên
            {
                if (attackIcons != null)
                {
                    attackIcons.Clear();
                    var lmb = new VisualElement();
                    lmb.AddToClassList("key-icon");
                    lmb.AddToClassList("key-icon-mouse-l");
                    attackIcons.Add(lmb);
                }
                if (attackText != null) attackText.text = "Chém";

                if (blockRow != null)
                {
                    blockRow.style.display = DisplayStyle.Flex;
                    if (blockText != null) blockText.text = "Giữ Chuột phải để đỡ khiên";
                }
            }
            else if (classIdx == 2) // Elena (Archer): Chuột phải + Chuột trái -> Bắn Cung
            {
                if (attackIcons != null)
                {
                    attackIcons.Clear();
                    var rmb = new VisualElement();
                    rmb.AddToClassList("key-icon");
                    rmb.AddToClassList("key-icon-mouse-r");
                    attackIcons.Add(rmb);

                    var plus = new Label("+");
                    plus.AddToClassList("key-combo-plus");
                    attackIcons.Add(plus);

                    var lmb = new VisualElement();
                    lmb.AddToClassList("key-icon");
                    lmb.AddToClassList("key-icon-mouse-l");
                    attackIcons.Add(lmb);
                }
                if (attackText != null) attackText.text = "Bắn Cung";
                if (blockRow != null) blockRow.style.display = DisplayStyle.None;
            }
            else if (classIdx == 1) // Maya (Support/Mage): Chuột phải + Chuột trái -> Chưởng Phép
            {
                if (attackIcons != null)
                {
                    attackIcons.Clear();
                    var rmb = new VisualElement();
                    rmb.AddToClassList("key-icon");
                    rmb.AddToClassList("key-icon-mouse-r");
                    attackIcons.Add(rmb);

                    var plus = new Label("+");
                    plus.AddToClassList("key-combo-plus");
                    attackIcons.Add(plus);

                    var lmb = new VisualElement();
                    lmb.AddToClassList("key-icon");
                    lmb.AddToClassList("key-icon-mouse-l");
                    attackIcons.Add(lmb);
                }
                if (attackText != null) attackText.text = "Chưởng Phép";
                if (blockRow != null) blockRow.style.display = DisplayStyle.None;
            }
            else // Leo (Assassin - 0): Click chuột trái Chém
            {
                if (attackIcons != null)
                {
                    attackIcons.Clear();
                    var lmb = new VisualElement();
                    lmb.AddToClassList("key-icon");
                    lmb.AddToClassList("key-icon-mouse-l");
                    attackIcons.Add(lmb);
                }
                if (attackText != null) attackText.text = "Chém";
                if (blockRow != null) blockRow.style.display = DisplayStyle.None;
            }
        }
    }

    public void ToggleTutorialPanel()
    {
        isTutorialManuallyToggled = true;
        isTutorialVisible = !isTutorialVisible;

        if (tutorialDetailsContainer != null)
        {
            tutorialDetailsContainer.style.display = isTutorialVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (f1ToggleHint != null)
        {
            f1ToggleHint.style.display = DisplayStyle.Flex;
        }
    }

    public void ShowMissionAlert(string message, float duration = -1f)
    {
        InitializeUI();
        var root = uiDocument != null ? uiDocument.rootVisualElement : null;
        if (root == null) return;

        if (notificationPopup == null)
        {
            notificationPopup = root.Q<VisualElement>("notification-popup");
            notificationPopupText = root.Q<Label>("notification-popup-text");
            notificationPopupIcon = root.Q<VisualElement>("notification-popup-icon");
        }

        // Fallback tạo giao diện động nếu chưa có sẵn trong UXML
        if (notificationPopup == null)
        {
            notificationPopup = new VisualElement();
            notificationPopup.name = "notification-popup";
            notificationPopup.AddToClassList("notification-popup");

            var bg = new VisualElement();
            bg.AddToClassList("notification-popup-bg");
            notificationPopup.Add(bg);

            var frame = new VisualElement();
            frame.AddToClassList("notification-popup-frame");
            notificationPopup.Add(frame);

            var content = new VisualElement();
            content.AddToClassList("notification-popup-content");

            notificationPopupIcon = new VisualElement();
            notificationPopupIcon.name = "notification-popup-icon";
            notificationPopupIcon.AddToClassList("notification-popup-icon");
            content.Add(notificationPopupIcon);

            notificationPopupText = new Label();
            notificationPopupText.name = "notification-popup-text";
            notificationPopupText.AddToClassList("notification-popup-text");
            content.Add(notificationPopupText);

            notificationPopup.Add(content);
            root.Add(notificationPopup);
        }

        missionAlertBox = notificationPopup;
        missionAlertText = notificationPopupText;

        if (notificationPopupText != null)
        {
            notificationPopupText.text = message;
        }

        // Cập nhật icon theo ngữ cảnh thông báo
        if (notificationPopupIcon != null)
        {
            notificationPopupIcon.RemoveFromClassList("icon-energy");
            notificationPopupIcon.RemoveFromClassList("icon-quest");
            notificationPopupIcon.RemoveFromClassList("icon-warning");

            string lowerMsg = (message ?? "").ToLower();
            if (lowerMsg.Contains("năng lượng") || lowerMsg.Contains("nang luong") || lowerMsg.Contains("energy") || lowerMsg.Contains("mana"))
            {
                notificationPopupIcon.AddToClassList("icon-energy");
            }
            else if (lowerMsg.Contains("cầu") || lowerMsg.Contains("cau") || lowerMsg.Contains("gỗ") || lowerMsg.Contains("go") || lowerMsg.Contains("nhiệm vụ") || lowerMsg.Contains("quest") || lowerMsg.Contains("trụ") || lowerMsg.Contains("tru"))
            {
                notificationPopupIcon.AddToClassList("icon-quest");
            }
            else
            {
                notificationPopupIcon.AddToClassList("icon-warning");
            }
        }

        // Kích hoạt hiển thị với hiệu ứng pop-in
        notificationPopup.RemoveFromClassList("show-notification");
        notificationPopup.RemoveFromClassList("pop-bump");
        
        notificationPopup.schedule.Execute(() => {
            if (notificationPopup != null)
            {
                notificationPopup.AddToClassList("show-notification");
                notificationPopup.AddToClassList("pop-bump");
                notificationPopup.schedule.Execute(() => {
                    if (notificationPopup != null) notificationPopup.RemoveFromClassList("pop-bump");
                }).StartingIn(100);
            }
        }).StartingIn(10);

        // Hủy schedule cũ nếu có
        if (hideNotificationScheduledItem != null)
        {
            hideNotificationScheduledItem.Pause();
            hideNotificationScheduledItem = null;
        }

        float displayTime = duration > 0f ? duration : 2.5f;
        hideNotificationScheduledItem = notificationPopup.schedule.Execute(() => {
            HideMissionAlert();
        }).StartingIn((long)(displayTime * 1000f));
    }

    public void HideMissionAlert()
    {
        if (notificationPopup != null)
        {
            notificationPopup.RemoveFromClassList("show-notification");
            notificationPopup.RemoveFromClassList("pop-bump");
        }
        if (missionAlertBox != null && missionAlertBox != notificationPopup)
        {
            missionAlertBox.style.display = DisplayStyle.None;
        }
    }

    private bool CanSwitchQuestOwner(object owner)
    {
        if (owner == null) return true;
        if (currentQuestOwner == null || currentQuestOwner == owner) return true;

        // If the owner making the request is already completed, it cannot take over an active quest
        if (owner is IQuestTrigger newQuest && newQuest.IsQuestCompleted)
        {
            return false;
        }

        // If current owner is completed, a new uncompleted quest can switch to take ownership
        if (currentQuestOwner is IQuestTrigger oldQuest && oldQuest.IsQuestCompleted)
        {
            return true;
        }

        // If current owner is a missing/destroyed object, allow switch
        if (currentQuestOwner is UnityEngine.Object uObj && uObj == null)
        {
            return true;
        }

        // Check if new owner has prerequisite completed AND current quest owner is either completed or not active
        if (owner is IQuestTrigger)
        {
            var method = owner.GetType().GetMethod("IsPrerequisiteCompleted");
            if (method != null && method.ReturnType == typeof(bool))
            {
                try
                {
                    bool isPreCompleted = (bool)method.Invoke(owner, null);
                    if (isPreCompleted)
                    {
                        if (currentQuestOwner is IQuestTrigger activeQuest)
                        {
                            return activeQuest.IsQuestCompleted;
                        }
                        return true;
                    }
                }
                catch { }
            }
        }

        return false;
    }

    public bool IsQuestPanelActive()
    {
        InitializeUI();
        return questPanel != null && questPanel.ClassListContains("show-quest");
    }

    public void ShowQuest(bool show, object owner = null)
    {
        InitializeUI();
        if (questPanel != null)
        {
            if (show)
            {
                if (currentQuestOwner != null && owner != null && currentQuestOwner != owner)
                {
                    if (CanSwitchQuestOwner(owner))
                    {
                        currentQuestOwner = owner;
                    }
                    else
                    {
                        return;
                    }
                }
                if (owner != null)
                {
                    currentQuestOwner = owner;
                }
                bool wasHidden = !questPanel.ClassListContains("show-quest");
                questPanel.AddToClassList("show-quest");
                if (wasHidden)
                {
                    TriggerQuestSpotlight();
                }
            }
            else
            {
                // Crucial fix: Only hide quest UI if requested by current owner or if owner is unspecified (null).
                // Do NOT allow a previous/other quest owner to hide an active quest's UI.
                if (owner == null || currentQuestOwner == owner)
                {
                    currentQuestOwner = null;
                    lastActiveQuestTitle = "";
                    questPanel.RemoveFromClassList("show-quest");
                    StopQuestSpotlight();
                }
            }
            Debug.Log($"[PlayerHUDController] ShowQuest({show}) (owner: {owner?.GetType().Name ?? "null"})");
        }
    }

    public void TriggerQuestSpotlight(float duration = 5.0f)
    {
        if (!gameObject.activeInHierarchy) return;
        if (questSpotlightCoroutine != null)
        {
            StopCoroutine(questSpotlightCoroutine);
        }
        questSpotlightCoroutine = StartCoroutine(QuestSpotlightRoutine(duration));
    }

    public void StopQuestSpotlight()
    {
        if (questSpotlightCoroutine != null)
        {
            StopCoroutine(questSpotlightCoroutine);
            questSpotlightCoroutine = null;
        }
        if (questSpotlightBackdrop != null)
        {
            questSpotlightBackdrop.RemoveFromClassList("active");
            questSpotlightBackdrop.style.display = DisplayStyle.None;
        }
        if (questGlowAura != null)
        {
            questGlowAura.RemoveFromClassList("active");
            questGlowAura.RemoveFromClassList("pulse-high");
            questGlowAura.RemoveFromClassList("pulse-low");
        }
        if (questPointerLeft != null)
        {
            questPointerLeft.RemoveFromClassList("active");
            questPointerLeft.RemoveFromClassList("pulse-pointing");
        }
        if (questPointerBottom != null)
        {
            questPointerBottom.RemoveFromClassList("active");
            questPointerBottom.RemoveFromClassList("pulse-pointing");
        }
        if (questPanel != null)
        {
            questPanel.RemoveFromClassList("spotlight-glow");
            questPanel.RemoveFromClassList("spotlight-pulse-high");
            questPanel.RemoveFromClassList("spotlight-pulse-low");
        }
    }

    private System.Collections.IEnumerator QuestSpotlightRoutine(float duration)
    {
        InitializeUI();
        // 1. Kích hoạt vầng hào quang phát sáng và 2 mũi tên chỉ dẫn (Trái + Dưới) - Đã bỏ hoàn toàn nền đen mờ màn hình
        if (questSpotlightBackdrop != null)
        {
            questSpotlightBackdrop.RemoveFromClassList("active");
            questSpotlightBackdrop.style.display = DisplayStyle.None;
        }
        if (questGlowAura != null) questGlowAura.AddToClassList("active");
        if (questPointerLeft != null) questPointerLeft.AddToClassList("active");
        if (questPointerBottom != null) questPointerBottom.AddToClassList("active");
        if (questPanel != null) questPanel.AddToClassList("spotlight-glow");

        // 2. Vòng lặp nhấp nháy phát sáng (shimmering glow) và hiệu ứng chỉ điểm nhịp nhàng của 2 mũi tên
        float elapsed = 0f;
        int step = 0;
        while (elapsed < duration)
        {
            yield return new WaitForSeconds(0.25f);
            elapsed += 0.25f;
            step++;

            // Nhấp nháy hào quang vàng kim xung quanh bảng nhiệm vụ
            bool isHigh = (step % 2 == 1);
            if (questGlowAura != null)
            {
                if (isHigh)
                {
                    questGlowAura.RemoveFromClassList("pulse-low");
                    questGlowAura.AddToClassList("pulse-high");
                }
                else
                {
                    questGlowAura.RemoveFromClassList("pulse-high");
                    questGlowAura.AddToClassList("pulse-low");
                }
            }
            if (questPanel != null)
            {
                if (isHigh)
                {
                    questPanel.RemoveFromClassList("spotlight-pulse-low");
                    questPanel.AddToClassList("spotlight-pulse-high");
                }
                else
                {
                    questPanel.RemoveFromClassList("spotlight-pulse-high");
                    questPanel.AddToClassList("spotlight-pulse-low");
                }
            }

            // Hiệu ứng nhấp nháy chuyển động đẩy tới của Mũi tên Trái (chỉ sang phải) và Mũi tên Dưới (chỉ lên trên)
            if (questPointerLeft != null)
            {
                if (isHigh) questPointerLeft.AddToClassList("pulse-pointing");
                else questPointerLeft.RemoveFromClassList("pulse-pointing");
            }
            if (questPointerBottom != null)
            {
                if (isHigh) questPointerBottom.AddToClassList("pulse-pointing");
                else questPointerBottom.RemoveFromClassList("pulse-pointing");
            }
        }

        // 3. Sau khoảng 5 giây, giao diện trở về trạng thái bình thường
        StopQuestSpotlight();
    }

    public void UpdateQuestProgress(int current, int target = 16, object owner = null)
    {
        if (currentQuestOwner != null && owner != null && currentQuestOwner != owner)
        {
            if (CanSwitchQuestOwner(owner))
            {
                currentQuestOwner = owner;
            }
            else
            {
                return;
            }
        }
        InitializeUI();
        if (questProgressText != null)
        {
            questProgressText.text = $"{current} / {target}";
        }
        if (questProgressBar != null)
        {
            float percent = target > 0 ? ((float)current / target) * 100f : 0f;
            questProgressBar.style.width = Length.Percent(Mathf.Clamp(percent, 0f, 100f));
        }
    }

    public void SetCrosshairVisible(bool visible)
    {
        InitializeUI();
        if (crosshairElement != null)
        {
            bool isRangedClass = LocalPlayerTarget != null && (LocalPlayerTarget.CharacterClassIndex == 2 || LocalPlayerTarget.CharacterClassIndex == 1);
            bool isRangedActive = LocalPlayerTarget != null && LocalPlayerTarget.GetActiveWeaponIndex() == 2;
            bool isAiming = false;
            if (LocalPlayerTarget != null)
            {
                if (LocalPlayerTarget is ArthurPlayer arthur) isAiming = arthur.IsAiming;
                else if (LocalPlayerTarget is LeoPlayer leo) isAiming = leo.IsAiming;
                else if (LocalPlayerTarget is ElenaPlayer elena) isAiming = elena.IsAiming;
                else if (LocalPlayerTarget is MayaPlayer maya) isAiming = maya.IsAiming;
            }
            crosshairElement.style.display = (visible && (isAiming || (isRangedClass && isRangedActive))) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void UpdateQuestDescription(string description, object owner = null)
    {
        if (currentQuestOwner != null && owner != null && currentQuestOwner != owner)
        {
            if (CanSwitchQuestOwner(owner))
            {
                currentQuestOwner = owner;
            }
            else
            {
                return;
            }
        }
        InitializeUI();
        if (questDescriptionText != null)
        {
            questDescriptionText.text = description;
        }
    }

    public void UpdateQuestTitle(string title, object owner = null)
    {
        if (currentQuestOwner != null && owner != null && currentQuestOwner != owner)
        {
            if (CanSwitchQuestOwner(owner))
            {
                currentQuestOwner = owner;
            }
            else
            {
                return;
            }
        }
        InitializeUI();
        if (questTitleText != null)
        {
            questTitleText.text = title;
        }
        if (!string.IsNullOrEmpty(title) && title != lastActiveQuestTitle)
        {
            lastActiveQuestTitle = title;
            if (questPanel != null && questPanel.ClassListContains("show-quest"))
            {
                TriggerQuestSpotlight();
            }
        }
    }

    public void UpdateQuestIcon(Sprite iconSprite, object owner = null)
    {
        if (currentQuestOwner != null && owner != null && currentQuestOwner != owner)
        {
            if (CanSwitchQuestOwner(owner))
            {
                currentQuestOwner = owner;
            }
            else
            {
                return;
            }
        }
        InitializeUI();
        if (questIcon != null)
        {
            if (iconSprite != null)
            {
                questIcon.style.backgroundImage = new StyleBackground(iconSprite);
            }
            else if (defaultQuestIcon != null)
            {
                questIcon.style.backgroundImage = new StyleBackground(defaultQuestIcon);
            }
            else
            {
                questIcon.style.backgroundImage = StyleKeyword.Null;
            }
        }
    }

    public void ToggleCoopBuildUI(BridgeCollapseTrigger bridge)
    {
        if (isCoopBuildingUIOpen)
        {
            CloseCoopBuildUI();
        }
        else
        {
            OpenCoopBuildUI(bridge);
        }
    }

    public void OpenCoopBuildUI(BridgeCollapseTrigger bridge)
    {
        InitializeUI();
        var root = uiDocument != null ? uiDocument.rootVisualElement : null;
        if (root == null) return;

        activeBridgeTrigger = bridge;

        if (lastActiveBridgeTrigger != bridge)
        {
            lastActiveBridgeTrigger = bridge;
            hasTransitionedBuildCameraOnce = false;
        }

        EnsureCoopBuildSpritesLoaded();

        if (coopBuildContainer == null)
        {
            // 1. Create main container (transparent wrapper holding the fantasy frame + space button)
            coopBuildContainer = new VisualElement();
            coopBuildContainer.name = "coop-build-container";
            
            coopBuildContainer.style.position = Position.Absolute;
            coopBuildContainer.style.left = Length.Percent(50f);
            coopBuildContainer.style.top = 20; // Top center
            coopBuildContainer.style.translate = new Translate(Length.Percent(-50f), Length.Percent(0f), 0f);
            coopBuildContainer.style.width = 580;
            coopBuildContainer.style.height = 160;
            coopBuildContainer.style.backgroundColor = Color.clear;
            coopBuildContainer.style.borderTopWidth = 0;
            coopBuildContainer.style.borderBottomWidth = 0;
            coopBuildContainer.style.borderLeftWidth = 0;
            coopBuildContainer.style.borderRightWidth = 0;
            coopBuildContainer.style.paddingLeft = 16;
            coopBuildContainer.style.paddingRight = 32; // Thu gọn mép phải vào trong khung viền vàng
            coopBuildContainer.style.paddingTop = 64;
            coopBuildContainer.style.paddingBottom = 26;
            coopBuildContainer.style.flexDirection = FlexDirection.Column;
            coopBuildContainer.style.justifyContent = Justify.Center;
            coopBuildContainer.style.alignItems = Align.Stretch;

            // 2. Create Space Keycap Button first (Layer dưới)
            coopBuildClickButton = new Button();
            coopBuildClickButton.name = "coop-build-space-button";
            coopBuildClickButton.style.width = Length.Percent(100f);
            coopBuildClickButton.style.height = Length.Percent(100f);
            coopBuildClickButton.style.flexGrow = 1;
            coopBuildClickButton.style.backgroundColor = new Color(0.10f, 0.11f, 0.15f, 0.95f); // Dark keycap body
            coopBuildClickButton.style.borderTopWidth = 1f;
            coopBuildClickButton.style.borderBottomWidth = 2.5f; // Keycap 3D border-bottom depth
            coopBuildClickButton.style.borderLeftWidth = 0f;
            coopBuildClickButton.style.borderRightWidth = 0f;
            coopBuildClickButton.style.borderTopColor = new Color(0.95f, 0.8f, 0.45f, 0.35f); // Subtle gold accent border
            coopBuildClickButton.style.borderBottomColor = new Color(0.04f, 0.04f, 0.06f, 0.95f); // Darker shadow
            coopBuildClickButton.style.borderLeftColor = Color.clear;
            coopBuildClickButton.style.borderRightColor = Color.clear;
            coopBuildClickButton.style.borderTopLeftRadius = 2;
            coopBuildClickButton.style.borderTopRightRadius = 2;
            coopBuildClickButton.style.borderBottomLeftRadius = 2;
            coopBuildClickButton.style.borderBottomRightRadius = 2;
            coopBuildClickButton.style.flexDirection = FlexDirection.Column;
            coopBuildClickButton.style.justifyContent = Justify.Center;
            coopBuildClickButton.style.alignItems = Align.Center;
            coopBuildClickButton.style.overflow = Overflow.Hidden;
            coopBuildClickButton.style.paddingLeft = 0;
            coopBuildClickButton.style.paddingRight = 0;
            coopBuildClickButton.style.paddingTop = 0;
            coopBuildClickButton.style.paddingBottom = 0;
            
            // Progress Fill inside the Keycap button (Neon Cyan progress fill)
            coopBuildProgressBarFill = new VisualElement();
            coopBuildProgressBarFill.name = "coop-build-progress-fill";
            coopBuildProgressBarFill.style.position = Position.Absolute;
            coopBuildProgressBarFill.style.left = 0;
            coopBuildProgressBarFill.style.top = 0;
            coopBuildProgressBarFill.style.bottom = 0;
            coopBuildProgressBarFill.style.width = Length.Percent(0f);
            coopBuildProgressBarFill.style.backgroundColor = new Color(0f, 0.72f, 0.95f, 0.4f); // Translucent neon cyan progress fill
            coopBuildProgressBarFill.style.borderTopLeftRadius = 2;
            coopBuildProgressBarFill.style.borderBottomLeftRadius = 2;
            coopBuildProgressBarFill.pickingMode = PickingMode.Ignore;
            coopBuildClickButton.Add(coopBuildProgressBarFill);

            // Label text inside Keycap
            coopBuildProgressLabel = new Label("SPACE (0%)");
            coopBuildProgressLabel.name = "coop-build-progress-label";
            coopBuildProgressLabel.style.color = new Color(0.95f, 0.95f, 0.92f, 1f);
            coopBuildProgressLabel.style.fontSize = 26;
            coopBuildProgressLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            coopBuildProgressLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            coopBuildProgressLabel.pickingMode = PickingMode.Ignore;
            coopBuildClickButton.Add(coopBuildProgressLabel);

            coopBuildClickButton.clicked += OnBuildButtonClicked;
            coopBuildContainer.Add(coopBuildClickButton);

            // 3. Background Decorative Frame composed of Left & Right halves (Layer trên cùng - đè lên nút Space)
            VisualElement frameBgContainer = new VisualElement();
            frameBgContainer.name = "coop-build-frame-bg";
            frameBgContainer.style.position = Position.Absolute;
            frameBgContainer.style.left = 0;
            frameBgContainer.style.top = 0;
            frameBgContainer.style.right = 0;
            frameBgContainer.style.bottom = 0;
            frameBgContainer.style.flexDirection = FlexDirection.Row;
            frameBgContainer.pickingMode = PickingMode.Ignore;

            coopBuildFrameLeft = new VisualElement();
            coopBuildFrameLeft.name = "coop-build-frame-left";
            coopBuildFrameLeft.style.width = Length.Percent(50f);
            coopBuildFrameLeft.style.height = Length.Percent(100f);
            coopBuildFrameLeft.pickingMode = PickingMode.Ignore;

            coopBuildFrameRight = new VisualElement();
            coopBuildFrameRight.name = "coop-build-frame-right";
            coopBuildFrameRight.style.width = Length.Percent(50f);
            coopBuildFrameRight.style.height = Length.Percent(100f);
            coopBuildFrameRight.pickingMode = PickingMode.Ignore;

            frameBgContainer.Add(coopBuildFrameLeft);
            frameBgContainer.Add(coopBuildFrameRight);
            coopBuildContainer.Add(frameBgContainer);

            root.Add(coopBuildContainer);
        }

        // Always ensure background frame sprites are bound to the visual elements
        if (coopBuildFrameLeft != null && coopBuildFrameLeftSprite != null)
        {
            coopBuildFrameLeft.style.backgroundImage = new StyleBackground(coopBuildFrameLeftSprite);
            coopBuildFrameLeft.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
        }
        if (coopBuildFrameRight != null && coopBuildFrameRightSprite != null)
        {
            coopBuildFrameRight.style.backgroundImage = new StyleBackground(coopBuildFrameRightSprite);
            coopBuildFrameRight.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
        }

        coopBuildContainer.style.display = DisplayStyle.Flex;
        isCoopBuildingUIOpen = true;
        isAnyUIOpen = true;

        // Disable local player controller and reset movement state
        var playerBehavior = LocalPlayerTarget as MonoBehaviour;
        if (playerBehavior != null)
        {
            var rb = playerBehavior.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }

            var animComponent = playerBehavior.GetComponent<Animator>();
            if (animComponent == null) animComponent = playerBehavior.GetComponentInChildren<Animator>();
            if (animComponent != null && animComponent.isActiveAndEnabled && animComponent.runtimeAnimatorController != null)
            {
                animComponent.SetFloat("Speed", 0f);
                animComponent.SetFloat("InputX", 0f);
                animComponent.SetFloat("InputZ", 0f);
            }

            // Sync zero movement over network before disabling the controller script
            if (playerBehavior is LeoPlayer leo)
            {
                if (leo.netMoveX != null) leo.netMoveX.Value = 0f;
                if (leo.netMoveZ != null) leo.netMoveZ.Value = 0f;
                if (leo.netSpeed != null) leo.netSpeed.Value = 0f;
            }
            else if (playerBehavior is ArthurPlayer arthur)
            {
                if (arthur.netMoveX != null) arthur.netMoveX.Value = 0f;
                if (arthur.netMoveZ != null) arthur.netMoveZ.Value = 0f;
                if (arthur.netSpeed != null) arthur.netSpeed.Value = 0f;
            }
            else if (playerBehavior is ElenaPlayer elena)
            {
                if (elena.netMoveX != null) elena.netMoveX.Value = 0f;
                if (elena.netMoveZ != null) elena.netMoveZ.Value = 0f;
            }
            else if (playerBehavior is MayaPlayer maya)
            {
                if (maya.netMoveX != null) maya.netMoveX.Value = 0f;
                if (maya.netMoveZ != null) maya.netMoveZ.Value = 0f;
            }

            playerBehavior.enabled = false;
        }

        // Initialize progress view immediately
        float prog = (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening) ? activeBridgeTrigger.buildProgress.Value : activeBridgeTrigger.localBuildProgress;
        UpdateCoopBuildProgress(prog);
    }

    public void CloseCoopBuildUI()
    {
        if (coopBuildContainer != null)
        {
            coopBuildContainer.style.display = DisplayStyle.None;
        }

        isCoopBuildingUIOpen = false;
        isAnyUIOpen = false;
        activeBridgeTrigger = null;
        isBuildCameraActive = false; // Reset camera state on exit
        hasTransitionedBuildCameraOnce = false;

        // Re-enable local player controller
        var playerBehavior = LocalPlayerTarget as MonoBehaviour;
        if (playerBehavior != null)
        {
            playerBehavior.enabled = true;
        }
    }

    private void UpdateCoopBuildCamera()
    {
        var playerBehavior = LocalPlayerTarget as MonoBehaviour;
        if (playerBehavior == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = FindAnyObjectByType<Camera>();
        if (mainCam == null) return;

        // Cần đảm bảo có activeBridgeTrigger hoặc tìm trong Scene
        if (activeBridgeTrigger == null)
        {
            activeBridgeTrigger = FindAnyObjectByType<BridgeCollapseTrigger>();
        }
        if (activeBridgeTrigger == null) return;

        // 1. Xác định vị trí trọng tâm giữa thân cầu
        Vector3 bridgeCenterPos = new Vector3(684.35f, 31.55f, 553.3f);
        if (activeBridgeTrigger.mainBridgeObject != null)
        {
            bridgeCenterPos = activeBridgeTrigger.mainBridgeObject.transform.position;
        }

        // 2. Trục thẳng của cây cầu (hướng từ đầu cầu bờ gần nhìn qua vực sâu sang bờ bên kia)
        // Cầu nằm theo trục X, hướng từ bờ gần (X ~ 710) sang bờ xa (X ~ 658) => Vector hướng là Vector3.left (-X)
        Vector3 bridgeForward = new Vector3(-1f, 0f, 0f);
        if (activeBridgeTrigger.mainBridgeObject != null)
        {
            Vector3 fwd = activeBridgeTrigger.mainBridgeObject.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.1f)
            {
                bridgeForward = fwd.normalized;
            }
        }

        // 3. Xác định vị trí đầu cầu trên tim đường cầu (triệt tiêu độ lệch Z để góc nhìn luôn thẳng tắp)
        Vector3 triggerPos = activeBridgeTrigger.transform.position;
        float distAlongBridge = Vector3.Dot(triggerPos - bridgeCenterPos, bridgeForward);
        
        // Nếu trigger chưa được tính hoặc quá gần, lấy mặc định khoảng cách nửa thân cầu ~24m
        if (distAlongBridge > -5f)
        {
            distAlongBridge = -24f;
        }

        // Điểm đầu cầu chuẩn: nằm chính xác trên tim đường Z của cây cầu
        Vector3 bridgeHeadPos = bridgeCenterPos + bridgeForward * distAlongBridge;
        bridgeHeadPos.y = Mathf.Max(triggerPos.y, 32.8f);

        // 4. Đặt Camera ngay trên đầu cây cầu ở trên cao nhìn xuống
        float actualUpOffset = Mathf.Max(buildCamUpwardOffset, 20f);
        float actualBackOffset = Mathf.Max(buildCamBackwardOffset, 6f);

        Vector3 targetCamPos = bridgeHeadPos - bridgeForward * actualBackOffset + Vector3.up * actualUpOffset;

        // 5. Góc nhìn của camera: Nhìn thẳng theo tim cầu từ trên cao xuống giữa thân cầu và bờ bên kia
        Vector3 lookTarget = bridgeCenterPos + Vector3.up * 1.5f;
        Quaternion targetCamRot = Quaternion.LookRotation((lookTarget - targetCamPos).normalized, Vector3.up);

        if (!isBuildCameraActive)
        {
            isBuildCameraActive = true;
            initialCamPosBeforeBuild = mainCam.transform.position;
            initialCamRotBeforeBuild = mainCam.transform.rotation;
            buildCameraTransitionTimer = 0f;
        }

        if (hasTransitionedBuildCameraOnce)
        {
            // Đã chuyển tiếp xong, giữ camera cố định ở góc nhìn thẳng chuẩn trên cao
            mainCam.transform.position = targetCamPos;
            mainCam.transform.rotation = targetCamRot;
        }
        else
        {
            // Di chuyển mượt mà lên vị trí trên cao
            buildCameraTransitionTimer += Time.deltaTime;
            float t = Mathf.Clamp01(buildCameraTransitionTimer / buildCameraTransitionDuration);
            
            // Dùng SmoothStep để di chuyển mượt mà
            float smoothT = t * t * (3f - 2f * t);

            mainCam.transform.position = Vector3.Lerp(initialCamPosBeforeBuild, targetCamPos, smoothT);
            mainCam.transform.rotation = Quaternion.Slerp(initialCamRotBeforeBuild, targetCamRot, smoothT);

            if (t >= 1f)
            {
                hasTransitionedBuildCameraOnce = true;
            }
        }
    }

    public void TriggerTreeFallCamera(ChoppableTree tree)
    {
        if (tree == null) return;
        
        // Tránh ghi đè camera nếu đang quay cây khác hoặc đang xây cầu
        if (isCoopBuildingUIOpen) return;
        if (isTreeCameraActive && activeFallingTree != null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = FindAnyObjectByType<Camera>();
        if (mainCam == null) return;

        var playerBehavior = LocalPlayerTarget as MonoBehaviour;
        Vector3 playerPos = (playerBehavior != null) ? playerBehavior.transform.position : tree.transform.position;

        isTreeCameraActive = true;
        activeFallingTree = tree.transform;
        treeCameraTransitionTimer = 0f;

        Vector3 dir = playerPos - tree.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.1f)
        {
            dir = (playerBehavior != null) ? -playerBehavior.transform.forward : -Vector3.forward;
        }
        fixedTreeCamDir = dir.normalized;

        initialCamPosBeforeTree = mainCam.transform.position;
        initialCamRotBeforeTree = mainCam.transform.rotation;
        normalCamOffsetFromPlayer = initialCamPosBeforeTree - playerPos;
    }

    private Vector3 fixedTreeCamDir = Vector3.back;

    private void UpdateTreeFallCamera()
    {
        if (!isTreeCameraActive || activeFallingTree == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = FindAnyObjectByType<Camera>();
        if (mainCam == null) return;

        var playerBehavior = LocalPlayerTarget as MonoBehaviour;
        Vector3 playerPos = (playerBehavior != null) ? playerBehavior.transform.position : activeFallingTree.position;
        Vector3 treePos = activeFallingTree.position;

        // Dùng hướng cố định ban đầu fixedTreeCamDir thay vì tính liên tục để tránh camera bị tự xoay tròn khi player di chuyển sát gốc cây
        Vector3 dirFromTreeToPlayer = fixedTreeCamDir;

        // Target camera position: lùi xa ra khỏi cây và nâng cao lên
        Vector3 targetCamPos = treePos + dirFromTreeToPlayer * treeCamDistance + Vector3.up * treeCamHeight;
        Vector3 lookTarget = treePos + Vector3.up * treeCamLookHeight;
        Quaternion targetCamRot = Quaternion.LookRotation(lookTarget - targetCamPos);

        treeCameraTransitionTimer += Time.deltaTime;
        float transitionDuration = 0.6f; // 0.6 giây lerp mượt mà

        if (treeCameraTransitionTimer < transitionDuration)
        {
            // Giai đoạn 1: Chuyển tiếp mượt mà vào camera cây ngã
            float t = treeCameraTransitionTimer / transitionDuration;
            float smoothT = t * t * (3f - 2f * t);
            mainCam.transform.position = Vector3.Lerp(initialCamPosBeforeTree, targetCamPos, smoothT);
            mainCam.transform.rotation = Quaternion.Slerp(initialCamRotBeforeTree, targetCamRot, smoothT);
        }
        else if (treeCameraTransitionTimer < treeCamDuration - transitionDuration)
        {
            // Giai đoạn 2: Giữ camera nhìn cây ngã
            mainCam.transform.position = targetCamPos;
            mainCam.transform.rotation = targetCamRot;
        }
        else if (treeCameraTransitionTimer < treeCamDuration)
        {
            // Giai đoạn 3: Chuyển tiếp mượt mà trả lại camera bình thường của người chơi
            float t = (treeCameraTransitionTimer - (treeCamDuration - transitionDuration)) / transitionDuration;
            float smoothT = t * t * (3f - 2f * t);
            
            Vector3 normalCamPos = playerPos + normalCamOffsetFromPlayer;
            Quaternion normalCamRot = initialCamRotBeforeTree;

            mainCam.transform.position = Vector3.Lerp(targetCamPos, normalCamPos, smoothT);
            mainCam.transform.rotation = Quaternion.Slerp(targetCamRot, normalCamRot, smoothT);
        }
        else
        {
            // Kết thúc hiệu ứng
            isTreeCameraActive = false;
            activeFallingTree = null;
        }
    }

    public void UpdateCoopBuildProgress(float prog)
    {
        if (coopBuildProgressBarFill != null)
        {
            coopBuildProgressBarFill.style.width = Length.Percent(prog);
        }
        if (coopBuildProgressLabel != null)
        {
            coopBuildProgressLabel.text = $"SPACE ({(int)prog}%)";
        }
    }

    public void OnBridgeBuildClickReceived()
    {
        if (coopBuildClickButton == null) return;

        // 1. Chớp sáng (Flash) nút SPACE
        // Chuyển màu nền sang màu vàng-cam sáng rực rỡ
        coopBuildClickButton.style.backgroundColor = new Color(1f, 0.72f, 0f, 0.95f);
        coopBuildClickButton.style.borderBottomWidth = 1.5f; // Giả lập cảm giác nhấn phím (lún xuống)
        
        // Reset về màu xám đen gốc sau 100ms
        coopBuildClickButton.schedule.Execute(() =>
        {
            if (coopBuildClickButton != null)
            {
                coopBuildClickButton.style.backgroundColor = new Color(0.10f, 0.11f, 0.15f, 0.95f);
                coopBuildClickButton.style.borderBottomWidth = 4f; // Trả lại độ dày 3D
            }
        }).StartingIn(100);

        // 2. Sinh hiệu ứng dăm gỗ/ánh sáng bay lên từ nút SPACE
        int particleCount = Random.Range(3, 6);
        for (int i = 0; i < particleCount; i++)
        {
            SpawnUIParticle();
        }
    }

    private void SpawnUIParticle()
    {
        if (coopBuildContainer == null) return;

        VisualElement particle = new VisualElement();
        particle.style.position = Position.Absolute;
        
        // Sinh ngẫu nhiên theo chiều ngang của phím Space
        float randomX = Random.Range(60f, 520f);
        particle.style.left = randomX;
        particle.style.bottom = 20;
        float size = Random.Range(5f, 10f);
        particle.style.width = size;
        particle.style.height = size;
        particle.style.borderTopLeftRadius = size / 2f;
        particle.style.borderTopRightRadius = size / 2f;
        particle.style.borderBottomLeftRadius = size / 2f;
        particle.style.borderBottomRightRadius = size / 2f;

        // Chọn ngẫu nhiên giữa màu Cam sáng và màu Trắng/Vàng
        Color[] colors = {
            new Color(1f, 0.75f, 0f, 0.9f),   // Vàng
            new Color(0.95f, 0.45f, 0.05f, 0.9f), // Cam sáng
            new Color(1f, 1f, 1f, 0.95f)     // Trắng
        };
        particle.style.backgroundColor = colors[Random.Range(0, colors.Length)];
        particle.style.borderTopWidth = 0.5f;
        particle.style.borderBottomWidth = 0.5f;
        particle.style.borderLeftWidth = 0.5f;
        particle.style.borderRightWidth = 0.5f;
        particle.style.borderTopColor = Color.white;
        particle.style.borderBottomColor = Color.white;
        particle.style.borderLeftColor = Color.white;
        particle.style.borderRightColor = Color.white;

        coopBuildContainer.Add(particle);

        // Hoạt ảnh bay lên và mờ dần (Float Up & Fade Out)
        float currentY = 40f;
        float speed = Random.Range(80f, 150f);
        float opacity = 1f;
        float driftDirection = Random.Range(-20f, 20f);

        particle.schedule.Execute(() =>
        {
            currentY += speed * 0.02f;
            opacity -= 0.02f * 2.5f; // Mờ dần hoàn toàn sau ~0.4 giây
            
            particle.style.bottom = currentY;
            particle.style.opacity = Mathf.Clamp01(opacity);
            
            // Bay lượn sóng ngang nhẹ nhàng
            float leftVal = randomX + Mathf.Sin(currentY * 0.06f) * driftDirection;
            particle.style.left = leftVal;

            if (opacity <= 0f)
            {
                if (particle.parent != null)
                {
                    particle.parent.Remove(particle);
                }
            }
        }).Every(20); // Chạy định kỳ mỗi 20ms
    }

    private void OnBuildButtonClicked()
    {
        if (activeBridgeTrigger == null) return;

        bool isNetwork = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;
        if (isNetwork)
        {
            activeBridgeTrigger.ClickBuildServerRpc();
        }
        else
        {
            activeBridgeTrigger.ClickBuildLocal();
        }
    }

    public void SetWeapon1IconOverride(Sprite customSprite)
    {
        if (weaponDurabilitySlot1 != null)
        {
            weaponDurabilitySlot1.style.display = (customSprite != null) ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (weaponImg1 != null)
        {
            if (customSprite != null)
            {
                weaponImg1.style.backgroundImage = new StyleBackground(customSprite);
            }
            else
            {
                // Reset to default
                if (hudProfiles != null && hudProfiles.Count > 0)
                {
                    int idx = lastSelectedProfileIndex;
                    if (hudProfiles.Count == 1)
                    {
                        idx = 0;
                    }
                    if (idx >= 0 && idx < hudProfiles.Count)
                    {
                        var profile = hudProfiles[idx];
                        if (profile.weapon1Sprite != null)
                        {
                            weaponImg1.style.backgroundImage = new StyleBackground(profile.weapon1Sprite);
                        }
                    }
                }
            }
        }
    }

    private void InitializeFogOfWar()
    {
        if (isFowInitialized) return;

        // 1. Tạo texture sương mù màu đen hoàn toàn
        fogOfWarTexture = new Texture2D(fowTextureSize, fowTextureSize, TextureFormat.RGBA32, false);
        fogOfWarTexture.wrapMode = TextureWrapMode.Clamp;
        fogOfWarTexture.filterMode = FilterMode.Bilinear;
        
        fogOfWarColors = new Color32[fowTextureSize * fowTextureSize];
        Color32 blackTransparent = new Color32(10, 15, 25, 255); // Màu đen mờ tối của sương mù
        for (int i = 0; i < fogOfWarColors.Length; i++)
        {
            fogOfWarColors[i] = blackTransparent;
        }
        fogOfWarTexture.SetPixels32(fogOfWarColors);
        fogOfWarTexture.Apply();

        // 2. Tạo GameObject mặt phẳng sương mù
        fogOfWarPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fogOfWarPlane.name = "FogOfWar_Plane_Generated";
        
        // Hủy Collider của Plane để tránh va chạm vật lý
        var collider = fogOfWarPlane.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        // Xoay quad để nằm ngang song song mặt đất
        fogOfWarPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        fogOfWarPlane.transform.localScale = new Vector3(fowWorldSize, fowWorldSize, 1f);

        // Gán layer FOW (dùng Layer 24)
        fogOfWarPlane.layer = 24;

        // 3. Tạo vật liệu trong suốt không bị ảnh hưởng bởi ánh sáng
        Shader unlitTransparentShader = Shader.Find("Unlit/Transparent");
        if (unlitTransparentShader == null)
        {
            unlitTransparentShader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        }
        
        fogOfWarMaterial = new Material(unlitTransparentShader);
        fogOfWarMaterial.mainTexture = fogOfWarTexture;
        fogOfWarPlane.GetComponent<MeshRenderer>().material = fogOfWarMaterial;

        isFowInitialized = true;
        Debug.Log("[FogOfWar] Khởi tạo Fog of War thành công.");
    }

    private void UpdateFogOfWar()
    {
        if (LocalPlayerTarget == null || LocalPlayerTarget.transform == null) return;

        // Đảm bảo đã khởi tạo
        if (!isFowInitialized)
        {
            InitializeFogOfWar();
        }

        Vector3 localPos = LocalPlayerTarget.transform.position;

        // 1. Cập nhật vị trí độ cao của Plane FOW (nằm trên đầu người chơi, dưới camera map)
        if (fogOfWarPlane != null)
        {
            // Center ở (0,0) trong X, Z; Y dịch chuyển theo người chơi để tránh bị clip khi leo đồi núi
            fogOfWarPlane.transform.position = new Vector3(0f, localPos.y + 35f, 0f);
        }

        // 2. Chuyển đổi tọa độ thế giới của player sang UV của Texture
        float minX = fowWorldCenter.x - fowWorldSize / 2f;
        float minZ = fowWorldCenter.y - fowWorldSize / 2f;

        float u = (localPos.x - minX) / fowWorldSize;
        float v = (localPos.z - minZ) / fowWorldSize;

        int px = Mathf.Clamp((int)(u * fowTextureSize), 0, fowTextureSize - 1);
        int py = Mathf.Clamp((int)(v * fowTextureSize), 0, fowTextureSize - 1);

        // 3. Xóa sương mù dạng hình tròn xung quanh vị trí của player
        float pixelRadius = (fowRevealRadius / fowWorldSize) * fowTextureSize;
        int r = Mathf.CeilToInt(pixelRadius);

        int startX = Mathf.Max(0, px - r);
        int endX = Mathf.Min(fowTextureSize - 1, px + r);
        int startY = Mathf.Max(0, py - r);
        int endY = Mathf.Min(fowTextureSize - 1, py + r);

        bool textureChanged = false;
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                float distSq = (x - px) * (x - px) + (y - py) * (y - py);
                if (distSq <= pixelRadius * pixelRadius)
                {
                    Color32 currentPixel = fogOfWarColors[y * fowTextureSize + x];
                    if (currentPixel.a > 0)
                    {
                        // Giảm độ mờ về 0 (sáng lên)
                        fogOfWarColors[y * fowTextureSize + x] = new Color32(0, 0, 0, 0);
                        textureChanged = true;
                    }
                }
            }
        }

        if (textureChanged)
        {
            fogOfWarTexture.SetPixels32(fogOfWarColors);
            fogOfWarTexture.Apply();
        }

        // 4. Đảm bảo loại bỏ Layer 24 ra khỏi tất cả các camera ngoại trừ camera Map
        foreach (var cam in Camera.allCameras)
        {
            if (cam != minimapCamera && cam != worldMapCamera && cam.name != "MiniMapCamera" && cam.name != "WorldMapCamera_Generated")
            {
                cam.cullingMask &= ~(1 << 24); // Tắt FOW khỏi cam chính
            }
        }

        // Đảm bảo camera map hiển thị Layer 24
        if (minimapCamera != null)
        {
            minimapCamera.cullingMask |= (1 << 24);
        }
        if (worldMapCamera != null)
        {
            worldMapCamera.cullingMask |= (1 << 24);
        }
    }
}