using UnityEngine;
using Unity.Netcode;

public class PressurePlateDoor : NetworkBehaviour
{
    [Header("--- CẤU HÌNH CỬA ---")]
    public Transform doorTransform; // Kéo object Cửa thật vào đây
    [Tooltip("Khoảng cách trượt (VD: X = 5 là cửa trượt ngang 5m)")]
    public Vector3 slideOffset;     // Nhập số mét cửa sẽ trượt đi
    public float doorSpeed = 5f;    // Tốc độ mở cửa

    [Header("--- CẤU HÌNH NÚT ĐẠP ---")]
    public Transform buttonTransform; // Kéo cục NÚT (Prefab_brick_17 (2)) vào đây
    public float sinkDistance = 0.15f;// Lún bao nhiêu? (Gợi ý: 0.15)
    public float buttonSpeed = 5f;    // Tốc độ lún

    // Biến nội bộ tự động lưu vị trí gốc lúc mới vào game
    private Vector3 doorInitialPos;
    private float buttonInitialY;
    private bool isInitialized = false;

    // Biến đồng bộ mạng: true là đang bị dẫm, false là thả ra
    public NetworkVariable<bool> isPressed = new NetworkVariable<bool>(false);

    // Đếm số người đang đứng trên nút
    private int playersOnPlate = 0;

    void Start()
    {
        // Tự động chốt tọa độ gốc của Cửa và Nút khi vừa ấn Play
        if (doorTransform != null) doorInitialPos = doorTransform.localPosition;
        if (buttonTransform != null) buttonInitialY = buttonTransform.localPosition.y;
        
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        // 1. Xử lý di chuyển CỬA
        if (doorTransform != null)
        {
            // Nếu bị dẫm -> Tọa độ mới = Tọa độ gốc + Khoảng cách trượt
            // Nếu nhả ra -> Quay về tọa độ gốc
            Vector3 targetDoorPos = isPressed.Value ? (doorInitialPos + slideOffset) : doorInitialPos;
            doorTransform.localPosition = Vector3.Lerp(doorTransform.localPosition, targetDoorPos, Time.deltaTime * doorSpeed);
        }

        // 2. Xử lý di chuyển NÚT
        if (buttonTransform != null)
        {
            float targetButtonY = isPressed.Value ? (buttonInitialY - sinkDistance) : buttonInitialY;
            Vector3 currentButtonPos = buttonTransform.localPosition;
            
            currentButtonPos.y = Mathf.Lerp(currentButtonPos.y, targetButtonY, Time.deltaTime * buttonSpeed);
            buttonTransform.localPosition = currentButtonPos;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; 
        
        if (other.CompareTag("Player")) 
        {
            playersOnPlate++;
            if (playersOnPlate > 0)
            {
                isPressed.Value = true; 
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        
        if (other.CompareTag("Player"))
        {
            playersOnPlate--;
            if (playersOnPlate <= 0)
            {
                playersOnPlate = 0;
                isPressed.Value = false; 
            }
        }
    }
}