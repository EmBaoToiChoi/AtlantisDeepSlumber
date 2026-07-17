using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class Puzzle4TrapTrigger : NetworkBehaviour
{
    [Header("Floor Parts")]
    public GameObject floorPartA;
    public GameObject floorPartB;
    
    private HashSet<ulong> playersTouched = new HashSet<ulong>();

    public bool activated = false;

    public void ActivateTrapFromTeleport()
    {
        if(!IsServer) return;
        
        if (!activated)
        {
            activated = true;
            HideFloorClientRpc();
        }
    }

    [ClientRpc]
    void HideFloorClientRpc()
    {
        if(floorPartA != null)
            floorPartA.SetActive(false);

        if(floorPartB != null)
            floorPartB.SetActive(false);

        Debug.Log("[Puzzle4Trap] Floor hidden by Teleport Trigger.");
    }

    [ClientRpc]
    public void CloseFloorClientRpc()
    {
        Debug.Log($"[Puzzle4Trap] CloseFloorClientRpc RECEIVED trên client. floorPartA={(floorPartA != null ? floorPartA.name : "NULL")}, floorPartB={(floorPartB != null ? floorPartB.name : "NULL")}");

        // KHÔNG gọi SetParent(null) vì có thể gây conflict với network hierarchy
        if (floorPartA != null)
        {
            floorPartA.SetActive(true);
            Debug.Log($"[Puzzle4Trap] floorPartA '{floorPartA.name}' -> SetActive(true)");
        }
        else
        {
            Debug.LogError("[Puzzle4Trap] floorPartA là NULL! Kiểm tra Inspector của Puzzle4TrapTrigger.");
        }

        if (floorPartB != null)
        {
            floorPartB.SetActive(true);
            Debug.Log($"[Puzzle4Trap] floorPartB '{floorPartB.name}' -> SetActive(true)");
        }
        else
        {
            Debug.LogError("[Puzzle4Trap] floorPartB là NULL! Kiểm tra Inspector của Puzzle4TrapTrigger.");
        }

        // Bật lại Collider để người chơi dẫm lên không bị rớt
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        ZoneTrigger zone = GetComponent<ZoneTrigger>();
        if (zone != null) zone.enabled = false;

        // Bật lại mesh renderer để thấy object
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = true;

        Debug.Log("[Puzzle4Trap] CloseFloorClientRpc HOÀN THÀNH.");
    }
}