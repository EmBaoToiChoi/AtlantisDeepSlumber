using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class Puzzle4Manager : NetworkBehaviour
{
    public EnergyColumn A;
    public EnergyColumn B;
    public EnergyColumn C;

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

    // Cache local player để tránh FindObjectsByType mỗi frame trong FixedUpdate
    private Rigidbody cachedLocalPlayerRb = null;
    private Collider cachedLocalPlayerCollider = null;
    private bool localPlayerCached = false;

    // Sau này đổi sang VFX Graph
    public GameObject beamA;
    public GameObject beamB;
    public GameObject beamC;

    public GameObject centerExplosion;

    // Trap Floor là khi hoàn thành sẽ mở ra
    public Puzzle4TrapTrigger trapFloor;

    [Header("Camera Toàn Cảnh")]
    public GameObject sharedCamera;

    [Header("Camera Tracking")]
    public float cameraFollowAmount = 0.2f;
    public float cameraFollowSpeed = 3f;
    private Vector3 initialCameraPos;
    private Transform localPlayerTransform;

    [ClientRpc]
    public void ToggleSharedCameraClientRpc(bool isActive, ClientRpcParams rpcParams = default)
    {
        if (sharedCamera != null)
        {
            sharedCamera.SetActive(isActive);
        }

        // Khi tắt camera minigame, đảm bảo Camera.main của player được bật lại
        if (!isActive)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null && !mainCam.gameObject.activeSelf)
            {
                mainCam.gameObject.SetActive(true);
                Debug.Log("[Puzzle4] Đã bật lại Camera.main: " + mainCam.gameObject.name);
            }
            else if (mainCam != null)
            {
                Debug.Log("[Puzzle4] Camera.main đang active: " + mainCam.gameObject.name + " | Depth: " + mainCam.depth);
            }
            else
            {
                Debug.LogWarning("[Puzzle4] Không tìm thấy Camera.main!");
            }

            if (sharedCamera != null)
            {
                Debug.Log("[Puzzle4] sharedCamera đã tắt: " + sharedCamera.name);
            }
        }
    }

    [ClientRpc]
    public void TeleportPlayerToCenterClientRpc(ulong objectId, Vector3 pos)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
            return;
        
        netObj.transform.position = pos;
        Rigidbody rb = netObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }

    void Start()
    {
        if (sharedCamera != null)
        {
            initialCameraPos = sharedCamera.transform.position;
        }
        if(beamA != null) beamA.SetActive(false);
        if(beamB != null) beamB.SetActive(false);
        if(beamC != null) beamC.SetActive(false);

        if(centerExplosion != null)
            centerExplosion.SetActive(false);

        if (balanceManager != null)
        {
            balanceManager.LockDisk();
        }
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

            if (puzzleCompleted.Value)
            {
                if (completeUI != null) completeUI.SetActive(true);
                if (sharedCamera != null) sharedCamera.SetActive(false);
            }
            puzzleCompleted.OnValueChanged += (oldVal, newVal) => {
                if (newVal)
                {
                    if (completeUI != null) completeUI.SetActive(true);
                    if (sharedCamera != null) sharedCamera.SetActive(false);
                }
            };
        }
    }

    void UpdateUIState(bool show)
    {
        Debug.Log($"[Puzzle4Manager] Gọi UpdateUIState({show})");
        bool uiFound = false;

        if (minigameUIs != null && minigameUIs.Length > 0)
        {
            foreach (GameObject ui in minigameUIs)
            {
                if (ui != null)
                {
                    ui.SetActive(show);
                    uiFound = true;
                    Debug.Log($"[Puzzle4Manager] Đã {(show ? "BẬT" : "TẮT")} UI từ mảng minigameUIs: {ui.name}");
                }
            }
        }
        
        // Nếu mảng rỗng HOẶC mảng có phần tử nhưng toàn null (bị mất tham chiếu)
        if (!uiFound)
        {
            Debug.Log("[Puzzle4Manager] Không có UI hợp lệ trong mảng minigameUIs, chuyển sang tìm tự động...");
            
            Puzzle4UIToolkit tkUI = FindAnyObjectByType<Puzzle4UIToolkit>(FindObjectsInactive.Include);
            if (tkUI != null)
            {
                if (show) tkUI.ShowPanel();
                else tkUI.HidePanel();
                Debug.Log("[Puzzle4Manager] Đã tự động tìm và cập nhật Puzzle4UIToolkit.");
            }

            BalanceMeterUI imgUI = FindAnyObjectByType<BalanceMeterUI>(FindObjectsInactive.Include);
            if (imgUI != null) 
            {
                imgUI.gameObject.SetActive(show);
                Debug.Log("[Puzzle4Manager] Đã tự động tìm và cập nhật BalanceMeterUI.");
            }

            Puzzle4UI p4UI = FindAnyObjectByType<Puzzle4UI>(FindObjectsInactive.Include);
            if (p4UI != null) 
            {
                p4UI.gameObject.SetActive(show);
                Debug.Log("[Puzzle4Manager] Đã tự động tìm và cập nhật Puzzle4UI.");
            }
        }
    }

    private bool isStartingUI = false;

    void Update()
    {
        if (!IsServer)
            return;

        // Chỉ chạy CheckComplete khi minigame đang thực sự bắt đầu (sau khi tele + timeline xong)
        if (!isMinigameStarted.Value)
            return;

        CheckComplete();
    }

    public void ScheduleMinigameStart(float delay)
    {
        StartCoroutine(ScheduledStartCoroutine(delay));
    }

    private IEnumerator ScheduledStartCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log("[Puzzle4Manager] Đã hết thời gian chờ Timeline, bắt đầu kích hoạt Minigame!");
        StartMinigameFromTeleport();
    }

    public void StartMinigameFromTeleport()
    {
        if (!IsServer) 
        {
            Debug.Log("[Puzzle4Manager] StartMinigame bị từ chối vì không phải Server.");
            return;
        }
        
        Debug.Log($"[Puzzle4Manager] Gọi StartMinigame! isMinigameStarted: {isMinigameStarted.Value}, isStartingUI: {isStartingUI}");

        if (!isMinigameStarted.Value && !isStartingUI)
        {
            isStartingUI = true;
            if (balanceManager != null)
            {
                Debug.Log("[Puzzle4Manager] UnlockDisk()");
                balanceManager.UnlockDisk();
            }
            else 
            {
                Debug.LogWarning("[Puzzle4Manager] LỖI: balanceManager bị null!");
            }
            
            Debug.Log("[Puzzle4Manager] Gọi ToggleSharedCameraClientRpc(true)");
            ToggleSharedCameraClientRpc(true);
            
            Debug.Log("[Puzzle4Manager] Chạy Coroutine DelayedStartMinigameUI()");
            StartCoroutine(DelayedStartMinigameUI());
        }
    }

    void FixedUpdate()
    {
        // Khi hoàn thành puzzle, dừng mọi lực tác động lên player
        if (hasCompleted) return;

        // Chỉ áp lực trượt khi minigame đang chạy
        if (!isMinigameStarted.Value) return;

        if (balanceManager != null && balanceManager.diskRigidbody != null)
        {
            if (balanceManager.CurrentAngle > 1f) // Đã bắt đầu trượt ngay khi nghiêng nhẹ
            {
                Vector3 normal = balanceManager.diskRigidbody.transform.up;
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
                
                // Lực trượt tỉ lệ với độ nghiêng (Nghiêng 5 độ = lực 100, 10 độ = 200, 15 độ = max 300)
                float slideForceMagn = balanceManager.CurrentAngle * 20f; 
                
                // Giới hạn lực đẩy tối đa để tránh lỗi vật lý (PhysX nảy văng) khi ép mạnh vào thành đĩa
                if (slideForceMagn > 300f) slideForceMagn = 300f;

                Vector3 slideForce = downhill * slideForceMagn;
                
                // Quan trọng: Bổ sung lực ép dính xuống mặt đĩa tỉ lệ thuận với lực trượt.
                // Khi trượt mạnh đụng vào viền đĩa (rim), nếu không có lực ép xuống, nhân vật sẽ bị bật tung lên!
                Vector3 stickyForce = -normal * (slideForceMagn * 0.8f);

                // Cache local player một lần thay vì FindObjectsByType mỗi frame
                if (!localPlayerCached)
                {
                    CacheLocalPlayer();
                }

                if (cachedLocalPlayerRb != null)
                {
                    Collider[] cols = cachedLocalPlayerRb.GetComponentsInChildren<Collider>();
                    bool isOnBoard = false;
                    foreach (var c in cols)
                    {
                        if (balanceManager.IsPlayerOnBoard(c))
                        {
                            isOnBoard = true;
                            break;
                        }
                    }

                    if (isOnBoard)
                    {
                        cachedLocalPlayerRb.AddForce(slideForce + stickyForce, ForceMode.Force);
                    }
                }
            }
        }
    }

    private void CacheLocalPlayer()
    {
        var allMonos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mono in allMonos)
        {
            if (mono is IPlayerHUDTarget player && player.IsOwner)
            {
                cachedLocalPlayerRb = player.gameObject.GetComponent<Rigidbody>();
                localPlayerCached = (cachedLocalPlayerRb != null);
                if (localPlayerCached)
                    Debug.Log("[Puzzle4Manager] Đã cache local player: " + player.gameObject.name);
                break;
            }
        }
    }

    void LateUpdate()
    {
        if (sharedCamera != null && sharedCamera.activeSelf)
        {
            if (localPlayerTransform == null)
            {
                var allMonos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var mono in allMonos)
                {
                    if (mono is IPlayerHUDTarget player && player.IsOwner)
                    {
                        localPlayerTransform = player.transform;
                        break;
                    }
                }
            }

            if (localPlayerTransform != null && balanceManager != null)
            {
                Vector3 playerOffset = localPlayerTransform.position - balanceManager.transform.position;
                Vector3 targetCamPos = initialCameraPos + new Vector3(playerOffset.x * cameraFollowAmount, playerOffset.y * cameraFollowAmount, playerOffset.z * cameraFollowAmount);
                sharedCamera.transform.position = Vector3.Lerp(sharedCamera.transform.position, targetCamPos, Time.deltaTime * cameraFollowSpeed);
            }
        }
    }

    IEnumerator DelayedStartMinigameUI()
    {
        yield return null; // Hiển thị liền luôn
        isMinigameStarted.Value = true;
    }



    void CheckComplete()
    {
        // Guard: nếu bất kỳ cột nào bị null thì bỏ qua, tránh crash
        if (A == null || B == null || C == null)
        {
            Debug.LogWarning("[Puzzle4Manager] Một hoặc nhiều EnergyColumn (A/B/C) chưa được gán trong Inspector!");
            return;
        }

        if (
            !hasCompleted &&
            A.IsCompleted() &&
            B.IsCompleted() &&
            C.IsCompleted()
        )
        {
            hasCompleted = true;

            // Tắt camera và UI minigame ngay lập tức 
            ToggleSharedCameraClientRpc(false);
            isMinigameStarted.Value = false;

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







    IEnumerator CompleteSequence()
    {
        yield return new WaitForSeconds(0.5f);

        if(beamA != null) beamA.SetActive(true);
        if(beamB != null) beamB.SetActive(true);
        if(beamC != null) beamC.SetActive(true);

        yield return new WaitForSeconds(0.5f);

        if(centerExplosion != null)
            centerExplosion.SetActive(true);

        yield return new WaitForSeconds(1f);

        ToggleSharedCameraClientRpc(false);

        yield return new WaitForSeconds(0.5f);

        if(castleGate != null)
            castleGate.SetActive(false);

        // Tự động đóng mặt đất lại (bật lại sàn) khi hoàn thành
        if (trapFloor != null)
        {
            trapFloor.CloseFloorClientRpc();
        }

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

        if(centerExplosion != null)
            centerExplosion.SetActive(false);
    }

}