using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class AscensionManager : NetworkBehaviour
{
    private Material[] activeMaterials = new Material[4];
    public LineRenderer[] flowLines; 
    [Header("Cấu hình Material")]
    public Material flowMaterial;    
    public Material redFlowMaterial; 
    
    [Header("Cấu hình Trụ")]
    public Transform[] pillarPositions = new Transform[4];
    public int[] pillarStates = new int[4]; 

    private float[] flowDirections = new float[4]; 
    private List<CrystalCore> placedCrystals = new List<CrystalCore>();
    private Coroutine timerCoroutine;
    private bool isTimerRunning = false;
    
    [Header("Cấu hình Thời gian")]
    public float timeLimit = 5f; 

    void Start()
    {
        for(int i = 0; i < flowDirections.Length; i++) flowDirections[i] = 1f;
    }

    void Update()
    {
        // Dọn dẹp danh sách ngọc nếu có viên nào bị null
        placedCrystals.RemoveAll(item => item == null);

        for (int i = 0; i < flowLines.Length; i++)
        {
            if (flowLines[i].enabled && activeMaterials[i] != null)
            {
                float speed = 0.5f * flowDirections[i];
                float offset = Time.time * speed;
                activeMaterials[i].SetTextureOffset("_MainTex", new Vector2(offset, 0));
            }
        }
    }

    public void SnapCrystalToPillar(CrystalCore crystal, int stationIndex)
    {
        if (!IsServer || stationIndex < 0 || stationIndex >= pillarPositions.Length) return;

        // 1. Lưu thông tin và cập nhật Server
        pillarStates[stationIndex] = crystal.crystalID; 
        if (!placedCrystals.Contains(crystal)) placedCrystals.Add(crystal);

        // 2. Bắt đầu đếm giờ nếu là viên đầu tiên
        if (!isTimerRunning && placedCrystals.Count == 1)
        {
            isTimerRunning = true;
            timerCoroutine = StartCoroutine(TimerCountdown());
        }
        
        CheckWinCondition();
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSnappedStateServerRpc(NetworkObjectReference crystalRef, bool state)
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
        while (timeLeft > 0)
        {
            yield return new WaitForSeconds(1f);
            timeLeft--;
        }

        if (placedCrystals.Count < 4)
        {
            EjectAllCrystals();
        }
        isTimerRunning = false;
    }

    void EjectAllCrystals()
    {
        if (!IsServer) return;

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

        // Reset trạng thái trụ cho Client đồng bộ
        foreach (var pillar in pillarPositions)
        {
            PillarStation station = pillar.GetComponent<PillarStation>();
            if (station != null) station.isOccupied.Value = false; 
        }

        foreach (var line in flowLines) if (line != null) line.enabled = false;

        placedCrystals.Clear();
        for (int i = 0; i < pillarStates.Length; i++) pillarStates[i] = 0;
        
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        isTimerRunning = false;
    }

    void CheckWinCondition()
    {
        if (placedCrystals.Count < 4) return; // Chưa đủ 4 viên thì đợi tiếp

        // Dừng timer khi đã đủ 4 viên để kiểm tra kết quả
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        isTimerRunning = false;

        bool allCorrect = true;
        for (int i = 0; i < pillarPositions.Length; i++)
        {
            bool isCorrect = (pillarStates[i] == i);
            // Gửi lệnh màu sắc cho toàn bộ Client
            SetFlowColorClientRpc(i, isCorrect ? Color.green : Color.red, true);
            
            if (!isCorrect) allCorrect = false;
        }

        if (allCorrect)
        {
            Debug.Log("Kích hoạt thành công!");
            // Gọi hàm mở cổng hoặc hiệu ứng chiến thắng tại đây
        }
        else
        {
            Debug.Log("Sai vị trí, văng ngọc!");
            StartCoroutine(DelayEject()); // Trễ 2s để người chơi kịp thấy màu đỏ
        }
    }

    [ClientRpc]
    private void SetFlowColorClientRpc(int stationIndex, Color color, bool isFlowingIn)
    {
        if (stationIndex < 0 || stationIndex >= flowLines.Length) return;
        StartCoroutine(AnimateFlow(stationIndex, color, isFlowingIn));
    }

    IEnumerator AnimateFlow(int i, Color color, bool isFlowingIn)
    {
        flowLines[i].enabled = true;
        flowDirections[i] = isFlowingIn ? 1f : -1f;
        Material selectedMat = (color == Color.red) ? redFlowMaterial : flowMaterial;
        flowLines[i].material = selectedMat;
        activeMaterials[i] = selectedMat; 
        yield break;
    }

    IEnumerator DelayEject()
    {
        yield return new WaitForSeconds(2.0f); 
        EjectAllCrystals();
    }
}