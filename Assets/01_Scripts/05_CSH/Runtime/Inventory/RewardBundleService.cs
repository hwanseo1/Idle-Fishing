using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 아이템/재화 지급을 한 번에 적용하기 위한 작은 보상 묶음 DTO입니다.
    /// </summary>
    public sealed class RewardBundle
    {
        public string SourceId;
        public string CurrencyId;
        public long CurrencyAmount;
        public readonly List<ItemDelta> ItemGrants = new List<ItemDelta>();

        public RewardBundle(string sourceId)
        {
            SourceId = sourceId;
        }
    }

    /// <summary>
    /// 여러 보상 지급을 원자적으로 적용하고 실패 시 지급 전 상태로 되돌립니다.
    /// </summary>
    public sealed class RewardBundleService
    {
        private readonly PlayerRuntimeState state;
        private readonly InventoryService inventoryService;

        /// <summary>
        /// 보상 지급 rollback을 위해 같은 플레이어 상태와 인벤토리 서비스를 참조합니다.
        /// </summary>
        public RewardBundleService(PlayerRuntimeState state, InventoryService inventoryService)
        {
            this.state = state;
            this.inventoryService = inventoryService;
        }

        /// <summary>
        /// 재화와 아이템 보상을 한 번에 적용하고, 중간 실패 시 지급 전 상태로 되돌립니다.
        /// </summary>
        public ServiceResult TryApplyRewardBundle(
            RewardBundle bundle,
            string successMessageKey,
            string itemApplyFailureMessageKey,
            string currencyFailureMessageKey)
        {
            if (state == null || inventoryService == null)
            {
                return ServiceResult.Fail("Reward bundle service is not initialized.", "reward_bundle.not_initialized");
            }

            if (bundle == null)
            {
                return ServiceResult.Fail("Reward bundle is null.", "reward_bundle.invalid_bundle");
            }

            ServiceResult validation = ValidateBundle(bundle);
            if (!validation.Success)
            {
                return validation;
            }

            RewardBundleRollbackState rollback = RewardBundleRollbackState.Capture(state);
            long currencyApplied = 0;
            if (bundle.CurrencyAmount > 0)
            {
                if (!FisherCurrencyContract.TryAddBalance(state, bundle.CurrencyId, bundle.CurrencyAmount, out _))
                {
                    rollback.Restore(state);
                    ServiceResult fail = ServiceResult.Fail(
                        "Reward bundle currency apply failed: " + bundle.CurrencyId,
                        currencyFailureMessageKey);
                    AddSourceId(fail, bundle.SourceId);
                    return fail;
                }

                currencyApplied = bundle.CurrencyAmount;
            }

            ServiceResult result = ServiceResult.Ok(successMessageKey);
            result.CurrencyDelta = currencyApplied;
            AddSourceId(result, bundle.SourceId);

            for (int i = 0; i < bundle.ItemGrants.Count; i++)
            {
                ItemDelta grant = bundle.ItemGrants[i];
                ServiceResult add = inventoryService.TryAddItem(grant.ItemId, grant.CountDelta, grant.InstanceId, grant.LevelIndex);
                if (!add.Success)
                {
                    rollback.Restore(state);
                    ServiceResult fail = ServiceResult.Fail(
                        "Reward bundle item apply failed. State rolled back. " + add.FailureReason,
                        itemApplyFailureMessageKey);
                    AddSourceId(fail, bundle.SourceId);
                    fail.AffectedIds.Add(grant.ItemId);
                    return fail;
                }

                result.ItemDeltas.Add(new ItemDelta(grant.ItemId, grant.CountDelta, grant.InstanceId, grant.LevelIndex));
                result.AffectedIds.Add(grant.ItemId);
            }

            return result;
        }

        private static ServiceResult ValidateBundle(RewardBundle bundle)
        {
            if (bundle.CurrencyAmount < 0)
            {
                return ServiceResult.Fail("Reward bundle currency amount must be positive.", "reward_bundle.invalid_currency_amount");
            }

            if (bundle.CurrencyAmount > 0 && !FisherCurrencyContract.IsKnownCurrency(bundle.CurrencyId))
            {
                return ServiceResult.Fail("Unsupported reward bundle currency: " + bundle.CurrencyId, "reward_bundle.unsupported_currency");
            }

            for (int i = 0; i < bundle.ItemGrants.Count; i++)
            {
                ItemDelta grant = bundle.ItemGrants[i];
                if (grant == null || string.IsNullOrWhiteSpace(grant.ItemId) || grant.CountDelta <= 0)
                {
                    return ServiceResult.Fail("Reward bundle item grants must be positive.", "reward_bundle.invalid_item_grant");
                }
            }

            return ServiceResult.Ok("reward_bundle.valid");
        }

        private static void AddSourceId(ServiceResult result, string sourceId)
        {
            if (result != null && !string.IsNullOrEmpty(sourceId))
            {
                result.AffectedIds.Add(sourceId);
            }
        }
    }

    /// <summary>
    /// RewardBundleService 실패 rollback에 필요한 플레이어 상태 snapshot입니다.
    /// </summary>
    internal sealed class RewardBundleRollbackState
    {
        private long softCurrency;
        private long prismPearl;
        private long pirateCoin;
        private long crewExp;
        private List<InventoryEntry> inventoryEntries;
        private Dictionary<string, int> itemAcquisitionCounts;
        private HashSet<string> discoveredCollectionItemIds;
        private HashSet<string> newItemNoticeIds;

        public static RewardBundleRollbackState Capture(PlayerRuntimeState state)
        {
            RewardBundleRollbackState snapshot = new RewardBundleRollbackState
            {
                softCurrency = state.softCurrency,
                prismPearl = state.prismPearl,
                pirateCoin = state.pirateCoin,
                crewExp = state.crewExp,
                inventoryEntries = CloneInventoryEntries(state.inventoryEntries),
                itemAcquisitionCounts = new Dictionary<string, int>(state.itemAcquisitionCounts, System.StringComparer.Ordinal),
                discoveredCollectionItemIds = new HashSet<string>(state.discoveredCollectionItemIds, System.StringComparer.Ordinal),
                newItemNoticeIds = new HashSet<string>(state.newItemNoticeIds, System.StringComparer.Ordinal)
            };

            return snapshot;
        }

        public void Restore(PlayerRuntimeState state)
        {
            state.softCurrency = softCurrency;
            state.prismPearl = prismPearl;
            state.pirateCoin = pirateCoin;
            state.crewExp = crewExp;

            state.inventoryEntries.Clear();
            for (int i = 0; i < inventoryEntries.Count; i++)
            {
                InventoryEntry entry = inventoryEntries[i];
                state.inventoryEntries.Add(CloneInventoryEntry(entry));
            }

            state.itemAcquisitionCounts.Clear();
            foreach (KeyValuePair<string, int> pair in itemAcquisitionCounts)
            {
                state.itemAcquisitionCounts[pair.Key] = pair.Value;
            }

            state.discoveredCollectionItemIds.Clear();
            foreach (string id in discoveredCollectionItemIds)
            {
                state.discoveredCollectionItemIds.Add(id);
            }

            state.newItemNoticeIds.Clear();
            foreach (string id in newItemNoticeIds)
            {
                state.newItemNoticeIds.Add(id);
            }
        }

        private static List<InventoryEntry> CloneInventoryEntries(List<InventoryEntry> entries)
        {
            List<InventoryEntry> clone = new List<InventoryEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                clone.Add(CloneInventoryEntry(entries[i]));
            }

            return clone;
        }

        private static InventoryEntry CloneInventoryEntry(InventoryEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return new InventoryEntry
            {
                itemId = entry.itemId,
                count = entry.count,
                instanceId = entry.instanceId,
                levelIndex = entry.levelIndex
            };
        }
    }
}
