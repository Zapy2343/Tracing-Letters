using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ProgressStatsDisplay : MonoBehaviour
{
    [Header("Text Targets")]
    [SerializeField] private TMP_Text totalPlayTimeText;
    [SerializeField] private TMP_Text overallProgressText;
    [SerializeField] private TMP_Text tracingProgressText;
    [SerializeField] private TMP_Text bubblePopProgressText;

    [Header("Slider Targets")]
    [SerializeField] private Slider overallProgressSlider;
    [SerializeField] private Slider tracingProgressSlider;
    [FormerlySerializedAs("bubblePopProgresSlider")]
    [SerializeField] private Slider bubblePopProgressSlider;

    [Header("Refresh")]
    [SerializeField] private bool refreshContinuously = true;
    [SerializeField] private float refreshIntervalSeconds = 0.25f;

    private float refreshTimer;

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        if (!refreshContinuously)
        {
            return;
        }

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < Mathf.Max(0.05f, refreshIntervalSeconds))
        {
            return;
        }

        refreshTimer = 0f;
        Refresh();
    }

    [ContextMenu("Refresh Progress Stats")]
    public void Refresh()
    {
        int tracingTotal = PlayProgressTracker.TracingTotalItems;
        int bubblePopTotal = PlayProgressTracker.BubblePopTotalItems;
        float overallProgress = PlayProgressTracker.GetOverallProgress01(tracingTotal, bubblePopTotal);
        float tracingProgress = PlayProgressTracker.GetTracingProgress01(tracingTotal);
        float bubblePopProgress = PlayProgressTracker.GetBubblePopProgress01(bubblePopTotal);

        if (totalPlayTimeText != null)
        {
            totalPlayTimeText.text = PlayProgressTracker.FormatPlayTime(PlayProgressTracker.TotalPlayTimeSeconds);
        }

        if (overallProgressText != null)
        {
            overallProgressText.text = FormatPercent(overallProgress);
        }

        if (tracingProgressText != null)
        {
            tracingProgressText.text = FormatPercent(tracingProgress);
        }

        if (bubblePopProgressText != null)
        {
            bubblePopProgressText.text = FormatPercent(bubblePopProgress);
        }

        SetSliderValue(overallProgressSlider, overallProgress);
        SetSliderValue(tracingProgressSlider, tracingProgress);
        SetSliderValue(bubblePopProgressSlider, bubblePopProgress);
    }

    private string FormatPercent(float progress01)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp01(progress01) * 100f)}%";
    }

    private void SetSliderValue(Slider slider, float progress01)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = Mathf.Clamp01(progress01);
    }
}
