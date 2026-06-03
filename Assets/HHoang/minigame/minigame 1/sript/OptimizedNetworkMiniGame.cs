using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class OptimizedNetworkMiniGame : NetworkBehaviour
{
    [Header("Cấu hình linh hoạt")]
    public float decayRate = 15f; 
    
    [Header("UI & Controls")]
    public GameObject miniGamePlayZone;
    public Slider localSlider;
    public Image imgA;
    public Image imgD;

    [Header("Cấu hình Mini-game")]
    public float pushAmount = 12f; 
    public float greenZoneMin = 85f;

    public List<GearRotator> gearList;

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
        // Cập nhật Slider của trạm người chơi đang đứng
        s0Value.OnValueChanged += (oldVal, newVal) => { if(currentStationIndex == 0) localSlider.value = newVal; };
        s1Value.OnValueChanged += (oldVal, newVal) => { if(currentStationIndex == 1) localSlider.value = newVal; };
        s2Value.OnValueChanged += (oldVal, newVal) => { if(currentStationIndex == 2) localSlider.value = newVal; };
        s3Value.OnValueChanged += (oldVal, newVal) => { if(currentStationIndex == 3) localSlider.value = newVal; };
    }

    void Update()
    {
        // Logic Client
        if (IsClient && isPlaying && !Application.isBatchMode)
        {
            HandleQTEInput();
        }

        // Logic Server: Đảm bảo luôn chạy bất kể điều kiện gì khác
        if (IsServer)
        {
            UpdateStationDrain();
            CheckGateStatus();
        }
    }

    private void UpdateStationDrain()
    {
        if (!IsServer) return;

        float normal = decayRate * Time.deltaTime;
        float fast = decayRate * 2.5f * Time.deltaTime; // Tốc độ tụt gấp 2.5 lần nếu chưa có ngọc

        // Trạm 0 & 1: Luôn tụt tốc độ bình thường
        s0Value.Value = Mathf.Clamp(s0Value.Value - normal, 0f, 100f);
        s1Value.Value = Mathf.Clamp(s1Value.Value - normal, 0f, 100f);

        // Trạm 2 & 3: Tụt nhanh nếu không có ngọc, có ngọc rồi thì tụt bình thường
        float s2Speed = station2HasCrystal.Value ? normal : fast;
        s2Value.Value = Mathf.Clamp(s2Value.Value - s2Speed, 0f, 100f);

        float s3Speed = station3HasCrystal.Value ? normal : fast;
        s3Value.Value = Mathf.Clamp(s3Value.Value - s3Speed, 0f, 100f);
    }

    private void CheckGateStatus()
    {
        if (!IsServer) return; 

        // 1. Kiểm tra cặp trạm ngoài cửa (Trạm 0 VÀ Trạm 1 phải cùng xanh)
        bool pair1Ready = (s0Owner.Value != ulong.MaxValue && s0Value.Value >= greenZoneMin) && 
                          (s1Owner.Value != ulong.MaxValue && s1Value.Value >= greenZoneMin);

        // 2. Kiểm tra cặp trạm sau cửa (Trạm 2 VÀ Trạm 3 phải cùng xanh)
        bool pair2Ready = (s2Owner.Value != ulong.MaxValue && s2Value.Value >= greenZoneMin) && 
                          (s3Owner.Value != ulong.MaxValue && s3Value.Value >= greenZoneMin);

        // Cửa sẽ mở nếu 1 trong 2 CẶP đang được giữ
        bool shouldBeOpen = pair1Ready || pair2Ready;

        // Chỉ thực hiện lệnh khi có sự thay đổi trạng thái
        if (shouldBeOpen != isCurrentlyOpen)
        {
            isCurrentlyOpen = shouldBeOpen;
            
            foreach (var gear in gearList)
            {
                if (gear == null) continue;
                
                if (isCurrentlyOpen) 
                {
                    gear.OpenGear();
                }
                else 
                {
                    // Trở về vị trí cũ nếu buông tay hoặc tụt khỏi vùng xanh
                    gear.ResetToSpinning(); 
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSliderServerRpc(int index, float amount)
    {
        switch(index)
        {
            case 0: s0Value.Value = Mathf.Clamp(s0Value.Value + amount, 0f, 100f); break;
            case 1: s1Value.Value = Mathf.Clamp(s1Value.Value + amount, 0f, 100f); break;
            case 2: s2Value.Value = Mathf.Clamp(s2Value.Value + amount, 0f, 100f); break;
            case 3: s3Value.Value = Mathf.Clamp(s3Value.Value + amount, 0f, 100f); break;
        }
    }

    public void HandleStationAccess(int index, ulong clientId)
    {
        if (!IsServer) return;
        SetOwner(index, clientId);
    }

    public void HandleStationRelease(int index, ulong clientId)
    {
        if (!IsServer) return;
        if (GetOwner(index) == clientId) SetOwner(index, ulong.MaxValue);
    }

    private void SetOwner(int index, ulong id)
    {
        switch(index)
        {
            case 0: s0Owner.Value = id; break;
            case 1: s1Owner.Value = id; break;
            case 2: s2Owner.Value = id; break;
            case 3: s3Owner.Value = id; break;
        }
    }

    private ulong GetOwner(int index) => index switch { 0 => s0Owner.Value, 1 => s1Owner.Value, 2 => s2Owner.Value, 3 => s3Owner.Value, _ => ulong.MaxValue };

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
        PlaySuccessTween(isA ? imgA : imgD);
        UpdateSliderServerRpc(currentStationIndex, pushAmount);
    }

    private void PlaySuccessTween(Image targetImg)
    {
        if (targetImg == null) return;
        targetImg.transform.DOKill();
        targetImg.transform.DOScale(1.2f, 0.1f).OnComplete(() => targetImg.transform.DOScale(1f, 0.1f));
    }

    public void SetStationCrystalStatus(int index, bool hasCrystal) 
    {
        if (IsServer) 
        {
            if (index == 2) station2HasCrystal.Value = hasCrystal;
            else if (index == 3) station3HasCrystal.Value = hasCrystal;
        }
    }
}