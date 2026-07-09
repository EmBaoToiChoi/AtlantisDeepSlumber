using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Script bổ trợ gắn trên Player để xử lý trạng thái bị đá ngã/choáng bởi Boss.
/// Đồng bộ hóa trạng thái qua mạng (Netcode) và khóa toàn bộ di chuyển/tấn công của người chơi.
/// </summary>
public class PlayerKickedStun : NetworkBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Tên của Trigger Parameter trong Animator để kích hoạt ngã (ví dụ: Kicked)")]
    public string kickedTriggerName = "Kicked";
    [Tooltip("Nếu không dùng Trigger, bạn có thể điền thẳng tên State hoạt ảnh ngã trong Animator ở đây")]
    public string kickedStateName = "";

    private MonoBehaviour playerScript;
    private Rigidbody rb;
    private Animator anim;
    private bool isStunned = false;

    private Camera targetCamera;
    private Vector3 lastCameraOffset;

    private void Start()
    {
        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            targetCamera = FindFirstObjectByType<Camera>();
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        // Tìm kiếm các script điều khiển nhân vật tương ứng gắn trên Player
        playerScript = (MonoBehaviour)GetComponent<LeoPlayer>()
            ?? (MonoBehaviour)GetComponent<ArthurPlayer>()
            ?? (MonoBehaviour)GetComponent<ElenaPlayer>()
            ?? (MonoBehaviour)GetComponent<MayaPlayer>()
            ?? (MonoBehaviour)GetComponent<SimplePlayerTest>();
    }

    /// <summary>
    /// Kích hoạt choáng và đẩy văng từ Server, đồng bộ hóa xuống tất cả Client.
    /// </summary>
    public void ApplyKickedStun(float duration, Vector3 knockbackForce)
    {
        if (!IsServer) return;
        ApplyKickedStunClientRpc(duration, knockbackForce);
    }

    [ClientRpc]
    private void ApplyKickedStunClientRpc(float duration, Vector3 knockbackForce)
    {
        // Chạy coroutine khóa điều khiển trên mọi máy khách (đặc biệt là máy Owner)
        StartCoroutine(StunRoutine(duration, knockbackForce));
    }

    private IEnumerator StunRoutine(float duration, Vector3 knockbackForce)
    {
        if (isStunned) yield break;
        isStunned = true;

        // Lưu trữ vị trí tương đối của Camera so với Player trước khi khóa điều khiển để camera follow tức thời
        if (targetCamera == null)
        {
            targetCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        }
        if (targetCamera != null)
        {
            lastCameraOffset = targetCamera.transform.position - transform.position;
        }

        // 1. Tạm thời vô hiệu hóa script điều khiển để khóa phím bấm di chuyển/tấn công
        if (playerScript != null)
        {
            playerScript.enabled = false;
        }

        // 2. Kích hoạt hoạt ảnh bị đá ngã
        if (anim != null && anim.isActiveAndEnabled)
        {
            if (!string.IsNullOrEmpty(kickedTriggerName))
            {
                anim.SetTrigger(kickedTriggerName);
            }
            else if (!string.IsNullOrEmpty(kickedStateName))
            {
                anim.Play(kickedStateName, 0, 0f);
            }
            else
            {
                // Dự phòng nếu không cấu hình: chơi hoạt ảnh trúng đòn ngẫu nhiên
                anim.ResetTrigger("GetHit");
                string hitAnim = Random.value < 0.5f ? "GetHit" : "GeiHit2";
                anim.Play(hitAnim, 0, 0f);
            }
        }

        // 3. Đẩy văng nhân vật vật lý
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(knockbackForce, ForceMode.Impulse);
        }

        // 4. Giữ trạng thái khóa trong suốt thời gian bị đá
        float timer = duration;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            
            // Giảm dần vận tốc trượt vật lý
            if (rb != null && timer < duration - 0.2f)
            {
                rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, Time.deltaTime * 8f);
            }
            yield return null;
        }

        // 5. Bật lại script điều khiển sau khi hết thời gian bị đá (nếu nhân vật chưa chết)
        if (playerScript != null && !IsPlayerDead())
        {
            playerScript.enabled = true;
        }

        isStunned = false;
    }

    private bool IsPlayerDead()
    {
        var hudTarget = GetComponent<IPlayerHUDTarget>();
        if (hudTarget != null)
        {
            return hudTarget.CurrentHealth <= 0;
        }
        return false;
    }

    private void LateUpdate()
    {
        // Khi bị choáng và script điều khiển bị tắt, script này sẽ giữ camera bám sát Player theo thời gian thực (Zero Delay)
        if (isStunned && targetCamera != null)
        {
            targetCamera.transform.position = transform.position + lastCameraOffset;
        }
    }
}
