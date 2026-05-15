using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video; // Bổ sung thư viện xử lý Video
using System.Threading.Tasks; // <-- THÊM DÒNG NÀY ĐỂ FIX LỖI BIÊN DỊCH ASYNC/AWAIT

public class AtlantisMenuController : MonoBehaviour
{
    [Header("Video Background Config")]
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private RenderTexture _videoRenderTexture;


    private UIDocument _uiDocument;
    private VisualElement _root;

    // Tham chiếu Bảng giao diện
    private VisualElement _mainMenuPanel;
    private VisualElement _networkMenuPanel;
    private VisualElement _createRoomPanel;
    private VisualElement _joinRoomPanel;
    private VisualElement _joinAuthPanel;
    private VisualElement _optionsMenuPanel;
    
    // Authentication Panels
    private VisualElement _loginPanel;
    private VisualElement _registerPanel;
    private VisualElement _verifyOtpPanel;
    private VisualElement _userProfileContainer;
    private Label _lblDisplayName;
    private string _currentRegEmail = "";
    private int _resendTimerCount = 0; // Bộ đếm ngược gửi lại OTP

    // Cấu trúc Dữ liệu đồng bộ HTML5 Particle
    private class ParticleData
    {
        public VisualElement RootElement;
        public VisualElement CoreElement; // Chỉ dùng cho bọt khí (Bubble)
        public bool IsBubble;
        public float X;
        public float Y;
        public float Radius;
        public float SpeedY;
        public float Wobble;
        public float WobbleSpeed;
        public float WobbleAmp;
        public float Opacity;
    }

    private List<ParticleData> _particles = new List<ParticleData>();
    private string _currentTargetRoomName = "";
    private const string DefaultRoomNamePlaceholder = "Atlantis Explorer";

    // Applied Settings State
    private string _appliedResolution = "1920x1080 (FHD)";
    private string _appliedQuality    = "High";
    private bool _appliedFullscreen   = true;
    private bool _appliedVsync        = true;
    private float _appliedMasterVol = 100f;
    private float _appliedMusicVol = 80f;
    private float _appliedSfxVol = 90f;
    private float _appliedSens = 50f;
    private bool _appliedInvertY = false;
    private LocalizationManager.Language _appliedLanguage = LocalizationManager.Language.English;

    // Custom Dropdown State
    private VisualElement _activeDropdownPopup;
    private string _resolutionValue = "1920x1080 (FHD)";
    private string _qualityValue    = "High";
    private string _languageValueString = "English";
    private bool _isCancelConfirmation = false;

    void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) return;
        _root = _uiDocument.rootVisualElement;

        // 1. Ánh xạ thành phần UI
        _mainMenuPanel = _root.Q<VisualElement>("main-menu-panel");
        _networkMenuPanel = _root.Q<VisualElement>("network-menu-panel");
        _createRoomPanel = _root.Q<VisualElement>("create-room-panel");
        _joinRoomPanel = _root.Q<VisualElement>("join-room-panel");
        _joinAuthPanel = _root.Q<VisualElement>("join-auth-panel");
        _optionsMenuPanel = _root.Q<VisualElement>("options-menu-panel");

        // Auth Panels
        _loginPanel = _root.Q<VisualElement>("login-panel");
        _registerPanel = _root.Q<VisualElement>("register-panel");
        _verifyOtpPanel = _root.Q<VisualElement>("verify-otp-panel");
        _userProfileContainer = _root.Q<VisualElement>("user-profile-container");
        _lblDisplayName = _root.Q<Label>("lbl-display-name");

        LocalizationManager.Initialize();
        RefreshLocalization();

        BindAuthEvents();

        // Gán Video RenderTexture làm nền tự động co dãn cho root-screen
        var rootScreen = _root.Q<VisualElement>(className: "root-screen") ?? _root;
        if (_videoRenderTexture != null)
        {
            // Dùng Background.FromRenderTexture để chuyển đổi chuẩn xác sang Background
            rootScreen.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_videoRenderTexture));
        }

        // Đảm bảo Video tự động lặp (Loop) và chạy
        if (_videoPlayer != null)
        {
            _videoPlayer.isLooping = true;
            _videoPlayer.Play();
        }

        // 2. Gán sự kiện Điều hướng
        var btnPlay = _root.Q<Button>("btn-play");
        if (btnPlay != null) btnPlay.clicked += () => ShowPanel(_networkMenuPanel);
        
        var btnOptions = _root.Q<Button>("btn-options");
        if (btnOptions != null) btnOptions.clicked += () => ShowPanel(_optionsMenuPanel);
        
        var btnQuit = _root.Q<Button>("btn-quit");
        if (btnQuit != null) btnQuit.clicked += QuitGame;

        var btnCreateHost = _root.Q<Button>("btn-create-host");
        if (btnCreateHost != null) btnCreateHost.clicked += () => ShowPanel(_createRoomPanel);
        
        var btnJoinClient = _root.Q<Button>("btn-join-client");
        if (btnJoinClient != null) btnJoinClient.clicked += () => ShowPanel(_joinRoomPanel);
        
        var btnLobbyBack = _root.Q<Button>("btn-lobby-back");
        if (btnLobbyBack != null) btnLobbyBack.clicked += () => ShowPanel(_mainMenuPanel);

        // Xử lý Placeholder Tên phòng
        var roomNameInput = _root.Q<TextField>("input-room-name");
        if (roomNameInput != null)
        {
            roomNameInput.RegisterCallback<FocusEvent>(evt =>
            {
                if (roomNameInput.value == DefaultRoomNamePlaceholder) roomNameInput.value = "";
            });
            roomNameInput.RegisterCallback<BlurEvent>(evt =>
            {
                if (string.IsNullOrWhiteSpace(roomNameInput.value)) roomNameInput.value = DefaultRoomNamePlaceholder;
            });
        }

        _root.Q<Button>("btn-create-back").clicked += () => ShowPanel(_networkMenuPanel);
        _root.Q<Button>("btn-confirm-create").clicked += ConfirmCreateRoom;

        var radioGroup = _root.Q<RadioButtonGroup>("radio-privacy");
        var pwdGroup = _root.Q<VisualElement>("password-group");
        radioGroup.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue == 1) pwdGroup.RemoveFromClassList("hidden-element");
            else pwdGroup.AddToClassList("hidden-element");
        });

        var btnJoinBack = _root.Q<Button>("btn-join-back");
        if (btnJoinBack != null) btnJoinBack.clicked += () => ShowPanel(_networkMenuPanel);
        
        var btnJoinById = _root.Q<Button>("btn-join-by-id");
        if (btnJoinById != null) btnJoinById.clicked += JoinByID;
        
        var btnJoinItem1 = _root.Q<Button>("btn-join-item-1");
        if (btnJoinItem1 != null) btnJoinItem1.clicked += () => JoinSpecificRoom("Abyss Crawlers", true);
        
        var btnJoinItem2 = _root.Q<Button>("btn-join-item-2");
        if (btnJoinItem2 != null) btnJoinItem2.clicked += () => JoinSpecificRoom("Deep Slumber #1", false);
        
        var btnAuthCancel = _root.Q<Button>("btn-auth-cancel");
        if (btnAuthCancel != null) btnAuthCancel.clicked += () => ShowPanel(_joinRoomPanel);
        
        var btnConfirmJoinPrivate = _root.Q<Button>("btn-confirm-join-private");
        if (btnConfirmJoinPrivate != null) btnConfirmJoinPrivate.clicked += ConfirmJoinPrivateRoom;

        var btnCancelOptions = _root.Q<Button>("btn-cancel-options");
        if (btnCancelOptions != null) btnCancelOptions.clicked += OnCancelOptions;
        
        var btnSaveOptions = _root.Q<Button>("btn-save-options");
        if (btnSaveOptions != null) btnSaveOptions.clicked += () => ShowConfirmOverlay(false);
        
        var btnYes = _root.Q<Button>("btn-confirm-yes");
        if (btnYes != null) btnYes.clicked += OnConfirmYes;
        
        var btnNo = _root.Q<Button>("btn-confirm-no");
        if (btnNo != null) btnNo.clicked += OnConfirmNo;

        // Khởi tạo trạng thái đã áp dụng (Applied) ban đầu
        InitializeAppliedState();

        SetupOptionsTabs();
        SetupSliders();
        SetupCustomDropdowns();

        // Khởi tạo trọn bộ Động cơ Hạt đồng bộ HTML5
        InitSyncedParticleEngine();

        // Check Login State
        if (PlayerPrefs.HasKey("AuthToken"))
        {
            _lblDisplayName.text = PlayerPrefs.GetString("AuthDisplayName", "UNKNOWN_USER");
            _userProfileContainer.RemoveFromClassList("hidden-element");
            ShowPanelImmediately(_mainMenuPanel);
        }
        else
        {
            ShowPanelImmediately(_loginPanel);
        }
    }



    // =========================================================================
    // CUSTOM DROPDOWN
    // =========================================================================
    private static readonly string[] ResolutionChoices = { "3840x2160 (4K)", "2560x1440 (2K)", "1920x1080 (FHD)", "1280x720 (HD)" };
    private string[] QualityChoices => new string[] { 
        LocalizationManager.Get("quality_ultra"), 
        LocalizationManager.Get("quality_high"), 
        LocalizationManager.Get("quality_medium"), 
        LocalizationManager.Get("quality_low") 
    };
    private static readonly string[] LanguageChoices   = { "English", "Vietnamese" };

    private void SetupCustomDropdowns()
    {
        // Đóng popup khi click ra ngoài
        _root.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (_activeDropdownPopup == null) return;
            if (!_activeDropdownPopup.worldBound.Contains(evt.position))
                CloseDropdownPopup();
        }, TrickleDown.TrickleDown);

        var resEl = _root.Q<VisualElement>("opt-resolution");
        var qualEl = _root.Q<VisualElement>("opt-quality");
        var langEl = _root.Q<VisualElement>("opt-language");

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

        if (langEl != null)
        {
            _languageValueString = LocalizationManager.CurrentLanguage.ToString();
            _root.Q<Label>("opt-language-value").text = _languageValueString;

            langEl.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleDropdown(langEl, LanguageChoices, ref _languageValueString, "opt-language-value", (val) => {
                    var previewLang = val == "Vietnamese" ? LocalizationManager.Language.Vietnamese : LocalizationManager.Language.English;
                    LocalizationManager.SetPreviewLanguage(previewLang);
                    RefreshLocalization();
                });
            });
        }
    }

    private void ToggleDropdown(VisualElement trigger, string[] choices, ref string currentValue, string valueLabelName, System.Action<string> onValueChange = null)
    {
        // Nếu popup này đang mở thì đóng lại
        if (_activeDropdownPopup != null)
        {
            CloseDropdownPopup();
            return;
        }

        string capturedLabelName = valueLabelName;
        string[] capturedChoices = choices;
        var capturedRef = trigger;

        // Tạo popup và gắn vào root-screen (position: absolute)
        var rootScreen = _root.Q<VisualElement>(className: "root-screen") ?? _root;
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
                _root.Q<Label>(capturedLabelName).text = choiceCapture;
                
                if (capturedLabelName == "opt-resolution-value") _resolutionValue = choiceCapture;
                else if (capturedLabelName == "opt-quality-value") _qualityValue = choiceCapture;
                else if (capturedLabelName == "opt-language-value") _languageValueString = choiceCapture;

                onValueChange?.Invoke(choiceCapture);
                CloseDropdownPopup();
            });
            popup.Add(item);
        }

        rootScreen.Add(popup);
        _activeDropdownPopup = popup;

        // Canh vị trí popup ngay dưới trigger, dùng GeometryChangedEvent để đảm bảo layout xong
        popup.RegisterCallback<GeometryChangedEvent>(_ => PositionPopup(popup, trigger, rootScreen));
        PositionPopup(popup, trigger, rootScreen);
    }

    private void PositionPopup(VisualElement popup, VisualElement trigger, VisualElement container)
    {
        var triggerRect  = trigger.worldBound;
        var containerRect = container.worldBound;
        float left = triggerRect.xMin - containerRect.xMin;
        float top  = triggerRect.yMax - containerRect.yMin + 2f;
        popup.style.left  = left;
        popup.style.top   = top;
        popup.style.width = triggerRect.width;
    }

    private void CloseDropdownPopup()
    {
        _activeDropdownPopup?.RemoveFromHierarchy();
        _activeDropdownPopup = null;
    }



    // =========================================================================
    // ĐỘNG CƠ HẠT ĐỒNG BỘ (HTML5 PARTICLE ENGINE)
    // =========================================================================
    private void InitSyncedParticleEngine()
    {
        _particles.Clear();
        float screenWidth = Screen.width > 0 ? Screen.width : 1920f;

        int targetParticleCount = Mathf.Clamp(Mathf.FloorToInt(screenWidth / 25f), 40, 80);

        var rootScreen = _root.Q<VisualElement>(className: "root-screen") ?? _root;

        for (int i = 0; i < targetParticleCount; i++)
        {
            var p = new ParticleData();
            p.IsBubble = Random.value < 0.15f;

            p.RootElement = new VisualElement();

            if (p.IsBubble)
            {
                p.RootElement.AddToClassList("bubble-fx");

                p.CoreElement = new VisualElement();
                p.CoreElement.AddToClassList("bubble-core");
                p.RootElement.Add(p.CoreElement);
            }
            else
            {
                p.RootElement.AddToClassList("ambient-spot");
            }

            rootScreen.Insert(0, p.RootElement);
            _particles.Add(p);

            ResetParticle(p, true);
        }

        // Vòng lặp kết xuất chính (~60 FPS)
        _root.schedule.Execute(() =>
        {
            float currentScreenHeight = Screen.height > 0 ? Screen.height : 1080f;

            foreach (var p in _particles)
            {
                p.Y -= p.SpeedY;
                p.Wobble += p.WobbleSpeed;
                float currentX = p.X + Mathf.Sin(p.Wobble) * p.WobbleAmp;

                if (p.Y < -50f)
                {
                    ResetParticle(p, false);
                }
                else
                {
                    p.RootElement.style.top = p.Y;
                    p.RootElement.style.left = currentX;

                    if (!p.IsBubble)
                    {
                        float dynamicOpacity = Mathf.Lerp(p.Opacity * 0.3f, p.Opacity, (Mathf.Sin(p.Wobble * 2f) + 1f) / 2f);
                        p.RootElement.style.opacity = dynamicOpacity;
                    }
                }
            }
            // Đã lược bỏ hoàn toàn phần code hiệu ứng thở (glow) nền logo tĩnh
        }).Every(16);
    }

    private void ResetParticle(ParticleData p, bool randomY)
    {
        float screenWidth = Screen.width > 0 ? Screen.width : 1920f;
        float screenHeight = Screen.height > 0 ? Screen.height : 1080f;

        p.X = Random.Range(0f, screenWidth);
        p.Y = randomY ? Random.Range(0f, screenHeight) : screenHeight + Random.Range(20f, 100f);
        p.Wobble = Random.Range(0f, Mathf.PI * 2f);

        if (p.IsBubble)
        {
            p.Radius = Random.Range(6f, 18f);
            p.SpeedY = Random.Range(0.8f, 2.5f);
            p.WobbleSpeed = 0.04f;
            p.WobbleAmp = 25f;
            p.Opacity = Random.Range(0.25f, 0.55f);

            p.RootElement.style.width = p.Radius;
            p.RootElement.style.height = p.Radius;
            p.RootElement.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, p.Opacity));
            p.RootElement.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, p.Opacity * 0.7f));
            p.RootElement.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, p.Opacity * 0.7f));
            p.RootElement.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, p.Opacity * 0.3f));

            if (p.CoreElement != null)
            {
                float coreSize = p.Radius * 0.4f;
                p.CoreElement.style.width = coreSize;
                p.CoreElement.style.height = coreSize;
                p.CoreElement.style.top = p.Radius * 0.15f;
                p.CoreElement.style.left = p.Radius * 0.15f;
                p.CoreElement.style.opacity = p.Opacity + 0.3f;
            }
        }
        else
        {
            p.Radius = Random.Range(2f, 5f);
            p.SpeedY = Random.Range(0.2f, 0.8f);
            p.WobbleSpeed = 0.02f;
            p.WobbleAmp = 8f;
            p.Opacity = Random.Range(0.25f, 0.85f);

            p.RootElement.style.width = p.Radius;
            p.RootElement.style.height = p.Radius;
            p.RootElement.style.opacity = p.Opacity;
        }
    }

    private void ConfirmCreateRoom()
    {
        string roomName = _root.Q<TextField>("input-room-name").value;
        if (string.IsNullOrWhiteSpace(roomName) || roomName == DefaultRoomNamePlaceholder) return;
        Debug.Log($"[HOST] Đã tạo phòng: {roomName}");
    }

    private void JoinByID()
    {
        string inputID = _root.Q<TextField>("input-room-id").value.Trim();
        if (inputID.Length == 6) Debug.Log($"[JOIN] Kết nối tới ID: #{inputID}");
    }

    private void JoinSpecificRoom(string roomName, bool isPrivate)
    {
        _currentTargetRoomName = roomName;
        if (isPrivate)
        {
            _root.Q<Label>("auth-room-name").text = $"SESSION: {roomName}";
            ShowPanel(_joinAuthPanel);
        }
        else Debug.Log($"[JOIN] Vào phòng: {roomName}");
    }

    private void ConfirmJoinPrivateRoom()
    {
        string pwd = _root.Q<TextField>("input-join-password").value;
        if (!string.IsNullOrWhiteSpace(pwd)) Debug.Log($"[JOIN] Vào phòng {_currentTargetRoomName} với mật khẩu.");
    }

    private void QuitGame() => Application.Quit();

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

        tabGeneralBtn.clicked += () => SwitchTab(tabGeneralBtn, tabGeneral);
        tabAudioBtn.clicked += () => SwitchTab(tabAudioBtn, tabAudio);
        tabGraphicsBtn.clicked += () => SwitchTab(tabGraphicsBtn, tabGraphics);
        tabControlsBtn.clicked += () => SwitchTab(tabControlsBtn, tabControls);
    }

    private void SwitchTab(Button activeBtn, VisualElement activeContent)
    {
        _root.Q<Button>("tab-general-btn").RemoveFromClassList("active");
        _root.Q<Button>("tab-audio-btn").RemoveFromClassList("active");
        _root.Q<Button>("tab-graphics-btn").RemoveFromClassList("active");
        _root.Q<Button>("tab-controls-btn").RemoveFromClassList("active");

        _root.Q<VisualElement>("tab-general").AddToClassList("hidden-element");
        _root.Q<VisualElement>("tab-audio").AddToClassList("hidden-element");
        _root.Q<VisualElement>("tab-graphics").AddToClassList("hidden-element");
        _root.Q<VisualElement>("tab-controls").AddToClassList("hidden-element");

        activeBtn.AddToClassList("active");
        activeContent.RemoveFromClassList("hidden-element");
    }

    private void SetupSliders()
    {
        BindSlider("slider-master", "val-master", true);
        BindSlider("slider-music", "val-music", true);
        BindSlider("slider-sfx", "val-sfx", true);
        BindSlider("slider-sens", "val-sens", false);
    }

    private void BindSlider(string sliderName, string labelName, bool isPercent)
    {
        var slider = _root.Q<Slider>(sliderName);
        var label = _root.Q<Label>(labelName);
        if (slider != null && label != null)
        {
            slider.RegisterValueChangedCallback(evt =>
            {
                label.text = Mathf.RoundToInt(evt.newValue) + (isPercent ? "%" : "");
            });
        }
    }

    private void InitializeAppliedState()
    {
        _appliedLanguage = LocalizationManager.CurrentLanguage;
        _languageValueString = _appliedLanguage.ToString();
        
        _appliedMasterVol = _root.Q<Slider>("slider-master")?.value ?? 100f;
        _appliedMusicVol = _root.Q<Slider>("slider-music")?.value ?? 80f;
        _appliedSfxVol = _root.Q<Slider>("slider-sfx")?.value ?? 90f;
        _appliedSens = _root.Q<Slider>("slider-sens")?.value ?? 50f;
        _appliedInvertY = _root.Q<Toggle>("opt-invert-y")?.value ?? false;
        
        _appliedResolution = _resolutionValue;
        _appliedQuality = _qualityValue;
        _appliedFullscreen = _root.Q<Toggle>("opt-fullscreen")?.value ?? true;
        _appliedVsync = _root.Q<Toggle>("opt-vsync")?.value ?? true;
    }

    private bool HasUnsavedChanges()
    {
        if (_languageValueString != _appliedLanguage.ToString()) return true;
        if (_resolutionValue != _appliedResolution) return true;
        if (_qualityValue != _appliedQuality) return true;
        
        if ((_root.Q<Slider>("slider-master")?.value ?? 100f) != _appliedMasterVol) return true;
        if ((_root.Q<Slider>("slider-music")?.value ?? 80f) != _appliedMusicVol) return true;
        if ((_root.Q<Slider>("slider-sfx")?.value ?? 90f) != _appliedSfxVol) return true;
        if ((_root.Q<Slider>("slider-sens")?.value ?? 50f) != _appliedSens) return true;
        
        if ((_root.Q<Toggle>("opt-fullscreen")?.value ?? true) != _appliedFullscreen) return true;
        if ((_root.Q<Toggle>("opt-vsync")?.value ?? true) != _appliedVsync) return true;
        if ((_root.Q<Toggle>("opt-invert-y")?.value ?? false) != _appliedInvertY) return true;
        
        return false;
    }

    private void OnCancelOptions()
    {
        if (HasUnsavedChanges())
        {
            ShowConfirmOverlay(true);
        }
        else
        {
            RevertOptionsUI();
            ShowPanel(_mainMenuPanel);
        }
    }

    private void OnConfirmYes()
    {
        SaveOptions();
        if (_isCancelConfirmation)
        {
            ShowPanel(_mainMenuPanel);
        }
    }

    private void OnConfirmNo()
    {
        if (_isCancelConfirmation)
        {
            RevertOptionsUI();
            HideConfirmOverlay();
            ShowPanel(_mainMenuPanel);
        }
        else
        {
            HideConfirmOverlay();
        }
    }

    private void ShowConfirmOverlay(bool forCancel)
    {
        _isCancelConfirmation = forCancel;
        var overlay = _root.Q<VisualElement>("confirm-overlay");
        if (overlay == null) return;

        var title = overlay.Q<Label>("lbl-confirm-title");
        var msg = overlay.Q<Label>("lbl-confirm-msg");
        var btnYes = overlay.Q<Button>("btn-confirm-yes");
        var btnNo = overlay.Q<Button>("btn-confirm-no");

        if (forCancel)
        {
            if (title != null) title.text = LocalizationManager.Get("confirm_save_title");
            if (msg != null) msg.text = LocalizationManager.Get("confirm_save_msg");
            if (btnYes != null) btnYes.text = LocalizationManager.Get("btn_save_yes");
            if (btnNo != null) btnNo.text = LocalizationManager.Get("btn_save_no");
        }
        else
        {
            if (title != null) title.text = LocalizationManager.Get("confirm_title");
            if (msg != null) msg.text = LocalizationManager.Get("confirm_msg");
            if (btnYes != null) btnYes.text = LocalizationManager.Get("btn_confirm_yes");
            if (btnNo != null) btnNo.text = LocalizationManager.Get("btn_confirm_no");
        }

        overlay.RemoveFromClassList("hidden-element");
    }

    private void HideConfirmOverlay()
    {
        _root.Q<VisualElement>("confirm-overlay")?.AddToClassList("hidden-element");
    }

    private VisualElement _currentActivePanel;

    private async void ShowPanel(VisualElement panel)
    {
        if (panel == _currentActivePanel) return;

        // 1. Fade Out bảng hiện tại
        if (_currentActivePanel != null)
        {
            _currentActivePanel.AddToClassList("panel-fade-out");
            await Task.Delay(350); // Chờ hiệu ứng gần xong
            _currentActivePanel.AddToClassList("hidden-panel");
            _currentActivePanel.RemoveFromClassList("panel-fade-out");
        }
        else
        {
            // Nếu lần đầu (init), ẩn tất cả ngay lập tức
            HideAllPanelsImmediately();
        }

        // 2. Logic đặc biệt cho Options
        if (panel == _optionsMenuPanel)
        {
            InitializeAppliedState();
        }

        // 3. Fade In bảng mới
        _currentActivePanel = panel;
        panel.RemoveFromClassList("hidden-panel");
        panel.AddToClassList("panel-fade-out"); // Bắt đầu ở trạng thái mờ
        
        // Wait a frame for UI Toolkit to recognize display change
        await Task.Yield();
        
        panel.RemoveFromClassList("panel-fade-out"); // Kích hoạt transition sang visible
    }

    private void HideAllPanelsImmediately()
    {
        _loginPanel.AddToClassList("hidden-panel");
        _registerPanel.AddToClassList("hidden-panel");
        _verifyOtpPanel.AddToClassList("hidden-panel");
        _mainMenuPanel.AddToClassList("hidden-panel");
        _networkMenuPanel.AddToClassList("hidden-panel");
        _createRoomPanel.AddToClassList("hidden-panel");
        _joinRoomPanel.AddToClassList("hidden-panel");
        _joinAuthPanel.AddToClassList("hidden-panel");
        _optionsMenuPanel.AddToClassList("hidden-panel");
    }

    private void ShowPanelImmediately(VisualElement panel)
    {
        HideAllPanelsImmediately();
        _currentActivePanel = panel;
        panel.RemoveFromClassList("hidden-panel");
    }

    private void RevertOptionsUI()
    {
        _resolutionValue = _appliedResolution;
        _qualityValue = _appliedQuality;
        _languageValueString = _appliedLanguage.ToString();
        
        LocalizationManager.SetPreviewLanguage(_appliedLanguage);
        RefreshLocalization();

        var resLbl = _root.Q<Label>("opt-resolution-value");
        if (resLbl != null) resLbl.text = _resolutionValue;

        var qualLbl = _root.Q<Label>("opt-quality-value");
        if (qualLbl != null) qualLbl.text = _qualityValue;
        
        var langLbl = _root.Q<Label>("opt-language-value");
        if (langLbl != null) langLbl.text = _languageValueString;

        SetSliderValue("slider-master", "val-master", _appliedMasterVol, true);
        SetSliderValue("slider-music", "val-music", _appliedMusicVol, true);
        SetSliderValue("slider-sfx", "val-sfx", _appliedSfxVol, true);
        SetSliderValue("slider-sens", "val-sens", _appliedSens, false);

        var fsToggle = _root.Q<Toggle>("opt-fullscreen");
        if (fsToggle != null) fsToggle.value = _appliedFullscreen;

        var vsyncToggle = _root.Q<Toggle>("opt-vsync");
        if (vsyncToggle != null) vsyncToggle.value = _appliedVsync;
        
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

    private void SaveOptions()
    {
        HideConfirmOverlay();

        _appliedResolution = _resolutionValue;
        _appliedQuality = _qualityValue;
        _appliedFullscreen = _root.Q<Toggle>("opt-fullscreen")?.value ?? true;
        _appliedVsync = _root.Q<Toggle>("opt-vsync")?.value ?? true;
        
        _appliedMasterVol = _root.Q<Slider>("slider-master")?.value ?? 100f;
        _appliedMusicVol = _root.Q<Slider>("slider-music")?.value ?? 80f;
        _appliedSfxVol = _root.Q<Slider>("slider-sfx")?.value ?? 90f;
        _appliedSens = _root.Q<Slider>("slider-sens")?.value ?? 50f;
        _appliedInvertY = _root.Q<Toggle>("opt-invert-y")?.value ?? false;

        _appliedLanguage = _languageValueString == "Vietnamese" ? LocalizationManager.Language.Vietnamese : LocalizationManager.Language.English;
        LocalizationManager.SetLanguage(_appliedLanguage);

        // --- RESOLUTION ---
        // Parse "WxH (label)" → e.g. "1920x1080 (FHD)" → 1920, 1080
        bool fullscreen = _appliedFullscreen;
        ParseResolution(_resolutionValue, out int w, out int h);
        if (w > 0 && h > 0)
            Screen.SetResolution(w, h, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        // --- V-SYNC ---
        bool vsync = _appliedVsync;
        QualitySettings.vSyncCount = vsync ? 1 : 0;

        // --- GRAPHICS QUALITY ---
        int qualityIndex = _qualityValue switch
        {
            "Ultra (Cinematic)" => QualitySettings.names.Length - 1,
            "High"              => Mathf.Max(0, QualitySettings.names.Length - 2),
            "Medium"            => Mathf.Max(0, QualitySettings.names.Length / 2),
            _                   => 0, // Low (Performance)
        };
        QualitySettings.SetQualityLevel(qualityIndex, true);

        Debug.Log($"[Options] Applied: {w}x{h} | Fullscreen={fullscreen} | VSync={vsync} | Quality={_qualityValue} (idx {qualityIndex})");

        ShowPanel(_mainMenuPanel);
    }

    private static void ParseResolution(string resStr, out int width, out int height)
    {
        // Dạng: "1920x1080 (FHD)" — lấy phần trước dấu cách
        width = height = 0;
        int spaceIdx = resStr.IndexOf(' ');
        string pair = spaceIdx > 0 ? resStr.Substring(0, spaceIdx) : resStr;
        var parts = pair.Split('x');
        if (parts.Length == 2)
        {
            int.TryParse(parts[0], out width);
            int.TryParse(parts[1], out height);
        }
    }

    // =========================================================================
    // AUTHENTICATION LOGIC
    // =========================================================================
    private void BindAuthEvents()
    {
        _root.Q<Button>("btn-goto-register").clicked += () => ShowPanel(_registerPanel);
        _root.Q<Button>("btn-goto-login").clicked += () => ShowPanel(_loginPanel);
        _root.Q<Button>("btn-cancel-otp").clicked += () => ShowPanel(_loginPanel);
        _root.Q<Button>("btn-logout").clicked += DoLogout;

        _root.Q<Button>("btn-login").clicked += DoLogin;
        _root.Q<Button>("btn-register").clicked += DoRegister;
        _root.Q<Button>("btn-verify-otp").clicked += DoVerifyOTP;
        _root.Q<Button>("btn-resend-otp").clicked += DoResendOTP;
    }

    private async void DoLogin()
    {
        var email = _root.Q<TextField>("input-login-email").value;
        var pwd = _root.Q<TextField>("input-login-password").value;
        var lblErr = _root.Q<Label>("lbl-login-error");

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd))
        {
            ShowError(lblErr, "Email and Password are required.");
            return;
        }

        lblErr.AddToClassList("hidden-element");
        var res = await AuthService.Login(email, pwd);

        if (res.success)
        {
            PlayerPrefs.SetString("AuthToken", res.token);
            PlayerPrefs.SetString("AuthEmail", res.email);
            PlayerPrefs.SetString("AuthDisplayName", res.displayName);
            PlayerPrefs.Save();

            _lblDisplayName.text = res.displayName;
            _userProfileContainer.RemoveFromClassList("hidden-element");
            ShowPanel(_mainMenuPanel);
        }
        else
        {
            if (res.needsVerification)
            {
                _currentRegEmail = res.email;
                _root.Q<Label>("lbl-otp-msg").text = "Account not verified. Check email.";
                ShowPanel(_verifyOtpPanel);
                StartResendTimer(30);
            }
            else
            {
                ShowError(lblErr, res.message);
            }
        }
    }

    private async void DoRegister()
    {
        var name = _root.Q<TextField>("input-reg-name").value;
        var email = _root.Q<TextField>("input-reg-email").value;
        var pwd = _root.Q<TextField>("input-reg-password").value;
        var confirm = _root.Q<TextField>("input-reg-confirm").value;
        var lblErr = _root.Q<Label>("lbl-reg-error");

        if (pwd != confirm)
        {
            ShowError(lblErr, "Passwords do not match.");
            return;
        }

        lblErr.AddToClassList("hidden-element");
        var res = await AuthService.Register(name, email, pwd);

        if (res.success)
        {
            _currentRegEmail = res.email ?? email;
            _root.Q<Label>("lbl-otp-msg").text = "We've sent a code to your email.";
            ShowPanel(_verifyOtpPanel);
            StartResendTimer(30);
        }
        else
        {
            ShowError(lblErr, res.message);
        }
    }

    private async void DoVerifyOTP()
    {
        var otp = _root.Q<TextField>("input-otp").value;
        var lblErr = _root.Q<Label>("lbl-otp-error");

        if (string.IsNullOrEmpty(otp)) return;

        lblErr.AddToClassList("hidden-element");
        var res = await AuthService.VerifyOTP(_currentRegEmail, otp);

        if (res.success)
        {
            _root.Q<TextField>("input-login-email").value = _currentRegEmail;
            _root.Q<TextField>("input-login-password").value = "";
            ShowPanel(_loginPanel);
            ShowError(_root.Q<Label>("lbl-login-error"), "Verification success! Please login.", false);
        }
        else
        {
            ShowError(lblErr, res.message);
        }
    }

    private async void DoResendOTP()
    {
        var lblErr = _root.Q<Label>("lbl-otp-error");
        lblErr.AddToClassList("hidden-element");
        var res = await AuthService.ResendOTP(_currentRegEmail);

        if (res.success)
        {
            _root.Q<Label>("lbl-otp-msg").text = "A new code has been sent.";
            StartResendTimer(30);
        }
        else
        {
            ShowError(lblErr, res.message);
        }
    }

    private void DoLogout()
    {
        PlayerPrefs.DeleteKey("AuthToken");
        PlayerPrefs.DeleteKey("AuthEmail");
        PlayerPrefs.DeleteKey("AuthDisplayName");
        PlayerPrefs.Save();
        
        _userProfileContainer.AddToClassList("hidden-element");
        
        // Clear text
        _lblDisplayName.text = "";
        _root.Q<TextField>("input-login-email").value = "";
        _root.Q<TextField>("input-login-password").value = "";
        
        ShowPanel(_loginPanel);
    }

    private void ShowError(Label lbl, string msg, bool isError = true)
    {
        lbl.text = msg;
        lbl.style.color = isError ? new StyleColor(new Color(1f, 0.31f, 0.31f)) : new StyleColor(new Color(0.47f, 0.9f, 1f));
        lbl.RemoveFromClassList("hidden-element");
    }

    private async void StartResendTimer(int seconds)
    {
        var btn = _root.Q<Button>("btn-resend-otp");
        if (btn == null) return;

        // Nếu đang đếm ngược rồi thì không chạy thêm cái khác
        if (_resendTimerCount > 0) return;

        _resendTimerCount = seconds;
        btn.SetEnabled(false);

        while (_resendTimerCount > 0)
        {
            btn.text = $"RESEND IN {_resendTimerCount}S";
            await Task.Delay(1000);
            _resendTimerCount--;
        }

        btn.text = "RESEND CODE";
        btn.SetEnabled(true);
    }

    private void RefreshLocalization()
    {
        // Login
        SetText("lbl-login-title", "login_title");
        SetText("lbl-login-email", "lbl_email");
        SetText("lbl-login-password", "lbl_password");
        SetText("btn-login", "btn_login");
        SetText("btn-goto-register", "btn_goto_register");

        // Register
        SetText("lbl-reg-title", "reg_title");
        SetText("lbl-reg-name", "lbl_display_name");
        SetText("lbl-reg-email", "lbl_email");
        SetText("lbl-reg-password", "lbl_password");
        SetText("lbl-reg-confirm", "lbl_confirm_password");
        SetText("btn-register", "btn_register");
        SetText("btn-goto-login", "btn_back_to_login");

        // OTP
        SetText("lbl-otp-title", "otp_title");
        SetText("lbl-otp-msg", "otp_msg");
        SetText("lbl-otp-spam-note", "otp_spam_note");
        SetText("lbl-otp-code", "lbl_otp_code");
        SetText("btn-verify-otp", "btn_verify");
        SetText("btn-resend-otp", _resendTimerCount > 0 ? null : "btn_resend");
        SetText("btn-cancel-otp", "btn_cancel");

        // Main Menu
        SetText("btn-play", "btn_play");
        SetText("btn-options", "btn_options");
        SetText("btn-quit", "btn_quit");

        // Profile
        SetText("lbl-profile-hint", "lbl_logged_in_as");
        SetText("btn-logout", "btn_logout");

        // Network Lobby
        SetText("lbl-lobby-title", "lobby_title");
        SetText("btn-create-host", "btn_create_host");
        SetText("btn-join-client", "btn_join_client");
        SetText("btn-lobby-back", "btn_back");

        // Create Room
        SetText("lbl-host-title", "host_title");
        SetText("lbl-room-name", "lbl_room_name");
        SetText("lbl-privacy", "lbl_privacy");
        
        var radioPublic = _root.Q<RadioButton>("radio-public");
        if (radioPublic != null) radioPublic.text = LocalizationManager.Get("lbl_public");
        
        var radioPrivate = _root.Q<RadioButton>("radio-private");
        if (radioPrivate != null) radioPrivate.text = LocalizationManager.Get("lbl_private");
        
        SetText("lbl-room-password", "lbl_room_password");
        SetText("btn-confirm-create", "btn_start_host");
        SetText("btn-create-back", "btn_back");

        // Join Room
        SetText("lbl-browser-title", "browser_title");
        SetText("btn-join-by-id", "btn_join_id");
        SetText("lbl-col-id", "col_id");
        SetText("lbl-col-name", "col_name");
        SetText("lbl-col-host", "col_host");
        SetText("lbl-col-players", "col_players");
        SetText("lbl-col-action", "col_action");
        SetText("btn-join-back", "btn_back");
        
        // Cập nhật text "VÀO" cho các phòng mẫu
        SetText("btn-join-item-1", "btn_join");
        SetText("btn-join-item-2", "btn_join");

        // Join Auth
        SetText("lbl-auth-title", "auth_title");
        SetText("lbl-auth-password", "lbl_auth_password");
        SetText("btn-confirm-join-private", "btn_connect");
        SetText("btn-auth-cancel", "btn_cancel");

        // Options
        SetText("lbl-options-title", "options_title");
        SetText("tab-general-btn", "tab_general");
        SetText("tab-audio-btn", "tab_audio");
        SetText("tab-graphics-btn", "tab_graphics");
        SetText("tab-controls-btn", "tab_controls");
        SetText("lbl-opt-language", "lbl_language");

        // Options Detail Labels
        SetText("lbl-master-volume", "lbl_master_volume");
        SetText("lbl-music-bgm", "lbl_music_bgm");
        SetText("lbl-effects-sfx", "lbl_effects_sfx");
        SetText("lbl-resolution", "lbl_resolution");
        SetText("lbl-graphics-quality", "lbl_graphics_quality");
        
        // Cập nhật giá trị hiển thị hiện tại của dropdown Quality
        var lblQuality = _root.Q<Label>("opt-quality-value");
        if (lblQuality != null)
        {
            // Tìm index của chất lượng hiện tại trong danh sách cũ và cập nhật text mới
            // Ở đây đơn giản là lấy theo giá trị _qualityValue nếu nó khớp key hoặc reset về High
            lblQuality.text = LocalizationManager.Get("quality_" + _qualityValue.ToLower().Split(' ')[0]);
        }

        var toggleFullscreen = _root.Q<Toggle>("opt-fullscreen");
        if (toggleFullscreen != null) toggleFullscreen.label = LocalizationManager.Get("lbl_fullscreen");
        
        var toggleVsync = _root.Q<Toggle>("opt-vsync");
        if (toggleVsync != null) toggleVsync.label = LocalizationManager.Get("lbl_vsync");
        
        var toggleInvertY = _root.Q<Toggle>("opt-invert-y");
        if (toggleInvertY != null) toggleInvertY.label = LocalizationManager.Get("lbl_invert_y");

        SetText("lbl-mouse-sens", "lbl_mouse_sens");

        SetText("btn-save-options", "btn_apply");
        SetText("btn-cancel-options", "btn_cancel_options");

        // Confirm Overlay
        SetText("lbl-confirm-title", "confirm_title");
        SetText("lbl-confirm-msg", "confirm_msg");
        SetText("btn-confirm-yes", "btn_confirm_yes");
        SetText("btn-confirm-no", "btn_confirm_no");
    }

    private void SetText(string elName, string locKey)
    {
        var el = _root.Q(elName);
        if (el == null) return;
        
        string val = LocalizationManager.Get(locKey);
        if (el is Label lbl) lbl.text = val;
        else if (el is Button btn && locKey != null) btn.text = val;
    }
}