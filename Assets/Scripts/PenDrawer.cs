using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PenDrawer : MonoBehaviour
{
    public enum DrawingMode
    {
        NormalDraw,
        RevealMask
    }

    [Header("Mode & Container Settings")]
    [Tooltip("Select whether to draw solid strokes or use strokes as a mask to reveal an underlying graphic.")]
    [SerializeField] private DrawingMode drawingMode = DrawingMode.RevealMask;

    [Tooltip("Parent transform to contain pen strokes. In RevealMask mode, strokes here write to stencil buffer.")]
    [SerializeField] private Transform maskParent;

    [Tooltip("The graphic/image to reveal (e.g., the colorful letter artwork). Auto-detected from maskParent if unassigned.")]
    [SerializeField] private Graphic revealTargetGraphic;

    [Tooltip("Stencil channel used by this drawer. Keep this different from other revealable groups if multiple masks are visible near each other.")]
    [Range(1, 255)]
    [SerializeField] private int stencilReference = 17;

    [Header("Pen Tip Reference & Offset")]
    [Tooltip("Transform representing the pencil tip. If unassigned, uses this GameObject with the tipOffset.")]
    [SerializeField] private Transform penTip;

    [Tooltip("Offset in UI pixels from the pencil object center/pivot to the pencil tip.")]
    [SerializeField] private Vector2 tipOffset = new Vector2(0f, 0f);

    [Header("Pen Customization")]
    [Tooltip("Pen stroke color (used in NormalDraw mode).")]
    [SerializeField] private Color penColor = Color.blue;

    [Tooltip("Radius / thickness of the pen stroke line in UI units.")]
    [Range(1f, 160f)]
    [SerializeField] private float penRadius = 15f;

    [Header("Drawing Settings")]
    [Tooltip("Minimum distance cursor must move to add a new point to the stroke.")]
    [SerializeField] private float minDistanceBetweenPoints = 3f;

    [Header("Letter Boundary Settings")]
    [Tooltip("If true, strokes can only start and continue over non-transparent pixels of the active reveal letter.")]
    [SerializeField] private bool restrictDrawingToLetterShape = true;

    [Tooltip("Extra tolerance in UI units when checking whether the pen tip is inside the active letter.")]
    [SerializeField] private float letterBoundaryTolerance = 6f;

    [Tooltip("If true, a valid pen touch is shifted toward the center of the active stroke before drawing.")]
    [SerializeField] private bool centerPenOnActiveStroke = true;

    [Tooltip("Search radius used to find the active stroke center near the pen touch.")]
    [Range(0.25f, 1.5f)]
    [SerializeField] private float centerSearchRadiusMultiplier = 0.8f;

    [Header("Sequence Tracing Settings")]
    [Tooltip("Designer-authored sequence data. Each letter can have ordered stroke masks.")]
    [SerializeField] private TracingSequenceAsset tracingSequence;

    [Tooltip("If true and the active letter has stroke steps, only the current step mask can be traced.")]
    [SerializeField] private bool useSequenceTracing = true;

    [Tooltip("If true, custom hand hint path points also define the required drawing order for the active stroke.")]
    [SerializeField] private bool enforceSequentialHintPath = true;

    [Tooltip("How close the pen must be to the first hint path point before a stroke can start.")]
    [SerializeField] private float sequentialStartTolerance = 55f;

    [Tooltip("How far the pen can drift from the current hint path segment while drawing.")]
    [SerializeField] private float sequentialPathTolerance = 70f;

    [Header("Sequence Status (Read Only)")]
    [SerializeField] private int currentLetterNumber = 1;
    [SerializeField] private int currentSequenceStepIndex = 0;

    [Tooltip("Parent Canvas containing the drawing UI. Auto-detected if empty.")]
    [SerializeField] private Canvas parentCanvas;

    [Tooltip("Parent transform to contain all drawn line objects in NormalDraw mode (optional).")]
    [SerializeField] private Transform linesParent;

    [Header("Auto-Completion Settings")]
    [Tooltip("If true, automatically reveals 100% of the graphic when fill percentage reaches the completion threshold.")]
    [SerializeField] private bool autoCompleteOnHighCoverage = true;

    [Tooltip("If true, auto-completion only triggers when the user lifts the pen/mouse (Mouse Up), preventing mid-stroke popping.")]
    [SerializeField] private bool completeOnlyOnMouseRelease = true;

    [Tooltip("Target fill percentage threshold (0.85 = 85%, 0.90 = 90%, 0.95 = 95%) to trigger 100% auto-fill.")]
    [Range(0.5f, 1.0f)]
    [SerializeField] private float completionThreshold = 0.88f;

    [Tooltip("Multiplier for stroke hit radius during coverage check (0.85 = 85% of pen radius).")]
    [Range(0.1f, 2.0f)]
    [SerializeField] private float hitRadiusRatio = 0.85f;

    [Tooltip("Resolution of sampling grid for calculating coverage progress.")]
    [SerializeField] private int sampleGridResolution = 48;

    [Tooltip("Event triggered when the letter mask reaches the auto-completion threshold.")]
    public UnityEvent OnMaskCompleted;

    [Tooltip("Event triggered when one sequence stroke step is completed and the drawer advances to the next step.")]
    public UnityEvent OnStrokeStepCompleted;

    [Tooltip("Event triggered when the user starts a valid tracing stroke.")]
    public UnityEvent OnTraceStarted;

    [Tooltip("Event triggered when the user stops a tracing stroke.")]
    public UnityEvent OnTraceStopped;

    [Header("Coverage Progress (Read Only)")]
    [Range(0f, 1f)]
    [SerializeField] private float currentCoverageProgress = 0f;
    [SerializeField] private bool isCompleted = false;

    [Header("Clear Settings")]
    [Tooltip("If true, double-clicking the right mouse button clears all drawn strokes.")]
    [SerializeField] private bool clearOnDoubleRightClick = true;

    [Tooltip("Maximum time gap in seconds between right clicks to count as a double-click.")]
    [SerializeField] private float doubleClickThreshold = 0.3f;

#if UNITY_EDITOR
    [Header("Editor Hint Path Recording")]
    [Tooltip("Play Mode authoring only. If enabled before tracing, the next completed stroke is saved as this stroke step's hand hint path.")]
    [SerializeField] private bool saveHandHintPathFromNextStroke = false;

    [Tooltip("Maximum number of saved waypoint points for the recorded hand hint path.")]
    [Range(2, 24)]
    [SerializeField] private int recordedHintPathMaxPoints = 10;

    [Tooltip("Small movements below this local UI distance are ignored before the path is resampled.")]
    [SerializeField] private float recordedHintPathMinPointDistance = 8f;
#endif

    private UILine currentUILine;
    private RectTransform canvasRectTransform;
    private Camera mainCamera;
    private Vector2 lastLocalPoint;
    private float lastRightClickTime = -1f;
    private Material maskWriterMaterial;
    private Material revealMaterial;
    private bool drawingLockedUntilRelease = false;
    [SerializeField] private bool drawingLockedAfterCompletion = false;
    [SerializeField] private bool drawingLockedForHint = false;
    private readonly List<Material> runtimeStrokeMaterials = new List<Material>();
    private readonly List<Image> sequenceStrokeLayers = new List<Image>();
    private readonly List<Material> sequenceRevealMaterials = new List<Material>();
    private Graphic hiddenFinalRevealTarget;
    private bool hiddenFinalRevealTargetWasEnabled;

    // Sample points data for coverage calculation
    private List<Vector2> targetSamplePoints = new List<Vector2>();
    private List<Vector2> remainingUncoveredPoints = new List<Vector2>();
    private int totalSamplePointsCount = 0;
    private LetterSequence activeLetterSequence;
    private readonly List<Vector2> activeSequentialPathLocal = new List<Vector2>();
    private int nextSequentialPointIndex = 1;
#if UNITY_EDITOR
    private readonly List<Vector2> lastCompletedHintPathLocal = new List<Vector2>();
    private readonly List<Vector2> activeHintPathLocalBuffer = new List<Vector2>();
    private int lastCompletedHintLetterNumber = -1;
    private int lastCompletedHintStepIndex = -1;
#endif

    public bool IsActivelyDrawing => currentUILine != null && !drawingLockedUntilRelease && !drawingLockedAfterCompletion;
    public bool IsDrawingLockedAfterCompletion => drawingLockedAfterCompletion;
    public bool IsDrawingLockedForHint => drawingLockedForHint;
    public int CurrentLetterNumber => currentLetterNumber;
    public int CurrentSequenceStepIndex => currentSequenceStepIndex;
    public LetterSequence CurrentLetterSequence => GetActiveLetterSequence();
    public TracingStrokeStep CurrentSequenceStep => GetActiveSequenceStep();
    public TracingSequenceAsset TracingSequence => tracingSequence;
    public TracingLetterAsset CurrentLetterAsset => tracingSequence != null ? tracingSequence.GetLetterAsset(currentLetterNumber) : null;
    public Graphic RevealTargetGraphic => revealTargetGraphic;
    public Canvas ParentCanvas => parentCanvas;

    public bool TryGetActiveHintPathLocal(out Vector2 start, out Vector2 end)
    {
        start = Vector2.zero;
        end = Vector2.zero;

        List<Vector2> path = new List<Vector2>();
        if (!TryGetActiveHintPathLocal(path))
        {
            return false;
        }

        start = path[0];
        end = path[path.Count - 1];
        return true;
    }

    public bool TryGetActiveHintPathLocal(List<Vector2> path, int maxPathPoints = 18)
    {
        if (path == null)
        {
            return false;
        }

        path.Clear();

        if (targetSamplePoints == null || targetSamplePoints.Count == 0)
        {
            return false;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < targetSamplePoints.Count; i++)
        {
            Vector2 point = targetSamplePoints[i];
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }

        if (float.IsInfinity(minX) || float.IsInfinity(maxX) || float.IsInfinity(minY) || float.IsInfinity(maxY))
        {
            return false;
        }

        float width = maxX - minX;
        float height = maxY - minY;
        if (width <= 0.01f && height <= 0.01f)
        {
            return false;
        }

        bool vertical = height >= width;
        int segmentCount = Mathf.Clamp(maxPathPoints, 2, 48);

        for (int segment = 0; segment < segmentCount; segment++)
        {
            float t0 = (float)segment / segmentCount;
            float t1 = (float)(segment + 1) / segmentCount;
            float binMin = vertical ? Mathf.Lerp(maxY, minY, t1) : Mathf.Lerp(minX, maxX, t0);
            float binMax = vertical ? Mathf.Lerp(maxY, minY, t0) : Mathf.Lerp(minX, maxX, t1);
            Vector2 average = Vector2.zero;
            int count = 0;

            for (int i = 0; i < targetSamplePoints.Count; i++)
            {
                Vector2 point = targetSamplePoints[i];
                float value = vertical ? point.y : point.x;
                if (value < binMin || value > binMax)
                {
                    continue;
                }

                average += point;
                count++;
            }

            if (count > 0)
            {
                path.Add(average / count);
            }
        }

        if (path.Count >= 2)
        {
            return true;
        }

        if (height >= width)
        {
            float centerX = (minX + maxX) * 0.5f;
            path.Add(new Vector2(centerX, maxY));
            path.Add(new Vector2(centerX, minY));
        }
        else
        {
            float centerY = (minY + maxY) * 0.5f;
            path.Add(new Vector2(minX, centerY));
            path.Add(new Vector2(maxX, centerY));
        }

        return true;
    }

    private void Start()
    {
        mainCamera = Camera.main;

        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (parentCanvas != null)
        {
            canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
        }

        SetupStencilMaterials();
        SetupSequenceStrokeLayers();
        RebuildSamplePoints();
    }

    /// <summary>
    /// Sets a new graphic as the reveal target and rebuilds stencil materials & sample points.
    /// </summary>
    public void SetRevealTargetGraphic(Graphic graphic)
    {
        ReleaseSequenceStrokeLayers();
        revealTargetGraphic = graphic;
        SetupStencilMaterials();
        SetupSequenceStrokeLayers();
        ClearAllLines();
    }

    public void SetRevealTargetGraphic(Graphic graphic, int letterNumber)
    {
        ReleaseSequenceStrokeLayers();
        currentLetterNumber = Mathf.Max(1, letterNumber);
        currentSequenceStepIndex = 0;
        activeLetterSequence = GetActiveLetterSequence();

        revealTargetGraphic = graphic;
        SetupStencilMaterials();
        SetupSequenceStrokeLayers();
        ClearAllLines();
    }

    public void SetCurrentLetterNumber(int letterNumber)
    {
        ReleaseSequenceStrokeLayers();
        currentLetterNumber = Mathf.Max(1, letterNumber);
        currentSequenceStepIndex = 0;
        activeLetterSequence = GetActiveLetterSequence();
        SetupSequenceStrokeLayers();
        ClearAllLines();
    }

    public void SetHintGateLocked(bool locked)
    {
        drawingLockedForHint = locked;

        if (drawingLockedForHint && currentUILine != null)
        {
            FinishStroke();
        }
    }

    [Header("Custom Stencil Shaders & Materials (Prevents Stripping in PC Build)")]
    [Tooltip("UI/StencilMaskWriter shader reference. Prevents shader stripping in PC builds.")]
    [SerializeField] private Shader maskWriterShader;

    [Tooltip("UI/StencilReveal shader reference. Prevents shader stripping in PC builds.")]
    [SerializeField] private Shader revealShader;

    [Tooltip("Pre-created Material using UI/StencilMaskWriter (Optional).")]
    [SerializeField] private Material customMaskWriterMaterial;

    [Tooltip("Pre-created Material using UI/StencilReveal (Optional).")]
    [SerializeField] private Material customRevealMaterial;

    public void SetupStencilMaterials()
    {
        if (drawingMode == DrawingMode.RevealMask)
        {
            if (customMaskWriterMaterial != null)
            {
                maskWriterMaterial = new Material(customMaskWriterMaterial);
            }
            else
            {
                Shader targetMaskShader = maskWriterShader != null ? maskWriterShader : Shader.Find("UI/StencilMaskWriter");
                if (targetMaskShader != null)
                {
                    maskWriterMaterial = new Material(targetMaskShader);
                }
                else
                {
                    Debug.LogWarning("[PenDrawer] UI/StencilMaskWriter shader not found! Please assign it in the PenDrawer Inspector component.");
                }
            }

            if (customRevealMaterial != null)
            {
                revealMaterial = new Material(customRevealMaterial);
            }
            else
            {
                Shader targetRevealShader = revealShader != null ? revealShader : Shader.Find("UI/StencilReveal");
                if (targetRevealShader != null)
                {
                    revealMaterial = new Material(targetRevealShader);
                }
                else
                {
                    Debug.LogWarning("[PenDrawer] UI/StencilReveal shader not found! Please assign it in the PenDrawer Inspector component.");
                }
            }

            ApplyStencilReference(maskWriterMaterial);
            ApplyStencilReference(revealMaterial);

            if (revealTargetGraphic == null && maskParent != null)
            {
                revealTargetGraphic = maskParent.GetComponentInChildren<Graphic>();
            }

            if (revealTargetGraphic != null && revealMaterial != null)
            {
                revealTargetGraphic.material = revealMaterial;
            }

            if (maskParent != null)
            {
                // Disable Unity's built-in Mask component if present, as it interferes with custom stencil shaders
                UnityEngine.UI.Mask unityMask = maskParent.GetComponent<UnityEngine.UI.Mask>();
                if (unityMask != null && unityMask.enabled)
                {
                    unityMask.enabled = false;
                    Debug.Log("[PenDrawer] Disabled built-in Mask component on " + maskParent.name + " so stencil shaders work correctly.");
                }
            }
        }
    }

    /// <summary>
    /// Re-samples the active letter graphic to generate coverage points for auto-completion detection.
    /// </summary>
    public void RebuildSamplePoints()
    {
        isCompleted = false;
        currentCoverageProgress = 0f;
        targetSamplePoints.Clear();
        remainingUncoveredPoints.Clear();
        totalSamplePointsCount = 0;

        // Reset stencil comparison to Equal (3)
        if (revealMaterial != null)
        {
            revealMaterial.SetFloat("_StencilComp", 3f);
        }

        if (!autoCompleteOnHighCoverage || drawingMode != DrawingMode.RevealMask) return;

        if (revealTargetGraphic == null && maskParent != null)
        {
            revealTargetGraphic = maskParent.GetComponentInChildren<Graphic>();
        }

        // Fallback auto-detection if revealTargetGraphic is unassigned
        if (revealTargetGraphic == null)
        {
#if UNITY_2023_1_OR_NEWER
            var images = FindObjectsByType<Image>(FindObjectsSortMode.None);
#else
            var images = FindObjectsOfType<Image>();
#endif
            foreach (var img in images)
            {
                if (img.gameObject.name.ToLower().Contains("design"))
                {
                    revealTargetGraphic = img;
                    break;
                }
            }
            if (revealTargetGraphic == null && images.Length > 0)
            {
                revealTargetGraphic = images[0];
            }
        }

        if (revealTargetGraphic == null) return;

        // Make sure material is attached to reveal target
        if (revealMaterial != null && revealTargetGraphic.material != revealMaterial)
        {
            revealTargetGraphic.material = revealMaterial;
        }

        Image image = revealTargetGraphic as Image;
        Sprite sprite = GetActiveCoverageSprite(image);
        if (sprite == null)
        {
            Debug.LogWarning($"[PenDrawer] '{revealTargetGraphic.name}' has no Sprite assigned! Coverage progress cannot be computed.");
            return;
        }

        targetSamplePoints = GenerateSamplePoints(sprite, revealTargetGraphic.rectTransform, sampleGridResolution);
        remainingUncoveredPoints = new List<Vector2>(targetSamplePoints);
        totalSamplePointsCount = targetSamplePoints.Count;

        Debug.Log($"[PenDrawer] Rebuilt {totalSamplePointsCount} coverage sample points for '{GetActiveTracingName()}'.");
    }

    private Sprite GetActiveCoverageSprite(Image revealImage)
    {
        TracingStrokeStep activeStep = GetActiveSequenceStep();
        if (activeStep != null && activeStep.AllowedAreaMask != null)
        {
            return activeStep.AllowedAreaMask;
        }

        return revealImage != null ? revealImage.sprite : null;
    }

    private LetterSequence GetActiveLetterSequence()
    {
        if (!useSequenceTracing || tracingSequence == null)
        {
            return null;
        }

        LetterSequence sequence = tracingSequence.GetLetter(currentLetterNumber);
        return sequence != null && sequence.HasSteps ? sequence : null;
    }

    private TracingStrokeStep GetActiveSequenceStep()
    {
        activeLetterSequence = GetActiveLetterSequence();
        if (activeLetterSequence == null)
        {
            return null;
        }

        return activeLetterSequence.GetStep(currentSequenceStepIndex);
    }

    private bool HasNextSequenceStep()
    {
        return activeLetterSequence != null && currentSequenceStepIndex + 1 < activeLetterSequence.StrokeSteps.Count;
    }

    private float GetActiveCompletionThreshold()
    {
        TracingStrokeStep activeStep = GetActiveSequenceStep();
        if (activeStep != null && activeStep.CompletionThresholdOverride > 0f)
        {
            return activeStep.CompletionThresholdOverride;
        }

        return completionThreshold;
    }

    private string GetActiveTracingName()
    {
        TracingStrokeStep activeStep = GetActiveSequenceStep();
        if (activeStep != null)
        {
            return $"Letter {currentLetterNumber}, Step {currentSequenceStepIndex + 1}: {activeStep.StepName}";
        }

        return revealTargetGraphic != null ? revealTargetGraphic.name : "active letter";
    }

    /// <summary>
    /// Generates grid sample points from the non-transparent parts of a sprite texture.
    /// </summary>
    private List<Vector2> GenerateSamplePoints(Sprite sprite, RectTransform rectTransform, int gridRes)
    {
        List<Vector2> points = new List<Vector2>();
        if (sprite == null || rectTransform == null || sprite.texture == null) return points;

        Texture2D mainTex = sprite.texture;
        Texture2D readableTex = null;
        bool createdTemp = false;

        try
        {
            if (mainTex.isReadable)
            {
                readableTex = mainTex;
            }
        }
        catch
        {
            readableTex = null;
        }

        if (readableTex == null)
        {
            RenderTexture rt = RenderTexture.GetTemporary(mainTex.width, mainTex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(mainTex, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            readableTex = new Texture2D(mainTex.width, mainTex.height, TextureFormat.RGBA32, false);
            readableTex.ReadPixels(new Rect(0, 0, mainTex.width, mainTex.height), 0, 0);
            readableTex.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            createdTemp = true;
        }

        Rect spriteRect = sprite.rect;
        int startX = (int)spriteRect.x;
        int startY = (int)spriteRect.y;
        int width = (int)spriteRect.width;
        int height = (int)spriteRect.height;

        int stepX = Mathf.Max(1, width / gridRes);
        int stepY = Mathf.Max(1, height / gridRes);

        Vector2 rectSize = rectTransform.rect.size;
        Vector2 pivot = rectTransform.pivot;

        Color32[] pixels = readableTex.GetPixels32();
        int texWidth = readableTex.width;

        for (int y = 0; y < height; y += stepY)
        {
            for (int x = 0; x < width; x += stepX)
            {
                int px = startX + x;
                int py = startY + y;
                int index = py * texWidth + px;

                if (index >= 0 && index < pixels.Length)
                {
                    if (pixels[index].a > 20) // Only count non-transparent letter pixels
                    {
                        float u = (float)x / width;
                        float v = (float)y / height;

                        Vector2 localPos = new Vector2(
                            (u - pivot.x) * rectSize.x,
                            (v - pivot.y) * rectSize.y
                        );
                        points.Add(localPos);
                    }
                }
            }
        }

        if (createdTemp && readableTex != null)
        {
            Destroy(readableTex);
        }

        return points;
    }

    private void Update()
    {
#if UNITY_EDITOR
        HandleHintPathRecordingShortcut();
#endif

        if (drawingLockedAfterCompletion)
        {
            if (currentUILine != null)
            {
                FinishStroke();
            }
            return;
        }

        // Handle double right-click to clear canvas
        if (clearOnDoubleRightClick && IsRightMouseJustPressed())
        {
            if (Time.time - lastRightClickTime <= doubleClickThreshold)
            {
                ClearAllLines();
                lastRightClickTime = -1f;
                return;
            }
            lastRightClickTime = Time.time;
        }

        if (parentCanvas == null || canvasRectTransform == null) return;

        Vector2 tipScreenPos = GetPenTipScreenPosition();

        if (IsMouseJustReleased())
        {
            drawingLockedUntilRelease = false;
            FinishStroke();
            return;
        }

        if (drawingLockedForHint)
        {
            return;
        }

        if (IsMouseJustPressed() && !CanDrawAtScreenPosition(tipScreenPos))
        {
            drawingLockedUntilRelease = true;
            return;
        }

        if (IsMouseHeldDown() && drawingLockedUntilRelease)
        {
            return;
        }

        if (IsMouseJustPressed() && CanDrawAtScreenPosition(tipScreenPos))
        {
            Vector2 drawingScreenPos = GetCenteredDrawingScreenPosition(tipScreenPos);
            StartNewStroke(drawingScreenPos);
        }
        else if (IsMouseHeldDown() && currentUILine != null && CanDrawAtScreenPosition(tipScreenPos, true))
        {
            Vector2 drawingScreenPos = GetCenteredDrawingScreenPosition(tipScreenPos);
            UpdateCurrentStroke(drawingScreenPos);
        }
        else if (IsMouseHeldDown() && currentUILine == null && CanDrawAtScreenPosition(tipScreenPos))
        {
            Vector2 drawingScreenPos = GetCenteredDrawingScreenPosition(tipScreenPos);
            StartNewStroke(drawingScreenPos);
        }
        else if (IsMouseHeldDown() && currentUILine != null && !CanDrawAtScreenPosition(tipScreenPos))
        {
            FinishStroke();
            drawingLockedUntilRelease = true;
        }
    }

    private void ApplyStencilReference(Material material)
    {
        ApplyStencilReference(material, Mathf.Clamp(stencilReference, 1, 255));
    }

    private static void ApplyStencilReference(Material material, int reference)
    {
        if (material != null && material.HasProperty("_StencilRef"))
        {
            material.SetFloat("_StencilRef", reference);
        }
    }

    private int GetSequenceStencilReference(int stepIndex)
    {
        int baseReference = Mathf.Clamp(stencilReference, 1, 255);
        return ((baseReference - 1 + Mathf.Max(0, stepIndex)) % 255) + 1;
    }

    private int GetActiveStencilReference()
    {
        return GetActiveSequenceStep() != null
            ? GetSequenceStencilReference(currentSequenceStepIndex)
            : Mathf.Clamp(stencilReference, 1, 255);
    }

    private void SetupSequenceStrokeLayers()
    {
        if (sequenceStrokeLayers.Count > 0 || hiddenFinalRevealTarget != null)
        {
            ReleaseSequenceStrokeLayers();
        }

        activeLetterSequence = GetActiveLetterSequence();
        if (drawingMode != DrawingMode.RevealMask || activeLetterSequence == null ||
            revealTargetGraphic == null || revealMaterial == null || revealTargetGraphic.transform.parent == null)
        {
            return;
        }

        hiddenFinalRevealTarget = revealTargetGraphic;
        hiddenFinalRevealTargetWasEnabled = revealTargetGraphic.enabled;
        revealTargetGraphic.enabled = false;

        Transform layerParent = revealTargetGraphic.transform;

        for (int i = 0; i < activeLetterSequence.StrokeSteps.Count; i++)
        {
            TracingStrokeStep step = activeLetterSequence.GetStep(i);
            if (step == null || step.AllowedAreaMask == null)
            {
                sequenceStrokeLayers.Add(null);
                sequenceRevealMaterials.Add(null);
                continue;
            }

            GameObject layerObject = new GameObject(
                $"SequenceStrokeLayer_{i + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            layerObject.transform.SetParent(layerParent, false);

            RectTransform layerRect = layerObject.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.pivot = new Vector2(0.5f, 0.5f);
            layerRect.anchoredPosition = Vector2.zero;
            layerRect.sizeDelta = Vector2.zero;
            layerRect.localRotation = Quaternion.identity;
            layerRect.localScale = Vector3.one;

            Material layerMaterial = new Material(revealMaterial);
            ApplyStencilReference(layerMaterial, GetSequenceStencilReference(i));
            if (layerMaterial.HasProperty("_StencilComp"))
            {
                layerMaterial.SetFloat("_StencilComp", 3f);
            }

            Image layerImage = layerObject.GetComponent<Image>();
            layerImage.sprite = step.AllowedAreaMask;
            layerImage.type = Image.Type.Simple;
            layerImage.preserveAspect = true;
            layerImage.useSpriteMesh = false;
            layerImage.color = Color.white;
            layerImage.raycastTarget = false;
            layerImage.maskable = false;
            layerImage.material = layerMaterial;

            layerObject.transform.SetSiblingIndex(i);
            sequenceStrokeLayers.Add(layerImage);
            sequenceRevealMaterials.Add(layerMaterial);
        }
    }

    private void ReleaseSequenceStrokeLayers()
    {
        for (int i = 0; i < sequenceStrokeLayers.Count; i++)
        {
            if (sequenceStrokeLayers[i] != null)
            {
                Destroy(sequenceStrokeLayers[i].gameObject);
            }
        }
        sequenceStrokeLayers.Clear();

        for (int i = 0; i < sequenceRevealMaterials.Count; i++)
        {
            if (sequenceRevealMaterials[i] != null)
            {
                Destroy(sequenceRevealMaterials[i]);
            }
        }
        sequenceRevealMaterials.Clear();

        if (hiddenFinalRevealTarget != null)
        {
            hiddenFinalRevealTarget.enabled = hiddenFinalRevealTargetWasEnabled;
        }
        hiddenFinalRevealTarget = null;
    }

    private void ResetSequenceStrokeLayers()
    {
        for (int i = 0; i < sequenceRevealMaterials.Count; i++)
        {
            Material material = sequenceRevealMaterials[i];
            if (material != null && material.HasProperty("_StencilComp"))
            {
                material.SetFloat("_StencilComp", 3f);
            }
        }

        if (hiddenFinalRevealTarget != null)
        {
            hiddenFinalRevealTarget.enabled = false;
        }
    }

    private Material CreateStrokeMaskMaterial()
    {
        if (maskWriterMaterial == null)
        {
            return null;
        }

        Material strokeMaterial = new Material(maskWriterMaterial);
        ApplyStencilReference(strokeMaterial, GetActiveStencilReference());

        runtimeStrokeMaterials.Add(strokeMaterial);
        return strokeMaterial;
    }

    private int GetStencilWriterSiblingIndex(Transform targetParent)
    {
        int firstRevealIndex = int.MaxValue;
        for (int i = 0; i < sequenceStrokeLayers.Count; i++)
        {
            Image layer = sequenceStrokeLayers[i];
            if (layer != null && layer.transform.parent == targetParent)
            {
                firstRevealIndex = Mathf.Min(firstRevealIndex, layer.transform.GetSiblingIndex());
            }
        }

        if (firstRevealIndex != int.MaxValue)
        {
            return firstRevealIndex;
        }

        if (revealTargetGraphic != null && revealTargetGraphic.transform.parent == targetParent)
        {
            return revealTargetGraphic.transform.GetSiblingIndex();
        }

        return 0;
    }

    private bool CanDrawAtScreenPosition(Vector2 screenPosition, bool advanceSequentialPath = false)
    {
        if (drawingMode != DrawingMode.RevealMask)
        {
            return true;
        }

        if (revealTargetGraphic == null && maskParent != null)
        {
            revealTargetGraphic = maskParent.GetComponentInChildren<Graphic>();
        }

        if (revealTargetGraphic == null)
        {
            return true;
        }

        if (!TryGetRevealLocalPoint(screenPosition, out Vector2 localPoint))
        {
            return false;
        }

        bool isInsideLetter = !restrictDrawingToLetterShape ||
            IsLocalPointInsideRevealSprite(localPoint, revealTargetGraphic.rectTransform);

        return isInsideLetter &&
            IsSequentialDrawAllowed(localPoint, advanceSequentialPath);
    }

    private bool TryGetRevealLocalPoint(Vector2 screenPosition, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (revealTargetGraphic == null)
        {
            return false;
        }

        RectTransform targetRect = revealTargetGraphic.rectTransform;
        Camera uiCamera = (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : parentCanvas?.worldCamera;
        if (uiCamera == null && parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = mainCamera;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenPosition, uiCamera, out localPoint);
    }

    private bool IsSequentialDrawAllowed(Vector2 localPoint, bool advanceSequentialPath)
    {
        if (!enforceSequentialHintPath || !TryGetActiveSequentialPath(activeSequentialPathLocal))
        {
            return true;
        }

        float startTolerance = Mathf.Max(sequentialStartTolerance, penRadius);
        if (currentUILine == null)
        {
            return Vector2.Distance(localPoint, activeSequentialPathLocal[0]) <= startTolerance;
        }

        nextSequentialPointIndex = Mathf.Clamp(nextSequentialPointIndex, 1, activeSequentialPathLocal.Count - 1);
        Vector2 from = activeSequentialPathLocal[nextSequentialPointIndex - 1];
        Vector2 to = activeSequentialPathLocal[nextSequentialPointIndex];
        float pathTolerance = Mathf.Max(sequentialPathTolerance, penRadius);
        bool isOnCurrentSegment = IsPointNearSegment(localPoint, from, to, pathTolerance);

        if (!isOnCurrentSegment)
        {
            return false;
        }

        if (advanceSequentialPath && Vector2.Distance(localPoint, to) <= pathTolerance)
        {
            nextSequentialPointIndex = Mathf.Min(nextSequentialPointIndex + 1, activeSequentialPathLocal.Count - 1);
        }

        return true;
    }

    private bool TryGetActiveSequentialPath(List<Vector2> outputPath)
    {
        if (outputPath == null)
        {
            return false;
        }

        outputPath.Clear();

        TracingStrokeStep step = GetActiveSequenceStep();
        return step != null && step.TryBuildHintPath(outputPath) && outputPath.Count >= 2;
    }

    private void BeginSequentialStroke(Vector2 screenPosition)
    {
        nextSequentialPointIndex = 1;

        if (!enforceSequentialHintPath ||
            !TryGetRevealLocalPoint(screenPosition, out Vector2 localPoint) ||
            !TryGetActiveSequentialPath(activeSequentialPathLocal))
        {
            return;
        }

        float tolerance = Mathf.Max(sequentialStartTolerance, penRadius);
        if (Vector2.Distance(localPoint, activeSequentialPathLocal[0]) > tolerance)
        {
            return;
        }

        while (nextSequentialPointIndex < activeSequentialPathLocal.Count - 1 &&
            Vector2.Distance(localPoint, activeSequentialPathLocal[nextSequentialPointIndex]) <= tolerance)
        {
            nextSequentialPointIndex++;
        }
    }

    private Vector2 GetCenteredDrawingScreenPosition(Vector2 screenPosition)
    {
        if (!centerPenOnActiveStroke || revealTargetGraphic == null ||
            targetSamplePoints == null || targetSamplePoints.Count == 0)
        {
            return screenPosition;
        }

        RectTransform targetRect = revealTargetGraphic.rectTransform;
        Camera uiCamera = (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : parentCanvas?.worldCamera;
        if (uiCamera == null && parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = mainCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetRect, screenPosition, uiCamera, out Vector2 touchedLocalPoint))
        {
            return screenPosition;
        }

        float searchRadius = Mathf.Max(
            letterBoundaryTolerance * 2f,
            penRadius * centerSearchRadiusMultiplier
        );
        float sqrSearchRadius = searchRadius * searchRadius;
        Vector2 centeredLocalPoint = Vector2.zero;
        int nearbyPointCount = 0;

        for (int i = 0; i < targetSamplePoints.Count; i++)
        {
            Vector2 samplePoint = targetSamplePoints[i];
            if ((samplePoint - touchedLocalPoint).sqrMagnitude <= sqrSearchRadius)
            {
                centeredLocalPoint += samplePoint;
                nearbyPointCount++;
            }
        }

        if (nearbyPointCount == 0)
        {
            return screenPosition;
        }

        centeredLocalPoint /= nearbyPointCount;
        Vector3 centeredWorldPoint = targetRect.TransformPoint(centeredLocalPoint);
        return RectTransformUtility.WorldToScreenPoint(uiCamera, centeredWorldPoint);
    }

    private bool IsLocalPointInsideRevealSprite(Vector2 localPoint, RectTransform targetRect)
    {
        Image image = revealTargetGraphic as Image;
        return IsLocalPointInsideSprite(localPoint, targetRect, GetActiveCoverageSprite(image), useSampleFallback: true);
    }

    private bool IsLocalPointInsideSprite(Vector2 localPoint, RectTransform targetRect, Sprite sprite, bool useSampleFallback)
    {
        if (!targetRect.rect.Contains(localPoint))
        {
            return useSampleFallback && IsNearSamplePoint(localPoint);
        }

        if (sprite == null || sprite.texture == null)
        {
            return true;
        }

        Rect rect = targetRect.rect;
        float u = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float v = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        Rect spriteRect = sprite.rect;
        int px = Mathf.Clamp(Mathf.FloorToInt(spriteRect.x + u * spriteRect.width), (int)spriteRect.xMin, (int)spriteRect.xMax - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(spriteRect.y + v * spriteRect.height), (int)spriteRect.yMin, (int)spriteRect.yMax - 1);

        try
        {
            if (sprite.texture.GetPixel(px, py).a > 0.08f)
            {
                return true;
            }
        }
        catch (UnityException)
        {
            return !useSampleFallback || IsNearSamplePoint(localPoint);
        }

        return useSampleFallback && IsNearSamplePoint(localPoint);
    }

    private bool IsNearSamplePoint(Vector2 localPoint)
    {
        if (targetSamplePoints == null || targetSamplePoints.Count == 0)
        {
            return false;
        }

        float gridSpacing = 0f;
        if (revealTargetGraphic != null && sampleGridResolution > 0)
        {
            Rect rect = revealTargetGraphic.rectTransform.rect;
            gridSpacing = Mathf.Max(rect.width, rect.height) / sampleGridResolution;
        }

        float tolerance = Mathf.Max(letterBoundaryTolerance, gridSpacing * 1.5f, penRadius * 0.25f);
        float sqrTolerance = tolerance * tolerance;

        for (int i = 0; i < targetSamplePoints.Count; i++)
        {
            if ((targetSamplePoints[i] - localPoint).sqrMagnitude <= sqrTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private void StartNewStroke(Vector2 screenPosition)
    {
        GameObject lineObj = new GameObject("PenStroke", typeof(RectTransform));
        
        Transform targetParent = null;
        if (drawingMode == DrawingMode.RevealMask && maskParent != null)
        {
            targetParent = maskParent;
        }
        else if (linesParent != null)
        {
            targetParent = linesParent;
        }
        else if (parentCanvas != null)
        {
            targetParent = parentCanvas.transform;
        }

        if (targetParent != null)
        {
            lineObj.transform.SetParent(targetParent, false);

            if (drawingMode == DrawingMode.RevealMask)
            {
                // Stencil writers must render before every sequence reveal layer.
                lineObj.transform.SetSiblingIndex(GetStencilWriterSiblingIndex(targetParent));
            }
            else if (targetParent == parentCanvas.transform)
            {
                // Place stroke directly behind the hand image in the Canvas hierarchy
                lineObj.transform.SetSiblingIndex(transform.GetSiblingIndex());
            }
        }

        RectTransform lineRect = lineObj.GetComponent<RectTransform>();
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = Vector2.one;
        lineRect.sizeDelta = Vector2.zero;
        lineRect.anchoredPosition = Vector2.zero;

        if (!TryGetStrokeLocalPoint(screenPosition, lineRect, out Vector2 localPoint))
        {
            Destroy(lineObj);
            return;
        }

        currentUILine = lineObj.AddComponent<UILine>();
        if (drawingMode == DrawingMode.RevealMask && maskWriterMaterial != null)
        {
            currentUILine.material = CreateStrokeMaskMaterial();
        }
        else
        {
            currentUILine.color = penColor;
        }
        currentUILine.thickness = penRadius;

        lastLocalPoint = localPoint;
        currentUILine.AddPoint(localPoint);
        BeginSequentialStroke(screenPosition);
        OnTraceStarted?.Invoke();

        CheckCoverageProgress(isFinalRelease: false);
    }

    private void UpdateCurrentStroke(Vector2 screenPosition)
    {
        RectTransform strokeRect = currentUILine != null ? currentUILine.rectTransform : null;
        if (TryGetStrokeLocalPoint(screenPosition, strokeRect, out Vector2 localPoint))
        {
            if (Vector2.Distance(lastLocalPoint, localPoint) >= minDistanceBetweenPoints)
            {
                lastLocalPoint = localPoint;
                currentUILine.AddPoint(localPoint);

                CheckCoverageProgress(isFinalRelease: false);
            }
        }
    }

    private bool TryGetStrokeLocalPoint(Vector2 screenPosition, RectTransform strokeRect, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;

        if (strokeRect == null)
        {
            return false;
        }

        Camera uiCamera = (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : parentCanvas.worldCamera;
        if (uiCamera == null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = mainCamera;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(strokeRect, screenPosition, uiCamera, out localPoint);
    }

    private void FinishStroke()
    {
        bool hadActiveStroke = currentUILine != null;
#if UNITY_EDITOR
        if (hadActiveStroke)
        {
            CacheCurrentStrokeAsHintPath();
        }
#endif
        CheckCoverageProgress(isFinalRelease: true);
        currentUILine = null;
        nextSequentialPointIndex = 1;

        if (hadActiveStroke)
        {
            OnTraceStopped?.Invoke();
#if UNITY_EDITOR
            if (saveHandHintPathFromNextStroke)
            {
                SaveLastCompletedHintPath();
            }
#endif
        }
    }

    /// <summary>
    /// Checks stroke line coverage against remaining uncovered sample points.
    /// </summary>
    private void CheckCoverageProgress(bool isFinalRelease)
    {
        if (!autoCompleteOnHighCoverage || isCompleted || totalSamplePointsCount == 0) return;
        if (currentUILine == null || currentUILine.points == null || currentUILine.points.Count == 0) return;

        List<Vector2> linePoints = currentUILine.points;
        float hitRadius = penRadius * hitRadiusRatio;

        RectTransform strokeRect = currentUILine.rectTransform;
        bool hasTarget = revealTargetGraphic != null && strokeRect != null;

        for (int i = remainingUncoveredPoints.Count - 1; i >= 0; i--)
        {
            Vector2 samplePt = remainingUncoveredPoints[i];
            
            Vector2 strokeSpacePt = samplePt;
            if (hasTarget)
            {
                Vector3 worldPt = revealTargetGraphic.rectTransform.TransformPoint(samplePt);
                strokeSpacePt = strokeRect.InverseTransformPoint(worldPt);
            }

            bool covered = false;
            for (int p = 0; p < linePoints.Count - 1; p++)
            {
                if (IsPointNearSegment(strokeSpacePt, linePoints[p], linePoints[p + 1], hitRadius))
                {
                    covered = true;
                    break;
                }
            }

            if (!covered && linePoints.Count == 1)
            {
                if (Vector2.Distance(strokeSpacePt, linePoints[0]) <= hitRadius)
                {
                    covered = true;
                }
            }

            if (covered)
            {
                remainingUncoveredPoints.RemoveAt(i);
            }
        }

        int coveredCount = totalSamplePointsCount - remainingUncoveredPoints.Count;
        currentCoverageProgress = (float)coveredCount / totalSamplePointsCount;

        float activeCompletionThreshold = GetActiveCompletionThreshold();

        if (isFinalRelease || currentCoverageProgress >= activeCompletionThreshold)
        {
            Debug.Log($"[PenDrawer] Progress: {currentCoverageProgress * 100:F1}% ({coveredCount}/{totalSamplePointsCount})");
        }

        if (currentCoverageProgress >= activeCompletionThreshold)
        {
            if (!completeOnlyOnMouseRelease || isFinalRelease)
            {
                AutoCompleteMask();
            }
        }
    }

    private bool IsPointNearSegment(Vector2 p, Vector2 a, Vector2 b, float radius)
    {
        Vector2 ab = b - a;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen == 0) return Vector2.Distance(p, a) <= radius;

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLen);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(p, projection) <= radius;
    }

    /// <summary>
    /// Instantly forces 100% reveal of the mask graphic.
    /// </summary>
    public void AutoCompleteMask()
    {
        if (isCompleted) return;

        activeLetterSequence = GetActiveLetterSequence();
        if (activeLetterSequence != null)
        {
            CompleteCurrentSequenceStrokeLayer();
            if (HasNextSequenceStep())
            {
                OnStrokeStepCompleted?.Invoke();
                AdvanceToNextSequenceStep();
                return;
            }
        }

        isCompleted = true;
        drawingLockedAfterCompletion = true;
        drawingLockedUntilRelease = true;
        currentCoverageProgress = 1f;

        if (revealMaterial != null)
        {
            // Set Stencil Comp to Always (8) to display 100% of the graphic
            revealMaterial.SetFloat("_StencilComp", 8f);
        }
        if (hiddenFinalRevealTarget != null)
        {
            hiddenFinalRevealTarget.enabled = true;
        }

        Debug.Log($"[PenDrawer] '{GetActiveTracingName()}' reached {GetActiveCompletionThreshold() * 100:F0}% coverage! Auto-completed to 100%.");
        OnMaskCompleted?.Invoke();
    }

    private void CompleteCurrentSequenceStrokeLayer()
    {
        if (currentSequenceStepIndex < 0 || currentSequenceStepIndex >= sequenceRevealMaterials.Count)
        {
            return;
        }

        Material activeLayerMaterial = sequenceRevealMaterials[currentSequenceStepIndex];
        if (activeLayerMaterial != null && activeLayerMaterial.HasProperty("_StencilComp"))
        {
            activeLayerMaterial.SetFloat("_StencilComp", 8f);
        }
    }

    private void AdvanceToNextSequenceStep()
    {
        currentSequenceStepIndex++;
        currentCoverageProgress = 0f;
        isCompleted = false;
        currentUILine = null;
        nextSequentialPointIndex = 1;

        RebuildSamplePoints();
        Debug.Log($"[PenDrawer] Advanced to {GetActiveTracingName()}.");
    }

    private Vector2 GetPenTipScreenPosition()
    {
        Vector2 mousePos = GetMouseScreenPosition();
        return mousePos + tipOffset;
    }

    public void ClearAllLines()
    {
        currentSequenceStepIndex = 0;
        activeLetterSequence = GetActiveLetterSequence();
        nextSequentialPointIndex = 1;
        drawingLockedAfterCompletion = false;
        drawingLockedUntilRelease = false;
        ResetSequenceStrokeLayers();

        List<Transform> parentsToCheck = new List<Transform>();
        if (maskParent != null) parentsToCheck.Add(maskParent);
        if (linesParent != null) parentsToCheck.Add(linesParent);
        if (revealTargetGraphic != null && revealTargetGraphic.transform.parent != null &&
            !parentsToCheck.Contains(revealTargetGraphic.transform.parent))
        {
            parentsToCheck.Add(revealTargetGraphic.transform.parent);
        }
        if (parentCanvas != null && !parentsToCheck.Contains(parentCanvas.transform)) parentsToCheck.Add(parentCanvas.transform);

        foreach (Transform parent in parentsToCheck)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith("PenStroke"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        for (int i = 0; i < runtimeStrokeMaterials.Count; i++)
        {
            if (runtimeStrokeMaterials[i] != null)
            {
                Destroy(runtimeStrokeMaterials[i]);
            }
        }
        runtimeStrokeMaterials.Clear();

        // Reset completion status and sample points for the new/cleared letter
        RebuildSamplePoints();
    }

#if UNITY_EDITOR
    private void HandleHintPathRecordingShortcut()
    {
        if (!Application.isPlaying || tracingSequence == null || revealTargetGraphic == null)
        {
            return;
        }

        if (!IsSaveHintPathKeyPressed())
        {
            return;
        }

        if (currentUILine != null)
        {
            CacheCurrentStrokeAsHintPath();
        }

        SaveLastCompletedHintPath();
    }

    private bool IsSaveHintPathKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        return false;
#endif
    }

    private void CacheCurrentStrokeAsHintPath()
    {
        lastCompletedHintPathLocal.Clear();

        if (!TryGetCurrentStrokeInRevealLocalSpace(activeHintPathLocalBuffer))
        {
            return;
        }

        ResamplePath(activeHintPathLocalBuffer, lastCompletedHintPathLocal, recordedHintPathMaxPoints);
        lastCompletedHintLetterNumber = currentLetterNumber;
        lastCompletedHintStepIndex = currentSequenceStepIndex;
    }

    private bool TryGetCurrentStrokeInRevealLocalSpace(List<Vector2> outputPath)
    {
        if (outputPath == null)
        {
            return false;
        }

        outputPath.Clear();

        if (currentUILine == null ||
            currentUILine.points == null ||
            currentUILine.points.Count < 2 ||
            currentUILine.rectTransform == null ||
            revealTargetGraphic == null)
        {
            return false;
        }

        RectTransform strokeRect = currentUILine.rectTransform;
        RectTransform revealRect = revealTargetGraphic.rectTransform;
        float minSqrDistance = Mathf.Max(0f, recordedHintPathMinPointDistance);
        minSqrDistance *= minSqrDistance;
        Vector2 lastAccepted = Vector2.zero;
        bool hasAcceptedPoint = false;

        for (int i = 0; i < currentUILine.points.Count; i++)
        {
            Vector3 worldPoint = strokeRect.TransformPoint(currentUILine.points[i]);
            Vector2 revealLocalPoint = revealRect.InverseTransformPoint(worldPoint);

            if (hasAcceptedPoint && (revealLocalPoint - lastAccepted).sqrMagnitude < minSqrDistance)
            {
                continue;
            }

            outputPath.Add(revealLocalPoint);
            lastAccepted = revealLocalPoint;
            hasAcceptedPoint = true;
        }

        if (outputPath.Count < 2 && currentUILine.points.Count >= 2)
        {
            Vector3 firstWorldPoint = strokeRect.TransformPoint(currentUILine.points[0]);
            Vector3 lastWorldPoint = strokeRect.TransformPoint(currentUILine.points[currentUILine.points.Count - 1]);
            outputPath.Clear();
            outputPath.Add(revealRect.InverseTransformPoint(firstWorldPoint));
            outputPath.Add(revealRect.InverseTransformPoint(lastWorldPoint));
        }

        return outputPath.Count >= 2;
    }

    private void ResamplePath(List<Vector2> sourcePath, List<Vector2> outputPath, int maxPoints)
    {
        outputPath.Clear();

        if (sourcePath == null || sourcePath.Count < 2)
        {
            return;
        }

        int targetCount = Mathf.Clamp(maxPoints, 2, sourcePath.Count);
        if (targetCount >= sourcePath.Count)
        {
            outputPath.AddRange(sourcePath);
            return;
        }

        float totalLength = GetPathLength(sourcePath);
        if (totalLength <= 0.01f)
        {
            outputPath.Add(sourcePath[0]);
            outputPath.Add(sourcePath[sourcePath.Count - 1]);
            return;
        }

        for (int i = 0; i < targetCount; i++)
        {
            float distance = totalLength * i / (targetCount - 1);
            outputPath.Add(GetPointAtDistance(sourcePath, distance));
        }
    }

    private float GetPathLength(List<Vector2> path)
    {
        float length = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            length += Vector2.Distance(path[i], path[i + 1]);
        }

        return length;
    }

    private Vector2 GetPointAtDistance(List<Vector2> path, float targetDistance)
    {
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
                float t = (targetDistance - travelled) / segmentLength;
                return Vector2.LerpUnclamped(from, to, t);
            }

            travelled += segmentLength;
        }

        return path[path.Count - 1];
    }

    private void SaveLastCompletedHintPath()
    {
        if (tracingSequence == null || lastCompletedHintPathLocal.Count < 2)
        {
            Debug.LogWarning("[PenDrawer] Draw a stroke first before saving a hand hint path.");
            return;
        }

        LetterSequence sequence = tracingSequence.GetLetter(lastCompletedHintLetterNumber);
        TracingStrokeStep step = sequence != null ? sequence.GetStep(lastCompletedHintStepIndex) : null;
        if (step == null)
        {
            Debug.LogWarning($"[PenDrawer] Could not find letter {lastCompletedHintLetterNumber}, stroke {lastCompletedHintStepIndex + 1} to save hand hint path.");
            return;
        }

        UnityEngine.Object assetToSave = tracingSequence.GetLetterAsset(lastCompletedHintLetterNumber);
        if (assetToSave == null)
        {
            assetToSave = tracingSequence;
        }

        UnityEditor.Undo.RecordObject(assetToSave, "Save Recorded Hand Hint Path");
        step.ReplaceHintPath(lastCompletedHintPathLocal);
        UnityEditor.EditorUtility.SetDirty(assetToSave);
        UnityEditor.AssetDatabase.SaveAssets();
        saveHandHintPathFromNextStroke = false;
        Debug.Log($"[PenDrawer] Saved {lastCompletedHintPathLocal.Count} hand hint path points for letter {lastCompletedHintLetterNumber}, stroke {lastCompletedHintStepIndex + 1}.");
    }
#endif

    private void OnDrawGizmosSelected()
    {
        if (revealTargetGraphic == null || targetSamplePoints == null || targetSamplePoints.Count == 0) return;

        foreach (Vector2 pt in targetSamplePoints)
        {
            if (remainingUncoveredPoints.Contains(pt))
            {
                Gizmos.color = Color.red;
            }
            else
            {
                Gizmos.color = Color.green;
            }

            Vector3 worldPos = revealTargetGraphic.rectTransform.TransformPoint(pt);
            Gizmos.DrawSphere(worldPos, 3f);
        }
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

    private bool IsMouseJustPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pen.current != null && (Pen.current.tip.wasPressedThisFrame || Pen.current.press.wasPressedThisFrame))
        {
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private bool IsMouseHeldDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pen.current != null && (Pen.current.tip.isPressed || Pen.current.press.isPressed))
        {
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            return true;
        }
        if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#else
        return false;
#endif
    }

    private bool IsMouseJustReleased()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pen.current != null && (Pen.current.tip.wasReleasedThisFrame || Pen.current.press.wasReleasedThisFrame))
        {
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            return true;
        }
        if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonUp(0);
#else
        return false;
#endif
    }

    private bool IsRightMouseJustPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pen.current != null && (Pen.current.firstBarrelButton.wasPressedThisFrame || Pen.current.secondBarrelButton.wasPressedThisFrame))
        {
            return true;
        }
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }
}
