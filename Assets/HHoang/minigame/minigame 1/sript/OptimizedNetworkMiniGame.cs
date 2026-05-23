using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using DG.Tweening; 

public class OptimizedNetworkMiniGame : NetworkBehaviour
{
    [Header("Cụm UI Duy Nhất Trên Canvas")]
    public GameObject miniGamePlayZone; 
    public Slider localSlider;          
    public Image imgA;                  
    public Image imgD;                  

    [Header("Cấu hình Thông Số")]
    public float drainSpeed = 15f;    
    public float pushAmount = 8f;      // Tăng điểm khi bấm đúng
    public float penaltyAmount = 6f;   // Trừ điểm khi bấm sai
    public float greenZoneMin = 85f;  
    public float greenZoneMax = 100f; 

    [Tooltip("Số lượng người cần đạt vùng xanh cùng lúc để dừng cưa (Mặc định là 2, chỉnh thành 1 để test một mình)")]
    public int requiredPlayers = 2;    // <--- BIẾN PUBLIC BẠN CẦN ĐỂ CHỈNH NGOÀI INSPECTOR

    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.4f); 
    public Color activeColor = Color.white;                    

    [Header("Bánh Răng Mạng")]
    public GearRotator gear1;
    public GearRotator gear2;
    private float originalSpeed1;
    private float originalSpeed2;

    // Tiến trình Slider của 2 trạm
    private NetworkVariable<float> syncSlider1 = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> syncSlider2 = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // BIẾN MẠNG QUYẾT ĐỊNH NÚT NÀO SÁNG (0: Không nút nào, 1: Nút A sáng, 2: Nút D sáng)
    private NetworkVariable<int> targetButtonStation1 = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> targetButtonStation2 = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int myLocalStation = 0;

    void Start()
    {
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(false);
        
        ResetButtonVisual(imgA);
        ResetButtonVisual(imgD);

        if (gear1 != null) originalSpeed1 = gear1.rotationSpeed;
        if (gear2 != null) originalSpeed2 = gear2.rotationSpeed;
    }

    public void ToggleMiniGame(int stationIndex, bool isOpening)
    {
        myLocalStation = isOpening ? stationIndex : 0;
        
        if (miniGamePlayZone != null) 
            miniGamePlayZone.SetActive(isOpening);

        if (isOpening)
        {
            UpdateTargetButtonVisual();
        }
    }

    void Update()
    {
        if (myLocalStation == 1) localSlider.value = syncSlider1.Value;
        else if (myLocalStation == 2) localSlider.value = syncSlider2.Value;

        if (myLocalStation != 0)
        {
            UpdateTargetButtonVisual();
            HandleQTEInput();
        }

        // SERVER xử lý trừ tiến trình và đếm số lượng trạm đạt chuẩn vùng xanh
        if (IsServer)
        {
            if (syncSlider1.Value > 0) syncSlider1.Value -= drainSpeed * Time.deltaTime;
            if (syncSlider2.Value > 0) syncSlider2.Value -= drainSpeed * Time.deltaTime;

            // Đếm xem hiện tại có bao nhiêu trạm đang ở trong Vùng Xanh
            int playersInGreenZone = 0;

            if (syncSlider1.Value >= greenZoneMin && syncSlider1.Value <= greenZoneMax)
            {
                playersInGreenZone++;
            }
            if (syncSlider2.Value >= greenZoneMin && syncSlider2.Value <= greenZoneMax)
            {
                playersInGreenZone++;
            }

            // NẾU SỐ LƯỢNG TRẠM ĐẠT VÙNG XANH LỚN HƠN HOẶC BẰNG SỐ LƯỢNG ĐƯỢC YÊU CẦU
            if (playersInGreenZone >= requiredPlayers)
            {
                // Cho 2 bánh răng đứng im hoàn toàn
                if (gear1 != null) gear1.UpdateSpeedFromServer(0f);
                if (gear2 != null) gear2.UpdateSpeedFromServer(0f);
            }
            else
            {
                // Nếu không đủ số người, bánh răng quay lại tốc độ bình thường
                if (gear1 != null) gear1.UpdateSpeedFromServer(originalSpeed1);
                if (gear2 != null) gear2.UpdateSpeedFromServer(originalSpeed2);
            }
        }
    }

    void UpdateTargetButtonVisual()
    {
        int currentTarget = (myLocalStation == 1) ? targetButtonStation1.Value : targetButtonStation2.Value;

        if (currentTarget == 1) 
        {
            SetButtonActive(imgA);
            SetButtonPassive(imgD);
        }
        else if (currentTarget == 2) 
        {
            SetButtonActive(imgD);
            SetButtonPassive(imgA);
        }
    }

    void SetButtonActive(Image targetImg)
    {
        targetImg.color = activeColor;
        targetImg.transform.localScale = Vector3.one;
    }

    void SetButtonPassive(Image targetImg)
    {
        targetImg.color = normalColor;
        targetImg.transform.localScale = Vector3.one;
    }

    void ResetButtonVisual(Image targetImg)
    {
        targetImg.color = normalColor;
        targetImg.transform.localScale = Vector3.one;
    }

    void HandleQTEInput()
    {
        int currentTarget = (myLocalStation == 1) ? targetButtonStation1.Value : targetButtonStation2.Value;

        if (Input.GetKeyDown(KeyCode.A))
        {
            if (currentTarget == 1) 
            {
                PlaySuccessTween(imgA);
                SubmitQTEResultServerRpc(myLocalStation, true);
            }
            else 
            {
                PlayFailTween(imgA);
                SubmitQTEResultServerRpc(myLocalStation, false);
            }
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            if (currentTarget == 2) 
            {
                PlaySuccessTween(imgD);
                SubmitQTEResultServerRpc(myLocalStation, true);
            }
            else 
            {
                PlayFailTween(imgD);
                SubmitQTEResultServerRpc(myLocalStation, false);
            }
        }
    }

    void PlaySuccessTween(Image targetImg)
    {
        targetImg.transform.DOKill();
        targetImg.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Append(targetImg.transform.DOScale(1.2f, 0.05f).SetEase(Ease.OutQuad)); 
        seq.Append(targetImg.transform.DOScale(1.0f, 0.05f).SetEase(Ease.InQuad));  
        seq.Join(targetImg.DOFade(0f, 0.08f)); 
    }

    void PlayFailTween(Image targetImg)
    {
        targetImg.transform.DOKill();
        targetImg.transform.DOPunchPosition(new Vector3(10f, 0f, 0f), 0.2f, 15, 0.5f);
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitQTEResultServerRpc(int stationIndex, bool isCorrect)
    {
        float amount = isCorrect ? pushAmount : -penaltyAmount;

        if (stationIndex == 1)
        {
            syncSlider1.Value = Mathf.Clamp(syncSlider1.Value + amount, 0f, 100f);
            if (isCorrect)
            {
                int nextBtn = Random.Range(1, 3); 
                targetButtonStation1.Value = nextBtn;
            }
        }
        else if (stationIndex == 2)
        {
            syncSlider2.Value = Mathf.Clamp(syncSlider2.Value + amount, 0f, 100f);
            if (isCorrect)
            {
                int nextBtn = Random.Range(1, 3);
                targetButtonStation2.Value = nextBtn;
            }
        }
    }
}