using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class RakanNPC : NetworkBehaviour
{
    [Header("Dialogue Configuration")]
    [SerializeField] private List<RakanDialogueController.DialogueLine> dialogueLines = new List<RakanDialogueController.DialogueLine>();

    [Header("Trigger Settings")]
    [Tooltip("Bán kính vùng tương tác nói chuyện")]
    [SerializeField] private float triggerRadius = 5f;

    private SphereCollider triggerCollider;
    private Rigidbody rb;
    private Animator animator;

    // Quản lý trạng thái tương tác phím G
    private bool isPlayerNearby = false;
    private SimplePlayerTest localPlayer;
    private int savedDialogueIndex = 0;

    private void Awake()
    {
        // Tự động thiết lập SphereCollider làm Trigger
        triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider == null) triggerCollider = gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = triggerRadius;

        // Tự động thiết lập Rigidbody để kích hoạt va chạm
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Tìm Animator trên bản thân hoặc con của GameObject
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Tự động thiết lập Collider vật lý (Không phải Trigger) để chặn đi xuyên qua NPC
        SetupPhysicalCollider();

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

        SetupPhysicalCollider();

        if (dialogueLines.Count == 0)
        {
            InitializeDefaultDialogue();
        }
    }

    /// <summary>
    /// Tạo một Collider vật lý phụ để chống người chơi đi xuyên qua NPC
    /// </summary>
    private void SetupPhysicalCollider()
    {
        Collider[] colliders = GetComponents<Collider>();
        bool hasPhysicalCollider = false;

        foreach (var col in colliders)
        {
            if (col != triggerCollider && !col.isTrigger)
            {
                hasPhysicalCollider = true;
                break;
            }
        }

        // Nếu chưa có Collider vật lý nào, tạo thêm một CapsuleCollider
        if (!hasPhysicalCollider)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.radius = 0.45f;
            capsule.height = 1.8f;
            Debug.Log("[RakanNPC] Tự động thêm CapsuleCollider vật lý (Is Trigger = False) để chặn người chơi đi xuyên.");
        }
    }

    private void InitializeDefaultDialogue()
    {
        dialogueLines.Add(new RakanDialogueController.DialogueLine
        {
            speakerName = "Rakan",
            text = "\"Arthur... và những kẻ ngoại tộc. Ta nghe tiếng bước chân các ngươi từ xa. Các ngươi tới đây tìm kiếm cái chết hay vinh quang?\""
        });

        dialogueLines.Add(new RakanDialogueController.DialogueLine
        {
            speakerName = "Arthur",
            text = "\"Chúng tôi tìm đường đến Cung điện Hoàng gia!\""
        });

        dialogueLines.Add(new RakanDialogueController.DialogueLine
        {
            speakerName = "Rakan",
            text = "\"Cung điện Hoàng gia? Lối đi phía trước đã bị khóa chặt rồi. Silas lẩm cẩm canh gác ngoài kia chắc cũng kể cho các ngươi về những viên ngọc rồi đúng không?\""
        });
    }

    private void Update()
    {
        if (isPlayerNearby && localPlayer != null)
        {
            // Nếu người chơi chết, hủy trạng thái tương tác
            if (localPlayer.CurrentHealth <= 0)
            {
                isPlayerNearby = false;
                localPlayer = null;
                HideDialogueAndPrompt();
                return;
            }

            bool isDialogueActive = RakanDialogueController.Instance != null && RakanDialogueController.Instance.IsActive;

            if (!isDialogueActive)
            {
                // Hiển thị gợi ý phím G độc lập
                if (RakanDialogueController.Instance != null)
                {
                    RakanDialogueController.Instance.ShowPrompt(true);
                }

                // Lắng nghe người chơi nhấn phím G để bắt đầu trò chuyện
                if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                {
                    if (RakanDialogueController.Instance != null)
                    {
                        RakanDialogueController.Instance.ShowPrompt(false);
                    }

                    // Nếu chơi offline (Standalone) thì bật cục bộ, ngược lại đồng bộ qua server
                    if (localPlayer.isStandaloneMode)
                    {
                        if (RakanDialogueController.Instance != null)
                        {
                            RakanDialogueController.Instance.StartDialogue(dialogueLines, localPlayer, this, savedDialogueIndex);
                        }
                        SetDialogueAnimation(true);
                    }
                    else
                    {
                        RequestStartDialogueServerRpc(savedDialogueIndex);
                    }
                }
            }
            else
            {
                // Khi đang trò chuyện, ẩn gợi ý tương tác phím G
                if (RakanDialogueController.Instance != null)
                {
                    RakanDialogueController.Instance.ShowPrompt(false);
                }

                // Lắng nghe người chơi nhấn phím G lần nữa để đóng trò chuyện
                if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                {
                    HideDialogueAndPrompt();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        SimplePlayerTest player = other.GetComponentInParent<SimplePlayerTest>();
        if (player == null) player = other.GetComponentInChildren<SimplePlayerTest>();
        if (player == null) player = other.GetComponent<SimplePlayerTest>();

        if (player != null)
        {
            bool isLocalPlayer = player.isStandaloneMode || player.IsOwner;

            if (isLocalPlayer)
            {
                isPlayerNearby = true;
                localPlayer = player;
                
                if (RakanDialogueController.Instance != null)
                {
                    RakanDialogueController.Instance.ShowPrompt(true);
                }
                else
                {
                    Debug.LogWarning("[RakanNPC] KHÔNG TÌM THẤY RakanDialogueController.Instance trong Scene! Bạn cần tạo một GameObject trong Hierarchy, thêm component UI Document (chọn RakanDialogue.uxml) và thêm script RakanDialogueController.");
                }
                Debug.Log($"[RakanNPC Trigger] Local Player {player.gameObject.name} đi VÀO vùng Sphere Trigger. Hiển thị gợi ý phím G.");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        SimplePlayerTest player = other.GetComponentInParent<SimplePlayerTest>();
        if (player == null) player = other.GetComponentInChildren<SimplePlayerTest>();
        if (player == null) player = other.GetComponent<SimplePlayerTest>();

        // Kiểm tra nếu chính localPlayer hiện tại đi ra ngoài
        if (player != null && player == localPlayer)
        {
            isPlayerNearby = false;
            localPlayer = null;
            HideDialogueAndPrompt();
            Debug.Log($"[RakanNPC Trigger] Local Player đi RA KHỎI vùng Sphere Trigger. Ẩn gợi ý tương tác phím G và đóng hội thoại.");
        }
    }

    private void HideDialogueAndPrompt()
    {
        if (RakanDialogueController.Instance != null)
        {
            RakanDialogueController.Instance.ShowPrompt(false);
        }

        if (RakanDialogueController.Instance != null)
        {
            // Nếu chơi standalone thì đóng cục bộ, chơi mạng thì gửi Rpc để đóng cho tất cả mọi người
            SimplePlayerTest tempPlayer = FindLocalPlayerInScene();
            if (tempPlayer != null && tempPlayer.isStandaloneMode)
            {
                RakanDialogueController.Instance.EndDialogue();
                SetDialogueAnimation(false);
            }
            else
            {
                RequestEndDialogueServerRpc();
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

    /// <summary>
    /// Lưu chỉ số hội thoại hiện tại
    /// </summary>
    public void SetSavedDialogueIndex(int index)
    {
        savedDialogueIndex = index;
    }

    private SimplePlayerTest FindLocalPlayerInScene()
    {
        SimplePlayerTest[] players = FindObjectsOfType<SimplePlayerTest>();
        foreach (var p in players)
        {
            if (p.isStandaloneMode || p.IsOwner)
            {
                return p;
            }
        }
        return null;
    }

    /// <summary>
    /// Điều khiển tham số Animation TroTruyen của Animator
    /// </summary>
    private void SetDialogueAnimation(bool isTalking)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == "TroTruyen")
                {
                    if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        animator.SetBool("TroTruyen", isTalking);
                    }
                    else if (param.type == AnimatorControllerParameterType.Trigger && isTalking)
                    {
                        animator.SetTrigger("TroTruyen");
                    }
                    break;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // ĐỒNG BỘ MẠNG CO-OP DIALOGUE (SERVER RPC & CLIENT RPC)
    // ═══════════════════════════════════════════════════════

    [ServerRpc(RequireOwnership = false)]
    public void RequestStartDialogueServerRpc(int startIndex)
    {
        StartDialogueClientRpc(startIndex);
    }

    [ClientRpc]
    private void StartDialogueClientRpc(int startIndex)
    {
        SimplePlayerTest local = FindLocalPlayerInScene();
        if (RakanDialogueController.Instance != null)
        {
            RakanDialogueController.Instance.StartDialogue(dialogueLines, local, this, startIndex);
        }
        SetDialogueAnimation(true);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestNextLineServerRpc()
    {
        NextLineClientRpc();
    }

    [ClientRpc]
    private void NextLineClientRpc()
    {
        if (RakanDialogueController.Instance != null)
        {
            RakanDialogueController.Instance.NextLine();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSelectChoiceServerRpc(int nextStepId)
    {
        SelectChoiceClientRpc(nextStepId);
    }

    [ClientRpc]
    private void SelectChoiceClientRpc(int nextStepId)
    {
        if (RakanDialogueController.Instance != null)
        {
            RakanDialogueController.Instance.SelectChoice(nextStepId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEndDialogueServerRpc()
    {
        EndDialogueClientRpc();
    }

    [ClientRpc]
    private void EndDialogueClientRpc()
    {
        if (RakanDialogueController.Instance != null)
        {
            RakanDialogueController.Instance.EndDialogue();
        }
        SetDialogueAnimation(false);
    }
}
