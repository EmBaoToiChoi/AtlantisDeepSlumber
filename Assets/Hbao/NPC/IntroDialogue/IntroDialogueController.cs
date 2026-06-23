using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class IntroDialogueController : MonoBehaviour
{
    public static IntroDialogueController Instance { get; private set; }

    [Header("UI Resources")]
    [Tooltip("Kéo thả IntroDialogue.uxml vào đây")]
    [SerializeField] private VisualTreeAsset dialogueUxml;
    
    [Tooltip("Kéo thả IntroDialogue.uss vào đây")]
    [SerializeField] private StyleSheet dialogueUss;

    [Header("NPC Configuration")]
    [SerializeField] private string defaultNpcName = "Ông Lão Dẫn Đường";
    
    [Header("Typewriter Settings")]
    [SerializeField] [Tooltip("Tốc độ gõ chữ (giây/ký tự)")] 
    private float typewriterSpeed = 0.04f;

    [Header("Trigger Options")]
    [SerializeField] [Tooltip("Tự động chạy hội thoại ngay khi load vào scene")]
    private bool triggerOnStart = true;

    [System.Serializable]
    public struct DialogueLine
    {
        public string speakerName;
        [TextArea(3, 5)]
        public string text;
    }

    [Header("Dialogue Content (Opening)")]
    [SerializeField] private List<DialogueLine> dialogueLines = new List<DialogueLine>();

    [Header("Dialogue Content (Bridge Collapse)")]
    [SerializeField] private List<DialogueLine> bridgeCollapseLines = new List<DialogueLine>();

    [Header("NPC Animation & Movement")]
    [Tooltip("Kéo thả Animator của NPC vào đây")]
    [SerializeField] private Animator npcAnimator;

    [Tooltip("Kéo thả Transform của NPC muốn di chuyển. Nếu để trống sẽ lấy Transform chứa Animator hoặc GameObject này.")]
    [SerializeField] private Transform npcTransform;

    [Tooltip("Tên tham số Animator khi nói chuyện (ví dụ: TroTruyen - kiểu Bool hoặc Trigger)")]
    [SerializeField] private string talkAnimParam = "TroTruyen";

    [Tooltip("Vị trí chỉ định mà NPC sẽ đi tới sau khi nói xong")]
    [SerializeField] private Transform npcMoveTarget;

    [Tooltip("Tên tham số Animator khi di chuyển (ví dụ: DiChuyen - kiểu Bool hoặc Float)")]
    [SerializeField] private string moveAnimParam = "DiChuyen";

    [Tooltip("Tốc độ di chuyển của NPC (m/s)")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Tốc độ xoay của NPC khi di chuyển")]
    [SerializeField] private float turnSpeed = 10f;

    [Tooltip("Khoảng cách tối thiểu để xác nhận đã đến vị trí target")]
    [SerializeField] private float stoppingDistance = 0.15f;

    private int currentLineIndex = 0;
    private bool isDialogueActive = false;
    private bool isTyping = false;
    private string currentLineText = "";
    private Coroutine typewriterCoroutine;
    private Coroutine npcMoveCoroutine;

    // Danh sách dòng thoại hiện đang hoạt động
    private List<DialogueLine> activeLines;

    // Lớp nội bộ lưu trữ tham chiếu VisualElement của từng bản sao UI (cho chế độ split-screen/coop)
    private class DialogueUIInstance
    {
        public VisualElement wrapperElement;
        public VisualElement boxElement;
        public Label speakerLabel;
        public Label textLabel;
        public Button nextButton;
        public Button skipButton;
    }

    private List<DialogueUIInstance> instantiatedDialogues = new List<DialogueUIInstance>();

    public bool IsActive => isDialogueActive;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Tự khởi tạo hội thoại mặc định nếu Inspector bị trống
        if (dialogueLines.Count == 0)
        {
            dialogueLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "Phía xa xa kia là lâu đài..."
            });
            dialogueLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "Trước mắt chúng ta cần phải đi qua chỗ này."
            });
        }

        if (bridgeCollapseLines.Count == 0)
        {
            bridgeCollapseLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "Cầu đã sập, các ngươi hãy đi tìm gỗ để sửa chữa chúng."
            });
        }

        // Auto-find components nếu thiếu
        if (npcAnimator == null)
        {
            npcAnimator = GetComponentInChildren<Animator>();
        }

        if (npcTransform == null)
        {
            npcTransform = (npcAnimator != null) ? npcAnimator.transform : transform;
        }
    }

    private IEnumerator Start()
    {
        // Chờ 0.2 giây để đảm bảo PlayerHUDManager và các Player Prefab được spawn/init hoàn chỉnh
        yield return new WaitForSeconds(0.2f);

        if (triggerOnStart)
        {
            StartDialogue();
        }
    }

    private void Update()
    {
        if (!isDialogueActive) return;

        // Cho phép dùng bàn phím để qua thoại hoặc bỏ qua nhanh
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame || 
                Keyboard.current.enterKey.wasPressedThisFrame || 
                Keyboard.current.fKey.wasPressedThisFrame)
            {
                AdvanceDialogue();
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SkipAllDialogue();
            }
        }
    }

    /// <summary>
    /// Bắt đầu hội thoại, khóa di chuyển của player và mở khóa chuột
    /// </summary>
    public void StartDialogue(List<DialogueLine> customLines = null)
    {
        if (isDialogueActive) return;
        
        // Chọn danh sách thoại hoạt động
        activeLines = (customLines != null && customLines.Count > 0) ? customLines : dialogueLines;

        isDialogueActive = true;
        currentLineIndex = 0;

        // Khóa phím di chuyển/hành động của Player thông qua biến tĩnh
        PlayerHUDController.isAnyUIOpen = true;

        // CỰC KỲ QUAN TRỌNG: Tự động hiển thị và mở khóa cursor trực tiếp để đảm bảo hoạt động trong mọi hoàn cảnh
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        // Tìm tất cả Player và mở khóa chuột
        var players = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p is IPlayerHUDTarget hudTarget)
            {
                if (hudTarget.IsOwner || hudTarget.IsStandaloneMode)
                {
                    hudTarget.SetCursorLock(false);
                }
            }
        }

        // Khởi tạo các bản sao VisualElement trên HUD của các người chơi
        SetupUIInstances();

        // Kích hoạt hiệu ứng hiển thị UI đối thoại
        SetWrapperClass("show-wrapper", true);
        SetBoxClass("show-dialogue", true);

        // Kích hoạt animation nói chuyện của NPC
        SetNpcTalking(true);

        // Chạy câu đầu tiên
        PlayDialogueLine(currentLineIndex);
    }

    /// <summary>
    /// Kích hoạt nhanh câu thoại khi cầu bị sập
    /// </summary>
    public void StartBridgeCollapseDialogue()
    {
        StartDialogue(bridgeCollapseLines);
    }

    /// <summary>
    /// Kích hoạt nhanh câu thoại nhắc nhở xây cầu
    /// </summary>
    public void StartReadyToBuildDialogue()
    {
        List<DialogueLine> readyLines = new List<DialogueLine>();
        readyLines.Add(new DialogueLine
        {
            speakerName = defaultNpcName,
            text = "4 người các ngươi hãy lại đây ấn F và click liên tục để xây cầu"
        });
        StartDialogue(readyLines);
    }

    /// <summary>
    /// Tìm tất cả Player HUD đang hoạt động và thêm UI đối thoại vào root của họ
    /// </summary>
    private void SetupUIInstances()
    {
        instantiatedDialogues.Clear();

        // Tìm tất cả Player HUD Controller đang active trong scene
        PlayerHUDController[] hudControllers = FindObjectsOfType<PlayerHUDController>();

        if (hudControllers != null && hudControllers.Length > 0)
        {
            foreach (var hud in hudControllers)
            {
                var uiDoc = hud.GetComponent<UIDocument>();
                if (uiDoc != null && uiDoc.rootVisualElement != null)
                {
                    CreateAndRegisterUIInstance(uiDoc.rootVisualElement);
                }
            }
        }

        // Fallback: nếu không tìm thấy HUD nào active (test đơn lẻ trong cảnh trống), dùng chính UIDocument của script này
        if (instantiatedDialogues.Count == 0)
        {
            var localUiDoc = GetComponent<UIDocument>();
            if (localUiDoc != null)
            {
                // Tự động giải quyết Panel Settings nếu bị trống để đảm bảo luôn vẽ UI
                ResolvePanelSettings();

                // CỰC KỲ QUAN TRỌNG: Thiết lập sortingOrder cực cao cho UIDocument cục bộ này để nó luôn vẽ trên cùng các HUD khác, đảm bảo nhận được click chuột!
                localUiDoc.sortingOrder = 999;

                if (localUiDoc.rootVisualElement != null)
                {
                    CreateAndRegisterUIInstance(localUiDoc.rootVisualElement);
                }
            }
        }
        
        Debug.Log($"[IntroDialogueController] Đã thiết lập xong {instantiatedDialogues.Count} bản sao UI thoại.");
    }

    /// <summary>
    /// Tự động sao chép Panel Settings từ UIDocument khác trong Scene nếu Panel Settings hiện tại bị trống
    /// </summary>
    private void ResolvePanelSettings()
    {
        var localUiDoc = GetComponent<UIDocument>();
        if (localUiDoc != null && localUiDoc.panelSettings == null)
        {
            var otherDocs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in otherDocs)
            {
                if (doc != localUiDoc && doc.panelSettings != null)
                {
                    localUiDoc.panelSettings = doc.panelSettings;
                    Debug.Log($"[IntroDialogueController] Tự động khắc phục lỗi thiếu Panel Settings bằng cách sao chép từ: {doc.gameObject.name}");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Nhân bản UXML và đăng ký callback sự kiện cho từng bản sao UI
    /// </summary>
    private void CreateAndRegisterUIInstance(VisualElement rootElement)
    {
        if (dialogueUxml == null)
        {
            Debug.LogError("[IntroDialogueController] visualTreeAsset (dialogueUxml) chưa được kéo thả vào Inspector!");
            return;
        }

        // Clone Visual Tree
        TemplateContainer template = dialogueUxml.CloneTree();
        
        // CỰC KỲ QUAN TRỌNG: Thiết lập TemplateContainer giãn đều bao trùm toàn bộ màn hình HUD
        // Tránh lỗi kích thước mặc định 0x0 khiến UI bên trong bị co nhỏ và không hiển thị!
        template.style.position = Position.Absolute;
        template.style.width = Length.Percent(100f);
        template.style.height = Length.Percent(100f);
        template.style.left = 0f;
        template.style.top = 0f;
        template.style.right = 0f;
        template.style.bottom = 0f;
        template.pickingMode = PickingMode.Position; // Đảm bảo template thu nhận sự kiện click chuột

        // Gán stylesheet nếu có
        if (dialogueUss != null)
        {
            template.styleSheets.Add(dialogueUss);
        }

        // Đẩy Visual Tree vào Root Visual Element của HUD
        rootElement.Add(template);

        // Truy xuất các elements con trong bản sao
        var wrapper = template.Q<VisualElement>("intro-dialogue-wrapper");
        var box = template.Q<VisualElement>("intro-dialogue-box");
        var speakerLabel = template.Q<Label>("intro-speaker-name");
        var textLabel = template.Q<Label>("intro-dialogue-text");
        var nextBtn = template.Q<Button>("intro-next-btn");
        var skipBtn = template.Q<Button>("intro-skip-btn");

        // CỰC KỲ QUAN TRỌNG: Đảm bảo thiết lập PickingMode.Position cho tất cả phần tử tương tác
        if (wrapper != null) wrapper.pickingMode = PickingMode.Position;
        if (box != null) box.pickingMode = PickingMode.Position;
        if (nextBtn != null) nextBtn.pickingMode = PickingMode.Position;
        if (skipBtn != null) skipBtn.pickingMode = PickingMode.Position;

        DialogueUIInstance instance = new DialogueUIInstance
        {
            wrapperElement = wrapper,
            boxElement = box,
            speakerLabel = speakerLabel,
            textLabel = textLabel,
            nextButton = nextBtn,
            skipButton = skipBtn
        };

        // Đăng ký sự kiện click vào Wrapper (click màn hình để skip nhẹ/advance)
        if (wrapper != null)
        {
            wrapper.RegisterCallback<ClickEvent>(OnDialogueWrapperClicked);
        }

        // Đăng ký sự kiện cho nút Tiếp tục
        if (nextBtn != null)
        {
            nextBtn.clicked += () => {
                if (isDialogueActive) AdvanceDialogue();
            };
            nextBtn.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
        }

        // Đăng ký sự kiện cho nút SKIP
        if (skipBtn != null)
        {
            skipBtn.clicked += () => {
                if (isDialogueActive) SkipAllDialogue();
            };
            skipBtn.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
        }

        instantiatedDialogues.Add(instance);
    }

    private void OnDialogueWrapperClicked(ClickEvent evt)
    {
        if (!isDialogueActive) return;
        
        // Tiến hành skip chữ chạy hoặc chuyển dòng thoại
        AdvanceDialogue();
        evt.StopPropagation();
    }

    /// <summary>
    /// Hiển thị câu thoại ở vị trí index
    /// </summary>
    private void PlayDialogueLine(int index)
    {
        if (activeLines == null || index < 0 || index >= activeLines.Count)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = activeLines[index];
        SetSpeakerName(line.speakerName);
        SetDialogueText("");

        currentLineText = line.text;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }

        typewriterCoroutine = StartCoroutine(TypewriterEffect(currentLineText));
    }

    /// <summary>
    /// Hiệu ứng gõ chữ từng ký tự chạy đồng bộ trên tất cả màn hình
    /// </summary>
    private IEnumerator TypewriterEffect(string fullText)
    {
        isTyping = true;
        SetDialogueText("");

        string currentTypedText = "";
        foreach (char c in fullText)
        {
            currentTypedText += c;
            SetDialogueText(currentTypedText);
            yield return new WaitForSeconds(typewriterSpeed);
        }

        isTyping = false;
        typewriterCoroutine = null;

        // Cập nhật nhãn nút "TIẾP TỤC" thành "KẾT THÚC" ở câu cuối cùng
        UpdateNextButtonText();
    }

    /// <summary>
    /// Bỏ qua typewriter để hiển thị toàn bộ chữ ngay lập tức
    /// </summary>
    private void SkipTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTyping = false;
        SetDialogueText(currentLineText);
        UpdateNextButtonText();
    }

    /// <summary>
    /// Nhấp để bỏ qua typewriter hoặc đi tiếp câu tiếp theo
    /// </summary>
    public void AdvanceDialogue()
    {
        if (isTyping)
        {
            SkipTypewriter();
        }
        else
        {
            currentLineIndex++;
            if (activeLines == null || currentLineIndex >= activeLines.Count)
            {
                EndDialogue();
            }
            else
            {
                PlayDialogueLine(currentLineIndex);
            }
        }
    }

    /// <summary>
    /// Nhấp nút SKIP để bỏ qua toàn bộ cuộc đối thoại ngay lập tức
    /// </summary>
    public void SkipAllDialogue()
    {
        Debug.Log("[IntroDialogueController] Người chơi đã bỏ qua toàn bộ cuộc đối thoại!");
        EndDialogue();
    }

    /// <summary>
    /// Kết thúc hội thoại, giải phóng visual tree và khôi phục di chuyển cho player
    /// </summary>
    public void EndDialogue()
    {
        if (!isDialogueActive) return;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTyping = false;
        isDialogueActive = false;

        // Tắt animation nói chuyện của NPC
        SetNpcTalking(false);

        // Xóa hoàn toàn các VisualElement đã nhân bản ra khỏi HUD của Player
        foreach (var instance in instantiatedDialogues)
        {
            if (instance.wrapperElement != null && instance.wrapperElement.parent != null)
            {
                instance.wrapperElement.parent.Remove(instance.wrapperElement);
            }
        }
        instantiatedDialogues.Clear();

        // Mở khóa phím di chuyển/hành động cho Player
        PlayerHUDController.isAnyUIOpen = false;

        // Khóa lại con trỏ chuột cho local player
        var players = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        bool foundPlayer = false;
        foreach (var p in players)
        {
            if (p is IPlayerHUDTarget hudTarget)
            {
                if (hudTarget.IsOwner || hudTarget.IsStandaloneMode)
                {
                    hudTarget.SetCursorLock(true);
                    foundPlayer = true;
                }
            }
        }
        if (!foundPlayer)
        {
            // Trả trạng thái con trỏ chuột về trạng thái Locked nếu không tìm thấy Player script (test môi trường cô lập)
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        Debug.Log("[IntroDialogueController] Kết thúc cuộc đối thoại giới thiệu. Khôi phục điều khiển cho Player.");

        // Bắt đầu di chuyển NPC tới vị trí chỉ định (nếu có và nếu đây là hội thoại khởi đầu ban đầu)
        // Chúng ta chỉ di chuyển NPC khi nó hoàn thành cuộc hội thoại giới thiệu ban đầu (chứ sập cầu thì không cần đi nữa)
        if (activeLines == dialogueLines && npcMoveTarget != null)
        {
            if (npcMoveCoroutine != null)
            {
                StopCoroutine(npcMoveCoroutine);
            }
            npcMoveCoroutine = StartCoroutine(MoveNpcToTargetRoutine());
        }
    }

    /// <summary>
    /// Di chuyển NPC mượt mà tới vị trí target và bật animation di chuyển tương ứng
    /// </summary>
    private IEnumerator MoveNpcToTargetRoutine()
    {
        if (npcTransform == null || npcMoveTarget == null) yield break;

        // Đợi 1 frame để Animator hoàn tất chuyển trạng thái TroTruyen = false trước khi bắt đầu di chuyển
        yield return null;

        Debug.Log($"[IntroDialogueController] Bắt đầu di chuyển NPC ({npcTransform.name}) tới target ({npcMoveTarget.name}).");

        // Bật animation di chuyển của NPC
        SetNpcMoving(true);

        Transform targetTrans = npcTransform;
        Vector3 destination = npcMoveTarget.position;

        // Giữ nguyên độ cao Y hiện tại của NPC để tránh rơi xuyên đất hoặc bay lơ lửng
        destination.y = targetTrans.position.y;

        float distance = Vector3.Distance(targetTrans.position, destination);
        
        while (distance > stoppingDistance)
        {
            // Di chuyển tịnh tiến
            targetTrans.position = Vector3.MoveTowards(targetTrans.position, destination, moveSpeed * Time.deltaTime);

            // Quay mặt về hướng di chuyển
            Vector3 direction = (destination - targetTrans.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                targetTrans.rotation = Quaternion.Slerp(targetTrans.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            distance = Vector3.Distance(targetTrans.position, destination);
            yield return null;
        }

        // Cập nhật vị trí chính xác tại đích
        targetTrans.position = destination;

        // Tắt animation di chuyển
        SetNpcMoving(false);

        Debug.Log($"[IntroDialogueController] NPC đã tới đích thành công.");
        npcMoveCoroutine = null;
    }

    // ═══════════════════════════════════════════════════════
    //  HỖ TRỢ ĐIỀU KHIỂN ANIMATION CỦA NPC
    // ═══════════════════════════════════════════════════════

    private void SetNpcTalking(bool isTalking)
    {
        if (npcAnimator == null || string.IsNullOrEmpty(talkAnimParam)) return;

        foreach (var param in npcAnimator.parameters)
        {
            if (param.name == talkAnimParam)
            {
                if (param.type == AnimatorControllerParameterType.Bool)
                {
                    npcAnimator.SetBool(talkAnimParam, isTalking);
                }
                else if (param.type == AnimatorControllerParameterType.Trigger)
                {
                    if (isTalking) npcAnimator.SetTrigger(talkAnimParam);
                    else npcAnimator.ResetTrigger(talkAnimParam);
                }
                return;
            }
        }
    }

    private void SetNpcMoving(bool isMoving)
    {
        if (npcAnimator == null || string.IsNullOrEmpty(moveAnimParam)) return;

        foreach (var param in npcAnimator.parameters)
        {
            if (param.name == moveAnimParam)
            {
                if (param.type == AnimatorControllerParameterType.Bool)
                {
                    npcAnimator.SetBool(moveAnimParam, isMoving);
                }
                else if (param.type == AnimatorControllerParameterType.Float)
                {
                    npcAnimator.SetFloat(moveAnimParam, isMoving ? 1.0f : 0.0f);
                }
                else if (param.type == AnimatorControllerParameterType.Trigger)
                {
                    if (isMoving) npcAnimator.SetTrigger(moveAnimParam);
                    else npcAnimator.ResetTrigger(moveAnimParam);
                }
                return;
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  CÁC PHƯƠNG THỨC TRUYỀN DỮ LIỆU ĐỒNG BỘ CHO CÁC BẢN SAO UI
    // ═══════════════════════════════════════════════════════

    private void SetDialogueText(string text)
    {
        foreach (var instance in instantiatedDialogues)
        {
            if (instance.textLabel != null)
            {
                instance.textLabel.text = text;
            }
        }
    }

    private void SetSpeakerName(string name)
    {
        foreach (var instance in instantiatedDialogues)
        {
            if (instance.speakerLabel != null)
            {
                instance.speakerLabel.text = name;
            }
        }
    }

    private void UpdateNextButtonText()
    {
        foreach (var instance in instantiatedDialogues)
        {
            if (instance.nextButton != null)
            {
                if (activeLines != null && currentLineIndex == activeLines.Count - 1)
                {
                    instance.nextButton.text = "KẾT THÚC";
                }
                else
                {
                    instance.nextButton.text = "TIẾP TỤC ▶";
                }
            }
        }
    }

    private void SetWrapperClass(string className, bool add)
    {
        foreach (var instance in instantiatedDialogues)
        {
            if (instance.wrapperElement != null)
            {
                if (add) instance.wrapperElement.AddToClassList(className);
                else instance.wrapperElement.RemoveFromClassList(className);
            }
        }
    }

    private void SetBoxClass(string className, bool add)
    {
        foreach (var instance in instantiatedDialogues)
        {
            if (instance.boxElement != null)
            {
                if (add) instance.boxElement.AddToClassList(className);
                else instance.boxElement.RemoveFromClassList(className);
            }
        }
    }
}
