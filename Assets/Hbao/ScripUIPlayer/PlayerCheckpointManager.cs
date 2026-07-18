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

    private void Start()
    {
        // Tự động quét và sắp xếp checkpoints theo chỉ số checkpointIndex nếu danh sách trống
        if (checkpoints == null || checkpoints.Count == 0)
        {
            checkpoints = new List<CheckpointZone>(FindObjectsOfType<CheckpointZone>());
            checkpoints.Sort((a, b) => a.checkpointIndex.CompareTo(b.checkpointIndex));
            Debug.Log($"[PlayerCheckpointManager] Đã tự động quét và sắp xếp {checkpoints.Count} Checkpoint(s) trong scene.");
        }
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
            return checkpointIndex == localPlayerCheckpointIndex;
        }

        // Lấy tên của người chơi local
        string localName = GetLocalPlayerName();
        if (string.IsNullOrEmpty(localName)) return false;

        // Quét qua danh sách đồng bộ mạng để tìm checkpoint tương ứng
        foreach (var data in networkPlayerCheckpoints)
        {
            if (data.PlayerName.ToString() == localName)
            {
                return data.CheckpointIndex == checkpointIndex;
            }
        }
        return false;
    }

    /// <summary>
    /// Đăng ký checkpoint mới khi người chơi đi vào vùng kích hoạt.
    /// </summary>
    public void RegisterCheckpoint(IPlayerHUDTarget player, CheckpointZone checkpoint)
    {
        if (player == null || checkpoint == null) return;

        // A. Chế độ chơi đơn
        if (player.isStandaloneMode)
        {
            if (localPlayerCheckpointIndex != checkpoint.checkpointIndex)
            {
                localPlayerCheckpointIndex = checkpoint.checkpointIndex;
                Debug.Log($"[Standalone Checkpoint] Đã lưu checkpoint '{checkpoint.gameObject.name}' (Index: {checkpoint.checkpointIndex}) cho người chơi chơi đơn!");
            }
            return;
        }

        // B. Chế độ chơi mạng (Chỉ xử lý trên Server)
        if (!IsServer) return;

        ulong clientId = player.OwnerClientId;
        string playerName = player.DisplayName;

        // Lưu vào cache Server-side bằng ClientId để xử lý hồi sinh nhanh
        playerCheckpointIndices[clientId] = checkpoint.checkpointIndex;

        // Cập nhật hoặc thêm mới vào NetworkList để đồng bộ xuống toàn bộ Client
        bool found = false;
        for (int i = 0; i < networkPlayerCheckpoints.Count; i++)
        {
            if (networkPlayerCheckpoints[i].PlayerName.ToString() == playerName)
            {
                networkPlayerCheckpoints[i] = new PlayerCheckpointData(playerName, checkpoint.checkpointIndex);
                found = true;
                break;
            }
        }

        if (!found)
        {
            networkPlayerCheckpoints.Add(new PlayerCheckpointData(playerName, checkpoint.checkpointIndex));
        }

        Debug.Log($"[Server Checkpoint] Đã đồng bộ checkpoint index {checkpoint.checkpointIndex} cho người chơi '{playerName}' (Client ID: {clientId})");
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

    private IEnumerator RespawnPlayerStandaloneCoroutine(IPlayerHUDTarget player)
    {
        localPlayerRespawning = true;
        Debug.LogWarning($"[Standalone Respawn] Người chơi '{player.DisplayName}' đã chết! Thực hiện hồi sinh ngay...");
        yield return null;

        // Xác định vị trí hồi sinh
        Vector3 spawnPos = localPlayerInitialPosition;
        if (defaultSpawnPoint != null)
        {
            spawnPos = defaultSpawnPoint.position;
        }

        if (localPlayerCheckpointIndex >= 0)
        {
            CheckpointZone activeZone = checkpoints.Find(c => c.checkpointIndex == localPlayerCheckpointIndex);
            if (activeZone != null)
            {
                spawnPos = activeZone.GetSpawnPosition();
            }
        }

        // Dịch chuyển người chơi và triệt tiêu vận tốc vật lý
        player.transform.position = spawnPos;
        ResetRigidbodyVelocity(player.gameObject);
        Debug.Log($"[Checkpoint Debug] Teleported player to {spawnPos}");

        // Hồi máu đầy và reset trạng thái hoạt ảnh
        HealAndResetPlayer(player);
        Debug.Log($"[Checkpoint Debug] HealAndResetPlayer completed");

        // Chờ thêm 1 frame để camera cập nhật vị trí mới theo player trước khi mở mắt
        yield return null;

        // Mở mắt (ResetDeathEffect)
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

            // 2. KHÔI PHỤC CHECKPOINT KHI KẾT NỐI LẠI (Reconnection hoặc Late joining)
            if (!playerCheckpointIndices.ContainsKey(clientId))
            {
                foreach (var data in networkPlayerCheckpoints)
                {
                    if (data.PlayerName.ToString() == playerName)
                    {
                        playerCheckpointIndices[clientId] = data.CheckpointIndex;
                        Debug.Log($"[Server Respawn] Đã phục hồi checkpoint index {data.CheckpointIndex} cho '{playerName}' (Client ID mới: {clientId}) sau khi kết nối lại.");
                        break;
                    }
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
        Debug.LogWarning($"[Server Respawn] Người chơi '{playerName}' (Client ID: {clientId}) đã chết! Thực hiện hồi sinh ngay...");
        yield return null;

        // Kiểm tra xem đối tượng người chơi còn tồn tại hay không
        if (player == null)
        {
            respawningPlayers.Remove(clientId);
            yield break;
        }

        // Xác định vị trí hồi sinh
        Vector3 spawnPos = playerInitialPositions.ContainsKey(clientId) ? playerInitialPositions[clientId] : player.transform.position;
        if (defaultSpawnPoint != null)
        {
            spawnPos = defaultSpawnPoint.position;
        }

        if (playerCheckpointIndices.ContainsKey(clientId))
        {
            int cpIdx = playerCheckpointIndices[clientId];
            CheckpointZone activeZone = checkpoints.Find(c => c.checkpointIndex == cpIdx);
            if (activeZone != null)
            {
                spawnPos = activeZone.GetSpawnPosition();
            }
        }

        // Dịch chuyển trên Server
        player.transform.position = spawnPos;
        ResetRigidbodyVelocity(player.gameObject);

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
            netObj.transform.position = pos;
            ResetRigidbodyVelocity(netObj.gameObject);

            // Buộc Animator chơi hoạt ảnh Idle cục bộ để đứng thẳng ngay lập tức
            Animator anim = netObj.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Play("Idle", 0, 0f);
                anim.ResetTrigger("Death");
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
        yield return null; // Chờ 1 frame để camera cập nhật vị trí mới theo player
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
        player.ResetDeathState();
        GameObject playerGo = player.gameObject;

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

                    // 3. Gán máu về tối đa
                    if (isStandalone)
                    {
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
                    }
                    else if (IsServer)
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

                    // 4. Reset hoạt ảnh về Idle
                    InvokePlayAnimationInherited(script, "Idle", 0.15f);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerCheckpointManager] Error resetting script {script.GetType().Name}: {ex}");
            }
        }

        Animator anim = playerGo.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Play("Idle", 0, 0f);
            anim.ResetTrigger("Death");
        }
    }

    #endregion
}
