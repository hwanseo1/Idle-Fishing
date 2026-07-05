using System;
using System.Collections.Generic;
using Fisher.Data;
using Newtonsoft.Json.Linq;
using RMS.Data;
using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 런타임 SO와 로컬 TitleData JSON을 기존 Fisher 서비스가 쓰는 BalanceCatalog로 변환합니다.
    /// </summary>
    public static class FisherLocalCatalogBuilder
    {
        private const string FishListKey = "FishList";
        private const string RecipeListKey = "RecipeList";
        private const string ShopItemListKey = "ShopItemList";
        private const string PremiumCurrencyProductListKey = "PremiumCurrencyProductList";
        private const string EconomyParamListKey = "EconomyParamList";
        private const string StackRuleListKey = "StackRuleList";
        private const string CollectionRewardListKey = "CollectionRewardList";
        private const string FragmentListKey = "FragmentList";

        public static BalanceBuildResult Build(FisherRuntimeCatalogSource source)
        {
            BalanceBuildResult result = new BalanceBuildResult();
            if (source == null)
            {
                result.Errors.Add("Fisher runtime catalog source is missing.");
                return result;
            }

            Dictionary<string, JObject> jsonByName = ParseJsonAssets(source.RuntimeJsonAssets, result);
            if (result.Errors.Count > 0)
            {
                return result;
            }

            JObject fishJson = OptionalJson(jsonByName, FishListKey);
            JObject fragmentJson = OptionalJson(jsonByName, FragmentListKey);
            Dictionary<string, StackRule> stackRules = BuildStackRules(RequiredJson(jsonByName, StackRuleListKey, result), result);
            Dictionary<string, ItemDefinition> items = BuildItems(source, fishJson, fragmentJson, stackRules, result);
            Dictionary<string, FishDefinition> fish = BuildFish(source, items, fishJson, result);
            Dictionary<string, RecipeDefinition> recipes = BuildRecipes(RequiredJson(jsonByName, RecipeListKey, result), items, result);
            BackfillFoodCrewExp(items, recipes, result);
            Dictionary<string, ShopItemDefinition> shopItems = BuildShopItems(RequiredJson(jsonByName, ShopItemListKey, result), items, result);
            Dictionary<string, PremiumCurrencyProductDefinition> premiumCurrencyProducts = BuildPremiumCurrencyProducts(OptionalRuntimeJson(jsonByName, PremiumCurrencyProductListKey, result), result);
            Dictionary<string, EconomyParam> economyParams = BuildEconomyParams(RequiredJson(jsonByName, EconomyParamListKey, result), result);
            Dictionary<string, CollectionRewardDefinition> collectionRewards = BuildCollectionRewards(RequiredJson(jsonByName, CollectionRewardListKey, result), items, result);

            if (result.Errors.Count > 0)
            {
                return result;
            }

            result.Catalog = new BalanceCatalog(items, fish, shopItems, recipes, collectionRewards, economyParams, stackRules, premiumCurrencyProducts);
            return result;
        }

        private static Dictionary<string, JObject> ParseJsonAssets(TextAsset[] assets, BalanceBuildResult result)
        {
            Dictionary<string, JObject> jsonByName = new Dictionary<string, JObject>(StringComparer.Ordinal);
            if (assets == null)
            {
                return jsonByName;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                TextAsset asset = assets[i];
                if (asset == null)
                {
                    continue;
                }

                try
                {
                    jsonByName[asset.name] = JObject.Parse(asset.text);
                }
                catch (Exception exception)
                {
                    result.Errors.Add(asset.name + " JSON parse failed: " + exception.Message);
                }
            }

            return jsonByName;
        }

        private static JObject RequiredJson(Dictionary<string, JObject> jsonByName, string key, BalanceBuildResult result)
        {
            if (jsonByName != null && jsonByName.TryGetValue(key, out JObject value) && value != null)
            {
                return value;
            }

            result.Errors.Add("Runtime JSON TextAsset is missing: " + key);
            return new JObject();
        }

        private static JObject OptionalJson(Dictionary<string, JObject> jsonByName, string key)
        {
            return jsonByName != null && jsonByName.TryGetValue(key, out JObject value) ? value : null;
        }

        private static JObject OptionalRuntimeJson(Dictionary<string, JObject> jsonByName, string key, BalanceBuildResult result)
        {
            JObject value = OptionalJson(jsonByName, key);
            if (value != null)
            {
                return value;
            }

            TextAsset asset = Resources.Load<TextAsset>("05_CSH/RuntimeCatalog/" + key);
            if (asset == null)
            {
                return null;
            }

            try
            {
                return JObject.Parse(asset.text);
            }
            catch (Exception exception)
            {
                result.Errors.Add(key + " JSON parse failed: " + exception.Message);
                return null;
            }
        }

        private static Dictionary<string, ItemDefinition> BuildItems(
            FisherRuntimeCatalogSource source,
            JObject fishJson,
            JObject fragmentJson,
            Dictionary<string, StackRule> stackRules,
            BalanceBuildResult result)
        {
            Dictionary<string, ItemDefinition> items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

            foreach (FishData fishData in source.FishDataAssets)
            {
                if (fishData == null || string.IsNullOrWhiteSpace(fishData.FishId))
                {
                    continue;
                }

                JObject server = fishJson?[fishData.FishId] as JObject;
                bool enabled = Bool(server, "enabled", true);
                bool sellable = Bool(server, "sellable", true);
                AddUnique(items, fishData.FishId, new ItemDefinition
                {
                    ItemId = fishData.FishId,
                    DisplayNameKo = String(server, "displayNameKo", fishData.DisplayName),
                    Category = "Fish",
                    Rarity = fishData.Rarity.ToString(),
                    SourceType = "Fishing",
                    SellPrice = sellable ? Long(server, "sellGold", fishData.SellPriceGold) : 0,
                    Stackable = true,
                    MaxStack = ResolveDefaultMaxStack(stackRules, "Fish", 999),
                    CookTag = string.Empty,
                    CrewExp = 0,
                    SortOrder = 0,
                    IsEnabled = enabled,
                    Notes = fishData.Description
                }, result);
            }

            foreach (ItemData asset in source.ItemDataAssets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.ItemId))
                {
                    continue;
                }

                if (asset.Category == ItemCategory.Fish)
                {
                    result.Warnings.Add("Fish ItemData is ignored because RMS FishData is the fish source: " + asset.ItemId);
                    continue;
                }

                JObject server = FindItemJson(source.RuntimeJsonAssets, asset.ItemId);
                bool enabled = Bool(server, "enabled", asset.IsEnabled);
                bool sellable = Bool(server, "sellable", ItemSellPolicy.IsSellableCategory(asset.Category.ToString()));
                AddUnique(items, asset.ItemId, new ItemDefinition
                {
                    ItemId = asset.ItemId,
                    DisplayNameKo = String(server, "displayNameKo", asset.DisplayName),
                    Category = String(server, "category", asset.Category.ToString()),
                    Rarity = asset.Rarity.ToString(),
                    SourceType = String(server, "sourceType", asset.SourceType),
                    SellPrice = sellable ? Long(server, "sellGold", asset.SellPriceGold) : 0,
                    Stackable = asset.Stackable,
                    MaxStack = asset.MaxStack,
                    CookTag = String(server, "cookTag", string.Empty),
                    CrewExp = Int(server, "crewExp", 0),
                    SortOrder = 0,
                    IsEnabled = enabled,
                    Notes = asset.Description
                }, result);
            }

            ApplyFragmentListItems(items, fragmentJson, stackRules, result);
            return items;
        }

        private static void ApplyFragmentListItems(
            Dictionary<string, ItemDefinition> items,
            JObject fragmentJson,
            Dictionary<string, StackRule> stackRules,
            BalanceBuildResult result)
        {
            if (items == null || fragmentJson == null)
            {
                return;
            }

            int defaultMaxStack = ResolveDefaultMaxStack(stackRules, "Ticket", 999);
            foreach (JProperty property in fragmentJson.Properties())
            {
                string itemId = property.Name;
                if (string.IsNullOrWhiteSpace(itemId) ||
                    !itemId.StartsWith("fragment_Crew_", StringComparison.Ordinal))
                {
                    result.Warnings.Add("FragmentList entry skipped because itemId is not fragment_Crew_*: " + itemId);
                    continue;
                }

                string crewId = property.Value == null || property.Value.Type == JTokenType.Null
                    ? string.Empty
                    : property.Value.Type == JTokenType.Object
                        ? String((JObject)property.Value, "crewId", string.Empty)
                        : property.Value.ToString();

                if (!items.TryGetValue(itemId, out ItemDefinition item) || item == null)
                {
                    item = new ItemDefinition
                    {
                        ItemId = itemId,
                        DisplayNameKo = string.IsNullOrWhiteSpace(crewId) ? itemId : crewId + " 조각",
                        Rarity = "Common",
                        CrewExp = 0,
                        SortOrder = 0,
                        Notes = string.IsNullOrWhiteSpace(crewId) ? "FragmentList crew fragment." : "FragmentList crew fragment for " + crewId + "."
                    };
                    AddUnique(items, itemId, item, result);
                }

                item.Category = "Ticket";
                item.SourceType = "Recruit";
                item.SellPrice = 0;
                item.Stackable = true;
                item.MaxStack = item.MaxStack > 0 ? item.MaxStack : defaultMaxStack;
                item.CookTag = "crew_fragment";
                item.IsEnabled = true;
            }
        }

        private static JObject FindItemJson(TextAsset[] jsonAssets, string itemId)
        {
            if (jsonAssets == null || string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            for (int i = 0; i < jsonAssets.Length; i++)
            {
                TextAsset asset = jsonAssets[i];
                if (asset == null ||
                    asset.name == RecipeListKey ||
                    asset.name == ShopItemListKey ||
                    asset.name == PremiumCurrencyProductListKey ||
                    asset.name == EconomyParamListKey ||
                    asset.name == StackRuleListKey ||
                    asset.name == CollectionRewardListKey)
                {
                    continue;
                }

                try
                {
                    JObject root = JObject.Parse(asset.text);
                    if (root[itemId] is JObject item)
                    {
                        return item;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static Dictionary<string, FishDefinition> BuildFish(
            FisherRuntimeCatalogSource source,
            Dictionary<string, ItemDefinition> items,
            JObject fishJson,
            BalanceBuildResult result)
        {
            Dictionary<string, FishDefinition> fish = new Dictionary<string, FishDefinition>(StringComparer.Ordinal);
            foreach (FishData fishData in source.FishDataAssets)
            {
                if (fishData == null || string.IsNullOrWhiteSpace(fishData.FishId))
                {
                    continue;
                }

                if (!items.TryGetValue(fishData.FishId, out ItemDefinition item))
                {
                    result.Errors.Add("Fish item definition is missing for RMS FishData: " + fishData.FishId);
                    continue;
                }

                JObject server = fishJson?[fishData.FishId] as JObject;
                AddUnique(fish, fishData.FishId, new FishDefinition
                {
                    FishId = fishData.FishId,
                    ItemId = fishData.FishId,
                    Grade = RarityToGrade(fishData.Rarity),
                    MinSizeCm = 0,
                    MaxSizeCm = 0,
                    BaseWeight = Mathf.RoundToInt(fishData.SpawnWeight),
                    BaseExp = 0,
                    DropGroup = fishData.AllowedStageIds == null ? string.Empty : string.Join("|", fishData.AllowedStageIds),
                    IsBoss = false,
                    IsEnabled = Bool(server, "enabled", item.IsEnabled),
                    Notes = fishData.Description
                }, result);
            }

            return fish;
        }

        private static Dictionary<string, RecipeDefinition> BuildRecipes(JObject root, Dictionary<string, ItemDefinition> items, BalanceBuildResult result)
        {
            Dictionary<string, RecipeDefinition> recipes = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                if (node == null)
                {
                    result.Errors.Add("Recipe node must be an object: " + property.Name);
                    continue;
                }

                JArray inputs = node["inputs"] as JArray;
                JObject firstInput = inputs != null && inputs.Count > 0 ? inputs[0] as JObject : null;
                JObject secondInput = inputs != null && inputs.Count > 1 ? inputs[1] as JObject : null;
                JObject output = node["output"] as JObject;
                string inputItemId = String(firstInput, "itemId", string.Empty);
                string inputItemId2 = String(secondInput, "itemId", string.Empty);
                string outputItemId = String(output, "itemId", string.Empty);

                AssertItemExists(items, inputItemId, property.Name + ".inputs[0]", result);
                AssertItemExists(items, inputItemId2, property.Name + ".inputs[1]", result);
                AssertItemExists(items, outputItemId, property.Name + ".output", result);

                AddUnique(recipes, property.Name, new RecipeDefinition
                {
                    RecipeId = property.Name,
                    InputItemId = inputItemId,
                    InputCount = Int(firstInput, "amount", 0),
                    InputItemId2 = inputItemId2,
                    InputCount2 = Int(secondInput, "amount", 0),
                    DurationSec = Int(node, "durationSec", 0),
                    OutputItemId = outputItemId,
                    OutputCount = Int(output, "amount", 0),
                    CrewExp = Int(node, "crewExp", 0),
                    UnlockCondition = String(node, "unlockCondition", string.Empty),
                    IsEnabled = Bool(node, "enabled", true),
                    Notes = string.Empty
                }, result);
            }

            return recipes;
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

        private static Dictionary<string, ShopItemDefinition> BuildShopItems(JObject root, Dictionary<string, ItemDefinition> items, BalanceBuildResult result)
        {
            Dictionary<string, ShopItemDefinition> shopItems = new Dictionary<string, ShopItemDefinition>(StringComparer.Ordinal);
            int sortOrder = 0;
            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                JObject reward = node?["reward"] as JObject;
                string rewardItemId = String(reward, "itemId", string.Empty);
                AssertItemExists(items, rewardItemId, property.Name + ".reward", result);

                AddUnique(shopItems, property.Name, new ShopItemDefinition
                {
                    ShopItemId = property.Name,
                    Category = String(node, "category", string.Empty),
                    PriceType = String(node, "priceType", string.Empty),
                    PriceAmount = Long(node, "priceAmount", 0),
                    RewardItemId = rewardItemId,
                    RewardCount = Int(reward, "amount", 0),
                    UnlockCondition = String(node, "unlockCondition", string.Empty),
                    SortOrder = sortOrder++,
                    IsEnabled = Bool(node, "enabled", true),
                    Notes = string.Empty,
                    VisibilityCondition = String(node, "visibilityCondition", string.Empty)
                }, result);
            }

            return shopItems;
        }

        private static Dictionary<string, PremiumCurrencyProductDefinition> BuildPremiumCurrencyProducts(JObject root, BalanceBuildResult result)
        {
            Dictionary<string, PremiumCurrencyProductDefinition> products = new Dictionary<string, PremiumCurrencyProductDefinition>(StringComparer.Ordinal);
            if (root == null)
            {
                return products;
            }

            int fallbackSortOrder = 0;
            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                PremiumCurrencyProductDefinition product = new PremiumCurrencyProductDefinition
                {
                    ProductId = property.Name,
                    CashAmount = Long(node, "cashAmount", 0),
                    PrismPearlAmount = Long(node, "prismPearlAmount", 0),
                    SortOrder = Int(node, "sortOrder", fallbackSortOrder++),
                    IsEnabled = Bool(node, "enabled", true),
                    Notes = String(node, "notes", string.Empty)
                };

                if (product.CashAmount <= 0 || product.PrismPearlAmount <= 0)
                {
                    result.Errors.Add("Premium currency product must have positive cashAmount and prismPearlAmount: " + property.Name);
                }

                AddUnique(products, product.ProductId, product, result);
            }

            return products;
        }

        private static Dictionary<string, EconomyParam> BuildEconomyParams(JObject root, BalanceBuildResult result)
        {
            Dictionary<string, EconomyParam> economy = new Dictionary<string, EconomyParam>(StringComparer.Ordinal);
            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                AddUnique(economy, property.Name, new EconomyParam
                {
                    Key = property.Name,
                    Value = String(node, "value", string.Empty),
                    ValueType = String(node, "valueType", "string"),
                    Scope = String(node, "scope", string.Empty),
                    IsEnabled = Bool(node, "enabled", true),
                    Notes = string.Empty
                }, result);
            }

            return economy;
        }

        private static Dictionary<string, StackRule> BuildStackRules(JObject root, BalanceBuildResult result)
        {
            Dictionary<string, StackRule> stackRules = new Dictionary<string, StackRule>(StringComparer.Ordinal);
            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                AddUnique(stackRules, property.Name, new StackRule
                {
                    Category = property.Name,
                    DefaultMaxStack = Int(node, "defaultMaxStack", 99),
                    OverflowPolicy = String(node, "overflowPolicy", "reject"),
                    IsEnabled = Bool(node, "enabled", true),
                    Notes = string.Empty
                }, result);
            }

            return stackRules;
        }

        private static Dictionary<string, CollectionRewardDefinition> BuildCollectionRewards(
            JObject root,
            Dictionary<string, ItemDefinition> items,
            BalanceBuildResult result)
        {
            Dictionary<string, CollectionRewardDefinition> rewards = new Dictionary<string, CollectionRewardDefinition>(StringComparer.Ordinal);
            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                string itemId = String(node, "itemId", string.Empty);
                string rewardItemId = String(node, "rewardItemId", string.Empty);
                AssertItemExists(items, itemId, property.Name + ".itemId", result);
                if (!string.IsNullOrEmpty(rewardItemId))
                {
                    AssertItemExists(items, rewardItemId, property.Name + ".rewardItemId", result);
                }

                AddUnique(rewards, property.Name, new CollectionRewardDefinition
                {
                    RewardId = property.Name,
                    RewardGroupId = String(node, "rewardGroupId", string.Empty),
                    ItemId = itemId,
                    ConditionType = String(node, "conditionType", string.Empty),
                    ConditionValue = Int(node, "conditionValue", 1),
                    RewardCurrency = String(node, "rewardCurrency", string.Empty),
                    RewardAmount = Long(node, "rewardAmount", 0),
                    RewardItemId = rewardItemId,
                    RewardItemCount = Int(node, "rewardItemCount", 0),
                    ClaimId = String(node, "claimId", property.Name),
                    SortOrder = Int(node, "sortOrder", 0),
                    IsEnabled = Bool(node, "enabled", true),
                    Notes = string.Empty
                }, result);
            }

            return rewards;
        }

        private static void AssertItemExists(Dictionary<string, ItemDefinition> items, string itemId, string location, BalanceBuildResult result)
        {
            if (!string.IsNullOrEmpty(itemId) && items.ContainsKey(itemId))
            {
                return;
            }

            result.Errors.Add(location + " references missing itemId: " + itemId);
        }

        private static void AddUnique<T>(Dictionary<string, T> map, string key, T value, BalanceBuildResult result)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                result.Errors.Add("Catalog key is empty.");
                return;
            }

            if (map.ContainsKey(key))
            {
                result.Errors.Add("Duplicate catalog key: " + key);
                return;
            }

            map.Add(key, value);
        }

        private static int ResolveDefaultMaxStack(Dictionary<string, StackRule> stackRules, string category, int fallback)
        {
            return stackRules != null &&
                   stackRules.TryGetValue(category, out StackRule rule) &&
                   rule != null &&
                   rule.DefaultMaxStack > 0
                ? rule.DefaultMaxStack
                : fallback;
        }

        private static int RarityToGrade(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Common:
                    return 1;
                case FishRarity.Rare:
                    return 2;
                default:
                    return 3;
            }
        }

        private static string String(JObject node, string key, string fallback)
        {
            return node != null && node.TryGetValue(key, out JToken token) && token.Type != JTokenType.Null
                ? token.ToString()
                : fallback;
        }

        private static bool Bool(JObject node, string key, bool fallback)
        {
            return node != null && node.TryGetValue(key, out JToken token) && token.Type != JTokenType.Null
                ? token.Value<bool>()
                : fallback;
        }

        private static int Int(JObject node, string key, int fallback)
        {
            return node != null && node.TryGetValue(key, out JToken token) && token.Type != JTokenType.Null && int.TryParse(token.ToString(), out int value)
                ? value
                : fallback;
        }

        private static long Long(JObject node, string key, long fallback)
        {
            return node != null && node.TryGetValue(key, out JToken token) && token.Type != JTokenType.Null && long.TryParse(token.ToString(), out long value)
                ? value
                : fallback;
        }
    }
}
