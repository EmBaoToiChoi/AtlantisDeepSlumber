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

    [Header("Environment Feedback")]
    public EnvironmentFeedback environmentFeedback;

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

        // Tự động tìm Player và vô hiệu hóa xoay camera thông qua Reflection để không chạm vào code gốc
        MonoBehaviour localPlayerScript = null;
        var allMonos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mono in allMonos)
        {
            if (mono is IPlayerHUDTarget player && player.IsOwner)
            {
                localPlayerScript = mono;
                break;
            }
        }

        if (localPlayerScript != null)
        {
            System.Type type = localPlayerScript.GetType();
            
            // Khóa/Mở khóa xoay camera (chuột)
            var enableCamField = type.GetField("enableCameraFollow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (enableCamField != null)
            {
                enableCamField.SetValue(localPlayerScript, !isActive);
            }

            // Gán lại camera mục tiêu để WASD di chuyển chuẩn xác theo góc nhìn
            var targetCamField = type.GetField("targetCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (targetCamField != null)
            {
                if (isActive && sharedCamera != null)
                {
                    targetCamField.SetValue(localPlayerScript, sharedCamera.GetComponent<Camera>());
                }
                else
                {
                    // Trả về Camera.main hoặc null để tự tìm
                    targetCamField.SetValue(localPlayerScript, null);
                }
            }
        }

        if (isActive)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.gameObject.SetActive(false);
            }
        }
        else
        {
            // Khi tắt camera minigame, đảm bảo Camera.main của player được bật lại
            Camera mainCam = Camera.main;
            if (mainCam != null && !mainCam.gameObject.activeSelf)
            {
                mainCam.gameObject.SetActive(true);
                Debug.Log("[Puzzle4] Đã bật lại Camera.main: " + mainCam.gameObject.name);
            }
            else if (mainCam == null)
            {
                // Nếu Camera.main trả về null do đã bị tắt, tìm thủ công tất cả Camera
                Camera[] allCams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Camera cam in allCams)
                {
                    if (cam.CompareTag("MainCamera") && cam.gameObject != sharedCamera)
                    {
                        cam.gameObject.SetActive(true);
                        Debug.Log("[Puzzle4] Đã tìm và bật lại MainCamera: " + cam.gameObject.name);
                        
                        if (localPlayerScript != null)
                        {
                            var targetCamField = localPlayerScript.GetType().GetField("targetCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (targetCamField != null) targetCamField.SetValue(localPlayerScript, cam);
                        }
                        break;
                    }
                }
            }
            else
            {
                Debug.Log("[Puzzle4] Camera.main đang active: " + mainCam.gameObject.name + " | Depth: " + mainCam.depth);
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

        // Disable CharacterController tạm thời để set position không bị block
        CharacterController cc = netObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        netObj.transform.position = pos;

        if (cc != null) cc.enabled = true;

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
        
        // Không dùng if (!uiFound) nữa, luôn luôn tìm và cập nhật UI bằng code
        // để đề phòng trường hợp mảng minigameUIs có phần tử nhưng không chứa UI Toolkit.
        Debug.Log("[Puzzle4Manager] Tìm tự động các UI script...");
            
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

    [ClientRpc]
    public void ToggleUIClientRpc(bool show)
    {
        UpdateUIState(show);
    }

    IEnumerator DelayedStartMinigameUI()
    {
        yield return new WaitForSeconds(4.5f);
        
        Debug.Log("[Puzzle4Manager] Bật UI minigame 4 trên các Client thông qua ClientRpc");
        ToggleUIClientRpc(true);
        
        isStartingUI = false;
        isMinigameStarted.Value = true;
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

            balanceManager.TriggerReturnToCenterAndLock();

            TriggerCompleteSequence();

            Debug.Log(
                "Puzzle 4 Complete"
            );
        }
    }







    public void TriggerCompleteSequence()
    {
        if (!IsServer) return;

        // Gọi CloseFloorClientRpc() trực tiếp từ server (KHÔNG được gọi ClientRpc từ bên trong ClientRpc/coroutine)
        if (trapFloor != null)
        {
            Debug.Log("[Puzzle4Manager] Gọi CloseFloorClientRpc từ server...");
            trapFloor.CloseFloorClientRpc();
        }
        else
        {
            Debug.LogError("[Puzzle4Manager] trapFloor là NULL! Tự tìm Puzzle4TrapTrigger...");
            // Fallback: tự tìm trong scene
            Puzzle4TrapTrigger found = FindAnyObjectByType<Puzzle4TrapTrigger>();
            if (found != null)
            {
                Debug.Log("[Puzzle4Manager] Tìm thấy Puzzle4TrapTrigger fallback: " + found.gameObject.name);
                trapFloor = found;
                trapFloor.CloseFloorClientRpc();
            }
            else
            {
                Debug.LogError("[Puzzle4Manager] KHÔNG TÌM THẤY Puzzle4TrapTrigger trong scene! Sàn sẽ không hiện.");
            }
        }

        PlayCompleteSequenceClientRpc();
    }

    [ClientRpc]
    private void PlayCompleteSequenceClientRpc()
    {
        StartCoroutine(CompleteSequenceCoroutine());
    }

    IEnumerator CompleteSequenceCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        if(beamA != null) beamA.SetActive(true);
        if(beamB != null) beamB.SetActive(true);
        if(beamC != null) beamC.SetActive(true);

        yield return new WaitForSeconds(0.5f);

        if(centerExplosion != null)
            centerExplosion.SetActive(true);

        yield return new WaitForSeconds(1f);

        if (IsServer) ToggleSharedCameraClientRpc(false);

        yield return new WaitForSeconds(0.5f);

        if(castleGate != null)
            castleGate.SetActive(false);

        // Fallback client-side: nếu server đã gọi CloseFloorClientRpc nhưng sàn vẫn chưa hiện,
        // thì client tự bật sàn lên (xảy ra khi trapFloor bị null trên server)
        Debug.Log("[Puzzle4Manager] Client-side: Đang thử bật sàn...");
        Puzzle4TrapTrigger localTrapFloor = (trapFloor != null) ? trapFloor : FindAnyObjectByType<Puzzle4TrapTrigger>();
        if (localTrapFloor != null)
        {
            if (localTrapFloor.floorPartA != null) localTrapFloor.floorPartA.SetActive(true);
            if (localTrapFloor.floorPartB != null) localTrapFloor.floorPartB.SetActive(true);
            Debug.Log("[Puzzle4Manager] Client-side: Đã bật sàn thành công.");
        }
        else
        {
            Debug.LogError("[Puzzle4Manager] Client-side: KHÔNG TÌM THẤY Puzzle4TrapTrigger để bật sàn!");
        }

        // CloseFloorClientRpc() đã được gọi từ TriggerCompleteSequence() trên server trước khi coroutine này chạy

        // Bật lại vùng box trigger để nó hiện lại như yêu cầu,
        // nhưng TẮT Collider để không kích hoạt minigame nữa sau khi hoàn thành.
        if(trapTrigger != null)
        {
            trapTrigger.SetActive(true);

            // Tắt Collider: người chơi KHÔNG thể kích hoạt lại
            Collider triggerCol = trapTrigger.GetComponent<Collider>();
            if (triggerCol != null) triggerCol.enabled = false;

            // Đảm bảo Renderer bật (nhìn thấy được)
            Renderer triggerRend = trapTrigger.GetComponent<Renderer>();
            if (triggerRend != null) triggerRend.enabled = true;

            // Thông báo tất cả client bật lại mesh (không cần Collider)
            Puzzle4TeleportTrigger teleportTrigger = trapTrigger.GetComponent<Puzzle4TeleportTrigger>();
            if (teleportTrigger != null)
            {
                teleportTrigger.ReappearClientRpc();
            }
        }

        // Tắt EnvironmentFeedback (particle + sound) khi hoàn thành
        if (environmentFeedback != null)
        {
            environmentFeedback.StopAll();
        }
        else
        {
            // Tự tìm nếu chưa gán
            EnvironmentFeedback ef = FindAnyObjectByType<EnvironmentFeedback>();
            if (ef != null) ef.StopAll();
        }

        if(beamA != null) beamA.SetActive(false);
        if(beamB != null) beamB.SetActive(false);
        if(beamC != null) beamC.SetActive(false);

        if(centerExplosion != null)
            centerExplosion.SetActive(false);
    }

}