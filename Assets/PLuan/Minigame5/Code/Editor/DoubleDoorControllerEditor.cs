using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DoubleDoorController))]
public class DoubleDoorControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Vẽ Inspector mặc định
        DrawDefaultInspector();

        DoubleDoorController controller = (DoubleDoorController)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("🎮 BẢNG ĐIỀU KHIỂN PREVIEW (EDITOR)", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
        if (GUILayout.Button("▶️ MỞ CỬA (Open Door)", GUILayout.Height(35)))
        {
            controller.OpenDoor();
        }

        GUI.backgroundColor = new Color(0.85f, 0.3f, 0.2f);
        if (GUILayout.Button("⏹️ ĐÓNG CỬA (Close Door)", GUILayout.Height(35)))
        {
            controller.CloseDoor();
        }

        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("💾 Lưu góc hiện tại làm Góc Đóng"))
        {
            controller.SaveCurrentAsClosedRotation();
            Debug.Log("[DoubleDoorController] Đã lưu góc quay hiện tại làm góc Đóng chuẩn.");
        }

        if (GUILayout.Button("🔄 Đặt lại về Góc Đóng"))
        {
            controller.ResetToClosedRotation();
            Debug.Log("[DoubleDoorController] Đã đưa cửa về góc Đóng chuẩn.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}
