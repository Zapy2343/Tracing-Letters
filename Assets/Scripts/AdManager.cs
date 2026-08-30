using System;
using Unity.Services.LevelPlay;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    public const string RemoveAdsPlayerPrefsKey = "remove_ads_purchased";
    public const string RemoveAdsPurchasedAtKey = "remove_ads_purchased_at_utc";
    public const string RemoveAdsDeviceIdKey = "remove_ads_device_id";
    public const string RemoveAdsProductIdKey = "remove_ads_product_id";
    const string WelcomeScreenSceneName = "WelcomeScreen";
    const string MainScreenSceneName = "MainScreen";
    const string TracingLetterSceneName = "Tracing Letter";
    const string BubblePopSceneName = "Bubble POP";

    [SerializeField]
    bool hasPurchasedRemoveAds;

    LevelPlayBannerAd bannerAd;
    LevelPlayInterstitialAd interstitialAd;
    LevelPlayRewardedAd rewardedAd;

    bool isInitializing;
    bool isInitialized;
    bool callbacksRegistered;
    bool showInterstitialWhenMainScreenLoads;

    public bool HasPurchasedRemoveAds => hasPurchasedRemoveAds;
    public bool IsInitialized => isInitialized;
    public bool IsInterstitialReady => interstitialAd != null && interstitialAd.IsAdReady();
    public bool IsRewardedReady => rewardedAd != null && rewardedAd.IsAdReady();

    public event Action OnInitialized;
    public event Action<string> OnInitializationFailed;
    public event Action OnRewardedAdRewarded;
    public event Action<bool> OnRemoveAdsPurchaseChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateAdManager()
    {
        if (Instance != null)
        {
            return;
        }

        var adManagerObject = new GameObject(nameof(AdManager));
        adManagerObject.AddComponent<AdManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        hasPurchasedRemoveAds = HasStoredRemoveAdsPurchase() || hasPurchasedRemoveAds;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        InitializeAds();
    }

    public void InitializeAds()
    {
        if (hasPurchasedRemoveAds || isInitialized || isInitializing)
        {
            return;
        }

        if (string.IsNullOrEmpty(AdConfig.AppKey) || AdConfig.AppKey == "unexpected_platform")
        {
            Debug.LogWarning("[AdManager] LevelPlay cannot initialize on this platform or without a valid app key.");
            return;
        }

        RegisterLevelPlayCallbacks();

        isInitializing = true;
        Debug.Log("[AdManager] Initializing LevelPlay SDK");
        LevelPlay.Init(AdConfig.AppKey);
    }

    public void SetRemoveAdsPurchased(bool purchased)
    {
        SetRemoveAdsPurchased(purchased, string.Empty);
    }

    public void SetRemoveAdsPurchased(bool purchased, string productId)
    {
        hasPurchasedRemoveAds = purchased;
        SaveRemoveAdsPurchaseState(purchased, productId);
        OnRemoveAdsPurchaseChanged?.Invoke(purchased);
        MainScreenAdUiController.RefreshCurrentMainScreen();
        
        if (purchased)
        {
            HideBannerAd();
            DestroyAds();
            isInitialized = false;
            isInitializing = false;
            return;
        }

        InitializeAds();
        ApplySceneAdPolicy(SceneManager.GetActiveScene());
    }

    public void LoadBannerAd()
    {
        if (!CanShowAds() || IsBannerBlockedScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        bannerAd?.LoadAd();
    }

    public void ShowBannerAd()
    {
        if (!CanShowAds() || IsBannerBlockedScene(SceneManager.GetActiveScene().name))
        {
            HideBannerAd();
            return;
        }

        bannerAd?.ShowAd();
    }

    public void HideBannerAd()
    {
        bannerAd?.HideAd();
    }

    public void LoadInterstitialAd()
    {
        if (!CanShowAds())
        {
            return;
        }

        interstitialAd?.LoadAd();
    }

    public bool ShowInterstitialAd()
    {
        if (!CanShowAds() || !IsInterstitialReady)
        {
            Debug.Log("[AdManager] Interstitial ad is not ready.");
            return false;
        }

        interstitialAd.ShowAd();
        return true;
    }

    public void ShowInterstitialOnNextMainScreen()
    {
        if (hasPurchasedRemoveAds)
        {
            return;
        }

        showInterstitialWhenMainScreenLoads = true;

        if (IsMainScreenActive())
        {
            TryShowPendingMainScreenInterstitial();
        }
    }

    public void LoadRewardedAd()
    {
        if (!CanShowAds())
        {
            return;
        }

        rewardedAd?.LoadAd();
    }

    public bool ShowRewardedAd()
    {
        if (!CanShowAds() || !IsRewardedReady)
        {
            Debug.Log("[AdManager] Rewarded ad is not ready.");
            return false;
        }

        rewardedAd.ShowAd();
        return true;
    }

    bool CanShowAds()
    {
        if (hasPurchasedRemoveAds)
        {
            Debug.Log("[AdManager] Ads are disabled because remove ads was purchased.");
            return false;
        }

        if (!isInitialized)
        {
            Debug.Log("[AdManager] LevelPlay is not initialized yet.");
            return false;
        }

        return true;
    }

    void RegisterLevelPlayCallbacks()
    {
        if (callbacksRegistered)
        {
            return;
        }

        LevelPlay.OnInitSuccess += OnLevelPlayInitSuccess;
        LevelPlay.OnInitFailed += OnLevelPlayInitFailed;
        callbacksRegistered = true;
    }

    void OnLevelPlayInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log($"[AdManager] LevelPlay initialized: {config}");
        isInitializing = false;
        isInitialized = true;

        CreateAds();
        LoadBannerAd();
        LoadInterstitialAd();
        LoadRewardedAd();
        ApplySceneAdPolicy(SceneManager.GetActiveScene());

        OnInitialized?.Invoke();
    }

    void OnLevelPlayInitFailed(LevelPlayInitError error)
    {
        Debug.LogWarning($"[AdManager] LevelPlay initialization failed: {error}");
        isInitializing = false;
        isInitialized = false;
        OnInitializationFailed?.Invoke(error.ToString());
    }

    void CreateAds()
    {
        if (bannerAd == null)
        {
            var bannerConfig = new LevelPlayBannerAd.Config.Builder()
                .SetSize(LevelPlayAdSize.BANNER)
                .SetPosition(LevelPlayBannerPosition.TopCenter)
                .SetRespectSafeArea(true)
                .Build();

            bannerAd = new LevelPlayBannerAd(AdConfig.BannerAdUnitId, bannerConfig);
            bannerAd.OnAdLoaded += BannerOnAdLoaded;
            bannerAd.OnAdLoadFailed += BannerOnAdLoadFailed;
        }

        if (interstitialAd == null)
        {
            interstitialAd = new LevelPlayInterstitialAd(AdConfig.InterstitalAdUnitId);
            interstitialAd.OnAdLoaded += InterstitialOnAdLoaded;
            interstitialAd.OnAdLoadFailed += InterstitialOnAdLoadFailed;
            interstitialAd.OnAdClosed += InterstitialOnAdClosed;
        }

        if (rewardedAd == null)
        {
            rewardedAd = new LevelPlayRewardedAd(AdConfig.RewardedVideoAdUnitId);
            rewardedAd.OnAdLoaded += RewardedOnAdLoaded;
            rewardedAd.OnAdLoadFailed += RewardedOnAdLoadFailed;
            rewardedAd.OnAdRewarded += RewardedOnAdRewarded;
            rewardedAd.OnAdClosed += RewardedOnAdClosed;
        }
    }

    void DestroyAds()
    {
        bannerAd?.DestroyAd();
        interstitialAd?.DestroyAd();
        rewardedAd?.DestroyAd();

        bannerAd = null;
        interstitialAd = null;
        rewardedAd = null;
    }

    void BannerOnAdLoaded(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[AdManager] Banner loaded: {adInfo}");
        if (IsBannerBlockedScene(SceneManager.GetActiveScene().name))
        {
            bannerAd?.HideAd();
            return;
        }

        bannerAd?.ShowAd();
    }

    void BannerOnAdLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[AdManager] Banner failed to load: {error}");
    }

    void InterstitialOnAdLoaded(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[AdManager] Interstitial loaded: {adInfo}");
        TryShowPendingMainScreenInterstitial();
    }

    void InterstitialOnAdLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[AdManager] Interstitial failed to load: {error}");
    }

    void InterstitialOnAdClosed(LevelPlayAdInfo adInfo)
    {
        LoadInterstitialAd();
    }

    void RewardedOnAdLoaded(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"[AdManager] Rewarded ad loaded: {adInfo}");
    }

    void RewardedOnAdLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[AdManager] Rewarded ad failed to load: {error}");
    }

    void RewardedOnAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        Debug.Log($"[AdManager] Rewarded ad reward granted: {reward}");
        OnRewardedAdRewarded?.Invoke();
    }

    void RewardedOnAdClosed(LevelPlayAdInfo adInfo)
    {
        LoadRewardedAd();
    }

    void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        if (callbacksRegistered)
        {
            LevelPlay.OnInitSuccess -= OnLevelPlayInitSuccess;
            LevelPlay.OnInitFailed -= OnLevelPlayInitFailed;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        DestroyAds();
        Instance = null;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneAdPolicy(scene);
    }

    void ApplySceneAdPolicy(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        if (IsBannerBlockedScene(scene.name))
        {
            HideBannerAd();
            if (scene.name == MainScreenSceneName)
            {
                TryShowPendingMainScreenInterstitial();
            }
        }
        else if (scene.name == TracingLetterSceneName || scene.name == BubblePopSceneName)
        {
            LoadBannerAd();
            ShowBannerAd();
        }
    }

    static bool IsBannerBlockedScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return true;
        string lower = sceneName.ToLower();
        return lower.Contains("welcome") || lower.Contains("main");
    }

    void TryShowPendingMainScreenInterstitial()
    {
        if (!showInterstitialWhenMainScreenLoads || !IsMainScreenActive())
        {
            return;
        }

        if (ShowInterstitialAd())
        {
            showInterstitialWhenMainScreenLoads = false;
        }
    }

    static bool IsMainScreenActive()
    {
        return SceneManager.GetActiveScene().name == MainScreenSceneName;
    }

    public static bool HasStoredRemoveAdsPurchase()
    {
        return PlayerPrefs.GetInt(RemoveAdsPlayerPrefsKey, 0) == 1;
    }

    static void SaveRemoveAdsPurchaseState(bool purchased, string productId)
    {
        PlayerPrefs.SetInt(RemoveAdsPlayerPrefsKey, purchased ? 1 : 0);

        if (purchased)
        {
            PlayerPrefs.SetString(RemoveAdsPurchasedAtKey, DateTime.UtcNow.ToString("o"));
            PlayerPrefs.SetString(RemoveAdsDeviceIdKey, SystemInfo.deviceUniqueIdentifier);

            if (!string.IsNullOrWhiteSpace(productId))
            {
                PlayerPrefs.SetString(RemoveAdsProductIdKey, productId);
            }
        }
        else
        {
            PlayerPrefs.DeleteKey(RemoveAdsPurchasedAtKey);
            PlayerPrefs.DeleteKey(RemoveAdsDeviceIdKey);
            PlayerPrefs.DeleteKey(RemoveAdsProductIdKey);
        }

        PlayerPrefs.Save();
    }
}
