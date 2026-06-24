using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PressurePlateTrigger : NetworkBehaviour
{
    [Header("Target Doors")]
    [Tooltip("Cánh cửa thứ nhất sẽ mở khi đè lên")]
    public PushableDoor targetDoor;
    [Tooltip("Cánh cửa thứ hai sẽ mở khi đè lên (Tùy chọn)")]
    public PushableDoor targetDoor2;

    [Header("Visual Feedback (Optional)")]
    [Tooltip("Vật thể nút ấn đi xuống khi bị đè")]
    public Transform buttonVisual;
    public float pressDepth = 0.15f;
    public float pressSpeed = 5f;

    [Header("Stone Sinking (Optional)")]
    [Tooltip("Khoảng cách cục đá đẩy lún xuống khi khớp vào nút sàn")]
    public float stoneSinkDepth = 0.2f;
    [Tooltip("Tốc độ lún của cục đá đẩy")]
    public float stoneSinkSpeed = 5f;

    private Vector3 unpressedPos;
    private Vector3 pressedPos;

    // Biến mạng đồng bộ trạng thái đè nút
    public NetworkVariable<bool> isPressedNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool localIsPressed = false; // Dùng khi chơi offline

    // Chỉ theo dõi collider trên Server hoặc máy chạy offline để tránh trùng lặp
    private HashSet<Collider> overlappingColliders = new HashSet<Collider>();

    public bool IsPressed => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isPressedNet.Value : localIsPressed;

    private void Start()
    {
        string door1Name = targetDoor != null ? targetDoor.gameObject.name : "CHƯA GÁN";
        string door2Name = targetDoor2 != null ? targetDoor2.gameObject.name : "KHÔNG DÙNG";
        Debug.Log($"[PressurePlateTrigger] Khởi động trên GameObject '{gameObject.name}'. Target Door 1: {door1Name}, Target Door 2: {door2Name}");

        // Kiểm tra và cấu hình Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            Debug.Log($"[PressurePlateTrigger] Tự động thêm Kinematic Rigidbody vào '{gameObject.name}' để kích hoạt nhận diện va chạm.");
        }
        else
        {
            Debug.Log($"[PressurePlateTrigger] Đã có Rigidbody trên '{gameObject.name}'. isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}");
            // Để đảm bảo nhận diện va chạm đáng tin cậy khi gắn trên vật thể tĩnh, nên đặt isKinematic = true
            if (!rb.isKinematic)
            {
                rb.isKinematic = true;
                Debug.LogWarning($"[PressurePlateTrigger] Đã tự động chuyển Rigidbody trên '{gameObject.name}' thành Kinematic để tránh trôi tự do.");
            }
        }

        // Kiểm tra các Collider
        Collider[] colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            Debug.LogError($"[PressurePlateTrigger] LỖI CỰC KỲ QUAN TRỌNG: Không tìm thấy bất kỳ Collider nào trên '{gameObject.name}' hoặc các con của nó! Nút sàn sẽ KHÔNG THỂ hoạt động. Vui lòng thêm Box Collider hoặc Mesh Collider vào vật thể này trong Unity Inspector.");
        }
        else
        {
            foreach (var col in colliders)
            {
                Debug.Log($"[PressurePlateTrigger] Tìm thấy Collider '{col.name}' trên GameObject '{col.gameObject.name}'. isTrigger: {col.isTrigger}, enabled: {col.enabled}");
            }
        }

        // Kiểm tra Layer Collision Matrix trong Project Settings
        int myLayer = gameObject.layer;
        string[] checkLayers = { "Player", "Default" };
        foreach (var lName in checkLayers)
        {
            int layerId = LayerMask.NameToLayer(lName);
            if (layerId != -1)
            {
                bool ignores = Physics.GetIgnoreLayerCollision(myLayer, layerId);
                if (ignores)
                {
                    Debug.LogError($"[PressurePlateTrigger] LỖI LAYER VA CHẠM: Lớp '{LayerMask.LayerToName(myLayer)}' và lớp '{lName}' đang bị BỎ QUA va chạm với nhau trong Settings! Hãy vào Edit -> Project Settings -> Physics -> Collision Matrix để tích chọn bật lại.");
                }
            }
        }

        if (buttonVisual == null)
        {
            // Mặc định sử dụng chính transform của đối tượng để di chuyển cả cụm (tránh lỗi chỉ dịch chuyển 1 LOD mesh con)
            buttonVisual = transform;
            Debug.Log($"[PressurePlateTrigger] Button Visual trống. Tự động sử dụng chính transform '{gameObject.name}' làm buttonVisual.");
        }

        if (buttonVisual != null)
        {
            unpressedPos = buttonVisual.localPosition;
            pressedPos = unpressedPos - new Vector3(0f, pressDepth, 0f);
        }
    }

    private void Update()
    {
        // Chỉ Server hoặc máy chơi offline mới kiểm tra va chạm và cập nhật trạng thái
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
        {
            // Dọn dẹp các collider đã bị hủy (destroyed) hoặc bị tắt (inactive) khỏi danh sách
            overlappingColliders.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy || !c.enabled);

            bool shouldBePressed = overlappingColliders.Count > 0;
            bool currentPressed = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isPressedNet.Value : localIsPressed;

            if (shouldBePressed != currentPressed)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    isPressedNet.Value = shouldBePressed;
                }
                else
                {
                    localIsPressed = shouldBePressed;
                }

                if (shouldBePressed)
                {
                    Debug.Log($"[PressurePlateTrigger] Nút sàn bị đè (Số lượng: {overlappingColliders.Count}).");
                    
                    // Chỉ kích hoạt mở cửa trực tiếp nếu nút này không tham gia câu đố nào
                    if (!IsPartOfActivePuzzle())
                    {
                        Debug.Log("[PressurePlateTrigger] Kích hoạt mở các cánh cửa trực tiếp!");
                        if (targetDoor != null) targetDoor.Open();
                        if (targetDoor2 != null) targetDoor2.Open();
                    }
                    else
                    {
                        Debug.Log("[PressurePlateTrigger] Nút này thuộc về một Câu đố (Puzzle). Bỏ qua mở cửa trực tiếp.");
                    }

                    // Giải phóng tất cả người chơi đẩy đá khi đá đè lên nút sàn
                    foreach (var col in overlappingColliders)
                    {
                        if (col != null)
                        {
                            PushableStone stone = col.GetComponent<PushableStone>();
                            if (stone == null) stone = col.GetComponentInParent<PushableStone>();
                            if (stone == null) stone = col.GetComponentInChildren<PushableStone>();
                            if (stone == null) stone = col.transform.root.GetComponentInChildren<PushableStone>();

                            if (stone != null)
                            {
                                stone.ReleaseAllPushers();
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log("[PressurePlateTrigger] Không còn vật thể đè.");
                    
                    // Chỉ kích hoạt đóng cửa trực tiếp nếu nút này không tham gia câu đố nào
                    if (!IsPartOfActivePuzzle())
                    {
                        Debug.Log("[PressurePlateTrigger] Kích hoạt đóng các cánh cửa trực tiếp!");
                        if (targetDoor != null) targetDoor.Close();
                        if (targetDoor2 != null) targetDoor2.Close();
                    }
                }
            }
        }

        // Cả Server và Client đều tự động Lerp chuyển động hình ảnh nút bấm dựa trên biến đồng bộ
        bool pressedState = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isPressedNet.Value : localIsPressed;
        if (buttonVisual != null)
        {
            // Nếu buttonVisual là chính root transform và đang chạy online trên Client,
            // để NetworkTransform tự động đồng bộ vị trí từ Server nhằm tránh xung đột giật lag.
            bool isRootWithNetworkTransform = (buttonVisual == transform) && 
                                              (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && GetComponent<Unity.Netcode.Components.NetworkTransform>() != null);
            
            if (!isRootWithNetworkTransform || IsServer)
            {
                Vector3 targetLocalPos = pressedState ? pressedPos : unpressedPos;
                buttonVisual.localPosition = Vector3.Lerp(buttonVisual.localPosition, targetLocalPos, Time.deltaTime * pressSpeed);
            }
        }

        // Tự động lún cục đá đẩy xuống khi đè lên nút sàn
        if (pressedState)
        {
            // Chỉ thực hiện di chuyển vật lý đá trên Server hoặc offline để tránh xung đột mạng
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
            {
                foreach (var col in overlappingColliders)
                {
                    if (col != null)
                    {
                        PushableStone stone = col.GetComponent<PushableStone>();
                        if (stone == null) stone = col.GetComponentInParent<PushableStone>();
                        if (stone == null) stone = col.GetComponentInChildren<PushableStone>();
                        if (stone == null) stone = col.transform.root.GetComponentInChildren<PushableStone>();

                        if (stone != null)
                        {
                            // Căn giữa cục đá theo X, Z của nút sàn và lún xuống theo Y
                            float targetY = transform.position.y - stoneSinkDepth;
                            Vector3 targetStonePos = new Vector3(transform.position.x, targetY, transform.position.z);
                            
                            stone.transform.position = Vector3.Lerp(stone.transform.position, targetStonePos, Time.deltaTime * stoneSinkSpeed);
                            
                            // Cập nhật vị trí mạng để đồng bộ sang Client
                            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                            {
                                stone.netPosition.Value = stone.transform.position;
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        // Chỉ xử lý trên Server hoặc Offline
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        Debug.Log($"[PressurePlateTrigger] OnTriggerEnter chạm bởi: {other.gameObject.name} (Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
        if (IsValidObject(other.gameObject))
        {
            overlappingColliders.Add(other);
            Debug.Log($"[PressurePlateTrigger] Thêm vào OnTriggerEnter: {other.gameObject.name}, số lượng hiện tại: {overlappingColliders.Count}");
        }
        else
        {
            Debug.Log($"[PressurePlateTrigger] Vật thể {other.gameObject.name} không hợp lệ (Không phải Player hoặc Stone).");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        Debug.Log($"[PressurePlateTrigger] OnTriggerExit thoát bởi: {other.gameObject.name}");
        if (overlappingColliders.Contains(other))
        {
            overlappingColliders.Remove(other);
            Debug.Log($"[PressurePlateTrigger] Xóa khỏi OnTriggerExit: {other.gameObject.name}, số lượng hiện tại: {overlappingColliders.Count}");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        Debug.Log($"[PressurePlateTrigger] OnCollisionEnter chạm bởi: {collision.gameObject.name} (Layer: {LayerMask.LayerToName(collision.gameObject.layer)})");
        if (IsValidObject(collision.gameObject))
        {
            overlappingColliders.Add(collision.collider);
            Debug.Log($"[PressurePlateTrigger] Thêm vào OnCollisionEnter: {collision.gameObject.name}, số lượng hiện tại: {overlappingColliders.Count}");
        }
        else
        {
            Debug.Log($"[PressurePlateTrigger] Vật thể {collision.gameObject.name} không hợp lệ (Không phải Player hoặc Stone).");
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision == null || collision.collider == null) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        Debug.Log($"[PressurePlateTrigger] OnCollisionExit thoát bởi: {collision.gameObject.name}");
        if (overlappingColliders.Contains(collision.collider))
        {
            overlappingColliders.Remove(collision.collider);
            Debug.Log($"[PressurePlateTrigger] Xóa khỏi OnCollisionExit: {collision.gameObject.name}, số lượng hiện tại: {overlappingColliders.Count}");
        }
    }

    private bool IsValidObject(GameObject go)
    {
        if (go == null) return false;
        return IsPlayer(go) || IsStone(go);
    }

    private bool IsStone(GameObject go)
    {
        if (go == null) return false;
        
        // Kiểm tra component ở bất kỳ đâu trong phân cấp root của vật thể va chạm
        if (go.GetComponent<PushableStone>() != null || 
            go.GetComponentInParent<PushableStone>() != null ||
            go.GetComponentInChildren<PushableStone>() != null ||
            go.transform.root.GetComponentInChildren<PushableStone>() != null)
        {
            return true;
        }

        string nameLower = go.name.ToLower();
        if (nameLower.Contains("stone") || nameLower.Contains("da") || nameLower.Contains("rock") || nameLower.Contains("brick"))
        {
            return true;
        }
        
        return false;
    }

    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;
        if (go.GetComponentInParent<IPlayerHUDTarget>() != null || go.GetComponent<IPlayerHUDTarget>() != null) return true;
        
        string nameLower = go.name.ToLower();
        if (nameLower.Contains("player") || go.CompareTag("Player") || nameLower.Contains("leo") || nameLower.Contains("elena") || nameLower.Contains("maya") || nameLower.Contains("arthur"))
        {
            return true;
        }
        return false;
    }

    private bool IsPartOfActivePuzzle()
    {
        PressurePlatePuzzleManager[] managers = FindObjectsOfType<PressurePlatePuzzleManager>();
        foreach (var manager in managers)
        {
            if (manager != null && manager.enabled)
            {
                if (manager.requiredPlates != null)
                {
                    foreach (var plate in manager.requiredPlates)
                    {
                        if (plate == this)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }
}
