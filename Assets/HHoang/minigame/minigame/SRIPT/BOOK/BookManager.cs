using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BookManager : NetworkBehaviour
{
    [Header("Cấu hình Sách Mật Mã")]
    [Tooltip("Kéo Prefab cuốn sách có gắn NetworkObject vào đây")]
    public GameObject bookPrefab;

    [Header("Danh sách Vị Trí Spawn")]
    [Tooltip("Kéo toàn bộ các điểm Spawn sách trên kệ vào đây")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("THIẾT LẬP 5 CUỐN SÁCH MẬT MÃ THẬT")]
    [Tooltip("Điền 5 ký hiệu thật để giải đố (Đã đổi thành các ký tự siêu an toàn font)")]
    public List<string> realSymbols = new List<string> { "▲", "■", "●", "✦", "♦" };
    
    [Tooltip("Điền 5 con số chuẩn tương ứng với 5 ký hiệu thật ở trên")]
    public List<int> realNumbers = new List<int> { 1, 2, 3, 4, 5 };

    [Header("THIẾT LẬP 3 CUỐN SÁCH GIẢ / ẢO")]
    [Tooltip("Điền 3 ký hiệu giả hoàn toàn khác biệt để đánh lừa (Đã đổi thành các ký tự siêu an toàn font)")]
    public List<string> fakeSymbols = new List<string> { "X", "♣", "♠" };

    [Tooltip("Điền 3 con số ảo đi kèm with 3 ký hiệu giả ở trên (Ví dụ: 6, 7, 8)")]
    public List<int> fakeNumbers = new List<int> { 6, 7, 8 };

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        SpawnRealAndFakeBooks();
    }

    private void SpawnRealAndFakeBooks()
    {
        if (bookPrefab == null)
        {
            Debug.LogError("Chưa kéo Prefab cuốn sách vô BookManager kìa ông ơi!");
            return;
        }

        int totalBooksToSpawn = realSymbols.Count + fakeSymbols.Count; 

        if (spawnPoints.Count < totalBooksToSpawn)
        {
            Debug.LogError($"Số lượng điểm spawn ({spawnPoints.Count}) ít hơn tổng số sách cần tạo ({totalBooksToSpawn})!");
            return;
        }

        if (realSymbols.Count != realNumbers.Count || fakeSymbols.Count != fakeNumbers.Count)
        {
            Debug.LogError("CẢNH BÁO: Số lượng Ký Hiệu và Số lượng Số đang không khớp nhau!");
            return;
        }

        List<string> finalSymbols = new List<string>();
        List<int> finalNumbers = new List<int>();
        List<bool> finalIsReal = new List<bool>(); 

        for (int i = 0; i < realSymbols.Count; i++)
        {
            finalSymbols.Add(realSymbols[i]);
            finalNumbers.Add(realNumbers[i]);
            finalIsReal.Add(true); 
        }

        for (int i = 0; i < fakeSymbols.Count; i++)
        {
            finalSymbols.Add(fakeSymbols[i]);
            finalNumbers.Add(fakeNumbers[i]);
            finalIsReal.Add(false); 
        }

        // Xáo trộn sách ngẫu nhiên
        for (int i = 0; i < finalSymbols.Count; i++)
        {
            int randomIndex = Random.Range(i, finalSymbols.Count);
            
            string tempSymbol = finalSymbols[i];
            finalSymbols[i] = finalSymbols[randomIndex];
            finalSymbols[randomIndex] = tempSymbol;

            int tempNumber = finalNumbers[i];
            finalNumbers[i] = finalNumbers[randomIndex];
            finalNumbers[randomIndex] = tempNumber;

            bool tempIsReal = finalIsReal[i];
            finalIsReal[i] = finalIsReal[randomIndex];
            finalIsReal[randomIndex] = tempIsReal;
        }

        List<Transform> availablePoints = new List<Transform>(spawnPoints);

        for (int i = 0; i < totalBooksToSpawn; i++)
        {
            int pointIndex = Random.Range(0, availablePoints.Count);
            Transform selectedPoint = availablePoints[pointIndex];

            // 1. Tạo bản sao Object trên Server trước
            GameObject spawnedBook = Instantiate(bookPrefab, selectedPoint.position, selectedPoint.rotation);

            // 2. PHẢI CÓ DÒNG NÀY: Gọi Network Spawn kích hoạt kết nối mạng trước!
            NetworkObject netObj = spawnedBook.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
            else
            {
                Debug.LogError($"Prefab {bookPrefab.name} chưa gắn component NetworkObject!");
                continue;
            }

            // 3. Sau khi đã Spawn mạng xong xuôi, tiến hành nạp data chuẩn vào NetworkVariable mà không lo bị reset về 0
            InteractableBook bookScript = spawnedBook.GetComponent<InteractableBook>();
            if (bookScript != null)
            {
                bookScript.SetupBookData(finalSymbols[i], finalNumbers[i], finalIsReal[i]);
            }

            availablePoints.RemoveAt(pointIndex);
        }

        Debug.Log($"[Server] Đã rải thành công {totalBooksToSpawn} cuốn sách lên mạng!");
    }
}