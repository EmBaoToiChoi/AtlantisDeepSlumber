using UnityEngine;
using Unity.Netcode;

public class Puzzle4TrapTrigger : NetworkBehaviour
{
    public GameObject floorPartA;
    public GameObject floorPartB;

    public bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        if(!IsServer)
            return;

        if(other.CompareTag("Player"))
        {
            if (!activated)
            {
                activated = true;
                OpenFloorClientRpc();
            }
        }
    }

    [ClientRpc]
    void OpenFloorClientRpc()
    {
        if(floorPartA != null)
            floorPartA.SetActive(false);

        if(floorPartB != null)
            floorPartB.SetActive(false);
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

        // Ẩn luôn cái box đi hoàn toàn để không bao giờ hiện nữa
        gameObject.SetActive(false);

        Debug.Log("Floor Appeared and Box Hidden");
    }

}