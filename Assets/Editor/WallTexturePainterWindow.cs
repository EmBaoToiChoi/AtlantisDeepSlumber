using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Công cụ hỗ trợ vẽ ký hiệu / gợi ý trực tiếp lên bức tường trong Scene View của Unity Editor.
/// Sử dụng Shader Sprites/Default chuẩn để đảm bảo KHÔNG BỊ LỖI MÀU TÍM (Magenta Shader Error).
/// Lớp Overlay hoàn toàn trong suốt, chỉ hiển thị đúng nét vẽ màu sắc bạn quết lên.
/// </summary>
public class WallTexturePainterWindow : EditorWindow
{
    private GameObject targetWallObject;
    private Color brushColor = Color.red; // Mặc định màu đỏ nổi bật
    private int brushRadius = 35;
    private bool isPaintingMode = false;

    private Texture2D editableTexture;
    private GameObject overlayQuadObject;
    private Material overlayMaterial;
    private MeshCollider overlayCollider;

    [MenuItem("Tools/Vẽ Trực Tiếp Lên Tường (Wall Painter)")]
    public static void ShowWindow()
    {
        WallTexturePainterWindow window = GetWindow<WallTexturePainterWindow>("Wall Painter");
        window.minSize = new Vector2(350, 480);
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
        brushColor = EditorGUILayout.ColorField("Màu cọ vẽ:", brushColor);
        brushRadius = EditorGUILayout.IntSlider("Kích thước cọ vẽ:", brushRadius, 5, 100);

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

            EditorGUILayout.HelpBox("Đang bật chế độ vẽ!\nNhấn giữ CHUỘT TRÁI trong Scene View để vẽ lên mặt tường.", MessageType.Warning);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Lưu Hình Vẽ Thành File PNG (Asset)", GUILayout.Height(35)))
            {
                SavePaintedTextureToAsset();
            }

            if (GUILayout.Button("Xóa Sạch Nét Vẽ (Reset)", GUILayout.Height(25)))
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

        // 1. TẠO LỚP OVERLAY QUAD ĐÈ SÁT MẶT TƯỜNG
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

            // Căn chỉnh Quad sát mặt tường
            Bounds bounds = targetRenderer.bounds;
            overlayQuadObject.transform.position = bounds.center + targetWallObject.transform.forward * -0.01f;
            overlayQuadObject.transform.rotation = targetWallObject.transform.rotation;
            overlayQuadObject.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(overlayQuadObject, "Create Wall Hint Overlay");
        }

        overlayCollider = overlayQuadObject.GetComponent<MeshCollider>();
        if (overlayCollider == null) overlayCollider = overlayQuadObject.AddComponent<MeshCollider>();
        overlayCollider.convex = false;

        // 2. KHỞI TẠO TEXTURE TRONG SUỐT
        int width = 1024;
        int height = 1024;

        if (editableTexture == null)
        {
            editableTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            ClearDrawingInternal();
        }

        // 3. SỬ DỤNG SHADER SPRITES/DEFAULT HOẶC URP UNLIT TRONG SUỐT (Chống lỗi màu tím Magenta 100%)
        Shader overlayShader = Shader.Find("Sprites/Default");
        if (overlayShader == null) overlayShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (overlayShader == null) overlayShader = Shader.Find("Unlit/Transparent");

        overlayMaterial = new Material(overlayShader);
        overlayMaterial.color = Color.white;

        ApplyTextureToMaterial(overlayMaterial, editableTexture);

        Renderer overlayRend = overlayQuadObject.GetComponent<Renderer>();
        if (overlayRend != null) overlayRend.sharedMaterial = overlayMaterial;

        isPaintingMode = true;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        SceneView.RepaintAll();

        Debug.Log("[WallPainter] Đã sửa xong Shader! Lớp vẽ giờ đây hoàn toàn trong suốt.");
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
        Debug.Log("[WallPainter] Đã xóa sạch nét vẽ.");
    }

    private void StopPaintingMode()
    {
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

            // Hiển thị vòng cọ vẽ
            Handles.color = brushColor;
            Handles.DrawWireDisc(hitInfo.point, hitInfo.normal, brushRadius * 0.03f);

            // Khi người chơi BẤM CHUỘT hoặc KÉO CHUỘT TRÁI
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                Vector2 uv = hitInfo.textureCoord;
                PaintPixelAtUV(uv);
                e.Use();
                SceneView.RepaintAll();
            }
        }
    }

    private void PaintPixelAtUV(Vector2 uv)
    {
        int w = editableTexture.width;
        int h = editableTexture.height;

        int px = Mathf.Clamp((int)(uv.x * w), 0, w - 1);
        int py = Mathf.Clamp((int)(uv.y * h), 0, h - 1);

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
                        editableTexture.SetPixel(nx, ny, brushColor);
                    }
                }
            }
        }
        editableTexture.Apply();

        ApplyTextureToMaterial(overlayMaterial, editableTexture);
        EditorUtility.SetDirty(overlayMaterial);
    }

    private void SavePaintedTextureToAsset()
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

        Texture2D savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        if (savedTex != null && overlayMaterial != null)
        {
            ApplyTextureToMaterial(overlayMaterial, savedTex);
        }

        Debug.Log($"[WallPainter] Đã lưu file ảnh ký hiệu vào: {savePath}");
        EditorUtility.DisplayDialog("Thành công", $"Đã lưu hình vẽ gợi ý thành file Asset:\n{savePath}", "OK");
    }
}
