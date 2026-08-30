using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Place this script on any UI Button (or UI GameObject) to play an AudioSource / AudioClip when clicked.
/// Non-singleton, reusable component.
/// </summary>
public class ButtonClickSound : MonoBehaviour, IPointerClickHandler
{
    [Header("Audio Settings")]
    [Tooltip("The audio clip played when this button is clicked.")]
    [SerializeField] private AudioClip clickClip;

    [Range(0f, 1f)]
    [SerializeField] private float volumeScale = 1f;

    [Header("Auto Listeners")]
    [Tooltip("If true, automatically hooks into the attached UI Button component on Awake.")]
    [SerializeField] private bool autoBindToButton = true;

    private Button targetButton;

    private void Awake()
    {
        if (autoBindToButton)
        {
            targetButton = GetComponent<Button>();
            if (targetButton != null)
            {
                targetButton.onClick.AddListener(PlaySound);
            }
        }
    }

    private void OnDestroy()
    {
        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(PlaySound);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // If not using Button component auto-binding, play on pointer click directly
        if (targetButton == null)
        {
            PlaySound();
        }
    }

    [ContextMenu("Play Click Sound")]
    public void PlaySound()
    {
        if (clickClip == null)
        {
            return;
        }

        // 1. Try GlobalSoundManager
        if (GlobalSoundManager.Instance != null)
        {
            if (!GlobalSoundManager.Instance.SoundEnabled) return;

            GlobalSoundManager.Instance.PlaySfx(clickClip, volumeScale);
        }
    }
}
