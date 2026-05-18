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
    private VisualElement lockIconF; // Tham chiếu tới icon ổ khóa để rung
    private VisualElement lockIconR; // Tham chiếu tới icon ổ khóa để rung
    private VisualElement skillImgF; // Tham chiếu tới hình ảnh kỹ năng để ẩn
    private VisualElement skillImgR; // Tham chiếu tới hình ảnh kỹ năng để ẩn
    private bool isSkillsUnlocked = false; // Trạng thái đã mở khóa kỹ năng hay chưa
    
    // Mặc định false -> Vào game chưa ấn M sẽ là tắt Mic
    private bool isMicOn = false; 

    // Cảnh báo vũ khí
    private VisualElement worldMapOverlay;
    private Label weaponWarning;
    private VisualElement weaponLock2; // Tham chiếu tới overlay khóa vũ khí
    private VisualElement lockIcon2;   // Tham chiếu tới icon ổ khóa để rung
    private VisualElement weaponImg2;  // Tham chiếu tới hình ảnh vũ khí để ẩn
    private bool isWeapon2Locked = true;
    private float warningTimer = 0f;
    private const float WARNING_DURATION = 2f;

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
        
        if (lockF != null) lockIconF = lockF.Q<VisualElement>(null, "skill-lock-icon");
        if (lockR != null) lockIconR = lockR.Q<VisualElement>(null, "skill-lock-icon");
        
        skillImgF = root.Q<VisualElement>("skill-img-f");
        skillImgR = root.Q<VisualElement>("skill-img-r");

        // Ẩn kỹ năng ngay từ đầu nếu đang khóa
        if (!isSkillsUnlocked)
        {
            if (skillImgF != null) skillImgF.style.visibility = Visibility.Hidden;
            if (skillImgR != null) skillImgR.style.visibility = Visibility.Hidden;
        }

        // Tìm Label cảnh báo và Overlay khóa
        worldMapOverlay = root.Q<VisualElement>("world-map-overlay");
        weaponWarning = root.Q<Label>("weapon-warning");
        weaponLock2 = root.Q<VisualElement>("weapon-lock-2");
        if (weaponLock2 != null) lockIcon2 = weaponLock2.Q<VisualElement>(null, "weapon-lock-icon");
        weaponImg2 = root.Q<VisualElement>("weapon-img-2");

        // Ẩn vũ khí 2 ngay từ đầu nếu đang khóa
        if (isWeapon2Locked && weaponImg2 != null)
        {
            weaponImg2.style.visibility = Visibility.Hidden;
        }

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

            // Mở khóa vũ khí 2 bằng phím K
            if (Keyboard.current.kKey.wasPressedThisFrame && isWeapon2Locked)
            {
                isWeapon2Locked = false;
                Debug.Log("Đã mở khóa Vũ khí 2!");
                
                if (weaponLock2 != null)
                {
                    weaponLock2.AddToClassList("unlocked-anim");
                }
                
                // Hiện lại vũ khí khi mở khóa
                if (weaponImg2 != null)
                {
                    weaponImg2.style.visibility = Visibility.Visible;
                }
            }

            // Mở/đóng Bản đồ thế giới bằng phím M
            if (Keyboard.current.mKey.wasPressedThisFrame)
            {
                if (worldMapOverlay != null)
                {
                    worldMapOverlay.ToggleInClassList("show-map");
                    bool isNowVisible = worldMapOverlay.ClassListContains("show-map");
                    Debug.Log("Đã " + (isNowVisible ? "mở" : "đóng") + " Bản đồ thế giới với hiệu ứng");
                }
            }


            // Mở khóa kỹ năng bằng phím L
            if (Keyboard.current.lKey.wasPressedThisFrame && !isSkillsUnlocked)
            {
                isSkillsUnlocked = true;
                Debug.Log("Đã mở khóa Kỹ năng!");
                
                // Thêm class để kích hoạt hiệu ứng rớt ổ khóa trong USS
                if (lockF != null) lockF.AddToClassList("unlocked-anim");
                if (lockR != null) lockR.AddToClassList("unlocked-anim");

                // Hiện lại hình ảnh kỹ năng khi mở khóa
                if (skillImgF != null) skillImgF.style.visibility = Visibility.Visible;
                if (skillImgR != null) skillImgR.style.visibility = Visibility.Visible;
            }

            // Kích hoạt Skill F (chỉ khi đã mở khóa)
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (isSkillsUnlocked)
                {
                    if (currentCooldownF <= 0f)
                    {
                        currentCooldownF = cooldownTimeF;
                        Debug.Log("Đã dùng kỹ năng F");
                    }
                }
                else
                {
                    ShowSkillWarning(lockIconF);
                }
            }

            // Kích hoạt Skill R (chỉ khi đã mở khóa)
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (isSkillsUnlocked)
                {
                    if (currentCooldownR <= 0f)
                    {
                        currentCooldownR = cooldownTimeR;
                        Debug.Log("Đã dùng kỹ năng R");
                    }
                }
                else
                {
                    ShowSkillWarning(lockIconR);
                }
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

        // Cập nhật timer cảnh báo
        if (warningTimer > 0f)
        {
            warningTimer -= Time.deltaTime;
            if (warningTimer <= 0f && weaponWarning != null)
            {
                weaponWarning.RemoveFromClassList("show-warning");
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
    public void SetHealth(float percentage)
    {
        if (hpFill != null)
        {
            // Cập nhật chiều rộng của hp-fill theo phần trăm máu thực tế (0% đến 100%)
            hpFill.style.width = Length.Percent(Mathf.Clamp(percentage * 100f, 0f, 100f));
        }
    }

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
            // Kiểm tra nếu vũ khí 2 đang bị khóa
            if (isWeapon2Locked)
            {
                ShowWeaponWarning();
                return; // Không cho phép chọn
            }

            weaponSlot2.RemoveFromClassList("weapon-inactive");
            weaponSlot2.AddToClassList("weapon-active");

            weaponSlot1.RemoveFromClassList("weapon-active");
            weaponSlot1.AddToClassList("weapon-inactive");
        }
    }

    private void ShowSkillWarning(VisualElement icon)
    {
        if (weaponWarning == null) return;

        Debug.Log("Kỹ năng đang bị khóa");
        weaponWarning.AddToClassList("show-warning");
        warningTimer = WARNING_DURATION;

        // Hiệu ứng rung ổ khóa kỹ năng
        if (icon != null)
        {
            icon.RemoveFromClassList("shake-left");
            icon.RemoveFromClassList("shake-right");
            
            icon.schedule.Execute(() => icon.AddToClassList("shake-left")).StartingIn(0);
            icon.schedule.Execute(() => {
                icon.RemoveFromClassList("shake-left");
                icon.AddToClassList("shake-right");
            }).StartingIn(60);
            icon.schedule.Execute(() => {
                icon.RemoveFromClassList("shake-right");
                icon.AddToClassList("shake-left");
            }).StartingIn(120);
            icon.schedule.Execute(() => {
                icon.RemoveFromClassList("shake-left");
                icon.AddToClassList("shake-right");
            }).StartingIn(180);
            icon.schedule.Execute(() => {
                icon.RemoveFromClassList("shake-right");
            }).StartingIn(240);
        }
    }

    private void ShowWeaponWarning()
    {
        if (weaponWarning == null) return;

        Debug.Log("Vũ khí đang bị khóa");
        weaponWarning.AddToClassList("show-warning");
        warningTimer = WARNING_DURATION;

        // Hiệu ứng rung ổ khóa qua lại
        if (lockIcon2 != null)
        {
            // Xóa các class cũ nếu có
            lockIcon2.RemoveFromClassList("shake-left");
            lockIcon2.RemoveFromClassList("shake-right");
            
            // Chuỗi rung nhanh qua lại
            lockIcon2.schedule.Execute(() => lockIcon2.AddToClassList("shake-left")).StartingIn(0);
            lockIcon2.schedule.Execute(() => {
                lockIcon2.RemoveFromClassList("shake-left");
                lockIcon2.AddToClassList("shake-right");
            }).StartingIn(60);
            lockIcon2.schedule.Execute(() => {
                lockIcon2.RemoveFromClassList("shake-right");
                lockIcon2.AddToClassList("shake-left");
            }).StartingIn(120);
            lockIcon2.schedule.Execute(() => {
                lockIcon2.RemoveFromClassList("shake-left");
                lockIcon2.AddToClassList("shake-right");
            }).StartingIn(180);
            lockIcon2.schedule.Execute(() => {
                lockIcon2.RemoveFromClassList("shake-right");
            }).StartingIn(240);
        }
    }
}