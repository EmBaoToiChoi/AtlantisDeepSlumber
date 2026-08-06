using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class Puzzle4Manager : NetworkBehaviour
{
    //cuscene
    [Header("Cutscene Settings")]
    public VideoCutsceneController completionCutscene;
    public GameObject objectToEnableAfterCutscene;
    //
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
    private SlideEffect cachedLocalPlayerSlideEffect = null;
    private Collider[] cachedLocalPlayerColliders = null;
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

        // Tìm local player script của client này
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
        }

        if (isActive)
        {
            // Lấy sCam một lần để dùng cho owner
            Camera sCam = sharedCamera != null ? sharedCamera.GetComponent<Camera>() : null;
            if (sCam != null) sCam.enabled = true;

            foreach (var mono in allMonos)
            {
                if (mono is IPlayerHUDTarget player)
                {
                    // Tắt camera follow cho TẤT CẢ nhân vật (tránh giật camera)
                    var enableCamField = mono.GetType().GetField("enableCameraFollow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (enableCamField != null) enableCamField.SetValue(mono, false);

                    // Chỉ gán targetCamera = sharedCamera cho NHÂN VẬT CỦA CLIENT NÀY (IsOwner)
                    // Không gán cho nhân vật của người chơi khác (network copies) vì sẽ bị sai hướng
                    if (player.IsOwner)
                    {
                        var targetCamField = mono.GetType().GetField("targetCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (targetCamField != null)
                        {
                            targetCamField.SetValue(mono, sCam);
                        }
                    }
                }
            }
            
            // Tắt tất cả MainCamera đang bật (trừ sharedCamera)
            Camera[] allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera cam in allCams)
            {
                if (cam.CompareTag("MainCamera") && (sharedCamera == null || cam.gameObject != sharedCamera))
                {
                    cam.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // Bật lại MainCamera của Local Player (tránh bật nhầm Camera của người chơi khác trên Client)
            Camera mainCam = null;
            
            // Ưu tiên 1: Tìm Camera.main nằm trong người của Local Player
            if (localPlayerScript != null)
            {
                Camera[] localCams = localPlayerScript.GetComponentsInChildren<Camera>(true);
                foreach (Camera c in localCams)
                {
                    if (c.CompareTag("MainCamera"))
                    {
                        c.gameObject.SetActive(true);
                        mainCam = c;
                        break;
                    }
                }
            }
            
            // Ưu tiên 2: Nếu scene chỉ dùng 1 Camera chung, tìm cái đầu tiên và bật nó lên
            if (mainCam == null)
            {
                Camera[] allCams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Camera cam in allCams)
                {
                    if (sharedCamera != null && cam.gameObject == sharedCamera) continue;
                    
                    if (cam.CompareTag("MainCamera"))
                    {
                        cam.gameObject.SetActive(true);
                        mainCam = cam;
                        break; // Quan trọng: Không bật bừa bãi tất cả Camera!
                    }
                }
            }
            
            // Mở khóa enableCameraFollow và gán lại targetCamera cho TẤT CẢ nhân vật
            foreach (var mono in allMonos)
            {
                if (mono is IPlayerHUDTarget)
                {
                    var enableCamField = mono.GetType().GetField("enableCameraFollow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (enableCamField != null) enableCamField.SetValue(mono, true);
                    
                    // Chỉ gán camera cho local player (IsOwner)
                    if (((IPlayerHUDTarget)mono).IsOwner && mainCam != null)
                    {
                        var targetCamField = mono.GetType().GetField("targetCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (targetCamField != null) targetCamField.SetValue(mono, mainCam);
                    }
                }
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

        // Bắt buộc gọi Teleport của NetworkTransform trên client làm chủ (Owner)
        // để tránh bị hệ thống tự kéo giật ngược về vị trí cũ (ClientNetworkTransform)
        if (netObj.IsOwner)
        {
            Unity.Netcode.Components.NetworkTransform netTransform = netObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (netTransform != null)
            {
                netTransform.Teleport(pos, netObj.transform.rotation, netObj.transform.localScale);
            }
        }

        Physics.SyncTransforms();

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
            
            Debug.Log("[Puzzle4Manager] Bật UI minigame 4 ngay lập tức trên các Client");
            ToggleUIClientRpc(true);
            
            isStartingUI = false;
            isMinigameStarted.Value = true;
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
                
                // GIẢM ĐỘ NHẠY TRƯỢT ĐỂ DỄ CHƠI HƠN: 
                // Giảm lực cơ bản và hệ số nhân để người chơi có thể chạy ngược lên
                float slideForceMagn = 20f + (balanceManager.CurrentAngle * 15f); 
                
                // Giới hạn lực đẩy tối đa để không làm người chơi bị kẹt đứng im ngoài mép
                if (slideForceMagn > 180f) slideForceMagn = 180f;

                Vector3 slideForce = downhill * slideForceMagn;
                
                // Quan trọng: Bổ sung lực ép dính xuống mặt đĩa tỉ lệ thuận với lực trượt.
                // Ép mạnh hơn (x1.0 thay vì 0.8) vì lực trượt ngang đã mạnh hơn, tránh nảy văng.
                Vector3 stickyForce = -normal * (slideForceMagn * 1.0f);

                // Cache local players một lần thay vì FindObjectsByType mỗi frame
                if (!playersCached)
                {
                    CacheLocalPlayers();
                }

                if (playersCached && cachedDiskTransform != null)
                {
                    foreach (var playerData in cachedLocalPlayers)
                    {
                        if (playerData.transform == null) continue;
                        
                        // Thay thế Trigger vật lý bằng check khoảng cách (hoạt động chính xác dù 1 hay 4 người)
                        Vector3 localPos = cachedDiskTransform.InverseTransformPoint(playerData.transform.position);
                        float horizDist = new Vector2(localPos.x, localPos.z).magnitude;
                        bool isOnBoard = (localPos.y > -1.5f) && (localPos.y < 5f) && (horizDist < 12f);

                        if (isOnBoard)
                        {
                            if (playerData.rb != null)
                            {
                                playerData.rb.AddForce(slideForce + stickyForce, ForceMode.Force);
                            }
                            
                            if (playerData.slideEffect != null)
                            {
                                playerData.slideEffect.ApplySlide(slideForce);
                            }
                        }
                    }
                }
            }
        }
    }

    private Transform cachedDiskTransform;
    
    // Struct lưu thông tin cache cho từng nhân vật
    private struct PlayerCacheData
    {
        public Transform transform;
        public Rigidbody rb;
        public SlideEffect slideEffect;
    }
    
    private System.Collections.Generic.List<PlayerCacheData> cachedLocalPlayers = new System.Collections.Generic.List<PlayerCacheData>();
    private bool playersCached = false;

    private void CacheLocalPlayers()
    {
        cachedLocalPlayers.Clear();
        
        // Cache transform của đĩa để so sánh khoảng cách không phụ thuộc Trigger vật lý
        if (balanceManager != null)
            cachedDiskTransform = balanceManager.diskRigidbody != null ? balanceManager.diskRigidbody.transform : balanceManager.transform;

        var allMonos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mono in allMonos)
        {
            // Cache TẤT CẢ nhân vật mà Client này sở hữu (hoặc nếu là host test thì sẽ add hết cả 4)
            if (mono is IPlayerHUDTarget player && player.IsOwner)
            {
                PlayerCacheData data = new PlayerCacheData
                {
                    transform = player.transform,
                    rb = player.gameObject.GetComponent<Rigidbody>(),
                    slideEffect = player.gameObject.GetComponent<SlideEffect>()
                };
                cachedLocalPlayers.Add(data);
                Debug.Log("[Puzzle4Manager] Đã cache local player để trượt: " + player.gameObject.name);
            }
        }
        
        playersCached = true;
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

            if (IsServer)
        {
            StartCoroutine(RunCutsceneAndEnableObject());
        }
    }
    // ==========================================
    // [CÁC HÀM THÊM MỚI] Xử lý Cutscene & Bật Object
    // ==========================================
    private IEnumerator RunCutsceneAndEnableObject()
    {
        if (completionCutscene != null)
        {
            // Kích hoạt cutscene
            completionCutscene.StartCutscene();
            
            // Đợi 1 giây để hệ thống video setup và biến isPlaying chuyển thành true
            yield return new WaitForSeconds(1f);
            
            // Tạm dừng logic ở đây cho đến khi video chạy xong (isPlaying quay về false)
            yield return new WaitUntil(() => !completionCutscene.isPlaying);
        }

        // Khi video xong, gọi ClientRpc để bật object trên toàn bộ Client
        EnableRewardObjectClientRpc();
    }

    [ClientRpc]
    private void EnableRewardObjectClientRpc()
    {
        if (objectToEnableAfterCutscene != null)
        {
            objectToEnableAfterCutscene.SetActive(true);
            Debug.Log("[Puzzle4Manager] Cutscene hoàn tất - Đã bật Object thành công.");
        }
    }

}