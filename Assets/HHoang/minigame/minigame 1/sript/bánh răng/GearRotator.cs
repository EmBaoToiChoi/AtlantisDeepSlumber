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

    private void OnCollisionEnter(Collision collision)
    {
        HandlePlayerDamage(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandlePlayerDamage(collision.gameObject);
    }

    private void OnCollisionExit(Collision collision)
    {
        RemovePlayerFromDamageList(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerDamage(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        HandlePlayerDamage(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        RemovePlayerFromDamageList(other.gameObject);
    }

    private static System.Reflection.MethodInfo GetMethodInherited(System.Type type, string name, System.Type[] types)
    {
        System.Type currentType = type;
        while (currentType != null)
        {
            System.Reflection.MethodInfo method = currentType.GetMethod(name, types);
            if (method != null) return method;
            currentType = currentType.BaseType;
        }
        return null;
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

            if (script is SimplePlayerTest || script is LeoPlayer || script is ArthurPlayer || 
                script is ElenaPlayer || script is MayaPlayer || typeName.EndsWith("Player"))
            {
                var requestDamageMethod = GetMethodInherited(type, "RequestTakeDamage", new System.Type[] { typeof(float) }) ??
                                           GetMethodInherited(type, "TakeDamage", new System.Type[] { typeof(float) });
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

        // Chỉ xử lý va chạm với CHÍNH người chơi sở hữu máy này (local player)
        // để tránh một máy khách này tính toán va chạm hộ cho máy khách khác gây lỗi nhân đôi sát thương.
        
        var elena = go.GetComponentInParent<ElenaPlayer>() ?? go.GetComponentInChildren<ElenaPlayer>();
        if (elena != null) 
        {
            if (elena.IsOwner || (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening))
            {
                playerRoot = elena.gameObject; 
                return true; 
            }
        }

        var arthur = go.GetComponentInParent<ArthurPlayer>() ?? go.GetComponentInChildren<ArthurPlayer>();
        if (arthur != null) 
        {
            if (arthur.IsOwner || (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening))
            {
                playerRoot = arthur.gameObject; 
                return true; 
            }
        }

        var leo = go.GetComponentInParent<LeoPlayer>() ?? go.GetComponentInChildren<LeoPlayer>();
        if (leo != null) 
        {
            if (leo.IsOwner || (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening))
            {
                playerRoot = leo.gameObject; 
                return true; 
            }
        }

        var maya = go.GetComponentInParent<MayaPlayer>() ?? go.GetComponentInChildren<MayaPlayer>();
        if (maya != null) 
        {
            if (maya.IsOwner || (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening))
            {
                playerRoot = maya.gameObject; 
                return true; 
            }
        }

        var simple = go.GetComponentInParent<SimplePlayerTest>() ?? go.GetComponentInChildren<SimplePlayerTest>();
        if (simple != null) 
        {
            if (simple.IsOwner || (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening))
            {
                playerRoot = simple.gameObject; 
                return true; 
            }
        }

        return false;
    }
}