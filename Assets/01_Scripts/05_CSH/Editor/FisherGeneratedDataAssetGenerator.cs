using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Fisher.Data;
using Newtonsoft.Json;
using RMS.Data;
using UnityEditor;
using UnityEngine;

namespace Fisher.PlayerSystems.Editor
{
    /// <summary>
    /// Generated CSV를 팀원이 확인하기 쉬운 ItemData / RecipeData 에셋으로 동기화합니다.
    /// 런타임은 생성된 SO와 로컬 TitleData JSON 카탈로그를 읽습니다.
    /// </summary>
    public static class FisherGeneratedDataAssetGenerator
    {
        #region Constants

        private const string GeneratedFolder = "Assets/03_Data/05_CSH/Generated";
        private const string ItemAssetFolder = "Assets/03_Data/05_CSH/Items";
        private const string FoodAssetFolder = "Assets/03_Data/05_CSH/Food";
        private const string FoodDatabaseAssetPath = FoodAssetFolder + "/FoodDatabase.asset";
        private const string RecipeAssetFolder = "Assets/03_Data/05_CSH/Recipes";
        private const string RmsFishDataFolder = "Assets/03_Data/01_RMS/FishData";
        private const string RuntimeCatalogFolder = "Assets/Resources/05_CSH/RuntimeCatalog";
        private const string RuntimeCatalogAssetPath = RuntimeCatalogFolder + "/FisherRuntimeCatalogSource.asset";
        private const string UiArtProfileAssetPath = "Assets/Resources/05_CSH/UI/FisherUiArtProfile.asset";
        private const string EtcIconFolder = "Assets/04_Resources/05_CSH/ETC";
        private static readonly string[] RuntimeJsonNames =
        {
            "FishList",
            "FoodList",
            "IngredientList",
            "TicketList",
            "BoxList",
            "FragmentList",
            "RecipeList",
            "ShopItemList",
            "PremiumCurrencyProductList",
            "EconomyParamList",
            "StackRuleList",
            "CollectionRewardList"
        };

        #endregion

        #region Menu Entry

        [MenuItem("FISHER/데이터/Generated CSV에서 Data 에셋 갱신")]
        public static void GenerateFromMenu()
        {
            try
            {
                GenerationReport report = Generate();
                Debug.Log(report.BuildMessage());
                EditorUtility.DisplayDialog("Fisher Data 에셋 갱신", report.BuildMessage(), "확인");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Fisher Data Asset Generator]\n" + exception);
                EditorUtility.DisplayDialog("Fisher Data 에셋 갱신 실패", exception.Message, "확인");
            }
        }

        public static void GenerateFromCommandLine()
        {
            try
            {
                GenerationReport report = Generate();
                Debug.Log(report.BuildMessage());
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[Fisher Data Asset Generator]\n" + exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        #endregion

        #region Generation

        private static GenerationReport Generate()
        {
            BalanceBuildResult build = BalanceCatalogBuilder.Build(new BalanceCsvSet
            {
                ItemsCsv = ReadGeneratedCsv("items.csv"),
                FishCsv = ReadGeneratedCsv("fish.csv"),
                ShopItemsCsv = ReadGeneratedCsv("shop_items.csv"),
                PremiumCurrencyProductsCsv = ReadGeneratedCsv("premium_currency_products.csv", optional: true),
                RecipesCsv = ReadGeneratedCsv("recipes.csv"),
                CollectionRewardsCsv = ReadGeneratedCsv("collection_rewards.csv"),
                EconomyParamsCsv = ReadGeneratedCsv("economy_params.csv"),
                StackRulesCsv = ReadGeneratedCsv("stack_rules.csv")
            });

            if (!build.Success)
            {
                throw new InvalidOperationException("Generated CSV 검증 실패: " + string.Join(" | ", build.Errors));
            }

            GenerationReport report = new GenerationReport();
            for (int i = 0; i < build.Warnings.Count; i++)
            {
                report.Warnings.Add(build.Warnings[i]);
            }

            EnsureAssetFolder(ItemAssetFolder);
            EnsureAssetFolder(FoodAssetFolder);
            EnsureAssetFolder(RecipeAssetFolder);
            EnsureAssetFolder(RuntimeCatalogFolder);

            Dictionary<string, ItemData> itemAssets = GenerateItems(build.Catalog, report);
            Dictionary<string, FoodData> foodAssets = GenerateFoods(build.Catalog, itemAssets, report);
            Dictionary<string, FishData> fishAssets = LoadFishDataById(report);
            Dictionary<string, RecipeData> recipeAssets = GenerateRecipes(build.Catalog, itemAssets, fishAssets, report);
            GenerateFoodDatabase(foodAssets, report);
            WriteRuntimeJsonFiles(build.Catalog, report);
            GenerateRuntimeCatalogSource(itemAssets, recipeAssets, fishAssets, report);
            SyncUiArtProfileIcons(itemAssets, build.Catalog.PremiumCurrencyProductsById, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return report;
        }

        private static Dictionary<string, ItemData> GenerateItems(BalanceCatalog catalog, GenerationReport report)
        {
            Dictionary<string, ItemData> assets = new Dictionary<string, ItemData>(StringComparer.Ordinal);
            foreach (ItemDefinition item in catalog.ItemsById.Values.OrderBy(value => value.SortOrder).ThenBy(value => value.ItemId, StringComparer.Ordinal))
            {
                if (string.Equals(item.Category, "Fish", StringComparison.Ordinal))
                {
                    report.SkippedFishItems++;
                    continue;
                }

                string assetPath = ItemAssetFolder + "/" + ToAssetFileName(item.ItemId);
                ItemData asset = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<ItemData>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                    report.CreatedItems++;
                }
                else
                {
                    report.UpdatedItems++;
                }

                SerializedObject serialized = new SerializedObject(asset);
                SetString(serialized, "_itemId", item.ItemId);
                SetString(serialized, "_displayName", item.DisplayNameKo);
                SetString(serialized, "_description", item.Notes);
                SetEnum(serialized, "_category", ParseItemCategory(item.Category, item.ItemId, report));
                SetEnum(serialized, "_rarity", ParseItemRarity(item.Rarity, item.ItemId, report));
                SetString(serialized, "_sourceType", item.SourceType);
                SetBool(serialized, "_stackable", item.Stackable);
                SetInt(serialized, "_maxStack", Mathf.Max(1, item.MaxStack));
                SetInt(serialized, "_sellPriceGold", ClampToInt(item.SellPrice, "sellPrice", item.ItemId, report));
                SetBool(serialized, "_sample", false);
                SetBool(serialized, "_enabled", item.IsEnabled);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                assets[item.ItemId] = asset;
            }

            return assets;
        }

        private static Dictionary<string, FoodData> GenerateFoods(
            BalanceCatalog catalog,
            IReadOnlyDictionary<string, ItemData> itemAssets,
            GenerationReport report)
        {
            Dictionary<string, FoodData> assets = new Dictionary<string, FoodData>(StringComparer.Ordinal);
            foreach (ItemDefinition item in catalog.ItemsById.Values
                         .Where(value => string.Equals(value.Category, "Food", StringComparison.Ordinal))
                         .OrderBy(value => value.SortOrder)
                         .ThenBy(value => value.ItemId, StringComparer.Ordinal))
            {
                string assetPath = FoodAssetFolder + "/" + ToAssetFileName(item.ItemId);
                FoodData asset = AssetDatabase.LoadAssetAtPath<FoodData>(assetPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<FoodData>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                    report.CreatedFoods++;
                }
                else
                {
                    report.UpdatedFoods++;
                }

                itemAssets.TryGetValue(item.ItemId, out ItemData itemAsset);
                if (itemAsset == null)
                {
                    report.Warnings.Add(item.ItemId + ": FoodData가 참조할 ItemData를 찾지 못했습니다.");
                }

                bool sellable = ItemSellPolicy.IsSellable(item);
                SerializedObject serialized = new SerializedObject(asset);
                SetString(serialized, "_foodId", item.ItemId);
                SetString(serialized, "_displayNameKo", item.DisplayNameKo);
                SetObject(serialized, "_itemData", itemAsset);
                SetBool(serialized, "_enabled", item.IsEnabled);
                SetBool(serialized, "_sellable", sellable);
                SetInt(serialized, "_sellGold", sellable ? ClampToInt(item.SellPrice, "sellGold", item.ItemId, report) : 0);
                SetString(serialized, "_category", item.Category);
                SetString(serialized, "_sourceType", item.SourceType);
                SetString(serialized, "_cookTag", item.CookTag);
                SetInt(serialized, "_crewExp", Mathf.Max(0, item.CrewExp));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                assets[item.ItemId] = asset;
            }

            return assets;
        }

        private static Dictionary<string, RecipeData> GenerateRecipes(
            BalanceCatalog catalog,
            IReadOnlyDictionary<string, ItemData> itemAssets,
            IReadOnlyDictionary<string, FishData> fishAssets,
            GenerationReport report)
        {
            Dictionary<string, RecipeData> assets = new Dictionary<string, RecipeData>(StringComparer.Ordinal);
            foreach (RecipeDefinition recipe in catalog.RecipesById.Values.OrderBy(value => value.RecipeId, StringComparer.Ordinal))
            {
                string assetPath = RecipeAssetFolder + "/" + ToAssetFileName(recipe.RecipeId);
                RecipeData asset = AssetDatabase.LoadAssetAtPath<RecipeData>(assetPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<RecipeData>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                    report.CreatedRecipes++;
                }
                else
                {
                    report.UpdatedRecipes++;
                }

                itemAssets.TryGetValue(recipe.OutputItemId, out ItemData outputItem);
                if (outputItem == null)
                {
                    report.Warnings.Add(recipe.RecipeId + ": outputItemId를 ItemData로 연결하지 못했습니다. outputItemId=" + recipe.OutputItemId);
                }

                SerializedObject serialized = new SerializedObject(asset);
                SetString(serialized, "_recipeId", recipe.RecipeId);
                SetString(serialized, "_displayName", outputItem == null ? recipe.RecipeId : outputItem.DisplayName);
                SetString(serialized, "_description", recipe.Notes);
                SetObject(serialized, "_outputItem", outputItem);
                SetInt(serialized, "_outputCount", Mathf.Max(1, recipe.OutputCount));
                SetInt(serialized, "_durationSeconds", Mathf.Max(0, recipe.DurationSec));
                SetInt(serialized, "_crewExp", Mathf.Max(0, recipe.CrewExp));
                SetInt(serialized, "_maxQueueCount", 99);
                SetBool(serialized, "_sample", false);
                SetBool(serialized, "_enabled", recipe.IsEnabled);
                SetFishIngredient(serialized, 0, recipe.InputItemId, recipe.InputCount, fishAssets, recipe.RecipeId, report);
                SetFishIngredient(serialized, 1, recipe.InputItemId2, recipe.InputCount2, fishAssets, recipe.RecipeId, report);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                assets[recipe.RecipeId] = asset;
            }

            return assets;
        }

        private static void GenerateFoodDatabase(
            IReadOnlyDictionary<string, FoodData> foodAssets,
            GenerationReport report)
        {
            FoodDatabase database = AssetDatabase.LoadAssetAtPath<FoodDatabase>(FoodDatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<FoodDatabase>();
                AssetDatabase.CreateAsset(database, FoodDatabaseAssetPath);
                report.FoodDatabaseCreated = true;
            }

            SerializedObject serialized = new SerializedObject(database);
            SetObjectArray(serialized, "_foods", foodAssets.Values
                .OrderBy(value => value.FoodId, StringComparer.Ordinal)
                .Cast<UnityEngine.Object>()
                .ToList());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            report.FoodDatabasePath = FoodDatabaseAssetPath;
        }

        #endregion

        #region Runtime Catalog Generation

        private static void WriteRuntimeJsonFiles(BalanceCatalog catalog, GenerationReport report)
        {
            WriteJson("FishList", BuildItemList(catalog, "Fish"), report);
            WriteJson("FoodList", BuildItemList(catalog, "Food"), report);
            WriteJson("IngredientList", BuildItemList(catalog, "UpgradeMaterial", "HighGradeMaterial"), report);
            WriteJson("TicketList", BuildItemList(catalog, "Ticket", "ChoiceTicket"), report);
            WriteJson("BoxList", BuildBoxList(catalog), report);
            WriteJson("FragmentList", BuildFragmentMap(catalog), report);
            WriteJson("RecipeList", BuildRecipeList(catalog), report);
            WriteJson("ShopItemList", BuildShopItemList(catalog), report);
            WriteJson("PremiumCurrencyProductList", BuildPremiumCurrencyProductList(catalog), report);
            WriteJson("EconomyParamList", BuildEconomyParamList(catalog), report);
            WriteJson("StackRuleList", BuildStackRuleList(catalog), report);
            WriteJson("CollectionRewardList", BuildCollectionRewardList(catalog), report);
        }

        private static SortedDictionary<string, object> BuildItemList(BalanceCatalog catalog, params string[] categories)
        {
            HashSet<string> categorySet = new HashSet<string>(categories, StringComparer.Ordinal);
            SortedDictionary<string, object> values = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (ItemDefinition item in catalog.ItemsById.Values.OrderBy(value => value.ItemId, StringComparer.Ordinal))
            {
                if (!categorySet.Contains(item.Category))
                {
                    continue;
                }

                bool sellable = ItemSellPolicy.IsSellable(item);
                if (string.Equals(item.Category, "Food", StringComparison.Ordinal))
                {
                    values[item.ItemId] = new
                    {
                        displayNameKo = item.DisplayNameKo,
                        enabled = item.IsEnabled,
                        sellable,
                        sellGold = sellable ? item.SellPrice : 0,
                        category = item.Category,
                        sourceType = item.SourceType,
                        cookTag = item.CookTag,
                        crewExp = item.CrewExp
                    };
                    continue;
                }

                values[item.ItemId] = new
                {
                    displayNameKo = item.DisplayNameKo,
                    enabled = item.IsEnabled,
                    sellable,
                    sellGold = sellable ? item.SellPrice : 0,
                    category = item.Category,
                    sourceType = item.SourceType,
                    cookTag = item.CookTag
                };
            }

            return values;
        }

        private static SortedDictionary<string, object> BuildBoxList(BalanceCatalog catalog)
        {
            SortedDictionary<string, object> values = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (ItemDefinition item in catalog.ItemsById.Values.OrderBy(value => value.ItemId, StringComparer.Ordinal))
            {
                if (item.Category != "Box")
                {
                    continue;
                }

                bool sellable = ItemSellPolicy.IsSellable(item);
                bool enabled = item.IsEnabled || IsEnabledShopRewardBox(catalog, item.ItemId);

                if (TryGetBoxChoiceRewards(item.ItemId, out object[] choiceRewards))
                {
                    values[item.ItemId] = new
                    {
                        displayNameKo = item.DisplayNameKo,
                        enabled,
                        sellable,
                        sellGold = sellable ? item.SellPrice : 0,
                        category = item.Category,
                        sourceType = item.SourceType,
                        cookTag = item.CookTag,
                        choiceRewards
                    };
                    continue;
                }

                if (TryGetBoxFixedRewards(item.ItemId, out object[] rewards))
                {
                    values[item.ItemId] = new
                    {
                        displayNameKo = item.DisplayNameKo,
                        enabled,
                        sellable,
                        sellGold = sellable ? item.SellPrice : 0,
                        category = item.Category,
                        sourceType = item.SourceType,
                        cookTag = item.CookTag,
                        rewards
                    };
                    continue;
                }

                values[item.ItemId] = new
                {
                    displayNameKo = item.DisplayNameKo,
                    enabled = item.IsEnabled,
                    sellable,
                    sellGold = sellable ? item.SellPrice : 0,
                    category = item.Category,
                    sourceType = item.SourceType,
                    cookTag = item.CookTag
                };
            }

            return values;
        }

        private static bool IsEnabledShopRewardBox(BalanceCatalog catalog, string itemId)
        {
            return catalog != null &&
                   catalog.ShopItemsById.Values.Any(shopItem =>
                       shopItem.IsEnabled &&
                       string.Equals(shopItem.RewardItemId, itemId, StringComparison.Ordinal));
        }

        private static bool TryGetBoxChoiceRewards(string itemId, out object[] rewards)
        {
            rewards = null;
            if (itemId != "box_basic_reward")
            {
                return false;
            }

            rewards = new object[]
            {
                Reward("IngredientInventory", "mat_upgrade_common", 10),
                Reward("IngredientInventory", "mat_upgrade_sturdy", 4),
                Reward("IngredientInventory", "mat_upgrade_refined", 1)
            };
            return true;
        }

        private static bool TryGetBoxFixedRewards(string itemId, out object[] rewards)
        {
            switch (itemId)
            {
                case "box_boat_upgrade_pack":
                    rewards = new object[]
                    {
                        Reward("IngredientInventory", "mat_upgrade_common", 20),
                        Reward("IngredientInventory", "mat_upgrade_sturdy", 4)
                    };
                    return true;
                case "box_stage3_push_pack":
                    rewards = new object[]
                    {
                        Reward("IngredientInventory", "mat_upgrade_common", 15),
                        Reward("IngredientInventory", "mat_upgrade_sturdy", 6),
                        Reward("IngredientInventory", "mat_upgrade_refined", 2),
                        Reward("OddmentInventory", "ticket_speedup_10m", 1)
                    };
                    return true;
                case "box_starter_cooking_pack":
                    rewards = new object[]
                    {
                        Reward("FishInventory", "fish_anchovy", 4),
                        Reward("FishInventory", "fish_saury", 2),
                        Reward("FishInventory", "fish_mackerel", 2),
                        Reward("OddmentInventory", "ticket_speedup_10m", 1)
                    };
                    return true;
                default:
                    rewards = null;
                    return false;
            }
        }

        private static object Reward(string inventoryKey, string itemId, int amount)
        {
            return new
            {
                inventoryKey,
                itemId,
                amount
            };
        }

        private static SortedDictionary<string, string> BuildFragmentMap(BalanceCatalog catalog)
        {
            SortedDictionary<string, string> values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (ItemDefinition item in catalog.ItemsById.Values.OrderBy(value => value.ItemId, StringComparer.Ordinal))
            {
                if (item.Category != "Ticket" || !item.ItemId.StartsWith("fragment_Crew_", StringComparison.Ordinal))
                {
                    continue;
                }

                values[item.ItemId] = item.ItemId.Substring("fragment_".Length);
            }

            return values;
        }

        private static SortedDictionary<string, object> BuildRecipeList(BalanceCatalog catalog)
        {
            SortedDictionary<string, object> values = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (RecipeDefinition recipe in catalog.RecipesById.Values.OrderBy(value => value.RecipeId, StringComparer.Ordinal))
            {
                List<object> inputs = new List<object>
                {
                    new
                    {
                        inventoryKey = InventoryKeyForItemId(recipe.InputItemId),
                        itemId = recipe.InputItemId,
                        amount = recipe.InputCount
                    }
                };
                if (!string.IsNullOrEmpty(recipe.InputItemId2))
                {
                    inputs.Add(new
                    {
                        inventoryKey = InventoryKeyForItemId(recipe.InputItemId2),
                        itemId = recipe.InputItemId2,
                        amount = recipe.InputCount2
                    });
                }

                values[recipe.RecipeId] = new
                {
                    enabled = recipe.IsEnabled,
                    durationSec = recipe.DurationSec,
                    inputs,
                    output = new
                    {
                        inventoryKey = InventoryKeyForItemId(recipe.OutputItemId),
                        itemId = recipe.OutputItemId,
                        amount = recipe.OutputCount
                    },
                    crewExp = recipe.CrewExp,
                    unlockCondition = recipe.UnlockCondition
                };
            }

            return values;
        }

        private static SortedDictionary<string, object> BuildShopItemList(BalanceCatalog catalog)
        {
            SortedDictionary<string, object> values = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (ShopItemDefinition shopItem in catalog.ShopItemsById.Values.OrderBy(value => value.SortOrder).ThenBy(value => value.ShopItemId, StringComparer.Ordinal))
            {
                values[shopItem.ShopItemId] = new
                {
                    enabled = shopItem.IsEnabled,
                    category = shopItem.Category,
                    priceType = shopItem.PriceType,
                    currencyCode = CurrencyCodeFor(shopItem.PriceType),
                    priceAmount = shopItem.PriceAmount,
                    reward = new
                    {
                        inventoryKey = InventoryKeyForItemId(shopItem.RewardItemId),
                        itemId = shopItem.RewardItemId,
                        amount = shopItem.RewardCount
                    },
                    unlockCondition = shopItem.UnlockCondition,
                    visibilityCondition = shopItem.VisibilityCondition
                };
            }

            return values;
        }

        private static SortedDictionary<string, object> BuildPremiumCurrencyProductList(BalanceCatalog catalog)
        {
            SortedDictionary<string, object> values = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (PremiumCurrencyProductDefinition product in catalog.PremiumCurrencyProductsById.Values.OrderBy(value => value.SortOrder).ThenBy(value => value.ProductId, StringComparer.Ordinal))
            {
                values[product.ProductId] = new
                {
                    enabled = product.IsEnabled,
                    cashAmount = product.CashAmount,
                    prismPearlAmount = product.PrismPearlAmount,
                    sortOrder = product.SortOrder,
                    notes = product.Notes
                };
            }

            return values;
        }

        private static SortedDictionary<string, object> BuildEconomyParamList(BalanceCatalog catalog)
        {
            SortedDictionary<string, object> values = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (EconomyParam param in catalog.EconomyParamsByKey.Values.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                values[param.Key] = new
                {
                    value = param.Value,
                    valueType = param.ValueType,
                    scope = param.Scope,
                    enabled = param.IsEnabled
                };
            }

            return values;
        }

        private static SortedDictionary<string, object> BuildStackRuleList(BalanceCatalog catalog)
        {
            SortedDictionary<string, object> values = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (StackRule rule in catalog.StackRulesByCategory.Values.OrderBy(value => value.Category, StringComparer.Ordinal))
            {
                values[rule.Category] = new
                {
                    defaultMaxStack = rule.DefaultMaxStack,
                    overflowPolicy = rule.OverflowPolicy,
                    enabled = rule.IsEnabled
                };
            }

            return values;
        }

        private static SortedDictionary<string, object> BuildCollectionRewardList(BalanceCatalog catalog)
        {
            SortedDictionary<string, object> values = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (CollectionRewardDefinition reward in catalog.CollectionRewardsById.Values.OrderBy(value => value.SortOrder).ThenBy(value => value.RewardId, StringComparer.Ordinal))
            {
                values[reward.RewardId] = new
                {
                    rewardGroupId = reward.RewardGroupId,
                    itemId = reward.ItemId,
                    conditionType = reward.ConditionType,
                    conditionValue = reward.ConditionValue,
                    rewardCurrency = reward.RewardCurrency,
                    rewardAmount = reward.RewardAmount,
                    rewardItemId = reward.RewardItemId,
                    rewardItemCount = reward.RewardItemCount,
                    claimId = reward.ClaimId,
                    sortOrder = reward.SortOrder,
                    enabled = reward.IsEnabled
                };
            }

            return values;
        }

        private static void WriteJson(string name, object value, GenerationReport report)
        {
            string assetPath = RuntimeCatalogFolder + "/" + name + ".json";
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            File.WriteAllText(absolutePath, JsonConvert.SerializeObject(value, Formatting.Indented), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath);
            report.RuntimeJsonFiles++;
        }

        private static void GenerateRuntimeCatalogSource(
            IReadOnlyDictionary<string, ItemData> itemAssets,
            IReadOnlyDictionary<string, RecipeData> recipeAssets,
            IReadOnlyDictionary<string, FishData> fishAssets,
            GenerationReport report)
        {
            FisherRuntimeCatalogSource source = AssetDatabase.LoadAssetAtPath<FisherRuntimeCatalogSource>(RuntimeCatalogAssetPath);
            if (source == null)
            {
                source = ScriptableObject.CreateInstance<FisherRuntimeCatalogSource>();
                AssetDatabase.CreateAsset(source, RuntimeCatalogAssetPath);
                report.RuntimeCatalogSourceCreated = true;
            }

            List<TextAsset> jsonAssets = new List<TextAsset>();
            for (int i = 0; i < RuntimeJsonNames.Length; i++)
            {
                string assetPath = RuntimeCatalogFolder + "/" + RuntimeJsonNames[i] + ".json";
                TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (jsonAsset != null)
                {
                    jsonAssets.Add(jsonAsset);
                }
                else
                {
                    report.Warnings.Add("Runtime JSON TextAsset 로드 실패: " + assetPath);
                }
            }

            SerializedObject serialized = new SerializedObject(source);
            SetObjectArray(serialized, "_itemDataAssets", itemAssets.Values.OrderBy(value => value.ItemId, StringComparer.Ordinal).Cast<UnityEngine.Object>().ToList());
            SetObjectArray(serialized, "_recipeDataAssets", recipeAssets.Values.OrderBy(value => value.RecipeId, StringComparer.Ordinal).Cast<UnityEngine.Object>().ToList());
            SetObjectArray(serialized, "_fishDataAssets", fishAssets.Values.OrderBy(value => value.FishId, StringComparer.Ordinal).Cast<UnityEngine.Object>().ToList());
            SetObjectArray(serialized, "_runtimeJsonAssets", jsonAssets.OrderBy(value => value.name, StringComparer.Ordinal).Cast<UnityEngine.Object>().ToList());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(source);
            report.RuntimeCatalogSourcePath = RuntimeCatalogAssetPath;
        }

        private static void SyncUiArtProfileIcons(
            IReadOnlyDictionary<string, ItemData> itemAssets,
            IReadOnlyDictionary<string, PremiumCurrencyProductDefinition> premiumProducts,
            GenerationReport report)
        {
            FisherUiArtProfile profile = AssetDatabase.LoadAssetAtPath<FisherUiArtProfile>(UiArtProfileAssetPath);
            if (profile == null)
            {
                report.Warnings.Add("UI ArtProfile 로드 실패: " + UiArtProfileAssetPath);
                return;
            }

            SerializedObject serialized = new SerializedObject(profile);
            SerializedProperty icons = serialized.FindProperty("_itemIcons");
            if (icons == null)
            {
                report.Warnings.Add("UI ArtProfile에서 _itemIcons 필드를 찾지 못했습니다.");
                return;
            }

            foreach (ItemData item in itemAssets.Values.OrderBy(value => value.ItemId, StringComparer.Ordinal))
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || item.Icon == null)
                {
                    continue;
                }

                if (SetIconBinding(icons, item.ItemId, item.Icon))
                {
                    report.UiArtProfileIconBindingsSynced++;
                }
            }

            if (premiumProducts != null)
            {
                foreach (PremiumCurrencyProductDefinition product in premiumProducts.Values.OrderBy(value => value.ProductId, StringComparer.Ordinal))
                {
                    if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
                    {
                        continue;
                    }

                    Sprite productIcon = LoadPremiumProductIcon(product.ProductId, report);
                    if (productIcon == null)
                    {
                        continue;
                    }

                    if (SetIconBinding(icons, product.ProductId, productIcon))
                    {
                        report.UiArtProfileIconBindingsSynced++;
                    }
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            report.UiArtProfilePath = UiArtProfileAssetPath;
        }

        private static bool SetIconBinding(SerializedProperty icons, string itemId, Sprite sprite)
        {
            SerializedProperty binding = FindIconBinding(icons, itemId);
            bool created = false;
            if (binding == null)
            {
                int index = icons.arraySize;
                icons.InsertArrayElementAtIndex(index);
                binding = icons.GetArrayElementAtIndex(index);
                created = true;
            }

            SerializedProperty itemIdProperty = binding.FindPropertyRelative("_itemId");
            SerializedProperty spriteProperty = binding.FindPropertyRelative("_sprite");
            bool changed = created;

            if (itemIdProperty != null && itemIdProperty.stringValue != itemId)
            {
                itemIdProperty.stringValue = itemId;
                changed = true;
            }

            if (spriteProperty != null && spriteProperty.objectReferenceValue != sprite)
            {
                spriteProperty.objectReferenceValue = sprite;
                changed = true;
            }

            return changed;
        }

        private static SerializedProperty FindIconBinding(SerializedProperty icons, string itemId)
        {
            for (int i = 0; i < icons.arraySize; i++)
            {
                SerializedProperty binding = icons.GetArrayElementAtIndex(i);
                SerializedProperty itemIdProperty = binding.FindPropertyRelative("_itemId");
                if (itemIdProperty != null && string.Equals(itemIdProperty.stringValue, itemId, StringComparison.Ordinal))
                {
                    return binding;
                }
            }

            return null;
        }

        private static Sprite LoadPremiumProductIcon(string productId, GenerationReport report)
        {
            string iconPath = PremiumProductIconPath(productId);
            if (string.IsNullOrEmpty(iconPath))
            {
                return null;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (sprite == null)
            {
                report.Warnings.Add("프리미엄 상품 아이콘 로드 실패: " + productId + " -> " + iconPath);
            }

            return sprite;
        }

        private static string PremiumProductIconPath(string productId)
        {
            switch (productId)
            {
                case "cash_prism_small_001":
                    return EtcIconFolder + "/PrismPearl_Pack_Small.png";
                case "cash_prism_medium_001":
                    return EtcIconFolder + "/PrismPearl_Pack_Medium.png";
                case "cash_prism_large_001":
                    return EtcIconFolder + "/PrismPearl_Pack_Large.png";
                default:
                    return null;
            }
        }

        private static string InventoryKeyForItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return "OddmentInventory";
            }

            if (itemId.StartsWith("fish_", StringComparison.Ordinal))
            {
                return "FishInventory";
            }

            if (itemId.StartsWith("food_", StringComparison.Ordinal))
            {
                return "FoodInventory";
            }

            if (itemId.StartsWith("mat_", StringComparison.Ordinal))
            {
                return "IngredientInventory";
            }

            return "OddmentInventory";
        }

        private static string CurrencyCodeFor(string priceType)
        {
            switch (priceType)
            {
                case "softCurrency":
                case "GD":
                    return "GD";
                case "prismPearl":
                case "PP":
                    return "PP";
                case "pirateCoin":
                case "PC":
                    return "PC";
                default:
                    return priceType ?? string.Empty;
            }
        }

        #endregion

        #region Asset Lookup

        private static Dictionary<string, FishData> LoadFishDataById(GenerationReport report)
        {
            Dictionary<string, FishData> fishById = new Dictionary<string, FishData>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:FishData", new[] { RmsFishDataFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                FishData fishData = AssetDatabase.LoadAssetAtPath<FishData>(path);
                if (fishData == null || string.IsNullOrEmpty(fishData.FishId))
                {
                    continue;
                }

                if (fishById.ContainsKey(fishData.FishId))
                {
                    report.Warnings.Add("중복 FishData fishId가 있어 첫 번째만 사용합니다. fishId=" + fishData.FishId);
                    continue;
                }

                fishById.Add(fishData.FishId, fishData);
            }

            report.LoadedFishData = fishById.Count;
            return fishById;
        }

        private static void SetFishIngredient(
            SerializedObject serialized,
            int index,
            string fishId,
            int count,
            IReadOnlyDictionary<string, FishData> fishAssets,
            string recipeId,
            GenerationReport report)
        {
            SerializedProperty ingredients = serialized.FindProperty("_fishIngredients");
            if (ingredients == null)
            {
                report.Warnings.Add(recipeId + ": _fishIngredients 필드를 찾지 못했습니다.");
                return;
            }

            if (ingredients.arraySize < 2)
            {
                ingredients.arraySize = 2;
            }

            SerializedProperty ingredient = ingredients.GetArrayElementAtIndex(index);
            SerializedProperty fishProperty = ingredient.FindPropertyRelative("_fishData");
            SerializedProperty countProperty = ingredient.FindPropertyRelative("_count");

            FishData fishData = null;
            if (!string.IsNullOrWhiteSpace(fishId) && !fishAssets.TryGetValue(fishId, out fishData))
            {
                report.Warnings.Add(recipeId + ": 재료 FishData를 찾지 못했습니다. fishId=" + fishId);
            }

            if (fishProperty != null)
            {
                fishProperty.objectReferenceValue = fishData;
            }

            if (countProperty != null)
            {
                countProperty.intValue = Mathf.Max(1, count);
            }
        }

        #endregion

        #region Serialized Property Helpers

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetObjectArray(SerializedObject serialized, string propertyName, IReadOnlyList<UnityEngine.Object> values)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            int count = values == null ? 0 : values.Count;
            property.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void SetEnum<T>(SerializedObject serialized, string propertyName, T value) where T : struct
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            string enumName = value.ToString();
            for (int i = 0; i < property.enumNames.Length; i++)
            {
                if (string.Equals(property.enumNames[i], enumName, StringComparison.Ordinal))
                {
                    property.enumValueIndex = i;
                    return;
                }
            }
        }

        #endregion

        #region Parsing Helpers

        private static ItemCategory ParseItemCategory(string value, string itemId, GenerationReport report)
        {
            if (Enum.TryParse(value, false, out ItemCategory category))
            {
                return category;
            }

            report.Warnings.Add(itemId + ": 알 수 없는 category라 Special로 둡니다. category=" + value);
            return ItemCategory.Special;
        }

        private static ItemRarity ParseItemRarity(string value, string itemId, GenerationReport report)
        {
            if (Enum.TryParse(value, false, out ItemRarity rarity))
            {
                return rarity;
            }

            report.Warnings.Add(itemId + ": 알 수 없는 rarity라 Common으로 둡니다. rarity=" + value);
            return ItemRarity.Common;
        }

        private static int ClampToInt(long value, string fieldName, string id, GenerationReport report)
        {
            if (value > int.MaxValue)
            {
                report.Warnings.Add(id + ": " + fieldName + " 값이 int 상한을 넘어 int.MaxValue로 제한됩니다.");
                return int.MaxValue;
            }

            if (value < int.MinValue)
            {
                report.Warnings.Add(id + ": " + fieldName + " 값이 int 하한보다 작아 int.MinValue로 제한됩니다.");
                return int.MinValue;
            }

            return (int)value;
        }

        private static string ReadGeneratedCsv(string fileName, bool optional = false)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), GeneratedFolder, fileName);
            if (!File.Exists(path))
            {
                if (optional)
                {
                    return null;
                }

                throw new FileNotFoundException("Generated CSV 파일을 찾지 못했습니다.", path);
            }

            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            parent = parent.Replace("\\", "/");
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string ToAssetFileName(string id)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < id.Length; i++)
            {
                char character = id[i];
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append('_');
                }
            }

            return builder + ".asset";
        }

        #endregion

        #region Report

        private sealed class GenerationReport
        {
            public int CreatedItems;
            public int UpdatedItems;
            public int SkippedFishItems;
            public int CreatedFoods;
            public int UpdatedFoods;
            public bool FoodDatabaseCreated;
            public string FoodDatabasePath;
            public int CreatedRecipes;
            public int UpdatedRecipes;
            public int LoadedFishData;
            public int RuntimeJsonFiles;
            public bool RuntimeCatalogSourceCreated;
            public string RuntimeCatalogSourcePath;
            public int UiArtProfileIconBindingsSynced;
            public string UiArtProfilePath;
            public readonly List<string> Warnings = new List<string>();

            public string BuildMessage()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("[Fisher Data Asset Generator]");
                builder.AppendLine("ItemData 생성: " + CreatedItems + ", 갱신: " + UpdatedItems);
                builder.AppendLine("Fish ItemData 제외: " + SkippedFishItems);
                builder.AppendLine("FoodData 생성: " + CreatedFoods + ", 갱신: " + UpdatedFoods);
                builder.AppendLine("FoodDatabase: " + FoodDatabasePath + (FoodDatabaseCreated ? " (생성)" : " (갱신)"));
                builder.AppendLine("RecipeData 생성: " + CreatedRecipes + ", 갱신: " + UpdatedRecipes);
                builder.AppendLine("연결 가능한 RMS FishData: " + LoadedFishData);
                builder.AppendLine("런타임 JSON 갱신: " + RuntimeJsonFiles);
                builder.AppendLine("런타임 카탈로그 Source: " + RuntimeCatalogSourcePath + (RuntimeCatalogSourceCreated ? " (생성)" : " (갱신)"));
                builder.AppendLine("UI ArtProfile 아이콘 동기화: " + UiArtProfileIconBindingsSynced + "개" + (string.IsNullOrEmpty(UiArtProfilePath) ? string.Empty : " (" + UiArtProfilePath + ")"));
                builder.AppendLine("경고: " + Warnings.Count);
                for (int i = 0; i < Warnings.Count; i++)
                {
                    builder.AppendLine("- " + Warnings[i]);
                }

                return builder.ToString();
            }
        }

        #endregion
    }

}
