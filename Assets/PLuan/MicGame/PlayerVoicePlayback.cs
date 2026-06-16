using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerVoicePlayback : MonoBehaviour
{
    // Static dictionary to save custom volume levels per player (ClientId -> Volume Multiplier 0.0f - 1.0f)
    public static readonly Dictionary<ulong, float> PlayerVolumeMultipliers = new Dictionary<ulong, float>();

    private AudioSource _audioSource;
    private readonly Queue<float> _playbackQueue = new Queue<float>();
    private const int MaxQueueSize = 32000; // Max 2 seconds of buffer
    
    public ulong ownerClientId;
    public bool isLocalPlayer = false;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure audio source for 2D voice chat
        _audioSource.spatialBlend = 0.0f;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume = 1.0f;
    }

    private void Start()
    {
        // Start playing silent clip to trigger OnAudioFilterRead
        if (_audioSource.clip == null)
        {
            _audioSource.clip = AudioClip.Create("SilentVoiceClip", 16000, 1, 16000, false);
            float[] silentData = new float[16000];
            _audioSource.clip.SetData(silentData, 0);
        }
        _audioSource.Play();
    }

    public void QueueSamples(float[] samples)
    {
        if (isLocalPlayer) return;

        lock (_playbackQueue)
        {
            if (_playbackQueue.Count < MaxQueueSize)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    _playbackQueue.Enqueue(samples[i]);
                }
            }
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (isLocalPlayer)
        {
            // Silence local player voice playback
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        float playbackVolume = 1f;
        
        // Use individual player volume multiplier if specified, otherwise fall back to global volume setting
        if (PlayerVolumeMultipliers.TryGetValue(ownerClientId, out float customVolume))
        {
            playbackVolume = customVolume;
        }
        else if (MicManager.Instance != null)
        {
            playbackVolume = MicManager.Instance.voicePlaybackVolume / 100f;
        }

        int queueCount = 0;
        lock (_playbackQueue)
        {
            queueCount = _playbackQueue.Count;
        }

        // Catch up logic: if buffer is getting large, read samples faster to reduce latency
        bool catchUp = queueCount > 8000; // >500ms latency

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;
            lock (_playbackQueue)
            {
                if (_playbackQueue.Count > 0)
                {
                    sample = _playbackQueue.Dequeue() * playbackVolume;
                    if (catchUp && _playbackQueue.Count > 0)
                    {
                        // Skip a sample to catch up
                        _playbackQueue.Dequeue();
                    }
                }
            }

            for (int c = 0; c < channels; c++)
            {
                data[i + c] = sample;
            }
        }
    }
}
