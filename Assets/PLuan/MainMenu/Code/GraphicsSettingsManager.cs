using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_2019_3_OR_NEWER
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// Lưu trữ + áp dụng toàn bộ cài đặt đồ họa của người chơi vào Unity runtime.
/// Dùng PlayerPrefs để setting tồn tại giữa các lần khởi động (đặc biệt cho build Windows).
/// Gọi GraphicsSettingsManager.ApplyAll() ngay khi game khởi động (NetworkBootstrap.Awake)
/// và GraphicsSettingsManager.SaveAndApply(snapshot) khi người chơi bấm ÁP DỤNG ở Main Menu.
/// </summary>
public static class GraphicsSettingsManager
{
    // ---------- PlayerPrefs Keys ----------
    public const string KEY_RESOLUTION = "Gfx.Resolution";   // "1920x1080 (FHD)"
    public const string KEY_QUALITY    = "Gfx.Quality";      // Low / Medium / High / Ultra
    public const string KEY_FULLSCREEN = "Gfx.Fullscreen";   // 0/1
    public const string KEY_VSYNC      = "Gfx.VSync";        // 0/1
    public const string KEY_FPS_CAP    = "Gfx.FpsCap";       // -1 / 30 / 60 / 120 / 144
    public const string KEY_AA         = "Gfx.AntiAliasing"; // 0 / 2 / 4 / 8
    public const string KEY_SHADOWS    = "Gfx.Shadows";      // Off / Low / Medium / High
    public const string KEY_POSTFX     = "Gfx.PostFX";       // 0/1
    public const string KEY_PARTICLES  = "Gfx.Particles";    // Low / Medium / High

    // ---------- Default Values ----------
    public const string DEFAULT_RESOLUTION = "1920x1080 (FHD)";
    public const string DEFAULT_QUALITY    = "High";
    public const bool   DEFAULT_FULLSCREEN = true;
    public const bool   DEFAULT_VSYNC      = true;
    public const int    DEFAULT_FPS_CAP    = -1;   // -1 = Unlimited
    public const int    DEFAULT_AA         = 0;    // Off
    public const string DEFAULT_SHADOWS    = "High";
    public const bool   DEFAULT_POSTFX     = true;
    public const string DEFAULT_PARTICLES  = "High";

    // ---------- Snapshot ----------
    public struct GraphicsSnapshot
    {
        public string Resolution;
        public string Quality;
        public bool   Fullscreen;
        public bool   VSync;
        public int    FpsCap;
        public int    AntiAliasing;
        public string Shadows;
        public bool   PostFX;
        public string Particles;

        public static GraphicsSnapshot Default => new GraphicsSnapshot
        {
            Resolution   = DEFAULT_RESOLUTION,
            Quality      = DEFAULT_QUALITY,
            Fullscreen   = DEFAULT_FULLSCREEN,
            VSync        = DEFAULT_VSYNC,
            FpsCap       = DEFAULT_FPS_CAP,
            AntiAliasing = DEFAULT_AA,
            Shadows      = DEFAULT_SHADOWS,
            PostFX       = DEFAULT_POSTFX,
            Particles    = DEFAULT_PARTICLES,
        };
    }

    /// <summary>Snapshot hiện tại trong bộ nhớ (đồng bộ với PlayerPrefs sau lần Save/Apply gần nhất).</summary>
    public static GraphicsSnapshot Current { get; private set; } = LoadFromPrefs();

    // =========================================================================
    //  PUBLIC API
    // =========================================================================

    /// <summary>Áp dụng snapshot hiện tại (đọc từ PlayerPrefs) vào Unity runtime.</summary>
    public static void ApplyAll()
    {
        Current = LoadFromPrefs();
        Apply(Current);
    }

    /// <summary>Lưu snapshot vào PlayerPrefs rồi áp dụng luôn.</summary>
    public static void SaveAndApply(GraphicsSnapshot s)
    {
        SaveToPrefs(s);
        Current = s;
        Apply(s);
    }

    /// <summary>Đồng bộ PlayerPrefs về default rồi áp dụng.</summary>
    public static void ResetToDefault()
    {
        SaveAndApply(GraphicsSnapshot.Default);
    }

    // =========================================================================
    //  APPLY (Runtime)
    // =========================================================================

    private static void Apply(GraphicsSnapshot s)
    {
        // --- Quality Level (phải set trước để các field khác override đúng cấp) ---
        int qualityIndex = ResolveQualityIndex(s.Quality);
        if (qualityIndex >= 0 && qualityIndex < QualitySettings.names.Length)
        {
            QualitySettings.SetQualityLevel(qualityIndex, true);
        }

        // --- V-Sync ---
        QualitySettings.vSyncCount = s.VSync ? 1 : 0;

        // --- FPS Cap ---
        // Khi V-Sync bật, để Application.targetFrameRate = -1 để engine dùng refresh rate.
        // Khi V-Sync tắt, giới hạn theo lựa chọn (-1 = unlimited).
        Application.targetFrameRate = s.VSync ? -1 : Mathf.Max(-1, s.FpsCap);

        // --- Anti-Aliasing (built-in + MSAA sample count trên URP asset) ---
        int aa = ClampAA(s.AntiAliasing);
        QualitySettings.antiAliasing = aa;
#if UNITY_2019_3_OR_NEWER
        TryApplyUrpMsaa(aa);
#endif

        // --- Shadows ---
        ApplyShadows(s.Shadows);

        // --- Particle Quality (ảnh hưởng soft particles + max particle count) ---
        ApplyParticleQuality(s.Particles, s.Quality);

        // --- Post-Processing ---
        ApplyPostFX(s.PostFX);

        // --- Resolution + Fullscreen ---
        ParseResolution(s.Resolution, out int w, out int h);
        if (w > 0 && h > 0)
        {
            FullScreenMode mode = s.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(w, h, mode);
        }

        Debug.Log($"[GraphicsSettings] Applied: Res={s.Resolution} | Quality={s.Quality}(idx {qualityIndex}) | FS={s.Fullscreen} | VSync={s.VSync} | FPS={s.FpsCap} | AA={aa} | Shadows={s.Shadows} | PostFX={s.PostFX} | Particles={s.Particles}");
    }

    private static int ResolveQualityIndex(string qualityLabel)
    {
        // Map theo tên hiển thị tương ứng các mức Unity Quality.
        // names[0] = "Low", ..., names[last] = "Ultra" (tùy project).
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0) return 0;

        switch (qualityLabel)
        {
            case "Ultra (Cinematic)": return names.Length - 1;
            case "High":              return Mathf.Max(0, names.Length - 2);
            case "Medium":            return Mathf.Max(0, names.Length / 2);
            case "Low (Performance)": return 0;
            default:                  // thử tìm nhãn khớp trực tiếp
                for (int i = 0; i < names.Length; i++)
                    if (names[i] == qualityLabel) return i;
                return 0;
        }
    }

    private static int ClampAA(int requested)
    {
        if (requested <= 0) return 0;
        if (requested <= 2) return 2;
        if (requested <= 4) return 4;
        return 8;
    }

#if UNITY_2019_3_OR_NEWER
    private static void TryApplyUrpMsaa(int sampleCount)
    {
        try
        {
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null) return;
            // MsaaQuality: 0=Off, 1=2x, 2=4x, 3=8x
            int quality = sampleCount switch
            {
                2 => 1,
                4 => 2,
                8 => 3,
                _ => 0,
            };
            rp.msaaSampleCount = sampleCount;
            // (best-effort; thuộc tính cũ của URP)
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GraphicsSettings] Could not set URP MSAA: {e.Message}");
        }
    }
#endif

    private static void ApplyShadows(string shadows)
    {
        switch (shadows)
        {
            case "Off":
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                break;
            case "Low":
                QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
                break;
            case "Medium":
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
                QualitySettings.shadowDistance = 40f;
                break;
            case "High":
            default:
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
                QualitySettings.shadowDistance = 80f;
                break;
        }
    }

    private static void ApplyParticleQuality(string particles, string quality)
    {
        // Soft particles: bật ở High/Medium, tắt ở Low.
        QualitySettings.softParticles = particles != "Low";

        // Tăng/giảm max particle count tỉ lệ với quality tổng.
        int multiplier = particles switch
        {
            "High"   => 2,
            "Medium" => 1,
            _        => 0, // Low
        };
        // Áp vào quality level hiện tại (best-effort: Unity không cho ghi maxParticles trực tiếp,
        // nhưng softParticles + LOD bias là phần người dùng cảm nhận rõ nhất).
        QualitySettings.lodBias = quality switch
        {
            "Ultra (Cinematic)" => 2f,
            "High"              => 1.5f,
            "Medium"            => 1f,
            _                   => 0.7f,
        } * (multiplier > 0 ? 1f : 0.85f);
    }

    private static void ApplyPostFX(bool enabled)
    {
        // Unity tích hợp: không có global toggle; ta lưu giá trị vào PlayerPrefs đã đủ.
        // Nếu project dùng URP Volume, designer có thể bind thêm ở đây (set Global Volume profile weight).
        // Best-effort: tìm Camera.main và tắt/bật component Behaviour có "Post" trong tên.
        var cam = Camera.main;
        if (cam == null) return;
        foreach (var b in cam.GetComponents<Behaviour>())
        {
            if (b == null) continue;
            string n = b.GetType().Name;
            if (n.Contains("Post") || n.Contains("Bloom") || n.Contains("Vignette") || n.Contains("Tonemapping"))
            {
                b.enabled = enabled;
            }
        }
    }

    private static void ParseResolution(string resStr, out int width, out int height)
    {
        width = height = 0;
        if (string.IsNullOrEmpty(resStr)) return;
        int spaceIdx = resStr.IndexOf(' ');
        string pair = spaceIdx > 0 ? resStr.Substring(0, spaceIdx) : resStr;
        var parts = pair.Split('x');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out width) &&
            int.TryParse(parts[1], out height))
        {
            return;
        }
        // fallback
        width = 1920; height = 1080;
    }

    // =========================================================================
    //  PLAYERPREFS I/O
    // =========================================================================

    private static GraphicsSnapshot LoadFromPrefs()
    {
        return new GraphicsSnapshot
        {
            Resolution   = PlayerPrefs.GetString(KEY_RESOLUTION, DEFAULT_RESOLUTION),
            Quality      = PlayerPrefs.GetString(KEY_QUALITY,    DEFAULT_QUALITY),
            Fullscreen   = PlayerPrefs.GetInt   (KEY_FULLSCREEN, DEFAULT_FULLSCREEN ? 1 : 0) == 1,
            VSync        = PlayerPrefs.GetInt   (KEY_VSYNC,      DEFAULT_VSYNC      ? 1 : 0) == 1,
            FpsCap       = PlayerPrefs.GetInt   (KEY_FPS_CAP,    DEFAULT_FPS_CAP),
            AntiAliasing = PlayerPrefs.GetInt   (KEY_AA,         DEFAULT_AA),
            Shadows      = PlayerPrefs.GetString(KEY_SHADOWS,    DEFAULT_SHADOWS),
            PostFX       = PlayerPrefs.GetInt   (KEY_POSTFX,     DEFAULT_POSTFX     ? 1 : 0) == 1,
            Particles    = PlayerPrefs.GetString(KEY_PARTICLES,  DEFAULT_PARTICLES),
        };
    }

    private static void SaveToPrefs(GraphicsSnapshot s)
    {
        PlayerPrefs.SetString(KEY_RESOLUTION, s.Resolution);
        PlayerPrefs.SetString(KEY_QUALITY,    s.Quality);
        PlayerPrefs.SetInt   (KEY_FULLSCREEN, s.Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt   (KEY_VSYNC,      s.VSync      ? 1 : 0);
        PlayerPrefs.SetInt   (KEY_FPS_CAP,    s.FpsCap);
        PlayerPrefs.SetInt   (KEY_AA,         s.AntiAliasing);
        PlayerPrefs.SetString(KEY_SHADOWS,    s.Shadows);
        PlayerPrefs.SetInt   (KEY_POSTFX,     s.PostFX ? 1 : 0);
        PlayerPrefs.SetString(KEY_PARTICLES,  s.Particles);
        PlayerPrefs.Save();
    }

    // =========================================================================
    //  MAPPING HELPERS (dùng cho UI)
    // =========================================================================

    public static int AaToIndex(int sample) => sample switch
    {
        2 => 1,
        4 => 2,
        8 => 3,
        _ => 0,
    };

    public static int AaFromIndex(int idx) => idx switch
    {
        1 => 2,
        2 => 4,
        3 => 8,
        _ => 0,
    };

    public static int ShadowToIndex(string s) => s switch
    {
        "Off"    => 0,
        "Low"    => 1,
        "Medium" => 2,
        _        => 3, // High
    };

    public static string ShadowFromIndex(int idx) => idx switch
    {
        0 => "Off",
        1 => "Low",
        2 => "Medium",
        _ => "High",
    };

    public static int ParticleToIndex(string s) => s switch
    {
        "Low"    => 0,
        "Medium" => 1,
        _        => 2, // High
    };

    public static string ParticleFromIndex(int idx) => idx switch
    {
        0 => "Low",
        1 => "Medium",
        _ => "High",
    };

    public static int FpsCapToIndex(int v) => v switch
    {
        30  => 1,
        60  => 2,
        120 => 3,
        144 => 4,
        -1  => 5,
        _   => 0, // 0 = 30 default; 0 cũng hợp lệ với unlimited tuỳ cách map
    };

    public static int FpsCapFromIndex(int idx) => idx switch
    {
        1 => 30,
        2 => 60,
        3 => 120,
        4 => 144,
        5 => -1,
        _ => -1,
    };
}
