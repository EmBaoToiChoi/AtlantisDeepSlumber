using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Video;
using System.Threading.Tasks;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("UI Document")]
    [SerializeField] private UIDocument _loadingUIDoc;

    [Header("Video Background Config")]
    [SerializeField] private VideoClip _loadingVideoClip;
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private RenderTexture _videoRenderTexture;

    private VisualElement _root;
    private VisualElement _videoBgElement;
    private VisualElement _progressFill;
    private Label _lblStatus;
    private Label _lblProgressPercent;
    private Label _lblLoadingTitle;

    private float _currentProgress = 0f;
    private float _targetProgress = 0f;
    private bool _isProgressActive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeUI()
    {
        if (_loadingUIDoc == null) return;
        
        // Đảm bảo loading screen luôn đè lên các UI khác
        _loadingUIDoc.sortingOrder = 9999;
        
        var rootVE = _loadingUIDoc.rootVisualElement;
        if (rootVE != null)
        {
            _root = rootVE.Q<VisualElement>("loading-root");
            _videoBgElement = rootVE.Q<VisualElement>("loading-video-bg");
            _progressFill = rootVE.Q<VisualElement>("progress-fill");
            _lblStatus = rootVE.Q<Label>("lbl-status");
            _lblProgressPercent = rootVE.Q<Label>("lbl-progress-percent");
            _lblLoadingTitle = rootVE.Q<Label>("lbl-loading-title");
        }

        // Thiết lập Video Background tự động
        SetupVideoPlayer();
        
        // Đảm bảo ẩn lúc đầu
        if (_root != null) _root.AddToClassList("hidden-element");
    }

    private void SetupVideoPlayer()
    {
        if (_videoRenderTexture == null)
        {
            _videoRenderTexture = new RenderTexture(1920, 1080, 0);
            _videoRenderTexture.name = "Loading_RenderTexture";
        }

        if (_videoPlayer == null)
        {
            _videoPlayer = GetComponent<VideoPlayer>();
            if (_videoPlayer == null)
            {
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }
        }

        _videoPlayer.playOnAwake = false;
        _videoPlayer.source = VideoSource.VideoClip;

#if UNITY_EDITOR
        if (_loadingVideoClip == null)
        {
            _loadingVideoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/PLuan/MainMenu/IMG/BackgroundLoading.mp4");
        }
#endif

        if (_loadingVideoClip != null)
        {
            _videoPlayer.clip = _loadingVideoClip;
        }
        _videoPlayer.isLooping = true; // Bật Loop cho video nền loading
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _videoRenderTexture;
        _videoPlayer.timeUpdateMode = VideoTimeUpdateMode.DSPTime; // Chạy theo thời gian thực (Audio/DSP) bất chấp Time.timeScale = 0
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // Tắt tiếng video nền để không át nhạc game

        // Gán RenderTexture vào UI Toolkit element
        if (_videoBgElement != null && _videoRenderTexture != null)
        {
            _videoBgElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_videoRenderTexture));
        }
        else if (_root != null && _videoRenderTexture != null)
        {
            _root.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_videoRenderTexture));
        }
    }

    private void Update()
    {
        if (_isProgressActive && _progressFill != null)
        {
            // Tăng thanh tiến trình mượt mà hướng tới target (dùng unscaledDeltaTime đề phòng game bị pause)
            _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.unscaledDeltaTime * 150f);
            _progressFill.style.width = Length.Percent(_currentProgress);

            if (_lblProgressPercent != null)
            {
                _lblProgressPercent.text = $"{Mathf.RoundToInt(_currentProgress)}%";
            }
        }
    }

    public void ShowLoading(string statusText)
    {
        if (_root == null) InitializeUI();
        if (_root == null) return;
        
        _root.RemoveFromClassList("hidden-element");
        _root.style.opacity = 1;
        if (_lblStatus != null) _lblStatus.text = statusText;
        
        _currentProgress = 0f;
        _targetProgress = 0f;
        _isProgressActive = true;
        if (_progressFill != null)
        {
            _progressFill.style.width = Length.Percent(0);
        }
        if (_lblProgressPercent != null)
        {
            _lblProgressPercent.text = "0%";
        }

        // Bắt đầu phát video nền lặp
        if (_videoPlayer != null && _videoPlayer.clip != null)
        {
            _videoPlayer.isLooping = true;
            _videoPlayer.Play();
        }
    }

    public void SetProgress(float progressPercent)
    {
        _targetProgress = progressPercent;
    }

    public void SetStatusText(string statusText)
    {
        if (_lblStatus != null)
        {
            _lblStatus.text = statusText;
        }
    }

    public async void HideLoading()
    {
        if (_root == null) return;
        if (_lblStatus != null) _lblStatus.text = "READY TO DESCEND";
        
        _targetProgress = 100f;
        
        // Chờ thanh tiến trình tăng đến 100% thật sự trên giao diện
        while (_currentProgress < 100f)
        {
            await Task.Yield();
        }
        
        await Task.Delay(500);
        _root.style.opacity = 0;
        await Task.Delay(500);
        _root.AddToClassList("hidden-element");
        _isProgressActive = false;

        // Tạm dừng video để tối ưu hóa CPU/GPU khi không cần hiển thị loading
        if (_videoPlayer != null && _videoPlayer.isPlaying)
        {
            _videoPlayer.Pause();
        }
    }

    public async Task LoadSceneAsync(string sceneName, string statusText = "INITIALIZING...")
    {
        if (_root == null) InitializeUI();
        if (_root == null)
        {
            Debug.LogError("[SceneLoader] UIDocument is missing or not initialized!");
            SceneManager.LoadScene(sceneName);
            return;
        }

        // Hiện loading screen
        ShowLoading(statusText);

        await Task.Delay(100); // Đợi 1 nhịp để UI kịp vẽ

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            SetProgress(progress * 100f);
            await Task.Yield();
        }

        SetProgress(100f);
        if (_lblStatus != null) _lblStatus.text = "READY TO DESCEND";
        
        // Chờ thanh progress bar chạy đến 100% thực sự
        while (_currentProgress < 100f)
        {
            await Task.Yield();
        }
        
        await Task.Delay(500);
        op.allowSceneActivation = true;

        while (!op.isDone) await Task.Yield();

        // Fade out
        _root.style.opacity = 0;
        await Task.Delay(500);
        _root.AddToClassList("hidden-element");
        _isProgressActive = false;

        if (_videoPlayer != null && _videoPlayer.isPlaying)
        {
            _videoPlayer.Pause();
        }
    }

    private void OnDestroy()
    {
        if (_videoRenderTexture != null)
        {
            _videoRenderTexture.Release();
            Destroy(_videoRenderTexture);
        }
    }
}
