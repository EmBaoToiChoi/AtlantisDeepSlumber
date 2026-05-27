using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class AscensionManager : NetworkBehaviour
{
    [Header("Cấu hình Trụ")]
    public Transform[] pillarPositions = new Transform[4];
    public int[] pillarStates = new int[4]; 

    private List<CrystalCore> placedCrystals = new List<CrystalCore>();
    private Coroutine timerCoroutine;
    private bool isTimerRunning = false;
    
    [Header("Cấu hình Thời gian")]
    [Tooltip("Thời gian (giây) để đặt đủ tinh thể")]
    public float timeLimit = 5f; 

    public void SnapCrystalToPillar(CrystalCore crystal, int stationIndex)
    {
        if (stationIndex < 0 || stationIndex >= pillarPositions.Length) return;

        PillarStation station = pillarPositions[stationIndex].GetComponent<PillarStation>();
        if (station == null) return;

        // 1. Đặt vị trí, khóa vật lý và đánh dấu đã cắm
        crystal.transform.position = station.snapPosition.position;
        crystal.transform.rotation = station.snapPosition.rotation;
        Rigidbody rb = crystal.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        
        //Gọi ServerRpc để cập nhật trạng thái đồng bộ
        UpdateSnappedStateServerRpc(crystal.NetworkObject, true);

        // 2. Lưu thông tin
        pillarStates[stationIndex] = crystal.crystalID; 
        if (!placedCrystals.Contains(crystal)) placedCrystals.Add(crystal);

        // 3. Bắt đầu đếm giờ nếu là viên đầu tiên
        if (!isTimerRunning && placedCrystals.Count == 1)
        {
            isTimerRunning = true;
            timerCoroutine = StartCoroutine(TimerCountdown());
        }
        
        CheckWinCondition();
    }
    [ServerRpc(RequireOwnership = false)]
    private void UpdateSnappedStateServerRpc(NetworkObjectReference crystalRef, bool state)
    {
        if (crystalRef.TryGet(out NetworkObject netObj))
        {
            var crystal = netObj.GetComponent<CrystalCore>();
            if (crystal != null) crystal.isSnapped.Value = state;
        }
    }

    IEnumerator TimerCountdown()
    {
        float timeLeft = timeLimit;

        Debug.Log($"Bắt đầu đếm ngược: {timeLeft} giây.");

        while (timeLeft > 0)
        {
            // Đợi 1 giây
            yield return new WaitForSeconds(1f);
            
            // Trừ thời gian và log ra màn hình
            timeLeft--;
            
            if (timeLeft > 0)
            {
                Debug.Log($"Còn lại: {timeLeft} giây...");
            }
            else
            {
                Debug.Log("Đã hết thời gian!");
            }
        }

        // Sau khi vòng lặp kết thúc, kiểm tra điều kiện
        if (placedCrystals.Count < 4)
        {
            Debug.Log("Thời gian kết thúc mà chưa đủ 4 viên. Reset hệ thống!");
            EjectAllCrystals();
        }
        
        isTimerRunning = false;
    }

    void EjectAllCrystals()
    {
        foreach (var crystal in placedCrystals)
        {
            if (crystal != null)
            {
                UpdateSnappedStateServerRpc(crystal.NetworkObject, false);
                Rigidbody rb = crystal.GetComponent<Rigidbody>();
                if (rb != null) 
                {
                    rb.isKinematic = false;
                    rb.AddForce(new Vector3(Random.Range(-2f, 2f), 5f, Random.Range(-2f, 2f)), ForceMode.Impulse);
                }
            }
        }
        foreach (var pillar in pillarPositions)
        {
            PillarStation station = pillar.GetComponent<PillarStation>();
            if (station != null) station.isOccupied = false;
        }
        placedCrystals.Clear();
        for (int i = 0; i < pillarStates.Length; i++) pillarStates[i] = 0;
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        isTimerRunning = false;
    }

    void CheckWinCondition()
    {
        // Chỉ kiểm tra khi đã đặt đủ 4 viên
        if (placedCrystals.Count < 4) return;

        // Đã đủ 4 viên, kiểm tra xem có viên nào sai trụ không
        bool allCorrect = true;
        for (int i = 0; i < pillarPositions.Length; i++)
        {
            // Kiểm tra xem pillarStates[i] (ID của viên đang ở trụ i) có bằng i không
            if (pillarStates[i] != i) 
            {
                allCorrect = false;
                break;
            }
        }

        if (allCorrect)
        {
            Debug.Log("Chúc mừng! Đã đặt đúng 4 viên vào đúng 4 trụ!");
            if (timerCoroutine != null) StopCoroutine(timerCoroutine);
            isTimerRunning = false;
        }
        else
        {
            Debug.Log("Có viên đặt sai vị trí! Văng hết ra!");
            EjectAllCrystals();
        }
    }
}