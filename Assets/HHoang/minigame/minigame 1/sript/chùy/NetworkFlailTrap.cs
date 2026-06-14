using UnityEngine;
using Unity.Netcode;
using System.Collections; // Phải gọi thư viện này ra mới xài đếm ngược (Coroutine) được nha

public class NetworkFlailTrap : NetworkBehaviour
{
    [Header("Kéo cái Object chứa Box Collider làm bẫy vào đây")]
    public BoxCollider vungKichHoat;

    [Header("Thời gian trễ (Delay)")]
    [Tooltip("Để 0 là rớt liền. Để 1 là dẫm trúng 1s sau mới rớt.")]
    public float delayTime = 0f;

    [Header("Trục lật (Chỉ để 1 trục là 1)")]
    public float axisX = 1f;
    public float axisY = 0f;
    public float axisZ = 0f;

    [Header("Cài đặt bẫy")]
    [Tooltip("Góc treo búa lúc chờ (90 là ngang)")]
    public float startAngle = 90f; 
    [Tooltip("Tốc độ lật qua lật lại")]
    public float swingSpeed = 3f;

    private NetworkVariable<bool> isTriggered = new NetworkVariable<bool>(false);
    private float timer = 0f;
    
    // Biến này để nhớ góc xoay lúc đầu của ông trên Scene
    private Quaternion gocXoayBanDau;

    // Cờ đánh dấu xem bẫy đang trong lúc chờ rớt không (để không bị đếm đè nhiều lần)
    private bool isCountingDown = false;

    void Start()
    {
        // Lưu lại vị trí góc xoay ông set up trong Scene
        gocXoayBanDau = transform.localRotation;
        
        // Tự động kéo búa lên vị trí chờ 90 độ
        SetAngle(startAngle);
    }

    void Update()
    {
        // 1. Quét vùng cảm ứng dưới đất (Thêm check !isCountingDown để đang đếm ngược thì không quét nữa)
        if (IsServer && !isTriggered.Value && !isCountingDown && vungKichHoat != null)
        {
            Collider[] hitColliders = Physics.OverlapBox(
                vungKichHoat.bounds.center, 
                vungKichHoat.bounds.extents, 
                vungKichHoat.transform.rotation
            );

            foreach (var hit in hitColliders)
            {
                if (hit.CompareTag("Player"))
                {
                    // Phát hiện Player là chạy hàm đếm ngược thả búa
                    StartCoroutine(DemNguocTruocKhiSap());
                    break;
                }
            }
        }

        // 2. Vung qua vung lại bằng công thức con lắc
        if (isTriggered.Value)
        {
            timer += Time.deltaTime;
            
            // Tạo dao động từ startAngle đến -startAngle mượt mà
            float angle = startAngle * Mathf.Cos(timer * swingSpeed);
            SetAngle(angle);
        }
    }

    // Hàm chuyên xử lý đếm ngược thời gian
    private IEnumerator DemNguocTruocKhiSap()
    {
        isCountingDown = true; // Khóa chốt lại, mấy thằng khác dẫm vô sau không làm đếm lại

        // Nếu ông chỉnh thời gian delay lớn hơn 0 thì nó mới đứng chờ
        if (delayTime > 0f)
        {
            yield return new WaitForSeconds(delayTime);
        }
        
        isTriggered.Value = true; // Hết giờ, lật cái rụp!
    }

    private void SetAngle(float angle)
    {
        Vector3 swingAxis = new Vector3(axisX, axisY, axisZ).normalized;
        // Nhân thêm góc xoay ban đầu để búa không bị lệch hướng
        transform.localRotation = gocXoayBanDau * Quaternion.AngleAxis(angle, swingAxis);
    }
}