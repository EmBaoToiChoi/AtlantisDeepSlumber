using Unity.Netcode;
using UnityEngine;

public class ArrowProjectile : NetworkBehaviour
{
    public float speed = 30f;
    public NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        30f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public float lifetime = 4f;
    public float damage = 20f;
    public bool isPiercing = false; // Cờ kiểm tra xem mũi tên có xuyên thấu quái vật hay không
    
    [HideInInspector]
    public NetworkBehaviour owner;

    // Danh sách lưu các quái vật đã trúng đòn để tránh việc một mũi tên xuyên gây sát thương nhiều lần trên cùng một quái
    private System.Collections.Generic.HashSet<Transform> hitEnemyRoots = new System.Collections.Generic.HashSet<Transform>();

    [Header("Visual Effects (VFX) Settings")]
    [Tooltip("Prefab VFX năng lượng xanh dương đi kèm mũi tên (nếu để trống sẽ dùng Par_BlueShoot_Bullet / Elena_Arrow_VFX).")]
    public GameObject arrowVfxPrefab;
    [Tooltip("Offset vị trí tương đối của VFX so với đầu/thân mũi tên.")]
    public Vector3 arrowVfxOffset = Vector3.zero;
    [Tooltip("Góc xoay tương đối của VFX so với hướng bay của mũi tên.")]
    public Vector3 arrowVfxRotation = Vector3.zero;
    [Tooltip("Tỷ lệ scale của VFX đạn ma thuật xanh.")]
    public float arrowVfxScale = 1.0f;

    private GameObject activeVfxInstance;
    private static GameObject defaultArrowVfxPrefab;

    private void OnEnable()
    {
        AttachArrowVfx();
    }

    private void Start()
    {
        Debug.Log($"[ArrowProjectile] Mũi tên được khởi tạo tại: {transform.position}, góc xoay: {transform.rotation.eulerAngles}, tỉ lệ scale: {transform.localScale}, Trạng thái active: {gameObject.activeSelf}, Trạng thái xuyên thấu: {isPiercing}");

        AttachArrowVfx();

        // Phá hủy cục bộ nếu không thuộc Netcode hoặc chạy trên Server để dọn dẹp
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void AttachArrowVfx()
    {
        if (activeVfxInstance != null) return;

        GameObject prefabToUse = arrowVfxPrefab;
        if (prefabToUse == null)
        {
            if (defaultArrowVfxPrefab == null)
            {
                defaultArrowVfxPrefab = Resources.Load<GameObject>("VFX/Elena_Arrow_VFX");
                if (defaultArrowVfxPrefab == null)
                {
                    defaultArrowVfxPrefab = Resources.Load<GameObject>("Par_BlueShoot_Bullet");
                }
            }
            prefabToUse = defaultArrowVfxPrefab;
        }

        if (prefabToUse != null)
        {
            activeVfxInstance = Instantiate(prefabToUse, transform);
            
            Transform capsuleChild = activeVfxInstance.transform.Find("Capsule");
            if (capsuleChild != null)
            {
                capsuleChild.gameObject.SetActive(false);
            }

            activeVfxInstance.transform.localPosition = arrowVfxOffset;
            activeVfxInstance.transform.localRotation = Quaternion.Euler(arrowVfxRotation);
            activeVfxInstance.transform.localScale = Vector3.one * Mathf.Max(0.1f, arrowVfxScale);

            // Bật Loop = true cho tất cả ParticleSystem (Particles_Shell, Particles_BulletHead, Par_BurstParticles)
            // để vệt năng lượng xanh lướt đi liên tục trên đường bay của mũi tên
            ParticleSystem[] psList = activeVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
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
    }

    private void Update()
    {
        float currentSpeed = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned) ? netSpeed.Value : speed;
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
    }

    private bool isHitPlay = false;

    private void PlayHitSound()
    {
        if (isHitPlay) return;
        isHitPlay = true;
        AudioClip hitClip = Resources.Load<AudioClip>("Audio/HitArrow");
        if (hitClip != null)
        {
            AudioSource.PlayClipAtPoint(hitClip, transform.position);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Bỏ qua va chạm với bất kỳ đối tượng Player nào (bao gồm chính Elena, LeoPlayer, SimplePlayerTest)
        if (other.CompareTag("Player") || 
            other.gameObject.layer == LayerMask.NameToLayer("Player") ||
            other.GetComponentInParent<ElenaPlayer>() != null ||
            other.GetComponentInParent<MayaPlayer>() != null ||
            other.GetComponentInParent<LeoPlayer>() != null ||
            other.GetComponentInParent<SimplePlayerTest>() != null)
        {
            return;
        }

        // Kiểm tra xem đối tượng va chạm có phải là Enemy hay không
        bool isEnemy = other.CompareTag("Enemy") || 
                       other.gameObject.layer == LayerMask.NameToLayer("Enemy") ||
                       other.name.ToLower().Contains("enemy") ||
                       other.GetComponentInParent<Enemy1_DapBua>() != null ||
                       other.GetComponentInParent<Enemy2_Zombie>() != null ||
                       other.GetComponentInParent<Enemy3_Buaa>() != null ||
                       other.GetComponentInParent<Enemy4_Bongtoi>() != null ||
                       other.GetComponentInParent<Enemy5_PhuThuy>() != null ||
                       other.GetComponentInParent<MiniBossAI>() != null ||
                       other.GetComponentInParent<FinalBossAI>() != null ||
                       other.GetComponentInParent<BossAI>() != null;

        if (isEnemy)
        {
            PlayHitSound();
            
            // Chỉ xử lý sát thương trên Server hoặc chế độ Standalone
            bool isServerOrStandalone = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
            if (!isServerOrStandalone) return;
            Transform enemyRoot = other.transform.root;
            if (hitEnemyRoots.Contains(enemyRoot))
            {
                // Bỏ qua nếu quái vật này đã bị mũi tên này bắn trúng rồi
                return;
            }
            hitEnemyRoots.Add(enemyRoot);

            Debug.Log($"[ArrowProjectile] Mũi tên va chạm trúng Enemy/Boss: {other.name}, Gây sát thương: {damage}");
            
            // Gây sát thương trực tiếp lên quái vật/boss tùy theo loại script của nó
            var e1 = other.GetComponentInParent<Enemy1_DapBua>();
            if (e1 != null) { e1.TakeDamage(damage); }
            else
            {
                var e2 = other.GetComponentInParent<Enemy2_Zombie>();
                if (e2 != null) { e2.TakeDamage(damage); }
                else
                {
                    var e3 = other.GetComponentInParent<Enemy3_Buaa>();
                    if (e3 != null) { e3.TakeDamage(damage); }
                    else
                    {
                        var e4 = other.GetComponentInParent<Enemy4_Bongtoi>();
                        if (e4 != null) { e4.TakeDamage(damage); }
                        else
                        {
                            var e5 = other.GetComponentInParent<Enemy5_PhuThuy>();
                            if (e5 != null) { e5.TakeDamage(damage); }
                            else
                            {
                                var mb = other.GetComponentInParent<MiniBossAI>();
                                if (mb != null) { mb.TakeDamage(damage); }
                                else
                                {
                                    var fb = other.GetComponentInParent<FinalBossAI>();
                                    if (fb != null) { fb.TakeDamage(damage); }
                                    else
                                    {
                                        var b = other.GetComponentInParent<BossAI>();
                                        if (b != null) { b.TakeDamage(damage); }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Gọi thêm hàm damage của ElenaPlayer để đồng nhất nếu có gán owner
            if (owner != null)
            {
                if (owner is ElenaPlayer elenaOwner)
                {
                    elenaOwner.TryDamageEnemy(other);
                }
                else if (owner is MayaPlayer mayaOwner)
                {
                    mayaOwner.TryDamageEnemy(other);
                }
            }

            // Nếu không phải mũi tên xuyên thấu (Kỹ năng E) thì mới tự hủy
            if (!isPiercing)
            {
                DespawnOrDestroy();
            }
        }
        else if (!other.isTrigger)
        {
            PlayHitSound();
            bool isServerOrStandalone = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
            if (!isServerOrStandalone) return;
            Debug.Log($"[ArrowProjectile] Mũi tên va chạm trúng chướng ngại vật: {other.name}, tự hủy.");
            // Va chạm với môi trường (tường, đất, v.v...)
            DespawnOrDestroy();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Nhận diện va chạm vật lý cứng và chuyển tiếp sang hàm xử lý Trigger
        if (collision.collider != null)
        {
            OnTriggerEnter(collision.collider);
        }
    }

    private void DetachOrCleanVfx()
    {
        if (activeVfxInstance != null)
        {
            // Tách VFX ra khỏi mũi tên để vệt sáng mờ dần tự nhiên theo đuôi khi mũi tên cắm/trúng mục tiêu
            activeVfxInstance.transform.SetParent(null);

            ParticleSystem[] psList = activeVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = false;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            Destroy(activeVfxInstance, 1.5f);
            activeVfxInstance = null;
        }
    }

    private void DespawnOrDestroy()
    {
        DetachOrCleanVfx();

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        DetachOrCleanVfx();
    }
}
