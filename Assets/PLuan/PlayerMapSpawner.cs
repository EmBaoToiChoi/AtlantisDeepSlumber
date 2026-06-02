using UnityEngine;
using Unity.Netcode;
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

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            // Lấy nhân vật đã chọn từ PlayerPrefs (đã được lưu ở waiting room)
            int selectedChar = PlayerPrefs.GetInt("SelectedCharacterId", 0);
            Debug.Log($"[PlayerMapSpawner] [CLIENT] Đã load cảnh gameplay. Gửi yêu cầu sinh Player cho Client {NetworkManager.Singleton.LocalClientId} (Nhân vật ID: {selectedChar})");
            
            // Gửi yêu cầu ServerRpc để server thực hiện spawn
            RequestSpawnPlayerServerRpc(selectedChar);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPlayerServerRpc(int characterId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerMapSpawner] [SERVER] Nhận yêu cầu spawn từ Client {clientId} cho Nhân vật ID: {characterId}");

        SpawnPlayerForClient(clientId, characterId);
    }

    private void SpawnPlayerForClient(ulong clientId, int characterId)
    {
        if (!IsServer) return;

        // 1. Xác định Prefab cần spawn
        GameObject selectedPrefab = GetPlayerPrefab(characterId);
        if (selectedPrefab == null)
        {
            Debug.LogError($"[PlayerMapSpawner] [SERVER] Không thể spawn! Chưa gán Prefab cho nhân vật ID {characterId} và không có prefab mặc định.");
            return;
        }

        // 2. Thu hồi PlayerObject cũ (nếu có - ví dụ lobby avatar mang sang từ waiting hall)
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientConnection))
        {
            if (clientConnection.PlayerObject != null)
            {
                // Nếu đã có nhân vật gameplay chính thức rồi thì bỏ qua không spawn lại
                if (clientConnection.PlayerObject.GetComponent<SimplePlayerTest>() != null)
                {
                    Debug.Log($"[PlayerMapSpawner] [SERVER] Client {clientId} đã có nhân vật gameplay chính thức. Bỏ qua spawn.");
                    return;
                }

                Debug.Log($"[PlayerMapSpawner] [SERVER] Phát hiện Client {clientId} có PlayerObject cũ (Lobby Avatar). Đang thu hồi...");
                NetworkObject oldPlayerObj = clientConnection.PlayerObject;
                if (oldPlayerObj.IsSpawned)
                {
                    oldPlayerObj.Despawn(true);
                }
                else
                {
                    Destroy(oldPlayerObj.gameObject);
                }
            }
        }

        // 3. Xác định vị trí spawn
        Transform spawnPoint = GetSpawnPointForClient(clientId);
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        // Thêm một chút offset ngẫu nhiên nhỏ để tránh các người chơi đè lên nhau chính xác tuyệt đối
        spawnPos += new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f));

        // 4. Khởi tạo và Spawn trên Network
        GameObject playerObj = Instantiate(selectedPrefab, spawnPos, spawnRot);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[PlayerMapSpawner] [SERVER] Đã spawn thành công gameplay Player cho Client {clientId} với prefab '{selectedPrefab.name}' tại {spawnPos}");
        }
        else
        {
            Debug.LogError($"[PlayerMapSpawner] [SERVER] Thất bại! Prefab '{selectedPrefab.name}' thiếu thành phần NetworkObject.");
            Destroy(playerObj);
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

        // Tự động tìm kiếm nếu chưa được gán
        if (!hasSpawnPoints)
        {
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

        // 2. Thử tìm kiếm theo Tên
        List<Transform> foundByName = new List<Transform>();
        for (int i = 1; i <= 10; i++)
        {
            GameObject go = GameObject.Find($"SpawnPoint{i}") ?? 
                            GameObject.Find($"SpawnPoint_{i}") ?? 
                            GameObject.Find($"Spawn Point {i}") ??
                            GameObject.Find($"Spawn_{i}") ??
                            GameObject.Find($"Spawn {i}");
            if (go != null)
            {
                foundByName.Add(go.transform);
            }
        }

        if (foundByName.Count > 0)
        {
            spawnPoints = foundByName.ToArray();
            Debug.Log($"[PlayerMapSpawner] Tự động tìm thấy {spawnPoints.Length} điểm spawn theo mẫu Tên.");
        }
        else
        {
            Debug.LogWarning("[PlayerMapSpawner] Không tìm thấy điểm spawn nào trong Scene! Player sẽ được spawn tại vị trí của Spawner.");
        }
    }
}
