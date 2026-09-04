using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TracingStrokeHintController : MonoBehaviour
{
    [SerializeField] private PenDrawer penDrawer;
    [SerializeField] private RectTransform handTemplate;
    [SerializeField] private float initialDelay = 0.6f;
    [SerializeField] private float stepDelay = 0.35f;
    [SerializeField] private float hintDuration = 1.25f;
    [SerializeField] private Vector2 hintOffset = new Vector2(35f, -35f);
    [SerializeField] private float handScale = 1.65f;

    private RectTransform hintHand;
    private CanvasGroup hintCanvasGroup;
    private Coroutine hintRoutine;
    private int shownLetterNumber = -1;
    private int shownStepIndex = -1;
    private int observedLetterNumber = -1;
    private int observedStepIndex = -1;
    private readonly HashSet<string> playedStrokeHints = new HashSet<string>();
    private readonly List<Vector2> localHintPath = new List<Vector2>();
    private readonly List<Vector2> canvasHintPath = new List<Vector2>();

    private float idleTimer = 0f;

    private void Awake()
    {
        ResolveReferences();
        CreateHintHand();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CreateHintHand();
        Subscribe();
        idleTimer = 0f;
        UpdateObservedStep();
        SetHintGate(false);
        ScheduleHint(initialDelay);
    }

    private void Update()
    {
        if (penDrawer == null)
        {
            return;
        }

        if (penDrawer.IsActivelyDrawing || IsUserAttemptingDraw())
        {
            idleTimer = 0f;
            if (hintRoutine != null)
            {
                StopHint();
            }
            return;
        }

        if (penDrawer.IsDrawingLockedAfterCompletion)
        {
            idleTimer = 0f;
            if (hintRoutine != null)
            {
                StopHint();
            }
            return;
        }

        if (penDrawer.CurrentLetterNumber != observedLetterNumber || penDrawer.CurrentSequenceStepIndex != observedStepIndex)
        {
            float delay = (penDrawer.CurrentLetterNumber != observedLetterNumber) ? initialDelay : stepDelay;
            idleTimer = 0f;
            UpdateObservedStep();
            SetHintGate(false);
            ScheduleHint(delay);
            return;
        }

        if (hintRoutine == null)
        {
            idleTimer += Time.unscaledDeltaTime;
            if (idleTimer >= stepDelay)
            {
                idleTimer = 0f;
                ScheduleHint(0f);
            }
        }
    }

    private bool IsUserAttemptingDraw()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0))
        {
            return true;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopHint();
        SetHintGate(false);
    }

    private void Subscribe()
    {
        if (penDrawer == null)
        {
            return;
        }

        penDrawer.OnStrokeStepCompleted.RemoveListener(ShowNextStepHint);
        penDrawer.OnMaskCompleted.RemoveListener(StopHint);
        penDrawer.OnTraceStarted.RemoveListener(StopHint);

        penDrawer.OnStrokeStepCompleted.AddListener(ShowNextStepHint);
        penDrawer.OnMaskCompleted.AddListener(StopHint);
        penDrawer.OnTraceStarted.AddListener(StopHint);
    }

    private void Unsubscribe()
    {
        if (penDrawer == null)
        {
            return;
        }

        penDrawer.OnStrokeStepCompleted.RemoveListener(ShowNextStepHint);
        penDrawer.OnMaskCompleted.RemoveListener(StopHint);
        penDrawer.OnTraceStarted.RemoveListener(StopHint);
    }

    private void ShowNextStepHint()
    {
        ScheduleHint(stepDelay);
    }

    private void ScheduleHint(float delay)
    {
        if (penDrawer == null)
        {
            return;
        }

        StopHint();
        hintRoutine = StartCoroutine(HintRoutine(delay));
    }

    private IEnumerator HintRoutine(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (!isActiveAndEnabled || penDrawer == null || penDrawer.IsActivelyDrawing || penDrawer.IsDrawingLockedAfterCompletion || IsUserAttemptingDraw())
        {
            hintRoutine = null;
            yield break;
        }

        if (!TryGetActiveStepLocalPath(canvasHintPath))
        {
            HideHintHand();
            SetHintGate(false);
            hintRoutine = null;
            yield break;
        }

        shownLetterNumber = penDrawer.CurrentLetterNumber;
        shownStepIndex = penDrawer.CurrentSequenceStepIndex;
        UpdateObservedStep();

        hintHand.gameObject.SetActive(true);
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, hintDuration);

        while (elapsed < duration)
        {
            if (penDrawer == null ||
                penDrawer.IsActivelyDrawing ||
                IsUserAttemptingDraw() ||
                penDrawer.CurrentLetterNumber != shownLetterNumber ||
                penDrawer.CurrentSequenceStepIndex != shownStepIndex)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float fade = Mathf.Sin(t * Mathf.PI);

            hintHand.anchoredPosition = GetPointOnPath(canvasHintPath, eased) + hintOffset;
            hintHand.localScale = Vector3.one * (handScale * Mathf.Lerp(0.9f, 1.04f, fade));
            hintCanvasGroup.alpha = fade;
            yield return null;
        }

        HideHintHand();
        SetHintGate(false);
        hintRoutine = null;
        idleTimer = 0f;
    }

    private void SetHintGate(bool locked)
    {
        if (penDrawer != null)
        {
            penDrawer.SetHintGateLocked(locked);
        }
    }

    private bool TryGetActiveStepLocalPath(List<Vector2> outputPath)
    {
        if (outputPath == null)
        {
            return false;
        }

        outputPath.Clear();

        if (penDrawer == null || penDrawer.RevealTargetGraphic == null)
        {
            return false;
        }

        RectTransform targetRect = penDrawer.RevealTargetGraphic.rectTransform;

        TracingStrokeStep step = penDrawer.CurrentSequenceStep;
        bool hasPath = step != null && step.TryBuildHintPath(localHintPath);
        if (!hasPath && !penDrawer.TryGetActiveHintPathLocal(localHintPath))
        {
            return false;
        }

        Transform hintParent = hintHand != null ? hintHand.parent : transform;

        for (int i = 0; i < localHintPath.Count; i++)
        {
            outputPath.Add(WorldToHintParentLocal(targetRect.TransformPoint(localHintPath[i]), hintParent));
        }

        return outputPath.Count >= 2;
    }

    private Vector2 GetPointOnPath(List<Vector2> path, float normalizedTime)
    {
        if (path == null || path.Count == 0)
        {
            return Vector2.zero;
        }

        if (path.Count == 1)
        {
            return path[0];
        }

        float totalLength = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            totalLength += Vector2.Distance(path[i], path[i + 1]);
        }

        if (totalLength <= 0.01f)
        {
            return path[path.Count - 1];
        }

        float targetDistance = Mathf.Clamp01(normalizedTime) * totalLength;
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
                float segmentTime = (targetDistance - travelled) / segmentLength;
                return Vector2.LerpUnclamped(from, to, segmentTime);
            }

            travelled += segmentLength;
        }

        return path[path.Count - 1];
    }

    private Vector2 WorldToHintParentLocal(Vector3 worldPosition, Transform hintParent)
    {
        RectTransform parentRect = hintParent as RectTransform;
        if (parentRect == null)
        {
            return worldPosition;
        }

        return parentRect.InverseTransformPoint(worldPosition);
    }

    private void CreateHintHand()
    {
        if (hintHand != null || handTemplate == null)
        {
            return;
        }

        hintHand = Instantiate(handTemplate, handTemplate.parent);
        hintHand.name = "Stroke Hint Hand";
        hintHand.SetAsLastSibling();

        Graphic graphic = hintHand.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = false;
        }

        hintCanvasGroup = hintHand.GetComponent<CanvasGroup>();
        if (hintCanvasGroup == null)
        {
            hintCanvasGroup = hintHand.gameObject.AddComponent<CanvasGroup>();
        }

        HideHintHand();
    }

    private void HideHintHand()
    {
        if (hintHand == null)
        {
            return;
        }

        hintHand.gameObject.SetActive(false);
        hintHand.localScale = Vector3.one;

        if (hintCanvasGroup != null)
        {
            hintCanvasGroup.alpha = 0f;
        }
    }

    private void StopHint()
    {
        if (hintRoutine != null)
        {
            StopCoroutine(hintRoutine);
            hintRoutine = null;
        }

        HideHintHand();
    }

    private void ResolveReferences()
    {
        if (penDrawer == null)
        {
#if UNITY_2023_1_OR_NEWER
            penDrawer = FindFirstObjectByType<PenDrawer>();
#else
            penDrawer = FindObjectOfType<PenDrawer>();
#endif
        }
    }

    private void UpdateObservedStep()
    {
        if (penDrawer == null)
        {
            return;
        }

        observedLetterNumber = penDrawer.CurrentLetterNumber;
        observedStepIndex = penDrawer.CurrentSequenceStepIndex;
    }

    private bool IsCurrentStrokeHintPlayed()
    {
        return penDrawer != null && playedStrokeHints.Contains(GetCurrentStrokeHintKey());
    }

    private string GetCurrentStrokeHintKey()
    {
        if (penDrawer == null)
        {
            return string.Empty;
        }

        return $"{penDrawer.CurrentLetterNumber}:{penDrawer.CurrentSequenceStepIndex}";
    }
}
