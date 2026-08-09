using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ArrowIndicatorVfx : MonoBehaviour
{
    [Header("--- NHẤP NHÁY MÀU SẮC (TINT FLASHING) ---")]
    [Tooltip("Bật hiệu ứng chớp tắt màu sắc nhịp thở")]
    public bool enableColorFlashing = true;

    [Tooltip("Màu sắc bình thường (Vàng hổ phách sáng / Hạt bạc)")]
    public Color normalColor = new Color(1f, 0.85f, 0.35f, 1f);

    [Tooltip("Màu sắc chớp bùng sáng (Đỏ ngọc ruby rực rỡ)")]
    public Color flashColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Tooltip("Tốc độ chớp tắt nhịp thở (lần/giây)")]
    public float flashSpeed = 4.5f;

    [Header("--- CHUYỂN ĐỘNG LẮC LƯ / BOUNCE ---")]
    [Tooltip("Bật hiệu ứng nhấp nhổm bay lơ lửng")]
    public bool enableBobbing = true;

    [Tooltip("Khoảng cách nhấp nhổm (mét)")]
    public float bobDistance = 0.15f;

    [Tooltip("Tốc độ nhấp nhổm lơ lửng")]
    public float bobSpeed = 3.5f;

    [Header("--- ÁNH SÁNG POINT LIGHT (VFX) ---")]
    [Tooltip("Bật hiệu ứng quầng sáng PointLight chiếu xung quanh mũi tên")]
    public bool enablePointLight = true;

    [Tooltip("Bán kính phát sáng (mét)")]
    public float lightRange = 3.5f;

    [Tooltip("Cường độ sáng tối đa")]
    public float maxLightIntensity = 4.0f;

    [Header("--- HẠT BỤI MA THUẬT (PARTICLE SPARKLES) ---")]
    [Tooltip("Bật hiệu ứng hạt bụi kim sa lóng lánh phát ra từ mũi tên")]
    public bool enableSparkleParticles = true;

    private SpriteRenderer spriteRenderer;
    private Light arrowLight;
    private ParticleSystem sparkleParticles;
    private Vector3 initialLocalPos;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialLocalPos = transform.localPosition;
    }

    private void Start()
    {
        // 1. Tự động khởi tạo PointLight chiếu sáng môi trường xung quanh
        if (enablePointLight)
        {
            GameObject lightObj = new GameObject("ArrowPointLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;

            arrowLight = lightObj.AddComponent<Light>();
            arrowLight.type = LightType.Point;
            arrowLight.range = lightRange;
            arrowLight.color = normalColor;
            arrowLight.intensity = maxLightIntensity;
        }

        // 2. Tự động khởi tạo ParticleSystem hạt kim sa lấp lánh
        if (enableSparkleParticles)
        {
            CreateSparkleParticles();
        }
    }

    private void CreateSparkleParticles()
    {
        GameObject psObj = new GameObject("ArrowSparkles");
        psObj.transform.SetParent(transform);
        psObj.transform.localPosition = Vector3.zero;

        sparkleParticles = psObj.AddComponent<ParticleSystem>();

        var main = sparkleParticles.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(normalColor, flashColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = sparkleParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var emission = sparkleParticles.emission;
        emission.rateOverTime = 25f;

        var colorOverLifetime = sparkleParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(normalColor, 0.0f), new GradientColorKey(flashColor, 0.5f), new GradientColorKey(normalColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(1.0f, 0.3f), new GradientAlphaKey(0f, 1.0f) }
        );
        colorOverLifetime.color = grad;

        var renderer = psObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Shader particleShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (particleShader != null)
            {
                Material pMat = new Material(particleShader);
                pMat.color = normalColor;
                renderer.material = pMat;
            }
        }
    }

    private float GetSyncedNetworkTime()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            return (float)Unity.Netcode.NetworkManager.Singleton.ServerTime.TimeAsFloat;
        }
        return Time.time;
    }

    private void Update()
    {
        // Sử dụng thời gian đồng bộ chuẩn từ Server Netcode để tất cả Player đều chớp cùng 1 tích tắc
        float networkTime = GetSyncedNetworkTime();
        float sineWave = (Mathf.Sin(networkTime * flashSpeed) + 1f) * 0.5f;

        // 1. Chớp tắt nhịp thở chuyển đổi giữa Đỏ Ngọc Ruby và Vàng Hổ Phách/Bạc
        if (enableColorFlashing && spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(normalColor, flashColor, sineWave);
        }

        // 2. Cập nhật quầng sáng PointLight rọi vào tường/nước xung quanh
        if (enablePointLight && arrowLight != null)
        {
            arrowLight.color = Color.Lerp(normalColor, flashColor, sineWave);
            arrowLight.intensity = maxLightIntensity * (0.65f + sineWave * 0.35f);
        }

        // 3. Nhấp nhổm nhấp nhô lơ lửng đồng bộ giữa tất cả các máy
        if (enableBobbing)
        {
            float bobOffset = Mathf.Sin(networkTime * bobSpeed) * bobDistance;
            transform.localPosition = initialLocalPos + transform.up * bobOffset;
        }
    }
}
