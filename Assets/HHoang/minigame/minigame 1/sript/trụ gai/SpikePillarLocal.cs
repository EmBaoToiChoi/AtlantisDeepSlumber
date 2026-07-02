using UnityEngine;

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

    private PillarState currentState = PillarState.Idle;
    private Vector3 startRollPosition;
    private float spawnTime;
    private SpikePillarPool associatedPool;

    public void Initialize(Vector3 spawnPosition, Vector3 direction, SpikePillarPool pool)
    {
        transform.position = spawnPosition;
        rollDirection = direction;
        associatedPool = pool;
        currentState = PillarState.Falling;
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
                startRollPosition = transform.position;
            }
        }
        else if (currentState == PillarState.Rolling)
        {
            // Lăn về phía trước
            transform.Translate(rollDirection.normalized * rollSpeed * Time.deltaTime, Space.World);

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
            bool shouldPlay = (currentState == PillarState.Rolling);
            if (shouldPlay && !rollDustEffect.isPlaying)
            {
                rollDustEffect.Play();
            }
            else if (!shouldPlay && rollDustEffect.isPlaying)
            {
                rollDustEffect.Stop();
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
        HandlePlayerCollision(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandlePlayerCollision(collision.gameObject);
    }

    private void HandlePlayerCollision(GameObject collidedObj)
    {
        // Chỉ xử lý chết trên Server (trong mạng) hoặc local (chơi đơn)
        bool isNetworkActive = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;
        bool isServer = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;

        if (isNetworkActive && !isServer) return;

        if (IsAnyPlayer(collidedObj, out GameObject playerRoot))
        {
            DealInstantDeath(playerRoot);
        }
    }

    private void DealInstantDeath(GameObject playerRoot)
    {
        Debug.Log($"[SpikePillar] Chạm vào người chơi {playerRoot.name}! Phá vỡ miễn nhiễm và gây chết ngay lập tức.");

        MonoBehaviour[] scripts = playerRoot.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;
            System.Type type = script.GetType();
            string typeName = type.Name;

            if (typeName == "SimplePlayerTest" || typeName == "LeoPlayer" || typeName == "ArthurPlayer" || 
                typeName == "ElenaPlayer" || typeName == "MayaPlayer" || typeName.EndsWith("Player"))
            {
                // Bẻ gãy toàn bộ trạng thái bất tử/né tránh của người chơi
                
                // 1. Tắt Q Skill Active
                var qActiveField = type.GetField("IsQSkillActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (qActiveField != null) qActiveField.SetValue(script, false);
                
                var qActiveProp = type.GetProperty("IsQSkillActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (qActiveProp != null && qActiveProp.CanWrite) qActiveProp.SetValue(script, false, null);

                // 2. Tắt rolling standalone
                var rollStandaloneField = type.GetField("isRollingStandalone", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rollStandaloneField != null) rollStandaloneField.SetValue(script, false);

                // 3. Tắt rolling net
                var rollNetField = type.GetField("isRollingNet", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rollNetField != null)
                {
                    object netVarObj = rollNetField.GetValue(script);
                    if (netVarObj != null)
                    {
                        var valueProp = netVarObj.GetType().GetProperty("Value");
                        if (valueProp != null && valueProp.CanWrite)
                        {
                            valueProp.SetValue(netVarObj, false);
                        }
                    }
                }

                // 4. Gây sát thương chết ngay
                var takeDamageMethod = type.GetMethod("TakeDamage", new System.Type[] { typeof(float) });
                if (takeDamageMethod != null)
                {
                    takeDamageMethod.Invoke(script, new object[] { 99999f });
                }
            }
        }
    }

    private bool IsAnyPlayer(GameObject go, out GameObject playerRoot)
    {
        playerRoot = null;
        if (go == null) return false;

        var elena = go.GetComponentInParent<ElenaPlayer>();
        if (elena != null) { playerRoot = elena.gameObject; return true; }

        var arthur = go.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) { playerRoot = arthur.gameObject; return true; }

        var leo = go.GetComponentInParent<LeoPlayer>();
        if (leo != null) { playerRoot = leo.gameObject; return true; }

        var maya = go.GetComponentInParent<MayaPlayer>();
        if (maya != null) { playerRoot = maya.gameObject; return true; }

        var simple = go.GetComponentInParent<SimplePlayerTest>();
        if (simple != null) { playerRoot = simple.gameObject; return true; }

        return false;
    }
}
