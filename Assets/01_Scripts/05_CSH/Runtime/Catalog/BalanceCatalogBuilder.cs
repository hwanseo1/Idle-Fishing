using System;
using System.Collections.Generic;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// Generated CSV 원문을 카탈로그 빌더에 전달하기 위한 입력 묶음입니다.
    /// </summary>
    public sealed class BalanceCsvSet
    {
        public string ItemsCsv;
        public string FishCsv;
        public string ShopItemsCsv;
        public string PremiumCurrencyProductsCsv;
        public string RecipesCsv;
        public string CollectionRewardsCsv;
        public string EconomyParamsCsv;
        public string StackRulesCsv;
    }

    /// <summary>
    /// CSV 빌드 결과, 경고, 오류, 완성된 카탈로그를 담습니다.
    /// </summary>
    public sealed class BalanceBuildResult
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public BalanceCatalog Catalog;

        /// <summary>
        /// 오류가 없고 카탈로그가 생성되었는지 확인합니다.
        /// </summary>
        public bool Success => Errors.Count == 0 && Catalog != null;
    }

    /// <summary>
    /// Fisher Generated CSV를 파싱하고 참조 무결성을 검증해 BalanceCatalog를 생성합니다.
    /// </summary>
    public static class BalanceCatalogBuilder
    {
        #region Constants

        private const long IntBalanceLimit = int.MaxValue;

        #endregion

        #region Build Entry

        /// <summary>
        /// 필수 테이블을 모두 읽고 데이터 행 검증을 통과한 경우에만 카탈로그를 반환합니다.
        /// </summary>
        public static BalanceBuildResult Build(BalanceCsvSet csvSet)
        {
            BalanceBuildResult result = new BalanceBuildResult();
            if (csvSet == null)
            {
                result.Errors.Add("CSV set is null.");
                return result;
            }

            BalanceCsvTable items = ParseRequiredTable("items", csvSet.ItemsCsv, result, "itemId", "displayNameKo", "category", "rarity", "sourceType", "sellPrice", "stackable", "maxStack", "isEnabled");
            BalanceCsvTable fish = ParseRequiredTable("fish", csvSet.FishCsv, result, "fishId", "itemId", "grade", "isEnabled");
            BalanceCsvTable shopItems = ParseRequiredTable("shop_items", csvSet.ShopItemsCsv, result, "shopItemId", "priceType", "priceAmount", "rewardItemId", "rewardCount", "isEnabled");
            BalanceCsvTable premiumCurrencyProducts = ParseOptionalTable("premium_currency_products", csvSet.PremiumCurrencyProductsCsv, result, "productId", "cashAmount", "prismPearlAmount", "sortOrder", "isEnabled");
            BalanceCsvTable recipes = ParseRequiredTable("recipes", csvSet.RecipesCsv, result, "recipeId", "inputItemId", "inputCount", "durationSec", "outputItemId", "outputCount", "isEnabled");
            BalanceCsvTable collectionRewards = ParseRequiredTable("collection_rewards", csvSet.CollectionRewardsCsv, result, "rewardId", "itemId", "conditionType", "claimId", "isEnabled");
            BalanceCsvTable economyParams = ParseRequiredTable("economy_params", csvSet.EconomyParamsCsv, result, "key", "value", "valueType", "scope", "isEnabled");
            BalanceCsvTable stackRules = ParseRequiredTable("stack_rules", csvSet.StackRulesCsv, result, "category", "defaultMaxStack", "overflowPolicy", "isEnabled");

            if (result.Errors.Count > 0)
            {
                return result;
            }

            Dictionary<string, ItemDefinition> itemMap = BuildItems(items, result);
            Dictionary<string, StackRule> stackRuleMap = BuildStackRules(stackRules, result);
            Dictionary<string, FishDefinition> fishMap = BuildFish(fish, itemMap, result);
            Dictionary<string, ShopItemDefinition> shopMap = BuildShopItems(shopItems, itemMap, result);
            Dictionary<string, PremiumCurrencyProductDefinition> premiumCurrencyProductMap = BuildPremiumCurrencyProducts(premiumCurrencyProducts, result);
            Dictionary<string, RecipeDefinition> recipeMap = BuildRecipes(recipes, itemMap, result);
            BackfillFoodCrewExp(itemMap, recipeMap, result);
            Dictionary<string, CollectionRewardDefinition> collectionMap = BuildCollectionRewards(collectionRewards, itemMap, result);
            Dictionary<string, EconomyParam> economyMap = BuildEconomyParams(economyParams, result);
            ValidateStackRuleCoverage(itemMap, stackRuleMap, result);
            ValidateEnabledFishGradeCompleteness(fishMap, itemMap, result);
            ValidateRecipeFishInputCounts(recipeMap, itemMap, result);

            if (result.Errors.Count > 0)
            {
                return result;
            }

            result.Catalog = new BalanceCatalog(itemMap, fishMap, shopMap, recipeMap, collectionMap, economyMap, stackRuleMap, premiumCurrencyProductMap);
            return result;
        }

        #endregion

        #region Table Parsing

        private static BalanceCsvTable ParseRequiredTable(string tableName, string csvText, BalanceBuildResult result, params string[] requiredHeaders)
        {
            BalanceCsvTable table = BalanceCsvTable.FromText(tableName, csvText);
            if (table.Headers.Count == 0)
            {
                result.Errors.Add(tableName + " is empty or has no header row.");
                return table;
            }

            HashSet<string> uniqueHeaders = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < table.Headers.Count; i++)
            {
                string header = table.Headers[i];
                if (string.IsNullOrEmpty(header))
                {
                    continue;
                }

                if (!uniqueHeaders.Add(header))
                {
                    result.Errors.Add(tableName + " has duplicate header: " + header);
                }
            }

            for (int i = 0; i < requiredHeaders.Length; i++)
            {
                if (!table.HasHeader(requiredHeaders[i]))
                {
                    result.Errors.Add(tableName + " is missing required header: " + requiredHeaders[i]);
                }
            }

            return table;
        }

        private static BalanceCsvTable ParseOptionalTable(string tableName, string csvText, BalanceBuildResult result, params string[] requiredHeaders)
        {
            if (!string.IsNullOrWhiteSpace(csvText))
            {
                return ParseRequiredTable(tableName, csvText, result, requiredHeaders);
            }

            return BalanceCsvTable.FromText(tableName, string.Join(",", requiredHeaders) + "\n");
        }

        #endregion

        #region Row Builders

        private static Dictionary<string, ItemDefinition> BuildItems(BalanceCsvTable table, BalanceBuildResult result)
        {
            Dictionary<string, ItemDefinition> map = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                string itemId = RequiredString(row, "itemId", result);
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                bool isEnabled = Bool(row, "isEnabled", result);
                ItemDefinition item = new ItemDefinition
                {
                    ItemId = itemId,
                    DisplayNameKo = RequiredString(row, "displayNameKo", result),
                    Category = RequiredString(row, "category", result),
                    Rarity = RequiredString(row, "rarity", result),
                    SourceType = RequiredString(row, "sourceType", result),
                    SellPrice = Long(row, "sellPrice", result),
                    Stackable = Bool(row, "stackable", result),
                    MaxStack = Int(row, "maxStack", result),
                    CookTag = row.GetString("cookTag"),
                    CrewExp = OptionalInt(row, "crewExp", 0),
                    SortOrder = OptionalInt(row, "sortOrder", 0),
                    IsEnabled = isEnabled,
                    Notes = row.GetString("notes")
                };

                if (item.Stackable && item.MaxStack <= 0)
                {
                    result.Errors.Add(row.Location + " stackable item must have maxStack > 0.");
                }

                if (!item.Stackable && item.MaxStack > 1)
                {
                    result.Warnings.Add(row.Location + " non-stackable item has maxStack > 1. Runtime should require instanceId.");
                }

                WarnIfExceedsIntBalance(row, "sellPrice", item.SellPrice, result);
                AddUnique(map, item.ItemId, item, row, "itemId", result);
            }

            return map;
        }

        private static Dictionary<string, FishDefinition> BuildFish(BalanceCsvTable table, Dictionary<string, ItemDefinition> items, BalanceBuildResult result)
        {
            Dictionary<string, FishDefinition> map = new Dictionary<string, FishDefinition>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                FishDefinition fish = new FishDefinition
                {
                    FishId = RequiredString(row, "fishId", result),
                    ItemId = RequiredString(row, "itemId", result),
                    Grade = Int(row, "grade", result),
                    MinSizeCm = OptionalInt(row, "minSizeCm", 0),
                    MaxSizeCm = OptionalInt(row, "maxSizeCm", 0),
                    BaseWeight = OptionalInt(row, "baseWeight", 0),
                    BaseExp = OptionalInt(row, "baseExp", 0),
                    DropGroup = row.GetString("dropGroup"),
                    IsBoss = OptionalBool(row, "isBoss", false),
                    IsEnabled = Bool(row, "isEnabled", result),
                    Notes = row.GetString("notes")
                };

                if (!items.ContainsKey(fish.ItemId))
                {
                    result.Errors.Add(row.Location + " references missing itemId: " + fish.ItemId);
                }

                if (fish.Grade < 1 || fish.Grade > 3)
                {
                    result.Errors.Add(row.Location + " fish grade must be between 1 and 3.");
                }

                if (fish.MaxSizeCm > 0 && fish.MinSizeCm > fish.MaxSizeCm)
                {
                    result.Errors.Add(row.Location + " minSizeCm must be <= maxSizeCm.");
                }

                AddUnique(map, fish.FishId, fish, row, "fishId", result);
            }

            return map;
        }

        private static Dictionary<string, ShopItemDefinition> BuildShopItems(BalanceCsvTable table, Dictionary<string, ItemDefinition> items, BalanceBuildResult result)
        {
            Dictionary<string, ShopItemDefinition> map = new Dictionary<string, ShopItemDefinition>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                ShopItemDefinition shopItem = new ShopItemDefinition
                {
                    ShopItemId = RequiredString(row, "shopItemId", result),
                    Category = row.GetString("category"),
                    PriceType = RequiredString(row, "priceType", result),
                    PriceAmount = Long(row, "priceAmount", result),
                    RewardItemId = RequiredString(row, "rewardItemId", result),
                    RewardCount = Int(row, "rewardCount", result),
                    UnlockCondition = row.GetString("unlockCondition"),
                    SortOrder = OptionalInt(row, "sortOrder", 0),
                    IsEnabled = Bool(row, "isEnabled", result),
                    Notes = row.GetString("notes"),
                    VisibilityCondition = row.GetString("visibilityCondition")
                };

                if (!items.ContainsKey(shopItem.RewardItemId))
                {
                    result.Errors.Add(row.Location + " references missing rewardItemId: " + shopItem.RewardItemId);
                }

                if (!IsKnownCurrency(shopItem.PriceType))
                {
                    result.Errors.Add(row.Location + " unsupported priceType: " + shopItem.PriceType);
                }

                if (shopItem.PriceAmount < 0 || shopItem.RewardCount <= 0)
                {
                    result.Errors.Add(row.Location + " priceAmount must be >= 0 and rewardCount must be > 0.");
                }

                WarnIfExceedsIntBalance(row, "priceAmount", shopItem.PriceAmount, result);
                AddUnique(map, shopItem.ShopItemId, shopItem, row, "shopItemId", result);
            }

            return map;
        }

        private static Dictionary<string, PremiumCurrencyProductDefinition> BuildPremiumCurrencyProducts(BalanceCsvTable table, BalanceBuildResult result)
        {
            Dictionary<string, PremiumCurrencyProductDefinition> map = new Dictionary<string, PremiumCurrencyProductDefinition>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                PremiumCurrencyProductDefinition product = new PremiumCurrencyProductDefinition
                {
                    ProductId = RequiredString(row, "productId", result),
                    CashAmount = Long(row, "cashAmount", result),
                    PrismPearlAmount = Long(row, "prismPearlAmount", result),
                    SortOrder = OptionalInt(row, "sortOrder", 0),
                    IsEnabled = Bool(row, "isEnabled", result),
                    Notes = row.GetString("notes")
                };

                if (product.CashAmount <= 0 || product.PrismPearlAmount <= 0)
                {
                    result.Errors.Add(row.Location + " cashAmount and prismPearlAmount must be > 0.");
                }

                WarnIfExceedsIntBalance(row, "prismPearlAmount", product.PrismPearlAmount, result);
                AddUnique(map, product.ProductId, product, row, "productId", result);
            }

            return map;
        }

        private static Dictionary<string, RecipeDefinition> BuildRecipes(BalanceCsvTable table, Dictionary<string, ItemDefinition> items, BalanceBuildResult result)
        {
            Dictionary<string, RecipeDefinition> map = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                RecipeDefinition recipe = new RecipeDefinition
                {
                    RecipeId = RequiredString(row, "recipeId", result),
                    InputItemId = RequiredString(row, "inputItemId", result),
                    InputCount = Int(row, "inputCount", result),
                    InputItemId2 = row.GetString("inputItemId2"),
                    InputCount2 = OptionalInt(row, "inputCount2", 0),
                    DurationSec = Int(row, "durationSec", result),
                    OutputItemId = RequiredString(row, "outputItemId", result),
                    OutputCount = Int(row, "outputCount", result),
                    CrewExp = OptionalInt(row, "crewExp", 0),
                    UnlockCondition = row.GetString("unlockCondition"),
                    IsEnabled = Bool(row, "isEnabled", result),
                    Notes = row.GetString("notes")
                };

                if (!items.ContainsKey(recipe.InputItemId))
                {
                    result.Errors.Add(row.Location + " references missing inputItemId: " + recipe.InputItemId);
                }

                if (!string.IsNullOrEmpty(recipe.InputItemId2) && !items.ContainsKey(recipe.InputItemId2))
                {
                    result.Errors.Add(row.Location + " references missing inputItemId2: " + recipe.InputItemId2);
                }

                if (!items.ContainsKey(recipe.OutputItemId))
                {
                    result.Errors.Add(row.Location + " references missing outputItemId: " + recipe.OutputItemId);
                }

                if (recipe.InputCount <= 0 || recipe.OutputCount <= 0 || recipe.DurationSec < 0)
                {
                    result.Errors.Add(row.Location + " recipe counts must be > 0 and durationSec must be >= 0.");
                }

                if (string.IsNullOrEmpty(recipe.InputItemId2))
                {
                    result.Warnings.Add(row.Location + " inputItemId2 is empty. Current cooking contract prefers two different fish inputs.");
                }
                else if (recipe.InputCount2 <= 0)
                {
                    result.Errors.Add(row.Location + " inputCount2 must be > 0 when inputItemId2 is set.");
                }

                if (string.IsNullOrEmpty(recipe.InputItemId2) && recipe.InputCount2 > 0)
                {
                    result.Errors.Add(row.Location + " inputItemId2 is required when inputCount2 is set.");
                }

                AddUnique(map, recipe.RecipeId, recipe, row, "recipeId", result);
            }

            return map;
        }

        private static void BackfillFoodCrewExp(
            Dictionary<string, ItemDefinition> items,
            Dictionary<string, RecipeDefinition> recipes,
            BalanceBuildResult result)
        {
            if (items == null || recipes == null)
            {
                return;
            }

            foreach (RecipeDefinition recipe in recipes.Values)
            {
                if (recipe == null ||
                    string.IsNullOrEmpty(recipe.OutputItemId) ||
                    recipe.CrewExp <= 0 ||
                    !items.TryGetValue(recipe.OutputItemId, out ItemDefinition item) ||
                    item == null ||
                    !string.Equals(item.Category, "Food", StringComparison.Ordinal))
                {
                    continue;
                }

                if (item.CrewExp <= 0)
                {
                    item.CrewExp = recipe.CrewExp;
                    continue;
                }

                if (item.CrewExp != recipe.CrewExp)
                {
                    result?.Warnings.Add(
                        "Food crewExp differs from recipe crewExp: " +
                        item.ItemId +
                        " / food=" +
                        item.CrewExp +
                        " recipe=" +
                        recipe.CrewExp +
                        " recipeId=" +
                        recipe.RecipeId);
                }
            }
        }

        private static Dictionary<string, CollectionRewardDefinition> BuildCollectionRewards(BalanceCsvTable table, Dictionary<string, ItemDefinition> items, BalanceBuildResult result)
        {
            Dictionary<string, CollectionRewardDefinition> map = new Dictionary<string, CollectionRewardDefinition>(StringComparer.Ordinal);
            HashSet<string> claimIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                CollectionRewardDefinition reward = new CollectionRewardDefinition
                {
                    RewardId = RequiredString(row, "rewardId", result),
                    RewardGroupId = row.GetString("rewardGroupId"),
                    ItemId = RequiredString(row, "itemId", result),
                    ConditionType = RequiredString(row, "conditionType", result),
                    ConditionValue = OptionalInt(row, "conditionValue", 1),
                    RewardCurrency = row.GetString("rewardCurrency"),
                    RewardAmount = OptionalLong(row, "rewardAmount", 0),
                    RewardItemId = row.GetString("rewardItemId"),
                    RewardItemCount = OptionalInt(row, "rewardItemCount", 0),
                    ClaimId = RequiredString(row, "claimId", result),
                    SortOrder = OptionalInt(row, "sortOrder", 0),
                    IsEnabled = Bool(row, "isEnabled", result),
                    Notes = row.GetString("notes")
                };

                if (!items.ContainsKey(reward.ItemId))
                {
                    result.Errors.Add(row.Location + " references missing itemId: " + reward.ItemId);
                }

                if (!string.IsNullOrEmpty(reward.RewardItemId) && !items.ContainsKey(reward.RewardItemId))
                {
                    result.Errors.Add(row.Location + " references missing rewardItemId: " + reward.RewardItemId);
                }

                if (reward.RewardAmount > 0 && !IsKnownCurrency(reward.RewardCurrency))
                {
                    result.Errors.Add(row.Location + " unsupported rewardCurrency: " + reward.RewardCurrency);
                }

                if (!claimIds.Add(reward.ClaimId))
                {
                    result.Errors.Add(row.Location + " duplicate claimId: " + reward.ClaimId);
                }

                if (reward.ConditionType != "discovery" && reward.ConditionType != "count")
                {
                    result.Errors.Add(row.Location + " unsupported conditionType: " + reward.ConditionType);
                }

                if (reward.ConditionValue <= 0)
                {
                    result.Errors.Add(row.Location + " conditionValue must be > 0.");
                }

                if (reward.RewardAmount < 0 || reward.RewardItemCount < 0)
                {
                    result.Errors.Add(row.Location + " reward amounts must be >= 0.");
                }

                WarnIfExceedsIntBalance(row, "rewardAmount", reward.RewardAmount, result);
                if (reward.RewardAmount == 0 && reward.RewardItemCount == 0)
                {
                    result.Warnings.Add(row.Location + " collection reward has no currency or item reward.");
                }

                AddUnique(map, reward.RewardId, reward, row, "rewardId", result);
            }

            return map;
        }

        #endregion

        #region Cross Table Validation

        private static void ValidateStackRuleCoverage(
            Dictionary<string, ItemDefinition> items,
            Dictionary<string, StackRule> stackRules,
            BalanceBuildResult result)
        {
            foreach (ItemDefinition item in items.Values)
            {
                if (item == null || !item.IsEnabled || string.IsNullOrEmpty(item.Category))
                {
                    continue;
                }

                if (!stackRules.TryGetValue(item.Category, out StackRule rule) || rule == null || !rule.IsEnabled)
                {
                    result.Warnings.Add("items:" + item.ItemId + " category has no enabled stack rule: " + item.Category);
                }
            }
        }

        private static void ValidateEnabledFishGradeCompleteness(
            Dictionary<string, FishDefinition> fish,
            Dictionary<string, ItemDefinition> items,
            BalanceBuildResult result)
        {
            Dictionary<string, HashSet<int>> gradesByItemId = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

            foreach (FishDefinition definition in fish.Values)
            {
                if (definition == null || !definition.IsEnabled)
                {
                    continue;
                }

                if (!items.TryGetValue(definition.ItemId, out ItemDefinition item) ||
                    item == null ||
                    !item.IsEnabled ||
                    item.Category != "Fish")
                {
                    continue;
                }

                if (!gradesByItemId.TryGetValue(definition.ItemId, out HashSet<int> grades))
                {
                    grades = new HashSet<int>();
                    gradesByItemId.Add(definition.ItemId, grades);
                }

                grades.Add(definition.Grade);
            }

            foreach (KeyValuePair<string, HashSet<int>> pair in gradesByItemId)
            {
                for (int grade = 1; grade <= 3; grade++)
                {
                    if (!pair.Value.Contains(grade))
                    {
                        result.Errors.Add("fish:" + pair.Key + " enabled species is missing grade " + grade + ".");
                    }
                }
            }
        }

        private static void ValidateRecipeFishInputCounts(
            Dictionary<string, RecipeDefinition> recipes,
            Dictionary<string, ItemDefinition> items,
            BalanceBuildResult result)
        {
            foreach (RecipeDefinition recipe in recipes.Values)
            {
                if (recipe == null || !recipe.IsEnabled)
                {
                    continue;
                }

                if (!items.TryGetValue(recipe.InputItemId, out ItemDefinition inputItem) ||
                    inputItem == null ||
                    inputItem.Category != "Fish")
                {
                    continue;
                }

                if (string.IsNullOrEmpty(recipe.InputItemId2))
                {
                    continue;
                }

                if (!items.TryGetValue(recipe.InputItemId2, out ItemDefinition secondInputItem) ||
                    secondInputItem == null ||
                    secondInputItem.Category != "Fish")
                {
                    result.Errors.Add("recipes:" + recipe.RecipeId + " inputItemId2 must reference a Fish item.");
                    continue;
                }

                if (recipe.InputItemId == recipe.InputItemId2)
                {
                    result.Errors.Add("recipes:" + recipe.RecipeId + " must use two different fish itemIds.");
                }
            }
        }

        #endregion

        #region Support Table Builders

        private static Dictionary<string, EconomyParam> BuildEconomyParams(BalanceCsvTable table, BalanceBuildResult result)
        {
            Dictionary<string, EconomyParam> map = new Dictionary<string, EconomyParam>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                EconomyParam param = new EconomyParam
                {
                    Key = RequiredString(row, "key", result),
                    Value = RequiredString(row, "value", result),
                    ValueType = RequiredString(row, "valueType", result),
                    Scope = row.GetString("scope"),
                    IsEnabled = Bool(row, "isEnabled", result),
                    Notes = row.GetString("notes")
                };

                AddUnique(map, param.Key, param, row, "key", result);
            }

            return map;
        }

        private static Dictionary<string, StackRule> BuildStackRules(BalanceCsvTable table, BalanceBuildResult result)
        {
            Dictionary<string, StackRule> map = new Dictionary<string, StackRule>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                StackRule rule = new StackRule
                {
                    Category = RequiredString(row, "category", result),
                    DefaultMaxStack = Int(row, "defaultMaxStack", result),
                    OverflowPolicy = RequiredString(row, "overflowPolicy", result),
                    IsEnabled = Bool(row, "isEnabled", result),
                    Notes = row.GetString("notes")
                };

                if (rule.DefaultMaxStack <= 0)
                {
                    result.Errors.Add(row.Location + " defaultMaxStack must be > 0.");
                }

                AddUnique(map, rule.Category, rule, row, "category", result);
            }

            return map;
        }

        #endregion

        #region Primitive Parsing

        private static void AddUnique<T>(Dictionary<string, T> map, string key, T value, BalanceCsvRow row, string keyName, BalanceBuildResult result)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (map.ContainsKey(key))
            {
                result.Errors.Add(row.Location + " duplicate " + keyName + ": " + key);
                return;
            }

            map.Add(key, value);
        }

        private static string RequiredString(BalanceCsvRow row, string header, BalanceBuildResult result)
        {
            string value = row.GetString(header);
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add(row.Location + " missing required value: " + header);
            }

            return value;
        }

        private static int Int(BalanceCsvRow row, string header, BalanceBuildResult result)
        {
            if (row.TryGetInt(header, out int value))
            {
                return value;
            }

            result.Errors.Add(row.Location + " invalid int: " + header);
            return 0;
        }

        private static int OptionalInt(BalanceCsvRow row, string header, int fallback)
        {
            return row.TryGetInt(header, out int value) ? value : fallback;
        }

        private static long Long(BalanceCsvRow row, string header, BalanceBuildResult result)
        {
            if (row.TryGetLong(header, out long value))
            {
                return value;
            }

            result.Errors.Add(row.Location + " invalid long: " + header);
            return 0;
        }

        private static long OptionalLong(BalanceCsvRow row, string header, long fallback)
        {
            return row.TryGetLong(header, out long value) ? value : fallback;
        }

        private static bool Bool(BalanceCsvRow row, string header, BalanceBuildResult result)
        {
            if (row.TryGetBool(header, out bool value))
            {
                return value;
            }

            result.Errors.Add(row.Location + " invalid bool: " + header);
            return false;
        }

        private static bool OptionalBool(BalanceCsvRow row, string header, bool fallback)
        {
            return row.TryGetBool(header, out bool value) ? value : fallback;
        }

        private static void WarnIfExceedsIntBalance(BalanceCsvRow row, string header, long value, BalanceBuildResult result)
        {
            if (value > IntBalanceLimit)
            {
                result.Warnings.Add(row.Location + " " + header + " exceeds int balance limit: " + value + " > " + IntBalanceLimit + ".");
            }
        }

        private static bool IsKnownCurrency(string currency)
        {
            return FisherCurrencyContract.IsKnownCurrency(currency);
        }

        #endregion
    }
}
