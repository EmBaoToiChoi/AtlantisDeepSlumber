using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    public Enemy1_DapBua enemy;
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

        if (enemy == null) enemy = GetComponentInParent<Enemy1_DapBua>();

        if (enemy != null)
        {
            // Cập nhật máu ban đầu dựa trên maxHealth thực tế của Enemy
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

    // Sửa kiểu dữ liệu từ int sang float để hết lỗi CS0123 và CS1503
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
        // Luôn kiểm tra camera nếu bị mất (ví dụ khi đổi scene)
        if (mainCamera == null) mainCamera = Camera.main;

        // Billboard logic: Luôn hướng về Camera
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}

