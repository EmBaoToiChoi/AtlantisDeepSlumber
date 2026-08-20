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

    private void Awake()
    {
        InitializeDoorPositions();
    }

    private void Start()
    {
        InitializeDoorPositions();

        Debug.Log($"[PushableDoor] Khởi chạy trên '{gameObject.name}'. Số lượng cánh cửa cần di chuyển: {doorsToMove.Length}");

        // Kiểm tra xung đột nhiều script cùng điều khiển một cánh cửa
        PushableDoor[] allDoors = FindObjectsOfType<PushableDoor>();
        foreach (var door in allDoors)
        {
            if (door != this && door.enabled && door.doorsToMove != null)
            {
                foreach (var t in door.doorsToMove)
                {
                    if (t == null) continue;
                    foreach (var myT in doorsToMove)
                    {
                        if (myT == t)
                        {
                            Debug.LogWarning($"[PushableDoor] Lưu ý: Cả script '{gameObject.name}' và script '{door.gameObject.name}' đều có tham chiếu vật thể '{t.name}'.");
                        }
                    }
                }
            }
        }
    }

    public void InitializeDoorPositions()
    {
        if (doorsToMove == null || doorsToMove.Length == 0)
        {
            doorsToMove = new Transform[] { transform };
        }

        if (openOffset == Vector3.zero)
        {
            openOffset = new Vector3(0f, 5f, 0f);
        }

        if (closedPositions == null || closedPositions.Length != doorsToMove.Length)
        {
            closedPositions = new Vector3[doorsToMove.Length];
            openPositions = new Vector3[doorsToMove.Length];

            for (int i = 0; i < doorsToMove.Length; i++)
            {
                if (doorsToMove[i] != null)
                {
                    closedPositions[i] = doorsToMove[i].position;
                    openPositions[i] = closedPositions[i] + openOffset;
                }
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
        if (closedPositions == null || openPositions == null || closedPositions.Length == 0)
        {
            InitializeDoorPositions();
        }

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
        if (closedPositions == null || openPositions == null || closedPositions.Length == 0)
        {
            InitializeDoorPositions();
        }

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
            localIsOpen = true;
            Debug.Log($"[PushableDoor] Offline: Yêu cầu mở cửa!");
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
        if (closedPositions == null || openPositions == null || closedPositions.Length == 0)
        {
            InitializeDoorPositions();
        }

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
            localIsOpen = false;
            Debug.Log($"[PushableDoor] Offline: Yêu cầu đóng cửa!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CloseServerRpc()
    {
        isOpen.Value = false;
        Debug.Log($"[PushableDoor] Server Rpc: Yêu cầu đóng cửa!");
    }
}
