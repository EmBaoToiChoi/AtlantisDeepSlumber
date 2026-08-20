using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BalancingPlatform : NetworkBehaviour
{
    [Header("Cài đặt Lực đè")]
    public float playerWeightForce = 50f; // Sức nặng của mỗi người chơi
    
    private Rigidbody rb;
    private HashSet<Collider> playersOnBoard = new HashSet<Collider>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Nếu không phải Server, tắt mô phỏng vật lý của cái đĩa đi
        // Để Client không tự tính toán lung tung, chỉ nhận góc xoay từ Server về
        if (!IsServer)
        {
            rb.isKinematic = true; 
        }

        // [TỰ ĐỘNG SỬA LỖI XOAY VÀ LỆCH CỘT TRỤ BẰNG CODE]
        if (IsServer)
        {
            // 1. Ép khóa cứng trục xoay ngang (Y-axis) của ConfigurableJoint để đĩa CHỈ NGHIÊNG, KHÔNG XOAY.
            ConfigurableJoint joint = GetComponent<ConfigurableJoint>();
            if (joint != null)
            {
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                Debug.Log("[BalancingPlatform] Đã tự động khóa trục xoay Y của Joint. Đĩa sẽ không bao giờ bị xoay mòng mòng nữa.");
            }
        }

        // 2. Ép tắt toàn bộ NetworkTransform của các cột trụ con (nếu có) để tránh lỗi giật lùi/lệch cột khi đĩa nghiêng.
        Unity.Netcode.Components.NetworkTransform[] childNetTransforms = GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>();
        foreach (var nt in childNetTransforms)
        {
            // Bỏ qua NetworkTransform của chính cái đĩa, chỉ tắt của các cột con
            if (nt.gameObject != this.gameObject)
            {
                nt.enabled = false;
                Debug.Log($"[BalancingPlatform] Đã tự động tắt NetworkTransform trên cột {nt.gameObject.name} để tránh lỗi lệch.");
            }
        }
    }

    // Khi người chơi nhảy lên đĩa
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Chỉ Server mới quản lý danh sách này

        // Check xem có đúng là Player không (nhớ tag nhân vật là "Player")
        if (other.CompareTag("Player"))
        {
            playersOnBoard.Add(other);
        }
    }

    // Khi người chơi rớt hoặc nhảy ra khỏi đĩa
    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            playersOnBoard.Remove(other);
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return; // Chốt chặn an toàn: Chỉ Server chạy code này

        // Dọn dẹp list nếu có player nào lỡ bị destroy (out game)
        playersOnBoard.RemoveWhere(p => p == null);

        // Áp dụng trọng lượng của từng người chơi lên đĩa
        foreach (var player in playersOnBoard)
        {
            // Lấy vị trí của người chơi
            Vector3 playerPos = player.transform.position;
            
            // Ép một lực hướng thẳng xuống (Vector3.down) ngay tại vị trí người chơi đang đứng
            // Do đĩa có RigidBody và Joint ở tâm, ép lực ở rìa tự khắc nó sẽ nghiêng (Đòn bẩy)
            rb.AddForceAtPosition(Vector3.down * playerWeightForce, playerPos, ForceMode.Force);
        }
    }
}