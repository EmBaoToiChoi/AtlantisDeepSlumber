using UnityEngine;

public class ElementalRockPuzzle : MonoBehaviour
{
    [Header("Puzzle Settings")]
    [Tooltip("Thời gian tối đa để kích hoạt đủ 4 nguyên tố kể từ nguyên tố đầu tiên (giây)")]
    public float timeLimit = 8f;

    [Header("Element Tags Configuration")]
    [Tooltip("Tag của đạn nguyên tố Lửa")]
    public string fireTag = "Lua";
    [Tooltip("Tag của đạn nguyên tố Nước")]
    public string waterTag = "Nuoc";
    [Tooltip("Tag của đạn nguyên tố Băng")]
    public string iceTag = "Bang";
    [Tooltip("Tag của đạn nguyên tố Sét")]
    public string lightningTag = "Set";

    [Header("Status (Read Only)")]
    [SerializeField] private int currentStep = 0;
    [SerializeField] private float timeRemaining = 0f;
    [SerializeField] private bool isTimerRunning = false;

    private string[] orderedTags;
    private GameObject lastHitObject; // Tránh việc một viên đạn va chạm liên tục nhiều lần trong các frame kế tiếp

    private void Start()
    {
        // Khởi tạo thứ tự các tag nguyên tố cần bắn vào đá: Lửa -> Nước -> Băng -> Sét
        orderedTags = new string[] { fireTag, waterTag, iceTag, lightningTag };
        ResetPuzzle();
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                Debug.Log($"[ElementalRockPuzzle] Hết thời gian {timeLimit}s! Đã reset câu đố phá đá.");
                ResetPuzzle();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleElementHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleElementHit(collision.gameObject);
    }

    private void HandleElementHit(GameObject hitObj)
    {
        if (hitObj == null) return;

        // Tránh nhận liên tiếp nhiều sự kiện va chạm từ cùng một viên đạn
        if (hitObj == lastHitObject) return;

        string hitTag = hitObj.tag;

        // Kiểm tra xem đối tượng va chạm có tag thuộc một trong các nguyên tố không
        if (hitTag == fireTag || hitTag == waterTag || hitTag == iceTag || hitTag == lightningTag)
        {
            lastHitObject = hitObj;
            string expectedTag = orderedTags[currentStep];

            if (hitTag == expectedTag)
            {
                // Đúng nguyên tố tiếp theo trong chuỗi
                if (currentStep == 0)
                {
                    // Bắt đầu đếm ngược từ nguyên tố đầu tiên (Lửa)
                    isTimerRunning = true;
                    timeRemaining = timeLimit;
                    Debug.Log($"[ElementalRockPuzzle] Đã bắn trúng nguyên tố đầu tiên ({hitTag}). Bắt đầu đếm ngược {timeLimit}s!");
                }
                else
                {
                    Debug.Log($"[ElementalRockPuzzle] Bắn trúng đúng nguyên tố ({hitTag}) ở bước {currentStep + 1}/{orderedTags.Length}!");
                }

                currentStep++;

                // Nếu đã bắn trúng đủ 4 nguyên tố theo đúng thứ tự
                if (currentStep >= orderedTags.Length)
                {
                    ShatterRock();
                }
            }
            else
            {
                // Sai thứ tự nguyên tố -> Reset câu đố
                Debug.Log($"[ElementalRockPuzzle] Sai thứ tự! Yêu cầu tag '{expectedTag}' nhưng trúng tag '{hitTag}'. Reset câu đố!");
                ResetPuzzle();
            }
        }
    }

    private void ResetPuzzle()
    {
        currentStep = 0;
        timeRemaining = 0f;
        isTimerRunning = false;
        lastHitObject = null;
    }

    private void ShatterRock()
    {
        Debug.Log("[ElementalRockPuzzle] Kích hoạt thành công cả 4 nguyên tố theo đúng thứ tự! Đá đã bị phá vỡ.");
        
        // Hủy viên đá
        Destroy(gameObject);
    }
}
