using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [System.Serializable]
    public struct EnemySpawnConfig
    {
        public string enemyName;
        public GameObject enemyPrefab;
    }

    [Header("Enemy Prefab Configuration")]
    [Tooltip("List of enemy prefabs to register. Assign Enemy 1 to 5 here.")]
    public EnemySpawnConfig[] enemyConfigs = new EnemySpawnConfig[5];

    [Header("Enemy Spawn Points")]
    [Tooltip("Designated spawn locations in the scene. If empty, spawns at the spawner's position.")]
    public Transform[] spawnPoints;

    [Header("Player Spawning Configuration")]
    [Tooltip("Prefab of the player to spawn. Usually contains SimplePlayerTest script.")]
    public GameObject playerPrefab;
    [Tooltip("Where the player will spawn. If empty, uses this Spawner's position.")]
    public Transform playerSpawnPoint;
    [Tooltip("If true, automatically spawns a player object for connected clients if they don't have one.")]
    public bool autoSpawnPlayer = true;

    [Header("Spawning Settings")]
    [Tooltip("If true, automatically spawns enemies at start when the server/host loaded the scene.")]
    public bool autoSpawnOnStart = true;

    [Header("Hotkeys for Quick Testing (Host/Server only)")]
    [Tooltip("Enable keyboard hotkeys to dynamically spawn enemies during playtesting.")]
    public bool enableHotkeys = true;

    private void Start()
    {
#if UNITY_EDITOR
        // Tự động kiểm tra và tạo NetworkManager nếu thiếu khi test trực tiếp scene HBao trong Editor
        if (NetworkManager.Singleton == null)
        {
            CreateEditorNetworkManager();
        }

        if (playerPrefab == null)
        {
            Debug.LogWarning("[EnemySpawner] 'playerPrefab' chưa được gán trong Inspector! Vui lòng gán Player Prefab vào EnemySpawner để tự động sinh Player.");
        }
#endif

        // Listen for client connection to spawn player for late-joining clients
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

#if UNITY_EDITOR
            // Tự động bật Host nếu chưa có Network nào chạy (khi ấn Play trực tiếp scene HBao)
            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
            {
                Debug.Log("[EnemySpawner] Đang tự động khởi chạy NetworkManager dưới vai trò HOST để test game trực tiếp...");
                EnsurePrefabsRegistered(NetworkManager.Singleton);
                NetworkManager.Singleton.StartHost();
            }
#endif
        }
        else
        {
            Debug.LogError("[EnemySpawner] Không tìm thấy NetworkManager trong Scene! Hãy thêm NetworkManager để chạy Netcode.");
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Tự động tạo NetworkManager giả định phục vụ việc test nhanh trực tiếp trong Unity Editor
    /// </summary>
    private void CreateEditorNetworkManager()
    {
        Debug.Log("[EnemySpawner] Không tìm thấy NetworkManager trong Scene. Đang khởi tạo NetworkManager tạm thời để phục vụ playtest...");
        
        GameObject netManagerObj = new GameObject("NetworkManager_EditorDebug");
        NetworkManager netManager = netManagerObj.AddComponent<NetworkManager>();
        
        // Thêm UnityTransport mặc định
        var transport = netManagerObj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        
        // Khởi tạo NetworkConfig mặc định
        netManager.NetworkConfig = new NetworkConfig();
        netManager.NetworkConfig.NetworkTransport = transport;
        
        // Đăng ký các Prefab
        EnsurePrefabsRegistered(netManager);
    }

    /// <summary>
    /// Đảm bảo tất cả các Prefab Player và Enemy đều được đăng ký trong NetworkPrefabs của NetworkManager
    /// </summary>
    private void EnsurePrefabsRegistered(NetworkManager netManager)
    {
        if (netManager == null || netManager.NetworkConfig == null || netManager.NetworkConfig.Prefabs == null) return;

        System.Action<GameObject> registerIfMissing = (prefab) =>
        {
            if (prefab == null) return;
            if (prefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError($"[EnemySpawner] Prefab '{prefab.name}' thiếu thành phần NetworkObject! Không thể đăng ký.");
                return;
            }

            bool exists = false;
            if (netManager.NetworkConfig.Prefabs.Prefabs != null)
            {
                foreach (var netPrefab in netManager.NetworkConfig.Prefabs.Prefabs)
                {
                    if (netPrefab.Prefab == prefab)
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                netManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                Debug.Log($"[EnemySpawner] Đã tự động đăng ký Prefab '{prefab.name}' vào danh sách NetworkPrefabs.");
            }
        };

        // Đăng ký Player Prefab
        registerIfMissing(playerPrefab);

        // Đăng ký tất cả Enemy Prefab
        if (enemyConfigs != null)
        {
            foreach (var config in enemyConfigs)
            {
                registerIfMissing(config.enemyPrefab);
            }
        }
    }
#endif

    private void OnDestroy()
    {
        // Clean up connection event
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    public override void OnNetworkSpawn()
    {
        // Only the Server/Host manages spawning of networked objects
        if (!IsServer) return;

        // 1. Đăng ký sự kiện chuyển cảnh thành công để tự động sinh Player cho tất cả các Client khi load xong
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadEventCompleted;
        }

        // 2. Auto spawn player for existing clients on scene start (Đặc biệt hữu ích khi Editor playtest trực tiếp)
        if (autoSpawnPlayer && playerPrefab != null)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null)
                {
                    SpawnPlayerForClient(client.ClientId);
                }
            }
        }

        // 3. Auto spawn enemies
        if (autoSpawnOnStart)
        {
            SpawnAllConfiguredEnemies();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
            }
        }
    }

    private void OnSceneLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        if (!IsServer || !autoSpawnPlayer || playerPrefab == null) return;

        Debug.Log($"[EnemySpawner] Phát hiện Scene '{sceneName}' đã load xong cho toàn bộ clients. Tiến hành sinh Player.");

        foreach (ulong clientId in clientsCompleted)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                if (client.PlayerObject == null)
                {
                    SpawnPlayerForClient(clientId);
                }
                else
                {
                    Debug.Log($"[EnemySpawner] Client {clientId} đã có PlayerObject, bỏ qua không sinh trùng lặp.");
                }
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer && autoSpawnPlayer)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void Update()
    {
        // Hotkey triggers are only evaluated on the Server/Host (or local testing in Host mode)
        if (!IsServer || !enableHotkeys) return;

        // Press '1' to spawn Enemy index 0
        if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnEnemyAtIndex(0);
        // Press '2' to spawn Enemy index 1
        if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnEnemyAtIndex(1);
        // Press '3' to spawn Enemy index 2
        if (Input.GetKeyDown(KeyCode.Alpha3)) SpawnEnemyAtIndex(2);
        // Press '4' to spawn Enemy index 3
        if (Input.GetKeyDown(KeyCode.Alpha4)) SpawnEnemyAtIndex(3);
        // Press '5' to spawn Enemy index 4
        if (Input.GetKeyDown(KeyCode.Alpha5)) SpawnEnemyAtIndex(4);
        
        // Press 'G' to spawn all enemies at once
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("[EnemySpawner] Hotkey 'G' pressed. Spawning all configured enemies!");
            SpawnAllConfiguredEnemies();
        }

        // Press 'P' to spawn/respawn player for Host
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("[EnemySpawner] Hotkey 'P' pressed. Spawning player for Host client.");
            SpawnPlayerForClient(NetworkManager.Singleton.LocalClientId);
        }
    }

    /// <summary>
    /// Spawns a Player object for a specific client ID and registers it as their player object.
    /// </summary>
    public void SpawnPlayerForClient(ulong clientId)
    {
        if (!IsServer || playerPrefab == null) return;

        // Double check if client already has a player object registered
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                Debug.Log($"[EnemySpawner] Client {clientId} already has a player object assigned.");
                return;
            }
        }

        Vector3 pos = playerSpawnPoint != null ? playerSpawnPoint.position : transform.position;
        Quaternion rot = playerSpawnPoint != null ? playerSpawnPoint.rotation : transform.rotation;

        GameObject playerObj = Instantiate(playerPrefab, pos, rot);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[EnemySpawner] Spawned Player for Client ID {clientId} successfully.");
        }
        else
        {
            Debug.LogError("[EnemySpawner] Player Prefab is missing a NetworkObject component!");
            Destroy(playerObj);
        }
    }

    /// <summary>
    /// Spawns all configured enemies at the available spawn points.
    /// </summary>
    public void SpawnAllConfiguredEnemies()
    {
        if (!IsServer) return;

        for (int i = 0; i < enemyConfigs.Length; i++)
        {
            SpawnEnemyAtIndex(i);
        }
    }

    /// <summary>
    /// Spawns a specific enemy from the configs array.
    /// </summary>
    /// <param name="index">Index of the enemy prefab in the config list.</param>
    public void SpawnEnemyAtIndex(int index)
    {
        if (!IsServer) return;

        if (index < 0 || index >= enemyConfigs.Length)
        {
            Debug.LogWarning($"[EnemySpawner] Spawn index {index} is out of range.");
            return;
        }

        EnemySpawnConfig config = enemyConfigs[index];
        if (config.enemyPrefab == null)
        {
            Debug.LogWarning($"[EnemySpawner] No prefab assigned for enemy config at index {index} ({config.enemyName}).");
            return;
        }

        // Determine spawn position and rotation
        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = transform.rotation;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Pick a spawn point based on the index (or wrap around if fewer points than enemies)
            int pointIndex = index % spawnPoints.Length;
            if (spawnPoints[pointIndex] != null)
            {
                spawnPosition = spawnPoints[pointIndex].position;
                spawnRotation = spawnPoints[pointIndex].rotation;
            }
        }

        // Offset slightly to prevent exact overlapping if spawned at the exact same location
        spawnPosition += new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));

        // Instantiate and Spawn across the network
        GameObject enemyObj = Instantiate(config.enemyPrefab, spawnPosition, spawnRotation);
        
        NetworkObject netObj = enemyObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
            Debug.Log($"[EnemySpawner] Spawned {config.enemyName} successfully at {spawnPosition}.");
        }
        else
        {
            Debug.LogError($"[EnemySpawner] Prefab '{config.enemyPrefab.name}' is missing a NetworkObject component! Cannot spawn on Netcode.");
            Destroy(enemyObj);
        }
    }
}
