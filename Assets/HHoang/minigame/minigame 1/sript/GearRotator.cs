using UnityEngine;
using Unity.Netcode;
using DG.Tweening;

public class GearRotator : NetworkBehaviour 
{
    [Header("Cấu hình")]
    public float rotationSpeed = 50f;
    public bool reverseDirection = false;
    private float originalSpeed;

    [Header("Cấu hình trượt mở cổng")]
    public Vector3 openOffset = new Vector3(-5f, 0, 0);
    public float moveDuration = 2f;
    public Vector3 rotationAxis = Vector3.up;
    private Vector3 originalPosition;

    private NetworkVariable<float> currentSpeed = new NetworkVariable<float>(0f, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        originalPosition = transform.localPosition;
        originalSpeed = rotationSpeed;
    }

    // Trong GearRotator.cs
    public void SetRotation(bool shouldRotate)
    {
        if (!IsServer) return;
        
        // Nếu nên xoay thì gán tốc độ, không thì cho bằng 0
        float direction = reverseDirection ? -1f : 1f;
        currentSpeed.Value = shouldRotate ? (originalSpeed * direction) : 0f;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Gán tốc độ mặc định ngay khi server khởi động object này
            float direction = reverseDirection ? -1f : 1f;
            currentSpeed.Value = originalSpeed * direction;
        }
        
        // Client không cần làm gì ở đây, nó sẽ tự nhận giá trị từ NetworkVariable
    }

    void Update()
    {
        // Quay bánh răng: Logic này an toàn cho cả Server và Client
        if (currentSpeed.Value != 0)
        {
            transform.Rotate(rotationAxis.normalized * currentSpeed.Value * Time.deltaTime, Space.Self);
        }
    }

    // Các hàm tương tác chỉ được gọi bởi Server hoặc qua ServerRpc
    public void OpenGear() { if (IsServer) OpenGearServerRpc(); }

    [ServerRpc(RequireOwnership = false)]
    private void OpenGearServerRpc()
    {
        currentSpeed.Value = 0f;
        OpenGearClientRpc(); // Đồng bộ hiệu ứng cho Client
    }

    [ClientRpc]
    private void OpenGearClientRpc()
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CloseGearServerRpc()
    {
        // Cập nhật giá trị quay về 0 để dừng trước
        currentSpeed.Value = 0f; 
        
        // Gọi ClientRpc để chạy Tween
        CloseGearClientRpc();
        
        // Server tự chờ đúng thời gian moveDuration rồi mới cho quay lại
        Invoke(nameof(ResumeRotation), moveDuration);
    }

    private void ResumeRotation()
    {
        if (!IsServer) return;
        float direction = reverseDirection ? -1f : 1f;
        currentSpeed.Value = originalSpeed * direction;
    }

    [ClientRpc]
    private void CloseGearClientRpc()
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.InOutCubic);
    }
    // Thêm hàm này vào GearRotator.cs
    public void CloseGear() 
    { 
        if (IsServer) CloseGearServerRpc(); 
    }

    void OnDestroy()
    {
        // Hủy bỏ lệnh chờ nếu object bị xóa giữa chừng
        CancelInvoke(nameof(ResumeRotation));
    }
}