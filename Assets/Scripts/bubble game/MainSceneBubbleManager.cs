using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated Background Interactive Bubble Manager for mainScreen scene.
/// Uses Object Pooling for spawning and popping bubbles with pop SFX and sprite animations.
/// </summary>
public class MainSceneBubbleManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private RectTransform playArea;

    [Header("Bubble Visuals")]
    [SerializeField] private bool useGeneratedBubbleSprites = true;
    [SerializeField] private Sprite assignedBubbleSprite;
    [SerializeField] private RuntimeAnimatorController popAnimatorController;

    [Header("Content Sprites")]
    [SerializeField] private List<Sprite> contentSprites = new List<Sprite>();

    [Header("Spawn Timing")]
    [SerializeField] private float spawnInterval = 1.6f;
    [SerializeField] private int maxActiveBubbles = 10;

    [Header("Object Pool")]
    [SerializeField] private int initialPoolSize = 14;
    [SerializeField] private bool allowPoolGrowth = true;

    [Header("Bubble Layout")]
    [SerializeField] private Vector2 bubbleSizeRange = new Vector2(140f, 210f);
    [SerializeField] private float contentFillPercent = 0.48f;

    [Header("Bubble Colors")]
    [SerializeField] private bool useRandomBubbleColors = true;
    [SerializeField]
    private List<Color> bubbleShellColors = new List<Color>
    {
        new Color(1.0f, 0.78f, 0.90f, 0.78f),
        new Color(0.66f, 0.87f, 1.0f, 0.78f),
        new Color(0.72f, 0.94f, 0.76f, 0.78f),
        new Color(1.0f, 0.87f, 0.55f, 0.78f),
        new Color(0.82f, 0.75f, 1.0f, 0.78f)
    };

    [Header("Motion")]
    [SerializeField] private Vector2 riseSpeedRange = new Vector2(120f, 240f);
    [SerializeField] private Vector2 wiggleAmplitudeRange = new Vector2(18f, 42f);
    [SerializeField] private Vector2 wiggleFrequencyRange = new Vector2(1.2f, 2.6f);
    [SerializeField] private float despawnPadding = 120f;

    [Header("Sound")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip bubblePopClip;

    [Header("FX Reference")]
    [SerializeField] private BubblePopFXManager fxManager;

    private readonly Queue<BubblePopBubble> bubblePool = new Queue<BubblePopBubble>();
    private readonly HashSet<BubblePopBubble> activeBubbles = new HashSet<BubblePopBubble>();

    private RectTransform poolContainer;
    private Sprite generatedBubbleSprite;
    private Coroutine spawnRoutine;

    private void Awake()
    {
        ResolveReferences();
        AutoLoadContentSpritesIfEmpty();
        AutoLoadPopAnimatorIfEmpty();
        AutoLoadAudioIfEmpty();
        InitializePoolContainer();
        InitializeBubblePool();
    }

    private void OnEnable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }
        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        ClearActiveBubbles();
    }

    private void ResolveReferences()
    {
        if (playArea == null)
        {
            playArea = GetComponent<RectTransform>();
        }

        if (playArea == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                playArea = parentCanvas.GetComponent<RectTransform>();
            }
        }

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    private void InitializePoolContainer()
    {
        if (playArea == null) return;

        GameObject pObj = new GameObject("MainScene_BubblePool", typeof(RectTransform));
        pObj.transform.SetParent(playArea, false);

        poolContainer = pObj.GetComponent<RectTransform>();
        poolContainer.anchorMin = Vector2.zero;
        poolContainer.anchorMax = Vector2.one;
        poolContainer.sizeDelta = Vector2.zero;
        poolContainer.anchoredPosition = Vector2.zero;
        pObj.SetActive(false);
    }

    private void InitializeBubblePool()
    {
        while (bubblePool.Count < initialPoolSize)
        {
            BubblePopBubble bubble = CreatePooledBubbleInstance();
            ReturnBubbleToPool(bubble);
        }
    }

    private BubblePopBubble CreatePooledBubbleInstance()
    {
        GameObject bObj = new GameObject("MainScene_Bubble", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button), typeof(BubblePopBubble), typeof(Animator));
        bObj.transform.SetParent(poolContainer, false);

        BubblePopBubble bubble = bObj.GetComponent<BubblePopBubble>();
        bObj.SetActive(false);
        return bubble;
    }

    private BubblePopBubble GetBubbleFromPool()
    {
        while (bubblePool.Count > 0)
        {
            BubblePopBubble bubble = bubblePool.Dequeue();
            if (bubble != null)
            {
                bubble.gameObject.transform.SetParent(playArea, false);
                bubble.gameObject.SetActive(true);
                return bubble;
            }
        }

        if (allowPoolGrowth)
        {
            BubblePopBubble newBubble = CreatePooledBubbleInstance();
            newBubble.gameObject.transform.SetParent(playArea, false);
            newBubble.gameObject.SetActive(true);
            return newBubble;
        }

        return null;
    }

    private void ReturnBubbleToPool(BubblePopBubble bubble)
    {
        if (bubble == null) return;

        activeBubbles.Remove(bubble);

        bubble.gameObject.SetActive(false);

        if (poolContainer != null && gameObject.activeInHierarchy && poolContainer.gameObject.activeInHierarchy)
        {
            bubble.gameObject.transform.SetParent(poolContainer, false);
        }

        bubblePool.Enqueue(bubble);
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (activeBubbles.Count < maxActiveBubbles && playArea != null)
            {
                SpawnBubble();
            }

            yield return new WaitForSeconds(Mathf.Max(0.2f, spawnInterval));
        }
    }

    public BubblePopBubble SpawnBubble()
    {
        BubblePopBubble bubble = GetBubbleFromPool();
        if (bubble == null) return null;

        RectTransform rect = bubble.GetComponent<RectTransform>();
        float size = GetRandomFromRange(bubbleSizeRange);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = GetRandomSpawnPosition(size);
        rect.SetAsLastSibling();

        Sprite contentSprite = PickRandomContentSprite();

        bubble.Configure(
            playArea,
            GetBubbleSprite(),
            contentSprite,
            null,
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

    private void HandleBubblePopped(BubblePopBubble bubble)
    {
        activeBubbles.Remove(bubble);

        if (bubblePopClip != null && sfxSource != null)
        {
            if (GlobalSoundManager.Instance != null)
            {
                GlobalSoundManager.Instance.PlaySfx(bubblePopClip);
            }
            else
            {
                sfxSource.PlayOneShot(bubblePopClip);
            }
        }

        if (bubble != null)
        {
            RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
            if (bubbleRect != null)
            {
                fxManager.PlayPopFX(bubbleRect.anchoredPosition);
            }
        }
    }

    public void ClearActiveBubbles()
    {
        List<BubblePopBubble> list = new List<BubblePopBubble>(activeBubbles);
        foreach (BubblePopBubble b in list)
        {
            ReturnBubbleToPool(b);
        }
        activeBubbles.Clear();
    }

    private Sprite PickRandomContentSprite()
    {
        if (contentSprites != null && contentSprites.Count > 0)
        {
            return contentSprites[Random.Range(0, contentSprites.Count)];
        }
        return null;
    }

    private Vector2 GetRandomSpawnPosition(float bubbleSize)
    {
        if (playArea == null) return Vector2.zero;

        float halfWidth = Mathf.Max(20f, (playArea.rect.width - bubbleSize) * 0.5f);
        float spawnX = Random.Range(-halfWidth, halfWidth);
        float spawnY = playArea.rect.yMin - bubbleSize * 0.5f - 20f;

        return new Vector2(spawnX, spawnY);
    }

    private Sprite GetBubbleSprite()
    {
        if (useGeneratedBubbleSprites)
        {
            if (generatedBubbleSprite == null)
            {
                generatedBubbleSprite = CreateGeneratedBubbleSprite();
            }
            return generatedBubbleSprite;
        }

        return assignedBubbleSprite;
    }

    private void ApplyBubbleShellColor(BubblePopBubble bubble)
    {
        if (!useRandomBubbleColors || bubbleShellColors == null || bubbleShellColors.Count == 0 || bubble == null)
        {
            return;
        }

        Image img = bubble.GetComponent<Image>();
        if (img != null)
        {
            img.color = bubbleShellColors[Random.Range(0, bubbleShellColors.Count)];
        }
    }

    private float GetRandomFromRange(Vector2 range)
    {
        return Random.Range(range.x, range.y);
    }

    private Sprite CreateGeneratedBubbleSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "MainScene_GeneratedBubble";

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.43f;
        float innerRadius = radius * 0.72f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y);
                float dist = Vector2.Distance(point, center);
                float fill = Mathf.InverseLerp(radius, innerRadius, dist);
                float outline = Mathf.Clamp01(1f - Mathf.Abs(dist - (radius - 2f)) / 4f);
                float highlight = Mathf.Clamp01(1f - Vector2.Distance(point, center + new Vector2(-22f, 24f)) / 16f);
                float lowerGlow = Mathf.Clamp01(1f - Vector2.Distance(point, center + new Vector2(18f, -22f)) / 30f);

                Color color = new Color(1f, 1f, 1f, 0.28f * fill);
                color = Color.Lerp(color, new Color(1f, 1f, 1f, 0.86f), highlight * 0.85f);
                color = Color.Lerp(color, new Color(1f, 1f, 1f, 0.42f), lowerGlow * 0.3f);
                color.a = Mathf.Max(color.a, outline * 0.72f);

                if (dist > radius)
                {
                    color = Color.clear;
                }

                tex.SetPixel(x, y, color);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void AutoLoadContentSpritesIfEmpty()
    {
#if UNITY_EDITOR
        if (contentSprites != null && contentSprites.Count > 0) return;

        contentSprites = new List<Sprite>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Sprites/Dotted Letters" });
        foreach (string guid in guids)
        {
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (s != null) contentSprites.Add(s);
        }
#endif
    }

    private void AutoLoadPopAnimatorIfEmpty()
    {
#if UNITY_EDITOR
        if (popAnimatorController != null) return;

        popAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Sprites/Animations/BubbleAnimCtrl.controller");
#endif
    }

    private void AutoLoadAudioIfEmpty()
    {
#if UNITY_EDITOR
        if (bubblePopClip == null)
        {
            bubblePopClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/Popin.mp3");
        }
#endif
    }
}
