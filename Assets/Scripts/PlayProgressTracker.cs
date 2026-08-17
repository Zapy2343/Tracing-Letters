using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayProgressTracker : MonoBehaviour
{
    public const string TotalPlayTimeSecondsKey = "play_progress_total_play_time_seconds";
    public const string TracingTotalItemsKey = "play_progress_tracing_total_items";
    public const string BubblePopTotalItemsKey = "play_progress_bubble_pop_total_items";

    private const float SaveIntervalSeconds = 5f;
    private static PlayProgressTracker instance;
    private static float unsavedPlayTimeSeconds;
    private static readonly HashSet<string> PlayTimeSceneNames = new HashSet<string>
    {
        "Tracing Letter",
        "Bubble POP"
    };

    private float saveTimer;
    private bool isTrackingPlayTime;

    public static float TotalPlayTimeSeconds => PlayerPrefs.GetFloat(TotalPlayTimeSecondsKey, 0f) + unsavedPlayTimeSeconds;

    public static int TracingTotalItems => PlayerPrefs.GetInt(TracingTotalItemsKey, 0);
    public static int BubblePopTotalItems => PlayerPrefs.GetInt(BubblePopTotalItemsKey, 0);

    public static float TracingProgress01 => GetTracingProgress01(TracingTotalItems);
    public static float BubblePopProgress01 => GetBubblePopProgress01(BubblePopTotalItems);
    public static float OverallProgress01 => GetOverallProgress01(TracingTotalItems, BubblePopTotalItems);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeTracker()
    {
        if (instance != null)
        {
            return;
        }

        GameObject trackerObject = new GameObject(nameof(PlayProgressTracker));
        instance = trackerObject.AddComponent<PlayProgressTracker>();
        DontDestroyOnLoad(trackerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        RefreshPlayTimeSceneState(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    private void Update()
    {
        if (!isTrackingPlayTime)
        {
            return;
        }

        RecordPlayTime(Time.unscaledDeltaTime);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SavePlayTime();
        }
    }

    private void OnApplicationQuit()
    {
        SavePlayTime();
    }

    public static void RegisterTracingTotalItems(int totalItems)
    {
        RegisterTotalItems(TracingTotalItemsKey, totalItems);
    }

    public static void RegisterBubblePopTotalItems(int totalItems)
    {
        RegisterTotalItems(BubblePopTotalItemsKey, totalItems);
    }

    public static void RegisterPlayTimeScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        PlayTimeSceneNames.Add(sceneName);
        if (instance != null)
        {
            instance.RefreshPlayTimeSceneState(SceneManager.GetActiveScene());
        }
    }

    public static string FormatPlayTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int remainingSeconds = totalSeconds % 60;

        return hours > 0
            ? $"{hours}h {minutes:00}m {remainingSeconds:00}s"
            : $"{minutes}m {remainingSeconds:00}s";
    }

    public static float GetTracingProgress01(int totalLetters)
    {
        return GetProgress01(KaKhaTracingProgress.GetCompletedLetterCount(totalLetters), totalLetters);
    }

    public static float GetBubblePopProgress01(int totalLevels)
    {
        return GetProgress01(BubblePopLevelMenu.GetCompletedLevelCount(totalLevels), totalLevels);
    }

    public static float GetOverallProgress01(int totalTracingLetters, int totalBubblePopLevels)
    {
        int completed = KaKhaTracingProgress.GetCompletedLetterCount(totalTracingLetters)
            + BubblePopLevelMenu.GetCompletedLevelCount(totalBubblePopLevels);
        int total = Mathf.Max(0, totalTracingLetters) + Mathf.Max(0, totalBubblePopLevels);

        return GetProgress01(completed, total);
    }

    public static void ResetPlayTime()
    {
        SavePlayTime();
        PlayerPrefs.DeleteKey(TotalPlayTimeSecondsKey);
        PlayerPrefs.Save();
    }

    private static void RegisterTotalItems(string key, int totalItems)
    {
        int safeTotal = Mathf.Max(0, totalItems);
        if (safeTotal != PlayerPrefs.GetInt(key, 0))
        {
            PlayerPrefs.SetInt(key, safeTotal);
            PlayerPrefs.Save();
        }
    }

    private static float GetProgress01(int completed, int total)
    {
        if (total <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)Mathf.Clamp(completed, 0, total) / total);
    }

    private void RecordPlayTime(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        unsavedPlayTimeSeconds += deltaSeconds;
        saveTimer += deltaSeconds;

        if (saveTimer >= SaveIntervalSeconds)
        {
            SavePlayTime();
            saveTimer = 0f;
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null)
        {
            instance.RefreshPlayTimeSceneState(scene);
        }
    }

    private void RefreshPlayTimeSceneState(Scene scene)
    {
        bool shouldTrack = scene.IsValid() && PlayTimeSceneNames.Contains(scene.name);
        if (!shouldTrack && isTrackingPlayTime)
        {
            SavePlayTime();
            saveTimer = 0f;
        }

        isTrackingPlayTime = shouldTrack;
    }

    private static void SavePlayTime()
    {
        if (unsavedPlayTimeSeconds <= 0f)
        {
            return;
        }

        PlayerPrefs.SetFloat(TotalPlayTimeSecondsKey, PlayerPrefs.GetFloat(TotalPlayTimeSecondsKey, 0f) + unsavedPlayTimeSeconds);
        unsavedPlayTimeSeconds = 0f;
        PlayerPrefs.Save();
    }
}
