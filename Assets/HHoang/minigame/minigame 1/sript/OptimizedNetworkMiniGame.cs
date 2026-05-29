using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using DG.Tweening;
using System.Collections.Generic;

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
    public float penaltyAmount = 6f;
    public float greenZoneMin = 85f;
    public float greenZoneMax = 100f;

    [Header("Visual Settings")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
    public Color activeColor = Color.white;

    [Header("Danh sách Bánh Răng")]
    public List<GearRotator> gearList;

    [Header("Danh sách trụ")]
    public NetworkVariable<bool> station0HasCrystal = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> station1HasCrystal = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> station2HasCrystal = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> station3HasCrystal = new NetworkVariable<bool>(false);

    private NetworkList<float> stationValues;
    private NetworkList<ulong> stationOwners;
    
    private int currentStationIndex = 0;
    private bool isPlaying = false;
    private bool isCurrentlyOpen = false;

    void Awake()
    {
        stationValues = new NetworkList<float>(new float[] { 0, 0, 0, 0 });
        stationOwners = new NetworkList<ulong>(new ulong[] { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue });
    }

    public override void OnNetworkSpawn()
    {
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(false);
    }

    void Update()
    {
        if (isPlaying)
        {
            if (localSlider != null && currentStationIndex < stationValues.Count) 
                localSlider.value = stationValues[currentStationIndex];
            
            imgA.color = activeColor;
            imgD.color = activeColor;
            
            HandleQTEInput();
        }

        if (IsServer)
        {
            for (int i = 0; i < stationValues.Count; i++)
            {
                float currentDrain = drainSpeed;
                if ((i == 2 && !station2HasCrystal.Value) || (i == 3 && !station3HasCrystal.Value))
                    currentDrain *= 2f; 

                if (stationValues[i] > 0) 
                    stationValues[i] -= currentDrain * Time.deltaTime;
            }

            bool pairA_Ready = (stationOwners[0] != ulong.MaxValue && stationValues[0] >= greenZoneMin) &&
                               (stationOwners[1] != ulong.MaxValue && stationValues[1] >= greenZoneMin);
            bool pairB_Ready = (stationOwners[2] != ulong.MaxValue && stationValues[2] >= greenZoneMin) &&
                               (stationOwners[3] != ulong.MaxValue && stationValues[3] >= greenZoneMin);
            
            bool shouldBeOpen = pairA_Ready || pairB_Ready;

            if (shouldBeOpen && !isCurrentlyOpen)
            {
                isCurrentlyOpen = true;
                foreach (var gear in gearList) 
                {
                    if (gear != null) {
                        gear.SetSpeed(gear.rotationSpeed * (gear.reverseDirection ? -1f : 1f));
                        gear.OpenGear();
                    }
                }
            }
            else if (!shouldBeOpen && isCurrentlyOpen)
            {
                isCurrentlyOpen = false;
                foreach (var gear in gearList) if (gear != null) gear.CloseGear();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSliderServerRpc(int index, float amount)
    {
        if (index < 0 || index >= stationValues.Count) return;
        stationValues[index] = Mathf.Clamp(stationValues[index] + amount, 0f, 100f);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestStationAccessServerRpc(int index, ulong clientId)
    {
        if (index >= 0 && index < stationOwners.Count && stationOwners[index] == ulong.MaxValue)
            stationOwners[index] = clientId;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReleaseStationServerRpc(int index, ulong clientId)
    {
        if (index >= 0 && index < stationOwners.Count && stationOwners[index] == clientId)
        {
            stationOwners[index] = ulong.MaxValue;
            stationValues[index] = 0f;
        }
    }

    public void ToggleMiniGame(int index, bool isOpening)
    {
        isPlaying = isOpening;
        currentStationIndex = index;
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(isOpening);
    }

    void HandleQTEInput()
    {
        if (Input.GetKeyDown(KeyCode.A)) ProcessInput(true);
        else if (Input.GetKeyDown(KeyCode.D)) ProcessInput(false);
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
        targetImg.DOColor(Color.green, 0.1f).OnComplete(() => targetImg.DOColor(activeColor, 0.2f));
    }
}