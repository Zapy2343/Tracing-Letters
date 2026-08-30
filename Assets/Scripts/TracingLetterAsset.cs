using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TracingLetter", menuName = "Tracing Letters/Tracing Letter")]
public class TracingLetterAsset : ScriptableObject
{
    [SerializeField] private LetterSequence letter = new LetterSequence();

    public LetterSequence Letter => letter;
    public int LetterNumber => letter != null ? letter.LetterNumber : 0;
    public Sprite BubbleCorrectImage => letter != null ? letter.BubbleCorrectImage : null;
    public IReadOnlyList<TracingStrokeStep> StrokeSteps => letter != null ? letter.StrokeSteps : null;
    public bool HasSteps => letter != null && letter.HasSteps;
    public TracingStrokeStep GetStep(int index) => letter != null ? letter.GetStep(index) : null;

#if UNITY_EDITOR
    public void CopyFrom(LetterSequence source, int fallbackLetterNumber)
    {
        if (letter == null)
        {
            letter = new LetterSequence();
        }

        letter.CopyFrom(source, fallbackLetterNumber);
    }

    public void SetLetterNumber(int letterNumber)
    {
        if (letter == null)
        {
            letter = new LetterSequence();
        }

        letter.SetLetterNumber(letterNumber);
    }
#endif
}
