using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

// Đổi thành NetworkBehaviour
public class DestroyReporter : NetworkBehaviour
{
    [HideInInspector]
    public UnityEvent OnTargetDestroyed = new UnityEvent();

    // Trong Netcode, dùng OnNetworkDespawn thay cho OnDestroy
    public override void OnNetworkDespawn()
    {
        // CHỈ CÓ SERVER mới được quyền báo cáo sự kiện chết để tính điểm
        if (IsServer && OnTargetDestroyed != null)
        {
            OnTargetDestroyed.Invoke();
        }
    }
}