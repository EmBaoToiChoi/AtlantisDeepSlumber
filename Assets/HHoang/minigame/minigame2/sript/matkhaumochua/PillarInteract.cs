using System.Collections;
using UnityEngine;
using Unity.Netcode; 

[RequireComponent(typeof(AudioSource))] // Tự động gắn loa vào trụ nếu quên
public class PillarInteract : NetworkBehaviour 
{
    [Header("Puzzle Settings")]
    [SerializeField] private int correctDirection = 0; 
    
    public NetworkVariable<int> currentDirection = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Rotation Settings")]
    [Tooltip("Thời gian (giây) để trụ xoay xong 1 mặt 90 độ. Nhập 1 = 1s, 10 = 10s.")]
    public float rotationDuration = 3f; 

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem dustEffect; 

    // [THÊM ÂM THANH] Nơi gắn loa và file âm thanh tiếng đá xoay
    [Header("Audio Settings")]
    public AudioSource pillarAudio;
    public AudioClip rotateSound;

    [Header("References")]
    [SerializeField] private PuzzleManager puzzleManager; 

    private bool isPlayerNearby = false;
    
    // KHÓA THAO TÁC TOÀN CỤC BẰNG THỜI GIAN CHUNG (STATIC)
    private float clientNextAllowedTime = 0f; 
    private float serverNextAllowedTime = 0f; 
    
    private float initialXRotation;
    private float initialZRotation;
    private float initialYRotation;

    void Awake()
    {
        // Ghi nhớ góc ban đầu local từ Inspector ngay khi Game khởi tạo
        initialXRotation = transform.rotation.eulerAngles.x;
        initialZRotation = transform.rotation.eulerAngles.z;
        initialYRotation = transform.rotation.eulerAngles.y;

        // [THÊM ÂM THANH] Tự động tìm cái Loa trên trụ
        if (pillarAudio == null)
        {
            pillarAudio = GetComponent<AudioSource>();
        }

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
        // BƯỚC 1: KHÓA TRÊN MÁY NGƯỜI CHƠI (CLIENT)
        // Nếu thời gian hiện tại chưa vượt qua mốc cho phép thì phớt lờ nút F
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F) && Time.time >= clientNextAllowedTime)
        {
            // Khóa nút F toàn bộ các trụ khác trên máy này trong [rotationDuration] giây
            clientNextAllowedTime = Time.time + rotationDuration;
            
            RequestRotatePillarServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRotatePillarServerRpc(ServerRpcParams rpcParams = default)
    {
        // BƯỚC 2: KHÓA TRÊN MÁY CHỦ (SERVER)
        // Phòng hờ 4 người ở 4 máy bấm cùng 1 lúc, máy chủ chỉ nhận 1 lệnh duy nhất
        if (Time.time < serverNextAllowedTime) return; 
        
        // Máy chủ bắt đầu khóa nhận lệnh từ các trụ khác
        serverNextAllowedTime = Time.time + rotationDuration;
        
        // Server thay đổi giá trị mạng (0 -> 1 -> 2 -> 3 -> 0)
        currentDirection.Value = (currentDirection.Value + 1) % 4;
    }

    private void OnDirectionChanged(int previousValue, int newValue)
    {
        float targetYAngle = initialYRotation + (newValue * 90f);

        // MẸO TOÁN HỌC: Fix lỗi trụ lật ngược khi xoay từ số 3 (270 độ) quay vòng về số 0.
        // Ta ép nó quay tiến lên 360 độ thay vì giật ngược lại về 0 độ.
        if (previousValue == 3 && newValue == 0)
        {
            targetYAngle = initialYRotation + 360f;
        }

        Quaternion targetRot = Quaternion.Euler(initialXRotation, targetYAngle, initialZRotation);
        
        // Kích hoạt hiệu ứng xoay từ từ
        StartCoroutine(AnimateRotation(targetRot, newValue));
    }

    private IEnumerator AnimateRotation(Quaternion targetRot, int finalDirectionValue)
    {
        if (dustEffect != null) dustEffect.Play();

        // [THÊM ÂM THANH] Bắt đầu phát tiếng đá lết
        if (pillarAudio != null && rotateSound != null)
        {
            pillarAudio.clip = rotateSound;
            pillarAudio.Play();
        }

        Quaternion startRot = transform.rotation;
        float elapsedTime = 0f;
        
        float actualDuration = Mathf.Max(0.1f, rotationDuration);

        while (elapsedTime < actualDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Tính tỷ lệ % thời gian trôi qua (0 đến 1)
            float progress = elapsedTime / actualDuration;
            
            // Ép mượt bằng SmoothStep (Khởi động chậm -> Xoay nhanh -> Dừng chậm lại)
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            
            transform.rotation = Quaternion.Lerp(startRot, targetRot, smoothProgress);
            yield return null; // Đợi tới khung hình tiếp theo
        }

        // Chốt sổ: Ép chuẩn xác góc quay mạng khi kết thúc Coroutine để chống sai số
        SetRotationFromDirection(finalDirectionValue);

        if (dustEffect != null) dustEffect.Stop();

        // [THÊM ÂM THANH] Ngắt tiếng đá lết ngay khi trụ xoay xong
        if (pillarAudio != null && pillarAudio.isPlaying)
        {
            pillarAudio.Stop();
        }

        // ==========================================
        // CHỈ SERVER: Quét đáp án ngay khi ĐÃ XOAY XONG
        // ==========================================
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