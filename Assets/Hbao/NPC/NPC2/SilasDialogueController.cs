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

    // Cấu trúc một nút đối thoại trong cây đối thoại rẽ nhánh
    public class DialogueStepNode
    {
        public int stepId;
        public string speakerName;
        public string text;
        public Sprite customAvatar;
        public List<DialogueChoiceOption> choices = new List<DialogueChoiceOption>();
        public int nextStepId = -1; // Nếu không có lựa chọn, tự động chuyển đến stepId này khi nhấn Tiếp tục hoặc phím. -1 là kết thúc.
    }

    public class DialogueChoiceOption
    {
        public string choiceText;
        public int nextStepId;
    }

    private Dictionary<int, DialogueStepNode> dialogueTree = new Dictionary<int, DialogueStepNode>();
    private int currentDialogueStep = 0;
    private VisualElement choicesContainer;

    private List<DialogueLine> currentLines = new List<DialogueLine>();
    private int currentLineIndex = -1;
    private bool isDialogueActive = false;
    private SimplePlayerTest activePlayer;
    private SilasNPC currentNPC;
    private bool isUIInitialized = false;
    private bool isPromptShowing = false;

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
        choicesContainer = root.Q<VisualElement>("dialogue-choices-container");

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
        if (choicesContainer != null)
        {
            choicesContainer.Clear();
            choicesContainer.style.display = DisplayStyle.None;
        }

        // Đăng ký sự kiện click chuột vào toàn bộ wrapper (click bất kỳ đâu trên màn hình cũng qua câu)
        if (dialogueWrapper != null)
        {
            dialogueWrapper.RegisterCallback<ClickEvent>(OnDialogueWrapperClicked);
        }

        // Đăng ký sự kiện click vào nút Tiếp Tục (nextButton) để chuyển dòng thoại
        if (nextButton != null)
        {
            nextButton.clicked += () => {
                if (isDialogueActive)
                    AdvanceDialogue();
            };
            // Ngăn sự kiện click lan truyền lên dialogueWrapper
            nextButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
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
        if (!isDialogueActive) return;

        VisualElement clickedElement = evt.target as VisualElement;
        if (clickedElement == null) return;

        // 1. Nếu click vào khoảng trống bên ngoài hộp thoại (chính là wrapper màu đen mờ)
        if (clickedElement == dialogueWrapper)
        {
            Debug.Log("[SilasDialogueController] Click bên ngoài hộp thoại. Đóng trò chuyện.");
            if (activePlayer == null || activePlayer.isStandaloneMode)
            {
                EndDialogue();
            }
            else
            {
                if (currentNPC != null)
                {
                    currentNPC.RequestEndDialogueServerRpc();
                }
                else
                {
                    EndDialogue();
                }
            }
            evt.StopPropagation();
        }
        // 2. Nếu click vào bên trong hộp thoại
        else if (dialogueBox != null && (clickedElement == dialogueBox || dialogueBox.Contains(clickedElement)))
        {
            // Không xử lý AdvanceDialogue nếu click vào choice button hoặc container chứa choices
            // (Choice button tự xử lý qua choiceBtn.clicked rồi)
            bool isClickOnChoices = choicesContainer != null &&
                (clickedElement == choicesContainer || choicesContainer.Contains(clickedElement));

            if (!isClickOnChoices)
            {
                Debug.Log("[SilasDialogueController] Click bên trong hộp thoại. Chuyển dòng thoại.");
                AdvanceDialogue();
            }
        }
    }

    /// <summary>
    /// Bắt đầu hội thoại (Tải từ vị trí lưu trước đó)
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines, SimplePlayerTest player, SilasNPC npc, int startIndex)
    {
        InitializeUI(); // Đảm bảo khởi tạo trước khi gọi bắt đầu

        // Khởi tạo cây đối thoại rẽ nhánh
        InitializeDialogueTree();

        // Tự động tắt gợi ý phím G khi bắt đầu nói chuyện
        ShowPrompt(false);

        activePlayer = player;
        currentNPC = npc;
        
        // Tải vị trí đã lưu, nếu không hợp lệ thì bắt đầu từ 0
        if (dialogueTree.ContainsKey(startIndex))
        {
            currentDialogueStep = startIndex;
        }
        else
        {
            currentDialogueStep = 0;
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

        DisplayCurrentStep();
    }

    /// <summary>
    /// Hỗ trợ tương thích ngược cho StartDialogue
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines, SimplePlayerTest player)
    {
        StartDialogue(lines, player, null, 0);
    }

    /// <summary>
    /// Khởi tạo cây đối thoại rẽ nhánh Silas
    /// </summary>
    private void InitializeDialogueTree()
    {
        dialogueTree.Clear();

        // Step 0: Khởi đầu cuộc trò chuyện
        DialogueStepNode step0 = new DialogueStepNode
        {
            stepId = 0,
            speakerName = "Silas",
            text = "\"Rakan vừa gửi mấy con cừu tới đây sao?\""
        };
        step0.choices.Add(new DialogueChoiceOption { choiceText = "Tôi không phải cừu, tôi là chiến binh!", nextStepId = 50 });
        step0.choices.Add(new DialogueChoiceOption { choiceText = "Ông là ai?", nextStepId = 10 });
        step0.choices.Add(new DialogueChoiceOption { choiceText = "Ông hãy tránh xa tôi ra?", nextStepId = 20 });
        step0.choices.Add(new DialogueChoiceOption { choiceText = "Không Thèm Trả lời?", nextStepId = 30 });
        dialogueTree.Add(0, step0);

        // Step 10: Nhánh hỏi "Ông là ai?"
        DialogueStepNode step10 = new DialogueStepNode
        {
            stepId = 10,
            speakerName = "Silas",
            text = "\"Ta là ai à, Hmmm… ta là lão lính gác già cả sắp chết thôi !\""
        };
        step10.choices.Add(new DialogueChoiceOption { choiceText = "Tại sao ông lại canh giữ nơi này?", nextStepId = 11 });
        dialogueTree.Add(10, step10);

        // Step 11: Nhánh hỏi "Tại sao ông lại canh giữ nơi này?"
        DialogueStepNode step11 = new DialogueStepNode
        {
            stepId = 11,
            speakerName = "Silas",
            text = "\"Ta đã đứng canh giữ chỗ này hàng thế kỷ. Nếu muốn vào sâu hơn, đừng có nhìn ta như thế. Ta không phải quái vật, ít nhất là chưa phải.\""
        };
        step11.choices.Add(new DialogueChoiceOption { choiceText = "Chúng tôi cần vào Cung điện Hoàng gia.", nextStepId = 12 });
        dialogueTree.Add(11, step11);

        // Step 12: Nhánh hỏi "Chúng tôi cần vào Cung điện Hoàng gia."
        DialogueStepNode step12 = new DialogueStepNode
        {
            stepId = 12,
            speakerName = "Silas",
            text = "\"Cung điện? Lũ ngoại lai các ngươi thật ngây thơ. Muốn mở được cánh cửa vào Phòng Ngai Vàng, các người cần 2 Viên ngọc của các khu vực để mở khóa cánh cửa cuối. Chúng là chìa khóa duy nhất để tháo mở cánh cửa của phòng Ngai Vàng.\""
        };
        step12.choices.Add(new DialogueChoiceOption { choiceText = "Tôi Cảm Ơn ông đã cảnh báo, nhưng tôi không cần", nextStepId = 13 });
        step12.choices.Add(new DialogueChoiceOption { choiceText = "Làm sao chúng tôi tìm được 2 Viên ngọc này?", nextStepId = 14 });
        dialogueTree.Add(12, step12);

        // Step 13: Cảm ơn nhưng không cần
        DialogueStepNode step13 = new DialogueStepNode
        {
            stepId = 13,
            speakerName = "Silas",
            text = "\"Chúc các ngươi may mắn.\"",
            nextStepId = -1
        };
        dialogueTree.Add(13, step13);

        // Step 14: Hỏi vị trí 3 viên ngọc
        DialogueStepNode step14 = new DialogueStepNode
        {
            stepId = 14,
            speakerName = "Silas",
            text = "\"Chúng bị phong ấn ở Rừng Sương Mù, Vực Thẳm Lửa, và Lăng Mộ Cổ. Tìm kiếm chúng chỉ có con đường chết.\""
        };
        step14.choices.Add(new DialogueChoiceOption { choiceText = "Cảm ơn lão già, chúng tôi sẽ đi lấy chúng!", nextStepId = 16 });
        dialogueTree.Add(14, step14);

        // Step 16: Phản hồi lấy ngọc
        DialogueStepNode step16 = new DialogueStepNode
        {
            stepId = 16,
            speakerName = "Silas",
            text = "\"Hahaha! Thật đáng để mong đợi. Để ta xem khi nào xác các ngươi trôi về tới đây.\"",
            nextStepId = -1
        };
        dialogueTree.Add(16, step16);

        // Step 20: Ông hãy tránh xa tôi ra
        DialogueStepNode step20 = new DialogueStepNode
        {
            stepId = 20,
            speakerName = "Silas",
            text = "\"Ta thấy ngươi khá là thú vị đấy ! Nhưng cẩn thận cái miệng của ngươi, ở đây bóng tối có tai đấy.\"",
            nextStepId = -1
        };
        dialogueTree.Add(20, step20);

        // Step 30: Không Thèm Trả lời
        DialogueStepNode step30 = new DialogueStepNode
        {
            stepId = 30,
            speakerName = "Silas",
            text = "\"Ta thấy ngươi khá là thú vị đấy ! Kẻ im lặng thường sống lâu hơn kẻ ba hoa ở thánh địa đầy rẫy tử khí này.\"",
            nextStepId = -1
        };
        dialogueTree.Add(30, step30);

        // Step 50: Chiến binh Arthur
        DialogueStepNode step50 = new DialogueStepNode
        {
            stepId = 50,
            speakerName = "Silas",
            text = "\"Hahaha! Chiến binh? Ở đây, chiến binh hay cừu non thì kết cục cũng đều nằm dưới lưỡi kiếm của hoàng gia mà thôi.\""
        };
        step50.choices.Add(new DialogueChoiceOption { choiceText = "Tại sao ông lại canh giữ nơi này?", nextStepId = 11 });
        step50.choices.Add(new DialogueChoiceOption { choiceText = "Tôi sẽ chứng minh cho ông thấy tôi khác biệt!", nextStepId = 51 });
        dialogueTree.Add(50, step50);

        // Step 51: Chứng minh khác biệt
        DialogueStepNode step51 = new DialogueStepNode
        {
            stepId = 51,
            speakerName = "Silas",
            text = "\"Khí phách đấy! Lối vào Cung điện Hoàng gia ngay phía trước, hãy đi và chứng minh lời ngươi nói xem nào.\"",
            nextStepId = -1
        };
        dialogueTree.Add(51, step51);
    }

    /// <summary>
    /// Hiển thị bước thoại hiện tại
    /// </summary>
    private void DisplayCurrentStep()
    {
        if (!dialogueTree.ContainsKey(currentDialogueStep))
        {
            EndDialogue();
            return;
        }

        DialogueStepNode node = dialogueTree[currentDialogueStep];

        // Cập nhật tên người nói
        if (speakerNameLabel != null)
            speakerNameLabel.text = node.speakerName;

        if (npcNameTag != null)
            npcNameTag.text = node.speakerName.ToUpper();

        // Lưu toàn bộ câu để dùng khi skip
        fullCurrentText = node.text;

        // Xử lý Avatar động
        Sprite finalAvatar = node.customAvatar;
        if (finalAvatar == null)
        {
            if (node.speakerName.Contains("Silas"))
            {
                finalAvatar = silasAvatar;
            }
            else if (node.speakerName.Contains("Arthur") || node.speakerName.Contains("Khiên") || node.speakerName.Contains("Người") || node.speakerName.Contains("Player"))
            {
                PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                if (hud != null && hud.hudProfiles != null && hud.hudProfiles.Count > 3)
                    finalAvatar = hud.hudProfiles[3].avatarSprite;
            }
        }
        if (avatarImage != null && finalAvatar != null)
            avatarImage.style.backgroundImage = new StyleBackground(finalAvatar);

        // Ẩn tạm thời các nút và lựa chọn khi đang gõ chữ
        if (nextButton != null)
            nextButton.style.display = DisplayStyle.None;

        if (choicesContainer != null)
        {
            choicesContainer.Clear();
            choicesContainer.style.display = DisplayStyle.None;
        }

        // Bắt đầu Typewriter
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterEffect(node.text));
    }

    /// <summary>
    /// Thiết lập hiển thị giao diện lựa chọn hoặc nút tiếp tục sau khi gõ chữ xong
    /// </summary>
    private void ShowNavigationUI()
    {
        if (!dialogueTree.ContainsKey(currentDialogueStep)) return;

        DialogueStepNode node = dialogueTree[currentDialogueStep];

        if (node.choices != null && node.choices.Count > 0)
        {
            // Ẩn nút "Tiếp tục"
            if (nextButton != null)
                nextButton.style.display = DisplayStyle.None;

            // Hiển thị danh sách lựa chọn
            if (choicesContainer != null)
            {
                choicesContainer.Clear();
                choicesContainer.style.display = DisplayStyle.Flex;

                foreach (var choice in node.choices)
                {
                    Button choiceBtn = new Button();
                    choiceBtn.text = choice.choiceText;
                    choiceBtn.AddToClassList("dialogue-choice-button");
                    
                    // Ngăn chặn sự kiện click lan truyền lên dialogueWrapper
                    choiceBtn.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

                    int targetStep = choice.nextStepId;
                    choiceBtn.clicked += () => {
                        ChooseOption(targetStep);
                    };

                    choicesContainer.Add(choiceBtn);
                }
            }
        }
        else
        {
            // Không có lựa chọn, hiển thị nút "Tiếp tục"
            if (nextButton != null)
                nextButton.style.display = DisplayStyle.Flex;

            if (choicesContainer != null)
            {
                choicesContainer.Clear();
                choicesContainer.style.display = DisplayStyle.None;
            }
        }
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
        ShowNavigationUI();
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
        ShowNavigationUI();
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
                if (isPromptShowing) return; // Đã hiển thị thì bỏ qua, tránh spam lập lịch mỗi frame
                isPromptShowing = true;

                interactionPrompt.style.display = DisplayStyle.Flex;
                interactionPrompt.schedule.Execute(() => {
                    if (isPromptShowing)
                    {
                        interactionPrompt.AddToClassList("show-prompt");
                    }
                }).StartingIn(10);
                Debug.Log("[SilasDialogueController] Hiển thị gợi ý phím G trò chuyện.");
            }
            else
            {
                if (!isPromptShowing) return; // Đã ẩn thì bỏ qua
                isPromptShowing = false;

                interactionPrompt.RemoveFromClassList("show-prompt");
                interactionPrompt.schedule.Execute(() => {
                    if (!isPromptShowing && !interactionPrompt.ClassListContains("show-prompt"))
                    {
                        interactionPrompt.style.display = DisplayStyle.None;
                    }
                }).StartingIn(350);
                Debug.Log("[SilasDialogueController] Ẩn gợi ý tương tác phím G.");
            }
        }
    }

    /// <summary>
    /// Chọn phương án và đồng bộ mạng
    /// </summary>
    public void ChooseOption(int nextStepId)
    {
        if (activePlayer == null || activePlayer.isStandaloneMode)
        {
            SelectChoice(nextStepId);
        }
        else
        {
            if (currentNPC != null)
            {
                currentNPC.RequestSelectChoiceServerRpc(nextStepId);
            }
            else
            {
                SelectChoice(nextStepId);
            }
        }
    }

    /// <summary>
    /// Xử lý chọn một nhánh và đi tiếp
    /// </summary>
    public void SelectChoice(int nextStepId)
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTyping = false;

        currentDialogueStep = nextStepId;

        // Lưu tiến trình cuộc đối thoại vào NPC
        if (currentNPC != null)
        {
            if (nextStepId != -1)
            {
                currentNPC.SetSavedDialogueIndex(nextStepId);
            }
            else
            {
                currentNPC.SetSavedDialogueIndex(0);
            }
        }

        if (nextStepId == -1)
        {
            EndDialogue();
        }
        else
        {
            DisplayCurrentStep();
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

        if (!dialogueTree.ContainsKey(currentDialogueStep))
        {
            EndDialogue();
            return;
        }

        DialogueStepNode node = dialogueTree[currentDialogueStep];

        // Nếu đang ở nút có lựa chọn mà chưa chọn gì, bắt buộc chọn, không cho bấm qua bằng click/phím thường
        if (node.choices != null && node.choices.Count > 0)
        {
            Debug.Log("[SilasDialogueController] Cần lựa chọn phương án đối thoại, không thể bỏ qua bằng Click/Phím thường.");
            return;
        }

        int nextStep = node.nextStepId;

        // Lưu tiến trình cuộc đối thoại vào NPC
        if (currentNPC != null)
        {
            if (nextStep != -1)
            {
                currentNPC.SetSavedDialogueIndex(nextStep);
            }
            else
            {
                // Reset lại từ đầu khi cuộc hội thoại kết thúc trọn vẹn
                currentNPC.SetSavedDialogueIndex(0);
            }
        }

        if (nextStep == -1)
        {
            EndDialogue();
        }
        else
        {
            currentDialogueStep = nextStep;
            DisplayCurrentStep();
        }
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
        currentDialogueStep = 0;
        activePlayer = null;

        // Ẩn nút Tiếp Tục và các lựa chọn
        if (nextButton != null)
            nextButton.style.display = DisplayStyle.None;
        if (choicesContainer != null)
        {
            choicesContainer.Clear();
            choicesContainer.style.display = DisplayStyle.None;
        }

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
