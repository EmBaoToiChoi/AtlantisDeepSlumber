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
    public List<GearRotator> gearList; // Bạn có thể thêm bao nhiêu bánh răng tùy thích trong Inspector
    // Khai báo trong class
    [Header("Danh sách trụ")]
    public NetworkVariable<bool> station0HasCrystal = new NetworkVariable<bool>(true); // Trạm 0, 1 mặc định có
    public NetworkVariable<bool> station1HasCrystal = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> station2HasCrystal = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> station3HasCrystal = new NetworkVariable<bool>(false);

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
            // ĐOẠN CODE MỚI ĐỂ CẢ 2 CÙNG SÁNG
            imgA.color = activeColor;
            imgD.color = activeColor;
            
            HandleQTEInput();
        }

        // 2. Logic cho Server (LUÔN CHẠY)
        if (IsServer)
        {
            // 1. Logic giảm điểm (Tự động tính tốc độ tùy theo tinh thể)
            for (int i = 0; i < stationValues.Count; i++)
            {
                float currentDrain = drainSpeed;
                
                // Kiểm tra cặp trong (index 2 và 3): Nếu không có tinh thể thì nhân tốc độ lên 5 lần
                if ((i == 2 && !station2HasCrystal.Value) || (i == 3 && !station3HasCrystal.Value))
                {
                    currentDrain *= 2f; 
                }

                if (stationValues[i] > 0) 
                    stationValues[i] -= currentDrain * Time.deltaTime;
            }

            // 2. Logic kiểm tra điều kiện mở cửa (CẶP A hoặc CẶP B)
            // Cặp A (0 & 1): Cả 2 phải có người đứng và trong vùng xanh
            bool pairA_Ready = (stationOwners[0] != ulong.MaxValue && stationValues[0] >= greenZoneMin) &&
                            (stationOwners[1] != ulong.MaxValue && stationValues[1] >= greenZoneMin);

            // Cặp B (2 & 3): Cả 2 phải có người đứng và trong vùng xanh
            bool pairB_Ready = (stationOwners[2] != ulong.MaxValue && stationValues[2] >= greenZoneMin) &&
                            (stationOwners[3] != ulong.MaxValue && stationValues[3] >= greenZoneMin);

            // Bánh răng chạy nếu 1 trong 2 cặp thỏa mãn
            bool shouldBeOpen = pairA_Ready || pairB_Ready;

            if (shouldBeOpen && !isCurrentlyOpen)
            {
                isCurrentlyOpen = true;
                foreach (var gear in gearList) if (gear != null) gear.OpenGear();
            }
            else if (!shouldBeOpen && isCurrentlyOpen)
            {
                isCurrentlyOpen = false;
                foreach (var gear in gearList) if (gear != null) gear.CloseGear();
            }
        }

        // Trong file OptimizedNetworkMiniGame.cs

        void HandleQTEInput()
        {
            // Bất kể nhấn A hay D, đều tính là input hợp lệ
            if (Input.GetKeyDown(KeyCode.A)) ProcessInput(true);
            else if (Input.GetKeyDown(KeyCode.D)) ProcessInput(false);
        }

        void ProcessInput(bool isA)
        {
            // Luôn luôn cộng điểm, không còn check đúng/sai
            PlaySuccessTween(isA ? imgA : imgD);
            UpdateSliderServerRpc(currentStationIndex, pushAmount);
            
            // Vẫn giữ lại Randomize nếu bạn muốn nút sáng đổi vị trí liên tục cho sinh động
            //RandomizeButtonServerRpc(); 
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
}