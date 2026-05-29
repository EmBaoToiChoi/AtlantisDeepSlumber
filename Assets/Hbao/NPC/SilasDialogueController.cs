using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class SilasDialogueController : MonoBehaviour
{
    public static SilasDialogueController Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Sprite silasAvatar; // Kéo thả ảnh silas_avatar.png vào đây trong Inspector

    private VisualElement dialogueBox;
    private VisualElement avatarImage;
    private Label speakerNameLabel;
    private Label dialogueTextLabel;
    private Label npcNameTag;

    private List<DialogueLine> currentLines = new List<DialogueLine>();
    private int currentLineIndex = -1;
    private bool isDialogueActive = false;
    private SimplePlayerTest activePlayer;

    // Typewriter effect
    [Header("Typewriter Settings")]
    [SerializeField] [Tooltip("Tốc độ gõ chữ: giây/ký tự (0.03 = nhanh, 0.08 = chậm)")] 
    private float typewriterSpeed = 0.04f;
    private Coroutine typewriterCoroutine;
    private bool isTyping = false;
    private string fullCurrentText = "";

    [System.Serializable]
    public struct DialogueLine
    {
        public string speakerName;
        [TextArea(3, 5)]
        public string text;
        public Sprite customAvatar;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private Button nextButton;
    private VisualElement dialogueWrapper;

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var root = uiDocument.rootVisualElement;
        dialogueWrapper = root.Q<VisualElement>("silas-dialogue-wrapper");
        dialogueBox = root.Q<VisualElement>("silas-dialogue-box");
        avatarImage = root.Q<VisualElement>("avatar-image");
        speakerNameLabel = root.Q<Label>("speaker-name");
        dialogueTextLabel = root.Q<Label>("dialogue-text");
        npcNameTag = root.Q<Label>("npc-name-tag");
        nextButton = root.Q<Button>("next-btn");

        // Ẩn hộp đối thoại và wrapper ban đầu
        if (dialogueWrapper != null)
        {
            dialogueWrapper.RemoveFromClassList("show-wrapper");
        }
        if (dialogueBox != null)
        {
            dialogueBox.RemoveFromClassList("show-dialogue");
            dialogueBox.style.display = DisplayStyle.None;
        }

        // Đăng ký sự kiện click chuột vào toàn bộ wrapper (click bất kỳ đâu trên màn hình cũng qua câu)
        if (dialogueWrapper != null)
        {
            dialogueWrapper.RegisterCallback<ClickEvent>(OnDialogueWrapperClicked);
        }
    }

    private void OnDisable()
    {
        if (dialogueWrapper != null)
        {
            dialogueWrapper.UnregisterCallback<ClickEvent>(OnDialogueWrapperClicked);
        }
    }

    private void Update()
    {
        if (!isDialogueActive) return;

        // Lắng nghe phím F hoặc Space hoặc Enter để qua câu thoại bằng phím
        if (Keyboard.current != null)
        {
            if (Keyboard.current.fKey.wasPressedThisFrame || 
                Keyboard.current.spaceKey.wasPressedThisFrame || 
                Keyboard.current.enterKey.wasPressedThisFrame)
            {
                NextLine();
            }
        }
    }

    private void OnDialogueWrapperClicked(ClickEvent evt)
    {
        if (isDialogueActive)
        {
            NextLine();
        }
    }

    /// <summary>
    /// Bắt đầu hội thoại
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines, SimplePlayerTest player)
    {
        if (lines == null || lines.Count == 0) return;

        activePlayer = player;
        currentLines = lines;
        currentLineIndex = 0;
        isDialogueActive = true;

        if (dialogueWrapper != null)
        {
            dialogueWrapper.AddToClassList("show-wrapper");
        }

        if (dialogueBox != null)
        {
            dialogueBox.style.display = DisplayStyle.Flex;
            // Gọi chuyển cảnh xuất hiện mượt mà sau 1 frame để USS Transition hoạt động
            dialogueBox.schedule.Execute(() => {
                dialogueBox.AddToClassList("show-dialogue");
            }).StartingIn(10);
        }

        DisplayCurrentLine();
    }

    /// <summary>
    /// Hiển thị câu thoại hiện tại với hiệu ứng Typewriter
    /// </summary>
    private void DisplayCurrentLine()
    {
        if (currentLineIndex < 0 || currentLineIndex >= currentLines.Count)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = currentLines[currentLineIndex];

        // Cập nhật tên người nói ngay lập tức
        if (speakerNameLabel != null)
            speakerNameLabel.text = line.speakerName;

        if (npcNameTag != null)
            npcNameTag.text = line.speakerName.ToUpper();

        // Lưu toàn bộ câu để dùng khi skip
        fullCurrentText = line.text;

        // Xử lý Avatar động
        Sprite finalAvatar = line.customAvatar;
        if (finalAvatar == null)
        {
            if (line.speakerName.Contains("Silas"))
            {
                finalAvatar = silasAvatar;
            }
            else if (line.speakerName.Contains("Arthur") || line.speakerName.Contains("Khiên"))
            {
                PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                if (hud != null && hud.hudProfiles != null && hud.hudProfiles.Count > 3)
                    finalAvatar = hud.hudProfiles[3].avatarSprite;
            }
        }
        if (avatarImage != null && finalAvatar != null)
            avatarImage.style.backgroundImage = new StyleBackground(finalAvatar);

        // Bắt đầu Typewriter
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterEffect(line.text));
    }

    /// <summary>
    /// Coroutine gõ chữ từng ký tự
    /// </summary>
    private IEnumerator TypewriterEffect(string fullText)
    {
        isTyping = true;
        if (dialogueTextLabel != null)
            dialogueTextLabel.text = "";

        foreach (char c in fullText)
        {
            if (dialogueTextLabel != null)
                dialogueTextLabel.text += c;
            yield return new WaitForSeconds(typewriterSpeed);
        }

        isTyping = false;
        typewriterCoroutine = null;
    }

    /// <summary>
    /// Hiện toàn bộ text ngay lập tức (skip typewriter)
    /// </summary>
    private void SkipTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTyping = false;
        if (dialogueTextLabel != null)
            dialogueTextLabel.text = fullCurrentText;
    }

    /// <summary>
    /// Chuyển sang câu tiếp theo. Nếu đang gõ thì skip trước.
    /// </summary>
    public void NextLine()
    {
        // Nếu đang gõ thì hiện hết câu hiện tại trước, không chuyển
        if (isTyping)
        {
            SkipTypewriter();
            return;
        }
        currentLineIndex++;
        DisplayCurrentLine();
    }

    /// <summary>
    /// Kết thúc đối thoại và ẩn UI
    /// </summary>
    public void EndDialogue()
    {
        if (!isDialogueActive) return;

        // Dừng typewriter nếu đang chạy
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTyping = false;

        isDialogueActive = false;
        currentLineIndex = -1;
        activePlayer = null;

        if (dialogueWrapper != null)
        {
            dialogueWrapper.RemoveFromClassList("show-wrapper");
        }

        if (dialogueBox != null)
        {
            dialogueBox.RemoveFromClassList("show-dialogue");
            
            // Đợi hiệu ứng Fade-out hoàn thành rồi ẩn hẳn display
            dialogueBox.schedule.Execute(() => {
                if (!isDialogueActive)
                {
                    dialogueBox.style.display = DisplayStyle.None;
                }
            }).StartingIn(350);
        }
    }
}
