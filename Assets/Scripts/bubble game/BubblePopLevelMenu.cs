using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BubblePopLevelMenu : MonoBehaviour
{
    public const string SelectedLevelIndexKey = "bubble_pop_selected_level_index";
    public const string SelectedLevelImageNameKey = "bubble_pop_selected_level_image_name";
    public const string UnlockedLevelIndexKey = "bubble_pop_unlocked_level_index";
    public const string BestScorePrefix = "bubble_pop_best_score_";
    public const string CompletedLevelPrefix = "bubble_pop_completed_level_";

    [Header("Scene")]
    [SerializeField] private string bubbleGameSceneName = "Bubble POP";

    [Header("Levels")]
    [SerializeField] private RectTransform bubblesRoot;
    [FormerlySerializedAs("letters")]
    [SerializeField] private List<Sprite> levelImages = new List<Sprite>();
    [SerializeField] private List<Button> bubbleButtons = new List<Button>();

    [Header("Score")]
    [SerializeField] private TMP_Text totalScoreText;

    [Header("Bubble Shell Visuals")]
    [Tooltip("When enabled, menu bubbles use a generated soft bubble image and the color palette below. When disabled, Assigned Bubble Sprite is used if provided.")]
    [SerializeField] private bool useGeneratedColoredBubbles = true;
    [SerializeField] private Sprite assignedBubbleSprite;
    [SerializeField] private List<Color> bubbleShellColors = new List<Color>
    {
        new Color(1f, 0.78f, 0.9f, 0.78f),
        new Color(0.66f, 0.87f, 1f, 0.78f),
        new Color(0.72f, 0.94f, 0.76f, 0.78f),
        new Color(1f, 0.87f, 0.55f, 0.78f),
        new Color(0.82f, 0.75f, 1f, 0.78f)
    };

    [Header("Lock Visuals")]
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private List<GameObject> lockOverlays = new List<GameObject>();

    private readonly List<UnityAction> clickActions = new List<UnityAction>();
    private Sprite generatedBubbleSprite;

    public int HighestUnlockedLevelIndex => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedLevelIndexKey, 0), 0, Mathf.Max(0, GetLevelCount() - 1));

    private void Awake()
    {
        AutoFillButtonsIfEmpty();
        EnsureImageSlots();
        PlayProgressTracker.RegisterBubblePopTotalItems(GetLevelCount());
        ApplyLevelImages();
        BindButtons();
        RefreshLocks();
    }

    private void OnEnable()
    {
        RefreshLocks();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    [ContextMenu("Refresh Locks")]
    public void RefreshLocks()
    {
        int highestUnlocked = HighestUnlockedLevelIndex;
        ApplyLevelImages();

        for (int i = 0; i < bubbleButtons.Count; i++)
        {
            Button button = bubbleButtons[i];
            bool unlocked = i <= highestUnlocked;

            if (button != null)
            {
                button.interactable = unlocked;
                Image image = button.targetGraphic as Image;
                if (image == null)
                {
                    image = button.GetComponent<Image>();
                }

                if (image != null)
                {
                    ApplyBubbleShellVisual(image, i, unlocked);
                }
            }

            if (i < lockOverlays.Count && lockOverlays[i] != null)
            {
                lockOverlays[i].SetActive(!unlocked);
            }
        }

        if (totalScoreText != null)
        {
            totalScoreText.text = GetTotalBestScore().ToString();
        }
    }

    public void OpenLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= GetLevelCount() || levelIndex > HighestUnlockedLevelIndex)
        {
            return;
        }

        Sprite levelImage = levelIndex < levelImages.Count ? levelImages[levelIndex] : null;
        PlayerPrefs.SetInt(SelectedLevelIndexKey, levelIndex);
        PlayerPrefs.SetString(SelectedLevelImageNameKey, levelImage != null ? levelImage.name : string.Empty);
        PlayerPrefs.Save();

        SmoothSceneLoader.LoadScene(bubbleGameSceneName);
    }

    public static int GetSelectedLevelIndex()
    {
        return PlayerPrefs.GetInt(SelectedLevelIndexKey, 0);
    }

    public static string GetSelectedLevelImageName()
    {
        return PlayerPrefs.GetString(SelectedLevelImageNameKey, string.Empty);
    }

    public static void CompleteSelectedLevel(int score)
    {
        CompleteLevel(GetSelectedLevelIndex(), score);
    }

    public static void CompleteLevel(int levelIndex, int score)
    {
        if (levelIndex < 0)
        {
            return;
        }

        string scoreKey = BestScorePrefix + levelIndex;
        PlayerPrefs.SetInt(CompletedLevelPrefix + levelIndex, 1);

        int bestScore = PlayerPrefs.GetInt(scoreKey, 0);
        if (score > bestScore)
        {
            PlayerPrefs.SetInt(scoreKey, score);
        }

        int highestUnlocked = PlayerPrefs.GetInt(UnlockedLevelIndexKey, 0);
        if (levelIndex >= highestUnlocked)
        {
            PlayerPrefs.SetInt(UnlockedLevelIndexKey, levelIndex + 1);
        }

        PlayerPrefs.Save();
    }

    public static bool IsLevelCompleted(int levelIndex)
    {
        return levelIndex >= 0 && PlayerPrefs.GetInt(CompletedLevelPrefix + levelIndex, 0) == 1;
    }

    public static int GetCompletedLevelCount(int totalLevels)
    {
        int completedCount = 0;
        int safeTotal = Mathf.Max(0, totalLevels);

        for (int i = 0; i < safeTotal; i++)
        {
            if (IsLevelCompleted(i))
            {
                completedCount++;
            }
        }

        return completedCount;
    }

    public static float GetProgress01(int totalLevels)
    {
        if (totalLevels <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)GetCompletedLevelCount(totalLevels) / totalLevels);
    }

    public int GetTotalBestScore()
    {
        int total = 0;
        for (int i = 0; i < GetLevelCount(); i++)
        {
            total += PlayerPrefs.GetInt(BestScorePrefix + i, 0);
        }

        return total;
    }

    [ContextMenu("Reset Bubble Progress")]
    public void ResetProgress()
    {
        PlayProgressTracker.RegisterBubblePopTotalItems(GetLevelCount());
        PlayerPrefs.SetInt(UnlockedLevelIndexKey, 0);
        PlayerPrefs.DeleteKey(SelectedLevelIndexKey);
        PlayerPrefs.DeleteKey(SelectedLevelImageNameKey);

        for (int i = 0; i < GetLevelCount(); i++)
        {
            PlayerPrefs.DeleteKey(BestScorePrefix + i);
            PlayerPrefs.DeleteKey(CompletedLevelPrefix + i);
        }

        PlayerPrefs.Save();
        RefreshLocks();
    }

    private void BindButtons()
    {
        UnbindButtons();

        for (int i = 0; i < bubbleButtons.Count; i++)
        {
            int levelIndex = i;
            UnityAction action = () => OpenLevel(levelIndex);
            clickActions.Add(action);

            if (bubbleButtons[i] != null)
            {
                bubbleButtons[i].onClick.AddListener(action);
            }
        }
    }

    private void UnbindButtons()
    {
        for (int i = 0; i < bubbleButtons.Count && i < clickActions.Count; i++)
        {
            if (bubbleButtons[i] != null)
            {
                bubbleButtons[i].onClick.RemoveListener(clickActions[i]);
            }
        }

        clickActions.Clear();
    }

    private void AutoFillButtonsIfEmpty()
    {
        if (bubbleButtons.Count > 0)
        {
            return;
        }

        Transform root = bubblesRoot != null ? bubblesRoot : transform;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        bubbleButtons.AddRange(buttons);
    }

    private void EnsureImageSlots()
    {
        while (levelImages.Count < bubbleButtons.Count)
        {
            levelImages.Add(null);
        }
    }

    private void ApplyLevelImages()
    {
        for (int i = 0; i < bubbleButtons.Count; i++)
        {
            if (bubbleButtons[i] == null)
            {
                continue;
            }

            Image contentImage = GetBubbleContentImage(bubbleButtons[i]);
            if (contentImage == null)
            {
                continue;
            }

            Sprite levelImage = i < levelImages.Count ? levelImages[i] : null;
            contentImage.sprite = levelImage;
            contentImage.enabled = levelImage != null;
            contentImage.gameObject.SetActive(levelImage != null);
            contentImage.preserveAspect = true;
            contentImage.raycastTarget = false;
        }
    }

    private Image GetBubbleContentImage(Button button)
    {
        Image buttonImage = button.targetGraphic as Image;
        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i] != buttonImage)
            {
                return images[i];
            }
        }

        return null;
    }

    private void ApplyBubbleShellVisual(Image shellImage, int index, bool unlocked)
    {
        if (shellImage == null)
        {
            return;
        }

        if (useGeneratedColoredBubbles)
        {
            shellImage.sprite = GetGeneratedBubbleSprite();
            shellImage.color = GetBubbleShellColor(index, unlocked);
        }
        else
        {
            if (assignedBubbleSprite != null)
            {
                shellImage.sprite = assignedBubbleSprite;
            }

            shellImage.color = unlocked ? unlockedColor : lockedColor;
        }

        shellImage.preserveAspect = true;
    }

    private Color GetBubbleShellColor(int index, bool unlocked)
    {
        Color baseColor = unlockedColor;
        if (bubbleShellColors != null && bubbleShellColors.Count > 0)
        {
            baseColor = bubbleShellColors[Mathf.Abs(index) % bubbleShellColors.Count];
        }

        if (unlocked)
        {
            return baseColor;
        }

        Color dimmed = Color.Lerp(baseColor, lockedColor, 0.55f);
        dimmed.a = Mathf.Min(baseColor.a, lockedColor.a);
        return dimmed;
    }

    private Sprite GetGeneratedBubbleSprite()
    {
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
        texture.name = "Generated Menu Bubble";

        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.43f;
        float innerRadius = radius * 0.72f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 point = new Vector2(x, y);
                float distance = Vector2.Distance(point, center);
                float fill = Mathf.InverseLerp(radius, innerRadius, distance);
                float outline = Mathf.Clamp01(1f - Mathf.Abs(distance - (radius - 2f)) / 4f);
                float highlight = Mathf.Clamp01(1f - Vector2.Distance(point, center + new Vector2(-22f, 24f)) / 16f);
                float lowerGlow = Mathf.Clamp01(1f - Vector2.Distance(point, center + new Vector2(18f, -22f)) / 30f);

                Color color = new Color(1f, 1f, 1f, 0.28f * fill);
                color = Color.Lerp(color, new Color(1f, 1f, 1f, 0.86f), highlight * 0.85f);
                color = Color.Lerp(color, new Color(1f, 1f, 1f, 0.42f), lowerGlow * 0.3f);
                color.a = Mathf.Max(color.a, outline * 0.72f);

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

    private int GetLevelCount()
    {
        return Mathf.Max(bubbleButtons.Count, levelImages.Count);
    }
}
 
