using System.Collections.Generic;
using UnityEngine;

public class PlayerHealOrbitVfx : MonoBehaviour
{
    [Header("--- CẤU HÌNH HIỆU ỨNG DẤU + BAY TỪ DƯỚI LÊN ĐẦU ---")]
    public GameObject itemPrefab;

    [Tooltip("Số lượng dấu + tối đa xuất hiện cùng lúc (cho hiệu ứng dày đặc đẹp mắt)")]
    public int maxCount = 22;

    [Tooltip("Tốc độ sinh thêm dấu + mới liên tục (cái/giây)")]
    public float spawnRate = 14f;

    [Tooltip("Bán kính vùng sinh dấu + xung quanh chân/thân player (m)")]
    public float spawnRadius = 0.45f;

    [Tooltip("Độ cao bắt đầu xuất hiện (ngang chân/thắt lưng)")]
    public float startHeight = 0.2f;

    [Tooltip("Tốc độ bay lên qua khỏi đầu (m/s)")]
    public float minRiseSpeed = 1.2f;
    public float maxRiseSpeed = 2.4f;

    [Tooltip("Kích thước dấu + thu nhỏ xinh xắn")]
    public float scale = 0.18f;

    [Tooltip("Tổng thời gian diễn ra hiệu ứng (giây)")]
    public float duration = 2.5f;

    [Header("--- ÁNH SÁNG XANH NGỌC LỤC BẢO ---")]
    public bool enableLightGlow = true;
    public Color healGlowColor = new Color(0.25f, 1.0f, 0.45f, 1f);

    private class PlusItem
    {
        public GameObject gameObject;
        public Transform transform;
        public Vector3 initialLocalPos;
        public float riseSpeed;
        public float swaySpeed;
        public float swayOffset;
        public float lifetime;
        public float maxLifetime;
        public float baseScale;
    }

    private List<PlusItem> activeItems = new List<PlusItem>();
    private Light healLight;
    private float timer = 0f;
    private float spawnTimer = 0f;

    public void Initialize(GameObject prefab)
    {
        itemPrefab = prefab;
    }

    private void Start()
    {
        if (enableLightGlow)
        {
            GameObject lightObj = new GameObject("HealAuraLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            healLight = lightObj.AddComponent<Light>();
            healLight.type = LightType.Point;
            healLight.color = healGlowColor;
            healLight.range = 3.5f;
            healLight.intensity = 0f;
        }

        // Sinh đợt dấu + đầu tiên dày đặc ngay lập tức
        for (int i = 0; i < 10; i++)
        {
            SpawnSinglePlus();
        }
    }

    private void SpawnSinglePlus()
    {
        if (itemPrefab == null) return;

        GameObject obj = Instantiate(itemPrefab, transform);
        
        // Gỡ bỏ Collider & script không dùng
        Collider[] cols = obj.GetComponentsInChildren<Collider>();
        foreach (var c in cols) Destroy(c);

        var scripts = obj.GetComponentsInChildren<MonoBehaviour>();
        foreach (var s in scripts)
        {
            if (s != this) Destroy(s);
        }

        Vector2 randCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 localStartPos = new Vector3(randCircle.x, startHeight + Random.Range(0f, 0.4f), randCircle.y);

        float itemScale = scale * Random.Range(0.75f, 1.25f);
        obj.transform.localPosition = localStartPos;
        obj.transform.localScale = Vector3.one * itemScale;
        obj.transform.localRotation = Random.rotation;

        PlusItem item = new PlusItem
        {
            gameObject = obj,
            transform = obj.transform,
            initialLocalPos = localStartPos,
            riseSpeed = Random.Range(minRiseSpeed, maxRiseSpeed),
            swaySpeed = Random.Range(2.5f, 6f),
            swayOffset = Random.Range(0f, Mathf.PI * 2f),
            lifetime = 0f,
            maxLifetime = Random.Range(0.8f, 1.3f),
            baseScale = itemScale
        };

        activeItems.Add(item);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Tự động sinh thêm dấu + mới liên tục trong suốt 2.5s
        if (timer < duration - 0.5f && activeItems.Count < maxCount)
        {
            spawnTimer += Time.deltaTime;
            float timeBetweenSpawns = 1f / spawnRate;
            while (spawnTimer >= timeBetweenSpawns)
            {
                spawnTimer -= timeBetweenSpawns;
                SpawnSinglePlus();
            }
        }

        // Cập nhật quầng sángPointLight tỏa ra từ thân người
        if (healLight != null)
        {
            float targetIntensity = (timer < duration - 0.5f) ? (3.5f + Mathf.Sin(timer * 10f) * 0.5f) : ((duration - timer) / 0.5f * 3.5f);
            healLight.intensity = Mathf.Max(0f, targetIntensity);
        }

        // Cập nhật vị trí từng dấu +: Bay từ chân ➔ qua khỏi đầu ➔ thu nhỏ tan biến
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            PlusItem item = activeItems[i];
            if (item.gameObject == null)
            {
                activeItems.RemoveAt(i);
                continue;
            }

            item.lifetime += Time.deltaTime;
            float progress = item.lifetime / item.maxLifetime;

            if (progress >= 1f)
            {
                Destroy(item.gameObject);
                activeItems.RemoveAt(i);
                continue;
            }

            // Di chuyển thẳng lên trên + lắc lư nhẹ nhàng 2 bên
            float currentY = item.initialLocalPos.y + (item.riseSpeed * item.lifetime);
            float swayX = Mathf.Sin(item.lifetime * item.swaySpeed + item.swayOffset) * 0.12f;
            float swayZ = Mathf.Cos(item.lifetime * item.swaySpeed + item.swayOffset) * 0.12f;

            item.transform.localPosition = new Vector3(
                item.initialLocalPos.x + swayX,
                currentY,
                item.initialLocalPos.z + swayZ
            );

            // Tự xoay 3D nhẹ nhàng
            item.transform.Rotate(Vector3.up * 160f * Time.deltaTime, Space.Self);

            // Phóng to nhẹ khi xuất hiện từ chân và thu nhỏ tan biến khi qua khỏi đầu
            float alphaScale = 1f;
            if (progress < 0.2f)
            {
                alphaScale = progress / 0.2f;
            }
            else if (progress > 0.6f)
            {
                alphaScale = (1f - progress) / 0.4f;
            }

            item.transform.localScale = Vector3.one * (item.baseScale * alphaScale);
        }

        if (timer >= duration && activeItems.Count == 0)
        {
            Destroy(gameObject);
        }
    }
}
