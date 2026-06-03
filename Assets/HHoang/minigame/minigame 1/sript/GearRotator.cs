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
        // Logic quay chỉ chạy nếu trạng thái là Spinning
        if (currentState.Value == GearState.Spinning)
        {
            float direction = reverseDirection ? -1f : 1f;
            transform.Rotate(rotationAxis.normalized * originalSpeed * direction * Time.deltaTime, Space.Self);
        }
    }

    // --- CÁC HÀM XỬ LÝ HIỆU ỨNG (Client tự thực thi khi currentState thay đổi) ---
    private void TriggerOpenVisuals() 
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
    }

    private void TriggerCloseVisuals() 
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.InOutCubic);
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

    // Hàm dự phòng: nếu muốn Server cho phép quay lại sau khi đóng
    public void ResetToSpinning()
    {
        if (IsServer) currentState.Value = GearState.Spinning;
    }
}