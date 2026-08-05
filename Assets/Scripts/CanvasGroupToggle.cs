using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toggles CanvasGroups so only one listed group is visible at a time.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CanvasGroupToggle : MonoBehaviour
{
    private CanvasGroup canvasGroup;

    [SerializeField] private List<CanvasGroup> canvasGroupsToToggle = new List<CanvasGroup>();

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
        HideAll();
        ApplyState(canvasGroupToShow, true);
    }

    [ContextMenu("Hide")]
    public void Hide()
    {
        ResolveCanvasGroup();
        ApplyState(canvasGroup, false);
    }

    public void SetVisible(bool visible)
    {
        ResolveCanvasGroup();

        if (visible)
        {
            Show(canvasGroup);
            return;
        }

        ApplyState(canvasGroup, false);
    }

    public void HideAll()
    {
        foreach (CanvasGroup group in canvasGroupsToToggle)
        {
            ApplyState(group, false);
        }
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
}
