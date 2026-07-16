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

    // Mỗi khi GameObject này được Active trên Timeline, chữ sẽ tự động chạy
    private void OnEnable()
    {
        if (_tmpText != null)
        {
            // Lưu lại text gốc, ẩn toàn bộ chữ đi trước
            string originalText = _tmpText.text;
            _tmpText.maxVisibleCharacters = 0;

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
            }
            _typewriterCoroutine = StartCoroutine(TypeText(originalText));
        }
    }

    private IEnumerator TypeText(string text)
    {
        // Đợi 1 frame để TextMesh Pro cập nhật đầy đủ thông tin ký tự
        yield return null; 
        
        int totalCharacters = _tmpText.textInfo.characterCount;
        int currentVisible = 0;

        while (currentVisible <= totalCharacters)
        {
            _tmpText.maxVisibleCharacters = currentVisible;
            currentVisible++;
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