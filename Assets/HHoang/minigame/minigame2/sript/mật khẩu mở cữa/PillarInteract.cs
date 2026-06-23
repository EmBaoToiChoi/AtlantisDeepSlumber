using System.Collections;
using UnityEngine;
using Unity.Netcode; // BẮT BUỘC PHẢI CÓ

public class PillarInteract : NetworkBehaviour // Đổi thành NetworkBehaviour
{
    [Header("Puzzle Settings")]
    [SerializeField] private int correctDirection = 0; 
    
    // Đồng bộ hướng của trụ từ Server về tất cả Client
    public NetworkVariable<int> currentDirection = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Rotation Settings")]
    [SerializeField] private float rotationDuration = 1f; 

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem dustEffect; 

    [Header("References")]
    [SerializeField] private PuzzleManager puzzleManager; 

    private bool isPlayerNearby = false;
    private bool isRotating = false; 
    
    private float initialXRotation;
    private float initialZRotation;
    private float initialYRotation;

    void Start()
    {
        // Ghi nhớ góc ban đầu local
        initialXRotation = transform.rotation.eulerAngles.x;
        initialZRotation = transform.rotation.eulerAngles.z;
        initialYRotation = transform.rotation.eulerAngles.y;

        if (dustEffect != null) dustEffect.Stop();
    }

    // Hàm này chạy khi Object được khởi tạo trên mạng
    public override void OnNetworkSpawn()
    {
        // Lắng nghe sự kiện thay đổi hướng để tất cả các máy cùng xoay mượt
        currentDirection.OnValueChanged += OnDirectionChanged;
        
        // Cập nhật góc quay hiện tại cho những ông vào phòng muộn (Late Joiner)
        SetRotationFromDirection(currentDirection.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentDirection.OnValueChanged -= OnDirectionChanged;
    }

    void Update()
    {
        // Chỉ Client đang đứng gần mới được quyền bấm F
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F) && !isRotating)
        {
            // Gửi yêu cầu lên Server đòi xoay trụ
            RequestRotatePillarServerRpc();
        }
    }

    // ServerRpc cho phép Client ra lệnh cho Server thực thi logic công bằng
    // RequireOwnership = false giúp bất kỳ Client nào cũng bấm được vào trụ chung của Map
    [ServerRpc(RequireOwnership = false)]
    private void RequestRotatePillarServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isRotating) return; // Bảo vệ server nếu có đứa cố tình spam packet

        // Server thay đổi giá trị mạng, tự động đồng bộ về toàn bộ các máy khác
        currentDirection.Value = (currentDirection.Value + 1) % 4;
    }

    // Hàm này tự động chạy trên TOÀN BỘ các máy (Server + Clients) khi currentDirection thay đổi
    private void OnDirectionChanged(int previousValue, int newValue)
    {
        float targetYRotation = initialYRotation + (newValue * 90f);
        Quaternion targetRot = Quaternion.Euler(initialXRotation, targetYRotation, initialZRotation);

        StartCoroutine(AnimateRotation(targetRot));
    }

    private IEnumerator AnimateRotation(Quaternion targetRot)
    {
        isRotating = true;
        if (dustEffect != null) dustEffect.Play();

        Quaternion startRot = transform.rotation;
        float elapsedTime = 0f;

        while (elapsedTime < rotationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / rotationDuration;
            transform.rotation = Quaternion.Lerp(startRot, targetRot, progress);
            yield return null;
        }

        transform.rotation = targetRot;
        if (dustEffect != null) dustEffect.Stop();
        isRotating = false;

        // CHỈ SERVER mới có quyền kiểm tra xem câu đố đã giải xong chưa
        if (IsServer && puzzleManager != null)
        {
            puzzleManager.CheckPuzzle();
        }
    }

    private void SetRotationFromDirection(int direction)
    {
        float targetYRotation = initialYRotation + (direction * 90f);
        transform.rotation = Quaternion.Euler(initialXRotation, targetYRotation, initialZRotation);
    }

    public bool IsCorrectDirection()
    {
        return currentDirection.Value == correctDirection;
    }

    // --- Vùng nhận diện người chơi mạng ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Kiểm tra xem GameObject va chạm có phải là CHÍNH MÌNH (Local Player) không
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsLocalPlayer)
            {
                isPlayerNearby = true;
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
            }
        }
    }
}