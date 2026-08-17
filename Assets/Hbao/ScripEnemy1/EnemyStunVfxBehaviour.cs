using UnityEngine;

/// <summary>
/// Component quản lý hiệu ứng VFX choáng (dizzy stars quay trên đầu quái) khi bị dặm khiên / choáng:
/// 1. Tự động tìm và ẩn child "Capsule" (mô hình thử nghiệm white capsule trong prefab).
/// 2. Định vị Transform chính xác trên đầu quái (headHeightOffset) và thu phóng to ra (vfxScale).
/// 3. Bật chế độ vòng lặp (Looping) cho tất cả các hệ thống hạt ParticleSystem.
/// 4. Tự động xóa VFX khi hết thời gian choáng hoặc khi quái chết/bị tiêu diệt.
/// </summary>
public class EnemyStunVfxBehaviour : MonoBehaviour
{
    private GameObject currentVfxInstance;
    private float stunTimer = 0f;
    private static GameObject defaultStunVfxPrefab;

    public static void ApplyStunVfx(GameObject enemy, float duration, GameObject customPrefab = null, float heightOffset = 2.1f, float vfxScale = 1.9f)
    {
        if (enemy == null) return;

        var behaviour = enemy.GetComponent<EnemyStunVfxBehaviour>();
        if (behaviour == null)
        {
            behaviour = enemy.AddComponent<EnemyStunVfxBehaviour>();
        }

        behaviour.ShowStunVfx(duration, customPrefab, heightOffset, vfxScale);
    }

    public static void RemoveStunVfx(GameObject enemy)
    {
        if (enemy == null) return;
        var behaviour = enemy.GetComponent<EnemyStunVfxBehaviour>();
        if (behaviour != null)
        {
            behaviour.ClearVfx();
        }
    }

    public void ShowStunVfx(float duration, GameObject customPrefab, float heightOffset, float vfxScale)
    {
        stunTimer = duration;

        if (currentVfxInstance == null)
        {
            GameObject prefabToUse = customPrefab;
            if (prefabToUse == null)
            {
                if (defaultStunVfxPrefab == null)
                {
                    defaultStunVfxPrefab = Resources.Load<GameObject>("VFX/Par_StunBuff");
                    if (defaultStunVfxPrefab == null)
                    {
                        defaultStunVfxPrefab = Resources.Load<GameObject>("Par_StunBuff");
                    }
                }
                prefabToUse = defaultStunVfxPrefab;
            }

            if (prefabToUse != null)
            {
                currentVfxInstance = Instantiate(prefabToUse, transform);
                
                // 1. Tắt bỏ mesh thử nghiệm "Capsule" trong prefab
                Transform capsuleChild = currentVfxInstance.transform.Find("Capsule");
                if (capsuleChild != null)
                {
                    capsuleChild.gameObject.SetActive(false);
                }

                // 2. Định vị Transform chính xác trên đầu quái & thu phóng to rõ nét
                currentVfxInstance.transform.localPosition = Vector3.up * heightOffset;
                currentVfxInstance.transform.localRotation = Quaternion.identity;
                currentVfxInstance.transform.localScale = Vector3.one * Mathf.Max(0.5f, vfxScale);

                // 3. Đảm bảo tất cả ParticleSystem lặp lại (Looping) trong suốt thời gian choáng
                ParticleSystem[] particleSystems = currentVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particleSystems)
                {
                    if (ps != null)
                    {
                        var main = ps.main;
                        main.loop = true;
                        if (!ps.isPlaying) ps.Play();
                    }
                }
            }
        }
    }

    private void Update()
    {
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                ClearVfx();
            }
        }
    }

    public void ClearVfx()
    {
        stunTimer = 0f;
        if (currentVfxInstance != null)
        {
            ParticleSystem[] particleSystems = currentVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = false;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            Destroy(currentVfxInstance, 0.5f);
            currentVfxInstance = null;
        }
    }

    private void OnDestroy()
    {
        if (currentVfxInstance != null)
        {
            Destroy(currentVfxInstance);
            currentVfxInstance = null;
        }
    }
}
