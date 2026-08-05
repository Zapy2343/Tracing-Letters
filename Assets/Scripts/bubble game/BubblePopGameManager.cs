using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Spawns image-filled bubbles at random times and positions, then lets them rise until popped or despawned.
/// </summary>
public class BubblePopGameManager : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Canvas that contains the bubble play area. Auto-detected if empty.")]
    [SerializeField] private Canvas targetCanvas;

    [Tooltip("RectTransform that defines where bubbles spawn and travel. Uses the canvas rect if empty.")]
    [SerializeField] private RectTransform playArea;

    [Tooltip("Optional prefab with BubblePopBubble, Image, Button, and child content Image. If empty, one is built at runtime.")]
    [SerializeField] private BubblePopBubble bubblePrefab;

    [Header("Designer Sprites")]
    [Tooltip("Use the soft generated bubble image. Disable this to use Bubble Sprite instead.")]
    [SerializeField] private bool useGeneratedBubbleSprite = true;

    [Tooltip("Outer bubble sprite used when Use Generated Bubble Sprite is disabled. If empty, a generated bubble is used.")]
    [SerializeField] private Sprite bubbleSprite;

    [Tooltip("Optional sprite shown during the pop animation.")]
    [SerializeField] private Sprite poppedBubbleSprite;

    [Tooltip("Animator Controller played when a bubble pops. Auto-loads the Burst controller in the Unity Editor if empty.")]
    [SerializeField] private RuntimeAnimatorController popAnimatorController;

    [Tooltip("Images randomly placed inside bubbles.")]
    [SerializeField] private List<Sprite> contentSprites = new List<Sprite>();

    [Tooltip("In the Unity Editor, fill Content Sprites from the project folder below when the list is empty.")]
    [SerializeField] private bool autoLoadContentSpritesInEditor = true;

    [Tooltip("Editor-only folder used to auto-fill Content Sprites for quick designer setup.")]
    [SerializeField] private string editorContentSpritesFolder = "Assets/Sprites/Images";

    [Header("Spawn Timing")]
    [Tooltip("Start spawning as soon as this object becomes active.")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("Random seconds between each bubble spawn.")]
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.45f, 1.25f);

    [Tooltip("Maximum number of live bubbles allowed at once.")]
    [SerializeField] private int maxActiveBubbles = 8;

    [Header("Object Pool")]
    [Tooltip("Number of bubbles created up front and reused during play.")]
    [SerializeField] private int initialPoolSize = 12;

    [Tooltip("Allow the pool to create more bubbles if all pooled bubbles are in use.")]
    [SerializeField] private bool allowPoolGrowth = true;

    [Tooltip("Optional parent for inactive pooled bubbles. Created automatically if empty.")]
    [SerializeField] private RectTransform poolContainer;

    [Header("Bubble Layout")]
    [Tooltip("Random bubble size in canvas units.")]
    [SerializeField] private Vector2 bubbleSizeRange = new Vector2(110f, 180f);

    [Tooltip("How far below the play area bubbles begin.")]
    [SerializeField] private float spawnPadding = 90f;

    [Tooltip("How far above the play area bubbles can travel before being destroyed.")]
    [SerializeField] private float despawnPadding = 120f;

    [Tooltip("Minimum horizontal distance from other low/just-spawned bubbles.")]
    [SerializeField] private float minimumSpawnSpacing = 140f;

    [Tooltip("How many random positions to try before accepting the best available spawn point.")]
    [SerializeField] private int spawnPositionAttempts = 12;

    [Tooltip("Only bubbles below this height from the bottom are considered for spawn spacing.")]
    [SerializeField] private float spawnSpacingCheckHeight = 260f;

    [Range(0.1f, 0.95f)]
    [Tooltip("How much of the bubble size the inside image fills.")]
    [SerializeField] private float contentFillPercent = 0.46f;

    [Header("Bubble Colors")]
    [Tooltip("Randomly tint spawned bubble shells using the palette below.")]
    [SerializeField] private bool useRandomBubbleColors = true;

    [Tooltip("Colors randomly applied to the outer bubble shell. Alpha controls bubble opacity.")]
    [SerializeField] private List<Color> bubbleShellColors = new List<Color>
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
    [Tooltip("Random upward speed in canvas units per second.")]
    [SerializeField] private Vector2 riseSpeedRange = new Vector2(90f, 175f);

    [Tooltip("Side-to-side drift strength while rising.")]
    [SerializeField] private Vector2 wiggleAmplitudeRange = new Vector2(15f, 45f);

    [Tooltip("Side-to-side drift speed while rising.")]
    [SerializeField] private Vector2 wiggleFrequencyRange = new Vector2(1.25f, 2.75f);

    [Header("Score")]
    [SerializeField] private int score;
    [SerializeField] private UnityEvent<int> onScoreChanged;
    [SerializeField] private UnityEvent<BubblePopBubble> onBubblePopped;

    private readonly List<BubblePopBubble> activeBubbles = new List<BubblePopBubble>();
    private readonly Queue<BubblePopBubble> pooledBubbles = new Queue<BubblePopBubble>();
    private readonly HashSet<BubblePopBubble> pooledBubbleSet = new HashSet<BubblePopBubble>();
    private Coroutine spawnRoutine;
    private Sprite generatedBubbleSprite;

    public int Score => score;
    public bool IsSpawning => spawnRoutine != null;

    private void Awake()
    {
        ResolveReferences();
        LoadContentSpritesIfEmpty();
        LoadPopAnimatorIfEmpty();
        InitializePool();
    }

    private void OnEnable()
    {
        if (autoStart)
        {
            StartGame();
        }
    }

    private void OnDisable()
    {
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
    }

    public void SetContentSprites(IList<Sprite> sprites)
    {
        contentSprites.Clear();
        if (sprites == null)
        {
            return;
        }

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] != null)
            {
                contentSprites.Add(sprites[i]);
            }
        }
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

        Sprite selectedContent = GetRandomContentSprite();
        bubble.Configure(
            playArea,
            GetBubbleSprite(),
            selectedContent,
            poppedBubbleSprite,
            popAnimatorController,
            GetRandomFromRange(riseSpeedRange),
            GetRandomFromRange(wiggleAmplitudeRange),
            GetRandomFromRange(wiggleFrequencyRange),
            despawnPadding,
            HandleBubblePopped,
            ReturnBubbleToPool);
        ApplyBubbleShellColor(bubble);
        bubble.SetContentFill(contentFillPercent);

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
        if (shellImage == null)
        {
            return;
        }

        shellImage.color = bubbleShellColors[Random.Range(0, bubbleShellColors.Count)];
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

        if (!allowPoolGrowth)
        {
            return null;
        }

        return CreateBubbleInstance(poolContainer != null ? poolContainer : playArea);
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
        if (bubble == null) return;
        if (pooledBubbleSet.Contains(bubble)) return;

        activeBubbles.Remove(bubble);

        if (poolContainer != null)
        {
            bubble.transform.SetParent(poolContainer, false);
        }

        bubble.gameObject.SetActive(false);
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

            float distance = Mathf.Abs(candidateX - bubbleRect.anchoredPosition.x);
            closestDistance = Mathf.Min(closestDistance, distance);
        }

        return closestDistance;
    }

    private Sprite GetRandomContentSprite()
    {
        if (contentSprites == null || contentSprites.Count == 0)
        {
            return null;
        }

        return contentSprites[Random.Range(0, contentSprites.Count)];
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

    private void HandleBubblePopped(BubblePopBubble bubble)
    {
        activeBubbles.Remove(bubble);
        score++;
        onBubblePopped?.Invoke(bubble);
        onScoreChanged?.Invoke(score);
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

        if (bubbleShellColors == null)
        {
            bubbleShellColors = new List<Color>();
        }
    }
}
