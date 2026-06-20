using UnityEngine;
using Unity.Netcode;

public class LeoInteraction : NetworkBehaviour
{
    private EnergyColumn currentColumn;

    void Update()
    {
        if(!IsOwner)
            return;

        CharacterInfo info =
            GetComponent<CharacterInfo>();

        if(info.characterType.Value
            != CharacterType.Leo)
            return;

        if(currentColumn == null)
            return;

        if(Input.GetKey(KeyCode.E))
        {
            currentColumn
                .AddChargeServerRpc(
                    20f * Time.deltaTime
                );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EnergyColumn column =
            other.GetComponent<EnergyColumn>();

        if(column != null)
            currentColumn = column;
    }

    private void OnTriggerExit(Collider other)
    {
        EnergyColumn column =
            other.GetComponent<EnergyColumn>();

        if(column != null)
            currentColumn = null;
    }
}