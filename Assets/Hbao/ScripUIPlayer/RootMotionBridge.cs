using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RootMotionBridge : MonoBehaviour
{
    private Animator anim;
    private Transform parentTransform;
    private Rigidbody parentRb;
    private Vector3 initialRootBoneLocalPos;
    private Vector3 initialTransformLocalPos;
    private Quaternion initialTransformLocalRot;
    private Transform rootBone;
    private bool hasRootBone = false;

    // Roll locking: lock Hips tại vị trí lúc BẮT ĐẦU roll (không phải Awake)
    private bool isRollLocked = false;
    private Vector3 rollLockHipsPos;  // Local XZ của Hips lúc bắt đầu roll

    private Transform FindHipsBone(Transform current)
    {
        if (current == null) return null;

        string nameLower = current.name.ToLower();
        if (nameLower.Contains("hips") || nameLower.Contains("pelvis"))
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindHipsBone(current.GetChild(i));
            if (found != null) return found;
        }

        return null;
    }

    private void Awake()
    {
        anim = GetComponent<Animator>();
        initialTransformLocalPos = transform.localPosition;
        initialTransformLocalRot = transform.localRotation;
        // Component này nằm ở Model con, parentTransform sẽ là đối tượng cha chứa SimplePlayerTest và NetworkTransform
        parentTransform = transform.parent;
        if (parentTransform == null)
        {
            Debug.LogError($"[RootMotionBridge] {gameObject.name} không có Transform cha! Không thể áp dụng Root Motion lên cha.");
            return;
        }

        parentRb = parentTransform.GetComponent<Rigidbody>();

        // Tìm xương gốc (Hips/Pelvis) bằng cách dùng GetBoneTransform của Humanoid
        if (anim != null)
        {
            rootBone = anim.GetBoneTransform(HumanBodyBones.Hips);
        }

        // Fallback đệ quy tìm xương hips/pelvis
        if (rootBone == null)
        {
            rootBone = FindHipsBone(transform);
        }

        // Fallback về con đầu tiên nếu không phải Humanoid và không tìm thấy xương hips/pelvis
        if (rootBone == null && transform.childCount > 0)
        {
            rootBone = transform.GetChild(0);
        }

        if (rootBone != null)
        {
            initialRootBoneLocalPos = rootBone.localPosition;
            hasRootBone = true;
            Debug.Log($"[RootMotionBridge] Đã tìm thấy xương gốc: {rootBone.name}, Vị trí ban đầu: {initialRootBoneLocalPos} (Parent: {parentTransform.name})");
        }
        else
        {
            Debug.LogWarning($"[RootMotionBridge] Không tìm thấy xương gốc (Hips/Pelvis) cho {gameObject.name}!");
        }
    }

    private int logCounter = 0;
    void OnAnimatorMove()
    {
        if (anim == null || parentTransform == null) return;

        // Chỉ áp dụng di chuyển từ Root Motion chuẩn của Unity khi applyRootMotion đang bật (đang lộn vòng)
        // Khi bình thường (walk/run/idle), applyRootMotion sẽ tắt để di chuyển bằng script không bị ảnh hưởng
        if (anim.applyRootMotion)
        {
            if (anim.deltaPosition.sqrMagnitude > 0.0001f)
            {
                if (logCounter++ % 10 == 0)
                {
                    Debug.Log($"[RootMotionBridge] applyRootMotion di chuyển cha bằng deltaPosition: {anim.deltaPosition}");
                }
            }

            if (parentRb != null)
            {
                parentRb.MovePosition(parentRb.position + anim.deltaPosition);
            }
            else
            {
                // Cộng thêm khoảng dịch chuyển từ Root Motion vào vị trí của cha
                parentTransform.position += anim.deltaPosition;
            }
        }
    }

    void LateUpdate()
    {
        if (anim == null) return;

        bool isRolling = false;
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("LonVong") || stateInfo.IsName("Lon Meo 2") || stateInfo.IsName("Lonmeo") || stateInfo.IsName("LonMeo2") || stateInfo.IsName("LonMeo"))
        {
            isRolling = true;
        }
        else if (anim.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(0);
            if (nextStateInfo.IsName("LonVong") || nextStateInfo.IsName("Lon Meo 2") || nextStateInfo.IsName("Lonmeo") || nextStateInfo.IsName("LonMeo2") || nextStateInfo.IsName("LonMeo"))
            {
                isRolling = true;
            }
        }

        if (isRolling || isRollLocked)
        {
            if (hasRootBone && rootBone != null)
            {
                // Lock Hips XZ về vị trí lúc BẮt ĐẦU roll — giữ model khớp capsule
                Vector3 lockPos = isRollLocked ? rollLockHipsPos : initialRootBoneLocalPos;
                Vector3 currentLocalPos = rootBone.localPosition;
                rootBone.localPosition = new Vector3(lockPos.x, currentLocalPos.y, lockPos.z);
            }
            else
            {
                // Fallback: lock transform XZ
                Vector3 lockPos = isRollLocked ? rollLockHipsPos : initialTransformLocalPos;
                Vector3 currentLocalPos = transform.localPosition;
                transform.localPosition = new Vector3(lockPos.x, currentLocalPos.y, lockPos.z);
            }
        }
        else
        {
            isRollLocked = false; // Tự động xả lock Hips khi không ở trong animation lộn, tránh vẹo xương sườn/hông
        }
    }

    /// <summary>
    /// Gọi TRƯỚC khi bắt đầu roll animation.
    /// Capture vị trí Hips hiện tại làm điểm lock — tránh lộn qua trái do lock sai tâm.
    /// </summary>
    public void BeginRoll()
    {
        if (hasRootBone && rootBone != null)
        {
            rollLockHipsPos = rootBone.localPosition;
        }
        else
        {
            rollLockHipsPos = transform.localPosition;
        }
        isRollLocked = true;
        enabled = true; // Đảm bảo bridge đang chạy để lock Hips
    }

    /// <summary>
    /// Gọi khi kết thúc roll animation.
    /// Tắt lock Hips để trả về hành vi bình thường.
    /// </summary>
    public void EndRoll()
    {
        isRollLocked = false;
    }

    /// <summary>
    /// Cộng dồn khoảng dịch chuyển hình ảnh của Model vào đối tượng Cha khi kết thúc lộn vòng,
    /// tránh hiện tượng nhân vật bị giật về vị trí cũ trong trường hợp không dùng Root Motion chuẩn.
    /// </summary>
    public void ApplyFinalOffset()
    {
        if (parentTransform == null) return;

        // Trường hợp 1: Hoạt ảnh di chuyển trực tiếp Object con chứa Animator
        Vector3 offset = transform.localPosition;
        if (offset.sqrMagnitude > 0.0001f)
        {
            Vector3 worldOffset = parentTransform.TransformDirection(offset);
            Vector3 oldPos = parentTransform.position;
            if (parentRb != null)
            {
                parentRb.position += worldOffset;
                parentTransform.position = parentRb.position;
            }
            else
            {
                parentTransform.position += worldOffset;
            }
            transform.localPosition = Vector3.zero;
            Debug.Log($"[RootMotionBridge] [Case 1] Đã bù tọa độ Model con: {offset} | Vị trí cha cũ: {oldPos} -> mới: {parentTransform.position}");
            return;
        }

        // Trường hợp 2: Hoạt ảnh di chuyển xương gốc (Hips/Pelvis)
        if (hasRootBone && rootBone != null)
        {
            Vector3 boneOffset = rootBone.localPosition;
            // Chỉ lấy độ lệch trục ngang X và Z, giữ nguyên độ cao Y mặc định
            Vector3 horizontalOffset = new Vector3(boneOffset.x - initialRootBoneLocalPos.x, 0f, boneOffset.z - initialRootBoneLocalPos.z);
            if (horizontalOffset.sqrMagnitude > 0.0001f)
            {
                Vector3 worldOffset = parentTransform.TransformDirection(horizontalOffset);
                Vector3 oldPos = parentTransform.position;
                if (parentRb != null)
                {
                    parentRb.position += worldOffset;
                    parentTransform.position = parentRb.position;
                }
                else
                {
                    parentTransform.position += worldOffset;
                }
                
                // Trả xương gốc về tọa độ ngang ban đầu, giữ nguyên Y hiện tại
                rootBone.localPosition = new Vector3(initialRootBoneLocalPos.x, boneOffset.y, initialRootBoneLocalPos.z);
                Debug.Log($"[RootMotionBridge] [Case 2] Đã bù tọa độ Xương gốc: {horizontalOffset} | Xương: {rootBone.name} | Vị trí cha cũ: {oldPos} -> mới: {parentTransform.position}");
            }
            else
            {
                Debug.Log($"[RootMotionBridge] [Case 2] Không bù tọa độ vì horizontalOffset nhỏ: {horizontalOffset}");
            }
        }
        else
        {
            Debug.LogWarning($"[RootMotionBridge] Không thể bù tọa độ vì hasRootBone={hasRootBone}, rootBone={rootBone?.name}");
        }
    }
}
