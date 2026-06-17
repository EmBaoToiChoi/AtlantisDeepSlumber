using UnityEngine;
using UnityEditor;

public class GPUInstancingOptimizer
{
    [MenuItem("Tools/Optimize/Enable GPU Instancing for All Materials")]
    public static void EnableGPUInstancing()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material");
        int count = 0;
        int total = guids.Length;
        
        try
        {
            for (int i = 0; i < total; i++)
            {
                string guid = guids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Cập nhật thanh tiến trình hiển thị cho người dùng
                if (i % 20 == 0)
                {
                    float progress = (float)i / total;
                    if (EditorUtility.DisplayCancelableProgressBar("Optimizing Materials", $"Checking {path}...", progress))
                    {
                        Debug.LogWarning("Material optimization cancelled by user.");
                        break;
                    }
                }
                
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader != null)
                {
                    if (!mat.enableInstancing)
                    {
                        mat.enableInstancing = true;
                        EditorUtility.SetDirty(mat);
                        count++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        
        if (count > 0)
        {
            AssetDatabase.SaveAssets();
        }
        Debug.Log($"Successfully enabled GPU Instancing on {count} out of {total} materials.");
    }
}
