using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class AscensionManager : NetworkBehaviour
{
    private Material[] activeMaterials = new Material[4];
    public LineRenderer[] flowLines; // 4 đường nối từ trụ về tâm
    [Header("Cấu hình Material")]
    public Material flowMaterial;    // Kéo Material màu xanh vào đây
    public Material redFlowMaterial; // Kéo Material màu đỏ vào đây
    
    [Header("Cấu hình Trụ")]
    public Transform[] pillarPositions = new Transform[4];
    public int[] pillarStates = new int[4]; 

    private float[] flowDirections = new float[4]; // 1 = chảy vào tâm, -1 = chảy ngược ra trụ
    private List<CrystalCore> placedCrystals = new List<CrystalCore>();
    private Coroutine timerCoroutine;
    private bool isTimerRunning = false;
    
    [Header("Cấu hình Thời gian")]
    [Tooltip("Thời gian (giây) để đặt đủ tinh thể")]
    public float timeLimit = 5f; 

    void Start()
    {
        // Khởi tạo hướng ban đầu là 1 (chảy vào tâm) để tránh lỗi
        for(int i = 0; i < flowDirections.Length; i++) flowDirections[i] = 1f;
    }

    void Update()
    {
        for (int i = 0; i < flowLines.Length; i++)
        {
            // Kiểm tra cả activeMaterials[i] để tránh NullReferenceException
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

    // Trong AscensionManager.cs, sửa hàm EjectAllCrystals
    void EjectAllCrystals()
    {
        foreach (var crystal in placedCrystals)
        {
            if (crystal != null)
            {
                // CẬP NHẬT: Phải set về false để nhặt lại được
                UpdateSnappedStateServerRpc(crystal.NetworkObject, false);
                
                Rigidbody rb = crystal.GetComponent<Rigidbody>();
                if (rb != null) 
                {
                    rb.isKinematic = false;
                    rb.AddForce(new Vector3(Random.Range(-2f, 2f), 5f, Random.Range(-2f, 2f)), ForceMode.Impulse);
                }
            }
        }

        // RESET TRẠNG THÁI TRỤ
        foreach (var pillar in pillarPositions)
        {
            PillarStation station = pillar.GetComponent<PillarStation>();
            if (station != null) 
            {
                // SỬA: Phải set .Value = false ở đây thì Client mới hết lỗi
                station.isOccupied.Value = false; 
            }
        }

        foreach (var line in flowLines)
        {
            if (line != null) line.enabled = false;
        }

        placedCrystals.Clear();
        for (int i = 0; i < pillarStates.Length; i++) pillarStates[i] = 0;
        
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        isTimerRunning = false;
        
        Debug.Log("Hệ thống đã reset toàn bộ và mở khóa tinh thể.");
    }

    void CheckWinCondition()
    {
        if (placedCrystals.Count < 4) return;

        bool allCorrect = true;
        for (int i = 0; i < pillarPositions.Length; i++)
        {
            bool isCorrect = (pillarStates[i] == i);
            
            // Gọi RPC với tham số 'true' để nước chảy vào tâm
            SetFlowColorClientRpc(i, isCorrect ? Color.green : Color.red, true);

            if (!isCorrect) allCorrect = false;
        }

        if (allCorrect)
        {
            Debug.Log("Chúc mừng! Đã đặt đúng 4 viên!");
            if (timerCoroutine != null) StopCoroutine(timerCoroutine);
            isTimerRunning = false;
        }
        else
        {
            Debug.Log("Có viên sai! Bắt đầu quy trình đẩy ra...");
            StartCoroutine(DelayEject());
        }
    }

    // Hàm ClientRpc để đồng bộ màu sắc cho mọi người chơi
    // Sửa lại hàm ClientRpc để truyền đủ 3 tham số xuống Coroutine
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

        // Chọn Material dựa trên màu: Màu đỏ thì dùng redFlowMaterial, còn lại dùng flowMaterial
        bool isRed = (color == Color.red);
        Material selectedMat = isRed ? redFlowMaterial : flowMaterial;
        
        flowLines[i].material = selectedMat;
        activeMaterials[i] = selectedMat; 

        // Reset vị trí về Trụ trước khi chạy
        Vector3 startPos = flowLines[i].GetPosition(0);
        Vector3 endPos = flowLines[i].GetPosition(1);
        flowLines[i].SetPosition(1, startPos); 

        float duration = 1.0f; 
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            flowLines[i].SetPosition(1, Vector3.Lerp(startPos, endPos, t));
            yield return null;
        }
    }

    // Thay thế hàm DelayEject cũ bằng hàm này
    IEnumerator DelayEject()
    {
        yield return new WaitForSeconds(2.0f); 

        Vector3[] startPositions = new Vector3[flowLines.Length];
        Vector3[] endPositions = new Vector3[flowLines.Length];

        for(int i = 0; i < flowLines.Length; i++) {
            startPositions[i] = flowLines[i].GetPosition(0);
            endPositions[i] = flowLines[i].GetPosition(1);
        }

        float duration = 1.0f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            for(int i = 0; i < flowLines.Length; i++) {
                if(flowLines[i] != null && flowLines[i].enabled) {
                    flowDirections[i] = -1f; 
                    flowLines[i].SetPosition(1, Vector3.Lerp(endPositions[i], startPositions[i], t));
                }
            }
            yield return null;
        }

        EjectAllCrystals();
        foreach (var line in flowLines)
        {
            if (line != null) line.enabled = false;
        }
    }
}