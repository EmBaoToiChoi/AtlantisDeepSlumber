#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class BackgroundRemover : EditorWindow
{
    [MenuItem("Tools/Snip Background from Crystals")]
    public static void RemoveBackground()
    {
        string[] fileNames = { "crystal_purple.png", "crystal_red.png" };
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");

        foreach (var fileName in fileNames)
        {
            string filePath = Path.Combine(resourcesPath, fileName);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[BackgroundRemover] File not found: {filePath}");
                continue;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(fileData);

            Texture2D newTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            Color[] pixels = tex.GetPixels();
            Color[] newPixels = new Color[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                // Check if color is close to black/dark background
                float brightness = (c.r + c.g + c.b) / 3.0f;
                if (brightness < 0.08f)
                {
                    newPixels[i] = new Color(0f, 0f, 0f, 0f);
                }
                else
                {
                    newPixels[i] = new Color(c.r, c.g, c.b, 1.0f);
                }
            }

            newTex.SetPixels(newPixels);
            newTex.Apply();

            byte[] pngData = newTex.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);
            
            // Adjust Texture Import settings to Sprite and enable Alpha
            string relativePath = "Assets/Resources/" + fileName;
            TextureImporter importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Debug.Log($"[BackgroundRemover] Successfully removed black background from: {fileName}");
        }

        AssetDatabase.Refresh();
    }
}
#endif
