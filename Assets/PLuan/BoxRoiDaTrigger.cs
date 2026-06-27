using UnityEngine;
using Unity.Netcode;

public class BoxRoiDaTrigger : NetworkBehaviour
{
    [Header("GameObjects to Toggle")]
    [Tooltip("Object Đá Chặn Cửa 1")]
    public GameObject DaChanCua1;
    [Tooltip("Object Đá Chặn Cửa 2")]
    public GameObject DaChanCua2;
    [Tooltip("Object Đá Chặn Cửa Chưa Rơi (sẽ bị tắt đi)")]
    public GameObject daChanCuaChuaRoi;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    private bool hasFallen = false;

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem đối tượng va chạm có phải là người chơi hay không
        if (IsPlayer(other.gameObject))
        {
            if (IsNetworkActive)
            {
                // Chỉ Server mới nhận va chạm và phát lệnh đồng bộ cho tất cả Client
                if (IsServer)
                {
                    ExecuteRockFall(); // Server chạy trước để cập nhật logic trạng thái
                    TriggerRockFallClientRpc(); // Đồng bộ sang các client khác
                }
            }
            else
            {
                // Fallback chạy cục bộ khi chơi Offline/Standalone
                ExecuteRockFall();
            }
        }
    }

    [ClientRpc]
    private void TriggerRockFallClientRpc()
    {
        ExecuteRockFall();
    }

    private void ExecuteRockFall()
    {
        if (hasFallen) return;
        hasFallen = true;

        Debug.Log($"[BoxRoiDaTrigger] Kích hoạt chuyển đổi trạng thái đá rơi.");

        // Kích hoạt đá 1
        if (DaChanCua1 != null)
        {
            DaChanCua1.SetActive(true);
            ActivateAllChildrenRecursive(DaChanCua1.transform);
            
            var puzzles1 = DaChanCua1.GetComponentsInChildren<ElementalRockPuzzle>(true);
            if (puzzles1 != null && puzzles1.Length > 0)
            {
                foreach (var puzzle in puzzles1)
                {
                    puzzle.ShowRock();
                }
                Debug.Log($"[BoxRoiDaTrigger] Đã gọi ShowRock() cho {puzzles1.Length} viên đá con của DaChanCua1.");
            }
        }

        // Kích hoạt đá 2
        if (DaChanCua2 != null)
        {
            DaChanCua2.SetActive(true);
            ActivateAllChildrenRecursive(DaChanCua2.transform);

            var puzzles2 = DaChanCua2.GetComponentsInChildren<ElementalRockPuzzle>(true);
            if (puzzles2 != null && puzzles2.Length > 0)
            {
                foreach (var puzzle in puzzles2)
                {
                    puzzle.ShowRock();
                }
                Debug.Log($"[BoxRoiDaTrigger] Đã gọi ShowRock() cho {puzzles2.Length} viên đá con của DaChanCua2.");
            }
        }

        // Tắt đá chưa rơi
        if (daChanCuaChuaRoi != null)
        {
            daChanCuaChuaRoi.SetActive(false);
        }

        // Ẩn chính BoxRoiDa (GameObject chứa script trigger này) để tránh kích hoạt lại
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Kích hoạt đệ quy tất cả GameObject con (từ trên xuống dưới).
    /// Đảm bảo các prefab con đang inactive cũng được bật lên.
    /// </summary>
    private void ActivateAllChildrenRecursive(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            child.gameObject.SetActive(true);
            ActivateAllChildrenRecursive(child);
        }
    }

    /// <summary>
    /// Kiểm tra xem GameObject va chạm có phải là một trong các loại nhân vật player hay không
    /// </summary>
    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;

        // Kiểm tra các component player có trong dự án
        if (go.GetComponent<LeoPlayer>() != null || go.GetComponentInParent<LeoPlayer>() != null) return true;
        if (go.GetComponent<ArthurPlayer>() != null || go.GetComponentInParent<ArthurPlayer>() != null) return true;
        if (go.GetComponent<ElenaPlayer>() != null || go.GetComponentInParent<ElenaPlayer>() != null) return true;
        if (go.GetComponent<MayaPlayer>() != null || go.GetComponentInParent<MayaPlayer>() != null) return true;

        // Fallback kiểm tra tag
        if (go.CompareTag("Player") || (go.transform.parent != null && go.transform.parent.CompareTag("Player"))) return true;

        return false;
    }
}
