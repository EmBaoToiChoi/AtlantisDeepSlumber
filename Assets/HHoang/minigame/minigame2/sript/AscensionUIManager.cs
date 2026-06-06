using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class AscensionUIManager : MonoBehaviour
{
    public AscensionManager ascensionManager;
    public StyleSheet styleSheet; // Kéo file USS vào đây trong Inspector
    
    private VisualElement countdownCircle;
    private Label countdownLabel;

    void Start()
    {
        var doc = GetComponent<UIDocument>();
        doc.rootVisualElement.Clear(); // Xóa sạch rác cũ

        // Load style
        doc.rootVisualElement.styleSheets.Add(styleSheet);

        // Tạo phần tử
        countdownCircle = new VisualElement();
        countdownCircle.AddToClassList("countdown-root");
        
        countdownLabel = new Label("0");
        countdownLabel.AddToClassList("countdown-label");
        
        countdownCircle.Add(countdownLabel);
        doc.rootVisualElement.Add(countdownCircle);
    }

    void Update()
    {
        if (ascensionManager == null || !ascensionManager.isTimerRunning.Value) 
        {
            if (countdownCircle != null) countdownCircle.style.display = DisplayStyle.None;
            return;
        }

        countdownCircle.style.display = DisplayStyle.Flex;

        double elapsed = NetworkManager.Singleton.ServerTime.Time - ascensionManager.startTime.Value;
        float remaining = Mathf.Max(0, ascensionManager.timeLimit - (float)elapsed);
        float percent = remaining / ascensionManager.timeLimit;

        // Visual logic
        countdownCircle.style.scale = new Scale(new Vector2(percent, percent));
        countdownCircle.style.backgroundColor = Color.Lerp(Color.red, new Color(0.2f, 0.2f, 0.2f, 0.5f), percent);
        countdownLabel.text = Mathf.CeilToInt(remaining).ToString();
    }
}