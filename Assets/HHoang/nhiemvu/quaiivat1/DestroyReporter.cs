using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class DestroyReporter : NetworkBehaviour
{
    [HideInInspector]
    public UnityEvent OnTargetDestroyed = new UnityEvent();

    private bool hasReported = false;

    public void ReportDestroyed()
    {
        if (hasReported) return;
        hasReported = true;
        OnTargetDestroyed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        // Netcode: Chỉ có Server được quyền báo cáo sự kiện chết để tính điểm
        if (IsServer)
        {
            ReportDestroyed();
        }
    }

    private void OnDestroy()
    {
        // Hỗ trợ chế độ Offline / Standalone
        bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!isNetwork)
        {
            ReportDestroyed();
        }
    }
}