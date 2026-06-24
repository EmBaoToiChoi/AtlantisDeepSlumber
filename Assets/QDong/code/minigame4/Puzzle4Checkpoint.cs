using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Gắn vào một GameObject làm điểm Checkpoint trong minigame 4.
/// Khi player chạm vào, checkpoint sẽ được kích hoạt và lưu làm điểm hồi sinh.
/// </summary>
public class Puzzle4Checkpoint : NetworkBehaviour
{
    [Tooltip("Hiệu ứng khi checkpoint được kích hoạt (không bắt buộc)")]
    public GameObject activateEffect;

    /// <summary>
    /// Trả về true nếu checkpoint này đã được người chơi kích hoạt.
    /// </summary>
    public bool IsActivated { get; private set; } = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (IsActivated) return;
        if (!other.CompareTag("Player")) return;

        IsActivated = true;
        ActivateCheckpointClientRpc();
        Debug.Log($"[Puzzle4Checkpoint] Checkpoint '{gameObject.name}' đã được kích hoạt!");
    }

    [ClientRpc]
    private void ActivateCheckpointClientRpc()
    {
        IsActivated = true;

        if (activateEffect != null)
        {
            activateEffect.SetActive(true);
        }

        Debug.Log($"[Puzzle4Checkpoint] Checkpoint '{gameObject.name}' đã kích hoạt trên Client.");
    }
}
