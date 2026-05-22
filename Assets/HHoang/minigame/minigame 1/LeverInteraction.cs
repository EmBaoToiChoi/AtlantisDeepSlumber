using UnityEngine;

public class LeverInteraction : MonoBehaviour
{
    [Header("Cấu hình nút bấm")]
    public KeyCode interactKey = KeyCode.E;
    public QTEController qteController; 

    private bool isPlayerInZone = false;
    private bool isPlayingQTE = false;

    void Update()
    {
        if (isPlayerInZone && Input.GetKeyDown(interactKey))
        {
            if (!isPlayingQTE)
            {
                // Bắt đầu chơi: Khóa chân, dừng bánh răng, đẻ nút vô hạn
                isPlayingQTE = true;
                if (qteController != null) qteController.StartQTE(this);
            }
            else
            {
                // Nhấn E lần nữa khi đang chơi để HỦY CHƠI: Mở khóa chân, chạy lại bánh răng
                isPlayingQTE = false;
                if (qteController != null) qteController.ForceStopQTE();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerInZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = false;
            if (isPlayingQTE)
            {
                isPlayingQTE = false;
                if (qteController != null) qteController.ForceStopQTE();
            }
        }
    }
}