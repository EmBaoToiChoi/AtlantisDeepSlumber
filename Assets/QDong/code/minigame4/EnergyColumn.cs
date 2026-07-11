using UnityEngine;
using Unity.Netcode;

public class EnergyColumn : NetworkBehaviour
{
    public NetworkVariable<float> charge =
        new NetworkVariable<float>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public float maxCharge = 100f;

    public GameObject chargingEffect;
    public GameObject completedEffect;

    private bool completed = false;

    public bool IsCompleted()
    {
        return charge.Value >= maxCharge;
    }

    void Start()
    {
        if(chargingEffect != null)
            chargingEffect.SetActive(false);

        if(completedEffect != null)
            completedEffect.SetActive(false);
    }

    private float previousCharge = 0f;
    private float lastChargeTime = 0f;

    private MaterialPropertyBlock propBlock;

    private void UpdateChargingEffectAlpha(float alpha)
    {
        if (chargingEffect == null) return;

        if (propBlock == null)
            propBlock = new MaterialPropertyBlock();

        Renderer[] renderers = chargingEffect.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;

            if (r is SpriteRenderer sr)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
                continue;
            }

            if (r.sharedMaterials == null) continue;

            for (int i = 0; i < r.sharedMaterials.Length; i++)
            {
                Material mat = r.sharedMaterials[i];
                if (mat == null) continue;

                r.GetPropertyBlock(propBlock, i);
                bool changed = false;

                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.GetColor("_Color");
                    c.a *= alpha;
                    propBlock.SetColor("_Color", c);
                    changed = true;
                }
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a *= alpha;
                    propBlock.SetColor("_BaseColor", c);
                    changed = true;
                }
                if (mat.HasProperty("_TintColor"))
                {
                    Color c = mat.GetColor("_TintColor");
                    c.a *= alpha;
                    propBlock.SetColor("_TintColor", c);
                    changed = true;
                }
                if (mat.HasProperty("_EmissionColor"))
                {
                    Color em = mat.GetColor("_EmissionColor");
                    propBlock.SetColor("_EmissionColor", em * alpha);
                    changed = true;
                }

                if (changed)
                {
                    r.SetPropertyBlock(propBlock, i);
                }
            }
        }
    }

    void Update()
    {
        if(charge.Value > 0)
        {
            if(chargingEffect != null)
            {
                if (!chargingEffect.activeSelf)
                    chargingEffect.SetActive(true);
                
                float progress = maxCharge > 0 ? Mathf.Clamp01(charge.Value / maxCharge) : 0f;
                UpdateChargingEffectAlpha(progress);
            }
        }
        else
        {
            if(chargingEffect != null)
                chargingEffect.SetActive(false);
        }

        if(
            charge.Value >= maxCharge &&
            !completed
        )
        {
            completed = true;

            Debug.Log(
                gameObject.name +
                " Completed"
            );
        }

        // Phát hiện xem có đang được sạc không (dựa vào việc charge.Value tăng lên)
        if (charge.Value > previousCharge)
        {
            lastChargeTime = Time.time;
            previousCharge = charge.Value;
        }

        // Nếu trong 0.5s vừa qua có thay đổi charge, tức là người chơi đang giữ E
        // Tăng từ 0.2 lên 0.5 để bù trừ độ trễ mạng (Network Latency)
        bool isChargingNow = (Time.time - lastChargeTime) < 0.5f && charge.Value < maxCharge;

        // Bật completedEffect khi đang được sạc (như yêu cầu của user)
        if (completedEffect != null)
        {
            if (isChargingNow && !completedEffect.activeSelf)
            {
                completedEffect.SetActive(true);
                ParticleSystem[] pss = completedEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in pss) ps.Play(true);
            }
            else if (!isChargingNow && completedEffect.activeSelf)
            {
                completedEffect.SetActive(false);
                ParticleSystem[] pss = completedEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in pss) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddChargeServerRpc(float amount)
    {
        charge.Value =
            Mathf.Clamp(
                charge.Value + amount,
                0,
                maxCharge
            );
    }
}