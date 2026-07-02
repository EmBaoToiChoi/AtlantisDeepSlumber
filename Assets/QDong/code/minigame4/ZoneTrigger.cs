using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class ZoneTrigger : NetworkBehaviour
{
    // List này chỉ cần Server quản lý để tính toán trọng lượng
    public List<IPlayerHUDTarget> playersInside = new List<IPlayerHUDTarget>();

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ Server/Host mới có quyền xử lý logic khi có người bước vào vùng Trigger
        if (!IsServer) return;

        IPlayerHUDTarget player = other.GetComponentInParent<IPlayerHUDTarget>();
        if (player != null && !playersInside.Contains(player))
        {
            playersInside.Add(player);
            Debug.Log(player.DisplayName + " entered " + gameObject.name);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Chỉ Server/Host mới có quyền xử lý logic khi có người rời vùng Trigger
        if (!IsServer) return;

        IPlayerHUDTarget player = other.GetComponentInParent<IPlayerHUDTarget>();
        if (player != null && playersInside.Contains(player))
        {
            playersInside.Remove(player);
            Debug.Log(player.DisplayName + " left " + gameObject.name);
        }
    }
}