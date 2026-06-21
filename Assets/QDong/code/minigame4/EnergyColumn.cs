using Unity.Netcode;
using UnityEngine;

public class EnergyColumn : NetworkBehaviour
{
    public NetworkVariable<float> charge =
        new NetworkVariable<float>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public float maxCharge = 100;

    public bool IsCompleted()
    {
        return charge.Value >= maxCharge;
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

        Debug.Log(
            gameObject.name +
            " Charge = " +
            charge.Value
        );
    }
}
