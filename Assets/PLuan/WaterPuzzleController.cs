using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class WaterPuzzleController : NetworkBehaviour
{
    public enum WaterPuzzleState
    {
        Pending = 0,        // Trước khi đá rơi
        Blocked = 1,        // Đá chặn 2 đầu, vũng nước xuất hiện ở giữa
        Overflowing = 2,    // Đá bị phá, nước tràn ra (nguy hiểm)
        Frozen = 3          // Bị đóng băng (cầu băng kích hoạt)
    }

    [Header("Puzzle Settings & Tags")]
    [Tooltip("Tag của nguyên tố băng dùng để đóng băng (mặc định là 'Bang')")]
    public string iceTag = "Bang";

    [Header("Blocking Elements")]
    [Tooltip("Danh sách các đá chặn. Khi tất cả bị phá hủy (null hoặc inactive), nước sẽ tràn ra.")]
    public GameObject[] blockingRocks;

    [Header("Water Objects")]
    [Tooltip("Vũng nước nhỏ ban đầu (hiện khi đá chặn)")]
    public GameObject puddleObject;

    [Tooltip("Dòng nước chảy xiết (hiện khi đá vỡ, gây sát thương/chặn đường)")]
    public GameObject flowingWaterObject;

    [Tooltip("Cầu băng (hiện khi bị đóng băng)")]
    public GameObject iceBridgeObject;

    [Header("Water Speed Settings")]
    [Tooltip("Tốc độ chảy của dòng nước chảy xiết")]
    public float flowingWaterSpeed = 5.0f;

    [Tooltip("Tốc độ chảy của vũng nước tĩnh")]
    public float puddleWaterSpeed = 0.0f;

    [Tooltip("Hướng chảy trục X (U) của dòng nước")]
    public float flowingWaterDirectionX = 1.0f;

    [Tooltip("Hướng chảy trục Y (V) của dòng nước")]
    public float flowingWaterDirectionY = 0.0f;

    [Header("Flow Animation Settings")]
    [Tooltip("Thời gian dòng nước trào ra chạy đến đích (giây)")]
    public float flowDuration = 2.0f;

    [Tooltip("Khoảng cách di chuyển vật lý của dòng nước (mét)")]
    public float flowTravelDistance = 15.0f;

    [Header("Hazard Settings")]
    [Tooltip("Điểm hồi sinh an toàn khi rơi xuống dòng nước chảy xiết")]
    public Transform safeRespawnPoint;

    [Tooltip("Lượng sát thương người chơi sẽ nhận khi chạm vào nước chảy xiết")]
    public float waterDamage = 15f;

    [Header("Freezing Effects")]
    [Tooltip("Thời gian thực hiện hiệu ứng đóng băng (giây)")]
    public float freezeDuration = 1.0f;

    [Tooltip("Prefab hiệu ứng nổ băng/tuyết rơi khi đóng băng")]
    public GameObject freezeParticlePrefab;

    [Tooltip("Các phân đoạn của cầu băng để tạo hiệu ứng đóng băng lan truyền từ đầu này sang đầu kia. " +
             "Nếu danh sách này trống, script sẽ tự động dùng hiệu ứng Scale trục Z của IceBridge.")]
    public List<GameObject> iceBridgeSegments = new List<GameObject>();

    [Tooltip("Âm thanh đóng băng")]
    public AudioClip freezeSound;

    // --- BIẾN ĐỒNG BỘ MẠNG (NETCODE) ---
    private NetworkVariable<WaterPuzzleState> currentState = new NetworkVariable<WaterPuzzleState>(
        WaterPuzzleState.Pending, // Mặc định là Pending lúc load màn chơi (Đá chưa rơi)
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private WaterPuzzleState localState = WaterPuzzleState.Pending;
    public WaterPuzzleState CurrentState => IsNetworkActive ? currentState.Value : localState;
    private AudioSource audioSource;
    private Coroutine freezeCoroutine;
    private bool isInitialized = false;
    private float overflowStateStartTime = 0f;
    private List<GameObject> activeRocksList = new List<GameObject>();
    private int initialRockCount = 0;
    private bool isRocksInitialized = false;
    private Vector3 originalFlowingWaterLocalPos;
    private Coroutine flowCoroutine;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    private void Awake()
    {
        if (flowingWaterObject != null)
        {
            originalFlowingWaterLocalPos = flowingWaterObject.transform.localPosition;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && freezeSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // Âm thanh 3D
        }

        // Đảm bảo ẩn/hiện đúng trạng thái ban đầu của môi trường ngay lập tức khi Awake (Pending - Ẩn tất cả nước/cầu)
        ApplyVisualStates(WaterPuzzleState.Pending, false);
    }

    private void Start()
    {
        // Gán detector cho FlowingWater để nhận biết đạn băng
        if (flowingWaterObject != null)
        {
            var detector = flowingWaterObject.GetComponent<WaterFreezeDetector>();
            if (detector == null)
            {
                detector = flowingWaterObject.AddComponent<WaterFreezeDetector>();
            }
            detector.Initialize(this);
        }

        // Lọc danh sách đá chặn khi bắt đầu
        activeRocksList.Clear();
        if (blockingRocks != null)
        {
            foreach (var rock in blockingRocks)
            {
                if (rock != null)
                {
                    activeRocksList.Add(rock);
                }
            }
        }

        // Tự động tìm thêm đá chặn từ scene nếu gán thiếu
        EnsureRocksInitialized();
        Debug.Log($"[WaterPuzzleController] Khởi tạo thành công: Theo dõi {activeRocksList.Count} viên đá chặn.");

        // Nếu chạy Offline, khởi tạo trạng thái ban đầu cục bộ là Pending
        if (!IsNetworkActive)
        {
            InitializeState(WaterPuzzleState.Pending);
        }
    }

    private void EnsureRocksInitialized()
    {
        if (isRocksInitialized)
        {
            // Chỉ loại bỏ các đá đã bị hủy/null trong quá trình chơi
            activeRocksList.RemoveAll(item => item == null);
            return;
        }

        // Loại bỏ các đá bị null ban đầu
        activeRocksList.RemoveAll(item => item == null);

        // Nếu danh sách trống, quét tìm lại từ Container hoặc Scene làm phương án dự phòng
        if (activeRocksList.Count == 0)
        {
            // Tìm từ container DaChanCuaDaRoi trước (chứa các thực thể đá thực tế)
            GameObject container = GameObject.Find("DaChanCuaDaRoi");
            if (container != null)
            {
                var puzzles = container.GetComponentsInChildren<ElementalRockPuzzle>(true);
                foreach (var puzzle in puzzles)
                {
                    if (puzzle != null && !activeRocksList.Contains(puzzle.gameObject))
                    {
                        activeRocksList.Add(puzzle.gameObject);
                        Debug.Log($"[WaterPuzzleController] Khởi tạo đá chặn từ container DaChanCuaDaRoi: '{puzzle.gameObject.name}'");
                    }
                }
            }

            // Nếu vẫn chưa tìm đủ 2 đá, quét toàn bộ Scene làm phương án dự phòng
            if (activeRocksList.Count < 2)
            {
#if UNITY_2023_1_OR_NEWER
                var scenePuzzles = FindObjectsByType<ElementalRockPuzzle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                var scenePuzzles = FindObjectsOfType<ElementalRockPuzzle>(true);
#endif
                foreach (var puzzle in scenePuzzles)
                {
                    if (puzzle != null && !activeRocksList.Contains(puzzle.gameObject))
                    {
                        activeRocksList.Add(puzzle.gameObject);
                        Debug.Log($"[WaterPuzzleController] Khởi tạo đá chặn từ Scene: '{puzzle.gameObject.name}'");
                    }
                }
            }
        }
        
        initialRockCount = activeRocksList.Count;
        isRocksInitialized = true;
        Debug.Log($"[WaterPuzzleController] Khởi tạo danh sách đá chặn hoàn tất. Số đá ban đầu: {initialRockCount}");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsNetworkActive)
        {
            currentState.OnValueChanged += OnPuzzleStateChanged;
            InitializeState(currentState.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsNetworkActive)
        {
            currentState.OnValueChanged -= OnPuzzleStateChanged;
        }
    }

    private void InitializeState(WaterPuzzleState initialState)
    {
        localState = initialState;
        ApplyVisualStates(initialState, false); // Không chạy hiệu ứng lúc mới load game
        isInitialized = true;
    }

    private void Update()
    {
        // Chỉ Server hoặc chế độ Offline mới có quyền kiểm tra trạng thái của đá để chuyển đổi trạng thái
        if (!IsNetworkActive || IsServer)
        {
            WaterPuzzleState currentActiveState = IsNetworkActive ? currentState.Value : localState;

            if (currentActiveState == WaterPuzzleState.Pending)
            {
                // Kiểm tra xem đã có ít nhất một viên đá chặn xuất hiện trong Scene chưa (Đá rơi xuống)
                if (CheckAnyRockActivated())
                {
                    ChangePuzzleState(WaterPuzzleState.Blocked);
                }
            }
            else if (currentActiveState == WaterPuzzleState.Blocked)
            {
                // Kiểm tra xem tất cả đá đã bị phá hủy hoàn toàn chưa
                if (CheckAllRocksDestroyed())
                {
                    ChangePuzzleState(WaterPuzzleState.Overflowing);
                }
            }
        }
    }

    private bool CheckAnyRockActivated()
    {
        EnsureRocksInitialized();
        if (activeRocksList == null || activeRocksList.Count == 0) return false;

        foreach (var rock in activeRocksList)
        {
            if (rock != null)
            {
                var puzzle = rock.GetComponentInChildren<ElementalRockPuzzle>(true);
                if (puzzle != null)
                {
                    // Nếu đá chặn có kịch bản ElementalRockPuzzle, kiểm tra trạng thái IsShown của nó
                    if (puzzle.IsShown)
                    {
                        Debug.Log($"[WaterPuzzleController] Phát hiện đá chặn '{rock.name}' đã kích hoạt hiển thị (rơi xuống scene)!");
                        return true;
                    }
                }
                else
                {
                    // Fallback nếu đá không có script ElementalRockPuzzle
                    if (rock.activeInHierarchy)
                    {
                        Debug.Log($"[WaterPuzzleController] Phát hiện đá chặn '{rock.name}' đã kích hoạt active (rơi xuống scene)!");
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private bool CheckAllRocksDestroyed()
    {
        EnsureRocksInitialized();
        // Nếu lúc bắt đầu không tìm thấy viên đá nào hợp lệ được gán trong Scene, không kích hoạt trào nước
        if (initialRockCount == 0) return false;

        foreach (var rock in activeRocksList)
        {
            if (rock != null)
            {
                var puzzle = rock.GetComponentInChildren<ElementalRockPuzzle>(true);
                if (puzzle != null)
                {
                    // Nếu có ít nhất 1 viên đá có script và đang hiển thị, chưa giải xong
                    if (puzzle.IsShown)
                    {
                        return false;
                    }
                }
                else
                {
                    // Fallback
                    if (rock.activeInHierarchy)
                    {
                        return false;
                    }
                }
            }
        }
        Debug.Log("[WaterPuzzleController] Phát hiện tất cả đá chặn gán ban đầu đã bị vỡ/ẩn hoàn toàn!");
        return true;
    }

    /// <summary>
    /// Thay đổi trạng thái câu đố (Chỉ chạy trên Server hoặc chế độ Offline)
    /// </summary>
    public void ChangePuzzleState(WaterPuzzleState newState)
    {
        Debug.Log($"[WaterPuzzleController] Yêu cầu chuyển đổi trạng thái câu đố sang: {newState}. IsNetworkActive: {IsNetworkActive}, IsServer: {IsServer}");
        if (IsNetworkActive)
        {
            if (IsServer)
            {
                currentState.Value = newState;
            }
        }
        else
        {
            if (localState != newState)
            {
                localState = newState;
                ApplyVisualStates(newState, true);
            }
        }
    }

    /// <summary>
    /// Kích hoạt đóng băng dòng nước (Elena bắn đạn băng trúng detector)
    /// </summary>
    public void FreezeWater()
    {
        WaterPuzzleState activeState = IsNetworkActive ? currentState.Value : localState;
        
        if (activeState == WaterPuzzleState.Overflowing)
        {
            // Tránh đạn băng đâm xuyên phá đá rồi đóng băng luôn dòng nước trong cùng 1 frame
            if (Time.time - overflowStateStartTime < 0.6f)
            {
                Debug.Log("[WaterPuzzleController] Quá nhanh! Bỏ qua yêu cầu đóng băng (chờ đạn cũ biến mất).");
                return;
            }

            Debug.Log("[WaterPuzzleController] Yêu cầu đóng băng dòng nước!");
            if (IsNetworkActive)
            {
                RequestFreezeServerRpc();
            }
            else
            {
                ChangePuzzleState(WaterPuzzleState.Frozen);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestFreezeServerRpc()
    {
        if (currentState.Value == WaterPuzzleState.Overflowing)
        {
            currentState.Value = WaterPuzzleState.Frozen;
        }
    }

    private void OnPuzzleStateChanged(WaterPuzzleState oldVal, WaterPuzzleState newVal)
    {
        Debug.Log($"[WaterPuzzleController] ĐỒNG BỘ MẠNG: Trạng thái câu đố thay đổi từ {oldVal} sang {newVal}");
        localState = newVal;
        ApplyVisualStates(newVal, true);
    }

    private void ApplyVisualStates(WaterPuzzleState state, bool animate)
    {
        Debug.Log($"[WaterPuzzleController] Cập nhật giao diện trạng thái: {state} (Chạy hiệu ứng: {animate})");

        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }

        if (flowCoroutine != null)
        {
            StopCoroutine(flowCoroutine);
            flowCoroutine = null;
        }

        switch (state)
        {
            case WaterPuzzleState.Pending:
                if (puddleObject != null) puddleObject.SetActive(false);
                if (flowingWaterObject != null)
                {
                    flowingWaterObject.SetActive(false);
                    flowingWaterObject.transform.localPosition = CalculateEndLocalPos();
                }
                if (iceBridgeObject != null) iceBridgeObject.SetActive(false);
                break;

            case WaterPuzzleState.Blocked:
                if (puddleObject != null) puddleObject.SetActive(false);
                if (flowingWaterObject != null)
                {
                    flowingWaterObject.SetActive(true);
                    flowingWaterObject.transform.localPosition = CalculateEndLocalPos();
                    var flowRenderer = flowingWaterObject.GetComponent<Renderer>();
                    if (flowRenderer != null && flowRenderer.material != null)
                    {
                        flowRenderer.material.SetFloat("_Speed", flowingWaterSpeed);
                        flowRenderer.material.SetFloat("_SpeedX", flowingWaterDirectionX);
                        flowRenderer.material.SetFloat("_SpeedY", flowingWaterDirectionY);
                    }
                }
                if (iceBridgeObject != null) iceBridgeObject.SetActive(false);
                break;

            case WaterPuzzleState.Overflowing:
                if (puddleObject != null) puddleObject.SetActive(false);
                if (flowingWaterObject != null)
                {
                    flowingWaterObject.SetActive(true);
                    flowingWaterObject.transform.localPosition = CalculateEndLocalPos();
                    var flowRenderer = flowingWaterObject.GetComponent<Renderer>();
                    if (flowRenderer != null && flowRenderer.material != null)
                    {
                        flowRenderer.material.SetFloat("_Speed", flowingWaterSpeed);
                        flowRenderer.material.SetFloat("_SpeedX", flowingWaterDirectionX);
                        flowRenderer.material.SetFloat("_SpeedY", flowingWaterDirectionY);
                    }
                }
                if (iceBridgeObject != null) iceBridgeObject.SetActive(false);
                overflowStateStartTime = Time.time;
                break;

            case WaterPuzzleState.Frozen:
                if (puddleObject != null) puddleObject.SetActive(false);
                if (flowingWaterObject != null)
                {
                    flowingWaterObject.SetActive(false);
                    flowingWaterObject.transform.localPosition = CalculateEndLocalPos();
                }
                
                if (iceBridgeObject != null)
                {
                    iceBridgeObject.SetActive(true);
                }

                if (animate)
                {
                    // Chạy hiệu ứng đóng băng mượt mà
                    freezeCoroutine = StartCoroutine(AnimateFreezingProcess());
                }
                else
                {
                    // Snap thẳng tới trạng thái đóng băng hoàn thành
                    SetBridgeSegmentsActive(true);
                    if (iceBridgeObject != null)
                    {
                        iceBridgeObject.transform.localScale = Vector3.one;
                    }
                }
                break;
        }
    }

    private IEnumerator AnimateFreezingProcess()
    {
        // 1. Chơi âm thanh đóng băng
        if (audioSource != null && freezeSound != null)
        {
            audioSource.PlayOneShot(freezeSound);
        }

        // 2. Chạy hiệu ứng hạt tại khu vực
        if (freezeParticlePrefab != null)
        {
            Vector3 particlePos = iceBridgeObject != null ? iceBridgeObject.transform.position : transform.position;
            var particles = Instantiate(freezeParticlePrefab, particlePos, Quaternion.identity);
            Destroy(particles.gameObject, 5f);
        }

        // 3. Hiệu ứng đóng băng lan truyền (Segmented Grow)
        if (iceBridgeSegments != null && iceBridgeSegments.Count > 0)
        {
            // Tắt toàn bộ segment trước
            SetBridgeSegmentsActive(false);

            float delayPerSeg = freezeDuration / iceBridgeSegments.Count;
            for (int i = 0; i < iceBridgeSegments.Count; i++)
            {
                if (iceBridgeSegments[i] != null)
                {
                    iceBridgeSegments[i].SetActive(true);
                    
                    // Tạo một hiệu ứng vụn băng nhỏ tại mỗi segment khi nó đóng băng
                    if (freezeParticlePrefab != null)
                    {
                        var segParticle = Instantiate(freezeParticlePrefab, iceBridgeSegments[i].transform.position, Quaternion.identity);
                        segParticle.transform.localScale = Vector3.one * 0.5f;
                        Destroy(segParticle.gameObject, 2f);
                    }
                }
                yield return new WaitForSeconds(delayPerSeg);
            }
        }
        else if (iceBridgeObject != null)
        {
            // Hiệu ứng Scale tuyến tính trục Z (Linear Scale)
            Vector3 originalScale = iceBridgeObject.transform.localScale;
            Vector3 startScale = new Vector3(originalScale.x, originalScale.y, 0f);
            iceBridgeObject.transform.localScale = startScale;

            float elapsed = 0f;
            while (elapsed < freezeDuration)
            {
                elapsed += Time.deltaTime;
                float pct = Mathf.Clamp01(elapsed / freezeDuration);
                
                // Scale tăng dần trục Z từ 0 lên tỷ lệ gốc
                iceBridgeObject.transform.localScale = Vector3.Lerp(startScale, originalScale, pct);
                yield return null;
            }
            iceBridgeObject.transform.localScale = originalScale;
        }
    }

    private void SetBridgeSegmentsActive(bool active)
    {
        if (iceBridgeSegments != null && iceBridgeSegments.Count > 0)
        {
            foreach (var seg in iceBridgeSegments)
            {
                if (seg != null) seg.SetActive(active);
            }
        }
    }

    // --- XỬ LÝ VA CHẠM DÒNG NƯỚC (HAZARD TRÊN SERVER & CLIENT) ---
    public void HandleHazardTriggerEnter(Collider other)
    {
        WaterPuzzleState activeState = IsNetworkActive ? currentState.Value : localState;
        if (activeState != WaterPuzzleState.Overflowing && activeState != WaterPuzzleState.Blocked) return;

        GameObject collidedObj = other.gameObject;

        // SERVER: Áp dụng sát thương máu cho Player
        if (!IsNetworkActive || IsServer)
        {
            if (IsAnyPlayer(collidedObj, out GameObject playerRoot))
            {
                ApplyWaterDamage(playerRoot);
            }
        }

        // CLIENT (Hoặc Offline): Dịch chuyển vị trí người chơi sở hữu về an toàn cục bộ để mượt mà di chuyển
        if (IsLocalPlayer(collidedObj, out GameObject localPlayerRoot))
        {
            TeleportPlayerLocal(localPlayerRoot);
        }
    }

    private bool IsAnyPlayer(GameObject go, out GameObject playerRoot)
    {
        playerRoot = null;
        if (go == null) return false;

        var elena = go.GetComponentInParent<ElenaPlayer>();
        if (elena != null) { playerRoot = elena.gameObject; return true; }

        var arthur = go.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) { playerRoot = arthur.gameObject; return true; }

        var leo = go.GetComponentInParent<LeoPlayer>();
        if (leo != null) { playerRoot = leo.gameObject; return true; }

        var maya = go.GetComponentInParent<MayaPlayer>();
        if (maya != null) { playerRoot = maya.gameObject; return true; }

        return false;
    }

    private bool IsLocalPlayer(GameObject go, out GameObject localPlayerRoot)
    {
        localPlayerRoot = null;
        if (go == null) return false;

        var elena = go.GetComponentInParent<ElenaPlayer>();
        if (elena != null)
        {
            if (!IsNetworkActive || elena.IsOwner) { localPlayerRoot = elena.gameObject; return true; }
        }

        var arthur = go.GetComponentInParent<ArthurPlayer>();
        if (arthur != null)
        {
            if (!IsNetworkActive || arthur.IsOwner) { localPlayerRoot = arthur.gameObject; return true; }
        }

        var leo = go.GetComponentInParent<LeoPlayer>();
        if (leo != null)
        {
            if (!IsNetworkActive || leo.IsOwner) { localPlayerRoot = leo.gameObject; return true; }
        }

        var maya = go.GetComponentInParent<MayaPlayer>();
        if (maya != null)
        {
            if (!IsNetworkActive || maya.IsOwner) { localPlayerRoot = maya.gameObject; return true; }
        }

        return false;
    }

    private void TeleportPlayerLocal(GameObject playerRoot)
    {
        if (safeRespawnPoint == null)
        {
            Debug.LogWarning("[WaterPuzzleController] Chưa gán safeRespawnPoint! Không thể dịch chuyển player.");
            return;
        }

        Debug.Log($"[WaterPuzzleController] Local Player chạm nước chảy xiết! Dịch chuyển về: {safeRespawnPoint.position}");

        var rb = playerRoot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        playerRoot.transform.position = safeRespawnPoint.position;
        playerRoot.transform.rotation = safeRespawnPoint.rotation;
    }

    private void ApplyWaterDamage(GameObject playerRoot)
    {
        if (waterDamage <= 0f) return;

        var elena = playerRoot.GetComponent<ElenaPlayer>();
        if (elena != null) { elena.TakeDamage(waterDamage); return; }

        var arthur = playerRoot.GetComponent<ArthurPlayer>();
        if (arthur != null) { arthur.TakeDamage(waterDamage); return; }

        var leo = playerRoot.GetComponent<LeoPlayer>();
        if (leo != null) { leo.TakeDamage(waterDamage); return; }

        var maya = playerRoot.GetComponent<MayaPlayer>();
        if (maya != null) { maya.TakeDamage(waterDamage); return; }
    }

    private Vector3 CalculateEndLocalPos()
    {
        if (flowingWaterObject == null) return originalFlowingWaterLocalPos;

        Vector3 startWorldPos = flowingWaterObject.transform.parent.TransformPoint(originalFlowingWaterLocalPos);
        
        // Lấy hướng forward của dòng nước và triệt tiêu thành phần Y để nước trượt ngang phẳng (không đi chúc xuống)
        Vector3 moveDir = flowingWaterObject.transform.forward;
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude > 0.001f)
        {
            moveDir.Normalize();
        }
        else
        {
            moveDir = Vector3.forward;
        }

        Vector3 endWorldPos = startWorldPos + moveDir * flowTravelDistance;
        return flowingWaterObject.transform.parent.InverseTransformPoint(endWorldPos);
    }

    private IEnumerator AnimateWaterFlowing()
    {
        if (flowingWaterObject == null) yield break;

        Vector3 startLocalPos = originalFlowingWaterLocalPos;
        Vector3 endLocalPos = CalculateEndLocalPos();

        flowingWaterObject.transform.localPosition = startLocalPos;

        float elapsed = 0f;
        while (elapsed < flowDuration)
        {
            elapsed += Time.deltaTime;
            float pct = Mathf.Clamp01(elapsed / flowDuration);
            flowingWaterObject.transform.localPosition = Vector3.Lerp(startLocalPos, endLocalPos, pct);
            yield return null;
        }
        flowingWaterObject.transform.localPosition = endLocalPos;
    }
}
