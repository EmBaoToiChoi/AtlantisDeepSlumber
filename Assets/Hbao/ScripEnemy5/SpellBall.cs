using UnityEngine;
using Unity.Netcode; // BẮT BUỘC phải có thư viện này

public class SpellBall : NetworkBehaviour // Đổi từ MonoBehaviour sang NetworkBehaviour
{
    private float lifeTimer = 5f;

    private void Update()
    {
        // Chỉ Server mới có quyền đếm giờ tự hủy quả cầu
        if (!IsServer) return;

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ Server mới có quyền tính sát thương và xóa vật thể
        if (!IsServer) return;

        // Logic gây sát thương nếu trúng Player ở đây...

        // Xóa quả cầu khỏi mạng lưới thay vì dùng Destroy
        GetComponent<NetworkObject>().Despawn();
    }
}