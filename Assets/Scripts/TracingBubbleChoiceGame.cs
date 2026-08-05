using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Shows a three-choice rising bubble quiz for the active tracing letter.
/// One bubble is correct; two are distractors. Wrong bubbles play feedback but do not pop.
/// </summary>
public class TracingBubbleChoiceGame : MonoBehaviour
{
    [Header("Tracing References")]
    [SerializeField] private PenDrawer penDrawer;
    [SerializeField] private LetterSwitcher letterSwitcher;
    [SerializeField] private TracingSoundManager soundManager;

    [Tooltip("Show the quiz when the entire current letter is completed.")]
    [SerializeField] private bool showOnLetterCompleted = true;

    [Header("Bubble UI")]
    [Tooltip("Canvas that contains the bubble choices. Auto-detected if empty.")]
    [SerializeField] private Canvas targetCanvas;

    [Tooltip("RectTransform where choice bubbles are placed. Uses the canvas rect if empty.")]
    [SerializeField] private RectTransform choiceArea;

    [Tooltip("Optional prefab with BubblePopBubble, Image, Button, and child content Image. If empty, one is built at runtime.")]
    [SerializeField] private BubblePopBubble bubblePrefab;

    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private Sprite poppedBubbleSprite;
    [SerializeField] private RuntimeAnimatorController popAnimatorController;

    [Range(0.1f, 0.95f)]
    [SerializeField] private float contentFillPercent = 0.84f;

    [Tooltip("How many bubble choices rise at once. One is correct; the rest are incorrect.")]
    [Range(3, 8)]
    [SerializeField] private int bubblesToRise = 3;

    [Tooltip("Random bubble size range in canvas units.")]
    [SerializeField] private Vector2 bubbleSizeRange = new Vector2(200f, 250f);

    [Tooltip("Extra distance below the canvas bottom where bubbles enter.")]
    [SerializeField] private float spawnBottomOffset = 40f;

    [Tooltip("Random extra distance below the canvas bottom. This staggers entry timing so bubbles do not all rise together.")]
    [SerializeField] private Vector2 spawnStaggerOffsetRange = new Vector2(0f, 180f);

    [Tooltip("Keeps random bubble X positions away from the left/right canvas edges.")]
    [SerializeField] private float horizontalSpawnPadding = 120f;

    [Tooltip("Minimum horizontal spacing between the 3 bubbles when they spawn together.")]
    [SerializeField] private float minimumSpawnSpacing = 180f;

    [Tooltip("How many random X positions to try before accepting the best fallback.")]
    [SerializeField] private int spawnPositionAttempts = 16;

    [Header("Bubble Motion")]
    [Tooltip("Random upward speed for quiz bubbles. Different speeds make each bubble reach the top at a different time.")]
    [SerializeField] private Vector2 riseSpeedRange = new Vector2(95f, 155f);

    [Tooltip("Side-to-side drift strength for quiz bubbles.")]
    [SerializeField] private float wiggleAmplitude = 24f;

    [Tooltip("Side-to-side drift speed for quiz bubbles.")]
    [SerializeField] private float wiggleFrequency = 1.8f;

    [Tooltip("Extra distance past the top before a bubble respawns.")]
    [SerializeField] private float despawnPadding = 80f;

    [Header("Designer Image Pool")]
    [Tooltip("All possible bubble images. The correct image comes from the active LetterSequence. Two incorrect bubbles are picked from this list by excluding the correct sprite.")]
    [SerializeField] private List<Sprite> bubbleOptionImages = new List<Sprite>();

    [Header("Score & Progress")]
    [SerializeField] private int scorePerCorrectBubble = 10;
    [SerializeField] private bool advanceToNextLetterAfterCorrect = true;
    [SerializeField] private float nextLetterDelay = 0.65f;

    [Header("Events")]
    [SerializeField] private UnityEvent onCorrectBubbleSelected;
    [SerializeField] private UnityEvent onWrongBubbleSelected;

    private readonly List<BubblePopBubble> activeChoices = new List<BubblePopBubble>();
    private readonly Dictionary<BubblePopBubble, BubbleChoiceData> bubbleDataLookup = new Dictionary<BubblePopBubble, BubbleChoiceData>();
    private Sprite generatedBubbleSprite;
    private Sprite activeCorrectSprite;
    private bool quizActive;
    private bool quizSolved;

    private struct BubbleChoiceData
    {
        public bool IsCorrect;
        public Sprite Sprite;
        public Vector2 SpawnPosition;
        public Vector2 Size;
    }

    private void Awake()
    {
        ResolveReferences();
        LoadPopAnimatorIfEmpty();
    }

    private void OnEnable()
    {
        if (penDrawer == null)
        {
#if UNITY_2023_1_OR_NEWER
            penDrawer = FindFirstObjectByType<PenDrawer>();
#else
            penDrawer = FindObjectOfType<PenDrawer>();
#endif
        }

        if (soundManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            soundManager = FindFirstObjectByType<TracingSoundManager>();
#else
            soundManager = FindObjectOfType<TracingSoundManager>();
#endif
        }

        if (letterSwitcher == null)
        {
#if UNITY_2023_1_OR_NEWER
            letterSwitcher = FindFirstObjectByType<LetterSwitcher>();
#else
            letterSwitcher = FindObjectOfType<LetterSwitcher>();
#endif
        }

        if (penDrawer != null && showOnLetterCompleted)
        {
            penDrawer.OnMaskCompleted.AddListener(ShowForCurrentTracingTarget);
        }
    }

    private void OnDisable()
    {
        if (penDrawer != null)
        {
            penDrawer.OnMaskCompleted.RemoveListener(ShowForCurrentTracingTarget);
        }

        ClearChoices();
    }

    [ContextMenu("Show Bubble Choices")]
    public void ShowForCurrentTracingTarget()
    {
        Sprite correctSprite = GetCorrectBubbleSprite();
        if (correctSprite == null)
        {
            Debug.LogWarning("[TracingBubbleChoiceGame] No letter-level Bubble Correct Image assigned on the active LetterSequence.");
            return;
        }

        ShowChoices(correctSprite);
    }

    public void ShowChoices(Sprite correctSprite)
    {
        ResolveReferences();
        if (choiceArea == null || correctSprite == null)
        {
            return;
        }

        ClearChoices();

        int targetBubbleCount = Mathf.Clamp(bubblesToRise, 3, 8);
        int wrongBubbleCount = targetBubbleCount - 1;
        List<Sprite> wrongSprites = BuildWrongSpriteList(correctSprite, wrongBubbleCount);
        if (wrongSprites.Count < wrongBubbleCount)
        {
            Debug.LogWarning("[TracingBubbleChoiceGame] Add at least 1 incorrect sprite to Bubble Option Images. The correct sprite is excluded automatically.");
        }

        activeCorrectSprite = correctSprite;
        quizActive = true;
        quizSolved = false;

        List<BubbleChoiceData> choices = new List<BubbleChoiceData>
        {
            new BubbleChoiceData { IsCorrect = true, Sprite = correctSprite }
        };

        for (int i = 0; i < wrongSprites.Count && choices.Count < targetBubbleCount; i++)
        {
            choices.Add(new BubbleChoiceData { IsCorrect = false, Sprite = wrongSprites[i] });
        }

        Shuffle(choices);

        for (int i = 0; i < choices.Count; i++)
        {
            BubbleChoiceData data = choices[i];
            data.Size = GetRandomBubbleSize();
            data.SpawnPosition = GetRandomSpawnPosition(GetActiveSpawnPositions(), data.Size);
            SpawnChoiceBubble(data);
        }
    }

    public void ClearChoices()
    {
        quizActive = false;
        bubbleDataLookup.Clear();

        for (int i = activeChoices.Count - 1; i >= 0; i--)
        {
            if (activeChoices[i] != null)
            {
                Destroy(activeChoices[i].gameObject);
            }
        }

        activeChoices.Clear();
    }

    private Sprite GetCorrectBubbleSprite()
    {
        if (penDrawer == null)
        {
            return null;
        }

        LetterSequence letter = penDrawer.CurrentLetterSequence;
        return letter != null ? letter.BubbleCorrectImage : null;
    }

    private List<Sprite> BuildWrongSpriteList(Sprite correctSprite, int count)
    {
        List<Sprite> availableWrongSprites = new List<Sprite>();

        for (int i = 0; i < bubbleOptionImages.Count; i++)
        {
            Sprite sprite = bubbleOptionImages[i];
            if (sprite != null && sprite != correctSprite && !availableWrongSprites.Contains(sprite))
            {
                availableWrongSprites.Add(sprite);
            }
        }

        List<Sprite> wrongSprites = new List<Sprite>();
        if (availableWrongSprites.Count == 0)
        {
            return wrongSprites;
        }

        Shuffle(availableWrongSprites);
        while (wrongSprites.Count < count)
        {
            for (int i = 0; i < availableWrongSprites.Count && wrongSprites.Count < count; i++)
            {
                wrongSprites.Add(availableWrongSprites[i]);
            }

            Shuffle(availableWrongSprites);
        }

        return wrongSprites;
    }

    private Vector2 GetRandomSpawnPosition(List<Vector2> existingPositions, Vector2 bubbleSize)
    {
        Rect rect = choiceArea != null ? choiceArea.rect : Rect.zero;
        float halfWidth = bubbleSize.x * 0.5f;
        float minX = rect.xMin + horizontalSpawnPadding + halfWidth;
        float maxX = rect.xMax - horizontalSpawnPadding - halfWidth;
        float y = GetCanvasBottomSpawnY(rect, bubbleSize);

        if (minX >= maxX)
        {
            return new Vector2(rect.center.x, y);
        }

        Vector2 bestPosition = new Vector2(Random.Range(minX, maxX), y);
        float bestDistance = GetClosestHorizontalDistance(bestPosition.x, existingPositions);

        for (int i = 0; i < spawnPositionAttempts; i++)
        {
            Vector2 candidate = new Vector2(Random.Range(minX, maxX), y);
            float distance = GetClosestHorizontalDistance(candidate.x, existingPositions);

            if (distance >= minimumSpawnSpacing)
            {
                return candidate;
            }

            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestPosition = candidate;
            }
        }

        return bestPosition;
    }

    private float GetCanvasBottomSpawnY(Rect rect, Vector2 bubbleSize)
    {
        float halfHeight = bubbleSize.y * 0.5f;
        float stagger = Random.Range(
            Mathf.Min(spawnStaggerOffsetRange.x, spawnStaggerOffsetRange.y),
            Mathf.Max(spawnStaggerOffsetRange.x, spawnStaggerOffsetRange.y));

        return rect.yMin - halfHeight - spawnBottomOffset - stagger;
    }

    private List<Vector2> GetActiveSpawnPositions()
    {
        List<Vector2> positions = new List<Vector2>();
        for (int i = 0; i < activeChoices.Count; i++)
        {
            if (activeChoices[i] == null)
            {
                continue;
            }

            RectTransform rectTransform = activeChoices[i].GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                positions.Add(rectTransform.anchoredPosition);
            }
        }

        return positions;
    }

    private float GetClosestHorizontalDistance(float candidateX, List<Vector2> positions)
    {
        if (positions == null || positions.Count == 0)
        {
            return float.MaxValue;
        }

        float closestDistance = float.MaxValue;
        for (int i = 0; i < positions.Count; i++)
        {
            closestDistance = Mathf.Min(closestDistance, Mathf.Abs(candidateX - positions[i].x));
        }

        return closestDistance;
    }

    private Vector2 GetRandomBubbleSize()
    {
        float size = Random.Range(
            Mathf.Min(bubbleSizeRange.x, bubbleSizeRange.y),
            Mathf.Max(bubbleSizeRange.x, bubbleSizeRange.y));

        return new Vector2(size, size);
    }

    private float GetRandomRiseSpeed()
    {
        return Random.Range(
            Mathf.Min(riseSpeedRange.x, riseSpeedRange.y),
            Mathf.Max(riseSpeedRange.x, riseSpeedRange.y));
    }

    private void SpawnChoiceBubble(BubbleChoiceData data)
    {
        if (data.Sprite == null)
        {
            return;
        }

        BubblePopBubble bubble = bubblePrefab != null
            ? Instantiate(bubblePrefab, choiceArea)
            : CreateRuntimeBubble(choiceArea);

        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRect.pivot = new Vector2(0.5f, 0.5f);
        bubbleRect.sizeDelta = data.Size;
        bubbleRect.anchoredPosition = data.SpawnPosition;
        bubbleRect.SetAsLastSibling();
        bubble.gameObject.SetActive(true);

        bubble.Configure(
            choiceArea,
            GetBubbleSprite(),
            data.Sprite,
            poppedBubbleSprite,
            popAnimatorController,
            GetRandomRiseSpeed(),
            wiggleAmplitude,
            wiggleFrequency,
            despawnPadding,
            HandleCorrectBubblePopped,
            HandleBubbleReleased,
            CanBubblePop,
            HandleWrongBubbleTapped);

        bubble.SetContentFill(contentFillPercent);
        activeChoices.Add(bubble);
        bubbleDataLookup[bubble] = data;
    }

    private bool CanBubblePop(BubblePopBubble bubble)
    {
        return bubbleDataLookup.TryGetValue(bubble, out BubbleChoiceData data) && data.IsCorrect;
    }

    private void HandleCorrectBubblePopped(BubblePopBubble bubble)
    {
        quizSolved = true;
        quizActive = false;

        soundManager?.PlayCorrectBubble();
        RewardCurrentLetter();
        onCorrectBubbleSelected?.Invoke();
        RemoveOtherChoices(bubble);

        if (advanceToNextLetterAfterCorrect)
        {
            StartCoroutine(AdvanceToNextLetterRoutine());
        }
    }

    private void HandleWrongBubbleTapped(BubblePopBubble bubble)
    {
        soundManager?.PlayWrongBubble();
        onWrongBubbleSelected?.Invoke();
    }

    private void RewardCurrentLetter()
    {
        int currentLetterNumber = penDrawer != null ? penDrawer.CurrentLetterNumber : 1;
        int totalLetters = letterSwitcher != null ? letterSwitcher.GetTotalCount() : Mathf.Max(1, currentLetterNumber + 1);
        KaKhaTracingProgress.CompleteLetter(currentLetterNumber, scorePerCorrectBubble, totalLetters);
    }

    private IEnumerator AdvanceToNextLetterRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, nextLetterDelay));

        if (letterSwitcher == null)
        {
            yield break;
        }

        int totalLetters = letterSwitcher.GetTotalCount();
        if (totalLetters <= 0)
        {
            yield break;
        }

        int currentLetterNumber = penDrawer != null ? penDrawer.CurrentLetterNumber : 1;
        int nextLetterNumber = Mathf.Min(currentLetterNumber + 1, totalLetters);
        if (nextLetterNumber == currentLetterNumber)
        {
            yield break;
        }

        PlayerPrefs.SetInt(KaKhaTracingProgress.SelectedTracingLetterNumberKey, nextLetterNumber);
        PlayerPrefs.Save();
        letterSwitcher.SetLetterByNumber(nextLetterNumber);
    }

    private void HandleBubbleReleased(BubblePopBubble bubble)
    {
        if (bubble == null)
        {
            return;
        }

        bool hadData = bubbleDataLookup.TryGetValue(bubble, out BubbleChoiceData data);
        activeChoices.Remove(bubble);
        bubbleDataLookup.Remove(bubble);

        Destroy(bubble.gameObject);

        if (!hadData || !quizActive || quizSolved)
        {
            return;
        }

        data.SpawnPosition = GetRandomSpawnPosition(GetActiveSpawnPositions(), data.Size);
        SpawnChoiceBubble(data);
    }

    private void RemoveOtherChoices(BubblePopBubble selectedBubble)
    {
        for (int i = activeChoices.Count - 1; i >= 0; i--)
        {
            BubblePopBubble bubble = activeChoices[i];
            if (bubble == null || bubble == selectedBubble)
            {
                continue;
            }

            bubbleDataLookup.Remove(bubble);
            activeChoices.RemoveAt(i);
            Destroy(bubble.gameObject);
        }
    }

    private BubblePopBubble CreateRuntimeBubble(RectTransform parent)
    {
        GameObject bubbleObject = new GameObject("Bubble Choice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button), typeof(BubblePopBubble));
        bubbleObject.transform.SetParent(parent, false);

        Image shellImage = bubbleObject.GetComponent<Image>();
        shellImage.sprite = GetBubbleSprite();
        shellImage.preserveAspect = true;
        shellImage.color = Color.white;

        Button button = bubbleObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        GameObject contentObject = new GameObject("Bubble Image", typeof(RectTransform), typeof(Image));
        contentObject.transform.SetParent(bubbleObject.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = Vector2.zero;

        Image contentImage = contentObject.GetComponent<Image>();
        contentImage.preserveAspect = true;
        contentImage.raycastTarget = false;

        BubblePopBubble bubble = bubbleObject.GetComponent<BubblePopBubble>();
        bubble.BindReferences(shellImage, contentImage, button);
        bubble.SetContentFill(contentFillPercent);

        return bubble;
    }

    private Sprite GetBubbleSprite()
    {
        if (bubbleSprite != null)
        {
            return bubbleSprite;
        }

        if (generatedBubbleSprite == null)
        {
            generatedBubbleSprite = CreateGeneratedBubbleSprite();
        }

        return generatedBubbleSprite;
    }

    private Sprite CreateGeneratedBubbleSprite()
    {
        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "Generated Choice Bubble Sprite";

        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.46f;
        float innerRadius = radius * 0.76f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outline = Mathf.Clamp01(1f - Mathf.Abs(distance - (radius - 2f)) / 4f);
                float fill = Mathf.InverseLerp(radius, innerRadius, distance);
                float highlight = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), center + new Vector2(-22f, 24f)) / 18f);

                Color color = new Color(0.72f, 0.95f, 1f, 0.32f * fill);
                color = Color.Lerp(color, new Color(1f, 1f, 1f, 0.72f), highlight * 0.8f);
                color.a = Mathf.Max(color.a, outline * 0.75f);

                if (distance > radius)
                {
                    color = Color.clear;
                }

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
    }

    private void ResolveReferences()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (choiceArea == null && targetCanvas != null)
        {
            choiceArea = targetCanvas.GetComponent<RectTransform>();
        }
    }

    private void LoadPopAnimatorIfEmpty()
    {
#if UNITY_EDITOR
        if (popAnimatorController != null) return;

        popAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Sprites/Animations/BubbleAnimCtrl.controller");
#endif
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private void OnValidate()
    {
        bubblesToRise = Mathf.Clamp(bubblesToRise, 3, 8);
        bubbleSizeRange.x = Mathf.Max(10f, bubbleSizeRange.x);
        bubbleSizeRange.y = Mathf.Max(10f, bubbleSizeRange.y);
        if (bubbleSizeRange.y < bubbleSizeRange.x)
        {
            bubbleSizeRange.y = bubbleSizeRange.x;
        }
        horizontalSpawnPadding = Mathf.Max(0f, horizontalSpawnPadding);
        spawnBottomOffset = Mathf.Max(0f, spawnBottomOffset);
        spawnStaggerOffsetRange.x = Mathf.Max(0f, spawnStaggerOffsetRange.x);
        spawnStaggerOffsetRange.y = Mathf.Max(0f, spawnStaggerOffsetRange.y);
        if (spawnStaggerOffsetRange.y < spawnStaggerOffsetRange.x)
        {
            spawnStaggerOffsetRange.y = spawnStaggerOffsetRange.x;
        }
        minimumSpawnSpacing = Mathf.Max(0f, minimumSpawnSpacing);
        spawnPositionAttempts = Mathf.Max(1, spawnPositionAttempts);
        riseSpeedRange.x = Mathf.Max(1f, riseSpeedRange.x);
        riseSpeedRange.y = Mathf.Max(1f, riseSpeedRange.y);
        if (riseSpeedRange.y < riseSpeedRange.x)
        {
            riseSpeedRange.y = riseSpeedRange.x;
        }
        wiggleAmplitude = Mathf.Max(0f, wiggleAmplitude);
        wiggleFrequency = Mathf.Max(0f, wiggleFrequency);
        despawnPadding = Mathf.Max(0f, despawnPadding);
        contentFillPercent = Mathf.Clamp(contentFillPercent, 0.1f, 0.95f);
    }
}
