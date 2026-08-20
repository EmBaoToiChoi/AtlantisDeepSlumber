using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Vùng kích hoạt Boss (Boss Trigger Zone).
/// Tàng hình Boss từ đầu game -> Đợi đủ người vào vùng -> Chạy Cutscene -> Bật hiện Boss và kích hoạt.
/// </summary>
public class BossTriggerZone : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kéo thả đối tượng Boss AI vào đây (nếu để trống, sẽ tự tìm kiếm BossAI trong scene)")]
    public BossAI boss;

    [Tooltip("Kéo đối tượng chứa script VideoCutsceneController vào đây")]
    public VideoCutsceneController bossIntroCutscene; 

    [Header("Settings")]
    [Tooltip("Hộp va chạm kích hoạt (nếu để trống, sẽ tự động lấy Collider trên đối tượng này)")]
    public Collider triggerCollider;
    
    [Tooltip("Chỉ cho phép kích hoạt một lần duy nhất")]
    public bool triggerOnlyOnce = true;
    private bool hasTriggered = false;

    // Lưu ID của những người chơi đang đứng trong vùng Trigger
    private HashSet<ulong> playersInZone = new HashSet<ulong>();

    private void Awake()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;
        else Debug.LogWarning($"[BossTriggerZone] {gameObject.name} chưa có Collider!");
    }

    private void Start()
    {
        if (boss == null)
        {
            boss = FindFirstObjectByType<BossAI>(FindObjectsInactive.Include);
        }

        // [SỬA LẠI] Dùng hàm tàng hình an toàn cho Netcode thay vì SetActive(false)
        if (boss != null)
        {
            SetBossVisibility(false);
            Debug.Log($"[BossTriggerZone] Đã làm tàng hình Boss '{boss.name}' ở đầu game (Netcode Safe).");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player") || IsPlayerObject(other.gameObject))
        {
            bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            bool isServer = !isNetworkActive || NetworkManager.Singleton.IsServer;

            if (isServer)
            {
                if (isNetworkActive)
                {
                    var netObj = other.GetComponentInParent<NetworkObject>();
                    if (netObj != null && netObj.IsPlayerObject)
                    {
                        playersInZone.Add(netObj.OwnerClientId); 
                        CheckCutsceneCondition(); 
                    }
                }
                else
                {
                    // Chơi offline
                    CheckCutsceneCondition(true); 
                }
            }
        }
    }

    private void CheckCutsceneCondition(bool forceTriggerOffline = false)
    {
        if (hasTriggered) return;

        bool canTrigger = forceTriggerOffline;

        if (!canTrigger && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            int totalPlayersInRoom = NetworkManager.Singleton.ConnectedClientsList.Count;
            int requiredPlayers = Mathf.Max(1, totalPlayersInRoom - 1); 

            playersInZone.RemoveWhere(id => !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));

            Debug.Log($"[BossTriggerZone] Số người đã check-in: {playersInZone.Count} / Cần thiết: {requiredPlayers} (Tổng user: {totalPlayersInRoom})");

            if (playersInZone.Count >= requiredPlayers)
            {
                canTrigger = true;
            }
        }

        if (canTrigger)
        {
            hasTriggered = true;
            playersInZone.Clear();
            Debug.Log("[BossTriggerZone] ĐÃ ĐỦ NGƯỜI! Tiến hành xử lý Cutscene/Boss...");

            if (triggerOnlyOnce && triggerCollider != null) triggerCollider.enabled = false;

            if (bossIntroCutscene != null)
            {
                StartCoroutine(WaitCutsceneAndActivateBoss());
            }
            else if (boss != null) 
            {
                // [SỬA LẠI] Bật hiện hình Boss lên trước khi gọi hàm ActivateBoss
                SetBossVisibility(true);
                boss.ActivateBoss();
                if (triggerOnlyOnce) gameObject.SetActive(false); 
            }
        }
    }

    private IEnumerator WaitCutsceneAndActivateBoss()
    {
        bossIntroCutscene.StartCutscene();
        yield return null;
        yield return new WaitUntil(() => !bossIntroCutscene.isPlaying);

        if (boss != null)
        {
            // [SỬA LẠI] Lúc này video đã xong, ta cho con Boss hiện hình ra!
            SetBossVisibility(true);
            
            boss.ActivateBoss();
            Debug.Log($"[BossTriggerZone] Video xong! Hiện hình và kích hoạt Boss '{boss.name}'.");
        }

        if (triggerOnlyOnce) gameObject.SetActive(false);
    }

    // =======================================================
    // [THÊM MỚI] HÀM BẬT/TẮT TÀNG HÌNH CHO BOSS (AN TOÀN CHO NETCODE)
    // =======================================================
    private void SetBossVisibility(bool isVisible)
    {
        if (boss == null) return;

        // 1. Bật/tắt tất cả hình ảnh (lớp vỏ ngoài của boss)
        Renderer[] renderers = boss.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = isVisible;
        }

        // 2. Bật/tắt tất cả hộp va chạm vật lý
        Collider[] colliders = boss.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            c.enabled = isVisible;
        }

        // 3. Tùy chọn: Bật/tắt Canvas (nếu boss có vác theo thanh máu UI trên đầu)
        Canvas[] canvases = boss.GetComponentsInChildren<Canvas>(true);
        foreach (var canvas in canvases)
        {
            canvas.enabled = isVisible;
        }
    }

    private bool IsPlayerObject(GameObject go)
    {
        if (go.CompareTag("Player")) return true;
        if (go.GetComponentInParent<IPlayerHUDTarget>() != null) return true;
        if (go.GetComponentInChildren<IPlayerHUDTarget>() != null) return true;
        return false;
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(col.center, col.size);
        }
        else
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawSphere(transform.position, 1.5f);
        }
    }
}