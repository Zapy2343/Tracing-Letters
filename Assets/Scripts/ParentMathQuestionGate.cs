using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Simple parent gate that asks addition or multiplication questions before showing reports.
/// </summary>
public class ParentMathQuestionGate : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField] private CanvasGroup parentCanvasGroup;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private GameObject reportsPanel;

    [Header("Question UI")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button[] answerButtons = new Button[4];
    [SerializeField] private TMP_Text[] answerLabels = new TMP_Text[4];

    [Header("Question Range")]
    [SerializeField] private int minimumValue = 1;
    [SerializeField] private int maximumValue = 10;

    private readonly List<int> currentAnswers = new List<int>(4);
    private readonly List<UnityAction> answerButtonActions = new List<UnityAction>(4);
    private int correctAnswer;
    private bool wasParentMenuVisible;

    private void Awake()
    {
        if (parentCanvasGroup == null)
        {
            parentCanvasGroup = GetComponent<CanvasGroup>();
        }

        BindAnswerButtons();
    }

    private void OnEnable()
    {
        BindAnswerButtons();
        wasParentMenuVisible = IsParentMenuVisible();

        if (wasParentMenuVisible)
        {
            ResetParentGate();
        }
    }

    private void OnDisable()
    {
        UnbindAnswerButtons();
    }

    private void Update()
    {
        bool isVisible = IsParentMenuVisible();
        if (isVisible && !wasParentMenuVisible)
        {
            ResetParentGate();
        }

        wasParentMenuVisible = isVisible;
    }

    [ContextMenu("Refresh Question")]
    public void RefreshQuestion()
    {
        int firstValue = Random.Range(minimumValue, maximumValue + 1);
        int secondValue = Random.Range(minimumValue, maximumValue + 1);
        bool useAddition = Random.value < 0.5f;

        correctAnswer = useAddition ? firstValue + secondValue : firstValue * secondValue;
        if (questionText != null)
        {
            questionText.text = useAddition ? $"{firstValue} + {secondValue} = ?" : $"{firstValue} x {secondValue} = ?";
        }

        BuildAnswerOptions();
        ApplyAnswerLabels();
    }

    public void ResetParentGate()
    {
        if (reportsPanel != null)
        {
            reportsPanel.SetActive(false);
        }

        if (questionPanel != null)
        {
            questionPanel.SetActive(true);
        }

        RefreshQuestion();
    }

    private void HandleAnswerClicked(int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex >= currentAnswers.Count)
        {
            RefreshQuestion();
            return;
        }

        if (currentAnswers[buttonIndex] == correctAnswer)
        {
            if (questionPanel != null)
            {
                questionPanel.SetActive(false);
            }

            if (reportsPanel != null)
            {
                reportsPanel.SetActive(true);
            }

            return;
        }

        RefreshQuestion();
    }

    private void BindAnswerButtons()
    {
        UnbindAnswerButtons();

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int buttonIndex = i;
            UnityAction action = () => HandleAnswerClicked(buttonIndex);
            answerButtonActions.Add(action);

            if (answerButtons[i] != null)
            {
                answerButtons[i].onClick.AddListener(action);
            }
        }
    }

    private void UnbindAnswerButtons()
    {
        for (int i = 0; i < answerButtons.Length && i < answerButtonActions.Count; i++)
        {
            if (answerButtons[i] != null)
            {
                answerButtons[i].onClick.RemoveListener(answerButtonActions[i]);
            }
        }

        answerButtonActions.Clear();
    }

    private void BuildAnswerOptions()
    {
        currentAnswers.Clear();
        currentAnswers.Add(correctAnswer);

        int guard = 0;
        while (currentAnswers.Count < answerButtons.Length && guard < 100)
        {
            int wrongAnswer = Mathf.Max(0, correctAnswer + Random.Range(-10, 11));
            if (wrongAnswer != correctAnswer && !currentAnswers.Contains(wrongAnswer))
            {
                currentAnswers.Add(wrongAnswer);
            }

            guard++;
        }

        while (currentAnswers.Count < answerButtons.Length)
        {
            int fallbackAnswer = correctAnswer + currentAnswers.Count + 1;
            if (!currentAnswers.Contains(fallbackAnswer))
            {
                currentAnswers.Add(fallbackAnswer);
            }
        }

        Shuffle(currentAnswers);
    }

    private void ApplyAnswerLabels()
    {
        for (int i = 0; i < answerLabels.Length && i < currentAnswers.Count; i++)
        {
            if (answerLabels[i] != null)
            {
                answerLabels[i].text = currentAnswers[i].ToString();
            }
        }
    }

    private bool IsParentMenuVisible()
    {
        return parentCanvasGroup == null || (parentCanvasGroup.alpha > 0f && parentCanvasGroup.gameObject.activeInHierarchy);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private void OnValidate()
    {
        minimumValue = Mathf.Max(0, minimumValue);
        maximumValue = Mathf.Max(minimumValue, maximumValue);
    }
}
