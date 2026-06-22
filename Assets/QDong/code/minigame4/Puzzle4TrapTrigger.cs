using UnityEngine;
using Unity.Netcode;

public class Puzzle4TrapTrigger : NetworkBehaviour
{
    public GameObject floorPartA;
    public GameObject floorPartB;

    private bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        if(activated)
            return;

        if(other.CompareTag("Player"))
        {
            activated = true;

            floorPartA.SetActive(false);
            floorPartB.SetActive(false);
        }
    }
}