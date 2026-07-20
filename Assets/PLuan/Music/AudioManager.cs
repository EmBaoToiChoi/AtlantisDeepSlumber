using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;

    [Header("Default Audio Clips")]
    [SerializeField] private AudioClip _defaultBgmClip;

    // Volume settings scaled from 0.0 to 1.0
    public float MasterVolume { get; private set; } = 1.0f;
    public float MusicVolume { get; private set; } = 0.8f;
    public float SFXVolume { get; private set; } = 0.9f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Debug.Log($"[AudioManager] SceneLoaded: '{scene.name}'");
        if (IsMenuOrLobbyScene(scene.name))
        {
            if (_bgmSource != null && !_bgmSource.isPlaying)
            {
                PlayBGM("Audio/BGM");
            }
        }
        else
        {
            Debug.Log($"[AudioManager] Entering Gameplay Scene '{scene.name}' - Stopping background music for combat/SFX clarity.");
            StopBGM();
        }
    }

    public bool IsMenuOrLobbyScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        string lower = sceneName.ToLower().Replace(" ", "").Replace("_", "");
        return lower.Contains("mainmenu") || lower.Contains("menu") || lower.Contains("lobby") || lower.Contains("waiting");
    }

    private void InitializeAudioManager()
    {
        // Add AudioSource for BGM if not assigned
        if (_bgmSource == null)
        {
            _bgmSource = GetComponent<AudioSource>();
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Configure BGM AudioSource
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;
        _bgmSource.spatialBlend = 0f; // 2D Sound for background music

        // Load saved volume preferences
        LoadVolumeSettings();
    }

    public void LoadVolumeSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 100f) / 100f;
        MusicVolume = PlayerPrefs.GetFloat("MusicVolume", 80f) / 100f;
        SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 90f) / 100f;

        ApplyMusicVolume();
    }

    public void SetVolumes(float masterPercent, float musicPercent, float sfxPercent, bool saveToPrefs = true)
    {
        MasterVolume = Mathf.Clamp01(masterPercent / 100f);
        MusicVolume = Mathf.Clamp01(musicPercent / 100f);
        SFXVolume = Mathf.Clamp01(sfxPercent / 100f);

        if (saveToPrefs)
        {
            // Save to PlayerPrefs
            PlayerPrefs.SetFloat("MasterVolume", masterPercent);
            PlayerPrefs.SetFloat("MusicVolume", musicPercent);
            PlayerPrefs.SetFloat("SFXVolume", sfxPercent);
            PlayerPrefs.Save();
        }

        ApplyMusicVolume();
        
        Debug.Log($"[AudioManager] Volumes updated - Master: {masterPercent}%, Music: {musicPercent}%, SFX: {sfxPercent}%, Saved={saveToPrefs}");
    }

    private void ApplyMusicVolume()
    {
        if (_bgmSource != null)
        {
            _bgmSource.volume = MusicVolume * MasterVolume;
        }
    }

    public float GetSFXVolumeTransformed()
    {
        return SFXVolume * MasterVolume;
    }

    public void StopBGM()
    {
        if (_bgmSource != null)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
            Debug.Log("[AudioManager] Stopped BGM.");
        }
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!IsMenuOrLobbyScene(activeScene))
        {
            Debug.Log($"[AudioManager] In-game scene ('{activeScene}'): BGM playback suppressed for gameplay SFX.");
            StopBGM();
            return;
        }

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            return; // Already playing this track
        }

        _bgmSource.clip = clip;
        ApplyMusicVolume();
        _bgmSource.Play();
        Debug.Log($"[AudioManager] Playing BGM Clip: {clip.name}");
    }

    public void PlayBGM(string resourcePath)
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip != null)
        {
            PlayBGM(clip);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] BGM clip not found at path: Resources/{resourcePath}");
        }
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float volumeScale = 1.0f)
    {
        if (clip == null) return;

        // Play spatialized sound effect at point
        float finalVolume = volumeScale * SFXVolume * MasterVolume;
        AudioSource.PlayClipAtPoint(clip, position, finalVolume);
    }
}
