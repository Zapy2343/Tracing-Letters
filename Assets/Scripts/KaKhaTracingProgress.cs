using UnityEngine;

public static class KaKhaTracingProgress
{
    public const string SelectedTracingLetterNumberKey = "ka_kha_selected_letter_number";
    public const string HighestUnlockedLetterNumberKey = "ka_kha_highest_unlocked_letter_number";
    public const string TotalScoreKey = "ka_kha_total_score";
    public const string BestScorePrefix = "ka_kha_best_score_";
    public const string CompletedLetterPrefix = "ka_kha_completed_letter_";

    public static int GetHighestUnlockedLetterNumber(int totalLetters)
    {
        int highestUnlocked = PlayerPrefs.GetInt(HighestUnlockedLetterNumberKey, 1);
        return Mathf.Clamp(highestUnlocked, 1, Mathf.Max(1, totalLetters));
    }

    public static int GetTotalScore()
    {
        return PlayerPrefs.GetInt(TotalScoreKey, 0);
    }

    public static void CompleteLetter(int letterNumber, int scoreToAdd, int totalLetters)
    {
        int safeLetterNumber = Mathf.Clamp(letterNumber, 1, Mathf.Max(1, totalLetters));
        int safeScore = Mathf.Max(0, scoreToAdd);
        string completedKey = CompletedLetterPrefix + safeLetterNumber;

        if (safeScore > 0 && PlayerPrefs.GetInt(completedKey, 0) == 0)
        {
            PlayerPrefs.SetInt(TotalScoreKey, GetTotalScore() + safeScore);
            PlayerPrefs.SetInt(completedKey, 1);
        }

        string bestScoreKey = BestScorePrefix + safeLetterNumber;
        int bestScore = PlayerPrefs.GetInt(bestScoreKey, 0);
        if (safeScore > bestScore)
        {
            PlayerPrefs.SetInt(bestScoreKey, safeScore);
        }

        int highestUnlocked = PlayerPrefs.GetInt(HighestUnlockedLetterNumberKey, 1);
        if (safeLetterNumber >= highestUnlocked && safeLetterNumber < totalLetters)
        {
            PlayerPrefs.SetInt(HighestUnlockedLetterNumberKey, safeLetterNumber + 1);
        }

        PlayerPrefs.Save();
    }

    public static void ResetProgress(int totalLetters)
    {
        PlayerPrefs.DeleteKey(SelectedTracingLetterNumberKey);
        PlayerPrefs.DeleteKey(HighestUnlockedLetterNumberKey);
        PlayerPrefs.DeleteKey(TotalScoreKey);

        for (int i = 1; i <= Mathf.Max(1, totalLetters); i++)
        {
            PlayerPrefs.DeleteKey(BestScorePrefix + i);
            PlayerPrefs.DeleteKey(CompletedLetterPrefix + i);
        }

        PlayerPrefs.Save();
    }
}
