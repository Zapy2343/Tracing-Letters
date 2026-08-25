using UnityEngine;

/// <summary>
/// SoundManager handles background music (BGM) and sound effects (SFX).
/// Attach this script to a "SoundManager" GameObject in your scene.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("BGM Settings")]
    [Tooltip("Reference to the Background Music audio clip.")]
    [SerializeField] private AudioClip bgmClip;

    [Tooltip("Play the BGM automatically when the scene starts.")]
    [SerializeField] private bool playBgmOnStart = true;

    [Tooltip("Whether the BGM should loop continuously.")]
    [SerializeField] private bool loopBgm = true;

    [Range(0f, 1f)]
    [Tooltip("Volume level for background music.")]
    [SerializeField] private float bgmVolume = 0.5f;

    [Header("SFX Settings")]
    [Range(0f, 1f)]
    [Tooltip("Volume level for sound effects.")]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Audio Sources (Optional - Auto created if empty)")]
    [Tooltip("Dedicated AudioSource for BGM. If left empty, one will be created automatically.")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("Dedicated AudioSource for SFX. If left empty, one will be created automatically.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Persistence")]
    [Tooltip("If true, this SoundManager will persist across scene loads.")]
    [SerializeField] private bool persistAcrossScenes = true;

    // Public properties for checking status
    public bool IsMuted { get; private set; }
    public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;
    public bool IsBGMPlaying => bgmSource != null && bgmSource.isPlaying;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        InitializeAudioSources();
    }

    private void Start()
    {
        if (playBgmOnStart && bgmClip != null)
        {
            PlayBGM(bgmClip, loopBgm);
        }
    }

    /// <summary>
    /// Ensures AudioSources for BGM and SFX exist and are properly configured.
    /// </summary>
    private void InitializeAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
        }

        bgmSource.loop = loopBgm;
        bgmSource.volume = bgmVolume;

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        sfxSource.volume = sfxVolume;
    }

    #region BGM Methods

    /// <summary>
    /// Plays the specified BGM clip, or the default referenced BGM if null is passed.
    /// </summary>
    public void PlayBGM(AudioClip clip = null, bool loop = true)
    {
        AudioClip clipToPlay = clip != null ? clip : bgmClip;

        if (clipToPlay == null)
        {
            Debug.LogWarning("[SoundManager] No BGM clip provided to play.", this);
            return;
        }

        if (bgmSource == null)
        {
            InitializeAudioSources();
        }

        // If the same clip is already playing, do nothing
        if (bgmSource.clip == clipToPlay && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = clipToPlay;
        bgmSource.loop = loop;
        bgmSource.volume = IsMuted ? 0f : bgmVolume;
        bgmSource.Play();
    }

    /// <summary>
    /// Stops the currently playing background music.
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    /// <summary>
    /// Pauses the currently playing background music.
    /// </summary>
    public void PauseBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
    }

    /// <summary>
    /// Resumes the background music if paused.
    /// </summary>
    public void ResumeBGM()
    {
        if (bgmSource != null && !bgmSource.isPlaying && bgmSource.clip != null)
        {
            bgmSource.UnPause();
        }
    }

    /// <summary>
    /// Sets the volume of the background music (0.0 to 1.0).
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null && !IsMuted)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    #endregion

    #region SFX Methods

    /// <summary>
    /// Plays a one-shot sound effect.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || IsMuted) return;

        if (sfxSource == null)
        {
            InitializeAudioSources();
        }

        sfxSource.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(volumeScale));
    }

    /// <summary>
    /// Sets the volume of sound effects (0.0 to 1.0).
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }

    #endregion

    #region Mute & Master Controls

    /// <summary>
    /// Toggles mute state for both BGM and SFX.
    /// </summary>
    public void ToggleMute()
    {
        SetMute(!IsMuted);
    }

    /// <summary>
    /// Sets mute state for audio.
    /// </summary>
    public void SetMute(bool mute)
    {
        IsMuted = mute;

        if (bgmSource != null)
        {
            bgmSource.volume = IsMuted ? 0f : bgmVolume;
        }
    }

    #endregion
}
