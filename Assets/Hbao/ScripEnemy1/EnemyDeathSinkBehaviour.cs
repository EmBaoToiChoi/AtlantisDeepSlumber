using System.Collections;
using UnityEngine;

/// <summary>
/// Component quản lý hiệu ứng khi quái vật chết:
/// 1. Phát âm thanh chết 3D cho 4 Player xung quanh nghe thấy.
/// 2. Chờ quái phát hết animation Die và nằm yên trên mặt đất 2.0 giây.
/// 3. Kích hoạt hố đen tử thần (VFX_Dark_Area_01) VÀ xúc tu/xích tối (Enemy_Death_Tentacles) quấn quanh xác quái.
/// 4. Từ từ kéo/tuột xác quái chìm xuống lòng hố/map.
/// 5. Ngay khi xác quái vừa chìm xong xuống đất, tắt & xóa ngay lập tức VFX hố và xúc tu.
/// </summary>
public class EnemyDeathSinkBehaviour : MonoBehaviour
{
    [Header("Sink Settings")]
    public float sinkSpeed = 0.9f;
    public float sinkDuration = 2.8f;
    public float delayBeforeSink = 2.0f; // Chờ quái gục và nằm yên 2s trước khi hố & xúc tu xuất hiện

    private static GameObject defaultVfxPrefab;
    private static GameObject defaultTentacleVfxPrefab;
    private static AudioClip defaultDeathClip;

    private GameObject spawnedVfx;
    private GameObject spawnedTentaclesVfx;
    private GameObject customVfxPrefabToUse;
    private float vfxScaleToUse = 1.85f;

    public static void ApplyDeathEffects(GameObject enemy, AudioClip customDeathClip = null, GameObject customVfxPrefab = null, float vfxScale = 1.85f)
    {
        if (enemy == null) return;

        if (enemy.GetComponent<EnemyDeathSinkBehaviour>() != null) return;

        Vector3 deathPos = enemy.transform.position;

        // 1. Phát âm thanh khi chết
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

        // 2. Tắt toàn bộ Colliders và NavMeshAgent trên quái lập tức
        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var cols = enemy.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c != null) c.enabled = false;
        }

        // 3. Đính kèm component quản lý thời gian gục -> hố rớt + xúc tu quấn -> chìm xác
        var sinker = enemy.AddComponent<EnemyDeathSinkBehaviour>();
        sinker.customVfxPrefabToUse = customVfxPrefab;
        sinker.vfxScaleToUse = vfxScale;
        sinker.StartSinking();
    }

    public void StartSinking()
    {
        StartCoroutine(SinkRoutine());
    }

    private IEnumerator SinkRoutine()
    {
        // Giai đoạn 1: Chờ quái phát animation Die và nằm yên trên mặt đất 2.0s
        yield return new WaitForSeconds(delayBeforeSink);

        // Giai đoạn 2: Tạo hố rớt đất VÀ xúc tu/xích tối quấn quanh xác quái
        Vector3 deathPos = transform.position;

        GameObject vfxPrefab = customVfxPrefabToUse;
        if (vfxPrefab == null)
        {
            if (defaultVfxPrefab == null)
            {
                defaultVfxPrefab = Resources.Load<GameObject>("VFX_Dark_Area_01");
                if (defaultVfxPrefab == null) defaultVfxPrefab = Resources.Load<GameObject>("VFX_Void_Area_01");
            }
            vfxPrefab = defaultVfxPrefab;
        }

        if (vfxPrefab != null)
        {
            Vector3 vfxPos = deathPos + Vector3.up * 0.05f;
            spawnedVfx = Instantiate(vfxPrefab, vfxPos, Quaternion.identity);
            spawnedVfx.transform.localScale = Vector3.one * Mathf.Max(0.5f, vfxScaleToUse);
        }

        // Tải VFX xúc tu / xích ma thuật quấn quanh xác quái
        if (defaultTentacleVfxPrefab == null)
        {
            defaultTentacleVfxPrefab = Resources.Load<GameObject>("VFX/Enemy_Death_Tentacles");
            if (defaultTentacleVfxPrefab == null)
            {
                defaultTentacleVfxPrefab = Resources.Load<GameObject>("Par_Restraint");
            }
        }

        if (defaultTentacleVfxPrefab != null)
        {
            spawnedTentaclesVfx = Instantiate(defaultTentacleVfxPrefab, transform);
            
            // Tắt mesh Capsule thử nghiệm nếu có trong prefab
            Transform capsuleChild = spawnedTentaclesVfx.transform.Find("Capsule");
            if (capsuleChild != null)
            {
                capsuleChild.gameObject.SetActive(false);
            }

            spawnedTentaclesVfx.transform.localPosition = Vector3.up * 0.3f;
            spawnedTentaclesVfx.transform.localRotation = Quaternion.identity;
            spawnedTentaclesVfx.transform.localScale = Vector3.one * (vfxScaleToUse * 0.9f);
        }

        // Giai đoạn 3: Từ từ cho xúc tu quấn xác quái và kéo chìm xuống hố/map
        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            float delta = Time.deltaTime;
            elapsed += delta;
            transform.position += Vector3.down * (sinkSpeed * delta);
            yield return null;
        }

        // Giai đoạn 4: Ngay khi hạ xác xuống xong, TẮT VÀ XÓA NGAY LẬP TỨC các VFX hố rớt & xúc tu
        ClearAllVfx();
    }

    private void ClearAllVfx()
    {
        if (spawnedVfx != null)
        {
            ParticleSystem[] psList = spawnedVfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = false;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            Destroy(spawnedVfx);
            spawnedVfx = null;
        }

        if (spawnedTentaclesVfx != null)
        {
            ParticleSystem[] psList = spawnedTentaclesVfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = false;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            Destroy(spawnedTentaclesVfx);
            spawnedTentaclesVfx = null;
        }
    }

    private void OnDestroy()
    {
        ClearAllVfx();
    }
}
