using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles one rising bubble, its contained image, click/tap pop input, and pop animation.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BubblePopBubble : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Outer bubble image. If empty, the first Image on this object is used.")]
    [SerializeField] private Image bubbleImage;

    [Tooltip("Image displayed inside the bubble.")]
    [SerializeField] private Image contentImage;

    [Tooltip("Button used for click/tap popping. Auto-added by the manager when needed.")]
    [SerializeField] private Button popButton;

    [Header("Pop Visuals")]
    [Tooltip("Optional sprite swapped onto the bubble during the pop animation.")]
    [SerializeField] private Sprite poppedBubbleSprite;

    [Tooltip("How long the pop animation lasts before the bubble is destroyed.")]
    [SerializeField] private float popDuration = 0.18f;

    [Tooltip("How large the bubble scales during its pop animation.")]
    [SerializeField] private float popScale = 1.25f;

    [Tooltip("Optional Animator Controller played on this bubble object when popped.")]
    [SerializeField] private RuntimeAnimatorController popAnimatorController;

    [Tooltip("Animator state to play when this bubble pops. The included controller uses Burst.")]
    [SerializeField] private string popAnimationStateName = "Burst";

    private RectTransform rectTransform;
    private RectTransform playArea;
    private CanvasGroup canvasGroup;
    private Animator popAnimator;
    private Action<BubblePopBubble> poppedCallback;
    private Action<BubblePopBubble> releasedCallback;
    private Coroutine popRoutine;
    private Color bubbleBaseColor = Color.white;
    private Color contentBaseColor = Color.white;
    private float riseSpeed;
    private float wiggleAmplitude;
    private float wiggleFrequency;
    private float despawnPadding;
    private float wiggleSeed;
    private bool popped;

    public bool IsPopped => popped;

    public void BindReferences(Image shellImage, Image insideImage, Button clickButton)
    {
        bubbleImage = shellImage;
        contentImage = insideImage;
        popButton = clickButton;
    }

    public void SetContentFill(float fillPercent)
    {
        if (contentImage == null) return;

        RectTransform contentRect = contentImage.GetComponent<RectTransform>();
        if (contentRect == null) return;

        float inset = (1f - Mathf.Clamp01(fillPercent)) * 0.5f;
        contentRect.anchorMin = new Vector2(inset, inset);
        contentRect.anchorMax = new Vector2(1f - inset, 1f - inset);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (bubbleImage == null)
        {
            bubbleImage = GetComponent<Image>();
        }

        if (popButton == null)
        {
            popButton = GetComponent<Button>();
        }

        popAnimator = GetComponent<Animator>();

        if (bubbleImage != null)
        {
            bubbleBaseColor = bubbleImage.color;
        }

        if (contentImage != null)
        {
            contentBaseColor = contentImage.color;
        }
    }

    private void OnEnable()
    {
        if (popButton != null)
        {
            popButton.onClick.AddListener(Pop);
        }
    }

    private void OnDisable()
    {
        if (popButton != null)
        {
            popButton.onClick.RemoveListener(Pop);
        }
    }

    private void Update()
    {
        if (popped || playArea == null) return;

        Vector2 position = rectTransform.anchoredPosition;
        position.y += riseSpeed * Time.deltaTime;
        position.x += Mathf.Sin((Time.time + wiggleSeed) * wiggleFrequency) * wiggleAmplitude * Time.deltaTime;
        rectTransform.anchoredPosition = position;

        float topLimit = playArea.rect.yMax + rectTransform.rect.height + despawnPadding;
        if (position.y > topLimit)
        {
            Release();
        }
    }

    public void Configure(
        RectTransform targetPlayArea,
        Sprite bubbleSprite,
        Sprite contentSprite,
        Sprite popSprite,
        RuntimeAnimatorController animatorController,
        float speed,
        float amplitude,
        float frequency,
        float padding,
        Action<BubblePopBubble> onPopped,
        Action<BubblePopBubble> onReleased)
    {
        if (popRoutine != null)
        {
            StopCoroutine(popRoutine);
            popRoutine = null;
        }

        playArea = targetPlayArea;
        poppedBubbleSprite = popSprite;
        if (animatorController != null)
        {
            popAnimatorController = animatorController;
        }

        if (popAnimator == null)
        {
            popAnimator = GetComponent<Animator>();
        }

        if (popAnimator != null && popAnimator.runtimeAnimatorController == null && popAnimatorController != null)
        {
            popAnimator.runtimeAnimatorController = popAnimatorController;
        }

        riseSpeed = speed;
        wiggleAmplitude = amplitude;
        wiggleFrequency = frequency;
        despawnPadding = padding;
        poppedCallback = onPopped;
        releasedCallback = onReleased;
        wiggleSeed = UnityEngine.Random.Range(0f, 100f);
        popped = false;

        if (bubbleImage != null)
        {
            bubbleImage.sprite = bubbleSprite;
            bubbleImage.raycastTarget = true;
            bubbleImage.color = bubbleBaseColor;
        }

        if (contentImage != null)
        {
            contentImage.sprite = contentSprite;
            contentImage.enabled = contentSprite != null;
            contentImage.raycastTarget = false;
            contentImage.preserveAspect = true;
            contentImage.color = contentBaseColor;
        }

        if (popButton != null)
        {
            popButton.interactable = true;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        if (popAnimator != null)
        {
            popAnimator.enabled = false;
        }

        rectTransform.localScale = Vector3.one;
    }

    public void Pop()
    {
        if (popped) return;

        popped = true;

        if (popButton != null)
        {
            popButton.interactable = false;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        poppedCallback?.Invoke(this);
        popRoutine = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        Vector3 startScale = rectTransform.localScale;
        Color startBubbleColor = bubbleImage != null ? bubbleImage.color : Color.white;
        Color startContentColor = contentImage != null ? contentImage.color : Color.white;
        bool hasAnimatorPop = TryPlayPopAnimator();

        if (contentImage != null && hasAnimatorPop)
        {
            contentImage.enabled = false;
        }

        if (bubbleImage != null && poppedBubbleSprite != null && !hasAnimatorPop)
        {
            bubbleImage.sprite = poppedBubbleSprite;
        }

        float duration = Mathf.Max(popDuration, GetPopAnimationDuration());
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            if (!hasAnimatorPop)
            {
                rectTransform.localScale = Vector3.Lerp(startScale, startScale * popScale, eased);

                if (bubbleImage != null)
                {
                    Color color = startBubbleColor;
                    color.a = Mathf.Lerp(startBubbleColor.a, 0f, t);
                    bubbleImage.color = color;
                }

                if (contentImage != null)
                {
                    Color color = startContentColor;
                    color.a = Mathf.Lerp(startContentColor.a, 0f, t);
                    contentImage.color = color;
                }
            }

            yield return null;
        }

        popRoutine = null;
        Release();
    }

    private bool TryPlayPopAnimator()
    {
        if (popAnimator == null)
        {
            popAnimator = GetComponent<Animator>();
        }

        if (popAnimator == null)
        {
            return false;
        }

        if (popAnimator.runtimeAnimatorController == null)
        {
            popAnimator.runtimeAnimatorController = popAnimatorController;
        }

        if (popAnimator.runtimeAnimatorController == null)
        {
            return false;
        }

        popAnimator.enabled = true;
        popAnimator.Rebind();
        popAnimator.Update(0f);

        if (!string.IsNullOrWhiteSpace(popAnimationStateName))
        {
            popAnimator.Play(popAnimationStateName, 0, 0f);
        }
        else
        {
            popAnimator.Play(0, 0, 0f);
        }

        return true;
    }

    private float GetPopAnimationDuration()
    {
        RuntimeAnimatorController controller = popAnimator != null && popAnimator.runtimeAnimatorController != null
            ? popAnimator.runtimeAnimatorController
            : popAnimatorController;

        if (controller == null || controller.animationClips == null)
        {
            return 0f;
        }

        float duration = 0f;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.length);
            }
        }

        return duration;
    }

    private void Release()
    {
        releasedCallback?.Invoke(this);
    }
}
