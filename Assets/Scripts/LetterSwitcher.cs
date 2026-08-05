using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls switching between matching Design Letters and Dotted Letters sprites synchronously
/// using Left and Right arrow keys or UI buttons, and automatically clears mask/tracing strokes on switch.
/// </summary>
public class LetterSwitcher : MonoBehaviour
{
    private const string SelectedTracingLetterNumberKey = "ka_kha_selected_letter_number";

    [Header("UI / Game Object References")]
    [Tooltip("Reference to the GameObject or UI Image for the Design Letter.")]
    [SerializeField] private GameObject designLetterObject;

    [Tooltip("Reference to the GameObject or UI Image for the Dotted Letter.")]
    [SerializeField] private GameObject dottedLetterObject;

    [Header("Pen & FX References (Optional)")]
    [Tooltip("Reference to PenDrawer script to automatically clear mask/strokes on letter switch. Auto-detected if empty.")]
    [SerializeField] private PenDrawer penDrawer;

    [Tooltip("Reference to TracingFXManager for transition FX. Auto-detected if empty.")]
    [SerializeField] private TracingFXManager fxManager;

    [Header("Sprite Lists (Optional - Drag & Drop Sprites here)")]
    [Tooltip("List of Design Letter Sprites (e.g. 1.png, 2.png, etc.). If left empty, will attempt to load from Resources/Design Letters folder.")]
    [SerializeField] private List<Sprite> designLetterSprites = new List<Sprite>();

    [Tooltip("List of Dotted Letter Sprites (e.g. 1.png, 2.png, etc.). If left empty, will attempt to load from Resources/Dotted Letters folder.")]
    [SerializeField] private List<Sprite> dottedLetterSprites = new List<Sprite>();

    [Header("Settings")]
    [Tooltip("Total number of letter pairs to attempt loading if using Resources folder.")]
    [SerializeField] private int maxLettersToLoadFromResources = 36;

    [Tooltip("Loop back to the first letter when reaching the end, and vice versa.")]
    [SerializeField] private bool loopNavigation = true;

    [Header("Current Status (Read Only)")]
    [SerializeField] private int currentLetterNumber = 1; // 1-based index for easy inspection (1 = Image 1)

    // Cached component references
    private Image designImage;
    private SpriteRenderer designSpriteRenderer;

    private Image dottedImage;
    private SpriteRenderer dottedSpriteRenderer;

    private void Awake()
    {
        InitializeComponents();
        LoadSpritesIfEmpty();
    }

    private void Start()
    {
        // Auto-detect PenDrawer if not assigned
        if (penDrawer == null)
        {
#if UNITY_2023_1_OR_NEWER
            penDrawer = FindFirstObjectByType<PenDrawer>();
#else
            penDrawer = FindObjectOfType<PenDrawer>();
#endif
        }

        // Auto-detect TracingFXManager if not assigned
        if (fxManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            fxManager = FindFirstObjectByType<TracingFXManager>();
#else
            fxManager = FindObjectOfType<TracingFXManager>();
#endif
        }

        // Set initial letter from the Ka/Kha menu selection without transition effect on start.
        int savedLetterNumber = PlayerPrefs.GetInt(SelectedTracingLetterNumberKey, 1);
        currentLetterNumber = Mathf.Clamp(savedLetterNumber, 1, Mathf.Max(1, GetTotalCount()));
        ApplyCurrentLetter();
    }

    private void Update()
    {
        HandleKeyboardInput();
    }

    /// <summary>
    /// Finds Image or SpriteRenderer components on the target objects.
    /// </summary>
    private void InitializeComponents()
    {
        if (designLetterObject != null)
        {
            designImage = designLetterObject.GetComponent<Image>();
            designSpriteRenderer = designLetterObject.GetComponent<SpriteRenderer>();
        }

        if (dottedLetterObject != null)
        {
            dottedImage = dottedLetterObject.GetComponent<Image>();
            dottedSpriteRenderer = dottedLetterObject.GetComponent<SpriteRenderer>();
        }
    }

    /// <summary>
    /// Loads sprites from Editor folder or Resources folder if list is empty.
    /// </summary>
    private void LoadSpritesIfEmpty()
    {
#if UNITY_EDITOR
        if (designLetterSprites == null || designLetterSprites.Count == 0)
        {
            designLetterSprites = LoadSpritesFromEditorFolder("Assets/Sprites/Design Letters");
        }

        if (dottedLetterSprites == null || dottedLetterSprites.Count == 0)
        {
            dottedLetterSprites = LoadSpritesFromEditorFolder("Assets/Sprites/Dotted Letters");
        }
#endif

        if (designLetterSprites == null || designLetterSprites.Count == 0)
        {
            designLetterSprites = LoadSpritesFromResources("Design Letters");
        }

        if (dottedLetterSprites == null || dottedLetterSprites.Count == 0)
        {
            dottedLetterSprites = LoadSpritesFromResources("Dotted Letters");
        }
    }

#if UNITY_EDITOR
    private List<Sprite> LoadSpritesFromEditorFolder(string folderPath)
    {
        List<Sprite> loaded = new List<Sprite>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        List<string> paths = new List<string>();
        foreach (string g in guids) paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(g));

        paths.Sort((a, b) => {
            int numA = ExtractNumberFromFileName(System.IO.Path.GetFileNameWithoutExtension(a));
            int numB = ExtractNumberFromFileName(System.IO.Path.GetFileNameWithoutExtension(b));
            return numA.CompareTo(numB);
        });

        foreach (string path in paths)
        {
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) loaded.Add(s);
        }
        return loaded;
    }

    private int ExtractNumberFromFileName(string name)
    {
        int val = 0;
        int.TryParse(name, out val);
        return val;
    }
#endif

    private List<Sprite> LoadSpritesFromResources(string folderName)
    {
        List<Sprite> loaded = new List<Sprite>();
        for (int i = 1; i <= maxLettersToLoadFromResources; i++)
        {
            Sprite sprite = Resources.Load<Sprite>($"{folderName}/{i}");
            if (sprite != null)
            {
                loaded.Add(sprite);
            }
            else
            {
                break;
            }
        }
        return loaded;
    }

    /// <summary>
    /// Handles Keyboard input for arrow keys.
    /// </summary>
    private void HandleKeyboardInput()
    {
        bool rightPressed = false;
        bool leftPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) rightPressed = true;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame) leftPressed = true;
        }
#endif

        if (!rightPressed && !leftPressed)
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.RightArrow)) rightPressed = true;
                if (Input.GetKeyDown(KeyCode.LeftArrow)) leftPressed = true;
            }
            catch
            {
                // Fallback for Unity Input System if Legacy Input throws exception
            }
        }

        if (rightPressed)
        {
            NextLetter();
        }
        else if (leftPressed)
        {
            PreviousLetter();
        }
    }

    /// <summary>
    /// Switches to the next letter image pair.
    /// </summary>
    public void NextLetter()
    {
        int totalLetters = GetTotalCount();
        if (totalLetters == 0) return;

        if (fxManager != null && fxManager.IsTransitioning) return;

        int nextNumber = currentLetterNumber + 1;
        if (nextNumber > totalLetters)
        {
            nextNumber = loopNavigation ? 1 : totalLetters;
        }

        if (nextNumber == currentLetterNumber) return;

        SetLetterByNumber(nextNumber);
    }

    /// <summary>
    /// Switches to the previous letter image pair.
    /// </summary>
    public void PreviousLetter()
    {
        int totalLetters = GetTotalCount();
        if (totalLetters == 0) return;

        if (fxManager != null && fxManager.IsTransitioning) return;

        int prevNumber = currentLetterNumber - 1;
        if (prevNumber < 1)
        {
            prevNumber = loopNavigation ? totalLetters : 1;
        }

        if (prevNumber == currentLetterNumber) return;

        SetLetterByNumber(prevNumber);
    }

    /// <summary>
    /// Sets a specific letter by 1-based number (e.g. 1 for image 1, 2 for image 2).
    /// </summary>
    public void SetLetterByNumber(int number)
    {
        int totalLetters = GetTotalCount();
        if (totalLetters == 0) return;

        if (fxManager != null && fxManager.IsTransitioning) return;

        int targetNumber = Mathf.Clamp(number, 1, totalLetters);
        currentLetterNumber = targetNumber;

        if (fxManager != null && fxManager.EnableTransitionFX && Application.isPlaying && gameObject.activeInHierarchy)
        {
            fxManager.PlayLetterTransition(() => ApplyCurrentLetter());
        }
        else
        {
            ApplyCurrentLetter();
        }
    }

    public GameObject DesignLetterObject => designLetterObject;
    public GameObject DottedLetterObject => dottedLetterObject;

    private void ApplyCurrentLetter()
    {
        int index = currentLetterNumber - 1; // Convert 1-based to 0-based index

        // 1. Apply Design Letter Sprite
        if (designLetterSprites != null && index >= 0 && index < designLetterSprites.Count)
        {
            Sprite designSprite = designLetterSprites[index];
            if (designImage != null) designImage.sprite = designSprite;
            if (designSpriteRenderer != null) designSpriteRenderer.sprite = designSprite;
        }

        // 2. Apply Dotted Letter Sprite
        if (dottedLetterSprites != null && index >= 0 && index < dottedLetterSprites.Count)
        {
            Sprite dottedSprite = dottedLetterSprites[index];
            if (dottedImage != null) dottedImage.sprite = dottedSprite;
            if (dottedSpriteRenderer != null) dottedSpriteRenderer.sprite = dottedSprite;
        }

        // 3. Clear drawn mask lines on letter switch & sync reveal target graphic
        if (penDrawer != null)
        {
            if (designImage != null)
            {
                penDrawer.SetRevealTargetGraphic(designImage, currentLetterNumber);
            }
            else
            {
                penDrawer.SetCurrentLetterNumber(currentLetterNumber);
                penDrawer.ClearAllLines();
            }
        }
    }

    /// <summary>
    /// Returns the maximum available letters count.
    /// </summary>
    public int GetTotalCount()
    {
        int designCount = designLetterSprites != null ? designLetterSprites.Count : 0;
        int dottedCount = dottedLetterSprites != null ? dottedLetterSprites.Count : 0;
        return Mathf.Max(designCount, dottedCount);
    }
}
