using UnityEngine;
public class SpellBall : MonoBehaviour {
    private void Start() { Destroy(gameObject, 5f); } // Tự hủy sau 5 giây tránh rác bộ nhớ
    private void OnTriggerEnter(Collider other) {
        // Logic gây sát thương nếu trúng Player ở đây (Script AI đã tích hợp sẵn cơ chế dự phòng rồi)
        Destroy(gameObject);
    }
}
