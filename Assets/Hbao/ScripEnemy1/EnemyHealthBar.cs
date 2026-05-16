using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    public Enemy1_DapBua enemy;
    public UIDocument uiDocument;

    private VisualElement progressBar;
    private Camera mainCamera;

    private void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        if (uiDocument != null)
        {
            var root = uiDocument.rootVisualElement;
            progressBar = root.Q<VisualElement>("progress-bar");
            var nameLabel = root.Q<Label>("enemy-name");
            
            if (nameLabel != null && enemy != null)
            {
                nameLabel.text = enemy.gameObject.name; // Hoặc gán tên tùy chỉnh
            }
        }

        if (enemy == null)
        {
            enemy = GetComponentInParent<Enemy1_DapBua>();
        }

        if (enemy != null)
        {
            // Cập nhật giá trị ban đầu
            UpdateHealthUI(0, enemy.currentHealth.Value);
            
            // Lắng nghe sự thay đổi của máu từ NetworkVariable
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

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void UpdateHealthUI(float previousValue, float newValue)
    {
        if (progressBar != null && enemy != null)
        {
            float percentage = Mathf.Clamp01(newValue / enemy.maxHealth) * 100f;
            progressBar.style.width = new Length(percentage, LengthUnit.Percent);
        }
    }

    private void LateUpdate()
    {
        // Tìm camera nếu chưa có
        if (mainCamera == null) mainCamera = Camera.main;

        // Làm cho thanh máu luôn hướng về phía Camera (Billboard effect)
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}
