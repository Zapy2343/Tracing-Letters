using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Spawns letter-filled bubbles at random positions. Correct letters pop and score; wrong letters keep rising.
/// </summary>
public class BubblePopGameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform playArea;
    [SerializeField] private BubblePopBubble bubblePrefab;

    [Header("Bubble Visuals")]
    [SerializeField] private bool useGeneratedBubbleSprite = true;
    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private Sprite poppedBubbleSprite;
    [SerializeField] private RuntimeAnimatorController popAnimatorController;

    [Header("Level Data")]
    [Tooltip("Sprite list representing the level content images inside bubbles.")]
    [SerializeField] private List<Sprite> contentSprites = new List<Sprite>();
    [SerializeField] private int currentLevelIndex;

    [Tooltip("Chance that a spawned bubble is the correct current level image.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float correctBubbleSpawnChance = 0.45f;

    [Header("Editor Sprite Fallback")]
    [SerializeField] private bool autoLoadContentSpritesInEditor = true;
    [SerializeField] private string editorContentSpritesFolder = "Assets/Sprites/Images";

    [Header("Spawn Timing")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.45f, 1.25f);
    [SerializeField] private int maxActiveBubbles = 8;

    [Header("Object Pool")]
    [SerializeField] private int initialPoolSize = 12;
    [SerializeField] private bool allowPoolGrowth = true;
    [SerializeField] private RectTransform poolContainer;

    [Header("Bubble Layout")]
    [SerializeField] private Vector2 bubbleSizeRange = new Vector2(110f, 180f);
    [SerializeField] private float spawnPadding = 90f;
    [SerializeField] private float despawnPadding = 120f;
    [SerializeField] private float minimumSpawnSpacing = 140f;
    [SerializeField] private int spawnPositionAttempts = 12;
    [SerializeField] private float spawnSpacingCheckHeight = 260f;

    [Range(0.1f, 0.95f)]
    [SerializeField] private float contentFillPercent = 0.48f;

    [Header("Bubble Colors")]
    [SerializeField] private bool useRandomBubbleColors = true;
    [SerializeField]
    private List<Color> bubbleShellColors = new List<Color>
    {
        new Color(0.62f, 0.87f, 1f, 0.86f),
        new Color(0.78f, 0.68f, 1f, 0.86f),
        new Color(1f, 0.70f, 0.82f, 0.86f),
        new Color(0.69f, 0.94f, 0.70f, 0.86f),
        new Color(1f, 0.86f, 0.52f, 0.86f),
        new Color(0.66f, 0.95f, 0.91f, 0.86f),
        new Color(1f, 0.74f, 0.60f, 0.86f)
    };

    [Header("Motion")]
    [SerializeField] private Vector2 riseSpeedRange = new Vector2(90f, 175f);
    [SerializeField] private Vector2 wiggleAmplitudeRange = new Vector2(15f, 45f);
    [SerializeField] private Vector2 wiggleFrequencyRange = new Vector2(1.25f, 2.75f);

    [Header("Sounds")]
    [SerializeField] private AudioClip bubblePopClip;
    [SerializeField] private AudioClip wrongBubbleClip;
    [SerializeField] private AudioSource sfxSource;

    [Header("Score")]
    [SerializeField] private int score;
    [SerializeField] private UnityEvent<int> onScoreChanged;
    [SerializeField] private UnityEvent<BubblePopBubble> onBubblePopped;

    [Header("Current Level Display")]
    [SerializeField] private TMP_Text currentLevelText;
    [SerializeField] private Image currentLevelImage;
    [SerializeField] private float currentLevelTopOffset = 150f;
    [SerializeField] private Vector2 currentLevelImageSize = new Vector2(150f, 150f);
    [field: SerializeField] public BubblePopFXManager bubblePopFXManager { get; private set; }

    private readonly List<BubblePopBubble> activeBubbles = new List<BubblePopBubble>();
    private readonly Queue<BubblePopBubble> pooledBubbles = new Queue<BubblePopBubble>();
    private readonly HashSet<BubblePopBubble> pooledBubbleSet = new HashSet<BubblePopBubble>();
    private readonly HashSet<BubblePopBubble> correctBubbles = new HashSet<BubblePopBubble>();
    private Coroutine spawnRoutine;
    private Sprite generatedBubbleSprite;

    public int Score => score;
    public bool IsSpawning => spawnRoutine != null;
    public int ContentSpriteCount => contentSprites != null ? contentSprites.Count : 0;
    public int LevelCount => ContentSpriteCount;
    public int CurrentLevelIndex => Mathf.Clamp(currentLevelIndex, 0, Mathf.Max(0, LevelCount - 1));
    public Sprite CurrentLevelSprite => GetContentSpriteForLevelIndex(CurrentLevelIndex);

    private void Awake()
    {
        ResolveReferences();
        LoadContentSpritesFromProviderIfEmpty();
        LoadContentSpritesIfEmpty();
        LoadPopAnimatorIfEmpty();
        LoadAudioClipsIfEmpty();
        currentLevelIndex = BubblePopLevelMenu.GetSelectedLevelIndex();
        RefreshCurrentLevelDisplay();
        InitializePool();
    }

    private void OnEnable()
    {
        if (GlobalSoundManager.Instance != null)
        {
            GlobalSoundManager.Instance.SetMusicDucked(true, 0.5f);
        }

        if (autoStart)
        {
            StartGame();
        }
    }

    private void OnDisable()
    {
        if (GlobalSoundManager.Instance != null)
        {
            GlobalSoundManager.Instance.SetMusicDucked(false);
        }

        StopGame();
    }

    [ContextMenu("Start Bubble Game")]
    public void StartGame()
    {
        if (spawnRoutine != null) return;

        ResolveReferences();
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    [ContextMenu("Stop Bubble Game")]
    public void StopGame()
    {
        if (spawnRoutine == null) return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    [ContextMenu("Reset Score")]
    public void ResetScore()
    {
        score = 0;
        onScoreChanged?.Invoke(score);
    }

    public void BeginLevel(int levelIndex)
    {
        currentLevelIndex = Mathf.Clamp(levelIndex, 0, Mathf.Max(0, LevelCount - 1));
        PlayerPrefs.SetInt(BubblePopLevelMenu.SelectedLevelIndexKey, currentLevelIndex);
        PlayerPrefs.SetString(BubblePopLevelMenu.SelectedLevelImageNameKey, CurrentLevelSprite != null ? CurrentLevelSprite.name : string.Empty);
        PlayerPrefs.Save();

        StopGame();
        ClearActiveBubbles();
        ResetScore();
        RefreshCurrentLevelDisplay();
        StartGame();
    }

    [ContextMenu("Clear Active Bubbles")]
    public void ClearActiveBubbles()
    {
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            if (activeBubbles[i] != null)
            {
                ReturnBubbleToPool(activeBubbles[i]);
            }
        }

        activeBubbles.Clear();
        correctBubbles.Clear();
    }

    public void SetContentSprites(IList<Sprite> sprites)
    {
        contentSprites.Clear();
        if (sprites != null)
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null)
                {
                    contentSprites.Add(sprites[i]);
                }
            }
        }

        currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, Mathf.Max(0, LevelCount - 1));
        RefreshCurrentLevelDisplay();
    }

    private IEnumerator SpawnLoop()
    {
        while (isActiveAndEnabled)
        {
            CleanupBubbleList();

            if (activeBubbles.Count < maxActiveBubbles)
            {
                SpawnBubble();
            }

            float waitTime = Random.Range(
                Mathf.Min(spawnIntervalRange.x, spawnIntervalRange.y),
                Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y));

            yield return new WaitForSeconds(Mathf.Max(0.05f, waitTime));
        }
    }

    public BubblePopBubble SpawnBubble()
    {
        ResolveReferences();
        if (playArea == null) return null;

        BubblePopBubble bubble = GetBubbleFromPool();
        if (bubble == null) return null;

        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
        bubble.transform.SetParent(playArea, false);
        bubble.gameObject.SetActive(true);

        float size = Random.Range(
            Mathf.Min(bubbleSizeRange.x, bubbleSizeRange.y),
            Mathf.Max(bubbleSizeRange.x, bubbleSizeRange.y));

        bubbleRect.sizeDelta = new Vector2(size, size);
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRect.pivot = new Vector2(0.5f, 0.5f);
        bubbleRect.anchoredPosition = GetRandomSpawnPosition(size);
        bubbleRect.SetAsLastSibling();

        Sprite contentSprite = PickBubbleSprite(out bool isCorrectBubble);
        bubble.Configure(
            playArea,
            GetBubbleSprite(),
            contentSprite,
            poppedBubbleSprite,
            popAnimatorController,
            GetRandomFromRange(riseSpeedRange),
            GetRandomFromRange(wiggleAmplitudeRange),
            GetRandomFromRange(wiggleFrequencyRange),
            despawnPadding,
            HandleBubblePopped,
            ReturnBubbleToPool,
            candidate => correctBubbles.Contains(candidate),
            HandleWrongBubbleTapped);
        ApplyBubbleShellColor(bubble);
        bubble.SetContentFill(contentFillPercent);

        if (isCorrectBubble)
        {
            correctBubbles.Add(bubble);
        }
        else
        {
            correctBubbles.Remove(bubble);
        }

        activeBubbles.Add(bubble);
        return bubble;
    }

    private void ApplyBubbleShellColor(BubblePopBubble bubble)
    {
        if (!useRandomBubbleColors || bubbleShellColors == null || bubbleShellColors.Count == 0 || bubble == null)
        {
            return;
        }

        Image shellImage = bubble.GetComponent<Image>();
        if (shellImage != null)
        {
            shellImage.color = bubbleShellColors[Random.Range(0, bubbleShellColors.Count)];
        }
    }

    private void InitializePool()
    {
        ResolveReferences();
        if (playArea == null) return;

        if (poolContainer == null)
        {
            GameObject poolObject = new GameObject("Bubble Pool", typeof(RectTransform));
            poolObject.transform.SetParent(playArea, false);
            poolContainer = poolObject.GetComponent<RectTransform>();
            poolContainer.anchorMin = Vector2.zero;
            poolContainer.anchorMax = Vector2.one;
            poolContainer.offsetMin = Vector2.zero;
            poolContainer.offsetMax = Vector2.zero;
        }

        while (pooledBubbles.Count < initialPoolSize)
        {
            BubblePopBubble bubble = CreateBubbleInstance(poolContainer);
            ReturnBubbleToPool(bubble);
        }
    }

    private BubblePopBubble GetBubbleFromPool()
    {
        CleanupBubbleList();

        while (pooledBubbles.Count > 0)
        {
            BubblePopBubble bubble = pooledBubbles.Dequeue();
            if (bubble != null)
            {
                pooledBubbleSet.Remove(bubble);
                return bubble;
            }
        }

        return allowPoolGrowth ? CreateBubbleInstance(poolContainer != null ? poolContainer : playArea) : null;
    }

    private BubblePopBubble CreateBubbleInstance(RectTransform parent)
    {
        BubblePopBubble bubble = bubblePrefab != null
            ? Instantiate(bubblePrefab, parent)
            : CreateRuntimeBubble(parent);

        bubble.gameObject.SetActive(false);
        return bubble;
    }

    private void ReturnBubbleToPool(BubblePopBubble bubble)
    {
        if (bubble == null || pooledBubbleSet.Contains(bubble)) return;

        activeBubbles.Remove(bubble);
        correctBubbles.Remove(bubble);

        bubble.gameObject.SetActive(false);

        if (poolContainer != null && gameObject.activeInHierarchy && poolContainer.gameObject.activeInHierarchy)
        {
            bubble.transform.SetParent(poolContainer, false);
        }

        pooledBubbles.Enqueue(bubble);
        pooledBubbleSet.Add(bubble);
    }

    private BubblePopBubble CreateRuntimeBubble(RectTransform parent)
    {
        GameObject bubbleObject = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button), typeof(BubblePopBubble));
        bubbleObject.transform.SetParent(parent, false);

        if (popAnimatorController != null)
        {
            Animator animator = bubbleObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = popAnimatorController;
            animator.enabled = false;
        }

        Image shellImage = bubbleObject.GetComponent<Image>();
        shellImage.sprite = GetBubbleSprite();
        shellImage.preserveAspect = true;
        shellImage.color = Color.white;

        Button button = bubbleObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        GameObject contentObject = new GameObject("Bubble Content Image", typeof(RectTransform), typeof(Image));
        contentObject.transform.SetParent(bubbleObject.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.08f, 0.08f);
        contentRect.anchorMax = new Vector2(0.92f, 0.92f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;

        Image insideImage = contentObject.GetComponent<Image>();
        insideImage.preserveAspect = true;
        insideImage.raycastTarget = false;
        insideImage.color = Color.white;

        BubblePopBubble bubble = bubbleObject.GetComponent<BubblePopBubble>();
        bubble.BindReferences(shellImage, insideImage, button);
        bubble.SetContentFill(contentFillPercent);

        return bubble;
    }

    private Vector2 GetRandomSpawnPosition(float bubbleSize)
    {
        Rect rect = playArea.rect;
        float halfWidth = bubbleSize * 0.5f;
        float minX = rect.xMin + halfWidth;
        float maxX = rect.xMax - halfWidth;
        float y = rect.yMin - spawnPadding - halfWidth;

        if (minX >= maxX)
        {
            return new Vector2(rect.center.x, y);
        }

        Vector2 bestPosition = new Vector2(Random.Range(minX, maxX), y);
        float bestDistance = GetClosestLowBubbleHorizontalDistance(bestPosition.x);

        for (int i = 0; i < spawnPositionAttempts; i++)
        {
            Vector2 candidate = new Vector2(Random.Range(minX, maxX), y);
            float closestDistance = GetClosestLowBubbleHorizontalDistance(candidate.x);

            if (closestDistance >= minimumSpawnSpacing)
            {
                return candidate;
            }

            if (closestDistance > bestDistance)
            {
                bestDistance = closestDistance;
                bestPosition = candidate;
            }
        }

        return bestPosition;
    }

    private float GetClosestLowBubbleHorizontalDistance(float candidateX)
    {
        float closestDistance = float.MaxValue;
        float bottomLimit = playArea.rect.yMin + spawnSpacingCheckHeight;

        for (int i = 0; i < activeBubbles.Count; i++)
        {
            BubblePopBubble bubble = activeBubbles[i];
            if (bubble == null || bubble.IsPopped) continue;

            RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
            if (bubbleRect == null || bubbleRect.anchoredPosition.y > bottomLimit) continue;

            closestDistance = Mathf.Min(closestDistance, Mathf.Abs(candidateX - bubbleRect.anchoredPosition.x));
        }

        return closestDistance;
    }

    private Sprite PickBubbleSprite(out bool isCorrectBubble)
    {
        Sprite correctSprite = CurrentLevelSprite;
        isCorrectBubble = correctSprite != null;

        if (correctSprite == null)
        {
            return null;
        }

        if (contentSprites == null || contentSprites.Count <= 1 || Random.value <= correctBubbleSpawnChance)
        {
            return correctSprite;
        }

        Sprite wrongSprite = GetRandomWrongSprite(correctSprite);
        if (wrongSprite != null)
        {
            isCorrectBubble = false;
            return wrongSprite;
        }

        return correctSprite;
    }

    private Sprite GetRandomWrongSprite(Sprite correctSprite)
    {
        if (contentSprites == null || contentSprites.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < 12; i++)
        {
            Sprite candidate = contentSprites[Random.Range(0, contentSprites.Count)];
            if (candidate != null && candidate != correctSprite)
            {
                return candidate;
            }
        }

        for (int i = 0; i < contentSprites.Count; i++)
        {
            if (contentSprites[i] != null && contentSprites[i] != correctSprite)
            {
                return contentSprites[i];
            }
        }

        return null;
    }

    public Sprite GetContentSpriteForLevelIndex(int levelIndex)
    {
        if (contentSprites == null || contentSprites.Count == 0)
        {
            return null;
        }

        int safeIndex = Mathf.Clamp(levelIndex, 0, contentSprites.Count - 1);
        return contentSprites[safeIndex];
    }

    private void RefreshCurrentLevelDisplay()
    {
        ResolveReferences();
        EnsureCurrentLevelDisplay();

        Sprite sprite = CurrentLevelSprite;

        if (currentLevelImage != null)
        {
            currentLevelImage.sprite = sprite;
            currentLevelImage.enabled = sprite != null;
            currentLevelImage.gameObject.SetActive(sprite != null);
            currentLevelImage.preserveAspect = true;
        }

        if (currentLevelText != null)
        {
            currentLevelText.enabled = false;
            currentLevelText.gameObject.SetActive(false);
        }
    }

    private void EnsureCurrentLevelDisplay()
    {
        if (currentLevelImage != null || targetCanvas == null)
        {
            return;
        }

        GameObject displayObject = new GameObject("Current Level Image", typeof(RectTransform), typeof(Image));
        displayObject.transform.SetParent(targetCanvas.transform, false);

        RectTransform rect = displayObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -Mathf.Max(80f, currentLevelTopOffset));
        rect.sizeDelta = currentLevelImageSize;
        rect.SetAsLastSibling();

        currentLevelImage = displayObject.GetComponent<Image>();
        currentLevelImage.preserveAspect = true;
        currentLevelImage.raycastTarget = false;
    }

    private void HandleWrongBubbleTapped(BubblePopBubble bubble)
    {
        PlaySfx(wrongBubbleClip);

        if (bubble != null)
        {
            bubble.PlayRejectedTapFeedback();
        }
    }

    private float GetRandomFromRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private Sprite GetBubbleSprite()
    {
        if (!useGeneratedBubbleSprite && bubbleSprite != null)
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
        texture.name = "Generated Bubble Sprite";

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

        if (targetCanvas == null)
        {
#if UNITY_2023_1_OR_NEWER
            targetCanvas = FindFirstObjectByType<Canvas>();
#else
            targetCanvas = FindObjectOfType<Canvas>();
#endif
        }

        if (playArea == null && targetCanvas != null)
        {
            playArea = targetCanvas.GetComponent<RectTransform>();
        }
    }

    private void LoadContentSpritesFromProviderIfEmpty()
    {
        if (contentSprites != null && contentSprites.Count > 0) return;

        BubblePopSelectedLevelImageProvider provider = GetComponent<BubblePopSelectedLevelImageProvider>();
        if (provider == null)
        {
#if UNITY_2023_1_OR_NEWER
            provider = FindFirstObjectByType<BubblePopSelectedLevelImageProvider>();
#else
            provider = FindObjectOfType<BubblePopSelectedLevelImageProvider>();
#endif
        }

        if (provider != null && provider.LevelImages != null && provider.LevelImages.Count > 0)
        {
            SetContentSprites(provider.LevelImages);
        }
    }

    private void LoadContentSpritesIfEmpty()
    {
#if UNITY_EDITOR
        if (contentSprites == null)
        {
            contentSprites = new List<Sprite>();
        }

        if (!autoLoadContentSpritesInEditor || contentSprites.Count > 0) return;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { editorContentSpritesFolder });
        List<string> paths = new List<string>();
        foreach (string guid in guids)
        {
            paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        }

        paths.Sort();

        foreach (string path in paths)
        {
            Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                contentSprites.Add(sprite);
            }
        }
#endif
    }

    private void LoadPopAnimatorIfEmpty()
    {
#if UNITY_EDITOR
        if (popAnimatorController != null) return;

        popAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Sprites/Animations/BubbleAnimCtrl.controller");
#endif
    }

    private void LoadAudioClipsIfEmpty()
    {
#if UNITY_EDITOR
        if (bubblePopClip == null)
        {
            bubblePopClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/Popin.mp3");
        }

        if (wrongBubbleClip == null)
        {
            wrongBubbleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/notification.mp3");
        }
#endif

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    private void HandleBubblePopped(BubblePopBubble bubble)
    {
        activeBubbles.Remove(bubble);
        correctBubbles.Remove(bubble);
        PlaySfx(bubblePopClip);
        score++;

        if (bubble != null)
        {
            RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
            if (bubbleRect != null)
            {
                if (bubblePopFXManager != null)
                {
                    bubblePopFXManager.PlayPopFX(bubbleRect.anchoredPosition);
                }
            }
        }

        onBubblePopped?.Invoke(bubble);
        onScoreChanged?.Invoke(score);
    }

    private void PlaySfx(AudioClip clip)
    {
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

    private void CleanupBubbleList()
    {
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            if (activeBubbles[i] == null)
            {
                activeBubbles.RemoveAt(i);
            }
        }
    }

    private void OnValidate()
    {
        maxActiveBubbles = Mathf.Max(1, maxActiveBubbles);
        initialPoolSize = Mathf.Max(0, initialPoolSize);
        minimumSpawnSpacing = Mathf.Max(0f, minimumSpawnSpacing);
        spawnPositionAttempts = Mathf.Max(1, spawnPositionAttempts);
        spawnSpacingCheckHeight = Mathf.Max(0f, spawnSpacingCheckHeight);
        spawnIntervalRange.x = Mathf.Max(0.05f, spawnIntervalRange.x);
        spawnIntervalRange.y = Mathf.Max(0.05f, spawnIntervalRange.y);
        bubbleSizeRange.x = Mathf.Max(10f, bubbleSizeRange.x);
        bubbleSizeRange.y = Mathf.Max(10f, bubbleSizeRange.y);
        riseSpeedRange.x = Mathf.Max(1f, riseSpeedRange.x);
        riseSpeedRange.y = Mathf.Max(1f, riseSpeedRange.y);
        contentFillPercent = Mathf.Clamp(contentFillPercent, 0.1f, 0.95f);
        currentLevelIndex = Mathf.Max(0, currentLevelIndex);
        correctBubbleSpawnChance = Mathf.Clamp(correctBubbleSpawnChance, 0.05f, 1f);
        currentLevelTopOffset = Mathf.Max(80f, currentLevelTopOffset);
        currentLevelImageSize.x = Mathf.Max(48f, currentLevelImageSize.x);
        currentLevelImageSize.y = Mathf.Max(48f, currentLevelImageSize.y);

        if (bubbleShellColors == null)
        {
            bubbleShellColors = new List<Color>();
        }
    }
}
