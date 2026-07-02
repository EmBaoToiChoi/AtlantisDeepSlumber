using UnityEngine;
using Unity.Netcode;

public class LeoInteraction : NetworkBehaviour
{
    private EnergyColumn currentColumn;
    private BalanceManager balanceManager;

    void Start()
    {
        balanceManager = FindAnyObjectByType<BalanceManager>();
    }

    void Update()
    {
        if(!IsOwner)
            return;

        IPlayerHUDTarget info =
            GetComponent<IPlayerHUDTarget>();

        if(info == null || info.CharacterClassIndex != 0) // 0 là Leo
            return;

        if(currentColumn == null)
            return;

        if(Input.GetKey(KeyCode.E))
        {
            if (balanceManager != null && balanceManager.CurrentAngle > 10f)
            {
                return; // Không cho phép sạc khi độ nghiêng > 10
            }

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