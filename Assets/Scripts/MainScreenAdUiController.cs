using UnityEngine;
using UnityEngine.SceneManagement;

public class MainScreenAdUiController : MonoBehaviour
{
    const string MainScreenSceneName = "MainScreen";
    const string RemoveAdsButtonName = "Remove Ads Button";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneHooks()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshCurrentMainScreen();
    }

    public static void RefreshCurrentMainScreen()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != MainScreenSceneName)
        {
            return;
        }

        bool hasPurchasedRemoveAds = AdManager.HasStoredRemoveAdsPurchase()
            || (AdManager.Instance != null && AdManager.Instance.HasPurchasedRemoveAds)
            || (IAPManager.Instance != null && IAPManager.Instance.HasNoAds());

        SetRemoveAdsButtonsVisible(activeScene, !hasPurchasedRemoveAds);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MainScreenSceneName)
        {
            RefreshCurrentMainScreen();
        }
    }

    private static void SetRemoveAdsButtonsVisible(Scene scene, bool visible)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform[] children = rootObjects[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < children.Length; j++)
            {
                if (children[j].name == RemoveAdsButtonName)
                {
                    children[j].gameObject.SetActive(visible);
                }
            }
        }
    }
}
