using System;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 상점 상품 구매, 재화 차감, 보상 지급 실패 시 환불을 담당합니다.
    /// </summary>
    public sealed class ShopService
    {
        #region Dependencies

        private readonly BalanceCatalog catalog;
        private readonly PlayerRuntimeState state;
        private readonly InventoryService inventoryService;
        private readonly RewardBundleService rewardBundleService;

        /// <summary>
        /// 카탈로그, 플레이어 상태, 인벤토리 서비스를 기준으로 상점 서비스를 생성합니다.
        /// </summary>
        public ShopService(
            BalanceCatalog catalog,
            PlayerRuntimeState state,
            InventoryService inventoryService,
            RewardBundleService rewardBundleService = null)
        {
            this.catalog = catalog;
            this.state = state;
            this.inventoryService = inventoryService;
            this.rewardBundleService = rewardBundleService ?? new RewardBundleService(state, inventoryService);
        }

        #endregion

        #region Purchase

        /// <summary>
        /// 현재 플레이어 snapshot 기준으로 shopItemId가 상점 목록에 노출될 수 있는지 확인합니다.
        /// </summary>
        public bool IsShopItemVisible(string shopItemId)
        {
            if (catalog == null || state == null || string.IsNullOrEmpty(shopItemId))
            {
                return false;
            }

            return catalog.TryGetShopItem(shopItemId, out ShopItemDefinition shopItem) &&
                   IsShopItemVisible(shopItem);
        }

        /// <summary>
        /// 현재 플레이어 snapshot 기준으로 상점 행의 visibilityCondition을 평가합니다.
        /// </summary>
        public bool IsShopItemVisible(ShopItemDefinition shopItem)
        {
            return catalog != null && state != null &&
                   UnlockConditionEvaluator.IsVisible(shopItem, catalog, state);
        }

        /// <summary>
        /// shopItemId 기준으로 상품을 구매하고 보상 아이템 지급 실패 시 재화를 되돌립니다.
        /// </summary>
        public ServiceResult TryPurchaseShopItem(string shopItemId)
        {
            if (catalog == null || state == null || inventoryService == null)
            {
                return ServiceResult.Fail("Shop service is not initialized.", "shop.not_initialized");
            }

            if (!catalog.TryGetShopItem(shopItemId, out ShopItemDefinition shopItem))
            {
                return ServiceResult.Fail("Unknown shopItemId: " + shopItemId, "shop.unknown_item");
            }

            if (!shopItem.IsEnabled)
            {
                return ServiceResult.Fail("Shop item is disabled.", "shop.disabled");
            }

            if (!IsShopItemVisible(shopItem))
            {
                return ServiceResult.Fail("Shop item is not visible for this player state.", "shop.not_visible");
            }

            if (!UnlockConditionEvaluator.IsUnlocked(shopItem.UnlockCondition, catalog, state))
            {
                return ServiceResult.Fail("Shop item is locked: " + shopItem.UnlockCondition, "shop.locked");
            }

            if (!FisherCurrencyContract.IsKnownCurrency(shopItem.PriceType))
            {
                return ServiceResult.Fail("Unsupported price type: " + shopItem.PriceType, "shop.unsupported_price_type");
            }

            if (shopItem.PriceAmount < 0 || shopItem.RewardCount <= 0)
            {
                return ServiceResult.Fail("Invalid shop price or reward count.", "shop.invalid_definition");
            }

            if (FisherCurrencyContract.GetBalance(state, shopItem.PriceType) < shopItem.PriceAmount)
            {
                return ServiceResult.Fail("Not enough currency.", "shop.not_enough_currency");
            }

            if (!FisherCurrencyContract.TryAddBalance(state, shopItem.PriceType, -shopItem.PriceAmount, out _))
            {
                return ServiceResult.Fail("Currency spend failed.", "shop.currency_spend_failed");
            }

            RewardBundle bundle = new RewardBundle(shopItem.ShopItemId);
            bundle.ItemGrants.Add(new ItemDelta(shopItem.RewardItemId, shopItem.RewardCount));
            ServiceResult rewardResult = rewardBundleService.TryApplyRewardBundle(
                bundle,
                "shop.purchase_success",
                "shop.reward_apply_failed_refunded",
                "shop.reward_currency_failed");
            if (!rewardResult.Success)
            {
                if (!FisherCurrencyContract.TryAddBalance(state, shopItem.PriceType, shopItem.PriceAmount, out _))
                {
                    return ServiceResult.Fail(
                        "Inventory reward apply failed and refund overflowed. " + rewardResult.FailureReason,
                        "shop.refund_overflow");
                }

                ServiceResult fail = ServiceResult.Fail(
                    "Inventory reward apply failed. Currency refunded. " + rewardResult.FailureReason,
                    "shop.reward_apply_failed_refunded");
                fail.AffectedIds.Add(shopItem.ShopItemId);
                fail.AffectedIds.Add(shopItem.RewardItemId);
                return fail;
            }

            rewardResult.CurrencyDelta = -shopItem.PriceAmount;
            return rewardResult;
        }

        /// <summary>
        /// 명시적으로 활성화된 경제 파라미터가 있을 때만 visible currency 간 교환을 수행합니다.
        /// Cash는 hidden runtime state라 이 경로에 포함하지 않습니다.
        /// </summary>
        public ServiceResult TryExchangeCurrency(string sourceCurrency, string targetCurrency, long sourceAmount, long targetAmount)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Shop service is not initialized.", "shop.not_initialized");
            }

            if (!ReadEconomyBool("currency_exchange_enabled", false))
            {
                return ServiceResult.Fail("Currency exchange is disabled.", "shop.exchange_disabled");
            }

            if (!FisherCurrencyContract.IsKnownCurrency(sourceCurrency) ||
                !FisherCurrencyContract.IsKnownCurrency(targetCurrency))
            {
                return ServiceResult.Fail("Unknown exchange currency.", "shop.unknown_currency");
            }

            if (IsSameCurrencySlot(sourceCurrency, targetCurrency))
            {
                return ServiceResult.Fail("Source and target currencies must differ.", "shop.invalid_exchange");
            }

            if (sourceAmount <= 0 || targetAmount <= 0)
            {
                return ServiceResult.Fail("Exchange amounts must be positive.", "shop.invalid_exchange");
            }

            if (FisherCurrencyContract.GetBalance(state, sourceCurrency) < sourceAmount)
            {
                return ServiceResult.Fail("Not enough source currency.", "shop.not_enough_currency");
            }

            if (!FisherCurrencyContract.TryAddBalance(state, sourceCurrency, -sourceAmount, out _))
            {
                return ServiceResult.Fail("Source currency spend failed.", "shop.exchange_currency_failed");
            }

            if (!FisherCurrencyContract.TryAddBalance(state, targetCurrency, targetAmount, out _))
            {
                FisherCurrencyContract.TryAddBalance(state, sourceCurrency, sourceAmount, out _);
                return ServiceResult.Fail("Target currency grant failed and source was refunded.", "shop.exchange_target_failed_refunded");
            }

            ServiceResult result = ServiceResult.Ok("shop.exchange_success");
            result.AffectedIds.Add(sourceCurrency);
            result.AffectedIds.Add(targetCurrency);
            return result;
        }

        /// <summary>
        /// 내부 결제용 프리미엄 상품 productId를 기준으로 Cash를 소비하고 Prism Pearl을 지급합니다.
        /// Cash 잔액은 UI에 노출하지 않고, 상품 정의도 표시 통화로 취급하지 않습니다.
        /// </summary>
        public ServiceResult TryPurchasePremiumCurrencyProduct(string productId)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Shop service is not initialized.", "shop.not_initialized");
            }

            if (!catalog.TryGetPremiumCurrencyProduct(productId, out PremiumCurrencyProductDefinition product))
            {
                return ServiceResult.Fail("Unknown premium currency product: " + productId, "shop.unknown_premium_currency_product");
            }

            if (!product.IsEnabled)
            {
                return ServiceResult.Fail("Premium currency product is disabled.", "shop.premium_currency_product_disabled");
            }

            ServiceResult result = TryPurchasePrismPearlWithCash(product.CashAmount, product.PrismPearlAmount);
            result.AffectedIds.Add(product.ProductId);
            return result;
        }

        /// <summary>
        /// 실제 결제 SDK가 붙기 전까지 내부 Cash를 소비해 Prism Pearl을 지급합니다. Cash는 UI 지갑에 표시하지 않습니다.
        /// </summary>
        public ServiceResult TryPurchasePrismPearlWithCash(long cashAmount, long prismPearlAmount)
        {
            if (state == null)
            {
                return ServiceResult.Fail("Shop service is not initialized.", "shop.not_initialized");
            }

            if (cashAmount <= 0 || prismPearlAmount <= 0)
            {
                return ServiceResult.Fail("Cash and Prism Pearl amounts must be positive.", "shop.invalid_cash_purchase");
            }

            if (!FisherCurrencyContract.TryConsumeHiddenCash(state, cashAmount, out _))
            {
                return ServiceResult.Fail("Not enough hidden Cash.", "shop.not_enough_cash");
            }

            if (!FisherCurrencyContract.TryAddBalance(state, "prismPearl", prismPearlAmount, out _))
            {
                FisherCurrencyContract.TryGrantHiddenCash(state, cashAmount, out _);
                return ServiceResult.Fail("Prism Pearl grant failed and Cash was refunded.", "shop.cash_purchase_refunded");
            }

            ServiceResult result = ServiceResult.Ok("shop.cash_prism_purchase_success");
            result.AffectedIds.Add("cash");
            result.AffectedIds.Add("prismPearl");
            return result;
        }

        #endregion

        #region Currency Helpers

        private bool ReadEconomyBool(string key, bool fallback)
        {
            if (catalog == null ||
                !catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param) ||
                param == null ||
                !param.IsEnabled)
            {
                return fallback;
            }

            return bool.TryParse(param.Value, out bool value) ? value : fallback;
        }

        private static bool IsSameCurrencySlot(string left, string right)
        {
            return FisherCurrencyContract.IsGoldCurrency(left) && FisherCurrencyContract.IsGoldCurrency(right) ||
                   FisherCurrencyContract.IsPrismPearl(left) && FisherCurrencyContract.IsPrismPearl(right) ||
                   FisherCurrencyContract.IsPirateCoin(left) && FisherCurrencyContract.IsPirateCoin(right);
        }

        #endregion
    }

}
