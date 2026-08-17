using UnityEngine;
using UnityEngine.Purchasing;

public class IAPManager : MonoBehaviour, IStoreListener
{
    public static IAPManager Instance { get; private set; }

    private IStoreController _store;
    private IExtensionProvider _extensions;

    public bool IsInitialized => _store != null;

    public event System.Action<string> OnPurchaseSuccess;
    public event System.Action<string> OnPurchaseFailedEvent;

    [SerializeField] string ProductId;
    [SerializeField] GameObject PurchaseSuccessPanel;

    void Awake()
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

    void InitializeIAP()
    {

        var module = StandardPurchasingModule.Instance();


        module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;


        var builder = ConfigurationBuilder.Instance(module);
       
        builder.AddProduct(IAPProducts.NO_ADS,
            ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    // Buy
    public void BuyProduct(string productId)
    {
        productId = ProductId;
        Debug.LogError("buy btn clicked");
        if (!IsInitialized)
        {
            Debug.LogError("IAP not ready");
            return;
        }

        Product p = _store.products.WithID(productId);
        if (p != null && p.availableToPurchase)
        {
            _store.InitiatePurchase(p);
            Debug.LogError($"Product successfull");
        }
        else
            Debug.LogError($"Product unavailable: {productId}");
    }

    // Restore 
    public void RestorePurchases()
    {
#if UNITY_IOS
    _extensions.GetExtension<IAppleExtensions>()
        .RestoreTransactions((result, error) =>
            Debug.Log($"Restore: {result} {error}"));
#elif UNITY_ANDROID
        _extensions.GetExtension<IGooglePlayStoreExtensions>()
            .RestoreTransactions((result, error) =>
                Debug.LogError($"Restore: {result} {error}"));
#endif
    }

    // Ownership checks 
    public bool HasNoAds()
    {
        return IsOwned(IAPProducts.NO_ADS);
            
    }
    
    public bool IsOwned(string productId)
    {
        productId = ProductId;

        if (!IsInitialized) return false;
        Product p = _store.products.WithID(productId);
        return p != null && p.hasReceipt;
    }

    public string GetPrice(string productId)
    {
        productId = ProductId;

        if (!IsInitialized) return "...";
        Product p = _store.products.WithID(productId);
        return p != null
            ? p.metadata.localizedPriceString
            : "N/A";
    }

    //IStoreListener 

    public void OnInitialized(
        IStoreController controller,
        IExtensionProvider extensions)
    {
        _store = controller;
        _extensions = extensions;
        Debug.LogError("IAP initialized");

        if (HasNoAds())
            SetNoAds(true);
    }

    public void OnInitializeFailed(
        InitializationFailureReason reason)
        => Debug.LogError($"IAP init failed: {reason}");

    public void OnInitializeFailed(
        InitializationFailureReason reason,
        string message)
        => Debug.LogError($"IAP init failed: {reason} {message}");

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string id = args.purchasedProduct.definition.id;

        switch (id)
        {
            // No Ads 
            case IAPProducts.NO_ADS:
                SetNoAds(true);
                break;
        }

        OnPurchaseSuccess?.Invoke(id);
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(
        Product product,
        PurchaseFailureReason reason)
    {
        Debug.LogError(
            $"Purchase failed: {product.definition.id} — {reason}");
        OnPurchaseFailedEvent?.Invoke(product.definition.id);
    }

    //Benefit helpers 

    void SetNoAds(bool value)
    {
        print("purchase done");
        AdManager.Instance.SetRemoveAdsPurchased(value);
        PurchaseSuccessPanel.SetActive(value);
    }
}


public static class IAPProducts
{
    public const string NO_ADS = "no_ads_forever";
}