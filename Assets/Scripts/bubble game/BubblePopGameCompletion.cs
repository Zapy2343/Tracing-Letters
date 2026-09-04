using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BubblePopGameCompletion : MonoBehaviour
{
    [SerializeField] private BubblePopGameManager gameManager;
    [SerializeField] private int targetScoreToComplete = 10;
    [SerializeField] private bool completeAutomaticallyAtTargetScore = true;
    [SerializeField] private float completionPauseSeconds = 1.35f;
    [SerializeField] private bool proceedToNextLevel = true;

    public int TargetScoreToComplete => targetScoreToComplete;

    [Header("Completion Presentation")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip completionClip;
    [SerializeField] private Vector2 completedLetterSize = new Vector2(360f, 360f);
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.15f);

    private bool completed;
    private AudioClip generatedCompletionClip;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<BubblePopGameManager>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (!completeAutomaticallyAtTargetScore || completed || gameManager == null)
        {
            return;
        }

        if (gameManager.Score >= targetScoreToComplete)
        {
            CompleteLevel();
        }
    }

    [ContextMenu("Complete Level")]
    public void CompleteLevel()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        StartCoroutine(CompleteLevelRoutine());
    }

    private IEnumerator CompleteLevelRoutine()
    {
        int score = gameManager != null ? gameManager.Score : 0;
        int completedLevelIndex = gameManager != null ? gameManager.CurrentLevelIndex : BubblePopLevelMenu.GetSelectedLevelIndex();
        Sprite completedLevelSprite = gameManager != null ? gameManager.GetContentSpriteForLevelIndex(completedLevelIndex) : null;

        if (completedLevelSprite == null)
        {
#if UNITY_2023_1_OR_NEWER
            BubblePopSelectedLevelImageProvider provider = FindFirstObjectByType<BubblePopSelectedLevelImageProvider>();
#else
            BubblePopSelectedLevelImageProvider provider = FindObjectOfType<BubblePopSelectedLevelImageProvider>();
#endif
            if (provider != null && provider.LevelImages != null && provider.LevelImages.Count > 0)
            {
                int safeIndex = Mathf.Clamp(completedLevelIndex, 0, provider.LevelImages.Count - 1);
                completedLevelSprite = provider.LevelImages[safeIndex];
            }
        }

        if (gameManager != null)
        {
            gameManager.StopGame();
        }

        BubblePopLevelMenu.CompleteLevel(completedLevelIndex, score);
        PlayCompletionSfx();

        gameManager.bubblePopFXManager.PlayCompletionCelebration(Vector2.zero);


        GameObject overlay = CreateCompletionOverlay(completedLevelSprite);
        yield return new WaitForSeconds(Mathf.Max(0.1f, completionPauseSeconds));

        if (overlay != null)
        {
            Destroy(overlay);
        }

        if (gameManager != null)
        {
            gameManager.ClearActiveBubbles();
        }

        if (proceedToNextLevel && gameManager != null && gameManager.LevelCount > 0)
        {
            int nextLevelIndex = completedLevelIndex + 1;
            if (nextLevelIndex < gameManager.LevelCount)
            {
                gameManager.BeginLevel(nextLevelIndex);
                completed = false;
            }
        }
    }

    private GameObject CreateCompletionOverlay(Sprite completedLevelSprite)
    {
        if (targetCanvas == null)
        {
            return null;
        }

        GameObject overlay = new GameObject("Bubble Level Complete Overlay", typeof(RectTransform), typeof(CanvasGroup));
        overlay.transform.SetParent(targetCanvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        GameObject washObject = new GameObject("Completion Wash", typeof(RectTransform), typeof(Image));
        washObject.transform.SetParent(overlay.transform, false);
        RectTransform washRect = washObject.GetComponent<RectTransform>();
        washRect.anchorMin = Vector2.zero;
        washRect.anchorMax = Vector2.one;
        washRect.offsetMin = Vector2.zero;
        washRect.offsetMax = Vector2.zero;

        Image wash = washObject.GetComponent<Image>();
        wash.color = overlayColor;
        wash.raycastTarget = false;

        if (completedLevelSprite != null)
        {
            GameObject imageObject = new GameObject("Completed Level Image", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(overlay.transform, false);

            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = completedLetterSize;

            Image img = imageObject.GetComponent<Image>();
            img.sprite = completedLevelSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            StartCoroutine(AnimateCompletedImage(imageRect));
        }

        return overlay;
    }

    private IEnumerator AnimateCompletedImage(RectTransform imageRect)
    {
        if (imageRect == null)
        {
            yield break;
        }

        float elapsed = 0f;
        const float duration = 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float scale;
            if (t < 0.6f)
            {
                float subT = t / 0.6f;
                scale = Mathf.Lerp(0.2f, 1.35f, Mathf.Sin(subT * Mathf.PI * 0.5f));
            }
            else
            {
                float subT = (t - 0.6f) / 0.4f;
                scale = Mathf.Lerp(1.35f, 1.0f, Mathf.Sin(subT * Mathf.PI * 0.5f));
            }

            imageRect.localScale = Vector3.one * scale;
            imageRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2.5f) * 6f * (1f - t));
            yield return null;
        }

        imageRect.localScale = Vector3.one;
        imageRect.localRotation = Quaternion.identity;
    }

    private void PlayCompletionSfx()
    {
        AudioClip clip = completionClip != null ? completionClip : GetGeneratedCompletionClip();
        if (clip == null || sfxSource == null)
        {
            return;
        }

        if (GlobalSoundManager.Instance != null)
        {
            GlobalSoundManager.Instance.PlaySfx(sfxSource, clip);
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    private AudioClip GetGeneratedCompletionClip()
    {
        if (generatedCompletionClip != null)
        {
            return generatedCompletionClip;
        }

        const int sampleRate = 44100;
        const float duration = 0.72f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float[] tones = { 523.25f, 659.25f, 783.99f };

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float tone = tones[Mathf.Min(tones.Length - 1, Mathf.FloorToInt(time / (duration / tones.Length)))];
            float envelope = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
            samples[i] = Mathf.Sin(time * tone * Mathf.PI * 2f) * envelope * 0.35f;
        }

        generatedCompletionClip = AudioClip.Create("Generated Bubble Completion SFX", sampleCount, 1, sampleRate, false);
        generatedCompletionClip.SetData(samples, 0);
        return generatedCompletionClip;
    }
}
