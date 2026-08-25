using UnityEngine;
using UnityEngine.Purchasing;

#pragma warning disable CS0618 // Suppresses obsolete warning while using Unity IAP transitional API

public class IAPManager : MonoBehaviour, IStoreListener
{
    public static IAPManager Instance { get; private set; }

    private IStoreController _store;
    private IExtensionProvider _extensions;

    public bool IsInitialized => _store != null;

    public event System.Action<string> OnPurchaseSuccess;
    public event System.Action<string> OnPurchaseFailedEvent;

    [SerializeField] private string defaultProductId = IAPProducts.NO_ADS;
    [SerializeField] private GameObject purchaseSuccessPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeIAP();
    }

    private void InitializeIAP()
    {
        var module = StandardPurchasingModule.Instance();
        module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;

        var builder = ConfigurationBuilder.Instance(module);
        builder.AddProduct(IAPProducts.NO_ADS, ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    // Buy
    public void BuyProduct(string productId = null)
    {
        string targetId = string.IsNullOrEmpty(productId) ? defaultProductId : productId;

        if (!IsInitialized)
        {
            Debug.LogError("[IAPManager] IAP not ready");
            return;
        }

        Product p = _store.products.WithID(targetId);
        if (p != null && p.availableToPurchase)
        {
            _store.InitiatePurchase(p);
            Debug.Log($"[IAPManager] Initiating purchase for: {targetId}");
        }
        else
        {
            Debug.LogError($"[IAPManager] Product unavailable: {targetId}");
        }
    }

    // Restore 
    public void RestorePurchases()
    {
#if UNITY_IOS
        _extensions?.GetExtension<IAppleExtensions>()
            ?.RestoreTransactions((result, error) =>
                Debug.Log($"[IAPManager] Restore: {result} {error}"));
#elif UNITY_ANDROID
        _extensions?.GetExtension<IGooglePlayStoreExtensions>()
            ?.RestoreTransactions((result, error) =>
                Debug.LogError($"[IAPManager] Restore: {result} {error}"));
#endif
    }

    // Ownership checks 
    public bool HasNoAds()
    {
        return IsOwned(IAPProducts.NO_ADS);
    }
    
    public bool IsOwned(string productId = null)
    {
        string targetId = string.IsNullOrEmpty(productId) ? defaultProductId : productId;

        if (!IsInitialized) return false;
        Product p = _store.products.WithID(targetId);
        return p != null && p.hasReceipt;
    }

    public string GetPrice(string productId = null)
    {
        string targetId = string.IsNullOrEmpty(productId) ? defaultProductId : productId;

        if (!IsInitialized) return "...";
        Product p = _store.products.WithID(targetId);
        return p != null ? p.metadata.localizedPriceString : "N/A";
    }

    // IStoreListener 

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _store = controller;
        _extensions = extensions;
        Debug.Log("[IAPManager] IAP initialized");

        if (HasNoAds())
        {
            SetNoAds(true);
        }
    }

    public void OnInitializeFailed(InitializationFailureReason reason)
    {
        Debug.LogError($"[IAPManager] IAP init failed: {reason}");
    }

    public void OnInitializeFailed(InitializationFailureReason reason, string message)
    {
        Debug.LogError($"[IAPManager] IAP init failed: {reason} {message}");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string id = args.purchasedProduct.definition.id;

        switch (id)
        {
            case IAPProducts.NO_ADS:
                SetNoAds(true);
                break;
        }

        OnPurchaseSuccess?.Invoke(id);
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
    {
        Debug.LogError($"[IAPManager] Purchase failed: {product.definition.id} - {reason}");
        OnPurchaseFailedEvent?.Invoke(product.definition.id);
    }

    // Benefit helpers 

    private void SetNoAds(bool value)
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.SetRemoveAdsPurchased(value);
        }

        if (purchaseSuccessPanel != null)
        {
            purchaseSuccessPanel.SetActive(value);
        }
    }
}

public static class IAPProducts
{
    public const string NO_ADS = "no_ads_forever";
}

#pragma warning restore CS0618
