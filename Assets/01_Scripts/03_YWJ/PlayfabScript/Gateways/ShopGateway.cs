using System;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class ShopGateway
{
    private readonly PlayFabGateway gateway;

    public ShopGateway(PlayFabGateway gateway)
    {
        this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public void PurchaseShopItem(
        string shopItemId,
        string requestId,
        Action<ExecuteCloudScriptResult> onSuccess = null,
        Action<PlayFabError> onError = null,
        bool forwardScriptErrors = false)
    {
        if (string.IsNullOrEmpty(shopItemId))
        {
            Debug.LogError("PurchaseShopItem: shopItemId가 비어있습니다.");
            return;
        }

        gateway.ExecuteCloudScript(
            "PurchaseShopItem",
            new
            {
                shopItemId = shopItemId,
                requestId = requestId
            },
            onSuccess,
            onError,
            forwardScriptErrors);
    }
}
