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

    void Update()
    {
        if(charge.Value > 0)
        {
            if(chargingEffect != null)
                chargingEffect.SetActive(true);
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

        // Nếu trong 0.2s vừa qua có thay đổi charge, tức là người chơi đang giữ E
        bool isChargingNow = (Time.time - lastChargeTime) < 0.2f && charge.Value < maxCharge;

        // Bật completedEffect khi đang được sạc
        if (completedEffect != null)
        {
            if (isChargingNow && !completedEffect.activeSelf)
            {
                completedEffect.SetActive(true);
                ParticleSystem ps = completedEffect.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
            }
            else if (!isChargingNow && completedEffect.activeSelf)
            {
                completedEffect.SetActive(false);
                ParticleSystem ps = completedEffect.GetComponent<ParticleSystem>();
                if (ps != null) ps.Stop();
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