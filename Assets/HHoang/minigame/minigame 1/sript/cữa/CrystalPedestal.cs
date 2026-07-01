using Unity.Netcode;
using UnityEngine;

public class CrystalPuzzleSystem : NetworkBehaviour
{
    [Header("--- CẤU HÌNH CHO TRỤ ---")]
    public GameObject crystalVisual; 
    public CrystalPuzzleSystem otherPedestal; 

    [Header("--- CẤU HÌNH ĐIỀU KHIỂN CỬA ---")]
    [Tooltip("Kéo 2 cánh cửa (cục đá) vào đây")]
    public Transform[] doorsToControl; 
    
    [Tooltip("Kéo 2 cái Empty GameObject làm điểm ĐÍCH ĐẾN (chỗ cửa đóng) vào đây")]
    public Transform[] targetPositions; 
    
    public float slideSpeed = 2f;

    public NetworkVariable<bool> hasCrystal = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private bool isDoorsMoving = false;

    public override void OnNetworkSpawn()
    {
        hasCrystal.OnValueChanged += OnCrystalStateChanged;
        if (crystalVisual != null) crystalVisual.SetActive(hasCrystal.Value);
    }

    public override void OnNetworkDespawn()
    {
        hasCrystal.OnValueChanged -= OnCrystalStateChanged;
    }

    private void OnCrystalStateChanged(bool previousValue, bool newValue)
    {
        if (crystalVisual != null) crystalVisual.SetActive(newValue);
    }

    // Khi ấn F
    public void InteractWithPedestal(GameObject player)
    {
        if (hasCrystal.Value) return;

        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null && interaction.HasCrystalInInventory) 
        {
            PlaceCrystalServerRpc(player.GetComponent<NetworkObject>().NetworkObjectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceCrystalServerRpc(ulong playerNetworkId)
    {
        if (hasCrystal.Value) return; 

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
        {
            PlayerInteraction interaction = playerObj.GetComponent<PlayerInteraction>();
            if (interaction != null) interaction.RemoveCrystal(); 
        }

        hasCrystal.Value = true;

        if (hasCrystal.Value && otherPedestal != null && otherPedestal.hasCrystal.Value)
        {
            isDoorsMoving = true;
            otherPedestal.StartMovingDoorsFromOther();
        }
    }

    public void StartMovingDoorsFromOther()
    {
        if (!IsServer) return;
        isDoorsMoving = true;
    }

    private void Update()
    {
        if (!IsServer || !isDoorsMoving) return;

        bool allDoorsReachedTarget = true;

        for (int i = 0; i < doorsToControl.Length; i++)
        {
            if (doorsToControl[i] == null || targetPositions.Length <= i || targetPositions[i] == null) continue;

            // Di chuyển cửa từ vị trí hiện tại tới VỊ TRÍ ĐÍCH ĐẾN (targetPositions)
            doorsToControl[i].position = Vector3.MoveTowards(doorsToControl[i].position, targetPositions[i].position, slideSpeed * Time.deltaTime);

            if (Vector3.Distance(doorsToControl[i].position, targetPositions[i].position) > 0.001f)
            {
                allDoorsReachedTarget = false;
            }
            else
            {
                doorsToControl[i].position = targetPositions[i].position; 
            }
        }

        if (allDoorsReachedTarget)
        {
            isDoorsMoving = false;
        }
    }
}