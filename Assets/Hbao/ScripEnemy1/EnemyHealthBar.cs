using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    public Enemy1_DapBua enemy;
    public Enemy2_Zombie enemy2;
    public Enemy3_Buaa enemy3;
    public Enemy4_Bongtoi enemy4;
    public Enemy5_PhuThuy enemy5;
    public MiniBossAI miniBoss;
    public Skeleton skeleton;
    public UIDocument uiDocument;

    private VisualElement progressBar; 
    private VisualElement progressFill;
    private VisualElement healthBarContainer;
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

    private bool isInitialized = false;

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

        if (quadTransform == null)
        {
            Debug.LogError($"[EnemyHealthBar] Không tìm thấy đối tượng 'Quad' ở {gameObject.name} hoặc cha của nó. Thanh máu World Space sẽ KHÔNG hiển thị. Hãy copy đối tượng 'Quad' từ Prefab Zombie hoặc Enemy khác sang!");
        }

        // Khởi tạo RenderTexture và Material trong suốt độc lập cho từng Enemy
        InitializeUniqueUI();

        QueryVisualElements();

        // Tự động tìm kiếm Enemy component ở cha nếu chưa được gán
        FindEnemyInParent();

        // Gán tên và subscribe sự kiện máu thay đổi
        InitEnemyHealthAndName();
    }

    private void QueryVisualElements()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var root = uiDocument.rootVisualElement;
            healthBarContainer = root.Q<VisualElement>("health-bar-container");
            progressBar = root.Q<VisualElement>("progress-bar"); 
            progressFill = root.Q<VisualElement>("progress-fill");
            nameLabel = root.Q<Label>("enemy-name");
        }
    }

    private void FindEnemyInParent()
    {
        if (miniBoss != null)
        {
            enemy = null; enemy2 = null; enemy3 = null; enemy4 = null; enemy5 = null; skeleton = null;
            return;
        }

        if (enemy == null && enemy2 == null && enemy3 == null && enemy4 == null && enemy5 == null && miniBoss == null && skeleton == null)
        {
            miniBoss = GetComponentInParent<MiniBossAI>();
            if (miniBoss == null && transform.parent != null) miniBoss = transform.parent.GetComponentInChildren<MiniBossAI>();
            if (miniBoss != null) return;

            enemy = GetComponentInParent<Enemy1_DapBua>();
            if (enemy == null && transform.parent != null) enemy = transform.parent.GetComponentInChildren<Enemy1_DapBua>();
            if (enemy != null) return;

            enemy2 = GetComponentInParent<Enemy2_Zombie>();
            if (enemy2 == null && transform.parent != null) enemy2 = transform.parent.GetComponentInChildren<Enemy2_Zombie>();
            if (enemy2 != null) return;

            enemy3 = GetComponentInParent<Enemy3_Buaa>();
            if (enemy3 == null && transform.parent != null) enemy3 = transform.parent.GetComponentInChildren<Enemy3_Buaa>();
            if (enemy3 != null) return;

            enemy4 = GetComponentInParent<Enemy4_Bongtoi>();
            if (enemy4 == null && transform.parent != null) enemy4 = transform.parent.GetComponentInChildren<Enemy4_Bongtoi>();
            if (enemy4 != null) return;

            enemy5 = GetComponentInParent<Enemy5_PhuThuy>();
            if (enemy5 == null && transform.parent != null) enemy5 = transform.parent.GetComponentInChildren<Enemy5_PhuThuy>();
            if (enemy5 != null) return;

            skeleton = GetComponentInParent<Skeleton>();
            if (skeleton == null && transform.parent != null) skeleton = transform.parent.GetComponentInChildren<Skeleton>();
        }
    }

    private void InitEnemyHealthAndName()
    {
        if (miniBoss != null)
        {
            // Mini Boss & Clones dùng thanh máu ở UI trên cùng góc màn hình (MiniBossHealthBar.cs), không dùng thanh máu 3D trên đầu
            if (quadTransform != null) quadTransform.gameObject.SetActive(false);
            if (uiDocument != null) uiDocument.enabled = false;
            enabled = false;
            return;
        }

        string enemyName = "Enemy";
        float curHp = GetActualHealth();
        float maxHp = GetMaxHealth();
        if (maxHp <= 0f) maxHp = 100f;

        if (enemy != null) enemyName = enemy.gameObject.name;
        else if (enemy2 != null) enemyName = enemy2.gameObject.name;
        else if (enemy3 != null) enemyName = enemy3.gameObject.name;
        else if (enemy4 != null) enemyName = enemy4.gameObject.name;
        else if (enemy5 != null) enemyName = enemy5.gameObject.name;
        else if (skeleton != null) enemyName = skeleton.gameObject.name;

        // Loại bỏ hậu tố (Clone) để tên hiển thị đẹp mắt
        if (enemyName.Contains("(Clone)"))
        {
            enemyName = enemyName.Replace("(Clone)", "").Trim();
        }

        if (nameLabel != null)
        {
            nameLabel.text = enemyName;
        }

        if (curHp > 0f)
        {
            displayedHealth = curHp;
            isInitialized = true;
        }
        else
        {
            displayedHealth = maxHp; // Tránh hiển thị rỗng lúc vừa spawn
        }

        if (progressFill != null && healthBarContainer != null && healthBarContainer.resolvedStyle.width > 0f)
        {
            progressFill.style.width = healthBarContainer.resolvedStyle.width;
        }

        float hpRatio = Mathf.Clamp01(displayedHealth / maxHp);
        float percent = (hpRatio <= 0f) ? 0f : Mathf.Lerp(11.3f, 90f, hpRatio);
        if (progressBar != null) progressBar.style.width = Length.Percent(percent);
    }

    private void OnDisable()
    {
        displayedHealth = -1f;
        isInitialized = false;
    }

    private float GetMaxHealth()
    {
        if (miniBoss != null) return miniBoss.maxHealth;
        if (enemy != null) return enemy.maxHealth;
        if (enemy2 != null) return enemy2.maxHealth;
        if (enemy3 != null) return enemy3.maxHealth;
        if (enemy4 != null) return enemy4.maxHealth;
        if (enemy5 != null) return enemy5.maxHealth;
        if (skeleton != null) return skeleton.maxHealth;
        return 100f;
    }

    private float GetActualHealth()
    {
        if (miniBoss != null) return miniBoss.ActualCurrentHealth;
        if (enemy != null) return enemy.ActualCurrentHealth;
        if (enemy2 != null) return enemy2.ActualCurrentHealth;
        if (enemy3 != null) return enemy3.ActualCurrentHealth;
        if (enemy4 != null) return enemy4.ActualCurrentHealth;
        if (enemy5 != null) return enemy5.ActualCurrentHealth;
        if (skeleton != null) return skeleton.ActualCurrentHealth;
        return 0f;
    }

    private void UpdateHealthAnimation()
    {
        // Thử gán lại các VisualElement nếu lúc OnEnable chưa tải xong
        if (progressBar == null || nameLabel == null)
        {
            QueryVisualElements();
            if (progressBar != null && nameLabel != null)
            {
                InitEnemyHealthAndName();
            }
        }

        FindEnemyInParent();

        float maxHp = GetMaxHealth();
        if (maxHp <= 0f) maxHp = 100f;

        float actualHp = GetActualHealth();

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

        if (progressFill != null && healthBarContainer != null && healthBarContainer.resolvedStyle.width > 0f)
        {
            progressFill.style.width = healthBarContainer.resolvedStyle.width;
        }

        float hpRatio = Mathf.Clamp01(displayedHealth / maxHp);
        float percent = (hpRatio <= 0f) ? 0f : Mathf.Lerp(11.3f, 90f, hpRatio);
        if (progressBar != null)
        {
            progressBar.style.width = Length.Percent(percent);
        }
    }

    private bool IsEnemyDead()
    {
        if (enemy != null && enemy.IsDead) return true;
        if (enemy2 != null && enemy2.IsDead) return true;
        if (enemy3 != null && enemy3.IsDead) return true;
        if (enemy4 != null && enemy4.IsDead) return true;
        if (enemy5 != null && enemy5.IsDead) return true;
        if (skeleton != null && (skeleton.ActualCurrentHealth <= 0f || skeleton.currentState == Skeleton.State.Dead)) return true;
        if (miniBoss != null && miniBoss.IsDead) return true;
        if (isInitialized && GetActualHealth() <= 0f) return true;
        return false;
    }

    private void Update()
    {
        if (IsEnemyDead())
        {
            if (quadTransform != null && quadTransform.gameObject.activeSelf) quadTransform.gameObject.SetActive(false);
            if (uiDocument != null && uiDocument.gameObject.activeSelf) uiDocument.gameObject.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

        // Luôn kiểm tra camera nếu bị mất (ví dụ khi đổi scene)
        if (mainCamera == null || !mainCamera.gameObject.activeInHierarchy) mainCamera = Camera.main;

        // Billboard logic: Luôn hướng về Camera
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
