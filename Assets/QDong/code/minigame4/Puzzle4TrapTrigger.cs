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
        // Tách mặt phẳng ra khỏi box (đề phòng trường hợp mặt phẳng là con của box)
        if(floorPartA != null)
        {
            floorPartA.transform.SetParent(null);
            floorPartA.SetActive(true);
        }

        if(floorPartB != null)
        {
            floorPartB.transform.SetParent(null);
            floorPartB.SetActive(true);
        }

        // Bật lại Collider để người chơi dẫm lên không bị rớt
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        ZoneTrigger zone = GetComponent<ZoneTrigger>();
        if (zone != null) zone.enabled = false;

        // Bật lại mesh renderer để thấy object
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = true;

        Debug.Log("[Puzzle4Trap] Floor appeared, trigger box enabled safely (NetworkObject still alive)");
    }
}