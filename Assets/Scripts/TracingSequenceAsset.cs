using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TracingSequence", menuName = "Tracing Letters/Tracing Sequence")]
public class TracingSequenceAsset : ScriptableObject
{
    [Tooltip("Main database list. Assign one separate Tracing Letter asset for each letter.")]
    [SerializeField] private List<TracingLetterAsset> letterAssets = new List<TracingLetterAsset>();

    [HideInInspector]
    [SerializeField] private List<LetterSequence> letters = new List<LetterSequence>();

    public LetterSequence GetLetter(int letterNumber)
    {
        TracingLetterAsset letterAsset = GetLetterAsset(letterNumber);
        if (letterAsset != null)
        {
            return letterAsset.Letter;
        }

        for (int i = 0; i < letters.Count; i++)
        {
            if (letters[i] != null && letters[i].LetterNumber == letterNumber)
            {
                return letters[i];
            }
        }

        return null;
    }

    public TracingLetterAsset GetLetterAsset(int letterNumber)
    {
        for (int i = 0; i < letterAssets.Count; i++)
        {
            if (letterAssets[i] != null && letterAssets[i].LetterNumber == letterNumber)
            {
                return letterAssets[i];
            }
        }

        return null;
    }

    public IReadOnlyList<TracingLetterAsset> LetterAssets => letterAssets;

#if UNITY_EDITOR
    public int LegacyLetterCount => letters != null ? letters.Count : 0;

    public LetterSequence GetLegacyLetterAt(int index)
    {
        if (letters == null || index < 0 || index >= letters.Count)
        {
            return null;
        }

        return letters[index];
    }

    public void SetLetterAsset(int index, TracingLetterAsset letterAsset)
    {
        if (letterAssets == null)
        {
            letterAssets = new List<TracingLetterAsset>();
        }

        while (letterAssets.Count <= index)
        {
            letterAssets.Add(null);
        }

        letterAssets[index] = letterAsset;
    }
#endif
}

[System.Serializable]
public class LetterSequence
{
    [Tooltip("1-based letter number. This should match the Design/Dotted Letters sprite number.")]
    [SerializeField] private int letterNumber = 1;

    [Tooltip("Default correct image used by bubble-choice quizzes after this letter is traced.")]
    [SerializeField] private Sprite bubbleCorrectImage;

    [Tooltip("Ordered stroke steps for this letter. Designers can add 3, 4, 5, or however many this letter needs.")]
    [SerializeField] private List<TracingStrokeStep> strokeSteps = new List<TracingStrokeStep>();

    public int LetterNumber => letterNumber;
    public Sprite BubbleCorrectImage => bubbleCorrectImage;
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

#if UNITY_EDITOR
    public void SetLetterNumber(int value)
    {
        letterNumber = Mathf.Max(1, value);
    }

    public void CopyFrom(LetterSequence source, int fallbackLetterNumber)
    {
        if (source == null)
        {
            letterNumber = Mathf.Max(1, fallbackLetterNumber);
            bubbleCorrectImage = null;
            strokeSteps = new List<TracingStrokeStep>();
            return;
        }

        letterNumber = Mathf.Max(1, source.LetterNumber > 0 ? source.LetterNumber : fallbackLetterNumber);
        bubbleCorrectImage = source.BubbleCorrectImage;
        strokeSteps = new List<TracingStrokeStep>();

        IReadOnlyList<TracingStrokeStep> sourceSteps = source.StrokeSteps;
        if (sourceSteps == null)
        {
            return;
        }

        for (int i = 0; i < sourceSteps.Count; i++)
        {
            TracingStrokeStep copiedStep = new TracingStrokeStep();
            copiedStep.CopyFrom(sourceSteps[i]);
            strokeSteps.Add(copiedStep);
        }
    }
#endif
}

[System.Serializable]
public class TracingStrokeStep
{
    [Tooltip("Designer-facing name, e.g. 'Top curve', 'Vertical line', 'Bottom hook'.")]
    [SerializeField] private string stepName = "Stroke";

    [Tooltip("Transparent PNG/Sprite that marks the allowed area for this sequence step. Non-transparent pixels are traceable.")]
    [SerializeField] private Sprite allowedAreaMask;

    [Tooltip("Optional hand hint path in the active letter image's local UI space. 2 points = line, 3+ points = smooth curve through the points. Leave empty to auto-generate from the stroke mask.")]
    [SerializeField] private List<Vector2> hintPathPoints = new List<Vector2>();

    [Tooltip("How many points to generate along the custom smooth hint path.")]
    [Range(6, 48)]
    [SerializeField] private int hintPathResolution = 18;

    [Tooltip("Optional per-step completion threshold. Use 0 to keep PenDrawer's default threshold.")]
    [Range(0f, 1f)]
    [SerializeField] private float completionThresholdOverride = 0f;

    public string StepName => stepName;
    public Sprite AllowedAreaMask => allowedAreaMask;
    public IReadOnlyList<Vector2> HintPathPoints => hintPathPoints;
    public int HintPathResolution => hintPathResolution;
    public bool HasCustomHintPath => hintPathPoints != null && hintPathPoints.Count >= 2;
    public int HintPathPointCount => hintPathPoints != null ? hintPathPoints.Count : 0;
    public float CompletionThresholdOverride => completionThresholdOverride;

    public bool TryBuildHintPath(List<Vector2> outputPath)
    {
        if (outputPath == null)
        {
            return false;
        }

        outputPath.Clear();

        if (!HasCustomHintPath)
        {
            return false;
        }

        if (hintPathPoints.Count == 2)
        {
            outputPath.Add(hintPathPoints[0]);
            outputPath.Add(hintPathPoints[1]);
            return true;
        }

        int segmentCount = hintPathPoints.Count - 1;
        int stepsPerSegment = Mathf.Max(2, Mathf.CeilToInt((float)Mathf.Max(2, hintPathResolution) / segmentCount));

        for (int segment = 0; segment < segmentCount; segment++)
        {
            Vector2 p0 = hintPathPoints[Mathf.Max(segment - 1, 0)];
            Vector2 p1 = hintPathPoints[segment];
            Vector2 p2 = hintPathPoints[segment + 1];
            Vector2 p3 = hintPathPoints[Mathf.Min(segment + 2, hintPathPoints.Count - 1)];

            for (int i = 0; i <= stepsPerSegment; i++)
            {
                if (segment > 0 && i == 0)
                {
                    continue;
                }

                float t = (float)i / stepsPerSegment;
                outputPath.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
            }
        }

        return outputPath.Count >= 2;
    }

    private static Vector2 EvaluateCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    public Vector2 GetHintPathPoint(int index)
    {
        if (hintPathPoints == null || index < 0 || index >= hintPathPoints.Count)
        {
            return Vector2.zero;
        }

        return hintPathPoints[index];
    }

#if UNITY_EDITOR
    public void CopyFrom(TracingStrokeStep source)
    {
        if (source == null)
        {
            stepName = "Stroke";
            allowedAreaMask = null;
            hintPathPoints = new List<Vector2>();
            hintPathResolution = 18;
            completionThresholdOverride = 0f;
            return;
        }

        stepName = source.StepName;
        allowedAreaMask = source.AllowedAreaMask;
        hintPathPoints = new List<Vector2>();
        if (source.HintPathPoints != null)
        {
            hintPathPoints.AddRange(source.HintPathPoints);
        }

        hintPathResolution = source.HintPathResolution;
        completionThresholdOverride = source.CompletionThresholdOverride;
    }

    public void SetHintPathPoint(int index, Vector2 point)
    {
        if (hintPathPoints == null || index < 0 || index >= hintPathPoints.Count)
        {
            return;
        }

        hintPathPoints[index] = point;
    }

    public void AddHintPathPoint(Vector2 point)
    {
        if (hintPathPoints == null)
        {
            hintPathPoints = new List<Vector2>();
        }

        hintPathPoints.Add(point);
    }

    public void RemoveLastHintPathPoint()
    {
        if (hintPathPoints == null || hintPathPoints.Count == 0)
        {
            return;
        }

        hintPathPoints.RemoveAt(hintPathPoints.Count - 1);
    }

    public void ClearHintPath()
    {
        if (hintPathPoints == null)
        {
            hintPathPoints = new List<Vector2>();
            return;
        }

        hintPathPoints.Clear();
    }

    public void ReplaceHintPath(IEnumerable<Vector2> points)
    {
        if (hintPathPoints == null)
        {
            hintPathPoints = new List<Vector2>();
        }

        hintPathPoints.Clear();
        if (points == null)
        {
            return;
        }

        hintPathPoints.AddRange(points);
    }

    public void CreateDefaultHintPath(Rect localRect)
    {
        if (hintPathPoints == null)
        {
            hintPathPoints = new List<Vector2>();
        }

        hintPathPoints.Clear();
        hintPathPoints.Add(new Vector2(localRect.xMin + localRect.width * 0.25f, localRect.center.y));
        hintPathPoints.Add(new Vector2(localRect.center.x, localRect.yMax - localRect.height * 0.15f));
        hintPathPoints.Add(new Vector2(localRect.xMax - localRect.width * 0.25f, localRect.center.y));
    }
#endif
}
