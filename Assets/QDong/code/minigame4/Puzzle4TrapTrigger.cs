using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class Puzzle4TrapTrigger : NetworkBehaviour
{
    [Header("Floor Parts")]
    public GameObject floorPartA;
    public GameObject floorPartB;
    
    [Header("New Mechanics")]
    [Tooltip("Kéo InstantDeathZone vào đây để tắt khi bắt đầu và bật lại khi hoàn thành")]
    public GameObject instantDeathZoneObj;
    
    [Tooltip("Kéo một Transform làm điểm tâm của đĩa (nơi teleport 4 player tới)")]
    public Transform teleportTarget;
    
    [Tooltip("Số lượng người chơi cần đi qua box để kích hoạt")]
    public int requiredPlayers = 4;
    
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

        if(instantDeathZoneObj != null)
            instantDeathZoneObj.SetActive(false);
        
        Debug.Log("[Puzzle4Trap] Floor and InstantDeathZone hidden by Teleport Trigger.");
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

        if(instantDeathZoneObj != null)
        {
            instantDeathZoneObj.transform.SetParent(null);
            instantDeathZoneObj.SetActive(true);
        }

        // Ẩn luôn cái box đi hoàn toàn để không bao giờ hiện nữa
        gameObject.SetActive(false);

        Debug.Log("Floor Appeared and Box Hidden");
    }
}