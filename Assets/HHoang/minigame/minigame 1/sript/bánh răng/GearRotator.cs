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
    public float moveDuration = 0.5f; 
    public Vector3 rotationAxis = Vector3.up;
    private Vector3 originalPosition;

    public NetworkVariable<GearState> currentState = new NetworkVariable<GearState>(GearState.Spinning);
    public ParticleSystem gearSmokeEffect;

    [Header("Cấu hình Gây Sát Thương")]
    public bool dealDamageOnContact = true;
    public float contactDamage = 40f;
    public float damageCooldown = 1.0f;

    private System.Collections.Generic.Dictionary<GameObject, float> nextDamageTime = new System.Collections.Generic.Dictionary<GameObject, float>();

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
        // Khi đang quay hoặc đang đóng/mở, bánh răng vẫn luôn tự quay tròn tại chỗ
        float direction = reverseDirection ? -1f : 1f;
        transform.Rotate(rotationAxis.normalized * originalSpeed * direction * Time.deltaTime, Space.Self);

        // Quản lý hiệu ứng khói bụi trực quan khi di chuyển
        if (gearSmokeEffect != null)
        {
            bool isMoving = (currentState.Value == GearState.Opening || currentState.Value == GearState.Closing);
            if (isMoving && !gearSmokeEffect.isPlaying) gearSmokeEffect.Play();
            else if (!isMoving && gearSmokeEffect.isPlaying) gearSmokeEffect.Stop();
        }

        if (nextDamageTime.Count > 0)
        {
            var keys = new System.Collections.Generic.List<GameObject>(nextDamageTime.Keys);
            foreach (var key in keys)
            {
                if (key == null || !key.activeInHierarchy) nextDamageTime.Remove(key);
            }
        }
    }

    private void TriggerOpenVisuals() 
    {
        transform.DOKill();
        // Đổi Ease.OutBack thành Ease.InOutSine để nó trượt êm, không bị giật nhún
        transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutSine);
    }

    private void TriggerCloseVisuals() 
    {
        transform.DOKill();
        // Đổi thành InOutSine để nó đóng dứt khoát, máy móc hơn, không bị trượt trớn chậm rề rề khúc cuối
        transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.InOutSine).OnComplete(() => {
            transform.localPosition = originalPosition; 
            if (IsServer) currentState.Value = GearState.Spinning;
        });
    }

    public void OpenGear() 
    { 
        if (IsServer && currentState.Value != GearState.Opening) currentState.Value = GearState.Opening; 
    }
    
    public void ResetToSpinning()
    {
        if (IsServer && currentState.Value != GearState.Closing) currentState.Value = GearState.Closing; 
    }
}