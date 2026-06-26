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

        CheckComplete();
    }

    void FixedUpdate()
    {
        if (balanceManager != null && balanceManager.diskRigidbody != null)
        {
            if (balanceManager.CurrentAngle > 10f)
            {
                Vector3 normal = balanceManager.diskRigidbody.transform.up;
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
                
                float slideForceMagn = (balanceManager.CurrentAngle - 10f) * 60f; 
                
                // Giới hạn lực đẩy tối đa để tránh lỗi vật lý (PhysX nảy văng) khi ép mạnh vào thành đĩa
                if (slideForceMagn > 300f) slideForceMagn = 300f;

                Vector3 slideForce = downhill * slideForceMagn;
                
                // Quan trọng: Bổ sung lực ép dính xuống mặt đĩa tỉ lệ thuận với lực trượt.
                // Khi trượt mạnh đụng vào viền đĩa (rim), nếu không có lực ép xuống, nhân vật sẽ bị bật tung lên!
                Vector3 stickyForce = -normal * (slideForceMagn * 0.8f);

                CharacterInfo[] players = FindObjectsByType<CharacterInfo>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    if (player.IsOwner) 
                    {
                        Collider col = player.GetComponent<Collider>();
                        if (col != null && balanceManager.IsPlayerOnBoard(col))
                        {
                            Rigidbody rb = player.GetComponent<Rigidbody>();
                            if (rb != null)
                            {
                                // Áp dụng cả lực trượt và lực dính
                                rb.AddForce(slideForce + stickyForce, ForceMode.Force);
                            }
                        }
                    }
                }
            }
        }
    }

    IEnumerator DelayedStartMinigameUI()
    {
        yield return new WaitForSeconds(2.5f);
        isMinigameStarted.Value = true;
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