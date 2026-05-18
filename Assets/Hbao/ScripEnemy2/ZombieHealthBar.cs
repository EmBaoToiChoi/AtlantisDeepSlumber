using UnityEngine;
using UnityEngine.UIElements;

public class ZombieHealthBar : MonoBehaviour
{
    [Header("References")]
    public Enemy2_Zombie enemy;
    public UIDocument uiDocument;

    private VisualElement progressBar; 
    private VisualElement yellowBar;
    private Label nameLabel;
    private Camera mainCamera;

    private void OnEnable()
    {
        mainCamera = Camera.main;
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

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

        if (enemy == null) enemy = GetComponentInParent<Enemy2_Zombie>();

        if (enemy != null)
        {
            // Cập nhật lượng HP khởi tạo của Zombie
            UpdateHealthUI(0f, enemy.currentHealth.Value);
            enemy.currentHealth.OnValueChanged += UpdateHealthUI;
        }
    }

    private void OnDisable()
    {
        if (enemy != null)
        {
            enemy.currentHealth.OnValueChanged -= UpdateHealthUI;
        }
    }

    private void UpdateHealthUI(float oldVal, float newVal)
    {
        if (enemy != null)
        {
            float maxHp = enemy.maxHealth > 0 ? enemy.maxHealth : 100f; 
            float percent = Mathf.Clamp01(newVal / maxHp) * 100f;
            
            if (progressBar != null) progressBar.style.width = Length.Percent(percent);
            if (yellowBar != null) yellowBar.style.width = Length.Percent(percent);
        }
    }

    private void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Billboard logic: Đảm bảo thanh máu luôn quay mặt đối diện phẳng với Camera góc nhìn Player
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}
