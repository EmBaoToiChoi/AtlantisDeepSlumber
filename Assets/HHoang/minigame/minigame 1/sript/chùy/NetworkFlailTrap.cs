using UnityEngine;
using Unity.Netcode;
using System.Collections; // Phải gọi thư viện này ra mới xài đếm ngược (Coroutine) được nha

public class NetworkFlailTrap : NetworkBehaviour
{
    [Header("Kéo cái Object chứa Box Collider làm bẫy vào đây")]
    public BoxCollider vungKichHoat;

    [Header("Thời gian trễ (Delay)")]
    [Tooltip("Để 0 là rớt liền. Để 1 là dẫm trúng 1s sau mới rớt.")]
    public float delayTime = 0f;

    [Header("Trục lật (Chỉ để 1 trục là 1)")]
    public float axisX = 1f;
    public float axisY = 0f;
    public float axisZ = 0f;

    [Header("Cài đặt bẫy")]
    [Tooltip("Góc treo búa lúc chờ (90 là ngang)")]
    public float startAngle = 90f; 
    [Tooltip("Tốc độ lật qua lật lại")]
    public float swingSpeed = 3f;

    [Header("Sát thương")]
    public float contactDamage = 40f;
    public float damageCooldown = 1f;

    private NetworkVariable<bool> isTriggered = new NetworkVariable<bool>(false);
    private float timer = 0f;
    
    // Biến này để nhớ góc xoay lúc đầu của ông trên Scene
    private Quaternion gocXoayBanDau;

    // Cờ đánh dấu xem bẫy đang trong lúc chờ rớt không (để không bị đếm đè nhiều lần)
    private bool isCountingDown = false;

    private System.Collections.Generic.Dictionary<GameObject, float> nextDamageTime = new System.Collections.Generic.Dictionary<GameObject, float>();

    void Start()
    {
        // Lưu lại vị trí góc xoay ông set up trong Scene
        gocXoayBanDau = transform.localRotation;
        
        // Tự động kéo búa lên vị trí chờ 90 độ
        SetAngle(startAngle);
    }

    void Update()
    {
        // 1. Quét vùng cảm ứng dưới đất (Thêm check !isCountingDown để đang đếm ngược thì không quét nữa)
        if (IsServer && !isTriggered.Value && !isCountingDown && vungKichHoat != null)
        {
            Collider[] hitColliders = Physics.OverlapBox(
                vungKichHoat.bounds.center, 
                vungKichHoat.bounds.extents, 
                vungKichHoat.transform.rotation
            );

            foreach (var hit in hitColliders)
            {
                if (hit.CompareTag("Player") || 
                    (hit.transform.parent != null && hit.transform.parent.CompareTag("Player")) ||
                    hit.GetComponentInParent<ElenaPlayer>() != null ||
                    hit.GetComponentInParent<MayaPlayer>() != null ||
                    hit.GetComponentInParent<LeoPlayer>() != null ||
                    hit.GetComponentInParent<ArthurPlayer>() != null ||
                    hit.GetComponentInParent<SimplePlayerTest>() != null)
                {
                    // Phát hiện Player là chạy hàm đếm ngược thả búa
                    StartCoroutine(DemNguocTruocKhiSap());
                    break;
                }
            }
        }

        // 2. Vung qua vung lại bằng công thức con lắc
        if (isTriggered.Value)
        {
            timer += Time.deltaTime;
            
            // Tạo dao động từ startAngle đến -startAngle mượt mà
            float angle = startAngle * Mathf.Cos(timer * swingSpeed);
            SetAngle(angle);
        }
    }

    // Hàm chuyên xử lý đếm ngược thời gian
    private IEnumerator DemNguocTruocKhiSap()
    {
        isCountingDown = true; // Khóa chốt lại, mấy thằng khác dẫm vô sau không làm đếm lại

        // Nếu ông chỉnh thời gian delay lớn hơn 0 thì nó mới đứng chờ
        if (delayTime > 0f)
        {
            yield return new WaitForSeconds(delayTime);
        }
        
        isTriggered.Value = true; // Hết giờ, lật cái rụp!
    }

    private void SetAngle(float angle)
    {
        Vector3 swingAxis = new Vector3(axisX, axisY, axisZ).normalized;
        // Nhân thêm góc xoay ban đầu để búa không bị lệch hướng
        transform.localRotation = gocXoayBanDau * Quaternion.AngleAxis(angle, swingAxis);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerCollision(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        HandlePlayerCollision(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandlePlayerCollision(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandlePlayerCollision(collision.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        RemovePlayerFromDamageList(other.gameObject);
    }

    private void OnCollisionExit(Collision collision)
    {
        RemovePlayerFromDamageList(collision.gameObject);
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

    private void HandlePlayerCollision(GameObject collidedObj)
    {
        // Chỉ xử lý va chạm trên Server (Online) hoặc Local (Offline) để tránh nhân đôi sát thương
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive && !IsServer) return;

        if (IsAnyPlayer(collidedObj, out GameObject playerRoot))
        {
            float currentTime = Time.time;
            if (!nextDamageTime.TryGetValue(playerRoot, out float nextTime) || currentTime >= nextTime)
            {
                DealContactDamage(playerRoot);
                nextDamageTime[playerRoot] = currentTime + damageCooldown;
            }
        }
    }

    private static System.Reflection.FieldInfo GetFieldInherited(System.Type type, string name, System.Reflection.BindingFlags flags)
    {
        System.Type currentType = type;
        while (currentType != null)
        {
            System.Reflection.FieldInfo field = currentType.GetField(name, flags);
            if (field != null) return field;
            currentType = currentType.BaseType;
        }
        return null;
    }

    private static System.Reflection.PropertyInfo GetPropertyInherited(System.Type type, string name, System.Reflection.BindingFlags flags)
    {
        System.Type currentType = type;
        while (currentType != null)
        {
            System.Reflection.PropertyInfo prop = currentType.GetProperty(name, flags);
            if (prop != null) return prop;
            currentType = currentType.BaseType;
        }
        return null;
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

    private void DealContactDamage(GameObject playerRoot)
    {
        Debug.Log($"[NetworkFlailTrap] Chạm vào người chơi {playerRoot.name}! Phá vỡ miễn nhiễm và gây {contactDamage} sát thương.");

        MonoBehaviour[] scripts = playerRoot.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;
            System.Type type = script.GetType();
            string typeName = type.Name;

            if (script is SimplePlayerTest || script is LeoPlayer || script is ArthurPlayer || 
                script is ElenaPlayer || script is MayaPlayer || typeName.EndsWith("Player"))
            {
                // Bẻ gãy toàn bộ trạng thái bất tử/né tránh của người chơi
                
                // 1. Tắt Q Skill Active
                var qActiveField = GetFieldInherited(type, "IsQSkillActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (qActiveField != null) qActiveField.SetValue(script, false);
                
                var qActiveProp = GetPropertyInherited(type, "IsQSkillActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (qActiveProp != null && qActiveProp.CanWrite) qActiveProp.SetValue(script, false, null);

                // 2. Tắt rolling standalone
                var rollStandaloneField = GetFieldInherited(type, "isRollingStandalone", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rollStandaloneField != null) rollStandaloneField.SetValue(script, false);

                // 3. Tắt rolling net
                var rollNetField = GetFieldInherited(type, "isRollingNet", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rollNetField != null)
                {
                    object netVarObj = rollNetField.GetValue(script);
                    if (netVarObj != null)
                    {
                        var valueProp = GetPropertyInherited(netVarObj.GetType(), "Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (valueProp != null && valueProp.CanWrite)
                        {
                            valueProp.SetValue(netVarObj, false);
                        }
                    }
                }

                // 4. Gây sát thương cấu hình
                var requestDamageMethod = GetMethodInherited(type, "RequestTakeDamage", new System.Type[] { typeof(float) }) ??
                                           GetMethodInherited(type, "TakeDamage", new System.Type[] { typeof(float) });
                if (requestDamageMethod != null)
                {
                    requestDamageMethod.Invoke(script, new object[] { contactDamage });
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