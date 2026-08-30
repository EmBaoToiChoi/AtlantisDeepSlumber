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
    private bool isInitialized = false;

    private void EnsureEnemyReference()
    {
        if (enemy == null)
        {
            enemy = GetComponentInParent<Enemy3_Buaa>();
            if (enemy == null && transform.parent != null)
            {
                enemy = transform.parent.GetComponentInChildren<Enemy3_Buaa>();
            }
            if (enemy == null)
            {
                enemy = GetComponent<Enemy3_Buaa>();
            }
        }
    }

    private void OnEnable()
    {
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

        EnsureEnemyReference();

        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var root = uiDocument.rootVisualElement;
            progressBar = root.Q<VisualElement>("progress-bar"); 
            nameLabel = root.Q<Label>("enemy-name");
            
            if (nameLabel != null && enemy != null)
            {
                string enemyName = enemy.gameObject.name;
                if (enemyName.Contains("(Clone)")) enemyName = enemyName.Replace("(Clone)", "").Trim();
                nameLabel.text = enemyName;
            }
        }

        float maxHp = (enemy != null && enemy.maxHealth > 0f) ? enemy.maxHealth : 100f;
        float curHp = (enemy != null) ? enemy.ActualCurrentHealth : maxHp;

        if (curHp > 0f)
        {
            displayedHealth = curHp;
            isInitialized = true;
        }
        else
        {
            displayedHealth = maxHp;
        }

        float percent = Mathf.Clamp01(displayedHealth / maxHp) * 100f;
        if (progressBar != null) progressBar.style.width = Length.Percent(percent);
    }

    private void OnDisable()
    {
        displayedHealth = -1f;
        isInitialized = false;
    }

    private void UpdateHealthAnimation()
    {
        EnsureEnemyReference();

        if (progressBar == null || nameLabel == null)
        {
            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                var root = uiDocument.rootVisualElement;
                progressBar = root.Q<VisualElement>("progress-bar");
                nameLabel = root.Q<Label>("enemy-name");
                if (nameLabel != null && enemy != null)
                {
                    string enemyName = enemy.gameObject.name;
                    if (enemyName.Contains("(Clone)")) enemyName = enemyName.Replace("(Clone)", "").Trim();
                    nameLabel.text = enemyName;
                }
            }
        }

        float maxHp = (enemy != null && enemy.maxHealth > 0f) ? enemy.maxHealth : 100f;
        float actualHp = (enemy != null) ? enemy.ActualCurrentHealth : 0f;

        if (actualHp > 0f)
        {
            isInitialized = true;
            displayedHealth = actualHp;
        }
        else if (!isInitialized)
        {
            displayedHealth = maxHp;
        }
        else
        {
            displayedHealth = actualHp;
        }

        float percent = Mathf.Clamp01(displayedHealth / maxHp) * 100f;
        if (progressBar != null)
        {
            progressBar.style.width = Length.Percent(percent);
        }
    }

    private void Update()
    {
        if (enemy != null && enemy.IsDead)
        {
            if (quadTransform != null && quadTransform.gameObject.activeSelf) quadTransform.gameObject.SetActive(false);
            if (uiDocument != null && uiDocument.gameObject.activeSelf) uiDocument.gameObject.SetActive(false);
            gameObject.SetActive(false);
            return;
        }
        if (isInitialized && enemy != null && enemy.ActualCurrentHealth <= 0f)
        {
            if (quadTransform != null && quadTransform.gameObject.activeSelf) quadTransform.gameObject.SetActive(false);
            if (uiDocument != null && uiDocument.gameObject.activeSelf) uiDocument.gameObject.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

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
