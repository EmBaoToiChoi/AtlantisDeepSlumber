using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class TMPTypewriterTimeline : MonoBehaviour
{
    private TMP_Text _tmpText;
    private Coroutine _typewriterCoroutine;

    [Header("Cài đặt tốc độ")]
    [Tooltip("Thời gian xuất hiện giữa mỗi ký tự (giây). Số càng nhỏ chữ chạy càng nhanh.")]
    [SerializeField] private float delayPerChar = 0.04f; 

    private void Awake()
    {
        _tmpText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (_tmpText != null)
        {
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
            }
            _typewriterCoroutine = StartCoroutine(TypeText());
        }
    }

    private IEnumerator TypeText()
    {
        // 1. Ẩn chữ ngay lập tức
        _tmpText.maxVisibleCharacters = 0;

        // 2. Ép TMP cập nhật Mesh và đếm số ký tự ngay lập tức (KHÔNG cần yield return null)
        _tmpText.ForceMeshUpdate(); 

        int totalCharacters = _tmpText.textInfo.characterCount;
        int currentVisible = 0;

        // 3. Chạy hiệu ứng gõ chữ
        while (currentVisible <= totalCharacters)
        {
            _tmpText.maxVisibleCharacters = currentVisible;
            currentVisible++;
            
            // Dùng WaitForSecondsRealtime nếu Timeline bị pause thời gian
            yield return new WaitForSeconds(delayPerChar); 
        }
    }

    private void OnDisable()
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
        }
    }
}