using Unity.Netcode;
using UnityEngine;

public class MayaProjectile : NetworkBehaviour
{
    public float speed = 30f;
    public float lifetime = 4f;
    public float damage = 20f;
    public bool isPiercing = false; // Cờ kiểm tra xem đạn ma thuật có xuyên thấu quái vật hay không
    
    [HideInInspector]
    public MayaPlayer owner;

    // Danh sách lưu các quái vật đã trúng đòn để tránh việc đạn xuyên gây sát thương nhiều lần trên cùng một quái
    private System.Collections.Generic.HashSet<Transform> hitEnemyRoots = new System.Collections.Generic.HashSet<Transform>();

    private void Start()
    {
        Debug.Log($"[MayaProjectile] Đạn ma thuật được khởi tạo tại: {transform.position}, góc xoay: {transform.rotation.eulerAngles}, tỉ lệ scale: {transform.localScale}, Trạng thái active: {gameObject.activeSelf}, Trạng thái xuyên thấu: {isPiercing}");

        // Phá hủy cục bộ nếu không thuộc Netcode hoặc chạy trên Server để dọn dẹp
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ xử lý va chạm trên Server hoặc chế độ Standalone
        bool isServerOrStandalone = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        if (!isServerOrStandalone) return;

        // Bỏ qua va chạm với bất kỳ đối tượng Player nào (bao gồm chính Maya, Elena, LeoPlayer, SimplePlayerTest)
        if (other.CompareTag("Player") || 
            other.gameObject.layer == LayerMask.NameToLayer("Player") ||
            other.GetComponentInParent<MayaPlayer>() != null ||
            other.GetComponentInParent<ElenaPlayer>() != null ||
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
            Transform enemyRoot = other.transform.root;
            if (hitEnemyRoots.Contains(enemyRoot))
            {
                // Bỏ qua nếu quái vật này đã bị đạn này bắn trúng rồi
                return;
            }
            hitEnemyRoots.Add(enemyRoot);

            Debug.Log($"[MayaProjectile] Đạn ma thuật va chạm trúng Enemy/Boss: {other.name}, Gây sát thương: {damage}");
            
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

            // Gọi thêm hàm damage của MayaPlayer để đồng nhất nếu có gán owner
            if (owner != null)
            {
                owner.TryDamageEnemy(other);
            }

            // Nếu không phải đạn xuyên thấu (Kỹ năng E) thì mới tự hủy
            if (!isPiercing)
            {
                DespawnOrDestroy();
            }
        }
        else if (!other.isTrigger)
        {
            Debug.Log($"[MayaProjectile] Đạn ma thuật va chạm trúng chướng ngại vật: {other.name}, tự hủy.");
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

    private void DespawnOrDestroy()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
