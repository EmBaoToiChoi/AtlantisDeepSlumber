using UnityEngine;
using UnityEngine.UIElements; 
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class OptimizedNetworkMiniGame : NetworkBehaviour
{
    [Header("Cấu hình")]
    public float decayRate = 15f; 
    public float pushAmount = 12f; 
    public float greenZoneMin = 85f;
    public int stationsNeededToOpen = 2; 

    [Header("UI & Gear")]
    public UIDocument uiDocument; 
    public List<GearRotator> gearList;

    private VisualElement mainContainer, progressFill, keyA, keyD;
    private int currentStationIndex = 0;
    private bool isPlaying = false;
    
    // --- BIẾN CỦA CLIENT ---
    private float localPredictedValue = 0f;
    private float displayValue = 0f; // BIẾN MỚI: Dùng để làm mượt thanh trượt
    private bool lastSentZoneStatus = false; 
    private float recoveryTimer = 0f; 
    private const float RECOVERY_WINDOW = 0.5f; 

    // --- BIẾN CỦA SERVER ---
    public NetworkVariable<bool> s0IsGreen = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> s1IsGreen = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> s2IsGreen = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> s3IsGreen = new NetworkVariable<bool>(false);
    
    public NetworkVariable<ulong> s0Owner = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> s1Owner = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> s2Owner = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<ulong> s3Owner = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<bool> station2HasCrystal = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> station3HasCrystal = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isCurrentlyOpen = new NetworkVariable<bool>(false);

    private float stateChangeTimer = 0f;
    private bool pendingState = false;

    public override void OnNetworkSpawn()
    {
        if (IsClient && uiDocument != null)
        {
            var root = uiDocument.rootVisualElement;
            mainContainer = root.Q<VisualElement>("MainContainer");
            progressFill = root.Q<VisualElement>("ProgressFill");
            keyA = root.Q<VisualElement>("KeyA");
            keyD = root.Q<VisualElement>("KeyD");
            if (mainContainer != null) mainContainer.style.display = DisplayStyle.None;
        }

        isCurrentlyOpen.OnValueChanged += (oldValue, newValue) => {
            foreach (var gear in gearList)
            {
                if (gear != null)
                {
                    if (newValue) gear.OpenGear();
                    else gear.ResetToSpinning();
                }
            }
        };

        if (isCurrentlyOpen.Value)
        {
            foreach (var gear in gearList) if (gear != null) gear.OpenGear();
        }
    }

    void Update()
    {
        if (IsClient && isPlaying) HandleLowFPSClientLogic();
        if (IsServer) CheckGateStatus();
    }

    private void HandleLowFPSClientLogic()
    {
        //if (GetOwner(currentStationIndex) != NetworkManager.Singleton.LocalClientId) return;

        if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
        {
            localPredictedValue = Mathf.Clamp(localPredictedValue + pushAmount, 0f, 100f);
            PlaySuccessVisual(Keyboard.current.aKey.wasPressedThisFrame ? keyA : keyD);
        }
        
        float currentDecay = decayRate;
        if (currentStationIndex == 2 && !station2HasCrystal.Value) currentDecay *= 2.5f;
        if (currentStationIndex == 3 && !station3HasCrystal.Value) currentDecay *= 2.5f;
        
        localPredictedValue = Mathf.Clamp(localPredictedValue - (currentDecay * Time.deltaTime), 0f, 100f);
        
        // Gọi hàm update UI mới mượt mà
        UpdateUI(); 

        bool currentZoneStatus = localPredictedValue >= greenZoneMin;

        if (currentZoneStatus != lastSentZoneStatus)
        {
            if (lastSentZoneStatus == true && currentZoneStatus == false)
            {
                recoveryTimer = RECOVERY_WINDOW; 
            }
            if (recoveryTimer > 0 && currentZoneStatus == true)
            {
                recoveryTimer = 0; 
            }
            else
            {
                lastSentZoneStatus = currentZoneStatus;
                UpdateZoneStatusServerRpc(currentStationIndex, lastSentZoneStatus);
            }
        }
        else if (currentZoneStatus == true && recoveryTimer <= 0)
        {
            if (Time.frameCount % 60 == 0) UpdateZoneStatusServerRpc(currentStationIndex, true);
        }

        if (recoveryTimer > 0) recoveryTimer -= Time.deltaTime;
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateZoneStatusServerRpc(int index, bool isGreen, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (GetOwner(index) != senderId) return;

        if (index == 0) s0IsGreen.Value = isGreen;
        else if (index == 1) s1IsGreen.Value = isGreen;
        else if (index == 2) s2IsGreen.Value = isGreen;
        else if (index == 3) s3IsGreen.Value = isGreen;
    }

    private void CheckGateStatus()
    {
        bool shouldBeOpen = false;
        if (stationsNeededToOpen == 1)
        {
            shouldBeOpen = (s0IsGreen.Value || s1IsGreen.Value || s2IsGreen.Value || s3IsGreen.Value);
        }
        else if (stationsNeededToOpen == 2)
        {
            shouldBeOpen = ((s0IsGreen.Value && s1IsGreen.Value) || (s2IsGreen.Value && s3IsGreen.Value));
        }

        if (shouldBeOpen != pendingState) { pendingState = shouldBeOpen; stateChangeTimer = 0f; }
        if (pendingState != isCurrentlyOpen.Value)
        {
            stateChangeTimer += Time.deltaTime;
            if (stateChangeTimer >= 0.5f) isCurrentlyOpen.Value = pendingState;
        }
        else stateChangeTimer = 0f;
    }

    public ulong GetOwner(int i) => i == 0 ? s0Owner.Value : i == 1 ? s1Owner.Value : i == 2 ? s2Owner.Value : s3Owner.Value;

    [ServerRpc(RequireOwnership = false)] 
    public void HandleStationAccessServerRpc(int i, ulong id, ServerRpcParams rpcParams = default) 
    {
        if (GetOwner(i) == ulong.MaxValue) 
        {
            SetOwner(i, rpcParams.Receive.SenderClientId); 
        }
    }
    
    private void SetOwner(int i, ulong id) { if (i == 0) s0Owner.Value = id; else if (i == 1) s1Owner.Value = id; else if (i == 2) s2Owner.Value = id; else if (i == 3) s3Owner.Value = id; }

    public void ToggleMiniGame(int index, bool isOpening)
    {
        currentStationIndex = index;
        isPlaying = isOpening;
        
        if (isOpening) 
        {
            localPredictedValue = 0f;
            displayValue = 0f; // Ép biến UI mượt về 0 luôn
            lastSentZoneStatus = false;
            recoveryTimer = 0f;
            UpdateUI(); // Cập nhật để màn hình thấy nó về 0 lập tức
            
            HandleStationAccessServerRpc(index, NetworkManager.Singleton.LocalClientId);
        }
        else 
        {
            UpdateZoneStatusServerRpc(index, false);
            HandleStationReleaseServerRpc(index, NetworkManager.Singleton.LocalClientId);
        }
        
        if (IsClient && mainContainer != null) mainContainer.style.display = isOpening ? DisplayStyle.Flex : DisplayStyle.None;
    }

    [ServerRpc(RequireOwnership = false)] 
    public void HandleStationReleaseServerRpc(int i, ulong id, ServerRpcParams rpcParams = default) 
    {
        if(GetOwner(i) == rpcParams.Receive.SenderClientId) 
        {
            SetOwner(i, ulong.MaxValue);
        }
    }
    
    // ĐÂY LÀ CHỖ LÀM MƯỢT UI
    private void UpdateUI() 
    { 
        if(progressFill != null) 
        {
            displayValue = Mathf.Lerp(displayValue, localPredictedValue, Time.deltaTime * 15f);
            progressFill.style.width = new Length(displayValue, LengthUnit.Percent); 
        }
    }

    private void PlaySuccessVisual(VisualElement k) { if (k == null) return; k.AddToClassList("pressed"); k.schedule.Execute(() => k.RemoveFromClassList("pressed")).StartingIn(100); }
    public void SetStationCrystalStatus(int index, bool hasCrystal) { if (IsServer) { if (index == 2) station2HasCrystal.Value = hasCrystal; else if (index == 3) station3HasCrystal.Value = hasCrystal; } }
}