using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Unity.Netcode;

[RequireComponent(typeof(UIDocument))]
public class EndingCreditsUI : MonoBehaviour
{
    public static EndingCreditsUI Instance { get; private set; }

    [Header("UI Document")]
    [SerializeField] private UIDocument _uiDocument;
    [SerializeField] private VisualTreeAsset _visualTreeAsset;
    [SerializeField] private PanelSettings _panelSettings;

    [Header("Configuration")]
    [TextArea(2, 5)]
    public string epilogueQuote = "Vực sâu nuốt chửng ánh sáng...\nNhưng giấc ngủ ngàn năm của Atlantis mới chỉ vừa bắt đầu.";
    public float epilogueDuration = 5f;
    public float creditScrollSpeed = 65f;
    [Range(1, 10)]
    public int creditLoopCount = 2;

    [Header("Ending Audio (Optional)")]
    public AudioClip endingMusic;

    private VisualElement _root;
    private VisualElement _epiloguePanel;
    private VisualElement _creditsViewport;
    private VisualElement _creditsScrollContainer;
    private Label _lblEpilogueQuote;
    private Button _btnSkip;

    private AudioSource _audioSource;
    private Coroutine _runningSequenceCoroutine;
    private bool _isExiting = false;

    public static bool IsEndingActive { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        InitializeUI();
    }

    private void OnDisable()
    {
        IsEndingActive = false;
    }

    private void OnDestroy()
    {
        IsEndingActive = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveAssets();
    }

    private void ResolveAssets()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument != null)
        {
            if (_visualTreeAsset == null && _uiDocument.visualTreeAsset == null)
            {
                _visualTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/HHoang/minigame/timeline/EndingCredits.uxml");
            }
            if (_visualTreeAsset != null && _uiDocument.visualTreeAsset == null)
            {
                _uiDocument.visualTreeAsset = _visualTreeAsset;
            }

            if (_panelSettings == null && _uiDocument.panelSettings == null)
            {
                _panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/HHoang/minigame/timeline/EndingCreditsPanelSettings.asset");
            }
            if (_panelSettings != null && _uiDocument.panelSettings == null)
            {
                _uiDocument.panelSettings = _panelSettings;
            }
        }
    }
#endif

    private void InitializeUI()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) return;

        _uiDocument.sortingOrder = 100000;

#if UNITY_EDITOR
        ResolveAssets();
#endif

        var rootVE = _uiDocument.rootVisualElement;
        if (rootVE == null) return;

        _root = rootVE.Q<VisualElement>("ending-root");
        _epiloguePanel = rootVE.Q<VisualElement>("epilogue-panel");
        _creditsViewport = rootVE.Q<VisualElement>("credits-viewport");
        _creditsScrollContainer = rootVE.Q<VisualElement>("credits-scroll-container");
        _lblEpilogueQuote = rootVE.Q<Label>("lbl-epilogue-quote");
        _btnSkip = rootVE.Q<Button>("btn-skip-credits");

        if (_btnSkip != null)
        {
            _btnSkip.clicked -= OnSkipClicked;
            _btnSkip.clicked += OnSkipClicked;
        }

        HideAll();
    }

    private void Update()
    {
        if (_root != null && _root.style.display == DisplayStyle.Flex && !_isExiting)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("[EndingCreditsUI] Phát hiện phím ESC/Space -> Bỏ qua và về Menu!");
                OnSkipClicked();
            }
        }
    }

    public void HideAll()
    {
        if (_root != null)
        {
            _root.style.display = DisplayStyle.None;
            _root.AddToClassList("hidden-element");
            _root.style.opacity = 0;
        }
        if (_epiloguePanel != null)
        {
            _epiloguePanel.style.display = DisplayStyle.None;
            _epiloguePanel.style.opacity = 0;
            _epiloguePanel.AddToClassList("hidden-element");
        }
        if (_creditsViewport != null)
        {
            _creditsViewport.style.display = DisplayStyle.None;
            _creditsViewport.AddToClassList("hidden-element");
        }
    }

    public void PlayEndingSequence(Action onComplete = null)
    {
        if (_runningSequenceCoroutine != null) StopCoroutine(_runningSequenceCoroutine);
        _runningSequenceCoroutine = StartCoroutine(EndingSequenceRoutine(onComplete));
    }

    private IEnumerator EndingSequenceRoutine(Action onComplete)
    {
        IsEndingActive = true;
        InitializeUI();
        _isExiting = false;

        // Mở khóa chuột
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        PlayerHUDController.isAnyUIOpen = true;

        if (_root != null)
        {
            _root.style.display = DisplayStyle.Flex;
            _root.RemoveFromClassList("hidden-element");
            _root.style.opacity = 1;
        }

        // Bật nhạc kết thúc: Ưu tiên nhạc custom nếu có, nếu không tự động bật BGM của MainMenu
        if (endingMusic != null)
        {
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.clip = endingMusic;
            _audioSource.loop = true;
            _audioSource.volume = 0.85f;
            _audioSource.Play();
        }
        else if (AudioManager.Instance != null)
        {
            Debug.Log("[EndingCreditsUI] Tự động bật BGM của MainMenu cho đoạn Ending Credits...");
            AudioManager.Instance.SetCutsceneActive(false, 0.5f);
            AudioManager.Instance.SetBossMusicActive(false, 0.5f);
            AudioManager.Instance.PlayBGM("Audio/BGM", 1.5f);
        }

        // ==========================================
        // GIAI ĐOẠN 1: EPILOGUE (DÒNG CHỮ LẮNG ĐỌNG)
        // ==========================================
        if (_epiloguePanel != null)
        {
            if (_lblEpilogueQuote != null && !string.IsNullOrEmpty(epilogueQuote))
            {
                _lblEpilogueQuote.text = $"\"{epilogueQuote}\"";
            }

            _epiloguePanel.style.display = DisplayStyle.Flex;
            _epiloguePanel.RemoveFromClassList("hidden-element");

            // Fade in dòng chữ trong 1.5s
            float t = 0;
            while (t < 1.5f && !_isExiting)
            {
                t += Time.unscaledDeltaTime;
                _epiloguePanel.style.opacity = Mathf.Lerp(0f, 1f, t / 1.5f);
                yield return null;
            }
            _epiloguePanel.style.opacity = 1f;

            // Giữ hiển thị
            float hold = 0;
            while (hold < epilogueDuration && !_isExiting)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            // Fade out dòng chữ trong 1.2s
            t = 0;
            while (t < 1.2f && !_isExiting)
            {
                t += Time.unscaledDeltaTime;
                _epiloguePanel.style.opacity = Mathf.Lerp(1f, 0f, t / 1.2f);
                yield return null;
            }
            _epiloguePanel.style.opacity = 0f;
            _epiloguePanel.style.display = DisplayStyle.None;
            _epiloguePanel.AddToClassList("hidden-element");
        }

        if (_isExiting) yield break;

        yield return new WaitForSecondsRealtime(0.4f);

        // ==========================================
        // GIAI ĐOẠN 2: ROLLING CREDITS
        // ==========================================
        if (_creditsViewport != null && _creditsScrollContainer != null)
        {
            _creditsViewport.style.display = DisplayStyle.Flex;
            _creditsViewport.RemoveFromClassList("hidden-element");

            float viewportHeight = Screen.height > 0 ? Screen.height : 1080f;
            float containerHeight = _creditsScrollContainer.layout.height > 0 ? _creditsScrollContainer.layout.height : 2200f;
            
            float startTop = viewportHeight + 100f;
            float endTop = -(containerHeight + 150f);
            int completedLoops = 0;

            _creditsScrollContainer.style.top = startTop;

            while (completedLoops < creditLoopCount && !_isExiting)
            {
                float currentTop = _creditsScrollContainer.style.top.value.value;
                currentTop -= creditScrollSpeed * Time.unscaledDeltaTime * 1.5f;
                _creditsScrollContainer.style.top = currentTop;

                if (currentTop <= endTop)
                {
                    completedLoops++;
                    Debug.Log($"[EndingCreditsUI] Hoàn thành {completedLoops}/{creditLoopCount} lượt Credit.");
                    _creditsScrollContainer.style.top = startTop;
                    yield return new WaitForSecondsRealtime(0.5f);
                }

                yield return null;
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(4f);
        }

        if (!_isExiting)
        {
            if (onComplete != null) onComplete.Invoke();
            else ReturnToMainMenu();
        }
    }

    private void OnSkipClicked()
    {
        if (_isExiting) return;
        _isExiting = true;
        Debug.Log("[EndingCreditsUI] Bấm nút Bỏ Qua -> Chuyển về MainMenu!");
        ReturnToMainMenu();
    }

    public void ReturnToMainMenu()
    {
        if (_isExiting) return;
        _isExiting = true;
        StartCoroutine(QuitToMainMenuRoutine());
    }

    private IEnumerator QuitToMainMenuRoutine()
    {
        string roomId = PlayerPrefs.GetString("CurrentRoomID", "");
        if (!string.IsNullOrEmpty(roomId))
        {
            _ = AuthService.LeaveRoom(roomId);
        }

        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[EndingCreditsUI] Ngắt kết nối Netcode...");
            NetworkManager.Singleton.Shutdown();
        }

        yield return null;
        yield return null;

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        PlayerHUDController.isAnyUIOpen = false;
        IsEndingActive = false;

        if (SceneLoader.Instance != null)
        {
            _ = SceneLoader.Instance.LoadSceneAsync("MainMenu", "CẢM ƠN BẠN ĐÃ TRẢI NGHIỆM TRÒ CHƠI...");
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    // =========================================================================
    // PREVIEW CHO UNITY EDITOR
    // =========================================================================
    public void PreviewCreditsInEditor()
    {
        HideAll();
        InitializeUI();
        if (_root != null)
        {
            _root.style.display = DisplayStyle.Flex;
            _root.RemoveFromClassList("hidden-element");
            _root.style.opacity = 1;
        }
        if (_creditsViewport != null && _creditsScrollContainer != null)
        {
            _creditsViewport.style.display = DisplayStyle.Flex;
            _creditsViewport.RemoveFromClassList("hidden-element");
            _creditsScrollContainer.style.top = 100f; // Vị trí dễ nhìn lúc preview
        }
        Debug.Log("[EndingCreditsUI] Đã hiển thị Preview Bảng Credits (không bị đè chữ)!");
    }

    public void PreviewEpilogueInEditor()
    {
        HideAll();
        InitializeUI();
        if (_root != null)
        {
            _root.style.display = DisplayStyle.Flex;
            _root.RemoveFromClassList("hidden-element");
            _root.style.opacity = 1;
        }
        if (_epiloguePanel != null)
        {
            if (_lblEpilogueQuote != null && !string.IsNullOrEmpty(epilogueQuote))
            {
                _lblEpilogueQuote.text = $"\"{epilogueQuote}\"";
            }
            _epiloguePanel.style.display = DisplayStyle.Flex;
            _epiloguePanel.RemoveFromClassList("hidden-element");
            _epiloguePanel.style.opacity = 1;
        }
        Debug.Log("[EndingCreditsUI] Đã hiển thị Preview Dòng chữ Epilogue!");
    }

    public void ClearPreviewInEditor()
    {
        HideAll();
        Debug.Log("[EndingCreditsUI] Đã ẩn UI Preview!");
    }
}
