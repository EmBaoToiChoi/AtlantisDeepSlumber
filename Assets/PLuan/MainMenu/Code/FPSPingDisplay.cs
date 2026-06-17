using UnityEngine;
using Unity.Netcode;

public class FPSPingDisplay : MonoBehaviour
{
    private static FPSPingDisplay _instance;

    private float _fps = 0f;
    private float _deltaTime = 0f;
    private ulong _ping = 0;
    private float _pingUpdateTimer = 0f;

    private Texture2D _backgroundTexture;
    private GUIStyle _style;

    private void Start()
    {
        // Tạo hình nền tối màu mờ để chữ hiển thị tương phản tốt trên mọi map
        _backgroundTexture = new Texture2D(1, 1);
        _backgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
        _backgroundTexture.Apply();
    }

    private void OnDestroy()
    {
        if (_backgroundTexture != null)
        {
            Destroy(_backgroundTexture);
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Tính FPS mượt mà
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        _fps = _deltaTime > 0f ? (1.0f / _deltaTime) : 0f;

        // Cập nhật Ping mỗi 1 giây để tránh lãng phí hiệu năng hệ thống
        _pingUpdateTimer += Time.unscaledDeltaTime;
        if (_pingUpdateTimer >= 1.0f)
        {
            _pingUpdateTimer = 0f;
            UpdatePing();
        }
    }

    private void UpdatePing()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsListening)
        {
            try
            {
                // Trong Netcode for GameObjects, Server ClientId luôn là 0
                _ping = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(0);
            }
            catch
            {
                _ping = 0;
            }
        }
        else
        {
            _ping = 0;
        }
    }

    private void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle();
            _style.alignment = TextAnchor.UpperLeft;
            _style.fontSize = 14;
            _style.normal.textColor = Color.green;
            _style.normal.background = _backgroundTexture;
            _style.padding = new RectOffset(8, 8, 8, 8);
        }

        // Khung hiển thị ở góc trên bên trái
        Rect rect = new Rect(10, 10, 250, 75);

        string networkRole = "OFFLINE";
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsClient)
                networkRole = "HOST";
            else if (NetworkManager.Singleton.IsServer)
                networkRole = "SERVER";
            else if (NetworkManager.Singleton.IsClient)
                networkRole = "CLIENT";
        }

        string pingText = (networkRole == "CLIENT") ? $"{_ping} ms" : "N/A (Host/Server)";
        string text = $"FPS: {Mathf.RoundToInt(_fps)}\nPING: {pingText}\nROLE: {networkRole}";

        GUI.Box(rect, text, _style);
    }
}
