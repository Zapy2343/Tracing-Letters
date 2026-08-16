using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class KaKhaGameMenuController : MonoBehaviour
{
    public const string SelectedTracingLetterNumberKey = KaKhaTracingProgress.SelectedTracingLetterNumberKey;

    [Header("Scene")]
    [SerializeField] private string tracingSceneName = "Tracing Letter";

    [Header("UI")]
    [SerializeField] private GameObject wordButtonObject;
    [SerializeField] private RectTransform wordRectTransform;
    [SerializeField] private Image wordImage;
    [SerializeField] private GameObject previousButtonObject;
    [SerializeField] private GameObject nextButtonObject;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Letter Images")]
    [SerializeField] private List<Sprite> dottedLetterImages = new List<Sprite>();
    [SerializeField] private int maxLettersToLoadFromResources = 36;
    [SerializeField] private string resourcesDottedLettersFolder = "Dotted Letters";
#if UNITY_EDITOR
    [SerializeField] private string editorDottedLettersFolder = "Assets/Sprites/Dotted Letters";
#endif

    [Header("Animation")]
    [SerializeField] private float slideDistance = 220f;
    [SerializeField] private float slideDuration = 0.22f;
    [SerializeField] private float lockedShakeDistance = 24f;
    [SerializeField] private float lockedShakeDuration = 0.25f;

    [Header("Lock Visuals")]
    [SerializeField] private Color unlockedImageColor = Color.white;
    [SerializeField] private Color lockedImageColor = new Color(1f, 1f, 1f, 0.38f);

    [Header("Current Status")]
    [SerializeField] private int currentLetterNumber = 1;

    private Button wordButton;
    private Button previousButton;
    private Button nextButton;
    private CanvasGroup wordCanvasGroup;
    private Coroutine slideRoutine;
    private Coroutine lockedFeedbackRoutine;
    private Vector2 restingPosition;
    private bool hasRestingPosition;

    private void Awake()
    {
        ResolveReferences();
        LoadSpritesIfEmpty();
        PlayProgressTracker.RegisterTracingTotalItems(GetTotalLetters());
        ConfigureButtons();
        ApplyCurrentLetter();
    }

    private void OnEnable()
    {
        if (wordRectTransform != null)
        {
            CaptureRestingPosition();
            ResetWordTransform();
        }

        RefreshProgressView();
    }

    private void OnDisable()
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        if (wordRectTransform != null)
        {
            ResetWordTransform();
        }

        if (wordCanvasGroup != null)
        {
            wordCanvasGroup.alpha = 1f;
        }

        if (wordImage != null)
        {
            wordImage.color = IsCurrentLetterUnlocked() ? unlockedImageColor : lockedImageColor;
        }
    }

    private void ResolveReferences()
    {
        if (wordImage == null && wordRectTransform != null)
        {
            wordImage = wordRectTransform.GetComponent<Image>();
        }

        if (wordRectTransform == null && wordImage != null)
        {
            wordRectTransform = wordImage.rectTransform;
        }

        if (wordCanvasGroup == null && wordRectTransform != null)
        {
            wordCanvasGroup = wordRectTransform.GetComponent<CanvasGroup>();
            if (wordCanvasGroup == null)
            {
                wordCanvasGroup = wordRectTransform.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (wordRectTransform != null)
        {
            CaptureRestingPosition();
        }
    }

    private void ConfigureButtons()
    {
        wordButton = EnsureButton(wordButtonObject);
        previousButton = EnsureButton(previousButtonObject);
        nextButton = EnsureButton(nextButtonObject);

        if (wordButton != null)
        {
            wordButton.interactable = true;
            wordButton.onClick.RemoveListener(OpenSelectedLetter);
            wordButton.onClick.AddListener(OpenSelectedLetter);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(ShowPreviousLetter);
            previousButton.onClick.AddListener(ShowPreviousLetter);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(ShowNextLetter);
            nextButton.onClick.AddListener(ShowNextLetter);
        }
    }

    private Button EnsureButton(GameObject buttonObject)
    {
        if (buttonObject == null)
        {
            return null;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
        }

        if (button.targetGraphic == null)
        {
            button.targetGraphic = buttonObject.GetComponent<Graphic>();
        }

        return button;
    }

    private void LoadSpritesIfEmpty()
    {
#if UNITY_EDITOR
        if (dottedLetterImages == null || dottedLetterImages.Count == 0)
        {
            dottedLetterImages = LoadSpritesFromEditorFolder(editorDottedLettersFolder);
        }
#endif

        if (dottedLetterImages == null || dottedLetterImages.Count == 0)
        {
            dottedLetterImages = LoadSpritesFromResources(resourcesDottedLettersFolder);
        }
    }

#if UNITY_EDITOR
    private List<Sprite> LoadSpritesFromEditorFolder(string folderPath)
    {
        List<Sprite> loadedSprites = new List<Sprite>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        List<string> spritePaths = new List<string>();

        foreach (string guid in guids)
        {
            spritePaths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        }

        spritePaths.Sort((left, right) =>
        {
            int leftNumber = ExtractNumberFromName(System.IO.Path.GetFileNameWithoutExtension(left));
            int rightNumber = ExtractNumberFromName(System.IO.Path.GetFileNameWithoutExtension(right));
            return leftNumber.CompareTo(rightNumber);
        });

        foreach (string spritePath in spritePaths)
        {
            Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null)
            {
                loadedSprites.Add(sprite);
            }
        }

        return loadedSprites;
    }
#endif

    private List<Sprite> LoadSpritesFromResources(string folderName)
    {
        List<Sprite> loadedSprites = new List<Sprite>();

        for (int i = 1; i <= maxLettersToLoadFromResources; i++)
        {
            Sprite sprite = Resources.Load<Sprite>($"{folderName}/{i}");
            if (sprite == null)
            {
                break;
            }

            loadedSprites.Add(sprite);
        }

        return loadedSprites;
    }

    private int ExtractNumberFromName(string name)
    {
        int value = 0;
        int.TryParse(name, out value);
        return value;
    }

    public void ShowNextLetter()
    {
        int totalLetters = GetTotalLetters();
        if (totalLetters == 0)
        {
            return;
        }

        int nextLetterNumber = currentLetterNumber + 1;
        if (nextLetterNumber > totalLetters)
        {
            nextLetterNumber = 1;
        }

        SlideToLetter(nextLetterNumber, 1);
    }

    public void ShowPreviousLetter()
    {
        int totalLetters = GetTotalLetters();
        if (totalLetters == 0)
        {
            return;
        }

        int previousLetterNumber = currentLetterNumber - 1;
        if (previousLetterNumber < 1)
        {
            previousLetterNumber = totalLetters;
        }

        SlideToLetter(previousLetterNumber, -1);
    }

    private void SlideToLetter(int letterNumber, int direction)
    {
        if (slideRoutine != null)
        {
            return;
        }

        StopLockedFeedback();
        slideRoutine = StartCoroutine(SlideToLetterRoutine(letterNumber, direction));
    }

    private IEnumerator SlideToLetterRoutine(int letterNumber, int direction)
    {
        if (wordRectTransform == null)
        {
            currentLetterNumber = letterNumber;
            ApplyCurrentLetter();
            yield break;
        }

        CaptureRestingPosition();
        ResetWordTransform();
        Vector2 exitPosition = restingPosition + Vector2.left * direction * slideDistance;
        Vector2 enterPosition = restingPosition - Vector2.left * direction * slideDistance;

        yield return AnimateWord(restingPosition, exitPosition, 1f, 0f);

        currentLetterNumber = letterNumber;
        ApplyCurrentLetter();
        wordRectTransform.anchoredPosition = enterPosition;

        yield return AnimateWord(enterPosition, restingPosition, 0f, 1f);
        slideRoutine = null;
    }

    private void StopLockedFeedback()
    {
        if (lockedFeedbackRoutine != null)
        {
            StopCoroutine(lockedFeedbackRoutine);
            lockedFeedbackRoutine = null;
        }

        if (wordImage != null)
        {
            wordImage.color = IsCurrentLetterUnlocked() ? unlockedImageColor : lockedImageColor;
        }
    }

    private void CaptureRestingPosition()
    {
        if (hasRestingPosition || wordRectTransform == null)
        {
            return;
        }

        restingPosition = wordRectTransform.anchoredPosition;
        hasRestingPosition = true;
    }

    private void ResetWordTransform()
    {
        if (wordRectTransform != null && hasRestingPosition)
        {
            wordRectTransform.anchoredPosition = restingPosition;
        }

        if (wordCanvasGroup != null)
        {
            wordCanvasGroup.alpha = 1f;
        }
    }

    private IEnumerator AnimateWord(Vector2 fromPosition, Vector2 toPosition, float fromAlpha, float toAlpha)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            wordRectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, eased);
            if (wordCanvasGroup != null)
            {
                wordCanvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);
            }

            yield return null;
        }

        wordRectTransform.anchoredPosition = toPosition;
        if (wordCanvasGroup != null)
        {
            wordCanvasGroup.alpha = toAlpha;
        }
    }

    private void ApplyCurrentLetter()
    {
        int totalLetters = GetTotalLetters();
        if (totalLetters > 0)
        {
            currentLetterNumber = Mathf.Clamp(currentLetterNumber, 1, totalLetters);

            if (wordImage != null)
            {
                wordImage.sprite = dottedLetterImages[currentLetterNumber - 1];
                wordImage.preserveAspect = true;
                wordImage.color = IsCurrentLetterUnlocked() ? unlockedImageColor : lockedImageColor;
            }
        }

        if (counterText != null)
        {
            counterText.text = $"{Mathf.Max(1, currentLetterNumber)} / {Mathf.Max(1, totalLetters)}";
        }

        RefreshProgressView();
    }

    private int GetTotalLetters()
    {
        return dottedLetterImages != null ? dottedLetterImages.Count : 0;
    }

    private void RefreshProgressView()
    {
        if (scoreText != null)
        {
            scoreText.text = KaKhaTracingProgress.GetTotalScore().ToString();
        }
    }

    private bool IsCurrentLetterUnlocked()
    {
        return currentLetterNumber <= KaKhaTracingProgress.GetHighestUnlockedLetterNumber(GetTotalLetters());
    }

    private void OpenSelectedLetter()
    {
        if (slideRoutine != null)
        {
            return;
        }

        if (!IsCurrentLetterUnlocked())
        {
            PlayLockedFeedback();
            return;
        }

        PlayerPrefs.SetInt(KaKhaTracingProgress.SelectedTracingLetterNumberKey, currentLetterNumber);
        PlayerPrefs.Save();
        SmoothSceneLoader.LoadScene(tracingSceneName);
    }

    private void PlayLockedFeedback()
    {
        if (slideRoutine != null)
        {
            return;
        }

        if (lockedFeedbackRoutine != null)
        {
            StopCoroutine(lockedFeedbackRoutine);
        }

        lockedFeedbackRoutine = StartCoroutine(PlayLockedFeedbackRoutine());
    }

    private IEnumerator PlayLockedFeedbackRoutine()
    {
        if (wordRectTransform == null)
        {
            yield break;
        }

        Vector2 startPosition = restingPosition;
        Color startColor = wordImage != null ? wordImage.color : Color.white;
        Color flashColor = new Color(1f, 0.58f, 0.58f, startColor.a);
        float elapsed = 0f;

        while (elapsed < lockedShakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lockedShakeDuration));
            float shake = Mathf.Sin(t * Mathf.PI * 6f) * lockedShakeDistance * (1f - t);
            wordRectTransform.anchoredPosition = startPosition + new Vector2(shake, 0f);

            if (wordImage != null)
            {
                wordImage.color = Color.Lerp(flashColor, lockedImageColor, t);
            }

            yield return null;
        }

        wordRectTransform.anchoredPosition = startPosition;
        if (wordImage != null)
        {
            wordImage.color = IsCurrentLetterUnlocked() ? unlockedImageColor : lockedImageColor;
        }

        lockedFeedbackRoutine = null;
    }

    [ContextMenu("Reset Ka Kha Progress")]
    public void ResetProgress()
    {
        KaKhaTracingProgress.ResetProgress(GetTotalLetters());
        currentLetterNumber = 1;
        ApplyCurrentLetter();
    }
}
