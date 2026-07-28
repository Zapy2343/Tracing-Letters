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
    private RectTransform fxContainer;
    private Material additiveMaterial;
    private float lastTrailSpawnTime;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        InitializeFXContainer();
        CreateAdditiveMaterial();
    }

    private void Start()
    {
#if UNITY_2023_1_OR_NEWER
        if (penDrawer == null) penDrawer = FindFirstObjectByType<PenDrawer>();
        if (letterSwitcher == null) letterSwitcher = FindFirstObjectByType<LetterSwitcher>();
#else
        if (penDrawer == null) penDrawer = FindObjectOfType<PenDrawer>();
        if (letterSwitcher == null) letterSwitcher = FindObjectOfType<LetterSwitcher>();
#endif

        if (penDrawer != null)
        {
            penDrawer.OnMaskCompleted.AddListener(OnLetterCompleted);
        }

        // Auto-detect letter rect transforms from LetterSwitcher if unassigned
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

    private void OnDestroy()
    {
        if (penDrawer != null)
        {
            penDrawer.OnMaskCompleted.RemoveListener(OnLetterCompleted);
        }
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
            GameObject container = new GameObject("FX_Container", typeof(RectTransform));
            container.transform.SetParent(parentCanvas.transform, false);

            fxContainer = container.GetComponent<RectTransform>();
            fxContainer.anchorMin = Vector2.zero;
            fxContainer.anchorMax = Vector2.one;
            fxContainer.sizeDelta = Vector2.zero;
            fxContainer.anchoredPosition = Vector2.zero;

            // FX Container sits near top of canvas hierarchy so FX render over UI elements
            fxContainer.SetAsLastSibling();
        }
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

        bool isDrawing = IsMouseHeldDown();
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
        // Re-check target letter transforms if not set
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

        Vector2 centerPos = Vector2.zero;

        if (designLetterTransform != null && fxContainer != null)
        {
            centerPos = fxContainer.InverseTransformPoint(designLetterTransform.position);
        }

        if (enableCompletionBurst)
        {
            StartCoroutine(TriggerCompletionBurstRoutine(centerPos));
        }

        if (animateLetterOnCompletion)
        {
            if (designLetterTransform != null) StartCoroutine(AnimateLetterBounceRoutine(designLetterTransform));
            if (dottedLetterTransform != null) StartCoroutine(AnimateLetterBounceRoutine(dottedLetterTransform));
        }
    }

    private IEnumerator TriggerCompletionBurstRoutine(Vector2 center)
    {
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

        // 3. Particle Explosion (ui_glow_spark)
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

        // 4. Sheen Sweep Shine across letter
        if (sheenSprite != null && designLetterTransform != null)
        {
            SpawnSheenSweep(center, 0.8f);
        }

        yield return null;
    }

    private void SpawnLightRays(Vector2 position, float duration)
    {
        GameObject raysObj = new GameObject("FX_LightRays", typeof(RectTransform), typeof(Image));
        raysObj.transform.SetParent(fxContainer, false);

        RectTransform rect = raysObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300f, 300f);
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

    private void SpawnStarburst(Vector2 position, float duration)
    {
        GameObject starObj = new GameObject("FX_Starburst", typeof(RectTransform), typeof(Image));
        starObj.transform.SetParent(fxContainer, false);

        RectTransform rect = starObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250f, 250f);
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
            SpawnStarburst(centerPos, 0.4f);
        }

        // --- PHASE 2: SWITCH SPRITE & CLEAR STROKES ---
        onSwitchCallback?.Invoke();
        yield return null;

        // --- PHASE 3: SPAWN (Swirl & Expand from Center) ---
        StartCoroutine(SpawnOutwardSpiralParticles(centerPos, halfDuration));

        if (lightRaysSprite != null)
        {
            SpawnLightRays(centerPos, 0.6f);
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
