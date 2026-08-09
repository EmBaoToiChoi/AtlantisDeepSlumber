using UnityEngine;

public class PlayerHealOrbitVfx : MonoBehaviour
{
    [Header("Orbit Visual Settings")]
    public GameObject itemPrefab;
    public int count = 5;             // Số lượng dấu + bay xung quanh
    public float radius = 0.75f;       // Bán kính xoay quanh player
    public float heightOffset = 0.8f;  // Độ cao bắt đầu (ngang hông/ngực)
    public float orbitSpeed = 220f;    // Tốc độ xoay vòng quanh player (độ/giây)
    public float riseSpeed = 0.6f;      // Tốc độ bay lên nhẹ nhàng
    public float scale = 0.22f;         // Kích thước thu nhỏ vừa vặn
    public float duration = 2.5f;      // Thời gian tồn tại hiệu ứng

    private GameObject[] instances;
    private float timer = 0f;
    private float currentAngle = 0f;
    private float currentHeight = 0f;

    public void Initialize(GameObject prefab)
    {
        itemPrefab = prefab;
    }

    private void Start()
    {
        currentHeight = heightOffset;
        instances = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            if (itemPrefab != null)
            {
                instances[i] = Instantiate(itemPrefab, transform);
                
                // Gỡ bỏ toàn bộ Collider để không vướng vật lý
                Collider[] cols = instances[i].GetComponentsInChildren<Collider>();
                foreach (var c in cols) Destroy(c);

                // Gỡ bỏ các script thừa của prefab cũ (như Viapix_HealingItem) nếu có
                var scripts = instances[i].GetComponentsInChildren<MonoBehaviour>();
                foreach (var s in scripts)
                {
                    if (s != this) Destroy(s);
                }

                instances[i].transform.localScale = Vector3.one * scale;
            }
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        currentAngle += orbitSpeed * Time.deltaTime;
        currentHeight += riseSpeed * Time.deltaTime;

        // Thu nhỏ dần ở 0.6 giây cuối
        float fadeFactor = 1f;
        if (timer > duration - 0.6f)
        {
            fadeFactor = Mathf.Clamp01((duration - timer) / 0.6f);
        }

        for (int i = 0; i < count; i++)
        {
            if (instances[i] == null) continue;

            float angleOffset = (360f / count) * i;
            float angleRad = (currentAngle + angleOffset) * Mathf.Deg2Rad;

            Vector3 localPos = new Vector3(
                Mathf.Cos(angleRad) * radius,
                currentHeight,
                Mathf.Sin(angleRad) * radius
            );

            instances[i].transform.localPosition = localPos;
            instances[i].transform.localScale = Vector3.one * (scale * fadeFactor);
            instances[i].transform.Rotate(Vector3.up, 300f * Time.deltaTime, Space.Self);
        }

        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }
}
