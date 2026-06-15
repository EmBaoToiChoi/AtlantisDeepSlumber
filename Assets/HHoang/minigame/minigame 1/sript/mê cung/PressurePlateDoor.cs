using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic; // Bắt buộc phải có dòng này để xài List

// Tạo một cấu trúc để gom các thông số của 1 cánh cửa lại với nhau
[System.Serializable] 
public class DoorConfig
{
    public Transform doorTransform; // Kéo object Cửa vào đây
    public Vector3 slideOffset;     // Khoảng cách trượt (VD: X = 5, Y = -3...)
    public float doorSpeed = 5f;    // Tốc độ di chuyển của riêng cửa này

    [HideInInspector]
    public Vector3 initialPos;      // Biến ẩn để code tự động lưu tọa độ gốc
}

public class PressurePlateDoor : NetworkBehaviour
{
    [Header("--- DANH SÁCH CÁC CỬA ---")]
    // Tạo một List chứa các cấu hình cửa ở trên
    public List<DoorConfig> doors = new List<DoorConfig>();

    [Header("--- CẤU HÌNH NÚT ĐẠP ---")]
    public Transform buttonTransform; // Kéo cục Nút vào đây
    public float sinkDistance = 0.15f;// Lún bao nhiêu?
    public float buttonSpeed = 5f;    // Tốc độ lún

    // Biến nội bộ
    private float buttonInitialY;
    private bool isInitialized = false;

    // Biến đồng bộ mạng
    public NetworkVariable<bool> isPressed = new NetworkVariable<bool>(false);

    private int playersOnPlate = 0;

    void Start()
    {
        // 1. Quét qua toàn bộ danh sách cửa và tự động lưu tọa độ gốc của từng cái
        foreach (var door in doors)
        {
            if (door.doorTransform != null)
            {
                door.initialPos = door.doorTransform.localPosition;
            }
        }

        // 2. Lưu tọa độ gốc của Nút
        if (buttonTransform != null) buttonInitialY = buttonTransform.localPosition.y;
        
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        // 1. Xử lý di chuyển TẤT CẢ các cửa trong List
        foreach (var door in doors)
        {
            if (door.doorTransform != null)
            {
                // Tính tọa độ đích cho từng cửa
                Vector3 targetDoorPos = isPressed.Value ? (door.initialPos + door.slideOffset) : door.initialPos;
                // Di chuyển cửa mượt mà
                door.doorTransform.localPosition = Vector3.Lerp(door.doorTransform.localPosition, targetDoorPos, Time.deltaTime * door.doorSpeed);
            }
        }

        // 2. Xử lý di chuyển NÚT (Giữ nguyên như cũ)
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