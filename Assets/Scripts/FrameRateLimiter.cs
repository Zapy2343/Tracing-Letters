using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    private const int TargetFrameRate = 60;

    private static FrameRateLimiter instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (instance != null)
        {
            ApplyFrameRateLimit();
            return;
        }

        GameObject limiterObject = new GameObject(nameof(FrameRateLimiter));
        instance = limiterObject.AddComponent<FrameRateLimiter>();
        DontDestroyOnLoad(limiterObject);
        ApplyFrameRateLimit();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
#if UNITY_EDITOR
            if (UnityEditor.Selection.activeGameObject == gameObject)
            {
                UnityEditor.Selection.activeGameObject = null;
            }
#endif
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyFrameRateLimit();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ApplyFrameRateLimit();
        }
    }

    private static void ApplyFrameRateLimit()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
