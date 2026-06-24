using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ExperienceGemObjectPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("Số lượng ngọc kinh nghiệm khởi tạo ban đầu")]
    public int initialPoolSize = 40;

    private GameObject expPrefab;
    private Queue<GameObject> poolQueue = new Queue<GameObject>();
    private HashSet<GameObject> activeObjects = new HashSet<GameObject>();
    
    private bool isHandlerRegistered = false;

    public static ExperienceGemObjectPool Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("ExperienceGemObjectPool");
                instance = go.AddComponent<ExperienceGemObjectPool>();
            }
            return instance;
        }
    }
    private static ExperienceGemObjectPool instance;

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
        // Thử tìm prefab "EXP" từ Resources
        expPrefab = Resources.Load<GameObject>("EXP");

        // Nếu chưa tìm thấy, duyệt danh sách NetworkPrefabs của NetworkManager
        if (expPrefab == null && NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
        {
            foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
            {
                if (networkPrefab.Prefab != null)
                {
                    var gem = networkPrefab.Prefab.GetComponent<ExperienceGem>();
                    if (gem != null)
                    {
                        expPrefab = networkPrefab.Prefab;
                        break;
                    }
                }
            }
        }
    }

    private void InitializePool()
    {
        if (expPrefab == null)
        {
            Debug.LogWarning("[ExperienceGemObjectPool] Chưa tìm thấy expPrefab để khởi tạo pool trong Awake/Start. Sẽ tự động khởi tạo khi GetOrCreate được gọi.");
            return;
        }

        if (poolQueue.Count > 0) return;

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(expPrefab);
            obj.name = expPrefab.name + "_Pooled";
            obj.SetActive(false);
            poolQueue.Enqueue(obj);
        }
        Debug.Log($"[ExperienceGemObjectPool] Đã khởi tạo thành công {initialPoolSize} viên EXP vào pool.");
    }

    private void RegisterNetworkHandler()
    {
        if (isHandlerRegistered) return;
        if (NetworkManager.Singleton == null) return;
        if (expPrefab == null) return;

        var netObj = expPrefab.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            NetworkManager.Singleton.PrefabHandler.AddHandler(netObj, new ExperienceGemPrefabHandler(this));
            isHandlerRegistered = true;
            Debug.Log("[ExperienceGemObjectPool] Đã đăng ký ExperienceGemPrefabHandler thành công với Netcode.");
        }
    }

    /// <summary>
    /// Lấy một viên EXP từ pool hoặc tạo mới nếu pool trống
    /// </summary>
    public GameObject GetOrCreate(GameObject requestedPrefab, Vector3 position, Quaternion rotation)
    {
        if (expPrefab == null && requestedPrefab != null)
        {
            expPrefab = requestedPrefab;
            InitializePool();
            RegisterNetworkHandler();
        }

        GameObject obj = null;
        while (poolQueue.Count > 0)
        {
            obj = poolQueue.Dequeue();
            if (obj != null) break;
        }

        if (obj == null)
        {
            GameObject prefabToUse = expPrefab != null ? expPrefab : requestedPrefab;
            if (prefabToUse != null)
            {
                obj = Instantiate(prefabToUse);
                obj.name = prefabToUse.name + "_Pooled_Extra";
            }
            else
            {
                Debug.LogError("[ExperienceGemObjectPool] Không thể lấy/tạo viên EXP vì không có prefab!");
                return null;
            }
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        
        // Reset state trước khi kích hoạt lại
        var gemScript = obj.GetComponent<ExperienceGem>();
        if (gemScript != null)
        {
            gemScript.ResetState();
        }

        obj.SetActive(true);
        activeObjects.Add(obj);
        return obj;
    }

    /// <summary>
    /// Trả viên EXP lại vào pool
    /// </summary>
    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        // Nếu đối tượng bị mất NetworkObject (trong trường hợp đặc biệt), không trả vào pool này nữa
        if (obj.GetComponent<NetworkObject>() == null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Destroy(obj);
            return;
        }

        obj.SetActive(false);

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
        if (NetworkManager.Singleton != null && expPrefab != null && isHandlerRegistered)
        {
            var netObj = expPrefab.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                NetworkManager.Singleton.PrefabHandler.RemoveHandler(netObj);
            }
        }
    }

    private class ExperienceGemPrefabHandler : INetworkPrefabInstanceHandler
    {
        private ExperienceGemObjectPool pool;

        public ExperienceGemPrefabHandler(ExperienceGemObjectPool pool)
        {
            this.pool = pool;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject obj = pool.GetOrCreate(pool.expPrefab, position, rotation);
            var netObj = obj.GetComponent<NetworkObject>();
            return netObj;
        }

        public void Destroy(NetworkObject networkObject)
        {
            pool.ReturnToPool(networkObject.gameObject);
        }
    }
}
