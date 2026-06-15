using UnityEngine;

public class MovementController : MonoBehaviour
{
    // Điền tên các script điều khiển của tụi nó vào đây
    [SerializeField] private string[] scriptNames = { "LeoPlayer", "ElenaPlayer", "MayaPlayer", "PlayerMovement", "InputHandler" };

    public void ToggleMovement(bool canMove)
    {
        foreach (var name in scriptNames)
        {
            // Tìm script theo tên (string)
            var script = GetComponent(name) as MonoBehaviour;
            if (script != null)
            {
                // Tắt/Bật script của tụi nó mà không cần đụng vào file gốc
                script.enabled = canMove;
            }
        }
    }
}