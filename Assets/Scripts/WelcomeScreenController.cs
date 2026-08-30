using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WelcomeScreenController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private LoadingScreenController loadingScreen;
    [SerializeField] private string nextSceneName = "MainScreen";
    [SerializeField] private bool loadNextSceneAdditively = true;
    [SerializeField] private bool unloadWelcomeSceneAfterLoad = true;

    [Header("Transition")]
    [SerializeField] private float fadeDuration = 0.45f;
    [SerializeField] private Sprite[] transitionBackgroundImages;
    [SerializeField] private Color[] transitionFallbackColors =
    {
        new Color(1f, 0.72f, 0.25f, 1f),
        new Color(0.98f, 0.45f, 0.62f, 1f),
        new Color(0.46f, 0.86f, 0.56f, 1f),
        new Color(0.5f, 0.36f, 0.95f, 1f)
    };

    [Header("Connectivity")]
    [SerializeField] private bool continueWithoutInternet = true;
    [SerializeField] private float internetCheckTimeout = 5f;
    [SerializeField] private string connectivityCheckUrl = "https://clients3.google.com/generate_204";

    [Header("App Update")]
    [SerializeField] private bool openStoreWhenForceUpdateRequired = true;

    private bool loadingAnimationFinished;
    private bool startupChecksFinished;
    private bool updateAvailable;
    private bool updateRequired;
    private bool isContinuing;
    private string updateUrl;

    private void Awake()
    {
        if (loadingScreen == null)
        {
            loadingScreen = FindFirstObjectByType<LoadingScreenController>();
        }

        if (loadingScreen != null)
        {
            loadingScreen.OnLoadingFinished.AddListener(HandleLoadingAnimationFinished);
        }
    }

    private void Start()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.HideBannerAd();
        }

        StartCoroutine(RunStartupChecks());
    }

    private void OnDestroy()
    {
        if (loadingScreen != null)
        {
            loadingScreen.OnLoadingFinished.RemoveListener(HandleLoadingAnimationFinished);
        }
    }

    private void HandleLoadingAnimationFinished()
    {
        loadingAnimationFinished = true;
        TryContinue();
    }

    private IEnumerator RunStartupChecks()
    {
        bool hasInternet = Application.internetReachability != NetworkReachability.NotReachable;

        if (hasInternet && !string.IsNullOrWhiteSpace(connectivityCheckUrl))
        {
            yield return CheckInternetConnection(result => hasInternet = result);
        }

        if (hasInternet)
        {
            yield return CheckForAppUpdate();
        }

        if (!hasInternet && !continueWithoutInternet)
        {
            yield break;
        }

        startupChecksFinished = true;
        TryContinue();
    }

    private IEnumerator CheckInternetConnection(Action<bool> onComplete)
    {
        using UnityWebRequest request = UnityWebRequest.Get(connectivityCheckUrl);
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(internetCheckTimeout));

        yield return request.SendWebRequest();

        bool connected = request.result == UnityWebRequest.Result.Success;
        onComplete?.Invoke(connected);
    }

    private IEnumerator CheckForAppUpdate()
    {
        if (AppUpdateChecker.Instance == null)
        {
            GameObject checkerObj = new GameObject("AppUpdateChecker");
            checkerObj.AddComponent<AppUpdateChecker>();
        }

        yield return AppUpdateChecker.Instance.CheckForUpdatesRoutine((available, required) =>
        {
            updateAvailable = available;
            updateRequired = required;
            updateUrl = AppUpdateChecker.Instance.StoreUrl;
        });

        if ((updateRequired || updateAvailable) && openStoreWhenForceUpdateRequired && !string.IsNullOrWhiteSpace(updateUrl))
        {
            AppUpdateChecker.Instance.OpenStorePage();
        }
    }

    private void TryContinue()
    {
        if (!loadingAnimationFinished || !startupChecksFinished || updateRequired)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            StartCoroutine(LoadNextSceneAndDestroyWelcome());
        }
    }

    private IEnumerator LoadNextSceneAndDestroyWelcome()
    {
        if (isContinuing)
        {
            yield break;
        }

        isContinuing = true;
        Scene welcomeScene = gameObject.scene;
        CanvasGroup fadeGroup = CreateFadeOverlay(out FadeOverlayCleanup fadeOverlayCleanup);

        yield return Fade(fadeGroup, 0f, 1f);

        if (loadNextSceneAdditively)
        {
            DisableAudioListeners(welcomeScene);

            Scene nextScene = SceneManager.GetSceneByName(nextSceneName);
            if (!nextScene.isLoaded)
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);
                while (loadOperation != null && !loadOperation.isDone)
                {
                    yield return null;
                }

                nextScene = SceneManager.GetSceneByName(nextSceneName);
            }

            if (nextScene.IsValid() && nextScene.isLoaded)
            {
                SceneManager.SetActiveScene(nextScene);
                MainScreenAdUiController.RefreshCurrentMainScreen();
            }

            fadeOverlayCleanup.FadeOutAndCleanup(welcomeScene, unloadWelcomeSceneAfterLoad && welcomeScene.name != nextSceneName);
            yield break;
        }

        fadeOverlayCleanup.LoadSingleSceneThenFadeOut(nextSceneName);
    }

    private CanvasGroup CreateFadeOverlay(out FadeOverlayCleanup fadeOverlayCleanup)
    {
        GameObject fadeRoot = new GameObject("Welcome Scene Fade");
        DontDestroyOnLoad(fadeRoot);
        fadeOverlayCleanup = fadeRoot.AddComponent<FadeOverlayCleanup>();

        GameObject canvasObject = new GameObject("Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(fadeRoot.transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        GameObject panelObject = new GameObject("Fade Background", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panel = panelObject.GetComponent<Image>();
        Sprite transitionImage = PickTransitionBackgroundImage();
        panel.sprite = transitionImage;
        panel.color = transitionImage != null ? Color.white : PickFallbackTransitionColor();
        panel.preserveAspect = transitionImage != null;
        panel.raycastTarget = false;

        fadeOverlayCleanup.Initialize(canvasGroup, fadeDuration);
        return canvasGroup;
    }

    private Sprite PickTransitionBackgroundImage()
    {
        if (transitionBackgroundImages == null || transitionBackgroundImages.Length == 0)
        {
            return null;
        }

        return transitionBackgroundImages[UnityEngine.Random.Range(0, transitionBackgroundImages.Length)];
    }

    private Color PickFallbackTransitionColor()
    {
        if (transitionFallbackColors == null || transitionFallbackColors.Length == 0)
        {
            return Color.black;
        }

        return transitionFallbackColors[UnityEngine.Random.Range(0, transitionFallbackColors.Length)];
    }

    private IEnumerator Fade(CanvasGroup canvasGroup, float fromAlpha, float toAlpha)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, fadeDuration);
        canvasGroup.alpha = fromAlpha;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            yield return null;
        }

        canvasGroup.alpha = toAlpha;
    }

    private static void DisableAudioListeners(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            AudioListener[] listeners = rootObjects[i].GetComponentsInChildren<AudioListener>(true);
            for (int j = 0; j < listeners.Length; j++)
            {
                listeners[j].enabled = false;
            }
        }
    }

    private sealed class FadeOverlayCleanup : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private float duration;

        public void Initialize(CanvasGroup targetCanvasGroup, float fadeDuration)
        {
            canvasGroup = targetCanvasGroup;
            duration = fadeDuration;
        }

        public void FadeOutAndCleanup(Scene sceneToUnload, bool unloadScene)
        {
            StartCoroutine(FadeOutAndCleanupRoutine(sceneToUnload, unloadScene));
        }

        public void LoadSingleSceneThenFadeOut(string sceneName)
        {
            StartCoroutine(LoadSingleSceneThenFadeOutRoutine(sceneName));
        }

        private IEnumerator FadeOutAndCleanupRoutine(Scene sceneToUnload, bool unloadScene)
        {
            if (unloadScene && sceneToUnload.IsValid() && sceneToUnload.isLoaded)
            {
                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload);
                while (unloadOperation != null && !unloadOperation.isDone)
                {
                    yield return null;
                }
            }

            yield return Fade(1f, 0f);
            Destroy(gameObject);
        }

        private IEnumerator LoadSingleSceneThenFadeOutRoutine(string sceneName)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
            while (loadOperation != null && !loadOperation.isDone)
            {
                yield return null;
            }

            yield return Fade(1f, 0f);
            Destroy(gameObject);
        }

        private IEnumerator Fade(float fromAlpha, float toAlpha)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);
            canvasGroup.alpha = fromAlpha;

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = t * t * (3f - 2f * t);
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
                yield return null;
            }

            canvasGroup.alpha = toAlpha;
        }
    }
}
