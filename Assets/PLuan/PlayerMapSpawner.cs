using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class PlayerMapSpawner : NetworkBehaviour
{
    [Header("Player Prefabs Configuration")]
    [Tooltip("Leo Prefab - Sát Thủ (ID = 0)")]
    [SerializeField] private GameObject leoPrefab;
    
    [Tooltip("Maya Prefab - Hỏa Thuật (ID = 1)")]
    [SerializeField] private GameObject mayaPrefab;
    
    [Tooltip("Elena Prefab - Cung Thủ (ID = 2)")]
    [SerializeField] private GameObject elenaPrefab;
    
    [Tooltip("Arthur Prefab - Tanker (ID = 3)")]
    [SerializeField] private GameObject arthurPrefab;

    [Tooltip("Prefab mặc định nếu không khớp ID nào")]
    [SerializeField] private GameObject defaultPlayerPrefab;

    [Header("Spawn Points Configuration")]
    [Tooltip("Kéo thả 4 điểm xuất phát trên bản đồ vào đây. Nếu bỏ trống, script tự động tìm trong scene theo tag hoặc tên.")]
    [SerializeField] private Transform[] spawnPoints = new Transform[4];

    // Lưu trữ ánh xạ từ ClientId sang chỉ số điểm spawn để tránh trùng lặp
    private Dictionary<ulong, int> clientSpawnIndices = new Dictionary<ulong, int>();

    public enum NetworkDebugStartMode
    {
        None,
        Host,
        Server,
        Client
    }

    [Header("Debug / Quick Test Settings")]
    [Tooltip("Prefab của NetworkManager. Nếu NetworkManager.Singleton null trong Editor, prefab này sẽ được Instantiate.")]
    [SerializeField] private GameObject networkManagerPrefab;
    [Tooltip("Tự động khởi chạy mạng khi chạy thử trực tiếp Scene này trong Editor")]
    [SerializeField] private bool autoStartNetworkInEditor = true;
    [Tooltip("Chế độ mạng muốn test (Host = Server + Client, Server = Dedicated Server, Client = Kết nối vào server)")]
    [SerializeField] private NetworkDebugStartMode debugStartMode = NetworkDebugStartMode.Client;
    [Tooltip("Địa chỉ IP để kết nối khi test (165.99.14.40 để kết nối thẳng tới VPS)")]
    [SerializeField] private string debugConnectAddress = "165.99.14.40";
    [Tooltip("Nhân vật muốn test nhanh (0 = Leo, 1 = Maya, 2 = Elena, 3 = Arthur)")]
    [SerializeField] private int debugCharacterId = 3; // Arthur mặc định cho test cận chiến

    private void Start()
    {
#if UNITY_EDITOR
        if (autoStartNetworkInEditor && NetworkManager.Singleton == null)
        {
            if (networkManagerPrefab != null)
            {
                Debug.Log("[PlayerMapSpawner] [DEBUG] Không tìm thấy NetworkManager trong scene. Đang khởi tạo từ prefab...");
                Instantiate(networkManagerPrefab);
            }
            else
            {
                Debug.LogWarning("[PlayerMapSpawner] [DEBUG] NetworkManager.Singleton bị null và networkManagerPrefab chưa được gán. Hãy kéo prefab NetworkManager từ Project/MainMenu vào ô networkManagerPrefab của PlayerMapSpawner trong scene Map.");
            }
        }

        if (autoStartNetworkInEditor && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            // Ép cấu hình UnityTransport về IP cục bộ chỉ định để tránh tự động kết nối ra VPS ngoài
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
            {
                string targetAddress = string.IsNullOrEmpty(debugConnectAddress) ? "127.0.0.1" : debugConnectAddress;
                transport.ConnectionData.Address = targetAddress;
                Debug.Log($"[PlayerMapSpawner] [DEBUG] Đã ép địa chỉ kết nối của UnityTransport về: {targetAddress}");
            }

            PlayerPrefs.SetInt("SelectedCharacterId", debugCharacterId);
            PlayerPrefs.Save();

            switch (debugStartMode)
            {
                case NetworkDebugStartMode.Host:
                    Debug.Log($"[PlayerMapSpawner] [DEBUG] Tự động khởi động HOST (Server+Client) và đặt SelectedCharacterId = {debugCharacterId}");
                    NetworkManager.Singleton.StartHost();
                    break;
                case NetworkDebugStartMode.Server:
                    Debug.Log("[PlayerMapSpawner] [DEBUG] Tự động khởi động DEDICATED SERVER (Không spawn nhân vật tại máy này).");
                    NetworkManager.Singleton.StartServer();
                    break;
                case NetworkDebugStartMode.Client:
                    Debug.Log($"[PlayerMapSpawner] [DEBUG] Tự động khởi động CLIENT và đặt SelectedCharacterId = {debugCharacterId}");
                    NetworkManager.Singleton.StartClient();
                    break;
            }
        }
#endif
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadEventCompleted;
            }
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

                // Tự động kích hoạt cơ chế spawn dự phòng cho mọi Client đã kết nối sẵn trong phòng
                if (NetworkManager.Singleton.ConnectedClientsList != null)
                {
                    foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                    {
                        StartCoroutine(DelayedAutoSpawnForClient(client.ClientId));
                    }
                }
            }
        }

        if (IsClient)
        {
            StartCoroutine(ClientRequestSpawnCoroutine());
        }
    }

    private IEnumerator ClientRequestSpawnCoroutine()
    {
        // Đợi 0.2s để scene load và NetworkObject đồng bộ hoàn toàn
        yield return new WaitForSeconds(0.2f);

        int selectedChar = PlayerPrefs.GetInt("SelectedCharacterId", 0);

        Vector3 customPos = SaveManager.PendingSpawnPosition;
        float customRotY = SaveManager.PendingSpawnRotationY;
        bool hasCustomPos = SaveManager.HasPendingSpawnPosition && (customPos != Vector3.zero);

        Debug.Log($"[PlayerMapSpawner] [CLIENT] Gửi ServerRpc yêu cầu sinh Player cho Client {NetworkManager.Singleton.LocalClientId} (Nhân vật ID: {selectedChar}, HasCustomPos: {hasCustomPos}, Pos: {customPos})");
        RequestSpawnPlayerServerRpc(selectedChar, customPos, customRotY, hasCustomPos);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
            }
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[PlayerMapSpawner] [SERVER] Client {clientId} kết nối tới Server. Đợi Client gửi RequestSpawnPlayerServerRpc...");
    }

    private void OnSceneLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;

        Debug.Log($"[PlayerMapSpawner] [SERVER] Scene '{sceneName}' tải xong cho {clientsCompleted?.Count ?? 0} clients.");
        
        if (clientsCompleted != null)
        {
            foreach (var clientId in clientsCompleted)
            {
                StartCoroutine(DelayedAutoSpawnForClient(clientId));
            }
        }
    }

    private IEnumerator DelayedAutoSpawnForClient(ulong clientId)
    {
        // Đợi 2.5 giây cho Client gửi RequestSpawnPlayerServerRpc
        yield return new WaitForSeconds(2.5f);

        if (!IsServer) yield break;
        if (NetworkManager.Singleton == null) yield break;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject == null)
            {
                Debug.LogWarning($"[PlayerMapSpawner] [SERVER] Client {clientId} chưa có PlayerObject sau thời gian chờ. Tự động fallback spawn...");
                SpawnPlayerForClient(clientId, 0);
            }
            else
            {
                Debug.Log($"[PlayerMapSpawner] [SERVER] Client {clientId} đã có PlayerObject hợp lệ. Bỏ qua fallback.");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPlayerServerRpc(int characterId, Vector3 customPos = default, float customRotY = 0f, bool hasCustomPos = false, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerMapSpawner] [SERVER] Nhận yêu cầu spawn từ Client {clientId} cho Nhân vật ID: {characterId} (HasCustomPos: {hasCustomPos}, Pos: {customPos})");

        SpawnPlayerForClient(clientId, characterId, customPos, customRotY, hasCustomPos);
    }

    public void SpawnPlayerForClient(ulong clientId, int characterId, Vector3 customPos = default, float customRotY = 0f, bool hasCustomPos = false)
    {
        if (!IsServer) return;
        StartCoroutine(SpawnPlayerCoroutine(clientId, characterId, customPos, customRotY, hasCustomPos));
    }

    private IEnumerator SpawnPlayerCoroutine(ulong clientId, int characterId, Vector3 customPos, float customRotY, bool hasCustomPos)
    {
        Debug.Log($"[PlayerMapSpawner] [SERVER] Bắt đầu coroutine sinh nhân vật cho Client {clientId} (ID: {characterId}, HasCustomPos: {hasCustomPos}, Pos: {customPos})");

        // 1. Xác định Prefab cần spawn
        GameObject selectedPrefab = GetPlayerPrefab(characterId);
        if (selectedPrefab == null)
        {
            Debug.LogError($"[PlayerMapSpawner] [SERVER] Không thể spawn! Chưa gán Prefab cho nhân vật ID {characterId} và không có prefab mặc định.");
            yield break;
        }

        // 2. Thu hồi PlayerObject cũ (nếu có) và đợi 1 frame để Netcode dọn sạch liên kết cũ
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientConnection))
        {
            if (clientConnection.PlayerObject != null)
            {
                Debug.Log($"[PlayerMapSpawner] [SERVER] Thu hồi PlayerObject cũ của Client {clientId}...");
                NetworkObject oldPlayerObj = clientConnection.PlayerObject;
                if (oldPlayerObj != null)
                {
                    if (oldPlayerObj.IsSpawned)
                    {
                        oldPlayerObj.Despawn(true);
                    }
                    else
                    {
                        Destroy(oldPlayerObj.gameObject);
                    }
                }
                yield return null; // Chờ 1 frame để NetworkManager cập nhật clientConnection.PlayerObject = null
            }
        }

        // 3. Xác định vị trí spawn
        Vector3 spawnPos;
        Quaternion spawnRot;

        bool isValidCustomPos = hasCustomPos && (customPos != Vector3.zero) && (customPos.sqrMagnitude > 10f);

        if (isValidCustomPos)
        {
            spawnPos = customPos + new Vector3(0, 0.5f, 0);
            spawnRot = Quaternion.Euler(0, customRotY, 0);
            Debug.Log($"[PlayerMapSpawner] [SERVER] Client {clientId} (Tiếp Tục) sử dụng vị trí lưu: {spawnPos}");
        }
        else
        {
            Transform spawnPoint = GetSpawnPointForClient(clientId);
            if (spawnPoint != null)
            {
                spawnPos = spawnPoint.position;
                spawnRot = spawnPoint.rotation;
                Debug.Log($"[PlayerMapSpawner] [SERVER] Client {clientId} (Chơi Mới) sử dụng SpawnPoint ban đầu: {spawnPoint.name} tại vị trí: {spawnPos}");
            }
            else
            {
                spawnPos = transform.position;
                spawnRot = transform.rotation;
                Debug.LogWarning($"[PlayerMapSpawner] [SERVER] Client {clientId} KHÔNG tìm thấy SpawnPoint hợp lệ! Fallback về vị trí Spawner: {spawnPos}");
            }

            spawnPos += new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, Random.Range(-0.2f, 0.2f));
        }

        // 4. Khởi tạo và Spawn trên Network
        GameObject playerObj = Instantiate(selectedPrefab, spawnPos, spawnRot);
        Rigidbody rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[PlayerMapSpawner] [SERVER] Đã spawn thành công gameplay Player cho Client {clientId} với prefab '{selectedPrefab.name}' tại {spawnPos}");

            // Gửi ClientRpc chỉ đích danh Client sở hữu để ép cập nhật đúng vị trí Slot/Save
            NotifySpawnPositionClientRpc(spawnPos, spawnRot.eulerAngles.y, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });
        }
        else
        {
            Debug.LogError($"[PlayerMapSpawner] [SERVER] Thất bại! Prefab '{selectedPrefab.name}' thiếu thành phần NetworkObject.");
            Destroy(playerObj);
        }
    }

    [ClientRpc]
    private void NotifySpawnPositionClientRpc(Vector3 pos, float rotY, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[PlayerMapSpawner] [CLIENT] Nhận lệnh đặt vị trí spawn từ Server: {pos}");
        StartCoroutine(ApplySpawnPositionCoroutine(pos, rotY));
    }

    private IEnumerator ApplySpawnPositionCoroutine(Vector3 pos, float rotY)
    {
        for (int i = 0; i < 15; i++)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localPlayerObj != null)
                {
                    localPlayerObj.transform.position = pos;
                    localPlayerObj.transform.rotation = Quaternion.Euler(0, rotY, 0);

                    var rb = localPlayerObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.position = pos;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    Debug.Log($"[PlayerMapSpawner] [CLIENT] Đã đặt vị trí nhân vật thành công tại slot: {pos}");
                    yield break;
                }
            }
            yield return null;
        }
    }

    private GameObject GetPlayerPrefab(int characterId)
    {
        switch (characterId)
        {
            case 0: return leoPrefab != null ? leoPrefab : defaultPlayerPrefab;
            case 1: return mayaPrefab != null ? mayaPrefab : defaultPlayerPrefab;
            case 2: return elenaPrefab != null ? elenaPrefab : defaultPlayerPrefab;
            case 3: return arthurPrefab != null ? arthurPrefab : defaultPlayerPrefab;
            default: return defaultPlayerPrefab;
        }
    }

    private Transform GetSpawnPointForClient(ulong clientId)
    {
        // Kiểm tra xem danh sách spawnPoints có hợp lệ không
        bool hasSpawnPoints = false;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            foreach (var sp in spawnPoints)
            {
                if (sp != null)
                {
                    hasSpawnPoints = true;
                    break;
                }
            }
        }

        // Tự động tìm kiếm nếu chưa được gán hoặc bị rỗng/chứa phần tử null
        if (!hasSpawnPoints)
        {
            Debug.Log("[PlayerMapSpawner] [SERVER] spawnPoints bị rỗng hoặc rỗng một phần. Tiến hành tìm kiếm động...");
            FindSpawnPointsDynamically();
        }

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Phân bổ điểm spawn cho client
            if (!clientSpawnIndices.TryGetValue(clientId, out int spawnIdx))
            {
                spawnIdx = clientSpawnIndices.Count;
                clientSpawnIndices[clientId] = spawnIdx;
            }

            int index = spawnIdx % spawnPoints.Length;
            if (spawnPoints[index] != null)
            {
                return spawnPoints[index];
            }

            // Fallback: Tìm điểm spawn đầu tiên không null
            foreach (var sp in spawnPoints)
            {
                if (sp != null) return sp;
            }
        }

        return null;
    }

    private void FindSpawnPointsDynamically()
    {
        // 1. Thử tìm kiếm theo Tag "SpawnPoint"
        GameObject[] taggedPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (taggedPoints != null && taggedPoints.Length > 0)
        {
            spawnPoints = new Transform[taggedPoints.Length];
            for (int i = 0; i < taggedPoints.Length; i++)
            {
                spawnPoints[i] = taggedPoints[i].transform;
            }
            Debug.Log($"[PlayerMapSpawner] Tự động tìm thấy {spawnPoints.Length} điểm spawn có Tag 'SpawnPoint'.");
            return;
        }

        // 2. Thử tìm kiếm theo Tên (bao gồm định dạng ngoặc đơn SpawnPoint (1))
        List<Transform> foundByName = new List<Transform>();
        for (int i = 1; i <= 10; i++)
        {
            GameObject go = GameObject.Find($"SpawnPoint ({i})") ?? 
                            GameObject.Find($"SpawnPoint({i})") ?? 
                            GameObject.Find($"SpawnPoint {i}") ?? 
                            GameObject.Find($"SpawnPoint_{i}") ?? 
                            GameObject.Find($"Spawn Point {i}") ??
                            GameObject.Find($"Spawn_{i}") ??
                            GameObject.Find($"Spawn {i}");
            if (go != null)
            {
                foundByName.Add(go.transform);
            }
        }

        // 3. Nếu vẫn không tìm thấy, quét tất cả Transform trong Scene có chứa từ khóa "SpawnPoint" (trừ chính đối tượng Spawner này)
        if (foundByName.Count == 0)
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t != transform && t.name.Contains("SpawnPoint") && t.name != "SpawnPoints")
                {
                    foundByName.Add(t);
                }
            }
        }

        if (foundByName.Count > 0)
        {
            spawnPoints = foundByName.ToArray();
            Debug.Log($"[PlayerMapSpawner] Tự động tìm thấy {spawnPoints.Length} điểm spawn bằng cách quét động tên đối tượng.");
        }
        else
        {
            Debug.LogWarning("[PlayerMapSpawner] Không tìm thấy điểm spawn nào trong Scene! Player sẽ được spawn tại vị trí của Spawner.");
        }
    }
}
