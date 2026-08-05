using UnityEditor;
using UnityEngine;
using System.IO;

public enum PainterToolMode
{
    Draw,   // Chế độ Vẽ
    Erase   // Chế độ Tẩy / Xóa nét
}

/// <summary>
/// Công cụ hỗ trợ vẽ ký hiệu / gợi ý trực tiếp lên bức tường trong Scene View của Unity Editor.
/// TỰ ĐỘNG LƯU NGẦM (Auto-Save) khi tắt chế độ vẽ và tự động thiết lập TextureImporter (AlphaIsTransparency & IsReadable).
/// </summary>
public class WallTexturePainterWindow : EditorWindow
{
    private GameObject targetWallObject;
    private PainterToolMode currentToolMode = PainterToolMode.Draw;
    private Color brushColor = Color.red; // Mặc định màu đỏ nổi bật
    private int brushRadius = 3;          // Mặc định nét vẽ nhỏ mảnh (radius = 3)
    private int textureResolutionIndex = 1; // 0: 1024x1024, 1: 2048x2048, 2: 4096x4096 (4K)
    private readonly int[] resolutions = new int[] { 1024, 2048, 4096 };
    private readonly string[] resolutionOptions = new string[] { "1024x1024 (Thường)", "2048x2048 (Nét mịn HD)", "4096x4096 (Nét siêu mảnh 4K)" };

    private bool isPaintingMode = false;

    private Texture2D editableTexture;
    private GameObject overlayQuadObject;
    private Material overlayMaterial;
    private MeshCollider overlayCollider;

    [MenuItem("Tools/Vẽ Trực Tiếp Lên Tường (Wall Painter)")]
    public static void ShowWindow()
    {
        WallTexturePainterWindow window = GetWindow<WallTexturePainterWindow>("Wall Painter");
        window.minSize = new Vector2(350, 560);
    }

    private void OnGUI()
    {
        GUILayout.Label("CÔNG CỤ VẼ KÝ HIỆU TRỰC TIẾP LÊN TƯỜNG", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // Ô chọn Object tường
        targetWallObject = (GameObject)EditorGUILayout.ObjectField("Bức tường mục tiêu:", targetWallObject, typeof(GameObject), true);

        if (targetWallObject == null && Selection.activeGameObject != null)
        {
            if (GUILayout.Button("Lấy Object đang chọn trong Hierarchy"))
            {
                targetWallObject = Selection.activeGameObject;
            }
        }

        if (targetWallObject == null)
        {
            EditorGUILayout.HelpBox("Hãy chọn bức tường stone_element... để bắt đầu vẽ.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(5);

        // NÚT CHỌN CHẾ ĐỘ: VẼ HOẶC TẨY / XÓA NÉT
        EditorGUILayout.LabelField("Chọn Công Cụ:", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();

        GUI.backgroundColor = (currentToolMode == PainterToolMode.Draw) ? new Color(0.3f, 0.8f, 1f) : Color.white;
        if (GUILayout.Button("🖌 VẼ NÉT", GUILayout.Height(30)))
        {
            currentToolMode = PainterToolMode.Draw;
        }

        GUI.backgroundColor = (currentToolMode == PainterToolMode.Erase) ? new Color(1f, 0.6f, 0.2f) : Color.white;
        if (GUILayout.Button("🧹 TẨY / XÓA NÉT", GUILayout.Height(30)))
        {
            currentToolMode = PainterToolMode.Erase;
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (currentToolMode == PainterToolMode.Draw)
        {
            brushColor = EditorGUILayout.ColorField("Màu cọ vẽ:", brushColor);
        }
        else
        {
            EditorGUILayout.HelpBox("Đang ở Chế Độ Tẩy: Di chuột và bấm vẽ để xóa nét thừa.", MessageType.Info);
        }

        // TÙY CHỈNH NÉT SIÊU MẢNH TỪ 1 PIXEL
        brushRadius = EditorGUILayout.IntSlider("Kích thước cọ / tẩy (Nét vẽ):", brushRadius, 1, 80);
        textureResolutionIndex = EditorGUILayout.Popup("Độ nét bức ảnh (Resolution):", textureResolutionIndex, resolutionOptions);

        EditorGUILayout.Space(10);

        if (!isPaintingMode)
        {
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("BẬT CHẾ ĐỘ VẼ TRONG SCENE VIEW", GUILayout.Height(40)))
            {
                StartPaintingMode();
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.2f);
            if (GUILayout.Button("TẮT CHẾ ĐỘ VẼ", GUILayout.Height(40)))
            {
                StopPaintingMode();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.HelpBox("💡 MẸO: Giữ phím SHIFT khi vẽ để nhanh chóng chuyển sang CỤC TẨY xóa nét!", MessageType.Warning);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Lưu Hình Vẽ Thành File PNG (Asset)", GUILayout.Height(35)))
            {
                SavePaintedTextureToAsset(true);
            }

            if (GUILayout.Button("Xóa Tất Cả (Reset)", GUILayout.Height(25)))
            {
                ClearDrawing();
            }
        }
    }

    private void StartPaintingMode()
    {
        if (targetWallObject == null) return;

        Renderer targetRenderer = targetWallObject.GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Object này không có MeshRenderer!", "OK");
            return;
        }

        // 1. TẠO / TÌM LỚP OVERLAY QUAD ĐÈ SÁT MẶT TƯỜNG
        Transform existingOverlay = targetWallObject.transform.Find("Wall_Hint_Overlay");
        if (existingOverlay != null)
        {
            overlayQuadObject = existingOverlay.gameObject;
        }
        else
        {
            overlayQuadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlayQuadObject.name = "Wall_Hint_Overlay";
            overlayQuadObject.transform.SetParent(targetWallObject.transform, false);

            Bounds bounds = targetRenderer.bounds;
            overlayQuadObject.transform.position = bounds.center + targetWallObject.transform.forward * -0.01f;
            overlayQuadObject.transform.rotation = targetWallObject.transform.rotation;
            overlayQuadObject.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(overlayQuadObject, "Create Wall Hint Overlay");
        }

        overlayCollider = overlayQuadObject.GetComponent<MeshCollider>();
        if (overlayCollider == null) overlayCollider = overlayQuadObject.AddComponent<MeshCollider>();
        overlayCollider.convex = false;

        int res = resolutions[textureResolutionIndex];
        bool loadedExisting = false;

        // 2. KHÔI PHỤC NẠP LẠI HÌNH VẼ CŨ
        if (editableTexture != null)
        {
            loadedExisting = true;
        }
        else
        {
            // Tìm file PNG đã tự động lưu trước đó
            string savePath = $"Assets/PLuan/Minigame5/Texture/Wall_Hint_{targetWallObject.name}.png";
            Texture2D savedTexAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);

            Texture srcTextureToLoad = null;
            if (savedTexAsset != null)
            {
                srcTextureToLoad = savedTexAsset;
                res = savedTexAsset.width;
            }
            else
            {
                Renderer overlayRend = overlayQuadObject.GetComponent<Renderer>();
                if (overlayRend != null && overlayRend.sharedMaterial != null && overlayRend.sharedMaterial.mainTexture != null)
                {
                    srcTextureToLoad = overlayRend.sharedMaterial.mainTexture;
                }
            }

            if (srcTextureToLoad != null)
            {
                editableTexture = CopyTextureToEditable(srcTextureToLoad, res, res);
                loadedExisting = true;
                Debug.Log($"[WallPainter] NẠP THÀNH CÔNG hình vẽ cũ ({res}x{res})!");
            }
        }

        if (!loadedExisting || editableTexture == null)
        {
            editableTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
            ClearDrawingInternal();
            Debug.Log($"[WallPainter] Tạo mới bản vẽ trong suốt ({res}x{res}).");
        }

        // 3. SỬ DỤNG SHADER SPRITES/DEFAULT
        Shader overlayShader = Shader.Find("Sprites/Default");
        if (overlayShader == null) overlayShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (overlayShader == null) overlayShader = Shader.Find("Unlit/Transparent");

        overlayMaterial = new Material(overlayShader);
        overlayMaterial.color = Color.white;

        ApplyTextureToMaterial(overlayMaterial, editableTexture);

        Renderer rend = overlayQuadObject.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = overlayMaterial;

        isPaintingMode = true;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        SceneView.RepaintAll();
    }

    private Texture2D CopyTextureToEditable(Texture srcTex, int width, int height)
    {
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(srcTex, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    private void ApplyTextureToMaterial(Material mat, Texture tex)
    {
        if (mat == null || tex == null) return;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        mat.mainTexture = tex;
    }

    private void ClearDrawingInternal()
    {
        if (editableTexture == null) return;
        Color[] clears = new Color[editableTexture.width * editableTexture.height];
        for (int i = 0; i < clears.Length; i++) clears[i] = Color.clear;
        editableTexture.SetPixels(clears);
        editableTexture.Apply();
    }

    private void ClearDrawing()
    {
        ClearDrawingInternal();
        if (overlayMaterial != null) ApplyTextureToMaterial(overlayMaterial, editableTexture);
        SceneView.RepaintAll();

        // Xóa luôn file PNG nếu có
        if (targetWallObject != null)
        {
            string savePath = $"Assets/PLuan/Minigame5/Texture/Wall_Hint_{targetWallObject.name}.png";
            if (File.Exists(savePath))
            {
                AssetDatabase.DeleteAsset(savePath);
            }
        }
        Debug.Log("[WallPainter] Đã xóa sạch toàn bộ hình vẽ.");
    }

    private void StopPaintingMode()
    {
        // TỰ ĐỘNG LƯU NGẦM HÌNH VẼ KHI BẤM TẮT CHẾ ĐỘ VẼ
        if (isPaintingMode && editableTexture != null)
        {
            SavePaintedTextureToAsset(false);
        }

        isPaintingMode = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        StopPaintingMode();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPaintingMode || overlayQuadObject == null || editableTexture == null) return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (overlayCollider != null && overlayCollider.Raycast(ray, out RaycastHit hitInfo, 10000f))
        {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            bool isErasing = (currentToolMode == PainterToolMode.Erase) || e.shift;

            float worldBrushRadius = (brushRadius / (float)editableTexture.width) * overlayQuadObject.transform.lossyScale.x;
            worldBrushRadius = Mathf.Max(worldBrushRadius, 0.005f);

            Handles.color = isErasing ? Color.cyan : brushColor;
            Handles.DrawWireDisc(hitInfo.point, hitInfo.normal, worldBrushRadius);

            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                Vector2 uv = hitInfo.textureCoord;
                PaintPixelAtUV(uv, isErasing);
                e.Use();
                SceneView.RepaintAll();
            }
        }
    }

    private void PaintPixelAtUV(Vector2 uv, bool isErasing)
    {
        int w = editableTexture.width;
        int h = editableTexture.height;

        int px = Mathf.Clamp((int)(uv.x * w), 0, w - 1);
        int py = Mathf.Clamp((int)(uv.y * h), 0, h - 1);

        Color applyColor = isErasing ? Color.clear : brushColor;

        for (int x = -brushRadius; x <= brushRadius; x++)
        {
            for (int y = -brushRadius; y <= brushRadius; y++)
            {
                if (x * x + y * y <= brushRadius * brushRadius)
                {
                    int nx = px + x;
                    int ny = py + y;
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                    {
                        editableTexture.SetPixel(nx, ny, applyColor);
                    }
                }
            }
        }
        editableTexture.Apply();

        ApplyTextureToMaterial(overlayMaterial, editableTexture);
        EditorUtility.SetDirty(overlayMaterial);
    }

    private void SavePaintedTextureToAsset(bool showDialog = true)
    {
        if (editableTexture == null || targetWallObject == null) return;

        string folderPath = "Assets/PLuan/Minigame5/Texture";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string savePath = $"{folderPath}/Wall_Hint_{targetWallObject.name}.png";
        byte[] bytes = editableTexture.EncodeToPNG();
        File.WriteAllBytes(savePath, bytes);

        AssetDatabase.Refresh();

        // TỰ ĐỘNG CẤU HÌNH THUỘC TÍNH FILE ẢNH ĐỂ HỖ TRỢ ALPHA TRONG SUỐT VÀ NẠP LẠI
        TextureImporter importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        Texture2D savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        if (savedTex != null && overlayMaterial != null)
        {
            ApplyTextureToMaterial(overlayMaterial, savedTex);
        }

        Debug.Log($"[WallPainter] Đã tự động lưu hình vẽ vào: {savePath}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Thành công", $"Đã lưu hình vẽ gợi ý thành file Asset:\n{savePath}", "OK");
        }
    }
}
