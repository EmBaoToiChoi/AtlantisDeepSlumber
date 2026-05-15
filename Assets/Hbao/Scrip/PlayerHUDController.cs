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
    
    // Kỹ năng
    private VisualElement cooldownF;
    private VisualElement cooldownR;
    private Label cooldownTextF;
    private Label cooldownTextR;
    private float cooldownTimeF = 10f; // Thời gian hồi chiêu F
    private float cooldownTimeR = 15f; // Thời gian hồi chiêu R
    private float currentCooldownF = 0f;
    private float currentCooldownR = 0f;
    
    // Khóa kỹ năng
    private VisualElement lockF;
    private VisualElement lockR;
    private bool isSkillsUnlocked = false; // Trạng thái đã mở khóa kỹ năng hay chưa
    
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

        // Tìm UI Kỹ năng
        cooldownF = root.Q<VisualElement>("skill-cooldown-f");
        cooldownR = root.Q<VisualElement>("skill-cooldown-r");
        cooldownTextF = root.Q<Label>("skill-cooldown-text-f");
        cooldownTextR = root.Q<Label>("skill-cooldown-text-r");
        
        lockF = root.Q<VisualElement>("skill-lock-f");
        lockR = root.Q<VisualElement>("skill-lock-r");

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

            // Mở khóa kỹ năng bằng phím L
            if (Keyboard.current.lKey.wasPressedThisFrame && !isSkillsUnlocked)
            {
                isSkillsUnlocked = true;
                Debug.Log("Đã mở khóa Kỹ năng!");
                
                // Thêm class để kích hoạt hiệu ứng rớt ổ khóa trong USS
                if (lockF != null) lockF.AddToClassList("unlocked-anim");
                if (lockR != null) lockR.AddToClassList("unlocked-anim");
            }

            // Kích hoạt Skill F (chỉ khi đã mở khóa)
            if (isSkillsUnlocked && Keyboard.current.fKey.wasPressedThisFrame && currentCooldownF <= 0f)
            {
                currentCooldownF = cooldownTimeF;
                Debug.Log("Đã dùng kỹ năng F");
            }

            // Kích hoạt Skill R (chỉ khi đã mở khóa)
            if (isSkillsUnlocked && Keyboard.current.rKey.wasPressedThisFrame && currentCooldownR <= 0f)
            {
                currentCooldownR = cooldownTimeR;
                Debug.Log("Đã dùng kỹ năng R");
            }
        }

        // Cập nhật hiệu ứng hồi chiêu F
        if (currentCooldownF > 0f)
        {
            currentCooldownF -= Time.deltaTime;
            if (cooldownF != null)
            {
                float percent = Mathf.Clamp01(currentCooldownF / cooldownTimeF) * 100f;
                cooldownF.style.height = Length.Percent(percent);
            }
            if (cooldownTextF != null)
            {
                cooldownTextF.text = Mathf.CeilToInt(currentCooldownF).ToString();
                cooldownTextF.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            if (cooldownF != null && cooldownF.style.height.value.value > 0)
                cooldownF.style.height = Length.Percent(0);
            if (cooldownTextF != null && cooldownTextF.style.display == DisplayStyle.Flex)
                cooldownTextF.style.display = DisplayStyle.None;
        }

        // Cập nhật hiệu ứng hồi chiêu R
        if (currentCooldownR > 0f)
        {
            currentCooldownR -= Time.deltaTime;
            if (cooldownR != null)
            {
                float percent = Mathf.Clamp01(currentCooldownR / cooldownTimeR) * 100f;
                cooldownR.style.height = Length.Percent(percent);
            }
            if (cooldownTextR != null)
            {
                cooldownTextR.text = Mathf.CeilToInt(currentCooldownR).ToString();
                cooldownTextR.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            if (cooldownR != null && cooldownR.style.height.value.value > 0)
                cooldownR.style.height = Length.Percent(0);
            if (cooldownTextR != null && cooldownTextR.style.display == DisplayStyle.Flex)
                cooldownTextR.style.display = DisplayStyle.None;
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