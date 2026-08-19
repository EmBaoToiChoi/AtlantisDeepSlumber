using UnityEngine;
using UnityEditor;
using TMPro;

[InitializeOnLoad]
public static class GenerateTMPFonts
{
    static GenerateTMPFonts()
    {
        EditorApplication.delayCall += GeneratePhilosopherFont;
    }

    [MenuItem("Tools/Atlantis/Generate Philosopher TMP Font")]
    public static void GeneratePhilosopherFont()
    {
        string fontPath = "Assets/PLuan/MainMenu/Font/Philosopher-Vietnamese.ttf";
        string assetPath = "Assets/PLuan/MainMenu/Font/Philosopher-Vietnamese SDF.asset";

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
        {
            // Already created
            return;
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            Debug.LogWarning("[TMP Generator] Could not find TTF font at " + fontPath);
            return;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
        if (fontAsset != null)
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=cyan>[TMP Generator]</color> Successfully created <b>" + assetPath + "</b> with Dynamic Vietnamese Glyph Support!");
        }
    }
}
