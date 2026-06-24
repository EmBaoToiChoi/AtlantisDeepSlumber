using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class LavaDamageZone : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Lượng sát thương gây ra mỗi giây")]
    public float damagePerSecond = 10f;

    [Tooltip("Thời gian giãn cách giữa các lần gây sát thương (giây)")]
    public float tickRate = 0.5f;

    // Lưu trữ thời điểm gây sát thương tiếp theo cho mỗi người chơi để tick sát thương
    private Dictionary<GameObject, float> nextDamageTime = new Dictionary<GameObject, float>();

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void Update()
    {
        // Dọn dẹp dictionary nếu các Player GameObject bị hủy/null (ví dụ khi chuyển scene hoặc hồi sinh)
        var keys = new List<GameObject>(nextDamageTime.Keys);
        foreach (var key in keys)
        {
            if (key == null || !key.activeInHierarchy)
            {
                nextDamageTime.Remove(key);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Chỉ xử lý gây sát thương trên Server (trong chế độ mạng) hoặc độc lập (chơi đơn)
        if (IsNetworkActive && !IsServer) return;

        if (IsAnyPlayer(other.gameObject, out GameObject playerRoot))
        {
            float currentTime = Time.time;
            if (!nextDamageTime.TryGetValue(playerRoot, out float nextTime))
            {
                // Người chơi mới bước vào lava, gây sát thương lần đầu tiên ngay lập tức
                DealDamage(playerRoot, damagePerSecond * tickRate);
                nextDamageTime[playerRoot] = currentTime + tickRate;
            }
            else if (currentTime >= nextTime)
            {
                // Đủ thời gian giãn cách, gây sát thương tiếp theo
                DealDamage(playerRoot, damagePerSecond * tickRate);
                nextDamageTime[playerRoot] = currentTime + tickRate;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Khi người chơi rời khỏi lava, xóa khỏi danh sách theo dõi sát thương
        if (IsAnyPlayer(other.gameObject, out GameObject playerRoot))
        {
            if (nextDamageTime.ContainsKey(playerRoot))
            {
                nextDamageTime.Remove(playerRoot);
            }
        }
    }

    private void DealDamage(GameObject playerRoot, float damage)
    {
        if (damage <= 0f) return;

        Debug.Log($"[LavaDamageZone] Gây {damage} sát thương cho {playerRoot.name}");

        var elena = playerRoot.GetComponent<ElenaPlayer>();
        if (elena != null) { elena.TakeDamage(damage); return; }

        var arthur = playerRoot.GetComponent<ArthurPlayer>();
        if (arthur != null) { arthur.TakeDamage(damage); return; }

        var leo = playerRoot.GetComponent<LeoPlayer>();
        if (leo != null) { leo.TakeDamage(damage); return; }

        var maya = playerRoot.GetComponent<MayaPlayer>();
        if (maya != null) { maya.TakeDamage(damage); return; }

        var simple = playerRoot.GetComponent<SimplePlayerTest>();
        if (simple != null) { simple.TakeDamage(damage); return; }
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
