using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Threading.Tasks;

public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    [Header("Network Settings")]
    public string serverIP = "165.99.14.40";
    public ushort serverPort = 7777;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Tự động chạy Server nếu là bản build Headless (chạy trên VPS)
        if (IsHeadless())
        {
            Debug.Log("[NET] Headless mode detected. Starting Dedicated Server...");
            StartDedicatedServer();
        }
    }

    private bool IsHeadless()
    {
        // Kiểm tra nếu game đang chạy không có màn hình (Dedicated Server build)
        return SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }

    public void StartDedicatedServer()
    {
        ConfigureTransport("0.0.0.0"); // Nghe trên mọi IP của VPS
        if (NetworkManager.Singleton.StartServer())
        {
            Debug.Log("[NET] Dedicated Server Started on VPS.");
            // Chỉ Server mới được phép chuyển Scene qua Network
            NetworkManager.Singleton.SceneManager.LoadScene("Waiting hall", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public async void StartClientAsPlayer()
    {
        ConfigureTransport(serverIP);
        
        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log($"[NET] Connecting to VPS: {serverIP}...");
            // Hiện màn hình loading trong khi chờ kết nối
            await SceneLoader.Instance.LoadSceneAsync("Waiting hall", "CONNECTING TO ATLANTIS SERVER...");
        }
    }


    private void ConfigureTransport(string ip)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = ip;
            transport.ConnectionData.Port = serverPort;
            // Tăng độ ổn định cho kết nối quốc tế/VPS
            transport.MaxConnectAttempts = 5;
            transport.ConnectTimeoutMS = 5000;
        }
    }
}
