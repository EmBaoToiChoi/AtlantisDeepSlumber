using UnityEngine;
using UnityEditor;
using TMPro;

public static class TMPFontCreatorHelper
{
    public const string ALL_VIETNAMESE_CHARS = 
        "AÁÀẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬBCDĐEÉÈẺẼẸÊỀẾỂỄỆFGHIÍÌỈĨỊJKLMNOÓÒỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢPQRSTUÚÙỦŨỤƯỪỨỬỮỰVWXYÝỲỶỸỴZ\n" +
        "aáàảãạăằắẳẵặâầấẩẫậbcdđeéèẻẽẹêềếểễệfghiíìỉĩịjklmnoóòỏõọôồốổỗộơờớởỡợpqrstuúùủũụưừứửữựvwxyýỳỷỹỵz\n" +
        "0123456789 !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~…“”‘’«»–—\n";

    [MenuItem("Tools/Atlantis/Copy Toàn Bộ Ký Tự Tiếng Việt Cho TextMeshPro")]
    public static void CopyVietnameseChars()
    {
        EditorGUIUtility.systemCopyBuffer = ALL_VIETNAMESE_CHARS;
        EditorUtility.DisplayDialog("Đã Copy", 
            "Đã copy toàn bộ bảng chữ cái Tiếng Việt vào Clipboard!\n\n" +
            "Bây giờ bạn mở 'Window -> TextMeshPro -> Font Asset Creator' và dán (Ctrl+V) vào ô 'Custom Character List' nhé!", "OK");
    }
}
