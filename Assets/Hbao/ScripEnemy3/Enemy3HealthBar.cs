using UnityEngine;
using UnityEngine.UIElements;

public class Enemy3HealthBar : MonoBehaviour
{
    [Header("References")]
    public Enemy3_Buaa enemy;
    public UIDocument uiDocument;

    private VisualElement progressBar; 
    private VisualElement yellowBar;
    private Label nameLabel;
    private Camera mainCamera;
    private Transform quadTransform;

    private RenderTexture uniqueRT;
    private PanelSettings uniqueSettings;
    private Material uniqueMaterial;

    private float displayedHealth = -1f;
    private float yellowHealth = -1f;
    private float yellowDrainDelay = 0.5f;
    private float yellowDrainTimer = 0f;

    private void OnEnable()
    {
        if (enemy == null) enemy = GetComponentInParent<Enemy3_Buaa>();

        mainCamera = Camera.main;
        if (uiDocument == null) uiDocument = GetComponentInChildren<UIDocument>();

        // Tìm Quad dùng làm Mesh hiển thị
        quadTransform = transform.Find("Quad");
        if (quadTransform == null && transform.parent != null)
        {
            quadTransform = transform.parent.Find("Quad");
        }
        if (quadTransform == null)
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Quad") { quadTransform = child; break; }
            }
        }
        if (quadTransform == null && transform.parent != null)
        {
            foreach (Transform child in transform.parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Quad") { quadTransform = child; break; }
            }
        }

        // Khởi tạo RenderTexture và Material trong suốt độc lập cho từng Enemy
        InitializeUniqueUI();

        if (uiDocument != null)
        {
            var root = uiDocument.rootVisualElement;
            progressBar = root.Q<VisualElement>("progress-bar"); 
            yellowBar = root.Q<VisualElement>("yellow-bar");
            nameLabel = root.Q<Label>("enemy-name");
            
            if (nameLabel != null && enemy != null)
            {
                nameLabel.text = enemy.gameObject.name;
            }
        }

        if (enemy != null)
        {
            float curHp = enemy.ActualCurrentHealth;
            displayedHealth = curHp;
            yellowHealth = curHp;

            float maxHp = enemy.maxHealth > 0 ? enemy.maxHealth : 100f; 
            float percent = Mathf.Clamp01(curHp / maxHp) * 100f;
            
            if (progressBar != null) progressBar.style.width = Length.Percent(percent);
            if (yellowBar != null) yellowBar.style.width = Length.Percent(percent);
        }
    }

    private void OnDisable()
    {
        displayedHealth = -1f;
        yellowHealth = -1f;
    }

    private void UpdateHealthAnimation()
    {
        if (enemy == null) return;

        float maxHp = enemy.maxHealth > 0f ? enemy.maxHealth : 100f;
        float actualHp = enemy.ActualCurrentHealth;

        if (displayedHealth < 0f)
        {
            displayedHealth = actualHp;
            yellowHealth = actualHp;
        }

        displayedHealth = actualHp;
        float percent = Mathf.Clamp01(displayedHealth / maxHp) * 100f;
        if (progressBar != null)
        {
            progressBar.style.width = Length.Percent(percent);
        }

        if (actualHp < yellowHealth)
        {
            if (yellowDrainTimer <= 0f || actualHp < displayedHealth)
            {
                yellowDrainTimer = yellowDrainDelay;
            }
        }
        else if (actualHp > yellowHealth)
        {
            yellowHealth = actualHp;
        }

        if (yellowDrainTimer > 0f)
        {
            yellowDrainTimer -= Time.deltaTime;
        }
        else
        {
            float drainSpeed = maxHp * 0.4f;
            yellowHealth = Mathf.MoveTowards(yellowHealth, actualHp, drainSpeed * Time.deltaTime);
        }

        float yellowPercent = Mathf.Clamp01(yellowHealth / maxHp) * 100f;
        if (yellowBar != null)
        {
            yellowBar.style.width = Length.Percent(yellowPercent);
        }
    }

    private void Update()
    {
        if (mainCamera == null || !mainCamera.gameObject.activeInHierarchy) mainCamera = Camera.main;

        // Billboard logic: Đảm bảo thanh máu luôn quay mặt phẳng đối diện Camera góc nhìn Player
        if (mainCamera != null)
        {
            // Sửa lỗi: Nếu script này được gắn ở Root của Enemy, ta chỉ quay Transform của UI Document (hoặc Canvas con)
            // để tránh làm quay cả thân quái (cúi mặt xuống đất)
            Transform targetRotationTransform = (uiDocument != null) ? uiDocument.transform : transform;
            
            // Nếu targetRotationTransform vẫn là root của Enemy (có script di chuyển/AI và Animator), ta BỎ QUA không quay để tránh lỗi cúi đầu
            if (targetRotationTransform == transform && GetComponent<Animator>() != null)
            {
                if (quadTransform != null)
                {
                    quadTransform.LookAt(quadTransform.position + mainCamera.transform.rotation * Vector3.forward,
                                         mainCamera.transform.rotation * Vector3.up);
                }
                UpdateHealthAnimation();
                return;
            }

            if (targetRotationTransform != null)
            {
                targetRotationTransform.LookAt(targetRotationTransform.position + mainCamera.transform.rotation * Vector3.forward,
                                 mainCamera.transform.rotation * Vector3.up);
            }

            if (quadTransform != null)
            {
                quadTransform.LookAt(quadTransform.position + mainCamera.transform.rotation * Vector3.forward,
                                 mainCamera.transform.rotation * Vector3.up);
            }
        }

        UpdateHealthAnimation();
    }

    private void InitializeUniqueUI()
    {
        if (uiDocument == null || uiDocument.panelSettings == null) return;

        // Tránh tạo lại nếu đã khởi tạo rồi (khi Object Pooling hoặc re-enable)
        if (uniqueRT == null)
        {
            int width = 512;
            int height = 512;
            if (uiDocument.panelSettings.targetTexture != null)
            {
                width = uiDocument.panelSettings.targetTexture.width;
                height = uiDocument.panelSettings.targetTexture.height;
            }

            // 1. Tạo RenderTexture riêng cho instance này
            uniqueRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            uniqueRT.Create();

            // 2. Clone PanelSettings và gán RenderTexture mới vào
            uniqueSettings = Instantiate(uiDocument.panelSettings);
            uniqueSettings.clearColor = true;
            uniqueSettings.colorClearValue = new Color(0f, 0f, 0f, 0f); // Hoàn toàn trong suốt
            uniqueSettings.targetTexture = uniqueRT;
            
            uiDocument.panelSettings = uniqueSettings;

            // 3. Clone Material của Quad và cấu hình Standard Shader sang chế độ Transparent
            if (quadTransform != null)
            {
                MeshRenderer renderer = quadTransform.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    uniqueMaterial = renderer.material; // Tự động clone material
                    
                    // Cấu hình Standard Shader thành Transparent mode
                    uniqueMaterial.SetFloat("_Mode", 3f);
                    uniqueMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    uniqueMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    uniqueMaterial.SetInt("_ZWrite", 0);
                    uniqueMaterial.DisableKeyword("_ALPHATEST_ON");
                    uniqueMaterial.DisableKeyword("_ALPHABLEND_ON");
                    uniqueMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    uniqueMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    
                    uniqueMaterial.mainTexture = uniqueRT;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (uniqueRT != null)
        {
            uniqueRT.Release();
            Destroy(uniqueRT);
        }
        if (uniqueSettings != null)
        {
            Destroy(uniqueSettings);
        }
        if (uniqueMaterial != null)
        {
            Destroy(uniqueMaterial);
        }
    }
}
