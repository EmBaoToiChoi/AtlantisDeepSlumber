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

    [Header("Màu sắc")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
    public Color activeColor = Color.white;

    [Header("Bánh Răng Mạng")]
    public GearRotator gear1;
    public GearRotator gear2;
    private float originalSpeed1;
    private float originalSpeed2;

    private bool isCurrentlyOpen = false;
    private bool isPlaying = false;

    // QUAN TRỌNG: Lưu ID người chơi đang chiếm trạm để đảm bảo 1 người 1 nút
    public NetworkVariable<ulong> station1Owner = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> station2Owner = new NetworkVariable<ulong>(ulong.MaxValue);

    private NetworkVariable<float> syncSlider = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> targetButton = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Start()
    {
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(false);
        ResetButtonVisual(imgA);
        ResetButtonVisual(imgD);
        if (gear1 != null) originalSpeed1 = gear1.rotationSpeed;
        if (gear2 != null) originalSpeed2 = gear2.rotationSpeed;
    }

    // HÀM GỌI TỪ INTERACTBOX
    [ServerRpc(RequireOwnership = false)]
    public void RequestStationAccessServerRpc(int stationIndex, ulong clientId)
    {
        // Kiểm tra trạm 1
        if (stationIndex == 1)
        {
            if (station1Owner.Value == ulong.MaxValue) 
                station1Owner.Value = clientId; 
        }
        // Kiểm tra trạm 2
        else if (stationIndex == 2)
        {
            if (station2Owner.Value == ulong.MaxValue) 
                station2Owner.Value = clientId;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReleaseStationServerRpc(int stationIndex, ulong clientId)
    {
        if (stationIndex == 1 && station1Owner.Value == clientId) station1Owner.Value = ulong.MaxValue;
        else if (stationIndex == 2 && station2Owner.Value == clientId) station2Owner.Value = ulong.MaxValue;
    }

    public void ToggleMiniGame(int stationIndex, bool isOpening)
    {
        isPlaying = isOpening;
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(isOpening);
        if (isOpening) UpdateTargetButtonVisual();
    }

    void Update()
    {
        if (localSlider != null) localSlider.value = syncSlider.Value;

        if (isPlaying)
        {
            UpdateTargetButtonVisual();
            HandleQTEInput();
        }

        if (IsServer)
        {
            if (syncSlider.Value > 0) syncSlider.Value -= drainSpeed * Time.deltaTime;

            // KIỂM TRA ĐIỀU KIỆN: Cả 2 trạm đều phải có người chiếm
            bool isInGreenZone = (syncSlider.Value >= greenZoneMin && syncSlider.Value <= greenZoneMax);
            bool hasEnoughPlayers = (station1Owner.Value != ulong.MaxValue && station2Owner.Value != ulong.MaxValue);

            if (isInGreenZone && hasEnoughPlayers)
            {
                if (!isCurrentlyOpen)
                {
                    isCurrentlyOpen = true;
                    gear1.UpdateSpeedFromServer(0f);
                    gear2.UpdateSpeedFromServer(0f);
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