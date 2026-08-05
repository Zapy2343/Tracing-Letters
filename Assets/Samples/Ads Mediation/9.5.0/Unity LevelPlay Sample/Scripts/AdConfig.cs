public static class AdConfig
{
    public static string AppKey => GetAppKey();
    public static string BannerAdUnitId => GetBannerAdUnitId();
    public static string InterstitalAdUnitId => GetInterstitialAdUnitId();
    public static string RewardedVideoAdUnitId => GetRewardedVideoAdUnitId();

    static string GetAppKey()
    {
#if UNITY_ANDROID
        return "277aadc25";
#elif UNITY_IPHONE
        return "277ace1ed";
#else
        return "unexpected_platform";
#endif
    }

    static string GetBannerAdUnitId()
    {
#if UNITY_ANDROID
        return "ygqdrzq6g8uhbtdp";
#elif UNITY_IPHONE
        return "auo2eimnq8gu2w41";
#else
        return "unexpected_platform";
#endif
    }
    static string GetInterstitialAdUnitId()
    {
#if UNITY_ANDROID
        return "b4e0ztfkrpg7kb62";
#elif UNITY_IPHONE
        return "x6zthp7f0zo0bq2l";
#else
        return "unexpected_platform";
#endif
    }

    static string GetRewardedVideoAdUnitId()
    {
#if UNITY_ANDROID
        return "q4858abe66iws5ei";
#elif UNITY_IPHONE
            return "x1mms5xicq0zwar5";
#else
            return "unexpected_platform";
#endif
    }
}
