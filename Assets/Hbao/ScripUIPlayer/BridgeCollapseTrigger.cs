using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

public class BridgeCollapseTrigger : NetworkBehaviour, IQuestTrigger
{
    public bool IsQuestCompleted => IsBridgeRepaired();
    public bool IsQuestActive => IsBridgeCollapsed() && !IsBridgeRepaired();

    [Header("Quest Prerequisite Settings")]
    [Tooltip("Nhiệm vụ tiền đề bắt buộc phải hoàn thành trước khi nhiệm vụ này được hiển thị/kích hoạt")]
    public MonoBehaviour prerequisiteQuest;

    public bool IsPrerequisiteCompleted()
    {
        if (prerequisiteQuest == null) return true;
        if (prerequisiteQuest is IQuestTrigger quest) return quest.IsQuestCompleted;
        if (prerequisiteQuest is BridgeCollapseTrigger bridge) return bridge.IsBridgeRepaired();
        return true;
    }

    [Header("Cutscene Configuration")]
    [Tooltip("Kéo thả object chứa Video Player vào đây. Nếu để trống, cầu sẽ sập luôn.")]
    public UnityEngine.Video.VideoPlayer collapseVideo; 

    // [SỬA Ở ĐÂY]: Thêm biến để nhét cái UI Canvas chứa video vào
    [Tooltip("Kéo thả cái Canvas chứa Raw Image (video) vào đây để code tự bật/tắt")]
    public GameObject cutsceneUI; 

    [Header("Player Movement Scripts")]
    [Tooltip("Điền tên chính xác của 4 script di chuyển vào đây (VD: Player1Move, ArthurController, v.v.)")]
    public System.Collections.Generic.List<string> playerMovementScriptNames = new System.Collections.Generic.List<string>();

    [Tooltip("Kéo thả GameObject (ví dụ UI Canvas, quái, cảnh...) muốn TẮT khi chiếu phim, xem xong sẽ BẬT lại.")]
    public GameObject objectToHideDuringCutscene;
    
    private bool isCutscenePlaying = false;
    [Header("NavMesh Bridge Configuration")]
    [Tooltip("Kéo thả NavMeshObstacle chặn cầu vào đây (sẽ BẬT khi sập, TẮT khi sửa xong)")]
    public NavMeshObstacle bridgeObstacle;

    [Tooltip("Kéo thả OffMeshLink nối 2 bờ vào đây (sẽ TẮT khi sập, BẬT khi sửa xong)")]
    public OffMeshLink bridgeOffMeshLink;

    [Header("Bridge Components Configuration")]
    [Tooltip("Các mảng cầu nguyên vẹn ban đầu (Sẽ bị tắt khi sập, bật lại khi sửa)")]
    public GameObject[] stableBridgeSegments;

    [Tooltip("Các mảng cầu sập/gãy (Sẽ được bật lên khi sập, tắt đi khi sửa)")]
    public GameObject[] brokenBridgeSegments;

    [Tooltip("Animator để chạy hoạt ảnh sập/sửa (nếu có)")]
    public Animator bridgeAnimator;
    public string collapseTriggerName = "Collapse";
    public string repairTriggerName = "Repair";

    [Tooltip("Cây cầu nguyên khối chính (Sẽ ẩn đi hoàn toàn khi sập, hiện lại khi được sửa đủ 16 gỗ)")]
    public GameObject mainBridgeObject;

    [Header("Quest Target Trees")]
    [Tooltip("Danh sách 4 cây gỗ nhiệm vụ tương ứng với 4 player (Leo=0, Maya=1, Elena=2, Arthur=3)")]
    public ChoppableTree[] targetTrees = new ChoppableTree[4];

    private Vector3 originalBridgePos;
    private Quaternion originalBridgeRot;
    private bool isGhostModeActive = false;
    private System.Collections.Generic.List<Renderer> savedRenderers = new System.Collections.Generic.List<Renderer>();
    private System.Collections.Generic.List<Material[]> savedMaterials = new System.Collections.Generic.List<Material[]>();
    private GameObject[] resolvedSegments;
    private GameObject solidBridgeInstance;
    private Vector3 originalLocalScale;
    private System.Collections.Generic.List<BoxCollider> cloneBoxColliders = new System.Collections.Generic.List<BoxCollider>();
    private System.Collections.Generic.List<Vector3> originalBoxSizes = new System.Collections.Generic.List<Vector3>();
    private System.Collections.Generic.List<Vector3> originalBoxCenters = new System.Collections.Generic.List<Vector3>();
    private System.Collections.Generic.List<Material> clippingMaterials = new System.Collections.Generic.List<Material>();
    private System.Collections.Generic.List<Material> ghostClippingMaterials = new System.Collections.Generic.List<Material>();

    [Header("Wood Quest Spawning Configuration")]
    [Tooltip("Prefab gỗ để người chơi thu thập (CollectibleItemDrop với itemName = 'WoodLog' hoặc 'ThanhGo')")]
    public GameObject woodLogPrefab;

    [Tooltip("Các vị trí spawn gỗ. Nếu bỏ trống sẽ tự động spawn ngẫu nhiên quanh trigger trên mặt đất.")]
    public Transform[] woodLogSpawnPoints;
    public float spawnRadius = 10f;
    public int logsToSpawn = 16;

    [Header("Quest & Interaction Settings")]
    public int requiredLogsToRepair = 16;
    public float repairInteractRadius = 4f;

    // Trạng thái mạng đồng bộ
    public NetworkVariable<bool> hasCollapsed = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> hasBeenRepaired = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> logsSubmitted = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> isReadyToBuild = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> buildProgress = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public enum BridgeDisplayMode { Auto, ForceSegmentBySegment, ForceScaleGrowth }

    [Header("Coop Build Config")]
    [Tooltip("Chế độ hiển thị tiến trình cầu:\n- Auto: Tự động phát hiện.\n- ForceSegmentBySegment: Hiện từng khúc/mảnh cầu.\n- ForceScaleGrowth: Kéo dài theo trục.")]
    public BridgeDisplayMode displayMode = BridgeDisplayMode.Auto;

    public float coopBuildDecayRate = 8f;
    public float soloBuildDecayRate = 0.5f;
    public float buildProgressPerClick = 1.5f;
    public ParticleSystem buildProgressParticles;

    [Header("Coop Build Visual & Audio Effects")]
    [Tooltip("Prefab búa để hiển thị hiệu ứng gõ khi xây cầu (Nếu trống sẽ dùng búa hình hộp tạm thời)")]
    public GameObject hammerPrefab;
    [Tooltip("Prefab cưa để hiển thị hiệu ứng cưa (Nếu trống sẽ dùng cưa hình hộp tạm thời)")]
    public GameObject sawPrefab;
    [Tooltip("Hiệu ứng bụi khói khi gõ/xây cầu (Nếu trống sẽ lấy từ buildProgressParticles)")]
    public ParticleSystem clickDustParticles;
    [Tooltip("Danh sách âm thanh búa gõ / cưa gỗ để phát ngẫu nhiên khi click")]
    public AudioClip[] buildAudioClips;

    [HideInInspector]
    public bool localReadyToBuild = false;
    [HideInInspector]
    public float localBuildProgress = 0f;

    private float lastProgress = 0f;
    private float particleStopTimer = 0f;

    private bool localCollapseTriggered = false;
    private bool localRepaired = false;
    private int localLogsSubmittedCount = 0;
    private IPlayerHUDTarget localPlayer;
    private PlayerHUDController hud;
    private float nextPlayerSearchTime = 0f;

    public bool IsBridgeCollapsed()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return hasCollapsed.Value;
        }
        return localCollapseTriggered;
    }

    public bool IsBridgeRepaired()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return hasBeenRepaired.Value;
        }
        return localRepaired;
    }

    public int GetLogsSubmittedCount()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return logsSubmitted.Value;
        }
        return localLogsSubmittedCount;
    }

    private void Awake()
    {
        // Fallback nếu mainBridgeObject bị bỏ trống trong Inspector
        if (mainBridgeObject == null && stableBridgeSegments != null && stableBridgeSegments.Length > 0)
        {
            mainBridgeObject = stableBridgeSegments[0];
            Debug.Log("[BridgeCollapseTrigger] Tự động gán mainBridgeObject bằng stableBridgeSegments[0] làm fallback!");
        }

        // Lưu lại vị trí, góc xoay ban đầu của cây cầu nguyên khối
        if (mainBridgeObject != null)
        {
            originalBridgePos = mainBridgeObject.transform.position;
            originalBridgeRot = mainBridgeObject.transform.rotation;
        }

        // --- ĐẢM BẢO ẨN HOÀN TOÀN CẦU LÚC KHỞI ĐẦU ---
        SetStableSegmentsActive(false);
        SetBrokenSegmentsActive(false);
        if (mainBridgeObject != null)
        {
            mainBridgeObject.SetActive(false);
        }

        // Tự động tìm kiếm nếu chưa được kéo thả trong Inspector
        if (bridgeObstacle == null)
        {
            bridgeObstacle = GetComponentInChildren<NavMeshObstacle>();
        }
        if (bridgeOffMeshLink == null)
        {
            bridgeOffMeshLink = GetComponentInChildren<OffMeshLink>();
        }

        // Mặc định lúc đầu chưa có cầu thì chặn đường đi (Obstacle bật, Link tắt)
        if (bridgeObstacle != null) bridgeObstacle.enabled = true;
        if (bridgeOffMeshLink != null) bridgeOffMeshLink.activated = false;
    }

    private void Start()
    {
        hud = FindAnyObjectByType<PlayerHUDController>();
        ResolveWoodLogPrefab();
        // Đảm bảo WoodLogObjectPool được khởi tạo sớm
        var pool = WoodLogObjectPool.Instance;
    }

    private void ResolveWoodLogPrefab()
    {
        if (woodLogPrefab == null || woodLogPrefab == gameObject || woodLogPrefab.GetComponent<BridgeCollapseTrigger>() != null)
        {
            Debug.LogWarning("[BridgeCollapseTrigger] Phát hiện woodLogPrefab chưa được cấu hình đúng. Đang tự động tìm kiếm...");
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null && NetworkManager.Singleton.NetworkConfig.Prefabs != null && NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs != null)
            {
                foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (networkPrefab.Prefab != null)
                    {
                        var col = networkPrefab.Prefab.GetComponent<CollectibleItemDrop>();
                        if (col != null && (col.itemName.Equals("wood_stack", System.StringComparison.OrdinalIgnoreCase) ||
                                            col.itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                                            col.itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase)))
                        {
                            woodLogPrefab = networkPrefab.Prefab;
                            Debug.Log($"[BridgeCollapseTrigger] Tự động sửa cấu hình woodLogPrefab thành công qua CollectibleItemDrop: {woodLogPrefab.name}");
                            return;
                        }
                    }
                }

                foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (networkPrefab.Prefab != null)
                    {
                        string pName = networkPrefab.Prefab.name.ToLower();
                        if (pName.Contains("wood_stack") || pName.Contains("firewood") || pName.Contains("woodlog") || pName.Contains("thanhgo"))
                        {
                            woodLogPrefab = networkPrefab.Prefab;
                            Debug.Log($"[BridgeCollapseTrigger] Tự động sửa cấu hình woodLogPrefab thành công qua tên prefab: {woodLogPrefab.name}");
                            return;
                        }
                    }
                }
            }

            GameObject loaded = Resources.Load<GameObject>("wood_stack");
            if (loaded == null)
            {
                loaded = Resources.Load<GameObject>("firewood_single");
            }
            if (loaded == null)
            {
                loaded = Resources.Load<GameObject>("WoodLog");
            }

            if (loaded != null)
            {
                woodLogPrefab = loaded;
                Debug.Log($"[BridgeCollapseTrigger] Tự động sửa cấu hình woodLogPrefab thành công từ Resources: {woodLogPrefab.name}");
                return;
            }

            Debug.LogError("[BridgeCollapseTrigger] Không thể tìm thấy WoodLog prefab hợp lệ!");
        }
    }

    public override void OnNetworkSpawn()
    {
        // Đồng bộ local với NetworkVariable ban đầu khi spawn trên mạng
        localCollapseTriggered = hasCollapsed.Value;
        localRepaired = hasBeenRepaired.Value;
        localLogsSubmittedCount = logsSubmitted.Value;

        // Đăng ký nhận sự kiện thay đổi trạng thái từ Server
        hasCollapsed.OnValueChanged += OnCollapseStateChanged;
        hasBeenRepaired.OnValueChanged += OnRepairStateChanged;
        logsSubmitted.OnValueChanged += OnLogsSubmittedChanged;
        isReadyToBuild.OnValueChanged += OnReadyToBuildChanged;

        // Cập nhật trạng thái hiển thị cho người vào trễ
        ApplyBridgeVisualState(hasCollapsed.Value, hasBeenRepaired.Value, logsSubmitted.Value);
    }

    public override void OnNetworkDespawn()
    {
        hasCollapsed.OnValueChanged -= OnCollapseStateChanged;
        hasBeenRepaired.OnValueChanged -= OnRepairStateChanged;
        logsSubmitted.OnValueChanged -= OnLogsSubmittedChanged;
        isReadyToBuild.OnValueChanged -= OnReadyToBuildChanged;
    }

    private void OnDestroy()
    {
        DestroySolidBridgeInstance();
    }

    private void OnReadyToBuildChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            if (IntroDialogueController.Instance != null)
            {
                IntroDialogueController.Instance.StartReadyToBuildDialogue();
            }
        }
        ApplyBridgeVisualState(IsBridgeCollapsed(), IsBridgeRepaired(), GetLogsSubmittedCount());
    }

    private void Update()
    {
        // 1. Tìm local player nếu chưa có
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // 1.5. Đảm bảo gắn chỉ đường cho local player nếu cầu sập và chưa sửa xong
        if (IsBridgeCollapsed() && !IsBridgeRepaired() && localPlayer != null)
        {
            var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
            if (indicator == null)
            {
                int classIndex = localPlayer.CharacterClassIndex;
                ChoppableTree myTree = ResolveTreeForClass(classIndex);
                bool isTreeCut = myTree != null && ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? myTree.isCutDown.Value : !myTree.gameObject.activeInHierarchy);
                if (myTree != null && !isTreeCut)
                {
                    indicator = localPlayer.gameObject.AddComponent<TreeGuidanceIndicator>();
                    indicator.targetTree = myTree;
                    indicator.reachDistance = 4f;
                    Debug.Log($"[BridgeCollapseTrigger] Gắn chỉ đường cho Local Player {localPlayer.DisplayName} tới cây: {myTree.name}");
                }
            }
        }

        // 2. Coop Building Progress and Decay logic
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool ready = isNetwork ? isReadyToBuild.Value : localReadyToBuild;
        float currentProgress = isNetwork ? buildProgress.Value : localBuildProgress;

        if (ready)
        {
            if (isNetwork && IsServer)
            {
                if (buildProgress.Value > 0f && buildProgress.Value < 100f)
                {
                    int playerCount = 1;
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    {
                        playerCount = NetworkManager.Singleton.ConnectedClients.Count;
                    }

                    float decayRate = coopBuildDecayRate;
                    if (playerCount == 1) decayRate = 0.5f; // Rất chậm, dễ thở cho 1 người chơi
                    else if (playerCount == 2) decayRate = 2.0f;
                    else if (playerCount == 3) decayRate = 4.0f;
                    else decayRate = 6.0f;

                    buildProgress.Value = Mathf.Clamp(buildProgress.Value - decayRate * Time.deltaTime, 0f, 100f);
                }
            }
            else if (!isNetwork)
            {
                if (localBuildProgress > 0f && localBuildProgress < 100f)
                {
                    localBuildProgress = Mathf.Clamp(localBuildProgress - soloBuildDecayRate * Time.deltaTime, 0f, 100f);
                }
            }

            // Hiển thị các mảnh cầu theo phần trăm tiến trình
            UpdateProgressiveBridgeSegments(currentProgress);

            // Emit particles when progress increases
            if (currentProgress > lastProgress)
            {
                SetParticlesActive(true);
                particleStopTimer = 0.5f; // keep playing for 0.5s after clicks stop
            }
            lastProgress = currentProgress;

            if (particleStopTimer > 0f)
            {
                particleStopTimer -= Time.deltaTime;
                if (particleStopTimer <= 0f)
                {
                    SetParticlesActive(false);
                }
            }

            // Periodically sync UI alert / quest to say "4 người các ngươi hãy lại đây ấn F và click liên tục để xây cầu"
            PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();
            if (localHudCtl != null)
            {
                localHudCtl.UpdateQuestDescription("Hãy lại gần cầu và nhấn Space để cùng nhau xây dựng");
                localHudCtl.UpdateQuestProgress((int)currentProgress, 100);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPrerequisiteCompleted()) return;

        // Kiểm tra sập cầu và chưa có video nào đang chạy
        if (!IsBridgeCollapsed() && !isCutscenePlaying)
        {
            if (IsPlayer(other.gameObject))
            {
                if (IsServer)
                {
                    Debug.Log($"[BridgeCollapseTrigger] Server nhận va chạm, bắt đầu chiếu Video.");
                    StartBridgeEventServer();
                }
                else
                {
                    RequestBridgeEventServerRpc();
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBridgeEventServerRpc()
    {
        if (!IsServer || IsBridgeCollapsed() || isCutscenePlaying || !IsPrerequisiteCompleted()) return;
        StartBridgeEventServer();
    }

    private void StartBridgeEventServer()
    {
        if (collapseVideo != null)
        {
            isCutscenePlaying = true;

            // 1. Giấu toàn bộ player khỏi quái vật bằng cách đổi Tag
            var activePlayers = PlayerHUDManager.ActivePlayers;
            foreach (var p in activePlayers)
            {
                if (p != null)
                {
                    MonoBehaviour playerMono = p as MonoBehaviour;
                    if (playerMono != null)
                    {
                        playerMono.gameObject.tag = "Untagged";
                    }
                }
            }

            // 2. TÌM VÀ ĐÓNG BĂNG TOÀN BỘ QUÁI VẬT TRÊN SERVER
            NavMeshAgent[] allEnemies = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach (var enemy in allEnemies)
            {
                if (enemy != null && enemy.isActiveAndEnabled)
                {
                    enemy.isStopped = true; // Bắt quái đứng im tại chỗ
                }
            }

            

            // 3. Gọi các Client bật VIDEO lên xem
            PlayCutsceneClientRpc();
            
            // Server bấm giờ bằng độ dài của video (Dùng lại hàm WaitForSeconds bình thường)
            StartCoroutine(WaitAndCollapseServer((float)collapseVideo.length));
        }
        else
        {
            TriggerBridgeCollapseServer();
        }
    }

    [ClientRpc]
    private void PlayCutsceneClientRpc()
    {
        if (objectToHideDuringCutscene != null)
        {
            objectToHideDuringCutscene.SetActive(false);
        }

        // Bật UI lên khi bắt đầu chiếu phim
        if (cutsceneUI != null) 
        {
            cutsceneUI.SetActive(true);
        }

        if (collapseVideo != null)
        {
            collapseVideo.Play();
        }

        // --- KHÓA DI CHUYỂN & PHANH GẤP ---
        if (localPlayer == null) FindLocalPlayer();
        if (localPlayer != null)
        {
            MonoBehaviour playerObj = localPlayer as MonoBehaviour;
            if (playerObj != null)
            {
                // 1. Tắt các script điều khiển
                MonoBehaviour[] allScripts = playerObj.GetComponents<MonoBehaviour>();
                foreach (var script in allScripts)
                {
                    if (script != null && playerMovementScriptNames.Contains(script.GetType().Name))
                    {
                        script.enabled = false;
                        Debug.Log($"[BridgeCollapseTrigger] Đã TẮT script di chuyển: {script.GetType().Name}");
                    }
                }

                // 2. [THÊM MỚI]: Thắng gấp Rigidbody (Xóa sạch lực quán tính)
                Rigidbody rb = playerObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Vì m đang xài Unity 6 nên dùng linearVelocity (nếu nó báo đỏ thì đổi thành velocity nha)
                    rb.linearVelocity = Vector3.zero; 
                    rb.angularVelocity = Vector3.zero;
                }

                // 3. [THÊM MỚI]: Thắng gấp NavMeshAgent (Nếu nhân vật xài NavMesh)
                UnityEngine.AI.NavMeshAgent agent = playerObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                {
                    agent.velocity = Vector3.zero;
                    agent.isStopped = true;
                }

                // 4. [THÊM MỚI]: Ép Animator dừng chạy (Moonwalk)
                Animator anim = playerObj.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    // Nếu game m xài parameter "Speed" hoặc "MoveSpeed" để chạy thì ép nó về 0 ở đây
                    // Ví dụ: anim.SetFloat("Speed", 0f);
                    anim.speed = 0f; // Tạm thời đóng băng luôn hoạt ảnh cho chắc cốp
                }
            }
        }
    }

    private System.Collections.IEnumerator WaitAndCollapseServer(float duration)
    {
        // Trả lại hàm chờ bình thường vì thời gian game vẫn trôi
        yield return new WaitForSeconds(duration);
        isCutscenePlaying = false;
        
        // 1. Phim hết, trả lại Tag "Player" để quái nhận diện lại mục tiêu
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null)
            {
                MonoBehaviour playerMono = p as MonoBehaviour;
                if (playerMono != null)
                {
                    playerMono.gameObject.tag = "Player";
                }
            }
        }

        // 2. MỞ KHÓA CHO TOÀN BỘ QUÁI CHẠY TIẾP
        NavMeshAgent[] allEnemies = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemies)
        {
            if (enemy != null && enemy.isActiveAndEnabled)
            {
                enemy.isStopped = false; 
            }
        }

        TriggerBridgeCollapseServer();
    }


    #region Collapse Logic

    private void TriggerBridgeCollapseServer()
    {
        if (!IsServer) return;
        
        hasCollapsed.Value = true;
        buildProgress.Value = 0f;
        hasBeenRepaired.Value = false;
        isReadyToBuild.Value = false;
        logsSubmitted.Value = 0;
        
        // Server thực hiện spawn gỗ xung quanh cầu
        SpawnWoodLogsServer();
        
        // Thông báo tất cả client hiển thị quest UI
        ShowCollapseUIClientRpc();
    }

[ClientRpc]
    private void ShowCollapseUIClientRpc()
    {
        // --- BẬT LẠI OBJECT KHI XEM PHIM XONG ---
        if (objectToHideDuringCutscene != null)
        {
            objectToHideDuringCutscene.SetActive(true);
        }

        // Tắt cái Canvas UI đi khi phim sập cầu chiếu xong
        if (cutsceneUI != null)
        {
            cutsceneUI.SetActive(false);
        }

        // 1. TẮT VIDEO
        if (collapseVideo != null)
        {
            collapseVideo.Stop(); 
        }

        // --- BẬT LẠI DI CHUYỂN & XẢ ĐÔNG NHÂN VẬT ---
        if (localPlayer == null) FindLocalPlayer();
        if (localPlayer != null)
        {
            MonoBehaviour playerObj = localPlayer as MonoBehaviour;
            if (playerObj != null)
            {
                // Bật lại các script điều khiển
                MonoBehaviour[] allScripts = playerObj.GetComponents<MonoBehaviour>();
                foreach (var script in allScripts)
                {
                    if (script != null && playerMovementScriptNames.Contains(script.GetType().Name))
                    {
                        script.enabled = true;
                        Debug.Log($"[BridgeCollapseTrigger] Đã BẬT LẠI script di chuyển: {script.GetType().Name}");
                    }
                }

                // [XẢ ĐÔNG 1]: Thả NavMeshAgent cho nó đi lại bình thường
                UnityEngine.AI.NavMeshAgent agent = playerObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                {
                    agent.isStopped = false;
                }

                // [XẢ ĐÔNG 2]: Trả lại tốc độ hoạt ảnh bình thường (Cái này làm m bị đơ nè)
                Animator anim = playerObj.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.speed = 1f; 
                }
            }
        }

        // 2. Hiện Quest UI
        PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
        if (localHud != null && IsPrerequisiteCompleted())
        {
            localHud.ShowQuest(true, this);
            localHud.UpdateQuestProgress(0, requiredLogsToRepair, this);
            localHud.ShowMissionAlert("CẦU ĐÃ BỊ SẬP! HÃY TÌM 16 THANH GỖ ĐỂ SỬA LẠI CẦU!", 5.0f);
        }
    }
    [ClientRpc]
    private void ShowRepairCompleteUIClientRpc()
    {
        PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
        if (localHud != null)
        {
            localHud.ShowInteractionPrompt(false, "");
            localHud.ShowQuest(false, this);
            localHud.ShowMissionAlert("CẦU ĐÃ ĐƯỢC SỬA CHỮA THÀNH CÔNG!", 5.0f);
        }
    }

    private void CollapseBridgeLocal()
    {
        localCollapseTriggered = true;
        localBuildProgress = 0f;
        localRepaired = false;
        localReadyToBuild = false;
        localLogsSubmittedCount = 0;
        DestroySolidBridgeInstance();

        if (bridgeObstacle != null)
        {
            bridgeObstacle.enabled = true;
        }
        if (bridgeOffMeshLink != null)
        {
            bridgeOffMeshLink.activated = false;
        }

        // Đảm bảo lưu lại vị trí ban đầu của cầu trước khi bị ẩn đi
        if (mainBridgeObject != null && originalBridgePos == Vector3.zero)
        {
            originalBridgePos = mainBridgeObject.transform.position;
            originalBridgeRot = mainBridgeObject.transform.rotation;
        }

        // --- ẨN HOÀN TOÀN CÁC PHÂN ĐOẠN (KHÔNG HIỂN THỊ MẢNH VỠ HAY GHOST LÚC SẬP BAN ĐẦU) ---
        SetStableSegmentsActive(false);
        SetBrokenSegmentsActive(false);
        if (mainBridgeObject != null)
        {
            mainBridgeObject.SetActive(false); // Ẩn hoàn toàn
        }

        // Tìm local player để thêm mũi tên hướng dẫn chỉ về cây gỗ nhiệm vụ tương ứng
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (localPlayer != null)
        {
            int classIndex = localPlayer.CharacterClassIndex;
            ChoppableTree myTree = ResolveTreeForClass(classIndex);
            bool isTreeCut = myTree != null && ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? myTree.isCutDown.Value : !myTree.gameObject.activeInHierarchy);
            if (myTree != null && !isTreeCut)
            {
                var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
                if (indicator == null)
                {
                    indicator = localPlayer.gameObject.AddComponent<TreeGuidanceIndicator>();
                }
                indicator.targetTree = myTree;
                indicator.reachDistance = 4f;
                Debug.Log($"[BridgeCollapseTrigger] Gắn mũi tên chỉ đường cho Local Player {localPlayer.DisplayName} (class {classIndex}) tới cây: {myTree.name}");
            }
        }

        // 3. Chạy Animator (nếu có)
        if (bridgeAnimator != null && !string.IsNullOrEmpty(collapseTriggerName))
        {
            bridgeAnimator.SetTrigger(collapseTriggerName);
        }

        // 4. Hiển thị UI Quest trên Client (dùng dynamic lookup để luôn tìm đúng HUD đang active)
        PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        if (localHudCtl != null && IsPrerequisiteCompleted())
        {
            int currentProgress = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? logsSubmitted.Value : localLogsSubmittedCount;
            localHudCtl.ShowQuest(true, this);
            localHudCtl.UpdateQuestProgress(currentProgress, requiredLogsToRepair, this);
            localHudCtl.ShowMissionAlert("HÃY TÌM 16 THANH GỖ ĐỂ SỬA LẠI CẦU!", 5.0f);
        }

        // 5. Nếu là Standalone thì tự động spawn gỗ cục bộ
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            SpawnWoodLogsLocal();
        }

        // 6. Hiển thị đối thoại giải thích sự kiện sập cầu
        if (IntroDialogueController.Instance != null)
        {
            IntroDialogueController.Instance.StartBridgeCollapseDialogue();
        }
    }

    private void OnCollapseStateChanged(bool oldVal, bool newVal)
    {
        localCollapseTriggered = newVal;
        if (newVal)
        {
            Debug.Log("[BridgeCollapseTrigger] Server báo trạng thái Cầu Đã Sập.");
            CollapseBridgeLocal();
        }
    }

    #endregion

    #region Repair & Log Submission Logic

    public void RequestSubmitCarriedLog()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            var netPlayer = localPlayer.gameObject.GetComponent<NetworkObject>();
            if (netPlayer != null)
            {
                var carrier = localPlayer.gameObject.GetComponent<PlayerLogCarrier>();
                int amount = carrier != null ? carrier.carriedLogCount : 1;
                if (carrier != null)
                {
                    carrier.DropLog();
                }
                SubmitCarriedLogServerRpc(netPlayer.NetworkObjectId, amount);
            }
        }
        else
        {
            // Chế độ offline/standalone
            var carrier = localPlayer.gameObject.GetComponent<PlayerLogCarrier>();
            int amount = carrier != null ? carrier.carriedLogCount : 1;
            if (carrier != null)
            {
                carrier.DropLog();
            }

            int remaining = requiredLogsToRepair - GetLogsSubmittedCount();
            if (remaining > 0)
            {
                int actualSubmit = Mathf.Min(amount, remaining);
                localLogsSubmittedCount += actualSubmit;
                if (localLogsSubmittedCount >= requiredLogsToRepair)
                {
                    localReadyToBuild = true;
                    if (IntroDialogueController.Instance != null)
                    {
                        IntroDialogueController.Instance.StartReadyToBuildDialogue();
                    }
                    PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
                    if (localHud != null)
                    {
                        localHud.UpdateQuestDescription("Hãy lại gần cầu và nhấn Space để cùng nhau xây dựng");
                        localHud.UpdateQuestProgress(0, 100);
                    }
                    ApplyBridgeVisualState(IsBridgeCollapsed(), IsBridgeRepaired(), GetLogsSubmittedCount());
                }
                else
                {
                    if (hud != null)
                    {
                        hud.UpdateQuestProgress(localLogsSubmittedCount, requiredLogsToRepair);
                    }
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitCarriedLogServerRpc(ulong playerNetObjectId, int amount)
    {
        if (!IsServer) return;

        SubmitCarriedLogClientRpc(playerNetObjectId);

        int remaining = requiredLogsToRepair - logsSubmitted.Value;
        if (remaining > 0)
        {
            int actualSubmit = Mathf.Min(amount, remaining);
            logsSubmitted.Value += actualSubmit;
            if (logsSubmitted.Value >= requiredLogsToRepair)
            {
                isReadyToBuild.Value = true;
                Debug.Log("[BridgeCollapseTrigger] Server: Cầu đã đủ gỗ, chuyển sang ReadyToBuild!");
            }
        }
    }

    [ClientRpc]
    private void SubmitCarriedLogClientRpc(ulong playerNetObjectId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNetObj))
        {
            var playerObj = playerNetObj.gameObject;
            var carrier = playerObj.GetComponent<PlayerLogCarrier>();
            if (carrier != null)
            {
                carrier.DropLog();
            }
        }
    }

    private void RequestSubmitLogs(int amount)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Chế độ online: Gửi ServerRpc
            SubmitLogsServerRpc(amount);
        }
        else
        {
            // Chế độ offline/standalone
            ConsumePlayerWood(localPlayer, amount);
            localLogsSubmittedCount += amount;
            
            if (localLogsSubmittedCount >= requiredLogsToRepair)
            {
                localReadyToBuild = true;
                if (IntroDialogueController.Instance != null)
                {
                    IntroDialogueController.Instance.StartReadyToBuildDialogue();
                }
                PlayerHUDController localHud = FindAnyObjectByType<PlayerHUDController>();
                if (localHud != null)
                {
                    localHud.UpdateQuestDescription("Hãy lại gần cầu và nhấn Space để cùng nhau xây dựng");
                    localHud.UpdateQuestProgress(0, 100);
                }
                ApplyBridgeVisualState(IsBridgeCollapsed(), IsBridgeRepaired(), GetLogsSubmittedCount());
            }
            else
            {
                if (hud != null)
                {
                    hud.UpdateQuestProgress(localLogsSubmittedCount, requiredLogsToRepair);
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitLogsServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        IPlayerHUDTarget playerTarget = FindPlayerByClientId(clientId);
        
        if (playerTarget != null)
        {
            int woodCount = GetPlayerWoodCount(playerTarget);
            int remaining = requiredLogsToRepair - logsSubmitted.Value;
            int actualToSubmit = Mathf.Min(amount, Mathf.Min(woodCount, remaining));
            
            if (actualToSubmit > 0)
            {
                // Trừ gỗ trên server
                ConsumePlayerWood(playerTarget, actualToSubmit);
                
                // Cập nhật tiến trình
                logsSubmitted.Value += actualToSubmit;
                
                Debug.Log($"[BridgeCollapseTrigger] Server: Client {clientId} đã gửi {actualToSubmit} thanh gỗ. Tiến trình: {logsSubmitted.Value}/{requiredLogsToRepair}");
                
                if (logsSubmitted.Value >= requiredLogsToRepair)
                {
                    isReadyToBuild.Value = true;
                    Debug.Log("[BridgeCollapseTrigger] Server: Cầu đã đủ gỗ, chuyển sang ReadyToBuild!");
                }
            }
        }
    }

    private void RepairBridgeLocal()
    {
        localRepaired = true;

        if (bridgeObstacle != null)
        {
            bridgeObstacle.enabled = false;
        }
        if (bridgeOffMeshLink != null)
        {
            bridgeOffMeshLink.activated = true;
        }

        // 1. Tắt nhắc nhở tương tác (dùng dynamic HUD lookup)
        PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        if (localHudCtl != null)
        {
            localHudCtl.ShowInteractionPrompt(false, "");
            localHudCtl.ShowQuest(false, this);
            localHudCtl.ShowMissionAlert("CẦU ĐÃ ĐƯỢC SỬA CHỮA THÀNH CÔNG!", 5.0f);
        }

        // Dọn dẹp mũi tên chỉ đường nếu còn
        if (localPlayer != null)
        {
            var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
            if (indicator != null)
            {
                Destroy(indicator);
            }
        }

        // 2. Khôi phục cây cầu gốc (xóa ghost mode, hiện lại và khôi phục materials)
        if (mainBridgeObject != null)
        {
            ApplyGhostMode(mainBridgeObject, false);
            mainBridgeObject.transform.position = originalBridgePos;
            mainBridgeObject.transform.rotation = originalBridgeRot;
        }

        SetStableSegmentsActive(true);
        SetBrokenSegmentsActive(false);

        // 3. Khôi phục tất cả materials đã lưu
        for (int i = 0; i < savedRenderers.Count; i++)
        {
            if (savedRenderers[i] != null)
            {
                savedRenderers[i].materials = savedMaterials[i];
            }
        }
        savedRenderers.Clear();
        savedMaterials.Clear();
        DestroySolidBridgeInstance();
        isGhostModeActive = false;

        // 4. Chạy Animator sửa cầu (nếu có)
        if (bridgeAnimator != null && !string.IsNullOrEmpty(repairTriggerName))
        {
            bridgeAnimator.SetTrigger(repairTriggerName);
        }

        // 5. NPC di chuyển và đối thoại sau khi cầu được sửa
        if (IntroDialogueController.Instance != null)
        {
            IntroDialogueController.Instance.TriggerMoveAndDialogueAfterBridge();
        }
    }

    private void OnRepairStateChanged(bool oldVal, bool newVal)
    {
        localRepaired = newVal;
        if (newVal)
        {
            Debug.Log("[BridgeCollapseTrigger] Cầu đã được sửa hoàn tất.");
            RepairBridgeLocal();
        }
    }

    private void OnLogsSubmittedChanged(int oldVal, int newVal)
    {
        localLogsSubmittedCount = newVal;
        Debug.Log($"[BridgeCollapseTrigger] Tiến trình xây cầu thay đổi: {newVal}/{requiredLogsToRepair}");
        
        // Cập nhật UI - dùng dynamic lookup để luôn tìm đúng HUD đang active trên màn hình của client này
        PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();
        if (localHudCtl != null)
        {
            localHudCtl.UpdateQuestProgress(newVal, requiredLogsToRepair);
        }
    }

    #endregion

    #region Blueprint & Wood Spawning Effects

    // ... (Toàn bộ các logic khác bên dưới giữ nguyên không thay đổi)
    /// <summary>
    /// Chuyển bridge sang chế độ Ghost (màu trắng trong suốt) hoặc khôi phục lại.
    /// Lưu materials gốc khi bật ghost, khôi phục khi tắt ghost.
    /// </summary>
    private void ApplyGhostMode(GameObject bridgeRoot, bool enableGhost)
    {
        if (bridgeRoot == null) return;

        if (enableGhost)
        {
            if (isGhostModeActive) return;
            isGhostModeActive = true;

            // Xóa danh sách lưu cũ
            savedRenderers.Clear();
            savedMaterials.Clear();

            // Lưu material của toàn bộ Renderer con và tắt renderer để ẩn hoàn toàn cầu ghost
            Renderer[] renderers = bridgeRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    savedRenderers.Add(r);
                    savedMaterials.Add(r.sharedMaterials);
                    r.enabled = false; // Tắt renderer để ẩn cầu ghost
                }
            }

            // Đảm bảo bật tất cả các GameObjects chứa Renderer con (ví dụ các mảnh cầu)
            if (stableBridgeSegments != null)
            {
                foreach (var go in stableBridgeSegments)
                {
                    if (go != null) go.SetActive(true);
                }
            }

            // Tắt toàn bộ colliders để người chơi không va chạm/đi trên đó được khi đang xây
            foreach (var col in mainBridgeObject.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
        }
        else
        {
            if (!isGhostModeActive) return;
            isGhostModeActive = false;

            // Khôi phục tất cả materials đã lưu
            for (int i = 0; i < savedRenderers.Count; i++)
            {
                if (savedRenderers[i] != null)
                {
                    savedRenderers[i].materials = savedMaterials[i];
                }
            }
            savedRenderers.Clear();
            savedMaterials.Clear();

            // Bật toàn bộ Renderers con và khôi phục hiển thị bình thường
            mainBridgeObject.SetActive(true);
            foreach (var r in mainBridgeObject.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }

            // Bật lại toàn bộ colliders để người chơi có thể đi qua cầu sau khi sửa xong
            foreach (var col in mainBridgeObject.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = true;
            }
        }
    }

    private void SetParticlesActive(bool active)
    {
        if (buildProgressParticles == null)
        {
            buildProgressParticles = GetComponentInChildren<ParticleSystem>();
        }

        if (buildProgressParticles != null)
        {
            var emission = buildProgressParticles.emission;
            emission.enabled = active;
            if (active && !buildProgressParticles.isPlaying)
            {
                buildProgressParticles.Play();
            }
        }
    }

    private Vector3 GetRandomBuildPosition()
    {
        Vector3 worldPos = transform.position;
        
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        float progress = isNetwork ? buildProgress.Value : localBuildProgress;

        // 1. Xác định xem có sử dụng chế độ Z-scale growth hay Segment-By-Segment
        bool useZScaleGrowth = false;
        if (displayMode == BridgeDisplayMode.ForceScaleGrowth)
        {
            useZScaleGrowth = true;
        }
        else if (displayMode == BridgeDisplayMode.ForceSegmentBySegment)
        {
            useZScaleGrowth = false;
        }
        else
        {
            if (stableBridgeSegments == null || stableBridgeSegments.Length <= 1)
            {
                if (mainBridgeObject != null)
                {
                    if (mainBridgeObject.GetComponent<LODGroup>() != null)
                    {
                        useZScaleGrowth = true;
                    }
                    else
                    {
                        bool hasLODChildren = false;
                        for (int i = 0; i < mainBridgeObject.transform.childCount; i++)
                        {
                            if (mainBridgeObject.transform.GetChild(i).name.Contains("LOD"))
                            {
                                hasLODChildren = true;
                                break;
                            }
                        }
                        if (hasLODChildren || mainBridgeObject.transform.childCount <= 1)
                        {
                            useZScaleGrowth = true;
                        }
                    }
                }
            }
        }

        // 2. Chế độ Z-scale growth (cầu nguyên khối co giãn)
        if (useZScaleGrowth && solidBridgeInstance != null)
        {
            float minVal, maxVal;
            int axis;
            GetBridgeLocalBounds(out minVal, out maxVal, out axis);
            float localLength = maxVal - minVal;
            float progressFactor = progress / 100f;

            // Rìa đang xây ở vị trí tương ứng trên trục dọc
            float localPosOnAxis = minVal + localLength * progressFactor;
            
            Vector3 leadingEdgeLocal = Vector3.zero;
            if (axis == 0) // X
            {
                leadingEdgeLocal = new Vector3(localPosOnAxis, 0.2f, Random.Range(-1.2f, 1.2f));
            }
            else if (axis == 1) // Y
            {
                leadingEdgeLocal = new Vector3(Random.Range(-1.2f, 1.2f), localPosOnAxis, Random.Range(-1.2f, 1.2f));
            }
            else // Z
            {
                leadingEdgeLocal = new Vector3(Random.Range(-1.2f, 1.2f), 0.2f, localPosOnAxis);
            }
            
            worldPos = solidBridgeInstance.transform.TransformPoint(leadingEdgeLocal);
            return worldPos;
        }

        // 3. Chế độ Segment-By-Segment (phân mảnh lắp ghép)
        if (!useZScaleGrowth)
        {
            GameObject[] segments = GetBridgeSegments();
            if (segments != null && segments.Length > 0)
            {
                int N = segments.Length;
                // Xác định mảnh cầu đang trong quá trình xây dựng ở ngưỡng progress hiện tại
                int activeCount = Mathf.Min(Mathf.FloorToInt((progress / 100f) * N), N);
                int currentBuildIndex = Mathf.Clamp(activeCount, 0, N - 1);
                
                GameObject activeSegment = segments[currentBuildIndex];
                if (activeSegment != null)
                {
                    Renderer[] renderers = activeSegment.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length > 0)
                    {
                        Renderer randomRenderer = renderers[Random.Range(0, renderers.Length)];
                        if (randomRenderer != null)
                        {
                            worldPos = randomRenderer.bounds.center;
                            float offsetX = Random.Range(-randomRenderer.bounds.extents.x * 0.6f, randomRenderer.bounds.extents.x * 0.6f);
                            float offsetZ = Random.Range(-randomRenderer.bounds.extents.z * 0.6f, randomRenderer.bounds.extents.z * 0.6f);
                            float offsetY = randomRenderer.bounds.extents.y + 0.15f;
                            
                            worldPos += new Vector3(offsetX, offsetY, offsetZ);
                            return worldPos;
                        }
                    }
                    else
                    {
                        // Fallback về vị trí của mảnh cầu đó
                        worldPos = activeSegment.transform.position + Vector3.up * 0.2f;
                        return worldPos;
                    }
                }
            }
        }

        // 4. Fallback cuối cùng nếu không thuộc các trường hợp trên
        if (mainBridgeObject != null)
        {
            Renderer[] renderers = mainBridgeObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Renderer randomRenderer = renderers[Random.Range(0, renderers.Length)];
                if (randomRenderer != null)
                {
                    worldPos = randomRenderer.bounds.center;
                    
                    // Add small random offset within the bounds of this specific renderer part
                    float offsetX = Random.Range(-randomRenderer.bounds.extents.x * 0.6f, randomRenderer.bounds.extents.x * 0.6f);
                    float offsetZ = Random.Range(-randomRenderer.bounds.extents.z * 0.6f, randomRenderer.bounds.extents.z * 0.6f);
                    float offsetY = randomRenderer.bounds.extents.y + 0.15f;
                    
                    worldPos += new Vector3(offsetX, offsetY, offsetZ);
                    return worldPos;
                }
            }
            
            // Fallback to mainBridgeObject pivot
            worldPos = mainBridgeObject.transform.position + Vector3.up * 0.5f;
        }
        return worldPos;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClickBuildServerRpc()
    {
        if (!IsServer || !isReadyToBuild.Value) return;

        int playerCount = 1;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            playerCount = NetworkManager.Singleton.ConnectedClients.Count;
        }

        float increment = buildProgressPerClick;
        if (playerCount == 1) increment = 1.0f;
        else if (playerCount == 2) increment = 1.5f;
        else if (playerCount == 3) increment = 2.0f;
        else increment = 2.5f;

        buildProgress.Value = Mathf.Min(buildProgress.Value + increment, 100f);

        Vector3 worldPos = GetRandomBuildPosition();
        int effectType = Random.Range(0, 2); 

        PlayBuildEffectClientRpc(worldPos, effectType);

        if (buildProgress.Value >= 100f)
        {
            hasBeenRepaired.Value = true;
            isReadyToBuild.Value = false;
            Debug.Log("[BridgeCollapseTrigger] Server: Cầu đã được xây dựng xong!");
            ShowRepairCompleteUIClientRpc();
        }
    }

    public void ClickBuildLocal()
    {
        if (!localReadyToBuild) return;

        localBuildProgress = Mathf.Min(localBuildProgress + 1.0f, 100f);

        Vector3 worldPos = GetRandomBuildPosition();
        int effectType = Random.Range(0, 2); 
        PlayBuildEffectLocal(worldPos, effectType);

        if (localBuildProgress >= 100f)
        {
            localRepaired = true;
            localReadyToBuild = false;
            Debug.Log("[BridgeCollapseTrigger] Standalone: Cầu đã được xây dựng xong!");
            RepairBridgeLocal();
        }
    }

    [ClientRpc]
    private void PlayBuildEffectClientRpc(Vector3 worldPos, int effectType)
    {
        PlayBuildEffectLocal(worldPos, effectType);
    }

    private void PlayBuildEffectLocal(Vector3 worldPos, int effectType)
    {

        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; 
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;
        }

        if (buildAudioClips != null && buildAudioClips.Length > 0)
        {
            AudioClip clip = buildAudioClips[Random.Range(0, buildAudioClips.Length)];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        if (clickDustParticles != null)
        {
            PlayParticleAtPosition(clickDustParticles, worldPos);
        }
        else if (buildProgressParticles != null)
        {
            PlayParticleAtPosition(buildProgressParticles, worldPos);
        }

        GameObject toolPrefab = (effectType == 0) ? hammerPrefab : sawPrefab;
        StartCoroutine(AnimateToolVisual(worldPos, toolPrefab, effectType));

        if (PlayerHUDController.Instance != null && PlayerHUDController.isCoopBuildingUIOpen)
        {
            PlayerHUDController.Instance.OnBridgeBuildClickReceived();
        }
    }

    private void PlayParticleAtPosition(ParticleSystem particlePrefabOrInstance, Vector3 position)
    {
        if (particlePrefabOrInstance == null) return;

        if (particlePrefabOrInstance.gameObject.scene.name == null)
        {
            ParticleSystem instantiated = Instantiate(particlePrefabOrInstance, position, Quaternion.identity);
            instantiated.Play();
            Destroy(instantiated.gameObject, 3f);
        }
        else
        {
            Vector3 originalPos = particlePrefabOrInstance.transform.position;
            particlePrefabOrInstance.transform.position = position;
            particlePrefabOrInstance.Emit(15);
            particlePrefabOrInstance.transform.position = originalPos;
        }
    }

    private System.Collections.IEnumerator AnimateToolVisual(Vector3 worldPos, GameObject prefab, int effectType)
    {
        GameObject toolInstance = null;
        if (prefab != null)
        {
            toolInstance = Instantiate(prefab, worldPos + Vector3.up * 0.5f, Quaternion.identity);
        }
        else
        {
            toolInstance = new GameObject(effectType == 0 ? "FallbackHammer" : "FallbackSaw");
            toolInstance.transform.position = worldPos + Vector3.up * 0.5f;

            if (effectType == 0)
            {
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(head.GetComponent<Collider>());
                head.transform.SetParent(toolInstance.transform, false);
                head.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                head.transform.localScale = new Vector3(0.12f, 0.08f, 0.08f);
                
                Renderer headRend = head.GetComponent<Renderer>();
                if (headRend != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (mat == null || mat.shader == null) mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.3f, 0.3f, 0.35f);
                    headRend.material = mat;
                }

                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(handle.GetComponent<Collider>());
                handle.transform.SetParent(toolInstance.transform, false);
                handle.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                handle.transform.localScale = new Vector3(0.04f, 0.2f, 0.04f);

                Renderer handleRend = handle.GetComponent<Renderer>();
                if (handleRend != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (mat == null || mat.shader == null) mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.45f, 0.28f, 0.12f);
                    handleRend.material = mat;
                }
            }
            else
            {
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(blade.GetComponent<Collider>());
                blade.transform.SetParent(toolInstance.transform, false);
                blade.transform.localPosition = new Vector3(0f, 0f, 0f);
                blade.transform.localScale = new Vector3(0.35f, 0.08f, 0.01f);

                Renderer bladeRend = blade.GetComponent<Renderer>();
                if (bladeRend != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (mat == null || mat.shader == null) mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.7f, 0.7f, 0.75f);
                    bladeRend.material = mat;
                }

                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(handle.GetComponent<Collider>());
                handle.transform.SetParent(toolInstance.transform, false);
                handle.transform.localPosition = new Vector3(-0.18f, 0f, 0f);
                handle.transform.localScale = new Vector3(0.05f, 0.12f, 0.04f);

                Renderer handleRend = handle.GetComponent<Renderer>();
                if (handleRend != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (mat == null || mat.shader == null) mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.8f, 0.4f, 0.1f);
                    handleRend.material = mat;
                }
            }
        }

        toolInstance.transform.localScale = Vector3.one * 1.5f;

        float duration = 0.4f;
        float elapsed = 0f;

        if (effectType == 0)
        {
            Quaternion startRot = Quaternion.Euler(0f, 0f, -40f);
            Quaternion endRot = Quaternion.Euler(0f, 0f, 50f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float curve = Mathf.Sin(t * Mathf.PI);
                toolInstance.transform.position = worldPos + Vector3.up * Mathf.Lerp(0.6f, 0.1f, curve);
                toolInstance.transform.rotation = Quaternion.Lerp(startRot, endRot, curve);
                yield return null;
            }
        }
        else
        {
            Vector3 startOffset = new Vector3(-0.2f, 0.1f, 0f);
            Vector3 endOffset = new Vector3(0.2f, 0.1f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float sawFactor = Mathf.PingPong(t * 4f, 1f);
                toolInstance.transform.position = worldPos + Vector3.Lerp(startOffset, endOffset, sawFactor);
                toolInstance.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
                yield return null;
            }
        }

        Destroy(toolInstance);
    }

    private void SpawnWoodLogsServer()
    {
        if (!IsServer || woodLogPrefab == null) return;

        if (woodLogPrefab == gameObject || woodLogPrefab.GetComponent<BridgeCollapseTrigger>() != null)
        {
            Debug.LogError("[BridgeCollapseTrigger] Infinite loop prevented in SpawnWoodLogsServer! woodLogPrefab is misconfigured.");
            return;
        }

        Debug.Log("[BridgeCollapseTrigger] Server: Đang spawn 16 thanh gỗ...");

        for (int i = 0; i < logsToSpawn; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            GameObject wood = WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, spawnPos, Quaternion.identity);
            
            var netObj = wood.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
        }
    }

    private void SpawnWoodLogsLocal()
    {
        if (woodLogPrefab == null) return;

        if (woodLogPrefab == gameObject || woodLogPrefab.GetComponent<BridgeCollapseTrigger>() != null)
        {
            Debug.LogError("[BridgeCollapseTrigger] Infinite loop prevented in SpawnWoodLogsLocal! woodLogPrefab is misconfigured.");
            return;
        }

        Debug.Log("[BridgeCollapseTrigger] Standalone: Đang spawn 16 thanh gỗ...");

        for (int i = 0; i < logsToSpawn; i++)
        {
            Vector3 spawnPos = GetSpawnPosition(i);
            WoodLogObjectPool.Instance.GetOrCreate(woodLogPrefab, spawnPos, Quaternion.identity);
        }
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (woodLogSpawnPoints != null && woodLogSpawnPoints.Length > 0)
        {
            return woodLogSpawnPoints[index % woodLogSpawnPoints.Length].position;
        }

        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 2.0f, randomCircle.y);

        if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 15.0f))
        {
            spawnPos.y = hit.point.y + 0.3f;
        }
        else
        {
            spawnPos.y = transform.position.y;
        }

        return spawnPos;
    }

    #endregion

    #region Helper Methods

    private void ApplyBridgeVisualState(bool collapsed, bool repaired, int logsProgress)
    {
        localCollapseTriggered = collapsed;
        localRepaired = repaired;
        localLogsSubmittedCount = logsProgress;

        PlayerHUDController localHudCtl = FindAnyObjectByType<PlayerHUDController>();

        if (repaired)
        {
            if (mainBridgeObject != null)
            {
                ApplyGhostMode(mainBridgeObject, false);
                mainBridgeObject.transform.position = originalBridgePos;
                mainBridgeObject.transform.rotation = originalBridgeRot;
            }
            SetStableSegmentsActive(true);
            SetBrokenSegmentsActive(false);
            if (localPlayer == null) FindLocalPlayer();
            if (localPlayer != null)
            {
                var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
                if (indicator != null) Destroy(indicator);
            }
            if (localHudCtl != null) localHudCtl.ShowQuest(false);
            DestroySolidBridgeInstance();
        }
        else
        {
            bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            bool isReady = isNetwork ? isReadyToBuild.Value : localReadyToBuild;

            if (!isReady)
            {
                SetStableSegmentsActive(false);
            }
            SetBrokenSegmentsActive(false);

            if (mainBridgeObject != null)
            {
                if (collapsed && isReady)
                {
                    ApplyGhostMode(mainBridgeObject, true); 
                }
                else
                {
                    if (isGhostModeActive)
                    {
                        ApplyGhostMode(mainBridgeObject, false);
                    }
                    mainBridgeObject.SetActive(false); 
                }
            }

            if (collapsed)
            {

                if (localPlayer == null) FindLocalPlayer();
                if (localPlayer != null)
                {
                    int classIndex = localPlayer.CharacterClassIndex;
                    ChoppableTree myTree = ResolveTreeForClass(classIndex);
                    bool isTreeCut = myTree != null && ((NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? myTree.isCutDown.Value : !myTree.gameObject.activeInHierarchy);
                    if (myTree != null && !isTreeCut)
                    {
                        var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
                        if (indicator == null)
                            indicator = localPlayer.gameObject.AddComponent<TreeGuidanceIndicator>();
                        indicator.targetTree = myTree;
                        indicator.reachDistance = 4f;
                    }
                }
                if (localHudCtl != null && IsPrerequisiteCompleted())
                {
                    localHudCtl.ShowQuest(true, this);
                    localHudCtl.UpdateQuestProgress(logsProgress, requiredLogsToRepair, this);
                }
            }
            else
            {
                if (localPlayer == null) FindLocalPlayer();
                if (localPlayer != null)
                {
                    var indicator = localPlayer.gameObject.GetComponent<TreeGuidanceIndicator>();
                    if (indicator != null) Destroy(indicator);
                }
            }
        }
    }

    private void SetStableSegmentsActive(bool active)
    {
        if (stableBridgeSegments != null)
        {
            foreach (var go in stableBridgeSegments)
            {
                if (go != null) go.SetActive(active);
            }
        }
    }

    private void SetBrokenSegmentsActive(bool active)
    {
        if (brokenBridgeSegments != null)
        {
            foreach (var go in brokenBridgeSegments)
            {
                if (go != null) go.SetActive(active);
            }
        }
    }

    private void SetMainBridgeActiveState(bool active)
    {
        if (mainBridgeObject == null) return;

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool isReady = isNetwork ? isReadyToBuild.Value : localReadyToBuild;
        bool collapsed = isNetwork ? hasCollapsed.Value : localCollapseTriggered;
        bool repaired = isNetwork ? hasBeenRepaired.Value : localRepaired;

        if (active || repaired)
        {
            mainBridgeObject.SetActive(true);
            var mainRend = mainBridgeObject.GetComponent<Renderer>();
            if (mainRend != null) mainRend.enabled = true;

            foreach (var col in mainBridgeObject.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = true;
            }
        }
        else
        {
            if (collapsed && isReady)
            {
                mainBridgeObject.SetActive(true);
                var mainRend = mainBridgeObject.GetComponent<Renderer>();
                if (mainRend != null) mainRend.enabled = false;

                var mainCols = mainBridgeObject.GetComponents<Collider>();
                foreach (var col in mainCols)
                {
                    col.enabled = false;
                }
            }
            else
            {
                mainBridgeObject.SetActive(false);
            }
        }
    }

    private GameObject[] GetBridgeSegments()
    {
        if (resolvedSegments != null && resolvedSegments.Length > 0)
        {
            return resolvedSegments;
        }

        if (stableBridgeSegments != null && stableBridgeSegments.Length > 1)
        {
            resolvedSegments = stableBridgeSegments;
            return resolvedSegments;
        }

        if (mainBridgeObject != null)
        {
            System.Collections.Generic.List<GameObject> dynamicSegments = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < mainBridgeObject.transform.childCount; i++)
            {
                Transform child = mainBridgeObject.transform.GetChild(i);
                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    dynamicSegments.Add(child.gameObject);
                }
            }

            if (dynamicSegments.Count > 0)
            {
                resolvedSegments = dynamicSegments.ToArray();
                return resolvedSegments;
            }
        }

        resolvedSegments = stableBridgeSegments;
        return resolvedSegments;
    }

    private void DestroySolidBridgeInstance()
    {
        if (solidBridgeInstance != null)
        {
            Destroy(solidBridgeInstance);
            solidBridgeInstance = null;
        }
        cloneBoxColliders.Clear();
        originalBoxSizes.Clear();
        originalBoxCenters.Clear();
        clippingMaterials.Clear();
        ghostClippingMaterials.Clear();
    }

    private void GetBridgeLocalBounds(out float minVal, out float maxVal, out int axis)
    {
        minVal = -5f;
        maxVal = 5f;
        axis = 2; 

        if (mainBridgeObject == null) return;

        MeshFilter[] mfs = mainBridgeObject.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in mfs)
        {
            if (mf != null && mf.sharedMesh != null)
            {
                var bounds = mf.sharedMesh.bounds;
                float sizeX = bounds.size.x;
                float sizeY = bounds.size.y;
                float sizeZ = bounds.size.z;

                if (sizeX > sizeY && sizeX > sizeZ)
                {
                    axis = 0; 
                    minVal = bounds.center.x - bounds.extents.x;
                    maxVal = bounds.center.x + bounds.extents.x;
                }
                else if (sizeY > sizeX && sizeY > sizeZ)
                {
                    axis = 1; 
                    minVal = bounds.center.y - bounds.extents.y;
                    maxVal = bounds.center.y + bounds.extents.y;
                }
                else
                {
                    axis = 2; 
                    minVal = bounds.center.z - bounds.extents.z;
                    maxVal = bounds.center.z + bounds.extents.z;
                }

                Debug.Log($"[BridgeCollapseTrigger] Mesh bounds size: X={sizeX}, Y={sizeY}, Z={sizeZ}. Chosen axis={axis}, minVal={minVal}, maxVal={maxVal}");
                return;
            }
        }
    }

    private void UpdateProgressiveBridgeScale(float progress)
    {
        if (mainBridgeObject == null) return;

        float progressFactor = progress / 100f;

        if (solidBridgeInstance == null)
        {
            solidBridgeInstance = Instantiate(mainBridgeObject, mainBridgeObject.transform.parent);
            solidBridgeInstance.name = mainBridgeObject.name + "_SolidInstance";

            originalLocalScale = mainBridgeObject.transform.localScale;

            solidBridgeInstance.transform.position = originalBridgePos;
            solidBridgeInstance.transform.rotation = originalBridgeRot;
            solidBridgeInstance.transform.localScale = originalLocalScale;

            cloneBoxColliders.Clear();
            originalBoxSizes.Clear();
            originalBoxCenters.Clear();
            foreach (var box in solidBridgeInstance.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box != null)
                {
                    cloneBoxColliders.Add(box);
                    originalBoxSizes.Add(box.size);
                    originalBoxCenters.Add(box.center);
                }
            }

            LODGroup lod = solidBridgeInstance.GetComponent<LODGroup>();
            if (lod != null)
            {
                lod.enabled = false;
            }

            for (int i = 0; i < solidBridgeInstance.transform.childCount; i++)
            {
                Transform child = solidBridgeInstance.transform.GetChild(i);
                if (child.name.Contains("LOD0"))
                {
                    child.gameObject.SetActive(true);
                }
                else if (child.name.Contains("LOD1") || child.name.Contains("LOD2"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            Renderer[] origRenders = mainBridgeObject.GetComponentsInChildren<Renderer>(true);
            Renderer[] cloneRenders = solidBridgeInstance.GetComponentsInChildren<Renderer>(true);
            clippingMaterials.Clear();

            Shader clippingShader = Resources.Load<Shader>("BridgeClipping");
            if (clippingShader == null)
            {
                clippingShader = Shader.Find("Custom/BridgeClipping");
            }
            if (clippingShader == null)
            {
                Debug.LogError("[BridgeCollapseTrigger] Không tìm thấy custom shader 'Custom/BridgeClipping' trong Resources lẫn Shader.Find!");
            }

            for (int k = 0; k < cloneRenders.Length; k++)
            {
                if (k < origRenders.Length && cloneRenders[k] != null && origRenders[k] != null)
                {
                    int savedIdx = savedRenderers.IndexOf(origRenders[k]);
                    Material origMat = null;
                    if (savedIdx >= 0 && savedIdx < savedMaterials.Count && savedMaterials[savedIdx].Length > 0)
                    {
                        origMat = savedMaterials[savedIdx][0];
                    }
                    if (origMat == null)
                    {
                        origMat = origRenders[k].sharedMaterial;
                    }

                    if (origMat != null && clippingShader != null)
                    {
                        Material clipMat = new Material(clippingShader);
                        
                        if (origMat.HasProperty("_Albedo")) clipMat.SetTexture("_Albedo", origMat.GetTexture("_Albedo"));
                        else if (origMat.HasProperty("_BaseMap")) clipMat.SetTexture("_Albedo", origMat.GetTexture("_BaseMap"));
                        else if (origMat.HasProperty("_MainTex")) clipMat.SetTexture("_Albedo", origMat.GetTexture("_MainTex"));

                        if (origMat.HasProperty("_Normal")) clipMat.SetTexture("_Normal", origMat.GetTexture("_Normal"));
                        else if (origMat.HasProperty("_BumpMap")) clipMat.SetTexture("_Normal", origMat.GetTexture("_BumpMap"));

                        if (origMat.HasProperty("_Specular")) clipMat.SetTexture("_Specular", origMat.GetTexture("_Specular"));

                        if (origMat.HasProperty("_Primary_Color")) clipMat.SetColor("_Primary_Color", origMat.GetColor("_Primary_Color"));
                        if (origMat.HasProperty("_Secondary_Color")) clipMat.SetColor("_Secondary_Color", origMat.GetColor("_Secondary_Color"));
                        if (origMat.HasProperty("_Tertiary_Color")) clipMat.SetColor("_Tertiary_Color", origMat.GetColor("_Tertiary_Color"));
                        if (origMat.HasProperty("_Color")) clipMat.SetColor("_Color", origMat.GetColor("_Color"));

                        cloneRenders[k].material = clipMat;
                        clippingMaterials.Add(clipMat);
                    }
                    else
                    {
                        if (savedIdx >= 0 && savedIdx < savedMaterials.Count)
                        {
                            cloneRenders[k].materials = savedMaterials[savedIdx];
                        }
                    }
                    cloneRenders[k].enabled = true;
                }
            }

            foreach (var col in solidBridgeInstance.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.enabled = true;
            }
        }

        float minVal, maxVal;
        int axis;
        GetBridgeLocalBounds(out minVal, out maxVal, out axis);

        if (clippingMaterials != null && clippingMaterials.Count > 0)
        {
            solidBridgeInstance.transform.position = originalBridgePos;
            solidBridgeInstance.transform.rotation = originalBridgeRot;
            solidBridgeInstance.transform.localScale = originalLocalScale;

            float clipThreshold = Mathf.Lerp(minVal, maxVal, progressFactor);

            Vector4 clipAxisVec = Vector4.zero;
            if (axis == 0) clipAxisVec = new Vector4(1f, 0f, 0f, 0f);
            else if (axis == 1) clipAxisVec = new Vector4(0f, 1f, 0f, 0f);
            else clipAxisVec = new Vector4(0f, 0f, 1f, 0f);

            Debug.Log($"[BridgeCollapseTrigger] Shader clipping active: progress={progress}%, axis={axis}, minVal={minVal}, maxVal={maxVal}, threshold={clipThreshold}");

            foreach (var mat in clippingMaterials)
            {
                if (mat != null)
                {
                    mat.SetVector("_ClipAxis", clipAxisVec);
                    mat.SetFloat("_ClipThreshold", clipThreshold);
                    mat.SetFloat("_InvertClip", 0f);
                }
            }
        }
        else
        {
            Vector3 newScale = originalLocalScale;
            Vector3 localOffset = Vector3.zero;
            float totalLength = maxVal - minVal;

            if (axis == 0) 
            {
                newScale.x = originalLocalScale.x * progressFactor;
                localOffset.x = -(1f - progressFactor) * totalLength * 0.5f;
            }
            else if (axis == 1) 
            {
                newScale.y = originalLocalScale.y * progressFactor;
                localOffset.y = -(1f - progressFactor) * totalLength * 0.5f;
            }
            else 
            {
                newScale.z = originalLocalScale.z * progressFactor;
                localOffset.z = -(1f - progressFactor) * totalLength * 0.5f;
            }

            solidBridgeInstance.transform.localScale = newScale;
            Vector3 worldOffset = originalBridgeRot * Vector3.Scale(localOffset, originalLocalScale);
            solidBridgeInstance.transform.position = originalBridgePos + worldOffset;
            Debug.Log($"[BridgeCollapseTrigger] Fallback scale active: progress={progress}%, scale={newScale}");
        }

        for (int i = 0; i < cloneBoxColliders.Count; i++)
        {
            if (cloneBoxColliders[i] != null && i < originalBoxSizes.Count && i < originalBoxCenters.Count)
            {
                Vector3 newSize = originalBoxSizes[i];
                Vector3 newCenter = originalBoxCenters[i];

                if (axis == 0) 
                {
                    newSize.x = originalBoxSizes[i].x * progressFactor;
                    newCenter.x = originalBoxCenters[i].x - (originalBoxSizes[i].x * 0.5f) * (1f - progressFactor);
                }
                else if (axis == 1) 
                {
                    newSize.y = originalBoxSizes[i].y * progressFactor;
                    newCenter.y = originalBoxCenters[i].y - (originalBoxSizes[i].y * 0.5f) * (1f - progressFactor);
                }
                else 
                {
                    newSize.z = originalBoxSizes[i].z * progressFactor;
                    newCenter.z = originalBoxCenters[i].z - (originalBoxSizes[i].z * 0.5f) * (1f - progressFactor);
                }

                cloneBoxColliders[i].size = newSize;
                cloneBoxColliders[i].center = newCenter;
            }
        }

        solidBridgeInstance.SetActive(true);
    }

    private void UpdateSegmentBySegmentProgress(float progress)
    {
        GameObject[] segments = GetBridgeSegments();
        if (segments == null || segments.Length == 0) return;

        int N = segments.Length;
        int activeCount = Mathf.Min(Mathf.FloorToInt((progress / 100f) * N), N);

        for (int i = 0; i < N; i++)
        {
            GameObject segment = segments[i];
            if (segment == null) continue;

            segment.SetActive(true);

            Renderer[] renderers = segment.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = segment.GetComponentsInChildren<Collider>(true);

            bool isSolid = (i < activeCount);

            if (isSolid)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    int savedIndex = savedRenderers.IndexOf(r);
                    if (savedIndex >= 0 && savedIndex < savedMaterials.Count)
                    {
                        r.materials = savedMaterials[savedIndex];
                    }
                    r.enabled = true;
                }
                foreach (var col in colliders)
                {
                    if (col != null) col.enabled = true;
                }
            }
            else
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!savedRenderers.Contains(r))
                    {
                        savedRenderers.Add(r);
                        savedMaterials.Add(r.sharedMaterials);
                    }
                    r.enabled = false;
                }
                foreach (var col in colliders)
                {
                    if (col != null) col.enabled = false;
                }
            }
        }
    }

    private void UpdateProgressiveBridgeSegments(float progress)
    {
        bool useZScaleGrowth = false;
        
        if (displayMode == BridgeDisplayMode.ForceScaleGrowth)
        {
            useZScaleGrowth = true;
        }
        else if (displayMode == BridgeDisplayMode.ForceSegmentBySegment)
        {
            useZScaleGrowth = false;
        }
        else
        {
            if (stableBridgeSegments == null || stableBridgeSegments.Length <= 1)
            {
                if (mainBridgeObject != null)
                {
                    if (mainBridgeObject.GetComponent<LODGroup>() != null)
                    {
                        useZScaleGrowth = true;
                    }
                    else
                    {
                        bool hasLODChildren = false;
                        for (int i = 0; i < mainBridgeObject.transform.childCount; i++)
                        {
                            if (mainBridgeObject.transform.GetChild(i).name.Contains("LOD"))
                            {
                                hasLODChildren = true;
                                break;
                            }
                        }
                        if (hasLODChildren || mainBridgeObject.transform.childCount <= 1)
                        {
                            useZScaleGrowth = true;
                        }
                    }
                }
            }
        }

        if (useZScaleGrowth)
        {
            UpdateProgressiveBridgeScale(progress);
        }
        else
        {
            DestroySolidBridgeInstance();
            UpdateSegmentBySegmentProgress(progress);
        }
    }

    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;

        if (go.GetComponent<IPlayerHUDTarget>() != null || go.GetComponentInParent<IPlayerHUDTarget>() != null) return true;

        if (go.CompareTag("Player") || (go.transform.parent != null && go.transform.parent.CompareTag("Player"))) return true;

        return false;
    }

    private ChoppableTree ResolveTreeForClass(int classIndex)
    {
        ChoppableTree myTree = null;
        if (targetTrees != null && targetTrees.Length > 0)
        {
            int idx = classIndex % targetTrees.Length;
            if (idx >= 0 && idx < targetTrees.Length)
            {
                myTree = targetTrees[idx];
            }
        }
        
        if (myTree == null && targetTrees != null)
        {
            foreach (var tree in targetTrees)
            {
                if (tree != null)
                {
                    myTree = tree;
                    break;
                }
            }
        }
        
        if (myTree == null)
        {
            var allTrees = FindObjectsByType<ChoppableTree>(FindObjectsSortMode.None);
            if (allTrees != null && allTrees.Length > 0)
            {
                myTree = allTrees[0];
            }
        }
        return myTree;
    }

    private void FindLocalPlayer()
    {
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            var p = PlayerHUDController.LocalPlayerTarget;
            if (p != null && (!isNetworkActive || p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && (!isNetworkActive || p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p;
                return;
            }
        }

        if (Time.time >= nextPlayerSearchTime)
        {
            nextPlayerSearchTime = Time.time + 2f;

            var allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mono in allComponents)
            {
                if (mono is IPlayerHUDTarget p)
                {
                    if (!isNetworkActive || p.IsStandaloneMode || p.IsOwner)
                    {
                        localPlayer = p;
                        return;
                    }
                }
            }
        }
    }

    private IPlayerHUDTarget FindPlayerByClientId(ulong clientId)
    {
        var activePlayers = PlayerHUDManager.ActivePlayers;
        foreach (var p in activePlayers)
        {
            if (p != null && p.IsSpawned && p.OwnerClientId == clientId)
            {
                return p;
            }
        }
        return null;
    }

    private int GetPlayerWoodCount(IPlayerHUDTarget player)
    {
        string[] slots = player.InventorySlots;
        if (slots == null) return 0;

        int woodCount = 0;
        foreach (var slotVal in slots)
        {
            if (string.IsNullOrEmpty(slotVal)) continue;
            string itemName = slotVal;
            int count = 1;
            if (slotVal.Contains(":"))
            {
                var parts = slotVal.Split(':');
                itemName = parts[0];
                int.TryParse(parts[1], out count);
            }
            if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
            {
                woodCount += count;
            }
        }
        return woodCount;
    }

    private void ConsumePlayerWood(IPlayerHUDTarget player, int amountNeeded)
    {
        string[] slots = player.InventorySlots;
        if (slots == null) return;

        int remainingNeeded = amountNeeded;
        for (int i = 0; i < slots.Length; i++)
        {
            if (string.IsNullOrEmpty(slots[i])) continue;
            string itemName = slots[i];
            int count = 1;
            if (slots[i].Contains(":"))
            {
                var parts = slots[i].Split(':');
                itemName = parts[0];
                int.TryParse(parts[1], out count);
            }

            if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
            {
                if (count <= remainingNeeded)
                {
                    remainingNeeded -= count;
                    slots[i] = "";
                }
                else
                {
                    slots[i] = itemName + ":" + (count - remainingNeeded);
                    remainingNeeded = 0;
                }

                if (remainingNeeded <= 0) break;
            }
        }

        if (player.IsStandaloneMode || player.IsOwner)
        {
            if (hud != null)
            {
                hud.SetInventorySlots(slots);
            }
        }

        player.SavePlayerStateToDatabase();
    }

    #endregion
}