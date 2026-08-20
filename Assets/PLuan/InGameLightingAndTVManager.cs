using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Collections;

/// <summary>
/// Quản lý ánh sáng và chế độ chống tối màn hình / TV trong lúc chơi (Runtime).
/// - Tự động tạo đèn dịu (Aura Light) theo chân Player để không bao giờ bị tối khi vào hang/râm/mê cung.
/// - Hỗ trợ phím tắt điều chỉnh độ sáng trực tiếp khi đang cắm máy chiếu / TV thuyết trình:
///   + F7 hoặc phím [: Bật/Tắt Chế Độ Trình Chiếu TV (TV Presentation Boost)
///   + F8 hoặc phím ]: Tăng độ sáng (+10%)
///   + F6: Giảm độ sáng (-10%)
///   + F9: Bật/Tắt hướng dẫn phím tắt độ sáng
/// </summary>
[DisallowMultipleComponent]
public class InGameLightingAndTVManager : MonoBehaviour
{
    private static InGameLightingAndTVManager _instance;
    public static InGameLightingAndTVManager Instance => _instance;

    [Header("1. Đèn Theo Chân Player (Player Aura Fill Light)")]
    [Tooltip("Tự động tạo và gắn đèn mềm theo người chơi (0% lag, không bóng đổ)")]
    public bool enablePlayerLight = true;
    [Range(0.2f, 3f)] public float playerLightIntensity = 1.0f;
    [Range(6f, 30f)] public float playerLightRange = 15f;
    public Color playerLightColor = new Color(1.0f, 0.95f, 0.88f, 1f);
    public Vector3 playerLightOffset = new Vector3(0f, 1.8f, 0f);

    [Header("2. Chế Độ Trình Chiếu TV (TV Boost Mode)")]
    [Tooltip("Bật tăng sáng mạnh mẽ khi cắm TV hoặc máy chiếu hội trường")]
    public bool tvBoostMode = false;
    [Range(1.0f, 2.5f)] public float tvBoostMultiplier = 1.45f;

    [Header("3. Điều Chỉnh Độ Sáng Tổng Thể (Runtime Brightness)")]
    [Range(0.5f, 2.5f)] public float globalBrightnessMultiplier = 1.0f;

    [Header("4. Tùy Chọn Hiển Thị (HUD Notifications)")]
    public bool showNotifications = true;

    // References nội bộ
    private Light _playerLightComponent;
    private GameObject _playerLightObject;
    private Transform _targetPlayerTransform;
    private float _searchPlayerTimer = 0f;

    private float _baseAmbientIntensity = 1.2f;
    private PostProcessVolume _postProcessVolume;
    private ColorGrading _colorGrading;

    // Toast HUD
    private string _notificationText = "";
    private float _notificationTimer = 0f;
    private bool _showHelpWindow = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Lưu ambient gốc của scene
        _baseAmbientIntensity = Mathf.Max(0.5f, RenderSettings.ambientIntensity);

        // Load cài đặt đã lưu nếu có
        LoadSavedSettings();
    }

    private void Start()
    {
        SetupPlayerLight();
        FindPostProcessing();
        ApplyLightingState();
        ShowToast("💡 Lighting Manager Đã Sẵn Sàng (F7: TV Mode | F8/F6: Tăng/Giảm sáng)");
    }

    private void Update()
    {
        HandleHotkeys();
        UpdatePlayerLightPosition();
    }

    private void HandleHotkeys()
    {
        // F7 hoặc Phím [: Bật/Tắt Chế độ TV
        if (Input.GetKeyDown(KeyCode.F7) || Input.GetKeyDown(KeyCode.LeftBracket))
        {
            tvBoostMode = !tvBoostMode;
            ApplyLightingState();
            SaveSettings();
            ShowToast(tvBoostMode ? "📺 [CHẾ ĐỘ TV / MÁY CHIẾU]: BẬT (Sáng rõ nét)" : "☀️ [CHẾ ĐỘ TIÊU CHUẨN]: BẬT (Màu tự nhiên)");
        }

        // F8 hoặc Phím ]: Tăng độ sáng
        if (Input.GetKeyDown(KeyCode.F8) || Input.GetKeyDown(KeyCode.RightBracket))
        {
            globalBrightnessMultiplier = Mathf.Clamp(globalBrightnessMultiplier + 0.1f, 0.5f, 2.5f);
            ApplyLightingState();
            SaveSettings();
            ShowToast($"☀️ Độ sáng: {Mathf.RoundToInt(globalBrightnessMultiplier * 100)}%");
        }

        // F6: Giảm độ sáng
        if (Input.GetKeyDown(KeyCode.F6))
        {
            globalBrightnessMultiplier = Mathf.Clamp(globalBrightnessMultiplier - 0.1f, 0.5f, 2.5f);
            ApplyLightingState();
            SaveSettings();
            ShowToast($"🌙 Độ sáng: {Mathf.RoundToInt(globalBrightnessMultiplier * 100)}%");
        }

        // F9: Bật/Tắt bảng hướng dẫn phím tắt
        if (Input.GetKeyDown(KeyCode.F9))
        {
            _showHelpWindow = !_showHelpWindow;
        }
    }

    public void ApplyLightingState()
    {
        float tvMultiplier = tvBoostMode ? tvBoostMultiplier : 1.0f;
        float totalMul = globalBrightnessMultiplier * tvMultiplier;

        // 1. Cập nhật Ambient Lighting
        RenderSettings.ambientIntensity = _baseAmbientIntensity * totalMul;

        // 2. Cập nhật Player Light
        if (_playerLightComponent != null)
        {
            _playerLightComponent.enabled = enablePlayerLight;
            _playerLightComponent.intensity = playerLightIntensity * totalMul;
            _playerLightComponent.range = playerLightRange * (tvBoostMode ? 1.2f : 1.0f);
            _playerLightComponent.color = playerLightColor;
        }

        // 3. Cập nhật Post-Processing nếu có
        if (_colorGrading != null)
        {
            float extraLift = tvBoostMode ? 0.08f : 0f;
            float extraExposure = (totalMul - 1f) * 0.4f;

            _colorGrading.lift.overrideState = true;
            _colorGrading.lift.value = new Vector4(1f, 1f, 1f, Mathf.Clamp01(0.04f + extraLift));

            _colorGrading.postExposure.overrideState = true;
            _colorGrading.postExposure.value = Mathf.Clamp(0.2f + extraExposure, -1f, 2f);
        }
    }

    private void SetupPlayerLight()
    {
        if (_playerLightObject == null)
        {
            _playerLightObject = GameObject.Find("Player_Runtime_AuraLight");
            if (_playerLightObject == null)
            {
                _playerLightObject = new GameObject("Player_Runtime_AuraLight");
            }
        }

        _playerLightComponent = _playerLightObject.GetComponent<Light>();
        if (_playerLightComponent == null)
        {
            _playerLightComponent = _playerLightObject.AddComponent<Light>();
        }

        _playerLightComponent.type = LightType.Point;
        _playerLightComponent.shadows = LightShadows.None; // 0% Lag
        _playerLightComponent.renderMode = LightRenderMode.ForcePixel;
        _playerLightComponent.intensity = playerLightIntensity;
        _playerLightComponent.range = playerLightRange;
        _playerLightComponent.color = playerLightColor;
        _playerLightComponent.enabled = enablePlayerLight;
    }

    private void UpdatePlayerLightPosition()
    {
        if (!enablePlayerLight || _playerLightObject == null) return;

        // Tìm kiếm player nếu chưa có target
        if (_targetPlayerTransform == null)
        {
            _searchPlayerTimer += Time.deltaTime;
            if (_searchPlayerTimer > 1.0f)
            {
                _searchPlayerTimer = 0f;
                FindPlayerTarget();
            }
        }

        if (_targetPlayerTransform != null)
        {
            _playerLightObject.transform.position = _targetPlayerTransform.position + playerLightOffset;
        }
        else if (Camera.main != null)
        {
            _playerLightObject.transform.position = Camera.main.transform.position;
        }
    }

    private void FindPlayerTarget()
    {
        // 1. Tìm theo Tag Player
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo != null)
        {
            _targetPlayerTransform = playerGo.transform;
            return;
        }

        // 2. Tìm theo các tên nhân vật phổ biến
        string[] candidateNames = new string[] { "Leo", "Maya", "Elena", "Arthur", "SatThu", "Player", "LocalPlayer" };
        foreach (var cName in candidateNames)
        {
            GameObject candidate = GameObject.Find(cName);
            if (candidate != null && candidate.activeInHierarchy)
            {
                _targetPlayerTransform = candidate.transform;
                return;
            }
        }

        // 3. Tìm GameObject có CharacterController hoặc Animator người chơi
        var animators = FindObjectsOfType<Animator>();
        foreach (var anim in animators)
        {
            if (anim.gameObject.name.ToLower().Contains("player") || 
                anim.gameObject.name.ToLower().Contains("leo") ||
                anim.gameObject.name.ToLower().Contains("satthu") ||
                anim.gameObject.name.ToLower().Contains("arthur"))
            {
                _targetPlayerTransform = anim.transform;
                return;
            }
        }
    }

    private void FindPostProcessing()
    {
        _postProcessVolume = FindObjectOfType<PostProcessVolume>();
        if (_postProcessVolume != null && _postProcessVolume.sharedProfile != null)
        {
            _postProcessVolume.sharedProfile.TryGetSettings(out _colorGrading);
        }
    }

    public void ShowToast(string message, float duration = 2.5f)
    {
        _notificationText = message;
        _notificationTimer = duration;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt("Lighting_TVMode", tvBoostMode ? 1 : 0);
        PlayerPrefs.SetFloat("Lighting_Brightness", globalBrightnessMultiplier);
        PlayerPrefs.Save();
    }

    private void LoadSavedSettings()
    {
        if (PlayerPrefs.HasKey("Lighting_TVMode"))
        {
            tvBoostMode = PlayerPrefs.GetInt("Lighting_TVMode") == 1;
        }
        if (PlayerPrefs.HasKey("Lighting_Brightness"))
        {
            globalBrightnessMultiplier = PlayerPrefs.GetFloat("Lighting_Brightness", 1.0f);
        }
    }

    private void OnGUI()
    {
        // Hiển thị thông báo Toast khi bấm phím tắt
        if (_notificationTimer > 0f && showNotifications)
        {
            _notificationTimer -= Time.deltaTime;

            GUIStyle toastStyle = new GUIStyle(GUI.skin.box);
            toastStyle.fontSize = 15;
            toastStyle.fontStyle = FontStyle.Bold;
            toastStyle.alignment = TextAnchor.MiddleCenter;
            toastStyle.normal.textColor = Color.yellow;

            float width = 480;
            float height = 45;
            float x = (Screen.width - width) / 2f;
            float y = 25f;

            GUI.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.88f);
            GUI.Box(new Rect(x, y, width, height), _notificationText, toastStyle);
            GUI.backgroundColor = Color.white;
        }

        // Hiển thị bảng phím tắt trợ giúp (F9)
        if (_showHelpWindow)
        {
            float w = 360;
            float h = 180;
            float px = Screen.width - w - 20;
            float py = 30;

            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            GUILayout.BeginArea(new Rect(px, py, w, h), GUI.skin.window);
            
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("🎮 ĐIỀU CHỈNH ĐỘ SÁNG TRÌNH CHIẾU", headerStyle);
            GUILayout.Space(5);
            GUILayout.Label("• [F7] hoặc [ : Bật/Tắt Chế Độ TV (Siêu Sáng)");
            GUILayout.Label("• [F8] hoặc ] : Tăng độ sáng (+10%)");
            GUILayout.Label("• [F6]        : Giảm độ sáng (-10%)");
            GUILayout.Label($"• Trạng thái TV Mode: {(tvBoostMode ? "ĐANG BẬT" : "TẮT")}");
            GUILayout.Label($"• Độ sáng hiện tại: {Mathf.RoundToInt(globalBrightnessMultiplier * 100)}%");
            GUILayout.Space(5);
            if (GUILayout.Button("Đóng Hướng Dẫn (F9)"))
            {
                _showHelpWindow = false;
            }

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }

    private void OnDestroy()
    {
        if (_playerLightObject != null)
        {
            Destroy(_playerLightObject);
        }
    }
}
