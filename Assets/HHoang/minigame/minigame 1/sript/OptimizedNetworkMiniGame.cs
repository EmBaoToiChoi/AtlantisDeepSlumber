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
    public float pushAmount = 8f;      
    public float penaltyAmount = 6f;   
    public float greenZoneMin = 85f;  
    public float greenZoneMax = 100f; 

    [Tooltip("Số người cần đạt vùng xanh để dừng cưa (Chỉnh 1 để test 1 mình)")]
    public int requiredPlayers = 2;    

    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.4f); 
    public Color activeColor = Color.white;                    

    [Header("Bánh Răng Mạng")]
    public GearRotator gear1;
    public GearRotator gear2;
    private float originalSpeed1;
    private float originalSpeed2;
    
    // Biến trạng thái để tránh gọi tween liên tục
    private bool isCurrentlyOpen = false;

    // CHỈ CẦN 1 THANH SLIDER CHO CẢ 2 TRẠM
    private NetworkVariable<float> syncSlider = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> targetButton = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool isPlaying = false;

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
        isPlaying = isOpening;
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(isOpening);
        if (isOpening) UpdateTargetButtonVisual();
    }

    void Update()
    {
        // Cập nhật giá trị hiển thị trên máy local
        if (localSlider != null) localSlider.value = syncSlider.Value;

        if (isPlaying)
        {
            UpdateTargetButtonVisual();
            HandleQTEInput();
        }

        if (IsServer)
        {
            // Trừ tiến trình tự động
            if (syncSlider.Value > 0) syncSlider.Value -= drainSpeed * Time.deltaTime;

            // Kiểm tra vùng xanh
            bool isInGreenZone = (syncSlider.Value >= greenZoneMin && syncSlider.Value <= greenZoneMax);

            if (isInGreenZone)
            {
                if (!isCurrentlyOpen)
                {
                    isCurrentlyOpen = true;
                    gear1.UpdateSpeedFromServer(0f);
                    gear2.UpdateSpeedFromServer(0f);
                    // Gọi hàm mở cổng (đã setup trong GearRotator)
                    gear1.OpenGear(); 
                    gear2.OpenGear();
                }
            }
            else
            {
                if (isCurrentlyOpen)
                {
                    isCurrentlyOpen = false;
                    gear1.UpdateSpeedFromServer(originalSpeed1);
                    gear2.UpdateSpeedFromServer(originalSpeed2);
                    gear1.CloseGear();
                    gear2.CloseGear();
                }
            }
        }
    }

    void UpdateTargetButtonVisual()
    {
        if (targetButton.Value == 1) { SetButtonActive(imgA); SetButtonPassive(imgD); }
        else { SetButtonActive(imgD); SetButtonPassive(imgA); }
    }

    void SetButtonActive(Image targetImg) { targetImg.color = activeColor; targetImg.transform.localScale = Vector3.one; }
    void SetButtonPassive(Image targetImg) { targetImg.color = normalColor; targetImg.transform.localScale = Vector3.one; }
    void ResetButtonVisual(Image targetImg) { targetImg.color = normalColor; targetImg.transform.localScale = Vector3.one; }

    void HandleQTEInput()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            if (targetButton.Value == 1) { PlaySuccessTween(imgA); SubmitQTEResultServerRpc(true); }
            else { PlayFailTween(imgA); SubmitQTEResultServerRpc(false); }
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (targetButton.Value == 2) { PlaySuccessTween(imgD); SubmitQTEResultServerRpc(true); }
            else { PlayFailTween(imgD); SubmitQTEResultServerRpc(false); }
        }
    }

    void PlaySuccessTween(Image targetImg)
    {
        targetImg.transform.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(targetImg.transform.DOScale(1.2f, 0.05f));
        seq.Append(targetImg.transform.DOScale(1.0f, 0.05f));
    }

    void PlayFailTween(Image targetImg)
    {
        targetImg.transform.DOKill();
        targetImg.transform.DOPunchPosition(new Vector3(10f, 0f, 0f), 0.2f, 15, 0.5f);
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitQTEResultServerRpc(bool isCorrect)
    {
        float amount = isCorrect ? pushAmount : -penaltyAmount;
        syncSlider.Value = Mathf.Clamp(syncSlider.Value + amount, 0f, 100f);
        if (isCorrect) targetButton.Value = Random.Range(1, 3);
    }
}