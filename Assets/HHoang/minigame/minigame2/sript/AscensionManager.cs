using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class AscensionManager : NetworkBehaviour
{
    [Header("Cấu hình Particle Dòng Chảy")]
    public ParticleSystem[] flowParticles; 
    
    [Header("Cấu hình Trụ")]
    public Transform[] pillarPositions = new Transform[4];
    public int[] pillarStates = new int[4]; 

    [Header("Cấu hình Hiệu ứng Chiến thắng")]
    public GameObject victoryEffectObject; 

    [Header("Cấu hình Cinematic Đá Rớt")]
    public GameObject bigStone;             // Cục đá bự
    public Transform stoneStartPos;         // Vị trí trên cao (bắt đầu rơi)
    public Transform stoneEndPos;           // Vị trí cố định (rơi xuống chạm đất)
    public GameObject cinematicCamera;      // Camera góc nhìn rộng trên map
    public float stoneFallDuration = 1.5f;  // Thời gian rơi (giây)
    public float cinematicWaitTime = 4.0f;  // Tổng thời gian chiếu Cinematic (giây)

    private List<CrystalCore> placedCrystals = new List<CrystalCore>();

    public NetworkVariable<double> startTime = new NetworkVariable<double>(0);
    public NetworkVariable<bool> isTimerRunning = new NetworkVariable<bool>(false);
    public NetworkVariable<int> correctCrystalsCount = new NetworkVariable<int>(-1);
    
    [Header("Cấu hình Thời gian")]
    public float timeLimit = 5f; 

    private float[] defaultLifetimes;

    void Start()
    {
        defaultLifetimes = new float[flowParticles.Length];

        for (int i = 0; i < flowParticles.Length; i++)
        {
            if (flowParticles[i] != null)
            {
                defaultLifetimes[i] = flowParticles[i].main.startLifetime.constant;
                flowParticles[i].gameObject.SetActive(false);
            }
        }

        if (victoryEffectObject != null) victoryEffectObject.SetActive(false);
        
        // Mặc định tắt đá và camera cinematic khi mới vào game
        if (bigStone != null) bigStone.SetActive(false);
        if (cinematicCamera != null) cinematicCamera.SetActive(false);
    }

    void Update()
    {
        if (IsServer && isTimerRunning.Value)
        {
            double elapsed = NetworkManager.Singleton.ServerTime.Time - startTime.Value;
            if (elapsed >= timeLimit)
            {
                placedCrystals.RemoveAll(item => item == null);
                if (placedCrystals.Count < 4) EjectAllCrystals();
                isTimerRunning.Value = false;
            }
        }
    }

    public void SnapCrystalToPillar(CrystalCore crystal, int stationIndex)
    {
        if (!IsServer || stationIndex < 0 || stationIndex >= pillarPositions.Length) return;

        crystal.LockToStation(); 
        pillarStates[stationIndex] = crystal.crystalID; 
        if (!placedCrystals.Contains(crystal)) placedCrystals.Add(crystal);

        if (!isTimerRunning.Value && placedCrystals.Count == 1)
        {
            startTime.Value = NetworkManager.Singleton.ServerTime.Time;
            isTimerRunning.Value = true;
        }
        
        CheckWinCondition();
    }

    public void EjectAllCrystals()
    {
        if (!IsServer) return;

        correctCrystalsCount.Value = -1; 
        placedCrystals.RemoveAll(item => item == null);

        int ejectedLayerIndex = LayerMask.NameToLayer("EjectedCrystal");

        foreach (var crystal in placedCrystals)
        {
            crystal.holderId.Value = ulong.MaxValue; 
            crystal.isSnapped.Value = false;         
            crystal.isSnapping.Value = false;        
            
            crystal.transform.SetParent(null);
            crystal.transform.position += Vector3.up * 2.0f; 

            int originalLayer = crystal.gameObject.layer;
            if (ejectedLayerIndex != -1) 
            {
                crystal.gameObject.layer = ejectedLayerIndex;
            }

            crystal.PerformDrop();

            Rigidbody rb = crystal.GetComponent<Rigidbody>();
            if (rb != null) 
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.ResetInertiaTensor();
                rb.WakeUp();
                
                rb.AddForce(new Vector3(Random.Range(-1f, 1f), 3f, Random.Range(-1f, 1f)), ForceMode.Impulse);
            }

            if (ejectedLayerIndex != -1)
            {
                StartCoroutine(ResetLayerRoutine(crystal.gameObject, originalLayer, 1.0f));
            }
        }

        foreach (var pillar in pillarPositions)
        {
            if (pillar != null && pillar.TryGetComponent<PillarStation>(out var station))
            {
                station.isOccupied.Value = false; 
                station.SetEffectStateClientRpc(false); 
            }
        }

        foreach (var ps in flowParticles) if (ps != null) { ps.Stop(); ps.gameObject.SetActive(false); }

        placedCrystals.Clear();
        for (int i = 0; i < pillarStates.Length; i++) pillarStates[i] = 0;
        
        isTimerRunning.Value = false;
    }

    System.Collections.IEnumerator ResetLayerRoutine(GameObject obj, int originalLayer, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null) obj.layer = originalLayer;
    }

    void CheckWinCondition()
    {
        if (placedCrystals.Count < 4) return;
        isTimerRunning.Value = false;

        bool allCorrect = true;
        int correctCount = 0; 

        for (int i = 0; i < pillarPositions.Length; i++)
        {
            if (pillarStates[i] == i) correctCount++; 
            else allCorrect = false; 
        }

        correctCrystalsCount.Value = correctCount; 
        Color finalColor = allCorrect ? Color.green : Color.red;

        for (int i = 0; i < pillarPositions.Length; i++) SetFlowColorClientRpc(i, finalColor);

        if (allCorrect) TriggerVictoryEffectsClientRpc();
        else StartCoroutine(DelayEject());
    }

    [ClientRpc]
    private void TriggerVictoryEffectsClientRpc()
    {
        if (victoryEffectObject != null) victoryEffectObject.SetActive(true);
        
        // Gọi Coroutine chiếu Cinematic trên TẤT CẢ các Client
        StartCoroutine(PlayVictoryCinematicRoutine());
    }

    // ==========================================
    // LOGIC CINEMATIC ĐÁ RỚT 
    // ==========================================
    System.Collections.IEnumerator PlayVictoryCinematicRoutine()
    {
        // 1. Bật Camera Cinematic đè lên góc nhìn người chơi
        if (cinematicCamera != null) cinematicCamera.SetActive(true);

        // 2. Chuẩn bị cục đá ở trên trời
        if (bigStone != null && stoneStartPos != null)
        {
            bigStone.SetActive(true);
            bigStone.transform.position = stoneStartPos.position;
        }

        // 3. Diễn hoạt đá rơi từ StartPos tới EndPos
        float elapsed = 0f;
        while (elapsed < stoneFallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stoneFallDuration;
            
            // Tính gia tốc (t*t) để đá rơi nhanh dần cho chân thực
            float curve = t * t; 

            if (bigStone != null && stoneStartPos != null && stoneEndPos != null)
            {
                bigStone.transform.position = Vector3.Lerp(stoneStartPos.position, stoneEndPos.position, curve);
            }
            yield return null;
        }

        // 4. Chốt cứng vị trí cục đá khi chạm đất (cố định lại luôn)
        if (bigStone != null && stoneEndPos != null)
        {
            bigStone.transform.position = stoneEndPos.position;
            
            // TIP: Nếu có Particle bụi mù hay rung màn hình thì gọi ở đây là hợp lý nhất!
        }

        // 5. Giữ camera thêm 1 khoảng thời gian để người chơi ngắm thành quả
        yield return new WaitForSeconds(Mathf.Max(0, cinematicWaitTime - stoneFallDuration));

        // 6. Tắt Camera Cinematic, hệ thống sẽ tự trả view về Camera của Player
        if (cinematicCamera != null) cinematicCamera.SetActive(false);
    }

    [ClientRpc]
    private void SetFlowColorClientRpc(int stationIndex, Color color)
    {
        if (stationIndex < 0 || stationIndex >= flowParticles.Length) return;
        ParticleSystem ps = flowParticles[stationIndex];
        if (ps != null)
        {
            ps.gameObject.SetActive(true);
            var main = ps.main;
            main.startColor = color; 
            main.startLifetime = defaultLifetimes[stationIndex]; 
            if (!ps.isPlaying) ps.Play();
        }
    }

    System.Collections.IEnumerator DelayEject()
    {
        yield return new WaitForSeconds(4.0f);
        ShrinkParticlesClientRpc();
        yield return new WaitForSeconds(4.0f);
        EjectAllCrystals();
    }

    [ClientRpc]
    private void ShrinkParticlesClientRpc()
    {
        StartCoroutine(ShrinkParticlesRoutine());
    }

    System.Collections.IEnumerator ShrinkParticlesRoutine()
    {
        float shrinkDuration = 3.0f; 
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Lerp(1f, 0f, elapsed / shrinkDuration);

            for (int i = 0; i < flowParticles.Length; i++)
            {
                if (flowParticles[i] != null && flowParticles[i].gameObject.activeSelf)
                {
                    var main = flowParticles[i].main;
                    main.startLifetime = defaultLifetimes[i] * t;
                }
            }
            yield return null;
        }
    }
}