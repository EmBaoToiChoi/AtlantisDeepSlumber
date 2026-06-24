using UnityEngine;
using Unity.Netcode;

public class Puzzle4TrapTrigger : NetworkBehaviour
{
    public GameObject floorPartA;
    public GameObject floorPartB;

    public bool activated = false;

    private bool isClosing = false;

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
            else if (!isClosing)
            {
                // Khi nhân vật chạm lại, đợi 3 giây rồi mới đóng mặt đất
                isClosing = true;
                StartCoroutine(DelayedClose());
            }
        }
    }

    private System.Collections.IEnumerator DelayedClose()
    {
        yield return new WaitForSeconds(1f);
        CloseFloorClientRpc();
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