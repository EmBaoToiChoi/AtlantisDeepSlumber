using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class ZoneTrigger : NetworkBehaviour
{
    // List này chỉ cần Server quản lý để tính toán trọng lượng
    public List<CharacterInfo> playersInside = new List<CharacterInfo>();

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ Server/Host mới có quyền xử lý logic khi có người bước vào vùng Trigger
        if (!IsServer) return;

        CharacterInfo player = other.GetComponent<CharacterInfo>();
        if (player != null && !playersInside.Contains(player))
        {
            playersInside.Add(player);
            Debug.Log(player.name + " entered " + gameObject.name);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Chỉ Server/Host mới có quyền xử lý logic khi có người rời vùng Trigger
        if (!IsServer) return;

        CharacterInfo player = other.GetComponent<CharacterInfo>();
        if (player != null && playersInside.Contains(player))
        {
            playersInside.Remove(player);
            Debug.Log(player.name + " left " + gameObject.name);
        }
    }
}