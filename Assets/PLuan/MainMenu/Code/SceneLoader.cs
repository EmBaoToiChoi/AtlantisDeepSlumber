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
        
        var rootVE = _loadingUIDoc.rootVisualElement;
        _root = rootVE.Q<VisualElement>("loading-root");
        _progressFill = rootVE.Q<VisualElement>("progress-fill");
        _lblStatus = rootVE.Q<Label>("lbl-status");
        
        // Đảm bảo ẩn lúc đầu
        if (_root != null) _root.AddToClassList("hidden-element");
    }

    public void ShowLoading(string statusText)
    {
        if (_root == null) InitializeUI();
        if (_root == null) return;
        
        _root.RemoveFromClassList("hidden-element");
        _root.style.opacity = 1;
        _lblStatus.text = statusText;
        _progressFill.style.width = Length.Percent(0);
    }

    public void SetProgress(float progressPercent)
    {
        if (_progressFill != null)
        {
            _progressFill.style.width = Length.Percent(progressPercent);
        }
    }

    public async void HideLoading()
    {
        if (_root == null) return;
        if (_lblStatus != null) _lblStatus.text = "READY TO DESCEND";
        if (_progressFill != null) _progressFill.style.width = Length.Percent(100);
        
        await Task.Delay(500);
        _root.style.opacity = 0;
        await Task.Delay(500);
        _root.AddToClassList("hidden-element");
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
        _root.RemoveFromClassList("hidden-element");
        _root.style.opacity = 1;
        _lblStatus.text = statusText;
        _progressFill.style.width = Length.Percent(0);

        await Task.Delay(100); // Đợi 1 nhịp để UI kịp vẽ

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            _progressFill.style.width = Length.Percent(progress * 100);
            await Task.Yield();
        }

        _progressFill.style.width = Length.Percent(100);
        _lblStatus.text = "READY TO DESCEND";
        
        await Task.Delay(500);
        op.allowSceneActivation = true;

        while (!op.isDone) await Task.Yield();

        // Fade out
        _root.style.opacity = 0;
        await Task.Delay(500);
        _root.AddToClassList("hidden-element");
    }
}
