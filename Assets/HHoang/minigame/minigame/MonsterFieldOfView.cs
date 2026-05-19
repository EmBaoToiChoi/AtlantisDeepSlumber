using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class MonsterFieldOfView : NetworkBehaviour
{
    [Header("Cấu hình Tầm Nhìn")]
    [Tooltip("Bán kính vùng nhìn")]
    public float viewRadius = 10f;
    [Tooltip("Góc nhìn (độ)")]
    [Range(0, 360)]
    public float viewAngle = 145f;

    [Header("Cấu hình Mạng & Layer")]
    public LayerMask targetMask; // Layer của Player
    public LayerMask obstacleMask; // Layer của Kệ Sách/Tường

    [Header("Cấu hình Đèn Pha (Spotlight)")]
    [Tooltip("Độ sáng mạnh yếu của đèn pha rọi xuống đất")]
    public float lightIntensity = 50f;
    [Tooltip("Góc nghiêng chúi xuống đất của đèn pha (Độ)")]
    public float lightPitchAngle = 25f;

    private List<Transform> spottedPlayers = new List<Transform>();
    private MonsterAI monsterAI; 
    private Light spotlight;

    public override void OnNetworkSpawn()
    {
        monsterAI = transform.GetComponentInParent<MonsterAI>();
        
        if (monsterAI == null && transform.parent != null)
        {
            monsterAI = transform.parent.GetComponent<MonsterAI>();
        }

        // 🔥 FIX LỖI TÍM: Tự tạo đèn Spotlight chuẩn, không dùng Particle Shader cũ gây lỗi render
        CreateSearchlightEffect();

        if (IsServer)
        {
            Debug.Log("<color=green>[FOV] Đã kích hoạt hệ thống quét tầm nhìn thành công trên Server!</color>");
            InvokeRepeating("FindTargetsWithDelay", 0.5f, 0.2f);
        }
    }

    private void CreateSearchlightEffect()
    {
        // Kiểm tra xem đã có đèn pha cũ chưa để tránh sinh lặp lại khi Re-spawn
        Transform oldLight = transform.Find("Monster_Searchlight");
        if (oldLight != null) Destroy(oldLight.gameObject);

        // Tạo Object Đèn Spotlight con nằm ngay tâm đầu quái (Cube)
        GameObject lightObj = new GameObject("Monster_Searchlight");
        lightObj.transform.SetParent(this.transform);
        lightObj.transform.localPosition = Vector3.zero; 
        lightObj.transform.localRotation = Quaternion.Euler(lightPitchAngle, 0, 0); // Chúi xuống đất

        spotlight = lightObj.AddComponent<Light>();
        spotlight.type = LightType.Spot;
        spotlight.color = Color.red; // Đèn màu đỏ rực
        spotlight.intensity = lightIntensity;
        spotlight.range = viewRadius + 3f; // Tầm rọi dài hơn tầm quét một chút cho đẹp
        spotlight.spotAngle = viewAngle;   // Khớp góc quạt nhìn của quái

        // Đảm bảo đèn có thể đổ bóng vật lý xuyên qua kệ sách nếu dự án có bật Shadow
        spotlight.shadows = LightShadows.Soft;
    }

    void FindTargetsWithDelay()
    {
        FindSpottedPlayers();
    }

    private void FindSpottedPlayers()
    {
        spottedPlayers.Clear();
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            
            // Xử lý góc nhìn ngang
            Vector3 targetPosHorizontal = new Vector3(target.position.x, transform.position.y, target.position.z);
            Vector3 directionToTarget = (targetPosHorizontal - transform.position).normalized;

            if (Vector3.Angle(transform.forward, directionToTarget) < viewAngle / 2)
            {
                float playerHeight = 2.0f;
                CapsuleCollider playerCollider = target.GetComponent<CapsuleCollider>();
                if (playerCollider != null)
                {
                    playerHeight = playerCollider.height * target.localScale.y;
                }

                Vector3 playerFoot = target.position;
                Vector3 playerCenter = target.position + Vector3.up * (playerHeight / 2f);
                Vector3 playerHead = target.position + Vector3.up * playerHeight;

                float dstToTarget = Vector3.Distance(transform.position, playerCenter);

                // Thuật toán 3 tia tam giác quét chống mù địa hình
                bool canSeeHead = !Physics.Raycast(transform.position, (playerHead - transform.position).normalized, dstToTarget, obstacleMask);
                bool canSeeCenter = !Physics.Raycast(transform.position, (playerCenter - transform.position).normalized, dstToTarget, obstacleMask);
                bool canSeeFoot = !Physics.Raycast(transform.position, (playerFoot - transform.position).normalized, dstToTarget, obstacleMask);

                Debug.DrawLine(transform.position, playerHead, canSeeHead ? Color.green : Color.yellow, 0.2f);
                Debug.DrawLine(transform.position, playerCenter, canSeeCenter ? Color.green : Color.yellow, 0.2f);
                Debug.DrawLine(transform.position, playerFoot, canSeeFoot ? Color.green : Color.yellow, 0.2f);

                if (canSeeHead || canSeeCenter || canSeeFoot)
                {
                    spottedPlayers.Add(target);
                    Debug.Log($"<color=red>🎯 [FOV] ĐÃ PHÁT HIỆN PLAYER: {target.name} bằng tầm nhìn!</color>");
                    
                    if (monsterAI != null)
                    {
                        monsterAI.SpottedPlayerByVision(target.position);
                    }
                }
            }
        }
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); 
        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2, false);

        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);

        int segments = 10; 
        Vector3 previousPoint = transform.position + viewAngleA * viewRadius;
        
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = (-viewAngle / 2) + (viewAngle / segments) * i;
            Vector3 currentDir = DirFromAngle(currentAngle, false);
            Vector3 currentPoint = transform.position + currentDir * viewRadius;
            
            Gizmos.DrawLine(transform.position, currentPoint);
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}