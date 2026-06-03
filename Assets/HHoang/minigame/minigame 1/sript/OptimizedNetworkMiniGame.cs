using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class OptimizedNetworkMiniGame : NetworkBehaviour
{
    [Header("UI & Controls")]
    public GameObject miniGamePlayZone;
    public Slider localSlider;
    public Image imgA;
    public Image imgD;

    [Header("Cấu hình Mini-game")]
    public float pushAmount = 8f;
    public float greenZoneMin = 85f;

    public List<GearRotator> gearList;

    // NetworkVariables (Dữ liệu quan trọng chỉ Server được viết)
    public NetworkVariable<float> s0Value = new NetworkVariable<float>(0f);
    public NetworkVariable<float> s1Value = new NetworkVariable<float>(0f);
    public NetworkVariable<float> s2Value = new NetworkVariable<float>(0f);
    public NetworkVariable<float> s3Value = new NetworkVariable<float>(0f);

    public NetworkVariable<ulong> s0Owner = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> s1Owner = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> s2Owner = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> s3Owner = new NetworkVariable<ulong>(ulong.MaxValue);

    public NetworkVariable<bool> station2HasCrystal = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> station3HasCrystal = new NetworkVariable<bool>(false);

    private int currentStationIndex = 0;
    private bool isPlaying = false;
    private bool isCurrentlyOpen = false;

    public override void OnNetworkSpawn()
    {
        // Chỉ Client mới cần tắt UI lúc khởi động
        if (IsClient && miniGamePlayZone != null) miniGamePlayZone.SetActive(false);
    }

    void Update()
    {
        // 1. Logic Client: Hiển thị UI và nhận Input
        if (IsClient && isPlaying && !Application.isBatchMode)
        {
            if (localSlider != null) localSlider.value = GetStationValue(currentStationIndex);
            HandleQTEInput();
        }

        // 2. Logic Server: Xử lý logic game
        if (IsServer)
        {
            UpdateStationDrain();
            CheckGateStatus();
        }
    }

    private void UpdateStationDrain()
    {
        // Dùng hằng số hoặc cấu hình để dễ chỉnh sửa
        DrainStation(ref s0Value, 15f);
        DrainStation(ref s1Value, 15f);
        DrainStation(ref s2Value, station2HasCrystal.Value ? 15f : 30f);
        DrainStation(ref s3Value, station3HasCrystal.Value ? 15f : 30f);
    }

    private void DrainStation(ref NetworkVariable<float> stat, float speed)
    {
        if (stat.Value > 0) stat.Value -= speed * Time.deltaTime;
    }

    private void CheckGateStatus()
    {
        bool pairA_Ready = (s0Owner.Value != ulong.MaxValue && s0Value.Value >= greenZoneMin) &&
                           (s1Owner.Value != ulong.MaxValue && s1Value.Value >= greenZoneMin);
        bool pairB_Ready = (s2Owner.Value != ulong.MaxValue && s2Value.Value >= greenZoneMin) &&
                           (s3Owner.Value != ulong.MaxValue && s3Value.Value >= greenZoneMin);
        
        bool shouldBeOpen = pairA_Ready || pairB_Ready;

        if (shouldBeOpen != isCurrentlyOpen)
        {
            isCurrentlyOpen = shouldBeOpen;
            foreach (var gear in gearList)
            {
                if (gear == null) continue;
                if (isCurrentlyOpen) gear.OpenGear();
                else gear.CloseGear();
            }
        }
    }

    // Các hàm Logic Server
    public void HandleStationAccess(int index, ulong clientId)
    {
        if (IsServer && GetOwner(index) == ulong.MaxValue) SetOwner(index, clientId);
    }

    public void HandleStationRelease(int index, ulong clientId)
    {
        if (IsServer && GetOwner(index) == clientId)
        {
            SetOwner(index, ulong.MaxValue);
            SetStationValue(index, 0f);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSliderServerRpc(int index, float amount)
    {
        float newVal = Mathf.Clamp(GetStationValue(index) + amount, 0f, 100f);
        SetStationValue(index, newVal);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetStationCrystalStatusServerRpc(int index, bool hasCrystal)
    {
        if (index == 2) station2HasCrystal.Value = hasCrystal;
        else if (index == 3) station3HasCrystal.Value = hasCrystal;
    }

    private float GetStationValue(int index) => index switch 
    { 
        0 => s0Value.Value, 
        1 => s1Value.Value, 
        2 => s2Value.Value, 
        3 => s3Value.Value, 
        _ => 0f // Dòng này giúp máy tính biết nếu index ngoài 0-3 thì trả về 0
    };

    private ulong GetOwner(int index) => index switch 
    { 
        0 => s0Owner.Value, 
        1 => s1Owner.Value, 
        2 => s2Owner.Value, 
        3 => s3Owner.Value, 
        _ => ulong.MaxValue // Dòng này giúp máy tính biết nếu index sai thì trả về giá trị "không ai sở hữu"
    };

    private void SetStationValue(int index, float val) 
    { 
        switch(index) 
        { 
            case 0: s0Value.Value = val; break; 
            case 1: s1Value.Value = val; break; 
            case 2: s2Value.Value = val; break; 
            case 3: s3Value.Value = val; break; 
            default: break; // Thêm dòng này để xử lý index sai
        } 
    }

    private void SetOwner(int index, ulong id) 
    { 
        switch(index) 
        { 
            case 0: s0Owner.Value = id; break; 
            case 1: s1Owner.Value = id; break; 
            case 2: s2Owner.Value = id; break; 
            case 3: s3Owner.Value = id; break; 
            default: break; // Thêm dòng này để xử lý index sai
        } 
    }
    public void ToggleMiniGame(int index, bool isOpening)
    {
        currentStationIndex = index;
        isPlaying = isOpening;
        if (IsClient && miniGamePlayZone != null) miniGamePlayZone.SetActive(isOpening);
    }

    private void HandleQTEInput()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame) ProcessInput(true);
        else if (Keyboard.current.dKey.wasPressedThisFrame) ProcessInput(false);
    }

    private void ProcessInput(bool isA)
    {
        if (IsClient) PlaySuccessTween(isA ? imgA : imgD);
        UpdateSliderServerRpc(currentStationIndex, pushAmount);
    }

    private void PlaySuccessTween(Image targetImg)
    {
        if (targetImg == null) return;
        targetImg.transform.DOKill();
        targetImg.transform.DOScale(1.2f, 0.1f).OnComplete(() => targetImg.transform.DOScale(1f, 0.1f));
    }
}