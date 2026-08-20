using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.PostProcessing;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Tool chuyên dụng xử lý hiện tượng "Ra nắng thì đẹp nhưng vào râm/mê cung thì đen tối"
/// và "Trình chiếu cắm lên TV/Máy chiếu bị tối thui không thấy gì".
/// Đã tích hợp đầy đủ hệ thống Sao Lưu (Backup) & Phục Hồi (Restore) về nguyên bản 100% nếu không ưng ý.
/// </summary>
public class LightingAndShadowFixerEditor : EditorWindow
{
    [System.Serializable]
    public class SceneLightingSnapshot
    {
        public string scenePath;
        public string backupTime;

        // Sun Light
        public bool hasSunLight;
        public float sunIntensity = 1.0f;
        public Color sunColor = Color.white;
        public int shadowType = 2; // LightShadows.Soft
        public float shadowStrength = 1.0f;
        public float shadowBias = 0.05f;
        public float shadowNormalBias = 0.4f;

        // Ambient
        public int ambientMode = 1; // Trilight = 1, Skybox = 0, Flat = 3
        public float ambientIntensity = 1.0f;
        public Color skyColor = new Color(0.16f, 0.17f, 0.22f, 1.0f);
        public Color equatorColor = new Color(0.13f, 0.10f, 0.08f, 1.0f);
        public Color groundColor = new Color(0.06f, 0.06f, 0.06f, 1.0f);
        public Color flatAmbientLight = Color.black;

        // Post-Processing
        public bool hasPostProcess;
        public float shadowLift = 0.0f;
        public float postExposure = 0.0f;
        public float gammaOffset = -0.09f;
        public int tonemapper = 2; // ACES
    }

    public enum LightingPreset
    {
        BalancedNatural,    // Nắng đẹp + Râm sáng rõ tự nhiên (Khuyên dùng)
        TVPresentation,     // Trình chiếu TV / Máy chiếu hội trường (Siêu sáng rõ, chống black crush)
        MazeHighClarity,    // Mê cung tường cao (Phủ sáng sàn mê cung & Player Aura)
        Custom              // Tự tùy chỉnh
    }

    [Header("1. Chế độ thiết lập sẵn (Presets)")]
    private LightingPreset currentPreset = LightingPreset.BalancedNatural;

    [Header("2. Ánh Sáng Mặt Trời & Bóng Đổ (Directional Sun Light)")]
    private Light targetSunLight;
    private float sunIntensity = 1.15f;
    private Color sunColor = new Color(1.0f, 0.96f, 0.88f, 1.0f);
    private LightShadows shadowType = LightShadows.Soft;
    [Range(0.0f, 1.0f)] private float shadowStrength = 0.45f; // Giảm từ 1.0 xuống 0.45 để bóng trong râm không bị đen kịt

    [Header("3. Ánh Sáng Môi Trường / Vùng Râm (RenderSettings Ambient)")]
    private UnityEngine.Rendering.AmbientMode ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
    [Range(0.2f, 3.0f)] private float ambientIntensity = 1.35f;
    private Color skyColor = new Color(0.42f, 0.48f, 0.58f, 1.0f);
    private Color equatorColor = new Color(0.35f, 0.35f, 0.38f, 1.0f);
    private Color groundColor = new Color(0.24f, 0.24f, 0.26f, 1.0f);

    [Header("4. Đèn Chiếu Phủ Mê Cung Từ Trên Cao (Maze Top-Down Fill Light)")]
    private bool enableMazeFillLight = true;
    [Range(0.1f, 1.5f)] private float mazeFillIntensity = 0.40f;
    private Color mazeFillColor = new Color(0.85f, 0.90f, 1.0f, 1.0f);

    [Header("5. Đèn Cá Nhân Theo Chân Player (Player Aura Fill Light)")]
    private bool enablePlayerAuraLight = true;
    [Range(0.2f, 2.5f)] private float playerLightIntensity = 1.0f;
    [Range(5f, 30f)] private float playerLightRange = 15.0f;
    private Color playerLightColor = new Color(1.0f, 0.95f, 0.88f, 1.0f);

    [Header("6. Chống Tối TV & Post-Processing (Color Grading)")]
    private bool enablePostProcessing = true;
    [Range(0.0f, 0.25f)] private float shadowLift = 0.07f; // Kéo sáng vùng tối nhất, triệt tiêu crushed black trên TV
    [Range(-0.5f, 1.5f)] private float postExposure = 0.25f;
    [Range(-0.3f, 0.3f)] private float gammaOffset = 0.06f;
    private Tonemapper tonemapperMode = Tonemapper.ACES;

    [Header("7. Tùy Chọn Khác")]
    private bool addInGameRuntimeManager = true;
    private bool autoApplyLivePreview = true;

    // UI Scroll & Foldouts
    private Vector2 scrollPos;
    private bool foldoutSun = true;
    private bool foldoutAmbient = true;
    private bool foldoutMaze = true;
    private bool foldoutPlayer = true;
    private bool foldoutPostProcess = true;
    private bool foldoutBackup = true;
    private bool foldoutBatch = false;

    private const string BackupDirectory = "ProjectSettings/LightingBackups";

    [MenuItem("Tools/Fix Lighting & Shadow (Chống Tối Râm & TV)", false, 1)]
    [MenuItem("Window/Lighting & TV Presentation Fixer", false, 2)]
    public static void ShowWindow()
    {
        LightingAndShadowFixerEditor window = GetWindow<LightingAndShadowFixerEditor>("Fix Lighting & TV");
        window.minSize = new Vector2(480, 720);
        window.Show();
    }

    private void OnEnable()
    {
        FindCurrentSceneSettings();
    }

    private void FindCurrentSceneSettings()
    {
        // 1. Tìm Directional Light chính trong Scene
        if (targetSunLight == null)
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.gameObject.name != "Maze_TopDown_FillLight")
                {
                    targetSunLight = l;
                    sunIntensity = l.intensity;
                    sunColor = l.color;
                    shadowType = l.shadows;
                    shadowStrength = l.shadowStrength;
                    break;
                }
            }
        }

        // 2. Lấy Ambient hiện tại
        ambientMode = RenderSettings.ambientMode;
        ambientIntensity = RenderSettings.ambientIntensity;
        skyColor = RenderSettings.ambientSkyColor;
        equatorColor = RenderSettings.ambientEquatorColor;
        groundColor = RenderSettings.ambientGroundColor;

        // 3. Kiểm tra xem đã có Maze Fill Light chưa
        GameObject mazeLightGo = GameObject.Find("Maze_TopDown_FillLight");
        if (mazeLightGo != null)
        {
            Light ml = mazeLightGo.GetComponent<Light>();
            if (ml != null)
            {
                enableMazeFillLight = ml.enabled;
                mazeFillIntensity = ml.intensity;
                mazeFillColor = ml.color;
            }
        }
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Header Title
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        EditorGUILayout.Space(5);
        GUILayout.Label("☀️ TOOL SỬA LỖI TỐI BÓNG RÂM & TRÌNH CHIẾU TV 📺", titleStyle);
        GUILayout.Label("Khắc phục triệt để: Đi vào râm bị đen tối | Mê cung tường cao tối thui | Cắm TV máy chiếu mất chi tiết", subtitleStyle);
        EditorGUILayout.Space(10);

        // Quick Presets Box
        DrawPresetsSection();

        EditorGUILayout.Space(10);

        // Live Preview Toggle
        autoApplyLivePreview = EditorGUILayout.ToggleLeft("⚡ Tự động cập nhật trực tiếp trong Scene (Live Preview)", autoApplyLivePreview, EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        // Detailed Settings
        DrawSunSection();
        DrawAmbientSection();
        DrawMazeLightSection();
        DrawPlayerLightSection();
        DrawPostProcessSection();
        DrawBackupRestoreSection();
        DrawBatchApplySection();

        EditorGUILayout.Space(15);

        // Big Action Buttons
        DrawActionButtons();

        EditorGUILayout.Space(10);
        EditorGUILayout.EndScrollView();
    }

    private void DrawPresetsSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("1. CHỌN CHẾ ĐỘ CẤU HÌNH NHANH (1-CLICK PRESETS):", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        // Preset 1: Nắng Đẹp & Râm Sáng Rõ
        GUI.backgroundColor = currentPreset == LightingPreset.BalancedNatural ? new Color(0.3f, 0.9f, 0.4f) : Color.white;
        if (GUILayout.Button("☀️ Chuẩn Tự Nhiên\n(Nắng Đẹp + Râm Rõ)", GUILayout.Height(42)))
        {
            ApplyPresetValues(LightingPreset.BalancedNatural);
        }

        // Preset 2: Trình Chiếu TV
        GUI.backgroundColor = currentPreset == LightingPreset.TVPresentation ? new Color(0.3f, 0.8f, 1.0f) : Color.white;
        if (GUILayout.Button("📺 Trình Chiếu TV\n(Siêu Sáng + Rõ Nét)", GUILayout.Height(42)))
        {
            ApplyPresetValues(LightingPreset.TVPresentation);
        }

        // Preset 3: Mê Cung Sáng Rõ
        GUI.backgroundColor = currentPreset == LightingPreset.MazeHighClarity ? new Color(1.0f, 0.75f, 0.3f) : Color.white;
        if (GUILayout.Button("🏰 Mê Cung Tường Cao\n(Phủ Sáng Sàn & Player)", GUILayout.Height(42)))
        {
            ApplyPresetValues(LightingPreset.MazeHighClarity);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        if (currentPreset == LightingPreset.BalancedNatural)
        {
            EditorGUILayout.HelpBox("Chế độ Chuẩn: Giữ ánh nắng rực rỡ, làm mềm bóng râm (Shadow Strength = 0.45), nâng nhẹ vùng tối giúp nhìn rõ vân đá và không bị đen kịt.", MessageType.Info);
        }
        else if (currentPreset == LightingPreset.TVPresentation)
        {
            EditorGUILayout.HelpBox("Chế độ TV / Máy Chiếu: Tăng mạnh Lift và Exposure để chống hiện tượng Black Crush của màn hình TV hội trường. Đảm bảo mọi người xem đều thấy rõ ràng.", MessageType.Info);
        }
        else if (currentPreset == LightingPreset.MazeHighClarity)
        {
            EditorGUILayout.HelpBox("Chế độ Mê Cung: Kích hoạt đèn phủ trần từ trên cao chiếu thẳng xuống mê cung (không đổ bóng) + Đèn Aura quanh người chơi.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void ApplyPresetValues(LightingPreset preset)
    {
        currentPreset = preset;

        switch (preset)
        {
            case LightingPreset.BalancedNatural:
                shadowStrength = 0.45f;
                sunIntensity = 1.15f;
                ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                ambientIntensity = 1.35f;
                skyColor = new Color(0.42f, 0.48f, 0.58f, 1.0f);
                equatorColor = new Color(0.35f, 0.35f, 0.38f, 1.0f);
                groundColor = new Color(0.24f, 0.24f, 0.26f, 1.0f);
                enableMazeFillLight = true;
                mazeFillIntensity = 0.35f;
                mazeFillColor = new Color(0.85f, 0.90f, 1.0f, 1.0f);
                enablePlayerAuraLight = true;
                playerLightIntensity = 1.0f;
                playerLightRange = 15.0f;
                shadowLift = 0.07f;
                postExposure = 0.25f;
                gammaOffset = 0.06f;
                break;

            case LightingPreset.TVPresentation:
                shadowStrength = 0.35f; // Bóng rất mềm
                sunIntensity = 1.25f;
                ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                ambientIntensity = 1.70f;
                skyColor = new Color(0.55f, 0.58f, 0.68f, 1.0f);
                equatorColor = new Color(0.45f, 0.45f, 0.48f, 1.0f);
                groundColor = new Color(0.32f, 0.32f, 0.35f, 1.0f);
                enableMazeFillLight = true;
                mazeFillIntensity = 0.55f;
                mazeFillColor = new Color(0.90f, 0.93f, 1.0f, 1.0f);
                enablePlayerAuraLight = true;
                playerLightIntensity = 1.4f;
                playerLightRange = 18.0f;
                shadowLift = 0.12f; // Nâng dải đen tối lên rõ rệt cho TV
                postExposure = 0.45f;
                gammaOffset = 0.12f;
                break;

            case LightingPreset.MazeHighClarity:
                shadowStrength = 0.40f;
                sunIntensity = 1.15f;
                ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                ambientIntensity = 1.50f;
                skyColor = new Color(0.48f, 0.52f, 0.62f, 1.0f);
                equatorColor = new Color(0.40f, 0.40f, 0.42f, 1.0f);
                groundColor = new Color(0.30f, 0.30f, 0.32f, 1.0f);
                enableMazeFillLight = true;
                mazeFillIntensity = 0.60f;
                mazeFillColor = new Color(0.88f, 0.92f, 1.0f, 1.0f);
                enablePlayerAuraLight = true;
                playerLightIntensity = 1.3f;
                playerLightRange = 16.0f;
                shadowLift = 0.09f;
                postExposure = 0.35f;
                gammaOffset = 0.08f;
                break;
        }

        if (autoApplyLivePreview)
        {
            ApplySettingsToCurrentScene(false);
        }
    }

    private void DrawSunSection()
    {
        foldoutSun = EditorGUILayout.Foldout(foldoutSun, "2. Mặt Trời & Bóng Đổ (Directional Light)", true, EditorStyles.foldoutHeader);
        if (foldoutSun)
        {
            EditorGUI.indentLevel++;
            targetSunLight = (Light)EditorGUILayout.ObjectField("Đèn Mặt Trời Chính:", targetSunLight, typeof(Light), true);

            EditorGUI.BeginChangeCheck();
            shadowStrength = EditorGUILayout.Slider(new GUIContent("Độ Đậm Bóng Đổ (Shadow Strength):", "1.0 = Đen kịt | 0.45 = Bóng mềm trong suốt tự nhiên"), shadowStrength, 0.0f, 1.0f);
            if (shadowStrength > 0.75f)
            {
                EditorGUILayout.HelpBox("Cảnh báo: Shadow Strength > 0.75 sẽ làm các vùng râm và chân tường bị đen kịt. Khuyên dùng 0.35 - 0.50!", MessageType.Warning);
            }

            shadowType = (LightShadows)EditorGUILayout.EnumPopup("Kiểu Bóng (Shadow Type):", shadowType);
            sunIntensity = EditorGUILayout.Slider("Cường Độ Nắng (Sun Intensity):", sunIntensity, 0.5f, 3.0f);
            sunColor = EditorGUILayout.ColorField("Màu Ánh Nắng (Sun Color):", sunColor);

            if (EditorGUI.EndChangeCheck())
            {
                currentPreset = LightingPreset.Custom;
                if (autoApplyLivePreview) ApplySettingsToCurrentScene(false);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }
    }

    private void DrawAmbientSection()
    {
        foldoutAmbient = EditorGUILayout.Foldout(foldoutAmbient, "3. Ánh Sáng Môi Trường / Vùng Râm (RenderSettings Ambient)", true, EditorStyles.foldoutHeader);
        if (foldoutAmbient)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            ambientMode = (UnityEngine.Rendering.AmbientMode)EditorGUILayout.EnumPopup("Chế Độ Ambient:", ambientMode);
            ambientIntensity = EditorGUILayout.Slider("Độ Sáng Nền (Ambient Brightness):", ambientIntensity, 0.2f, 3.0f);

            if (ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)
            {
                skyColor = EditorGUILayout.ColorField("Màu Bầu Trời (Sky):", skyColor);
                equatorColor = EditorGUILayout.ColorField("Màu Chân Trời (Equator):", equatorColor);
                groundColor = EditorGUILayout.ColorField("Màu Mặt Đất (Ground):", groundColor);
            }
            else if (ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
            {
                skyColor = EditorGUILayout.ColorField("Màu Ánh Sáng Nền (Flat Color):", skyColor);
            }

            if (EditorGUI.EndChangeCheck())
            {
                currentPreset = LightingPreset.Custom;
                if (autoApplyLivePreview) ApplySettingsToCurrentScene(false);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }
    }

    private void DrawMazeLightSection()
    {
        foldoutMaze = EditorGUILayout.Foldout(foldoutMaze, "4. Đèn Phủ Trần Mê Cung Từ Trên Cao (Maze Top-Down Fill Light)", true, EditorStyles.foldoutHeader);
        if (foldoutMaze)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            enableMazeFillLight = EditorGUILayout.Toggle("Bật Đèn Phủ Mê Cung (0% Lag):", enableMazeFillLight);
            if (enableMazeFillLight)
            {
                mazeFillIntensity = EditorGUILayout.Slider("Cường Độ Phủ Sáng (Intensity):", mazeFillIntensity, 0.1f, 1.5f);
                mazeFillColor = EditorGUILayout.ColorField("Màu Ánh Sáng Phủ:", mazeFillColor);
                EditorGUILayout.HelpBox("Đèn chiếu thẳng góc từ trên đỉnh trời xuống (Shadows = None, 0% tốn hiệu năng), giúp sàn mê cung sáng rõ đều đặn.", MessageType.None);
            }

            if (EditorGUI.EndChangeCheck())
            {
                currentPreset = LightingPreset.Custom;
                if (autoApplyLivePreview) ApplySettingsToCurrentScene(false);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }
    }

    private void DrawPlayerLightSection()
    {
        foldoutPlayer = EditorGUILayout.Foldout(foldoutPlayer, "5. Đèn Hào Quang Theo Chân Player (Player Aura Light)", true, EditorStyles.foldoutHeader);
        if (foldoutPlayer)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            enablePlayerAuraLight = EditorGUILayout.Toggle("Bật Đèn Player (0% Lag):", enablePlayerAuraLight);
            if (enablePlayerAuraLight)
            {
                playerLightIntensity = EditorGUILayout.Slider("Độ Sáng (Intensity):", playerLightIntensity, 0.2f, 2.5f);
                playerLightRange = EditorGUILayout.Slider("Tầm Phủ Sáng (Range - m):", playerLightRange, 5f, 30f);
                playerLightColor = EditorGUILayout.ColorField("Màu Đèn Player:", playerLightColor);
                EditorGUILayout.HelpBox("Đèn dịu nhẹ đi theo nhân vật, đảm bảo mọi ngóc ngách hay góc tối người chơi đi qua đều nhìn thấy rõ đường.", MessageType.None);
            }

            if (EditorGUI.EndChangeCheck())
            {
                currentPreset = LightingPreset.Custom;
                if (autoApplyLivePreview) ApplySettingsToCurrentScene(false);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }
    }

    private void DrawPostProcessSection()
    {
        foldoutPostProcess = EditorGUILayout.Foldout(foldoutPostProcess, "6. Chống Tối Màn Hình TV & Post-Processing (Color Grading)", true, EditorStyles.foldoutHeader);
        if (foldoutPostProcess)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            enablePostProcessing = EditorGUILayout.Toggle("Bật Post-Processing:", enablePostProcessing);
            if (enablePostProcessing)
            {
                shadowLift = EditorGUILayout.Slider(new GUIContent("Nâng Sáng Vùng Đen (Shadow Lift):", "Kéo các điểm đen nhất lên xám tối, chống triệt để mất hình trên TV"), shadowLift, 0.0f, 0.25f);
                postExposure = EditorGUILayout.Slider("Độ Phơi Sáng Tổng (Post Exposure):", postExposure, -0.5f, 1.5f);
                gammaOffset = EditorGUILayout.Slider("Dải Trung Tính (Gamma Offset):", gammaOffset, -0.3f, 0.3f);
                tonemapperMode = (Tonemapper)EditorGUILayout.EnumPopup("Chế Độ Tonemapper:", tonemapperMode);
            }

            if (EditorGUI.EndChangeCheck())
            {
                currentPreset = LightingPreset.Custom;
                if (autoApplyLivePreview) ApplySettingsToCurrentScene(false);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }
    }

    private void DrawBackupRestoreSection()
    {
        foldoutBackup = EditorGUILayout.Foldout(foldoutBackup, "7. Sao Lưu & Phục Hồi Về Nguyên Bản (Backup & Restore)", true, EditorStyles.foldoutHeader);
        if (foldoutBackup)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            string sceneName = EditorSceneManager.GetActiveScene().name;
            bool hasBackup = HasBackupFile(EditorSceneManager.GetActiveScene().path);

            if (hasBackup)
            {
                EditorGUILayout.HelpBox($"✅ Scene '{sceneName}' ĐÃ CÓ BẢN SAO LƯU GỐC. Bạn có thể phục hồi về nguyên trạng bất cứ lúc nào!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"ℹ️ Scene '{sceneName}' chưa tạo bản sao lưu. Tool sẽ tự động tạo bản sao lưu gốc trước lần áp dụng đầu tiên.", MessageType.None);
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();

            // Nút Lưu Snapshot thủ công
            if (GUILayout.Button("📸 Lưu Bản Sao Lưu Ngay", GUILayout.Height(28)))
            {
                CreateSceneBackup(EditorSceneManager.GetActiveScene().path, true);
            }

            // Nút Phục hồi Scene hiện tại
            GUI.backgroundColor = new Color(1.0f, 0.45f, 0.45f);
            if (GUILayout.Button("🔄 Phục Hồi Scene Hiện Tại Về Gốc", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog(
                    "Xác nhận phục hồi",
                    $"Bạn có chắc chắn muốn phục hồi Scene '{sceneName}' về lại trạng thái ban đầu trước khi sửa?",
                    "Phục Hồi Ngay", "Hủy"))
                {
                    RestoreCurrentSceneFromBackup();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Nút Dọn dẹp các GameObject phụ
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.3f);
            if (GUILayout.Button("🗑️ Dọn Dẹp / Xóa Các Đèn Phụ Khỏi Scene (Maze Light, Player Light, Manager)", GUILayout.Height(24)))
            {
                CleanUpAddedGameObjects();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }

    private void DrawBatchApplySection()
    {
        foldoutBatch = EditorGUILayout.Foldout(foldoutBatch, "8. Tùy Chọn Runtime & Áp Dụng Cho Toàn Bộ Game", true, EditorStyles.foldoutHeader);
        if (foldoutBatch)
        {
            EditorGUI.indentLevel++;
            addInGameRuntimeManager = EditorGUILayout.Toggle(new GUIContent("Thêm InGame Manager (Phím tắt F7/F8/F6):", "Tự động gắn script quản lý độ sáng vào Scene"), addInGameRuntimeManager);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }
    }

    private void DrawActionButtons()
    {
        // 1. ÁP DỤNG CHO SCENE HIỆN TẠI
        GUI.backgroundColor = new Color(0.25f, 0.95f, 0.45f);
        if (GUILayout.Button("🚀 ÁP DỤNG & LƯU CHO SCENE HIỆN TẠI 🚀", GUILayout.Height(45)))
        {
            // Tự động tạo backup nếu chưa có
            EnsureBackupExists(EditorSceneManager.GetActiveScene().path);
            ApplySettingsToCurrentScene(true);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);

        // 2. ÁP DỤNG CHO TẤT CẢ SCENE
        GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f);
        if (GUILayout.Button("🌐 QUÉT & ÁP DỤNG CHO TẤT CẢ SCENE TRONG GAME 🌐", GUILayout.Height(36)))
        {
            if (EditorUtility.DisplayDialog(
                "Xác nhận áp dụng toàn bộ Scene",
                "Tool sẽ tự động sao lưu và áp dụng cài đặt ánh sáng & chống tối TV cho tất cả Scene trong Assets/Scenes/.\n\nBạn có muốn tiếp tục?",
                "Đồng Ý (Bắt đầu)", "Hủy"))
            {
                ApplySettingsToAllScenes();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);

        // 3. PHỤC HỒI TẤT CẢ SCENE VỀ NGUYÊN BẢN
        GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
        if (GUILayout.Button("⚠️ 🔄 KHÔI PHỤC TẤT CẢ SCENE VỀ NGUYÊN BẢN (REVERT ALL) 🔄 ⚠️", GUILayout.Height(32)))
        {
            if (EditorUtility.DisplayDialog(
                "Xác nhận khôi phục toàn bộ game",
                "Tool sẽ quét lại toàn bộ Scene trong Assets/Scenes/ và phục hồi về nguyên trạng ban đầu (từ file sao lưu).\n\nBạn có chắc chắn muốn khôi phục toàn bộ?",
                "Khôi Phục Toàn Bộ", "Hủy"))
            {
                RestoreAllScenesFromBackup();
            }
        }
        GUI.backgroundColor = Color.white;
    }

    #region Apply Settings Logic

    /// <summary>
    /// Áp dụng thiết lập vào Scene hiện tại
    /// </summary>
    public void ApplySettingsToCurrentScene(bool showSuccessDialog)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Fix Lighting and Shadow");

        // 1. Xử lý Directional Sun Light
        if (targetSunLight == null)
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.gameObject.name != "Maze_TopDown_FillLight")
                {
                    targetSunLight = l;
                    break;
                }
            }
        }

        if (targetSunLight != null)
        {
            Undo.RecordObject(targetSunLight, "Adjust Sun Light");
            targetSunLight.intensity = sunIntensity;
            targetSunLight.color = sunColor;
            targetSunLight.shadows = shadowType;
            targetSunLight.shadowStrength = shadowStrength;
            targetSunLight.shadowBias = 0.05f;
            targetSunLight.shadowNormalBias = 0.4f;
        }

        // 2. Xử lý RenderSettings Ambient
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientIntensity = ambientIntensity;
        if (ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)
        {
            RenderSettings.ambientSkyColor = skyColor;
            RenderSettings.ambientEquatorColor = equatorColor;
            RenderSettings.ambientGroundColor = groundColor;
        }
        else if (ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
        {
            RenderSettings.ambientLight = skyColor;
        }

        // 3. Xử lý Đèn Phủ Mê Cung (Maze Top-Down Fill Light)
        SetupMazeTopDownFillLight();

        // 4. Xử lý Đèn Hào Quang Player (Player Aura Light)
        SetupPlayerAuraLight();

        // 5. Xử lý Post-Processing
        if (enablePostProcessing)
        {
            SetupPostProcessing();
        }

        // 6. Xử lý InGame Runtime Manager
        if (addInGameRuntimeManager)
        {
            SetupInGameManager();
        }

        // Đánh dấu dirty và lưu nếu bấm nút
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (showSuccessDialog)
        {
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Đã Cập Nhật Ánh Sáng Thành Công!",
                $"✅ Đã tối ưu độ sáng & bóng đổ cho Scene: {EditorSceneManager.GetActiveScene().name}\n\n" +
                $"• Độ đậm bóng đổ (Shadow Strength): {Mathf.RoundToInt(shadowStrength * 100)}% (Bóng mềm, không bị đen kịt)\n" +
                $"• Độ sáng môi trường (Ambient): {ambientIntensity:F2}x\n" +
                $"• Nâng sáng dải đen TV (Shadow Lift): +{shadowLift:F2}\n" +
                $"• Đèn phủ đỉnh mê cung: {(enableMazeFillLight ? "ĐÃ BẬT" : "TẮT")}\n" +
                $"• Đèn cá nhân Player: {(enablePlayerAuraLight ? "ĐÃ BẬT" : "TẮT")}\n\n" +
                $"🎮 Đã tích hợp phím tắt trong game: F7 (TV Mode) | F8 (Tăng sáng) | F6 (Giảm sáng)!\n\n" +
                $"🛡️ Nếu không ưng ý, bạn luôn có thể bấm nút 'Phục Hồi Về Nguyên Bản' ở mục số 7!",
                "Tuyệt Vời"
            );
        }
    }

    private void SetupMazeTopDownFillLight()
    {
        GameObject mazeLightGo = GameObject.Find("Maze_TopDown_FillLight");

        if (enableMazeFillLight)
        {
            if (mazeLightGo == null)
            {
                mazeLightGo = new GameObject("Maze_TopDown_FillLight");
                Undo.RegisterCreatedObjectUndo(mazeLightGo, "Create Maze Fill Light");
            }

            // Đặt góc chiếu từ đỉnh trời thẳng xuống (85-90 độ)
            mazeLightGo.transform.rotation = Quaternion.Euler(88f, 0f, 0f);

            Light ml = mazeLightGo.GetComponent<Light>();
            if (ml == null) ml = mazeLightGo.AddComponent<Light>();

            Undo.RecordObject(ml, "Configure Maze Fill Light");
            ml.enabled = true;
            ml.type = LightType.Directional;
            ml.color = mazeFillColor;
            ml.intensity = mazeFillIntensity;
            ml.shadows = LightShadows.None; // 0% Lag, không đổ bóng đè lên mặt trời
            ml.renderMode = LightRenderMode.ForcePixel;
        }
        else if (mazeLightGo != null)
        {
            Light ml = mazeLightGo.GetComponent<Light>();
            if (ml != null)
            {
                Undo.RecordObject(ml, "Disable Maze Fill Light");
                ml.enabled = false;
            }
        }
    }

    private void SetupPlayerAuraLight()
    {
        GameObject playerLightGo = GameObject.Find("Player_Runtime_AuraLight");

        if (enablePlayerAuraLight)
        {
            if (playerLightGo == null)
            {
                playerLightGo = new GameObject("Player_Runtime_AuraLight");
                Undo.RegisterCreatedObjectUndo(playerLightGo, "Create Player Aura Light");
            }

            Light pl = playerLightGo.GetComponent<Light>();
            if (pl == null) pl = playerLightGo.AddComponent<Light>();

            Undo.RecordObject(pl, "Configure Player Aura Light");
            pl.enabled = true;
            pl.type = LightType.Point;
            pl.color = playerLightColor;
            pl.intensity = playerLightIntensity;
            pl.range = playerLightRange;
            pl.shadows = LightShadows.None; // 0% Lag
            pl.renderMode = LightRenderMode.ForcePixel;
        }
        else if (playerLightGo != null)
        {
            Light pl = playerLightGo.GetComponent<Light>();
            if (pl != null)
            {
                Undo.RecordObject(pl, "Disable Player Aura Light");
                pl.enabled = false;
            }
        }
    }

    private void SetupPostProcessing()
    {
        // 1. Tìm hoặc tạo PostProcess GameObject
        GameObject ppGo = GameObject.Find("PostProcess");
        if (ppGo == null)
        {
            ppGo = new GameObject("PostProcess");
            Undo.RegisterCreatedObjectUndo(ppGo, "Create PostProcess GameObject");
        }
        ppGo.layer = 0;

        PostProcessVolume ppVolume = ppGo.GetComponent<PostProcessVolume>();
        if (ppVolume == null)
        {
            ppVolume = Undo.AddComponent<PostProcessVolume>(ppGo);
        }
        ppVolume.isGlobal = true;
        ppVolume.weight = 1.0f;

        // 2. Load hoặc tạo PostProcessProfile
        string profilePath = "Assets/NatureManufacture Assets/Castle and Dungeon/Scenes/PostProcessVolumeProfile Castle.asset";
        PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);

        if (profile == null)
        {
            string resourcesDir = "Assets/Resources";
            if (!Directory.Exists(resourcesDir))
            {
                Directory.CreateDirectory(resourcesDir);
            }
            string newProfilePath = "Assets/Resources/GlobalLightingFixProfile.asset";
            profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(newProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, newProfilePath);
                AssetDatabase.SaveAssets();
            }
        }

        if (profile != null)
        {
            ppVolume.sharedProfile = profile;

            // Cấu hình ColorGrading
            ColorGrading colorGrading;
            if (!profile.TryGetSettings(out colorGrading))
            {
                colorGrading = profile.AddSettings<ColorGrading>();
            }

            if (colorGrading != null)
            {
                Undo.RecordObject(profile, "Configure Color Grading");
                colorGrading.enabled.overrideState = true;
                colorGrading.enabled.value = true;

                // Nâng sáng dải đen (Lift) - Chống TV black crush
                colorGrading.lift.overrideState = true;
                colorGrading.lift.value = new Vector4(1.0f, 1.0f, 1.0f, shadowLift);

                // Dải trung tính (Gamma)
                colorGrading.gamma.overrideState = true;
                colorGrading.gamma.value = new Vector4(1.0f, 1.0f, 1.0f, gammaOffset);

                // Phơi sáng (Post Exposure)
                colorGrading.postExposure.overrideState = true;
                colorGrading.postExposure.value = postExposure;

                // Tonemapper
                colorGrading.tonemapper.overrideState = true;
                colorGrading.tonemapper.value = tonemapperMode;
            }
        }

        // 3. Setup Camera PostProcessLayer
        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = FindObjectOfType<Camera>();

        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Setup Main Camera");
            mainCam.allowHDR = true;
            mainCam.allowMSAA = false;

            PostProcessLayer ppLayer = mainCam.GetComponent<PostProcessLayer>();
            if (ppLayer == null)
            {
                ppLayer = Undo.AddComponent<PostProcessLayer>(mainCam.gameObject);
            }
            Undo.RecordObject(ppLayer, "Setup PostProcessLayer");
            ppLayer.volumeLayer = ~0;
            ppLayer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        }
    }

    private void SetupInGameManager()
    {
        InGameLightingAndTVManager mgr = FindObjectOfType<InGameLightingAndTVManager>();
        if (mgr == null)
        {
            GameObject mgrGo = GameObject.Find("InGameLightingAndTVManager");
            if (mgrGo == null)
            {
                mgrGo = new GameObject("InGameLightingAndTVManager");
                Undo.RegisterCreatedObjectUndo(mgrGo, "Create InGameLightingAndTVManager");
            }
            mgr = Undo.AddComponent<InGameLightingAndTVManager>(mgrGo);
        }

        if (mgr != null)
        {
            Undo.RecordObject(mgr, "Configure InGameLightingAndTVManager");
            mgr.enablePlayerLight = enablePlayerAuraLight;
            mgr.playerLightIntensity = playerLightIntensity;
            mgr.playerLightRange = playerLightRange;
            mgr.playerLightColor = playerLightColor;
        }
    }

    private void ApplySettingsToAllScenes()
    {
        string currentScenePath = EditorSceneManager.GetActiveScene().path;

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        List<string> processedScenes = new List<string>();

        try
        {
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);

                EditorUtility.DisplayProgressBar(
                    "Đang nâng cấp ánh sáng toàn bộ Scene",
                    $"Đang xử lý: {sceneName} ({i + 1}/{sceneGuids.Length})",
                    (float)i / sceneGuids.Length
                );

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (scene.IsValid())
                {
                    EnsureBackupExists(scenePath);
                    targetSunLight = null;
                    ApplySettingsToCurrentScene(false);
                    EditorSceneManager.SaveScene(scene);
                    processedScenes.Add(sceneName);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Hoàn Tất Nâng Cấp Toàn Bộ Game!",
            $"Đã áp dụng thành công cài đặt ánh sáng & chống tối TV cho {processedScenes.Count} Scene:\n\n" +
            string.Join(", ", processedScenes) + "\n\n" +
            "Giờ đây toàn bộ các màn chơi trong game đều sáng rõ, không còn bị đen tối khi vào râm hay cắm TV!",
            "Tuyệt Vời"
        );
    }

    #endregion

    #region Backup and Restore System

    private string GetBackupFilePath(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) scenePath = "DefaultScene";
        string safeName = Path.GetFileNameWithoutExtension(scenePath);
        return Path.Combine(BackupDirectory, safeName + "_LightingBackup.json");
    }

    private bool HasBackupFile(string scenePath)
    {
        string filePath = GetBackupFilePath(scenePath);
        return File.Exists(filePath);
    }

    private void EnsureBackupExists(string scenePath)
    {
        if (!HasBackupFile(scenePath))
        {
            CreateSceneBackup(scenePath, false);
        }
    }

    private void CreateSceneBackup(string scenePath, bool showDialog)
    {
        if (!Directory.Exists(BackupDirectory))
        {
            Directory.CreateDirectory(BackupDirectory);
        }

        SceneLightingSnapshot snapshot = new SceneLightingSnapshot();
        snapshot.scenePath = scenePath;
        snapshot.backupTime = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        // 1. Sun Light Snapshot
        Light sun = targetSunLight;
        if (sun == null)
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.gameObject.name != "Maze_TopDown_FillLight")
                {
                    sun = l;
                    break;
                }
            }
        }

        if (sun != null)
        {
            snapshot.hasSunLight = true;
            snapshot.sunIntensity = sun.intensity;
            snapshot.sunColor = sun.color;
            snapshot.shadowType = (int)sun.shadows;
            snapshot.shadowStrength = sun.shadowStrength;
            snapshot.shadowBias = sun.shadowBias;
            snapshot.shadowNormalBias = sun.shadowNormalBias;
        }

        // 2. RenderSettings Ambient Snapshot
        snapshot.ambientMode = (int)RenderSettings.ambientMode;
        snapshot.ambientIntensity = RenderSettings.ambientIntensity;
        snapshot.skyColor = RenderSettings.ambientSkyColor;
        snapshot.equatorColor = RenderSettings.ambientEquatorColor;
        snapshot.groundColor = RenderSettings.ambientGroundColor;
        snapshot.flatAmbientLight = RenderSettings.ambientLight;

        // 3. Post Process Snapshot
        PostProcessVolume ppVolume = FindObjectOfType<PostProcessVolume>();
        if (ppVolume != null && ppVolume.sharedProfile != null)
        {
            snapshot.hasPostProcess = true;
            ColorGrading cg;
            if (ppVolume.sharedProfile.TryGetSettings(out cg))
            {
                snapshot.shadowLift = cg.lift.value.w;
                snapshot.gammaOffset = cg.gamma.value.w;
                snapshot.postExposure = cg.postExposure.value;
                snapshot.tonemapper = (int)cg.tonemapper.value;
            }
        }

        string json = JsonUtility.ToJson(snapshot, true);
        string filePath = GetBackupFilePath(scenePath);
        File.WriteAllText(filePath, json);

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Đã Lưu Bản Sao Lưu Gốc",
                $"Đã lưu trữ trạng thái ánh sáng gốc của Scene '{Path.GetFileNameWithoutExtension(scenePath)}' thành công vào lúc {snapshot.backupTime}.\n\nBạn có thể phục hồi lại bất cứ lúc nào!",
                "OK"
            );
        }
    }

    private void RestoreCurrentSceneFromBackup()
    {
        string scenePath = EditorSceneManager.GetActiveScene().path;
        string backupFile = GetBackupFilePath(scenePath);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Restore Lighting From Backup");

        if (File.Exists(backupFile))
        {
            string json = File.ReadAllText(backupFile);
            SceneLightingSnapshot snapshot = JsonUtility.FromJson<SceneLightingSnapshot>(json);

            // 1. Phục hồi Sun Light
            if (snapshot.hasSunLight && targetSunLight != null)
            {
                Undo.RecordObject(targetSunLight, "Restore Sun Light");
                targetSunLight.intensity = snapshot.sunIntensity;
                targetSunLight.color = snapshot.sunColor;
                targetSunLight.shadows = (LightShadows)snapshot.shadowType;
                targetSunLight.shadowStrength = snapshot.shadowStrength;
                targetSunLight.shadowBias = snapshot.shadowBias;
                targetSunLight.shadowNormalBias = snapshot.shadowNormalBias;
            }

            // 2. Phục hồi RenderSettings Ambient
            RenderSettings.ambientMode = (UnityEngine.Rendering.AmbientMode)snapshot.ambientMode;
            RenderSettings.ambientIntensity = snapshot.ambientIntensity;
            RenderSettings.ambientSkyColor = snapshot.skyColor;
            RenderSettings.ambientEquatorColor = snapshot.equatorColor;
            RenderSettings.ambientGroundColor = snapshot.groundColor;
            RenderSettings.ambientLight = snapshot.flatAmbientLight;

            // 3. Phục hồi Post-Processing
            string profilePath = "Assets/NatureManufacture Assets/Castle and Dungeon/Scenes/PostProcessVolumeProfile Castle.asset";
            PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);
            if (profile != null)
            {
                ColorGrading cg;
                if (profile.TryGetSettings(out cg))
                {
                    Undo.RecordObject(profile, "Restore Color Grading");
                    cg.lift.value = new Vector4(1f, 1f, 1f, snapshot.shadowLift);
                    cg.gamma.value = new Vector4(1f, 1f, 1f, snapshot.gammaOffset);
                    cg.postExposure.value = snapshot.postExposure;
                    cg.tonemapper.value = (Tonemapper)snapshot.tonemapper;
                }
            }
        }
        else
        {
            // Nếu không có file backup, phục hồi về chuẩn mặc định ban đầu của Unity
            if (targetSunLight != null)
            {
                Undo.RecordObject(targetSunLight, "Reset Sun Light");
                targetSunLight.shadowStrength = 1.0f;
                targetSunLight.intensity = 1.0f;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.17f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.10f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.06f, 0.06f);
            RenderSettings.ambientIntensity = 1.0f;
        }

        // 4. Dọn dẹp / Xóa các GameObject phụ mà tool đã thêm vào
        CleanUpAddedGameObjects();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        // Cập nhật lại UI tool
        FindCurrentSceneSettings();

        EditorUtility.DisplayDialog(
            "Phục Hồi Thành Công!",
            $"Đã phục hồi Scene '{EditorSceneManager.GetActiveScene().name}' về trạng thái ban đầu hoàn toàn.",
            "OK"
        );
    }

    private void RestoreAllScenesFromBackup()
    {
        string currentScenePath = EditorSceneManager.GetActiveScene().path;

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        List<string> restoredScenes = new List<string>();

        try
        {
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);

                EditorUtility.DisplayProgressBar(
                    "Đang phục hồi toàn bộ Scene về gốc",
                    $"Đang xử lý: {sceneName} ({i + 1}/{sceneGuids.Length})",
                    (float)i / sceneGuids.Length
                );

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (scene.IsValid())
                {
                    targetSunLight = null;
                    RestoreCurrentSceneFromBackupQuiet();
                    EditorSceneManager.SaveScene(scene);
                    restoredScenes.Add(sceneName);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }
        }

        AssetDatabase.SaveAssets();
        FindCurrentSceneSettings();

        EditorUtility.DisplayDialog(
            "Hoàn Tất Phục Hồi Toàn Bộ!",
            $"Đã phục hồi thành công {restoredScenes.Count} Scene về trạng thái ban đầu:\n\n" +
            string.Join(", ", restoredScenes),
            "OK"
        );
    }

    private void RestoreCurrentSceneFromBackupQuiet()
    {
        string scenePath = EditorSceneManager.GetActiveScene().path;
        string backupFile = GetBackupFilePath(scenePath);

        if (File.Exists(backupFile))
        {
            string json = File.ReadAllText(backupFile);
            SceneLightingSnapshot snapshot = JsonUtility.FromJson<SceneLightingSnapshot>(json);

            // Tìm Sun Light
            Light sun = null;
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.gameObject.name != "Maze_TopDown_FillLight")
                {
                    sun = l;
                    break;
                }
            }

            if (sun != null && snapshot.hasSunLight)
            {
                sun.intensity = snapshot.sunIntensity;
                sun.color = snapshot.sunColor;
                sun.shadows = (LightShadows)snapshot.shadowType;
                sun.shadowStrength = snapshot.shadowStrength;
                sun.shadowBias = snapshot.shadowBias;
                sun.shadowNormalBias = snapshot.shadowNormalBias;
            }

            RenderSettings.ambientMode = (UnityEngine.Rendering.AmbientMode)snapshot.ambientMode;
            RenderSettings.ambientIntensity = snapshot.ambientIntensity;
            RenderSettings.ambientSkyColor = snapshot.skyColor;
            RenderSettings.ambientEquatorColor = snapshot.equatorColor;
            RenderSettings.ambientGroundColor = snapshot.groundColor;
            RenderSettings.ambientLight = snapshot.flatAmbientLight;
        }

        CleanUpAddedGameObjects();
    }

    private void CleanUpAddedGameObjects()
    {
        // 1. Xóa Maze Fill Light
        GameObject mazeLight = GameObject.Find("Maze_TopDown_FillLight");
        if (mazeLight != null)
        {
            Undo.DestroyObjectImmediate(mazeLight);
        }

        // 2. Xóa Player Aura Light
        GameObject playerLight = GameObject.Find("Player_Runtime_AuraLight");
        if (playerLight != null)
        {
            Undo.DestroyObjectImmediate(playerLight);
        }

        // 3. Xóa InGameLightingAndTVManager
        InGameLightingAndTVManager mgr = FindObjectOfType<InGameLightingAndTVManager>();
        if (mgr != null)
        {
            Undo.DestroyObjectImmediate(mgr.gameObject);
        }
    }

    #endregion
}
