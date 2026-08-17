using System.Collections;
using UnityEngine;

/// <summary>
/// Component quản lý hiệu ứng khi 5 con quái vật (Enemy 1 -> 5) chết:
/// 1. Phát âm thanh gục chết 3D cho 4 Player xung quanh nghe thấy.
/// 2. Chờ quái phát hết animation Die và nằm yên trên mặt đất 2.0 giây.
/// 3. Kích hoạt hố đen tử thần mở rộng rực rỡ (VFX_Dark_Area_01) dưới chân quái.
/// 4. Quái bị hút và chìm sâu xuống lòng đất với vận tốc rơi 1.8m/s, kết hợp co nhỏ và xoay độ nghiêng về phía lòng hố.
/// 5. Ngay khi xác quái vừa rơi chìm hẳn xuống đất, TẮT VÀ XÓA NGAY LẬP TỨC VFX hố đen.
/// Đồng bộ hoàn hảo cho cả 4 người chơi trong phòng Multiplayer.
/// </summary>
public class EnemyDeathSinkBehaviour : MonoBehaviour
{
    [Header("Sink Settings")]
    public float sinkSpeed = 1.8f;      // Tăng tốc độ chìm sâu xuống lòng đất để nhìn rõ rệt đợt rơi
    public float sinkDuration = 2.0f;   // Thời gian xác bị hút rơi sâu xuống hố (2.0s)
    public float delayBeforeSink = 2.0f;// Chờ quái gục và nằm yên 2s trước khi hố xuất hiện

    private static GameObject defaultVfxPrefab;
    private static AudioClip defaultDeathClip;

    private GameObject spawnedVfx;
    private GameObject customVfxPrefabToUse;
    private float vfxScaleToUse = 1.5f; // Chỉnh hố đen vừa vặn 1.5f theo yêu cầu

    public static void ApplyDeathEffects(GameObject enemy, AudioClip customDeathClip = null, GameObject customVfxPrefab = null, float vfxScale = 1.5f)
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

        // 3. Đính kèm component quản lý thời gian gục -> hố rớt -> chìm xác
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
        // Giai đoạn 1: Chờ quái phát hết animation Die và nằm yên trên mặt đất đúng 2.0s
        yield return new WaitForSeconds(delayBeforeSink);

        // Giai đoạn 2: Tạo hố đen tử thần kích thước vừa vặn 1.5f dưới chân quái
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
            spawnedVfx.transform.localScale = Vector3.one * 1.5f;

            ParticleSystem[] psList = spawnedVfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = true;
                    if (!ps.isPlaying) ps.Play();
                }
            }
        }

        // Giai đoạn 3: Xác quái bị hút rơi mạnh xuống sâu dưới lòng đất (tạo chuyển động rơi cực kỳ rõ rệt)
        Vector3 initialScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            float delta = Time.deltaTime;
            elapsed += delta;
            float progress = Mathf.Clamp01(elapsed / sinkDuration);

            // Rơi sâu xuống lòng đất với vận tốc 1.8m/s
            transform.position += Vector3.down * (sinkSpeed * delta);

            // Co nhỏ nhẹ xác lại khi chìm vào lòng hư vô (từ 100% -> 60%)
            transform.localScale = Vector3.Lerp(initialScale, initialScale * 0.6f, progress);

            // Nghiêng nhẹ xác theo chiều rơi chìm xuống hố
            transform.Rotate(Vector3.right * (25f * delta), Space.World);

            yield return null;
        }

        // Giai đoạn 4: Ngay khi xác quái vừa chìm biến mất hoàn toàn xuống lòng đất, TẮT VÀ XÓA NGAY LẬP TỨC VFX HỐ
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
    }

    private void OnDestroy()
    {
        ClearAllVfx();
    }
}
