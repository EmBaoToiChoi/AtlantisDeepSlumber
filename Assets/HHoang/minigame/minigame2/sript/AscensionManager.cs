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
    public NetworkVariable<int> correctCrystalsCount = new NetworkVariable<int>(-1);
    
    [Header("Cấu hình Thời gian")]
    public float timeLimit = 5f; 

    private float[] defaultLifetimes;

    void Start()
    {
        // Khởi tạo mảng lưu trữ
        defaultLifetimes = new float[flowParticles.Length];

        for (int i = 0; i < flowParticles.Length; i++)
        {
            if (flowParticles[i] != null)
            {
                // Lưu lại startLifetime mặc định
                defaultLifetimes[i] = flowParticles[i].main.startLifetime.constant;
                flowParticles[i].gameObject.SetActive(false);
            }
        }

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

        correctCrystalsCount.Value = -1; // Reset trạng thái UI về mặc định

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
        // Reset trạng thái trụ
        foreach (var pillar in pillarPositions)
        {
            if (pillar != null && pillar.TryGetComponent<PillarStation>(out var station))
            {
                // Phải có ngoặc nhọn ở đây
                station.isOccupied.Value = false; 
                station.SetEffectStateClientRpc(false); 
            } // Phải có ngoặc nhọn đóng ở đây
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

    /*void CheckWinCondition()
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
    }*/

    void CheckWinCondition()
    {
        if (placedCrystals.Count < 4) return;
        isTimerRunning.Value = false;

        bool allCorrect = true;
        int correctCount = 0; // Khởi tạo đếm số ngọc đúng

        // BƯỚC 1: Quét TẤT CẢ các trụ để đếm chính xác có bao nhiêu viên đặt đúng
        for (int i = 0; i < pillarPositions.Length; i++)
        {
            if (pillarStates[i] == i)
            {
                correctCount++; // Nếu đúng vị trí thì cộng thêm 1
            }
            else
            {
                allCorrect = false; // Nếu có 1 cái sai thì đánh dấu là chưa hoàn thành toàn bộ
                // BỎ lệnh break; ở đây để vòng lặp tiếp tục chạy và đếm hết 4 trụ
            }
        }

        // Cập nhật số lượng đếm được lên biến mạng để UI Client tự động thay đổi (ví dụ: 2/4)
        correctCrystalsCount.Value = correctCount; 

        // BƯỚC 2: Chốt màu cho TẤT CẢ các trụ
        Color finalColor = allCorrect ? Color.green : Color.red;

        for (int i = 0; i < pillarPositions.Length; i++)
        {
            SetFlowColorClientRpc(i, finalColor);
        }

        // BƯỚC 3: Kích hoạt hiệu ứng
        if (allCorrect) 
        {
            TriggerVictoryEffectsClientRpc();
        }
        else 
        {
            StartCoroutine(DelayEject());
        }
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
            
            // THÊM DÒNG NÀY: Phục hồi lại lifetime ban đầu
            main.startLifetime = defaultLifetimes[stationIndex]; 

            if (!ps.isPlaying) ps.Play();
        }
    }

    System.Collections.IEnumerator DelayEject()
    {
        // 1. Đợi 2 giây để người chơi nhìn thấy màu đỏ báo lỗi
        yield return new WaitForSeconds(4.0f);

        // 2. Yêu cầu tất cả Client làm hiệu ứng tụt startLifetime
        ShrinkParticlesClientRpc();

        // 3. Server đợi thêm 2 giây cho animation tụt chạy xong
        yield return new WaitForSeconds(4.0f);

        // 4. Văng ngọc ra ngoài
        EjectAllCrystals();
    }

    // ==========================================
    // CÁC HÀM XỬ LÝ HIỆU ỨNG TỤT START LIFETIME
    // ==========================================

    [ClientRpc]
    private void ShrinkParticlesClientRpc()
    {
        // Chạy Coroutine giảm dần hiệu ứng trên từng máy Client
        StartCoroutine(ShrinkParticlesRoutine());
    }

    System.Collections.IEnumerator ShrinkParticlesRoutine()
    {
        float shrinkDuration = 3.0f; // Thời gian tụt dần (2 giây)
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            
            // Tính tỷ lệ từ 1 tụt dần về 0
            float t = Mathf.Lerp(1f, 0f, elapsed / shrinkDuration);

            for (int i = 0; i < flowParticles.Length; i++)
            {
                if (flowParticles[i] != null && flowParticles[i].gameObject.activeSelf)
                {
                    var main = flowParticles[i].main;
                    // Nhân lifetime ban đầu với tỷ lệ t để giảm dần về 0
                    main.startLifetime = defaultLifetimes[i] * t;
                }
            }
            yield return null;
        }
    }
}