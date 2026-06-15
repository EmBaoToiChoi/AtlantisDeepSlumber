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
        // TRẢ LẠI NHƯ CŨ: Xóa bỏ chữ !IsServer ở đây
        if (!isInitialized) return; 

        // 1. Xử lý CỬA bằng MoveTowards
        foreach (var door in doors)
        {
            if (door.doorTransform != null)
            {
                Vector3 targetDoorPos = isPressed.Value ? (door.initialPos + door.slideOffset) : door.initialPos;
                door.doorTransform.localPosition = Vector3.MoveTowards(door.doorTransform.localPosition, targetDoorPos, door.doorSpeed * Time.deltaTime);
            }
        }

        // 2. Xử lý NÚT bằng MoveTowards
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