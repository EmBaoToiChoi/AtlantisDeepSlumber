using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class SilasNPC : NetworkBehaviour
{
    [Header("Dialogue Configuration")]
    [SerializeField] private List<SilasDialogueController.DialogueLine> dialogueLines = new List<SilasDialogueController.DialogueLine>();

    [Header("Trigger Settings")]
    [Tooltip("Bán kính vùng tương tác nói chuyện")]
    [SerializeField] private float triggerRadius = 5f;

    private SphereCollider triggerCollider;
    private Rigidbody rb;

    // Quản lý trạng thái tương tác phím G
    private bool isPlayerNearby = false;
    private SimplePlayerTest localPlayer;
    private int savedDialogueIndex = 0;

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

    private void Update()
    {
        if (isPlayerNearby && localPlayer != null)
        {
            // Nếu người chơi chết, hủy trạng thái tương tác
            if (localPlayer.CurrentHealth <= 0)
            {
                isPlayerNearby = false;
                localPlayer = null;
                
                if (SilasDialogueController.Instance != null)
                {
                    SilasDialogueController.Instance.ShowPrompt(false);
                    SilasDialogueController.Instance.EndDialogue();
                }
                return;
            }

            bool isDialogueActive = SilasDialogueController.Instance != null && SilasDialogueController.Instance.IsActive;

            if (!isDialogueActive)
            {
                // Hiển thị gợi ý phím G độc lập thông qua Dialogue UI (Tránh lỗi mất HUD)
                if (SilasDialogueController.Instance != null)
                {
                    SilasDialogueController.Instance.ShowPrompt(true);
                }

                // Lắng nghe người chơi nhấn phím G để bắt đầu trò chuyện
                if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                {
                    if (SilasDialogueController.Instance != null)
                    {
                        SilasDialogueController.Instance.ShowPrompt(false);
                    }

                    // Nếu chơi offline (Standalone) thì bật cục bộ, ngược lại đồng bộ qua server
                    if (localPlayer.isStandaloneMode)
                    {
                        if (SilasDialogueController.Instance != null)
                        {
                            SilasDialogueController.Instance.StartDialogue(dialogueLines, localPlayer, this, savedDialogueIndex);
                        }
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
                if (SilasDialogueController.Instance != null)
                {
                    SilasDialogueController.Instance.ShowPrompt(false);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        SimplePlayerTest player = other.GetComponentInParent<SimplePlayerTest>();
        if (player != null)
        {
            bool isLocalPlayer = player.isStandaloneMode || player.IsOwner;

            if (isLocalPlayer)
            {
                isPlayerNearby = true;
                localPlayer = player;
                
                if (SilasDialogueController.Instance != null)
                {
                    SilasDialogueController.Instance.ShowPrompt(true);
                }
                Debug.Log($"[SilasNPC] Local Player {player.gameObject.name} lại gần. Hiển thị gợi ý phím G.");
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
                isPlayerNearby = false;
                localPlayer = null;
                
                if (SilasDialogueController.Instance != null)
                {
                    SilasDialogueController.Instance.ShowPrompt(false);
                    SilasDialogueController.Instance.EndDialogue();
                }
                Debug.Log($"[SilasNPC] Local Player {player.gameObject.name} đi ra xa. Ẩn gợi ý tương tác phím G.");
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
        if (SilasDialogueController.Instance != null)
        {
            SilasDialogueController.Instance.StartDialogue(dialogueLines, local, this, startIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestNextLineServerRpc()
    {
        NextLineClientRpc();
    }

    [ClientRpc]
    private void NextLineClientRpc()
    {
        if (SilasDialogueController.Instance != null)
        {
            SilasDialogueController.Instance.NextLine();
        }
    }
}
