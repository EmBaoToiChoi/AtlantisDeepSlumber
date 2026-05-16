using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Text;

[System.Serializable]
public class AuthResponse
{
    public bool success;
    public string message;
    public string token;
    public string displayName;
    public string email;
    public bool needsVerification;
}



[System.Serializable]
public class RoomPlayer
{
    public string user;
    public string displayName;
    public int slot;
}

[System.Serializable]
public class RoomData
{
    public string _id;
    public string roomId;
    public string roomName;
    public string host;
    public bool isPrivate;
    public int maxPlayers;
    public string status;
    public RoomPlayer[] players;
}

[System.Serializable]
public class RoomResponse
{
    public bool success;
    public string message;
    public RoomData room;
}

[System.Serializable]
public class RoomListResponse
{
    public bool success;
    public string message;
    public RoomData[] rooms;
}


[System.Serializable]
public class CreateRoomRequest
{
    public string roomName;
    public bool isPrivate;
    public string password;
}

[System.Serializable]
public class JoinRoomRequest
{
    public string roomId;
    public string password;
}


[System.Serializable]
public class RegisterRequest
{
    public string displayName;
    public string email;
    public string password;
}

[System.Serializable]
public class LoginRequest
{
    public string email;
    public string password;
}

[System.Serializable]
public class VerifyOTPRequest
{
    public string email;
    public string otp;
}

[System.Serializable]
public class ResendOTPRequest
{
    public string email;
}

public static class AuthService
{
    // Có thể cấu hình URL này qua config file sau này khi deploy
    private static readonly string BASE_URL = "http://165.99.14.40:3000/api";

    public static async Task<AuthResponse> Register(string displayName, string email, string password)
    {
        var reqObj = new RegisterRequest { displayName = displayName, email = email, password = password };
        string json = JsonUtility.ToJson(reqObj);
        return await SendRequest($"{BASE_URL}/register", json);
    }

    public static async Task<AuthResponse> Login(string email, string password)
    {
        var reqObj = new LoginRequest { email = email, password = password };
        string json = JsonUtility.ToJson(reqObj);
        return await SendRequest($"{BASE_URL}/login", json);
    }

    public static async Task<AuthResponse> VerifyOTP(string email, string otp)
    {
        var reqObj = new VerifyOTPRequest { email = email, otp = otp };
        string json = JsonUtility.ToJson(reqObj);
        return await SendRequest($"{BASE_URL}/verify-otp", json);
    }

    public static async Task<AuthResponse> ResendOTP(string email)
    {
        var reqObj = new ResendOTPRequest { email = email };
        string json = JsonUtility.ToJson(reqObj);
        return await SendRequest($"{BASE_URL}/resend-otp", json);
    }

    // --- Room APIs ---

    public static async Task<RoomResponse> CreateRoom(string roomName, bool isPrivate, string password)
    {
        var reqObj = new CreateRoomRequest { roomName = roomName, isPrivate = isPrivate, password = password };
        string json = JsonUtility.ToJson(reqObj);
        return await SendRoomRequest($"{BASE_URL}/rooms/create", "POST", json);
    }

    public static async Task<RoomResponse> JoinRoom(string roomId, string password)
    {
        var reqObj = new JoinRoomRequest { roomId = roomId, password = password };
        string json = JsonUtility.ToJson(reqObj);
        return await SendRoomRequest($"{BASE_URL}/rooms/join", "POST", json);
    }

    public static async Task<RoomResponse> GetRoomStatus(string roomId)
    {
        return await SendRoomRequest($"{BASE_URL}/rooms/{roomId}", "GET", null);
    }

    public static async Task<RoomListResponse> GetRooms()
    {
        using (UnityWebRequest req = UnityWebRequest.Get($"{BASE_URL}/rooms"))
        {
            // Thêm Authorization header nếu có token
            string token = PlayerPrefs.GetString("AuthToken", "");
            if (!string.IsNullOrEmpty(token))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
            }

            var operation = req.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AUTH SERVICE] GetRooms Error: {req.error}");
                return new RoomListResponse { success = false, message = req.error };
            }

            string json = req.downloadHandler.text;
            Debug.Log($"[AUTH SERVICE] GetRooms Response: {json}");

            try
            {
                return JsonUtility.FromJson<RoomListResponse>(json);
            }
            catch
            {
                return new RoomListResponse { success = false, message = "Parse Error" };
            }
        }
    }



    public static async Task<RoomResponse> LeaveRoom(string roomId)
    {
        string json = "{\"roomId\":\"" + roomId + "\"}";
        return await SendRoomRequest($"{BASE_URL}/rooms/leave", "POST", json);
    }


    private static async Task<RoomResponse> SendRoomRequest(string url, string method, string jsonBody)

    {
        using (UnityWebRequest req = new UnityWebRequest(url, method))
        {
            if (!string.IsNullOrEmpty(jsonBody))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            // Thêm Authorization header nếu có token
            string token = PlayerPrefs.GetString("AuthToken", "");
            if (!string.IsNullOrEmpty(token))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
            }

            var operation = req.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            string responseText = req.downloadHandler.text;
            if (req.result == UnityWebRequest.Result.ConnectionError)
                return new RoomResponse { success = false, message = "Network Error!" };

            try
            {
                return JsonUtility.FromJson<RoomResponse>(responseText);
            }
            catch
            {
                return new RoomResponse { success = false, message = "Parse error: " + responseText };
            }
        }
    }


    private static async Task<AuthResponse> SendRequest(string url, string jsonBody)
    {
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var operation = req.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            // Kể cả khi backend trả về lỗi (400, 401, v.v.), ta vẫn có thể có chuỗi JSON trả về chứa message
            string responseText = req.downloadHandler.text;
            
            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                return new AuthResponse { success = false, message = "Network Error! Cannot connect to server." };
            }

            try
            {
                if (string.IsNullOrEmpty(responseText))
                {
                    return new AuthResponse { success = false, message = "Server returned empty response." };
                }
                AuthResponse response = JsonUtility.FromJson<AuthResponse>(responseText);
                return response;
            }
            catch
            {
                return new AuthResponse { success = false, message = "Server response parse error." };
            }
        }
    }
}
