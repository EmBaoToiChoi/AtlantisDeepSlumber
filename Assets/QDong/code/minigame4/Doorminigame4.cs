using UnityEngine;
using Unity.Netcode;

public class Doorminigame4 : NetworkBehaviour
{
    public Puzzle4Manager puzzle4;

    void Update()
    {
        if (puzzle4.puzzleCompleted.Value)
        {
            gameObject.SetActive(false);
        }
    }
}