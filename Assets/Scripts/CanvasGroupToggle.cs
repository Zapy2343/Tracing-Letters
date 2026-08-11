using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toggles CanvasGroups so only one listed group is visible at a time.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CanvasGroupToggle : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private readonly Dictionary<CanvasGroup, Coroutine> activeAnimations = new Dictionary<CanvasGroup, Coroutine>();
    private readonly Dictionary<CanvasGroup, Vector2> shownPositions = new Dictionary<CanvasGroup, Vector2>();
    private readonly Dictionary<CanvasGroup, Vector3> shownScales = new Dictionary<CanvasGroup, Vector3>();

    [SerializeField] private List<CanvasGroup> canvasGroupsToToggle = new List<CanvasGroup>();
    [SerializeField] private bool animateTransitions = true;
    [SerializeField] private float showDuration = 0.28f;
    [SerializeField] private float hideDuration = 0.18f;
    [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -35f);
    [SerializeField] private float hiddenScale = 0.94f;

    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0f;

    private void Awake()
    {
        ResolveCanvasGroup();
        ApplyState(canvasGroup, IsVisible);
    }

    [ContextMenu("Toggle")]
    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
    }

    [ContextMenu("Show")]
    public void Show()
    {
        ResolveCanvasGroup();
        Show(canvasGroup);
    }

    public void Show(CanvasGroup canvasGroupToShow)
    {
        HideAll(canvasGroupToShow);
        SetVisible(canvasGroupToShow, true, animateTransitions);
    }

    [ContextMenu("Hide")]
    public void Hide()
    {
        ResolveCanvasGroup();
        SetVisible(canvasGroup, false, animateTransitions);
    }

    public void SetVisible(bool visible)
    {
        ResolveCanvasGroup();

        if (visible)
        {
            Show(canvasGroup);
            return;
        }

        SetVisible(canvasGroup, false, animateTransitions);
    }

    public void HideAll()
    {
        HideAll(null);
    }

    private void HideAll(CanvasGroup exceptGroup)
    {
        foreach (CanvasGroup group in canvasGroupsToToggle)
        {
            if (group == exceptGroup)
            {
                continue;
            }

            SetVisible(group, false, animateTransitions);
        }
    }

    private void SetVisible(CanvasGroup group, bool visible, bool animated)
    {
        if (group == null)
        {
            return;
        }

        if (!animated || !Application.isPlaying)
        {
            ApplyState(group, visible);
            return;
        }

        if (activeAnimations.TryGetValue(group, out Coroutine activeAnimation) && activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        CaptureShownTransform(group);
        activeAnimations[group] = StartCoroutine(AnimateGroup(group, visible));
    }

    private IEnumerator AnimateGroup(CanvasGroup group, bool visible)
    {
        if (group == null)
        {
            yield break;
        }

        RectTransform rect = group.transform as RectTransform;
        Vector2 shownPosition = shownPositions.TryGetValue(group, out Vector2 cachedPosition)
            ? cachedPosition
            : rect != null ? rect.anchoredPosition : Vector2.zero;
        Vector3 shownScale = shownScales.TryGetValue(group, out Vector3 cachedScale)
            ? cachedScale
            : rect != null ? rect.localScale : Vector3.one;
        float startAlpha = group.alpha;
        float endAlpha = visible ? 1f : 0f;
        float duration = Mathf.Max(0.01f, visible ? showDuration : hideDuration);

        if (visible)
        {
            group.gameObject.SetActive(true);
            group.interactable = true;
            group.blocksRaycasts = true;
            ApplyTransformState(rect, shownPosition, shownScale, 0f);
        }
        else
        {
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = visible ? EaseOutBack(t) : EaseInCubic(t);
            float state = visible ? eased : 1f - eased;

            group.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
            ApplyTransformState(rect, shownPosition, shownScale, state);
            yield return null;
        }

        ApplyState(group, visible);
        ApplyTransformState(rect, shownPosition, shownScale, 1f);
        activeAnimations.Remove(group);
    }

    private void CaptureShownTransform(CanvasGroup group)
    {
        if (group == null || shownPositions.ContainsKey(group))
        {
            return;
        }

        RectTransform rect = group.transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        shownPositions[group] = rect.anchoredPosition;
        shownScales[group] = rect.localScale;
    }

    private void ApplyState(CanvasGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void ApplyTransformState(RectTransform rect, Vector2 shownPosition, Vector3 shownScale, float state)
    {
        if (rect == null)
        {
            return;
        }

        float clampedState = Mathf.Clamp01(state);
        rect.anchoredPosition = shownPosition + hiddenOffset * (1f - clampedState);
        rect.localScale = shownScale * Mathf.Lerp(hiddenScale, 1f, clampedState);
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private float EaseOutBack(float t)
    {
        const float overshoot = 1.35f;
        float shifted = t - 1f;
        return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
    }

    private void ResolveCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Reset()
    {
        ResolveCanvasGroup();

        if (canvasGroup != null && !canvasGroupsToToggle.Contains(canvasGroup))
        {
            canvasGroupsToToggle.Add(canvasGroup);
        }
    }

    private void OnValidate()
    {
        showDuration = Mathf.Max(0.01f, showDuration);
        hideDuration = Mathf.Max(0.01f, hideDuration);
        hiddenScale = Mathf.Clamp(hiddenScale, 0.1f, 1f);
    }
}
