using Unity.Netcode;
using UnityEngine;

public class SimplePlayerTest : NetworkBehaviour
{
    public float moveSpeed = 5f;
    public float damageAmount = 20f;
    public float attackRange = 3f;

    void Update()
    {
        // Chỉ điều khiển nếu là máy của mình (Local Player)
        if (!IsOwner) return;

        // 1. Logic Di chuyển đơn giản
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(moveX, 0, moveZ);
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);

        if (move != Vector3.zero)
        {
            transform.forward = move; // Xoay Cube về hướng di chuyển
        }

        // 2. Logic Tấn công bằng Chuột Trái
        if (Input.GetMouseButtonDown(0))
        {
            AttackServerRpc();
        }
    }

    [ServerRpc]
    void AttackServerRpc()
    {
        // Bắn Raycast từ vị trí Cube ra phía trước để tìm Enemy
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, attackRange))
        {
            Enemy1_DapBua enemy = hit.collider.GetComponentInParent<Enemy1_DapBua>();
            if (enemy != null)
            {
                enemy.TakeDamage(damageAmount);
                Debug.Log("Đã gây " + damageAmount + " sát thương lên " + enemy.gameObject.name);
            }
        }
        
        // Vẽ tia đỏ trong Scene để dễ nhìn thấy tầm đánh
        Debug.DrawRay(transform.position, transform.forward * attackRange, Color.red, 0.5f);
    }
}
