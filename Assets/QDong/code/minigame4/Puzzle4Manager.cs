using UnityEngine;
using Unity.Netcode;

public class Puzzle4Manager : NetworkBehaviour
{
    public EnergyColumn A;
    public EnergyColumn B;
    public EnergyColumn C;
    public EnergyColumn D;

    public BalanceManager balanceManager;

    public NetworkVariable<bool>
        puzzleCompleted =
        new NetworkVariable<bool>();

    public NetworkVariable<float>
        failTimer =
        new NetworkVariable<float>(3f);

    void Update()
    {
        if(!IsServer)
            return;

        CheckFail();
        CheckComplete();
        // Debug.Log(
        //     "PuzzleManager Running - Server = "
        //     + IsServer
        // );
    }

    void CheckFail()
    {
        if(balanceManager.CurrentAngle > 2)
        {
            failTimer.Value -= Time.deltaTime;

            if(failTimer.Value <= 0)
            {
                Debug.Log("FAIL");
            }
        }
        else
        {
            failTimer.Value = 3f;
        }
        // Debug.Log(
        //     "CurrentAngle = " +
        //     balanceManager.CurrentAngle
        // );
    }

    void CheckComplete()
    {
        // Debug.Log("A = " + A.charge.Value);
        // Debug.Log("B = " + B.charge.Value);
        // Debug.Log("C = " + C.charge.Value);
        // Debug.Log("D = " + D.charge.Value);
        if(
            A.IsCompleted() &&
            B.IsCompleted() &&
            C.IsCompleted() &&
            D.IsCompleted()
        )
        {
            Debug.Log("Puzzle 4 Complete");
            puzzleCompleted.Value = true;
        }
    }
}