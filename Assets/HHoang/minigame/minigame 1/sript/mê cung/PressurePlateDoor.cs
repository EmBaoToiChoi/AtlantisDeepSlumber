using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[System.Serializable] 
public class DoorConfig
{
    public Transform doorTransform; 
    public Vector3 slideOffset;     
    public float doorSpeed = 5f;    

    [HideInInspector]
    public Vector3 initialPos;      
}

public class PressurePlateDoor : NetworkBehaviour
{
    // [THÊM MỚI] Danh sách toàn cục lưu tất cả các nút đang có trong game
    public static List<PressurePlateDoor> allPlates = new List<PressurePlateDoor>();
    
    // [THÊM MỚI] Chống lỗi cửa chạy nhanh gấp đôi khi có 2 nút cùng điều khiển
    private static int lastFrameCount = -1;
    private static HashSet<Transform> updatedDoorsThisFrame = new HashSet<Transform>();

    [Header("--- DANH SÁCH CÁC CỬA ---")]
    public List<DoorConfig> doors = new List<DoorConfig>();

    [Header("--- CẤU HÌNH NÚT ĐẠP ---")]
    public Transform buttonTransform; 
    public float sinkDistance = 0.15f;
    public float buttonSpeed = 2f;    

    private float buttonInitialY;
    private bool isInitialized = false;

    public NetworkVariable<bool> isPressed = new NetworkVariable<bool>(false);

    private int playersOnPlate = 0;

    void Awake()
    {
        allPlates.Add(this); // Báo danh nút này vào hệ thống
    }

    void OnDestroy()
    {
        allPlates.Remove(this); // Hủy báo danh khi xóa nút
    }

    void Start()
    {
        foreach (var door in doors)
        {
            if (door.doorTransform != null)
            {
                door.initialPos = door.doorTransform.localPosition;
            }
        }

        if (buttonTransform != null) buttonInitialY = buttonTransform.localPosition.y;
        
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return; 

        // Reset bộ đếm chống lặp mỗi frame
        if (Time.frameCount != lastFrameCount)
        {
            updatedDoorsThisFrame.Clear();
            lastFrameCount = Time.frameCount;
        }

        // 1. Xử lý CỬA
        foreach (var door in doors)
        {
            if (door.doorTransform != null)
            {
                // Nếu cửa này đã được một nút khác di chuyển trong frame này rồi thì bỏ qua
                if (updatedDoorsThisFrame.Contains(door.doorTransform)) continue;

                // KIỂM TRA ĐỒNG ĐỘI: Xem có NÚT NÀO BẤT KỲ đang bị dẫm mà cùng chung cánh cửa này không?
                bool shouldOpenDoor = false;
                foreach (var plate in allPlates)
                {
                    if (plate.isPressed.Value)
                    {
                        foreach (var pDoor in plate.doors)
                        {
                            if (pDoor.doorTransform == door.doorTransform)
                            {
                                shouldOpenDoor = true;
                                break;
                            }
                        }
                    }
                    if (shouldOpenDoor) break;
                }

                Vector3 targetDoorPos = shouldOpenDoor ? (door.initialPos + door.slideOffset) : door.initialPos;
                door.doorTransform.localPosition = Vector3.MoveTowards(door.doorTransform.localPosition, targetDoorPos, door.doorSpeed * Time.deltaTime);
                
                // Đánh dấu là cửa này đã được xử lý xong
                updatedDoorsThisFrame.Add(door.doorTransform);
            }
        }

        // 2. Xử lý NÚT (Chỉ lún cái nút hiện tại)
        if (buttonTransform != null)
        {
            float targetButtonY = isPressed.Value ? (buttonInitialY - sinkDistance) : buttonInitialY;
            Vector3 currentButtonPos = buttonTransform.localPosition;
            
            currentButtonPos.y = Mathf.MoveTowards(currentButtonPos.y, targetButtonY, buttonSpeed * Time.deltaTime);
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