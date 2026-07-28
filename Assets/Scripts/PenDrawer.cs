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

    [Header("Pen Tip Reference & Offset")]
    [Tooltip("Transform representing the pencil tip. If unassigned, uses this GameObject with the tipOffset.")]
    [SerializeField] private Transform penTip;

    [Tooltip("Offset in UI pixels from the pencil object center/pivot to the pencil tip.")]
    [SerializeField] private Vector2 tipOffset = new Vector2(0f, 0f);

    [Header("Pen Customization")]
    [Tooltip("Pen stroke color (used in NormalDraw mode).")]
    [SerializeField] private Color penColor = Color.blue;

    [Tooltip("Radius / thickness of the pen stroke line in UI units.")]
    [Range(1f, 100f)]
    [SerializeField] private float penRadius = 15f;

    [Header("Drawing Settings")]
    [Tooltip("Minimum distance cursor must move to add a new point to the stroke.")]
    [SerializeField] private float minDistanceBetweenPoints = 3f;

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

    [Header("Coverage Progress (Read Only)")]
    [Range(0f, 1f)]
    [SerializeField] private float currentCoverageProgress = 0f;
    [SerializeField] private bool isCompleted = false;

    [Header("Clear Settings")]
    [Tooltip("If true, double-clicking the right mouse button clears all drawn strokes.")]
    [SerializeField] private bool clearOnDoubleRightClick = true;

    [Tooltip("Maximum time gap in seconds between right clicks to count as a double-click.")]
    [SerializeField] private float doubleClickThreshold = 0.3f;

    private UILine currentUILine;
    private RectTransform canvasRectTransform;
    private Camera mainCamera;
    private Vector2 lastLocalPoint;
    private float lastRightClickTime = -1f;
    private Material maskWriterMaterial;
    private Material revealMaterial;

    // Sample points data for coverage calculation
    private List<Vector2> targetSamplePoints = new List<Vector2>();
    private List<Vector2> remainingUncoveredPoints = new List<Vector2>();
    private int totalSamplePointsCount = 0;

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
        RebuildSamplePoints();
    }

    /// <summary>
    /// Sets a new graphic as the reveal target and rebuilds stencil materials & sample points.
    /// </summary>
    public void SetRevealTargetGraphic(Graphic graphic)
    {
        revealTargetGraphic = graphic;
        SetupStencilMaterials();
        ClearAllLines();
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
                maskWriterMaterial = customMaskWriterMaterial;
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
                revealMaterial = customRevealMaterial;
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
        Sprite sprite = (image != null) ? image.sprite : null;
        if (sprite == null)
        {
            Debug.LogWarning($"[PenDrawer] '{revealTargetGraphic.name}' has no Sprite assigned! Coverage progress cannot be computed.");
            return;
        }

        targetSamplePoints = GenerateSamplePoints(sprite, revealTargetGraphic.rectTransform, sampleGridResolution);
        remainingUncoveredPoints = new List<Vector2>(targetSamplePoints);
        totalSamplePointsCount = targetSamplePoints.Count;

        Debug.Log($"[PenDrawer] Rebuilt {totalSamplePointsCount} coverage sample points for '{revealTargetGraphic.name}'.");
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

        if (IsMouseJustPressed())
        {
            StartNewStroke(tipScreenPos);
        }
        else if (IsMouseHeldDown() && currentUILine != null)
        {
            UpdateCurrentStroke(tipScreenPos);
        }
        else if (IsMouseJustReleased())
        {
            FinishStroke();
        }
    }

    private void StartNewStroke(Vector2 screenPosition)
    {
        Camera uiCamera = (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : parentCanvas.worldCamera;
        if (uiCamera == null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = mainCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPosition, uiCamera, out Vector2 localPoint))
        {
            return;
        }

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
                // Strokes MUST be drawn BEFORE revealTargetGraphic in the Canvas hierarchy so stencil buffer is populated first
                if (revealTargetGraphic != null && revealTargetGraphic.transform.parent == targetParent)
                {
                    int targetIndex = revealTargetGraphic.transform.GetSiblingIndex();
                    lineObj.transform.SetSiblingIndex(targetIndex);
                }
                else
                {
                    lineObj.transform.SetAsFirstSibling();
                }
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

        currentUILine = lineObj.AddComponent<UILine>();
        if (drawingMode == DrawingMode.RevealMask && maskWriterMaterial != null)
        {
            currentUILine.material = maskWriterMaterial;
        }
        else
        {
            currentUILine.color = penColor;
        }
        currentUILine.thickness = penRadius;

        lastLocalPoint = localPoint;
        currentUILine.AddPoint(localPoint);

        CheckCoverageProgress(isFinalRelease: false);
    }

    private void UpdateCurrentStroke(Vector2 screenPosition)
    {
        Camera uiCamera = (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : parentCanvas.worldCamera;
        if (uiCamera == null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = mainCamera;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPosition, uiCamera, out Vector2 localPoint))
        {
            if (Vector2.Distance(lastLocalPoint, localPoint) >= minDistanceBetweenPoints)
            {
                lastLocalPoint = localPoint;
                currentUILine.AddPoint(localPoint);

                CheckCoverageProgress(isFinalRelease: false);
            }
        }
    }

    private void FinishStroke()
    {
        CheckCoverageProgress(isFinalRelease: true);
        currentUILine = null;
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

        bool hasTarget = revealTargetGraphic != null && canvasRectTransform != null;

        for (int i = remainingUncoveredPoints.Count - 1; i >= 0; i--)
        {
            Vector2 samplePt = remainingUncoveredPoints[i];
            
            // Transform samplePt from revealTargetGraphic local space -> world space -> canvasRectTransform local space
            Vector2 canvasSpacePt = samplePt;
            if (hasTarget)
            {
                Vector3 worldPt = revealTargetGraphic.rectTransform.TransformPoint(samplePt);
                canvasSpacePt = canvasRectTransform.InverseTransformPoint(worldPt);
            }

            bool covered = false;
            for (int p = 0; p < linePoints.Count - 1; p++)
            {
                if (IsPointNearSegment(canvasSpacePt, linePoints[p], linePoints[p + 1], hitRadius))
                {
                    covered = true;
                    break;
                }
            }

            if (!covered && linePoints.Count == 1)
            {
                if (Vector2.Distance(canvasSpacePt, linePoints[0]) <= hitRadius)
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

        if (isFinalRelease || currentCoverageProgress >= completionThreshold)
        {
            Debug.Log($"[PenDrawer] Progress: {currentCoverageProgress * 100:F1}% ({coveredCount}/{totalSamplePointsCount})");
        }

        if (currentCoverageProgress >= completionThreshold)
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

        isCompleted = true;
        currentCoverageProgress = 1f;

        if (revealMaterial != null)
        {
            // Set Stencil Comp to Always (8) to display 100% of the graphic
            revealMaterial.SetFloat("_StencilComp", 8f);
        }

        Debug.Log($"[PenDrawer] Mask reached {completionThreshold * 100:F0}% coverage! Auto-completed to 100%.");
        OnMaskCompleted?.Invoke();
    }

    private Vector2 GetPenTipScreenPosition()
    {
        Vector2 mousePos = GetMouseScreenPosition();
        return mousePos + tipOffset;
    }

    public void ClearAllLines()
    {
        List<Transform> parentsToCheck = new List<Transform>();
        if (maskParent != null) parentsToCheck.Add(maskParent);
        if (linesParent != null) parentsToCheck.Add(linesParent);
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

        // Reset completion status and sample points for the new/cleared letter
        RebuildSamplePoints();
    }

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
