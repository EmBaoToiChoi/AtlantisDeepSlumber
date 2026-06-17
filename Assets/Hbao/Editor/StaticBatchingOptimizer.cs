using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class StaticBatchingOptimizer
{
    [MenuItem("Tools/Optimize/Mark Environment Objects as Batching Static")]
    public static void MarkStatic()
    {
        // Tìm tất cả các MeshRenderer trong Scene hiện tại
        MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();
        int count = 0;
        int total = renderers.Length;
        
        try
        {
            for (int i = 0; i < total; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null) continue;
                
                GameObject go = renderer.gameObject;
                
                // Cập nhật thanh tiến trình hiển thị cho người dùng
                if (i % 20 == 0)
                {
                    float progress = (float)i / total;
                    if (EditorUtility.DisplayCancelableProgressBar("Optimizing Static Objects", $"Checking {go.name}...", progress))
                    {
                        Debug.LogWarning("Static batching optimization cancelled by user.");
                        break;
                    }
                }
                
                // Bỏ qua các đối tượng chuyển động linh hoạt (Player, Enemy, Đạn, VFX, v.v.)
                if (go.GetComponent<Animator>() != null || 
                    go.GetComponentInParent<Animator>() != null ||
                    go.GetComponent<Rigidbody>() != null ||
                    go.name.Contains("Player") || 
                    go.name.Contains("Enemy") || 
                    go.name.Contains("Monster") ||
                    go.name.Contains("NPC") ||
                    go.name.Contains("Weapon") ||
                    go.name.Contains("Bullet") ||
                    go.name.Contains("Effect") ||
                    go.name.Contains("VFX"))
                {
                    continue;
                }

                // Gán cờ Batching Static
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0)
                {
                    GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.BatchingStatic);
                    EditorUtility.SetDirty(go);
                    count++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Đánh dấu Scene thay đổi để người dùng lưu lại
        if (count > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        
        Debug.Log($"Successfully marked {count} out of {total} MeshRenderers in the scene as Batching Static.");
    }
}
