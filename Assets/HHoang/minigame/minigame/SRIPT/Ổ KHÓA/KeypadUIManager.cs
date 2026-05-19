using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;

public class KeypadUIManager : MonoBehaviour
{
    // Chuyển sang MonoBehaviour thông thường vì UI chỉ xử lý Local trên máy mỗi người, 
    // Việc truyền nhận dữ liệu đã có KeypadLock (NetworkBehaviour) lo liệu lo gì hack!
    public static KeypadUIManager Instance { get; private set; }

    [Header("UI Components")]
    public GameObject keypadPanel;
    public TextMeshProUGUI displayTextBox;

    [Header("Buttons Setup")]
    public Button[] numberButtons; 
    public Button btnClear;
    public Button btnEnter;
    public Button btnClose;

    private string currentInput = "";
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (keypadPanel != null)
        {
            canvasGroup = keypadPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = keypadPanel.AddComponent<CanvasGroup>();

            keypadPanel.SetActive(false);
            canvasGroup.alpha = 0f;
        }
    }

    private void Start()
    {
        for (int i = 0; i < numberButtons.Length; i++)
        {
            int number = i; 
            if (numberButtons[i] != null)
            {
                numberButtons[i].onClick.AddListener(() => AppendNumber(number));
            }
        }

        if (btnClear != null) btnClear.onClick.AddListener(ClearInput);
        if (btnEnter != null) btnEnter.onClick.AddListener(SubmitCode);
        if (btnClose != null) btnClose.onClick.AddListener(CloseKeypadUI);
    }

    public void OpenKeypadUI()
    {
        if (keypadPanel == null || keypadPanel.activeSelf) return;

        currentInput = "";
        displayTextBox.text = "ENTER CODE";
        displayTextBox.color = Color.white;

        keypadPanel.SetActive(true);
        canvasGroup.DOKill();
        canvasGroup.DOFade(1f, 0.3f);

        // Mở khóa chuột để click nút
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseKeypadUI()
    {
        if (keypadPanel == null || !keypadPanel.activeSelf) return;

        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.2f).OnComplete(() => {
            keypadPanel.SetActive(false);
            
            // Khóa chuột lại để tiếp tục chơi game
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        });
    }

    private void AppendNumber(int num)
    {
        if (currentInput.Length >= 5) return; 

        if (displayTextBox.text == "ENTER CODE" || displayTextBox.text == "WRONG")
        {
            displayTextBox.text = "";
            displayTextBox.color = Color.white;
        }

        currentInput += num.ToString();
        displayTextBox.text = currentInput;
    }

    private void ClearInput()
    {
        currentInput = "";
        displayTextBox.text = "";
        displayTextBox.color = Color.white;
    }

    private void SubmitCode()
    {
        if (currentInput.Length == 0) return;

        // Lấy LocalClientId của chính người chơi đang bấm UI này
        ulong localId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        
        if (KeypadLock.Instance != null)
        {
            KeypadLock.Instance.CheckPasswordServerRpc(currentInput, localId);
        }
    }

    public void OnInputWrong()
    {
        currentInput = "";
        displayTextBox.text = "WRONG";
        displayTextBox.color = Color.red;

        displayTextBox.transform.DOComplete();
        displayTextBox.transform.DOShakePosition(0.4f, strength: 10f, vibrato: 15);
    }
}