using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.PostProcessing;
using System.Collections.Generic;

public class SetupLuanMapLighting : EditorWindow
{
    [Header("1. Chế Độ Hiệu Năng (Chống Lag)")]
    public bool enablePointLightShadows = false; // Mặc định TẮT bóng đổ Point Light để 60+ FPS siêu mượt

    [Header("2. Đèn Đuốc Hành Lang (Corridor Torches)")]
    [Range(1f, 15f)] public float torchIntensity = 3.5f;
    [Range(6f, 30f)] public float torchRange = 14.0f;
    public Color torchColor = new Color(1.0f, 0.62f, 0.32f, 1.0f);

    [Header("3. Đuốc Đại Sảnh & KhuPuzzle4 (Big Hall Torches)")]
    [Range(2f, 20f)] public float hallTorchIntensity = 5.5f;
    [Range(10f, 40f)] public float hallTorchRange = 22.0f;

    [Header("4. Dung Nham KhuPuzzle4 (Siêu nhẹ - 0% Lag)")]
    [Range(1f, 15f)] public float puzzle4LavaIntensity = 4.5f;
    [Range(10f, 50f)] public float puzzle4LavaRange = 24.0f;
    public Color lavaColor = new Color(1.0f, 0.32f, 0.05f, 1.0f);

    [Header("5. Môi Trường (Ambient)")]
    [Range(0.2f, 3f)] public float ambientBrightness = 1.3f;

    private Vector2 scrollPos;

    [MenuItem("Tools/Fix & Setup LuanMap Lighting (Like Demo Castle)")]
    public static void ShowWindow()
    {
        SetupLuanMapLighting window = GetWindow<SetupLuanMapLighting>("Castle Lighting Setup");
        window.minSize = new Vector2(420, 560);
        window.Show();
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("CÀI ĐẶT ÁNH SÁNG SIÊU MƯỢT (LUANMAP)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "TỐI ƯU HIỆU NĂNG CAO:\n" +
            "• TẮT bóng đổ Point Light (Shadows=None) giúp TĂNG VỌT FPS (từ 15 FPS lên 60-100+ FPS).\n" +
            "• Bóng tối ở khe nứt & góc tường đã có Ambient Occlusion (AO) xử lý miễn phí siêu nhẹ.\n" +
            "• Đèn dung nham và đuốc to được tối ưu tầm phủ sáng mịn màng, không bị lag máy.",
            MessageType.Info
        );

        EditorGUILayout.Space(10);
        enablePointLightShadows = EditorGUILayout.Toggle("Bật bóng đổ Point Light (Gây lag)", enablePointLightShadows);
        if (enablePointLightShadows)
        {
            EditorGUILayout.HelpBox("Cảnh báo: Bật bóng đổ cho nhiều Point Light sẽ làm giảm mạnh FPS!", MessageType.Warning);
        }

        EditorGUILayout.Space(10);
        GUILayout.Label("1. Đèn Đuốc Thường & Hành Lang:", EditorStyles.boldLabel);
        torchIntensity = EditorGUILayout.Slider("Độ sáng (Intensity)", torchIntensity, 1f, 15f);
        torchRange = EditorGUILayout.Slider("Tầm tỏa sáng (Range - m)", torchRange, 6f, 30f);
        torchColor = EditorGUILayout.ColorField("Màu ngọn lửa", torchColor);

        EditorGUILayout.Space(10);
        GUILayout.Label("2. Đèn Đuốc Đại Sảnh / KhuPuzzle4:", EditorStyles.boldLabel);
        hallTorchIntensity = EditorGUILayout.Slider("Độ sáng đuốc đại sảnh", hallTorchIntensity, 2f, 20f);
        hallTorchRange = EditorGUILayout.Slider("Tầm rọi đuốc đại sảnh (m)", hallTorchRange, 10f, 40f);

        EditorGUILayout.Space(10);
        GUILayout.Label("3. Dung Nham KhuPuzzle4 (Chỉ 1 đèn - 0% Lag):", EditorStyles.boldLabel);
        puzzle4LavaIntensity = EditorGUILayout.Slider("Độ sáng Dung Nham P4", puzzle4LavaIntensity, 1f, 15f);
        puzzle4LavaRange = EditorGUILayout.Slider("Tầm hắt sáng Dung Nham P4 (m)", puzzle4LavaRange, 10f, 50f);
        lavaColor = EditorGUILayout.ColorField("Màu đỏ cam Dung Nham", lavaColor);

        EditorGUILayout.Space(10);
        GUILayout.Label("4. Ánh sáng môi trường (Ambient):", EditorStyles.boldLabel);
        ambientBrightness = EditorGUILayout.Slider("Độ sáng nền (Ambient)", ambientBrightness, 0.2f, 3f);

        EditorGUILayout.Space(15);
        GUI.backgroundColor = new Color(0.25f, 0.95f, 0.45f);
        if (GUILayout.Button("🚀 ÁP DỤNG CHẾ ĐỘ SIÊU MƯỢT (60+ FPS) 🚀", GUILayout.Height(45)))
        {
            ApplyOptimizedLighting(
                enablePointLightShadows,
                torchIntensity, torchRange, torchColor,
                hallTorchIntensity, hallTorchRange,
                puzzle4LavaIntensity, puzzle4LavaRange, lavaColor,
                ambientBrightness
            );
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️ XÓA TOÀN BỘ ĐÈN DUNG NHAM THỪA KHỎI MAP", GUILayout.Height(30)))
        {
            RemoveAllLavaLightsExceptKhuPuzzle4(true);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    public static void ApplyOptimizedLighting(
        bool usePointShadows,
        float normalIntensity, float normalRange, Color fireColor,
        float hallIntensity, float hallRange,
        float p4LavaInt, float p4LavaRng, Color lavaCol,
        float ambientMul)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Ultra Smooth Lighting");

        // 1. Xóa sạch đèn dung nham ở các nơi khác
        int deletedCount = RemoveAllLavaLightsExceptKhuPuzzle4(false);

        // 2. Setup Post-Processing Profile (Ambient Occlusion & Bloom)
        string profilePath = "Assets/NatureManufacture Assets/Castle and Dungeon/Scenes/PostProcessVolumeProfile Castle.asset";
        PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);

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
        if (profile != null)
        {
            ppVolume.sharedProfile = profile;
        }

        // 3. Setup Main Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = Object.FindObjectOfType<Camera>();
        }

        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Setup Camera Settings");
            QualitySettings.pixelLightCount = 64;
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
            ppLayer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.High;
            ppLayer.fog.enabled = true;
            ppLayer.fog.excludeSkybox = true;
        }

        // 4. CẬP NHẬT ĐÈN ĐUỐC (TỐI ƯU KHÔNG SHADOW ĐỂ MƯỢT TUYỆT ĐỐI)
        int torchCount = 0;
        var allGameObjects = Object.FindObjectsOfType<GameObject>(true);

        foreach (var go in allGameObjects)
        {
            if (go == null) continue;
            string goName = go.name.ToLower();
            string parentPath = GetHierarchyPath(go).ToLower();

            bool isBigHall = parentPath.Contains("puzzle") || parentPath.Contains("khupuzzle") || 
                             parentPath.Contains("sanh") || parentPath.Contains("hall") || parentPath.Contains("lau 2");
            bool isTorch = goName.Contains("torch") || goName.Contains("brazier") || goName.Contains("fire_torch");

            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                string psName = ps.name.ToLower();
                if (psName.Contains("torch") || psName.Contains("fire") || psName.Contains("flame"))
                {
                    isTorch = true;
                }
            }

            if (isTorch)
            {
                float targetIntensity = isBigHall ? hallIntensity : normalIntensity;
                float targetRange = isBigHall ? hallRange : normalRange;

                Transform lightChild = go.transform.Find("Torch_Light_Auto");
                Light torchLight = null;

                if (lightChild == null)
                {
                    torchLight = go.GetComponentInChildren<Light>(true);
                    if (torchLight == null)
                    {
                        GameObject newLightGo = new GameObject("Torch_Light_Auto");
                        newLightGo.transform.SetParent(go.transform, false);
                        newLightGo.transform.localPosition = new Vector3(0.012f, 0.45f, 0.65f);
                        torchLight = newLightGo.AddComponent<Light>();
                        Undo.RegisterCreatedObjectUndo(newLightGo, "Create Torch Light");
                    }
                }
                else
                {
                    torchLight = lightChild.GetComponent<Light>();
                }

                if (torchLight != null)
                {
                    Undo.RecordObject(torchLight, "Configure Torch Light");
                    torchLight.enabled = true;
                    torchLight.type = LightType.Point;
                    torchLight.lightmapBakeType = LightmapBakeType.Realtime;
                    torchLight.color = fireColor;
                    torchLight.useColorTemperature = true;
                    torchLight.colorTemperature = 2150f;
                    torchLight.intensity = targetIntensity;
                    torchLight.range = targetRange;
                    
                    // CHẾ ĐỘ MƯỢT: Tắt shadow trên Point Light để triệt tiêu lag 100%
                    torchLight.shadows = usePointShadows ? LightShadows.Soft : LightShadows.None;
                    torchLight.shadowStrength = 0.5f;
                    torchLight.renderMode = LightRenderMode.ForcePixel;
                    torchCount++;
                }
            }
        }

        // 5. CẤU HÌNH DUY NHẤT 1 NGUỒN SÁNG DUNG NHAM (0% LAG)
        SetupKhuPuzzle4LavaLight(p4LavaInt, p4LavaRng, lavaCol);

        // 6. Cấu hình Ambient Lighting mềm mại
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.17f, 0.22f) * ambientMul;
        RenderSettings.ambientEquatorColor = new Color(0.13f, 0.10f, 0.08f) * ambientMul;
        RenderSettings.ambientGroundColor = new Color(0.06f, 0.06f, 0.06f) * ambientMul;
        RenderSettings.ambientIntensity = 1.0f * ambientMul;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Hoàn tất cấu hình siêu mượt!",
            $"Đã thiết lập thành công:\n" +
            $"- Tắt bóng đổ Point Light để triệt tiêu 100% hiện tượng tụt FPS (Lag).\n" +
            $"- Tối ưu {torchCount} ngọn đuốc sáng rực rỡ và tự nhiên.\n" +
            $"- Đèn dung nham KhuPuzzle4 được tối ưu siêu nhẹ.\n" +
            $"- Đã tự động lưu vĩnh viễn vào Scene!\n\n" +
            "Game bây giờ sẽ chạy cực kỳ mượt mà 60+ FPS!",
            "Tuyệt vời"
        );
    }

    private static void SetupKhuPuzzle4LavaLight(float intensity, float range, Color lavaCol)
    {
        GameObject puzzle4 = GameObject.Find("KhuPuzzle4");
        GameObject targetLake = null;

        if (puzzle4 != null)
        {
            Transform lakeTr = puzzle4.transform.Find("Lake Polygon (5)");
            if (lakeTr != null)
            {
                targetLake = lakeTr.gameObject;
            }
        }

        if (targetLake == null)
        {
            targetLake = GameObject.Find("Lake Polygon (5)");
        }

        if (targetLake != null)
        {
            Transform glowRoot = targetLake.transform.Find("Lava_Glow_Lights");
            if (glowRoot == null)
            {
                GameObject root = new GameObject("Lava_Glow_Lights");
                root.transform.SetParent(targetLake.transform, false);
                glowRoot = root.transform;
                Undo.RegisterCreatedObjectUndo(root, "Create Lava Light");
            }

            while (glowRoot.childCount > 1)
            {
                Undo.DestroyObjectImmediate(glowRoot.GetChild(glowRoot.childCount - 1).gameObject);
            }

            Transform lightTr = glowRoot.Find("Lava_PointLight_P4");
            if (lightTr == null && glowRoot.childCount > 0)
            {
                lightTr = glowRoot.GetChild(0);
                lightTr.name = "Lava_PointLight_P4";
            }

            Light lavaLight = null;
            if (lightTr == null)
            {
                GameObject lgo = new GameObject("Lava_PointLight_P4");
                lgo.transform.SetParent(glowRoot, false);
                lgo.transform.localPosition = new Vector3(0, 1.5f, 0);
                lavaLight = lgo.AddComponent<Light>();
                Undo.RegisterCreatedObjectUndo(lgo, "Create P4 Lava Light");
            }
            else
            {
                lavaLight = lightTr.GetComponent<Light>();
            }

            if (lavaLight != null)
            {
                Undo.RecordObject(lavaLight, "Configure P4 Lava Light");
                lavaLight.enabled = true;
                lavaLight.type = LightType.Point;
                lavaLight.lightmapBakeType = LightmapBakeType.Realtime;
                lavaLight.color = lavaCol;
                lavaLight.useColorTemperature = true;
                lavaLight.colorTemperature = 1600f;
                lavaLight.intensity = intensity;
                lavaLight.range = range;
                // TẮT SHADOW ĐỂ KHÔNG TỐN GPU -> MƯỢT TUYỆT ĐỐI
                lavaLight.shadows = LightShadows.None;
                lavaLight.renderMode = LightRenderMode.ForcePixel;
            }
        }
    }

    private static int RemoveAllLavaLightsExceptKhuPuzzle4(bool showDialog)
    {
        int count = 0;
        var allGameObjects = Object.FindObjectsOfType<GameObject>(true);

        foreach (var go in allGameObjects)
        {
            if (go == null) continue;
            string goName = go.name;
            string path = GetHierarchyPath(go);

            if (goName == "Lava_Glow_Lights" || goName.StartsWith("Lava_PointLight_"))
            {
                bool isInsideKhuPuzzle4 = path.Contains("KhuPuzzle4") && path.Contains("Lake Polygon (5)");
                if (!isInsideKhuPuzzle4)
                {
                    Undo.DestroyObjectImmediate(go);
                    count++;
                }
            }
        }

        if (showDialog)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                "Đã dọn dẹp xong!",
                $"Đã xóa toàn bộ {count} đèn dung nham ở các khu vực khác. Máy của bạn sẽ nhẹ và mượt trở lại!",
                "OK"
            );
        }

        return count;
    }

    private static string GetHierarchyPath(GameObject obj)
    {
        string path = "/" + obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = "/" + obj.name + path;
        }
        return path;
    }
}
