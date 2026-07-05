namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 아이템/물고기 발견 등록과 도감 보상 수령을 담당합니다.
    /// </summary>
    public sealed class CollectionService
    {
        #region Dependencies

        private readonly BalanceCatalog catalog;
        private readonly PlayerRuntimeState state;
        private readonly InventoryService inventoryService;
        private readonly RewardBundleService rewardBundleService;

        /// <summary>
        /// 카탈로그, 플레이어 상태, 인벤토리 서비스를 기준으로 도감 서비스를 생성합니다.
        /// </summary>
        public CollectionService(
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

        #region Discovery

        /// <summary>
        /// Generated fishId 또는 itemId를 받아 도감 발견 상태로 등록합니다.
        /// 외부 낚시/보상 어댑터는 CSH 내부 ID 종류를 몰라도 이 진입점으로 연결할 수 있습니다.
        /// </summary>
        public ServiceResult TryRegisterCatalogDiscovery(string catalogId)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Collection service is not initialized.", "collection.not_initialized");
            }

            if (catalog.TryGetFish(catalogId, out _))
            {
                return TryRegisterFishDiscovery(catalogId);
            }

            if (catalog.TryGetItem(catalogId, out _))
            {
                return TryRegisterDiscovery(catalogId);
            }

            return ServiceResult.Fail("Unknown catalog discovery id: " + catalogId, "collection.unknown_catalog_id");
        }

        /// <summary>
        /// itemId 기준으로 도감 발견 상태를 등록합니다.
        /// </summary>
        public ServiceResult TryRegisterDiscovery(string itemId)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Collection service is not initialized.", "collection.not_initialized");
            }

            if (!catalog.TryGetItem(itemId, out ItemDefinition item))
            {
                return ServiceResult.Fail("Unknown itemId: " + itemId, "collection.unknown_item");
            }

            if (!item.IsEnabled)
            {
                return ServiceResult.Fail("Item definition is disabled: " + itemId, "collection.item_disabled");
            }

            if (!state.discoveredCollectionItemIds.Add(itemId))
            {
                ServiceResult already = ServiceResult.Ok("collection.discovery_already_registered");
                already.AffectedIds.Add(itemId);
                return already;
            }

            ServiceResult result = ServiceResult.Ok("collection.discovery_registered");
            result.AffectedIds.Add(itemId);
            return result;
        }

        /// <summary>
        /// fishId 기준으로 등급이 있는 물고기 발견 상태를 등록합니다.
        /// </summary>
        public ServiceResult TryRegisterFishDiscovery(string fishId)
        {
            if (catalog == null || state == null)
            {
                return ServiceResult.Fail("Collection service is not initialized.", "collection.not_initialized");
            }

            if (!catalog.TryGetFish(fishId, out FishDefinition fish))
            {
                return ServiceResult.Fail("Unknown fishId: " + fishId, "collection.unknown_fish");
            }

            if (!fish.IsEnabled)
            {
                return ServiceResult.Fail("Fish definition is disabled: " + fishId, "collection.fish_disabled");
            }

            if (!catalog.TryGetItem(fish.ItemId, out ItemDefinition item) || !item.IsEnabled)
            {
                return ServiceResult.Fail("Fish item definition is disabled or missing: " + fish.ItemId, "collection.item_disabled");
            }

            if (!state.discoveredCollectionItemIds.Add(fishId))
            {
                ServiceResult already = ServiceResult.Ok("collection.discovery_already_registered");
                already.AffectedIds.Add(fishId);
                return already;
            }

            ServiceResult result = ServiceResult.Ok("collection.discovery_registered");
            result.AffectedIds.Add(fishId);
            result.AffectedIds.Add(fish.ItemId);
            return result;
        }

        #endregion

        #region Rewards

        /// <summary>
        /// 도감 보상 조건을 확인하고 재화/아이템 보상을 원자적으로 정산합니다.
        /// </summary>
        public ServiceResult TryClaimCollectionReward(string rewardId)
        {
            if (catalog == null || state == null || inventoryService == null)
            {
                return ServiceResult.Fail("Collection service is not initialized.", "collection.not_initialized");
            }

            if (!catalog.TryGetCollectionReward(rewardId, out CollectionRewardDefinition reward))
            {
                return ServiceResult.Fail("Unknown rewardId: " + rewardId, "collection.unknown_reward");
            }

            if (!reward.IsEnabled)
            {
                return ServiceResult.Fail("Collection reward is disabled.", "collection.reward_disabled");
            }

            if (state.claimedRewardIds.Contains(reward.ClaimId))
            {
                return ServiceResult.Fail("Collection reward is already claimed.", "collection.already_claimed");
            }

            if (reward.RewardAmount > 0 && !IsSoftCurrency(reward.RewardCurrency))
            {
                return ServiceResult.Fail(
                    "Unsupported collection reward currency: " + reward.RewardCurrency,
                    "collection.unsupported_reward_currency");
            }

            ServiceResult condition = EvaluateCondition(reward);
            if (!condition.Success)
            {
                return condition;
            }

            RewardBundle bundle = new RewardBundle(reward.RewardId)
            {
                CurrencyId = reward.RewardCurrency,
                CurrencyAmount = reward.RewardAmount
            };

            if (!string.IsNullOrEmpty(reward.RewardItemId) && reward.RewardItemCount > 0)
            {
                bundle.ItemGrants.Add(new ItemDelta(reward.RewardItemId, reward.RewardItemCount));
            }

            ServiceResult result = rewardBundleService.TryApplyRewardBundle(
                bundle,
                "collection.claim_success",
                "collection.reward_apply_failed_rolled_back",
                "collection.currency_overflow");
            if (!result.Success)
            {
                result.AffectedIds.Add(reward.ClaimId);
                return result;
            }

            state.claimedRewardIds.Add(reward.ClaimId);
            result.AffectedIds.Add(reward.ClaimId);
            return result;
        }

        #endregion

        #region Conditions

        private ServiceResult EvaluateCondition(CollectionRewardDefinition reward)
        {
            if (reward.ConditionType == "discovery")
            {
                if (!HasDiscoveredItem(reward.ItemId))
                {
                    return ServiceResult.Fail("Collection item is not discovered.", "collection.not_discovered");
                }

                return ServiceResult.Ok("collection.condition_met");
            }

            if (reward.ConditionType == "count")
            {
                if (!HasDiscoveredItem(reward.ItemId))
                {
                    return ServiceResult.Fail("Collection item is not discovered.", "collection.not_discovered");
                }

                int requiredCount = reward.ConditionValue > 0 ? reward.ConditionValue : 1;
                int acquiredCount = inventoryService.GetAcquiredCount(reward.ItemId);
                if (acquiredCount < requiredCount)
                {
                    return ServiceResult.Fail(
                        "Collection count condition is not met. required=" + requiredCount + ", acquired=" + acquiredCount,
                        "collection.condition_not_met");
                }

                return ServiceResult.Ok("collection.condition_met");
            }

            return ServiceResult.Fail(
                "Unsupported collection conditionType: " + reward.ConditionType,
                "collection.unsupported_condition");
        }

        #endregion

        #region Discovery Helpers

        private bool HasDiscoveredItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || state == null)
            {
                return false;
            }

            if (state.discoveredCollectionItemIds.Contains(itemId))
            {
                return true;
            }

            if (catalog == null)
            {
                return false;
            }

            foreach (FishDefinition fish in catalog.FishById.Values)
            {
                if (fish != null &&
                    fish.IsEnabled &&
                    fish.ItemId == itemId &&
                    state.discoveredCollectionItemIds.Contains(fish.FishId))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Currency Helpers

        private static bool IsSoftCurrency(string rewardCurrency)
        {
            return FisherCurrencyContract.IsGoldCurrency(rewardCurrency);
        }

        #endregion
    }
}
