using UnityEngine;
using Unity.Netcode;

public class PushableDoor : NetworkBehaviour
{
    [Header("Door Target Transforms")]
    [Tooltip("Danh sách các Transform sẽ di chuyển (nếu trống sẽ tự di chuyển chính GameObject này)")]
    public Transform[] doorsToMove;

    [Header("Door Settings")]
    [Tooltip("Vị trí dịch chuyển khi mở cửa (Ví dụ: Y = 5 để kéo lên)")]
    public Vector3 openOffset = new Vector3(0f, 5f, 0f);
    public float speed = 2.0f;

    // Network variable to sync the open/closed state of the door across the network
    public NetworkVariable<bool> isOpen = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Vector3[] closedPositions;
    private Vector3[] openPositions;
    private bool localIsOpen = false; // Used for offline/standalone mode
    private bool wasMoving = false;

    private void Start()
    {
        // Nếu không gán transform mục tiêu nào, mặc định di chuyển chính GameObject chứa script
        if (doorsToMove == null || doorsToMove.Length == 0)
        {
            doorsToMove = new Transform[] { transform };
        }

        closedPositions = new Vector3[doorsToMove.Length];
        openPositions = new Vector3[doorsToMove.Length];

        Debug.Log($"[PushableDoor] Khởi chạy trên '{gameObject.name}'. Số lượng cánh cửa cần di chuyển: {doorsToMove.Length}");

        // Kiểm tra xung đột nhiều script cùng điều khiển một cánh cửa
        PushableDoor[] allDoors = FindObjectsOfType<PushableDoor>();
        foreach (var door in allDoors)
        {
            if (door != this && door.enabled)
            {
                foreach (var t in door.doorsToMove)
                {
                    if (t == null) continue;
                    foreach (var myT in doorsToMove)
                    {
                        if (myT == t)
                        {
                            Debug.LogError($"[PushableDoor] LỖI XUNG ĐỘT SCRIPTS: Cả script '{gameObject.name}' và script '{door.gameObject.name}' đều đang điều khiển chung vật thể '{t.name}'. Chúng sẽ tranh chấp vị trí và làm cửa không thể di chuyển! Vui lòng xóa component PushableDoor trên một trong hai đối tượng.");
                        }
                    }
                }
            }
        }

        for (int i = 0; i < doorsToMove.Length; i++)
        {
            if (doorsToMove[i] != null)
            {
                closedPositions[i] = doorsToMove[i].position;
                openPositions[i] = closedPositions[i] + openOffset;

                // Kiểm tra xem có phải gán nhầm file Prefab Asset từ Project thay vì Scene Instance từ Hierarchy không
                if (!doorsToMove[i].gameObject.scene.IsValid())
                {
                    Debug.LogError($"[PushableDoor] LỖI CỰC KỲ NGUY HIỂM: Cánh cửa '{doorsToMove[i].name}' (phần tử {i}) là một PREFAB ASSET từ Project, không phải Scene Instance trong Hierarchy! Nó sẽ không thể di chuyển trong game. Hãy xóa và kéo lại đối tượng từ Hierarchy vào bảng Inspector.");
                }
                else
                {
                    Debug.Log($"[PushableDoor] Cánh cửa {i}: '{doorsToMove[i].name}' gán thành công. Vị trí đóng: {closedPositions[i]}, Vị trí mở: {openPositions[i]}");
                }
            }
            else
            {
                Debug.LogWarning($"[PushableDoor] Cánh cửa ở phần tử {i} đang bị NULL!");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isOpen.OnValueChanged += OnDoorStateChanged;
        if (isOpen.Value)
        {
            localIsOpen = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        isOpen.OnValueChanged -= OnDoorStateChanged;
    }

    private void OnDoorStateChanged(bool oldVal, bool newVal)
    {
        Debug.Log($"[PushableDoor] Đồng bộ trạng thái cửa mạng: {(newVal ? "MỞ" : "ĐÓNG")}");
    }

    private void Update()
    {
        bool openState = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isOpen.Value : localIsOpen;

        bool isAnyMoving = false;
        for (int i = 0; i < doorsToMove.Length; i++)
        {
            if (doorsToMove[i] != null)
            {
                Vector3 targetPos = openState ? openPositions[i] : closedPositions[i];
                float distance = Vector3.Distance(doorsToMove[i].position, targetPos);
                if (distance > 0.001f)
                {
                    doorsToMove[i].position = Vector3.MoveTowards(doorsToMove[i].position, targetPos, Time.deltaTime * speed);
                    isAnyMoving = true;
                }
            }
        }

        if (isAnyMoving && !wasMoving)
        {
            wasMoving = true;
            Debug.Log($"[PushableDoor] Bắt đầu di chuyển các cánh cửa (Trạng thái: {(openState ? "MỞ" : "ĐÓNG")})...");
        }
        else if (!isAnyMoving && wasMoving)
        {
            wasMoving = false;
            Debug.Log($"[PushableDoor] Các cánh cửa đã hoàn thành di chuyển đến vị trí {(openState ? "MỞ" : "ĐÓNG")}.");
        }
    }

    public void Open()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                isOpen.Value = true;
            }
            else
            {
                OpenServerRpc();
            }
        }
        else
        {
            if (!localIsOpen)
            {
                localIsOpen = true;
                Debug.Log($"[PushableDoor] Offline: Yêu cầu mở cửa!");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void OpenServerRpc()
    {
        isOpen.Value = true;
        Debug.Log($"[PushableDoor] Server Rpc: Yêu cầu mở cửa!");
    }

    public void Close()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                isOpen.Value = false;
            }
            else
            {
                CloseServerRpc();
            }
        }
        else
        {
            if (localIsOpen)
            {
                localIsOpen = false;
                Debug.Log($"[PushableDoor] Offline: Yêu cầu đóng cửa!");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CloseServerRpc()
    {
        isOpen.Value = false;
        Debug.Log($"[PushableDoor] Server Rpc: Yêu cầu đóng cửa!");
    }
}
