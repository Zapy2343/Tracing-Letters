using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated component to display the combined total score from the Tracing Letter game and Bubble POP game.
/// Can be attached to any UI GameObject in mainScreen or menus.
/// </summary>
public class CombinedTotalScoreDisplay : MonoBehaviour
{
    [Header("Text Target (Assign either TMP_Text or UI Text)")]
    [SerializeField] private TMP_Text tmpScoreText;
    [SerializeField] private Text uiScoreText;

    [Header("Format Settings")]
    [SerializeField] private string prefix = "";
    [SerializeField] private string suffix = "";
    [SerializeField] private bool useNumberFormatting = false;

    [Header("Auto Refresh")]
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool refreshContinuously = true;
    [SerializeField] private float refreshIntervalSeconds = 0.5f;

    private float refreshTimer;

    private void Awake()
    {
        AutoDetectTextComponent();
    }

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            RefreshScoreDisplay();
        }
    }

    private void Update()
    {
        if (!refreshContinuously)
        {
            return;
        }

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= Mathf.Max(0.1f, refreshIntervalSeconds))
        {
            refreshTimer = 0f;
            RefreshScoreDisplay();
        }
    }

    [ContextMenu("Refresh Combined Total Score")]
    public void RefreshScoreDisplay()
    {
        AutoDetectTextComponent();

        int tracingScore = KaKhaTracingProgress.GetTotalScore();
        int bubblePopScore = KaKhaTracingProgress.GetTotalBubblePopScore();
        int combinedTotal = tracingScore + bubblePopScore;

        string formattedScore = useNumberFormatting ? combinedTotal.ToString("N0") : combinedTotal.ToString();
        string displayText = $"{prefix}{formattedScore}{suffix}";

        if (tmpScoreText != null)
        {
            tmpScoreText.text = displayText;
        }

        if (uiScoreText != null)
        {
            uiScoreText.text = displayText;
        }
    }

    private void AutoDetectTextComponent()
    {
        if (tmpScoreText == null && uiScoreText == null)
        {
            tmpScoreText = GetComponent<TMP_Text>();
            if (tmpScoreText == null)
            {
                uiScoreText = GetComponent<Text>();
            }
        }
    }

    public static int GetCombinedTotalScore()
    {
        return KaKhaTracingProgress.GetTotalScore() + KaKhaTracingProgress.GetTotalBubblePopScore();
    }
}
