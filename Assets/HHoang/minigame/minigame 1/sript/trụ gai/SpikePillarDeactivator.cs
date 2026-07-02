using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class SpikePillarDeactivator : MonoBehaviour
{
    [Header("Cấu hình liên kết")]
    [Tooltip("Kéo SpikePillarManager vào đây")]
    public SpikePillarManager manager;

    [Header("Cấu hình yêu cầu")]
    [Tooltip("Số lượng người chơi cần chạm vào để biến mất. Để <= 0 để tự động tính theo số người chơi thực tế.")]
    public int requiredPlayers = 4;

    // Lưu danh sách người chơi đang đứng trong vùng chạm (Safe Box)
    private HashSet<GameObject> playersInZone = new HashSet<GameObject>();

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void Update()
    {
        // Dọn dẹp danh sách nếu có người chơi bị thoát game hoặc hủy đối tượng
        var keys = new List<GameObject>(playersInZone);
        bool changed = false;
        foreach (var key in keys)
        {
            if (key == null || !key.activeInHierarchy)
            {
                playersInZone.Remove(key);
                changed = true;
            }
        }

        if (changed)
        {
            CheckDeactivationCondition();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ xử lý logic đếm trên Server hoặc Standalone (chơi đơn)
        if (IsNetworkActive && !IsServer) return;

        if (IsAnyPlayer(other.gameObject, out GameObject playerRoot))
        {
            if (!playersInZone.Contains(playerRoot))
            {
                playersInZone.Add(playerRoot);
                Debug.Log($"[SpikePillarDeactivator] Người chơi '{playerRoot.name}' đã vào vùng an toàn. Tổng số: {playersInZone.Count}");
                CheckDeactivationCondition();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Chỉ xử lý logic đếm trên Server hoặc Standalone (chơi đơn)
        if (IsNetworkActive && !IsServer) return;

        if (IsAnyPlayer(other.gameObject, out GameObject playerRoot))
        {
            if (playersInZone.Contains(playerRoot))
            {
                playersInZone.Remove(playerRoot);
                Debug.Log($"[SpikePillarDeactivator] Người chơi '{playerRoot.name}' rời vùng an toàn. Tổng số: {playersInZone.Count}");
                CheckDeactivationCondition();
            }
        }
    }

    private void CheckDeactivationCondition()
    {
        if (manager == null) return;

        int targetCount = requiredPlayers;
        if (targetCount <= 0)
        {
            if (IsNetworkActive)
            {
                targetCount = NetworkManager.Singleton.ConnectedClients.Count;
            }
            else
            {
                targetCount = 1; // Chơi đơn
            }
        }

        if (playersInZone.Count >= targetCount)
        {
            Debug.Log($"[SpikePillarDeactivator] Đủ {playersInZone.Count}/{targetCount} người chơi! Tắt cột gai.");
            manager.DeactivateSpawning();
        }
    }

    private bool IsAnyPlayer(GameObject go, out GameObject playerRoot)
    {
        playerRoot = null;
        if (go == null) return false;

        var elena = go.GetComponentInParent<ElenaPlayer>();
        if (elena != null) { playerRoot = elena.gameObject; return true; }

        var arthur = go.GetComponentInParent<ArthurPlayer>();
        if (arthur != null) { playerRoot = arthur.gameObject; return true; }

        var leo = go.GetComponentInParent<LeoPlayer>();
        if (leo != null) { playerRoot = leo.gameObject; return true; }

        var maya = go.GetComponentInParent<MayaPlayer>();
        if (maya != null) { playerRoot = maya.gameObject; return true; }

        var simple = go.GetComponentInParent<SimplePlayerTest>();
        if (simple != null) { playerRoot = simple.gameObject; return true; }

        return false;
    }
}
