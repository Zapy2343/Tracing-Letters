using System;
using UnityEngine;

/// <summary>
/// Persistent app-wide audio controller for music and sound effects.
/// Put one GlobalSoundManager in the first scene and call ToggleMusic/ToggleSound from UI buttons or switches.
/// </summary>
public class GlobalSoundManager : MonoBehaviour
{
    private const string SoundEnabledKey = "GlobalSoundManager.SoundEnabled";
    private const string MusicEnabledKey = "GlobalSoundManager.MusicEnabled";
    private const string SoundVolumeKey = "GlobalSoundManager.SoundVolume";
    private const string MusicVolumeKey = "GlobalSoundManager.MusicVolume";

    public static GlobalSoundManager Instance { get; private set; }
    public static event Action OnSettingsChanged;

    [Header("Music")]
    [Tooltip("AudioSource used for background music. Auto-created if empty.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("AudioSource used for global one-shot UI sounds. Auto-created if empty.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Default background music played when the manager starts.")]
    [SerializeField] private AudioClip defaultMusicClip;

    [Tooltip("If true, default music starts automatically when music is enabled.")]
    [SerializeField] private bool playMusicOnStart = true;

    [Header("Defaults")]
    [SerializeField] private bool defaultSoundEnabled = true;
    [SerializeField] private bool defaultMusicEnabled = true;

    [Range(0f, 1f)]
    [SerializeField] private float defaultSoundVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float defaultMusicVolume = 0.6f;

    public bool SoundEnabled { get; private set; }
    public bool MusicEnabled { get; private set; }
    public float SoundVolume { get; private set; }
    public float MusicVolume { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveAudioSources();
        LoadSettings();
        ApplyMusicState();
    }

    private void Start()
    {
        if (playMusicOnStart && defaultMusicClip != null)
        {
            PlayMusic(defaultMusicClip);
        }
    }

    public void ToggleSound()
    {
        SetSoundEnabled(!SoundEnabled);
    }

    public void ToggleMusic()
    {
        SetMusicEnabled(!MusicEnabled);
    }

    public void SetSoundEnabled(bool enabled)
    {
        SoundEnabled = enabled;
        SaveBool(SoundEnabledKey, SoundEnabled);
        NotifySettingsChanged();
    }

    public void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;
        SaveBool(MusicEnabledKey, MusicEnabled);
        ApplyMusicState();
        NotifySettingsChanged();
    }

    public void SetSoundVolume(float volume)
    {
        SoundVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SoundVolumeKey, SoundVolume);
        PlayerPrefs.Save();
        NotifySettingsChanged();
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.Save();
        ApplyMusicState();
        NotifySettingsChanged();
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (!SoundEnabled || clip == null)
        {
            return;
        }

        ResolveAudioSources();
        sfxSource.PlayOneShot(clip, SoundVolume * Mathf.Clamp01(volumeScale));
    }

    public void PlaySfx(AudioSource source, AudioClip clip, float volumeScale = 1f)
    {
        if (!SoundEnabled || source == null || clip == null)
        {
            return;
        }

        source.PlayOneShot(clip, SoundVolume * Mathf.Clamp01(volumeScale));
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        ResolveAudioSources();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        ApplyMusicState();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    private void ResolveAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;

        if (sfxSource == null)
        {
            AudioSource[] audioSources = GetComponents<AudioSource>();
            for (int i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i] != musicSource)
                {
                    sfxSource = audioSources[i];
                    break;
                }
            }
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
    }

    private void LoadSettings()
    {
        SoundEnabled = LoadBool(SoundEnabledKey, defaultSoundEnabled);
        MusicEnabled = LoadBool(MusicEnabledKey, defaultMusicEnabled);
        SoundVolume = PlayerPrefs.GetFloat(SoundVolumeKey, Mathf.Clamp01(defaultSoundVolume));
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, Mathf.Clamp01(defaultMusicVolume));
    }

    private void ApplyMusicState()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.volume = MusicEnabled ? MusicVolume : 0f;

        if (MusicEnabled && musicSource.clip != null && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
        else if (!MusicEnabled && musicSource.isPlaying)
        {
            musicSource.Pause();
        }
    }

    private void NotifySettingsChanged()
    {
        OnSettingsChanged?.Invoke();
    }

    private static bool LoadBool(string key, bool defaultValue)
    {
        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
    }

    private static void SaveBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
