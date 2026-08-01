using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TracingSequence", menuName = "Tracing Letters/Tracing Sequence")]
public class TracingSequenceAsset : ScriptableObject
{
    [SerializeField] private List<LetterSequence> letters = new List<LetterSequence>();

    public LetterSequence GetLetter(int letterNumber)
    {
        for (int i = 0; i < letters.Count; i++)
        {
            if (letters[i] != null && letters[i].LetterNumber == letterNumber)
            {
                return letters[i];
            }
        }

        return null;
    }
}

[System.Serializable]
public class LetterSequence
{
    [Tooltip("1-based letter number. This should match the Design/Dotted Letters sprite number.")]
    [SerializeField] private int letterNumber = 1;

    [Tooltip("Ordered stroke steps for this letter. Designers can add 3, 4, 5, or however many this letter needs.")]
    [SerializeField] private List<TracingStrokeStep> strokeSteps = new List<TracingStrokeStep>();

    public int LetterNumber => letterNumber;
    public IReadOnlyList<TracingStrokeStep> StrokeSteps => strokeSteps;

    public bool HasSteps => strokeSteps != null && strokeSteps.Count > 0;

    public TracingStrokeStep GetStep(int index)
    {
        if (strokeSteps == null || index < 0 || index >= strokeSteps.Count)
        {
            return null;
        }

        return strokeSteps[index];
    }
}

[System.Serializable]
public class TracingStrokeStep
{
    [Tooltip("Designer-facing name, e.g. 'Top curve', 'Vertical line', 'Bottom hook'.")]
    [SerializeField] private string stepName = "Stroke";

    [Tooltip("Transparent PNG/Sprite that marks the allowed area for this sequence step. Non-transparent pixels are traceable.")]
    [SerializeField] private Sprite allowedAreaMask;

    [Tooltip("Optional per-step completion threshold. Use 0 to keep PenDrawer's default threshold.")]
    [Range(0f, 1f)]
    [SerializeField] private float completionThresholdOverride = 0f;

    public string StepName => stepName;
    public Sprite AllowedAreaMask => allowedAreaMask;
    public float CompletionThresholdOverride => completionThresholdOverride;
}
