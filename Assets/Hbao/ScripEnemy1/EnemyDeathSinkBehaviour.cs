using System.Collections;
using UnityEngine;

/// <summary>
/// Component quản lý hiệu ứng khi quái vật chết:
/// 1. Phát âm thanh chết 3D (3D spatial audio) cho cả 4 Player xung quanh nghe thấy.
/// 2. Tạo hiệu ứng hố rớt (Ground Hole VFX với vết nứt đất đỏ) dưới chân quái.
/// 3. Chờ quái hoàn thiện animation Die và VFX mở rộng hoàn chỉnh (vfx đã oke).
/// 4. Từ từ cho xác quái tuột (chìm) xuống hố trước khi bị despawn/destroy.
/// 5. Tự động tắt vòng lặp (looping) của VFX sau khi quái lọt xuống để vết nứt & hố mờ dần mượt mà.
/// </summary>
public class EnemyDeathSinkBehaviour : MonoBehaviour
{
    [Header("Sink Settings")]
    public float sinkSpeed = 0.75f;
    public float sinkDuration = 2.8f;
    public float delayBeforeSink = 1.2f; // Chờ quái gục xong & VFX hiện nguyên hình hoàn chỉnh

    private static GameObject defaultVfxPrefab;
    private static AudioClip defaultDeathClip;

    private GameObject spawnedVfx;

    public static void ApplyDeathEffects(GameObject enemy, AudioClip customDeathClip = null, GameObject customVfxPrefab = null, float vfxScale = 1.85f)
    {
        if (enemy == null) return;

        // Tránh gắn trùng lặp component nếu quái đã ở trạng thái chết
        if (enemy.GetComponent<EnemyDeathSinkBehaviour>() != null) return;

        Vector3 deathPos = enemy.transform.position;

        // 1. Âm thanh khi chết (3D Spatial Audio cho cả 4 player nghe thấy)
        AudioClip clipToPlay = customDeathClip;
        if (clipToPlay == null)
        {
            if (defaultDeathClip == null)
            {
                defaultDeathClip = Resources.Load<AudioClip>("Audio/BreakSkill");
                if (defaultDeathClip == null) defaultDeathClip = Resources.Load<AudioClip>("Audio/ChemChuaHit");
            }
            clipToPlay = defaultDeathClip;
        }

        if (clipToPlay != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(clipToPlay, deathPos, 1.0f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clipToPlay, deathPos, 1.0f);
            }
        }

        // 2. Tạo VFX vết nứt & hố tử thần dưới chân quái (vùng VFX to ôm trọn xác quái)
        GameObject vfxPrefab = customVfxPrefab;
        if (vfxPrefab == null)
        {
            if (defaultVfxPrefab == null)
            {
                defaultVfxPrefab = Resources.Load<GameObject>("VFX_Dark_Area_01");
                if (defaultVfxPrefab == null) defaultVfxPrefab = Resources.Load<GameObject>("VFX_Void_Area_01");
            }
            vfxPrefab = defaultVfxPrefab;
        }

        GameObject spawnedVfxObj = null;
        if (vfxPrefab != null)
        {
            Vector3 vfxPos = deathPos + Vector3.up * 0.05f;
            spawnedVfxObj = Instantiate(vfxPrefab, vfxPos, Quaternion.identity);
            spawnedVfxObj.transform.localScale = Vector3.one * Mathf.Max(0.5f, vfxScale);
        }

        // 3. Tắt toàn bộ Colliders và NavMeshAgent trên quái để rơi xuyên đất mượt mà
        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var cols = enemy.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c != null) c.enabled = false;
        }

        // 4. Đính kèm component làm xác quái từ từ tuột xuống hố
        var sinker = enemy.AddComponent<EnemyDeathSinkBehaviour>();
        sinker.spawnedVfx = spawnedVfxObj;
        sinker.StartSinking();
    }

    public void StartSinking()
    {
        StartCoroutine(SinkRoutine());
    }

    private IEnumerator SinkRoutine()
    {
        // Bước 1: Chờ quái hoàn thiện Animation Die và VFX hố rớt hiện nguyên hình đẹp mắt
        yield return new WaitForSeconds(delayBeforeSink);

        // Bước 2: Từ từ tuột (chìm) xác quái xuống lòng hố/map
        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            float delta = Time.deltaTime;
            elapsed += delta;
            transform.position += Vector3.down * (sinkSpeed * delta);
            yield return null;
        }

        // Bước 3: Khi xác đã chìm xong bên dưới map, dừng tạo thêm hạt (emission) để vết nứt & hố mờ dần mượt mà
        if (spawnedVfx != null)
        {
            ParticleSystem[] psList = spawnedVfx.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in psList)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = false;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            Destroy(spawnedVfx, 2.0f);
            spawnedVfx = null;
        }
    }

    private void OnDestroy()
    {
        // Cleanup VFX nếu quái bị destroy sớm
        if (spawnedVfx != null)
        {
            Destroy(spawnedVfx);
            spawnedVfx = null;
        }
    }
}
