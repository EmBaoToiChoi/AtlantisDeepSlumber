using UnityEngine;

public class WaterFreezeDetector : MonoBehaviour
{
    private WaterPuzzleController controller;

    public void Initialize(WaterPuzzleController parentController)
    {
        controller = parentController;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject, other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject, collision.collider);
    }

    private void HandleCollision(GameObject hitObj, Collider collider)
    {
        if (controller == null) return;

        // 1. Kiểm tra va chạm với đạn Băng (Elena Ice Projectile)
        if (hitObj.CompareTag(controller.iceTag) || 
            hitObj.name.ToLower().Contains("ice") || 
            hitObj.GetComponent<ElenaIceProjectile>() != null)
        {
            controller.FreezeWater();
            return;
        }

        // 2. Kiểm tra va chạm với Player (Hazard)
        if (collider != null)
        {
            controller.HandleHazardTriggerEnter(collider);
        }
    }
}
