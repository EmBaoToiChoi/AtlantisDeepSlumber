using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.AI;

[RequireComponent(typeof(UIDocument))]
public class IntroDialogueController : NetworkBehaviour
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

    [Header("Dialogue Content (After Bridge Repaired)")]
    [SerializeField] private List<DialogueLine> afterBridgeRepairedLines = new List<DialogueLine>();

    [Header("Dialogue Content (Maze Entrance)")]
    [SerializeField] private List<DialogueLine> mazeEntranceLines = new List<DialogueLine>();

    [Header("Dialogue Content (Maze Wait Complete)")]
    [SerializeField] private List<DialogueLine> mazeWaitCompleteLines = new List<DialogueLine>();

    [Header("Dialogue Content (Second Wait Complete)")]
    [SerializeField] private List<DialogueLine> secondWaitCompleteLines = new List<DialogueLine>();

    [Header("Dialogue Content (Coop Puzzle)")]
    [SerializeField] private List<DialogueLine> coopPuzzleLines = new List<DialogueLine>();

    [Header("NPC Animation & Movement")]
    [Tooltip("Kéo thả Animator của NPC vào đây")]
    [SerializeField] private Animator npcAnimator;

    [Tooltip("Kéo thả Transform của NPC muốn di chuyển. Nếu để trống sẽ lấy Transform chứa Animator hoặc GameObject này.")]
    [SerializeField] private Transform npcTransform;

    [Tooltip("Tên tham số Animator khi nói chuyện (ví dụ: TroTruyen - kiểu Bool hoặc Trigger)")]
    [SerializeField] private string talkAnimParam = "TroTruyen";

    [Tooltip("Vị trí chỉ định mà NPC sẽ đi tới sau khi nói xong")]
    [SerializeField] private Transform npcMoveTarget;

    [Tooltip("Vị trí chỉ định mà NPC sẽ đi tới sau khi sửa xong cầu")]
    [SerializeField] private Transform npcMoveTargetAfterBridge;

    [Header("Maze Follow Configuration")]
    [Tooltip("Vị trí điểm dừng ở mê cung (Transform target)")]
    [SerializeField] private Transform npcMazeTarget;

    [Tooltip("Khoảng cách kích hoạt hội thoại tại điểm dừng mê cung")]
    [SerializeField] private float mazeTargetTriggerDistance = 2.5f;

    [Tooltip("Vị trí chỉ định để NPC di chuyển tới và biến mất (mờ dần)")]
    [SerializeField] private Transform npcFadeTarget;

    [Tooltip("Vị trí điểm chờ tiếp theo trong mê cung sau khi biến mất")]
    [SerializeField] private Transform npcMazeWaitTarget;

    [Tooltip("Khoảng cách kích hoạt hội thoại khen ngợi khi người chơi lại gần NPC tại điểm chờ")]
    [SerializeField] private float mazeWaitTriggerDistance = 4.0f;

    [Tooltip("Vị trí chỉ định để NPC di chuyển tới và biến mất lần 2")]
    [SerializeField] private Transform npcSecondFadeTarget;

    [Tooltip("Vị trí điểm chờ tiếp theo (lần 2) trong mê cung sau khi biến mất")]
    [SerializeField] private Transform npcSecondWaitTarget;

    [Tooltip("Khoảng cách kích hoạt hội thoại lần 2 khi người chơi lại gần NPC tại điểm chờ 2")]
    [SerializeField] private float npcSecondWaitTriggerDistance = 4.0f;

    [Tooltip("Vị trí điểm phối hợp (Transform thứ 7)")]
    [SerializeField] private Transform npcCoopPuzzleTarget;

    [Tooltip("Khoảng cách tối thiểu duy trì với người chơi khi follow")]
    [SerializeField] private float followKeepDistance = 2.0f;

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
    private bool isFollowingPlayer = false;
    private Transform playerToFollow = null;

    private bool hasStartedOpeningMove = false;
    private bool hasStartedMazeFollow = false;
    private bool hasStartedSecondFadeMove = false;
    private bool hasStartedCoopMove = false;
    private int activeDialogueType = 0;

    private Vector3 lastPosition;
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool IsServerOrOffline => !IsNetworkActive || IsServer;

    // Danh sách dòng thoại hiện đang hoạt động
    private List<DialogueLine> activeLines;
    private NavMeshAgent navMeshAgent;

    private NetworkVariable<bool> isNpcMovingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isNpcTalkingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> npcScaleNet = new NetworkVariable<float>(
        1.0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> activeDialogueTypeNet = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

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
        navMeshAgent = GetComponent<NavMeshAgent>();
        // Tự động gắn các component mạng cần thiết nếu bị thiếu
        if (GetComponent<NetworkObject>() == null)
        {
            gameObject.AddComponent<NetworkObject>();
            Debug.Log($"[IntroDialogueController] Tự động thêm NetworkObject cho {gameObject.name}");
        }
        if (GetComponent<Unity.Netcode.Components.NetworkTransform>() == null)
        {
            gameObject.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            Debug.Log($"[IntroDialogueController] Tự động thêm NetworkTransform cho {gameObject.name}");
        }

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

        if (afterBridgeRepairedLines.Count == 0)
        {
            afterBridgeRepairedLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "các ngươi có thấy hình vẽ trên tường không ? bây giờ chúng ta cần tìm những cục đá có hình như thế để đạp lên và mở cửa"
            });
        }

        if (mazeEntranceLines.Count == 0)
        {
            mazeEntranceLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "các ngươi hãy cố gắng để vượt qua các mê cung này nhé , cuối mê cung có thứ gì đó đang rình rập các ngươi"
            });
            mazeEntranceLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "ta đi trước đây !"
            });
        }

        if (mazeWaitCompleteLines.Count == 0)
        {
            mazeWaitCompleteLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "khá khen cho các ngươi, hãy tiếp tục đi và đừng bỏ cuộc nhé"
            });
        }

        if (secondWaitCompleteLines.Count == 0)
        {
            secondWaitCompleteLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "các ngươi đã đi tới đây rồi sao ? hãy cẩn thận, phía trước có mối nguy hiểm rất lớn đấy!"
            });
        }

        if (coopPuzzleLines.Count == 0)
        {
            coopPuzzleLines.Add(new DialogueLine
            {
                speakerName = defaultNpcName,
                text = "các ngươi phải tìm cách phối hợp với nhau để vượt qua chỗ này"
            });
        }

        // Auto-find components nếu thiếu
        if (npcAnimator == null)
        {
            npcAnimator = GetComponentInChildren<Animator>();
        }

        if (npcTransform == null)
        {
            npcTransform = transform;
        }
    }

    private IEnumerator Start()
    {
        lastPosition = transform.position;

        // Chờ 0.2 giây để đảm bảo PlayerHUDManager và các Player Prefab được spawn/init hoàn chỉnh
        yield return new WaitForSeconds(0.2f);

        // Nếu chơi offline, tự động chạy đối thoại khởi đầu
        if (!IsNetworkActive && triggerOnStart)
        {
            TriggerDialogue(0);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        lastPosition = transform.position;

        if (!IsServer && navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }

        // Đăng ký sự kiện đồng bộ trạng thái animation qua mạng
        isNpcMovingNet.OnValueChanged += OnNpcMovingNetChanged;
        isNpcTalkingNet.OnValueChanged += OnNpcTalkingNetChanged;
        npcScaleNet.OnValueChanged += OnNpcScaleNetChanged;
        activeDialogueTypeNet.OnValueChanged += OnActiveDialogueTypeNetChanged;

        // Khởi tạo trạng thái ban đầu cho Client khi spawn trễ
        if (!IsServer)
        {
            ApplyNpcMovingLocal(isNpcMovingNet.Value);
            ApplyNpcTalkingLocal(isNpcTalkingNet.Value);
            if (npcTransform != null)
            {
                npcTransform.localScale = Vector3.one * npcScaleNet.Value;
            }
            if (activeDialogueTypeNet.Value >= 0)
            {
                ExecuteDialogueLocal(activeDialogueTypeNet.Value);
            }
        }

        // Nếu chơi online, Server sẽ chịu trách nhiệm phát sự kiện đối thoại
        if (IsServer && triggerOnStart)
        {
            TriggerDialogue(0);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        isNpcMovingNet.OnValueChanged -= OnNpcMovingNetChanged;
        isNpcTalkingNet.OnValueChanged -= OnNpcTalkingNetChanged;
        npcScaleNet.OnValueChanged -= OnNpcScaleNetChanged;
        activeDialogueTypeNet.OnValueChanged -= OnActiveDialogueTypeNetChanged;
    }

    private void OnNpcScaleNetChanged(float oldVal, float newVal)
    {
        if (!IsServer && npcTransform != null)
        {
            npcTransform.localScale = Vector3.one * newVal;
        }
    }

    private void OnActiveDialogueTypeNetChanged(int oldVal, int newVal)
    {
        if (!IsServer)
        {
            if (newVal >= 0)
            {
                ExecuteDialogueLocal(newVal);
            }
            else
            {
                CloseDialogueUIOnly();
                PlayerHUDController.isAnyUIOpen = false;
                
                // Khôi phục con trỏ chuột cho client
                var players = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p is IPlayerHUDTarget hudTarget && (hudTarget.IsOwner || hudTarget.IsStandaloneMode))
                    {
                        hudTarget.SetCursorLock(true);
                    }
                }
            }
        }
    }

    private void OnNpcMovingNetChanged(bool oldVal, bool newVal)
    {
        if (!IsServer)
        {
            ApplyNpcMovingLocal(newVal);
        }
    }

    private void OnNpcTalkingNetChanged(bool oldVal, bool newVal)
    {
        if (!IsServer)
        {
            ApplyNpcTalkingLocal(newVal);
        }
    }

    private void Update()
    {
        if (isDialogueActive)
        {
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
        SetNpcMoving(false);

        // Chạy câu đầu tiên
        PlayDialogueLine(currentLineIndex);
    }

    /// <summary>
    /// Kích hoạt nhanh câu thoại khi cầu bị sập
    /// </summary>
    public void StartBridgeCollapseDialogue()
    {
        if (IsServerOrOffline)
        {
            TriggerDialogue(1);
        }
    }

    /// <summary>
    /// Kích hoạt nhanh câu thoại nhắc nhở xây cầu
    /// </summary>
    public void StartReadyToBuildDialogue()
    {
        if (IsServerOrOffline)
        {
            TriggerDialogue(2);
        }
    }

    /// <summary>
    /// Đăng ký thêm một HUD mới xuất hiện trong khi hội thoại đang chạy.
    /// Giải quyết triệt để lỗi đua luồng/khởi tạo trễ trong môi trường Multiplayer Netcode.
    /// </summary>
    public void RegisterNewHUD(PlayerHUDController hud)
    {
        if (hud == null || !isDialogueActive) return;

        var uiDoc = hud.GetComponent<UIDocument>();
        if (uiDoc != null && uiDoc.rootVisualElement != null)
        {
            // Kiểm tra xem HUD này đã được tạo UI đối thoại chưa (tránh trùng lặp)
            foreach (var instance in instantiatedDialogues)
            {
                if (instance.wrapperElement != null && uiDoc.rootVisualElement.Contains(instance.wrapperElement))
                {
                    return; // Đã tồn tại, bỏ qua
                }
            }

            // Nếu trước đó đang dùng fallback (chạy trên localUiDoc của chính controller), hãy gỡ nó ra để tránh trùng lặp
            var localUiDoc = GetComponent<UIDocument>();
            if (localUiDoc != null && localUiDoc != uiDoc && localUiDoc.rootVisualElement != null)
            {
                for (int i = instantiatedDialogues.Count - 1; i >= 0; i--)
                {
                    var inst = instantiatedDialogues[i];
                    if (inst.wrapperElement != null && localUiDoc.rootVisualElement.Contains(inst.wrapperElement))
                    {
                        localUiDoc.rootVisualElement.Remove(inst.wrapperElement);
                        instantiatedDialogues.RemoveAt(i);
                    }
                }
            }

            // Tạo UI đối thoại cho HUD mới này
            CreateAndRegisterUIInstance(uiDoc.rootVisualElement);

            // Cập nhật nội dung hiện tại cho HUD mới
            if (activeLines != null && currentLineIndex >= 0 && currentLineIndex < activeLines.Count)
            {
                DialogueLine line = activeLines[currentLineIndex];
                
                // Tìm instance vừa mới được thêm ở cuối list
                var newInstance = instantiatedDialogues[instantiatedDialogues.Count - 1];
                if (newInstance.speakerLabel != null) newInstance.speakerLabel.text = line.speakerName;
                if (newInstance.textLabel != null) newInstance.textLabel.text = isTyping ? "" : currentLineText;
                if (newInstance.nextButton != null)
                {
                    if (currentLineIndex == activeLines.Count - 1)
                    {
                        newInstance.nextButton.text = "KẾT THÚC";
                    }
                    else
                    {
                        newInstance.nextButton.text = "TIẾP TỤC ▶";
                    }
                }
                
                // Thêm class css để hiển thị
                if (newInstance.wrapperElement != null) newInstance.wrapperElement.AddToClassList("show-wrapper");
                if (newInstance.boxElement != null) newInstance.boxElement.AddToClassList("show-dialogue");
            }
        }
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
        template.BringToFront(); // Đảm bảo đè lên trên cùng của HUD cha
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
        SetNpcMoving(false);

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

        // Thông báo cho Server để di chuyển NPC ngay khi người đầu tiên kết thúc hội thoại
        if (activeDialogueType == 0)
        {
            NotifyStartNpcMovement(0);
        }
        else if (activeDialogueType == 4)
        {
            NotifyStartNpcMovement(4);
        }
        else if (activeDialogueType == 6)
        {
            NotifyStartNpcMovement(6);
        }
        else if (activeDialogueType == 7)
        {
            NotifyStartNpcMovement(7);
        }
        else if (activeDialogueType == 8)
        {
            NotifyStartNpcMovement(8);
        }
    }

    /// <summary>
    /// Di chuyển NPC mượt mà tới vị trí target và bật animation di chuyển tương ứng
    /// </summary>
    private bool CheckNavMeshPath(Vector3 targetPosition)
    {
        if (navMeshAgent == null)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        // Dùng NavMesh.CalculatePath tĩnh để không bị ảnh hưởng bởi việc bật/tắt NavMeshAgent
        if (NavMesh.CalculatePath(npcTransform.position, targetPosition, NavMesh.AllAreas, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                // Tính chiều dài thực tế của đường đi trên NavMesh
                float pathLength = 0f;
                if (path.corners.Length >= 2)
                {
                    for (int i = 1; i < path.corners.Length; i++)
                    {
                        pathLength += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    }
                }
                float directDist = Vector3.Distance(npcTransform.position, targetPosition);
                
                // Nếu khoảng cách ngắn hoặc đường đi không bị đi vòng quá 2.5 lần đường chim bay
                if (directDist < 2f || pathLength < directDist * 2.5f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private IEnumerator MoveNpcToTargetRoutine()
    {
        if (npcTransform == null || npcMoveTarget == null) yield break;

        // Đợi 1 frame để Animator hoàn tất chuyển trạng thái TroTruyen = false trước khi bắt đầu di chuyển
        yield return null;

        Debug.Log($"[IntroDialogueController] Bắt đầu di chuyển NPC ({npcTransform.name}) tới target ({npcMoveTarget.name}).");

        // Đảm bảo tắt nói chuyện, bật di chuyển
        SetNpcTalking(false);
        SetNpcMoving(true);

        Transform targetTrans = npcTransform;
        Vector3 destination = npcMoveTarget.position;

        bool useNavMesh = CheckNavMeshPath(destination);
        if (useNavMesh)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = true;
                yield return null; // chờ 1 frame để Agent khởi tạo trên NavMesh
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = moveSpeed;
                navMeshAgent.SetDestination(destination);
            }
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false; // Tắt hẳn Agent để có thể di chuyển bằng Transform tự do
            }
        }

        float distance = Vector3.Distance(targetTrans.position, destination);
        float maxDuration = (distance / moveSpeed) + 3f;
        float elapsed = 0f;
        float pathCheckTimer = 0f;
        
        while (distance > stoppingDistance && elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            pathCheckTimer += Time.deltaTime;
            if (pathCheckTimer >= 0.25f)
            {
                pathCheckTimer = 0f;
                useNavMesh = CheckNavMeshPath(destination);
            }

            if (useNavMesh)
            {
                if (navMeshAgent != null && !navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = true;
                    yield return null;
                }
                if (navMeshAgent != null && navMeshAgent.isStopped)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = moveSpeed;
                    navMeshAgent.SetDestination(destination);
                }
                bool isMoving = navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.15f;
                SetNpcMoving(isMoving);
            }
            else
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = false;
                }

                targetTrans.position = Vector3.MoveTowards(targetTrans.position, destination, moveSpeed * Time.deltaTime);

                Vector3 direction = (destination - targetTrans.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(direction);
                    targetTrans.rotation = Quaternion.Slerp(targetTrans.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
                SetNpcMoving(true);
            }

            distance = Vector3.Distance(targetTrans.position, destination);
            yield return null;
        }

        // Đưa NPC về vị trí và góc xoay chính xác tuyệt đối của target
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.Warp(destination);
            navMeshAgent.isStopped = true;
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
            targetTrans.position = destination;
        }
        targetTrans.rotation = npcMoveTarget.rotation;

        SetNpcMoving(false);
        Debug.Log($"[IntroDialogueController] NPC đã tới đích thành công.");
        npcMoveCoroutine = null;
    }

    // ═══════════════════════════════════════════════════════
    //  HỖ TRỢ ĐIỀU KHIỂN ANIMATION CỦA NPC
    // ═══════════════════════════════════════════════════════

    private void SetNpcTalking(bool isTalking)
    {
        if (IsNetworkActive && IsServer)
        {
            isNpcTalkingNet.Value = isTalking;
        }
        ApplyNpcTalkingLocal(isTalking);
    }

    private void ApplyNpcTalkingLocal(bool isTalking)
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
        if (IsNetworkActive && IsServer)
        {
            isNpcMovingNet.Value = isMoving;
        }
        ApplyNpcMovingLocal(isMoving);
    }

    private void ApplyNpcMovingLocal(bool isMoving)
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

    /// <summary>
    /// Kích hoạt NPC di chuyển tới vị trí thứ 2 và sau đó bắt đầu hội thoại sau khi sửa cầu xong
    /// </summary>
    public void TriggerMoveAndDialogueAfterBridge()
    {
        if (!IsServerOrOffline) return;

        if (npcMoveTargetAfterBridge != null)
        {
            if (npcMoveCoroutine != null)
            {
                StopCoroutine(npcMoveCoroutine);
            }
            npcMoveCoroutine = StartCoroutine(MoveNpcToTargetAfterBridgeRoutine());
        }
        else
        {
            // Nếu không có target chỉ định, chạy hội thoại ngay lập tức
            TriggerDialogue(3);
        }
    }

    private IEnumerator MoveNpcToTargetAfterBridgeRoutine()
    {
        if (npcTransform == null || npcMoveTargetAfterBridge == null) yield break;

        // Đảm bảo cầu đã được sửa xong mới cho đi qua
        var bridgeTrigger = FindAnyObjectByType<BridgeCollapseTrigger>();
        if (bridgeTrigger != null && !bridgeTrigger.IsBridgeRepaired())
        {
            Debug.LogWarning("[IntroDialogueController] Cầu chưa được sửa xong! Rakan không di chuyển từ 1 sang 2.");
            npcMoveCoroutine = null;
            yield break;
        }

        yield return null; // chờ 1 frame

        Debug.Log($"[IntroDialogueController] NPC di chuyển sau khi sửa cầu tới target: {npcMoveTargetAfterBridge.name}");

        SetNpcTalking(false);
        SetNpcMoving(true);

        Transform targetTrans = npcTransform;
        Vector3 destination = npcMoveTargetAfterBridge.position;

        bool useNavMesh = CheckNavMeshPath(destination);
        if (useNavMesh)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = true;
                yield return null;
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = moveSpeed;
                navMeshAgent.SetDestination(destination);
            }
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
        }

        float distance = Vector3.Distance(targetTrans.position, destination);
        float maxDuration = (distance / moveSpeed) + 3f;
        float elapsed = 0f;
        float pathCheckTimer = 0f;
        
        while (distance > stoppingDistance && elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            pathCheckTimer += Time.deltaTime;
            if (pathCheckTimer >= 0.25f)
            {
                pathCheckTimer = 0f;
                useNavMesh = CheckNavMeshPath(destination);
            }

            if (useNavMesh)
            {
                if (navMeshAgent != null && !navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = true;
                    yield return null;
                }
                if (navMeshAgent != null && navMeshAgent.isStopped)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = moveSpeed;
                    navMeshAgent.SetDestination(destination);
                }
                bool isMoving = navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.15f;
                SetNpcMoving(isMoving);
            }
            else
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = false;
                }

                targetTrans.position = Vector3.MoveTowards(targetTrans.position, destination, moveSpeed * Time.deltaTime);

                Vector3 direction = (destination - targetTrans.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(direction);
                    targetTrans.rotation = Quaternion.Slerp(targetTrans.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
                SetNpcMoving(true);
            }

            distance = Vector3.Distance(targetTrans.position, destination);
            yield return null;
        }

        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.Warp(destination);
            navMeshAgent.isStopped = true;
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
            targetTrans.position = destination;
        }
        targetTrans.rotation = npcMoveTargetAfterBridge.rotation;

        SetNpcMoving(false);

        Debug.Log($"[IntroDialogueController] NPC đã tới vị trí sau khi sửa cầu. Khởi chạy hội thoại.");
        npcMoveCoroutine = null;

        // Bắt đầu hội thoại
        TriggerDialogue(3);
    }

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

    // ═══════════════════════════════════════════════════════
    //  HỖ TRỢ NPC DI CHUYỂN FOLLOW NGƯỜI CHƠI & DỪNG TẠI MÊ CUNG
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Bắt đầu follow một người chơi bất kỳ cho đến khi tới điểm dừng mê cung.
    /// </summary>
    /// <summary>
    /// Kích hoạt NPC di chuyển tới vị trí điểm dừng ở mê cung.
    /// </summary>
    public void StartNpcFollowingPlayer()
    {
        if (!IsServerOrOffline) return;

        if (npcMoveCoroutine != null)
        {
            StopCoroutine(npcMoveCoroutine);
        }
        npcMoveCoroutine = StartCoroutine(MoveNpcToMazeTargetRoutine());
    }

    private IEnumerator MoveNpcToMazeTargetRoutine()
    {
        if (npcTransform == null || npcMazeTarget == null)
        {
            Debug.LogWarning("[IntroDialogueController] NPC hoặc npcMazeTarget chưa được cấu hình để di chuyển tới mê cung!");
            yield break;
        }

        // Đảm bảo câu đố nút sàn đã giải xong và cửa đã mở mới cho đi qua
        var puzzleManager = FindAnyObjectByType<PressurePlatePuzzleManager>();
        bool isSolved = false;
        if (puzzleManager != null)
        {
            isSolved = puzzleManager.IsSolved();
        }
        if (!isSolved)
        {
            Debug.LogWarning("[IntroDialogueController] Cửa chưa được mở! Rakan không di chuyển từ 2 sang 3.");
            npcMoveCoroutine = null;
            yield break;
        }

        // Đợi 2 giây để cửa mở ra hoàn toàn trước khi NPC bắt đầu đi qua
        yield return new WaitForSeconds(2f);

        Debug.Log($"[IntroDialogueController] NPC di chuyển tới điểm dừng mê cung: {npcMazeTarget.name}");
        
        SetNpcTalking(false);
        SetNpcMoving(true);

        Transform targetTrans = npcTransform;
        Vector3 destination = npcMazeTarget.position;

        bool useNavMesh = CheckNavMeshPath(destination);
        if (useNavMesh)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = true;
                yield return null;
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = moveSpeed;
                navMeshAgent.SetDestination(destination);
            }
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
        }

        float distance = Vector3.Distance(targetTrans.position, destination);
        float maxDuration = (distance / moveSpeed) + 3f;
        float elapsed = 0f;
        float pathCheckTimer = 0f;
        
        while (distance > stoppingDistance && elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            pathCheckTimer += Time.deltaTime;
            if (pathCheckTimer >= 0.25f)
            {
                pathCheckTimer = 0f;
                useNavMesh = CheckNavMeshPath(destination);
            }

            if (useNavMesh)
            {
                if (navMeshAgent != null && !navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = true;
                    yield return null;
                }
                if (navMeshAgent != null && navMeshAgent.isStopped)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = moveSpeed;
                    navMeshAgent.SetDestination(destination);
                }
                bool isMoving = navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.15f;
                SetNpcMoving(isMoving);
            }
            else
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = false;
                }

                targetTrans.position = Vector3.MoveTowards(targetTrans.position, destination, moveSpeed * Time.deltaTime);

                Vector3 direction = (destination - targetTrans.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(direction);
                    targetTrans.rotation = Quaternion.Slerp(targetTrans.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
                SetNpcMoving(true);
            }

            distance = Vector3.Distance(targetTrans.position, destination);
            yield return null;
        }

        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.Warp(destination);
            navMeshAgent.isStopped = true;
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
            targetTrans.position = destination;
        }
        targetTrans.rotation = npcMazeTarget.rotation;

        SetNpcMoving(false);
        npcMoveCoroutine = null;

        Debug.Log("[IntroDialogueController] NPC đã đứng tại điểm dừng mê cung. Bắt đầu hội thoại.");

        // Quay mặt về hướng người chơi gần nhất để nói chuyện
        Transform nearbyPlayer = FindPlayerToFollow();
        if (nearbyPlayer != null)
        {
            Vector3 lookDir = (nearbyPlayer.position - npcTransform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                npcTransform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        // Bắt đầu hội thoại mê cung
        TriggerDialogue(4);
    }

    private Transform FindPlayerToFollow()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Transform closestPlayer = null;
        float closestDistance = float.MaxValue;

        foreach (var player in players)
        {
            if (player == null || !player.activeInHierarchy) continue;

            float dist = Vector3.Distance(npcTransform.position, player.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPlayer = player.transform;
            }
        }
        return closestPlayer;
    }

    private IEnumerator PermanentFollowPlayerRoutine()
    {
        isFollowingPlayer = true;
        SetNpcTalking(false);
        SetNpcMoving(true);

        Debug.Log("[IntroDialogueController] NPC bắt đầu follow người chơi vĩnh viễn...");

        float searchTimer = 0f;
        float pathCheckTimer = 0f;
        bool useNavMesh = false;

        while (isFollowingPlayer)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= 0.2f || playerToFollow == null)
            {
                searchTimer = 0f;
                playerToFollow = FindPlayerToFollow();
            }

            if (playerToFollow != null)
            {
                Vector3 targetPos = playerToFollow.position;

                pathCheckTimer += Time.deltaTime;
                if (pathCheckTimer >= 0.25f)
                {
                    pathCheckTimer = 0f;
                    useNavMesh = CheckNavMeshPath(targetPos);
                }

                if (useNavMesh)
                {
                    if (navMeshAgent != null && !navMeshAgent.enabled)
                    {
                        navMeshAgent.enabled = true;
                        yield return null;
                    }
                    if (navMeshAgent != null && navMeshAgent.isStopped)
                    {
                        navMeshAgent.isStopped = false;
                        navMeshAgent.speed = moveSpeed;
                        navMeshAgent.stoppingDistance = followKeepDistance;
                    }
                    navMeshAgent.SetDestination(targetPos);
                    bool isMoving = navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.15f;
                    SetNpcMoving(isMoving);
                }
                else
                {
                    if (navMeshAgent != null && navMeshAgent.enabled)
                    {
                        navMeshAgent.enabled = false;
                    }

                    float distToPlayer = Vector3.Distance(npcTransform.position, targetPos);

                    if (distToPlayer > followKeepDistance)
                    {
                        SetNpcMoving(true);
                        npcTransform.position = Vector3.MoveTowards(npcTransform.position, targetPos, moveSpeed * Time.deltaTime);

                        // Quay mặt về hướng di chuyển
                        Vector3 direction = (targetPos - npcTransform.position).normalized;
                        if (direction != Vector3.zero)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(direction);
                            npcTransform.rotation = Quaternion.Slerp(npcTransform.rotation, targetRot, turnSpeed * Time.deltaTime);
                        }
                    }
                    else
                    {
                        // Nếu đã đứng gần player đầy đủ, dừng đi bộ nhưng quay mặt về phía player
                        SetNpcMoving(false);
                        Vector3 direction = (targetPos - npcTransform.position).normalized;
                        if (direction != Vector3.zero)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(direction);
                            npcTransform.rotation = Quaternion.Slerp(npcTransform.rotation, targetRot, turnSpeed * Time.deltaTime);
                        }
                    }
                }
            }
            else
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = false;
                }
                SetNpcMoving(false);
            }

            yield return null;
        }

        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
        {
            navMeshAgent.isStopped = true;
        }
        SetNpcMoving(false);
    }

    // ═══════════════════════════════════════════════════════
    //  ĐỒNG BỘ HÓA ĐỐI THOẠI QUA MẠNG (NETCODE CLIENT RPC)
    // ═══════════════════════════════════════════════════════

    private void TriggerDialogue(int dialogueType)
    {
        if (IsNetworkActive && IsServer)
        {
            activeDialogueTypeNet.Value = dialogueType;
        }
        else if (!IsNetworkActive)
        {
            ExecuteDialogueLocal(dialogueType);
        }
    }

    private void CloseDialogueUIOnly()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTyping = false;
        isDialogueActive = false;

        foreach (var instance in instantiatedDialogues)
        {
            if (instance.wrapperElement != null && instance.wrapperElement.parent != null)
            {
                instance.wrapperElement.parent.Remove(instance.wrapperElement);
            }
        }
        instantiatedDialogues.Clear();
    }

    private void ExecuteDialogueLocal(int dialogueType)
    {
        if (isDialogueActive)
        {
            // Dọn dẹp hội thoại cũ để đè hội thoại mới lên lập tức, tránh xung đột/đua luồng giữa các chương truyện
            CloseDialogueUIOnly();
        }

        activeDialogueType = dialogueType;

        if (dialogueType == 0)
        {
            hasStartedOpeningMove = false;
        }
        else if (dialogueType == 4)
        {
            hasStartedMazeFollow = false;
            hasStartedSecondFadeMove = false;
            hasStartedCoopMove = false;
        }

        switch (dialogueType)
        {
            case 0:
                StartDialogue(dialogueLines);
                break;
            case 1:
                StartDialogue(bridgeCollapseLines);
                break;
            case 2:
                List<DialogueLine> readyLines = new List<DialogueLine>();
                readyLines.Add(new DialogueLine
                {
                    speakerName = defaultNpcName,
                    text = "4 người các ngươi hãy lại đây ấn F và click liên tục để xây cầu"
                });
                StartDialogue(readyLines);
                break;
            case 3:
                StartDialogue(afterBridgeRepairedLines);
                break;
            case 4:
                StartDialogue(mazeEntranceLines);
                break;
            case 6:
                StartDialogue(mazeWaitCompleteLines);
                break;
            case 7:
                StartDialogue(secondWaitCompleteLines);
                break;
            case 8:
                StartDialogue(coopPuzzleLines);
                break;
        }
    }

    [ClientRpc]
    private void StartDialogueClientRpc(int dialogueType)
    {
        ExecuteDialogueLocal(dialogueType);
    }

    // ═══════════════════════════════════════════════════════
    //  ĐỒNG BỘ HÓA KÍCH HOẠT DI CHUYỂN NPC KHI CÓ NGƯỜI ĐỌC XONG
    // ═══════════════════════════════════════════════════════

    private void NotifyStartNpcMovement(int type)
    {
        if (IsNetworkActive)
        {
            NotifyStartNpcMovementServerRpc(type);
        }
        else
        {
            StartNpcMovementLocal(type);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyStartNpcMovementServerRpc(int type)
    {
        StartNpcMovementLocal(type);
    }

    private void StartNpcMovementLocal(int type)
    {
        if (!IsServerOrOffline) return;

        if (IsNetworkActive && IsServer)
        {
            activeDialogueTypeNet.Value = -1; // Đóng hội thoại trên mọi client
        }

        if (type == 0) // Opening dialogue finished by anyone
        {
            if (!hasStartedOpeningMove && npcMoveTarget != null)
            {
                hasStartedOpeningMove = true;
                if (npcMoveCoroutine != null)
                {
                    StopCoroutine(npcMoveCoroutine);
                }
                npcMoveCoroutine = StartCoroutine(MoveNpcToTargetRoutine());
            }
        }
        else if (type == 4) // Maze entrance dialogue finished by anyone
        {
            if (!hasStartedMazeFollow)
            {
                hasStartedMazeFollow = true;
                if (npcMoveCoroutine != null)
                {
                    StopCoroutine(npcMoveCoroutine);
                }
                npcMoveCoroutine = StartCoroutine(MoveNpcToFadeTargetAndTeleportRoutine());
            }
        }
        else if (type == 6) // Maze wait complete dialogue finished (Dialogue 6 Praise)
        {
            if (!hasStartedSecondFadeMove)
            {
                hasStartedSecondFadeMove = true;
                if (npcMoveCoroutine != null)
                {
                    StopCoroutine(npcMoveCoroutine);
                }
                npcMoveCoroutine = StartCoroutine(MoveNpcToSecondFadeTargetAndTeleportRoutine());
            }
        }
        else if (type == 7) // Second wait complete dialogue finished (Dialogue 7 Warning)
        {
            if (!hasStartedCoopMove)
            {
                hasStartedCoopMove = true;
                if (npcMoveCoroutine != null)
                {
                    StopCoroutine(npcMoveCoroutine);
                }
                npcMoveCoroutine = StartCoroutine(MoveNpcToCoopPuzzleTargetRoutine());
            }
        }
        else if (type == 8) // Coop puzzle dialogue finished (Dialogue 8)
        {
            if (npcMoveCoroutine != null)
            {
                StopCoroutine(npcMoveCoroutine);
            }
            npcMoveCoroutine = StartCoroutine(PermanentFollowPlayerRoutine());
        }
    }

    private IEnumerator FadeNpcRoutine(bool fadeOut, float duration)
    {
        float elapsed = 0f;
        float startVal = fadeOut ? 1f : 0f;
        float endVal = fadeOut ? 0f : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float currentScale = Mathf.Lerp(startVal, endVal, t);

            if (IsNetworkActive && IsServer)
            {
                npcScaleNet.Value = currentScale;
            }
            if (npcTransform != null)
            {
                npcTransform.localScale = Vector3.one * currentScale;
            }
            yield return null;
        }

        if (IsNetworkActive && IsServer)
        {
            npcScaleNet.Value = endVal;
        }
        if (npcTransform != null)
        {
            npcTransform.localScale = Vector3.one * endVal;
        }
    }

    private IEnumerator MoveNpcToFadeTargetAndTeleportRoutine()
    {
        if (npcTransform == null || npcFadeTarget == null || npcMazeWaitTarget == null)
        {
            Debug.LogWarning("[IntroDialogueController] Thiếu npcFadeTarget hoặc npcMazeWaitTarget!");
            yield break;
        }

        Debug.Log($"[IntroDialogueController] NPC di chuyển tới điểm biến mất: {npcFadeTarget.name}");
        SetNpcMoving(true);

        Transform targetTrans = npcTransform;
        Vector3 destination = npcFadeTarget.position;

        bool useNavMesh = CheckNavMeshPath(destination);
        if (useNavMesh)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = true;
                yield return null;
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = moveSpeed;
                navMeshAgent.SetDestination(destination);
            }
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
        }

        float distance = Vector3.Distance(targetTrans.position, destination);
        float maxDuration = (distance / moveSpeed) + 3f;
        float elapsed = 0f;
        
        while (distance > stoppingDistance && elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;
            useNavMesh = CheckNavMeshPath(destination);

            if (useNavMesh)
            {
                if (navMeshAgent != null && !navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = true;
                    yield return null;
                }
                if (navMeshAgent != null && navMeshAgent.isStopped)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = moveSpeed;
                    navMeshAgent.SetDestination(destination);
                }
                bool isMoving = navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.15f;
                SetNpcMoving(isMoving);
            }
            else
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = false;
                }

                targetTrans.position = Vector3.MoveTowards(targetTrans.position, destination, moveSpeed * Time.deltaTime);

                Vector3 direction = (destination - targetTrans.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(direction);
                    targetTrans.rotation = Quaternion.Slerp(targetTrans.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
                SetNpcMoving(true);
            }

            distance = Vector3.Distance(targetTrans.position, destination);
            yield return null;
        }

        // Đã đến điểm biến mất
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.Warp(destination);
            navMeshAgent.isStopped = true;
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
            targetTrans.position = destination;
        }
        targetTrans.rotation = npcFadeTarget.rotation;
        SetNpcMoving(false);

        // 1. Biến mất (Fade out)
        Debug.Log("[IntroDialogueController] NPC đang biến mất...");
        yield return StartCoroutine(FadeNpcRoutine(true, 1.5f));

        // 2. Dịch chuyển đến điểm chờ trong mê cung
        Debug.Log($"[IntroDialogueController] NPC dịch chuyển tới: {npcMazeWaitTarget.name}");
        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = false; // Tắt agent trước khi dịch chuyển
        }
        targetTrans.position = npcMazeWaitTarget.position;
        targetTrans.rotation = npcMazeWaitTarget.rotation;

        // 3. Hiện lại (Fade in) tại vị trí mới
        yield return StartCoroutine(FadeNpcRoutine(false, 1.5f));
        if (navMeshAgent != null)
        {
            // Bật lại agent nếu vị trí mới có NavMesh
            navMeshAgent.enabled = true;
            yield return null;
            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
            }
            else
            {
                navMeshAgent.enabled = false;
            }
        }

        // 4. Bắt đầu lắng nghe người chơi lại gần
        npcMoveCoroutine = StartCoroutine(WaitForPlayersAtMazeWaitTargetRoutine());
    }

    private IEnumerator WaitForPlayersAtMazeWaitTargetRoutine()
    {
        Debug.Log("[IntroDialogueController] NPC đang chờ người chơi lại gần điểm chờ mê cung...");
        bool triggered = false;

        while (!triggered)
        {
            // Tìm khoảng cách tới người chơi gần nhất
            Transform closestPlayer = FindPlayerToFollow();
            if (closestPlayer != null)
            {
                float dist = Vector3.Distance(npcTransform.position, closestPlayer.position);
                if (dist <= mazeWaitTriggerDistance)
                {
                    triggered = true;
                }
            }
            yield return new WaitForSeconds(0.2f); // Kiểm tra mỗi 0.2 giây để tối ưu hiệu năng
        }

        Debug.Log("[IntroDialogueController] Người chơi đã lại gần Rakan tại điểm chờ mê cung. Kích hoạt thoại chặng tiếp theo.");

        // Xoay mặt về phía người chơi gần nhất để nói chuyện
        Transform nearbyPlayer = FindPlayerToFollow();
        if (nearbyPlayer != null)
        {
            Vector3 lookDir = (nearbyPlayer.position - npcTransform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                npcTransform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        // Kích hoạt hội thoại khen ngợi (Dialogue Type 6)
        TriggerDialogue(6);
    }

    private IEnumerator MoveNpcToSecondFadeTargetAndTeleportRoutine()
    {
        if (npcTransform == null || npcSecondFadeTarget == null || npcSecondWaitTarget == null)
        {
            Debug.LogWarning("[IntroDialogueController] Thiếu npcSecondFadeTarget hoặc npcSecondWaitTarget!");
            yield break;
        }

        Debug.Log($"[IntroDialogueController] NPC di chuyển tới điểm biến mất lần 2: {npcSecondFadeTarget.name}");
        SetNpcMoving(true);

        Transform targetTrans = npcTransform;
        Vector3 destination = npcSecondFadeTarget.position;

        bool useNavMesh = CheckNavMeshPath(destination);
        if (useNavMesh)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = true;
                yield return null;
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = moveSpeed;
                navMeshAgent.SetDestination(destination);
            }
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
        }

        float distance = Vector3.Distance(targetTrans.position, destination);
        float maxDuration = (distance / moveSpeed) + 3f;
        float elapsed = 0f;
        float pathCheckTimer = 0f;
        
        while (distance > stoppingDistance && elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            pathCheckTimer += Time.deltaTime;
            if (pathCheckTimer >= 0.25f)
            {
                pathCheckTimer = 0f;
                useNavMesh = CheckNavMeshPath(destination);
            }

            if (useNavMesh)
            {
                if (navMeshAgent != null && !navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = true;
                    yield return null;
                }
                if (navMeshAgent != null && navMeshAgent.isStopped)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = moveSpeed;
                    navMeshAgent.SetDestination(destination);
                }
                bool isMoving = navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.15f;
                SetNpcMoving(isMoving);
            }
            else
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = false;
                }

                targetTrans.position = Vector3.MoveTowards(targetTrans.position, destination, moveSpeed * Time.deltaTime);

                Vector3 direction = (destination - targetTrans.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(direction);
                    targetTrans.rotation = Quaternion.Slerp(targetTrans.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
                SetNpcMoving(true);
            }

            distance = Vector3.Distance(targetTrans.position, destination);
            yield return null;
        }

        // Đã đến điểm biến mất lần 2
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.Warp(destination);
            navMeshAgent.isStopped = true;
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
            targetTrans.position = destination;
        }
        targetTrans.rotation = npcSecondFadeTarget.rotation;
        SetNpcMoving(false);

        // 1. Biến mất lần 2
        Debug.Log("[IntroDialogueController] NPC đang biến mất lần 2...");
        yield return StartCoroutine(FadeNpcRoutine(true, 1.5f));

        // 2. Dịch chuyển đến điểm chờ thứ 2 trong mê cung
        Debug.Log($"[IntroDialogueController] NPC dịch chuyển tới (lần 2): {npcSecondWaitTarget.name}");
        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }
        targetTrans.position = npcSecondWaitTarget.position;
        targetTrans.rotation = npcSecondWaitTarget.rotation;

        // 3. Hiện lại lần 2
        yield return StartCoroutine(FadeNpcRoutine(false, 1.5f));
        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = true;
            yield return null;
            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
            }
            else
            {
                navMeshAgent.enabled = false;
            }
        }

        // 4. Lắng nghe người tiếp cận
        npcMoveCoroutine = StartCoroutine(WaitForPlayersAtSecondWaitTargetRoutine());
    }

    private IEnumerator WaitForPlayersAtSecondWaitTargetRoutine()
    {
        Debug.Log("[IntroDialogueController] NPC đang chờ người chơi lại gần điểm chờ mê cung 2...");
        bool triggered = false;

        while (!triggered)
        {
            Transform closestPlayer = FindPlayerToFollow();
            if (closestPlayer != null)
            {
                float dist = Vector3.Distance(npcTransform.position, closestPlayer.position);
                if (dist <= npcSecondWaitTriggerDistance)
                {
                    triggered = true;
                }
            }
            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log("[IntroDialogueController] Người chơi lại gần điểm chờ 2. Kích hoạt thoại khen ngợi 2.");

        Transform nearbyPlayer = FindPlayerToFollow();
        if (nearbyPlayer != null)
        {
            Vector3 lookDir = (nearbyPlayer.position - npcTransform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                npcTransform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        TriggerDialogue(7);
    }

    private IEnumerator MoveNpcToCoopPuzzleTargetRoutine()
    {
        if (npcTransform == null || npcCoopPuzzleTarget == null) yield break;

        Debug.Log($"[IntroDialogueController] NPC di chuyển tới điểm phối hợp (Transform 7): {npcCoopPuzzleTarget.name}");
        SetNpcTalking(false);
        SetNpcMoving(true);

        Transform targetTrans = npcTransform;
        Vector3 destination = npcCoopPuzzleTarget.position;

        bool useNavMesh = CheckNavMeshPath(destination);
        if (useNavMesh)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = true;
                yield return null;
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = moveSpeed;
                navMeshAgent.SetDestination(destination);
            }
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
        }

        float distance = Vector3.Distance(targetTrans.position, destination);
        float maxDuration = (distance / moveSpeed) + 3f;
        float elapsed = 0f;
        float pathCheckTimer = 0f;
        
        while (distance > stoppingDistance && elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            pathCheckTimer += Time.deltaTime;
            if (pathCheckTimer >= 0.25f)
            {
                pathCheckTimer = 0f;
                useNavMesh = CheckNavMeshPath(destination);
            }

            if (useNavMesh)
            {
                if (navMeshAgent != null && !navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = true;
                    yield return null;
                }
                if (navMeshAgent != null && navMeshAgent.isStopped)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = moveSpeed;
                    navMeshAgent.SetDestination(destination);
                }
                bool isMoving = navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.15f;
                SetNpcMoving(isMoving);
            }
            else
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = false;
                }

                targetTrans.position = Vector3.MoveTowards(targetTrans.position, destination, moveSpeed * Time.deltaTime);

                Vector3 direction = (destination - targetTrans.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(direction);
                    targetTrans.rotation = Quaternion.Slerp(targetTrans.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
                SetNpcMoving(true);
            }

            distance = Vector3.Distance(targetTrans.position, destination);
            yield return null;
        }

        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.Warp(destination);
            navMeshAgent.isStopped = true;
        }
        else
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
            targetTrans.position = destination;
        }
        targetTrans.rotation = npcCoopPuzzleTarget.rotation;

        SetNpcMoving(false);
        npcMoveCoroutine = null;

        Debug.Log("[IntroDialogueController] NPC đã đứng tại điểm phối hợp (Transform 7). Bắt đầu hội thoại.");

        // Xoay mặt về hướng người chơi gần nhất để nói chuyện
        Transform nearbyPlayer = FindPlayerToFollow();
        if (nearbyPlayer != null)
        {
            Vector3 lookDir = (nearbyPlayer.position - npcTransform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                npcTransform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        // Bắt đầu hội thoại phối hợp (Dialogue Type 8)
        TriggerDialogue(8);
    }
}
