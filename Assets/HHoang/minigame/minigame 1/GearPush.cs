using UnityEngine;

public class GearPush : MonoBehaviour
{
    private GearRotation gearRotation;

    [Header("Cấu hình Lực Đẩy Tối Đa (Khi bánh răng quay max tốc)")]
    public float maxPushForce = 2f; 
    public float maxUpwardForce = 0.05f;

    [Header("Giới hạn lực tối thiểu")]
    public float minPushForce = 0.05f;

    void Start()
    {
        gearRotation = GetComponent<GearRotation>();
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            float speedRatio = GetCurrentSpeedRatio();

            // Nếu cả 2 nút đã bị dẫm (bánh răng dừng hẳn), không đẩy người chơi nữa
            if (speedRatio <= 0.05f) return;

            Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                float calculatedPushForce = Mathf.Lerp(minPushForce, maxPushForce, speedRatio);
                float calculatedUpwardForce = Mathf.Lerp(0f, maxUpwardForce, speedRatio);

                Vector3 contactPoint = collision.contacts[0].point;
                Vector3 pushDirection = contactPoint - transform.position;
                pushDirection.y = 0; 
                pushDirection = pushDirection.normalized;

                pushDirection += Vector3.up * (calculatedUpwardForce / calculatedPushForce);

                // Lực đẩy sẽ tự yếu đi khi chỉ có 1 người dẫm ván (bánh răng quay chậm)
                playerRb.AddForce(pushDirection * calculatedPushForce * 10f, ForceMode.Force);
            }
        }
    }

    private float GetCurrentSpeedRatio()
    {
        if (gearRotation != null)
        {
            // Lấy tỷ lệ tốc độ trung bình từ script GearRotation
            return gearRotation.GetAverageSpeedRatio();
        }
        return 1f; 
    }
}