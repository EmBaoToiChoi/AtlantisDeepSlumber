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
    [Header("References (để trống để tự tìm)")]
    [Tooltip("Animator của Leo. Để trống để tự tìm.")]
    public Animator anim;

    [Header("Chế độ Test (Căn chỉnh trực quan)")]
    [Tooltip("Bật cái này để LUÔN LUÔN áp dụng offset (kể cả khi đứng im không đánh) giúp bạn dễ dàng kéo slider căn chỉnh các góc xoay tay/thân trong Unity Editor. Tắt đi khi chơi thật.")]
    public bool testMode = false;

    [Tooltip("Tên State muốn preview khi bật Test Mode. Để trống để preview thông số chung ở trên, hoặc gõ tên state (ví dụ: Combo1kiem) để preview thông số cấu hình riêng của state đó.")]
    public string testPreviewState = "";

    [Header("Tốc Độ Chuyển Tiếp Mượt Mà (Smoothing)")]
    [Range(1f, 30f)]
    [Tooltip("Tốc độ chuyển tiếp giữa các tư thế bẻ xương (càng cao càng nhanh, mặc định là 15). Giúp tránh giật giật khi bắt đầu hoặc kết thúc đòn đánh.")]
    public float blendSpeed = 15f;

    [Header("Gán Xương Thủ Công (Nếu tự tìm sai/không hoạt động)")]
    [Tooltip("Kéo xương Spine từ Hierarchy vào đây nếu script không tự tìm đúng.")]
    public Transform manualSpine;
    [Tooltip("Kéo xương Chest từ Hierarchy vào đây.")]
    public Transform manualChest;
    [Tooltip("Kéo xương UpperChest từ Hierarchy vào đây.")]
    public Transform manualUpperChest;
    [Tooltip("Kéo xương UpperArm phải từ Hierarchy vào đây.")]
    public Transform manualRightUpperArm;
    [Tooltip("Kéo xương UpperArm trái từ Hierarchy vào đây.")]
    public Transform manualLeftUpperArm;

    // ─────────────────────────────────────────────────────────
    //  BODY LOCK — Spine / Chest
    // ─────────────────────────────────────────────────────────
    [Header("Khóa Thân Người Khi Tấn Công (Spine / Chest)")]
    [Tooltip("Bật / tắt toàn bộ tính năng khóa thân.")]
    public bool lockBodyOnAttack = true;

    [Tooltip("Nếu true: khóa thân LUÔN LUÔN (cả khi đứng + di chuyển).\nNếu false: chỉ khóa khi đang trong attack state.")]
    public bool lockBodyAlways = true;

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

    [Header("Cấu Hình Riêng Cho Từng Skill (Tùy Chọn)")]
    [Tooltip("Nếu Animator đang chơi State nào trong danh sách này, script sẽ áp dụng thông số riêng của State đó thay vì thông số chung ở trên.")]
    public System.Collections.Generic.List<SkillBoneOffset> skillOverrides = new System.Collections.Generic.List<SkillBoneOffset>();

    // ─────────────────────────────────────────────────────────
    //  ATTACK STATE NAMES
    // ─────────────────────────────────────────────────────────
    [Header("Tên Animator States Được Coi Là Tấn Công")]
    [Tooltip("Các tên State trong Animator Controller của Leo — phải khớp chính xác.\n" +
             "Mở Window > Animator khi chạy Play để xem tên state đang active.")]
    public string[] attackStateNames = new[]
    {
        // ─── Combo tay không ───
        "combodam",        // Combo đấm
        "DamPhai",         // Đấm phải
        "DamTrai",         // Đấm trái
        "combodua",        // Combo đưa
        "Attackdoucombo",  // Attack đôi combo

        // ─── Combo kiếm mới 5 bước ───
        "attacktaytrai",
        "attacktayphai",
        "Slash1Combo2",
        "Slash2combo2",
        "Slash3combo2",

        // ─── Combo kiếm cũ ───
        "Combo1kiem",      // Combo kiếm 1
        "catkiemtayphai",  // Cắt kiếm tay phải
        "catkiemtaytrai",  // Cắt kiếm tay trái

        // ─── Đặc biệt ───
        "Lonmeo",          // Lộn mèo / dodge attack

        // ─── Fallback (thêm thủ công nếu phát hiện thêm) ───
        "Attack"
    };

    // ─────────────────────────────────────────────────────────
    //  DEBUG & STATE INFO
    // ─────────────────────────────────────────────────────────
    [Header("Debug / Gizmos")]
    [Tooltip("Hiện Gizmo hướng nhìn + vị trí xương trong Scene view.")]
    public bool showDebugGizmos = true;

    [Header("Trạng Thái Hiện Tại (Chỉ Xem)")]
    [Tooltip("Trạng thái tấn công hiện tại có đang được phát hiện bởi Animator hay không.\n" +
             "Dùng để test: Hãy đánh và xem ô này có tự động tích chọn hay không.")]
    public bool isInAttackState;

    // ─────────────────────────────────────────────────────────
    //  PRIVATE BONE REFS & SMOOTHING VARIABLES
    // ─────────────────────────────────────────────────────────
    private Transform spineT;
    private Transform chestT;
    private Transform upperChestT;
    private Transform rightUpperArmT;
    private Transform leftUpperArmT;
    private bool bonesFound;

    private int[] attackStateHashes;

    // Giá trị lưu trữ nội suy mượt mà để chống giật
    private float currentBodyStrength;
    private Vector3 currentSpineOffset;
    private float currentRightStrength;
    private Vector3 currentRightOffset;
    private float currentLeftStrength;
    private Vector3 currentLeftOffset;

    // ─────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        InitializeHashes();
    }

    void Start()
    {
        ResolveAnimator();
        FindBones();
    }

    void OnValidate()
    {
        InitializeHashes();
    }

    void InitializeHashes()
    {
        // 1. Hashes cho danh sách tên state tấn công chung
        if (attackStateNames != null)
        {
            attackStateHashes = new int[attackStateNames.Length];
            for (int i = 0; i < attackStateNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(attackStateNames[i]))
                    attackStateHashes[i] = Animator.StringToHash(attackStateNames[i]);
            }
        }

        // 2. Hashes cho cấu hình riêng từng skill
        if (skillOverrides != null)
        {
            foreach (var cfg in skillOverrides)
            {
                if (cfg != null && !string.IsNullOrEmpty(cfg.stateName))
                {
                    cfg.stateHash = Animator.StringToHash(cfg.stateName);
                }
            }
        }
    }

    /// <summary>
    /// LateUpdate chạy SAU Animator.Update() — ghi đè rotation xương ở đây.
    /// </summary>
    void LateUpdate()
    {
        if (!bonesFound) { FindBones(); return; }
        if (anim == null || !anim.isActiveAndEnabled) return;

        bool bodyLockEnabled = false;
        float targetBodyStrength = 0f;
        Vector3 targetSpineOffset = Vector3.zero;

        bool applyR = false;
        Vector3 targetRightOffset = Vector3.zero;
        float targetRightStrength = 0f;

        bool applyL = false;
        Vector3 targetLeftOffset = Vector3.zero;
        float targetLeftStrength = 0f;

        SkillBoneOffset activeOverride = null;

        if (testMode)
        {
            isInAttackState = true;
            activeOverride = GetPreviewOverride();

            // Chế độ Test: lấy trực tiếp thông số target làm thông số chạy để không bị trễ khi kéo slider
            bodyLockEnabled = (activeOverride != null && activeOverride.overrideBodyLock) ? activeOverride.lockBodyOnAttack : lockBodyOnAttack;
            currentBodyStrength = (activeOverride != null && activeOverride.overrideBodyLock) ? activeOverride.bodyLockStrength : bodyLockStrength;
            currentSpineOffset = (activeOverride != null && activeOverride.overrideBodyLock) ? activeOverride.spineOffsetEuler : spineOffsetEuler;

            applyR = (activeOverride != null) ? activeOverride.applyRightArmOffset : applyRightArmOffset;
            currentRightOffset = (activeOverride != null) ? activeOverride.rightUpperArmOffset : rightUpperArmOffset;
            currentRightStrength = applyR ? ((activeOverride != null) ? activeOverride.rightArmStrength : rightArmStrength) : 0f;

            applyL = (activeOverride != null) ? activeOverride.applyLeftArmOffset : applyLeftArmOffset;
            currentLeftOffset = (activeOverride != null) ? activeOverride.leftUpperArmOffset : leftUpperArmOffset;
            currentLeftStrength = applyL ? ((activeOverride != null) ? activeOverride.leftArmStrength : leftArmStrength) : 0f;
        }
        else
        {
            int currentAttackHash = GetCurrentAttackStateHash(out bool isAttacking);
            isInAttackState = isAttacking;
            activeOverride = isAttacking ? GetActiveOverride(currentAttackHash) : null;

            // 1. Tính toán target cho Khóa thân
            bool targetLockBodyOnAttack = (activeOverride != null && activeOverride.overrideBodyLock) ? activeOverride.lockBodyOnAttack : lockBodyOnAttack;
            bodyLockEnabled = targetLockBodyOnAttack;

            if (lockBodyAlways || (isInAttackState && targetLockBodyOnAttack))
            {
                targetBodyStrength = (activeOverride != null && activeOverride.overrideBodyLock) ? activeOverride.bodyLockStrength : bodyLockStrength;
                targetSpineOffset = (activeOverride != null && activeOverride.overrideBodyLock) ? activeOverride.spineOffsetEuler : spineOffsetEuler;
            }
            else
            {
                targetBodyStrength = 0f;
                targetSpineOffset = Vector3.zero;
            }

            // 2. Tính toán target cho Tay phải
            applyR = (activeOverride != null) ? activeOverride.applyRightArmOffset : applyRightArmOffset;
            if (isInAttackState && applyR)
            {
                targetRightOffset = (activeOverride != null) ? activeOverride.rightUpperArmOffset : rightUpperArmOffset;
                targetRightStrength = (activeOverride != null) ? activeOverride.rightArmStrength : rightArmStrength;
            }
            else
            {
                targetRightOffset = Vector3.zero;
                targetRightStrength = 0f;
            }

            // 3. Tính toán target cho Tay trái
            applyL = (activeOverride != null) ? activeOverride.applyLeftArmOffset : applyLeftArmOffset;
            if (isInAttackState && applyL)
            {
                targetLeftOffset = (activeOverride != null) ? activeOverride.leftUpperArmOffset : leftUpperArmOffset;
                targetLeftStrength = (activeOverride != null) ? activeOverride.leftArmStrength : leftArmStrength;
            }
            else
            {
                targetLeftOffset = Vector3.zero;
                targetLeftStrength = 0f;
            }

            // Nội suy mượt mà các giá trị qua các khung hình (Damping)
            float t = Time.deltaTime * blendSpeed;
            currentBodyStrength = Mathf.Lerp(currentBodyStrength, targetBodyStrength, t);
            currentSpineOffset = Vector3.Lerp(currentSpineOffset, targetSpineOffset, t);

            currentRightOffset = Vector3.Lerp(currentRightOffset, targetRightOffset, t);
            currentRightStrength = Mathf.Lerp(currentRightStrength, targetRightStrength, t);

            currentLeftOffset = Vector3.Lerp(currentLeftOffset, targetLeftOffset, t);
            currentLeftStrength = Mathf.Lerp(currentLeftStrength, targetLeftStrength, t);
        }

        // Áp dụng bẻ xương
        if (bodyLockEnabled && currentBodyStrength > 0f)
        {
            ApplyBodyLockSmooth(currentBodyStrength, currentSpineOffset);
        }

        ApplyArmOffsetsSmooth(currentRightStrength, currentRightOffset, currentLeftStrength, currentLeftOffset);
    }

    // ─────────────────────────────────────────────────────────
    //  CORE CORRECTION
    // ─────────────────────────────────────────────────────────

    void ApplyBodyLockSmooth(float strength, Vector3 offsetEuler)
    {
        Transform target = preferChestBone
            ? (upperChestT ?? chestT ?? spineT)
            : (spineT ?? chestT ?? upperChestT);

        if (target == null) return;

        Quaternion relativeRot = Quaternion.Inverse(transform.rotation) * target.rotation;
        Vector3 euler = relativeRot.eulerAngles;

        float pitch = Mathf.DeltaAngle(0, euler.x);
        float yaw = Mathf.DeltaAngle(0, euler.y);
        float roll = Mathf.DeltaAngle(0, euler.z);

        float targetPitch = pitch + offsetEuler.x;
        float targetYaw = offsetEuler.y;
        float targetRoll = roll + offsetEuler.z;

        Quaternion desiredRelative = Quaternion.Euler(targetPitch, targetYaw, targetRoll);
        Quaternion desiredWorld = transform.rotation * desiredRelative;

        target.rotation = Quaternion.Slerp(target.rotation, desiredWorld, strength);
    }

    void ApplyArmOffsetsSmooth(float rStrength, Vector3 rOffset, float lStrength, Vector3 lOffset)
    {
        if (rightUpperArmT != null && rStrength > 0f)
        {
            Quaternion orig = rightUpperArmT.localRotation;
            rightUpperArmT.localRotation = Quaternion.Slerp(orig,
                orig * Quaternion.Euler(rOffset), rStrength);
        }

        if (leftUpperArmT != null && lStrength > 0f)
        {
            Quaternion orig = leftUpperArmT.localRotation;
            leftUpperArmT.localRotation = Quaternion.Slerp(orig,
                orig * Quaternion.Euler(lOffset), lStrength);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  ATTACK DETECTION & CONFIG FINDERS
    // ─────────────────────────────────────────────────────────

    int GetCurrentAttackStateHash(out bool isAttacking)
    {
        isAttacking = false;
        if (anim == null) return 0;

        int layers = Mathf.Min(anim.layerCount, 2);
        for (int l = 0; l < layers; l++)
        {
            var info = anim.GetCurrentAnimatorStateInfo(l);
            if (info.normalizedTime >= 0.95f) continue;

            // 1. Ưu tiên kiểm tra trong cấu hình riêng trước
            if (skillOverrides != null)
            {
                foreach (var cfg in skillOverrides)
                {
                    if (cfg != null && cfg.stateHash != 0 && info.shortNameHash == cfg.stateHash)
                    {
                        isAttacking = true;
                        return cfg.stateHash;
                    }
                }
            }

            // 2. Kiểm tra trong danh sách attackStateNames chung
            if (attackStateHashes != null)
            {
                foreach (int hash in attackStateHashes)
                {
                    if (hash != 0 && info.shortNameHash == hash)
                    {
                        isAttacking = true;
                        return hash;
                    }
                }
            }
        }
        return 0;
    }

    SkillBoneOffset GetActiveOverride(int currentHash)
    {
        if (skillOverrides == null || currentHash == 0) return null;
        foreach (var cfg in skillOverrides)
        {
            if (cfg != null && cfg.stateHash == currentHash)
                return cfg;
        }
        return null;
    }

    SkillBoneOffset GetPreviewOverride()
    {
        if (string.IsNullOrEmpty(testPreviewState) || skillOverrides == null) return null;
        int previewHash = Animator.StringToHash(testPreviewState);
        foreach (var cfg in skillOverrides)
        {
            if (cfg != null && cfg.stateHash == previewHash)
                return cfg;
        }
        return null;
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

        // 1. Ưu tiên xương gán thủ công trước
        spineT          = manualSpine;
        chestT          = manualChest;
        upperChestT     = manualUpperChest;
        rightUpperArmT  = manualRightUpperArm;
        leftUpperArmT   = manualLeftUpperArm;

        if (anim != null)
        {
            if (anim.isHuman)
            {
                // Humanoid rig — tự động tìm nếu chưa gán thủ công
                if (spineT == null)          spineT          = anim.GetBoneTransform(HumanBodyBones.Spine);
                if (chestT == null)          chestT          = anim.GetBoneTransform(HumanBodyBones.Chest);
                if (upperChestT == null)     upperChestT     = anim.GetBoneTransform(HumanBodyBones.UpperChest);
                if (rightUpperArmT == null)  rightUpperArmT  = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (leftUpperArmT == null)   leftUpperArmT   = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Debug.Log("[LeoBoneCorrector] Humanoid rig ✓ — xương tìm qua HumanBodyBones hoặc kéo thả thủ công.");
            }
            else
            {
                // Generic rig — tự động tìm nếu chưa gán thủ công
                if (spineT == null)          spineT         = FindBone("Spine",      "Spine1",     "spine_01");
                if (chestT == null)          chestT         = FindBone("Chest",      "Spine2",     "spine_02");
                if (upperChestT == null)     upperChestT    = FindBone("UpperChest", "Spine3",     "spine_03");
                if (rightUpperArmT == null)  rightUpperArmT = FindBone("RightArm",   "RightUpperArm", "mixamorig:RightArm", "UpperArm.R");
                if (leftUpperArmT == null)   leftUpperArmT  = FindBone("LeftArm",    "LeftUpperArm",  "mixamorig:LeftArm",  "UpperArm.L");
                Debug.Log($"[LeoBoneCorrector] Generic rig — Spine={spineT?.name}, " +
                          $"Chest={chestT?.name}, R.Arm={rightUpperArmT?.name}, L.Arm={leftUpperArmT?.name}");
            }
        }

        // Đánh dấu là đã tìm thấy nếu ít nhất 1 xương quan trọng được gán hoặc tìm thấy
        bonesFound = spineT != null || chestT != null || upperChestT != null || rightUpperArmT != null || leftUpperArmT != null;
        
        if (!bonesFound)
            Debug.LogWarning("[LeoBoneCorrector] ⚠ Không tìm được bất kỳ xương nào! " +
                             "Hãy kéo thả xương từ Hierarchy vào các ô 'Gán Xương Thủ Công'.");
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

[System.Serializable]
public class SkillBoneOffset
{
    [Tooltip("Tên Animator State của skill này (ví dụ: Combo1kiem, DamPhai,...)")]
    public string stateName;

    [Header("Khóa Thân Người")]
    [Tooltip("Tích chọn để tự định nghĩa lại thông số khóa thân cho riêng State này (nếu không tích, sẽ dùng thông số mặc định ở trên).")]
    public bool overrideBodyLock = false;
    [Tooltip("Bật / tắt khóa thân cho State này.")]
    public bool lockBodyOnAttack = true;
    [Range(0f, 1f)]
    [Tooltip("Độ mạnh khóa thân.")]
    public float bodyLockStrength = 0.9f;
    [Tooltip("Offset góc xoay thân (X, Y, Z).")]
    public Vector3 spineOffsetEuler = Vector3.zero;

    [Header("Offset Tay Phải")]
    [Tooltip("Tích chọn để áp dụng xoay tay phải khi chơi State này.")]
    public bool applyRightArmOffset = false;
    [Tooltip("Offset góc xoay tay phải (X, Y, Z).")]
    public Vector3 rightUpperArmOffset = Vector3.zero;
    [Range(0f, 1f)]
    [Tooltip("Độ mạnh tác động lên tay phải.")]
    public float rightArmStrength = 1f;

    [Header("Offset Tay Trái")]
    [Tooltip("Tích chọn để áp dụng xoay tay trái khi chơi State này.")]
    public bool applyLeftArmOffset = false;
    [Tooltip("Offset góc xoay tay trái (X, Y, Z).")]
    public Vector3 leftUpperArmOffset = Vector3.zero;
    [Range(0f, 1f)]
    [Tooltip("Độ mạnh tác động lên tay trái.")]
    public float leftArmStrength = 1f;

    [System.NonSerialized]
    [HideInInspector]
    public int stateHash; // Cache hash để check runtime cực nhanh
}
