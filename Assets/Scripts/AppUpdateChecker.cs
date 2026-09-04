using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Handles platform-aware App Update checks for both Android and iOS.
/// On iOS, queries Apple's iTunes App Store API or custom JSON manifest.
/// On Android, queries Google Play Store or custom JSON manifest.
/// </summary>
public class AppUpdateChecker : MonoBehaviour
{
    [Serializable]
#pragma warning disable 0649
    private class UpdateManifest
    {
        public string latestVersion;
        public string minSupportedVersion;
        public string updateUrlAndroid;
        public string updateUrlIOS;
        public string updateUrl;
        public bool forceUpdate;
    }

    [Serializable]
    private class ITunesLookupResult
    {
        public int resultCount;
        public ITunesAppInfo[] results;
    }

    [Serializable]
    private class ITunesAppInfo
    {
        public string version;
        public string trackViewUrl;
    }
#pragma warning restore 0649

    public static AppUpdateChecker Instance { get; private set; }

    [Header("iOS Settings")]
    [Tooltip("Your iOS App Store Bundle Identifier (e.g. com.pasakasa.tracingletters). Auto-detected if empty.")]
    [SerializeField] private string iosBundleId = "";

    [Tooltip("Your Apple App ID (e.g. 1234567890). Optional, used for direct App Store link if bundle lookup is empty.")]
    [SerializeField] private string appleAppId = "";

    [Header("Android Settings")]
    [Tooltip("Android Package Name (e.g. com.pasakasa.tracingletters). Auto-detected if empty.")]
    [SerializeField] private string androidPackageName = "";

    [Header("Custom Remote JSON Manifest (Optional)")]
    [Tooltip("Optional URL returning JSON with latestVersion, minSupportedVersion, updateUrlAndroid, updateUrlIOS, and forceUpdate fields.")]
    [SerializeField] private string customManifestUrl = "";

    [Header("Check Configuration")]
    [SerializeField] private float requestTimeout = 6f;
    [SerializeField] private bool autoCheckOnStart = false;

    [Header("UI Prompt (Optional)")]
    [SerializeField] private GameObject updateDialogPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text messageText;
    [SerializeField] private Button updateButton;
    [SerializeField] private Button skipButton;

    public bool IsUpdateAvailable { get; private set; }
    public bool IsUpdateRequired { get; private set; }
    public string LatestVersion { get; private set; }
    public string StoreUrl { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
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

        Instance = this;
        DontDestroyOnLoad(gameObject);
        AutoFillPackageIdentifiers();
    }

    private void Start()
    {
        if (autoCheckOnStart)
        {
            StartCoroutine(CheckForUpdatesRoutine());
        }
    }

    public void CheckForUpdates(Action<bool, bool> onComplete = null)
    {
        StartCoroutine(CheckForUpdatesRoutine(onComplete));
    }

    public IEnumerator CheckForUpdatesRoutine(Action<bool, bool> onComplete = null)
    {
        IsUpdateAvailable = false;
        IsUpdateRequired = false;
        LatestVersion = Application.version;
        StoreUrl = GetDefaultStoreUrl();

        // 1. If Custom JSON Manifest URL is provided, check custom manifest
        if (!string.IsNullOrWhiteSpace(customManifestUrl))
        {
            yield return CheckCustomManifest();
        }
        else
        {
            // 2. Platform-specific check
#if UNITY_IOS
            yield return CheckIOSAppStore();
#elif UNITY_ANDROID
            yield return CheckAndroidPlayStore();
#else
            yield return null;
#endif
        }

        onComplete?.Invoke(IsUpdateAvailable, IsUpdateRequired);
    }

    private IEnumerator CheckIOSAppStore()
    {
        string bundleId = !string.IsNullOrWhiteSpace(iosBundleId) ? iosBundleId : Application.identifier;
        string lookupUrl = $"https://itunes.apple.com/lookup?bundleId={Uri.EscapeDataString(bundleId)}";

        if (!string.IsNullOrWhiteSpace(appleAppId))
        {
            lookupUrl = $"https://itunes.apple.com/lookup?id={Uri.EscapeDataString(appleAppId)}";
        }

        using UnityWebRequest request = UnityWebRequest.Get(lookupUrl);
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeout));

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success || string.IsNullOrWhiteSpace(request.downloadHandler.text))
        {
            yield break;
        }

        try
        {
            ITunesLookupResult response = JsonUtility.FromJson<ITunesLookupResult>(request.downloadHandler.text);
            if (response != null && response.resultCount > 0 && response.results != null && response.results.Length > 0)
            {
                ITunesAppInfo info = response.results[0];
                LatestVersion = info.version;
                if (!string.IsNullOrWhiteSpace(info.trackViewUrl))
                {
                    StoreUrl = info.trackViewUrl;
                }

                IsUpdateAvailable = IsRemoteVersionNewer(LatestVersion, Application.version);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppUpdateChecker] Failed to parse iOS iTunes lookup response: {ex.Message}");
        }
    }

    private IEnumerator CheckAndroidPlayStore()
    {
        string pkgName = !string.IsNullOrWhiteSpace(androidPackageName) ? androidPackageName : Application.identifier;
        StoreUrl = $"market://details?id={pkgName}";
        yield return null;
    }

    private IEnumerator CheckCustomManifest()
    {
        using UnityWebRequest request = UnityWebRequest.Get(customManifestUrl);
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeout));

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success || string.IsNullOrWhiteSpace(request.downloadHandler.text))
        {
            yield break;
        }

        try
        {
            UpdateManifest manifest = JsonUtility.FromJson<UpdateManifest>(request.downloadHandler.text);
            if (manifest != null)
            {
                LatestVersion = manifest.latestVersion;

#if UNITY_IOS
                StoreUrl = !string.IsNullOrWhiteSpace(manifest.updateUrlIOS) ? manifest.updateUrlIOS : manifest.updateUrl;
#else
                StoreUrl = !string.IsNullOrWhiteSpace(manifest.updateUrlAndroid) ? manifest.updateUrlAndroid : manifest.updateUrl;
#endif
                if (string.IsNullOrWhiteSpace(StoreUrl))
                {
                    StoreUrl = GetDefaultStoreUrl();
                }

                IsUpdateAvailable = IsRemoteVersionNewer(manifest.latestVersion, Application.version);
                IsUpdateRequired = manifest.forceUpdate || IsRemoteVersionNewer(manifest.minSupportedVersion, Application.version);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppUpdateChecker] Failed to parse custom update manifest: {ex.Message}");
        }
    }

    public void OpenStorePage()
    {
        string url = !string.IsNullOrWhiteSpace(StoreUrl) ? StoreUrl : GetDefaultStoreUrl();
        if (!string.IsNullOrWhiteSpace(url))
        {
            Application.OpenURL(url);
        }
    }

    public string GetDefaultStoreUrl()
    {
#if UNITY_IOS
        string bundleId = !string.IsNullOrWhiteSpace(iosBundleId) ? iosBundleId : Application.identifier;
        if (!string.IsNullOrWhiteSpace(appleAppId))
        {
            return $"https://apps.apple.com/app/id{appleAppId}";
        }
        return $"https://apps.apple.com/app/id{bundleId}";
#elif UNITY_ANDROID
        string pkgName = !string.IsNullOrWhiteSpace(androidPackageName) ? androidPackageName : Application.identifier;
        return $"market://details?id={pkgName}";
#else
        return "";
#endif
    }

    public static bool IsRemoteVersionNewer(string remoteVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(remoteVersion) || string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        string[] remoteParts = remoteVersion.Split('.');
        string[] currentParts = currentVersion.Split('.');
        int length = Mathf.Max(remoteParts.Length, currentParts.Length);

        for (int i = 0; i < length; i++)
        {
            int remoteNumber = (i < remoteParts.Length && int.TryParse(remoteParts[i], out int r)) ? r : 0;
            int currentNumber = (i < currentParts.Length && int.TryParse(currentParts[i], out int c)) ? c : 0;

            if (remoteNumber > currentNumber) return true;
            if (remoteNumber < currentNumber) return false;
        }

        return false;
    }

    private void AutoFillPackageIdentifiers()
    {
        if (string.IsNullOrWhiteSpace(iosBundleId))
        {
            iosBundleId = Application.identifier;
        }

        if (string.IsNullOrWhiteSpace(androidPackageName))
        {
            androidPackageName = Application.identifier;
        }
    }
}
