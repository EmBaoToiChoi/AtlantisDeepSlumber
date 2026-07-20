using UnityEngine;
using Unity.Netcode;

public class SpikePillarLocal : MonoBehaviour
{
    public enum PillarState { Idle, Falling, Rolling }

    [Header("Cấu hình di chuyển")]
    public float fallSpeed = 15f;
    public float rollSpeed = 8f;
    public float rotateSpeed = 360f; // Tốc độ xoay (độ/giây)
    public bool reverseRotation = false; // Đảo ngược chiều xoay (Đặt false để quay tiến tới mặc định)
    public Vector3 rollDirection = Vector3.forward;
    public Vector3 rotationAxis = Vector3.right; // Trục gai nằm ngang xoay tròn

    [Header("Cấu hình va chạm mặt đất")]
    public LayerMask groundLayer;
    public float checkGroundDistance = 1.0f; // Khoảng cách từ tâm đến mặt đất để dừng rơi và bắt đầu lăn

    [Header("Cấu hình giới hạn")]
    public float maxLifetime = 25f;
    public float maxRollDistance = 120f;

    [Header("Hiệu ứng bụi khói khi lăn")]
    public ParticleSystem rollDustEffect;

    [Header("Cấu hình Gây Sát Thương")]
    public float contactDamage = 40f;
    public float damageCooldown = 1.0f;

    private PillarState currentState = PillarState.Idle;
    private Vector3 startRollPosition;
    private float spawnTime;
    private SpikePillarPool associatedPool;
    private System.Collections.Generic.Dictionary<GameObject, float> nextDamageTime = new System.Collections.Generic.Dictionary<GameObject, float>();
    private bool isTouchingGround = false;

    public void Initialize(Vector3 spawnPosition, Vector3 direction, SpikePillarPool pool)
    {
        transform.position = spawnPosition;
        rollDirection = direction;
        associatedPool = pool;
        currentState = PillarState.Falling;
        isTouchingGround = false;
        spawnTime = Time.time;
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (currentState == PillarState.Idle) return;

        // Tự động tính toán trục xoay dựa trên hướng lăn để hướng xoay luôn khớp với hướng di chuyển
        Vector3 dynamicRotationAxis = Vector3.Cross(Vector3.up, rollDirection.normalized);
        if (dynamicRotationAxis == Vector3.zero)
        {
            dynamicRotationAxis = rotationAxis;
        }

        float dir = reverseRotation ? -1f : 1f;
        transform.Rotate(dynamicRotationAxis, rotateSpeed * dir * Time.deltaTime, Space.World);

        if (currentState == PillarState.Falling)
        {
            // Rơi xuống
            transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);

            // Kiểm tra chạm đất bằng Raycast
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, checkGroundDistance, groundLayer))
            {
                // Set vị trí khớp mặt đất (cộng thêm khoảng cách bán kính và dịch lên chút)
                transform.position = new Vector3(transform.position.x, hit.point.y + checkGroundDistance - 0.05f, transform.position.z);
                currentState = PillarState.Rolling;
                isTouchingGround = true;
                startRollPosition = transform.position;
            }
        }
        else if (currentState == PillarState.Rolling)
        {
            // Lăn về phía trước
            transform.Translate(rollDirection.normalized * rollSpeed * Time.deltaTime, Space.World);

            // Kiểm tra chạm đất để bám địa hình hoặc rơi xuống hố
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, checkGroundDistance + 0.5f, groundLayer))
            {
                // Bám sát mặt đất
                transform.position = new Vector3(transform.position.x, hit.point.y + checkGroundDistance - 0.05f, transform.position.z);
                isTouchingGround = true;
            }
            else
            {
                // Không có đất -> Rơi xuống hố (di chuyển tịnh tiến đi xuống)
                transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);
                isTouchingGround = false;
            }

            // Tự động thu hồi nếu lăn quá xa
            if (Vector3.Distance(startRollPosition, transform.position) >= maxRollDistance)
            {
                ReturnToPool();
            }
        }

        // Tự động thu hồi nếu tồn tại quá lâu
        if (Time.time - spawnTime >= maxLifetime)
        {
            ReturnToPool();
        }

        // Cập nhật hiệu ứng bụi khói
        if (rollDustEffect != null)
        {
            bool shouldPlay = (currentState == PillarState.Rolling && isTouchingGround);
            if (shouldPlay && !rollDustEffect.isPlaying)
            {
                rollDustEffect.Play();
            }
            else if (!shouldPlay && rollDustEffect.isPlaying)
            {
                rollDustEffect.Stop();
            }
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

    private void ReturnToPool()
    {
        currentState = PillarState.Idle;
        if (rollDustEffect != null)
        {
            rollDustEffect.Stop();
            rollDustEffect.Clear();
        }
        gameObject.SetActive(false);
        if (associatedPool != null)
        {
            associatedPool.ReturnPillar(this);
        }
        else
        {
            Destroy(gameObject);
        }
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

    private void HandlePlayerDamage(GameObject otherGo)
    {
        if (IsAnyPlayer(otherGo, out GameObject playerRoot))
        {
            // Kiểm tra khoảng cách thực tế để tránh lỗi trôi/kẹt trigger khi dịch chuyển
            float dist = Vector3.Distance(transform.position, playerRoot.transform.position);
            if (dist > 10f)
            {
                if (nextDamageTime.ContainsKey(playerRoot))
                {
                    nextDamageTime.Remove(playerRoot);
                }
                return;
            }

            if (nextDamageTime.ContainsKey(playerRoot))
            {
                if (Time.time >= nextDamageTime[playerRoot])
                {
                    DealDamage(playerRoot, contactDamage);
                    nextDamageTime[playerRoot] = Time.time + damageCooldown;
                }
            }
            else
            {
                DealDamage(playerRoot, contactDamage);
                nextDamageTime[playerRoot] = Time.time + damageCooldown;
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

        Debug.Log($"[SpikePillarLocal] Gây {damage} sát thương cho {playerRoot.name}");

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
