using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Bắt buộc phải có thư viện này để dùng List

public class CameraShakeTimeline : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Kéo tất cả các Camera cậu muốn rung vào danh sách này")]
    public List<Transform> cameras = new List<Transform>(); 

    [Header("Shake Settings")]
    public float shakeDuration = 0.5f;   
    public float shakeMagnitude = 0.2f;  

    private bool isShaking = false;

    // Timeline sẽ gọi hàm này
    public void TriggerShake()
    {
        // Kiểm tra xem danh sách có camera nào không
        if (!isShaking && cameras.Count > 0)
        {
            StartCoroutine(Shake());
        }
    }

    private IEnumerator Shake()
    {
        isShaking = true;
        
        // Tạo một list để lưu lại vị trí local ban đầu của TẤT CẢ camera
        List<Vector3> originalPositions = new List<Vector3>();
        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i] != null)
            {
                originalPositions.Add(cameras[i].localPosition);
            }
            else
            {
                originalPositions.Add(Vector3.zero); // Phòng trường hợp cậu để trống một ô trong list
            }
        }

        float elapsed = 0.0f;

        while (elapsed < shakeDuration)
        {
            // Tạo ra tọa độ rung. Tính 1 lần để áp dụng chung cho mọi cam, tạo cảm giác động đất đồng nhất.
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            // Áp dụng độ rung cho từng camera có trong danh sách
            for (int i = 0; i < cameras.Count; i++)
            {
                if (cameras[i] != null)
                {
                    cameras[i].localPosition = new Vector3(originalPositions[i].x + x, originalPositions[i].y + y, originalPositions[i].z);
                }
            }

            elapsed += Time.deltaTime;
            yield return null; 
        }

        // Trả tất cả camera về vị trí cũ khi hết thời gian
        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].localPosition = originalPositions[i];
            }
        }
        
        isShaking = false;
    }
}