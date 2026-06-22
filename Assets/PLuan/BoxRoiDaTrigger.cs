using UnityEngine;
using Unity.Netcode;

public class BoxRoiDaTrigger : NetworkBehaviour
{
    [Header("GameObjects to Toggle")]
    [Tooltip("Object Đá Chặn Cửa Đã Rơi (sẽ được kích hoạt)")]
    public GameObject daChanCuaDaRoi;

    [Tooltip("Object Đá Chặn Cửa Chưa Rơi (sẽ bị tắt đi)")]
    public GameObject daChanCuaChuaRoi;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

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
                    TriggerRockFallClientRpc();
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
        Debug.Log($"[BoxRoiDaTrigger] Kích hoạt chuyển đổi trạng thái đá rơi.");

        // Kích hoạt đá đã rơi
        if (daChanCuaDaRoi != null)
        {
            daChanCuaDaRoi.SetActive(true);
            
            // Kích hoạt tất cả GameObject con đệ quy (vì con có thể đang inactive)
            ActivateAllChildrenRecursive(daChanCuaDaRoi.transform);

            // Hỗ trợ trường hợp đá được kích hoạt sẵn trong hierarchy (để tránh lỗi Netcode) nhưng dùng StartHidden
            // Tìm TẤT CẢ các script ElementalRockPuzzle trên con (có thể có nhiều viên đá con)
            var puzzles = daChanCuaDaRoi.GetComponentsInChildren<ElementalRockPuzzle>(true);
            if (puzzles != null && puzzles.Length > 0)
            {
                foreach (var puzzle in puzzles)
                {
                    puzzle.ShowRock();
                }
                Debug.Log($"[BoxRoiDaTrigger] Đã gọi ShowRock() cho {puzzles.Length} viên đá con.");
            }
            else
            {
                Debug.LogWarning("[BoxRoiDaTrigger] Không tìm thấy ElementalRockPuzzle trên con của 'daChanCuaDaRoi'!");
            }
        }
        else
        {
            Debug.LogWarning("[BoxRoiDaTrigger] Chưa gán object 'daChanCuaDaRoi'!", this);
        }

        // Tắt đá chưa rơi
        if (daChanCuaChuaRoi != null)
        {
            daChanCuaChuaRoi.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[BoxRoiDaTrigger] Chưa gán object 'daChanCuaChuaRoi'!", this);
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
