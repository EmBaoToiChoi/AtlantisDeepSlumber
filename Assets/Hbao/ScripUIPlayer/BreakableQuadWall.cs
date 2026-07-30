using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class BreakableQuadWall : NetworkBehaviour
{
    [Header("--- VỊ TRÍ SPAWN VFX & TÙY CHỈNH ---")]
    [Tooltip("Nếu tích chọn, VFX sẽ xuất hiện chính giữa tâm Quad. Nếu bỏ tích, VFX xuất hiện tại điểm chạm chân/người chơi")]
    public bool spawnAtQuadCenter = true;

    [Tooltip("Offset dịch chuyển vị trí VFX (X, Y, Z) so với tâm Quad hoặc điểm chạm")]
    public Vector3 vfxSpawnOffset = Vector3.zero;

    [Tooltip("Sử dụng Offset theo hướng xoay của Quad (Local Space) thay vì hệ tọa độ thế giới (World Space)")]
    public bool useLocalOffset = true;

    [Header("--- HƯỚNG XOAY VFX (ROTATION) ---")]
    [Tooltip("Nếu tích chọn, VFX sẽ xoay theo góc xoay của Quad. Nếu bỏ tích, VFX sẽ dùng góc xoay mặc định (Identity)")]
    public bool useQuadRotation = true;

    [Tooltip("Offset góc xoay VFX (Góc Euler X, Y, Z - ví dụ: X = -90 để xoay VFX hướng ra ngoài hay lên trên)")]
    public Vector3 vfxRotationOffset = Vector3.zero;

    [Header("--- CẤU HÌNH HIỆU ỨNG VỠ (VFX & ÂM THANH) ---")]
    [Tooltip("Prefab VFX hiệu ứng vỡ/nổ khi người chơi chạm vào Quad (Kéo Prefab Particle System vào đây)")]
    public GameObject shatterVFXPrefab;

    [Tooltip("Prefab mảnh vỡ 3D (Tùy chọn: nếu có sẵn prefab các mảnh đá/gỗ vỡ nổ ra)")]
    public GameObject brokenDebrisPrefab;

    [Tooltip("Thời gian tự hủy của Prefab VFX / mảnh vỡ sau khi sinh ra (giây)")]
    public float vfxLifetime = 5f;

    [Tooltip("Âm thanh vỡ phát ra khi Quad bị vỡ (Tùy chọn)")]
    public AudioClip shatterSound;

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    [Header("--- LỰC NỔ MẢNH VỠ (DÙNG CHO BROKEN DEBRIS PREFAB) ---")]
    [Tooltip("Lực nổ tác động lên các mảnh vỡ có Rigidbody")]
    public float explosionForce = 500f;

    [Tooltip("Bán kính tác động của lực nổ mảnh vỡ")]
    public float explosionRadius = 5f;

    [Tooltip("Điểm nâng tâm nổ (Upward modifier) tạo hiệu ứng văng lên cao")]
    public float explosionUpward = 1f;

    [Header("--- CẤU HÌNH THỜI GIAN & HÀNH ĐỘNG ---")]
    [Tooltip("Thời gian chờ (giây) từ khi người chơi chạm đến khi Quad vỡ (0 = vỡ ngay lập tức)")]
    public float breakDelay = 0f;

    [Tooltip("Bật hiệu ứng rung lắc Quad nhẹ trước khi vỡ")]
    public bool enableShakeEffect = false;

    [Tooltip("Thời gian rung lắc trước khi vỡ (giây)")]
    public float shakeDuration = 0.3f;

    [Tooltip("Cường độ rung lắc")]
    public float shakeIntensity = 0.1f;

    [Tooltip("Xóa hoàn toàn GameObject khỏi Scene hay chỉ ẩn Mesh & Collider")]
    public bool destroyGameObject = false;

    [Tooltip("Thời gian hủy GameObject sau khi vỡ (nếu destroyGameObject = true)")]
    public float destroyDelayAfterShatter = 5f;

    // Biến mạng đồng bộ trạng thái vỡ trên toàn mạng
    public NetworkVariable<bool> isShattered = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool isShatteredLocal = false;
    private MeshRenderer meshRenderer;
    private Collider wallCollider;
    private Vector3 originalPosition;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();

        wallCollider = GetComponent<Collider>();
        if (wallCollider == null) wallCollider = GetComponentInChildren<Collider>();

        originalPosition = transform.position;
    }

    public Vector3 GetQuadCenterPosition()
    {
        if (meshRenderer != null) return meshRenderer.bounds.center;
        if (wallCollider != null) return wallCollider.bounds.center;
        return transform.position;
    }

    public override void OnNetworkSpawn()
    {
        isShattered.OnValueChanged += OnShatteredChanged;

        // Nếu người chơi vào sau mà bức tường đã bị vỡ trước đó
        if (isShattered.Value)
        {
            ApplyShatterStateVisualsOnly();
        }
    }

    public override void OnNetworkDespawn()
    {
        isShattered.OnValueChanged -= OnShatteredChanged;
    }

    private void OnShatteredChanged(bool oldVal, bool newVal)
    {
        if (newVal && !isShatteredLocal)
        {
            TriggerShatterSequence();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        HandlePlayerTouch(other.gameObject, contactPoint);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;
        Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        HandlePlayerTouch(collision.gameObject, contactPoint);
    }

    private void HandlePlayerTouch(GameObject touchedObject, Vector3 contactPoint)
    {
        if (isShatteredLocal || (isShattered != null && isShattered.Value)) return;

        if (IsPlayer(touchedObject))
        {
            bool isNetwork = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            if (isNetwork)
            {
                if (IsServer)
                {
                    isShattered.Value = true;
                }
                else
                {
                    RequestBreakServerRpc();
                }
            }
            else
            {
                TriggerShatterSequence(contactPoint);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBreakServerRpc()
    {
        if (!isShattered.Value)
        {
            isShattered.Value = true;
        }
    }

    private void TriggerShatterSequence(Vector3 contactPoint = default)
    {
        if (isShatteredLocal) return;
        isShatteredLocal = true;

        if (contactPoint == default)
        {
            contactPoint = transform.position;
        }

        StartCoroutine(ShatterCoroutine(contactPoint));
    }

    private IEnumerator ShatterCoroutine(Vector3 contactPoint)
    {
        // Tính toán vị trí & góc xoay spawn VFX dựa theo thiết lập
        Vector3 basePos = spawnAtQuadCenter ? GetQuadCenterPosition() : contactPoint;
        Vector3 finalVFXPos = basePos + (useLocalOffset ? transform.TransformVector(vfxSpawnOffset) : vfxSpawnOffset);
        
        Quaternion baseRot = useQuadRotation ? transform.rotation : Quaternion.identity;
        Quaternion finalVFXRot = baseRot * Quaternion.Euler(vfxRotationOffset);

        // 1. Rung lắc trước khi vỡ (nếu bật)
        if (enableShakeEffect && shakeDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                Vector3 randomOffset = Random.insideUnitSphere * shakeIntensity;
                transform.position = originalPosition + randomOffset;
                yield return null;
            }
            transform.position = originalPosition;
        }

        // 2. Chờ delay trước khi vỡ
        if (breakDelay > 0f)
        {
            yield return new WaitForSeconds(breakDelay);
        }

        // 3. Âm thanh vỡ
        if (shatterSound != null)
        {
            AudioSource.PlayClipAtPoint(shatterSound, finalVFXPos, soundVolume);
        }

        // 4. Sinh ra Prefab VFX vỡ (Particle System / Explosion VFX) tại vị trí & góc xoay đã thiết lập
        if (shatterVFXPrefab != null)
        {
            GameObject vfxInstance = Instantiate(shatterVFXPrefab, finalVFXPos, finalVFXRot);
            if (vfxLifetime > 0f)
            {
                Destroy(vfxInstance, vfxLifetime);
            }
        }

        // 5. Sinh ra Prefab mảnh vỡ 3D văng nổ ra (nếu có)
        if (brokenDebrisPrefab != null)
        {
            GameObject debrisInstance = Instantiate(brokenDebrisPrefab, GetQuadCenterPosition(), finalVFXRot);
            debrisInstance.transform.localScale = transform.lossyScale;

            Rigidbody[] rbs = debrisInstance.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in rbs)
            {
                if (rb != null)
                {
                    rb.AddExplosionForce(explosionForce, finalVFXPos, explosionRadius, explosionUpward, ForceMode.Impulse);
                }
            }

            if (vfxLifetime > 0f)
            {
                Destroy(debrisInstance, vfxLifetime);
            }
        }

        // 6. Ẩn Mesh và Collider của Quad lập tức để người chơi đi qua được
        DisableQuadVisualsAndPhysics();

        // 7. Xóa GameObject khỏi Scene nếu destroyGameObject = true
        if (destroyGameObject)
        {
            Destroy(gameObject, destroyDelayAfterShatter);
        }
    }

    private void DisableQuadVisualsAndPhysics()
    {
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (wallCollider != null) wallCollider.enabled = false;

        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
        foreach (var r in childRenderers)
        {
            if (r != null) r.enabled = false;
        }

        Collider[] childColliders = GetComponentsInChildren<Collider>();
        foreach (var c in childColliders)
        {
            if (c != null) c.enabled = false;
        }
    }

    private void ApplyShatterStateVisualsOnly()
    {
        isShatteredLocal = true;
        DisableQuadVisualsAndPhysics();
        if (destroyGameObject)
        {
            Destroy(gameObject);
        }
    }

    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;
        if (go.GetComponent<IPlayerHUDTarget>() != null || go.GetComponentInParent<IPlayerHUDTarget>() != null) return true;
        if (go.CompareTag("Player") || (go.transform.parent != null && go.transform.parent.CompareTag("Player"))) return true;
        return false;
    }
}
