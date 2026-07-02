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

    [Header("Cấu hình Gây Sát Thương")]
    public bool dealDamageOnContact = true;
    public float contactDamage = 40f;
    public float damageCooldown = 1.0f; // Giãn cách giữa các lần bị trừ máu từ bánh răng này

    private System.Collections.Generic.Dictionary<GameObject, float> nextDamageTime = new System.Collections.Generic.Dictionary<GameObject, float>();

    private bool IsNetworkActive => NetworkManager != null && NetworkManager.IsListening;

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

        // Dọn dẹp dictionary nếu các Player GameObject bị hủy/null hoặc không hoạt động
        if (nextDamageTime.Count > 0)
        {
            var keys = new System.Collections.Generic.List<GameObject>(nextDamageTime.Keys);
            foreach (var key in keys)
            {
                if (key == null || !key.activeInHierarchy)
                {
                    nextDamageTime.Remove(key);
                }
            }
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

    private void HandlePlayerDamage(GameObject otherGo)
    {
        if (!dealDamageOnContact) return;

        if (IsAnyPlayer(otherGo, out GameObject playerRoot))
        {
            float currentTime = Time.time;
            if (!nextDamageTime.TryGetValue(playerRoot, out float nextTime) || currentTime >= nextTime)
            {
                DealDamage(playerRoot, contactDamage);
                nextDamageTime[playerRoot] = currentTime + damageCooldown;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandlePlayerDamage(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandlePlayerDamage(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerDamage(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        HandlePlayerDamage(other.gameObject);
    }

    private void OnCollisionExit(Collision collision)
    {
        RemovePlayerFromDamageList(collision.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        RemovePlayerFromDamageList(other.gameObject);
    }

    private void RemovePlayerFromDamageList(GameObject otherGo)
    {
        if (IsAnyPlayer(otherGo, out GameObject playerRoot))
        {
            if (nextDamageTime.ContainsKey(playerRoot))
            {
                nextDamageTime.Remove(playerRoot);
            }
        }
    }

    private void DealDamage(GameObject playerRoot, float damage)
    {
        if (damage <= 0f) return;

        Debug.Log($"[GearRotator] Gây {damage} sát thương cho {playerRoot.name}");

        MonoBehaviour[] scripts = playerRoot.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;
            System.Type type = script.GetType();
            string typeName = type.Name;

            if (typeName == "SimplePlayerTest" || typeName == "LeoPlayer" || typeName == "ArthurPlayer" || 
                typeName == "ElenaPlayer" || typeName == "MayaPlayer" || typeName.EndsWith("Player"))
            {
                var requestDamageMethod = type.GetMethod("RequestTakeDamage", new System.Type[] { typeof(float) }) ??
                                          type.GetMethod("TakeDamage", new System.Type[] { typeof(float) });
                if (requestDamageMethod != null)
                {
                    requestDamageMethod.Invoke(script, new object[] { damage });
                    return;
                }
            }
        }
    }

    private bool IsAnyPlayer(GameObject go, out GameObject playerRoot)
    {
        playerRoot = null;
        if (go == null) return false;

        var elena = go.GetComponentInParent<ElenaPlayer>() ?? go.GetComponentInChildren<ElenaPlayer>();
        if (elena != null) { playerRoot = elena.gameObject; return true; }

        var arthur = go.GetComponentInParent<ArthurPlayer>() ?? go.GetComponentInChildren<ArthurPlayer>();
        if (arthur != null) { playerRoot = arthur.gameObject; return true; }

        var leo = go.GetComponentInParent<LeoPlayer>() ?? go.GetComponentInChildren<LeoPlayer>();
        if (leo != null) { playerRoot = leo.gameObject; return true; }

        var maya = go.GetComponentInParent<MayaPlayer>() ?? go.GetComponentInChildren<MayaPlayer>();
        if (maya != null) { playerRoot = maya.gameObject; return true; }

        var simple = go.GetComponentInParent<SimplePlayerTest>() ?? go.GetComponentInChildren<SimplePlayerTest>();
        if (simple != null) { playerRoot = simple.gameObject; return true; }

        return false;
    }
}