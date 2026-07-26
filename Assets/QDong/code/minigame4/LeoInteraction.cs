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

        // Chỉ cho sạc khi minigame đã bắt đầu
        Puzzle4Manager p4Manager = FindAnyObjectByType<Puzzle4Manager>();
        if (p4Manager == null || !p4Manager.isMinigameStarted.Value || p4Manager.puzzleCompleted.Value)
        {
            currentColumn.ShowInteractUI(false);
            return;
        }

        currentColumn.ShowInteractUI(true);

        if(Input.GetKey(KeyCode.C))
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
        if (!IsOwner) return;

        IPlayerHUDTarget info = GetComponent<IPlayerHUDTarget>();
        if (info == null || info.CharacterClassIndex != 0) return;

        EnergyColumn column =
            other.GetComponent<EnergyColumn>();

        if(column != null)
            currentColumn = column;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        EnergyColumn column =
            other.GetComponent<EnergyColumn>();

        if(column != null && currentColumn == column)
        {
            currentColumn.ShowInteractUI(false);
            currentColumn = null;
        }
    }
}