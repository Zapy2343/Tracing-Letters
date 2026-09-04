using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent app-wide audio controller for music playlist, looping, ducking, and sound effects.
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

    [Header("Music Playlist")]
    [Tooltip("List of background music clips available to play.")]
    [SerializeField] private List<AudioClip> bgMusicClips = new List<AudioClip>();

    [Tooltip("If true, the current music track will loop continuously.")]
    [SerializeField] private bool loopCurrentMusic = true;

    [Tooltip("If loopCurrentMusic is false, automatically play the next track when current ends.")]
    [SerializeField] private bool autoPlayNextTrack = true;

    [SerializeField] private int currentTrackIndex = 0;

    [Header("Audio Sources")]
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
    public bool IsMusicDucked { get; private set; }
    public int CurrentTrackIndex => currentTrackIndex;
    public int MusicClipCount => bgMusicClips != null ? bgMusicClips.Count : 0;

    private float musicDuckMultiplier = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
#if UNITY_EDITOR
            if (UnityEditor.Selection.activeGameObject == gameObject)
            {
                UnityEditor.Selection.activeGameObject = null;
            }
#endif
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        AutoLoadMusicClipsIfEmpty();
        ResolveAudioSources();
        LoadSettings();
        ApplyMusicState();
    }

    private void Start()
    {
        if (playMusicOnStart)
        {
            if (bgMusicClips != null && bgMusicClips.Count > 0)
            {
                PlayMusicTrack(currentTrackIndex);
            }
            else if (defaultMusicClip != null)
            {
                PlayMusic(defaultMusicClip);
            }
        }
    }

    private void Update()
    {
        if (!loopCurrentMusic && autoPlayNextTrack && MusicEnabled && musicSource != null && !musicSource.isPlaying && MusicClipCount > 1)
        {
            PlayRandomTrack();
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

    public void SetMusicDucked(bool ducked, float duckMultiplier = 0.5f)
    {
        IsMusicDucked = ducked;
        musicDuckMultiplier = Mathf.Clamp01(duckMultiplier);
        ApplyMusicState();
    }

    public void SetLoopCurrentMusic(bool loop)
    {
        loopCurrentMusic = loop;
        if (musicSource != null)
        {
            musicSource.loop = loopCurrentMusic;
        }
    }

    public void PlayMusicTrack(int index)
    {
        if (bgMusicClips == null || bgMusicClips.Count == 0)
        {
            if (defaultMusicClip != null)
            {
                PlayMusic(defaultMusicClip);
            }
            return;
        }

        currentTrackIndex = Mathf.Clamp(index, 0, bgMusicClips.Count - 1);
        AudioClip clip = bgMusicClips[currentTrackIndex];
        if (clip != null)
        {
            PlayMusic(clip);
        }
    }

    public void PlayNextTrack()
    {
        if (bgMusicClips == null || bgMusicClips.Count == 0) return;
        int nextIndex = (currentTrackIndex + 1) % bgMusicClips.Count;
        PlayMusicTrack(nextIndex);
    }

    public void PlayPreviousTrack()
    {
        if (bgMusicClips == null || bgMusicClips.Count == 0) return;
        int prevIndex = (currentTrackIndex - 1 + bgMusicClips.Count) % bgMusicClips.Count;
        PlayMusicTrack(prevIndex);
    }

    public void PlayRandomTrack()
    {
        if (bgMusicClips == null || bgMusicClips.Count == 0) return;
        int randomIndex = UnityEngine.Random.Range(0, bgMusicClips.Count);
        PlayMusicTrack(randomIndex);
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (!SoundEnabled || clip == null)
        {
            return;
        }

        ResolveAudioSources();
        if (sfxSource != null)
        {
            sfxSource.volume = SoundVolume;
            sfxSource.mute = !SoundEnabled;
            sfxSource.PlayOneShot(clip, SoundVolume * Mathf.Clamp01(volumeScale));
        }
    }

    public void PlaySfx(AudioSource source, AudioClip clip, float volumeScale = 1f)
    {
        if (!SoundEnabled || clip == null)
        {
            return;
        }

        ResolveAudioSources();

        AudioSource targetSource = source;
        if (targetSource == null || targetSource == musicSource)
        {
            targetSource = sfxSource;
        }

        if (targetSource != null)
        {
            targetSource.volume = SoundVolume;
            targetSource.mute = !SoundEnabled;
            targetSource.PlayOneShot(clip, SoundVolume * Mathf.Clamp01(volumeScale));
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        ResolveAudioSources();
        musicSource.clip = clip;
        musicSource.loop = loopCurrentMusic;
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
        AudioSource[] sources = GetComponents<AudioSource>();

        if (musicSource == null)
        {
            if (sources != null && sources.Length > 0)
            {
                musicSource = sources[0];
            }
            else
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        musicSource.loop = loopCurrentMusic;
        musicSource.playOnAwake = false;

        if (sfxSource == null || sfxSource == musicSource)
        {
            sources = GetComponents<AudioSource>();
            sfxSource = null;
            if (sources != null)
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] != null && sources[i] != musicSource)
                    {
                        sfxSource = sources[i];
                        break;
                    }
                }
            }

            if (sfxSource == null || sfxSource == musicSource)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
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

        float effectiveVolume = MusicVolume;
        if (IsMusicDucked)
        {
            effectiveVolume *= musicDuckMultiplier;
        }

        musicSource.volume = MusicEnabled ? effectiveVolume : 0f;

        if (MusicEnabled && musicSource.clip != null && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
        else if (!MusicEnabled && musicSource.isPlaying)
        {
            musicSource.Pause();
        }
    }

    private void AutoLoadMusicClipsIfEmpty()
    {
#if UNITY_EDITOR
        if (bgMusicClips != null && bgMusicClips.Count > 0) return;

        bgMusicClips = new List<AudioClip>();
        string[] searchPaths = new string[] {
            "Assets/Sounds/BG.mp3",
            "Assets/Sounds/ExtraBGM.mp3",
            "Assets/Sounds/Momo Moonlight.mp3",
            "Assets/Sounds/Pocket Lullaby.mp3"
        };

        foreach (string path in searchPaths)
        {
            AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                bgMusicClips.Add(clip);
            }
        }

        if (defaultMusicClip == null && bgMusicClips.Count > 0)
        {
            defaultMusicClip = bgMusicClips[0];
        }
#endif
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
