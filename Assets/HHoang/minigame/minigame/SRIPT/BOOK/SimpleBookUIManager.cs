using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class SimpleBookUIManager : NetworkBehaviour
{
    public static SimpleBookUIManager Instance { get; private set; }

    [Header("Cấu hình UI Component")]
    public GameObject bookUIPanel; 
    public TextMeshProUGUI symbolText; 
    public TextMeshProUGUI numberText;

    [Header("Cấu hình Text Góc Trái Màn Hình")]
    public TextMeshProUGUI matchCodeText; 

    private CanvasGroup panelCanvasGroup;
    private Coroutine autoCloseCoroutine;
    private List<string> foundCodes = new List<string>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (bookUIPanel != null)
        {
            panelCanvasGroup = bookUIPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null) panelCanvasGroup = bookUIPanel.AddComponent<CanvasGroup>();

            panelCanvasGroup.alpha = 0f;
            bookUIPanel.SetActive(false);
        }

        if (matchCodeText != null)
        {
            matchCodeText.text = "Mật mã đã tìm:\n";
        }
    }

    [ClientRpc]
    public void SyncBookPickupClientRpc(string symbol, int number, bool isReal, ulong pickerClientId)
    {
        // Chặn an toàn: Nếu chuỗi ký hiệu truyền xuống bị rỗng thì bỏ qua
        if (string.IsNullOrEmpty(symbol))
        {
            Debug.LogWarning("[UI] Nhận ClientRpc nhưng dữ liệu ký hiệu bị trống rỗng!");
            return;
        }

        // --- 1. HIỆN BẢNG CANVANS GIỮA MÀN HÌNH (CHỈ DÀNH CHO NGƯỜI NHẶT) ---
        ulong myClientId = NetworkManager.Singleton.LocalClientId;

        if (myClientId == pickerClientId)
        {
            if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);

            if (bookUIPanel != null)
            {
                bookUIPanel.SetActive(true);
                if (symbolText != null) symbolText.text = symbol;
                if (numberText != null) numberText.text = number.ToString();

                panelCanvasGroup.DOKill();
                panelCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutCubic);

                autoCloseCoroutine = StartCoroutine(AutoCloseTimer());
            }
            else
            {
                Debug.LogError("Chưa kéo ô Book UI Panel vào script SimpleBookUIManager ngoài Inspector kìa ông ơi!");
            }
        }

        // --- 2. ĐỒNG BỘ CHỮ Ở GÓC TRÁI MÀN HÌNH (CẬP NHẬT CHO CẢ PHÒNG) ---
        if (matchCodeText != null)
        {
            string newCodeEntry = $"{symbol} = {number}";

            if (!foundCodes.Contains(newCodeEntry))
            {
                foundCodes.Add(newCodeEntry);
                UpdateMatchCodeUI();
            }
        }
    }

    private void UpdateMatchCodeUI()
    {
        string currentDisplay = "Mật mã đã tìm:\n";
        foreach (string code in foundCodes)
        {
            currentDisplay += $"{code}\n";
        }
        matchCodeText.text = currentDisplay;
    }

    private IEnumerator AutoCloseTimer()
    {
        yield return new WaitForSeconds(3f);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.DOKill();
            panelCanvasGroup.DOFade(0f, 0.4f).SetEase(Ease.InCubic).OnComplete(() => {
                bookUIPanel.SetActive(false);
            });
        }
    }
}