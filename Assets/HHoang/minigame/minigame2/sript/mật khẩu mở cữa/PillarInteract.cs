using System.Collections;
using UnityEngine;
using Unity.Netcode; 

public class PillarInteract : NetworkBehaviour 
{
    [Header("Puzzle Settings")]
    [SerializeField] private int correctDirection = 0; 
    
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

    void Awake()
    {
        // Ghi nhớ góc ban đầu local từ Inspector ngay khi Game khởi tạo
        initialXRotation = transform.rotation.eulerAngles.x;
        initialZRotation = transform.rotation.eulerAngles.z;
        initialYRotation = transform.rotation.eulerAngles.y;

        if (puzzleManager == null)
        {
            puzzleManager = Object.FindFirstObjectByType<PuzzleManager>();
            if (puzzleManager != null)
            {
                Debug.Log($"<color=cyan>[{gameObject.name}] Đã tự kết nối thành công với PuzzleManager trên Scene!</color>");
            }
        }
    }

    void Start()
    {
        if (dustEffect != null) dustEffect.Stop();
    }

    public override void OnNetworkSpawn()
    {
        currentDirection.OnValueChanged += OnDirectionChanged;
        SetRotationFromDirection(currentDirection.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentDirection.OnValueChanged -= OnDirectionChanged;
    }

    void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F) && !isRotating)
        {
            RequestRotatePillarServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRotatePillarServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isRotating) return; 
        
        // Server thay đổi giá trị mạng
        currentDirection.Value = (currentDirection.Value + 1) % 4;
    }

    private void OnDirectionChanged(int previousValue, int newValue)
    {
        // Chạy hiệu ứng xoay mượt ở local máy của mỗi người chơi
        float targetYRotation = initialYRotation + (newValue * 90f);
        Quaternion targetRot = Quaternion.Euler(initialXRotation, targetYRotation, initialZRotation);

        StartCoroutine(AnimateRotation(targetRot));

        // CHỈ SERVER: Thực hiện quét đáp án ngay khi biến mạng vừa cập nhật xong xuôi
        if (IsServer && puzzleManager != null)
        {
            puzzleManager.CheckPuzzle();
        }
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

    public int GetCurrentDirectionValue() { return currentDirection.Value; }
    public int GetCorrectDirectionValue() { return correctDirection; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
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