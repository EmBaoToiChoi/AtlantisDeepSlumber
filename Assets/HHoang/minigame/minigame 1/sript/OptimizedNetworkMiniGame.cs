using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.InputSystem; // Dòng này là bắt buộc

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
// Trong OptimizedNetworkMiniGame.cs
// SỬA: Truyền giá trị mặc định cho tất cả NetworkVariable
    public NetworkVariable<bool> station2HasCrystal = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> station3HasCrystal = new NetworkVariable<bool>(false);

    private NetworkList<float> stationValues = new NetworkList<float>();
    private NetworkList<ulong> stationOwners = new NetworkList<ulong>();
    
    private int currentStationIndex = 0;
    private bool isPlaying = false;
    private bool isCurrentlyOpen = false;

    // Thêm vào OptimizedNetworkMiniGame.cs
    [ServerRpc(RequireOwnership = false)]
    public void SetStationCrystalStatusServerRpc(int index, bool hasCrystal)
    {
        if (!IsServer) return; // Chỉ Server mới được phép ghi
        
        if (index == 2) station2HasCrystal.Value = hasCrystal;
        else if (index == 3) station3HasCrystal.Value = hasCrystal;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // 1. Chỉ khởi tạo giá trị ban đầu nếu danh sách chưa có gì.
            // Đây là cách an toàn nhất để tránh việc mỗi lần spawn đối tượng lại reset về 0.
            if (stationValues.Count == 0)
            {
                for (int i = 0; i < 4; i++) 
                {
                    stationValues.Add(0f);
                    stationOwners.Add(ulong.MaxValue);
                }
            }
            
            // 2. Không cần gán lại giá trị cho NetworkVariable ở đây 
            // vì bạn đã có giá trị mặc định ở dòng khai báo (new NetworkVariable<bool>(false)).
        }
        
        // Tắt UI zone khi bắt đầu
        if (miniGamePlayZone != null) miniGamePlayZone.SetActive(false);
    }

    void Update()
    {
        // 1. Kiểm tra sự sẵn sàng của NetworkList trước khi truy cập (Dành cho Client & Server)
        bool isListReady = stationValues != null && stationValues.Count > 0;

        if (isPlaying && isListReady)
        {
            if (localSlider != null && currentStationIndex < stationValues.Count) 
                localSlider.value = stationValues[currentStationIndex];
            
            imgA.color = activeColor;
            imgD.color = activeColor;
            
            HandleQTEInput();
        }

        // 2. Logic phía Server
        if (IsServer && isListReady)
        {
            // Duyệt danh sách và xử lý drain
            for (int i = 0; i < stationValues.Count; i++)
            {
                float currentDrain = drainSpeed;
                
                // Kiểm tra chỉ số trạm (Tránh index ngoài phạm vi nếu thay đổi cấu trúc)
                if ((i == 2 && !station2HasCrystal.Value) || (i == 3 && !station3HasCrystal.Value))
                    currentDrain *= 2f; 

                if (stationValues[i] > 0) 
                    stationValues[i] -= currentDrain * Time.deltaTime;
            }

            // Logic mở cổng
            bool pairA_Ready = (stationOwners[0] != ulong.MaxValue && stationValues[0] >= greenZoneMin) &&
                            (stationOwners[1] != ulong.MaxValue && stationValues[1] >= greenZoneMin);
            bool pairB_Ready = (stationOwners[2] != ulong.MaxValue && stationValues[2] >= greenZoneMin) &&
                            (stationOwners[3] != ulong.MaxValue && stationValues[3] >= greenZoneMin);
            
            bool shouldBeOpen = pairA_Ready || pairB_Ready;

            // Chỉ trigger khi có thay đổi trạng thái (tránh lặp lại lệnh gọi gear mỗi frame)
            if (shouldBeOpen != isCurrentlyOpen)
            {
                isCurrentlyOpen = shouldBeOpen;
                foreach (var gear in gearList) 
                {
                    if (gear == null) continue;
                    
                    if (isCurrentlyOpen)
                    {
                        gear.SetSpeed(gear.rotationSpeed * (gear.reverseDirection ? -1f : 1f));
                        gear.OpenGear();
                    }
                    else
                    {
                        gear.CloseGear();
                    }
                }
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
        currentStationIndex = index;
        isPlaying = isOpening;
        if (miniGamePlayZone != null) 
        {
            miniGamePlayZone.SetActive(isOpening);
        }
        else 
        {
            Debug.LogWarning("MiniGamePlayZone chưa được gán trong Inspector!");
        }
    }

    void HandleQTEInput()
    {
        // Chỉ xử lý input nếu người chơi này đang sở hữu mini-game này (hoặc đang tương tác)
        // Bạn nên truyền thông tin ClientID vào hoặc kiểm tra quyền sở hữu ở đây
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame) ProcessInput(true);
            else if (Keyboard.current.dKey.wasPressedThisFrame) ProcessInput(false);
        }
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