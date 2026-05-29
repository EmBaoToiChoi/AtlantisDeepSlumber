using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SilasNPC : MonoBehaviour
{
    [Header("Dialogue Configuration")]
    [SerializeField] private List<SilasDialogueController.DialogueLine> dialogueLines = new List<SilasDialogueController.DialogueLine>();

    [Header("Trigger Settings")]
    [Tooltip("Bán kính vùng tương tác nói chuyện")]
    [SerializeField] private float triggerRadius = 5f;

    private SphereCollider triggerCollider;
    private Rigidbody rb;

    private void Awake()
    {
        // Tự động thiết lập SphereCollider
        triggerCollider = GetComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = triggerRadius;

        // Tự động thiết lập Rigidbody để đảm bảo Trigger luôn được kích hoạt
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Khởi tạo sẵn các câu thoại cốt truyện chính nếu danh sách trống
        if (dialogueLines.Count == 0)
        {
            InitializeDefaultDialogue();
        }
    }

    private void Reset()
    {
        triggerRadius = 5f;
        triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.radius = triggerRadius;
        }

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (dialogueLines.Count == 0)
        {
            InitializeDefaultDialogue();
        }
    }

    private void InitializeDefaultDialogue()
    {
        dialogueLines.Add(new SilasDialogueController.DialogueLine
        {
            speakerName = "Silas",
            text = "(Không ngẩng đầu lên, giọng khàn đặc) \"Rakan vừa gửi mấy 'con cừu' tới đây sao? Ta đã đứng cạnh chỗ này hàng thế kỷ. Nếu muốn vào sâu hơn, đừng có nhìn ta như thế. Ta không phải quái vật, ít nhất là chưa phải.\""
        });

        dialogueLines.Add(new SilasDialogueController.DialogueLine
        {
            speakerName = "Arthur (Cảnh giác, giơ Khiên)",
            text = "\"Ông là ai? Tại sao ông lại canh giữ nơi này? Chúng tôi cần vào Cung điện Hoàng gia.\""
        });

        dialogueLines.Add(new SilasDialogueController.DialogueLine
        {
            speakerName = "Silas",
            text = "\"Cung điện? Lũ ngoại lai các ngươi thật ngây thơ. Muốn mở được cánh cửa vào Phòng Ngai Vàng, các người cần 3 Viên ngọc của các khu vực để mở khóa cánh cửa cuối. Chúng là chìa khóa duy nhất để tháo mở cánh cửa của phòng Ngai Vàng.\""
        });
    }

    private void OnTriggerEnter(Collider other)
    {
        // Tìm component SimplePlayerTest trên đối tượng va chạm
        SimplePlayerTest player = other.GetComponentInParent<SimplePlayerTest>();
        if (player != null)
        {
            // Chỉ hiển thị UI cho người chơi cục bộ (Local Player) của client này
            // (Hỗ trợ cả chế độ Standalone lẫn Netcode multiplayer)
            bool isLocalPlayer = player.isStandaloneMode || player.IsOwner;

            if (isLocalPlayer)
            {
                Debug.Log($"[SilasNPC] Local Player {player.gameObject.name} lại gần. Bắt đầu đối thoại.");
                if (SilasDialogueController.Instance != null)
                {
                    SilasDialogueController.Instance.StartDialogue(dialogueLines, player);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        SimplePlayerTest player = other.GetComponentInParent<SimplePlayerTest>();
        if (player != null)
        {
            bool isLocalPlayer = player.isStandaloneMode || player.IsOwner;

            if (isLocalPlayer)
            {
                Debug.Log($"[SilasNPC] Local Player {player.gameObject.name} đi ra khỏi vùng. Kết thúc đối thoại.");
                if (SilasDialogueController.Instance != null)
                {
                    SilasDialogueController.Instance.EndDialogue();
                }
            }
        }
    }

    // Cập nhật bán kính trigger nếu thay đổi trong editor
    private void OnValidate()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.radius = triggerRadius;
        }
    }
}
