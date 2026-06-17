using UnityEngine;
using Unity.Netcode;

public class BoxRoiDaTrigger : NetworkBehaviour
{
    [Header("GameObjects to Toggle")]
    [Tooltip("Object Đá Chặn Cửa Đã Rơi (sẽ được kích hoạt)")]
    public GameObject daChanCuaDaRoi;

    [Tooltip("Object Đá Chặn Cửa Chưa Rơi (sẽ bị tắt đi)")]
    public GameObject daChanCuaChuaRoi;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

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
