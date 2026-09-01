using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Quản lý trung tâm cho hệ thống Checkpoint và Hồi sinh của người chơi.
/// Hỗ trợ tất cả các lớp nhân vật (Leo, Arthur, Elena, Maya) thông qua interface IPlayerHUDTarget.
/// </summary>
public class PlayerCheckpointManager : NetworkBehaviour
{
    /// <summary>
    /// Cấu trúc dữ liệu đồng bộ checkpoint của người chơi qua mạng.
    /// Dùng PlayerName làm khóa để nhận diện chuẩn xác ngay cả khi bị mất kết nối và kết nối lại (Client ID bị thay đổi).
    /// </summary>
    public struct PlayerCheckpointData : INetworkSerializable, System.IEquatable<PlayerCheckpointData>
    {
        public Unity.Collections.FixedString64Bytes PlayerName;
        public int CheckpointIndex;

        public PlayerCheckpointData(string playerName, int checkpointIndex)
        {
            PlayerName = playerName;
            CheckpointIndex = checkpointIndex;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref CheckpointIndex);
        }

        public bool Equals(PlayerCheckpointData other)
        {
            return PlayerName.Equals(other.PlayerName) && CheckpointIndex == other.CheckpointIndex;
        }
    }

    public static PlayerCheckpointManager Instance { get; private set; }

    [Header("Checkpoint Setup")]
    [Tooltip("Danh sách các khu vực Checkpoint. Nếu để trống, hệ thống sẽ tự động quét các CheckpointZone trong scene.")]
    public List<CheckpointZone> checkpoints = new List<CheckpointZone>();

    [Tooltip("Điểm hồi sinh mặc định nếu người chơi chưa chạm vào bất kỳ checkpoint nào.")]
    public Transform defaultSpawnPoint;

    [Tooltip("Thời gian chờ (giây) sau khi chết để bắt đầu hồi sinh.")]
    public float respawnDelay = 2.5f;

    // --- Đồng bộ Mạng (Unity Netcode) ---
    // Danh sách checkpoint đồng bộ tự động từ Server xuống toàn bộ Client
    private NetworkList<PlayerCheckpointData> networkPlayerCheckpoints;

    // Bộ nhớ tạm trên Server lưu Client ID ứng với index checkpoint
    private Dictionary<ulong, int> playerCheckpointIndices = new Dictionary<ulong, int>();
    // Bộ nhớ tạm trên Server lưu vị trí xuất phát ban đầu của Client ID
    private Dictionary<ulong, Vector3> playerInitialPositions = new Dictionary<ulong, Vector3>();
    // Danh sách Client ID đang chờ hồi sinh
    private HashSet<ulong> respawningPlayers = new HashSet<ulong>();
    private int globalLatestCheckpointIndex = -1;

    // Cache vị trí checkpoint ĐÃ KÍCH HOẠT (player đã đi qua) - dùng cho respawn
    private Dictionary<int, Vector3> cachedCheckpointPositions = new Dictionary<int, Vector3>();
    // Lookup vị trí TẤT CẢ checkpoint trong scene (dùng để tìm vị trí khi cần)
    private Dictionary<int, Vector3> allCheckpointPositions = new Dictionary<int, Vector3>();
    // Flag đánh dấu đã có ít nhất 1 checkpoint được kích hoạt trong session này
    private bool hasActivatedAnyCheckpoint = false;

    // --- Chế độ Chơi Đơn (Standalone) ---
    private int localPlayerCheckpointIndex = -1;
    private Vector3 localPlayerInitialPosition = Vector3.zero;
    private bool localPlayerRespawning = false;
    private bool hasStoredLocalInitialPos = false;

    private Dictionary<ulong, float> playerDeathTimes = new Dictionary<ulong, float>();
    private float localPlayerDeathTime = 0f;
    private bool hasLocalPlayerDied = false;

    private void Awake()
    {
        // Khởi tạo NetworkList trong Awake
        networkPlayerCheckpoints = new NetworkList<PlayerCheckpointData>(
            null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Khi scene được load (kể cả khi play lại từ đầu), reset toàn bộ checkpoint state.
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Debug.Log($"[PlayerCheckpointManager] Scene '{scene.name}' loaded (mode: {mode}). Resetting all checkpoint data.");
        ResetAllCheckpointData();
    }

    private void Start()
    {
        // Reset toàn bộ dữ liệu checkpoint khi bắt đầu chơi mới
        ResetAllCheckpointData();

        // Tự động quét và sắp xếp checkpoints theo chỉ số checkpointIndex nếu danh sách trống
        if (checkpoints == null || checkpoints.Count == 0)
        {
            RescanCheckpoints();
        }
        else
        {
            // Cache vị trí tất cả checkpoints đã được gán sẵn trong Inspector
            CacheAllCheckpointPositions();
        }
    }

    /// <summary>
    /// Reset toàn bộ trạng thái checkpoint về mặc định.
    /// Gọi khi bắt đầu game mới hoặc khi scene được load lại.
    /// </summary>
    public void ResetAllCheckpoints()
    {
        Debug.Log("[PlayerCheckpointManager] ResetAllCheckpoints() called externally.");
        ResetAllCheckpointData();
    }

    /// <summary>
    /// Internal: Xóa sạch toàn bộ dữ liệu checkpoint trong bộ nhớ.
    /// Không ảnh hưởng đến danh sách CheckpointZone (vì chúng là object trong scene).
    /// </summary>
    private void ResetAllCheckpointData()
    {
        // Reset checkpoint index về -1 (chưa chạm checkpoint nào)
        localPlayerCheckpointIndex = -1;
        globalLatestCheckpointIndex = -1;
        hasActivatedAnyCheckpoint = false;

        // Reset trạng thái respawn standalone
        localPlayerRespawning = false;
        hasStoredLocalInitialPos = false;
        localPlayerInitialPosition = Vector3.zero;
        hasLocalPlayerDied = false;
        localPlayerDeathTime = 0f;

        // Xóa cache vị trí checkpoint ĐÃ KÍCH HOẠT (quan trọng: KHÔNG xóa allCheckpointPositions)
        cachedCheckpointPositions.Clear();

        // Xóa dữ liệu network checkpoint
        playerCheckpointIndices.Clear();
        playerInitialPositions.Clear();
        respawningPlayers.Clear();
        playerDeathTimes.Clear();

        // Xóa NetworkList nếu là Server
        try
        {
            if (networkPlayerCheckpoints != null && IsServer)
            {
                networkPlayerCheckpoints.Clear();
            }
        }
        catch (System.Exception) { /* Bỏ qua nếu chưa spawn network */ }

        Debug.Log("[PlayerCheckpointManager] All checkpoint data has been reset. hasActivatedAnyCheckpoint=false");
    }

    private void RescanCheckpoints()
    {
        checkpoints = new List<CheckpointZone>(FindObjectsByType<CheckpointZone>(FindObjectsSortMode.None));
        if (checkpoints.Count == 0)
        {
            var parentCp = GameObject.Find("Checkpoint") ?? GameObject.Find("QuanTrong/Checkpoint");
            if (parentCp != null)
            {
                int idx = 0;
                for (int i = 0; i < parentCp.transform.childCount; i++)
                {
                    var child = parentCp.transform.GetChild(i);
                    var cz = child.GetComponent<CheckpointZone>();
                    if (cz == null)
                    {
                        cz = child.gameObject.AddComponent<CheckpointZone>();
                        cz.checkpointIndex = idx++;
                        cz.spawnPointOverride = child;
                        cz.radius = 15f;
                    }
                    checkpoints.Add(cz);
                }
            }
        }
        checkpoints.Sort((a, b) => a.checkpointIndex.CompareTo(b.checkpointIndex));
        CacheAllCheckpointPositions();
        Debug.Log($"[PlayerCheckpointManager] Đã tự động quét và sắp xếp {checkpoints.Count} Checkpoint(s) trong scene.");
    }

    /// <summary>
    /// Lưu vị trí hồi sinh của tất cả CheckpointZone vào lookup dictionary (allCheckpointPositions).
    /// KHÔNG ghi vào cachedCheckpointPositions (chỉ checkpoint ĐÃ KÍCH HOẠT mới được cache).
    /// </summary>
    private void CacheAllCheckpointPositions()
    {
        allCheckpointPositions.Clear();
        foreach (var cp in checkpoints)
        {
            if (cp != null)
            {
                allCheckpointPositions[cp.checkpointIndex] = cp.GetSpawnPosition();
            }
        }
        Debug.Log($"[PlayerCheckpointManager] Cached {allCheckpointPositions.Count} checkpoint positions into lookup table.");
    }

    private void Update()
    {
        // 1. Chế độ Chơi đơn (Standalone)
        if (IsStandaloneMode())
        {
            HandleStandaloneUpdate();
        }
        // 2. Chế độ Chơi mạng (Chỉ chạy giám sát máu trên Server)
        else if (IsServer)
        {
            HandleNetworkUpdate();
        }
    }

    /// <summary>
    /// Kiểm tra xem Checkpoint có đang hoạt động đối với Client Local hay không.
    /// </summary>
    public bool IsCheckpointActiveForLocalPlayer(int checkpointIndex)
    {
        if (IsStandaloneMode())
        {
            return checkpointIndex == localPlayerCheckpointIndex || checkpointIndex == globalLatestCheckpointIndex;
        }

        // Lấy tên của người chơi local
        string localName = GetLocalPlayerName();
        if (!string.IsNullOrEmpty(localName))
        {
            // Quét qua danh sách đồng bộ mạng để tìm checkpoint tương ứng
            foreach (var data in networkPlayerCheckpoints)
            {
                if (data.PlayerName.ToString() == localName)
                {
                    return data.CheckpointIndex == checkpointIndex;
                }
            }
        }

        if (globalLatestCheckpointIndex >= 0)
        {
            return checkpointIndex == globalLatestCheckpointIndex;
        }

        return false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (networkPlayerCheckpoints != null)
        {
            networkPlayerCheckpoints.OnListChanged += OnNetworkCheckpointsChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (networkPlayerCheckpoints != null)
        {
            networkPlayerCheckpoints.OnListChanged -= OnNetworkCheckpointsChanged;
        }
    }

    private void OnNetworkCheckpointsChanged(NetworkListEvent<PlayerCheckpointData> changeEvent)
    {
        string localName = GetLocalPlayerName();
        foreach (var data in networkPlayerCheckpoints)
        {
            // Đánh dấu đã có checkpoint được kích hoạt
            hasActivatedAnyCheckpoint = true;
            if (data.CheckpointIndex > globalLatestCheckpointIndex)
            {
                globalLatestCheckpointIndex = data.CheckpointIndex;
            }
            if (!string.IsNullOrEmpty(localName) && data.PlayerName.ToString() == localName)
            {
                if (data.CheckpointIndex > localPlayerCheckpointIndex)
                {
                    localPlayerCheckpointIndex = data.CheckpointIndex;
                }
            }
        }
    }

    /// <summary>
    /// Đăng ký checkpoint mới khi 1 người chơi đi vào vùng kích hoạt.
    /// Checkpoint này sẽ được đồng bộ áp dụng cho TOÀN BỘ người chơi trong đội.
    /// </summary>
    public void RegisterCheckpoint(IPlayerHUDTarget player, CheckpointZone checkpoint)
    {
        if (checkpoint == null) return;

        // Không đăng ký checkpoint nếu player đang chết hoặc đang respawn
        if (player != null && player.CurrentHealth <= 0)
        {
            Debug.Log($"[Checkpoint] Bỏ qua checkpoint '{checkpoint.gameObject.name}' vì player đang chết.");
            return;
        }
        if (localPlayerRespawning)
        {
            Debug.Log($"[Checkpoint] Bỏ qua checkpoint '{checkpoint.gameObject.name}' vì player đang respawn.");
            return;
        }

        int newIndex = checkpoint.checkpointIndex;

        // Cache vị trí checkpoint ĐÃ KÍCH HOẠT
        cachedCheckpointPositions[newIndex] = checkpoint.GetSpawnPosition();
        // Cập nhật lookup table
        allCheckpointPositions[newIndex] = checkpoint.GetSpawnPosition();
        // Đánh dấu đã có checkpoint được kích hoạt
        hasActivatedAnyCheckpoint = true;

        // Luôn cập nhật chỉ số checkpoint cục bộ và checkpoint mới nhất
        if (newIndex > localPlayerCheckpointIndex)
        {
            localPlayerCheckpointIndex = newIndex;
        }
        if (newIndex > globalLatestCheckpointIndex || globalLatestCheckpointIndex < 0)
        {
            globalLatestCheckpointIndex = newIndex;
        }

        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (isNetwork)
        {
            if (!IsServer)
            {
                RegisterCheckpointServerRpc(newIndex);
            }
            else
            {
                RegisterCheckpointInternal(newIndex, player != null ? player.DisplayName : "");
            }
        }
        else
        {
            Debug.Log($"[Standalone Checkpoint] Đã lưu checkpoint '{checkpoint.gameObject.name}' (Index: {newIndex}) tại {cachedCheckpointPositions[newIndex]} cho người chơi!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterCheckpointServerRpc(int checkpointIndex, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[Server Checkpoint] Nhận yêu cầu lưu checkpoint index {checkpointIndex} từ Client ID {senderId}");
        RegisterCheckpointInternal(checkpointIndex, $"Client {senderId}");
    }

    private void RegisterCheckpointInternal(int newCpIdx, string activatorName)
    {
        if (newCpIdx > globalLatestCheckpointIndex || globalLatestCheckpointIndex < 0)
        {
            globalLatestCheckpointIndex = newCpIdx;
        }

        int targetIndex = globalLatestCheckpointIndex >= 0 ? globalLatestCheckpointIndex : newCpIdx;

        // Cache vị trí checkpoint ngay lập tức để không bị mất khi CheckpointZone bị destroy
        CacheCheckpointPosition(targetIndex);

        // Cập nhật checkpoint mới cho TOÀN BỘ người chơi hiện có trong phòng
        List<IPlayerHUDTarget> activePlayers = FindAllActivePlayers();
        foreach (var p in activePlayers)
        {
            if (p == null) continue;

            ulong cId = p.OwnerClientId;
            string pName = p.DisplayName;

            // Lưu vào cache Server-side bằng ClientId
            playerCheckpointIndices[cId] = targetIndex;

            // Cập nhật hoặc thêm mới vào NetworkList để đồng bộ xuống toàn bộ Client
            bool found = false;
            for (int i = 0; i < networkPlayerCheckpoints.Count; i++)
            {
                if (networkPlayerCheckpoints[i].PlayerName.ToString() == pName)
                {
                    networkPlayerCheckpoints[i] = new PlayerCheckpointData(pName, targetIndex);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                networkPlayerCheckpoints.Add(new PlayerCheckpointData(pName, targetIndex));
            }
        }

        // Cập nhật bổ sung cho tất cả Client ID đang kết nối
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds != null)
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                playerCheckpointIndices[clientId] = targetIndex;
            }
        }

        // Lấy vị trí checkpoint để gửi tới Client qua RPC
        Vector3 cpPos = Vector3.zero;
        if (cachedCheckpointPositions.ContainsKey(targetIndex))
        {
            cpPos = cachedCheckpointPositions[targetIndex];
        }
        else if (allCheckpointPositions.ContainsKey(targetIndex))
        {
            cpPos = allCheckpointPositions[targetIndex];
            // Đồng bộ ngược lại vào cache để lần sau không bị miss
            cachedCheckpointPositions[targetIndex] = cpPos;
        }

        // Phát ClientRpc thông báo đồng bộ checkpoint index mới + vị trí tới toàn bộ Client
        SyncCheckpointClientRpc(targetIndex, cpPos);

        Debug.Log($"[Server Checkpoint] Đã đồng bộ checkpoint index {targetIndex} (pos: {cpPos}) cho TOÀN BỘ người chơi do '{activatorName}' kích hoạt!");
    }

    /// <summary>
    /// Cache vị trí checkpoint theo index từ danh sách CheckpointZone.
    /// </summary>
    private void CacheCheckpointPosition(int cpIndex)
    {
        // Ưu tiên lấy từ CheckpointZone còn tồn tại
        if (checkpoints != null)
        {
            foreach (var cp in checkpoints)
            {
                if (cp != null && cp.checkpointIndex == cpIndex)
                {
                    Vector3 pos = cp.GetSpawnPosition();
                    cachedCheckpointPositions[cpIndex] = pos;
                    allCheckpointPositions[cpIndex] = pos;
                    return;
                }
            }
        }

        // Thử tìm lại trong scene nếu không có trong list
        var allZones = FindObjectsByType<CheckpointZone>(FindObjectsSortMode.None);
        foreach (var zone in allZones)
        {
            if (zone != null && zone.checkpointIndex == cpIndex)
            {
                Vector3 pos = zone.GetSpawnPosition();
                cachedCheckpointPositions[cpIndex] = pos;
                allCheckpointPositions[cpIndex] = pos;
                return;
            }
        }
    }

    [ClientRpc]
    private void SyncCheckpointClientRpc(int checkpointIndex, Vector3 checkpointPosition)
    {
        if (checkpointIndex > globalLatestCheckpointIndex || globalLatestCheckpointIndex < 0)
        {
            globalLatestCheckpointIndex = checkpointIndex;
        }
        if (checkpointIndex > localPlayerCheckpointIndex)
        {
            localPlayerCheckpointIndex = checkpointIndex;
        }
        // Đánh dấu đã có checkpoint được kích hoạt
        hasActivatedAnyCheckpoint = true;
        if (checkpointPosition != Vector3.zero)
        {
            cachedCheckpointPositions[checkpointIndex] = checkpointPosition;
            allCheckpointPositions[checkpointIndex] = checkpointPosition;
            SaveManager.SaveWorldSave(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, checkpointPosition, 0f);
        }
        Debug.Log($"[Client Checkpoint] Đã cập nhật checkpoint index {checkpointIndex} (pos: {checkpointPosition}) trên Client từ Server.");
    }

    #region Standalone Respawn Logic

    private void HandleStandaloneUpdate()
    {
        // Quét và tìm local player
        IPlayerHUDTarget localPlayer = FindLocalStandalonePlayer();
        if (localPlayer == null) return;

        // Lưu vị trí xuất phát ban đầu nếu chưa lưu
        if (!hasStoredLocalInitialPos)
        {
            localPlayerInitialPosition = localPlayer.transform.position;
            hasStoredLocalInitialPos = true;
            Debug.Log($"[Standalone Checkpoint] Đã lưu vị trí xuất phát ban đầu: {localPlayerInitialPosition}");
        }

        // Giám sát máu người chơi
        if (localPlayer.CurrentHealth <= 0)
        {
            if (!hasLocalPlayerDied)
            {
                localPlayerDeathTime = Time.time;
                hasLocalPlayerDied = true;
                Debug.Log($"[Checkpoint Debug] Standalone Local Player DIED. Death time: {localPlayerDeathTime}");
            }

            bool timeOut = (Time.time - localPlayerDeathTime) > 6f;
            if ((localPlayer.IsDeathAnimationFinished || timeOut) && !localPlayerRespawning)
            {
                Debug.Log($"[Checkpoint Debug] Triggering Respawn. IsDeathAnimationFinished={localPlayer.IsDeathAnimationFinished}, timeOut={timeOut}");
                StartCoroutine(RespawnPlayerStandaloneCoroutine(localPlayer));
            }
        }
        else
        {
            hasLocalPlayerDied = false;
        }
    }

    private Vector3 GetCalculatedSpawnPosition(IPlayerHUDTarget player, ulong clientId)
    {
        // Quét lại checkpoints nếu danh sách trống hoặc tất cả entries đều null
        if (checkpoints == null || checkpoints.Count == 0 || checkpoints.TrueForAll(c => c == null))
        {
            RescanCheckpoints();
        }

        int targetCpIndex = -1;

        // CHỈ tìm checkpoint nếu đã có ít nhất 1 checkpoint được kích hoạt trong session này
        if (hasActivatedAnyCheckpoint)
        {
            if (player != null && player.isStandaloneMode)
            {
                // Standalone: ưu tiên checkpoint cao nhất giữa local và global
                targetCpIndex = Mathf.Max(localPlayerCheckpointIndex, globalLatestCheckpointIndex);
            }
            else
            {
                // Network: lấy checkpoint index từ server-side cache
                if (playerCheckpointIndices.ContainsKey(clientId) && playerCheckpointIndices[clientId] >= 0)
                {
                    targetCpIndex = playerCheckpointIndices[clientId];
                }

                // Nếu globalLatestCheckpointIndex mới hơn hoặc bằng checkpoint của player, ưu tiên dùng checkpoint chung mới nhất của đội
                if (globalLatestCheckpointIndex >= 0 && (targetCpIndex < 0 || globalLatestCheckpointIndex >= targetCpIndex))
                {
                    targetCpIndex = globalLatestCheckpointIndex;
                }
            }
        }

        Debug.Log($"[Checkpoint Respawn] Đang tính vị trí hồi sinh cho clientId={clientId}, " +
                  $"targetCpIndex={targetCpIndex}, globalLatest={globalLatestCheckpointIndex}, " +
                  $"localCp={localPlayerCheckpointIndex}, hasActivated={hasActivatedAnyCheckpoint}, " +
                  $"activatedCacheCount={cachedCheckpointPositions.Count}");

        // === NẾU CHƯA CÓ CHECKPOINT NÀO ĐƯỢC KÍCH HOẠT → về defaultSpawnPoint ngay ===
        if (!hasActivatedAnyCheckpoint || targetCpIndex < 0)
        {
            // Ưu tiên 1: defaultSpawnPoint
            if (defaultSpawnPoint != null)
            {
                Debug.Log($"[Checkpoint Respawn] Chưa kích hoạt checkpoint nào. Dùng defaultSpawnPoint tại {defaultSpawnPoint.position}");
                return defaultSpawnPoint.position;
            }

            // Ưu tiên 2: Vị trí ban đầu của player
            if (player != null && player.isStandaloneMode && hasStoredLocalInitialPos && localPlayerInitialPosition != Vector3.zero)
            {
                Debug.Log($"[Checkpoint Respawn] Chưa kích hoạt checkpoint nào. Dùng standalone initial position tại {localPlayerInitialPosition}");
                return localPlayerInitialPosition;
            }
            if (player != null && !player.isStandaloneMode && playerInitialPositions.ContainsKey(clientId) && playerInitialPositions[clientId] != Vector3.zero)
            {
                Debug.Log($"[Checkpoint Respawn] Chưa kích hoạt checkpoint nào. Dùng network initial position tại {playerInitialPositions[clientId]}");
                return playerInitialPositions[clientId];
            }

            // Ưu tiên 3: Checkpoint index 0 (điểm đầu tiên)
            if (checkpoints != null && checkpoints.Count > 0 && checkpoints[0] != null)
            {
                Debug.Log($"[Checkpoint Respawn] Chưa kích hoạt checkpoint nào. Dùng checkpoint đầu tiên (index 0).");
                return checkpoints[0].GetSpawnPosition();
            }

            // Ưu tiên 4: Quét lại scene tìm checkpoint
            RescanCheckpoints();
            if (checkpoints != null && checkpoints.Count > 0 && checkpoints[0] != null)
            {
                Debug.LogWarning($"[Checkpoint Respawn] Đã quét lại scene, dùng checkpoint đầu tiên.");
                return checkpoints[0].GetSpawnPosition();
            }

            // KHÔNG BAO GIỜ trả về player.transform.position (vị trí chết)
            Debug.LogWarning($"[Checkpoint Respawn] Chưa kích hoạt checkpoint nào và không tìm thấy defaultSpawnPoint! Trả về vị trí an toàn.");
            return Vector3.up * 2f;
        }

        // === ĐÃ CÓ CHECKPOINT KÍCH HOẠT → tìm vị trí checkpoint ===

        // 1. CheckpointZone tương ứng với targetCpIndex (ưu tiên object còn sống)
        if (targetCpIndex >= 0)
        {
            CheckpointZone zone = checkpoints.Find(c => c != null && c.checkpointIndex == targetCpIndex);
            if (zone != null)
            {
                Vector3 pos = zone.GetSpawnPosition();
                Debug.Log($"[Checkpoint Respawn] Sử dụng CheckpointZone index {targetCpIndex} tại {pos}");
                return pos;
            }

            // Fallback: dùng vị trí đã cache (checkpoint ĐÃ KÍCH HOẠT) nếu CheckpointZone bị destroy/null
            if (cachedCheckpointPositions.ContainsKey(targetCpIndex) && cachedCheckpointPositions[targetCpIndex] != Vector3.zero)
            {
                Vector3 pos = cachedCheckpointPositions[targetCpIndex];
                Debug.Log($"[Checkpoint Respawn] CheckpointZone index {targetCpIndex} đã bị destroy, sử dụng vị trí đã cache: {pos}");
                return pos;
            }

            // Fallback: dùng lookup table nếu có
            if (allCheckpointPositions.ContainsKey(targetCpIndex) && allCheckpointPositions[targetCpIndex] != Vector3.zero)
            {
                Vector3 pos = allCheckpointPositions[targetCpIndex];
                Debug.Log($"[Checkpoint Respawn] Sử dụng allCheckpointPositions lookup index {targetCpIndex} tại {pos}");
                return pos;
            }
        }

        // 2. Dự phòng: Checkpoint ĐÃ KÍCH HOẠT có index cao nhất
        if (cachedCheckpointPositions.Count > 0)
        {
            int highestIndex = -1;
            Vector3 highestPos = Vector3.zero;
            foreach (var kvp in cachedCheckpointPositions)
            {
                if (kvp.Key > highestIndex && kvp.Value != Vector3.zero)
                {
                    highestIndex = kvp.Key;
                    highestPos = kvp.Value;
                }
            }
            if (highestIndex >= 0)
            {
                Debug.Log($"[Checkpoint Respawn] Fallback: highest ACTIVATED checkpoint index {highestIndex} tại {highestPos}");
                return highestPos;
            }
        }

        // 3. Dự phòng cuối: defaultSpawnPoint
        if (defaultSpawnPoint != null)
        {
            Debug.LogWarning($"[Checkpoint Respawn] Fallback cuối: defaultSpawnPoint tại {defaultSpawnPoint.position}");
            return defaultSpawnPoint.position;
        }

        // 4. Dự phòng: Vị trí ban đầu xuất phát của player
        if (player != null && player.isStandaloneMode && hasStoredLocalInitialPos && localPlayerInitialPosition != Vector3.zero)
        {
            Debug.LogWarning($"[Checkpoint Respawn] Fallback: standalone initial position tại {localPlayerInitialPosition}");
            return localPlayerInitialPosition;
        }
        if (player != null && !player.isStandaloneMode && playerInitialPositions.ContainsKey(clientId) && playerInitialPositions[clientId] != Vector3.zero)
        {
            Debug.LogWarning($"[Checkpoint Respawn] Fallback: network initial position tại {playerInitialPositions[clientId]}");
            return playerInitialPositions[clientId];
        }

        // 5. Quét lại scene tìm checkpoint bất kỳ
        RescanCheckpoints();
        if (checkpoints != null && checkpoints.Count > 0)
        {
            foreach (var cp in checkpoints)
            {
                if (cp != null)
                {
                    Debug.LogWarning($"[Checkpoint Respawn] Emergency rescan: dùng checkpoint index {cp.checkpointIndex}");
                    return cp.GetSpawnPosition();
                }
            }
        }

        // KHÔNG BAO GIỜ trả về player.transform.position (vị trí chết) - trả về vị trí an toàn
        Debug.LogError($"[Checkpoint Respawn] KHÔNG TÌM THẤY CHECKPOINT NÀO! Trả về vị trí an toàn (0, 2, 0).");
        return Vector3.up * 2f;
    }

    private void TeleportPlayerSafely(GameObject go, Vector3 pos)
    {
        if (go == null) return;

        // Tắt CharacterController tạm thời nếu có để gán transform.position không bị cưỡng chế đè lại
        CharacterController cc = go.GetComponent<CharacterController>();
        if (cc == null) cc = go.GetComponentInChildren<CharacterController>();
        if (cc == null) cc = go.GetComponentInParent<CharacterController>();

        bool wasCcEnabled = false;
        if (cc != null)
        {
            wasCcEnabled = cc.enabled;
            cc.enabled = false;
        }

        // Triệt tiêu vận tốc Rigidbody
        ResetRigidbodyVelocity(go);

        // Gán vị trí mới
        go.transform.position = pos;

        // Gọi NetworkTransform.Teleport nếu có component
        var netTransform = go.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform == null) netTransform = go.GetComponentInChildren<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform == null) netTransform = go.GetComponentInParent<Unity.Netcode.Components.NetworkTransform>();

        if (netTransform != null && netTransform.IsSpawned)
        {
            try
            {
                netTransform.Teleport(pos, go.transform.rotation, go.transform.localScale);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerCheckpointManager] NetworkTransform.Teleport warning: {ex.Message}");
            }
        }

        // Bật lại CharacterController
        if (cc != null && wasCcEnabled)
        {
            cc.enabled = true;
        }
    }

    private IEnumerator RespawnPlayerStandaloneCoroutine(IPlayerHUDTarget player)
    {
        localPlayerRespawning = true;
        Debug.LogWarning($"[Standalone Respawn] Người chơi '{player.DisplayName}' đã chết! Chờ {respawnDelay}s trước khi hồi sinh...");

        // Chờ respawnDelay để đảm bảo checkpoint data đã được cache đầy đủ
        yield return new WaitForSeconds(respawnDelay);

        // Kiểm tra player còn tồn tại sau khi chờ
        if (player == null || player.gameObject == null)
        {
            localPlayerRespawning = false;
            yield break;
        }

        // Xác định vị trí hồi sinh Checkpoint chuẩn xác
        Vector3 spawnPos = GetCalculatedSpawnPosition(player, 0);

        // Dịch chuyển an toàn (tắt CC, reset RB, gọi Teleport)
        TeleportPlayerSafely(player.gameObject, spawnPos);
        Debug.Log($"[Checkpoint Debug] Teleported player to {spawnPos}");

        // Hồi máu đầy và reset trạng thái hoạt ảnh
        HealAndResetPlayer(player);
        Debug.Log($"[Checkpoint Debug] HealAndResetPlayer completed");

        // Chờ 2 frame để camera và Animator cập nhật tư thế đứng Idle dưới màn hình đen
        yield return null;
        yield return new WaitForEndOfFrame();

        // Mở mắt (ResetDeathEffect) khi nhân vật đã sẵn sàng đứng ở Checkpoint
        PlayerDeathEffectManager.Instance.ResetDeathEffect();
        Debug.Log($"[Checkpoint Debug] ResetDeathEffect called. Eyes opened.");

        localPlayerRespawning = false;
        Debug.Log($"[Standalone Respawn] Hồi sinh hoàn tất cho '{player.DisplayName}' tại: {spawnPos}");
    }

    #endregion

    #region Network Respawn Logic

    private void HandleNetworkUpdate()
    {
        // Quét toàn bộ người chơi hợp lệ (Leo, Arthur, Elena, Maya, SimplePlayerTest) có trong màn chơi
        List<IPlayerHUDTarget> players = FindAllActivePlayers();
        foreach (var player in players)
        {
            if (player == null || player.isStandaloneMode) continue;

            ulong clientId = player.OwnerClientId;
            string playerName = player.DisplayName;

            // 1. Lưu vị trí xuất phát ban đầu của client nếu chưa có trong cache
            if (!playerInitialPositions.ContainsKey(clientId))
            {
                playerInitialPositions[clientId] = player.transform.position;
                Debug.Log($"[Server Respawn] Đã lưu vị trí ban đầu cho Client ID {clientId} ({playerName}): {player.transform.position}");
            }

            // 2. KHÔI PHỤC HOẶC GÁN CHECKPOINT KHI KẾT NỐI LẠI (Reconnection hoặc Late joining)
            if (!playerCheckpointIndices.ContainsKey(clientId))
            {
                bool foundInList = false;
                foreach (var data in networkPlayerCheckpoints)
                {
                    if (data.PlayerName.ToString() == playerName)
                    {
                        playerCheckpointIndices[clientId] = data.CheckpointIndex;
                        foundInList = true;
                        Debug.Log($"[Server Respawn] Đã phục hồi checkpoint index {data.CheckpointIndex} cho '{playerName}' (Client ID mới: {clientId}) sau khi kết nối lại.");
                        break;
                    }
                }

                if (!foundInList && globalLatestCheckpointIndex >= 0)
                {
                    playerCheckpointIndices[clientId] = globalLatestCheckpointIndex;
                    networkPlayerCheckpoints.Add(new PlayerCheckpointData(playerName, globalLatestCheckpointIndex));
                    Debug.Log($"[Server Respawn] Đã gán checkpoint chung index {globalLatestCheckpointIndex} cho người chơi mới '{playerName}' (Client ID: {clientId}).");
                }
            }

            // 3. Kiểm tra máu và kích hoạt hồi sinh trên Server
            if (player.CurrentHealth <= 0)
            {
                if (!playerDeathTimes.ContainsKey(clientId))
                {
                    playerDeathTimes[clientId] = Time.time;
                }

                bool timeOut = (Time.time - playerDeathTimes[clientId]) > 6f;
                if ((player.IsDeathAnimationFinished || timeOut) && !respawningPlayers.Contains(clientId))
                {
                    respawningPlayers.Add(clientId);
                    StartCoroutine(RespawnPlayerNetworkCoroutine(player, clientId));
                }
            }
            else
            {
                if (playerDeathTimes.ContainsKey(clientId))
                {
                    playerDeathTimes.Remove(clientId);
                }
            }
        }
    }

    private IEnumerator RespawnPlayerNetworkCoroutine(IPlayerHUDTarget player, ulong clientId)
    {
        string playerName = player.DisplayName;
        Debug.LogWarning($"[Server Respawn] Người chơi '{playerName}' (Client ID: {clientId}) đã chết! Chờ {respawnDelay}s trước khi hồi sinh...");

        // Chờ respawnDelay để đảm bảo checkpoint data đã được đồng bộ đầy đủ
        yield return new WaitForSeconds(respawnDelay);

        // Kiểm tra xem đối tượng người chơi còn tồn tại hay không
        if (player == null)
        {
            respawningPlayers.Remove(clientId);
            yield break;
        }

        // Xác định vị trí hồi sinh Checkpoint chuẩn xác
        Vector3 spawnPos = GetCalculatedSpawnPosition(player, clientId);

        // Dịch chuyển trên Server an toàn
        TeleportPlayerSafely(player.gameObject, spawnPos);

        // Gửi ClientRpc dịch chuyển và reset vật lý trên tất cả các Client khác
        TeleportPlayerClientRpc(player.gameObject.GetComponent<NetworkObject>().NetworkObjectId, spawnPos);

        // Hồi máu đầy và reset trạng thái hoạt ảnh trên Server
        HealAndResetPlayer(player);

        respawningPlayers.Remove(clientId);
        Debug.Log($"[Server Respawn] Đã hồi sinh '{playerName}' (Client ID: {clientId}) thành công tại: {spawnPos}");
    }

    /// <summary>
    /// Đồng bộ dịch chuyển và triệt tiêu vận tốc vật lý trên client để tránh lỗi giật lag vị trí (rubber-banding).
    /// </summary>
    [ClientRpc]
    private void TeleportPlayerClientRpc(ulong networkObjectId, Vector3 pos)
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            TeleportPlayerSafely(netObj.gameObject, pos);

            // Reset trạng thái choáng trên client
            var stun = netObj.GetComponent<PlayerKickedStun>() ?? netObj.GetComponentInChildren<PlayerKickedStun>();
            if (stun != null)
            {
                stun.ResetStunState();
            }

            // Đảm bảo bật lại script điều khiển của Player trên client
            MonoBehaviour[] scripts = netObj.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script == null) continue;
                string typeName = script.GetType().Name;
                if (script is SimplePlayerTest || script is LeoPlayer || script is ArthurPlayer || 
                    script is ElenaPlayer || script is MayaPlayer || typeName.EndsWith("Player"))
                {
                    if (!script.enabled)
                    {
                        script.enabled = true;
                        Debug.LogWarning($"[Client Teleport] Đã kích hoạt lại script {script.GetType().Name} của người chơi!");
                    }
                }
            }

            // Reset toàn bộ trạng thái chết (currentAnimState, triggers, health) trên client
            var playerTarget = netObj.GetComponent<IPlayerHUDTarget>();
            if (playerTarget != null)
            {
                HealAndResetPlayer(playerTarget);
            }

            // Nếu đây là người chơi của chúng ta, mở mắt (ResetDeathEffect) sau khi chờ 1 frame để camera cập nhật vị trí
            if (netObj.IsOwner)
            {
                StartCoroutine(OpenEyesAfterFrameCoroutine());
            }
        }
    }

    private IEnumerator OpenEyesAfterFrameCoroutine()
    {
        // 1. Reset trạng thái chết cục bộ và chuyển Animator về Idle trước bên dưới màn hình đen
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (mb is LeoPlayer || mb is ArthurPlayer || mb is ElenaPlayer || mb is MayaPlayer || typeName.EndsWith("Player"))
            {
                PropertyInfo isOwnerProp = mb.GetType().GetProperty("IsOwner", BindingFlags.Public | BindingFlags.Instance);
                if (isOwnerProp != null && (bool)isOwnerProp.GetValue(mb))
                {
                    MethodInfo resetMethod = mb.GetType().GetMethod("ResetDeathState", BindingFlags.Public | BindingFlags.Instance);
                    if (resetMethod != null)
                    {
                        resetMethod.Invoke(mb, null);
                        Debug.Log($"[Checkpoint Client] Đã reset trạng thái chết cục bộ cho Owner Player: {mb.name}");
                    }
                }
            }
        }

        // 2. Chờ 2 frame để Camera và Animator cập nhật tư thế đứng Idle dưới màn hình đen
        yield return null;
        yield return new WaitForEndOfFrame();

        // 3. Mở mắt UI (ResetDeathEffect) khi nhân vật đã sẵn sàng đứng ở Checkpoint
        PlayerDeathEffectManager.Instance.ResetDeathEffect();
    }

    #endregion

    #region Helper Methods

    private bool IsStandaloneMode()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return true;
        }

        IPlayerHUDTarget localPlayer = FindLocalStandalonePlayer();
        if (localPlayer != null && localPlayer.isStandaloneMode)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Quét toàn bộ MonoBehaviour trong scene có thực thi interface IPlayerHUDTarget để chọn ra các người chơi phù hợp nhất.
    /// </summary>
    private List<IPlayerHUDTarget> FindAllActivePlayers()
    {
        List<IPlayerHUDTarget> activePlayers = new List<IPlayerHUDTarget>();
        HashSet<GameObject> uniqueGameObjects = new HashSet<GameObject>();

        MonoBehaviour[] monos = FindObjectsOfType<MonoBehaviour>();
        foreach (var mono in monos)
        {
            if (mono == null) continue;
            if (mono is IPlayerHUDTarget)
            {
                uniqueGameObjects.Add(mono.gameObject);
            }
        }

        foreach (var go in uniqueGameObjects)
        {
            IPlayerHUDTarget targetComponent = null;

            // Thứ tự ưu tiên lớp nhân vật chi tiết trước
            targetComponent = go.GetComponent<LeoPlayer>();
            if (targetComponent == null) targetComponent = go.GetComponent<ArthurPlayer>();
            if (targetComponent == null) targetComponent = go.GetComponent<ElenaPlayer>();
            if (targetComponent == null) targetComponent = go.GetComponent<MayaPlayer>();

            // Sử dụng SimplePlayerTest nếu không có lớp nhân vật chi tiết nào
            if (targetComponent == null) targetComponent = go.GetComponent<SimplePlayerTest>();

            // Dự phòng cuối cùng
            if (targetComponent == null) targetComponent = go.GetComponent<IPlayerHUDTarget>();

            if (targetComponent != null && !activePlayers.Contains(targetComponent))
            {
                activePlayers.Add(targetComponent);
            }
        }

        return activePlayers;
    }

    private IPlayerHUDTarget FindLocalStandalonePlayer()
    {
        List<IPlayerHUDTarget> players = FindAllActivePlayers();
        foreach (var p in players)
        {
            if (p != null && p.isStandaloneMode)
            {
                return p;
            }
        }
        return null;
    }

    private string GetLocalPlayerName()
    {
        List<IPlayerHUDTarget> players = FindAllActivePlayers();
        foreach (var p in players)
        {
            if (p != null && p.IsOwner)
            {
                return p.DisplayName;
            }
        }
        return PlayerPrefs.GetString("AuthDisplayName", "Explorer");
    }

    private void ResetRigidbodyVelocity(GameObject go)
    {
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }

    /// <summary>
    /// Sử dụng Reflection để hồi đầy máu cho tất cả các loại lớp nhân vật 
    /// (SimplePlayerTest, LeoPlayer, ArthurPlayer, ElenaPlayer, MayaPlayer)
    /// và phục hồi trạng thái hoạt ảnh.
    /// </summary>
    private FieldInfo GetFieldInherited(System.Type type, string name, BindingFlags flags)
    {
        System.Type currentType = type;
        while (currentType != null)
        {
            FieldInfo field = currentType.GetField(name, flags);
            if (field != null) return field;
            currentType = currentType.BaseType;
        }
        return null;
    }

    private PropertyInfo GetPropertyInherited(System.Type type, string name, BindingFlags flags)
    {
        System.Type currentType = type;
        while (currentType != null)
        {
            PropertyInfo prop = currentType.GetProperty(name, flags);
            if (prop != null) return prop;
            currentType = currentType.BaseType;
        }
        return null;
    }

    private MethodInfo GetMethodInherited(System.Type type, string name, BindingFlags flags)
    {
        System.Type currentType = type;
        while (currentType != null)
        {
            MethodInfo method = currentType.GetMethod(name, flags);
            if (method != null) return method;
            currentType = currentType.BaseType;
        }
        return null;
    }

    private void InvokePlayAnimationInherited(MonoBehaviour script, string animName, float fadeTime)
    {
        MethodInfo playAnimMethod = GetMethodInherited(script.GetType(), "PlayAnimation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (playAnimMethod != null)
        {
            var parameters = playAnimMethod.GetParameters();
            object[] args = new object[parameters.Length];
            if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string))
            {
                args[0] = animName;
            }
            if (parameters.Length > 1 && parameters[1].ParameterType == typeof(float))
            {
                args[1] = fadeTime;
            }
            for (int i = 2; i < parameters.Length; i++)
            {
                args[i] = parameters[i].DefaultValue;
            }
            try
            {
                playAnimMethod.Invoke(script, args);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerCheckpointManager] Error invoking PlayAnimation: {ex}");
            }
        }
    }

    /// <summary>
    /// Sử dụng Reflection để hồi đầy máu cho tất cả các loại lớp nhân vật 
    /// (SimplePlayerTest, LeoPlayer, ArthurPlayer, ElenaPlayer, MayaPlayer)
    /// và phục hồi trạng thái hoạt ảnh.
    /// </summary>
    private void HealAndResetPlayer(IPlayerHUDTarget player)
    {
        if (player == null) return;
        GameObject playerGo = player.gameObject;

        // Reset trạng thái choáng vật lý nếu có
        var stun = playerGo.GetComponent<PlayerKickedStun>() ?? playerGo.GetComponentInChildren<PlayerKickedStun>();
        if (stun != null)
        {
            stun.ResetStunState();
        }

        MonoBehaviour[] scripts = playerGo.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;
            try
            {
                string typeName = script.GetType().Name;

                if (script is SimplePlayerTest || script is LeoPlayer || script is ArthurPlayer || 
                    script is ElenaPlayer || script is MayaPlayer || typeName.EndsWith("Player"))
                {
                    // Đảm bảo bật lại script điều khiển của Player nếu bị tắt trước đó
                    if (!script.enabled)
                    {
                        script.enabled = true;
                        Debug.LogWarning($"[PlayerCheckpointManager] Đã bật lại script {script.GetType().Name} của người chơi!");
                    }

                    // 1. Lấy lượng máu tối đa (maxHealth)
                    float maxHp = 100f;
                    FieldInfo maxHealthField = GetFieldInherited(script.GetType(), "maxHealth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (maxHealthField != null)
                    {
                        maxHp = (float)maxHealthField.GetValue(script);
                    }

                    // 2. Kiểm tra chế độ Standalone
                    bool isStandalone = false;
                    FieldInfo standaloneField = GetFieldInherited(script.GetType(), "isStandaloneMode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (standaloneField != null)
                    {
                        isStandalone = (bool)standaloneField.GetValue(script);
                    }
                    else
                    {
                        PropertyInfo standaloneProp = GetPropertyInherited(script.GetType(), "IsStandaloneMode", BindingFlags.Public | BindingFlags.Instance);
                        if (standaloneProp != null)
                        {
                            isStandalone = (bool)standaloneProp.GetValue(script);
                        }
                    }

                    // 3. Gán máu về tối đa TRƯỚC KHI reset trạng thái chết (cho cả Standalone lẫn Network)
                    FieldInfo localHealthField = GetFieldInherited(script.GetType(), "localHealth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (localHealthField != null)
                    {
                        localHealthField.SetValue(script, maxHp);
                    }

                    MethodInfo updateHudMethod = GetMethodInherited(script.GetType(), "UpdateHealthHUD", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (updateHudMethod != null)
                    {
                        updateHudMethod.Invoke(script, new object[] { maxHp });
                    }

                    if (!isStandalone && IsServer)
                    {
                        FieldInfo currentHealthField = GetFieldInherited(script.GetType(), "currentHealth", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (currentHealthField != null)
                        {
                            object netVarObj = currentHealthField.GetValue(script);
                            if (netVarObj != null)
                            {
                                PropertyInfo valueProp = netVarObj.GetType().GetProperty("Value");
                                if (valueProp != null)
                                {
                                    valueProp.SetValue(netVarObj, maxHp);
                                }
                            }
                        }
                    }

                    // 4. Reset trạng thái chết
                    script.GetType().GetMethod("ResetDeathState", BindingFlags.Public | BindingFlags.Instance)?.Invoke(script, null);

                    // 5. Reset hoạt ảnh về Idle
                    InvokePlayAnimationInherited(script, "Idle", 0.15f);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerCheckpointManager] Error resetting script {script.GetType().Name}: {ex}");
            }
        }

        player.ResetDeathState();

        Animator anim = playerGo.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.ResetTrigger("Death");
            anim.ResetTrigger("GetHit");
            anim.ResetTrigger("GeiHit2");
            if (anim.layerCount > 1)
            {
                anim.SetLayerWeight(1, 0f);
                anim.Play("New State", 1, 0f);
            }
            anim.Play("Idle", 0, 0f);
            anim.Update(0f);
        }
    }

    #endregion
}
