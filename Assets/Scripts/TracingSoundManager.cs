using UnityEngine;

/// <summary>
/// Central place for tracing and bubble quiz sounds. Designers can drag AudioClips here in the Inspector.
/// </summary>
public class TracingSoundManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional PenDrawer to monitor for a soothing tracing loop while the child draws.")]
    [SerializeField] private PenDrawer penDrawer;

    [Tooltip("AudioSource for one-shot sounds such as correct, wrong, and completion.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("AudioSource for the optional tracing loop. It will be set to loop automatically.")]
    [SerializeField] private AudioSource tracingLoopSource;

    [Header("Tracing Sounds")]
    [Tooltip("Soothing loop played while tracing. Leave empty if you do not want a tracing sound.")]
    [SerializeField] private AudioClip tracingLoopClip;

    [Tooltip("Played when one tracing sequence step is completed.")]
    [SerializeField] private AudioClip strokeStepCompleteClip;

    [Tooltip("Played when the full letter tracing is completed.")]
    [SerializeField] private AudioClip letterCompleteClip;

    [Header("Word / Letter Audio Clips (Fallback)")]
    [Tooltip("Fallback sound played when a letter/word starts if the active TracingLetterAsset has no StartSound assigned.")]
    [SerializeField] private AudioClip defaultStartSound;

    [Tooltip("Fallback sound played when tracing & correct bubble are completed if the active TracingLetterAsset has no FinishingSound assigned.")]
    [SerializeField] private AudioClip defaultFinishingSound;

    [Header("Bubble Quiz Sounds")]
    [Tooltip("Played when the child taps the correct bubble.")]
    [SerializeField] private AudioClip correctBubbleClip;

    [Tooltip("Played when the child taps an incorrect bubble.")]
    [SerializeField] private AudioClip wrongBubbleClip;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float tracingLoopVolume = 0.35f;

    private bool wasTracing;

    private void Awake()
    {
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        if (tracingLoopSource == null)
        {
            tracingLoopSource = gameObject.AddComponent<AudioSource>();
        }

        tracingLoopSource.loop = true;
        tracingLoopSource.playOnAwake = false;
        tracingLoopSource.volume = tracingLoopVolume;
    }

    private void OnEnable()
    {
        GlobalSoundManager.OnSettingsChanged += HandleGlobalSoundSettingsChanged;

        if (penDrawer != null)
        {
            penDrawer.OnStrokeStepCompleted.AddListener(PlayStrokeStepComplete);
            penDrawer.OnMaskCompleted.AddListener(PlayLetterComplete);
        }
    }

    private void OnDisable()
    {
        GlobalSoundManager.OnSettingsChanged -= HandleGlobalSoundSettingsChanged;

        if (penDrawer != null)
        {
            penDrawer.OnStrokeStepCompleted.RemoveListener(PlayStrokeStepComplete);
            penDrawer.OnMaskCompleted.RemoveListener(PlayLetterComplete);
        }

        StopTracingLoop();
    }

    private void Update()
    {
        if (penDrawer == null)
        {
            return;
        }

        bool isTracing = penDrawer.IsActivelyDrawing;
        if (isTracing && !wasTracing)
        {
            StartTracingLoop();
        }
        else if (!isTracing && wasTracing)
        {
            StopTracingLoop();
        }

        wasTracing = isTracing;
    }

    public void PlayCorrectBubble()
    {
        PlayOneShot(correctBubbleClip);
    }

    public void PlayWrongBubble()
    {
        PlayOneShot(wrongBubbleClip);
    }

    public void PlayStrokeStepComplete()
    {
        PlayOneShot(strokeStepCompleteClip);
    }

    public void PlayLetterComplete()
    {
        PlayOneShot(letterCompleteClip);
    }

    /// <summary>
    /// Plays the start sound for the active word/letter.
    /// Uses TracingLetterAsset.StartSound if assigned, otherwise falls back to defaultStartSound.
    /// </summary>
    public void PlayLetterStartSound(TracingLetterAsset letterAsset = null)
    {
        AudioClip clip = null;
        if (letterAsset != null)
        {
            clip = letterAsset.StartSound;
        }
        else if (penDrawer != null)
        {
            TracingLetterAsset activeAsset = penDrawer.CurrentLetterAsset;
            if (activeAsset != null && activeAsset.StartSound != null)
            {
                clip = activeAsset.StartSound;
            }
            else
            {
                LetterSequence activeSeq = penDrawer.CurrentLetterSequence;
                if (activeSeq != null)
                {
                    clip = activeSeq.StartSound;
                }
            }
        }

        if (clip == null)
        {
            clip = defaultStartSound;
        }

        if (clip != null)
        {
            PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Plays the finishing sound after completing tracing and popping the correct bubble.
    /// Uses TracingLetterAsset.FinishingSound if assigned, otherwise falls back to defaultFinishingSound.
    /// </summary>
    public void PlayLetterFinishingSound(TracingLetterAsset letterAsset = null)
    {
        AudioClip clip = null;
        if (letterAsset != null)
        {
            clip = letterAsset.FinishingSound;
        }
        else if (penDrawer != null)
        {
            TracingLetterAsset activeAsset = penDrawer.CurrentLetterAsset;
            if (activeAsset != null && activeAsset.FinishingSound != null)
            {
                clip = activeAsset.FinishingSound;
            }
            else
            {
                LetterSequence activeSeq = penDrawer.CurrentLetterSequence;
                if (activeSeq != null)
                {
                    clip = activeSeq.FinishingSound;
                }
            }
        }

        if (clip == null)
        {
            clip = defaultFinishingSound;
        }

        if (clip != null)
        {
            PlayOneShot(clip);
        }
    }

    private void StartTracingLoop()
    {
        if (tracingLoopSource == null || tracingLoopClip == null)
        {
            return;
        }

        if (GlobalSoundManager.Instance != null && !GlobalSoundManager.Instance.SoundEnabled)
        {
            StopTracingLoop();
            return;
        }

        tracingLoopSource.clip = tracingLoopClip;
        tracingLoopSource.volume = GetGlobalSfxVolumeScale() * tracingLoopVolume;

        if (!tracingLoopSource.isPlaying)
        {
            tracingLoopSource.Play();
        }
    }

    private void StopTracingLoop()
    {
        if (tracingLoopSource != null && tracingLoopSource.isPlaying)
        {
            //tracingLoopSource.Stop();
        }
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
        {
            return;
        }

        if (GlobalSoundManager.Instance != null)
        {
            GlobalSoundManager.Instance.PlaySfx(sfxSource, clip, sfxVolume);
            return;
        }

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    private float GetGlobalSfxVolumeScale()
    {
        return GlobalSoundManager.Instance != null ? GlobalSoundManager.Instance.SoundVolume : 1f;
    }

    private void HandleGlobalSoundSettingsChanged()
    {
        if (GlobalSoundManager.Instance != null && !GlobalSoundManager.Instance.SoundEnabled)
        {
            StopTracingLoop();
            return;
        }

        if (tracingLoopSource != null)
        {
            tracingLoopSource.volume = GetGlobalSfxVolumeScale() * tracingLoopVolume;
        }
    }
}
