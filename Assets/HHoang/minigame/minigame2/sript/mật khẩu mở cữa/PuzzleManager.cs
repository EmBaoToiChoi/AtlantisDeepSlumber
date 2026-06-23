using System.Collections;
using UnityEngine;
using Unity.Netcode; // BẮT BUỘC PHẢI CÓ

public class PuzzleManager : NetworkBehaviour // Đổi thành NetworkBehaviour
{
    [Header("Puzzle Setup")]
    [SerializeField] private PillarInteract[] pillars = new PillarInteract[4]; 
    
    [Header("Door Settings")]
    [SerializeField] private GameObject door; 
    [SerializeField] private float openHeight = 5f; 
    [SerializeField] private float openDuration = 3f; 

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem dustEffect; 

    // Biến mạng lưu trạng thái xem giải xong chưa (Chỉ Server được sửa)
    private NetworkVariable<bool> isPuzzleSolved = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // Lắng nghe khi nào Server báo Đã giải xong thì Clients tự chạy hiệu ứng trượt cửa
        isPuzzleSolved.OnValueChanged += OnPuzzleSolvedChanged;
        
        // Nếu có ông nào kết nối muộn mà cửa đã mở rồi, lập tức dịch cửa lên luôn
        if (isPuzzleSolved.Value && door != null)
        {
            door.transform.position = door.transform.position + Vector3.up * openHeight;
        }
    }

    public override void OnNetworkDespawn()
    {
        isPuzzleSolved.OnValueChanged -= OnPuzzleSolvedChanged;
    }

    // Hàm này chỉ thực thi trên Server (đã được bảo vệ từ PillarInteract)
    public void CheckPuzzle()
    {
        if (!IsServer) return; 
        if (isPuzzleSolved.Value) return; 

        bool allCorrect = true;

        foreach (PillarInteract pillar in pillars)
        {
            if (pillar != null && !pillar.IsCorrectDirection())
            {
                allCorrect = false;
                break; 
            }
        }

        if (allCorrect)
        {
            OpenDoor();
        }
    }

    private void OpenDoor()
    {
        // Server đổi giá trị -> kích hoạt OnPuzzleSolvedChanged trên toàn mạng
        isPuzzleSolved.Value = true;
        Debug.Log("[Server] Tất cả các trụ đã đúng hướng! Kích hoạt mở cửa.");
    }

    private void OnPuzzleSolvedChanged(bool previousValue, bool newValue)
    {
        if (newValue == true && door != null)
        {
            // Cả Server và tất cả Client cùng chạy Coroutine trượt cửa mượt mà tại máy của họ
            StartCoroutine(SlideDoorOpen());
        }
    }

    private IEnumerator SlideDoorOpen()
    {
        Vector3 startPos = door.transform.position;
        Vector3 targetPos = startPos + Vector3.up * openHeight; 

        if (dustEffect != null)
        {
            dustEffect.Play();
        }

        float elapsedTime = 0f;

        while (elapsedTime < openDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / openDuration;
            door.transform.position = Vector3.Lerp(startPos, targetPos, progress);
            yield return null; 
        }

        door.transform.position = targetPos;

        if (dustEffect != null)
        {
            dustEffect.Stop();
        }
    }
}