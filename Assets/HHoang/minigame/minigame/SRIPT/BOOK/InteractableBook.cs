using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public class InteractableBook : NetworkBehaviour
{
    private NetworkVariable<FixedString32Bytes> netBookSymbol = new NetworkVariable<FixedString32Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        
    private NetworkVariable<int> netBookNumber = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> netIsRealBook = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool isPlayerInside = false;

    public void SetupBookData(string symbol, int number, bool isReal)
    {
        if (!IsServer) return; 
        netBookSymbol.Value = symbol;
        netBookNumber.Value = number;
        netIsRealBook.Value = isReal;
    }

    private void Update()
    {
        if (isPlayerInside && Input.GetKeyDown(KeyCode.F))
        {
            ulong localPlayerId = NetworkManager.Singleton.LocalClientId;
            
            // Gửi tín hiệu nhặt lên Server xử lý dữ liệu gốc chuẩn nhất
            InteractBookServerRpc(localPlayerId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void InteractBookServerRpc(ulong pickerClientId)
    {
        // Server lấy dữ liệu chuẩn xác tuyệt đối đang được lưu trữ an toàn trong NetworkVariable
        string symbol = netBookSymbol.Value.ToString();
        int number = netBookNumber.Value;
        bool isReal = netIsRealBook.Value;

        Debug.Log($"[Server] Client {pickerClientId} nhặt thành công cuốn sách: {symbol} = {number}");

        if (SimpleBookUIManager.Instance != null)
        {
            // Phát ClientRpc truyền dữ liệu mạng chuẩn xuống cho tất cả Client hiển thị UI
            SimpleBookUIManager.Instance.SyncBookPickupClientRpc(symbol, number, isReal, pickerClientId);
        }

        // Xóa cuốn sách khỏi mạng
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Despawn(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            if (playerNetObj != null && playerNetObj.IsLocalPlayer)
            {
                isPlayerInside = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            if (playerNetObj != null && playerNetObj.IsLocalPlayer)
            {
                isPlayerInside = false;
            }
        }
    }
}