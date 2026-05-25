using Unity.Netcode;
using UnityEngine;

public class AutoNetworkManager : MonoBehaviour
{
    // Kéo thả vào Inspector để chọn chế độ khi nhấn Play
    public bool isHost = true; 

    void Start()
    {
        if (isHost)
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("Đã bắt đầu làm HOST");
        }
        else
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("Đã bắt đầu làm CLIENT");
        }
    }
}