using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    #region Table Row Models

    /// <summary>
    /// items.csv의 한 행을 표현하는 아이템 기준 데이터입니다.
    /// </summary>
    public sealed class ItemDefinition
    {
        public string ItemId;
        public string DisplayNameKo;
        public string Category;
        public string Rarity;
        public string SourceType;
        public long SellPrice;
        public bool Stackable;
        public int MaxStack;
        public string CookTag;
        public int CrewExp;
        public int SortOrder;
        public bool IsEnabled;
        public string Notes;
    }

    /// <summary>
    /// 아이템 판매 가능 여부를 카테고리와 판매가 양쪽으로 고정합니다.
    /// sellPrice만으로 판단하면 고급재료/티켓에 실수로 가격이 들어갔을 때 사고가 날 수 있습니다.
    /// </summary>
    public static class ItemSellPolicy
    {
        public static bool IsSellable(ItemDefinition item)
        {
            return item != null &&
                   item.IsEnabled &&
                   IsSellableCategory(item.Category) &&
                   item.SellPrice > 0;
        }

        public static bool IsSellableCategory(string category)
        {
            switch (category)
            {
                case "Fish":
                case "Food":
                case "UpgradeMaterial":
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// fish.csv의 한 행을 표현하는 물고기 등급별 기준 데이터입니다.
    /// </summary>
    public sealed class FishDefinition
    {
        public string FishId;
        public string ItemId;
        public int Grade;
        public int MinSizeCm;
        public int MaxSizeCm;
        public int BaseWeight;
        public int BaseExp;
        public string DropGroup;
        public bool IsBoss;
        public bool IsEnabled;
        public string Notes;
    }

    /// <summary>
    /// shop_items.csv의 한 행을 표현하는 상점 판매 상품 데이터입니다.
    /// </summary>
    public sealed class ShopItemDefinition
    {
        public string ShopItemId;
        public string Category;
        public string PriceType;
        public long PriceAmount;
        public string RewardItemId;
        public int RewardCount;
        public string UnlockCondition;
        public int SortOrder;
        public bool IsEnabled;
        public string Notes;
        public string VisibilityCondition;
    }

    /// <summary>
    /// premium_currency_products.csv의 한 행을 표현하는 내부 결제용 Cash -> Prism Pearl 상품입니다.
    /// Cash는 UI 지갑/상점 보유량에 표시하지 않습니다.
    /// </summary>
    public sealed class PremiumCurrencyProductDefinition
    {
        public string ProductId;
        public long CashAmount;
        public long PrismPearlAmount;
        public int SortOrder;
        public bool IsEnabled;
        public string Notes;
    }

    /// <summary>
    /// recipes.csv의 한 행을 표현하는 요리 레시피 데이터입니다.
    /// </summary>
    public sealed class RecipeDefinition
    {
        public string RecipeId;
        public string InputItemId;
        public int InputCount;
        public string InputItemId2;
        public int InputCount2;
        public int DurationSec;
        public string OutputItemId;
        public int OutputCount;
        public int CrewExp;
        public string UnlockCondition;
        public bool IsEnabled;
        public string Notes;
    }

    /// <summary>
    /// collection_rewards.csv의 한 행을 표현하는 도감 보상 데이터입니다.
    /// </summary>
    public sealed class CollectionRewardDefinition
    {
        public string RewardId;
        public string RewardGroupId;
        public string ItemId;
        public string ConditionType;
        public int ConditionValue;
        public string RewardCurrency;
        public long RewardAmount;
        public string RewardItemId;
        public int RewardItemCount;
        public string ClaimId;
        public int SortOrder;
        public bool IsEnabled;
        public string Notes;
    }

    /// <summary>
    /// CSH 경제 파라미터 한 행을 표현합니다. TitleData가 비어 있으면 FisherEconomyDefaults가 같은 계약의 기본값을 제공합니다.
    /// </summary>
    public sealed class EconomyParam
    {
        public string Key;
        public string Value;
        public string ValueType;
        public string Scope;
        public bool IsEnabled;
        public string Notes;
    }

    /// <summary>
    /// stack_rules.csv의 한 행을 표현하는 분류별 기본 스택 규칙입니다.
    /// </summary>
    public sealed class StackRule
    {
        public string Category;
        public int DefaultMaxStack;
        public string OverflowPolicy;
        public bool IsEnabled;
        public string Notes;
    }

    #endregion

    /// <summary>
    /// CSV에서 생성된 Fisher 밸런스 데이터를 서비스가 조회하기 쉽게 묶은 읽기 전용 카탈로그입니다.
    /// </summary>
    public sealed class BalanceCatalog
    {
        #region Storage

        private readonly Dictionary<string, ItemDefinition> itemsById;
        private readonly Dictionary<string, FishDefinition> fishById;
        private readonly Dictionary<string, ShopItemDefinition> shopItemsById;
        private readonly Dictionary<string, PremiumCurrencyProductDefinition> premiumCurrencyProductsById;
        private readonly Dictionary<string, RecipeDefinition> recipesById;
        private readonly Dictionary<string, CollectionRewardDefinition> collectionRewardsById;
        private readonly Dictionary<string, EconomyParam> economyParamsByKey;
        private readonly Dictionary<string, StackRule> stackRulesByCategory;

        #endregion

        #region Initialization

        /// <summary>
        /// 검증이 끝난 테이블별 dictionary를 받아 읽기 전용 카탈로그를 생성합니다.
        /// </summary>
        public BalanceCatalog(
            Dictionary<string, ItemDefinition> itemsById,
            Dictionary<string, FishDefinition> fishById,
            Dictionary<string, ShopItemDefinition> shopItemsById,
            Dictionary<string, RecipeDefinition> recipesById,
            Dictionary<string, CollectionRewardDefinition> collectionRewardsById,
            Dictionary<string, EconomyParam> economyParamsByKey,
            Dictionary<string, StackRule> stackRulesByCategory,
            Dictionary<string, PremiumCurrencyProductDefinition> premiumCurrencyProductsById = null)
        {
            this.itemsById = itemsById;
            this.fishById = fishById;
            this.shopItemsById = shopItemsById;
            this.premiumCurrencyProductsById = premiumCurrencyProductsById ?? new Dictionary<string, PremiumCurrencyProductDefinition>();
            this.recipesById = recipesById;
            this.collectionRewardsById = collectionRewardsById;
            this.economyParamsByKey = economyParamsByKey;
            this.stackRulesByCategory = stackRulesByCategory;
        }

        #endregion

        #region Tables

        /// <summary>
        /// itemId 기준 아이템 테이블입니다.
        /// </summary>
        public IReadOnlyDictionary<string, ItemDefinition> ItemsById => itemsById;

        /// <summary>
        /// fishId 기준 물고기 등급 테이블입니다.
        /// </summary>
        public IReadOnlyDictionary<string, FishDefinition> FishById => fishById;

        /// <summary>
        /// shopItemId 기준 상점 상품 테이블입니다.
        /// </summary>
        public IReadOnlyDictionary<string, ShopItemDefinition> ShopItemsById => shopItemsById;

        /// <summary>
        /// 내부 결제용 Cash -> Prism Pearl 프리미엄 상품 테이블입니다. Cash는 표시 통화가 아닙니다.
        /// </summary>
        public IReadOnlyDictionary<string, PremiumCurrencyProductDefinition> PremiumCurrencyProductsById => premiumCurrencyProductsById;

        /// <summary>
        /// recipeId 기준 요리 레시피 테이블입니다.
        /// </summary>
        public IReadOnlyDictionary<string, RecipeDefinition> RecipesById => recipesById;

        /// <summary>
        /// rewardId 기준 도감 보상 테이블입니다.
        /// </summary>
        public IReadOnlyDictionary<string, CollectionRewardDefinition> CollectionRewardsById => collectionRewardsById;

        /// <summary>
        /// key 기준 경제 파라미터 테이블입니다.
        /// </summary>
        public IReadOnlyDictionary<string, EconomyParam> EconomyParamsByKey => economyParamsByKey;

        /// <summary>
        /// category 기준 기본 스택 규칙 테이블입니다.
        /// </summary>
        public IReadOnlyDictionary<string, StackRule> StackRulesByCategory => stackRulesByCategory;

        #endregion

        #region Lookups

        /// <summary>
        /// itemId로 아이템 기준 데이터를 조회합니다.
        /// </summary>
        public bool TryGetItem(string itemId, out ItemDefinition item)
        {
            return itemsById.TryGetValue(itemId, out item);
        }

        /// <summary>
        /// fishId로 등급별 물고기 기준 데이터를 조회합니다.
        /// </summary>
        public bool TryGetFish(string fishId, out FishDefinition fish)
        {
            return fishById.TryGetValue(fishId, out fish);
        }

        /// <summary>
        /// shopItemId로 상점 상품 기준 데이터를 조회합니다.
        /// </summary>
        public bool TryGetShopItem(string shopItemId, out ShopItemDefinition shopItem)
        {
            return shopItemsById.TryGetValue(shopItemId, out shopItem);
        }

        /// <summary>
        /// productId로 내부 결제용 프리미엄 상품 기준 데이터를 조회합니다.
        /// </summary>
        public bool TryGetPremiumCurrencyProduct(string productId, out PremiumCurrencyProductDefinition product)
        {
            return premiumCurrencyProductsById.TryGetValue(productId, out product);
        }

        /// <summary>
        /// recipeId로 요리 레시피 기준 데이터를 조회합니다.
        /// </summary>
        public bool TryGetRecipe(string recipeId, out RecipeDefinition recipe)
        {
            return recipesById.TryGetValue(recipeId, out recipe);
        }

        /// <summary>
        /// rewardId로 도감 보상 기준 데이터를 조회합니다.
        /// </summary>
        public bool TryGetCollectionReward(string rewardId, out CollectionRewardDefinition reward)
        {
            return collectionRewardsById.TryGetValue(rewardId, out reward);
        }

        /// <summary>
        /// 아이템의 개별 maxStack을 우선하고 없으면 분류별 기본 스택 규칙을 적용합니다.
        /// </summary>
        public int ResolveMaxStack(ItemDefinition item)
        {
            if (item == null)
            {
                return 1;
            }

            if (item.MaxStack > 0)
            {
                return item.MaxStack;
            }

            if (stackRulesByCategory.TryGetValue(item.Category, out StackRule rule) && rule.DefaultMaxStack > 0)
            {
                return rule.DefaultMaxStack;
            }

            return item.Stackable ? 99 : 1;
        }

        #endregion
    }

    /// <summary>
    /// 카탈로그 값이 없을 때도 Unity와 CloudScript가 같은 CSH 로컬 기본값을 쓰게 하는 계약입니다.
    /// </summary>
    internal static class FisherEconomyDefaults
    {
        public const long InitialBagCapacity = 30;
        public const long BagCapacityStep = 5;
        public const long BagCapacityMax = 120;
        public const long BagCapacityGoldCostBase = 500;
        public const long InitialCookingSlots = 3;
        public const long MaxCookingSlots = 3;
        public const long SpeedupTicketSeconds = 600;

        /// <summary>
        /// 아직 EconomyParamList를 쓰는 기존 영역은 CSH 기본 계약값으로 고정하고, 나머지는 호출자가 준 값을 유지합니다.
        /// </summary>
        public static long ResolveLong(string key, long fallback)
        {
            switch (key)
            {
                case "initial_bag_capacity":
                    return InitialBagCapacity;
                case "bag_capacity_step":
                    return BagCapacityStep;
                case "bag_capacity_max":
                    return BagCapacityMax;
                case "bag_capacity_gold_cost_base":
                    return BagCapacityGoldCostBase;
                case "speedup_ticket_seconds":
                    return SpeedupTicketSeconds;
                default:
                    return fallback;
            }
        }
    }
}
