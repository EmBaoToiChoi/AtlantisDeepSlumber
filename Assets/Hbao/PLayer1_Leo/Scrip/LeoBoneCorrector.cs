using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Hiệu chỉnh xương Leo khi tấn công — chạy LateUpdate SAU Animator.
/// Network-compatible: tự động áp dụng trên TẤT CẢ clients cho TẤT CẢ player instances.
///
/// CÁCH HOẠT ĐỘNG:
///   • Phát hiện attack state qua Animator (state đã được đồng bộ bởi PlayAnimationClientRpc).
///   • Khóa Spine/Chest theo transform.forward (đã được đồng bộ bởi NetworkTransform).
///   • Áp offset tay (tùy chỉnh trong Inspector).
///   • Không cần NetworkVariable bổ sung vì dữ liệu nguồn đã sync sẵn.
///
/// SETUP:
///   1. Thêm component này vào GameObject của Leo (cùng chỗ với LeoAssassin/Animator).
///   2. Nhấn Play → chỉnh slider real-time trong Inspector khi đang đánh.
///   3. Lưu giá trị vào Prefab.
/// </summary>
[DisallowMultipleComponent]
public class LeoBoneCorrector : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    //  REFERENCES
    // ─────────────────────────────────────────────────────────
    [Header("References (để trống để tự tìm)")]
    [Tooltip("Animator của Leo. Để trống để tự tìm.")]
    public Animator anim;

    // ─────────────────────────────────────────────────────────
    //  BODY LOCK — Spine / Chest
    // ─────────────────────────────────────────────────────────
    [Header("Khóa Thân Người Khi Tấn Công (Spine / Chest)")]
    [Tooltip("Bật / tắt toàn bộ tính năng khóa thân.")]
    public bool lockBodyOnAttack = true;

    [Range(0f, 1f)]
    [Tooltip("Độ mạnh khóa (0 = tắt hoàn toàn, 1 = khóa 100% theo transform.forward).")]
    public float bodyLockStrength = 0.9f;

    [Tooltip("Offset Euler bổ sung sau khi khóa.\n" +
             "X = nghiêng trước/sau, Y = xoay thêm, Z = nghiêng ngang.\n" +
             "Để (0,0,0) nếu tư thế đã thẳng.")]
    public Vector3 spineOffsetEuler = Vector3.zero;

    [Tooltip("Dùng Chest thay vì Spine làm điểm khóa (thường tốt hơn với rig Mixamo/Humanoid).")]
    public bool preferChestBone = true;

    // ─────────────────────────────────────────────────────────
    //  ARM OFFSETS
    // ─────────────────────────────────────────────────────────
    [Header("Offset Tay Phải (đấm / chém chính)")]
    public bool applyRightArmOffset = false;
    [Tooltip("Offset Euler LOCAL của UpperArm phải.\nX / Y / Z → chỉnh để thẳng tay khi đấm.")]
    public Vector3 rightUpperArmOffset = Vector3.zero;
    [Range(0f, 1f)] public float rightArmStrength = 1f;

    [Header("Offset Tay Trái (vũ khí phụ / chém đôi)")]
    public bool applyLeftArmOffset = false;
    [Tooltip("Offset Euler LOCAL của UpperArm trái.")]
    public Vector3 leftUpperArmOffset = Vector3.zero;
    [Range(0f, 1f)] public float leftArmStrength = 1f;

    // ─────────────────────────────────────────────────────────
    //  ATTACK STATE NAMES
    // ─────────────────────────────────────────────────────────
    [Header("Tên Animator States Được Coi Là Tấn Công")]
    public string[] attackStateNames = new[]
    {
        "Punch1", "Punch2",
        "Dam1",   "Dam2",
        "Slash1", "Slash2", "Slash3",
        "Slash_1","Slash_2","Slash_3",
        "Chem1",  "Chem2",  "Chem3",
        "Chem_1", "Chem_2", "Chem_3"
    };

    // ─────────────────────────────────────────────────────────
    //  DEBUG
    // ─────────────────────────────────────────────────────────
    [Header("Debug / Gizmos")]
    [Tooltip("Hiện Gizmo hướng nhìn + vị trí xương trong Scene view.")]
    public bool showDebugGizmos = true;

    [HideInInspector] public bool isInAttackState;

    // ─────────────────────────────────────────────────────────
    //  PRIVATE BONE REFS
    // ─────────────────────────────────────────────────────────
    private Transform spineT;
    private Transform chestT;
    private Transform upperChestT;
    private Transform rightUpperArmT;
    private Transform leftUpperArmT;
    private bool bonesFound;

    // NetworkBehaviour trên cùng GO (nếu có) — dùng để kiểm tra trạng thái mạng
    private NetworkBehaviour netBehaviour;

    // ─────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        netBehaviour = GetComponent<NetworkBehaviour>();
    }

    void Start()
    {
        ResolveAnimator();
        FindBones();
    }

    /// <summary>
    /// LateUpdate chạy SAU Animator.Update() — ghi đè rotation xương ở đây.
    ///
    /// NETWORK NOTE:
    ///   • Owner client   : Animator state được set trực tiếp bởi input, transform.forward
    ///                      được cập nhật bởi LeoPlayer rotation logic.
    ///   • Remote clients : Animator state được sync bởi PlayAnimationClientRpc.
    ///                      transform.forward/rotation được sync bởi NetworkTransform.
    ///   → Cả hai trường hợp đều cung cấp đủ dữ liệu cho LeoBoneCorrector chạy đúng.
    ///     Không cần NetworkVariable bổ sung.
    /// </summary>
    void LateUpdate()
    {
        if (!bonesFound) { FindBones(); return; }
        if (anim == null || !anim.isActiveAndEnabled) return;

        // Nếu đang trong trạng thái network nhưng chưa spawn → bỏ qua
        if (netBehaviour != null && !netBehaviour.IsSpawned) return;

        isInAttackState = IsCurrentlyAttacking();
        if (!isInAttackState) return;

        ApplyBodyLock();
        ApplyArmOffsets();
    }

    // ─────────────────────────────────────────────────────────
    //  CORE CORRECTION
    // ─────────────────────────────────────────────────────────

    void ApplyBodyLock()
    {
        if (!lockBodyOnAttack || bodyLockStrength <= 0f) return;

        // Hướng mong muốn: theo transform.forward của nhân vật (đã sync qua NetworkTransform)
        Quaternion desired = Quaternion.LookRotation(transform.forward, Vector3.up)
                           * Quaternion.Euler(spineOffsetEuler);

        // Chọn xương: UpperChest → Chest → Spine theo ưu tiên
        Transform target = preferChestBone
            ? (upperChestT ?? chestT ?? spineT)
            : (spineT ?? chestT ?? upperChestT);

        if (target == null) return;
        target.rotation = Quaternion.Slerp(target.rotation, desired, bodyLockStrength);
    }

    void ApplyArmOffsets()
    {
        if (applyRightArmOffset && rightUpperArmT != null && rightArmStrength > 0f)
        {
            Quaternion orig = rightUpperArmT.localRotation;
            rightUpperArmT.localRotation = Quaternion.Slerp(orig,
                orig * Quaternion.Euler(rightUpperArmOffset), rightArmStrength);
        }

        if (applyLeftArmOffset && leftUpperArmT != null && leftArmStrength > 0f)
        {
            Quaternion orig = leftUpperArmT.localRotation;
            leftUpperArmT.localRotation = Quaternion.Slerp(orig,
                orig * Quaternion.Euler(leftUpperArmOffset), leftArmStrength);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  ATTACK DETECTION
    // ─────────────────────────────────────────────────────────

    bool IsCurrentlyAttacking()
    {
        if (anim == null || attackStateNames == null) return false;

        int layers = Mathf.Min(anim.layerCount, 2);
        for (int l = 0; l < layers; l++)
        {
            var info = anim.GetCurrentAnimatorStateInfo(l);
            foreach (string s in attackStateNames)
                if (!string.IsNullOrEmpty(s) && info.IsName(s) && info.normalizedTime < 0.95f)
                    return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────
    //  BONE DISCOVERY
    // ─────────────────────────────────────────────────────────

    void ResolveAnimator()
    {
        if (anim != null) return;
        anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        if (anim == null)
            Debug.LogWarning("[LeoBoneCorrector] Không tìm được Animator! Kéo thủ công vào field 'Anim'.");
    }

    void FindBones()
    {
        ResolveAnimator();
        if (anim == null) return;

        if (anim.isHuman)
        {
            // Humanoid rig — chính xác nhất
            spineT          = anim.GetBoneTransform(HumanBodyBones.Spine);
            chestT          = anim.GetBoneTransform(HumanBodyBones.Chest);
            upperChestT     = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            rightUpperArmT  = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftUpperArmT   = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Debug.Log("[LeoBoneCorrector] Humanoid rig ✓ — xương tìm qua HumanBodyBones.");
        }
        else
        {
            // Generic rig — tìm theo tên phổ biến
            spineT         = FindBone("Spine",      "Spine1",     "spine_01");
            chestT         = FindBone("Chest",      "Spine2",     "spine_02");
            upperChestT    = FindBone("UpperChest", "Spine3",     "spine_03");
            rightUpperArmT = FindBone("RightArm",   "RightUpperArm", "mixamorig:RightArm", "UpperArm.R");
            leftUpperArmT  = FindBone("LeftArm",    "LeftUpperArm",  "mixamorig:LeftArm",  "UpperArm.L");
            Debug.Log($"[LeoBoneCorrector] Generic rig — Spine={spineT?.name}, " +
                      $"Chest={chestT?.name}, R.Arm={rightUpperArmT?.name}, L.Arm={leftUpperArmT?.name}");
        }

        bonesFound = spineT != null || chestT != null || upperChestT != null;
        if (!bonesFound)
            Debug.LogWarning("[LeoBoneCorrector] ⚠ Không tìm được xương Spine/Chest! " +
                             "Kiểm tra Avatar Type = Humanoid.");
    }

    Transform FindBone(params string[] hints)
    {
        foreach (var hint in hints)
        {
            var t = SearchRecursive(transform, hint);
            if (t != null) return t;
        }
        return null;
    }

    Transform SearchRecursive(Transform parent, string hint)
    {
        foreach (Transform child in parent)
        {
            // Bỏ qua prefix "mixamorig:" khi so sánh
            string n = child.name.Contains(":") ? child.name[(child.name.LastIndexOf(':') + 1)..] : child.name;
            if (n.Equals(hint, System.StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith(hint, System.StringComparison.OrdinalIgnoreCase))
                return child;

            var r = SearchRecursive(child, hint);
            if (r != null) return r;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        Vector3 origin = transform.position + Vector3.up * 1.3f;
        Gizmos.color = isInAttackState ? Color.red : Color.green;
        Gizmos.DrawRay(origin, transform.forward * 1.6f);
        Gizmos.DrawSphere(origin + transform.forward * 1.6f, 0.06f);

        var pivot = upperChestT ?? chestT ?? spineT;
        if (pivot != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pivot.position, 0.055f);
            Gizmos.DrawRay(pivot.position, pivot.forward * 0.45f);
        }

        if (rightUpperArmT != null) { Gizmos.color = Color.cyan;    Gizmos.DrawSphere(rightUpperArmT.position, 0.04f); }
        if (leftUpperArmT  != null) { Gizmos.color = Color.magenta; Gizmos.DrawSphere(leftUpperArmT.position,  0.04f); }
    }
}
