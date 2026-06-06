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

    private List<CrystalCore> placedCrystals = new List<CrystalCore>();

    public NetworkVariable<double> startTime = new NetworkVariable<double>(0);
    public NetworkVariable<bool> isTimerRunning = new NetworkVariable<bool>(false);
    
    [Header("Cấu hình Thời gian")]
    public float timeLimit = 5f; 

    void Start()
    {
        foreach (var ps in flowParticles) if (ps != null) ps.gameObject.SetActive(false);
        if (victoryEffectObject != null) victoryEffectObject.SetActive(false);
    }

    void Update()
    {
        if (IsServer && isTimerRunning.Value)
        {
            double elapsed = NetworkManager.Singleton.ServerTime.Time - startTime.Value;
            if (elapsed >= timeLimit)
            {
                if (placedCrystals.Count < 4) EjectAllCrystals();
                isTimerRunning.Value = false;
            }
        }
        placedCrystals.RemoveAll(item => item == null);
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

        // 1. Văng ngọc ra và giải phóng
        foreach (var crystal in placedCrystals)
        {
            if (crystal != null)
            {
                // --- BƯỚC 1: RESET LOGIC TRƯỚC ---
                crystal.holderId.Value = ulong.MaxValue; // Xóa chủ sở hữu
                crystal.isSnapped.Value = false;         // Xóa trạng thái khóa vào trụ
                crystal.isSnapping.Value = false;        // Xóa trạng thái đang bay
                
                // --- BƯỚC 2: DỜI VỊ TRÍ AN TOÀN ---
                crystal.transform.SetParent(null);
                crystal.transform.position += Vector3.up * 2.0f; // Dời ra ngoài bệ

                // 2. Lấy Collider của ngọc
                Collider crystalCol = crystal.GetComponent<Collider>();
                
                // 3. VÔ HIỆU HÓA VA CHẠM VỚI TRỤ TRONG 1 GIÂY
                foreach (var pillar in pillarPositions)
                {
                    if (pillar != null && pillar.TryGetComponent<Collider>(out var pillarCol))
                    {
                        Physics.IgnoreCollision(crystalCol, pillarCol, true);
                    }
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
                    
                    // Lực đẩy nhẹ hướng lên
                    rb.AddForce(new Vector3(Random.Range(-1f, 1f), 3f, Random.Range(-1f, 1f)), ForceMode.Impulse);
                }

                // 4. BẬT LẠI VA CHẠM SAU 1 GIÂY
                StartCoroutine(ReEnableCollision(crystalCol, 1.0f));
            }
        }

        // Reset trạng thái trụ
        foreach (var pillar in pillarPositions)
        {
            if (pillar != null && pillar.TryGetComponent<PillarStation>(out var station))
                station.isOccupied.Value = false; 
        }

        // 3. Reset các thiết lập manager
        foreach (var ps in flowParticles) if (ps != null) { ps.Stop(); ps.gameObject.SetActive(false); }

        placedCrystals.Clear();
        for (int i = 0; i < pillarStates.Length; i++) pillarStates[i] = 0;
        
        isTimerRunning.Value = false;
    }

    // Coroutine để bật lại va chạm sau khi ngọc văng ra an toàn
    System.Collections.IEnumerator ReEnableCollision(Collider crystalCol, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (crystalCol != null)
        {
            foreach (var pillar in pillarPositions)
            {
                if (pillar != null && pillar.TryGetComponent<Collider>(out var pillarCol))
                    Physics.IgnoreCollision(crystalCol, pillarCol, false);
            }
        }
    }

    void CheckWinCondition()
    {
        if (placedCrystals.Count < 4) return;
        isTimerRunning.Value = false;

        bool allCorrect = true;
        for (int i = 0; i < pillarPositions.Length; i++)
        {
            bool isCorrect = (pillarStates[i] == i);
            SetFlowColorClientRpc(i, isCorrect ? Color.green : Color.red);
            if (!isCorrect) allCorrect = false;
        }

        if (allCorrect) TriggerVictoryEffectsClientRpc();
        else StartCoroutine(DelayEject());
    }

    [ClientRpc]
    private void TriggerVictoryEffectsClientRpc()
    {
        if (victoryEffectObject != null) victoryEffectObject.SetActive(true);
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
            if (!ps.isPlaying) ps.Play();
        }
    }

    System.Collections.IEnumerator DelayEject()
    {
        yield return new WaitForSeconds(6.0f);
        EjectAllCrystals();
    }
}