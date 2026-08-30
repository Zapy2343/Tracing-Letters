using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles one rising bubble, its contained letter, click/tap pop input, and pop animation.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BubblePopBubble : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Outer bubble image. If empty, the first Image on this object is used.")]
    [SerializeField] private Image bubbleImage;

    [Tooltip("Image displayed inside the bubble.")]
    [SerializeField] private Image contentImage;

    [Tooltip("Text displayed inside the bubble.")]
    [SerializeField] private TMP_Text contentText;

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
    private Func<BubblePopBubble, bool> canPopCallback;
    private Action<BubblePopBubble> rejectedPopCallback;
    private Coroutine popRoutine;
    private Coroutine rejectedTapRoutine;
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

    public void BindReferences(Image shellImage, Image insideImage, TMP_Text letterText, Button clickButton)
    {
        bubbleImage = shellImage;
        contentImage = insideImage;
        contentText = letterText;
        popButton = clickButton;
    }

    public void SetContentFill(float fillPercent)
    {
        ResolveContentReferences();

        float inset = (1f - Mathf.Clamp01(fillPercent)) * 0.5f;
        Vector2 min = new Vector2(inset, inset);
        Vector2 max = new Vector2(1f - inset, 1f - inset);

        if (contentImage != null)
        {
            RectTransform imgRect = contentImage.GetComponent<RectTransform>();
            if (imgRect != null)
            {
                imgRect.anchorMin = min;
                imgRect.anchorMax = max;
                imgRect.offsetMin = Vector2.zero;
                imgRect.offsetMax = Vector2.zero;
                imgRect.anchoredPosition = Vector2.zero;
            }
        }

        if (contentText != null)
        {
            RectTransform txtRect = contentText.GetComponent<RectTransform>();
            if (txtRect != null)
            {
                txtRect.anchorMin = min;
                txtRect.anchorMax = max;
                txtRect.offsetMin = Vector2.zero;
                txtRect.offsetMax = Vector2.zero;
                txtRect.anchoredPosition = Vector2.zero;
            }
        }
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        ResolveContentReferences();

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
        Action<BubblePopBubble> onReleased,
        Func<BubblePopBubble, bool> canPop = null,
        Action<BubblePopBubble> onRejectedPop = null)
    {
        Configure(
            targetPlayArea,
            bubbleSprite,
            string.Empty,
            popSprite,
            animatorController,
            speed,
            amplitude,
            frequency,
            padding,
            onPopped,
            onReleased,
            canPop,
            onRejectedPop);

        if (contentImage != null)
        {
            contentImage.sprite = contentSprite;
            contentImage.gameObject.SetActive(contentSprite != null);
            contentImage.enabled = contentSprite != null;
            contentImage.raycastTarget = false;
            contentImage.preserveAspect = true;
            contentImage.color = contentBaseColor.a > 0f ? contentBaseColor : Color.white;
            contentImage.transform.SetAsLastSibling();
        }

        if (contentText != null)
        {
            contentText.text = string.Empty;
            contentText.gameObject.SetActive(false);
            contentText.enabled = false;
        }
    }

    public void Configure(
        RectTransform targetPlayArea,
        Sprite bubbleSprite,
        string contentLetter,
        Sprite popSprite,
        RuntimeAnimatorController animatorController,
        float speed,
        float amplitude,
        float frequency,
        float padding,
        Action<BubblePopBubble> onPopped,
        Action<BubblePopBubble> onReleased,
        Func<BubblePopBubble, bool> canPop = null,
        Action<BubblePopBubble> onRejectedPop = null)
    {
        ResolveContentReferences();

        if (popRoutine != null)
        {
            StopCoroutine(popRoutine);
            popRoutine = null;
        }

        if (rejectedTapRoutine != null)
        {
            StopCoroutine(rejectedTapRoutine);
            rejectedTapRoutine = null;
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
        canPopCallback = canPop;
        rejectedPopCallback = onRejectedPop;
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
            contentImage.sprite = null;
            contentImage.gameObject.SetActive(false);
            contentImage.enabled = false;
            contentImage.raycastTarget = false;
            contentImage.preserveAspect = true;
            contentImage.color = contentBaseColor.a > 0f ? contentBaseColor : Color.white;
        }

        if (contentText != null)
        {
            contentText.text = contentLetter;
            contentText.gameObject.SetActive(!string.IsNullOrWhiteSpace(contentLetter));
            contentText.enabled = !string.IsNullOrWhiteSpace(contentLetter);
            contentText.raycastTarget = false;
            contentText.alignment = TextAlignmentOptions.Center;
            contentText.enableAutoSizing = true;
            contentText.fontSizeMin = 34f;
            contentText.fontSizeMax = 94f;
            contentText.color = new Color(0.23f, 0.11f, 0.42f, 1f);
            contentText.transform.SetAsLastSibling();
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
        rectTransform.localRotation = Quaternion.identity;
    }

    private void ResolveContentReferences()
    {
        if (bubbleImage == null)
        {
            bubbleImage = GetComponent<Image>();
        }

        if (contentText == null)
        {
            contentText = GetComponentInChildren<TMP_Text>(true);
        }

        if (contentImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i] != bubbleImage)
                {
                    contentImage = images[i];
                    break;
                }
            }
        }

        if (contentImage == null)
        {
            GameObject contentObject = new GameObject("Bubble Image", typeof(RectTransform), typeof(Image));
            contentObject.transform.SetParent(transform, false);

            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.08f, 0.08f);
            contentRect.anchorMax = new Vector2(0.92f, 0.92f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;

            contentImage = contentObject.GetComponent<Image>();
            contentImage.preserveAspect = true;
            contentImage.raycastTarget = false;
            contentImage.color = Color.white;
        }

        if (contentText == null)
        {
            GameObject textObject = new GameObject("Bubble Letter", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.08f, 0.08f);
            textRect.anchorMax = new Vector2(0.92f, 0.92f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            contentText = textObject.GetComponent<TextMeshProUGUI>();
            contentText.alignment = TextAlignmentOptions.Center;
            contentText.enableAutoSizing = true;
            contentText.fontSizeMin = 34f;
            contentText.fontSizeMax = 94f;
            contentText.color = new Color(0.23f, 0.11f, 0.42f, 1f);
            contentText.raycastTarget = false;
        }
    }

    public void Pop()
    {
        if (popped) return;

        if (canPopCallback != null && !canPopCallback(this))
        {
            rejectedPopCallback?.Invoke(this);
            return;
        }

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

    public void PlayRejectedTapFeedback()
    {
        if (popped || rectTransform == null)
        {
            return;
        }

        if (rejectedTapRoutine != null)
        {
            StopCoroutine(rejectedTapRoutine);
        }

        rejectedTapRoutine = StartCoroutine(RejectedTapRoutine());
    }

    private IEnumerator PopRoutine()
    {
        Vector3 startScale = rectTransform.localScale;
        Color startBubbleColor = bubbleImage != null ? bubbleImage.color : Color.white;
        Color startContentColor = contentImage != null ? contentImage.color : Color.white;
        Color startTextColor = contentText != null ? contentText.color : Color.white;
        bool hasAnimatorPop = TryPlayPopAnimator();

        if (contentImage != null && hasAnimatorPop)
        {
            contentImage.enabled = false;
        }

        if (contentText != null && hasAnimatorPop)
        {
            contentText.enabled = false;
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

                if (contentText != null)
                {
                    Color color = startTextColor;
                    color.a = Mathf.Lerp(startTextColor.a, 0f, t);
                    contentText.color = color;
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
            popAnimator = gameObject.AddComponent<Animator>();
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

    private IEnumerator RejectedTapRoutine()
    {
        Vector3 startScale = rectTransform.localScale;
        Quaternion startRotation = rectTransform.localRotation;
        float elapsed = 0f;
        const float duration = 0.22f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float wobble = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t);
            rectTransform.localScale = startScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f);
            rectTransform.localRotation = startRotation * Quaternion.Euler(0f, 0f, wobble * 10f);
            yield return null;
        }

        rectTransform.localScale = startScale;
        rectTransform.localRotation = startRotation;
        rejectedTapRoutine = null;
    }
}
