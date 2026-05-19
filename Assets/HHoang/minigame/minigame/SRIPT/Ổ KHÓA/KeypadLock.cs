using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class KeypadLock : NetworkBehaviour
{
    public static KeypadLock Instance { get; private set; }

    [Header("Cấu hình Cửa")]
    [Tooltip("Kéo cánh cửa hoặc cụm Object chứa cửa vào đây để ẩn đi khi mở khóa")]
    public GameObject doorObject;

    // Lưu chuỗi mật mã đúng dưới dạng NetworkVariable
    private NetworkVariable<Unity.Collections.FixedString32Bytes> correctPassword = 
        new NetworkVariable<Unity.Collections.FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool isDoorOpened = false;
    private bool isPlayerNear = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Bắt đầu xử lý lấy mật mã cố định từ danh sách gốc ban đầu
        StartCoroutine(GeneratePasswordFromBooks());
    }

    private IEnumerator GeneratePasswordFromBooks()
    {
        // Chờ 1 Frame để đảm bảo BookManager trong Scene đã khởi tạo xong hoàn toàn ngoài Inspector
        yield return null;

        BookManager bookManager = FindFirstObjectByType<BookManager>();
        if (bookManager != null && bookManager.realNumbers != null && bookManager.realNumbers.Count >= 5)
        {
            string pass = "";
            
            // ĐÃ SỬA CHUẨN: Lấy nguyên bản thứ tự từ List realNumbers gốc mà ông đã sắp xếp trong Inspector 
            // (Ví dụ theo ảnh: Element 0 = 3, Element 1 = 1, Element 2 = 5... -> Mật mã luôn là "31526")
            for (int i = 0; i < 5; i++)
            {
                pass += bookManager.realNumbers[i].ToString();
            }
            
            correctPassword.Value = pass;
            Debug.Log($"<color=cyan>[Server] Mật mã mở cửa CỐ ĐỊNH THEO INSPECTOR: {pass}</color>");
        }
        else
        {
            Debug.LogError("[KeypadLock] Không tìm thấy BookManager hoặc danh sách realNumbers chưa cấu hình đủ 5 số!");
        }
    }

    // RPC nhận chuỗi số từ Client gửi lên để Server check đúng/sai
    [ServerRpc(RequireOwnership = false)]
    public void CheckPasswordServerRpc(string inputCode, ulong clientId)
    {
        if (isDoorOpened) return;

        if (inputCode == correctPassword.Value.ToString())
        {
            Debug.Log($"<color=green>[Server] Client {clientId} đã nhập ĐÚNG mật mã: {inputCode}! Đang mở cửa...</color>");
            isDoorOpened = true;
            OpenDoorClientRpc();
        }
        else
        {
            Debug.Log($"<color=red>[Server] Client {clientId} nhập SAI mật mã: {inputCode}</color>");
            
            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
            NotifyWrongPasswordClientRpc(rpcParams);
        }
    }

    [ClientRpc]
    private void OpenDoorClientRpc()
    {
        isDoorOpened = true;

        if (doorObject != null)
        {
            doorObject.SetActive(false); // Ẩn cụm cửa chặn đường
        }

        if (KeypadUIManager.Instance != null)
        {
            KeypadUIManager.Instance.CloseKeypadUI();
            if (SimpleBookUIManager.Instance != null && SimpleBookUIManager.Instance.matchCodeText != null)
            {
                SimpleBookUIManager.Instance.matchCodeText.text += "\n<color=green>CỬA ĐÃ MỞ! CHẠY ĐI!!!</color>";
            }
        }

        Debug.Log("[Client] Cửa đã mở thành công!");
    }

    [ClientRpc]
    private void NotifyWrongPasswordClientRpc(ClientRpcParams rpcParams = default)
    {
        if (KeypadUIManager.Instance != null)
        {
            KeypadUIManager.Instance.OnInputWrong();
        }
    }

    private void Update()
    {
        if (isPlayerNear && !isDoorOpened && Input.GetKeyDown(KeyCode.F))
        {
            if (KeypadUIManager.Instance != null)
            {
                KeypadUIManager.Instance.OpenKeypadUI();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsLocalPlayer) isPlayerNear = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsLocalPlayer)
            {
                isPlayerNear = false;
                if (KeypadUIManager.Instance != null) KeypadUIManager.Instance.CloseKeypadUI();
            }
        }
    }
}