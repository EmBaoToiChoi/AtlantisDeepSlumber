using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class AscensionUIManager : MonoBehaviour
{
    public AscensionManager ascensionManager;
    public StyleSheet styleSheet; 
    
    private VisualElement countdownCircle;
    private Label countdownLabel;

    private int lastSecondsRemaining = -1;
    private float pulseTimer = 0f;
    private float fadeTimer = 0f;
    private bool isFading = false;

    // CÁC BIẾN BỔ SUNG CHO HIỆU ỨNG BIẾN HÌNH (MORPHING) MƯỢT MÀ
    private int lastScore = -1; 
    private float morphProgress = 0f;
    private float morphDuration = 0.4f; // Thời gian giãn từ hình tròn ra chữ nhật (0.4 giây)

    void Start()
    {
        var doc = GetComponent<UIDocument>();
        doc.rootVisualElement.Clear(); 

        doc.rootVisualElement.styleSheets.Add(styleSheet);

        countdownCircle = new VisualElement();
        countdownCircle.AddToClassList("countdown-root");
        
        countdownLabel = new Label("0");
        countdownLabel.AddToClassList("countdown-label");
        
        countdownCircle.Add(countdownLabel);
        doc.rootVisualElement.Add(countdownCircle);

        // Căn giữa chữ ở bên trong khung
        countdownCircle.style.justifyContent = Justify.Center;
        countdownCircle.style.alignItems = Align.Center;
        countdownCircle.style.overflow = Overflow.Hidden; 

        // =================================================================
        // THÊM DÒNG NÀY: Bắt ép cái khung luôn tự động căn giữa màn hình. 
        // Khi width thay đổi (80 -> 320), nó sẽ tự động giãn đều ra 2 bên!
        // =================================================================
        countdownCircle.style.alignSelf = Align.Center; 
    }

    void Update()
    {
        if (ascensionManager == null) return;

        // =================================================================
        // TRẠNG THÁI 1: ĐANG ĐẾM NGƯỢC
        // =================================================================
        if (ascensionManager.isTimerRunning.Value)
        {
            isFading = false;
            fadeTimer = 0f;
            lastScore = -1; // Reset để chuẩn bị sẵn sàng cho hiệu ứng biến hình lần sau
            countdownCircle.style.opacity = 1f;
            countdownCircle.style.display = DisplayStyle.Flex;

            // KIỂU DÁNG: Khóa cố định hình TRÒN 80x80
            countdownCircle.style.width = 80;
            countdownCircle.style.height = 80;
            countdownCircle.style.borderTopLeftRadius = 40;
            countdownCircle.style.borderTopRightRadius = 40;
            countdownCircle.style.borderBottomLeftRadius = 40;
            countdownCircle.style.borderBottomRightRadius = 40;

            countdownLabel.style.fontSize = 28; 

            double elapsed = NetworkManager.Singleton.ServerTime.Time - ascensionManager.startTime.Value;
            float remaining = Mathf.Max(0, ascensionManager.timeLimit - (float)elapsed);
            float percent = remaining / ascensionManager.timeLimit;

            int currentSeconds = Mathf.CeilToInt(remaining);
            countdownLabel.text = currentSeconds.ToString();

            countdownLabel.style.color = Color.Lerp(Color.red, Color.white, percent);

            // Logic nhịp nảy nhẹ (giật 7%)
            if (currentSeconds != lastSecondsRemaining)
            {
                lastSecondsRemaining = currentSeconds;
                pulseTimer = 0.25f;
            }

            if (pulseTimer > 0)
            {
                pulseTimer -= Time.deltaTime;
                float progress = (0.25f - pulseTimer) / 0.25f;
                float scaleValue = 1f + Mathf.Sin(progress * Mathf.PI) * 0.07f; 
                countdownLabel.style.scale = new Scale(new Vector2(scaleValue, scaleValue));
            }
            else
            {
                countdownLabel.style.scale = new Scale(Vector2.one);
            }

            return;
        }

        // =================================================================
        // TRẠNG THÁI 2: VỪA ĐẶT ĐỦ 4 VIÊN NGỌC (Kết quả)
        // =================================================================
        int score = ascensionManager.correctCrystalsCount.Value;
        if (score >= 0)
        {
            countdownCircle.style.display = DisplayStyle.Flex;
            countdownLabel.style.scale = new Scale(Vector2.one);

            // KÍCH HOẠT BIẾN HÌNH VÀO FRAME ĐẦU TIÊN
            if (lastScore == -1)
            {
                lastScore = score;
                morphProgress = 0f; // Bắt đầu tính giờ giãn khung
                isFading = true;
                fadeTimer = 4.0f; // Tổng thời gian hiển thị 4 giây
                
                if (score == 4)
                {
                    countdownLabel.text = "Đã hoàn thành thử thách";
                    countdownLabel.style.color = Color.green;
                }
                else
                {
                    // === ĐỔI TEXT Ở ĐÂY ===
                    countdownLabel.text = "Đã kích hoạt: " + score + "/4";
                    
                    // Gợi ý: Nếu text dài rồi thì chữ màu đỏ chót (Color.red) nhìn hơi chói. 
                    // Có thể đổi thành màu cam hoặc vàng cho mượt mắt hơn.
                    countdownLabel.style.color = new Color(1f, 0.5f, 0f); // Màu cam (Orange)
                }
            }

            // CHẠY HIỆU ỨNG TỪ TỪ KÉO GIÃN KHUNG (MORPHING)
            if (morphProgress < 1f)
            {
                morphProgress += Time.deltaTime / morphDuration;
                float t = Mathf.Clamp01(morphProgress); // t chạy từ 0 đến 1

                // Chuyển đổi mượt mà các kích thước: Rộng 80->320, Cao 80->90, Bo góc 40->45
                countdownCircle.style.width = Mathf.Lerp(80f, 420f, t);
                countdownCircle.style.height = Mathf.Lerp(80f, 90f, t);
                
                float currentRadius = Mathf.Lerp(40f, 45f, t);
                countdownCircle.style.borderTopLeftRadius = currentRadius;
                countdownCircle.style.borderTopRightRadius = currentRadius;
                countdownCircle.style.borderBottomLeftRadius = currentRadius;
                countdownCircle.style.borderBottomRightRadius = currentRadius;
            }
            else
            {
                // Chốt thông số ở mức tối đa khi giãn xong
                countdownCircle.style.width = 420;
                countdownCircle.style.height = 90;
                countdownCircle.style.borderTopLeftRadius = 45;
                countdownCircle.style.borderTopRightRadius = 45;
                countdownCircle.style.borderBottomLeftRadius = 45;
                countdownCircle.style.borderBottomRightRadius = 45;
            }

            // Xử lý đếm ngược mờ dần sau 4 giây
            if (fadeTimer > 0)
            {
                fadeTimer -= Time.deltaTime;
                if (fadeTimer <= 1.0f)
                {
                    // 1 giây cuối cùng mờ dần (Lerp opacity từ 1 về 0)
                    countdownCircle.style.opacity = fadeTimer;
                }
            }
            else
            {
                countdownCircle.style.display = DisplayStyle.None;
            }

            lastSecondsRemaining = -1;
            return;
        }

        // =================================================================
        // TRẠNG THÁI MẶC ĐỊNH: ẨN KHI KHÔNG HOẠT ĐỘNG
        // =================================================================
        if (!isFading)
        {
            countdownCircle.style.display = DisplayStyle.None;
        }
    }
}