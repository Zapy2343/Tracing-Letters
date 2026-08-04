using UnityEngine;

/// <summary>
/// Toggles a CanvasGroup between visible/interactable and hidden/non-interactable states.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CanvasGroupToggle : MonoBehaviour
{
    [Tooltip("CanvasGroup to control. Uses the CanvasGroup on this object when left empty.")]
    [SerializeField] private CanvasGroup canvasGroup;

    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0f;

    private void Awake()
    {
        ResolveCanvasGroup();
        ApplyState(IsVisible);
    }

    [ContextMenu("Toggle")]
    public void Toggle()
    {
        SetVisible(!IsVisible);
    }

    [ContextMenu("Show")]
    public void Show()
    {
        SetVisible(true);
    }

    [ContextMenu("Hide")]
    public void Hide()
    {
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        ResolveCanvasGroup();
        ApplyState(visible);
    }

    private void ApplyState(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void ResolveCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
