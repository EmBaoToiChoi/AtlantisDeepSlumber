using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(VideoCutsceneController))]
public class VideoCutsceneControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Vẽ Inspector mặc định
        DrawDefaultInspector();

        VideoCutsceneController controller = (VideoCutsceneController)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🎬 TRÌNH ĐIỀU KHIỂN & XEM TRƯỚC (UI TOOLKIT)", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUI.backgroundColor = new Color(0.2f, 0.75f, 1f, 1f);
        if (GUILayout.Button("🎬 XEM THỬ: 2. BẢNG ROLLING CREDITS", GUILayout.Height(34)))
        {
            controller.PreviewCreditsInEditor();
        }

        GUI.backgroundColor = new Color(0.85f, 0.65f, 1f, 1f);
        if (GUILayout.Button("📜 XEM THỬ: 1. DÒNG CHỮ EPILOGUE (MÀN HÌNH ĐEN)", GUILayout.Height(30)))
        {
            controller.PreviewEpilogueInEditor();
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
        if (GUILayout.Button("🗑️ XÓA / TẮT UI PREVIEW", GUILayout.Height(28)))
        {
            controller.ClearPreviewInEditor();
        }

        EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(0.9f, 0.8f, 0.2f, 1f);
        if (GUILayout.Button("🎨 MỞ FILE UI TOOLKIT (UXML) TRONG UI BUILDER", GUILayout.Height(30)))
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/HHoang/minigame/timeline/EndingCredits.uxml");
            if (uxml != null)
            {
                AssetDatabase.OpenAsset(uxml);
            }
            else
            {
                Debug.LogWarning("[VideoCutsceneEditor] Không tìm thấy file EndingCredits.uxml");
            }
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f, 1f);
            if (GUILayout.Button("⚡ CHẠY THỬ TOÀN BỘ HOẠT CẢNH (PLAY MODE)", GUILayout.Height(38)))
            {
                controller.TestEndingInPlayMode();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();
    }
}
