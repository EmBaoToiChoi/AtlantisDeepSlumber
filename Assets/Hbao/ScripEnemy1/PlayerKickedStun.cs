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
    private bool isWaitingForStandUp = false;

    public void OnStandUpFinished()
    {
        isWaitingForStandUp = false;
        Debug.Log($"[{gameObject.name}] OnStandUpFinished received.");
    }

    /// <summary>
    /// Reset toàn bộ trạng thái choáng và bật lại script điều khiển (dùng khi hồi sinh).
    /// </summary>
    public void ResetStunState()
    {
        isStunned = false;
        isWaitingForStandUp = false;
        StopAllCoroutines();
        if (playerScript != null)
        {
            playerScript.enabled = true;
        }
        Debug.Log($"[{gameObject.name}] PlayerKickedStun: Đã reset trạng thái choáng và bật lại phím điều khiển.");
    }

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
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
        {
            if (!IsServer) return;
            ApplyKickedStunClientRpc(duration, knockbackForce);
        }
        else
        {
            StartCoroutine(StunRoutine(duration, knockbackForce));
        }
    }

    [ClientRpc]
    private void ApplyKickedStunClientRpc(float duration, Vector3 knockbackForce)
    {
        // Chạy coroutine khóa điều khiển trên mọi máy khách (đặc biệt là máy Owner)
        StartCoroutine(StunRoutine(duration, knockbackForce));
    }

    /// <summary>
    /// Kích hoạt hiệu ứng bị lốc xoáy hất tung lên cao, xoay vòng trên không, rơi xuống đất và ngã rồi đứng dậy.
    /// </summary>
    public void ApplyTornadoKnockup(float liftHeight = 4.5f, float liftDuration = 1.2f)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
        {
            if (!IsServer) return;
            ApplyTornadoKnockupClientRpc(liftHeight, liftDuration);
        }
        else
        {
            StartCoroutine(TornadoTrapRoutine(liftHeight, liftDuration));
        }
    }

    [ClientRpc]
    private void ApplyTornadoKnockupClientRpc(float liftHeight, float liftDuration)
    {
        StartCoroutine(TornadoTrapRoutine(liftHeight, liftDuration));
    }

    private IEnumerator TornadoTrapRoutine(float liftHeight, float liftDuration)
    {
        if (isStunned) yield break;
        isStunned = true;

        if (targetCamera == null)
        {
            targetCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        }
        if (targetCamera != null)
        {
            lastCameraOffset = targetCamera.transform.position - transform.position;
        }

        // 1. Tạm thời vô hiệu hóa script điều khiển
        if (playerScript != null)
        {
            playerScript.enabled = false;
        }

        // Kích hoạt rung camera bão tố cho Player bị cuốn
        CameraShakeHelper.Shake(liftDuration + 0.2f, 0.2f);

        Vector3 startPos = transform.position;
        Vector3 peakPos = startPos + Vector3.up * liftHeight;

        // 2. Giai đoạn bay lên & xoay tít trên không trong tâm bão
        float elapsed = 0f;
        while (elapsed < liftDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / liftDuration);
            
            // Bay lên và hạ xuống theo đường cong Parabola (sin(t * PI))
            float heightFactor = Mathf.Sin(t * Mathf.PI);
            Vector3 wobble = new Vector3(Mathf.Cos(elapsed * 15f) * 0.35f, 0f, Mathf.Sin(elapsed * 15f) * 0.35f);
            transform.position = Vector3.Lerp(startPos, peakPos, heightFactor) + wobble;

            // Xoay tròn đều quanh trục Y như bị lốc xoáy cuốn
            transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);

            yield return null;
        }

        transform.position = startPos;

        // Rung nhẹ khi đập người xuống đất
        CameraShakeHelper.Shake(0.35f, 0.16f);

        // 3. Rơi xuống đất -> Kích hoạt hoạt ảnh té ngã và đứng dậy
        if (anim != null && anim.isActiveAndEnabled)
        {
            isWaitingForStandUp = true;
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
                anim.Play("te", 0, 0f);
            }
        }

        // 4. Giữ khóa điều khiển cho đến khi hoạt ảnh té & đứng dậy (NgoiDay) hoàn tất
        float safetyTimer = 4.0f;
        yield return new WaitForSeconds(0.5f); // Chờ Animator chuyển vào state ngã

        while (isWaitingForStandUp && safetyTimer > 0)
        {
            safetyTimer -= Time.deltaTime;
            yield return null;
        }

        // 5. Bật lại điều khiển cho Player sau khi đứng dậy hoàn tất
        if (playerScript != null && !IsPlayerDead())
        {
            playerScript.enabled = true;
        }

        isStunned = false;
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
            isWaitingForStandUp = true;
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
        float maxSafetyTimer = 5f;
        while ((timer > 0 || isWaitingForStandUp) && maxSafetyTimer > 0)
        {
            if (timer > 0)
            {
                timer -= Time.deltaTime;
                // Giảm dần vận tốc trượt vật lý
                if (rb != null && timer < duration - 0.2f)
                {
                    rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, Time.deltaTime * 8f);
                }
            }
            if (timer <= 0)
            {
                maxSafetyTimer -= Time.deltaTime;
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
