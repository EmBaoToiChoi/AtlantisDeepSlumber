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

    public NetworkVariable<GearState> currentState = new NetworkVariable<GearState>(GearState.Spinning);

    public ParticleSystem gearSmokeEffect;
    void Awake()
    {
        originalPosition = transform.localPosition;
        originalSpeed = rotationSpeed;
    }

    public override void OnNetworkSpawn()
    {
        currentState.OnValueChanged += (oldState, newState) => {
            if (newState == GearState.Opening) TriggerOpenVisuals();
            else if (newState == GearState.Closing) TriggerCloseVisuals();
        };
    }

    void Update()
    {
        // CẢNH BÁO: Chỉ thực hiện ép vị trí nếu Bánh răng KHÔNG trong trạng thái đang Opening hoặc Closing
        // Dùng currentState để ngăn chặn Server ép vị trí khi đang trượt
        if (currentState.Value == GearState.Spinning)
        {
            float direction = reverseDirection ? -1f : 1f;
            transform.Rotate(rotationAxis.normalized * originalSpeed * direction * Time.deltaTime, Space.Self);
            
            // CHỈ ÉP VỊ TRÍ KHI LÀ SERVER VÀ KHÔNG CÓ TWEEN NÀO ĐANG CHẠY
            if (IsServer && !DOTween.IsTweening(transform))
            {
                if (Vector3.Distance(transform.localPosition, originalPosition) > 0.1f)
                {
                    transform.localPosition = originalPosition;
                }
            }
        }

        // Phần logic khói giữ nguyên (đã ổn)
        if (gearSmokeEffect != null)
        {
            bool isMoving = DOTween.IsTweening(transform);
            if (isMoving && !gearSmokeEffect.isPlaying) gearSmokeEffect.Play();
            else if (!isMoving && gearSmokeEffect.isPlaying) gearSmokeEffect.Stop();
        }
    }

    private void TriggerOpenVisuals() 
    {
        transform.DOKill();
        DOVirtual.DelayedCall(1.0f, () => {
            transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
        });
    }

    private void TriggerCloseVisuals() 
    {
        transform.DOKill();
        // Thay vì delayed call 1s làm người chơi thấy "khựng", 
        // mình cho nó trượt về ngay lập tức nhưng với tốc độ mượt.
        // Ease.OutCubic làm nó trượt nhanh lúc đầu, chậm dần lúc về đích.
        transform.DOLocalMove(originalPosition, 1.5f).SetEase(Ease.OutCubic);
    }

    public void OpenGear() { if (IsServer) currentState.Value = GearState.Opening; }
    public void CloseGear() { if (IsServer) currentState.Value = GearState.Closing; }

    public void ResetToSpinning()
    {
        if (IsServer) 
        {
            // 1. Tắt quay ngay lập tức trên Server để tránh nhảy vị trí
            currentState.Value = GearState.Closing; 
            
            // 2. Gọi ClientRpc để tất cả máy reset mượt mà
            ResetClientVisualsClientRpc();
            
            // 3. Đợi đủ thời gian trượt về rồi mới cho quay lại
            DOVirtual.DelayedCall(1.6f, () => {
                if (IsServer) currentState.Value = GearState.Spinning;
            });
        }
    }

    [ClientRpc]
    private void ResetClientVisualsClientRpc()
    {
        transform.DOKill();
        // Thêm .OnComplete để đảm bảo sau khi trượt xong, nó nằm đúng vị trí gốc
        transform.DOLocalMove(originalPosition, 1.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => {
                    transform.localPosition = originalPosition;
                });
    }
}