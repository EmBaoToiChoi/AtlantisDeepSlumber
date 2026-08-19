using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using TMPro;

public static class GenerateTMPFonts
{
    // Bảng toàn bộ ký tự Tiếng Việt (Hoa, thường, số, dấu câu)
    private const string VIETNAMESE_CHARS = 
        "AÁÀẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬBCDĐEÉÈẺẼẸÊỀẾỂỄỆFGHIÍÌỈĨỊJKLMNOÓÒỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢPQRSTUÚÙỦŨỤƯỪỨỬỮỰVWXYÝỲỶỸỴZ" +
        "aáàảãạăằắẳẵặâầấẩẫậbcdđeéèẻẽẹêềếểễệfghiíìỉĩịjklmnoóòỏõọôồốổỗộơờớởỡợpqrstuúùủũụưừứửữựvwxyýỳỷỹỵz" +
        "0123456789 !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~…“”‘’«»–—";

    [MenuItem("Tools/Atlantis/Tạo TMP Font Asset - Philosopher (Chuẩn 100% Tiếng Việt)")]
    public static void GeneratePhilosopher()
    {
        GenerateTMPFontAsset("Assets/PLuan/MainMenu/Font/Philosopher-Vietnamese.ttf", "Assets/PLuan/MainMenu/Font/Philosopher-Vietnamese SDF.asset");
    }

    [MenuItem("Tools/Atlantis/Tạo TMP Font Asset - Cinzel (Chuẩn 100% Tiếng Việt)")]
    public static void GenerateCinzel()
    {
        GenerateTMPFontAsset("Assets/PLuan/MainMenu/Font/Cinzel-Vietnamese.ttf", "Assets/PLuan/MainMenu/Font/Cinzel-Vietnamese SDF.asset");
    }

    [MenuItem("Tools/Atlantis/Tạo TMP Font Asset - Spectral (Chuẩn 100% Tiếng Việt)")]
    public static void GenerateSpectral()
    {
        GenerateTMPFontAsset("Assets/PLuan/MainMenu/Font/Spectral-Vietnamese.ttf", "Assets/PLuan/MainMenu/Font/Spectral-Vietnamese SDF.asset");
    }

    public static void GenerateTMPFontAsset(string fontPath, string targetAssetPath)
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (sourceFont == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy file font tại: " + fontPath, "OK");
            return;
        }

        // Xóa asset cũ nếu có để tạo mới hoàn toàn sạch
        AssetDatabase.DeleteAsset(targetAssetPath);

        // Tạo TMP Font Asset dạng Dynamic có kèm sẵn Character Table
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
        if (fontAsset != null)
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            
            // Nạp toàn bộ bảng chữ cái Tiếng Việt vào Font Asset
            fontAsset.TryAddCharacters(VIETNAMESE_CHARS);

            // Gán Shader chuẩn TextMesh Pro UI Canvas
            Shader tmpShader = Shader.Find("TextMeshPro/Distance Field");
            if (tmpShader != null && fontAsset.material != null)
            {
                fontAsset.material.shader = tmpShader;
            }

            AssetDatabase.CreateAsset(fontAsset, targetAssetPath);
            
            // Lưu texture atlas kèm vào trong sub-asset
            if (fontAsset.atlasTextures != null)
            {
                foreach (var tex in fontAsset.atlasTextures)
                {
                    if (tex != null)
                    {
                        AssetDatabase.AddObjectToAsset(tex, fontAsset);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Thành công", 
                "Đã tạo Font Asset TextMesh Pro thành công:\n" + targetAssetPath + 
                "\n\nĐã nạp 100% bộ chữ Tiếng Việt. Bạn có thể kéo thả vào component TextMeshPro - Text (UI) ngay!", "OK");
        }
    }
}
