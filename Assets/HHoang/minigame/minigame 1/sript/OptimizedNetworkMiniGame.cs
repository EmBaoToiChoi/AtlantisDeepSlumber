using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using DG.Tweening;

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

    [Header("Bánh Răng")]
    public GearRotator gear1;
    public GearRotator gear2;

    private NetworkList<float> stationValues;
    private NetworkList<ulong> stationOwners;
    
    private int currentStationIndex = 0;
    private bool isPlaying = false;
    private bool isCurrentlyOpen = false;

    // Biến đồng bộ trạng thái nút nào đang sáng (true = A, false = D)
    private NetworkVariable<bool> isANeeded = new NetworkVariable<bool>(true);

    void Awake()
    {
        stationValues = new NetworkList<float>(new float[] { 0, 0, 0, 0 });
        stationOwners = new NetworkList<ulong>(new ulong[] { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue });
    }

    public override void OnNetworkSpawn()
    {
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateSliderServerRpc(int index, float amount)
    {
        if (index < 0 || index >= stationValues.Count) return;
        stationValues[index] = Mathf.Clamp(stationValues[index] + amount, 0f, 100f);
    }

    [ServerRpc(RequireOwnership = false)]
    void RandomizeButtonServerRpc()
    {
        isANeeded.Value = Random.value > 0.5f;
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

    void Update()
    {
        // 1. Cập nhật UI & Input (Chỉ chạy khi đang chơi)
        if (isPlaying)
        {
            if (localSlider != null && currentStationIndex < stationValues.Count) 
                localSlider.value = stationValues[currentStationIndex];
            
            // Cập nhật màu nút dựa trên trạng thái Network
            imgA.color = isANeeded.Value ? activeColor : normalColor;
            imgD.color = !isANeeded.Value ? activeColor : normalColor;
            
            HandleQTEInput();
        }

        // 2. Logic cho Server (LUÔN CHẠY)
        if (IsServer)
        {
            int activeInGreenZone = 0;
            int totalOccupied = 0;

            for (int i = 0; i < stationValues.Count; i++)
            {
                if (stationValues[i] > 0) stationValues[i] -= drainSpeed * Time.deltaTime;
                if (stationOwners[i] != ulong.MaxValue)
                {
                    totalOccupied++;
                    if (stationValues[i] >= greenZoneMin && stationValues[i] <= greenZoneMax)
                        activeInGreenZone++;
                }
            }

            if (totalOccupied >= 2 && activeInGreenZone == totalOccupied)
            {
                if (!isCurrentlyOpen) { isCurrentlyOpen = true; gear1.OpenGear(); gear2.OpenGear(); }
            }
            else if (isCurrentlyOpen)
            {
                isCurrentlyOpen = false;
                gear1.CloseGear(); gear2.CloseGear();
            }
        }
    }

    void HandleQTEInput()
    {
        if (Input.GetKeyDown(KeyCode.A)) ProcessInput(true, isANeeded.Value);
        else if (Input.GetKeyDown(KeyCode.D)) ProcessInput(false, !isANeeded.Value);
    }

    void ProcessInput(bool isA, bool isCorrect)
    {
        if (isCorrect)
        {
            PlaySuccessTween(isA ? imgA : imgD);
            UpdateSliderServerRpc(currentStationIndex, pushAmount);
            RandomizeButtonServerRpc(); // Random lại nút ngay khi nhấn đúng
        }
        else
        {
            PlayFailTween(isA ? imgA : imgD);
            UpdateSliderServerRpc(currentStationIndex, -penaltyAmount);
        }
    }

    void PlaySuccessTween(Image targetImg)
    {
        targetImg.transform.DOKill();
        targetImg.transform.DOScale(1.2f, 0.1f).OnComplete(() => targetImg.transform.DOScale(1f, 0.1f));
        targetImg.DOColor(Color.green, 0.1f).OnComplete(() => targetImg.DOColor(activeColor, 0.2f));
    }

    void PlayFailTween(Image targetImg)
    {
        targetImg.transform.DOKill();
        targetImg.transform.DOPunchPosition(new Vector3(10f, 0f, 0f), 0.2f, 10, 0.5f);
        targetImg.DOColor(Color.red, 0.1f).OnComplete(() => targetImg.DOColor(normalColor, 0.2f));
    }
}