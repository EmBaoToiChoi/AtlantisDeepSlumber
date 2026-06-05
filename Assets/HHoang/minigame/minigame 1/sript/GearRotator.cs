using UnityEngine;
using Unity.Netcode;
using DG.Tweening;

public enum GearState { Spinning, Opening, Closing }

public class GearRotator : NetworkBehaviour 
{
    [Header("Cấu hình Quay")]
    public float rotationSpeed = 50f;
    public bool reverseDirection = false;
    private float originalSpeed;

    [Header("Cấu hình trượt mở cổng")]
    public Vector3 openOffset = new Vector3(-5f, 0, 0);
    public float moveDuration = 2f;
    public Vector3 rotationAxis = Vector3.up;
    private Vector3 originalPosition;

    // Biến đồng bộ trạng thái giữa Server và Client
    public NetworkVariable<GearState> currentState = new NetworkVariable<GearState>(GearState.Spinning);

    void Awake()
    {
        originalPosition = transform.localPosition;
        originalSpeed = rotationSpeed;
    }

    public override void OnNetworkSpawn()
    {
        // Lắng nghe thay đổi từ Server để Client tự cập nhật hiệu ứng
        currentState.OnValueChanged += (oldState, newState) => {
            if (newState == GearState.Opening) TriggerOpenVisuals();
            else if (newState == GearState.Closing) TriggerCloseVisuals();
        };
    }

    void Update()
    {
        if (currentState.Value == GearState.Spinning)
        {
            // 1. Quay
            float direction = reverseDirection ? -1f : 1f;
            transform.Rotate(rotationAxis.normalized * originalSpeed * direction * Time.deltaTime, Space.Self);
            
            // 2. Ép về vị trí gốc nếu bị lệch 
            // Thay vì dùng IsTweening, ta chỉ cần kiểm tra khoảng cách nhỏ (SqrMagnitude) 
            // để tránh việc gán vị trí liên tục gây lỗi logic
            if (Vector3.Distance(transform.localPosition, originalPosition) > 0.001f)
            {
                // Chỉ gán nếu không có Tween nào đang chạy (nếu bạn vẫn muốn dùng Tweening)
                if (!DOTween.IsTweening(transform)) 
                {
                    transform.localPosition = originalPosition;
                }
            }
        }
    }

    // --- CÁC HÀM XỬ LÝ HIỆU ỨNG (Client tự thực thi khi currentState thay đổi) ---
// --- CÁC HÀM XỬ LÝ HIỆU ỨNG (Client tự thực thi khi currentState thay đổi) ---
    private void TriggerOpenVisuals() 
    {
        transform.DOKill();
        // Thêm Delay 1s trước khi mở
        DOVirtual.DelayedCall(1.0f, () => {
            transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
        });
    }

    private void TriggerCloseVisuals() 
    {
        transform.DOKill();
        // Thêm Delay 1s trước khi đóng
        DOVirtual.DelayedCall(1.0f, () => {
            transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.OutBack, 0.5f);
        });
    }

    // --- CÁC HÀM ĐIỀU KHIỂN (Chỉ Server được phép gọi) ---
    public void OpenGear() 
    { 
        if (IsServer) currentState.Value = GearState.Opening; 
    }

    public void CloseGear() 
    { 
        if (IsServer) currentState.Value = GearState.Closing; 
    }

    [ClientRpc]
    private void ForceCloseVisualsClientRpc()
    {
        // 1. Dừng ngay mọi lệnh di chuyển cũ để tránh xung đột
        transform.DOKill();

        // 2. Trượt về vị trí gốc với hiệu ứng OutBack
        // Tham số 0.5f là độ "nẩy" khi về đến nơi.
        // Cổng đóng sẽ chạy nhanh, chạm vào điểm gốc rồi "cạch" nhẹ một cái mới dừng hẳn.
        transform.DOLocalMove(originalPosition, moveDuration)
                 .SetEase(Ease.OutBack, 0.5f); 
    }

    // Hàm dự phòng: nếu muốn Server cho phép quay lại sau khi đóng
    public void ResetToSpinning()
    {
        if (IsServer) 
        {
            // 1. Chuyển trạng thái về Spinning
            currentState.Value = GearState.Spinning;
            
            // 2. Ép tất cả Client chạy animation về vị trí cũ ngay lập tức
            ForceCloseVisualsClientRpc(); 
        }
    }
}