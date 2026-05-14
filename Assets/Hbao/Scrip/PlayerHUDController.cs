using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement hpFill;
    private VisualElement mpFill;
    private VisualElement expFill;
    
    // Tham chiếu trực tiếp tới phần tử chứa icon
    private VisualElement micIcon;
    
    // Mặc định false -> Vào game chưa ấn M sẽ là tắt Mic
    private bool isMicOn = false; 

    void OnEnable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        var root = uiDocument.rootVisualElement;

        hpFill = root.Q<VisualElement>("hp-fill");
        mpFill = root.Q<VisualElement>("mp-fill");
        expFill = root.Q<VisualElement>("exp-fill");

        // Tìm UI Mic Icon trực tiếp
        micIcon = root.Q<VisualElement>("mic-icon");

        // Đồng bộ trạng thái UI ngay khi load game (Tắt)
        UpdateMicUI();
    }

    void Update()
    {
        // Khi ấn M sẽ đổi trạng thái
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMic();
        }
    }

    public void ToggleMic()
    {
        isMicOn = !isMicOn;
        UpdateMicUI();
    }

    private void UpdateMicUI()
    {
        if (micIcon == null) return;

        if (isMicOn)
        {
            micIcon.RemoveFromClassList("mic-off");
            micIcon.AddToClassList("mic-on");
        }
        else
        {
            micIcon.RemoveFromClassList("mic-on");
            micIcon.AddToClassList("mic-off");
        }
    }

    // --- Giữ nguyên các hàm cập nhật HP/MP/EXP của bạn bên dưới ---
    public void SetHealth(float percentage) { /* ... */ }
}