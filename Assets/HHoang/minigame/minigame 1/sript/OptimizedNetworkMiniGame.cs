using UnityEngine;
using UnityEngine.UIElements; 
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class OptimizedNetworkMiniGame : NetworkBehaviour
{
    [Header("Cấu hình linh hoạt")]
    public float decayRate = 15f; 
    
    [Header("UI Toolkit Setup")]
    public UIDocument uiDocument; 

    private VisualElement mainContainer;
    private VisualElement progressFill;
    private VisualElement keyA;
    private VisualElement keyD;

    [Header("Cấu hình Mini-game")]
    public float pushAmount = 12f; 
    public float greenZoneMin = 85f;

    [Header("Cấu hình số người")]
    [Tooltip("Số trạm cần đạt để mở cổng (1 hoặc 2)")]
    public int stationsNeededToOpen = 2; 

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

    // Dùng duy nhất biến này để đồng bộ trạng thái mở cổng
    public NetworkVariable<bool> isCurrentlyOpen = new NetworkVariable<bool>(false);

    private int currentStationIndex = 0;
    private bool isPlaying = false;
    private float syncTimer = 0f;
    private float localPredictedValue = 0f;

    public override void OnNetworkSpawn()
    {
        if (uiDocument != null)
        {
            var root = uiDocument.rootVisualElement;
            mainContainer = root.Q<VisualElement>("MainContainer");
            progressFill = root.Q<VisualElement>("ProgressFill");
            keyA = root.Q<VisualElement>("KeyA");
            keyD = root.Q<VisualElement>("KeyD");

            if (mainContainer != null)
            {
                mainContainer.AddToClassList("hidden");
                mainContainer.style.display = DisplayStyle.None;
            }
        }

        s0Value.OnValueChanged += (oldVal, newVal) => SyncUI(0, newVal);
        s1Value.OnValueChanged += (oldVal, newVal) => SyncUI(1, newVal);
        s2Value.OnValueChanged += (oldVal, newVal) => SyncUI(2, newVal);
        s3Value.OnValueChanged += (oldVal, newVal) => SyncUI(3, newVal);

        // Đảm bảo bánh răng cập nhật ngay khi isCurrentlyOpen thay đổi
        isCurrentlyOpen.OnValueChanged += (oldVal, newVal) => {
            foreach (var gear in gearList)
            {
                if (gear != null)
                {
                    if (newVal) gear.OpenGear();
                    else gear.ResetToSpinning();
                }
            }
        };
    }

    private void SyncUI(int index, float serverValue)
    {
        if (currentStationIndex == index && progressFill != null)
        {
            if (Mathf.Abs(localPredictedValue - serverValue) > 5f)
                localPredictedValue = serverValue; 
            else
                localPredictedValue = Mathf.Lerp(localPredictedValue, serverValue, 0.5f);
            
            progressFill.style.width = new Length(localPredictedValue, LengthUnit.Percent);
        }
    }

    void Update()
    {
        if (IsClient && isPlaying && !Application.isBatchMode)
        {
            HandleQTEInput();
        }

        if (IsServer)
        {
            UpdateStationDrain();
            CheckGateStatus();

            syncTimer += Time.deltaTime;
            if (syncTimer >= 0.5f) 
            {
                s0Value.Value = s0Value.Value; 
                s1Value.Value = s1Value.Value;
                s2Value.Value = s2Value.Value;
                s3Value.Value = s3Value.Value;
                syncTimer = 0f;
            }
        }
    }

    private void UpdateStationDrain()
    {
        if (!IsServer) return;

        float normal = decayRate * Time.deltaTime;
        float fast = decayRate * 2.5f * Time.deltaTime;

        s0Value.Value = Mathf.Clamp(s0Value.Value - normal, 0f, 100f);
        s1Value.Value = Mathf.Clamp(s1Value.Value - normal, 0f, 100f);

        float s2Speed = station2HasCrystal.Value ? normal : fast;
        s2Value.Value = Mathf.Clamp(s2Value.Value - s2Speed, 0f, 100f);

        float s3Speed = station3HasCrystal.Value ? normal : fast;
        s3Value.Value = Mathf.Clamp(s3Value.Value - s3Speed, 0f, 100f);
    }

    private void CheckGateStatus()
    {
        if (!IsServer) return; 

        // TÍNH TOÁN NGƯỠNG ĐỘNG
        // Nếu cổng đang đóng, dùng greenZoneMin (85). 
        // Nếu cổng đã mở, dùng ngưỡng thấp hơn (75) để tạo khoảng đệm.
        float threshold = isCurrentlyOpen.Value ? (greenZoneMin - 15f) : greenZoneMin;

        // SỬ DỤNG 'threshold' CHO TẤT CẢ CÁC TRẠM
        bool s0Ready = s0Value.Value >= threshold;
        bool s1Ready = s1Value.Value >= threshold;
        bool s2Ready = s2Value.Value >= threshold;
        bool s3Ready = s3Value.Value >= threshold;

        bool shouldBeOpen = (stationsNeededToOpen == 1) 
            ? (s0Ready || s1Ready || s2Ready || s3Ready) 
            : ((s0Ready && s1Ready) || (s2Ready && s3Ready));

        if (shouldBeOpen != isCurrentlyOpen.Value)
        {
            isCurrentlyOpen.Value = shouldBeOpen;
            Debug.Log($"[SERVER] Cổng đã mở trạng thái: {isCurrentlyOpen.Value}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSliderServerRpc(int index, float amount)
    {
        float newValue = Mathf.Clamp(GetStationValue(index) + amount, 0f, 100f);
        switch(index)
        {
            case 0: s0Value.Value = newValue; break;
            case 1: s1Value.Value = newValue; break;
            case 2: s2Value.Value = newValue; break;
            case 3: s3Value.Value = newValue; break;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void HandleStationAccessServerRpc(int index, ulong clientId)
    {
        SetOwner(index, clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void HandleStationReleaseServerRpc(int index, ulong clientId)
    {
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
        
        if (isOpening) HandleStationAccessServerRpc(index, NetworkManager.Singleton.LocalClientId);
        else HandleStationReleaseServerRpc(index, NetworkManager.Singleton.LocalClientId);

        if (IsClient && mainContainer != null) 
        {
            if (isOpening) 
            {
                mainContainer.RemoveFromClassList("hidden");
                mainContainer.style.display = DisplayStyle.Flex;
                localPredictedValue = GetStationValue(index);
                progressFill.style.width = new Length(localPredictedValue, LengthUnit.Percent);
            }
            else 
            {
                mainContainer.AddToClassList("hidden");
                Invoke(nameof(HideUIDelay), 0.3f); 
            }
        }
    }

    private void HideUIDelay()
    {
        if (mainContainer != null && mainContainer.ClassListContains("hidden"))
            mainContainer.style.display = DisplayStyle.None;
    }

    private float GetStationValue(int index) => index switch { 0 => s0Value.Value, 1 => s1Value.Value, 2 => s2Value.Value, 3 => s3Value.Value, _ => 0f };

    private void HandleQTEInput()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame) ProcessInput(true);
        else if (Keyboard.current.dKey.wasPressedThisFrame) ProcessInput(false);
    }

    private void ProcessInput(bool isA)
    {
        PlaySuccessVisual(isA ? keyA : keyD);
        localPredictedValue = Mathf.Clamp(localPredictedValue + pushAmount, 0f, 100f);
        progressFill.style.width = new Length(localPredictedValue, LengthUnit.Percent);
        UpdateSliderServerRpc(currentStationIndex, pushAmount);
    }

    private void PlaySuccessVisual(VisualElement targetKey)
    {
        if (targetKey == null) return;
        targetKey.AddToClassList("pressed");
        targetKey.schedule.Execute(() => {
            targetKey.RemoveFromClassList("pressed");
        }).StartingIn(100); 
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