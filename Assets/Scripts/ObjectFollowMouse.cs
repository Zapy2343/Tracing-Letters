using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ObjectFollowMouse : MonoBehaviour
{
    public enum FollowAnchor
    {
        Center,
        TopLeft
    }

    [Header("Target Settings")]
    [Tooltip("The object that will follow the mouse cursor. If left unassigned, this object itself will follow the mouse.")]
    [SerializeField] private GameObject targetObject;

    [Header("Anchor Settings")]
    [Tooltip("Select whether the Center or Top-Left corner of the object pins to the mouse.")]
    [SerializeField] private FollowAnchor anchorPoint = FollowAnchor.TopLeft;

    [Header("UI Canvas Settings (For UI Elements)")]
    [Tooltip("Target Canvas if moving a UI element. Auto-detected from Target Object if left empty.")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Shadow Settings (Optional)")]
    [Tooltip("UI Shadow component to dynamically adjust. Auto-detected from Target Object if left empty.")]
    [SerializeField] private Shadow targetShadow;

    [Tooltip("Shadow distance when mouse button is released.")]
    [SerializeField] private Vector2 defaultShadowDistance = new Vector2(-36f, -45f);

    [Tooltip("Shadow distance when mouse button is held down.")]
    [SerializeField] private Vector2 pressedShadowDistance = new Vector2(-5f, -5f);

    [Tooltip("Speed of transition between released and pressed shadow state (0 = instant).")]
    [SerializeField] private float shadowTransitionSpeed = 20f;

    [Header("Camera Settings (For World Space Objects)")]
    [Tooltip("Camera used to calculate screen to world position. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera mainCamera;

    [Header("World Space Movement Settings")]
    [Tooltip("If true, retains the target's Z position (ideal for 2D world games).")]
    [SerializeField] private bool is2D = true;

    [Tooltip("Distance from camera along Z axis (used if is2D is false).")]
    [SerializeField] private float distanceFromCamera = 10f;

    [Header("Smoothing")]
    [Tooltip("Set above 0 for smooth movement delay (0 = instant tracking).")]
    [SerializeField] private float smoothSpeed = 0f;

    private RectTransform targetRectTransform;
    private RectTransform canvasRectTransform;
    private Renderer targetRenderer;
    private Vector3 velocity3D = Vector3.zero;
    private Vector2 velocity2D = Vector2.zero;

    private void Start()
    {
        // Default target object to this GameObject if null
        if (targetObject == null)
        {
            targetObject = gameObject;
        }

        // Find main camera if not set
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (targetObject != null)
        {
            targetRectTransform = targetObject.GetComponent<RectTransform>();
            targetRenderer = targetObject.GetComponent<Renderer>();

            if (targetShadow == null)
            {
                targetShadow = targetObject.GetComponent<Shadow>();
            }

            if (targetCanvas == null)
            {
                targetCanvas = targetObject.GetComponentInParent<Canvas>();
            }

            if (targetCanvas != null)
            {
                canvasRectTransform = targetCanvas.GetComponent<RectTransform>();
            }
        }
    }

    private void Update()
    {
        if (targetObject == null) return;

        Vector2 mouseScreenPos = GetMouseScreenPosition();
        bool isPressed = IsMousePressed();

        // Handle dynamic shadow effect
        UpdateShadow(isPressed);

        // --- UI CANVAS MODE ---
        if (targetCanvas != null && targetRectTransform != null && canvasRectTransform != null)
        {
            Camera uiCamera = (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : targetCanvas.worldCamera;
            if (uiCamera == null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = mainCamera;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, mouseScreenPos, uiCamera, out Vector2 localPoint))
            {
                Vector2 finalAnchoredPos = localPoint;

                if (anchorPoint == FollowAnchor.TopLeft)
                {
                    Rect rect = targetRectTransform.rect;
                    // Offset by local top-left position relative to pivot
                    Vector2 topLeftOffset = new Vector2(rect.xMin, rect.yMax);
                    finalAnchoredPos -= topLeftOffset;
                }

                if (smoothSpeed > 0f)
                {
                    targetRectTransform.anchoredPosition = Vector2.SmoothDamp(
                        targetRectTransform.anchoredPosition,
                        finalAnchoredPos,
                        ref velocity2D,
                        smoothSpeed
                    );
                }
                else
                {
                    targetRectTransform.anchoredPosition = finalAnchoredPos;
                }
            }
            return;
        }

        // --- WORLD SPACE MODE ---
        if (mainCamera == null) return;

        float zDepth = is2D 
            ? Mathf.Abs(mainCamera.transform.position.z - targetObject.transform.position.z) 
            : distanceFromCamera;

        Vector3 mouseScreenPosition3D = new Vector3(mouseScreenPos.x, mouseScreenPos.y, zDepth);
        Vector3 targetWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition3D);

        if (anchorPoint == FollowAnchor.TopLeft && targetRenderer != null)
        {
            Vector3 extents = targetRenderer.bounds.extents;
            targetWorldPosition -= new Vector3(-extents.x, extents.y, 0f);
        }

        if (is2D)
        {
            targetWorldPosition.z = targetObject.transform.position.z;
        }

        if (smoothSpeed > 0f)
        {
            targetObject.transform.position = Vector3.SmoothDamp(
                targetObject.transform.position, 
                targetWorldPosition, 
                ref velocity3D, 
                smoothSpeed
            );
        }
        else
        {
            targetObject.transform.position = targetWorldPosition;
        }
    }

    private void UpdateShadow(bool isPressed)
    {
        if (targetShadow == null) return;

        Vector2 targetDist = isPressed ? pressedShadowDistance : defaultShadowDistance;

        if (shadowTransitionSpeed > 0f)
        {
            targetShadow.effectDistance = Vector2.Lerp(
                targetShadow.effectDistance,
                targetDist,
                Time.deltaTime * shadowTransitionSpeed
            );
        }
        else
        {
            targetShadow.effectDistance = targetDist;
        }
    }

    private Vector2 GetMouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        // 1. Check Pen (XP-Pen, Wacom, Surface Pen)
        if (Pen.current != null)
        {
            Vector2 penPos = Pen.current.position.ReadValue();
            if (penPos != Vector2.zero) return penPos;
        }

        // 2. Check Mouse
        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (mousePos != Vector2.zero) return mousePos;
        }

        // 3. Check general Pointer
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

    private bool IsMousePressed()
    {
#if ENABLE_INPUT_SYSTEM
        // Check Pen tip / press
        if (Pen.current != null && (Pen.current.tip.isPressed || Pen.current.press.isPressed))
        {
            return true;
        }

        // Check Mouse left button
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            return true;
        }

        // Check general Pointer
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
}
