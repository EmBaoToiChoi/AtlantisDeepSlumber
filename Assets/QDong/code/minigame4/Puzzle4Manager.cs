using UnityEngine;
using Unity.Netcode;
using TMPro;

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
    public TMP_Text countdownText;

    private bool hasFailed = false;

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
        if(balanceManager.CurrentAngle > 15)
        {
            failTimer.Value -= Time.deltaTime;

            countdownText.text =
                Mathf.CeilToInt(
                    failTimer.Value
                ).ToString();

            if(failTimer.Value <= 0 && !hasFailed)
            {
                hasFailed = true;

                Debug.Log("FAIL");

                balanceManager.FlipDisk();
            }
        }
        else
        {
            failTimer.Value = 3f;
        }
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