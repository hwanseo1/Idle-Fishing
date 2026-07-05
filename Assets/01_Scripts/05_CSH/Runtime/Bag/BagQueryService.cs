using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 가방 snapshot을 만들 때 적용할 상태 필터입니다.
    /// </summary>
    public enum BagFilter
    {
        None,
        Sellable,
        Cookable,
        NewDiscovery,
        MissingIngredient
    }

    /// <summary>
    /// 가방 목록 조회에 사용할 분류와 상태 필터 조건입니다.
    /// </summary>
    public sealed class BagQueryOptions
    {
        public string Category = "All";
        public BagFilter Filter = BagFilter.None;
    }

    /// <summary>
    /// UI에 바로 표시할 수 있도록 인벤토리와 카탈로그 정보를 합친 가방 행 데이터입니다.
    /// </summary>
    public sealed class BagItemView
    {
        public string ItemId;
        public string DisplayNameKo;
        public string Category;
        public string Rarity;
        public long SellPrice;
        public int Count;
        public bool Sellable;
        public bool Cookable;
        public bool NewDiscovery;
        public bool MissingIngredient;
        public bool Locked;
        public bool NewNotice;
    }

    /// <summary>
    /// 플레이어 인벤토리를 가방 UI용 snapshot으로 변환합니다.
    /// </summary>
    public sealed class BagQueryService
    {
        #region Dependencies

        private readonly BalanceCatalog catalog;
        private readonly PlayerRuntimeState state;

        /// <summary>
        /// 카탈로그와 플레이어 상태를 기준으로 가방 조회 서비스를 생성합니다.
        /// </summary>
        public BagQueryService(BalanceCatalog catalog, PlayerRuntimeState state)
        {
            this.catalog = catalog;
            this.state = state;
        }

        #endregion

        #region Query

        /// <summary>
        /// 현재 인벤토리를 itemId 단위로 합산하고 정렬, 분류, 상태 필터를 적용합니다.
        /// </summary>
        public List<BagItemView> BuildSnapshot(BagQueryOptions options)
        {
            List<BagItemView> result = new List<BagItemView>();
            if (catalog == null || state == null)
            {
                return result;
            }

            Dictionary<string, int> counts = AggregateStackCounts();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (!catalog.TryGetItem(pair.Key, out ItemDefinition item))
                {
                    continue;
                }

                BagItemView view = new BagItemView
                {
                    ItemId = item.ItemId,
                    DisplayNameKo = item.DisplayNameKo,
                    Category = item.Category,
                    Rarity = item.Rarity,
                    SellPrice = item.SellPrice,
                    Count = pair.Value,
                    Sellable = ItemSellPolicy.IsSellable(item),
                    Cookable = IsRecipeInput(item.ItemId),
                    NewDiscovery = !HasDiscoveredCollectionEntryForItem(item.ItemId),
                    MissingIngredient = IsMissingIngredient(item.ItemId, pair.Value),
                    Locked = state.lockedItemIds.Contains(item.ItemId),
                    NewNotice = state.newItemNoticeIds.Contains(item.ItemId)
                };

                if (MatchesCategory(view, options) && MatchesFilter(view, options))
                {
                    result.Add(view);
                }
            }

            result.Sort(CompareBagItems);
            return result;
        }

        #endregion

        #region Aggregation

        private Dictionary<string, int> AggregateStackCounts()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
            IReadOnlyList<InventoryEntry> entries = state.inventoryEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                InventoryEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.count <= 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.instanceId) || entry.levelIndex >= 0)
                {
                    AddCount(counts, entry.itemId, entry.count);
                    continue;
                }

                AddCount(counts, entry.itemId, entry.count);
            }

            return counts;
        }

        private static void AddCount(Dictionary<string, int> counts, string itemId, int count)
        {
            if (counts.TryGetValue(itemId, out int existing))
            {
                long total = (long)existing + count;
                counts[itemId] = total >= int.MaxValue ? int.MaxValue : (int)total;
                return;
            }

            counts[itemId] = count;
        }

        #endregion

        #region Item State

        private bool IsRecipeInput(string itemId)
        {
            foreach (RecipeDefinition recipe in catalog.RecipesById.Values)
            {
                if (recipe.IsEnabled && (recipe.InputItemId == itemId || recipe.InputItemId2 == itemId))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsMissingIngredient(string itemId, int count)
        {
            foreach (RecipeDefinition recipe in catalog.RecipesById.Values)
            {
                if (!recipe.IsEnabled)
                {
                    continue;
                }

                if (recipe.InputItemId == itemId && count < recipe.InputCount)
                {
                    return true;
                }

                if (recipe.InputItemId2 == itemId && count < recipe.InputCount2)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasDiscoveredCollectionEntryForItem(string itemId)
        {
            if (state.discoveredCollectionItemIds.Contains(itemId))
            {
                return true;
            }

            foreach (FishDefinition fish in catalog.FishById.Values)
            {
                if (fish.IsEnabled && fish.ItemId == itemId && state.discoveredCollectionItemIds.Contains(fish.FishId))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Filters

        private static bool MatchesCategory(BagItemView view, BagQueryOptions options)
        {
            if (options == null || string.IsNullOrEmpty(options.Category) || options.Category == "All")
            {
                return true;
            }

            if (options.Category == "Other")
            {
                return view.Category == "Ticket" || view.Category == "Special" || view.Category == "Box" || view.Category == "ChoiceTicket";
            }

            if (options.Category == "Material")
            {
                return view.Category == "Material" || view.Category == "UpgradeMaterial" || view.Category == "HighGradeMaterial";
            }

            return view.Category == options.Category;
        }

        private static bool MatchesFilter(BagItemView view, BagQueryOptions options)
        {
            if (options == null)
            {
                return true;
            }

            switch (options.Filter)
            {
                case BagFilter.Sellable:
                    return view.Sellable;
                case BagFilter.Cookable:
                    return view.Cookable;
                case BagFilter.NewDiscovery:
                    return view.NewDiscovery;
                case BagFilter.MissingIngredient:
                    return view.MissingIngredient;
                default:
                    return true;
            }
        }

        #endregion

        #region Sort

        private static int CompareBagItems(BagItemView left, BagItemView right)
        {
            int rarity = RarityRank(right.Rarity).CompareTo(RarityRank(left.Rarity));
            if (rarity != 0)
            {
                return rarity;
            }

            int sellPrice = right.SellPrice.CompareTo(left.SellPrice);
            if (sellPrice != 0)
            {
                return sellPrice;
            }

            int count = right.Count.CompareTo(left.Count);
            if (count != 0)
            {
                return count;
            }

            return string.Compare(left.DisplayNameKo, right.DisplayNameKo, System.StringComparison.Ordinal);
        }

        private static int RarityRank(string rarity)
        {
            switch (rarity)
            {
                case "Legendary":
                    return 5;
                case "Epic":
                    return 4;
                case "Rare":
                    return 3;
                case "Uncommon":
                    return 2;
                case "Common":
                    return 1;
                default:
                    return 0;
            }
        }

        #endregion
    }
}
