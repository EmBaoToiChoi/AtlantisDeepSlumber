using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.InputSystem;

public class MicManager : MonoBehaviour
{
    public static MicManager Instance { get; private set; }

    [Header("Settings")]
    public bool IsLocalTransmitting { get; private set; } = false;
    private bool _isMutedAuto = true;
    public bool IsMuted
    {
        get
        {
            if (transmissionMode == 0) // Push To Talk
            {
                return !Input.GetKey(pttKey);
            }
            return _isMutedAuto;
        }
        set
        {
            if (transmissionMode == 1)
            {
                _isMutedAuto = value;
            }
        }
    }
    public string selectedDevice = "";
    public int transmissionMode = 0; // 0 = Push To Talk, 1 = Auto (Voice Active)
    public float micInputVolume = 100f; // 0 - 100
    public float voicePlaybackVolume = 100f; // 0 - 100
    public KeyCode pttKey = KeyCode.V;
    public float autoThreshold = 0.015f; // RMS threshold for auto voice transmission

    [Header("Recording Config")]
    public int sampleRate = 16000;
    private const int ClipLengthSeconds = 10;

    private AudioClip _recordingClip;
    private int _lastSamplePosition = 0;
    private List<float> _accumulatedSamples = new List<float>();

    private bool _isNetworkHooked = false;
    private bool _isMessageHandlerRegistered = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    private void Start()
    {
        StartCoroutine(RecordingRoutine());
        StartCoroutine(AutoAttachPlaybackRoutine());
        StartCoroutine(HookNetworkManagerRoutine());
    }

    public void LoadSettings()
    {
        selectedDevice = PlayerPrefs.GetString("MicDevice", ""); // Empty string means Default
        transmissionMode = PlayerPrefs.GetInt("MicMode", 0);
        micInputVolume = PlayerPrefs.GetFloat("MicInputVolume", 100f);
        voicePlaybackVolume = PlayerPrefs.GetFloat("MicPlaybackVolume", 100f);
        pttKey = (KeyCode)PlayerPrefs.GetInt("MicPTTKey", (int)KeyCode.V);

        // Validate selected device is still connected (if not default)
        if (!string.IsNullOrEmpty(selectedDevice))
        {
            bool found = false;
            foreach (var dev in Microphone.devices)
            {
                if (dev == selectedDevice)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                selectedDevice = ""; // Fallback to Default
            }
        }
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetString("MicDevice", selectedDevice);
        PlayerPrefs.SetInt("MicMode", transmissionMode);
        PlayerPrefs.SetFloat("MicInputVolume", micInputVolume);
        PlayerPrefs.SetFloat("MicPlaybackVolume", voicePlaybackVolume);
        PlayerPrefs.SetInt("MicPTTKey", (int)pttKey);
        PlayerPrefs.Save();

        Debug.Log($"[MicManager] Settings Saved: Device={selectedDevice}, Mode={transmissionMode}, InputVol={micInputVolume}, PlaybackVol={voicePlaybackVolume}, PTTKey={pttKey}");

        // Restart recording with the new device
        RestartRecording();
    }

    public void RestartRecording()
    {
        StopRecording();
        StartCoroutine(RecordingRoutine());
    }

    private void StopRecording()
    {
        // For default device, Microphone.IsRecording uses empty string
        string devToStop = string.IsNullOrEmpty(selectedDevice) ? "" : selectedDevice;
        if (Microphone.IsRecording(devToStop))
        {
            Microphone.End(devToStop);
        }
        _recordingClip = null;
        _lastSamplePosition = 0;
        _accumulatedSamples.Clear();
    }

    private IEnumerator RecordingRoutine()
    {
        yield return new WaitForSeconds(0.5f); // Small delay on start

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[MicManager] No microphone devices found.");
            yield break;
        }

        string deviceToUse = string.IsNullOrEmpty(selectedDevice) ? "" : selectedDevice;

        // Query capabilities for specific device
        Microphone.GetDeviceCaps(deviceToUse, out int minFreq, out int maxFreq);
        if (minFreq > 0 && maxFreq > 0)
        {
            if (sampleRate < minFreq) sampleRate = minFreq;
            else if (sampleRate > maxFreq) sampleRate = maxFreq;
        }

        // Passing empty string uses default device
        _recordingClip = Microphone.Start(deviceToUse, true, ClipLengthSeconds, sampleRate);
        _lastSamplePosition = Microphone.GetPosition(deviceToUse);

        Debug.Log($"[MicManager] Started recording on [{(string.IsNullOrEmpty(deviceToUse) ? "System Default" : deviceToUse)}] at {sampleRate}Hz.");
    }

    private void Update()
    {
        // In Auto mode, pttKey toggles mute status
        if (transmissionMode == 1)
        {
            if (Input.GetKeyDown(pttKey))
            {
                _isMutedAuto = !_isMutedAuto;
                Debug.Log($"[MicManager] Auto Mode: Mic Muted toggled to: {_isMutedAuto} via {pttKey}");
            }
        }

        string deviceToUse = string.IsNullOrEmpty(selectedDevice) ? "" : selectedDevice;
        if (_recordingClip == null || !Microphone.IsRecording(deviceToUse))
        {
            IsLocalTransmitting = false;
            return;
        }

        int currentPosition = Microphone.GetPosition(deviceToUse);
        int sampleCount = 0;

        if (currentPosition > _lastSamplePosition)
        {
            sampleCount = currentPosition - _lastSamplePosition;
        }
        else if (currentPosition < _lastSamplePosition)
        {
            // Circular wrap-around
            sampleCount = (_recordingClip.samples - _lastSamplePosition) + currentPosition;
        }

        // Prevent overflow / catch up if we are lagging
        if (sampleCount > sampleRate * 2) // More than 2 seconds behind
        {
            _lastSamplePosition = currentPosition;
            _accumulatedSamples.Clear();
            IsLocalTransmitting = false;
            return;
        }

        if (sampleCount > 0)
        {
            float[] temp = new float[sampleCount];
            _recordingClip.GetData(temp, _lastSamplePosition);
            _accumulatedSamples.AddRange(temp);
            _lastSamplePosition = currentPosition;
        }

        // Process samples in 100ms chunks (1600 samples at 16000Hz)
        int chunkSize = Mathf.FloorToInt(sampleRate * 0.1f);
        while (_accumulatedSamples.Count >= chunkSize)
        {
            float[] chunk = new float[chunkSize];
            _accumulatedSamples.CopyTo(0, chunk, 0, chunkSize);
            _accumulatedSamples.RemoveRange(0, chunkSize);

            ProcessAudioChunk(chunk);
        }
    }

    private void ProcessAudioChunk(float[] chunk)
    {
        // Adjust input volume scale (0 to 1)
        float volumeFactor = micInputVolume / 100f;
        if (volumeFactor != 1f)
        {
            for (int i = 0; i < chunk.Length; i++)
            {
                chunk[i] *= volumeFactor;
            }
        }

        bool isTransmitting = false;

        // Check if transmission is active if not muted
        if (!IsMuted)
        {
            if (transmissionMode == 0) // Push To Talk
            {
                isTransmitting = Input.GetKey(pttKey);
            }
            else // Auto (Voice Activation)
            {
                // Calculate RMS
                float sum = 0;
                for (int i = 0; i < chunk.Length; i++)
                {
                    sum += chunk[i] * chunk[i];
                }
                float rms = Mathf.Sqrt(sum / chunk.Length);
                isTransmitting = rms > autoThreshold;
            }
        }

        IsLocalTransmitting = isTransmitting;

        if (isTransmitting && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsListening)
        {
            TransmitVoiceMessage(NetworkManager.Singleton.LocalClientId, chunk);
        }
    }

    private void TransmitVoiceMessage(ulong senderId, float[] samples)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;

        var writer = new FastBufferWriter(sizeof(ulong) + sizeof(int) + samples.Length * sizeof(float), Allocator.Temp);
        writer.WriteValueSafe(senderId);
        writer.WriteValueSafe(samples.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            writer.WriteValueSafe(samples[i]);
        }

        // Send to server
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            "VoiceChatMsg",
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.ReliableFragmentedSequenced
        );
        writer.Dispose();
    }

    private IEnumerator HookNetworkManagerRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);
            if (NetworkManager.Singleton != null && !_isNetworkHooked)
            {
                NetworkManager.Singleton.OnClientStarted += OnNetworkStarted;
                NetworkManager.Singleton.OnServerStarted += OnNetworkStarted;
                NetworkManager.Singleton.OnClientStopped += OnNetworkStopped;
                NetworkManager.Singleton.OnServerStopped += OnNetworkStopped;
                _isNetworkHooked = true;
                
                // If already running
                if (NetworkManager.Singleton.IsListening)
                {
                    OnNetworkStarted();
                }
            }
        }
    }

    private void OnNetworkStarted()
    {
        if (!_isMessageHandlerRegistered && NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("VoiceChatMsg", HandleVoiceMessage);
            _isMessageHandlerRegistered = true;
            Debug.Log("[MicManager] NGO Custom Message handler registered for 'VoiceChatMsg'.");
        }
    }

    private void OnNetworkStopped(bool isHost)
    {
        _isMessageHandlerRegistered = false;
    }

    private void HandleVoiceMessage(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong voiceSenderId);
        reader.ReadValueSafe(out int length);
        float[] samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            reader.ReadValueSafe(out samples[i]);
        }

        if (NetworkManager.Singleton == null) return;

        // 1. Relayer: if we are server, send to all other clients
        if (NetworkManager.Singleton.IsServer)
        {
            RelayVoiceMessage(voiceSenderId, samples);
        }

        // 2. Playback: if we are client and this isn't our own voice
        if (NetworkManager.Singleton.IsClient && voiceSenderId != NetworkManager.Singleton.LocalClientId)
        {
            PlayVoiceSample(voiceSenderId, samples);
        }
    }

    private void RelayVoiceMessage(ulong voiceSenderId, float[] samples)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;

        var writer = new FastBufferWriter(sizeof(ulong) + sizeof(int) + samples.Length * sizeof(float), Allocator.Temp);
        writer.WriteValueSafe(voiceSenderId);
        writer.WriteValueSafe(samples.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            writer.WriteValueSafe(samples[i]);
        }

        foreach (ulong targetClientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // Do not send back to original voice speaker, and don't send to server itself
            if (targetClientId == voiceSenderId || targetClientId == NetworkManager.ServerClientId)
                continue;

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                "VoiceChatMsg",
                targetClientId,
                writer,
                NetworkDelivery.ReliableFragmentedSequenced
            );
        }
        writer.Dispose();
    }

    private void PlayVoiceSample(ulong voiceSenderId, float[] samples)
    {
        var netObjs = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (var netObj in netObjs)
        {
            if (netObj.IsPlayerObject && netObj.OwnerClientId == voiceSenderId)
            {
                var playback = netObj.GetComponent<PlayerVoicePlayback>();
                if (playback != null)
                {
                    // Đảm bảo client ID và isLocalPlayer luôn được gán đúng
                    playback.ownerClientId = voiceSenderId;
                    playback.isLocalPlayer = (NetworkManager.Singleton != null && voiceSenderId == NetworkManager.Singleton.LocalClientId);
                    
                    playback.QueueSamples(samples);
                }
                break;
            }
        }
    }

    private IEnumerator AutoAttachPlaybackRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                var netObjs = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
                foreach (var netObj in netObjs)
                {
                    if (netObj.IsPlayerObject && netObj.IsSpawned)
                    {
                        var playback = netObj.GetComponent<PlayerVoicePlayback>();
                        if (playback == null)
                        {
                            playback = netObj.gameObject.AddComponent<PlayerVoicePlayback>();
                            Debug.Log($"[MicManager] Auto-attached PlayerVoicePlayback to ClientId={netObj.OwnerClientId}, isLocal={netObj.IsOwner}");
                        }

                        // Đảm bảo ownerClientId và isLocalPlayer luôn được cập nhật chính xác (ngay cả khi script đã được gán sẵn từ trước trong Editor)
                        if (playback.ownerClientId != netObj.OwnerClientId || playback.isLocalPlayer != netObj.IsOwner)
                        {
                            playback.ownerClientId = netObj.OwnerClientId;
                            playback.isLocalPlayer = netObj.IsOwner;
                            Debug.Log($"[MicManager] Configured PlayerVoicePlayback for ClientId={netObj.OwnerClientId}, isLocal={netObj.IsOwner}");
                        }
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        StopRecording();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStarted -= OnNetworkStarted;
            NetworkManager.Singleton.OnServerStarted -= OnNetworkStarted;
            NetworkManager.Singleton.OnClientStopped -= OnNetworkStopped;
            NetworkManager.Singleton.OnServerStopped -= OnNetworkStopped;
        }
    }
}
