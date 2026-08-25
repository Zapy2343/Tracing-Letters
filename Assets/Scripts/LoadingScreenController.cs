using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls and animates the two-phase splash/loading screen sequence:
/// Phase 1: BG1 visible -> Logo fades in -> "Presents" text fades in.
/// Phase 2: BG2 & Logo (1) fade in together -> Logo (1) slides up and fades out ->
///          BG1/Logo/Presents disabled -> BG2 fades out revealing the game.
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    [Header("Hierarchy References")]
    [Tooltip("Background 1 (initial background).")]
    [SerializeField] private GameObject bg1;

    [Tooltip("First Logo (e.g., Company/Studio logo).")]
    [SerializeField] private GameObject logo1;

    [Tooltip("Presents text object.")]
    [SerializeField] private GameObject presentsText;

    [Tooltip("Background 2 (transition/game background).")]
    [SerializeField] private GameObject bg2;

    [Tooltip("Second Logo (e.g., Game Title / Logo (1)).")]
    [SerializeField] private GameObject logo2;

    [Header("Animation Timings - Phase 1")]
    [Tooltip("Initial delay before first logo starts fading in.")]
    [SerializeField] private float initialDelay = 0.2f;

    [Tooltip("Duration for Logo 1 to fade in.")]
    [SerializeField] private float logo1FadeInDuration = 0.8f;

    [Tooltip("Delay after Logo 1 finishes before Presents text fades in.")]
    [SerializeField] private float delayBeforePresents = 0.3f;

    [Tooltip("Duration for Presents text to fade in.")]
    [SerializeField] private float presentsFadeInDuration = 0.6f;

    [Tooltip("How long to hold Phase 1 (Logo 1 + Presents) on screen before transitioning to Phase 2.")]
    [SerializeField] private float phase1HoldDuration = 1.0f;

    [Header("Animation Timings - Phase 2")]
    [Tooltip("Duration for BG2 and Logo 2 to fade in simultaneously.")]
    [SerializeField] private float phase2FadeInDuration = 0.8f;

    [Tooltip("How long Logo 2 stays fully visible before sliding up and fading out.")]
    [SerializeField] private float phase2HoldDuration = 1.0f;

    [Tooltip("Distance in units/pixels Logo 2 moves upward while fading out.")]
    [SerializeField] private float logo2SlideUpDistance = 80f;

    [Tooltip("Duration for Logo 2 to slide up and fade out.")]
    [SerializeField] private float logo2SlideAndFadeDuration = 0.7f;

    [Tooltip("Duration for BG2 to fade out, revealing the game.")]
    [SerializeField] private float bg2FadeOutDuration = 0.8f;

    [Header("Options")]
    [Tooltip("Start the loading screen animation automatically in Start().")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("Deactivate this LoadingScreen GameObject when the sequence finishes.")]
    [SerializeField] private bool deactivateOnComplete = true;

    [Header("Events")]
    [Tooltip("Fires when the loading animation is completely finished.")]
    public UnityEvent OnLoadingFinished;

    // Cached CanvasGroups for smooth alpha control
    private CanvasGroup _bg1Group;
    private CanvasGroup _logo1Group;
    private CanvasGroup _presentsGroup;
    private CanvasGroup _bg2Group;
    private CanvasGroup _logo2Group;
    private RectTransform _logo2Rect;
    private Vector2 _logo2OriginalAnchoredPos;
    private Coroutine _animationRoutine;

    private void Awake()
    {
        CacheComponents();
        PrepareInitialState();
    }

    private void Start()
    {
        if (autoStart)
        {
            StartLoadingSequence();
        }
    }

    /// <summary>
    /// Finds or adds CanvasGroups to each element to ensure smooth alpha fading.
    /// </summary>
    private void CacheComponents()
    {
        if (bg1 != null) _bg1Group = GetOrAddCanvasGroup(bg1);
        if (logo1 != null) _logo1Group = GetOrAddCanvasGroup(logo1);
        if (presentsText != null) _presentsGroup = GetOrAddCanvasGroup(presentsText);
        if (bg2 != null) _bg2Group = GetOrAddCanvasGroup(bg2);

        if (logo2 != null)
        {
            _logo2Group = GetOrAddCanvasGroup(logo2);
            _logo2Rect = logo2.GetComponent<RectTransform>();
            if (_logo2Rect != null)
            {
                _logo2OriginalAnchoredPos = _logo2Rect.anchoredPosition;
            }
        }
    }

    /// <summary>
    /// Sets the exact initial state: BG1 active with alpha 1, all other 4 elements disabled/hidden.
    /// </summary>
    public void PrepareInitialState()
    {
        // BG1 active and fully visible
        if (bg1 != null)
        {
            bg1.SetActive(true);
            if (_bg1Group != null) _bg1Group.alpha = 1f;
        }

        // Hide and disable Logo 1
        if (logo1 != null)
        {
            if (_logo1Group != null) _logo1Group.alpha = 0f;
            logo1.SetActive(false);
        }

        // Hide and disable Presents
        if (presentsText != null)
        {
            if (_presentsGroup != null) _presentsGroup.alpha = 0f;
            presentsText.SetActive(false);
        }

        // Hide and disable BG2
        if (bg2 != null)
        {
            if (_bg2Group != null) _bg2Group.alpha = 0f;
            bg2.SetActive(false);
        }

        // Hide and disable Logo 2
        if (logo2 != null)
        {
            if (_logo2Group != null) _logo2Group.alpha = 0f;
            if (_logo2Rect != null) _logo2Rect.anchoredPosition = _logo2OriginalAnchoredPos;
            logo2.SetActive(false);
        }
    }

    /// <summary>
    /// Public method to start or restart the loading animation sequence.
    /// </summary>
    [ContextMenu("Play Animation Preview")]
    public void StartLoadingSequence()
    {
        if (_animationRoutine != null)
        {
            StopCoroutine(_animationRoutine);
        }

        PrepareInitialState();
        _animationRoutine = StartCoroutine(AnimateSequence());
    }

    private IEnumerator AnimateSequence()
    {
        // 0. Initial brief pause
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        // 1. Fade in Logo 1
        if (logo1 != null)
        {
            logo1.SetActive(true);
            yield return FadeCanvasGroup(_logo1Group, 0f, 1f, logo1FadeInDuration);
        }

        // 2. Short delay before Presents
        if (delayBeforePresents > 0f)
        {
            yield return new WaitForSeconds(delayBeforePresents);
        }

        // 3. Fade in Presents text
        if (presentsText != null)
        {
            presentsText.SetActive(true);
            yield return FadeCanvasGroup(_presentsGroup, 0f, 1f, presentsFadeInDuration);
        }

        // 4. Hold Phase 1
        if (phase1HoldDuration > 0f)
        {
            yield return new WaitForSeconds(phase1HoldDuration);
        }

        // 5. Fade in BG2 and Logo 2 at the same time
        if (bg2 != null) bg2.SetActive(true);
        if (logo2 != null)
        {
            logo2.SetActive(true);
            if (_logo2Rect != null) _logo2Rect.anchoredPosition = _logo2OriginalAnchoredPos;
        }

        yield return FadeTwoCanvasGroupsTogether(_bg2Group, _logo2Group, 0f, 1f, phase2FadeInDuration);

        // 6. Hold Phase 2
        if (phase2HoldDuration > 0f)
        {
            yield return new WaitForSeconds(phase2HoldDuration);
        }

        // 7. Logo 2 slides slightly slowly upward and fades out
        if (logo2 != null)
        {
            yield return SlideAndFadeOutLogo2(_logo2Group, _logo2Rect, _logo2OriginalAnchoredPos, logo2SlideUpDistance, logo2SlideAndFadeDuration);
            logo2.SetActive(false);
        }

        // 8. Disable BG1, Logo 1, and Presents before fading out BG2
        if (bg1 != null) bg1.SetActive(false);
        if (logo1 != null) logo1.SetActive(false);
        if (presentsText != null) presentsText.SetActive(false);

        // 9. BG2 fades out, revealing the game behind it
        if (bg2 != null)
        {
            yield return FadeCanvasGroup(_bg2Group, 1f, 0f, bg2FadeOutDuration);
            bg2.SetActive(false);
        }

        // 10. Complete
        OnLoadingFinished?.Invoke();

        if (deactivateOnComplete)
        {
            gameObject.SetActive(false);
        }

        _animationRoutine = null;
    }

    /// <summary>
    /// Smoothly fades a single CanvasGroup from 'fromAlpha' to 'toAlpha'.
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup group, float fromAlpha, float toAlpha, float duration)
    {
        if (group == null) yield break;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        group.alpha = fromAlpha;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, smoothT);
            yield return null;
        }

        group.alpha = toAlpha;
    }

    /// <summary>
    /// Fades two CanvasGroups simultaneously.
    /// </summary>
    private IEnumerator FadeTwoCanvasGroupsTogether(CanvasGroup groupA, CanvasGroup groupB, float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        if (groupA != null) groupA.alpha = fromAlpha;
        if (groupB != null) groupB.alpha = fromAlpha;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float currentAlpha = Mathf.Lerp(fromAlpha, toAlpha, smoothT);

            if (groupA != null) groupA.alpha = currentAlpha;
            if (groupB != null) groupB.alpha = currentAlpha;

            yield return null;
        }

        if (groupA != null) groupA.alpha = toAlpha;
        if (groupB != null) groupB.alpha = toAlpha;
    }

    /// <summary>
    /// Slides Logo 2 slightly upward on Y axis while fading it out.
    /// </summary>
    private IEnumerator SlideAndFadeOutLogo2(CanvasGroup group, RectTransform rect, Vector2 startPos, float distanceY, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        Vector2 targetPos = startPos + new Vector2(0f, distanceY);

        if (group != null) group.alpha = 1f;
        if (rect != null) rect.anchoredPosition = startPos;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (group != null)
            {
                group.alpha = Mathf.Lerp(1f, 0f, smoothT);
            }

            if (rect != null)
            {
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, smoothT);
            }

            yield return null;
        }

        if (group != null) group.alpha = 0f;
        if (rect != null) rect.anchoredPosition = targetPos;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = go.AddComponent<CanvasGroup>();
        }
        return cg;
    }
}
