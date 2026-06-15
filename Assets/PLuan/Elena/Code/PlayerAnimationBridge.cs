using UnityEngine;

public class PlayerAnimationBridge : MonoBehaviour
{
    private ElenaPlayer mainPlayerScript;

    void Start()
    {
        // Tự động tìm script ElenaPlayer nằm trên Object cha (Player2)
        mainPlayerScript = GetComponentInParent<ElenaPlayer>();
        
        if (mainPlayerScript == null)
        {
            Debug.LogError($"[{gameObject.name}] Không tìm thấy script ElenaPlayer trên Object cha!");
        }
    }

    // Hàm này sẽ hứng Event từ Animator của Object con
    public void OnWeaponDrawGrab()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnWeaponDrawGrab(); // Bắn tiếp lên cho cha xử lý
        }
    }

    // Hàm này cũng thế
    public void OnWeaponSheathPlace()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnWeaponSheathPlace(); // Bắn tiếp lên cho cha xử lý
        }
    }

    // Forward sự kiện kết thúc nhào lộn
    public void OnRollEnd()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnRollEnd();
        }
    }

    // Forward sự kiện rút tên lên tay (Animation Event)
    public void OnDrawArrow()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnDrawArrow();
        }
    }

    public void OnPickItemEvent()
    {
        if (mainPlayerScript != null)
        {
            mainPlayerScript.OnPickItemEvent();
        }
    }
}