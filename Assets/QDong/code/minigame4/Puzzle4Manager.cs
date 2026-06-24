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

    public NetworkVariable<bool> isMinigameStarted =
        new NetworkVariable<bool>(false);

    public GameObject[] minigameUIs;

    public NetworkVariable<float> failTimer =
        new NetworkVariable<float>(3f);

    public TMP_Text countdownText;

    private bool hasFailed = false;

    public GameObject completeUI;
    public GameObject castleGate;
    public GameObject trapTrigger;

    private bool hasCompleted = false;

    // Sau này đổi sang VFX Graph
    public GameObject beamA;
    public GameObject beamB;
    public GameObject beamC;
    public GameObject beamD;

    public GameObject centerExplosion;

    // Trap Floor là khi hoàn thành sẽ mở ra
    public Puzzle4TrapTrigger trapFloor;

    void Start()
    {
        if(beamA != null) beamA.SetActive(false);
        if(beamB != null) beamB.SetActive(false);
        if(beamC != null) beamC.SetActive(false);
        if(beamD != null) beamD.SetActive(false);

        if(centerExplosion != null)
            centerExplosion.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsClient)
        {
            UpdateUIState(isMinigameStarted.Value);
            isMinigameStarted.OnValueChanged += (oldVal, newVal) => {
                UpdateUIState(newVal);
            };
        }
    }

    void UpdateUIState(bool show)
    {
        if (minigameUIs != null && minigameUIs.Length > 0)
        {
            foreach (GameObject ui in minigameUIs)
            {
                if (ui != null)
                    ui.SetActive(show);
            }
        }
        else
        {
            BalanceMeterUIToolkit tkUI = FindAnyObjectByType<BalanceMeterUIToolkit>(FindObjectsInactive.Include);
            if (tkUI != null) tkUI.gameObject.SetActive(show);

            BalanceMeterUI imgUI = FindAnyObjectByType<BalanceMeterUI>(FindObjectsInactive.Include);
            if (imgUI != null) imgUI.gameObject.SetActive(show);

            Puzzle4UI p4UI = FindAnyObjectByType<Puzzle4UI>(FindObjectsInactive.Include);
            if (p4UI != null) p4UI.gameObject.SetActive(show);
        }
    }

    private bool isStartingUI = false;

    void Update()
    {
        if (!IsServer)
            return;

        if (trapFloor != null && trapFloor.activated && !isMinigameStarted.Value && !isStartingUI)
        {
            isStartingUI = true;
            StartCoroutine(DelayedStartMinigameUI());
        }

        CheckFail();
        CheckComplete();
    }

    IEnumerator DelayedStartMinigameUI()
    {
        yield return new WaitForSeconds(2.5f);
        isMinigameStarted.Value = true;
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
        if (
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

        LaunchPlayersOutward();

        yield return new WaitForSeconds(3f);

        ResetPuzzle();
    }

    void LaunchPlayersOutward()
    {
        CharacterInfo[] players =
            FindObjectsByType<CharacterInfo>(
                FindObjectsSortMode.None
            );

        foreach (var player in players)
        {
            PlayerKnockback knockback =
                player.GetComponent<PlayerKnockback>();

            if (knockback == null)
                continue;

            Vector3 center =
                balanceManager.diskRigidbody.transform.position;

            Vector3 direction =
                (player.transform.position - center).normalized;

            direction.y = 0f;

            direction.Normalize();

            Vector3 force =
                Vector3.up * 3f;

            knockback.Launch(force);
        }
    }

    void LaunchPlayersUp()
    {
        CharacterInfo[] players =
            FindObjectsByType<CharacterInfo>(
                FindObjectsSortMode.None
            );

        foreach (var player in players)
        {
            PlayerKnockback knockback =
                player.GetComponent<PlayerKnockback>();

            if (knockback == null)
                continue;

            Vector3 force =
                Vector3.up * 15f;

            knockback.Launch(force);
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
        yield return new WaitForSeconds(1f);

        if(beamA != null) beamA.SetActive(true);
        if(beamB != null) beamB.SetActive(true);
        if(beamC != null) beamC.SetActive(true);
        if(beamD != null) beamD.SetActive(true);

        yield return new WaitForSeconds(2f);

        if(centerExplosion != null)
            centerExplosion.SetActive(true);

        yield return new WaitForSeconds(1f);

        yield return new WaitForSeconds(7f);

        LaunchPlayersUp();

        // Chờ người chơi bay lên khỏi mặt đất một chút
        yield return new WaitForSeconds(0.001f);

        // Đóng mặt đường (hộp) lại để người chơi không bị rớt xuống
        if(trapFloor != null)
        {
            trapFloor.CloseFloorClientRpc();
        }

        yield return new WaitForSeconds(1f);

        if(castleGate != null)
            castleGate.SetActive(false);

        // Tắt vùng box trigger để không bị kích hoạt lại
        if(trapTrigger != null)
        {
            // Kiểm tra xem trapTrigger có cùng object với trapFloor không
            // Nếu cùng object, việc SetActive(false) sẽ làm ẩn luôn cả mặt đất (floorPart)
            if (trapFloor != null && trapTrigger == trapFloor.gameObject)
            {
                Collider col = trapTrigger.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                
                ZoneTrigger zone = trapTrigger.GetComponent<ZoneTrigger>();
                if (zone != null) zone.enabled = false;
            }
            else
            {
                trapTrigger.SetActive(false);
            }
        }

        if(beamA != null) beamA.SetActive(false);
        if(beamB != null) beamB.SetActive(false);
        if(beamC != null) beamC.SetActive(false);
        if(beamD != null) beamD.SetActive(false);

        if(centerExplosion != null)
            centerExplosion.SetActive(false);
    }

}