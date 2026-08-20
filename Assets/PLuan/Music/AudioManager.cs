using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public const float DEFAULT_AMBIENT_BGM_MULTIPLIER = 0.25f;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;

    [Header("Default Audio Clips")]
    [SerializeField] private AudioClip _defaultBgmClip;

    // Volume settings scaled from 0.0 to 1.0
    public float MasterVolume { get; private set; } = 1.0f;
    public float MusicVolume { get; private set; } = 0.8f;
    public float SFXVolume { get; private set; } = 0.9f;

    public float AmbientBGMVolumeMultiplier { get; set; } = DEFAULT_AMBIENT_BGM_MULTIPLIER;

    private float _bgmVolumeMultiplier = 1.0f;
    private Coroutine _fadeCoroutine;

    private bool _isCutsceneActive = false;
    private bool _isBossMusicActive = false;

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
            // Reset trạng thái cutscene & boss khi quay về Menu/Lobby
            _isCutsceneActive = false;
            _isBossMusicActive = false;

            PlayBGM("Audio/BGM", 1.0f);
        }
        else
        {
            // Khi vào các Scene Gameplay / Map / Cutscene
            if (!_isCutsceneActive && !_isBossMusicActive)
            {
                if (_bgmSource != null && !_bgmSource.isPlaying)
                {
                    PlayAmbientBGM(AmbientBGMVolumeMultiplier, 1.5f);
                }
                else
                {
                    // Nếu đang phát từ trước, chuyển mượt về âm lượng nền nhỏ
                    FadeBGMToMultiplier(AmbientBGMVolumeMultiplier, 1.5f);
                }
            }
        }
    }

    public bool IsMenuOrLobbyScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        string lower = sceneName.ToLower().Replace(" ", "").Replace("_", "");
        return lower.Contains("mainmenu") || lower.Contains("menu") || lower.Contains("lobby") || lower.Contains("waiting");
    }

    public bool IsCutsceneScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        string lower = sceneName.ToLower().Replace(" ", "").Replace("_", "");
        return lower.Contains("mapstart") || lower.Contains("cutscene");
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

    public void SetBGMVolumeMultiplier(float multiplier)
    {
        _bgmVolumeMultiplier = Mathf.Clamp01(multiplier);
        ApplyMusicVolume();
    }

    public void FadeBGMToMultiplier(float targetMultiplier, float duration = 1.0f)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeToMultiplierCoroutine(targetMultiplier, duration));
    }

    private IEnumerator FadeToMultiplierCoroutine(float targetMultiplier, float duration)
    {
        if (_bgmSource == null) yield break;
        if (!_bgmSource.isPlaying && targetMultiplier > 0f)
        {
            _bgmSource.Play();
        }

        float startMul = _bgmVolumeMultiplier;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _bgmVolumeMultiplier = Mathf.Lerp(startMul, targetMultiplier, t);
            ApplyMusicVolume();
            yield return null;
        }
        _bgmVolumeMultiplier = targetMultiplier;
        ApplyMusicVolume();

        if (_bgmVolumeMultiplier <= 0.001f && _bgmSource != null && _bgmSource.isPlaying)
        {
            _bgmSource.Pause();
        }
        _fadeCoroutine = null;
    }

    public void FadeOutBGM(float duration = 1.5f)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutBGMCoroutine(duration));
    }

    private IEnumerator FadeOutBGMCoroutine(float duration)
    {
        if (_bgmSource == null || !_bgmSource.isPlaying) yield break;

        float startMul = _bgmVolumeMultiplier;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _bgmVolumeMultiplier = Mathf.Lerp(startMul, 0f, t);
            ApplyMusicVolume();
            yield return null;
        }

        _bgmVolumeMultiplier = 0f;
        ApplyMusicVolume();

        if (_bgmSource != null && _bgmSource.isPlaying)
        {
            _bgmSource.Pause();
        }
        _fadeCoroutine = null;
    }

    public void SetCutsceneActive(bool active, float fadeDuration = 0.5f)
    {
        _isCutsceneActive = active;
        Debug.Log($"[AudioManager] SetCutsceneActive: {active}, isBossActive: {_isBossMusicActive}");
        EvaluateBGMState(fadeDuration);
    }

    public void SetBossMusicActive(bool active, float fadeDuration = 0.8f)
    {
        _isBossMusicActive = active;
        Debug.Log($"[AudioManager] SetBossMusicActive: {active}, isCutsceneActive: {_isCutsceneActive}");
        EvaluateBGMState(fadeDuration);
    }

    public void EvaluateBGMState(float fadeDuration = 1.0f)
    {
        if (_isCutsceneActive || _isBossMusicActive)
        {
            FadeOutBGM(fadeDuration);
        }
        else
        {
            ResumeAmbientBGM(fadeDuration);
        }
    }

    public void PlayAmbientBGM(float targetMultiplier = DEFAULT_AMBIENT_BGM_MULTIPLIER, float fadeDuration = 1.5f)
    {
        if (_isCutsceneActive || _isBossMusicActive) return;

        AmbientBGMVolumeMultiplier = Mathf.Clamp01(targetMultiplier);

        if (_bgmSource != null)
        {
            if (_bgmSource.clip == null)
            {
                _bgmSource.clip = Resources.Load<AudioClip>("Audio/BGM");
            }

            if (_bgmSource.clip != null)
            {
                if (!_bgmSource.isPlaying)
                {
                    _bgmVolumeMultiplier = 0f;
                    ApplyMusicVolume();
                    _bgmSource.Play();
                }
                FadeBGMToMultiplier(AmbientBGMVolumeMultiplier, fadeDuration);
            }
        }
    }

    public void ResumeAmbientBGM(float fadeDuration = 1.2f)
    {
        if (_isCutsceneActive || _isBossMusicActive) return;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        float targetMultiplier = IsMenuOrLobbyScene(sceneName) ? 1.0f : AmbientBGMVolumeMultiplier;

        if (_bgmSource != null)
        {
            if (_bgmSource.clip == null)
            {
                _bgmSource.clip = Resources.Load<AudioClip>("Audio/BGM");
            }

            if (_bgmSource.clip != null)
            {
                if (!_bgmSource.isPlaying)
                {
                    _bgmVolumeMultiplier = 0f;
                    ApplyMusicVolume();
                    _bgmSource.Play();
                }
                FadeBGMToMultiplier(targetMultiplier, fadeDuration);
            }
        }
    }

    private void ApplyMusicVolume()
    {
        if (_bgmSource != null)
        {
            _bgmSource.volume = MusicVolume * MasterVolume * _bgmVolumeMultiplier;
        }
    }

    public float GetSFXVolumeTransformed()
    {
        return SFXVolume * MasterVolume;
    }

    public void StopBGM()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (_bgmSource != null)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
            _bgmVolumeMultiplier = 1.0f;
            Debug.Log("[AudioManager] Stopped BGM.");
        }
    }

    public void PlayBGM(AudioClip clip, float volumeMultiplier = 1.0f)
    {
        if (clip == null) return;

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            FadeBGMToMultiplier(volumeMultiplier, 0.5f);
            return;
        }

        _bgmSource.clip = clip;
        _bgmVolumeMultiplier = volumeMultiplier;
        ApplyMusicVolume();

        if (!_isCutsceneActive && !_isBossMusicActive)
        {
            _bgmSource.Play();
            Debug.Log($"[AudioManager] Playing BGM Clip: {clip.name} (Multiplier: {volumeMultiplier})");
        }
    }

    public void PlayBGM(string resourcePath, float volumeMultiplier = 1.0f)
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip != null)
        {
            PlayBGM(clip, volumeMultiplier);
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

