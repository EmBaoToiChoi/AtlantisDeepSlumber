using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RootMotionBridge : MonoBehaviour
{
    private Animator anim;
    private Transform parentTransform;
    private Rigidbody parentRb;
    private Vector3 initialRootBoneLocalPos;
    private Vector3 initialTransformLocalPos;
    private Transform rootBone;
    private bool hasRootBone = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        initialTransformLocalPos = transform.localPosition;
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

        // Fallback về con đầu tiên nếu không phải Humanoid
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
                parentRb.MoveRotation(parentRb.rotation * anim.deltaRotation);
            }
            else
            {
                // Cộng thêm khoảng dịch chuyển từ Root Motion vào vị trí của cha
                parentTransform.position += anim.deltaPosition;
                
                // Cộng thêm góc xoay từ Root Motion vào cha
                parentTransform.rotation *= anim.deltaRotation;
            }
        }
    }

    void LateUpdate()
    {
        if (anim == null) return;

        bool isRolling = false;
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("LonVong") || stateInfo.IsName("Lon Meo 2") || stateInfo.IsName("Lonmeo"))
        {
            isRolling = true;
        }
        else if (anim.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = anim.GetNextAnimatorStateInfo(0);
            if (nextStateInfo.IsName("LonVong") || nextStateInfo.IsName("Lon Meo 2") || nextStateInfo.IsName("Lonmeo"))
            {
                isRolling = true;
            }
        }

        if (isRolling)
        {
            if (hasRootBone && rootBone != null)
            {
                // Khóa tọa độ ngang local X và Z của xương gốc (Hips) về vị trí ban đầu
                // Điều này ép hoạt ảnh lộn vòng chạy tại chỗ so với đối tượng cha (Capsule Collider).
                Vector3 currentLocalPos = rootBone.localPosition;
                rootBone.localPosition = new Vector3(initialRootBoneLocalPos.x, currentLocalPos.y, initialRootBoneLocalPos.z);
            }
            else
            {
                // Khóa tọa độ ngang local X và Z của chính transform chứa Animator (dành cho Generic rig hoặc khi không tìm thấy xương hông)
                Vector3 currentLocalPos = transform.localPosition;
                transform.localPosition = new Vector3(initialTransformLocalPos.x, currentLocalPos.y, initialTransformLocalPos.z);
            }
        }
        else
        {
            // Nếu là người chơi proxy (không phải chủ sở hữu/chơi đơn trên máy này), ép model về tâm đối tượng cha
            if (parentTransform != null)
            {
                IPlayerHUDTarget player = parentTransform.GetComponent<IPlayerHUDTarget>();
                bool isLocalOrOwner = player == null || player.IsStandaloneMode || player.IsOwner;
                if (!isLocalOrOwner)
                {
                    if (hasRootBone && rootBone != null)
                    {
                        rootBone.localPosition = new Vector3(initialRootBoneLocalPos.x, rootBone.localPosition.y, initialRootBoneLocalPos.z);
                    }
                    transform.localPosition = initialTransformLocalPos;
                }
            }
        }
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
