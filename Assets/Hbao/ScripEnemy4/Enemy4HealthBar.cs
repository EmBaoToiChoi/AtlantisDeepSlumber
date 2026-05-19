using UnityEngine;
using UnityEngine.UIElements;

public class Enemy4HealthBar : MonoBehaviour
{
    [Header("References")]
    public Enemy4_Bongtoi enemy;
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

        if (enemy == null) enemy = GetComponentInParent<Enemy4_Bongtoi>();

        if (enemy != null)
        {
            // Khởi tạo HP ban đầu
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

        // Xoay mặt phẳng UI đối diện với hướng camera của người chơi (Billboard Effect)
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}
