using System.Collections;
using UnityEngine;

public class PurchaseResult : MonoBehaviour
{
    [SerializeField] GameObject ShopUI;
    private void OnEnable()
    {
        StartCoroutine(IECloseShop());
    }

    IEnumerator IECloseShop()
    {
        yield return new WaitForSeconds(2f);
        ShopUI.SetActive(false);
    }
}
