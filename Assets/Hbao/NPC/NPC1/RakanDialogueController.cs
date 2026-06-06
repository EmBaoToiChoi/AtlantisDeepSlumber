using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class RakanDialogueController : MonoBehaviour
{
    public static RakanDialogueController Instance { get; private set; }
    public static bool HasFinishedStoryOnce = false;

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Sprite rakanAvatar; // Kéo thả ảnh rakan_avatar.png vào đây trong Inspector

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
    private IPlayerHUDTarget activePlayer;
    public static IPlayerHUDTarget LocalPlayerTarget { get; set; }
    private RakanNPC currentNPC;
    private bool isUIInitialized = false;
    private bool isPromptShowing = false;

    public bool IsActive => isDialogueActive;
    public bool IsTyping => isTyping;

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
        Debug.Log("[RakanDialogueController] UI Toolkit Đối thoại đã được khởi tạo thành công!");
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

        // Lắng nghe phím F, Space, Enter hoặc G để qua câu thoại bằng phím
        if (Keyboard.current != null)
        {
            if (Keyboard.current.fKey.wasPressedThisFrame || 
                Keyboard.current.spaceKey.wasPressedThisFrame || 
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.gKey.wasPressedThisFrame)
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
            Debug.Log("[RakanDialogueController] Click bên ngoài hộp thoại. Đóng trò chuyện.");
            if (activePlayer == null || activePlayer.isStandaloneMode || currentDialogueStep == 999)
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
                Debug.Log("[RakanDialogueController] Click bên trong hộp thoại. Chuyển dòng thoại.");
                AdvanceDialogue();
            }
        }
    }

    /// <summary>
    /// Bắt đầu hội thoại (Tải từ vị trí lưu trước đó)
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines, IPlayerHUDTarget player, RakanNPC npc, int startIndex)
    {
        InitializeUI(); // Đảm bảo khởi tạo trước khi gọi bắt đầu

        // Khởi tạo cây đối thoại rẽ nhánh
        InitializeDialogueTree();

        // Tự động tắt gợi ý phím G khi bắt đầu nói chuyện
        ShowPrompt(false);

        // Tự động ẩn thông báo nhiệm vụ khi bắt đầu hội thoại
        PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
        if (hud != null)
        {
            hud.HideMissionAlert();
        }

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
    public void StartDialogue(List<DialogueLine> lines, IPlayerHUDTarget player)
    {
        StartDialogue(lines, player, null, 0);
    }

    private void InitializeDialogueTree()
    {
        dialogueTree.Clear();

        // Step 0: Truyền thuyết Atlantis của Rakan - Khúc 1
        DialogueStepNode step0 = new DialogueStepNode
        {
            stepId = 0,
            speakerName = "Rakan",
            text = "\"Atlantis sở hữu những công nghệ vượt bậc, bỏ xa mọi giới hạn của trí tuệ con người. Nơi này từng là một hòn đảo huy hoàng rực sáng rực rỡ, một Utopia thực sự...\"",
            nextStepId = 1
        };
        dialogueTree.Add(0, step0);

        // Step 1: Truyền thuyết Atlantis của Rakan - Khúc 2
        DialogueStepNode step1 = new DialogueStepNode
        {
            stepId = 1,
            speakerName = "Rakan",
            text = "\"Nơi người ta ngạo mạn tin rằng mình đã nắm giữ được quyền năng của các vị thần. Thế nhưng, do Đức Vua lạm dụng năng lượng viên ngọc bên trong \\\"Thương Thần\\\" để thúc đẩy sự phát triển đến độ cực hạn...\"",
            nextStepId = 2
        };
        dialogueTree.Add(1, step1);

        // Step 2: Truyền thuyết Atlantis của Rakan - Khúc 3
        DialogueStepNode step2 = new DialogueStepNode
        {
            stepId = 2,
            speakerName = "Rakan",
            text = "\"... khiến viên ngọc hết năng lượng. Không có nguồn năng lượng ấy, hòn đảo đã chìm dưới đáy đại dương từ rất lâu, chìm sâu vào bóng tối mà không thấy được ánh sáng.\"",
            nextStepId = 3
        };
        dialogueTree.Add(2, step2);

        // Step 3: Truyền thuyết Atlantis của Rakan - Khúc 4
        DialogueStepNode step3 = new DialogueStepNode
        {
            stepId = 3,
            speakerName = "Rakan",
            text = "\"Chính sai lầm đó đã khiến người dân biến dị từ từ thành những con quái vật chỉ biết cắn xé. Lão cảnh báo các ngươi: Viên Ngọc chính là mỏ neo giữ hòn đảo khỏi việc chìm lại xuống đáy biển!\"",
            nextStepId = -1
        };
        dialogueTree.Add(3, step3);

        // Step 100: Đối thoại sau khi tiêu diệt quái vật - Khúc 1
        DialogueStepNode step100 = new DialogueStepNode
        {
            stepId = 100,
            speakerName = "Rakan",
            text = "\"Các ngươi làm ta nhớ tới ta lúc còn trẻ, ta còn dũng mãnh hơn các ngươi gấp trăm lần! Chính vì nhìn các ngươi làm ta thấy chính mình lúc còn trẻ...\"",
            nextStepId = 101
        };
        dialogueTree.Add(100, step100);

        // Step 101: Đối thoại sau khi tiêu diệt quái vật - Khúc 2
        DialogueStepNode step101 = new DialogueStepNode
        {
            stepId = 101,
            speakerName = "Rakan",
            text = "\"... nên ta sẽ trao lại cho các ngươi các món vũ khí do chính đôi tay của ta chế tạo. Ta từng là Thợ rèn và cũng là Cận vệ của nhà Vua.\"",
            nextStepId = 102
        };
        dialogueTree.Add(101, step101);

        // Step 102: Đối thoại sau khi tiêu diệt quái vật - Khúc 3
        DialogueStepNode step102 = new DialogueStepNode
        {
            stepId = 102,
            speakerName = "Rakan",
            text = "\"Các ngươi thấy cái rương ở phía kia không? Đó là món quà của ta, hãy đến đó và nhận lấy nó!\"",
            nextStepId = -1
        };
        dialogueTree.Add(102, step102);

        // Đếm tổng số người chơi kết nối trong phòng
        int totalPlayers = 1;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients != null)
        {
            totalPlayers = NetworkManager.Singleton.ConnectedClients.Count;
        }
        else
        {
            LeoPlayer[] players = FindObjectsOfType<LeoPlayer>();
            if (players != null && players.Length > 0) totalPlayers = players.Length;
        }

        // Step 999: Thông báo yêu cầu đủ toàn bộ người chơi đứng gần
        DialogueStepNode step999 = new DialogueStepNode
        {
            stepId = 999,
            speakerName = "Rakan",
            text = $"\"Hãy gọi bạn các ngươi đến đây! Ta chỉ đối thoại khi có đủ {totalPlayers} chiến binh tụ họp tại đây.\"",
            nextStepId = -1
        };
        dialogueTree.Add(999, step999);
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
            if (node.speakerName.Contains("Rakan"))
            {
                finalAvatar = rakanAvatar;
            }
            else if (node.speakerName.Contains("Arthur") || node.speakerName.Contains("Khiên") || node.speakerName.Contains("Người") || node.speakerName.Contains("Player"))
            {
                int selectedChar = PlayerPrefs.GetInt("SelectedCharacterId", 0);
                PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                if (hud != null && hud.hudProfiles != null && selectedChar >= 0 && selectedChar < hud.hudProfiles.Count)
                    finalAvatar = hud.hudProfiles[selectedChar].avatarSprite;
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
        // Standalone mode, câu chuyện Rakan "Hãy gọi bạn" (999), hoặc câu chuyện đã kể xong → xử lý local
        if (activePlayer == null || activePlayer.isStandaloneMode || currentDialogueStep == 999 || HasFinishedStoryOnce)
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
                Debug.Log("[RakanDialogueController] Hiển thị gợi ý phím G trò chuyện.");
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
                Debug.Log("[RakanDialogueController] Ẩn gợi ý tương tác phím G.");
            }
        }
    }

    /// <summary>
    /// Chọn phương án và đồng bộ mạng
    /// </summary>
    public void ChooseOption(int nextStepId)
    {
        if (activePlayer == null || activePlayer.isStandaloneMode || currentDialogueStep == 999 || HasFinishedStoryOnce)
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
            Debug.Log("[RakanDialogueController] Cần lựa chọn phương án đối thoại, không thể bỏ qua bằng Click/Phím thường.");
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
            bool wasStoryStep = (currentDialogueStep == 3);
            bool wasDefeatedStep = (currentDialogueStep == 102);

            if (wasStoryStep)
            {
                HasFinishedStoryOnce = true;
                PlayerPrefs.SetInt("RakanDialogueFinished", 1);
                PlayerPrefs.Save();
                Debug.Log("[RakanDialogueController] Đã hoàn thành câu chuyện Rakan. Sẵn sàng kích hoạt Spawner cửa!");
            }

            if (activePlayer != null && !activePlayer.isStandaloneMode && currentNPC != null)
            {
                currentNPC.RequestEndDialogueServerRpc(wasStoryStep, wasDefeatedStep);
            }
            else
            {
                if (currentNPC != null)
                {
                    currentNPC.CheckAndEnableGateSpawner();
                    if (wasDefeatedStep)
                    {
                        currentNPC.EnableGiftChest();
                    }
                }
                EndDialogue();
            }
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
