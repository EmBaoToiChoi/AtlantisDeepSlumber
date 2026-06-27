using UnityEngine;

public class OilLampController : MonoBehaviour
{
    [Header("Reference")]
    public BalanceManager balanceManager;

    public ParticleSystem fireParticle;

    public Light fireLight;

    [Header("Settings")]
    public float safeAngle = 10f;
    public float dangerAngle = 15f;

    private ParticleSystem.MainModule main;

    void Start()
    {
        if (fireParticle != null)
            main = fireParticle.main;
    }

    void Update()
    {
        if (balanceManager == null)
            return;

        float angle = balanceManager.CurrentAngle;

        UpdateFire(angle);
    }

    void UpdateFire(float angle)
    {
        if (fireParticle == null || fireLight == null)
            return;

        Color targetColor;

        float targetSize;

        float targetSpeed;

        float targetIntensity;

        if (angle <= safeAngle)
        {
            //--------------------
            // Vàng
            //--------------------
            targetColor = new Color(1f, 0.82f, 0.2f);

            targetSize = 0.35f;

            targetSpeed = 1.5f;

            targetIntensity = 1.2f;
        }
        else if (angle <= dangerAngle)
        {
            //--------------------
            // Cam
            //--------------------

            float t =
                Mathf.InverseLerp(
                    safeAngle,
                    dangerAngle,
                    angle
                );

            targetColor =
                Color.Lerp(
                    new Color(1f,0.82f,0.2f),
                    new Color(1f,0.45f,0f),
                    t
                );

            targetSize =
                Mathf.Lerp(
                    0.35f,
                    0.55f,
                    t
                );

            targetSpeed =
                Mathf.Lerp(
                    1.5f,
                    2.2f,
                    t
                );

            targetIntensity =
                Mathf.Lerp(
                    1.2f,
                    2.2f,
                    t
                );
        }
        else
        {
            //--------------------
            // Đỏ
            //--------------------

            float t =
                Mathf.InverseLerp(
                    dangerAngle,
                    20f,
                    angle
                );

            targetColor =
                Color.Lerp(
                    new Color(1f,0.45f,0f),
                    Color.red,
                    t
                );

            targetSize =
                Mathf.Lerp(
                    0.55f,
                    0.8f,
                    t
                );

            targetSpeed =
                Mathf.Lerp(
                    2.2f,
                    3.2f,
                    t
                );

            targetIntensity =
                Mathf.Lerp(
                    2.2f,
                    3.5f,
                    t
                );
        }

        //----------------------
        // Smooth
        //----------------------

        main.startColor =
            Color.Lerp(
                (Color)main.startColor.color,
                targetColor,
                Time.deltaTime * 4f
            );

        main.startSize =
            Mathf.Lerp(
                main.startSize.constant,
                targetSize,
                Time.deltaTime * 4f
            );

        main.startSpeed =
            Mathf.Lerp(
                main.startSpeed.constant,
                targetSpeed,
                Time.deltaTime * 4f
            );

        fireLight.color =
            Color.Lerp(
                fireLight.color,
                targetColor,
                Time.deltaTime * 4f
            );

        fireLight.intensity =
            Mathf.Lerp(
                fireLight.intensity,
                targetIntensity,
                Time.deltaTime * 4f
            );
    }
}