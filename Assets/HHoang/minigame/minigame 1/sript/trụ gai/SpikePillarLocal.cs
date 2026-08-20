using UnityEngine;
using Unity.Netcode;

public class SpikePillarLocal : MonoBehaviour
{
    public enum PillarState { Idle, Falling, Rolling }

    [Header("Cấu hình di chuyển")]
    public float fallSpeed = 15f;
    public float rollSpeed = 8f;
    public float rotateSpeed = 360f; // Tốc độ xoay (độ/giây)
    public bool reverseRotation = false; // Đảo ngược chiều xoay nếu bị xoay ngược
    public Vector3 rollDirection = Vector3.forward;
    [Tooltip("Góc bù cho 3D model. (0, 90, 0) đặt trụ nằm ngang chắn ngang hành lang để lăn bánh tới trước")]
    public Vector3 meshEulerOffset = new Vector3(0f, 90f, 0f);

    [Header("Cấu hình va chạm mặt đất")]
    public LayerMask groundLayer;
    public float checkGroundDistance = 1.0f; // Khoảng cách từ tâm đến mặt đất để dừng rơi và bắt đầu lăn
    public float cylinderRadius = 1.0f; // Bán kính trụ để tính tốc độ lăn khớp mặt đất

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
    private float currentRollAngle = 0f;
    private Quaternion baseOrientation = Quaternion.identity;

    public void Initialize(Vector3 spawnPosition, Vector3 direction, SpikePillarPool pool)
    {
        transform.position = spawnPosition;
        
        // Chuẩn hóa hướng lăn trên mặt phẳng ngang (X-Z)
        Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);
        if (flatDir.sqrMagnitude < 0.0001f) flatDir = Vector3.forward;
        rollDirection = flatDir.normalized;
        
        associatedPool = pool;
        currentState = PillarState.Falling;
        isTouchingGround = false;
        spawnTime = Time.time;
        currentRollAngle = 0f;
        
        // Căn chỉnh góc xoay cơ sở: thân trụ nằm ngang tuyệt đối, vuông góc với hướng lăn
        baseOrientation = Quaternion.LookRotation(rollDirection, Vector3.up);
        ApplyRotation(0f);
        
        gameObject.SetActive(true);
    }

    public void ApplyRotation(float angle)
    {
        // baseOrientation hướng thẳng theo rollDirection
        // Trục X cục bộ (Vector3.right) luôn nằm ngang vuông góc với hướng lăn
        // Xoay quanh trục X cục bộ để lăn tròn về phía trước không bao giờ bị xéo
        Quaternion rollRot = Quaternion.AngleAxis(angle, Vector3.right);
        Quaternion offsetRot = (meshEulerOffset != Vector3.zero) ? Quaternion.Euler(meshEulerOffset) : Quaternion.identity;
        
        transform.rotation = baseOrientation * rollRot * offsetRot;
    }

    void Update()
    {
        if (currentState == PillarState.Idle) return;

        LayerMask mask = (groundLayer.value != 0) ? groundLayer : ~LayerMask.GetMask("Player", "Ignore Raycast");

        if (currentState == PillarState.Falling)
        {
            // Rơi thẳng đứng xuống
            transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);
            ApplyRotation(0f);

            // Kiểm tra chạm đất bằng Raycast
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, checkGroundDistance + 0.5f, mask))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y + checkGroundDistance - 0.05f, transform.position.z);
                currentState = PillarState.Rolling;
                isTouchingGround = true;
                startRollPosition = transform.position;
            }
            else if (Time.time - spawnTime > 2.0f)
            {
                // Fallback: Nếu rơi quá 2s mà không trúng Raycast, tự động chuyển sang lăn để không bị kẹt
                currentState = PillarState.Rolling;
                isTouchingGround = true;
                startRollPosition = transform.position;
            }
        }
        else if (currentState == PillarState.Rolling)
        {
            // Lăn về phía trước theo rollDirection
            transform.Translate(rollDirection * rollSpeed * Time.deltaTime, Space.World);

            // Tự động xoay tròn lăn bánh
            float rollDelta = (rotateSpeed > 0f ? rotateSpeed : (rollSpeed / Mathf.Max(cylinderRadius, 0.1f)) * Mathf.Rad2Deg) * Time.deltaTime;
            if (reverseRotation) rollDelta = -rollDelta;
            currentRollAngle = (currentRollAngle + rollDelta) % 360f;
            ApplyRotation(currentRollAngle);

            // Kiểm tra chạm đất để bám địa hình hoặc rơi xuống hố
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, checkGroundDistance + 1.0f, mask))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y + checkGroundDistance - 0.05f, transform.position.z);
                isTouchingGround = true;
            }
            else
            {
                // Không có đất -> Rơi xuống hố
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

        var elena = playerRoot.GetComponent<ElenaPlayer>() ?? playerRoot.GetComponentInChildren<ElenaPlayer>();
        if (elena != null) { elena.RequestTakeDamage(damage); return; }

        var arthur = playerRoot.GetComponent<ArthurPlayer>() ?? playerRoot.GetComponentInChildren<ArthurPlayer>();
        if (arthur != null) { arthur.RequestTakeDamage(damage); return; }

        var leo = playerRoot.GetComponent<LeoPlayer>() ?? playerRoot.GetComponentInChildren<LeoPlayer>();
        if (leo != null) { leo.RequestTakeDamage(damage); return; }

        var maya = playerRoot.GetComponent<MayaPlayer>() ?? playerRoot.GetComponentInChildren<MayaPlayer>();
        if (maya != null) { maya.RequestTakeDamage(damage); return; }

        var simple = playerRoot.GetComponent<SimplePlayerTest>() ?? playerRoot.GetComponentInChildren<SimplePlayerTest>();
        if (simple != null) { simple.TakeDamage(damage); return; }

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
