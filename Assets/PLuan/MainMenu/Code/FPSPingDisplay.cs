using UnityEngine;
using Unity.Netcode;

public class FPSPingDisplay : MonoBehaviour
{
    private static FPSPingDisplay _instance;

    private float _fps = 0f;
    private float _deltaTime = 0f;
    private ulong _ping = 0;
    private float _pingUpdateTimer = 0f;

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
        // Khung hiển thị ở góc trên bên trái
        GUIStyle style = new GUIStyle();
        Rect rect = new Rect(10, 10, 250, 75);
        
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 14;
        style.normal.textColor = Color.green;

        // Tạo hình nền tối màu mờ để chữ hiển thị tương phản tốt trên mọi map
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
        texture.Apply();
        style.normal.background = texture;
        style.padding = new RectOffset(8, 8, 8, 8);

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

        GUI.Box(rect, text, style);
    }
}
