using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class Puzzle4Manager : NetworkBehaviour
{
    public EnergyColumn A;
    public EnergyColumn B;
    public EnergyColumn C;
    public EnergyColumn D;

    public BalanceManager balanceManager;

    public NetworkVariable<bool> puzzleCompleted =
        new NetworkVariable<bool>();

    public NetworkVariable<float> failTimer =
        new NetworkVariable<float>(3f);

    public TMP_Text countdownText;

    private bool hasFailed = false;

    public GameObject completeUI;
    public GameObject castleGate;
    public GameObject trapTrigger;

    private bool hasCompleted = false;

    public ParticleSystem centerExplosion;

    void Update()
    {
        if (!IsServer)
            return;

        CheckFail();
        CheckComplete();
    }

    void CheckFail()
    {
        if (balanceManager.CurrentAngle > 15)
        {
            failTimer.Value -= Time.deltaTime;

            if (countdownText != null)
            {
                countdownText.text =
                    Mathf.CeilToInt(
                        failTimer.Value
                    ).ToString();
            }

            if (failTimer.Value <= 0 && !hasFailed)
            {
                hasFailed = true;

                StartCoroutine(FailSequence());
            }
        }
        else
        {
            failTimer.Value = 3f;

            if (countdownText != null)
            {
                countdownText.text = "";
            }
        }
    }

    void CheckComplete()
    {
        if(
            !hasCompleted &&
            A.IsCompleted() &&
            B.IsCompleted() &&
            C.IsCompleted() &&
            D.IsCompleted()
        )
        {
            hasCompleted = true;

            puzzleCompleted.Value = true;

            StartCoroutine(
                balanceManager.ReturnToCenterAndLock()
            );

            StartCoroutine(
                CompleteSequence()
            );

            Debug.Log(
                "Puzzle 4 Complete"
            );
        }
    }

    IEnumerator FailSequence()
    {
        balanceManager.FlipDisk();

        yield return new WaitForSeconds(0.4f);

        LaunchAllPlayers();

        yield return new WaitForSeconds(3f);

        ResetPuzzle();
    }

    void LaunchAllPlayers()
    {
        CharacterInfo[] players =
            FindObjectsByType<CharacterInfo>(
                FindObjectsSortMode.None
            );

        foreach (var player in players)
        {
            PlayerKnockback knockback =
                player.GetComponent<PlayerKnockback>();

            if (knockback != null)
            {
                // Vector3 force =
                //     Vector3.up * 15f +
                //     player.transform.forward * 8f;

                Vector3 center =
                    balanceManager.diskRigidbody.transform.position;

                Vector3 direction =
                    player.transform.position - center;

                direction.y = 0f;

                direction.Normalize();

                Vector3 force =
                    direction * 150f +
                    Vector3.up * 35f;

                Debug.Log("Player Pos = " + player.transform.position);
                Debug.Log("Disk Pos = " + balanceManager.diskRigidbody.transform.position);
                Debug.Log("Direction = " + direction);
                Debug.Log("Force = " + force);

                knockback.Launch(force);
            }
        }
    }
    void ResetPuzzle()
    {
        A.charge.Value = 0;
        B.charge.Value = 0;
        C.charge.Value = 0;
        D.charge.Value = 0;

        failTimer.Value = 3f;

        hasFailed = false;

        balanceManager.ResetDisk();
    }

    IEnumerator CompleteSequence()
    {
        // đợi tia điện hội tụ

        yield return new WaitForSeconds(1f);

        if(centerExplosion != null)
        {
            centerExplosion.Play();
        }

        yield return new WaitForSeconds(0.5f);

        completeUI.SetActive(true);

        yield return new WaitForSeconds(3f);

        LaunchAllPlayers();

        completeUI.SetActive(false);

        castleGate.SetActive(false);

        trapTrigger.SetActive(false);
    }
}