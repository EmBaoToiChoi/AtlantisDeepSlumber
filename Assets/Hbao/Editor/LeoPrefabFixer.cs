using UnityEditor;
using UnityEngine;

public class LeoPrefabFixer
{
    [MenuItem("Tools/Optimize/Fix Leo Prefab")]
    public static void FixLeoPrefab()
    {
        string prefabPath = "Assets/Hbao/Prefab/Player_Leo.prefab";
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[LeoPrefabFixer] Cannot find Player_Leo prefab at {prefabPath}");
            EditorUtility.DisplayDialog("Error", $"Cannot find Player_Leo prefab at {prefabPath}", "OK");
            return;
        }

        // Load prefab contents for editing
        string assetPath = AssetDatabase.GetAssetPath(prefabRoot);
        GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);
        
        Animator anim = contentsRoot.GetComponentInChildren<Animator>(true);
        if (anim == null)
        {
            Debug.LogError("[LeoPrefabFixer] Cannot find Animator in Player_Leo prefab child hierarchy!");
            EditorUtility.DisplayDialog("Error", "Cannot find Animator in Player_Leo prefab child hierarchy!", "OK");
            PrefabUtility.UnloadPrefabContents(contentsRoot);
            return;
        }

        GameObject animGo = anim.gameObject;
        RootMotionBridge bridge = animGo.GetComponent<RootMotionBridge>();
        if (bridge == null)
        {
            bridge = animGo.AddComponent<RootMotionBridge>();
            Debug.Log($"[LeoPrefabFixer] Attached RootMotionBridge to child GameObject '{animGo.name}' successfully!");
            PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
            Debug.Log("[LeoPrefabFixer] Saved Player_Leo prefab with RootMotionBridge attached.");
            EditorUtility.DisplayDialog("Success", "Successfully attached RootMotionBridge to Player_Leo prefab!", "OK");
        }
        else
        {
            Debug.Log("[LeoPrefabFixer] RootMotionBridge is already attached to the Leo prefab.");
            EditorUtility.DisplayDialog("Info", "RootMotionBridge is already attached to the Leo prefab.", "OK");
        }

        PrefabUtility.UnloadPrefabContents(contentsRoot);
    }
}
