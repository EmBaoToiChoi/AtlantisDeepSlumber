using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class FPSPingDisplay : MonoBehaviour
{
    private static FPSPingDisplay _instance;

    public enum DisplayMode
    {
        Detailed,   // Hiển thị đầy đủ thông số (FPS, Ping, Jitter, Mức báo động, Độ ổn định)
        Compact,    // Hiển thị rút gọn (FPS, Ping & Icon báo động)
        Hidden      // Ẩn tạm thời
    }

    [Header("Display Settings")]
    [SerializeField] private DisplayMode _currentMode = DisplayMode.Detailed;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F3; // Phím F3 để đổi chế độ

    private float _fps = 0f;
    private float _deltaTime = 0f;
    private ulong _currentPing = 0;
    private float _avgPing = 0f;
    private float _jitter = 0f;
    private float _stabilityPercent = 100f;
    private float _pingUpdateTimer = 0f;

    private readonly List<ulong> _pingHistory = new List<ulong>();
    private const int MaxPingSamples = 10;

    private Texture2D _bgTexture;
    private Texture2D _warningBgTexture;
    private GUIStyle _panelStyle;
    private GUIStyle _warningBannerStyle;
    private bool _blinkState = false;
    private float _blinkTimer = 0f;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CreateBackgroundTextures();
    }

    private void OnDestroy()
    {
        if (_bgTexture != null) Destroy(_bgTexture);
        if (_warningBgTexture != null) Destroy(_warningBgTexture);
    }

    private void CreateBackgroundTextures()
    {
        _bgTexture = new Texture2D(1, 1);
        _bgTexture.SetPixel(0, 0, new Color(0.01f, 0.03f, 0.06f, 0.75f));
        _bgTexture.Apply();

        _warningBgTexture = new Texture2D(1, 1);
        _warningBgTexture.SetPixel(0, 0, new Color(0.6f, 0.05f, 0.05f, 0.85f));
        _warningBgTexture.Apply();
    }

    private void Update()
    {
        // 1. Phím tắt F3 chuyển đổi giữa các chế độ hiển thị
        if (Input.GetKeyDown(_toggleKey))
        {
            if (_currentMode == DisplayMode.Detailed) _currentMode = DisplayMode.Compact;
            else if (_currentMode == DisplayMode.Compact) _currentMode = DisplayMode.Hidden;
            else _currentMode = DisplayMode.Detailed;
        }

        // 2. Tính FPS mượt mà
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        _fps = _deltaTime > 0f ? (1.0f / _deltaTime) : 0f;

        // 3. Chu kỳ cập nhật Ping & Độ ổn định (mỗi 0.8 giây)
        _pingUpdateTimer += Time.unscaledDeltaTime;
        if (_pingUpdateTimer >= 0.8f)
        {
            _pingUpdateTimer = 0f;
            UpdatePingAndStability();
        }

        // 4. Nhấp nháy cảnh báo khi Báo động đỏ
        _blinkTimer += Time.unscaledDeltaTime;
        if (_blinkTimer >= 0.4f)
        {
            _blinkTimer = 0f;
            _blinkState = !_blinkState;
        }
    }

    private void UpdatePingAndStability()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
            {
                // Dedicated Server
                _currentPing = 0;
            }
            else if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsClient)
            {
                // Host máy chủ nội bộ
                _currentPing = 0;
            }
            else if (NetworkManager.Singleton.IsClient)
            {
                // Client kết nối tới Server
                try
                {
                    _currentPing = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(0);
                }
                catch
                {
                    _currentPing = 0;
                }
            }
        }
        else
        {
            _currentPing = 0;
        }

        // Lưu lịch sử mẫu ping để tính Average & Jitter & Độ ổn định
        _pingHistory.Add(_currentPing);
        if (_pingHistory.Count > MaxPingSamples)
        {
            _pingHistory.RemoveAt(0);
        }

        // Tính Average Ping
        float sum = 0f;
        foreach (ulong p in _pingHistory) sum += p;
        _avgPing = _pingHistory.Count > 0 ? sum / _pingHistory.Count : _currentPing;

        // Tính Jitter (độ lệch trung bình giữa các lần ping liên tiếp)
        if (_pingHistory.Count > 1)
        {
            float jitterSum = 0f;
            for (int i = 1; i < _pingHistory.Count; i++)
            {
                jitterSum += Mathf.Abs((long)_pingHistory[i] - (long)_pingHistory[i - 1]);
            }
            _jitter = jitterSum / (_pingHistory.Count - 1);
        }
        else
        {
            _jitter = 0f;
        }

        // Tính % Độ ổn định (Jitter càng thấp, Ping càng ổn định thì % càng cao)
        float penalty = (_jitter * 1.5f) + (_avgPing > 100f ? (_avgPing - 100f) * 0.2f : 0f);
        _stabilityPercent = Mathf.Clamp(100f - penalty, 10f, 100f);
    }

    private void OnGUI()
    {
        if (_currentMode == DisplayMode.Hidden) return;

        InitStyles();

        string networkRole = "OFFLINE";
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsClient) networkRole = "HOST";
            else if (NetworkManager.Singleton.IsServer) networkRole = "SERVER";
            else if (NetworkManager.Singleton.IsClient) networkRole = "CLIENT";
        }

        bool isClient = networkRole == "CLIENT";

        // Xác định Mức Báo Động & Màu sắc
        string alertLevelText;
        string pingColorHex;
        string stabilityText;
        string stabilityColorHex;

        if (!isClient)
        {
            alertLevelText = "<color=#00FF88>🟢 TỐT (MÁY CHỦ/HOST)</color>";
            pingColorHex = "#00FF88";
            stabilityText = "<color=#00FF88>100% (Hoàn hảo)</color>";
        }
        else if (_currentPing < 60)
        {
            alertLevelText = "<color=#00FF88>🟢 MỨC 1: TỐT (MƯỢT MÀ)</color>";
            pingColorHex = "#00FF88";
            stabilityColorHex = _stabilityPercent >= 85f ? "#00FF88" : "#FFDD00";
            stabilityText = $"<color={stabilityColorHex}>{_stabilityPercent:F0}% (Rất ổn định)</color>";
        }
        else if (_currentPing <= 120)
        {
            alertLevelText = "<color=#FFDD00>🟡 MỨC 2: BÌNH THƯỜNG</color>";
            pingColorHex = "#FFDD00";
            stabilityColorHex = _stabilityPercent >= 70f ? "#FFDD00" : "#FF7700";
            stabilityText = $"<color={stabilityColorHex}>{_stabilityPercent:F0}% (Ổn định)</color>";
        }
        else if (_currentPing <= 200)
        {
            alertLevelText = "<color=#FF7700>🟠 MỨC 3: CẢNH BÁO (LAG NHẸ)</color>";
            pingColorHex = "#FF7700";
            stabilityColorHex = "#FF7700";
            stabilityText = $"<color={stabilityColorHex}>{_stabilityPercent:F0}% (Trung bình / Trễ)</color>";
        }
        else
        {
            string redTag = _blinkState ? "<color=#FF3344>" : "<color=#FFAAAA>";
            alertLevelText = $"{redTag}🔴 MỨC 4: BÁO ĐỘNG ĐỎ (LAG NẶNG)</color>";
            pingColorHex = "#FF3344";
            stabilityText = $"<color=#FF3344>{_stabilityPercent:F0}% (Kém / Không ổn định)</color>";
        }

        // ─── VẼ GIAO DIỆN THEO CHẾ ĐỘ ───────────────────────────────────────

        if (_currentMode == DisplayMode.Compact)
        {
            // CHẾ ĐỘ RÚT GỌN (Compact): Góc trên bên trái nhỏ gọn
            Rect compactRect = new Rect(10, 10, 200, 48);
            string fpsColor = _fps >= 50 ? "#00FF88" : _fps >= 30 ? "#FFDD00" : "#FF3344";
            string pingVal = isClient ? $"{_currentPing} ms" : "0 ms";

            string content = $"FPS: <color={fpsColor}>{Mathf.RoundToInt(_fps)}</color> | PING: <color={pingColorHex}>{pingVal}</color>\n" +
                             $"{alertLevelText}";

            GUI.Box(compactRect, content, _panelStyle);
        }
        else if (_currentMode == DisplayMode.Detailed)
        {
            // CHẾ ĐỘ CHI TIẾT (Detailed): Hiển thị đầy đủ thông số
            Rect detailedRect = new Rect(10, 10, 290, 115);
            string fpsColor = _fps >= 50 ? "#00FF88" : _fps >= 30 ? "#FFDD00" : "#FF3344";
            string pingVal = isClient ? $"{_currentPing} ms (Avg: {_avgPing:F0}ms)" : "0 ms (Host)";

            string content =
                $"<b>ATLANTIS NETWORK MONITOR</b> <color=#718da5>[F3 Đổi]</color>\n" +
                $"• FPS: <color={fpsColor}><b>{Mathf.RoundToInt(_fps)}</b></color>  |  ROLE: <color=#00e8ff>{networkRole}</color>\n" +
                $"• PING: <color={pingColorHex}><b>{pingVal}</b></color> (Jitter: {_jitter:F0}ms)\n" +
                $"• MỨC ĐỘ ỔN ĐỊNH: {stabilityText}\n" +
                $"• BÁO ĐỘNG: {alertLevelText}";

            GUI.Box(detailedRect, content, _panelStyle);
        }

        // ─── CẢNH BÁO NỔI BẬT TRÊN ĐẦU MÀN HÌNH NẾU BÁO ĐỘNG ĐỎ (>200ms) ───
        if (isClient && _currentPing > 200 && _blinkState)
        {
            float bannerWidth = 420;
            float bannerHeight = 32;
            Rect bannerRect = new Rect((Screen.width - bannerWidth) / 2f, 12, bannerWidth, bannerHeight);
            GUI.Box(bannerRect, $"⚠ <b>CẢNH BÁO MẠNG: ĐỘ TRỄ CAO ({_currentPing} ms)</b> - HÀNH ĐỘNG CÓ THỂ BỊ DELAY", _warningBannerStyle);
        }
    }

    private void InitStyles()
    {
        if (_panelStyle == null)
        {
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
            _panelStyle.normal.textColor = Color.white;
            _panelStyle.normal.background = _bgTexture;
        }

        if (_warningBannerStyle == null)
        {
            _warningBannerStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                richText = true
            };
            _warningBannerStyle.normal.textColor = Color.yellow;
            _warningBannerStyle.normal.background = _warningBgTexture;
        }
    }
}
