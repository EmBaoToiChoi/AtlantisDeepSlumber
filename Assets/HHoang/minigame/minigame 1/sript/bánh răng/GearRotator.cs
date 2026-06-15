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
    
    // Thêm biến này để nắm đầu cái Timer, cần là huỷ ngay
    private Tween stateTween; 

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

        if (currentState.Value == GearState.Opening) TriggerOpenVisuals();
    }

    void Update()
    {
        if (currentState.Value == GearState.Spinning)
        {
            float direction = reverseDirection ? -1f : 1f;
            transform.Rotate(rotationAxis.normalized * originalSpeed * direction * Time.deltaTime, Space.Self);
            
            if (IsServer && !DOTween.IsTweening(transform))
            {
                if (Vector3.Distance(transform.localPosition, originalPosition) > 0.1f)
                {
                    transform.localPosition = originalPosition;
                }
            }
        }

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
        stateTween?.Kill(); // Nếu đang hẹn giờ đóng mà bị bắt mở -> Huỷ ngay cái timer đóng!
        
        DOVirtual.DelayedCall(1.0f, () => {
            transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
        });
    }

    private void TriggerCloseVisuals() 
    {
        transform.DOKill();
        stateTween?.Kill(); 
        
        transform.DOLocalMove(originalPosition, 1.5f).SetEase(Ease.OutCubic).OnComplete(() => {
            transform.localPosition = originalPosition; // Chạy xong thì gán cứng vị trí cho chắc ăn
        });
    }

    public void OpenGear() { if (IsServer) currentState.Value = GearState.Opening; }
    public void CloseGear() { if (IsServer) currentState.Value = GearState.Closing; }

    public void ResetToSpinning()
    {
        if (IsServer) 
        {
            currentState.Value = GearState.Closing; 
            
            stateTween?.Kill(); 
            // Đặt timer 1.6s để quay về Spinning
            stateTween = DOVirtual.DelayedCall(1.6f, () => {
                // KIỂM TRA CHÉO: Tới giờ rồi, mày có còn đang Closing không? 
                // Nếu bị chuyển sang Opening rồi thì bỏ qua không Spinning nữa!
                if (IsServer && currentState.Value == GearState.Closing) 
                {
                    currentState.Value = GearState.Spinning;
                }
            });
        }
    }
}