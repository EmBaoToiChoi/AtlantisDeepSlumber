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

            if(completedEffect != null)
                completedEffect.SetActive(true);

            Debug.Log(
                gameObject.name +
                " Completed"
            );
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