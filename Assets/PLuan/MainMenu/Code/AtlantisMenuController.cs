using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video; // Bổ sung thư viện xử lý Video
using System.Threading.Tasks; // <-- THÊM DÒNG NÀY ĐỂ FIX LỖI BIÊN DỊCH ASYNC/AWAIT
using static UnityEngine.ShadowQuality;

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
    
    [Header("Network & Lobby")]
    [SerializeField] private NetworkBootstrap _netBootstrap;
    [SerializeField] private VisualTreeAsset _roomItemTemplate; 
    





    
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
    private string _currentTargetRoomName;
    private ScrollView _roomScrollView;

    private async void Start()
    {
        _isProcessingRoom = false; // Reset cờ chống spam
        await Task.Yield();
    }


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
    private LocalizationManager.Language _appliedLanguage = LocalizationManager.Language.Vietnamese;
    private string _appliedMicDevice = "Default";
    private string _appliedMicMode = "Push To Talk";
    private float _appliedMicInputVol = 100f;
    private float _appliedVoicePlaybackVol = 100f;
    private string _appliedOutputDevice = "System Default";
    private string _appliedPttKey = "V";

    // Custom Dropdown State
    private VisualElement _activeDropdownPopup;
    private string _resolutionValue = "1920x1080 (FHD)";
    private string _qualityValue    = "High";
    private int _aaIndex            = 0; // 0=Off, 1=2x, 2=4x, 3=8x
    private int _shadowIndex        = 3; // 0=Off, 1=Low, 2=Medium, 3=High
    private int _particleIndex      = 2; // 0=Low, 1=Medium, 2=High
    private int _fpsCapIndex        = 0; // 0=Unlimited, 1=30, 2=60, 3=120, 4=144
    private bool _postfxValue       = true;
    private string _languageValueString = "Vietnamese";
    private string _micDeviceValue = "Default";
    private string _micModeValue = "Push To Talk";
    private string _outputDeviceValue = "System Default";
    private string _pttKeyValue = "V";
    private bool _isCancelConfirmation = false;
    private VisualElement _currentActiveTab;
    private bool _isRebindingPTT = false;
    private float _rebindCooldown = 0f;
    
    // Smooth Scroll State for ScrollViews
    private class SmoothScrollTracker
    {
        public ScrollView ScrollView;
        public float TargetY;
        public bool IsActive;
    }
    private ScrollView _optionsScrollView;
    private SmoothScrollTracker _optionsScrollTracker = new SmoothScrollTracker();
    private SmoothScrollTracker _roomScrollTracker = new SmoothScrollTracker();

    void OnEnable()
    {
        if (_netBootstrap == null) _netBootstrap = FindFirstObjectByType<NetworkBootstrap>();
        if (_netBootstrap == null) _netBootstrap = NetworkBootstrap.Instance;

        // Áp dụng graphics settings đã lưu ngay khi scene Main Menu load
        // (NetworkBootstrap cũng gọi ApplyAll trong Awake — đây là lớp bảo hiểm idempotent).
        GraphicsSettingsManager.ApplyAll();

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
        _roomScrollView = _root.Q<ScrollView>("room-scroll-view");

        // Auth Panels
        _loginPanel = _root.Q<VisualElement>("login-panel");
        _registerPanel = _root.Q<VisualElement>("register-panel");
        _verifyOtpPanel = _root.Q<VisualElement>("verify-otp-panel");
        _userProfileContainer = _root.Q<VisualElement>("user-profile-container");
        _lblDisplayName = _root.Q<Label>("lbl-display-name");



        LocalizationManager.Initialize();
        RefreshLocalization();

        BindAuthEvents();
        AuthService.OnTokenExpired += HandleTokenExpired;

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
        if (btnJoinClient != null) {
            btnJoinClient.clicked += () => { RefreshRoomList(); ShowPanel(_joinRoomPanel); };
        }
        
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

        // btn-leave-room và btn-start-game đã chuyển sang WaittingRoom script quản lý UI riêng



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
        if (btnJoinItem1 != null) btnJoinItem1.clicked += () => JoinSpecificRoom("DEMO1", "Abyss Crawlers", true);
        
        var btnJoinItem2 = _root.Q<Button>("btn-join-item-2");
        if (btnJoinItem2 != null) btnJoinItem2.clicked += () => JoinSpecificRoom("DEMO2", "Deep Slumber #1", false);
        
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
        SetupSmoothScroll();

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

    void OnDisable()
    {
        AuthService.OnTokenExpired -= HandleTokenExpired;
    }

    private void HandleTokenExpired()
    {
        Debug.LogWarning("[AUTH] Token expired or invalid. Logging out...");
        DoLogout();
        ShowError(_root.Q<Label>("lbl-login-error"), "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
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
    private static readonly string[] LanguageChoices   = { "Vietnamese" };
    private string[] MicDeviceChoices => GetMicrophoneDevices();
    private string[] MicModeChoices => new string[] { 
        LocalizationManager.Get("mic_mode_ptt"), 
        LocalizationManager.Get("mic_mode_auto") 
    };
    private static readonly string[] OutputDeviceChoices = { "System Default", "Headphones (High Definition Audio)", "Speakers (High Definition Audio)" };
    private static readonly string[] PttKeyChoices = { "V", "G", "T", "Y", "LeftShift", "LeftAlt", "Space" };

    private string[] GetMicrophoneDevices()
    {
        var devices = Microphone.devices;
        var list = new List<string>();
        list.Add("Default");
        if (devices != null && devices.Length > 0)
        {
            foreach (var dev in devices)
            {
                list.Add(dev);
            }
        }
        return list.ToArray();
    }

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

        // --- Graphics sub-options ---
        var aaEl       = _root.Q<VisualElement>("opt-aa");
        var shadowEl   = _root.Q<VisualElement>("opt-shadows");
        var particleEl = _root.Q<VisualElement>("opt-particles");
        var fpscapEl   = _root.Q<VisualElement>("opt-fpscap");

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
                _rebindCooldown = 0.2f; // cooldown to prevent immediately catching Mouse0
                var pttKeyLbl = _root.Q<Label>("opt-ptt-key-value");
                if (pttKeyLbl != null)
                {
                    pttKeyLbl.text = "...";
                }
            });
        }
    }

    /// <summary>Phiên bản index-based của ToggleDropdown, dùng cho dropdown graphics (AA/Shadows/Particles/FPSCap).</summary>
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

        var rootScreen = _root.Q<VisualElement>(className: "root-screen") ?? _root;
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

        rootScreen.Add(popup);
        _activeDropdownPopup = popup;
        popup.RegisterCallback<GeometryChangedEvent>(_ => PositionPopup(popup, trigger, rootScreen));
        PositionPopup(popup, trigger, rootScreen);
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
                else if (capturedLabelName == "opt-mic-device-value") _micDeviceValue = choiceCapture;
                else if (capturedLabelName == "opt-mic-mode-value") _micModeValue = choiceCapture;
                else if (capturedLabelName == "opt-output-device-value") _outputDeviceValue = choiceCapture;
                else if (capturedLabelName == "opt-ptt-key-value") _pttKeyValue = choiceCapture;

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

    private async void ConfirmCreateRoom()
    {
        var btnConfirm = _root.Q<Button>("btn-confirm-create");
        if (btnConfirm != null && !btnConfirm.enabledSelf) return; // Đã đang tạo, không cho nhấn thêm

        string roomName = _root.Q<TextField>("input-room-name").value;
        if (string.IsNullOrWhiteSpace(roomName) || roomName == DefaultRoomNamePlaceholder) return;

        bool isPrivate = _root.Q<RadioButtonGroup>("radio-privacy").value == 1;
        string password = _root.Q<TextField>("input-room-password").value;

        // Khóa nút và đổi chữ để báo hiệu đang xử lý
        if (btnConfirm != null) {
            btnConfirm.SetEnabled(false);
            btnConfirm.text = "ĐANG TẠO...";
        }

        Debug.Log($"[HOST] Đang tạo phòng: {roomName}");
        
        var response = await AuthService.CreateRoom(roomName, isPrivate, password);
        if (response != null && response.success)
        {
            // Lưu lại thông tin phòng thật
            PlayerPrefs.SetString("CurrentRoomID", response.room.roomId);
            PlayerPrefs.SetString("CurrentRoomName", response.room.roomName);
            PlayerPrefs.SetInt("IsRoomHost", 1); 
            PlayerPrefs.Save();

            if (_netBootstrap == null) _netBootstrap = FindFirstObjectByType<NetworkBootstrap>();

            if (_netBootstrap != null)
            {
                Debug.Log("[Room] Đang kết nối về VPS...");
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.ShowLoading("Đang tải môi trường...");
                }
                _netBootstrap.StartServerAsHost();
            }


            else
            {
                Debug.LogError("[Room] THẤT BẠI: Không tìm thấy NetworkBootstrap trong cảnh!");
                if (btnConfirm != null) {
                    btnConfirm.SetEnabled(true);
                    btnConfirm.text = "XÁC NHẬN";
                }
            }

        }
        else
        {
            Debug.LogError($"[HOST] Lỗi tạo phòng: {response?.message}");
            // Mở lại nút nếu lỗi để người dùng thử lại
            if (btnConfirm != null) {
                btnConfirm.SetEnabled(true);
                btnConfirm.text = "XÁC NHẬN";
            }
        }
    }


    private async void JoinByID()
    {
        string inputID = _root.Q<TextField>("input-room-id").value.Trim().ToUpper();
        if (inputID.Length != 6) return;

        Debug.Log($"[JOIN] Kết nối tới ID: #{inputID}");
        var response = await AuthService.JoinRoom(inputID, "");
        if (response != null && response.success)
        {
            // Lưu lại thông tin phòng thật
            PlayerPrefs.SetString("CurrentRoomID", inputID);
            PlayerPrefs.SetString("CurrentRoomName", response.room.roomName);
            PlayerPrefs.SetInt("IsRoomHost", 0);
            PlayerPrefs.Save();

            if (_netBootstrap != null)
            {
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.ShowLoading("CONNECTING TO SESSION...");
                }
                _netBootstrap.StartClientAsPlayer();
            }
        }




        else
        {
            Debug.LogError($"[JOIN] Lỗi tham gia: {response?.message}");
        }
    }




    private void CreateRoomItem(RoomData room)
    {
        if (_roomItemTemplate == null) return;
        var item = _roomItemTemplate.Instantiate();
        
        // 1. Gán ID phòng
        var idLbl = item.Q<Label>("lbl-room-id");
        if (idLbl != null) idLbl.text = $"#{room.roomId}";

        // 2. Gán Tên phòng
        var nameLbl = item.Q<Label>("lbl-room-name");
        if (nameLbl != null) nameLbl.text = room.roomName;
        
        // 3. Hiển thị tên Chủ phòng trực tiếp từ đối tượng host
        var hostLbl = item.Q<Label>("lbl-host-name");
        if (hostLbl != null) 
        {
            hostLbl.text = room.host?.displayName ?? "Unknown Host";
        }


        
        // 4. Gán số lượng người chơi
        var playersLbl = item.Q<Label>("lbl-players");
        if (playersLbl != null) 
        {
            int current = room.players?.Length ?? 0;
            playersLbl.text = $"{current}/{room.maxPlayers}";
        }
        
        // 5. Chỉ khi nhấn NÚT JOIN mới thực hiện Join
        var joinBtn = item.Q<Button>("btn-join-room");
        if (joinBtn != null)
        {
            joinBtn.clicked += () => JoinSpecificRoom(room.roomId, room.roomName, room.isPrivate);
        }

        _roomScrollView.Add(item);
    }



    private bool _isProcessingRoom = false; // Flag chống spam

    private async void JoinSpecificRoom(string roomId, string roomName, bool isPrivate)
    {
        if (_isProcessingRoom) return; // Nếu đang xử lý thì bỏ qua
        _isProcessingRoom = true;

        Debug.Log($"[JOIN] Yêu cầu vào phòng: {roomName} (#{roomId})");
        
        if (isPrivate)
        {
            _currentTargetRoomName = roomName;
            PlayerPrefs.SetString("PendingJoinID", roomId); 
            ShowPanel(_joinAuthPanel);
            _isProcessingRoom = false; // Mở lại để nhập pass
            return;
        }

        var response = await AuthService.JoinRoom(roomId, "");
        if (response != null && response.success)
        {
            PlayerPrefs.SetString("CurrentRoomID", roomId);
            PlayerPrefs.SetString("CurrentRoomName", roomName);
            PlayerPrefs.SetInt("IsRoomHost", 0);
            PlayerPrefs.Save();

            if (_netBootstrap != null)
            {
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.ShowLoading("JOINING EXPEDITION...");
                }
                _netBootstrap.StartClientAsPlayer();
            }
        }
        else
        {
            Debug.LogError($"[JOIN] Lỗi: {response?.message}");
            _isProcessingRoom = false; // Thất bại thì mở lại để chọn phòng khác
        }
    }

    private async void ConfirmJoinPrivateRoom()
    {
        if (_isProcessingRoom) return;
        _isProcessingRoom = true;

        string pwd = _root.Q<TextField>("input-join-password").value;
        string roomId = PlayerPrefs.GetString("PendingJoinID", "");
        
        var response = await AuthService.JoinRoom(roomId, pwd);
        if (response != null && response.success)
        {
            PlayerPrefs.SetString("CurrentRoomID", roomId);
            PlayerPrefs.SetString("CurrentRoomName", _currentTargetRoomName);
            PlayerPrefs.SetInt("IsRoomHost", 0);
            PlayerPrefs.Save();

            if (_netBootstrap != null)
            {
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.ShowLoading("ACCESS GRANTED...");
                }
                _netBootstrap.StartClientAsPlayer();
            }
        }
        else
        {
            Debug.LogError($"[JOIN] Sai mật khẩu: {response?.message}");
            _isProcessingRoom = false;
        }
    }


    private async void RefreshRoomList()
    {
        if (_roomScrollView == null) return;
        _roomScrollView.Clear();

        var res = await AuthService.GetRooms();
        if (res != null && res.success)
        {
            if (res.rooms == null || res.rooms.Length == 0)
            {
                var emptyLabel = new Label(LocalizationManager.Get("lobby_empty"));
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
                _roomScrollView.Add(emptyLabel);
            }
            else
            {
                foreach (var room in res.rooms) CreateRoomItem(room);
            }
        }
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

    private async void SwitchTab(Button activeBtn, VisualElement activeContent)
    {
        // Self-heal active tab if null
        if (_currentActiveTab == null)
        {
            var tabGeneral = _root.Q<VisualElement>("tab-general");
            var tabAudio = _root.Q<VisualElement>("tab-audio");
            var tabGraphics = _root.Q<VisualElement>("tab-graphics");
            var tabControls = _root.Q<VisualElement>("tab-controls");
            
            if (tabGeneral != null && !tabGeneral.ClassListContains("hidden-element")) _currentActiveTab = tabGeneral;
            else if (tabAudio != null && !tabAudio.ClassListContains("hidden-element")) _currentActiveTab = tabAudio;
            else if (tabGraphics != null && !tabGraphics.ClassListContains("hidden-element")) _currentActiveTab = tabGraphics;
            else if (tabControls != null && !tabControls.ClassListContains("hidden-element")) _currentActiveTab = tabControls;
            else _currentActiveTab = tabGeneral;
        }

        if (activeContent == _currentActiveTab) return;

        // Reset scroll position to top when switching tabs
        if (_optionsScrollView != null)
        {
            _optionsScrollView.scrollOffset = Vector2.zero;
            if (_optionsScrollTracker != null)
            {
                _optionsScrollTracker.TargetY = 0f;
                _optionsScrollTracker.IsActive = false;
            }
        }

        // 1. Reset buttons active state
        _root.Q<Button>("tab-general-btn").RemoveFromClassList("active");
        _root.Q<Button>("tab-audio-btn").RemoveFromClassList("active");
        _root.Q<Button>("tab-graphics-btn").RemoveFromClassList("active");
        _root.Q<Button>("tab-controls-btn").RemoveFromClassList("active");
        activeBtn.AddToClassList("active");

        // 2. Fade Out old tab
        if (_currentActiveTab != null)
        {
            _currentActiveTab.AddToClassList("tab-fade-out");
            await Task.Delay(250); // Wait for the transition to complete (0.25s)
            _currentActiveTab.AddToClassList("hidden-element");
            _currentActiveTab.RemoveFromClassList("tab-fade-out");
        }

        // 3. Fade In new tab
        _currentActiveTab = activeContent;
        activeContent.RemoveFromClassList("hidden-element");
        activeContent.AddToClassList("tab-fade-out"); // Starts faded out and shifted
        
        await Task.Delay(50); // Wait 50ms to ensure layout engine registers the display change and initial faded-out state
        
        activeContent.RemoveFromClassList("tab-fade-out"); // Transitions back to opacity 1, translate 0
    }

    private void SetupSmoothScroll()
    {
        _optionsScrollView = _root.Q<ScrollView>("options-scroll-view");
        if (_optionsScrollView != null)
        {
            _optionsScrollTracker.ScrollView = _optionsScrollView;
            _optionsScrollView.RegisterCallback<WheelEvent>(evt => OnScrollWheel(evt, _optionsScrollTracker), TrickleDown.TrickleDown);
        }

        if (_roomScrollView != null)
        {
            _roomScrollTracker.ScrollView = _roomScrollView;
            _roomScrollView.RegisterCallback<WheelEvent>(evt => OnScrollWheel(evt, _roomScrollTracker), TrickleDown.TrickleDown);
        }
    }

    private void OnScrollWheel(WheelEvent evt, SmoothScrollTracker tracker)
    {
        if (tracker == null || tracker.ScrollView == null) return;

        // Prevent Unity's default snappy scroll
        evt.StopPropagation();

        float maxScroll = tracker.ScrollView.verticalScroller.highValue;
        if (maxScroll <= 0f) return;

        // Sync target if we weren't actively smooth scrolling
        if (!tracker.IsActive)
        {
            tracker.TargetY = tracker.ScrollView.scrollOffset.y;
        }

        // Custom smooth scrolling step size (100f per scroll tick, combined with 8f lerp speed for premium gliding feel)
        float step = evt.delta.y * 100f;
        tracker.TargetY = Mathf.Clamp(tracker.TargetY + step, 0f, maxScroll);
        tracker.IsActive = true;
    }

    private void Update()
    {
        UpdateTrackerScroll(_optionsScrollTracker);
        UpdateTrackerScroll(_roomScrollTracker);

        if (_isRebindingPTT)
        {
            _rebindCooldown -= Time.unscaledDeltaTime;
            if (_rebindCooldown <= 0f)
            {
                if (Input.anyKeyDown)
                {
                    foreach (KeyCode kcode in System.Enum.GetValues(typeof(KeyCode)))
                    {
                        if (Input.GetKeyDown(kcode))
                        {
                            string keyName = kcode.ToString();
                            _pttKeyValue = keyName;
                            var pttKeyLbl = _root.Q<Label>("opt-ptt-key-value");
                            if (pttKeyLbl != null) pttKeyLbl.text = keyName;

                            _isRebindingPTT = false;
                            Debug.Log($"[Rebind] PTT Key rebound to: {keyName}");
                            break;
                        }
                    }
                }
            }
        }
    }

    private void UpdateTrackerScroll(SmoothScrollTracker tracker)
    {
        if (tracker == null || tracker.ScrollView == null) return;

        if (tracker.IsActive)
        {
            float currentY = tracker.ScrollView.scrollOffset.y;
            // Interpolate smoothly using unscaled delta time to be independent of game speed (8f factor gives a gorgeous glide)
            float newY = Mathf.Lerp(currentY, tracker.TargetY, Time.unscaledDeltaTime * 8f);

            if (Mathf.Abs(newY - tracker.TargetY) < 0.2f)
            {
                newY = tracker.TargetY;
                tracker.IsActive = false;
            }

            tracker.ScrollView.scrollOffset = new Vector2(tracker.ScrollView.scrollOffset.x, newY);
        }
        else
        {
            // Sync target with any manual scrollbar handle drag
            tracker.TargetY = tracker.ScrollView.scrollOffset.y;
        }
    }

    private void SetupSliders()
    {
        BindSlider("slider-master", "val-master", true, (val) => UpdateRealtimeVolumes());
        BindSlider("slider-music", "val-music", true, (val) => UpdateRealtimeVolumes());
        BindSlider("slider-sfx", "val-sfx", true, (val) => UpdateRealtimeVolumes());
        BindSlider("slider-sens", "val-sens", false);
        BindSlider("slider-mic-input", "val-mic-input", true);
        BindSlider("slider-voice-playback", "val-voice-playback", true);
    }

    private void BindSlider(string sliderName, string labelName, bool isPercent, System.Action<float> onValueChanged = null)
    {
        var slider = _root.Q<Slider>(sliderName);
        var label = _root.Q<Label>(labelName);
        if (slider != null && label != null)
        {
            slider.RegisterValueChangedCallback(evt =>
            {
                label.text = Mathf.RoundToInt(evt.newValue) + (isPercent ? "%" : "");
                onValueChanged?.Invoke(evt.newValue);
            });
        }
    }

    private void UpdateRealtimeVolumes()
    {
        float tempMaster = _root.Q<Slider>("slider-master")?.value ?? 100f;
        float tempMusic = _root.Q<Slider>("slider-music")?.value ?? 80f;
        float tempSfx = _root.Q<Slider>("slider-sfx")?.value ?? 90f;

        if (AudioManager.Instance != null)
        {
            // Set volumes temporarily in real-time (without saving to PlayerPrefs during drag)
            AudioManager.Instance.SetVolumes(tempMaster, tempMusic, tempSfx, false);
        }
    }

    private void InitializeAppliedState()
    {
        _appliedLanguage = LocalizationManager.CurrentLanguage;
        _languageValueString = _appliedLanguage.ToString();
        
        _appliedMasterVol = PlayerPrefs.GetFloat("MasterVolume", 100f);
        _appliedMusicVol = PlayerPrefs.GetFloat("MusicVolume", 80f);
        _appliedSfxVol = PlayerPrefs.GetFloat("SFXVolume", 90f);
        _appliedSens = _root.Q<Slider>("slider-sens")?.value ?? 50f;
        _appliedInvertY = _root.Q<Toggle>("opt-invert-y")?.value ?? false;

        _appliedResolution = _resolutionValue;
        _appliedQuality = _qualityValue;
        _appliedFullscreen = _root.Q<Toggle>("opt-fullscreen")?.value ?? true;
        _appliedVsync = _root.Q<Toggle>("opt-vsync")?.value ?? true;
        _postfxValue = _root.Q<Toggle>("opt-postfx")?.value ?? true;

        // Đồng bộ index graphics sub-options từ PlayerPrefs (để IsAnyOptionChanged hoạt động đúng).
        GraphicsSettingsManager.ApplyAll();
        var curGfx = GraphicsSettingsManager.Current;
        _aaIndex       = GraphicsSettingsManager.AaToIndex(curGfx.AntiAliasing);
        _shadowIndex   = GraphicsSettingsManager.ShadowToIndex(curGfx.Shadows);
        _particleIndex = GraphicsSettingsManager.ParticleToIndex(curGfx.Particles);
        _fpsCapIndex   = GraphicsSettingsManager.FpsCapToIndex(curGfx.FpsCap);

        if (MicManager.Instance != null)
        {
            MicManager.Instance.LoadSettings();
            _appliedMicDevice = MicManager.Instance.selectedDevice;
            if (string.IsNullOrEmpty(_appliedMicDevice)) _appliedMicDevice = "Default";
            _appliedMicMode = MicManager.Instance.transmissionMode == 0 ? LocalizationManager.Get("mic_mode_ptt") : LocalizationManager.Get("mic_mode_auto");
            _appliedMicInputVol = MicManager.Instance.micInputVolume;
            _appliedVoicePlaybackVol = MicManager.Instance.voicePlaybackVolume;
        }
        else
        {
            _appliedMicDevice = PlayerPrefs.GetString("MicDevice", "Default");
            int modeVal = PlayerPrefs.GetInt("MicMode", 0);
            _appliedMicMode = modeVal == 0 ? LocalizationManager.Get("mic_mode_ptt") : LocalizationManager.Get("mic_mode_auto");
            _appliedMicInputVol = PlayerPrefs.GetFloat("MicInputVolume", 100f);
            _appliedVoicePlaybackVol = PlayerPrefs.GetFloat("MicPlaybackVolume", 100f);
        }

        _appliedOutputDevice = PlayerPrefs.GetString("OutputDevice", "System Default");
        _appliedPttKey = PlayerPrefs.GetString("MicPTTKeyString", "V");

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

        SetSliderValue("slider-master", "val-master", _appliedMasterVol, true);
        SetSliderValue("slider-music", "val-music", _appliedMusicVol, true);
        SetSliderValue("slider-sfx", "val-sfx", _appliedSfxVol, true);
        SetSliderValue("slider-mic-input", "val-mic-input", _appliedMicInputVol, true);
        SetSliderValue("slider-voice-playback", "val-voice-playback", _appliedVoicePlaybackVol, true);

        // Auto-spawn or reference AudioManager and play background music
        if (AudioManager.Instance == null)
        {
            GameObject amObj = new GameObject("AudioManager");
            amObj.AddComponent<AudioManager>();
        }
        AudioManager.Instance.SetVolumes(_appliedMasterVol, _appliedMusicVol, _appliedSfxVol);
        AudioManager.Instance.PlayBGM("Audio/BGM");
    }

    private bool HasUnsavedChanges()
    {
        if (_languageValueString != _appliedLanguage.ToString()) return true;
        if (_resolutionValue != _appliedResolution) return true;
        if (_qualityValue != _appliedQuality) return true;
        if (_micDeviceValue != _appliedMicDevice) return true;
        if (_micModeValue != _appliedMicMode) return true;
        if (_outputDeviceValue != _appliedOutputDevice) return true;
        if (_pttKeyValue != _appliedPttKey) return true;
        
        if ((_root.Q<Slider>("slider-master")?.value ?? 100f) != _appliedMasterVol) return true;
        if ((_root.Q<Slider>("slider-music")?.value ?? 80f) != _appliedMusicVol) return true;
        if ((_root.Q<Slider>("slider-sfx")?.value ?? 90f) != _appliedSfxVol) return true;
        if ((_root.Q<Slider>("slider-sens")?.value ?? 50f) != _appliedSens) return true;
        if ((_root.Q<Slider>("slider-mic-input")?.value ?? 100f) != _appliedMicInputVol) return true;
        if ((_root.Q<Slider>("slider-voice-playback")?.value ?? 100f) != _appliedVoicePlaybackVol) return true;
        
        if ((_root.Q<Toggle>("opt-fullscreen")?.value ?? true) != _appliedFullscreen) return true;
        if ((_root.Q<Toggle>("opt-vsync")?.value ?? true) != _appliedVsync) return true;
        if ((_root.Q<Toggle>("opt-postfx")?.value ?? true) != _postfxValue) return true;

        // Graphics sub-options: so sánh theo snapshot hiện tại (đã được ApplyAll đồng bộ vào PlayerPrefs).
        var curGfx = GraphicsSettingsManager.Current;
        if (GraphicsSettingsManager.AaToIndex(curGfx.AntiAliasing)       != _aaIndex)       return true;
        if (GraphicsSettingsManager.ShadowToIndex(curGfx.Shadows)         != _shadowIndex)   return true;
        if (GraphicsSettingsManager.ParticleToIndex(curGfx.Particles)     != _particleIndex) return true;
        if (GraphicsSettingsManager.FpsCapToIndex(curGfx.FpsCap)         != _fpsCapIndex)   return true;
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

        // --- Graphics sub-options: read from PlayerPrefs to keep UI in sync after Cancel ---
        var cur = GraphicsSettingsManager.Current;
        _aaIndex       = GraphicsSettingsManager.AaToIndex(cur.AntiAliasing);
        _shadowIndex   = GraphicsSettingsManager.ShadowToIndex(cur.Shadows);
        _particleIndex = GraphicsSettingsManager.ParticleToIndex(cur.Particles);
        _fpsCapIndex   = GraphicsSettingsManager.FpsCapToIndex(cur.FpsCap);
        _postfxValue   = cur.PostFX;
        SyncGraphicsLabelsFromIndex();

        // --- Graphics sub-options: keep label text in sync with current index state ---
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

    /// <summary>Đồng bộ text hiển thị 4 dropdown graphics + toggle postfx từ các field index.</summary>
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

    private void SaveOptions()
    {
        HideConfirmOverlay();

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

        // Apply and save volume settings to AudioManager
        if (AudioManager.Instance == null)
        {
            GameObject amObj = new GameObject("AudioManager");
            amObj.AddComponent<AudioManager>();
        }
        AudioManager.Instance.SetVolumes(_appliedMasterVol, _appliedMusicVol, _appliedSfxVol);

        // Save Mic Settings to PlayerPrefs
        PlayerPrefs.SetString("MicDevice", _appliedMicDevice == "Default" ? "" : _appliedMicDevice);
        int modeIdx = (_appliedMicMode == LocalizationManager.Get("mic_mode_auto")) ? 1 : 0;
        PlayerPrefs.SetInt("MicMode", modeIdx);
        PlayerPrefs.SetFloat("MicInputVolume", _appliedMicInputVol);
        PlayerPrefs.SetFloat("MicPlaybackVolume", _appliedVoicePlaybackVol);
        PlayerPrefs.SetString("OutputDevice", _appliedOutputDevice);
        PlayerPrefs.SetString("MicPTTKeyString", _appliedPttKey);
        
        // Parse key
        if (System.Enum.TryParse(_appliedPttKey, out KeyCode key))
        {
            PlayerPrefs.SetInt("MicPTTKey", (int)key);
        }
        PlayerPrefs.Save();

        if (MicManager.Instance != null)
        {
            MicManager.Instance.LoadSettings();
            MicManager.Instance.RestartRecording();
        }

        // --- GRAPHICS (resolution / vsync / quality / aa / shadows / particles / fps cap / postfx) ---
        // Đọc lại từ UI để chắc chắn khớp với lựa chọn mới nhất, build snapshot rồi nhờ manager lưu + áp dụng.
        var gfx = new GraphicsSettingsManager.GraphicsSnapshot
        {
            Resolution   = _resolutionValue,
            Quality      = _qualityValue,
            Fullscreen   = _appliedFullscreen,
            VSync        = _appliedVsync,
            FpsCap       = GraphicsSettingsManager.FpsCapFromIndex(_fpsCapIndex),
            AntiAliasing = GraphicsSettingsManager.AaFromIndex(_aaIndex),
            Shadows      = GraphicsSettingsManager.ShadowFromIndex(_shadowIndex),
            PostFX       = _postfxValue,
            Particles    = GraphicsSettingsManager.ParticleFromIndex(_particleIndex),
        };
        GraphicsSettingsManager.SaveAndApply(gfx);

        Debug.Log($"[Options] Graphics applied: {gfx.Resolution} | FS={gfx.Fullscreen} | VSync={gfx.VSync} | Quality={gfx.Quality} | AA={gfx.AntiAliasing}x | Shadows={gfx.Shadows} | Particles={gfx.Particles} | FPS={gfx.FpsCap} | PostFX={gfx.PostFX}");

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
                _root.Q<Label>("lbl-otp-msg").text = "Tài khoản chưa xác thực. Vui lòng kiểm tra email.";
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
        var btn = _root.Q<Button>("btn-register");
        var name = _root.Q<TextField>("input-reg-name").value;
        var email = _root.Q<TextField>("input-reg-email").value;
        var pwd = _root.Q<TextField>("input-reg-password").value;
        var confirm = _root.Q<TextField>("input-reg-confirm").value;
        var lblErr = _root.Q<Label>("lbl-reg-error");

        if (pwd != confirm)
        {
            ShowError(lblErr, "Mật khẩu xác nhận không khớp.");
            return;
        }

        // Chống spam: Disable nút
        if (btn != null)
        {
            btn.SetEnabled(false);
            btn.text = "ĐANG XỬ LÝ...";
        }

        lblErr.AddToClassList("hidden-element");
        var res = await AuthService.Register(name, email, pwd);

        if (res.success)
        {
            _currentRegEmail = res.email ?? email;
            _root.Q<Label>("lbl-otp-msg").text = "Chúng tôi đã gửi mã xác thực tới email của bạn.";
            ShowPanel(_verifyOtpPanel);
            StartResendTimer(30);
        }
        else
        {
            ShowError(lblErr, res.message);
        }

        // Re-enable nút nếu lỗi hoặc sau khi xử lý xong
        if (btn != null)
        {
            btn.SetEnabled(true);
            btn.text = "TẠO TÀI KHOẢN";
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
            ShowError(_root.Q<Label>("lbl-login-error"), "Xác minh thành công! Vui lòng đăng nhập.", false);
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
            _root.Q<Label>("lbl-otp-msg").text = "Mã xác thực mới đã được gửi.";
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
            btn.text = $"GỬI LẠI TRONG {_resendTimerCount}S";
            await Task.Delay(1000);
            _resendTimerCount--;
        }

        btn.text = "GỬI LẠI MÃ";
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
        SetText("lbl-opt-mic-device", "lbl_opt_mic_device");
        SetText("lbl-opt-mic-mode", "lbl_opt_mic_mode");
        SetText("lbl-mic-input-vol", "lbl_mic_input_vol");
        SetText("lbl-voice-playback-vol", "lbl_voice_playback_vol");
        SetText("lbl-opt-output-device", "lbl_opt_output_device");
        SetText("lbl-ptt-key", "lbl_ptt_key");
        
        // Cập nhật giá trị hiển thị hiện tại của dropdown Quality
        var lblQuality = _root.Q<Label>("opt-quality-value");
        if (lblQuality != null)
        {
            // Tìm index của chất lượng hiện tại trong danh sách cũ và cập nhật text mới
            // Ở đây đơn giản là lấy theo giá trị _qualityValue nếu nó khớp key hoặc reset về High
            lblQuality.text = LocalizationManager.Get("quality_" + _qualityValue.ToLower().Split(' ')[0]);
        }

        // Cập nhật giá trị hiển thị hiện tại của dropdown Mic Mode
        var lblMicMode = _root.Q<Label>("opt-mic-mode-value");
        if (lblMicMode != null)
        {
            lblMicMode.text = (_micModeValue == "Auto (Voice Active)" || _micModeValue == "Tự động phát hiện" || _micModeValue == "Auto")
                ? LocalizationManager.Get("mic_mode_auto")
                : LocalizationManager.Get("mic_mode_ptt");
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
