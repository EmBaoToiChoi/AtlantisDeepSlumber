using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SpikePillarManager))]
public class SpikePillarManagerEditor : Editor
{
    private SpikePillarManager manager;

    private void OnEnable()
    {
        manager = (SpikePillarManager)target;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (manager != null && manager.autoAnimatePreview && !Application.isPlaying)
        {
            SceneView.RepaintAll();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🎬 CÔNG CỤ PREVIEW TRONG SCENE VIEW", EditorStyles.boldLabel);

        // Nút bật/tắt Auto Play Preview
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = manager.autoAnimatePreview ? new Color(0.3f, 1f, 0.4f) : new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button(manager.autoAnimatePreview ? "⏸ DỪNG ANIMATION PREVIEW" : "▶ TỰ ĐỘNG CHẠY PREVIEW", GUILayout.Height(30)))
        {
            Undo.RecordObject(manager, "Toggle Auto Animate Preview");
            manager.autoAnimatePreview = !manager.autoAnimatePreview;
            if (manager.autoAnimatePreview) manager.previewInEditMode = true;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = manager.previewReverseRotation ? new Color(1f, 0.6f, 0.2f) : new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button(manager.previewReverseRotation ? "🔄 ĐANG ĐẢO CHIỀU XOAY" : "🔄 ĐẢO CHIỀU XOAY (FLIP)", GUILayout.Height(30)))
        {
            Undo.RecordObject(manager, "Toggle Preview Reverse Rotation");
            manager.previewReverseRotation = !manager.previewReverseRotation;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🧭 CÀI ĐẶT NHANH HƯỚNG LĂN (ROLL DIRECTION)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Trục Z- (0, 0, -1)"))
        {
            Undo.RecordObject(manager, "Set Roll Dir Z-");
            manager.rollDirection = new Vector3(0, 0, -1);
            manager.directionMode = SpikePillarManager.DirectionMode.WorldSpace;
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Trục Z+ (0, 0, 1)"))
        {
            Undo.RecordObject(manager, "Set Roll Dir Z+");
            manager.rollDirection = new Vector3(0, 0, 1);
            manager.directionMode = SpikePillarManager.DirectionMode.WorldSpace;
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Trục X- (-1, 0, 0)"))
        {
            Undo.RecordObject(manager, "Set Roll Dir X-");
            manager.rollDirection = new Vector3(-1, 0, 0);
            manager.directionMode = SpikePillarManager.DirectionMode.WorldSpace;
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Trục X+ (1, 0, 0)"))
        {
            Undo.RecordObject(manager, "Set Roll Dir X+");
            manager.rollDirection = new Vector3(1, 0, 0);
            manager.directionMode = SpikePillarManager.DirectionMode.WorldSpace;
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("Khớp theo Transform System (Forward)", GUILayout.Height(25)))
        {
            Undo.RecordObject(manager, "Set Roll Dir Local Forward");
            manager.rollDirection = new Vector3(0, 0, 1);
            manager.directionMode = SpikePillarManager.DirectionMode.LocalSpace;
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Đảo Ngược 180° Hướng Lăn", GUILayout.Height(25)))
        {
            Undo.RecordObject(manager, "Invert Roll Dir");
            manager.rollDirection = -manager.rollDirection;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("📐 ĐẶT HƯỚNG THÂN TRỤ (NẰM NGANG / DỌC / ĐỨNG)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = (manager.meshEulerOffset == new Vector3(0, 90, 0)) ? new Color(0.3f, 1f, 0.4f) : new Color(0.85f, 0.85f, 0.85f);
        if (GUILayout.Button("📐 NẰM NGANG CHẮN HÀNH LANG (0, 90, 0)", GUILayout.Height(28)))
        {
            Undo.RecordObject(manager, "Set Mesh Offset Horizontal 90");
            manager.meshEulerOffset = new Vector3(0, 90, 0);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = (manager.meshEulerOffset == new Vector3(0, -90, 0)) ? new Color(0.3f, 1f, 0.4f) : new Color(0.85f, 0.85f, 0.85f);
        if (GUILayout.Button("📐 NẰM NGANG (0, -90, 0)", GUILayout.Height(28)))
        {
            Undo.RecordObject(manager, "Set Mesh Offset Horizontal -90");
            manager.meshEulerOffset = new Vector3(0, -90, 0);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📐 DỌC THEO ĐƯỜNG ĐI (0, 0, 0)"))
        {
            Undo.RecordObject(manager, "Set Mesh Offset Longitudinal");
            manager.meshEulerOffset = new Vector3(0, 0, 0);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("📐 DỰNG ĐỨNG (90, 0, 0)"))
        {
            Undo.RecordObject(manager, "Set Mesh Offset Vertical");
            manager.meshEulerOffset = new Vector3(90, 0, 0);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            $"Hướng lăn thực tế hiện tại: {manager.GetEffectiveRollDirection()}\n" +
            $"Góc bù mô hình (Mesh Offset): {manager.meshEulerOffset} (Khuyên dùng (0, 90, 0) để trụ nằm ngang lăn tới)\n" +
            $"Trục lăn con lăn nằm ngang: {Vector3.Cross(Vector3.up, manager.GetEffectiveRollDirection()).normalized}\n" +
            $"Kéo thanh 'Preview Timeline' hoặc bật 'Tự động chạy Preview' để quan sát 3D trong Scene View.",
            MessageType.Info
        );

        serializedObject.ApplyModifiedProperties();
    }
}
