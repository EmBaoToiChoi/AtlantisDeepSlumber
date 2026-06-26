using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class WoodLogObjectPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("Số lượng gỗ khởi tạo ban đầu")]
    public int initialPoolSize = 20;

    private GameObject woodPrefab;
    public GameObject WoodPrefab => woodPrefab;
    private Queue<GameObject> poolQueue = new Queue<GameObject>();
    private HashSet<GameObject> activeObjects = new HashSet<GameObject>();
    
    private bool isHandlerRegistered = false;

    public static WoodLogObjectPool Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("WoodLogObjectPool");
                instance = go.AddComponent<WoodLogObjectPool>();
            }
            return instance;
        }
    }
    private static WoodLogObjectPool instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        ResolvePrefab();
    }

    private void Start()
    {
        InitializePool();
        RegisterNetworkHandler();
    }

    private void ResolvePrefab()
    {
        // Thử tìm prefab "wood_stack", "firewood_single" hoặc "WoodLog" từ Resources
        woodPrefab = Resources.Load<GameObject>("wood_stack");
        if (woodPrefab == null)
        {
            woodPrefab = Resources.Load<GameObject>("firewood_single");
        }
        if (woodPrefab == null)
        {
            woodPrefab = Resources.Load<GameObject>("WoodLog");
        }

        // Nếu chưa tìm thấy, duyệt danh sách NetworkPrefabs của NetworkManager
        if (woodPrefab == null && NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null && NetworkManager.Singleton.NetworkConfig.Prefabs != null && NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs != null)
        {
            foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
            {
                if (networkPrefab.Prefab != null)
                {
                    var col = networkPrefab.Prefab.GetComponent<CollectibleItemDrop>();
                    if (col != null && (col.itemName.Equals("wood_stack", System.StringComparison.OrdinalIgnoreCase) ||
                                        col.itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || 
                                        col.itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase) ||
                                        networkPrefab.Prefab.name.ToLower().Contains("wood_stack")))
                    {
                        woodPrefab = networkPrefab.Prefab;
                        break;
                    }
                }
            }
        }
    }

    private void InitializePool()
    {
        if (woodPrefab == null)
        {
            Debug.LogWarning("[WoodLogObjectPool] Chưa tìm thấy woodPrefab để khởi tạo pool trong Awake/Start.");
            return;
        }

        if (poolQueue.Count > 0) return;

        for (int i = 0; i < initialPoolSize; i++)
        {
            // Không truyền transform vào Instantiate để tránh lỗi re-parenting khi Netcode chưa lắng nghe
            GameObject obj = Instantiate(woodPrefab);
            obj.name = woodPrefab.name + "_Pooled";
            obj.SetActive(false);
            poolQueue.Enqueue(obj);
        }
        Debug.Log($"[WoodLogObjectPool] Đã khởi tạo thành công {initialPoolSize} thanh gỗ vào pool.");
    }

    private void RegisterNetworkHandler()
    {
        if (isHandlerRegistered) return;
        if (NetworkManager.Singleton == null) return;
        if (woodPrefab == null) return;

        var netObj = woodPrefab.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            NetworkManager.Singleton.PrefabHandler.AddHandler(netObj, new WoodLogPrefabHandler(this));
            isHandlerRegistered = true;
            Debug.Log("[WoodLogObjectPool] Đã đăng ký WoodLogPrefabHandler thành công với Netcode.");
        }
    }

    /// <summary>
    /// Lấy một thanh gỗ từ pool hoặc tạo mới nếu pool trống
    /// </summary>
    public GameObject GetOrCreate(GameObject requestedPrefab, Vector3 position, Quaternion rotation)
    {
        // Nếu thay đổi prefab yêu cầu, tái tạo lại toàn bộ pool với prefab mới
        if (requestedPrefab != null && woodPrefab != null && requestedPrefab != woodPrefab)
        {
            Debug.Log($"[WoodLogObjectPool] Phát hiện thay đổi prefab yêu cầu: {woodPrefab.name} -> {requestedPrefab.name}. Đang cấu hình lại pool...");

            // 1. Hủy bỏ Handler cũ trên Netcode
            if (isHandlerRegistered && NetworkManager.Singleton != null)
            {
                var oldNetObj = woodPrefab.GetComponent<NetworkObject>();
                if (oldNetObj != null)
                {
                    NetworkManager.Singleton.PrefabHandler.RemoveHandler(oldNetObj);
                }
                isHandlerRegistered = false;
            }

            // 2. Hủy các đối tượng cũ trong poolQueue
            while (poolQueue.Count > 0)
            {
                GameObject oldObj = poolQueue.Dequeue();
                if (oldObj != null)
                {
                    Destroy(oldObj);
                }
            }

            // Cũng hủy các đối tượng đang active cũ
            foreach (var actObj in activeObjects)
            {
                if (actObj != null)
                {
                    Destroy(actObj);
                }
            }
            activeObjects.Clear();

            // 3. Thiết lập prefab mới và khởi tạo lại pool
            woodPrefab = requestedPrefab;
            InitializePool();
            RegisterNetworkHandler();
        }
        else if (woodPrefab == null && requestedPrefab != null)
        {
            woodPrefab = requestedPrefab;
            InitializePool();
            RegisterNetworkHandler();
        }

        GameObject obj = null;
        while (poolQueue.Count > 0)
        {
            obj = poolQueue.Dequeue();
            if (obj != null) break; // Bỏ qua nếu đối tượng bị hủy ngoài ý muốn
        }

        if (obj == null)
        {
            GameObject prefabToUse = woodPrefab != null ? woodPrefab : requestedPrefab;
            if (prefabToUse != null)
            {
                obj = Instantiate(prefabToUse);
                obj.name = prefabToUse.name + "_Pooled_Extra";
            }
            else
            {
                Debug.LogError("[WoodLogObjectPool] Không thể lấy/tạo gỗ vì không có prefab!");
                return null;
            }
        }

        // Không cần SetParent(null) vì đối tượng đã ở root level
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);
        activeObjects.Add(obj);
        return obj;
    }

    /// <summary>
    /// Trả thanh gỗ lại vào pool
    /// </summary>
    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        // Nếu đối tượng đã được bưng (bị biến đổi thành cosmetic), không trả vào pool này nữa
        // vì nó đã bị hủy NetworkObject và các script khác.
        if (obj.GetComponent<NetworkObject>() == null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Destroy(obj);
            return;
        }

        obj.SetActive(false);
        // Không set parent về transform của pool để tránh cảnh báo re-parenting NetworkObject từ Netcode

        if (activeObjects.Contains(obj))
        {
            activeObjects.Remove(obj);
            poolQueue.Enqueue(obj);
        }
        else if (!poolQueue.Contains(obj))
        {
            poolQueue.Enqueue(obj);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && woodPrefab != null && isHandlerRegistered)
        {
            var netObj = woodPrefab.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                NetworkManager.Singleton.PrefabHandler.RemoveHandler(netObj);
            }
        }
    }

    private class WoodLogPrefabHandler : INetworkPrefabInstanceHandler
    {
        private WoodLogObjectPool pool;

        public WoodLogPrefabHandler(WoodLogObjectPool pool)
        {
            this.pool = pool;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject obj = pool.GetOrCreate(pool.woodPrefab, position, rotation);
            var netObj = obj.GetComponent<NetworkObject>();
            return netObj;
        }

        public void Destroy(NetworkObject networkObject)
        {
            pool.ReturnToPool(networkObject.gameObject);
        }
    }
}
