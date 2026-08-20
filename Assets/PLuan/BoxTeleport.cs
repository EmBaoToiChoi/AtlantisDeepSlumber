using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_AI_NAVIGATION || UNITY_AI
using UnityEngine.AI;
#endif

public class BoxTeleport : MonoBehaviour
{
    [Header("--- ĐÍCH ĐẾN DỊCH CHUYỂN ---")]
    [Tooltip("Kéo GameObject / Transform đích đến vào đây để dịch chuyển tới")]
    public Transform targetDestination;

    [Tooltip("Độ lệch vị trí khi xuất hiện tại điểm đến (ví dụ: Y = 0.5 để không bị lún xuống đất)")]
    public Vector3 spawnOffset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("Có xoay hướng nhìn của đối tượng theo hướng của targetDestination không?")]
    public bool matchTargetRotation = true;

    [Header("--- ĐỐI TƯỢNG ĐƯỢC DỊCH CHUYỂN ---")]
    [Tooltip("Chỉ cho phép Player dịch chuyển (nếu tắt thì bất kỳ vật thể nào chạm vào cũng dịch chuyển)")]
    public bool onlyPlayer = true;

    [Tooltip("Tag của người chơi")]
    public string playerTag = "Player";

    [Header("--- THIẾT LẬP THỜI GIAN & HIỆU ỨNG ---")]
    [Tooltip("Thời gian chờ giữa 2 lần dịch chuyển (tránh bị dịch chuyển liên tục lặp vòng)")]
    public float teleportCooldown = 1.0f;

    [Tooltip("Âm thanh khi dịch chuyển (tùy chọn)")]
    public AudioClip teleportSound;

    [Tooltip("Prefab hiệu ứng VFX khi dịch chuyển (tùy chọn)")]
    public GameObject teleportVfxPrefab;

    [Tooltip("Có tạo hiệu ứng VFX tại điểm đến hay không?")]
    public bool spawnVfxAtDestination = true;

    // Lưu thời gian dịch chuyển gần nhất của từng đối tượng
    private Dictionary<GameObject, float> lastTeleportTimes = new Dictionary<GameObject, float>();

    private void Awake()
    {
        // Đảm bảo Box có Collider và nên bật IsTrigger
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"[BoxTeleport] '{gameObject.name}' chưa có Collider! Đang tự động thêm BoxCollider (Is Trigger = true).");
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTeleport(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Hỗ trợ trường hợp người dùng quên tích 'Is Trigger' trên Collider
        HandleTeleport(collision.gameObject);
    }

    /// <summary>
    /// Xử lý logic dịch chuyển đối tượng
    /// </summary>
    public void HandleTeleport(GameObject touchedObject)
    {
        if (targetDestination == null)
        {
            Debug.LogWarning($"[BoxTeleport] '{gameObject.name}' chưa được gán Target Destination trong Inspector!");
            return;
        }

        if (touchedObject == null) return;

        // Tìm Root GameObject hoặc Player Object cha nếu va chạm ở Collider con
        GameObject rootTarget = GetRootTargetObject(touchedObject);
        if (rootTarget == null) rootTarget = touchedObject;

        // Kiểm tra điều kiện Player nếu bật onlyPlayer
        if (onlyPlayer && !IsPlayer(rootTarget) && !IsPlayer(touchedObject))
        {
            return;
        }

        // Kiểm tra Cooldown tránh spam dịch chuyển liên tục
        if (lastTeleportTimes.TryGetValue(rootTarget, out float lastTime))
        {
            if (Time.time - lastTime < teleportCooldown)
            {
                return;
            }
        }
        lastTeleportTimes[rootTarget] = Time.time;

        // Thực hiện dịch chuyển an toàn qua Coroutine
        StartCoroutine(TeleportSafelyRoutine(rootTarget));
    }

    private IEnumerator TeleportSafelyRoutine(GameObject targetObj)
    {
        if (targetObj == null || targetDestination == null) yield break;

        Vector3 destinationPos = targetDestination.position + spawnOffset;
        Quaternion destinationRot = matchTargetRotation ? targetDestination.rotation : targetObj.transform.rotation;

        // 1. Phát âm thanh (nếu có)
        if (teleportSound != null)
        {
            AudioSource.PlayClipAtPoint(teleportSound, transform.position);
        }

        // 2. Tạo hiệu ứng VFX tại vị trí ban đầu (nếu có)
        if (teleportVfxPrefab != null)
        {
            Instantiate(teleportVfxPrefab, targetObj.transform.position, Quaternion.identity);
        }

        // 3. Tìm các component điều khiển di chuyển
        CharacterController cc = targetObj.GetComponent<CharacterController>();
        if (cc == null) cc = targetObj.GetComponentInChildren<CharacterController>();
        if (cc == null) cc = targetObj.GetComponentInParent<CharacterController>();

        UnityEngine.AI.NavMeshAgent nav = targetObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav == null) nav = targetObj.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
        if (nav == null) nav = targetObj.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();

        Rigidbody rb = targetObj.GetComponent<Rigidbody>();
        if (rb == null) rb = targetObj.GetComponentInChildren<Rigidbody>();
        if (rb == null) rb = targetObj.GetComponentInParent<Rigidbody>();

        // 4. Vô hiệu hóa tạm thời để tránh Unity Physics cưỡng chế vị trí cũ
        bool wasCcEnabled = false;
        if (cc != null)
        {
            wasCcEnabled = cc.enabled;
            cc.enabled = false;
        }

        bool wasNavEnabled = false;
        if (nav != null)
        {
            wasNavEnabled = nav.enabled;
            nav.enabled = false;
        }

        bool wasKinematic = false;
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
#endif
        }

        yield return new WaitForEndOfFrame();

        // 5. Cập nhật vị trí & hướng quay
        targetObj.transform.position = destinationPos;
        if (matchTargetRotation)
        {
            targetObj.transform.rotation = destinationRot;
        }

        if (rb != null)
        {
            rb.position = destinationPos;
            if (matchTargetRotation) rb.rotation = destinationRot;
        }

        // Hỗ trợ Unity Netcode for GameObjects (NetworkTransform) nếu có
        var netTransform = targetObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform == null) netTransform = targetObj.GetComponentInChildren<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform == null) netTransform = targetObj.GetComponentInParent<Unity.Netcode.Components.NetworkTransform>();

        if (netTransform != null && netTransform.IsSpawned)
        {
            try
            {
                netTransform.Teleport(destinationPos, destinationRot, targetObj.transform.localScale);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoxTeleport] NetworkTransform.Teleport warning: {ex.Message}");
            }
        }

        // Đồng bộ hóa Physics của Unity ngay lập tức
        Physics.SyncTransforms();

        yield return new WaitForEndOfFrame();

        // 6. Khôi phục lại trạng thái của các component
        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
#endif
        }

        if (cc != null && wasCcEnabled)
        {
            cc.enabled = true;
        }

        if (nav != null && wasNavEnabled)
        {
            nav.enabled = true;
            nav.Warp(destinationPos);
        }

        // 7. Tạo hiệu ứng VFX tại điểm đến (nếu có)
        if (teleportVfxPrefab != null && spawnVfxAtDestination)
        {
            Instantiate(teleportVfxPrefab, destinationPos, Quaternion.identity);
        }

        Debug.Log($"[BoxTeleport] Đã dịch chuyển thành công '{targetObj.name}' tới '{targetDestination.name}' tại tọa độ {destinationPos}");
    }

    /// <summary>
    /// Tìm đối tượng gốc (Root Player hoặc Rigidbody holder)
    /// </summary>
    private GameObject GetRootTargetObject(GameObject go)
    {
        if (go == null) return null;

        // Ưu tiên đối tượng có CharacterController
        CharacterController cc = go.GetComponentInParent<CharacterController>();
        if (cc != null) return cc.gameObject;

        // Hoặc đối tượng có Rigidbody
        Rigidbody rb = go.GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.gameObject;

        // Hoặc đối tượng cha có tag Player
        Transform curr = go.transform;
        while (curr != null)
        {
            if (curr.CompareTag(playerTag)) return curr.gameObject;
            curr = curr.parent;
        }

        return go.transform.root.gameObject;
    }

    /// <summary>
    /// Kiểm tra xem đối tượng có phải là người chơi hay không
    /// </summary>
    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;

        // Kiểm tra Tag
        if (go.CompareTag(playerTag)) return true;
        if (go.transform.parent != null && go.transform.parent.CompareTag(playerTag)) return true;
        if (go.transform.root != null && go.transform.root.CompareTag(playerTag)) return true;

        // Kiểm tra các script Player đặc thù trong game nếu có
        if (go.GetComponent<CharacterController>() != null || go.GetComponentInParent<CharacterController>() != null) return true;
        if (go.GetComponent("LeoPlayer") != null || go.GetComponentInParent<MonoBehaviour>()?.GetType().Name == "LeoPlayer") return true;
        if (go.GetComponent("ArthurPlayer") != null || go.GetComponentInParent<MonoBehaviour>()?.GetType().Name == "ArthurPlayer") return true;
        if (go.GetComponent("ElenaPlayer") != null || go.GetComponentInParent<MonoBehaviour>()?.GetType().Name == "ElenaPlayer") return true;
        if (go.GetComponent("MayaPlayer") != null || go.GetComponentInParent<MonoBehaviour>()?.GetType().Name == "MayaPlayer") return true;

        return false;
    }

    // Vẽ đường nối trực quan trong Scene View của Unity Editor
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }

        if (targetDestination != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 destPos = targetDestination.position + spawnOffset;
            Gizmos.DrawLine(transform.position, destPos);
            Gizmos.DrawSphere(destPos, 0.3f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (targetDestination != null)
        {
            Gizmos.color = Color.green;
            Vector3 destPos = targetDestination.position + spawnOffset;
            Gizmos.DrawLine(transform.position, destPos);
            Gizmos.DrawWireSphere(destPos, 0.5f);
        }
    }
}
