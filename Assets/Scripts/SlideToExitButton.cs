using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlideToExitButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Scene")]
    [SerializeField] private string targetSceneName = "MainScreen";

    [Header("Slide")]
    [SerializeField] private RectTransform slidingButton;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private float requiredSlideDistance = 420f;
    [Range(0.5f, 1f)]
    [SerializeField] private float completionPercent = 0.92f;
    [SerializeField] private float resetDuration = 0.25f;
    [Range(0.05f, 1f)]
    [SerializeField] private float idleButtonAlpha = 0.5f;
    [SerializeField] private float holdHintDelay = 0.35f;
    [SerializeField] private float dragStartThreshold = 12f;

    [Header("Hand Hint")]
    [SerializeField] private RectTransform handImage;
    [SerializeField] private bool startHandHintFromButton = true;
    [SerializeField] private Vector2 handStartOffset = Vector2.zero;
    [SerializeField] private float handSlideDistance = 260f;
    [SerializeField] private float handHintDuration = 1.1f;
    [SerializeField] private float handHintDelay = 0.15f;
    [SerializeField] private float handScale = 1.5f;

    private CanvasGroup handCanvasGroup;
    private CanvasGroup buttonCanvasGroup;
    private Coroutine resetRoutine;
    private Coroutine handHintRoutine;
    private Coroutine holdHintDelayRoutine;
    private Vector2 startPosition;
    private Vector2 handStartPosition;
    private Vector2 pointerDownPosition;
    private bool isDragging;
    private bool isLoading;
    private bool hasDragged;

    private void Awake()
    {
        ResolveReferences();
        CacheStartPositions();
        SetButtonAlpha(idleButtonAlpha);
        HideHandHint();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheStartPositions();
        ResetButtonInstant();
        SetButtonAlpha(idleButtonAlpha);
        HideHandHint();
    }

    private void OnDisable()
    {
        StopHintsAndReset();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isLoading)
        {
            return;
        }

        StopHintsAndReset();
        hasDragged = false;
        isDragging = true;
        pointerDownPosition = eventData.position;
        SetButtonAlpha(1f);
        HideHandHint();
        holdHintDelayRoutine = StartCoroutine(PlayHandHintAfterHoldDelay());
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLoading || !isDragging || slidingButton == null)
        {
            return;
        }

        float scaleFactor = parentCanvas != null ? Mathf.Max(0.01f, parentCanvas.scaleFactor) : 1f;
        float leftDragDistance = Mathf.Max(0f, (pointerDownPosition.x - eventData.position.x) / scaleFactor);

        if (leftDragDistance < dragStartThreshold)
        {
            slidingButton.anchoredPosition = startPosition;
            return;
        }

        if (!hasDragged)
        {
            hasDragged = true;
            StopHandHint();
            HideHandHint();
        }

        Vector2 position = slidingButton.anchoredPosition;
        position.x = Mathf.Clamp(startPosition.x - leftDragDistance, startPosition.x - requiredSlideDistance, startPosition.x);
        position.y = startPosition.y;
        slidingButton.anchoredPosition = position;

        float progress = Mathf.InverseLerp(startPosition.x, startPosition.x - requiredSlideDistance, position.x);
        if (progress >= completionPercent)
        {
            isDragging = false;
            CompleteSlide();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isLoading || slidingButton == null)
        {
            return;
        }

        isDragging = false;
        StopHandHint();
        HideHandHint();
        float progress = Mathf.InverseLerp(startPosition.x, startPosition.x - requiredSlideDistance, slidingButton.anchoredPosition.x);
        if (progress >= completionPercent)
        {
            CompleteSlide();
            return;
        }

        resetRoutine = StartCoroutine(ResetButtonRoutine(!hasDragged));
    }

    private void CompleteSlide()
    {
        isLoading = true;
        HideHandHint();
        SetButtonAlpha(1f);

        if (targetSceneName == "MainScreen" && AdManager.Instance != null)
        {
            AdManager.Instance.ShowInterstitialOnNextMainScreen();
        }

        SmoothSceneLoader.LoadScene(targetSceneName);
    }

    private IEnumerator ResetButtonRoutine(bool showHintAfterReset)
    {
        Vector2 from = slidingButton.anchoredPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, resetDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            slidingButton.anchoredPosition = Vector2.LerpUnclamped(from, startPosition, eased);
            yield return null;
        }

        slidingButton.anchoredPosition = startPosition;
        SetButtonAlpha(idleButtonAlpha);
        resetRoutine = null;
    }

    private IEnumerator PlayHandHintAfterHoldDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdHintDelay));

        if (isDragging && !hasDragged && !isLoading)
        {
            PlayHandHint();
        }

        holdHintDelayRoutine = null;
    }

    private void PlayHandHint()
    {
        if (handImage == null)
        {
            return;
        }

        if (handHintRoutine != null)
        {
            StopCoroutine(handHintRoutine);
        }

        handHintRoutine = StartCoroutine(HandHintRoutine());
    }

    private IEnumerator HandHintRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, handHintDelay));

        while (isDragging && !hasDragged && !isLoading)
        {
            handImage.gameObject.SetActive(true);
            Vector2 from = GetHandHintStartPosition();
            Vector2 to = from + Vector2.left * handSlideDistance;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, handHintDuration);

            while (elapsed < duration && isDragging && !hasDragged && !isLoading)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float fade = Mathf.Sin(t * Mathf.PI);

                handImage.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                handImage.localScale = Vector3.one * (handScale * Mathf.Lerp(0.92f, 1.04f, fade));

                if (handCanvasGroup != null)
                {
                    handCanvasGroup.alpha = fade;
                }

                yield return null;
            }

            handImage.anchoredPosition = GetHandHintStartPosition();
            if (handCanvasGroup != null)
            {
                handCanvasGroup.alpha = 0f;
            }

            yield return new WaitForSecondsRealtime(0.18f);
        }

        HideHandHint();
    }

    private void ResetButtonInstant()
    {
        if (slidingButton != null)
        {
            slidingButton.anchoredPosition = startPosition;
        }

        SetButtonAlpha(idleButtonAlpha);
    }

    private void HideHandHint()
    {
        if (handImage == null)
        {
            return;
        }

        handImage.anchoredPosition = GetHandHintStartPosition();
        handImage.localScale = Vector3.one;
        handImage.gameObject.SetActive(false);

        if (handCanvasGroup != null)
        {
            handCanvasGroup.alpha = 0f;
        }
    }

    private void StopHintsAndReset()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }

        StopHandHint();
    }

    private void StopHandHint()
    {
        if (holdHintDelayRoutine != null)
        {
            StopCoroutine(holdHintDelayRoutine);
            holdHintDelayRoutine = null;
        }

        if (handHintRoutine != null)
        {
            StopCoroutine(handHintRoutine);
            handHintRoutine = null;
        }
    }

    private void ResolveReferences()
    {
        if (slidingButton == null)
        {
            slidingButton = GetComponent<RectTransform>();
        }

        if (buttonCanvasGroup == null && slidingButton != null)
        {
            buttonCanvasGroup = slidingButton.GetComponent<CanvasGroup>();
            if (buttonCanvasGroup == null)
            {
                buttonCanvasGroup = slidingButton.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (handImage != null)
        {
            handCanvasGroup = handImage.GetComponent<CanvasGroup>();
            if (handCanvasGroup == null)
            {
                handCanvasGroup = handImage.gameObject.AddComponent<CanvasGroup>();
            }

            Graphic handGraphic = handImage.GetComponent<Graphic>();
            if (handGraphic != null)
            {
                handGraphic.raycastTarget = false;
            }
        }
    }

    private void CacheStartPositions()
    {
        if (slidingButton != null)
        {
            startPosition = slidingButton.anchoredPosition;
        }

        if (handImage != null)
        {
            handStartPosition = handImage.anchoredPosition;
        }
    }

    private Vector2 GetHandHintStartPosition()
    {
        if (!startHandHintFromButton || handImage == null || slidingButton == null)
        {
            return handStartPosition;
        }

        RectTransform handParent = handImage.parent as RectTransform;
        if (handParent == null)
        {
            return handStartPosition;
        }

        Vector3 buttonWorldCenter = slidingButton.TransformPoint(slidingButton.rect.center);
        Vector2 localPosition = handParent.InverseTransformPoint(buttonWorldCenter);
        return localPosition + handStartOffset;
    }

    private void OnValidate()
    {
        requiredSlideDistance = Mathf.Max(1f, requiredSlideDistance);
        handSlideDistance = Mathf.Max(1f, handSlideDistance);
        resetDuration = Mathf.Max(0.01f, resetDuration);
        holdHintDelay = Mathf.Max(0f, holdHintDelay);
        dragStartThreshold = Mathf.Max(0f, dragStartThreshold);
        handHintDuration = Mathf.Max(0.01f, handHintDuration);
        handHintDelay = Mathf.Max(0f, handHintDelay);
    }

    private void SetButtonAlpha(float alpha)
    {
        if (buttonCanvasGroup != null)
        {
            buttonCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}
