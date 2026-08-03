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
        if (penDrawer != null)
        {
            penDrawer.OnStrokeStepCompleted.AddListener(PlayStrokeStepComplete);
            penDrawer.OnMaskCompleted.AddListener(PlayLetterComplete);
        }
    }

    private void OnDisable()
    {
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

    private void StartTracingLoop()
    {
        if (tracingLoopSource == null || tracingLoopClip == null)
        {
            return;
        }

        tracingLoopSource.clip = tracingLoopClip;
        tracingLoopSource.volume = tracingLoopVolume;

        if (!tracingLoopSource.isPlaying)
        {
            tracingLoopSource.Play();
        }
    }

    private void StopTracingLoop()
    {
        if (tracingLoopSource != null && tracingLoopSource.isPlaying)
        {
            tracingLoopSource.Stop();
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
