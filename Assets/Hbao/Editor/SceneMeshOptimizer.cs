using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hbao.EditorTools
{
    public class SceneMeshOptimizer : EditorWindow
    {
        private string targetScenePath = "Assets/Scenes/HBaoMapKhu1.unity";

        [MenuItem("Tools/Tối ưu dung lượng Scene (Fix Mesh Clones)")]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneMeshOptimizer>("Scene Optimizer");
            window.minSize = new Vector2(450, 300);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("CÔNG CỤ TỐI ƯU DUNG LƯỢNG SCENE UNITY", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Công cụ này sẽ tự động tìm các Mesh bị Clone/nhúng trực tiếp vào file .unity (khiến scene nặng hàng trăm MB) " +
                "và nối lại về các file FBX gốc của Project, giúp giảm dung lượng Scene từ 350MB xuống còn ~2-5MB.",
                MessageType.Info);

            GUILayout.Space(15);
            targetScenePath = EditorGUILayout.TextField("Đường dẫn Scene cần tối ưu:", targetScenePath);

            GUILayout.Space(15);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("🚀 TỐI ƯU HÓA SCENE HBAOMAPKHU1", GUILayout.Height(45)))
            {
                OptimizeScene(targetScenePath);
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            if (GUILayout.Button("⚡ Tối ưu Scene ĐANG MỞ hiện tại", GUILayout.Height(35)))
            {
                OptimizeCurrentActiveScene();
            }
        }

        public static void OptimizeCurrentActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
            {
                EditorUtility.DisplayDialog("Lỗi", "Scene hiện tại chưa được lưu hoặc không hợp lệ!", "OK");
                return;
            }
            OptimizeScene(activeScene.path);
        }

        public static void OptimizeScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy file scene tại: {scenePath}", "OK");
                return;
            }

            long oldSizeBytes = new FileInfo(scenePath).Length;
            double oldSizeMB = oldSizeBytes / (1024.0 * 1024.0);

            // Mở scene nếu chưa mở
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EditorUtility.DisplayProgressBar("Tối ưu Scene", "Đang quét danh mục Mesh trong Project...", 0.1f);

            // Xây dựng từ điển tất cả Mesh trong Project
            Dictionary<string, Mesh> meshDict = BuildProjectMeshDictionary();

            EditorUtility.DisplayProgressBar("Tối ưu Scene", "Đang tối ưu MeshFilter & MeshCollider...", 0.4f);

            int relinkedCount = 0;
            int extractedCount = 0;

            string extractedFolder = "Assets/Scenes/BakedSceneMeshes";
            if (!AssetDatabase.IsValidFolder(extractedFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                {
                    AssetDatabase.CreateFolder("Assets", "Scenes");
                }
                AssetDatabase.CreateFolder("Assets/Scenes", "BakedSceneMeshes");
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();

            // 1. Quét tất cả MeshFilter
            foreach (var root in rootObjects)
            {
                MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh == null) continue;

                    string assetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                    bool isEmbedded = string.IsNullOrEmpty(assetPath) || assetPath.EndsWith(".unity");
                    bool isCloneName = mf.sharedMesh.name.Contains("(Clone)");

                    if (isEmbedded || isCloneName)
                    {
                        string cleanName = mf.sharedMesh.name.Replace("(Clone)", "").Trim();

                        if (meshDict.TryGetValue(cleanName, out Mesh originalMesh))
                        {
                            Undo.RecordObject(mf, "Relink SharedMesh");
                            mf.sharedMesh = originalMesh;
                            relinkedCount++;
                        }
                        else
                        {
                            // Lưu mesh ngoại tuyến thành .asset để không nhúng vào .unity
                            string newAssetPath = $"{extractedFolder}/{cleanName}_{System.Guid.NewGuid().ToString().Substring(0, 8)}.asset";
                            Mesh newMeshAsset = Object.Instantiate(mf.sharedMesh);
                            newMeshAsset.name = cleanName;
                            AssetDatabase.CreateAsset(newMeshAsset, newAssetPath);

                            Undo.RecordObject(mf, "Extract Mesh Asset");
                            mf.sharedMesh = newMeshAsset;
                            meshDict[cleanName] = newMeshAsset;
                            extractedCount++;
                        }
                    }
                }

                // 2. Quét tất cả MeshCollider
                MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
                foreach (var mc in meshColliders)
                {
                    if (mc.sharedMesh == null) continue;

                    string assetPath = AssetDatabase.GetAssetPath(mc.sharedMesh);
                    bool isEmbedded = string.IsNullOrEmpty(assetPath) || assetPath.EndsWith(".unity");
                    bool isCloneName = mc.sharedMesh.name.Contains("(Clone)");

                    if (isEmbedded || isCloneName)
                    {
                        string cleanName = mc.sharedMesh.name.Replace("(Clone)", "").Trim();

                        if (meshDict.TryGetValue(cleanName, out Mesh originalMesh))
                        {
                            Undo.RecordObject(mc, "Relink SharedMesh Collider");
                            mc.sharedMesh = originalMesh;
                            relinkedCount++;
                        }
                    }
                }
            }

            EditorUtility.DisplayProgressBar("Tối ưu Scene", "Đang lưu Scene...", 0.8f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();

            long newSizeBytes = new FileInfo(scenePath).Length;
            double newSizeMB = newSizeBytes / (1024.0 * 1024.0);

            string reportMsg = $"ĐÃ TỐI ƯU SCENE THÀNH CÔNG!\n\n" +
                               $"* Dung lượng cũ: {oldSizeMB:F2} MB\n" +
                               $"* Dung lượng mới: {newSizeMB:F2} MB\n" +
                               $"* Số mesh đã nối lại về file gốc: {relinkedCount}\n" +
                               $"* Số mesh được tách thành .asset: {extractedCount}\n\n" +
                               $"Bây giờ bạn có thể commit và push lên GitHub dễ dàng mà không bị lỗi >100MB!";

            Debug.Log($"[SceneMeshOptimizer] {reportMsg}");
            EditorUtility.DisplayDialog("Tối Ưu Thành Công!", reportMsg, "Tuyệt vời!");
        }

        private static Dictionary<string, Mesh> BuildProjectMeshDictionary()
        {
            var dict = new Dictionary<string, Mesh>(System.StringComparer.OrdinalIgnoreCase);

            // Tìm tất cả FBX và Model assets trong project
            string[] guids = AssetDatabase.FindAssets("t:Model");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    if (asset is Mesh mesh && !string.IsNullOrEmpty(mesh.name))
                    {
                        if (!dict.ContainsKey(mesh.name))
                        {
                            dict[mesh.name] = mesh;
                        }
                    }
                }
            }

            // Tìm thêm các .asset mesh đã có sẵn
            string[] meshAssetGuids = AssetDatabase.FindAssets("t:Mesh");
            foreach (var guid in meshAssetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".unity")) continue;

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    if (asset is Mesh mesh && !string.IsNullOrEmpty(mesh.name))
                    {
                        if (!dict.ContainsKey(mesh.name))
                        {
                            dict[mesh.name] = mesh;
                        }
                    }
                }
            }

            return dict;
        }
    }
}
