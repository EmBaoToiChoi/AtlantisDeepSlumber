using UnityEngine;
using Unity.Netcode;

public class CrystalSnapFollow : NetworkBehaviour
{
    private CrystalCore core;
    // Biến này để lưu vị trí cần hút về
    public Transform targetSnapPoint; 

    void Awake() => core = GetComponent<CrystalCore>();

    void LateUpdate()
    {
        // Nếu đã được lắp (isSnapped) và có trạm đích, thì hút về
        if (core != null && core.isSnapped.Value && targetSnapPoint != null)
        {
            transform.position = targetSnapPoint.position;
            transform.rotation = targetSnapPoint.rotation;
        }
    }
}