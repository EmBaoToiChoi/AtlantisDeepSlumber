using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class LeverDoor : NetworkBehaviour
{
    // Danh sách toàn cục lưu tất cả các Cần gạt trong game
    public static List<LeverDoor> allLevers = new List<LeverDoor>();
    
    // Chống lỗi cửa di chuyển thừa khi có nhiều cần gạt cùng điều khiển 1 cửa
    private static int lastFrameCount = -1;
    private static HashSet<Transform> updatedDoorsThisFrame = new HashSet<Transform>();

    [Header("--- DANH SÁCH CÁC CỬA ---")]
    public List<DoorConfig> doors = new List<DoorConfig>();

    [Header("--- CẤU HÌNH CẦN GẠT (LEVER) ---")]
    [Tooltip("Kéo object tay cầm cần gạt (Lever) vào đây")]
    public Transform leverHandle; 
    
    [Tooltip("Góc xoay xuống của cần gạt khi giữ F (Ví dụ: X = 60 nghĩa là gạt xuống 60 độ)")]
    public Vector3 pulledRotationOffset = new Vector3(60f, 0f, 0f); 
    
    [Tooltip("Tốc độ quay của cần gạt (độ/giây)")]
    public float leverRotateSpeed = 180f; 

    [Header("--- TƯƠNG TÁC PHÍM ---")]
    [Tooltip("Phím cần giữ để gạt cần (Mặc định là F)")]
    public KeyCode interactKey = KeyCode.F;
    
    [Tooltip("(Tuỳ chọn) Kéo UI hiển thị gợi ý 'Giữ F' vào đây")]
    public GameObject interactUI; 

    [Header("--- ÂM THANH (TUỲ CHỌN) ---")]
    public AudioSource leverAudioSource;
    public AudioClip pullSound;

    // Trạng thái mạng: true = đang gạt xuống / mở cửa, false = nhả ra / đóng cửa
    public NetworkVariable<bool> isPressed = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private Quaternion initialLeverLocalRot;
    private bool isPlayerNearby = false;
    private bool lastSentState = false;
    private bool isInitialized = false;

    void Awake()
    {
        allLevers.Add(this);
    }

    void OnDestroy()
    {
        allLevers.Remove(this);
    }

    void Start()
    {
        foreach (var door in doors)
        {
            if (door != null && door.doorTransform != null)
            {
                door.initialPos = door.doorTransform.localPosition;
            }
        }

        if (leverHandle != null)
        {
            initialLeverLocalRot = leverHandle.localRotation;
        }

        if (interactUI != null) interactUI.SetActive(false);

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        // 1. Kiểm tra giữ phím F nếu người chơi đang ở trong vùng Trigger Cần Gạt
        if (isPlayerNearby)
        {
            bool holdingF = Input.GetKey(interactKey);
            if (holdingF != lastSentState)
            {
                lastSentState = holdingF;
                SetPressedServerRpc(holdingF);
            }
        }

        // Reset bộ đếm chống lặp cửa mỗi frame
        if (Time.frameCount != lastFrameCount)
        {
            updatedDoorsThisFrame.Clear();
            lastFrameCount = Time.frameCount;
        }

        // 2. Xử lý di chuyển CỬA
        foreach (var door in doors)
        {
            if (door != null && door.doorTransform != null)
            {
                if (updatedDoorsThisFrame.Contains(door.doorTransform)) continue;

                bool shouldOpenDoor = false;

                // Kiểm tra tất cả các Cần Gạt trong game xem có cái nào cùng điều khiển cửa này và đang bị gạt không
                foreach (var lever in allLevers)
                {
                    if (lever != null && lever.isPressed.Value)
                    {
                        foreach (var pDoor in lever.doors)
                        {
                            if (pDoor != null && pDoor.doorTransform == door.doorTransform)
                            {
                                shouldOpenDoor = true;
                                break;
                            }
                        }
                    }
                    if (shouldOpenDoor) break;
                }

                // Kết hợp kiểm tra thêm cả các PressurePlateDoor (Nút đạp đá) nếu có
                if (!shouldOpenDoor && PressurePlateDoor.allPlates != null)
                {
                    foreach (var plate in PressurePlateDoor.allPlates)
                    {
                        if (plate != null && plate.isPressed.Value)
                        {
                            foreach (var pDoor in plate.doors)
                            {
                                if (pDoor != null && pDoor.doorTransform == door.doorTransform)
                                {
                                    shouldOpenDoor = true;
                                    break;
                                }
                            }
                        }
                        if (shouldOpenDoor) break;
                    }
                }

                Vector3 targetDoorPos = shouldOpenDoor ? (door.initialPos + door.slideOffset) : door.initialPos;
                bool isMoving = Vector3.Distance(door.doorTransform.localPosition, targetDoorPos) > 0.001f;

                door.doorTransform.localPosition = Vector3.MoveTowards(
                    door.doorTransform.localPosition, 
                    targetDoorPos, 
                    door.doorSpeed * Time.deltaTime
                );

                // Âm thanh cửa trượt
                if (door.slideAudioSource != null)
                {
                    if (isMoving && !door.slideAudioSource.isPlaying)
                    {
                        door.slideAudioSource.Play();
                    }
                    else if (!isMoving && door.slideAudioSource.isPlaying)
                    {
                        door.slideAudioSource.Stop();
                    }
                }

                updatedDoorsThisFrame.Add(door.doorTransform);
            }
        }

        // 3. Xử lý xoay CẦN GẠT (Lever Handle)
        if (leverHandle != null)
        {
            Quaternion targetLeverRot = isPressed.Value 
                ? (initialLeverLocalRot * Quaternion.Euler(pulledRotationOffset)) 
                : initialLeverLocalRot;

            bool wereRotating = leverHandle.localRotation != targetLeverRot;

            leverHandle.localRotation = Quaternion.RotateTowards(
                leverHandle.localRotation, 
                targetLeverRot, 
                leverRotateSpeed * Time.deltaTime
            );

            // Âm thanh gạt cần
            if (wereRotating && leverAudioSource != null && pullSound != null && !leverAudioSource.isPlaying)
            {
                leverAudioSource.PlayOneShot(pullSound);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPressedServerRpc(bool pressed)
    {
        isPressed.Value = pressed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsLocalPlayer)
            {
                isPlayerNearby = true;
                if (interactUI != null) interactUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsLocalPlayer)
            {
                isPlayerNearby = false;
                if (interactUI != null) interactUI.SetActive(false);

                // Nếu người chơi rời khỏi vùng Trigger khi đang giữ F -> tự động nhả cần gạt
                if (lastSentState)
                {
                    lastSentState = false;
                    SetPressedServerRpc(false);
                }
            }
        }
    }
}
