using UnityEngine;
using Unity.Netcode;
using System.Collections;

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

    // Đồng bộ trạng thái kích hoạt và thời gian kích hoạt chuẩn Server
    private NetworkVariable<bool> isTriggered = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> triggerServerTime = new NetworkVariable<float>(
        0f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private bool localTriggered = false;
    private float timer = 0f;
    
    // Biến này để nhớ góc xoay lúc đầu của ông trên Scene
    private Quaternion gocXoayBanDau;

    // Cờ đánh dấu xem bẫy đang trong lúc chờ rớt không (để không bị đếm đè nhiều lần)
    private bool isCountingDown = false;

    private System.Collections.Generic.Dictionary<GameObject, float> nextDamageTime = new System.Collections.Generic.Dictionary<GameObject, float>();

    void Start()
    {
        // Lưu lại vị trí góc xoay lúc đầu trên Scene
        gocXoayBanDau = transform.localRotation;
        
        // Tự động kéo búa lên vị trí chờ ban đầu
        SetAngle(startAngle);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Nếu bẫy không có vùng kích hoạt (vungKichHoat == null), Server tự động kích hoạt bẫy đung đưa liên tục
        if (IsServer && vungKichHoat == null)
        {
            triggerServerTime.Value = (NetworkManager.Singleton != null) ? (float)NetworkManager.Singleton.ServerTime.TimeAsFloat : 0f;
            isTriggered.Value = true;
        }
    }

    public bool IsTrapActive()
    {
        if (vungKichHoat == null) return true; // Không có vùng kích hoạt thì bẫy luôn hoạt động
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive)
        {
            return isTriggered.Value;
        }
        return localTriggered;
    }

    void Update()
    {
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // Dọn dẹp dictionary nếu GameObject người chơi không còn tồn tại
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

        // 1. Quét vùng cảm ứng dưới đất
        if (!IsTrapActive() && !isCountingDown && vungKichHoat != null)
        {
            Collider[] hitColliders = Physics.OverlapBox(
                vungKichHoat.bounds.center, 
                vungKichHoat.bounds.extents, 
                vungKichHoat.transform.rotation
            );

            foreach (var hit in hitColliders)
            {
                if (IsAnyPlayer(hit.gameObject, out GameObject pRoot))
                {
                    if (isNetworkActive)
                    {
                        if (IsServer)
                        {
                            StartCoroutine(DemNguocTruocKhiSap());
                            break;
                        }
                        else
                        {
                            // Client phát hiện dẫm vào vùng kích hoạt thì gửi RPC lên Server
                            var netObj = pRoot.GetComponent<NetworkObject>();
                            if (netObj != null && netObj.IsOwner)
                            {
                                TriggerTrapServerRpc();
                                isCountingDown = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Offline mode
                        StartCoroutine(DemNguocTruocKhiSap());
                        break;
                    }
                }
            }
        }

        // 2. Vung qua vung lại bằng công thức con lắc đồng bộ
        if (IsTrapActive())
        {
            float elapsed;
            if (isNetworkActive && triggerServerTime.Value > 0f)
            {
                // Đồng bộ chính xác 100% góc xoay theo thời gian Server (kể cả người chơi vào sau / giật lag)
                elapsed = (float)NetworkManager.Singleton.ServerTime.TimeAsFloat - triggerServerTime.Value;
            }
            else
            {
                timer += Time.deltaTime;
                elapsed = timer;
            }

            // Tạo dao động từ startAngle đến -startAngle mượt mà
            float angle = startAngle * Mathf.Cos(elapsed * swingSpeed);
            SetAngle(angle);
        }
        else
        {
            // Giữ nguyên góc chờ ban đầu khi chưa kích hoạt
            SetAngle(startAngle);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TriggerTrapServerRpc()
    {
        if (!isTriggered.Value && !isCountingDown)
        {
            StartCoroutine(DemNguocTruocKhiSap());
        }
    }

    // Hàm chuyên xử lý đếm ngược thời gian
    private IEnumerator DemNguocTruocKhiSap()
    {
        isCountingDown = true; // Khóa chốt lại, tránh đếm đè

        if (delayTime > 0f)
        {
            yield return new WaitForSeconds(delayTime);
        }
        
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive && IsServer)
        {
            triggerServerTime.Value = (NetworkManager.Singleton != null) ? (float)NetworkManager.Singleton.ServerTime.TimeAsFloat : 0f;
            isTriggered.Value = true;
        }
        localTriggered = true;
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
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (IsAnyPlayer(collidedObj, out GameObject playerRoot))
        {
            // Trong chế độ mạng:
            // Server xử lý va chạm cho mọi người chơi.
            // Client chỉ kích hoạt gây sát thương nếu chính nhân vật của Client đó (IsOwner) bị chạm trúng.
            if (isNetworkActive && !IsServer)
            {
                var netObj = playerRoot.GetComponent<NetworkObject>();
                if (netObj == null || !netObj.IsOwner) return;
            }

            float currentTime = Time.time;
            if (!nextDamageTime.TryGetValue(playerRoot, out float nextTime) || currentTime >= nextTime)
            {
                DealContactDamage(playerRoot);
                nextDamageTime[playerRoot] = currentTime + damageCooldown;
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

    private void DealContactDamage(GameObject playerRoot)
    {
        if (playerRoot == null || contactDamage <= 0f) return;

        Debug.Log($"[NetworkFlailTrap] Chạm vào người chơi {playerRoot.name}! Gây {contactDamage} sát thương.");

        // Gọi trực tiếp hàm TakeDamage / RequestTakeDamage cho từng nhân vật (tự động xử lý ServerRpc nếu là Client)
        var elena = playerRoot.GetComponent<ElenaPlayer>() ?? playerRoot.GetComponentInChildren<ElenaPlayer>();
        if (elena != null) { elena.RequestTakeDamage(contactDamage); return; }

        var arthur = playerRoot.GetComponent<ArthurPlayer>() ?? playerRoot.GetComponentInChildren<ArthurPlayer>();
        if (arthur != null) { arthur.RequestTakeDamage(contactDamage); return; }

        var leo = playerRoot.GetComponent<LeoPlayer>() ?? playerRoot.GetComponentInChildren<LeoPlayer>();
        if (leo != null) { leo.RequestTakeDamage(contactDamage); return; }

        var maya = playerRoot.GetComponent<MayaPlayer>() ?? playerRoot.GetComponentInChildren<MayaPlayer>();
        if (maya != null) { maya.RequestTakeDamage(contactDamage); return; }

        var simple = playerRoot.GetComponent<SimplePlayerTest>() ?? playerRoot.GetComponentInChildren<SimplePlayerTest>();
        if (simple != null) { simple.TakeDamage(contactDamage); return; }

        // Fallback: Tìm các script Player khác qua Reflection
        MonoBehaviour[] scripts = playerRoot.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;
            System.Type type = script.GetType();
            string typeName = type.Name;

            if (typeName.EndsWith("Player"))
            {
                var requestDamageMethod = GetMethodInherited(type, "RequestTakeDamage", new System.Type[] { typeof(float) }) ??
                                           GetMethodInherited(type, "TakeDamage", new System.Type[] { typeof(float) });
                if (requestDamageMethod != null)
                {
                    requestDamageMethod.Invoke(script, new object[] { contactDamage });
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

        if (go.CompareTag("Player"))
        {
            playerRoot = go;
            return true;
        }

        if (go.transform.root != null && go.transform.root.CompareTag("Player"))
        {
            playerRoot = go.transform.root.gameObject;
            return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        DrawGizmoTrap(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmoTrap(true);
    }

    private void DrawGizmoTrap(bool isSelected)
    {
        // 1. Vẽ vùng kích hoạt
        if (vungKichHoat != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.4f);
            Gizmos.matrix = Matrix4x4.TRS(vungKichHoat.transform.position, vungKichHoat.transform.rotation, vungKichHoat.transform.lossyScale);
            Gizmos.DrawWireCube(vungKichHoat.center, vungKichHoat.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        // 2. Vẽ trục lắc và góc quét con lắc
        Vector3 pivot = transform.position;
        Vector3 swingAxis = transform.TransformDirection(new Vector3(axisX, axisY, axisZ).normalized);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pivot, 0.4f);
        Gizmos.DrawLine(pivot - swingAxis * 2f, pivot + swingAxis * 2f);

        // Chiều dài dây treo ước lượng từ SphereCollider
        float armLength = 5f;
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc != null)
        {
            armLength = Mathf.Abs(sc.center.y) * transform.lossyScale.y;
        }

        Vector3 downDir = Vector3.down * armLength;
        int arcSegments = 20;
        Vector3 prevArcPt = Vector3.zero;

        for (int i = 0; i <= arcSegments; i++)
        {
            float a = Mathf.Lerp(-Mathf.Abs(startAngle), Mathf.Abs(startAngle), (float)i / arcSegments);
            Vector3 arcPt = pivot + Quaternion.AngleAxis(a, swingAxis) * downDir;

            if (i > 0)
            {
                Gizmos.color = isSelected ? new Color(0f, 1f, 1f, 0.9f) : new Color(0f, 0.8f, 1f, 0.4f);
                Gizmos.DrawLine(prevArcPt, arcPt);
            }
            prevArcPt = arcPt;
        }

        // Vẽ 2 điểm cực đại của góc quét
        Vector3 maxLeft = pivot + Quaternion.AngleAxis(-Mathf.Abs(startAngle), swingAxis) * downDir;
        Vector3 maxRight = pivot + Quaternion.AngleAxis(Mathf.Abs(startAngle), swingAxis) * downDir;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
        Gizmos.DrawLine(pivot, maxLeft);
        Gizmos.DrawLine(pivot, maxRight);
    }
}