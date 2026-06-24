using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.Threading.Tasks;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private UIDocument _loadingUIDoc;
    private VisualElement _root;
    private VisualElement _progressFill;
    private Label _lblStatus;

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
            _progressFill = rootVE.Q<VisualElement>("progress-fill");
            _lblStatus = rootVE.Q<Label>("lbl-status");
        }
        
        // Đảm bảo ẩn lúc đầu
        if (_root != null) _root.AddToClassList("hidden-element");
    }

    private void Update()
    {
        if (_isProgressActive && _progressFill != null)
        {
            // Tăng thanh tiến trình mượt mà hướng tới target (dùng unscaledDeltaTime đề phòng game bị pause)
            _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.unscaledDeltaTime * 150f);
            _progressFill.style.width = Length.Percent(_currentProgress);
        }
    }

    public void ShowLoading(string statusText)
    {
        if (_root == null) InitializeUI();
        if (_root == null) return;
        
        _root.RemoveFromClassList("hidden-element");
        _root.style.opacity = 1;
        _lblStatus.text = statusText;
        
        _currentProgress = 0f;
        _targetProgress = 0f;
        _isProgressActive = true;
        if (_progressFill != null)
        {
            _progressFill.style.width = Length.Percent(0);
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
        _lblStatus.text = "READY TO DESCEND";
        
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
    }
}
