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
    private VisualElement interactionPrompt;

    private List<DialogueLine> currentLines = new List<DialogueLine>();
    private int currentLineIndex = -1;
    private bool isDialogueActive = false;
    private SimplePlayerTest activePlayer;
    private SilasNPC currentNPC;
    private bool isUIInitialized = false;

    public bool IsActive => isDialogueActive;

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
        InitializeUI();
    }

    public void InitializeUI()
    {
        if (isUIInitialized) return;

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
        interactionPrompt = root.Q<VisualElement>("silas-interaction-prompt");

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
        if (interactionPrompt != null)
        {
            interactionPrompt.RemoveFromClassList("show-prompt");
            interactionPrompt.style.display = DisplayStyle.None;
        }

        // Đăng ký sự kiện click chuột vào toàn bộ wrapper (click bất kỳ đâu trên màn hình cũng qua câu)
        if (dialogueWrapper != null)
        {
            dialogueWrapper.RegisterCallback<ClickEvent>(OnDialogueWrapperClicked);
        }

        isUIInitialized = true;
        Debug.Log("[SilasDialogueController] UI Toolkit Đối thoại đã được khởi tạo thành công!");
    }

    private void OnDisable()
    {
        if (isUIInitialized && dialogueWrapper != null)
        {
            dialogueWrapper.UnregisterCallback<ClickEvent>(OnDialogueWrapperClicked);
        }
        isUIInitialized = false;
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
                AdvanceDialogue();
            }
        }
    }

    private void OnDialogueWrapperClicked(ClickEvent evt)
    {
        if (isDialogueActive)
        {
            AdvanceDialogue();
        }
    }

    /// <summary>
    /// Bắt đầu hội thoại (Tải từ vị trí lưu trước đó)
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines, SimplePlayerTest player, SilasNPC npc, int startIndex)
    {
        InitializeUI(); // Đảm bảo khởi tạo trước khi gọi bắt đầu

        // Tự động tắt gợi ý phím G khi bắt đầu nói chuyện
        ShowPrompt(false);

        if (lines == null || lines.Count == 0) return;

        activePlayer = player;
        currentLines = lines;
        currentNPC = npc;
        
        // Tải vị trí đã lưu, nếu không hợp lệ thì bắt đầu từ 0
        if (startIndex >= 0 && startIndex < lines.Count)
        {
            currentLineIndex = startIndex;
        }
        else
        {
            currentLineIndex = 0;
        }

        isDialogueActive = true;

        if (dialogueWrapper != null)
        {
            dialogueWrapper.AddToClassList("show-wrapper");
        }

        if (dialogueBox != null)
        {
            dialogueBox.style.display = DisplayStyle.Flex;
            dialogueBox.schedule.Execute(() => {
                dialogueBox.AddToClassList("show-dialogue");
            }).StartingIn(10);
        }

        DisplayCurrentLine();
    }

    /// <summary>
    /// Hỗ trợ tương thích ngược cho StartDialogue
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines, SimplePlayerTest player)
    {
        StartDialogue(lines, player, null, 0);
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
    /// Tiến hành chuyển câu thoại. Nếu chơi Standalone thì chạy ngay, nếu chơi Multi thì đồng bộ qua Server RPC.
    /// </summary>
    public void AdvanceDialogue()
    {
        if (activePlayer == null || activePlayer.isStandaloneMode)
        {
            NextLine();
        }
        else
        {
            if (currentNPC != null)
            {
                currentNPC.RequestNextLineServerRpc();
            }
            else
            {
                NextLine();
            }
        }
    }

    /// <summary>
    /// Hiển thị hoặc ẩn ô gợi ý tương tác phím G
    /// </summary>
    public void ShowPrompt(bool show)
    {
        InitializeUI();
        if (interactionPrompt != null)
        {
            if (show)
            {
                interactionPrompt.style.display = DisplayStyle.Flex;
                interactionPrompt.schedule.Execute(() => {
                    interactionPrompt.AddToClassList("show-prompt");
                }).StartingIn(10);
                Debug.Log("[SilasDialogueController] Hiển thị gợi ý phím G trò chuyện.");
            }
            else
            {
                interactionPrompt.RemoveFromClassList("show-prompt");
                interactionPrompt.schedule.Execute(() => {
                    if (!interactionPrompt.ClassListContains("show-prompt"))
                    {
                        interactionPrompt.style.display = DisplayStyle.None;
                    }
                }).StartingIn(350);
                Debug.Log("[SilasDialogueController] Ẩn gợi ý tương tác phím G.");
            }
        }
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
        
        // Lưu tiến trình cuộc đối thoại vào NPC
        if (currentNPC != null)
        {
            if (currentLineIndex < currentLines.Count)
            {
                currentNPC.SetSavedDialogueIndex(currentLineIndex);
            }
            else
            {
                // Reset lại từ đầu khi cuộc hội thoại kết thúc trọn vẹn
                currentNPC.SetSavedDialogueIndex(0);
            }
        }

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
