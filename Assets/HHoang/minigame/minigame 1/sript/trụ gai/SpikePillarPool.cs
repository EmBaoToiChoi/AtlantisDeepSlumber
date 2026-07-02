using UnityEngine;
using System.Collections.Generic;

public class SpikePillarPool : MonoBehaviour
{
    [Header("Spike Pillar Prefab")]
    public SpikePillarLocal pillarPrefab;
    public int initialPoolSize = 5;

    private List<SpikePillarLocal> pool = new List<SpikePillarLocal>();

    void Start()
    {
        if (pillarPrefab == null)
        {
            Debug.LogError("[SpikePillarPool] Prefab chưa được gán!");
            return;
        }

        // Khởi tạo trước các phần tử trong pool
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewPillarInstance();
        }
    }

    private SpikePillarLocal CreateNewPillarInstance()
    {
        SpikePillarLocal instance = Instantiate(pillarPrefab, transform);
        instance.gameObject.SetActive(false);
        pool.Add(instance);
        return instance;
    }

    public SpikePillarLocal GetPillar()
    {
        // Tìm phần tử rảnh rỗi
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].gameObject.activeSelf)
            {
                return pool[i];
            }
        }

        // Nếu hết thì tạo mới (Expand pool)
        return CreateNewPillarInstance();
    }

    public void ReturnPillar(SpikePillarLocal pillar)
    {
        if (pillar != null)
        {
            pillar.gameObject.SetActive(false);
        }
    }

    // Thu hồi toàn bộ trụ đang hoạt động về pool
    public void RecallAll()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && pool[i].gameObject.activeSelf)
            {
                pool[i].gameObject.SetActive(false);
            }
        }
    }
}
