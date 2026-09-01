using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using Cursor = UnityEngine.Cursor;

public class InGamePauseMenu : MonoBehaviour
{
    public static InGamePauseMenu Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private UIDocument _uiDocument;
    [SerializeField] private VisualTreeAsset _pauseUxml;
    [SerializeField] private StyleSheet _menuUss;
    [SerializeField] private StyleSheet _pauseUss;
    [SerializeField] private PanelSettings _pausePanelSettings;

    // UI Elements
    private VisualElement _root;
    private class SmoothScrollTracker
    {
        public ScrollView ScrollView;
        public float TargetY;
        public bool IsActive;
    }

    private VisualElement _pauseRoot;
    private VisualElement _pauseMenuPanel;
    private VisualElement _optionsMenuPanel;
    private VisualElement _confirmQuitOverlay;
    private VisualElement _activeDropdownPopup;
    private ScrollView _optionsScrollView;
    private SmoothScrollTracker _optionsScrollTracker = new SmoothScrollTracker();
    private VisualElement _currentActiveTab;

    // State flags
    private bool _isPauseOpen = false;
    private bool _isSettingsOpen = false;
    private bool _isConfirmQuitOpen = false;
    private bool _isRebindingPTT = false;
    private float _rebindCooldown = 0f;

    // Applied & Pending Settings Values
    private string _resolutionValue;
    private string _qualityValue;
    private string _languageValueString;
    private string _micDeviceValue;
    private string _micModeValue;
    private string _outputDeviceValue;
    private string _pttKeyValue;
    private int _aaIndex = 0;
    private int _shadowIndex = 0;
    private int _particleIndex = 0;
    private int _fpsCapIndex = 0;
    private bool _postfxValue = true;

    private string _appliedResolution;
    private string _appliedQuality;
    private bool _appliedFullscreen = true;
    private bool _appliedVsync = true;
    private float _appliedMasterVol = 100f;
    private float _appliedMusicVol = 80f;
    private float _appliedSfxVol = 90f;
    private float _appliedSens = 50f;
    private bool _appliedInvertY = false;
    private string _appliedMicDevice = "Default";
    private string _appliedMicMode;
    private string _appliedOutputDevice = "System Default";
    private string _appliedPttKey = "V";
    private float _appliedMicInputVol = 100f;
    private float _appliedVoicePlaybackVol = 100f;
    private LocalizationManager.Language _appliedLanguage = LocalizationManager.Language.Vietnamese;

    // Dropdown choices
    private static readonly string[] ResolutionChoices = { "3840x2160 (4K)", "2560x1440 (2K)", "1920x1080 (FHD)", "1280x720 (HD)" };
    private string[] QualityChoices => new string[] {
        LocalizationManager.Get("quality_ultra"),
        LocalizationManager.Get("quality_high"),
        LocalizationManager.Get("quality_medium"),
        LocalizationManager.Get("quality_low")
    };
    private string[] AaChoices => new string[] {
        LocalizationManager.Get("aa_off"),
        LocalizationManager.Get("aa_2x"),
        LocalizationManager.Get("aa_4x"),
        LocalizationManager.Get("aa_8x")
    };
    private string[] ShadowChoices => new string[] {
        LocalizationManager.Get("shadow_off"),
        LocalizationManager.Get("shadow_low"),
        LocalizationManager.Get("shadow_medium"),
        LocalizationManager.Get("shadow_high")
    };
    private string[] ParticleChoices => new string[] {
        LocalizationManager.Get("particles_low"),
        LocalizationManager.Get("particles_medium"),
        LocalizationManager.Get("particles_high")
    };
    private string[] FpsCapChoices => new string[] {
        LocalizationManager.Get("fps_uncapped"),
        LocalizationManager.Get("fps_30"),
        LocalizationManager.Get("fps_60"),
        LocalizationManager.Get("fps_120"),
        LocalizationManager.Get("fps_144")
    };
    private static readonly string[] LanguageChoices = { "Vietnamese" };
    private string[] MicDeviceChoices => GetMicrophoneDevices();
    private string[] MicModeChoices => new string[] { 
        LocalizationManager.Get("mic_mode_ptt"), 
        LocalizationManager.Get("mic_mode_auto") 
    };
    private static readonly string[] OutputDeviceChoices = { "System Default", "Headphones (High Definition Audio)", "Speakers (High Definition Audio)" };
    private static readonly string[] PttKeyChoices = { "V", "G", "T", "Y", "LeftShift", "LeftAlt", "Space" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            var go = new GameObject("InGamePauseMenu");
            go.AddComponent<InGamePauseMenu>();
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureUIDocument();
            InitializeUI();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureUIDocument()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) _uiDocument = gameObject.AddComponent<UIDocument>();

        _uiDocument.sortingOrder = 99999; // Lớp vẽ cao nhất toàn game, đè lên mọi HUD và UI khác

        EnsurePanelEventComponents();

#if UNITY_EDITOR
        if (_pauseUxml == null)
        {
            _pauseUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/PLuan/InGamePause/InGamePauseMenu.uxml");
        }
        if (_menuUss == null)
        {
            _menuUss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PLuan/MainMenu/Code/AtlantisMenu.uss");
        }
        if (_pauseUss == null)
        {
            _pauseUss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PLuan/InGamePause/InGamePauseMenu.uss");
        }
        if (_pausePanelSettings == null)
        {
            _pausePanelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/PLuan/InGamePause/PausePanelSettings.asset");
        }
#endif

        if (_pauseUxml == null)
        {
            _pauseUxml = Resources.Load<VisualTreeAsset>("InGamePause/InGamePauseMenu");
        }
        if (_pauseUss == null)
        {
            _pauseUss = Resources.Load<StyleSheet>("InGamePause/InGamePauseMenu");
        }
        if (_pausePanelSettings == null)
        {
            _pausePanelSettings = Resources.Load<PanelSettings>("InGamePause/PausePanelSettings");
        }

        _uiDocument.visualTreeAsset = null;
        ResolvePanelSettings();
    }

    private void EnsurePanelEventComponents()
    {
        if (_uiDocument == null) return;
        GameObject go = _uiDocument.gameObject;

        var handler = go.GetComponent<PanelEventHandler>();
        if (handler == null) handler = go.AddComponent<PanelEventHandler>();
        handler.enabled = true;

        var raycaster = go.GetComponent<PanelRaycaster>();
        if (raycaster == null) raycaster = go.AddComponent<PanelRaycaster>();
        raycaster.enabled = true;
    }

    private void ResolvePanelSettings()
    {
        if (_uiDocument == null) return;

#if UNITY_EDITOR
        if (_pausePanelSettings == null)
        {
            _pausePanelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/PLuan/InGamePause/PausePanelSettings.asset");
        }
#endif
        if (_pausePanelSettings == null)
        {
            _pausePanelSettings = Resources.Load<PanelSettings>("InGamePause/PausePanelSettings");
        }

        if (_pausePanelSettings != null)
        {
            _uiDocument.panelSettings = _pausePanelSettings;
        }
        else if (_uiDocument.panelSettings == null)
        {
            var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var d in docs)
            {
                if (d != _uiDocument && d.panelSettings != null && d.panelSettings.targetTexture == null)
                {
                    _uiDocument.panelSettings = d.panelSettings;
                    break;
                }
            }
        }
    }

    private void SetupEventSystem()
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
        var inputSystemModule = activeES.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (inputSystemModule == null)
        {
            inputSystemModule = activeES.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        inputSystemModule.enabled = true;
        inputSystemModule.AssignDefaultActions();

        var standalone = activeES.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone != null)
        {
            standalone.enabled = false;
        }
#else
        var standalone = activeES.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone == null)
        {
            standalone = activeES.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        standalone.enabled = true;
#endif
    }

    private void InitializeUI()
    {
        if (_uiDocument == null) return;
        _root = _uiDocument.rootVisualElement;
        if (_root == null) return;

        _root.Clear();

        _root.style.width = Length.Percent(100);
        _root.style.height = Length.Percent(100);
        _root.style.position = Position.Absolute;
        _root.style.left = 0;
        _root.style.top = 0;
        _root.style.right = 0;
        _root.style.bottom = 0;
        _root.style.justifyContent = Justify.Center;
        _root.style.alignItems = Align.Center;

        if (_menuUss != null && !_root.styleSheets.Contains(_menuUss))
        {
            _root.styleSheets.Add(_menuUss);
        }
        if (_pauseUss != null && !_root.styleSheets.Contains(_pauseUss))
        {
            _root.styleSheets.Add(_pauseUss);
        }

        if (_pauseUxml != null)
        {
            _pauseUxml.CloneTree(_root);
        }

        _pauseRoot = _root.Q<VisualElement>("pause-root");
        _pauseMenuPanel = _root.Q<VisualElement>("pause-menu-panel");
        _optionsMenuPanel = _root.Q<VisualElement>("options-menu-panel");
        _confirmQuitOverlay = _root.Q<VisualElement>("confirm-quit-overlay");

        // 3 Pause Action Buttons (hỗ trợ cả clicked và ClickEvent để tương tác 100% nhạy)
        BindButton("btn-pause-resume", ResumeGame);
        BindButton("btn-pause-settings", OpenSettings);
        BindButton("btn-pause-quit", () => ShowQuitConfirm(true));

        // Options Actions
        BindButton("btn-save-options", SaveSettings);
        BindButton("btn-cancel-options", CancelSettings);

        // Confirm Quit Actions
        BindButton("btn-confirm-quit-yes", ConfirmQuitToMainMenu);
        BindButton("btn-confirm-quit-no", () => ShowQuitConfirm(false));

        // Setup Options Subsystems
        SetupOptionsTabs();
        SetupSliders();
        SetupCustomDropdowns();
        SetupSmoothScroll();

        // Ẩn ban đầu
        if (_root != null) _root.pickingMode = PickingMode.Ignore;
        if (_pauseRoot != null)
        {
            _pauseRoot.pickingMode = PickingMode.Ignore;
            _pauseRoot.AddToClassList("hidden-element");
        }
    }

    private void BindButton(string buttonName, System.Action onClick)
    {
        var btn = _root.Q<Button>(buttonName);
        if (btn == null) return;
        btn.clicked += onClick;
        btn.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            onClick?.Invoke();
        });
    }

    public static bool IsCutsceneActive()
    {
        // 1. Kiểm tra LocalCutsceneVideoPlayer trong Scene đang thực sự phát video
        var localCutscenes = FindObjectsByType<LocalCutsceneVideoPlayer>(FindObjectsSortMode.None);
        foreach (var lcp in localCutscenes)
        {
            if (lcp != null && lcp.gameObject.activeInHierarchy && lcp.IsCutsceneActive)
            {
                return true;
            }
        }

        // 2. Kiểm tra VideoCutsceneController trong Scene đang phát video
        var videoCutscenes = FindObjectsByType<VideoCutsceneController>(FindObjectsSortMode.None);
        foreach (var vcc in videoCutscenes)
        {
            if (vcc != null && vcc.gameObject.activeInHierarchy)
            {
                if (vcc.isPlaying || (vcc.videoUI != null && vcc.videoUI.activeSelf))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Update()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        // Không chạy pause menu ở MainMenu và Lobby
        if (sceneName == "MainMenu" || sceneName == "Lobby")
        {
            if (_isPauseOpen)
            {
                _isPauseOpen = false;
                _isSettingsOpen = false;
                _isConfirmQuitOpen = false;
                _pauseRoot?.AddToClassList("hidden-element");
                if (_root != null) _root.pickingMode = PickingMode.Ignore;
                if (_pauseRoot != null) _pauseRoot.pickingMode = PickingMode.Ignore;
            }
            return;
        }

        // Không cho phép tạm dừng nếu đang chạy Cutscene (để phím ESC dành riêng cho bỏ qua cutscene)
        if (IsCutsceneActive())
        {
            if (_isPauseOpen)
            {
                ResumeGame();
            }
            return;
        }

        // Xử lý PTT Rebind
        if (_isRebindingPTT)
        {
            if (_rebindCooldown > 0f)
            {
                _rebindCooldown -= Time.unscaledDeltaTime;
                return;
            }

            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(k))
                {
                    var pttLbl = _root.Q<Label>("opt-ptt-key-value");
                    if (k == KeyCode.Escape)
                    {
                        _isRebindingPTT = false;
                        if (pttLbl != null) pttLbl.text = _pttKeyValue;
                        return;
                    }
                    _pttKeyValue = k.ToString();
                    if (pttLbl != null) pttLbl.text = _pttKeyValue;
                    _isRebindingPTT = false;
                    return;
                }
            }
            return;
        }

        // Xử lý phím ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapePress();
        }

        // Xử lý hover & click thủ công (fallback raycast) để 100% đè lên mọi HUD gameplay
        UpdateManualPointerFallback();

        UpdateTrackerScroll(_optionsScrollTracker);
    }

    private VisualElement _lastHoveredButton = null;

    private void UpdateManualPointerFallback()
    {
        if (!_isPauseOpen || _root == null || _root.panel == null)
        {
            if (_lastHoveredButton != null)
            {
                _lastHoveredButton.RemoveFromClassList("force-hover");
                _lastHoveredButton = null;
            }
            return;
        }

        Vector2 guiMousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, guiMousePos);

        VisualElement picked = _root.panel.Pick(panelPos);

        // Hover handling
        VisualElement hoveredBtn = picked;
        while (hoveredBtn != null && !(hoveredBtn is Button) && !hoveredBtn.ClassListContains("pause-action-btn"))
        {
            hoveredBtn = hoveredBtn.parent;
        }

        if (hoveredBtn != _lastHoveredButton)
        {
            if (_lastHoveredButton != null)
            {
                _lastHoveredButton.RemoveFromClassList("force-hover");
            }
            _lastHoveredButton = hoveredBtn;
            if (_lastHoveredButton != null)
            {
                _lastHoveredButton.AddToClassList("force-hover");
            }
        }

        // Click handling
        if (Input.GetMouseButtonDown(0))
        {
            if (picked != null)
            {
                // 1. Kiểm tra Dropdown Item
                VisualElement itemEl = picked;
                while (itemEl != null && !itemEl.ClassListContains("custom-dropdown-item"))
                {
                    itemEl = itemEl.parent;
                }
                if (itemEl is Label itemLbl && _activeDropdownPopup != null)
                {
                    using var clickEvt = PointerUpEvent.GetPooled();
                    clickEvt.target = itemLbl;
                    itemLbl.SendEvent(clickEvt);
                    return;
                }

                // 2. Kiểm tra Button
                VisualElement btnEl = picked;
                while (btnEl != null && !(btnEl is Button))
                {
                    btnEl = btnEl.parent;
                }

                if (btnEl is Button btn)
                {
                    Debug.Log($"[PauseMenu Manual Click] Click Button: {btn.name}");
                    if (btn.name == "btn-pause-resume") ResumeGame();
                    else if (btn.name == "btn-pause-settings") OpenSettings();
                    else if (btn.name == "btn-pause-quit") ShowQuitConfirm(true);
                    else if (btn.name == "btn-confirm-quit-yes") ConfirmQuitToMainMenu();
                    else if (btn.name == "btn-confirm-quit-no") ShowQuitConfirm(false);
                    else if (btn.name == "btn-save-options") SaveSettings();
                    else if (btn.name == "btn-cancel-options") CancelSettings();
                    else if (btn.name == "tab-general-btn") SwitchTab(btn, _root.Q<VisualElement>("tab-general"));
                    else if (btn.name == "tab-audio-btn") SwitchTab(btn, _root.Q<VisualElement>("tab-audio"));
                    else if (btn.name == "tab-graphics-btn") SwitchTab(btn, _root.Q<VisualElement>("tab-graphics"));
                    else if (btn.name == "tab-controls-btn") SwitchTab(btn, _root.Q<VisualElement>("tab-controls"));
                    return;
                }

                // 3. Kiểm tra Custom Dropdown Trigger
                VisualElement dropdownEl = picked;
                while (dropdownEl != null && !dropdownEl.ClassListContains("custom-dropdown"))
                {
                    dropdownEl = dropdownEl.parent;
                }
                if (dropdownEl != null)
                {
                    using var clickEvt = PointerUpEvent.GetPooled();
                    clickEvt.target = dropdownEl;
                    dropdownEl.SendEvent(clickEvt);
                    return;
                }

                // 4. Nếu click ngoài dropdown popup -> đóng popup
                if (_activeDropdownPopup != null && !picked.ClassListContains("custom-dropdown-popup"))
                {
                    CloseDropdownPopup();
                    return;
                }

                // 5. Kiểm tra Toggle
                VisualElement toggleEl = picked;
                while (toggleEl != null && !(toggleEl is Toggle))
                {
                    toggleEl = toggleEl.parent;
                }
                if (toggleEl is Toggle tgl)
                {
                    tgl.value = !tgl.value;
                    return;
                }
            }
        }
    }

    private void HandleEscapePress()
    {
        // Nếu đang trong Cutscene thì không xử lý
        if (IsCutsceneActive()) return;

        // 1. Nếu đang mở popup dropdown -> đóng popup
        if (_activeDropdownPopup != null)
        {
            CloseDropdownPopup();
            return;
        }

        // 2. Nếu đang mở Popup Xác nhận Thoát -> Đóng popup xác nhận
        if (_isConfirmQuitOpen)
        {
            ShowQuitConfirm(false);
            return;
        }

        // 3. Nếu đang ở màn hình Cài Đặt (Settings) -> Hủy thay đổi và quay lại bảng 3 nút Tạm Dừng
        if (_isSettingsOpen)
        {
            CancelSettings();
            return;
        }

        // 4. Nếu đang mở bảng Tạm Dừng -> Tiếp tục chơi (Resume)
        if (_isPauseOpen)
        {
            ResumeGame();
            return;
        }

        // Nếu đang mở hội thoại NPC, không mở pause menu để tránh đè phím ESC của hội thoại
        bool isDialogueOpen = (RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive) ||
                              (SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive) ||
                              (IntroDialogueController.Instance != null && IntroDialogueController.Instance.IsActive);
        if (isDialogueOpen) return;

        // 5. Nếu đang chơi bình thường -> Bật bảng Tạm Dừng
        PauseGame();
    }

    public void PauseGame()
    {
        _isPauseOpen = true;
        _isSettingsOpen = false;
        _isConfirmQuitOpen = false;

        ResolvePanelSettings();
        EnsurePanelEventComponents();
        SetupEventSystem();

        if (_root == null || _pauseRoot == null)
        {
            EnsureUIDocument();
            InitializeUI();
        }

        if (_pauseUss != null && _root != null && !_root.styleSheets.Contains(_pauseUss))
        {
            _root.styleSheets.Add(_pauseUss);
        }

        if (_root != null) _root.pickingMode = PickingMode.Position;
        if (_pauseRoot != null)
        {
            _pauseRoot.style.display = DisplayStyle.Flex;
            _pauseRoot.pickingMode = PickingMode.Position;
            _pauseRoot.RemoveFromClassList("hidden-element");
            _pauseRoot.BringToFront();
        }

        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.style.display = DisplayStyle.Flex;
            _pauseMenuPanel.RemoveFromClassList("hidden-panel");
        }
        if (_optionsMenuPanel != null)
        {
            _optionsMenuPanel.style.display = DisplayStyle.None;
            _optionsMenuPanel.AddToClassList("hidden-panel");
        }
        if (_confirmQuitOverlay != null)
        {
            _confirmQuitOverlay.style.display = DisplayStyle.None;
            _confirmQuitOverlay.AddToClassList("hidden-element");
        }

        // Unlock chuột và khóa camera/di chuyển nhân vật
        SetPlayerControlBlocked(true);
    }

    public void ResumeGame()
    {
        _isPauseOpen = false;
        _isSettingsOpen = false;
        _isConfirmQuitOpen = false;
        CloseDropdownPopup();

        if (_pauseRoot != null)
        {
            _pauseRoot.style.display = DisplayStyle.None;
            _pauseRoot.AddToClassList("hidden-element");
            _pauseRoot.pickingMode = PickingMode.Ignore;
        }
        if (_root != null)
        {
            _root.pickingMode = PickingMode.Ignore;
        }

        // Khóa lại con trỏ chuột và mở lại điều khiển camera cho người chơi
        SetPlayerControlBlocked(false);
    }

    public bool IsPauseOpen => _isPauseOpen;

    private void SetPlayerControlBlocked(bool blocked)
    {
        PlayerHUDController.isAnyUIOpen = blocked;

        if (blocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        var target = PlayerHUDController.LocalPlayerTarget;
        if (target != null)
        {
            target.SetCursorLock(!blocked);
        }
        else
        {
            var targets = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in targets)
            {
                if (mb is IPlayerHUDTarget hudTarget)
                {
                    hudTarget.SetCursorLock(!blocked);
                }
            }
        }
    }

    public void OpenSettings()
    {
        _isSettingsOpen = true;
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.style.display = DisplayStyle.None;
            _pauseMenuPanel.AddToClassList("hidden-panel");
        }
        if (_optionsMenuPanel != null)
        {
            _optionsMenuPanel.style.display = DisplayStyle.Flex;
            _optionsMenuPanel.RemoveFromClassList("hidden-panel");
        }

        InitializeAppliedState();
    }

    public void CancelSettings()
    {
        RevertOptionsUI();
        _isSettingsOpen = false;
        CloseDropdownPopup();

        if (_optionsMenuPanel != null)
        {
            _optionsMenuPanel.style.display = DisplayStyle.None;
            _optionsMenuPanel.AddToClassList("hidden-panel");
        }
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.style.display = DisplayStyle.Flex;
            _pauseMenuPanel.RemoveFromClassList("hidden-panel");
        }
    }

    public void SaveSettings()
    {
        CloseDropdownPopup();
        _isSettingsOpen = false;

        _appliedResolution = _resolutionValue;
        _appliedQuality = _qualityValue;
        _appliedFullscreen = _root.Q<Toggle>("opt-fullscreen")?.value ?? true;
        _appliedVsync = _root.Q<Toggle>("opt-vsync")?.value ?? true;
        _postfxValue = _root.Q<Toggle>("opt-postfx")?.value ?? true;

        _appliedMasterVol = _root.Q<Slider>("slider-master")?.value ?? 100f;
        _appliedMusicVol = _root.Q<Slider>("slider-music")?.value ?? 80f;
        _appliedSfxVol = _root.Q<Slider>("slider-sfx")?.value ?? 90f;
        _appliedSens = _root.Q<Slider>("slider-sens")?.value ?? 50f;
        _appliedInvertY = _root.Q<Toggle>("opt-invert-y")?.value ?? false;

        _appliedMicDevice = _micDeviceValue;
        _appliedMicMode = _micModeValue;
        _appliedOutputDevice = _outputDeviceValue;
        _appliedPttKey = _pttKeyValue;
        _appliedMicInputVol = _root.Q<Slider>("slider-mic-input")?.value ?? 100f;
        _appliedVoicePlaybackVol = _root.Q<Slider>("slider-voice-playback")?.value ?? 100f;

        _appliedLanguage = _languageValueString == "Vietnamese" ? LocalizationManager.Language.Vietnamese : LocalizationManager.Language.English;
        LocalizationManager.SetLanguage(_appliedLanguage);

        // Áp dụng âm lượng
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolumes(_appliedMasterVol, _appliedMusicVol, _appliedSfxVol);
        }

        // Lưu cài đặt mic
        PlayerPrefs.SetString("MicDevice", _appliedMicDevice == "Default" ? "" : _appliedMicDevice);
        int modeIdx = (_appliedMicMode == LocalizationManager.Get("mic_mode_auto")) ? 1 : 0;
        PlayerPrefs.SetInt("MicMode", modeIdx);
        PlayerPrefs.SetFloat("MicInputVolume", _appliedMicInputVol);
        PlayerPrefs.SetFloat("MicPlaybackVolume", _appliedVoicePlaybackVol);
        PlayerPrefs.SetString("OutputDevice", _appliedOutputDevice);
        PlayerPrefs.SetString("MicPTTKeyString", _appliedPttKey);
        if (System.Enum.TryParse(_appliedPttKey, out KeyCode key))
        {
            PlayerPrefs.SetInt("MicPTTKey", (int)key);
        }

        // Lưu cài đặt điều khiển
        PlayerPrefs.SetFloat("MouseSensitivity", _appliedSens);
        PlayerPrefs.SetInt("InvertY", _appliedInvertY ? 1 : 0);
        PlayerPrefs.Save();

        if (MicManager.Instance != null)
        {
            MicManager.Instance.LoadSettings();
            MicManager.Instance.RestartRecording();
        }

        // Áp dụng đồ họa
        var gfx = new GraphicsSettingsManager.GraphicsSnapshot
        {
            Resolution = _resolutionValue,
            Quality = _qualityValue,
            Fullscreen = _appliedFullscreen,
            VSync = _appliedVsync,
            FpsCap = GraphicsSettingsManager.FpsCapFromIndex(_fpsCapIndex),
            AntiAliasing = GraphicsSettingsManager.AaFromIndex(_aaIndex),
            Shadows = GraphicsSettingsManager.ShadowFromIndex(_shadowIndex),
            PostFX = _postfxValue,
            Particles = GraphicsSettingsManager.ParticleFromIndex(_particleIndex),
        };
        GraphicsSettingsManager.SaveAndApply(gfx);

        Debug.Log("[PauseMenu] Đã lưu và áp dụng toàn bộ cài đặt.");

        if (_optionsMenuPanel != null)
        {
            _optionsMenuPanel.style.display = DisplayStyle.None;
            _optionsMenuPanel.AddToClassList("hidden-panel");
        }
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.style.display = DisplayStyle.Flex;
            _pauseMenuPanel.RemoveFromClassList("hidden-panel");
        }
    }

    private void ShowQuitConfirm(bool show)
    {
        _isConfirmQuitOpen = show;
        if (_confirmQuitOverlay != null)
        {
            if (show)
            {
                _confirmQuitOverlay.style.display = DisplayStyle.Flex;
                _confirmQuitOverlay.RemoveFromClassList("hidden-element");
                _confirmQuitOverlay.BringToFront();
            }
            else
            {
                _confirmQuitOverlay.style.display = DisplayStyle.None;
                _confirmQuitOverlay.AddToClassList("hidden-element");
            }
        }
    }

    private void ConfirmQuitToMainMenu()
    {
        Debug.Log("[PauseMenu] Rời trận đấu và quay về MainMenu...");
        ResumeGame();
        StartCoroutine(SafeQuitRoutine());
    }

    private IEnumerator SafeQuitRoutine()
    {
        string roomId = PlayerPrefs.GetString("CurrentRoomID", "");
        if (!string.IsNullOrEmpty(roomId))
        {
            _ = AuthService.LeaveRoom(roomId);
        }

        // Tắt kết nối Netcode
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[PauseMenu] Đang ngắt kết nối Netcode...");
            NetworkManager.Singleton.Shutdown();
        }

        // Đợi 2 frames để dọn sạch tài nguyên
        yield return null;
        yield return null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PlayerHUDController.isAnyUIOpen = false;

        if (SceneLoader.Instance != null)
        {
            _ = SceneLoader.Instance.LoadSceneAsync("MainMenu", "ĐANG QUAY VỀ MENU CHÍNH...");
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    // =========================================================================
    // OPTIONS & SETTINGS INITIALIZATION & HELPERS
    // =========================================================================

    private void InitializeAppliedState()
    {
        if (_appliedMicMode == null) _appliedMicMode = LocalizationManager.Get("mic_mode_ptt");

        _resolutionValue = PlayerPrefs.GetString(GraphicsSettingsManager.KEY_RESOLUTION, GraphicsSettingsManager.DEFAULT_RESOLUTION);
        _appliedResolution = _resolutionValue;

        _qualityValue = PlayerPrefs.GetString(GraphicsSettingsManager.KEY_QUALITY, GraphicsSettingsManager.DEFAULT_QUALITY);
        _appliedQuality = _qualityValue;

        _appliedFullscreen = PlayerPrefs.GetInt(GraphicsSettingsManager.KEY_FULLSCREEN, GraphicsSettingsManager.DEFAULT_FULLSCREEN ? 1 : 0) == 1;
        _appliedVsync = PlayerPrefs.GetInt(GraphicsSettingsManager.KEY_VSYNC, GraphicsSettingsManager.DEFAULT_VSYNC ? 1 : 0) == 1;

        _appliedMasterVol = PlayerPrefs.GetFloat("MasterVolume", 100f);
        _appliedMusicVol = PlayerPrefs.GetFloat("MusicVolume", 80f);
        _appliedSfxVol = PlayerPrefs.GetFloat("SFXVolume", 90f);

        _appliedSens = PlayerPrefs.GetFloat("MouseSensitivity", 50f);
        _appliedInvertY = PlayerPrefs.GetInt("InvertY", 0) == 1;

        _appliedMicDevice = PlayerPrefs.GetString("MicDevice", "Default");
        if (string.IsNullOrEmpty(_appliedMicDevice)) _appliedMicDevice = "Default";

        int micModeStored = PlayerPrefs.GetInt("MicMode", 0);
        _appliedMicMode = (micModeStored == 1) ? LocalizationManager.Get("mic_mode_auto") : LocalizationManager.Get("mic_mode_ptt");

        _appliedMicInputVol = PlayerPrefs.GetFloat("MicInputVolume", 100f);
        _appliedVoicePlaybackVol = PlayerPrefs.GetFloat("MicPlaybackVolume", 100f);
        _appliedOutputDevice = PlayerPrefs.GetString("OutputDevice", "System Default");
        _appliedPttKey = PlayerPrefs.GetString("MicPTTKeyString", "V");

        _appliedLanguage = LocalizationManager.CurrentLanguage;
        _languageValueString = _appliedLanguage.ToString();

        RevertOptionsUI();
    }

    private void RevertOptionsUI()
    {
        _resolutionValue = _appliedResolution;
        _qualityValue = _appliedQuality;
        _languageValueString = _appliedLanguage.ToString();

        var resLbl = _root.Q<Label>("opt-resolution-value");
        if (resLbl != null) resLbl.text = _resolutionValue;

        var qualLbl = _root.Q<Label>("opt-quality-value");
        if (qualLbl != null) qualLbl.text = _qualityValue;

        var langLbl = _root.Q<Label>("opt-language-value");
        if (langLbl != null) langLbl.text = _languageValueString;

        _micDeviceValue = _appliedMicDevice;
        _micModeValue = _appliedMicMode;
        _outputDeviceValue = _appliedOutputDevice;
        _pttKeyValue = _appliedPttKey;

        var micDevLbl = _root.Q<Label>("opt-mic-device-value");
        if (micDevLbl != null) micDevLbl.text = _micDeviceValue;

        var micModeLbl = _root.Q<Label>("opt-mic-mode-value");
        if (micModeLbl != null) micModeLbl.text = _micModeValue;

        var outDevLbl = _root.Q<Label>("opt-output-device-value");
        if (outDevLbl != null) outDevLbl.text = _outputDeviceValue;

        var pttKeyLbl = _root.Q<Label>("opt-ptt-key-value");
        if (pttKeyLbl != null) pttKeyLbl.text = _pttKeyValue;

        var cur = GraphicsSettingsManager.Current;
        _aaIndex = GraphicsSettingsManager.AaToIndex(cur.AntiAliasing);
        _shadowIndex = GraphicsSettingsManager.ShadowToIndex(cur.Shadows);
        _particleIndex = GraphicsSettingsManager.ParticleToIndex(cur.Particles);
        _fpsCapIndex = GraphicsSettingsManager.FpsCapToIndex(cur.FpsCap);
        _postfxValue = cur.PostFX;
        SyncGraphicsLabelsFromIndex();

        SetSliderValue("slider-master", "val-master", _appliedMasterVol, true);
        SetSliderValue("slider-music", "val-music", _appliedMusicVol, true);
        SetSliderValue("slider-sfx", "val-sfx", _appliedSfxVol, true);
        SetSliderValue("slider-sens", "val-sens", _appliedSens, false);
        SetSliderValue("slider-mic-input", "val-mic-input", _appliedMicInputVol, true);
        SetSliderValue("slider-voice-playback", "val-voice-playback", _appliedVoicePlaybackVol, true);

        var fsToggle = _root.Q<Toggle>("opt-fullscreen");
        if (fsToggle != null) fsToggle.value = _appliedFullscreen;

        var vsyncToggle = _root.Q<Toggle>("opt-vsync");
        if (vsyncToggle != null) vsyncToggle.value = _appliedVsync;

        var postfxTgl = _root.Q<Toggle>("opt-postfx");
        if (postfxTgl != null) postfxTgl.value = _postfxValue;

        var invertToggle = _root.Q<Toggle>("opt-invert-y");
        if (invertToggle != null) invertToggle.value = _appliedInvertY;
    }

    private void SetSliderValue(string sliderName, string labelName, float val, bool isPercent)
    {
        var slider = _root.Q<Slider>(sliderName);
        var label = _root.Q<Label>(labelName);
        if (slider != null) slider.value = val;
        if (label != null) label.text = Mathf.RoundToInt(val) + (isPercent ? "%" : "");
    }

    private void SyncGraphicsLabelsFromIndex()
    {
        if (AaChoices.Length > 0)
        {
            int idx = Mathf.Clamp(_aaIndex, 0, AaChoices.Length - 1);
            _aaIndex = idx;
            var lbl = _root.Q<Label>("opt-aa-value");
            if (lbl != null) lbl.text = AaChoices[idx];
        }
        if (ShadowChoices.Length > 0)
        {
            int idx = Mathf.Clamp(_shadowIndex, 0, ShadowChoices.Length - 1);
            _shadowIndex = idx;
            var lbl = _root.Q<Label>("opt-shadows-value");
            if (lbl != null) lbl.text = ShadowChoices[idx];
        }
        if (ParticleChoices.Length > 0)
        {
            int idx = Mathf.Clamp(_particleIndex, 0, ParticleChoices.Length - 1);
            _particleIndex = idx;
            var lbl = _root.Q<Label>("opt-particles-value");
            if (lbl != null) lbl.text = ParticleChoices[idx];
        }
        if (FpsCapChoices.Length > 0)
        {
            int idx = Mathf.Clamp(_fpsCapIndex, 0, FpsCapChoices.Length - 1);
            _fpsCapIndex = idx;
            var lbl = _root.Q<Label>("opt-fpscap-value");
            if (lbl != null) lbl.text = FpsCapChoices[idx];
        }
        var postfxTgl = _root.Q<Toggle>("opt-postfx");
        if (postfxTgl != null) postfxTgl.value = _postfxValue;
    }

    private void SetupOptionsTabs()
    {
        var tabGeneralBtn = _root.Q<Button>("tab-general-btn");
        var tabAudioBtn = _root.Q<Button>("tab-audio-btn");
        var tabGraphicsBtn = _root.Q<Button>("tab-graphics-btn");
        var tabControlsBtn = _root.Q<Button>("tab-controls-btn");

        var tabGeneral = _root.Q<VisualElement>("tab-general");
        var tabAudio = _root.Q<VisualElement>("tab-audio");
        var tabGraphics = _root.Q<VisualElement>("tab-graphics");
        var tabControls = _root.Q<VisualElement>("tab-controls");

        _currentActiveTab = tabGeneral;

        BindTabButton(tabGeneralBtn, tabGeneral);
        BindTabButton(tabAudioBtn, tabAudio);
        BindTabButton(tabGraphicsBtn, tabGraphics);
        BindTabButton(tabControlsBtn, tabControls);
    }

    private void BindTabButton(Button btn, VisualElement content)
    {
        if (btn == null || content == null) return;
        btn.clicked += () => SwitchTab(btn, content);
        btn.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            SwitchTab(btn, content);
        });
    }

    private async void SwitchTab(Button activeBtn, VisualElement activeContent)
    {
        if (activeContent == _currentActiveTab) return;

        if (_optionsScrollView != null)
        {
            _optionsScrollView.scrollOffset = Vector2.zero;
            if (_optionsScrollTracker != null)
            {
                _optionsScrollTracker.TargetY = 0f;
                _optionsScrollTracker.IsActive = false;
            }
        }

        _root.Q<Button>("tab-general-btn")?.RemoveFromClassList("active");
        _root.Q<Button>("tab-audio-btn")?.RemoveFromClassList("active");
        _root.Q<Button>("tab-graphics-btn")?.RemoveFromClassList("active");
        _root.Q<Button>("tab-controls-btn")?.RemoveFromClassList("active");
        activeBtn.AddToClassList("active");

        if (_currentActiveTab != null)
        {
            _currentActiveTab.AddToClassList("tab-fade-out");
            await Task.Delay(200);
            _currentActiveTab.AddToClassList("hidden-element");
            _currentActiveTab.RemoveFromClassList("tab-fade-out");
        }

        _currentActiveTab = activeContent;
        activeContent.RemoveFromClassList("hidden-element");
        activeContent.AddToClassList("tab-fade-out");

        await Task.Delay(50);
        activeContent.RemoveFromClassList("tab-fade-out");
    }

    private void SetupSliders()
    {
        RegisterSlider("slider-master", "val-master", true, val => {
            if (AudioManager.Instance != null)
            {
                float music = _root.Q<Slider>("slider-music")?.value ?? _appliedMusicVol;
                float sfx = _root.Q<Slider>("slider-sfx")?.value ?? _appliedSfxVol;
                AudioManager.Instance.SetVolumes(val, music, sfx, false);
            }
        });

        RegisterSlider("slider-music", "val-music", true, val => {
            if (AudioManager.Instance != null)
            {
                float master = _root.Q<Slider>("slider-master")?.value ?? _appliedMasterVol;
                float sfx = _root.Q<Slider>("slider-sfx")?.value ?? _appliedSfxVol;
                AudioManager.Instance.SetVolumes(master, val, sfx, false);
            }
        });

        RegisterSlider("slider-sfx", "val-sfx", true, val => {
            if (AudioManager.Instance != null)
            {
                float master = _root.Q<Slider>("slider-master")?.value ?? _appliedMasterVol;
                float music = _root.Q<Slider>("slider-music")?.value ?? _appliedMusicVol;
                AudioManager.Instance.SetVolumes(master, music, val, false);
            }
        });

        RegisterSlider("slider-sens", "val-sens", false, _ => {});
        RegisterSlider("slider-mic-input", "val-mic-input", true, _ => {});
        RegisterSlider("slider-voice-playback", "val-voice-playback", true, _ => {});
    }

    private void RegisterSlider(string sliderName, string labelName, bool isPercent, System.Action<float> onLiveChange)
    {
        var slider = _root.Q<Slider>(sliderName);
        var label = _root.Q<Label>(labelName);
        if (slider == null) return;

        slider.RegisterValueChangedCallback(evt =>
        {
            if (label != null)
            {
                label.text = Mathf.RoundToInt(evt.newValue) + (isPercent ? "%" : "");
            }
            onLiveChange?.Invoke(evt.newValue);
        });
    }

    private void SetupCustomDropdowns()
    {
        _root.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (_activeDropdownPopup == null) return;
            if (!_activeDropdownPopup.worldBound.Contains(evt.position))
                CloseDropdownPopup();
        }, TrickleDown.TrickleDown);

        var resEl = _root.Q<VisualElement>("opt-resolution");
        var qualEl = _root.Q<VisualElement>("opt-quality");
        var langEl = _root.Q<VisualElement>("opt-language");
        var micDevEl = _root.Q<VisualElement>("opt-mic-device");
        var micModeEl = _root.Q<VisualElement>("opt-mic-mode");
        var outputDevEl = _root.Q<VisualElement>("opt-output-device");
        var pttKeyEl = _root.Q<VisualElement>("opt-ptt-key");

        if (resEl != null)
            resEl.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleDropdown(resEl, ResolutionChoices, ref _resolutionValue, "opt-resolution-value");
            });

        if (qualEl != null)
            qualEl.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleDropdown(qualEl, QualityChoices, ref _qualityValue, "opt-quality-value");
            });

        var aaEl = _root.Q<VisualElement>("opt-aa");
        var shadowEl = _root.Q<VisualElement>("opt-shadows");
        var particleEl = _root.Q<VisualElement>("opt-particles");
        var fpscapEl = _root.Q<VisualElement>("opt-fpscap");

        if (aaEl != null) aaEl.RegisterCallback<PointerUpEvent>(evt =>
        {
            evt.StopPropagation();
            ToggleDropdownInt(aaEl, AaChoices, _aaIndex, "opt-aa-value", idx => _aaIndex = idx);
        });

        if (shadowEl != null) shadowEl.RegisterCallback<PointerUpEvent>(evt =>
        {
            evt.StopPropagation();
            ToggleDropdownInt(shadowEl, ShadowChoices, _shadowIndex, "opt-shadows-value", idx => _shadowIndex = idx);
        });

        if (particleEl != null) particleEl.RegisterCallback<PointerUpEvent>(evt =>
        {
            evt.StopPropagation();
            ToggleDropdownInt(particleEl, ParticleChoices, _particleIndex, "opt-particles-value", idx => _particleIndex = idx);
        });

        if (fpscapEl != null) fpscapEl.RegisterCallback<PointerUpEvent>(evt =>
        {
            evt.StopPropagation();
            ToggleDropdownInt(fpscapEl, FpsCapChoices, _fpsCapIndex, "opt-fpscap-value", idx => _fpsCapIndex = idx);
        });

        if (langEl != null)
        {
            langEl.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleDropdown(langEl, LanguageChoices, ref _languageValueString, "opt-language-value");
            });
        }

        if (micDevEl != null)
        {
            micDevEl.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleDropdown(micDevEl, MicDeviceChoices, ref _micDeviceValue, "opt-mic-device-value");
            });
        }

        if (micModeEl != null)
        {
            micModeEl.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleDropdown(micModeEl, MicModeChoices, ref _micModeValue, "opt-mic-mode-value");
            });
        }

        if (outputDevEl != null)
        {
            outputDevEl.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleDropdown(outputDevEl, OutputDeviceChoices, ref _outputDeviceValue, "opt-output-device-value");
            });
        }

        if (pttKeyEl != null)
        {
            pttKeyEl.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                _isRebindingPTT = true;
                _rebindCooldown = 0.2f;
                var pttKeyLbl = _root.Q<Label>("opt-ptt-key-value");
                if (pttKeyLbl != null) pttKeyLbl.text = "...";
            });
        }
    }

    private void ToggleDropdownInt(VisualElement trigger, string[] choices, int currentIndex, string valueLabelName, System.Action<int> onSelected)
    {
        if (_activeDropdownPopup != null)
        {
            CloseDropdownPopup();
            return;
        }

        string capturedLabelName = valueLabelName;
        string[] capturedChoices = choices;
        System.Action<int> capturedOnSelected = onSelected;

        var popup = new VisualElement();
        popup.AddToClassList("custom-dropdown-popup");

        for (int i = 0; i < capturedChoices.Length; i++)
        {
            int choiceIdx = i;
            string choiceText = capturedChoices[i];
            var item = new Label(choiceText);
            item.AddToClassList("custom-dropdown-item");
            item.RegisterCallback<PointerUpEvent>(e =>
            {
                e.StopPropagation();
                var lbl = _root.Q<Label>(capturedLabelName);
                if (lbl != null) lbl.text = choiceText;
                capturedOnSelected?.Invoke(choiceIdx);
                CloseDropdownPopup();
            });
            popup.Add(item);
        }

        _root.Add(popup);
        _activeDropdownPopup = popup;
        popup.RegisterCallback<GeometryChangedEvent>(_ => PositionPopup(popup, trigger, _root));
        PositionPopup(popup, trigger, _root);
    }

    private void ToggleDropdown(VisualElement trigger, string[] choices, ref string currentValue, string valueLabelName, System.Action<string> onValueChange = null)
    {
        if (_activeDropdownPopup != null)
        {
            CloseDropdownPopup();
            return;
        }

        string capturedLabelName = valueLabelName;
        string[] capturedChoices = choices;

        var popup = new VisualElement();
        popup.AddToClassList("custom-dropdown-popup");

        foreach (var choice in capturedChoices)
        {
            var choiceCapture = choice;
            var item = new Label(choiceCapture);
            item.AddToClassList("custom-dropdown-item");
            item.RegisterCallback<PointerUpEvent>(e =>
            {
                e.StopPropagation();
                var lbl = _root.Q<Label>(capturedLabelName);
                if (lbl != null) lbl.text = choiceCapture;

                if (capturedLabelName == "opt-resolution-value") _resolutionValue = choiceCapture;
                else if (capturedLabelName == "opt-quality-value") _qualityValue = choiceCapture;
                else if (capturedLabelName == "opt-language-value") _languageValueString = choiceCapture;
                else if (capturedLabelName == "opt-mic-device-value") _micDeviceValue = choiceCapture;
                else if (capturedLabelName == "opt-mic-mode-value") _micModeValue = choiceCapture;
                else if (capturedLabelName == "opt-output-device-value") _outputDeviceValue = choiceCapture;
                else if (capturedLabelName == "opt-ptt-key-value") _pttKeyValue = choiceCapture;

                onValueChange?.Invoke(choiceCapture);
                CloseDropdownPopup();
            });
            popup.Add(item);
        }

        _root.Add(popup);
        _activeDropdownPopup = popup;
        popup.RegisterCallback<GeometryChangedEvent>(_ => PositionPopup(popup, trigger, _root));
        PositionPopup(popup, trigger, _root);
    }

    private void PositionPopup(VisualElement popup, VisualElement trigger, VisualElement container)
    {
        var triggerRect = trigger.worldBound;
        var containerRect = container.worldBound;
        float left = triggerRect.xMin - containerRect.xMin;
        float top = triggerRect.yMax - containerRect.yMin + 2f;
        popup.style.left = left;
        popup.style.top = top;
        popup.style.width = triggerRect.width;
    }

    private void CloseDropdownPopup()
    {
        _activeDropdownPopup?.RemoveFromHierarchy();
        _activeDropdownPopup = null;
    }

    private void SetupSmoothScroll()
    {
        _optionsScrollView = _root.Q<ScrollView>("options-scroll-view");
        if (_optionsScrollView != null)
        {
            _optionsScrollTracker.ScrollView = _optionsScrollView;
            _optionsScrollView.RegisterCallback<WheelEvent>(evt => OnScrollWheel(evt, _optionsScrollTracker), TrickleDown.TrickleDown);
        }
    }

    private void OnScrollWheel(WheelEvent evt, SmoothScrollTracker tracker)
    {
        if (tracker == null || tracker.ScrollView == null) return;
        evt.StopPropagation();

        float maxScroll = tracker.ScrollView.verticalScroller.highValue;
        if (maxScroll <= 0f) return;

        if (!tracker.IsActive)
        {
            tracker.TargetY = tracker.ScrollView.scrollOffset.y;
        }

        float step = evt.delta.y * 100f;
        tracker.TargetY = Mathf.Clamp(tracker.TargetY + step, 0f, maxScroll);
        tracker.IsActive = true;
    }

    private void UpdateTrackerScroll(SmoothScrollTracker tracker)
    {
        if (tracker != null && tracker.IsActive && tracker.ScrollView != null)
        {
            float current = tracker.ScrollView.scrollOffset.y;
            float next = Mathf.Lerp(current, tracker.TargetY, Time.unscaledDeltaTime * 8f);

            if (Mathf.Abs(next - tracker.TargetY) < 0.5f)
            {
                next = tracker.TargetY;
                tracker.IsActive = false;
            }
            tracker.ScrollView.scrollOffset = new Vector2(tracker.ScrollView.scrollOffset.x, next);
        }
    }

    private string[] GetMicrophoneDevices()
    {
        var devices = Microphone.devices;
        var list = new List<string>();
        list.Add("Default");
        if (devices != null && devices.Length > 0)
        {
            foreach (var dev in devices) list.Add(dev);
        }
        return list.ToArray();
    }
}
