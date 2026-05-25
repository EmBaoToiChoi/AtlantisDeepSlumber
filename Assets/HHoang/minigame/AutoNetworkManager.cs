using Unity.Netcode;
using UnityEngine;

public class AutoNetworkManager : MonoBehaviour
{
    void Start()
    {
        // Kiểm tra xem có tham số dòng lệnh "host" không
        // Nếu muốn test nhanh, bạn có thể chỉnh mặc định ở đây
        if (SystemInfo.deviceType == DeviceType.Desktop)
        {
            // Mặc định nhấn Play là làm Host
            NetworkManager.Singleton.StartHost();
            Debug.Log("Started as Host");
        }
        else
        {
            // Hoặc bạn có thể thêm logic để tự join Client
            NetworkManager.Singleton.StartClient();
            Debug.Log("Started as Client");
        }
    }
}