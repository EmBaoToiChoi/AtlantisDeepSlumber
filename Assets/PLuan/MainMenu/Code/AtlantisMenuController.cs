using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class AtlantisMenuController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _root;

    // Tham chiếu Bảng giao diện
    private VisualElement _mainMenuPanel;
    private VisualElement _networkMenuPanel;
    private VisualElement _createRoomPanel;
    private VisualElement _joinRoomPanel;
    private VisualElement _joinAuthPanel;
    private VisualElement _optionsMenuPanel;
    private VisualElement _gameLogo;

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
    private float _glowTimer = 0f;
    private string _currentTargetRoomName = "";
    private const string DefaultRoomNamePlaceholder = "Atlantis Explorer";

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
        _gameLogo = _root.Q<VisualElement>("game-logo");

        // 2. Gán sự kiện Điều hướng
        _root.Q<Button>("btn-play").clicked += () => ShowPanel(_networkMenuPanel);
        _root.Q<Button>("btn-options").clicked += () => ShowPanel(_optionsMenuPanel);
        _root.Q<Button>("btn-quit").clicked += QuitGame;

        _root.Q<Button>("btn-create-host").clicked += () => ShowPanel(_createRoomPanel);
        _root.Q<Button>("btn-join-client").clicked += () => ShowPanel(_joinRoomPanel);
        _root.Q<Button>("btn-lobby-back").clicked += () => ShowPanel(_mainMenuPanel);

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

        _root.Q<Button>("btn-join-back").clicked += () => ShowPanel(_networkMenuPanel);
        _root.Q<Button>("btn-join-by-id").clicked += JoinByID;
        _root.Q<Button>("btn-join-item-1").clicked += () => JoinSpecificRoom("Abyss Crawlers", true);
        _root.Q<Button>("btn-join-item-2").clicked += () => JoinSpecificRoom("Deep Slumber #1", false);
        _root.Q<Button>("btn-auth-cancel").clicked += () => ShowPanel(_joinRoomPanel);
        _root.Q<Button>("btn-confirm-join-private").clicked += ConfirmJoinPrivateRoom;

        _root.Q<Button>("btn-cancel-options").clicked += () => ShowPanel(_mainMenuPanel);
        _root.Q<Button>("btn-save-options").clicked += SaveOptions;

        SetupOptionsTabs();
        SetupSliders();

        // Khởi tạo trọn bộ Động cơ Hạt đồng bộ HTML5
        InitSyncedParticleEngine();

        ShowPanel(_mainMenuPanel);
    }

    private void HideAllPanels()
    {
        _mainMenuPanel.AddToClassList("hidden-panel");
        _networkMenuPanel.AddToClassList("hidden-panel");
        _createRoomPanel.AddToClassList("hidden-panel");
        _joinRoomPanel.AddToClassList("hidden-panel");
        _joinAuthPanel.AddToClassList("hidden-panel");
        _optionsMenuPanel.AddToClassList("hidden-panel");
    }

    private void ShowPanel(VisualElement panelToShow)
    {
        HideAllPanels();
        panelToShow.RemoveFromClassList("hidden-panel");
    }

    // =========================================================================
    // ĐỘNG CƠ HẠT ĐỒNG BỘ (HTML5 PARTICLE ENGINE)
    // =========================================================================
    private void InitSyncedParticleEngine()
    {
        _particles.Clear();
        float screenWidth = Screen.width > 0 ? Screen.width : 1920f;

        // Công thức tính mật độ hạt gốc: window.innerWidth / 15
        // Tối ưu hóa trên giao diện game để tránh quá tải layout: chia cho hệ số an toàn hơn
        int targetParticleCount = Mathf.Clamp(Mathf.FloorToInt(screenWidth / 25f), 40, 80);

        var rootScreen = _root.Q<VisualElement>(className: "root-screen") ?? _root;

        for (int i = 0; i < targetParticleCount; i++)
        {
            var p = new ParticleData();
            // Tỷ lệ xuất hiện: 15% là Bọt khí (Bubble), 85% là Đốm sáng (Plankton)
            p.IsBubble = Random.value < 0.15f;

            p.RootElement = new VisualElement();

            if (p.IsBubble)
            {
                p.RootElement.AddToClassList("bubble-fx");

                // Tạo lõi highlight đặc trưng của HTML5 Canvas
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
                // Cập nhật vị trí Y
                p.Y -= p.SpeedY;

                // Cập nhật dao động ngang (Wobble)
                p.Wobble += p.WobbleSpeed;
                float currentX = p.X + Mathf.Sin(p.Wobble) * p.WobbleAmp;

                // Tái tạo lại hạt khi trôi qua trần
                if (p.Y < -50f)
                {
                    ResetParticle(p, false);
                }
                else
                {
                    // Cập nhật CSS Trực tiếp
                    p.RootElement.style.top = p.Y;
                    p.RootElement.style.left = currentX;

                    // Giả lập hiệu ứng Twinkle/Glow chớp tắt mượt mà cho đốm sáng
                    if (!p.IsBubble)
                    {
                        float dynamicOpacity = Mathf.Lerp(p.Opacity * 0.3f, p.Opacity, (Mathf.Sin(p.Wobble * 2f) + 1f) / 2f);
                        p.RootElement.style.opacity = dynamicOpacity;
                    }
                }
            }

            // Hiệu ứng Backlight thở đằng sau Logo (Tinh chỉnh dải Alpha mượt mà để quầng sáng thoát nền tự nhiên)
            if (_gameLogo != null)
            {
                _glowTimer += 0.025f;
                // Hạ dải Alpha dao động xuống (0.0f -> 0.12f) để tạo luồng hào quang mờ ảo, không bị gắt
                float alpha = Mathf.Lerp(0.0f, 0.12f, (Mathf.Sin(_glowTimer) + 1f) / 2f);
                _gameLogo.style.backgroundColor = new StyleColor(new Color(0.31f, 1.0f, 0.7f, alpha));
            }

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
            // Bán kính gốc: Math.random() * 5 + 2 (Đường kính: 4px -> 14px)
            // Phóng to nhẹ trên UI Toolkit để sắc nét hơn
            p.Radius = Random.Range(6f, 18f);
            p.SpeedY = Random.Range(0.8f, 2.5f); // Bay nhanh hơn
            p.WobbleSpeed = 0.04f;
            p.WobbleAmp = 25f; // Lắc ngang rộng (0.4 trong mã gốc)
            p.Opacity = Random.Range(0.25f, 0.55f);

            p.RootElement.style.width = p.Radius;
            p.RootElement.style.height = p.Radius;
            p.RootElement.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, p.Opacity));
            p.RootElement.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, p.Opacity * 0.7f));
            p.RootElement.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, p.Opacity * 0.7f));
            p.RootElement.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, p.Opacity * 0.3f));

            // Định vị chính xác Lõi Highlight góc trên bên trái:
            // ctx.arc(x - radius*0.3, y - radius*0.3, radius*0.2)
            if (p.CoreElement != null)
            {
                float coreSize = p.Radius * 0.4f; // Đường kính lõi
                p.CoreElement.style.width = coreSize;
                p.CoreElement.style.height = coreSize;
                p.CoreElement.style.top = p.Radius * 0.15f;
                p.CoreElement.style.left = p.Radius * 0.15f;
                p.CoreElement.style.opacity = p.Opacity + 0.3f;
            }
        }
        else
        {
            // Bán kính gốc: Math.random() * 1.5 + 0.2 (Đường kính: 0.4px -> 3.4px)
            // Ánh xạ sang kích thước pixel an toàn hiển thị
            p.Radius = Random.Range(2f, 5f);
            p.SpeedY = Random.Range(0.2f, 0.8f); // Trôi cực chậm
            p.WobbleSpeed = 0.02f;
            p.WobbleAmp = 8f; // Lắc ngang hẹp (0.15 trong mã gốc)
            p.Opacity = Random.Range(0.25f, 0.85f);

            p.RootElement.style.width = p.Radius;
            p.RootElement.style.height = p.Radius;
            p.RootElement.style.opacity = p.Opacity;
        }
    }

    // =========================================================================
    // CÁC HÀM XỬ LÝ SỰ KIỆN UI CHUNG
    // =========================================================================
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
        var tabAudioBtn = _root.Q<Button>("tab-audio-btn");
        var tabGraphicsBtn = _root.Q<Button>("tab-graphics-btn");
        var tabControlsBtn = _root.Q<Button>("tab-controls-btn");

        var tabAudio = _root.Q<VisualElement>("tab-audio");
        var tabGraphics = _root.Q<VisualElement>("tab-graphics");
        var tabControls = _root.Q<VisualElement>("tab-controls");

        tabAudioBtn.clicked += () => SwitchTab(tabAudioBtn, tabAudio);
        tabGraphicsBtn.clicked += () => SwitchTab(tabGraphicsBtn, tabGraphics);
        tabControlsBtn.clicked += () => SwitchTab(tabControlsBtn, tabControls);
    }

    private void SwitchTab(Button activeBtn, VisualElement activeContent)
    {
        _root.Q<Button>("tab-audio-btn").RemoveFromClassList("active");
        _root.Q<Button>("tab-graphics-btn").RemoveFromClassList("active");
        _root.Q<Button>("tab-controls-btn").RemoveFromClassList("active");
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

    private void SaveOptions() => ShowPanel(_mainMenuPanel);
}