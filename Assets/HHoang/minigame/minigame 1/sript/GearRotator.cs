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
        // QUAN TRỌNG: Chỉ quay khi State là Spinning. 
        // Khi đang Opening hoặc Closing, Update "ngủ", không can thiệp vào vị trí nữa.
        if (currentState.Value == GearState.Spinning)
        {
            float direction = reverseDirection ? -1f : 1f;
            transform.Rotate(rotationAxis.normalized * originalSpeed * direction * Time.deltaTime, Space.Self);
            
            // Chỉ ép vị trí khi không có Tween nào đang chạy và không ở trạng thái mở/đóng
            if (!DOTween.IsTweening(transform) && Vector3.Distance(transform.localPosition, originalPosition) > 0.001f)
            {
                transform.localPosition = originalPosition;
            }
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
            // Gửi lệnh cho Client trượt về vị trí cũ trước khi reset trạng thái quay
            ResetClientVisualsClientRpc();
            
            // Đợi 1.6s (dài hơn duration 1.5s của Tween) rồi mới cho quay
            DOVirtual.DelayedCall(1.6f, () => {
                currentState.Value = GearState.Spinning;
            });
        }
    }

    [ClientRpc]
    private void ResetClientVisualsClientRpc()
    {
        transform.DOKill();
        // Đảm bảo nó trượt về mượt, không giật
        transform.DOLocalMove(originalPosition, 1.5f).SetEase(Ease.OutCubic);
    }
}