using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Gắn vào bất kỳ GameObject nào — KHÔNG cần Box Collider riêng.
///
/// Script tự lắng nghe Puzzle4Manager để ẩn/hiện cầu thang đá:
///   - isMinigameStarted == true  → cầu thang ẨN (minigame đang chạy)
///   - puzzleCompleted  == true   → cầu thang HIỆN lại
///
/// Không cần trigger box riêng — dùng chung sự kiện từ Puzzle4TrapTrigger
/// thông qua Puzzle4Manager.isMinigameStarted.
/// </summary>
public class BoxTrapObject : NetworkBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Kéo Puzzle4Manager vào đây. Để trống sẽ tự tìm trong scene.")]
    public Puzzle4Manager puzzle4Manager;

    [Header("Phần cầu thang đá")]
    [Tooltip("Kéo tất cả GameObject cầu thang đá vào đây (có thể nhiều phần).")]
    public GameObject[] stairParts;

    // ─────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Start()
    {
        if (puzzle4Manager == null)
            puzzle4Manager = FindAnyObjectByType<Puzzle4Manager>();

        if (puzzle4Manager == null)
            Debug.LogWarning("[BoxTrapObject] Không tìm thấy Puzzle4Manager trong scene!");
    }

    // ─────────────────────────────────────────────
    // Network Lifecycle
    // ─────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (puzzle4Manager == null) return;

        // Áp dụng trạng thái đúng khi client vào sau (reconnect / late join)
        if (puzzle4Manager.puzzleCompleted.Value)
        {
            SetStairsVisible(true);
        }
        else if (puzzle4Manager.isMinigameStarted.Value)
        {
            SetStairsVisible(false);
        }

        // Lắng nghe khi minigame bắt đầu → ẩn cầu thang
        puzzle4Manager.isMinigameStarted.OnValueChanged += OnMinigameStartedChanged;

        // Lắng nghe khi puzzle hoàn thành → hiện cầu thang
        puzzle4Manager.puzzleCompleted.OnValueChanged += OnPuzzleCompletedChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (puzzle4Manager == null) return;

        puzzle4Manager.isMinigameStarted.OnValueChanged -= OnMinigameStartedChanged;
        puzzle4Manager.puzzleCompleted.OnValueChanged   -= OnPuzzleCompletedChanged;
    }

    // ─────────────────────────────────────────────
    // Callbacks
    // ─────────────────────────────────────────────

    private void OnMinigameStartedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            // Minigame bắt đầu → ẩn cầu thang
            SetStairsVisible(false);
            Debug.Log("[BoxTrapObject] Minigame bắt đầu → cầu thang đá đã ẩn.");
        }
    }

    private void OnPuzzleCompletedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            // Puzzle hoàn thành → hiện cầu thang
            SetStairsVisible(true);
            Debug.Log("[BoxTrapObject] Puzzle hoàn thành → cầu thang đá hiện lại.");
        }
    }

    // ─────────────────────────────────────────────
    // ClientRpc
    // ─────────────────────────────────────────────



    // ─────────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────────

    private void SetStairsVisible(bool visible)
    {
        foreach (var part in stairParts)
        {
            if (part != null)
                part.SetActive(visible);
        }
    }
}
