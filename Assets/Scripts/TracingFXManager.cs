using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Manages glowing particle trails while tracing letters, and triggers rewarding celebration bursts 
/// and letter bounce animations upon letter completion.
/// </summary>
public class TracingFXManager : MonoBehaviour
{
    [Header("Script References")]
    [Tooltip("Reference to PenDrawer script. Auto-detected if empty.")]
    [SerializeField] private PenDrawer penDrawer;

    [Tooltip("Reference to LetterSwitcher script. Auto-detected if empty.")]
    [SerializeField] private LetterSwitcher letterSwitcher;

    [Header("Target GameObjects for Animation")]
    [Tooltip("Design Letter RectTransform for completion bounce animation. Auto-detected if empty.")]
    [SerializeField] private RectTransform designLetterTransform;

    [Tooltip("Dotted Letter RectTransform for completion bounce animation. Auto-detected if empty.")]
    [SerializeField] private RectTransform dottedLetterTransform;

    [Header("FX Sprites (Assign from Sprites/FX sprites)")]
    [Tooltip("Sparkle/particle sprite (ui_glow_spark.png).")]
    [SerializeField] private Sprite sparkSprite;

    [Tooltip("Glow sprite (Glow.png).")]
    [SerializeField] private Sprite glowSprite;

    [Tooltip("Starburst sprite (Glow1.png or Glow3.png).")]
    [SerializeField] private Sprite starburstSprite;

    [Tooltip("Light Rays sprite (GlowFxLightRays.png).")]
    [SerializeField] private Sprite lightRaysSprite;

    [Tooltip("Sheen streak sprite (ui_sheen_sprite.png).")]
    [SerializeField] private Sprite sheenSprite;

    [Header("Reference Completion FX Sprites (Assign from Sprites/CompletionFX)")]
    [Tooltip("Balloon sprites sliced from Completion fx.png.")]
    [SerializeField] private Sprite[] completionBalloonSprites;

    [Tooltip("Star and strip confetti sprites sliced from Completion fx.png.")]
    [SerializeField] private Sprite[] completionConfettiSprites;

    [Tooltip("White sparkle sprite sliced from Sparkles.png.")]
    [SerializeField] private Sprite whiteSparkleSprite;

    [Tooltip("White streak sprite sliced from Sparkles.png.")]
    [SerializeField] private Sprite whiteStreakSprite;

    [Header("Tracing Trail FX Settings")]
    [Tooltip("Enable sparkle particle trail while actively tracing.")]
    [SerializeField] private bool enableTracingTrail = true;

    [Tooltip("Time interval in seconds between spawning trail sparkles while drawing.")]
    [SerializeField] private float trailSpawnInterval = 0.035f;

    [Tooltip("Random size range for trail sparkles (min, max).")]
    [SerializeField] private Vector2 trailParticleSizeRange = new Vector2(25f, 50f);

    [Tooltip("Lifetime of trail sparkles in seconds.")]
    [SerializeField] private float trailParticleLifetime = 0.45f;

    [Header("Completion Burst FX Settings")]
    [Tooltip("Enable celebration burst upon 100% letter completion.")]
    [SerializeField] private bool enableCompletionBurst = true;

    [Tooltip("Number of spark particles in completion explosion.")]
    [SerializeField] private int burstParticleCount = 35;

    [Tooltip("Speed range for burst particles (min, max).")]
    [SerializeField] private Vector2 burstSpeedRange = new Vector2(180f, 450f);

    [Tooltip("Flash the finished letter shape with a glow pulse when tracing completes.")]
    [SerializeField] private bool enableLetterGlowPulse = true;

    [Tooltip("Number of duplicate letter-shaped glow pulses on completion.")]
    [SerializeField] private int letterGlowPulseCount = 2;

    [Tooltip("Duration of each letter-shaped glow pulse.")]
    [SerializeField] private float letterGlowPulseDuration = 0.55f;

    [Tooltip("Extra scale added to the largest letter glow pulse.")]
    [SerializeField] private float letterGlowPulseScale = 0.22f;

    [Tooltip("Enable expanding circular ripples around the completed letter.")]
    [SerializeField] private bool enableCompletionRipples = true;

    [Tooltip("Number of expanding ripples around the completed letter.")]
    [SerializeField] private int completionRippleCount = 2;

    [Tooltip("Enable upward sparkle shower after the letter completes.")]
    [SerializeField] private bool enableCompletionSparkleShower = true;

    [Tooltip("Number of sparkles in the completion shower.")]
    [SerializeField] private int sparkleShowerParticleCount = 24;

    [Tooltip("Replay a smaller sparkle pop shortly after completion so it remains visible above follow-up UI.")]
    [SerializeField] private bool playDelayedCompletionAccent = true;

    [Tooltip("Delay before the follow-up completion accent in seconds.")]
    [SerializeField] private float delayedCompletionAccentDelay = 0.18f;

    [Tooltip("Always spawn a normal UI full-screen flash on completion. This is a visible fallback that does not rely on FX sprites or additive shaders.")]
    [SerializeField] private bool enableGuaranteedCompletionFlash = true;

    [Tooltip("Total time the reference-style completion celebration should stay on screen before gameplay continues.")]
    [SerializeField] private float referenceCompletionDuration = 2.4f;

    [Tooltip("How many screen-wide sparkles/confetti particles appear during the completion overlay.")]
    [SerializeField] private int fullScreenSparkleCount = 70;

    [Tooltip("Peak opacity of the full-screen completion color wash.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float fullScreenFlashMaxAlpha = 0.72f;

    [Tooltip("Float balloons from the top like the reference video.")]
    [SerializeField] private bool enableReferenceBalloons = true;

    [Tooltip("Number of balloons to spawn on completion.")]
    [SerializeField] private int referenceBalloonCount = 12;

    [Tooltip("Height range for completion balloons in UI units.")]
    [SerializeField] private Vector2 referenceBalloonHeightRange = new Vector2(190f, 270f);

    [Tooltip("Spawn star/strip confetti from the center like the reference video.")]
    [SerializeField] private bool enableReferenceConfetti = true;

    [Tooltip("Number of reference confetti pieces to spawn.")]
    [SerializeField] private int referenceConfettiCount = 90;

    [Tooltip("Spawn white streaks behind the letter like the reference video.")]
    [SerializeField] private bool enableReferenceWhiteStreaks = true;

    [Tooltip("Draw a bright yellow magic sweep over the completed letter path, matching the reference video.")]
    [SerializeField] private bool enableReferenceStyleLetterSweep = true;

    [Tooltip("Color of the reference-style completion sweep.")]
    [SerializeField] private Color completionSweepColor = new Color(1f, 0.88f, 0.04f, 0.95f);

    [Tooltip("Thickness of the yellow completion sweep in UI units.")]
    [SerializeField] private float completionSweepThickness = 78f;

    [Tooltip("Seconds each stroke sweep takes to draw.")]
    [SerializeField] private float completionSweepStrokeDuration = 0.36f;

    [Tooltip("Delay between separate stroke sweeps.")]
    [SerializeField] private float completionSweepStepDelay = 0.08f;

    [Header("Completion Letter Animation")]
    [Tooltip("Animate letter with elastic bounce and wobble on completion.")]
    [SerializeField] private bool animateLetterOnCompletion = true;

    [Tooltip("Duration of the completion bounce animation in seconds.")]
    [SerializeField] private float bounceDuration = 0.65f;

    [Tooltip("Peak scale multiplier during bounce (1.25 = 125% size).")]
    [SerializeField] private float peakScale = 1.25f;

    [Header("Spiral Magic Transition FX Settings")]
    [Tooltip("Enable spiral magic despawn/spawn transition effect when switching letters.")]
    [SerializeField] private bool enableTransitionFX = true;

    [Tooltip("Total duration of the transition animation in seconds.")]
    [SerializeField] private float transitionDuration = 0.55f;

    [Tooltip("Number of full rotations the letter turns during despawn and spawn.")]
    [SerializeField] private float spiralRotations = 2.0f;

    [Tooltip("Number of magic particles in spiral vortex.")]
    [SerializeField] private int spiralParticleCount = 24;

    [Tooltip("Scale multiplier for the big flash/rays when a letter loads in.")]
    [SerializeField] private float letterLoadBurstScale = 1.85f;

    public bool EnableTransitionFX => enableTransitionFX;
    public bool IsTransitioning { get; private set; }

    [Header("Vibrant FX Palette")]
    [SerializeField]
    private Color[] vibrantColors = new Color[]
    {
        new Color(1.0f, 0.85f, 0.2f),  // Gold / Yellow
        new Color(0.2f, 0.9f, 1.0f),   // Cyan / Aqua
        new Color(1.0f, 0.3f, 0.85f),  // Vivid Pink / Magenta
        new Color(0.3f, 1.0f, 0.4f),   // Lime Green
        new Color(1.0f, 0.5f, 0.15f)   // Electric Orange
    };

    private Canvas parentCanvas;
    private Canvas fxCanvas;
    private RectTransform fxContainer;
    private Material additiveMaterial;
    private Sprite generatedSoftCircleSprite;
    private float lastTrailSpawnTime;
    private Camera mainCamera;
    private bool subscribedToCompletion;
    private float lastCompletionFxTime = -10f;

    private void Awake()
    {
        mainCamera = Camera.main;
        InitializeFXContainer();
        CreateAdditiveMaterial();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToPenDrawer();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeToPenDrawer();
    }

    private void OnDisable()
    {
        UnsubscribeFromPenDrawer();
    }

    private void OnDestroy()
    {
        UnsubscribeFromPenDrawer();
    }

    private void ResolveReferences()
    {
#if UNITY_2023_1_OR_NEWER
        if (penDrawer == null) penDrawer = FindFirstObjectByType<PenDrawer>();
        if (letterSwitcher == null) letterSwitcher = FindFirstObjectByType<LetterSwitcher>();
#else
        if (penDrawer == null) penDrawer = FindObjectOfType<PenDrawer>();
        if (letterSwitcher == null) letterSwitcher = FindObjectOfType<LetterSwitcher>();
#endif

        if (letterSwitcher != null)
        {
            if (designLetterTransform == null && letterSwitcher.DesignLetterObject != null)
            {
                designLetterTransform = letterSwitcher.DesignLetterObject.GetComponent<RectTransform>();
            }
            if (dottedLetterTransform == null && letterSwitcher.DottedLetterObject != null)
            {
                dottedLetterTransform = letterSwitcher.DottedLetterObject.GetComponent<RectTransform>();
            }
        }
    }

    private void SubscribeToPenDrawer()
    {
        if (subscribedToCompletion || penDrawer == null)
        {
            return;
        }

        penDrawer.OnMaskCompleted.AddListener(OnLetterCompleted);
        subscribedToCompletion = true;
    }

    private void UnsubscribeFromPenDrawer()
    {
        if (!subscribedToCompletion || penDrawer == null)
        {
            return;
        }

        penDrawer.OnMaskCompleted.RemoveListener(OnLetterCompleted);
        subscribedToCompletion = false;
    }

    private void InitializeFXContainer()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
#if UNITY_2023_1_OR_NEWER
            parentCanvas = FindFirstObjectByType<Canvas>();
#else
            parentCanvas = FindObjectOfType<Canvas>();
#endif
        }

        if (parentCanvas != null)
        {
            GameObject container = new GameObject("FX_Container", typeof(RectTransform), typeof(Canvas));
            container.transform.SetParent(parentCanvas.transform, false);

            fxContainer = container.GetComponent<RectTransform>();
            fxContainer.anchorMin = Vector2.zero;
            fxContainer.anchorMax = Vector2.one;
            fxContainer.sizeDelta = Vector2.zero;
            fxContainer.anchoredPosition = Vector2.zero;

            fxCanvas = container.GetComponent<Canvas>();
            fxCanvas.overrideSorting = true;
            fxCanvas.sortingOrder = parentCanvas.sortingOrder + 100;
            fxCanvas.pixelPerfect = parentCanvas.pixelPerfect;

            EnsureFXContainerOnTop();
        }
    }

    private void EnsureFXContainerOnTop()
    {
        if (fxContainer == null)
        {
            InitializeFXContainer();
        }

        if (fxContainer == null)
        {
            return;
        }

        if (fxCanvas == null)
        {
            fxCanvas = fxContainer.GetComponent<Canvas>();
            if (fxCanvas == null)
            {
                fxCanvas = fxContainer.gameObject.AddComponent<Canvas>();
            }
        }

        fxCanvas.overrideSorting = true;
        fxCanvas.sortingOrder = parentCanvas != null ? parentCanvas.sortingOrder + 100 : 100;
        fxContainer.SetAsLastSibling();
    }

    [Header("Custom Additive Shader & Material (Prevents Stripping in PC Build)")]
    [Tooltip("UI/Additive shader reference. Assigning this ensures Unity bundles the shader in PC builds.")]
    [SerializeField] private Shader additiveShader;

    [Tooltip("Pre-created Material using UI/Additive (Optional).")]
    [SerializeField] private Material customAdditiveMaterial;

    private void CreateAdditiveMaterial()
    {
        if (customAdditiveMaterial != null)
        {
            additiveMaterial = customAdditiveMaterial;
            return;
        }

        Shader targetShader = additiveShader != null ? additiveShader : Shader.Find("UI/Additive");
        if (targetShader != null)
        {
            additiveMaterial = new Material(targetShader);
        }
        else
        {
            Debug.LogWarning("[TracingFXManager] UI/Additive shader not found! Please assign it in the TracingFXManager Inspector component.");
        }
    }

    private void Update()
    {
        HandleTracingTrail();
    }

    /// <summary>
    /// Spawns glowing sparkles at the pen tip position while drawing.
    /// </summary>
    private void HandleTracingTrail()
    {
        if (!enableTracingTrail || fxContainer == null) return;

        bool isDrawing = penDrawer != null ? penDrawer.IsActivelyDrawing : IsMouseHeldDown();
        if (!isDrawing) return;

        if (Time.time - lastTrailSpawnTime >= trailSpawnInterval)
        {
            lastTrailSpawnTime = Time.time;
            Vector2 mouseScreenPos = GetMouseScreenPosition();

            Camera uiCamera = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? parentCanvas.worldCamera : null;
            if (uiCamera == null && parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) uiCamera = mainCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(fxContainer, mouseScreenPos, uiCamera, out Vector2 localPos))
            {
                SpawnTrailParticle(localPos);
            }
        }
    }

    private void SpawnTrailParticle(Vector2 position)
    {
        Sprite particleSprite = sparkSprite != null ? sparkSprite : glowSprite;
        if (particleSprite == null) return;

        GameObject pObj = new GameObject("TrailParticle", typeof(RectTransform), typeof(Image));
        pObj.transform.SetParent(fxContainer, false);

        RectTransform rect = pObj.GetComponent<RectTransform>();
        float size = Random.Range(trailParticleSizeRange.x, trailParticleSizeRange.y);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Image img = pObj.GetComponent<Image>();
        img.sprite = particleSprite;
        if (additiveMaterial != null) img.material = additiveMaterial;

        Color col = GetRandomVibrantColor();
        img.color = col;

        StartCoroutine(AnimateTrailParticle(rect, img, trailParticleLifetime));
    }

    private IEnumerator AnimateTrailParticle(RectTransform rect, Image img, float duration)
    {
        float elapsed = 0f;
        Vector3 initialScale = rect.localScale;
        Color startColor = img.color;
        Vector2 floatDir = new Vector2(Random.Range(-20f, 20f), Random.Range(20f, 60f));

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Float upward slightly & shrink
            rect.anchoredPosition += floatDir * Time.deltaTime;
            rect.localScale = Vector3.Lerp(initialScale, Vector3.zero, t * t);

            // Fade out alpha
            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    /// <summary>
    /// Context menu option & public method to test the completion effect at runtime.
    /// </summary>
    [ContextMenu("Test Completion Burst")]
    public void TestCompletionEffect()
    {
        OnLetterCompleted();
    }

    /// <summary>
    /// Triggered automatically when PenDrawer completes 100% mask fill.
    /// </summary>
    public void OnLetterCompleted()
    {
        if (Time.time - lastCompletionFxTime < 0.15f)
        {
            return;
        }

        lastCompletionFxTime = Time.time;
        ResolveReferences();
        EnsureFXContainerOnTop();

        Vector2 centerPos = Vector2.zero;

        if (designLetterTransform != null && fxContainer != null)
        {
            centerPos = fxContainer.InverseTransformPoint(designLetterTransform.position);
        }

        if (enableCompletionBurst)
        {
            StartCoroutine(TriggerCompletionBurstRoutine(centerPos));
        }

        if (enableReferenceStyleLetterSweep)
        {
            StartCoroutine(PlayReferenceStyleLetterSweepRoutine());
        }

        if (enableGuaranteedCompletionFlash)
        {
            SpawnGuaranteedCompletionFlash(centerPos, true, Mathf.Max(2.05f, referenceCompletionDuration));
        }

        if (animateLetterOnCompletion)
        {
            if (designLetterTransform != null) StartCoroutine(AnimateLetterBounceRoutine(designLetterTransform));
            if (dottedLetterTransform != null) StartCoroutine(AnimateLetterBounceRoutine(dottedLetterTransform));
        }

        if (playDelayedCompletionAccent)
        {
            StartCoroutine(DelayedCompletionAccentRoutine(centerPos));
        }
    }

    private IEnumerator PlayReferenceStyleLetterSweepRoutine()
    {
        if (fxContainer == null || designLetterTransform == null)
        {
            yield break;
        }

        List<List<Vector2>> strokePaths = BuildCompletionSweepPaths();
        if (strokePaths.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < strokePaths.Count; i++)
        {
            StartCoroutine(AnimateCompletionSweepStroke(strokePaths[i], i * completionSweepStepDelay));
        }

        yield return null;
    }

    private List<List<Vector2>> BuildCompletionSweepPaths()
    {
        List<List<Vector2>> paths = new List<List<Vector2>>();

        LetterSequence letter = penDrawer != null ? penDrawer.CurrentLetterSequence : null;
        IReadOnlyList<TracingStrokeStep> steps = letter != null ? letter.StrokeSteps : null;

        if (steps != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                TracingStrokeStep step = steps[i];
                if (step == null)
                {
                    continue;
                }

                List<Vector2> localPath = new List<Vector2>();
                if (!step.TryBuildHintPath(localPath) || localPath.Count < 2)
                {
                    continue;
                }

                paths.Add(ConvertRevealPathToFxPath(localPath));
            }
        }

        if (paths.Count == 0 && penDrawer != null)
        {
            List<Vector2> fallbackPath = new List<Vector2>();
            if (penDrawer.TryGetActiveHintPathLocal(fallbackPath) && fallbackPath.Count >= 2)
            {
                paths.Add(ConvertRevealPathToFxPath(fallbackPath));
            }
        }

        if (paths.Count == 0 && designLetterTransform != null)
        {
            Rect rect = designLetterTransform.rect;
            List<Vector2> fallbackPath = new List<Vector2>
            {
                new Vector2(rect.xMin + rect.width * 0.18f, rect.center.y),
                new Vector2(rect.center.x, rect.yMax - rect.height * 0.15f),
                new Vector2(rect.xMax - rect.width * 0.18f, rect.center.y)
            };
            paths.Add(ConvertRevealPathToFxPath(fallbackPath));
        }

        return paths;
    }

    private List<Vector2> ConvertRevealPathToFxPath(List<Vector2> revealLocalPath)
    {
        List<Vector2> fxPath = new List<Vector2>();
        if (revealLocalPath == null || designLetterTransform == null || fxContainer == null)
        {
            return fxPath;
        }

        for (int i = 0; i < revealLocalPath.Count; i++)
        {
            Vector3 worldPoint = designLetterTransform.TransformPoint(revealLocalPath[i]);
            fxPath.Add(fxContainer.InverseTransformPoint(worldPoint));
        }

        return fxPath;
    }

    private IEnumerator AnimateCompletionSweepStroke(List<Vector2> path, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (path == null || path.Count < 2 || fxContainer == null)
        {
            yield break;
        }

        GameObject strokeObj = new GameObject("FX_ReferenceLetterSweep", typeof(RectTransform), typeof(UILine));
        strokeObj.transform.SetParent(fxContainer, false);
        strokeObj.transform.SetAsLastSibling();

        RectTransform rect = strokeObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        UILine line = strokeObj.GetComponent<UILine>();
        line.raycastTarget = false;
        line.material = null;
        line.color = completionSweepColor;
        line.thickness = completionSweepThickness;

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, completionSweepStrokeDuration);
        float totalLength = GetPathLength(path);
        int sparkleIndex = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            float distance = totalLength * eased;

            RebuildPartialLine(line, path, distance);

            if (sparkleIndex < 5 && t >= sparkleIndex / 5f)
            {
                Vector2 sparklePos = GetPointAtDistance(path, distance);
                SpawnSweepSparkle(sparklePos);
                sparkleIndex++;
            }

            yield return null;
        }

        RebuildPartialLine(line, path, totalLength);
        yield return StartCoroutine(FadeSweepLine(line, 0.45f));

        Destroy(strokeObj);
    }

    private void RebuildPartialLine(UILine line, List<Vector2> path, float distance)
    {
        if (line == null || path == null || path.Count < 2)
        {
            return;
        }

        line.ClearPoints();
        line.AddPoint(path[0]);

        float travelled = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2 from = path[i];
            Vector2 to = path[i + 1];
            float segmentLength = Vector2.Distance(from, to);
            if (segmentLength <= 0.01f)
            {
                continue;
            }

            if (travelled + segmentLength <= distance)
            {
                line.AddPoint(to);
                travelled += segmentLength;
                continue;
            }

            float segmentT = Mathf.Clamp01((distance - travelled) / segmentLength);
            line.AddPoint(Vector2.Lerp(from, to, segmentT));
            return;
        }
    }

    private IEnumerator FadeSweepLine(UILine line, float duration)
    {
        if (line == null)
        {
            yield break;
        }

        Color startColor = line.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            line.color = c;
            yield return null;
        }
    }

    private void SpawnSweepSparkle(Vector2 position)
    {
        Sprite sparkleSprite = GetCompletionParticleSprite(GetGeneratedSoftCircleSprite());
        if (sparkleSprite == null || fxContainer == null)
        {
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir == Vector2.zero) dir = Vector2.up;
            SpawnVisibleFallbackSpark(position, dir, Random.Range(75f, 180f), Random.Range(22f, 46f), Random.Range(0.35f, 0.6f), sparkleSprite);
        }
    }

    private float GetPathLength(List<Vector2> path)
    {
        if (path == null || path.Count < 2)
        {
            return 0f;
        }

        float length = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            length += Vector2.Distance(path[i], path[i + 1]);
        }

        return length;
    }

    private Vector2 GetPointAtDistance(List<Vector2> path, float targetDistance)
    {
        if (path == null || path.Count == 0)
        {
            return Vector2.zero;
        }

        if (path.Count == 1)
        {
            return path[0];
        }

        float travelled = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2 from = path[i];
            Vector2 to = path[i + 1];
            float segmentLength = Vector2.Distance(from, to);
            if (segmentLength <= 0.01f)
            {
                continue;
            }

            if (travelled + segmentLength >= targetDistance)
            {
                float t = Mathf.Clamp01((targetDistance - travelled) / segmentLength);
                return Vector2.Lerp(from, to, t);
            }

            travelled += segmentLength;
        }

        return path[path.Count - 1];
    }

    private IEnumerator TriggerCompletionBurstRoutine(Vector2 center)
    {
        EnsureFXContainerOnTop();

        // 0. Letter-shaped magic flash, so the reward feels attached to the completed letter.
        if (enableLetterGlowPulse && designLetterTransform != null)
        {
            for (int i = 0; i < Mathf.Max(1, letterGlowPulseCount); i++)
            {
                float delay = i * 0.08f;
                float scaleBoost = letterGlowPulseScale * (1f + i * 0.25f);
                SpawnLetterGlowPulse(designLetterTransform, delay, letterGlowPulseDuration, scaleBoost);
            }
        }

        // 1. Light Rays Burst (GlowFxLightRays)
        if (lightRaysSprite != null)
        {
            SpawnLightRays(center, 1.2f);
        }

        // 2. Center Starburst Flash (Glow1 / Glow3)
        if (starburstSprite != null || glowSprite != null)
        {
            SpawnStarburst(center, 0.7f);
        }

        // 3. Expanding soft ripples around the completed letter
        if (enableCompletionRipples && (glowSprite != null || starburstSprite != null))
        {
            for (int i = 0; i < Mathf.Max(1, completionRippleCount); i++)
            {
                SpawnCompletionRipple(center, i * 0.1f, 0.75f + i * 0.1f, i);
            }
        }

        // 4. Particle Explosion (ui_glow_spark)
        Sprite particleSprite = sparkSprite != null ? sparkSprite : glowSprite;
        if (particleSprite != null)
        {
            for (int i = 0; i < burstParticleCount; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = Random.Range(burstSpeedRange.x, burstSpeedRange.y);
                float size = Random.Range(30f, 75f);
                Color color = GetRandomVibrantColor();

                SpawnBurstParticle(center, dir, speed, size, color, Random.Range(0.7f, 1.1f));
            }
        }

        // 5. Upward sparkle shower like a small celebratory magic spray
        if (enableCompletionSparkleShower && particleSprite != null)
        {
            SpawnSparkleShower(center);
        }

        // 6. Sheen Sweep Shine across letter
        if (sheenSprite != null && designLetterTransform != null)
        {
            SpawnSheenSweep(center, 0.8f);
        }

        yield return null;

        EnsureFXContainerOnTop();
    }

    private IEnumerator DelayedCompletionAccentRoutine(Vector2 center)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delayedCompletionAccentDelay));
        EnsureFXContainerOnTop();

        if (enableGuaranteedCompletionFlash)
        {
            SpawnGuaranteedCompletionFlash(center, false, 0.55f, 340f, 0.62f);
        }

        if (starburstSprite != null || glowSprite != null)
        {
            SpawnStarburst(center, 0.45f);
        }

        if (sparkSprite != null || glowSprite != null)
        {
            for (int i = 0; i < 14; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                SpawnBurstParticle(center, dir, Random.Range(220f, 420f), Random.Range(35f, 80f), GetRandomVibrantColor(), 0.65f);
            }
        }
    }

    private void SpawnGuaranteedCompletionFlash(Vector2 center, bool coverScreen, float duration = 0.9f, float size = 460f, float maxAlpha = 0.78f)
    {
        if (fxContainer == null)
        {
            return;
        }

        Sprite flashSprite = GetGeneratedSoftCircleSprite();
        if (flashSprite == null)
        {
            return;
        }

        if (coverScreen)
        {
            SpawnFullScreenCompletionWash(duration);
            SpawnReferenceCompletionCelebration(duration);
        }

        GameObject flashObj = new GameObject("FX_GuaranteedCompletionFlash", typeof(RectTransform), typeof(Image));
        flashObj.transform.SetParent(fxContainer, false);
        flashObj.transform.SetAsLastSibling();

        RectTransform rect = flashObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = center;
        rect.localScale = Vector3.one * 0.2f;

        Image img = flashObj.GetComponent<Image>();
        img.sprite = flashSprite;
        img.raycastTarget = false;
        img.material = null;
        img.color = new Color(1f, 0.92f, 0.12f, 0f);

        StartCoroutine(AnimateGuaranteedCompletionFlash(rect, img, duration, maxAlpha));

        Sprite particleSprite = GetCompletionParticleSprite(flashSprite);
        for (int i = 0; i < 18; i++)
        {
            float angle = ((float)i / 18f * 360f + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpawnVisibleFallbackSpark(center, dir, Random.Range(190f, 380f), Random.Range(24f, 52f), Random.Range(0.55f, 0.9f), particleSprite);
        }
    }

    private void SpawnFullScreenCompletionWash(float duration)
    {
        if (fxContainer == null)
        {
            return;
        }

        GameObject washObj = new GameObject("FX_FullScreenCompletionWash", typeof(RectTransform), typeof(Image));
        washObj.transform.SetParent(fxContainer, false);
        washObj.transform.SetAsLastSibling();

        RectTransform rect = washObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image img = washObj.GetComponent<Image>();
        img.raycastTarget = false;
        img.material = null;
        img.color = new Color(1f, 0.78f, 0.12f, 0f);

        StartCoroutine(AnimateFullScreenCompletionWash(rect, img, duration));
    }

    private IEnumerator AnimateFullScreenCompletionWash(RectTransform rect, Image img, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Color c = Color.Lerp(
                new Color(1f, 0.72f, 0.1f, 0f),
                new Color(0.18f, 0.92f, 1f, 0f),
                Mathf.Clamp01((t - 0.18f) / 0.65f));
            c.a = Mathf.Sin(t * Mathf.PI) * fullScreenFlashMaxAlpha;
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnReferenceCompletionCelebration(float duration)
    {
        if (enableReferenceWhiteStreaks)
        {
            SpawnReferenceWhiteStreaks(duration);
        }

        if (enableReferenceConfetti)
        {
            SpawnReferenceConfetti(duration);
        }

        if (enableReferenceBalloons)
        {
            SpawnReferenceBalloons(duration);
        }

        SpawnFullScreenSparkles(duration);
    }

    private void SpawnReferenceBalloons(float duration)
    {
        if (fxContainer == null || completionBalloonSprites == null || completionBalloonSprites.Length == 0)
        {
            return;
        }

        Rect rect = fxContainer.rect;
        int count = Mathf.Max(1, referenceBalloonCount);
        for (int i = 0; i < count; i++)
        {
            Sprite sprite = completionBalloonSprites[i % completionBalloonSprites.Length];
            if (sprite == null)
            {
                continue;
            }

            float fanT = count == 1 ? 0.5f : (float)i / (count - 1);
            float targetX = Mathf.Lerp(rect.xMin + rect.width * 0.08f, rect.xMax - rect.width * 0.08f, fanT);
            targetX += Random.Range(-45f, 45f);
            float targetY = rect.yMax - Random.Range(50f, 180f);
            Vector2 start = GetCompletionCenter();
            start += Random.insideUnitCircle * Random.Range(0f, 45f);
            float size = Random.Range(
                Mathf.Min(referenceBalloonHeightRange.x, referenceBalloonHeightRange.y),
                Mathf.Max(referenceBalloonHeightRange.x, referenceBalloonHeightRange.y));
            float delay = Random.Range(0f, 0.28f);

            SpawnReferenceBalloon(sprite, start, new Vector2(targetX, targetY), size, delay, duration + Random.Range(0.7f, 1.2f));
        }
    }

    private void SpawnReferenceBalloon(Sprite sprite, Vector2 start, Vector2 target, float height, float delay, float duration)
    {
        GameObject obj = new GameObject("FX_ReferenceBalloon", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(fxContainer, false);
        obj.transform.SetAsLastSibling();

        RectTransform rect = obj.GetComponent<RectTransform>();
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        rect.sizeDelta = new Vector2(height * aspect, height);
        rect.anchoredPosition = start;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-8f, 8f));

        Image img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.material = null;
        img.color = Color.white;

        StartCoroutine(AnimateReferenceBalloon(rect, img, start, target, delay, duration));
    }

    private IEnumerator AnimateReferenceBalloon(RectTransform rect, Image img, Vector2 start, Vector2 target, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        float swayPhase = Random.Range(0f, Mathf.PI * 2f);
        float swayAmount = Random.Range(18f, 42f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.55f), 3f);

            Vector2 pos = Vector2.Lerp(start, target, eased);
            pos.x += Mathf.Sin(Time.time * 1.8f + swayPhase) * swayAmount;
            rect.anchoredPosition = pos;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.4f + swayPhase) * 5f);

            Color c = img.color;
            c.a = t < 0.82f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.82f) / 0.18f);
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnReferenceConfetti(float duration)
    {
        if (fxContainer == null || completionConfettiSprites == null || completionConfettiSprites.Length == 0)
        {
            return;
        }

        Vector2 center = GetCompletionCenter();

        int count = Mathf.Max(1, referenceConfettiCount);
        for (int i = 0; i < count; i++)
        {
            Sprite sprite = completionConfettiSprites[Random.Range(0, completionConfettiSprites.Length)];
            if (sprite == null)
            {
                continue;
            }

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 origin = center + Random.insideUnitCircle * Random.Range(20f, 110f);
            Vector2 velocity = dir * Random.Range(260f, 760f) + Vector2.up * Random.Range(40f, 260f);
            float size = Random.Range(26f, 72f);
            float delay = Random.Range(0f, 0.22f);

            SpawnReferenceConfettiPiece(sprite, origin, velocity, size, delay, duration + Random.Range(0.25f, 0.9f));
        }
    }

    private void SpawnReferenceConfettiPiece(Sprite sprite, Vector2 origin, Vector2 velocity, float size, float delay, float duration)
    {
        GameObject obj = new GameObject("FX_ReferenceConfetti", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(fxContainer, false);
        obj.transform.SetAsLastSibling();

        RectTransform rect = obj.GetComponent<RectTransform>();
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        rect.sizeDelta = new Vector2(size * aspect, size);
        rect.anchoredPosition = origin;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Image img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.material = null;
        img.color = Color.white;

        StartCoroutine(AnimateReferenceConfettiPiece(rect, img, velocity, delay, duration));
    }

    private IEnumerator AnimateReferenceConfettiPiece(RectTransform rect, Image img, Vector2 velocity, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        float spin = Random.Range(-720f, 720f);
        Vector2 gravity = new Vector2(0f, -380f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            velocity += gravity * Time.deltaTime;
            rect.anchoredPosition += velocity * Time.deltaTime;
            rect.Rotate(0f, 0f, spin * Time.deltaTime);
            rect.localScale = Vector3.one * (0.85f + Mathf.Sin(t * Mathf.PI * 4f) * 0.15f);

            Color c = img.color;
            c.a = t < 0.78f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.78f) / 0.22f);
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnReferenceWhiteStreaks(float duration)
    {
        if (fxContainer == null)
        {
            return;
        }

        Sprite streak = whiteStreakSprite != null ? whiteStreakSprite : sheenSprite;
        if (streak == null)
        {
            return;
        }

        Vector2 center = GetCompletionCenter();

        for (int i = 0; i < 10; i++)
        {
            float angle = (i / 10f * 360f + Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 pos = center + dir * Random.Range(150f, 380f);
            SpawnReferenceWhiteStreak(streak, pos, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, Random.Range(160f, 280f), Random.Range(0f, 0.25f), duration * 0.9f);
        }
    }

    private void SpawnReferenceWhiteStreak(Sprite sprite, Vector2 position, float angle, float size, float delay, float duration)
    {
        GameObject obj = new GameObject("FX_ReferenceWhiteStreak", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(fxContainer, false);
        obj.transform.SetAsLastSibling();

        RectTransform rect = obj.GetComponent<RectTransform>();
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        rect.sizeDelta = new Vector2(size * aspect, size);
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.material = null;
        img.color = new Color(1f, 1f, 1f, 0f);

        StartCoroutine(AnimateReferenceWhiteStreak(rect, img, delay, duration));
    }

    private IEnumerator AnimateReferenceWhiteStreak(RectTransform rect, Image img, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        Vector3 startScale = rect.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.localScale = startScale * Mathf.Lerp(0.4f, 1.2f, Mathf.Sin(t * Mathf.PI));

            Color c = img.color;
            c.a = Mathf.Sin(t * Mathf.PI) * 0.9f;
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnFullScreenSparkles(float duration)
    {
        if (fxContainer == null)
        {
            return;
        }

        Sprite particleSprite = GetCompletionParticleSprite(GetGeneratedSoftCircleSprite());
        if (particleSprite == null)
        {
            return;
        }

        Rect rect = fxContainer.rect;
        int count = Mathf.Max(1, fullScreenSparkleCount);
        for (int i = 0; i < count; i++)
        {
            Vector2 origin = new Vector2(
                Random.Range(rect.xMin, rect.xMax),
                Random.Range(rect.yMin, rect.yMax));
            Vector2 velocity = new Vector2(Random.Range(-120f, 120f), Random.Range(90f, 360f));
            float size = Random.Range(28f, 86f);
            float delay = Random.Range(0f, 0.28f);
            Color color = GetRandomVibrantColor();
            StartCoroutine(AnimateFullScreenSparkle(origin, velocity, size, color, delay, duration + Random.Range(0.2f, 0.65f), particleSprite));
        }
    }

    private Sprite GetCompletionParticleSprite(Sprite fallback)
    {
        if (whiteSparkleSprite != null)
        {
            return whiteSparkleSprite;
        }

        if (sparkSprite != null)
        {
            return sparkSprite;
        }

        return fallback;
    }

    private Vector2 GetCompletionCenter()
    {
        if (designLetterTransform != null && fxContainer != null)
        {
            return fxContainer.InverseTransformPoint(designLetterTransform.position);
        }

        return Vector2.zero;
    }

    private IEnumerator AnimateFullScreenSparkle(Vector2 origin, Vector2 velocity, float size, Color color, float delay, float duration, Sprite particleSprite)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (fxContainer == null || particleSprite == null)
        {
            yield break;
        }

        GameObject sparkObj = new GameObject("FX_FullScreenSparkle", typeof(RectTransform), typeof(Image));
        sparkObj.transform.SetParent(fxContainer, false);
        sparkObj.transform.SetAsLastSibling();

        RectTransform rect = sparkObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = origin;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Image img = sparkObj.GetComponent<Image>();
        img.sprite = particleSprite;
        img.raycastTarget = false;
        img.material = null;
        img.color = color;

        float elapsed = 0f;
        float spinSpeed = Random.Range(-500f, 500f);
        Vector2 gravity = new Vector2(0f, -250f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            velocity += gravity * Time.deltaTime;
            rect.anchoredPosition += velocity * Time.deltaTime;
            rect.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            float pulse = 0.85f + Mathf.Sin(t * Mathf.PI * 5f) * 0.2f;
            rect.localScale = Vector3.one * Mathf.Lerp(pulse, 0f, t * t);

            Color c = color;
            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Destroy(sparkObj);
    }

    private IEnumerator AnimateGuaranteedCompletionFlash(RectTransform rect, Image img, float duration, float maxAlpha)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(0.25f, 1.65f, 1f - Mathf.Pow(1f - t, 3f));
            rect.localScale = new Vector3(scale, scale, 1f);

            Color c = img.color;
            c.a = Mathf.Sin(t * Mathf.PI) * maxAlpha;
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnVisibleFallbackSpark(Vector2 center, Vector2 dir, float speed, float size, float duration, Sprite particleSprite)
    {
        if (fxContainer == null || particleSprite == null)
        {
            return;
        }

        GameObject sparkObj = new GameObject("FX_VisibleCompletionSpark", typeof(RectTransform), typeof(Image));
        sparkObj.transform.SetParent(fxContainer, false);
        sparkObj.transform.SetAsLastSibling();

        RectTransform rect = sparkObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = center;

        Image img = sparkObj.GetComponent<Image>();
        img.sprite = particleSprite;
        img.raycastTarget = false;
        img.material = null;
        img.color = GetRandomVibrantColor();

        StartCoroutine(AnimateVisibleFallbackSpark(rect, img, dir * speed, duration));
    }

    private IEnumerator AnimateVisibleFallbackSpark(RectTransform rect, Image img, Vector2 velocity, float duration)
    {
        float elapsed = 0f;
        Color startColor = img.color;
        float spinSpeed = Random.Range(-360f, 360f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            rect.anchoredPosition += velocity * Time.deltaTime;
            velocity = Vector2.Lerp(velocity, Vector2.zero, Time.deltaTime * 2.8f);
            rect.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            rect.localScale = Vector3.one * Mathf.Lerp(1.1f, 0f, t * t);

            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private Sprite GetGeneratedSoftCircleSprite()
    {
        if (generatedSoftCircleSprite != null)
        {
            return generatedSoftCircleSprite;
        }

        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "Generated Completion Soft Circle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.48f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - normalizedDistance);
                alpha = alpha * alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        generatedSoftCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return generatedSoftCircleSprite;
    }

    private void SpawnLetterGlowPulse(RectTransform sourceTransform, float delay, float duration, float scaleBoost)
    {
        if (sourceTransform == null || fxContainer == null)
        {
            return;
        }

        Image sourceImage = sourceTransform.GetComponent<Image>();
        if (sourceImage == null || sourceImage.sprite == null)
        {
            return;
        }

        GameObject glowObj = new GameObject("FX_LetterGlowPulse", typeof(RectTransform), typeof(Image));
        glowObj.transform.SetParent(fxContainer, false);

        RectTransform rect = glowObj.GetComponent<RectTransform>();
        rect.sizeDelta = sourceTransform.rect.size;
        rect.position = sourceTransform.position;
        rect.localRotation = sourceTransform.localRotation;
        rect.localScale = sourceTransform.lossyScale;

        Image img = glowObj.GetComponent<Image>();
        img.sprite = sourceImage.sprite;
        img.preserveAspect = sourceImage.preserveAspect;
        img.raycastTarget = false;
        if (additiveMaterial != null) img.material = additiveMaterial;
        img.color = new Color(1f, 0.95f, 0.35f, 0f);

        StartCoroutine(AnimateLetterGlowPulse(rect, img, delay, duration, scaleBoost));
    }

    private IEnumerator AnimateLetterGlowPulse(RectTransform rect, Image img, float delay, float duration, float scaleBoost)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        Vector3 startScale = rect.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float alpha = Mathf.Sin(t * Mathf.PI) * 0.85f;

            rect.localScale = startScale * (1f + scaleBoost * easeOut);

            Color c = img.color;
            c.a = alpha;
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnCompletionRipple(Vector2 position, float delay, float duration, int index)
    {
        if (fxContainer == null)
        {
            return;
        }

        Sprite rippleSprite = glowSprite != null ? glowSprite : starburstSprite;
        if (rippleSprite == null)
        {
            return;
        }

        GameObject rippleObj = new GameObject("FX_CompletionRipple", typeof(RectTransform), typeof(Image));
        rippleObj.transform.SetParent(fxContainer, false);

        RectTransform rect = rippleObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240f, 240f);
        rect.anchoredPosition = position;

        Image img = rippleObj.GetComponent<Image>();
        img.sprite = rippleSprite;
        img.raycastTarget = false;
        if (additiveMaterial != null) img.material = additiveMaterial;
        img.color = index % 2 == 0
            ? new Color(0.2f, 0.95f, 1f, 0f)
            : new Color(1f, 0.55f, 0.95f, 0f);

        StartCoroutine(AnimateCompletionRipple(rect, img, delay, duration));
    }

    private IEnumerator AnimateCompletionRipple(RectTransform rect, Image img, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        Color startColor = img.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeOut = 1f - Mathf.Pow(1f - t, 2f);
            float scale = Mathf.Lerp(0.35f, 2.35f, easeOut);

            rect.localScale = new Vector3(scale, scale, 1f);
            rect.Rotate(0f, 0f, 35f * Time.deltaTime);

            Color c = startColor;
            c.a = Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(0.55f, 0.1f, t);
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnSparkleShower(Vector2 center)
    {
        Sprite particleSprite = sparkSprite != null ? sparkSprite : glowSprite;
        if (particleSprite == null || fxContainer == null)
        {
            return;
        }

        int count = Mathf.Max(1, sparkleShowerParticleCount);
        for (int i = 0; i < count; i++)
        {
            float delay = Random.Range(0f, 0.28f);
            Vector2 origin = center + new Vector2(Random.Range(-120f, 120f), Random.Range(-30f, 80f));
            Vector2 velocity = new Vector2(Random.Range(-90f, 90f), Random.Range(170f, 330f));
            float size = Random.Range(18f, 46f);
            Color color = GetRandomVibrantColor();
            StartCoroutine(AnimateSparkleShowerParticle(origin, velocity, size, color, delay, Random.Range(0.75f, 1.15f)));
        }
    }

    private IEnumerator AnimateSparkleShowerParticle(Vector2 origin, Vector2 velocity, float size, Color color, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        Sprite particleSprite = sparkSprite != null ? sparkSprite : glowSprite;
        if (particleSprite == null || fxContainer == null)
        {
            yield break;
        }

        GameObject pObj = new GameObject("SparkleShowerParticle", typeof(RectTransform), typeof(Image));
        pObj.transform.SetParent(fxContainer, false);

        RectTransform rect = pObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = origin;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Image img = pObj.GetComponent<Image>();
        img.sprite = particleSprite;
        img.raycastTarget = false;
        if (additiveMaterial != null) img.material = additiveMaterial;
        img.color = color;

        float elapsed = 0f;
        float spinSpeed = Random.Range(-260f, 260f);
        Vector2 gravity = new Vector2(0f, -280f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            velocity += gravity * Time.deltaTime;
            rect.anchoredPosition += velocity * Time.deltaTime;
            rect.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            float scale = Mathf.Lerp(0.8f, 0f, t * t);
            rect.localScale = new Vector3(scale, scale, 1f);

            Color c = color;
            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Destroy(pObj);
    }

    private void SpawnLightRays(Vector2 position, float duration, float sizeMultiplier = 1f)
    {
        GameObject raysObj = new GameObject("FX_LightRays", typeof(RectTransform), typeof(Image));
        raysObj.transform.SetParent(fxContainer, false);

        RectTransform rect = raysObj.GetComponent<RectTransform>();
        float size = 300f * Mathf.Max(0.1f, sizeMultiplier);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = position;

        Image img = raysObj.GetComponent<Image>();
        img.sprite = lightRaysSprite;
        if (additiveMaterial != null) img.material = additiveMaterial;
        img.color = new Color(1f, 0.9f, 0.5f, 1f); // Warm Gold

        StartCoroutine(AnimateLightRays(rect, img, duration));
    }

    private IEnumerator AnimateLightRays(RectTransform rect, Image img, float duration)
    {
        float elapsed = 0f;
        Color startCol = img.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Expand and spin
            float scale = Mathf.Lerp(0.3f, 2.2f, Mathf.Sin(t * Mathf.PI * 0.5f));
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.Rotate(0f, 0f, 90f * Time.deltaTime);

            // Fade out
            Color c = startCol;
            c.a = Mathf.Lerp(1f, 0f, t * t);
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnStarburst(Vector2 position, float duration, float sizeMultiplier = 1f)
    {
        GameObject starObj = new GameObject("FX_Starburst", typeof(RectTransform), typeof(Image));
        starObj.transform.SetParent(fxContainer, false);

        RectTransform rect = starObj.GetComponent<RectTransform>();
        float size = 250f * Mathf.Max(0.1f, sizeMultiplier);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = position;

        Image img = starObj.GetComponent<Image>();
        img.sprite = starburstSprite != null ? starburstSprite : glowSprite;
        if (additiveMaterial != null) img.material = additiveMaterial;
        img.color = Color.white;

        StartCoroutine(AnimateStarburst(rect, img, duration));
    }

    private IEnumerator AnimateStarburst(RectTransform rect, Image img, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scale = Mathf.Lerp(0.2f, 1.8f, Mathf.Sin(t * Mathf.PI * 0.5f));
            rect.localScale = new Vector3(scale, scale, 1f);

            Color c = Color.white;
            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnBurstParticle(Vector2 origin, Vector2 dir, float speed, float size, Color color, float duration)
    {
        GameObject pObj = new GameObject("BurstParticle", typeof(RectTransform), typeof(Image));
        pObj.transform.SetParent(fxContainer, false);

        RectTransform rect = pObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = origin;

        Image img = pObj.GetComponent<Image>();
        img.sprite = sparkSprite != null ? sparkSprite : glowSprite;
        if (additiveMaterial != null) img.material = additiveMaterial;
        img.color = color;

        StartCoroutine(AnimateBurstParticle(rect, img, dir, speed, duration));
    }

    private IEnumerator AnimateBurstParticle(RectTransform rect, Image img, Vector2 dir, float speed, float duration)
    {
        float elapsed = 0f;
        Vector2 velocity = dir * speed;
        Color startCol = img.color;
        float spinSpeed = Random.Range(-360f, 360f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rect.anchoredPosition += velocity * Time.deltaTime;
            velocity = Vector2.Lerp(velocity, Vector2.zero, Time.deltaTime * 3f); // Slow down drag

            rect.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            float scale = Mathf.Lerp(1f, 0f, t * t);
            rect.localScale = new Vector3(scale, scale, 1f);

            Color c = startCol;
            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private void SpawnSheenSweep(Vector2 position, float duration)
    {
        GameObject sheenObj = new GameObject("FX_Sheen", typeof(RectTransform), typeof(Image));
        sheenObj.transform.SetParent(fxContainer, false);

        RectTransform rect = sheenObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250f, 250f);
        rect.anchoredPosition = position + new Vector2(-150f, 150f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -45f);

        Image img = sheenObj.GetComponent<Image>();
        img.sprite = sheenSprite;
        if (additiveMaterial != null) img.material = additiveMaterial;
        img.color = new Color(1f, 1f, 1f, 0.8f);

        StartCoroutine(AnimateSheenSweep(rect, img, position, duration));
    }

    private IEnumerator AnimateSheenSweep(RectTransform rect, Image img, Vector2 targetCenter, float duration)
    {
        float elapsed = 0f;
        Vector2 startPos = targetCenter + new Vector2(-200f, 200f);
        Vector2 endPos = targetCenter + new Vector2(200f, -200f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            Color c = img.color;
            c.a = Mathf.Sin(t * Mathf.PI) * 0.85f;
            img.color = c;

            yield return null;
        }

        Destroy(rect.gameObject);
    }

    private IEnumerator AnimateLetterBounceRoutine(RectTransform targetTransform)
    {
        if (targetTransform == null) yield break;

        Vector3 originalScale = targetTransform.localScale;
        Quaternion originalRot = targetTransform.localRotation;

        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bounceDuration;

            // Elastic scale curve (Punch up to 1.25 -> back to 1.0)
            float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * (peakScale - 1f);
            targetTransform.localScale = originalScale * scaleMultiplier;

            // Playful wobble angle
            float tiltAngle = Mathf.Sin(t * Mathf.PI * 3f) * 6f * (1f - t);
            targetTransform.localRotation = originalRot * Quaternion.Euler(0f, 0f, tiltAngle);

            yield return null;
        }

        targetTransform.localScale = originalScale;
        targetTransform.localRotation = originalRot;
    }

    private Color GetRandomVibrantColor()
    {
        if (vibrantColors != null && vibrantColors.Length > 0)
        {
            return vibrantColors[Random.Range(0, vibrantColors.Length)];
        }
        return Color.yellow;
    }

    private Vector2 GetMouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pen.current != null)
        {
            Vector2 penPos = Pen.current.position.ReadValue();
            if (penPos != Vector2.zero) return penPos;
        }
        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (mousePos != Vector2.zero) return mousePos;
        }
        if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    private bool IsMouseHeldDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pen.current != null && (Pen.current.tip.isPressed || Pen.current.press.isPressed)) return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        if (Pointer.current != null && Pointer.current.press.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#else
        return false;
#endif
    }

    /// <summary>
    /// Plays the spiral magic despawn/spawn transition effect when switching letters.
    /// Executes onSwitchCallback at scale 0 to swap letter sprites.
    /// </summary>
    public void PlayLetterTransition(System.Action onSwitchCallback)
    {
        if (IsTransitioning) return;
        StartCoroutine(SpiralTransitionRoutine(onSwitchCallback));
    }

    private IEnumerator SpiralTransitionRoutine(System.Action onSwitchCallback)
    {
        IsTransitioning = true;

        // Auto-detect letter rect transforms if unassigned
        if (letterSwitcher != null)
        {
            if (designLetterTransform == null && letterSwitcher.DesignLetterObject != null)
                designLetterTransform = letterSwitcher.DesignLetterObject.GetComponent<RectTransform>();
            if (dottedLetterTransform == null && letterSwitcher.DottedLetterObject != null)
                dottedLetterTransform = letterSwitcher.DottedLetterObject.GetComponent<RectTransform>();
        }

        Vector2 centerPos = Vector2.zero;
        if (designLetterTransform != null && fxContainer != null)
        {
            centerPos = fxContainer.InverseTransformPoint(designLetterTransform.position);
        }

        Vector3 designOrigScale = designLetterTransform != null ? designLetterTransform.localScale : Vector3.one;
        Quaternion designOrigRot = designLetterTransform != null ? designLetterTransform.localRotation : Quaternion.identity;

        Vector3 dottedOrigScale = dottedLetterTransform != null ? dottedLetterTransform.localScale : Vector3.one;
        Quaternion dottedOrigRot = dottedLetterTransform != null ? dottedLetterTransform.localRotation : Quaternion.identity;

        float halfDuration = transitionDuration * 0.5f;

        // --- PHASE 1: DESPAWN (Swirl & Shrink into Center) ---
        StartCoroutine(SpawnInwardSpiralParticles(centerPos, halfDuration));

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float easeT = t * t; // Acceleration

            float scaleMult = Mathf.Lerp(1f, 0f, easeT);
            float rotAngle = easeT * spiralRotations * 360f;

            if (designLetterTransform != null)
            {
                designLetterTransform.localScale = designOrigScale * scaleMult;
                designLetterTransform.localRotation = designOrigRot * Quaternion.Euler(0f, 0f, rotAngle);
            }
            if (dottedLetterTransform != null)
            {
                dottedLetterTransform.localScale = dottedOrigScale * scaleMult;
                dottedLetterTransform.localRotation = dottedOrigRot * Quaternion.Euler(0f, 0f, rotAngle);
            }

            yield return null;
        }

        if (designLetterTransform != null) designLetterTransform.localScale = Vector3.zero;
        if (dottedLetterTransform != null) dottedLetterTransform.localScale = Vector3.zero;

        // Central Magic Starburst Flash
        if (starburstSprite != null || glowSprite != null)
        {
            SpawnStarburst(centerPos, 0.55f, letterLoadBurstScale);
        }

        // --- PHASE 2: SWITCH SPRITE & CLEAR STROKES ---
        onSwitchCallback?.Invoke();
        yield return null;

        // --- PHASE 3: SPAWN (Swirl & Expand from Center) ---
        StartCoroutine(SpawnOutwardSpiralParticles(centerPos, halfDuration));

        if (lightRaysSprite != null)
        {
            SpawnLightRays(centerPos, 0.85f, letterLoadBurstScale);
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);

            // Elastic pop curve: 0 -> 1.15 -> 1.0
            float scaleMult = (t < 0.7f)
                ? Mathf.Lerp(0f, 1.15f, t / 0.7f)
                : Mathf.Lerp(1.15f, 1.0f, (t - 0.7f) / 0.3f);

            float rotAngle = (1f - t) * spiralRotations * -360f;

            if (designLetterTransform != null)
            {
                designLetterTransform.localScale = designOrigScale * scaleMult;
                designLetterTransform.localRotation = designOrigRot * Quaternion.Euler(0f, 0f, rotAngle);
            }
            if (dottedLetterTransform != null)
            {
                dottedLetterTransform.localScale = dottedOrigScale * scaleMult;
                dottedLetterTransform.localRotation = dottedOrigRot * Quaternion.Euler(0f, 0f, rotAngle);
            }

            yield return null;
        }

        // Reset exact original scale & rotation
        if (designLetterTransform != null)
        {
            designLetterTransform.localScale = designOrigScale;
            designLetterTransform.localRotation = designOrigRot;
        }
        if (dottedLetterTransform != null)
        {
            dottedLetterTransform.localScale = dottedOrigScale;
            dottedLetterTransform.localRotation = dottedOrigRot;
        }

        IsTransitioning = false;
    }

    private IEnumerator SpawnInwardSpiralParticles(Vector2 center, float duration)
    {
        if (fxContainer == null) yield break;
        int count = spiralParticleCount;
        float startRadius = 220f;

        for (int i = 0; i < count; i++)
        {
            float delay = (float)i / count * duration * 0.7f;
            float initialAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Color color = GetRandomVibrantColor();
            float particleLife = duration - delay;

            StartCoroutine(AnimateSingleSpiralParticle(center, initialAngle, startRadius, 0f, delay, particleLife, color, true));
        }
        yield break;
    }

    private IEnumerator SpawnOutwardSpiralParticles(Vector2 center, float duration)
    {
        if (fxContainer == null) yield break;
        int count = spiralParticleCount;
        float targetRadius = 220f;

        for (int i = 0; i < count; i++)
        {
            float delay = (float)i / count * duration * 0.6f;
            float initialAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Color color = GetRandomVibrantColor();
            float particleLife = duration - delay;

            StartCoroutine(AnimateSingleSpiralParticle(center, initialAngle, 0f, targetRadius, delay, particleLife, color, false));
        }
        yield break;
    }

    private IEnumerator AnimateSingleSpiralParticle(Vector2 center, float startAngle, float startRadius, float endRadius, float startDelay, float lifeDuration, Color color, bool isInward)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        Sprite particleSprite = sparkSprite != null ? sparkSprite : glowSprite;
        if (particleSprite == null || fxContainer == null) yield break;

        GameObject pObj = new GameObject("SpiralParticle", typeof(RectTransform), typeof(Image));
        pObj.transform.SetParent(fxContainer, false);

        RectTransform rect = pObj.GetComponent<RectTransform>();
        float pSize = Random.Range(30f, 60f);
        rect.sizeDelta = new Vector2(pSize, pSize);

        Image img = pObj.GetComponent<Image>();
        img.sprite = particleSprite;
        if (additiveMaterial != null) img.material = additiveMaterial;
        img.color = color;

        float elapsed = 0f;
        float spinSpeed = isInward ? 8f : -8f;

        while (elapsed < lifeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifeDuration);

            float currentRadius = Mathf.Lerp(startRadius, endRadius, t);
            float currentAngle = startAngle + t * spinSpeed;

            Vector2 offset = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * currentRadius;
            rect.anchoredPosition = center + offset;

            float scale = isInward ? Mathf.Lerp(1f, 0.2f, t) : Mathf.Lerp(0.2f, 1.2f, t);
            rect.localScale = new Vector3(scale, scale, 1f);

            Color c = color;
            c.a = Mathf.Sin(t * Mathf.PI);
            img.color = c;

            yield return null;
        }

        Destroy(pObj);
    }
}
