using UnityEngine;
using Unity.Netcode;

public class SceneTransitionTrigger : NetworkBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Tên của scene muốn chuyển tới")]
    public string targetSceneName = "Map2";

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ Server/Host mới có thẩm quyền chuyển scene đồng bộ cho tất cả người chơi
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        // Kiểm tra xem đối tượng va chạm có phải là một người chơi không
        if (IsPlayer(other.gameObject))
        {
            Debug.Log($"[SceneTransitionTrigger] Phát hiện người chơi '{other.gameObject.name}' chạm vào cổng dịch chuyển. Đang chuyển sang scene '{targetSceneName}'...");
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.SceneManager != null)
            {
                // Gọi SceneManager của Netcode để tự động load scene và kéo tất cả Client đi theo một cách đồng bộ
                NetworkManager.Singleton.SceneManager.LoadScene(targetSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                // Fallback cho chế độ chơi đơn (Offline/Standalone)
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem GameObject va chạm có phải là một trong 4 loại nhân vật player hay không
    /// </summary>
    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;

        // Kiểm tra trực tiếp trên Object va chạm hoặc trên Object cha (nếu collider nằm ở child object)
        if (go.GetComponent<LeoPlayer>() != null || go.GetComponentInParent<LeoPlayer>() != null) return true;
        if (go.GetComponent<ArthurPlayer>() != null || go.GetComponentInParent<ArthurPlayer>() != null) return true;
        if (go.GetComponent<ElenaPlayer>() != null || go.GetComponentInParent<ElenaPlayer>() != null) return true;
        if (go.GetComponent<MayaPlayer>() != null || go.GetComponentInParent<MayaPlayer>() != null) return true;

        // Fallback kiểm tra tag
        if (go.CompareTag("Player") || (go.transform.parent != null && go.transform.parent.CompareTag("Player"))) return true;

        return false;
    }
}
