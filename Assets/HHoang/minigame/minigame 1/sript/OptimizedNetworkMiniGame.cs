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
    public float drainSpeed = 15f;
    public float pushAmount = 8f;
    public float greenZoneMin = 85f;

    [Header("Danh sách Bánh Răng")]
    public List<GearRotator> gearList;

    // NetworkVariables
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
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(false);
    }

    void Update()
    {
        if (isPlaying && localSlider != null)
        {
            localSlider.value = GetStationValue(currentStationIndex);
            HandleQTEInput();
        }

        if (IsServer)
        {
            UpdateStationDrain();
            CheckGateStatus();
        }
    }

    // --- CÁC HÀM XỬ LÝ LOGIC TRUNG TÂM (Được gọi từ ServerRpc trong InteractBox) ---
    public void HandleStationAccess(int index, ulong clientId)
    {
        if (IsServer && GetOwner(index) == ulong.MaxValue)
        {
            SetOwner(index, clientId);
        }
    }

    public void HandleStationRelease(int index, ulong clientId)
    {
        if (IsServer && GetOwner(index) == clientId)
        {
            SetOwner(index, ulong.MaxValue);
            SetStationValue(index, 0f);
        }
    }

    private void UpdateStationDrain()
    {
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

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSliderServerRpc(int index, float amount)
    {
        float newVal = Mathf.Clamp(GetStationValue(index) + amount, 0f, 100f);
        SetStationValue(index, newVal);
    }

    // --- CÁC HÀM HELPER ---
    private float GetStationValue(int index) => index switch { 0 => s0Value.Value, 1 => s1Value.Value, 2 => s2Value.Value, 3 => s3Value.Value, _ => 0f };
    private void SetStationValue(int index, float val) { switch(index) { case 0: s0Value.Value = val; break; case 1: s1Value.Value = val; break; case 2: s2Value.Value = val; break; case 3: s3Value.Value = val; break; } }
    private ulong GetOwner(int index) => index switch { 0 => s0Owner.Value, 1 => s1Owner.Value, 2 => s2Owner.Value, 3 => s3Owner.Value, _ => ulong.MaxValue };
    private void SetOwner(int index, ulong id) { switch(index) { case 0: s0Owner.Value = id; break; case 1: s1Owner.Value = id; break; case 2: s2Owner.Value = id; break; case 3: s3Owner.Value = id; break; } }

    public void ToggleMiniGame(int index, bool isOpening)
    {
        currentStationIndex = index;
        isPlaying = isOpening;
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(isOpening);
    }

    void HandleQTEInput()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.aKey.wasPressedThisFrame) ProcessInput(true);
        else if (Keyboard.current.dKey.wasPressedThisFrame) ProcessInput(false);
    }

    void ProcessInput(bool isA)
    {
        PlaySuccessTween(isA ? imgA : imgD);
        UpdateSliderServerRpc(currentStationIndex, pushAmount);
    }

    void PlaySuccessTween(Image targetImg)
    {
        targetImg.transform.DOKill();
        targetImg.transform.DOScale(1.2f, 0.1f).OnComplete(() => targetImg.transform.DOScale(1f, 0.1f));
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetStationCrystalStatusServerRpc(int index, bool hasCrystal)
    {
        if (index == 2) station2HasCrystal.Value = hasCrystal;
        else if (index == 3) station3HasCrystal.Value = hasCrystal;
    }
}