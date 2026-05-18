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
    public float viewAngle = 90f;
    [Tooltip("Độ sâu của vùng 'block' tầm nhìn (kệ sách)")]
    public float visionObscureDepth = 1f;

    [Header("Cấu hình Mạng & Layer")]
    public LayerMask targetMask; // Layer của Player
    public LayerMask obstacleMask; // Layer của Kệ Sách/Tường

    private List<Transform> spottedPlayers = new List<Transform>();
    private MonsterAI monsterAI; // Khai báo biến để lưu kết nối tới Quái AI cha

    void Start()
    {
        monsterAI = transform.GetComponentInParent<MonsterAI>();
        
        if (monsterAI == null && transform.parent != null)
        {
            monsterAI = transform.parent.GetComponent<MonsterAI>();
        }

        if (monsterAI == null)
        {
            Debug.LogError("🚨 LỖI: Không tìm thấy script MonsterAI trên Object cha! Ông check xem đã gắn MonsterAI vào enemyyyyyy chưa nha.");
        }

        if (IsServer)
        {
            InvokeRepeating("FindTargetsWithDelay", 0.1f, 0.2f);
        }
    }

    void FindTargetsWithDelay()
    {
        FindSpottedPlayers();
    }

    // --- HÀM TÌM NGƯỜI CHƠI TRONG VÙNG NHÌN (ĐÃ FIX SĂN PHẲNG TRỤC Y) ---
    private void FindSpottedPlayers()
    {
        spottedPlayers.Clear();
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            
            // 🔥 ĐÃ FIX CHÍ MẠNG: Đưa vị trí Player về cùng độ cao Y với cái mặt quái để tính hướng không bị cắm xuống đất
            Vector3 targetPositionAtSameHeight = new Vector3(target.position.x, transform.position.y, target.position.z);
            Vector3 directionToTarget = (targetPositionAtSameHeight - transform.position).normalized;

            // Kiểm tra góc nhìn dựa trên hướng đã san phẳng Y
            if (Vector3.Angle(transform.forward, directionToTarget) < viewAngle / 2)
            {
                float dstToTarget = Vector3.Distance(transform.position, targetPositionAtSameHeight);

                // Bắn tia Raycast song song với mặt đất (không lo bị đập trúng sàn nhà)
                if (!Physics.Raycast(transform.position, directionToTarget, dstToTarget, obstacleMask))
                {
                    spottedPlayers.Add(target);
                    
                    // Ra lệnh cho quái cha dí liền!
                    if (monsterAI != null)
                    {
                        monsterAI.SpottedPlayerByVision(target.position);
                    }
                }
            }
        }
    }

    // --- HÀM TÍNH HƯỚNG THEO GÓC ---
    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }

    // --- HÀM VẼ VÙNG NHÌN TRONG EDITOR ---
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f); 
        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2, false);

        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);

        Gizmos.color = new Color(1f, 0f, 0f, 0.1f); 
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

        Gizmos.color = Color.red;
        for (int i = 0; i < spottedPlayers.Count; i++)
        {
            if (spottedPlayers[i] != null)
            {
                Gizmos.DrawLine(transform.position, spottedPlayers[i].position);
            }
        }
    }
}