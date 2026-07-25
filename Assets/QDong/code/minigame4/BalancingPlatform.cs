using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BalancingPlatform : NetworkBehaviour
{
    [Header("Cài đặt Lực đè")]
    public float playerWeightForce = 50f; // Sức nặng của mỗi người chơi
    
    private Rigidbody rb;
    private Dictionary<Transform, int> playersOnBoard = new Dictionary<Transform, int>();

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
    }

    // Khi người chơi nhảy lên đĩa
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Chỉ Server mới quản lý danh sách này

        // Check xem có đúng là Player không (nhớ tag nhân vật là "Player")
        if (other.CompareTag("Player"))
        {
            Transform rootT = other.transform.root;
            if (!playersOnBoard.ContainsKey(rootT))
            {
                playersOnBoard[rootT] = 0;
            }
            playersOnBoard[rootT]++;
        }
    }

    // Khi người chơi rớt hoặc nhảy ra khỏi đĩa
    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            Transform rootT = other.transform.root;
            if (playersOnBoard.ContainsKey(rootT))
            {
                playersOnBoard[rootT]--;
                if (playersOnBoard[rootT] <= 0)
                {
                    playersOnBoard.Remove(rootT);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return; // Chốt chặn an toàn: Chỉ Server chạy code này

        // Dọn dẹp list nếu có player nào lỡ bị destroy (out game)
        List<Transform> keysToRemove = new List<Transform>();
        foreach (var key in playersOnBoard.Keys)
        {
            if (key == null) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
        {
            playersOnBoard.Remove(key);
        }

        // Áp dụng trọng lượng của từng người chơi lên đĩa
        foreach (var player in playersOnBoard.Keys)
        {
            // Lấy vị trí của người chơi
            Vector3 playerPos = player.position;
            
            // Ép một lực hướng thẳng xuống (Vector3.down) ngay tại vị trí người chơi đang đứng
            // Do đĩa có RigidBody và Joint ở tâm, ép lực ở rìa tự khắc nó sẽ nghiêng (Đòn bẩy)
            rb.AddForceAtPosition(Vector3.down * playerWeightForce, playerPos, ForceMode.Force);
        }
    }
}