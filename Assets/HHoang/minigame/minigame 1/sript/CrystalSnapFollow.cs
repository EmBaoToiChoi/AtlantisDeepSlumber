using UnityEngine;
using Unity.Netcode;

public class CrystalSnapFollow : NetworkBehaviour
{
    private CrystalCore core;
    public Transform targetSnapPoint; 

    void Awake() => core = GetComponent<CrystalCore>();

    void LateUpdate()
    {
        if (core == null) return;

        // Nếu đã khóa (Snapped), vật thể không còn di chuyển tự do
        if (core.isSnapped.Value && targetSnapPoint != null)
        {
            // Chỉ cần cập nhật trên Server, NetworkTransform sẽ đồng bộ tới Client
            if (IsServer)
            {
                if (transform.position != targetSnapPoint.position || transform.rotation != targetSnapPoint.rotation)
                {
                    transform.SetPositionAndRotation(targetSnapPoint.position, targetSnapPoint.rotation);
                }
            }
        }
    }
}