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

    private bool hasCrystalLocal = false;

    public bool IsCrystalPlaced => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? hasCrystal.Value : hasCrystalLocal;

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
        if (IsCrystalPlaced) return;

        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null && interaction.HasCrystalInInventory) 
        {
            bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (isNetworkActive)
            {
                PlaceCrystalServerRpc(player.GetComponent<NetworkObject>().NetworkObjectId);
            }
            else
            {
                // Chơi đơn (Offline)
                interaction.RemoveCrystal();
                hasCrystalLocal = true;
                if (crystalVisual != null) crystalVisual.SetActive(true);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceCrystalServerRpc(ulong playerNetworkId, ServerRpcParams rpcParams = default)
    {
        if (hasCrystal.Value) return; 

        // Lấy clientId của người gửi request để gửi lệnh xóa ngọc về đúng client đó
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        RemoveCrystalFromOwnerClientRpc(playerNetworkId, senderClientId);

        hasCrystal.Value = true;
    }

    /// <summary>
    /// Gửi lệnh xóa ngọc khỏi inventory về đúng client sở hữu player.
    /// Chỉ client có LocalClientId == targetClientId mới thực thi.
    /// </summary>
    [ClientRpc]
    private void RemoveCrystalFromOwnerClientRpc(ulong playerNetworkId, ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
        {
            PlayerInteraction interaction = playerObj.GetComponent<PlayerInteraction>();
            if (interaction != null) interaction.RemoveCrystal();
        }
    }

    private void Update()
    {
        // Cả 2 bệ ngọc đều phải được đặt ngọc mới mở cửa
        bool otherPlaced = otherPedestal != null && otherPedestal.IsCrystalPlaced;
        if (!IsCrystalPlaced || !otherPlaced) return;

        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool isServerInstance = IsServer;

        for (int i = 0; i < doorsToControl.Length; i++)
        {
            if (doorsToControl[i] == null || targetPositions.Length <= i || targetPositions[i] == null) continue;

            // Nếu cửa có đồng bộ vị trí tự động qua mạng (NetworkTransform), chỉ có Server mới di chuyển cửa
            var netObj = doorsToControl[i].GetComponent<NetworkObject>();
            var hasNetTransform = doorsToControl[i].GetComponent<Unity.Netcode.Components.NetworkTransform>() != null;
            if (isNetworkActive && netObj != null && hasNetTransform && !isServerInstance)
            {
                continue; // Client bỏ qua, để NetworkTransform đồng bộ từ Server xuống
            }

            // Di chuyển cửa từ vị trí hiện tại tới VỊ TRÍ ĐÍCH ĐẾN (targetPositions)
            doorsToControl[i].position = Vector3.MoveTowards(doorsToControl[i].position, targetPositions[i].position, slideSpeed * Time.deltaTime);
        }
    }
}