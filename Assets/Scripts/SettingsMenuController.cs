using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls the Settings Menu UI, panel toggle, and Music/Sound ON-OFF states with visual OFF icon toggling.
/// Automatically closes the settings panel when clicking outside or on enable.
/// </summary>
public class SettingsMenuController : MonoBehaviour
{
    [Header("Settings Panel")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;

    [Header("Music UI")]
    [SerializeField] private Button musicButton;
    [Tooltip("The red slash / OFF image icon GameObject. Shown when Music is OFF, hidden when Music is ON.")]
    [SerializeField] private GameObject musicOffIcon;

    [Header("Sound UI")]
    [SerializeField] private Button soundButton;
    [Tooltip("The red slash / OFF image icon GameObject. Shown when Sound is OFF, hidden when Sound is ON.")]
    [SerializeField] private GameObject soundOffIcon;

    [Header("Click Outside & Enable Behavior")]
    [SerializeField] private bool closeOnPointerClickOutside = true;
    [SerializeField] private bool closeOnEnable = true;

    private RectTransform settingsPanelRect;
    private RectTransform settingsButtonRect;
    private bool justOpenedThisFrame = false;

    private void Awake()
    {
        AutoDetectReferences();
        BindButtonListeners();
    }

    private void OnEnable()
    {
        GlobalSoundManager.OnSettingsChanged += RefreshUI;
        AutoDetectReferences();

        if (closeOnEnable)
        {
            CloseSettingsPanel();
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        GlobalSoundManager.OnSettingsChanged -= RefreshUI;
    }

    private void Update()
    {
        if (!closeOnPointerClickOutside || settingsPanel == null || !settingsPanel.activeSelf)
        {
            return;
        }

        if (justOpenedThisFrame)
        {
            justOpenedThisFrame = false;
            return;
        }

        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            Vector2 pointerPos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;

            if (!IsPointerOverRect(settingsPanelRect, pointerPos) && !IsPointerOverRect(settingsButtonRect, pointerPos))
            {
                CloseSettingsPanel();
            }
        }
    }

    public void ToggleSettingsPanel()
    {
        AutoDetectReferences();

        if (settingsPanel != null)
        {
            bool nextState = !settingsPanel.activeSelf;
            if (nextState)
            {
                OpenSettingsPanel();
            }
            else
            {
                CloseSettingsPanel();
            }
        }
    }

    public void OpenSettingsPanel()
    {
        AutoDetectReferences();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
            justOpenedThisFrame = true;
            RefreshUI();
        }
    }

    public void CloseSettingsPanel()
    {
        AutoDetectReferences();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void ToggleMusic()
    {
        if (GlobalSoundManager.Instance != null)
        {
            GlobalSoundManager.Instance.ToggleMusic();
        }

        if (SoundManager.Instance != null)
        {
            bool musicOn = GlobalSoundManager.Instance != null ? GlobalSoundManager.Instance.MusicEnabled : true;
            SoundManager.Instance.SetBGMMuted(!musicOn);
        }

        RefreshUI();
    }

    public void ToggleSound()
    {
        if (GlobalSoundManager.Instance != null)
        {
            GlobalSoundManager.Instance.ToggleSound();
        }

        if (SoundManager.Instance != null)
        {
            bool soundOn = GlobalSoundManager.Instance != null ? GlobalSoundManager.Instance.SoundEnabled : true;
            SoundManager.Instance.SetSFXMuted(!soundOn);
        }

        RefreshUI();
    }

    public void RefreshUI()
    {
        bool musicOn = GlobalSoundManager.Instance != null ? GlobalSoundManager.Instance.MusicEnabled : true;
        bool soundOn = GlobalSoundManager.Instance != null ? GlobalSoundManager.Instance.SoundEnabled : true;

        if (musicOffIcon != null)
        {
            musicOffIcon.SetActive(!musicOn);
        }

        if (soundOffIcon != null)
        {
            soundOffIcon.SetActive(!soundOn);
        }
    }

    private bool IsPointerOverRect(RectTransform rectTransform, Vector2 screenPoint)
    {
        if (rectTransform == null) return false;

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, cam);
    }

    private void BindButtonListeners()
    {
        if (settingsButton == null)
        {
            settingsButton = GetComponent<Button>();
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(ToggleSettingsPanel);
            settingsButton.onClick.AddListener(ToggleSettingsPanel);
        }

        if (musicButton != null)
        {
            musicButton.onClick.RemoveListener(ToggleMusic);
            musicButton.onClick.AddListener(ToggleMusic);
        }

        if (soundButton != null)
        {
            soundButton.onClick.RemoveListener(ToggleSound);
            soundButton.onClick.AddListener(ToggleSound);
        }
    }

    private void AutoDetectReferences()
    {
        if (settingsButton == null)
        {
            settingsButton = GetComponent<Button>();
        }

        if (settingsButton != null)
        {
            settingsButtonRect = settingsButton.GetComponent<RectTransform>();
        }

        if (settingsPanel == null)
        {
            Transform panelTransform = transform.Find("panel");
            if (panelTransform == null)
            {
                panelTransform = transform.Find("Panel");
            }

            if (panelTransform == null && transform.parent != null)
            {
                panelTransform = transform.parent.Find("panel");
                if (panelTransform == null)
                {
                    panelTransform = transform.parent.Find("Panel");
                }
            }

            if (panelTransform != null)
            {
                settingsPanel = panelTransform.gameObject;
            }
        }

        if (settingsPanel != null)
        {
            settingsPanelRect = settingsPanel.GetComponent<RectTransform>();

            if (musicButton == null)
            {
                Transform mBtn = settingsPanel.transform.Find("Music Button");
                if (mBtn != null) musicButton = mBtn.GetComponent<Button>();
            }

            if (soundButton == null)
            {
                Transform sBtn = settingsPanel.transform.Find("Sound Button");
                if (sBtn != null) soundButton = sBtn.GetComponent<Button>();
            }
        }

        if (musicButton != null && musicOffIcon == null)
        {
            Transform offChild = musicButton.transform.Find("Off");
            if (offChild != null)
            {
                musicOffIcon = offChild.gameObject;
            }
        }

        if (soundButton != null && soundOffIcon == null)
        {
            Transform offChild = soundButton.transform.Find("Off");
            if (offChild != null)
            {
                soundOffIcon = offChild.gameObject;
            }
        }
    }
}
