using UnityEngine;
using Unity.Netcode;

public class InstantDeathZone : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Lượng sát thương gây ra ngay lập tức")]
    public float instantDamage = 9999f;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ xử lý gây sát thương trên Server (trong chế độ mạng) hoặc độc lập (chơi đơn)
        if (IsNetworkActive && !IsServer) return;

        if (IsAnyPlayer(other.gameObject, out GameObject playerRoot))
        {
            DealDamage(playerRoot, instantDamage);
        }
    }

    private void DealDamage(GameObject playerRoot, float damage)
    {
        if (damage <= 0f) return;

        Debug.Log($"[InstantDeathZone] Gây {damage} sát thương cho {playerRoot.name} (chết ngay lập tức)");

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
