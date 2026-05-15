using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class PlayerHUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement hpFill;
    private VisualElement mpFill;
    private VisualElement expFill;
    
    // Tham chiếu trực tiếp tới phần tử chứa icon
    private VisualElement micIcon;
    private VisualElement weaponSlot1;
    private VisualElement weaponSlot2;
    
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
        
        weaponSlot1 = root.Q<VisualElement>("weapon-slot-1");
        weaponSlot2 = root.Q<VisualElement>("weapon-slot-2");

        // Đồng bộ trạng thái UI ngay khi load game (Tắt)
        UpdateMicUI();
        SelectWeapon(1); // Mặc định chọn vũ khí 1 khi vào game
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            // Khi ấn e sẽ đổi trạng thái (Sử dụng Input System mới)
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Debug.Log("Đã ấn E để chuyển đổi trạng thái Mic");
                ToggleMic();
            }

            // Chuyển vũ khí 1 và 2
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                SelectWeapon(1);
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                SelectWeapon(2);
            }
        }
    }

    public void ToggleMic()
    {
        isMicOn = !isMicOn;
        Debug.Log("Trạng thái Mic hiện tại: " + (isMicOn ? "Mở" : "Tắt"));
        UpdateMicUI();
    }

    private void UpdateMicUI()
    {
        if (micIcon == null)
        {
            Debug.LogWarning("Không tìm thấy mic-icon trong UIDocument! Hãy kiểm tra lại file UXML.");
            return;
        }

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

    private void SelectWeapon(int index)
    {
        if (weaponSlot1 == null || weaponSlot2 == null) return;

        if (index == 1)
        {
            weaponSlot1.RemoveFromClassList("weapon-inactive");
            weaponSlot1.AddToClassList("weapon-active");

            weaponSlot2.RemoveFromClassList("weapon-active");
            weaponSlot2.AddToClassList("weapon-inactive");
        }
        else if (index == 2)
        {
            weaponSlot2.RemoveFromClassList("weapon-inactive");
            weaponSlot2.AddToClassList("weapon-active");

            weaponSlot1.RemoveFromClassList("weapon-active");
            weaponSlot1.AddToClassList("weapon-inactive");
        }
    }
}