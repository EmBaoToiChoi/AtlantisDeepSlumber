using UnityEngine;
using Unity.Netcode; // BẮT BUỘC dùng Netcode
using System.Collections.Generic;

public class BookManager : NetworkBehaviour
{
    [Header("Cấu hình Sách Mật Mã")]
    [Tooltip("Kéo Prefab cuốn sách có gắn NetworkObject vào đây")]
    public GameObject bookPrefab;

    [Header("Danh sách Vị Trí Spawn")]
    [Tooltip("Kéo toàn bộ 20 điểm Spawn sách trên kệ vào đây")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Số lượng sách cần tạo")]
    public int totalBooksToSpawn = 8;

    // Hàm này chạy khi hệ thống mạng khởi động (Host/Server bật lên)
    public override void OnNetworkSpawn()
    {
        // QUY TẮC VÀNG: Chỉ Server mới được quyền quyết định vị trí ngẫu nhiên và sinh vật phẩm
        if (!IsServer) return;

        SpawnBooksRandomly();
    }

    private void SpawnBooksRandomly()
    {
        if (bookPrefab == null)
        {
            Debug.LogError("Chưa kéo Prefab cuốn sách vô BookManager kìa ông ơi!");
            return;
        }

        if (spawnPoints.Count < totalBooksToSpawn)
        {
            Debug.LogError("Số lượng điểm spawn ít hơn số sách muốn tạo rồi!");
            return;
        }

        // Tạo một danh sách phụ từ danh sách gốc để trích xuất ngẫu nhiên không bị trùng vị trí
        List<Transform> availablePoints = new List<Transform>(spawnPoints);

        for (int i = 0; i < totalBooksToSpawn; i++)
        {
            // 1. Chọn ngẫu nhiên một chỉ số trong danh sách vị trí còn trống
            int randomIndex = Random.Range(0, availablePoints.Count);
            Transform selectedPoint = availablePoints[randomIndex];

            // 2. Tạo cuốn sách ra tại vị trí đó bằng lệnh Unity gốc trên Server
            GameObject spawnedBook = Instantiate(bookPrefab, selectedPoint.position, selectedPoint.rotation);

            // 3. Lấy NetworkObject của cuốn sách ra và ra lệnh phát sóng đồng bộ xuống tất cả Client
            NetworkObject netObj = spawnedBook.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
            else
            {
                Debug.LogError($"Prefab {bookPrefab.name} chưa gắn component NetworkObject!");
            }

            // 4. QUAN TRỌNG: Loại bỏ điểm vừa chọn ra khỏi danh sách phụ để cuốn sách sau không bị spawn đè lên cuốn sách trước
            availablePoints.RemoveAt(randomIndex);
        }

        Debug.Log($"[Server] Đã rải ngẫu nhiên thành công {totalBooksToSpawn} cuốn sách mật mã lên các kệ!");
    }
}