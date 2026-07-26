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
                if (sr.sharedMaterial != null && sr.sharedMaterial.HasProperty("_FillAmount"))
                {
                    sr.GetPropertyBlock(propBlock);
                    propBlock.SetFloat("_FillAmount", alpha);
                    sr.SetPropertyBlock(propBlock);
                }
                else
                {
                    Color c = sr.color;
                    c.a = alpha;
                    sr.color = c;
                }
                continue;
            }

            if (r.sharedMaterials == null) continue;

            for (int i = 0; i < r.sharedMaterials.Length; i++)
            {
                Material mat = r.sharedMaterials[i];
                if (mat == null) continue;

                r.GetPropertyBlock(propBlock, i);
                bool changed = false;

                // Ưu tiên truyền _FillAmount nếu Material có hỗ trợ hiệu ứng nạp từ dưới lên
                if (mat.HasProperty("_FillAmount"))
                {
                    propBlock.SetFloat("_FillAmount", alpha);

                    // Tự động tính toán _MinBound và _MaxBound từ kích thước thực của mô hình 3D (Mesh)
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Bounds bounds = mf.sharedMesh.bounds;
                        int fillAxis = mat.HasProperty("_FillAxis") ? mat.GetInt("_FillAxis") : 1;
                        
                        // Tự động ép trục nạp năng lượng thành World Y (từ dưới lên) cho tia sét
                        if (r.gameObject.name.Contains("Setcuadong"))
                        {
                            fillAxis = 3;
                            propBlock.SetInt("_FillAxis", 3);
                        }

                        float minB = bounds.min.y;
                        float maxB = bounds.max.y;
                        if (fillAxis == 0) { minB = bounds.min.x; maxB = bounds.max.x; }
                        else if (fillAxis == 2) { minB = bounds.min.z; maxB = bounds.max.z; }
                        else if (fillAxis == 3) { 
                            Bounds worldBounds = r.bounds;
                            minB = worldBounds.min.y; 
                            maxB = worldBounds.max.y; 
                        }

                        propBlock.SetFloat("_MinBound", minB);
                        propBlock.SetFloat("_MaxBound", maxB);
                    }

                    changed = true;
                }
                else
                {
                    // Nếu không có _FillAmount thì làm mờ (Fade Alpha) như cũ
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
        if (chargingEffect != null)
        {
            if (!chargingEffect.activeSelf)
                chargingEffect.SetActive(true); // Luôn hiện hình tia sét (khi = 0 nó sẽ màu xám)
            
            float progress = maxCharge > 0 ? Mathf.Clamp01(charge.Value / maxCharge) : 0f;
            UpdateChargingEffectAlpha(progress);
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

            // Bật Particle Effect chạy liên tục khi đã nạp đầy 100%
            if (completedEffect != null)
            {
                completedEffect.SetActive(true);
                ParticleSystem[] pss = completedEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in pss) ps.Play(true);
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