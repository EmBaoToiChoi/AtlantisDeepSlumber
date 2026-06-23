using System.Collections;
using UnityEngine;
using Unity.Netcode; 

public class PuzzleManager : NetworkBehaviour 
{
    [Header("Puzzle Setup")]
    [SerializeField] private PillarInteract[] pillars = new PillarInteract[4]; 
    
    [Header("Door Settings")]
    [SerializeField] private GameObject door; 
    [SerializeField] private float openHeight = 5f; 
    [SerializeField] private float openDuration = 3f; 

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem dustEffect; 

    private NetworkVariable<bool> isPuzzleSolved = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        isPuzzleSolved.OnValueChanged += OnPuzzleSolvedChanged;
        
        if (isPuzzleSolved.Value && door != null)
        {
            door.transform.position = door.transform.position + Vector3.up * openHeight;
        }
    }

    public override void OnNetworkDespawn()
    {
        isPuzzleSolved.OnValueChanged -= OnPuzzleSolvedChanged;
    }

    public void CheckPuzzle()
    {
        if (!IsServer) return; 
        if (isPuzzleSolved.Value) return; 

        Debug.Log("<color=yellow>================ SERVER QUÉT ĐÁP ÁN ===============</color>");
        
        if (pillars == null || pillars.Length == 0) return;

        bool secretariesCorrect = true;

        for (int i = 0; i < pillars.Length; i++)
        {
            if (pillars[i] == null)
            {
                secretariesCorrect = false;
                break;
            }

            int current = pillars[i].GetCurrentDirectionValue();
            int target = pillars[i].GetCorrectDirectionValue();
            Debug.Log($"[Trạng Thái] {pillars[i].gameObject.name} -> Hiện tại: {current} | Đáp án: {target}");

            if (!pillars[i].IsCorrectDirection())
            {
                secretariesCorrect = false; 
                break; // Ngắt kiểm tra ngay khi phát hiện có 1 trụ chưa đúng hướng
            }
        }

        if (secretariesCorrect)
        {
            OpenDoor();
        }
    }

    private void OpenDoor()
    {
        isPuzzleSolved.Value = true;
        Debug.Log("<color=green>[SERVER] ĐÃ KHỚP TOÀN BỘ ĐÁP ÁN! ĐANG MỞ CỬA...</color>");
    }

    private void OnPuzzleSolvedChanged(bool previousValue, bool newValue)
    {
        if (newValue == true && door != null)
        {
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