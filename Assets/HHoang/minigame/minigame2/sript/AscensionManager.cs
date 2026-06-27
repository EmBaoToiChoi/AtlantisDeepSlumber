using UnityEngine;
using Unity.Netcode;

public class AscensionManager : NetworkBehaviour
{
    [Header("Đáp Án Giải Đố Nguyên Tố")]
    [Tooltip("Chọn hệ đúng cho 4 trụ tương ứng (0, 1, 2, 3)")]
    public ElementType[] correctCombination = new ElementType[4];

    [Header("Cấu hình Trụ & Mật Khẩu")]
    public Transform[] pillarPositions = new Transform[4];
    [Tooltip("Kéo cái Trụ Mật Khẩu chứa script PasswordPillarReveal vào đây")]
    public PasswordPillarReveal passwordPillar;

    [Header("Cấu hình Particle Dòng Chảy")]
    public ParticleSystem[] flowParticles; 
    private float[] defaultLifetimes;

    [Header("Cấu hình Hiệu ứng Chiến thắng")]
    public GameObject victoryEffectObject; 

    [Header("Cấu hình Cinematic Đá Rớt")]
    public GameObject bigStone;             
    public Transform stoneStartPos;         
    public Transform stoneEndPos;           
    public GameObject cinematicCamera;      
    public float stoneFallDuration = 1.5f;  
    public float cinematicWaitTime = 4.0f;  

    // Khóa trạng thái để không cho bắn tiếp khi đang check hoặc đã thắng
    private bool isCheckingOrWon = false;

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
        if (bigStone != null) bigStone.SetActive(false);
        if (cinematicCamera != null) cinematicCamera.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Chỉ Server mới có quyền theo dõi sự thay đổi nguyên tố của các trụ
        if (IsServer)
        {
            foreach (var pillarTransform in pillarPositions)
            {
                if (pillarTransform != null && pillarTransform.TryGetComponent<ElementalPillar>(out var pillar))
                {
                    // Đăng ký: Cứ mỗi khi có trụ đổi màu, gọi hàm kiểm tra
                    pillar.currentElement.OnValueChanged += (oldVal, newVal) => 
                    {
                        if (newVal != ElementType.None) CheckWinCondition();
                    };
                }
            }
        }
    }

    // Server check xem đã bắn đủ 4 trụ chưa và đúng hệ không
    void CheckWinCondition()
    {
        if (isCheckingOrWon) return;

        int filledCount = 0;
        bool allCorrect = true;

        for (int i = 0; i < pillarPositions.Length; i++)
        {
            var station = pillarPositions[i].GetComponent<ElementalPillar>();
            if (station != null && station.currentElement.Value != ElementType.None)
            {
                filledCount++;
                // So sánh nguyên tố đang có trên trụ với đáp án
                if (station.currentElement.Value != correctCombination[i])
                {
                    allCorrect = false; 
                }
            }
        }

        // Nếu chưa bắn sáng đủ 4 trụ thì chưa làm gì cả
        if (filledCount < 4) return;

        isCheckingOrWon = true; // Khóa lại không cho xử lý đè

        if (allCorrect)
        {
            // GIẢI ĐÚNG: Đổi dòng chảy thành màu xanh và kích hoạt chiến thắng
            for (int i = 0; i < pillarPositions.Length; i++) SetFlowColorClientRpc(i, Color.green);
            TriggerVictoryEffectsClientRpc();
        }
        else
        {
            // GIẢI SAI: Đổi dòng chảy màu đỏ và reset lại sau vài giây
            for (int i = 0; i < pillarPositions.Length; i++) SetFlowColorClientRpc(i, Color.red);
            StartCoroutine(DelayResetPillarsRoutine());
        }
    }

    [ClientRpc]
    private void TriggerVictoryEffectsClientRpc()
    {
        if (victoryEffectObject != null) victoryEffectObject.SetActive(true);
        
        // Bắt đầu hãm phanh hiển thị mật khẩu
        if (passwordPillar != null)
        {
            passwordPillar.TriggerRevealClientRpc();
        }

        StartCoroutine(PlayVictoryCinematicRoutine());
    }

    // ==========================================
    // LOGIC THẤT BẠI - RESET TRỤ
    // ==========================================
    System.Collections.IEnumerator DelayResetPillarsRoutine()
    {
        yield return new WaitForSeconds(3.0f); // Ngắm dòng chảy báo lỗi 3 giây

        ShrinkParticlesClientRpc(); // Tắt dòng chảy
        
        yield return new WaitForSeconds(1.5f); // Đợi tắt xong

        // Server tắt nguyên tố trên cả 4 trụ (Code mạng sẽ tự báo cho Client tắt đèn)
        foreach (var pillar in pillarPositions)
        {
            if (pillar != null && pillar.TryGetComponent<ElementalPillar>(out var station))
            {
                station.currentElement.Value = ElementType.None; 
            }
        }

        isCheckingOrWon = false; // Mở khóa cho bắn lại
    }

    // ==========================================
    // LOGIC CINEMATIC ĐÁ RỚT & DÒNG CHẢY
    // ==========================================
    System.Collections.IEnumerator PlayVictoryCinematicRoutine()
    {
        if (cinematicCamera != null) cinematicCamera.SetActive(true);

        if (bigStone != null && stoneStartPos != null)
        {
            bigStone.SetActive(true);
            bigStone.transform.position = stoneStartPos.position;
        }

        float elapsed = 0f;
        while (elapsed < stoneFallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stoneFallDuration;
            float curve = t * t; 

            if (bigStone != null && stoneStartPos != null && stoneEndPos != null)
            {
                bigStone.transform.position = Vector3.Lerp(stoneStartPos.position, stoneEndPos.position, curve);
            }
            yield return null;
        }

        if (bigStone != null && stoneEndPos != null)
        {
            bigStone.transform.position = stoneEndPos.position;
        }

        yield return new WaitForSeconds(Mathf.Max(0, cinematicWaitTime - stoneFallDuration));

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

    [ClientRpc]
    private void ShrinkParticlesClientRpc()
    {
        StartCoroutine(ShrinkParticlesRoutine());
    }

    System.Collections.IEnumerator ShrinkParticlesRoutine()
    {
        float shrinkDuration = 1.5f; 
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

        // Tắt hẳn hạt
        for (int i = 0; i < flowParticles.Length; i++)
        {
            if (flowParticles[i] != null) flowParticles[i].gameObject.SetActive(false);
        }
    }
}