using UnityEngine;
using UnityEngine.SceneManagement;

public class BubblePopGameCompletion : MonoBehaviour
{
    [SerializeField] private BubblePopGameManager gameManager;
    [SerializeField] private int targetScoreToComplete = 10;
    [SerializeField] private bool completeAutomaticallyAtTargetScore = true;
    [SerializeField] private string menuSceneName = "MainScreen";

    private bool completed;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<BubblePopGameManager>();
        }
    }

    private void Update()
    {
        if (!completeAutomaticallyAtTargetScore || completed || gameManager == null)
        {
            return;
        }

        if (gameManager.Score >= targetScoreToComplete)
        {
            CompleteLevel();
        }
    }

    [ContextMenu("Complete Level")]
    public void CompleteLevel()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        int score = gameManager != null ? gameManager.Score : 0;
        BubblePopLevelMenu.CompleteSelectedLevel(score);

        if (!string.IsNullOrWhiteSpace(menuSceneName))
        {
            SmoothSceneLoader.LoadScene(menuSceneName);
        }
    }
}
