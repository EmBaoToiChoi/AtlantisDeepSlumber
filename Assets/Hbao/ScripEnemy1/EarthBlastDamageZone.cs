using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Script gây sát thương va chạm khi người chơi chạm vào bất kỳ phần nào của đá triệu hồi (Earth Blast).
/// Gây 30 sát thương và có cooldown để tránh gây chết người chơi tức thì.
/// </summary>
public class EarthBlastDamageZone : MonoBehaviour
{
    public float damage = 30f;
    public float damageCooldown = 1.0f;
    private Dictionary<Transform, float> lastDamageTimes = new Dictionary<Transform, float>();

    private void OnTriggerEnter(Collider other)
    {
        ProcessDamage(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessDamage(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessDamage(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessDamage(collision.gameObject);
    }

    private void ProcessDamage(GameObject target)
    {
        if (target == null) return;

        var player = target.GetComponentInParent<IPlayerHUDTarget>() ?? target.GetComponentInChildren<IPlayerHUDTarget>();
        if (player != null && player.CurrentHealth > 0)
        {
            Transform playerTrans = player.transform;
            if (!lastDamageTimes.TryGetValue(playerTrans, out float lastTime) || Time.time - lastTime >= damageCooldown)
            {
                lastDamageTimes[playerTrans] = Time.time;
                EnemyDamageHelper.DealDamage(playerTrans, damage, Vector3.zero);
                Debug.Log($"[EarthBlastDamageZone] Gây {damage} sát thương va chạm cho {playerTrans.name}");
            }
        }
    }

    /// <summary>
    /// Hàm tiện ích tự động gắn Rigidbody Kinematic và các Collider phủ kín 100% toàn bộ Prefab đá (Mesh, Particle, Renderers),
    /// đảm bảo đụng vào bất kỳ góc/mảnh đá nào của Prefab cũng lập tức bị trừ 30 máu.
    /// </summary>
    public static void SetupRockColliders(GameObject blastInstance, float earthBlastRadius, float earthBlastScale, float damageAmount = 30f)
    {
        if (blastInstance == null) return;

        // 1. Gắn Rigidbody Kinematic ở Root để Unity Physics luôn phát tín hiệu Trigger 100%
        var rb = blastInstance.GetComponent<Rigidbody>();
        if (rb == null) rb = blastInstance.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 2. Gắn SphereCollider Trigger ở Root phủ toàn bộ vòng viền cảnh báo
        var mainCol = blastInstance.GetComponent<SphereCollider>();
        if (mainCol == null) mainCol = blastInstance.AddComponent<SphereCollider>();
        mainCol.isTrigger = true;
        mainCol.radius = Mathf.Max(0.5f, earthBlastRadius / Mathf.Max(0.1f, earthBlastScale));

        // 3. Gắn EarthBlastDamageZone vào Root (Sát thương 30)
        var rootDmg = blastInstance.GetComponent<EarthBlastDamageZone>();
        if (rootDmg == null) rootDmg = blastInstance.AddComponent<EarthBlastDamageZone>();
        rootDmg.damage = damageAmount;

        // 4. Tính toán Bounding Box bao phủ 100% toàn bộ visual renderers của Prefab
        Renderer[] allRenderers = blastInstance.GetComponentsInChildren<Renderer>(true);
        if (allRenderers != null && allRenderers.Length > 0)
        {
            Bounds combinedBounds = allRenderers[0].bounds;
            for (int i = 1; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null)
                {
                    combinedBounds.Encapsulate(allRenderers[i].bounds);
                }
            }

            // Gắn thêm BoxCollider ở Root bao trọn không gian hình khối của Prefab
            var boundsBox = blastInstance.AddComponent<BoxCollider>();
            boundsBox.isTrigger = true;
            boundsBox.center = blastInstance.transform.InverseTransformPoint(combinedBounds.center);
            Vector3 worldSize = combinedBounds.size;
            Vector3 localScale = blastInstance.transform.lossyScale;
            boundsBox.size = new Vector3(
                worldSize.x / Mathf.Max(0.001f, localScale.x),
                worldSize.y / Mathf.Max(0.001f, localScale.y),
                worldSize.z / Mathf.Max(0.001f, localScale.z)
            );
        }

        // 5. Duyệt qua tất cả các GameObject con (Particle System, Mesh, v.v...) và gắn Collider + DamageZone trực tiếp
        Transform[] allTransforms = blastInstance.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            GameObject childObj = t.gameObject;

            // Nếu childObj có MeshFilter, ParticleSystem hoặc Renderer nhưng chưa có Collider
            MeshFilter mf = childObj.GetComponent<MeshFilter>();
            Renderer rend = childObj.GetComponent<Renderer>();
            ParticleSystem ps = childObj.GetComponent<ParticleSystem>();

            if (mf != null || rend != null || ps != null)
            {
                if (childObj.GetComponent<Collider>() == null)
                {
                    if (mf != null && mf.sharedMesh != null)
                    {
                        try
                        {
                            var mc = childObj.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = true;
                            mc.isTrigger = true;
                        }
                        catch
                        {
                            var bc = childObj.AddComponent<BoxCollider>();
                            bc.isTrigger = true;
                        }
                    }
                    else
                    {
                        var bc = childObj.AddComponent<BoxCollider>();
                        bc.isTrigger = true;
                        if (rend != null)
                        {
                            bc.center = childObj.transform.InverseTransformPoint(rend.bounds.center);
                            Vector3 rSize = rend.bounds.size;
                            Vector3 cScale = childObj.transform.lossyScale;
                            bc.size = new Vector3(
                                rSize.x / Mathf.Max(0.001f, cScale.x),
                                rSize.y / Mathf.Max(0.001f, cScale.y),
                                rSize.z / Mathf.Max(0.001f, cScale.z)
                            );
                        }
                    }
                }
                else
                {
                    var existingCol = childObj.GetComponent<Collider>();
                    if (existingCol != null) existingCol.isTrigger = true;
                }

                // Gắn thêm EarthBlastDamageZone cho từng child object
                var childDmg = childObj.GetComponent<EarthBlastDamageZone>();
                if (childDmg == null) childDmg = childObj.AddComponent<EarthBlastDamageZone>();
                childDmg.damage = damageAmount;
            }
        }
    }
}


