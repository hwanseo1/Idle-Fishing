using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Fisher.PlayerSystems.Editor
{
    /// <summary>
    /// Generated CSV와 Fisher 서비스 계약을 Unity Editor에서 빠르게 검증합니다.
    /// </summary>
    public static class FisherDataContractSelfTest
    {
        #region Constants

        private const string GeneratedFolder = "Assets/03_Data/05_CSH/Generated";
        private const string RuntimeCatalogAssetPath = "Assets/Resources/05_CSH/RuntimeCatalog/FisherRuntimeCatalogSource.asset";
        private const string TestVersion = "FISHER_CORE_CONTRACT_REPORT_2026_06_12";

        #endregion

        #region Menu Entry

        /// <summary>
        /// Unity Editor 메뉴에서 전체 self-test를 실행합니다.
        /// </summary>
        [MenuItem("FISHER/데이터/CSV 계약 검사")]
        public static void RunMenu()
        {
            try
            {
                string report = RunAll();
                Debug.Log("[Fisher Data Contract Self Test]\n" + report);
                EditorUtility.DisplayDialog("Fisher CSV 계약 검사", "성공했습니다.\n\n" + report, "확인");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Fisher Data Contract Self Test]\n" + exception);
                EditorUtility.DisplayDialog("Fisher CSV 계약 검사 실패", exception.Message, "확인");
            }
        }

        /// <summary>
        /// 밸런스/BM 담당자가 현재 Generated CSV의 위험 구간을 빠르게 훑는 진단 메뉴입니다.
        /// </summary>
        public static void RunBalanceBmDiagnosticMenu()
        {
            try
            {
                string report = RunBalanceBmDiagnostics();
                Debug.Log("[Fisher Balance/BM Diagnostic]\n" + report);
                EditorUtility.DisplayDialog("Fisher 밸런스/BM 빠른 진단", "완료했습니다.\n\n" + report, "확인");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Fisher Balance/BM Diagnostic]\n" + exception);
                EditorUtility.DisplayDialog("Fisher 밸런스/BM 빠른 진단 실패", exception.Message, "확인");
            }
        }

        /// <summary>
        /// Unity batchmode에서 self-test를 실행하고 성공/실패 exit code를 반환합니다.
        /// </summary>
        public static void RunBatchMode()
        {
            try
            {
                string report = RunAll();
                Debug.Log("[Fisher Data Contract Self Test Batch]\n" + report);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[Fisher Data Contract Self Test Batch]\n" + exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        #endregion

        #region Test Entry

        /// <summary>
        /// 전체 Fisher 서비스 계약 검증을 실행하고 사람이 읽을 수 있는 보고서를 반환합니다.
        /// </summary>
        public static string RunAll()
        {
            TestReport report = new TestReport();
            report.Add("=== Fisher CSV 계약 검사 ===");
            report.Add("검사 버전: " + TestVersion);
            report.Add("CSV 원본: " + GeneratedFolder);
            report.Add("런타임 원천: " + RuntimeCatalogAssetPath);

            report.Run("Generated CSV 기준 흐름", () => RunGeneratedCsvFlowTests(report));
            report.Run("런타임 SO/JSON 카탈로그 검증", () => RunRuntimeCatalogSourceTests(report));
            report.Run("데이터 오류 검증", () => RunCatalogValidationTests(report));
            report.Run("숫자 표시/상한 계약 검증", () => RunNumberDisplayContractTests(report));
            report.Run("화폐 snapshot/표시 계약 검증", () => RunCurrencyDisplayContractTests(report));
            report.Run("서버 요청 gate 계약 검증", () => RunServerRequestGateContractTests(report));
            report.Run("서버 mutation 응답 계약 검증", () => RunServerMutationResponseContractTests(report));
            report.Run("cshRuntimeState 브릿지 수신 계약 검증", RunCshRuntimeStateBridgeContractTests);
            report.Run("런타임 UI 생성 금지 계약 검증", () => RunRuntimeUiNoGenerationContractTests(report));
            report.Run("RMS Fish/Boss 참조 검증", () => RunRmsReferenceSeamTests(report));
            report.Run("서버 ID 매핑 계약 검증", () => RunServerIdMappingContractTests(report));
            report.Run("CSH/YWJ 선원 조각 연동 계약 검증", () => RunCrewFragmentExternalContractTests(report));
            report.Run("서버/TitleData 데이터 계약 검증", () => RunServerTitleDataContractTests(report));
            report.Run("직접낚시 어댑터 계약 검증", () => RunFishingAdapterContractTests(report));
            report.Run("인벤토리 계약 검증", () => RunInventoryContractTests(report));
            report.Run("가방 capacity 계약 검증", () => RunBagCapacityContractTests(report));
            report.Run("아이템 수량 overflow 계약 검증", () => RunItemCountOverflowContractTests(report));
            report.Run("가방 조회 계약 검증", () => RunBagQueryContractTests(report));
            report.Run("저장 DTO 계약 검증", () => RunSaveMapperContractTests(report));
            report.Run("서비스 결과 DTO 계약 검증", () => RunResultDtoContractTests(report));
            report.Run("보상 묶음 seam 검증", () => RunRewardBundleSeamTests(report));
            report.Run("재화 overflow 계약 검증", () => RunCurrencyOverflowContractTests(report));
            report.Run("상점 계약 검증", () => RunShopContractTests(report));
            report.Run("요리 슬롯 계약 검증", () => RunCookingSlotContractTests(report));
            report.Run("요리 실패 흐름 검증", () => RunCookingFailureTests(report));
            report.Run("도감 계약 검증", () => RunCollectionContractTests(report));
            report.Run("도감 보상 확장 접점 검증", () => RunCollectionRewardSeamTests(report));

            string result = report.Build();
            if (report.HasFailures)
            {
                throw new InvalidOperationException(result);
            }

            return result;
        }

        public static string RunBalanceBmDiagnostics()
        {
            TestReport report = new TestReport();
            report.Add("=== Fisher 밸런스/BM 빠른 진단 ===");
            report.Add("기준: " + GeneratedFolder);
            report.Add("주의: 경고는 자동 너프가 아니라 수치 검토 후보입니다.");

            BalanceCatalog catalog = BuildGeneratedCatalogForTool("balance/BM diagnostic");
            report.Run("선원 모집 정책 진단", () => DiagnoseCrewGachaPolicy(catalog, report));
            report.Run("BM/상점 노출 진단", () => DiagnoseBmShopPolicy(catalog, report));
            report.Run("강화재료 경제 진단", () => DiagnoseUpgradeMaterialEconomy(catalog, report));
            report.Run("판매불가 서버형 아이템 진단", () => DiagnoseServerOnlyItemPolicy(catalog, report));

            string result = report.Build();
            if (report.HasFailures)
            {
                throw new InvalidOperationException(result);
            }

            return result;
        }

        #endregion

        #region Test Cases

        private static void RunGeneratedCsvFlowTests(TestReport report)
        {
            BalanceBuildResult build = BalanceCatalogBuilder.Build(new BalanceCsvSet
            {
                ItemsCsv = ReadGenerated("items.csv"),
                FishCsv = ReadGenerated("fish.csv"),
                ShopItemsCsv = ReadGenerated("shop_items.csv"),
                PremiumCurrencyProductsCsv = ReadGenerated("premium_currency_products.csv", optional: true),
                RecipesCsv = ReadGenerated("recipes.csv"),
                CollectionRewardsCsv = ReadGenerated("collection_rewards.csv"),
                EconomyParamsCsv = ReadGenerated("economy_params.csv"),
                StackRulesCsv = ReadGenerated("stack_rules.csv")
            });

            AssertTrue(build.Success, "catalog build should succeed. errors=" + string.Join(" | ", build.Errors));
            report.Add("카탈로그 행 수: items=" + build.Catalog.ItemsById.Count
                + ", fish=" + build.Catalog.FishById.Count
                + ", recipes=" + build.Catalog.RecipesById.Count
                + ", shopItems=" + build.Catalog.ShopItemsById.Count
                + ", premiumCurrencyProducts=" + build.Catalog.PremiumCurrencyProductsById.Count
                + ", collectionRewards=" + build.Catalog.CollectionRewardsById.Count);
            AssertRequiredDataContractRows(build.Catalog);
            report.Add("데이터 사전 생성: OK");

            PlayerRuntimeState state = new PlayerRuntimeState
            {
                softCurrency = 120
            };

            ManualClock clock = new ManualClock(new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
            InventoryService inventory = new InventoryService(build.Catalog, state);
            CookingService cooking = new CookingService(build.Catalog, state, inventory, clock);
            CollectionService collection = new CollectionService(build.Catalog, state, inventory);
            BagQueryService bagQuery = new BagQueryService(build.Catalog, state);

            AssertSuccess(inventory.TryAddItem("fish_anchovy", 5), "fish gain x5");
            AssertSuccess(inventory.TryAddItem("fish_saury", 5), "saury gain x5");
            AssertEqual(5, inventory.GetAcquiredCount("fish_anchovy"), "anchovy acquired before sell/cook");
            AssertEqual(0, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions
            {
                Filter = BagFilter.NewDiscovery
            }), "fish_anchovy"), "item discovery is automatic after gain");
            AssertTrue(state.discoveredCollectionItemIds.Contains("fish_anchovy"), "anchovy item auto discovery");
            AssertTrue(state.discoveredCollectionItemIds.Contains("fish_saury"), "saury item auto discovery");
            AssertTrue(state.newItemNoticeIds.Contains("fish_anchovy"), "anchovy new item notice");

            AssertSuccess(collection.TryRegisterFishDiscovery("fish_anchovy_g1"), "collection grade 1 discovery");
            AssertSuccess(collection.TryRegisterFishDiscovery("fish_anchovy_g1"), "duplicate grade 1 discovery should be idempotent");
            AssertEqual(3, state.discoveredCollectionItemIds.Count, "item auto discovery plus one grade discovery");

            ServiceResult failedSell = inventory.TrySellItem("fish_anchovy", 99);
            AssertFalse(failedSell.Success, "oversell should fail");
            AssertEqual(120L, state.softCurrency, "currency should not change after failed sell");
            AssertEqual(5, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_anchovy"), "anchovy count after failed sell");

            AssertSuccess(inventory.TrySellItem("fish_anchovy", 1), "sell fish x1");
            long expectedCurrencyAfterFishSale = 120L + build.Catalog.ItemsById["fish_anchovy"].SellPrice;
            AssertEqual(expectedCurrencyAfterFishSale, state.softCurrency, "currency after fish sale");

            AssertSuccess(cooking.TryStartRecipe("recipe_grilled_anchovy_g1"), "recipe start");
            PlayerStateSaveData activeRecipeSave = PlayerStateSaveMapper.Capture(state, clock.UtcNow.Ticks);
            AssertEqual(clock.UtcNow.Ticks, activeRecipeSave.lastTrustedServerUtcTicks, "trusted time saved");
            state = PlayerStateSaveMapper.Restore(activeRecipeSave);
            AssertTrue(state.activeRecipeState != null, "active recipe should restore");
            AssertEqual("recipe_grilled_anchovy_g1", state.activeRecipeState.recipeId, "restored active recipe id");
            AssertEqual(5, state.itemAcquisitionCounts["fish_anchovy"], "restored acquisition count");
            AssertEqual(3, state.discoveredCollectionItemIds.Count, "restored discovery count before cooked output");

            inventory = new InventoryService(build.Catalog, state);
            cooking = new CookingService(build.Catalog, state, inventory, clock);
            bagQuery = new BagQueryService(build.Catalog, state);

            AssertFailKey(cooking.TryCompleteRecipe(), "cooking.not_ready", "recipe should not complete before timer");
            clock.AdvanceSeconds(60);
            AssertSuccess(cooking.TryCompleteRecipe(), "recipe complete");
            AssertEqual(5L, state.crewExp, "crew exp after recipe");

            List<BagItemView> bag = bagQuery.BuildSnapshot(new BagQueryOptions());
            AssertEqual(3, bag.Count, "final bag row count");
            AssertEqual(3, CountBagItem(bag, "fish_anchovy"), "final anchovy count");
            AssertEqual(4, CountBagItem(bag, "fish_saury"), "final saury count");
            AssertEqual(1, CountBagItem(bag, "food_grilled_anchovy"), "final food count");

            PlayerStateSaveData finalSave = PlayerStateSaveMapper.Capture(state, clock.UtcNow.Ticks);
            PlayerRuntimeState finalRestored = PlayerStateSaveMapper.Restore(finalSave);
            AssertEqual(expectedCurrencyAfterFishSale, finalRestored.softCurrency, "restored final currency");
            AssertEqual(5L, finalRestored.crewExp, "restored final crew exp");
            AssertEqual(0, finalRestored.claimedRewardIds.Count, "restored claimed reward count");
            AssertEqual(5, finalRestored.itemAcquisitionCounts["fish_anchovy"], "restored final acquisition count");
            AssertEqual(4, finalRestored.discoveredCollectionItemIds.Count, "restored final discovery count");

            BagQueryService restoredBagQuery = new BagQueryService(build.Catalog, finalRestored);
            AssertEqual(3, restoredBagQuery.BuildSnapshot(new BagQueryOptions()).Count, "restored final bag row count");

            RunGeneratedSpeedupFixtureFlow(build.Catalog);

            report.Add("서비스 흐름: OK");
            report.Add("저장 복원: OK");
            report.Add("최종 재화: " + state.softCurrency);
            report.Add("최종 크루 경험치: " + state.crewExp);
            report.Add("도감 발견 수: " + state.discoveredCollectionItemIds.Count);
            report.Add("가방 행 수: " + bag.Count);
        }

        private static void RunRuntimeCatalogSourceTests(TestReport report)
        {
            FisherRuntimeCatalogSource source = AssetDatabase.LoadAssetAtPath<FisherRuntimeCatalogSource>(RuntimeCatalogAssetPath);
            AssertTrue(source != null, "runtime catalog source should exist: " + RuntimeCatalogAssetPath);
            AssertTrue(source.ItemDataAssets.Length > 0, "runtime catalog source should contain ItemData assets");
            AssertTrue(source.RecipeDataAssets.Length > 0, "runtime catalog source should contain RecipeData assets");
            AssertTrue(source.FishDataAssets.Length > 0, "runtime catalog source should contain RMS FishData assets");
            AssertTrue(source.RuntimeJsonAssets.Length > 0, "runtime catalog source should contain JSON TextAssets");

            BalanceBuildResult build = FisherLocalCatalogBuilder.Build(source);
            AssertTrue(build.Success, "runtime SO/JSON catalog build should succeed. errors=" + string.Join(" | ", build.Errors));
            AssertRequiredDataContractRows(build.Catalog);
            AssertTrue(build.Catalog.ItemsById.ContainsKey("fish_anchovy"), "runtime catalog should include RMS fish itemId: fish_anchovy");
            AssertTrue(build.Catalog.RecipesById.ContainsKey("recipe_grilled_anchovy_g1"), "runtime catalog should include recipe JSON");
            AssertTrue(build.Catalog.ShopItemsById.ContainsKey("shop_upgrade_common_pack_001"), "runtime catalog should include shop JSON");
            AssertTrue(build.Catalog.PremiumCurrencyProductsById.ContainsKey("cash_prism_small_001"), "runtime catalog should include premium currency product JSON fallback");
            report.Add("런타임 SO/JSON 카탈로그: items=" + build.Catalog.ItemsById.Count
                + ", fish=" + build.Catalog.FishById.Count
                + ", recipes=" + build.Catalog.RecipesById.Count
                + ", shopItems=" + build.Catalog.ShopItemsById.Count
                + ", premiumCurrencyProducts=" + build.Catalog.PremiumCurrencyProductsById.Count
                + ", collectionRewards=" + build.Catalog.CollectionRewardsById.Count);
        }

        private static void AssertRequiredDataContractRows(BalanceCatalog catalog)
        {
            AssertTrue(catalog != null, "catalog should exist for required data contract rows");

            string[] economyParamKeys =
            {
                "initial_bag_capacity",
                "bag_capacity_step",
                "bag_capacity_max",
                "bag_capacity_gold_cost_base",
                "speedup_ticket_seconds",
                "instant_complete_ticket_enabled",
                "offline_reward_cap_minutes",
                "gold_gacha_enabled",
                "premium_gacha_enabled",
                "crew_gacha_gold_reward_policy",
                "crew_gacha_gold_fragment_id_pattern",
                "crew_gacha_gold_fragment_grade_scope",
                "crew_gacha_gold_full_crew_chance_pct",
                "crew_gacha_gold_full_crew_grade_scope",
                "crew_gacha_gold_ten_full_crew_guarantee",
                "crew_gacha_fragments_required_r",
                "crew_gacha_fragments_required_sr",
                "crew_gacha_fragments_required_ssr",
                "crew_gacha_common_fragment_item_id",
                "crew_gacha_prism_reward_policy",
                "crew_gacha_prism_ticket_item_id",
                "bm_placeholder_enabled",
                "currency_exchange_enabled",
                "kmb_display_enabled"
            };

            for (int i = 0; i < economyParamKeys.Length; i++)
            {
                AssertTrue(catalog.EconomyParamsByKey.ContainsKey(economyParamKeys[i]), "economy param exists: " + economyParamKeys[i]);
            }

            string[] disabledItemIds =
            {
                "mat_high_grade_core",
                "ticket_instant_complete",
                "ticket_crew_fragment_basic",
                "box_basic_reward",
                "ticket_choice_basic"
            };

            for (int i = 0; i < disabledItemIds.Length; i++)
            {
                AssertTrue(catalog.ItemsById.TryGetValue(disabledItemIds[i], out ItemDefinition item), "placeholder item exists: " + disabledItemIds[i]);
                AssertFalse(item.IsEnabled, "placeholder item disabled: " + disabledItemIds[i]);
            }

            AssertTrue(catalog.ItemsById.TryGetValue("mat_upgrade_common", out ItemDefinition upgradeMaterial), "upgrade material item exists");
            AssertTrue(upgradeMaterial.IsEnabled, "upgrade material is enabled for Gold shop");
            AssertEqual("UpgradeMaterial", upgradeMaterial.Category, "upgrade material category");
            AssertTrue(catalog.ItemsById.TryGetValue("ticket_speedup_10m", out ItemDefinition speedupTicket), "speedup ticket item exists");
            AssertTrue(speedupTicket.IsEnabled, "speedup ticket is enabled for P0 fixture flow");
            AssertTrue(catalog.EconomyParamsByKey.TryGetValue("speedup_ticket_seconds", out EconomyParam speedupSeconds), "speedup ticket seconds exists");
            AssertTrue(speedupSeconds.IsEnabled, "speedup ticket seconds enabled for P0 fixture flow");
            AssertTrue(catalog.ItemsById.TryGetValue("ticket_recruit_basic", out ItemDefinition basicRecruitTicket), "basic recruit ticket exists");
            AssertTrue(basicRecruitTicket.IsEnabled, "basic recruit ticket is enabled for gold fragment gacha seam");
            AssertTrue(catalog.ItemsById.TryGetValue("ticket_crew_fragment_basic", out ItemDefinition crewFragment), "crew fragment ticket exists");
            AssertFalse(crewFragment.IsEnabled, "common crew fragment is held for package/reward use");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Navigator_Jun", "R");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Captain_Max", "R");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Dasol", "R");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Rookie_Sailor", "R");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Veteran_Angler", "SR");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Chef_Lin", "SR");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Veteran_Navigator", "SR");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_OldMan_And_Sea", "SSR");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Moby_Dick", "SSR");
            AssertEnabledCrewFragmentItem(catalog, "fragment_Crew_Master_Angler", "SSR");
            AssertTrue(catalog.ItemsById.TryGetValue("ticket_recruit_premium", out ItemDefinition premiumRecruitTicket), "premium recruit ticket exists");
            AssertTrue(premiumRecruitTicket.IsEnabled, "premium recruit ticket is enabled for prism gacha ticket seam");
            AssertEnabledBoatCollectionItem(catalog, "boat_vacuum");
            AssertEnabledBoatCollectionItem(catalog, "boat_lamp");
            AssertEnabledBoatCollectionItem(catalog, "boat_penguin");

            string[] disabledShopItemIds =
            {
                "shop_bag_capacity_001",
                "shop_cooking_slot_001",
                "shop_recruit_ticket_001",
                "shop_instant_complete_ticket_001",
                "shop_gold_crew_fragment_001",
                "shop_special_goods_placeholder",
                "shop_exchange_placeholder",
                "shop_material_choice_box_001"
            };

            for (int i = 0; i < disabledShopItemIds.Length; i++)
            {
                AssertTrue(catalog.ShopItemsById.TryGetValue(disabledShopItemIds[i], out ShopItemDefinition shopItem), "placeholder shop item exists: " + disabledShopItemIds[i]);
                AssertFalse(shopItem.IsEnabled, "placeholder shop item disabled: " + disabledShopItemIds[i]);
            }

            string[] stageOnePearlShopItemIds =
            {
                "shop_prism_recruit_ticket_001",
                "shop_prism_recruit_ticket_010",
                "shop_speedup_ticket_001",
                "shop_starter_cooking_pack_001"
            };

            for (int i = 0; i < stageOnePearlShopItemIds.Length; i++)
            {
                AssertTrue(catalog.ShopItemsById.TryGetValue(stageOnePearlShopItemIds[i], out ShopItemDefinition shopItem), "stage 1 pearl shop item exists: " + stageOnePearlShopItemIds[i]);
                AssertTrue(shopItem.IsEnabled, "stage 1 pearl shop item enabled: " + stageOnePearlShopItemIds[i]);
                AssertEqual("stage>=1", shopItem.UnlockCondition, "stage 1 pearl shop item unlock: " + stageOnePearlShopItemIds[i]);
                AssertEqual("stage>=1", shopItem.VisibilityCondition, "stage 1 pearl shop item visibility: " + stageOnePearlShopItemIds[i]);
            }

            AssertTrue(catalog.ShopItemsById.TryGetValue("shop_material_choice_box_001", out ShopItemDefinition specialOffer), "player-sync special offer exists");
            AssertEqual("stage>=2;currency:softCurrency>=700;acquired:fish_anchovy>=1", specialOffer.VisibilityCondition, "player-sync special offer visibility condition");
            AssertShopConditionReferences(catalog);
        }

        private static void AssertEnabledBoatCollectionItem(BalanceCatalog catalog, string itemId)
        {
            AssertTrue(catalog.ItemsById.TryGetValue(itemId, out ItemDefinition item), "boat collection item exists: " + itemId);
            AssertTrue(item.IsEnabled, "boat collection item enabled: " + itemId);
            AssertEqual("Boat", item.Category, "boat collection category: " + itemId);
        }

        private static void AssertEnabledCrewFragmentItem(BalanceCatalog catalog, string itemId, string gradeLabel)
        {
            AssertTrue(catalog.ItemsById.TryGetValue(itemId, out ItemDefinition item), "crew fragment item exists: " + itemId);
            AssertTrue(item.IsEnabled, "crew fragment item enabled: " + itemId);
            AssertEqual("Ticket", item.Category, "crew fragment category: " + itemId);
            AssertEqual("Recruit", item.SourceType, "crew fragment source type: " + itemId);
            AssertTrue(item.CookTag == "crew_fragment", "crew fragment tag: " + itemId + " / grade=" + gradeLabel);
        }

        private static void AssertShopConditionReferences(BalanceCatalog catalog)
        {
            foreach (ShopItemDefinition shopItem in catalog.ShopItemsById.Values)
            {
                AssertConditionReferences(catalog, shopItem.UnlockCondition, shopItem.ShopItemId + ".unlockCondition", shopItem.IsEnabled);
                AssertConditionReferences(catalog, shopItem.VisibilityCondition, shopItem.ShopItemId + ".visibilityCondition", shopItem.IsEnabled);
            }
        }

        private static void RunServerTitleDataContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildGeneratedCatalogForTool("server TitleData contract check");
            AssertUpgradeMaterialContract(catalog, "mat_upgrade_common");
            AssertUpgradeMaterialContract(catalog, "mat_upgrade_sturdy");
            AssertUpgradeMaterialContract(catalog, "mat_upgrade_refined");
            AssertServerOnlyMaterialContract(catalog, "mat_high_grade_core");

            int serverOnlyCount = 0;
            int fragmentCount = 0;
            foreach (ItemDefinition item in catalog.ItemsById.Values)
            {
                if (IsServerOnlyItemCategory(item.Category))
                {
                    serverOnlyCount++;
                    AssertEqual(0L, item.SellPrice, "server-only item sellPrice must stay 0: " + item.ItemId + " / category=" + item.Category);
                    AssertFalse(ItemSellPolicy.IsSellable(item), "server-only item must not be sellable by policy: " + item.ItemId);

                    if (item.IsEnabled)
                    {
                        PlayerRuntimeState state = new PlayerRuntimeState();
                        InventoryService inventory = new InventoryService(catalog, state);
                        ServiceResult add = inventory.TryAddItem(item.ItemId, 1);
                        if (add.Success)
                        {
                            AssertFailKey(inventory.TrySellItem(item.ItemId, 1), "inventory.not_sellable", "server-only sell block: " + item.ItemId);
                        }
                    }
                }

                if (item.ItemId.StartsWith("fragment_", StringComparison.Ordinal))
                {
                    fragmentCount++;
                    AssertTrue(item.ItemId.StartsWith("fragment_Crew_", StringComparison.Ordinal), "crew fragment id prefix should be fragment_Crew_: " + item.ItemId);
                    AssertFalse(item.ItemId.StartsWith("crew_", StringComparison.OrdinalIgnoreCase), "crew fragment must not use crew_ prefix: " + item.ItemId);
                    AssertEqual("Ticket", item.Category, "crew fragment category should remain Ticket: " + item.ItemId);
                    AssertEqual("crew_fragment", item.CookTag, "crew fragment cookTag: " + item.ItemId);
                }
            }

            foreach (ShopItemDefinition shopItem in catalog.ShopItemsById.Values)
            {
                AssertTrue(!string.IsNullOrEmpty(shopItem.RewardItemId), "shop rewardItemId should exist for server TitleData export: " + shopItem.ShopItemId);
                AssertTrue(catalog.ItemsById.TryGetValue(shopItem.RewardItemId, out ItemDefinition reward), "shop reward item should exist: " + shopItem.ShopItemId + " -> " + shopItem.RewardItemId);
                if (shopItem.IsEnabled && IsServerOnlyItemCategory(reward.Category))
                {
                    AssertFalse(ItemSellPolicy.IsSellable(reward), "enabled shop reward must not become sellable server-only item: " + shopItem.ShopItemId + " -> " + reward.ItemId);
                }
            }

            report.Add("서버형 아이템 판매 차단: " + serverOnlyCount + " items OK");
            report.Add("선원 조각 ID 규칙: " + fragmentCount + " fragments OK");
        }

        private static void DiagnoseCrewGachaPolicy(BalanceCatalog catalog, TestReport report)
        {
            AssertParam(catalog, "crew_gacha_gold_reward_policy", "crew_specific_fragments_with_low_full_crew");
            AssertParam(catalog, "crew_gacha_gold_fragment_id_pattern", "fragment_Crew_*");
            AssertParam(catalog, "crew_gacha_gold_fragment_grade_scope", "R_SR_only");
            AssertParam(catalog, "crew_gacha_gold_full_crew_grade_scope", "R_SR_only");
            AssertParam(catalog, "crew_gacha_gold_ten_full_crew_guarantee", "FALSE");
            AssertParam(catalog, "crew_gacha_prism_reward_policy", "full_crew_with_fragment_fallback");
            AssertParam(catalog, "crew_gacha_prism_ticket_item_id", "ticket_recruit_premium");

            decimal fullCrewChance = GetDecimalParam(catalog, "crew_gacha_gold_full_crew_chance_pct");
            if (fullCrewChance < 2m || fullCrewChance > 3m)
            {
                report.Warn("골드 모집 완성 선원 확률이 합의 범위 2~3% 밖입니다. value=" + fullCrewChance.ToString(CultureInfo.InvariantCulture));
            }

            long goldSingleCost = GetLongParam(catalog, "crew_gacha_gold_single_cost");
            long goldTenCost = GetLongParam(catalog, "crew_gacha_gold_ten_cost");
            long prismSingleCost = GetLongParam(catalog, "crew_gacha_prism_single_cost");
            long prismTenCost = GetLongParam(catalog, "crew_gacha_prism_ten_cost");
            if (goldSingleCost > 0 && goldTenCost != goldSingleCost * 10)
            {
                report.Warn("골드 10회 모집 비용이 1회 비용 x10과 다릅니다. single=" + goldSingleCost + ", ten=" + goldTenCost);
            }

            if (prismSingleCost > 0 && prismTenCost > 0)
            {
                long expectedTenCost = prismSingleCost * 9;
                if (prismTenCost != expectedTenCost)
                {
                    report.Warn("프리미엄 10회 모집 비용이 임시 10% 할인 기준과 다릅니다. expected=" + expectedTenCost + ", actual=" + prismTenCost);
                }
            }

            int rRequired = GetIntParam(catalog, "crew_gacha_fragments_required_r");
            int srRequired = GetIntParam(catalog, "crew_gacha_fragments_required_sr");
            int ssrRequired = GetIntParam(catalog, "crew_gacha_fragments_required_ssr");
            AssertTrue(rRequired > 0 && srRequired > rRequired && ssrRequired > srRequired, "crew fragment requirements should increase by grade.");

            report.Add("골드 모집: 선원별 조각 기본 / 완성 선원 " + fullCrewChance.ToString(CultureInfo.InvariantCulture) + "% / 10회 보장 없음");
            report.Add("조각 교환 기준: R=" + rRequired + ", SR=" + srRequired + ", SSR=" + ssrRequired);
        }

        private static void DiagnoseBmShopPolicy(BalanceCatalog catalog, TestReport report)
        {
            bool bmEnabled = GetBoolParam(catalog, "bm_placeholder_enabled");
            int paidEnabled = 0;
            int paidDisabled = 0;
            int goldEnabled = 0;

            foreach (ShopItemDefinition shopItem in catalog.ShopItemsById.Values)
            {
                if (shopItem.PriceType == "prismPearl" ||
                    shopItem.PriceType == "pirateCoin")
                {
                    if (shopItem.IsEnabled)
                    {
                        paidEnabled++;
                        if (!bmEnabled)
                        {
                            report.Warn("BM placeholder가 꺼져 있는데 유료/특수 재화 상품이 enabled입니다: " + shopItem.ShopItemId);
                        }
                    }
                    else
                    {
                        paidDisabled++;
                    }
                }
                else if (shopItem.PriceType == "softCurrency" && shopItem.IsEnabled)
                {
                    goldEnabled++;
                }

                AssertTrue(catalog.ItemsById.ContainsKey(shopItem.RewardItemId), "shop reward item should exist: " + shopItem.ShopItemId + " -> " + shopItem.RewardItemId);
            }

            report.Add("상점 enabled 상품: 골드=" + goldEnabled + ", 유료/특수=" + paidEnabled + ", 유료/특수 보류=" + paidDisabled);
            report.Add("BM 실행 플래그: " + (bmEnabled ? "ON" : "OFF"));
        }

        private static void DiagnoseUpgradeMaterialEconomy(BalanceCatalog catalog, TestReport report)
        {
            long startingGold = GetLongParam(catalog, "starting_soft_currency");
            int activeMaterialPacks = 0;
            bool hasCommonPack = false;
            bool hasSturdyPack = false;
            bool hasRefinedPack = false;

            foreach (ShopItemDefinition shopItem in catalog.ShopItemsById.Values)
            {
                if (!catalog.ItemsById.TryGetValue(shopItem.RewardItemId, out ItemDefinition reward) ||
                    reward.Category != "UpgradeMaterial")
                {
                    continue;
                }

                if (shopItem.IsEnabled)
                {
                    activeMaterialPacks++;
                    if (shopItem.RewardItemId == "mat_upgrade_common")
                    {
                        hasCommonPack = true;
                    }
                    else if (shopItem.RewardItemId == "mat_upgrade_sturdy")
                    {
                        hasSturdyPack = true;
                    }
                    else if (shopItem.RewardItemId == "mat_upgrade_refined")
                    {
                        hasRefinedPack = true;
                    }

                    if (shopItem.PriceType == "softCurrency" &&
                        shopItem.PriceAmount > 0 &&
                        startingGold >= shopItem.PriceAmount &&
                        ConditionContainsStageOne(shopItem.UnlockCondition))
                    {
                        long buyCount = startingGold / shopItem.PriceAmount;
                        report.Warn("시작 골드로 즉시 재료팩 구매 가능: " + shopItem.ShopItemId + " x" + buyCount + " / price=" + shopItem.PriceAmount);
                    }

                    if (shopItem.RewardCount > 0 && reward.SellPrice > 0)
                    {
                        decimal unitBuyPrice = shopItem.PriceAmount / (decimal)shopItem.RewardCount;
                        if (unitBuyPrice <= reward.SellPrice * 2m)
                        {
                            report.Warn("재료팩 구매가와 재판매가 차이가 작습니다: " + shopItem.ShopItemId + " unitBuy=" + unitBuyPrice.ToString("0.##", CultureInfo.InvariantCulture) + ", sell=" + reward.SellPrice);
                        }
                    }
                }
            }

            AssertTrue(hasCommonPack, "enabled common upgrade material pack should exist.");
            AssertTrue(hasSturdyPack, "enabled sturdy upgrade material pack should exist.");
            AssertTrue(hasRefinedPack, "enabled refined upgrade material pack should exist.");
            report.Add("활성 강화재료 상점팩: " + activeMaterialPacks + "개 / 시작 골드=" + startingGold);
        }

        private static void DiagnoseServerOnlyItemPolicy(BalanceCatalog catalog, TestReport report)
        {
            int serverOnlyCount = 0;
            int enabledServerOnlyCount = 0;
            foreach (ItemDefinition item in catalog.ItemsById.Values)
            {
                if (!IsServerOnlyItemCategory(item.Category))
                {
                    continue;
                }

                serverOnlyCount++;
                if (item.IsEnabled)
                {
                    enabledServerOnlyCount++;
                }

                AssertEqual(0L, item.SellPrice, "server-only item sellPrice must stay 0: " + item.ItemId);
                AssertFalse(ItemSellPolicy.IsSellable(item), "server-only item must not be sellable: " + item.ItemId);
            }

            report.Add("서버형 판매불가 아이템: " + serverOnlyCount + "개 / enabled=" + enabledServerOnlyCount);
        }

        private static void AssertUpgradeMaterialContract(BalanceCatalog catalog, string itemId)
        {
            AssertTrue(catalog.ItemsById.TryGetValue(itemId, out ItemDefinition item), "upgrade material exists: " + itemId);
            AssertTrue(item.IsEnabled, "upgrade material enabled: " + itemId);
            AssertEqual("UpgradeMaterial", item.Category, "upgrade material category: " + itemId);
            AssertTrue(item.Stackable, "upgrade material should stack: " + itemId);
            AssertTrue(item.MaxStack > 0, "upgrade material maxStack: " + itemId);
            AssertTrue(item.SellPrice > 0, "upgrade material sellPrice should be positive for UI and sell flow: " + itemId);
            AssertTrue(ItemSellPolicy.IsSellable(item), "upgrade material should remain sellable: " + itemId);
        }

        private static void AssertServerOnlyMaterialContract(BalanceCatalog catalog, string itemId)
        {
            AssertTrue(catalog.ItemsById.TryGetValue(itemId, out ItemDefinition item), "server-only material exists: " + itemId);
            AssertEqual("HighGradeMaterial", item.Category, "server-only material category: " + itemId);
            AssertFalse(item.IsEnabled, "server-only high grade material should stay disabled until server use is ready: " + itemId);
            AssertEqual(0L, item.SellPrice, "server-only material sellPrice must stay 0: " + itemId);
            AssertFalse(ItemSellPolicy.IsSellable(item), "server-only material must not be sellable: " + itemId);
        }

        private static bool IsServerOnlyItemCategory(string category)
        {
            return category == "HighGradeMaterial" ||
                   category == "Ticket" ||
                   category == "Box" ||
                   category == "ChoiceTicket";
        }

        private static BalanceCatalog BuildGeneratedCatalogForTool(string label)
        {
            BalanceBuildResult build = BalanceCatalogBuilder.Build(new BalanceCsvSet
            {
                ItemsCsv = ReadGenerated("items.csv"),
                FishCsv = ReadGenerated("fish.csv"),
                ShopItemsCsv = ReadGenerated("shop_items.csv"),
                PremiumCurrencyProductsCsv = ReadGenerated("premium_currency_products.csv", optional: true),
                RecipesCsv = ReadGenerated("recipes.csv"),
                CollectionRewardsCsv = ReadGenerated("collection_rewards.csv"),
                EconomyParamsCsv = ReadGenerated("economy_params.csv"),
                StackRulesCsv = ReadGenerated("stack_rules.csv")
            });
            AssertTrue(build.Success, "catalog build should succeed before " + label + ". errors=" + string.Join(" | ", build.Errors));
            return build.Catalog;
        }

        private static void AssertParam(BalanceCatalog catalog, string key, string expectedValue)
        {
            AssertTrue(catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param), "economy param exists: " + key);
            AssertEqual(expectedValue, param.Value, "economy param value: " + key);
        }

        private static bool GetBoolParam(BalanceCatalog catalog, string key)
        {
            AssertTrue(catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param), "economy param exists: " + key);
            return string.Equals(param.Value, "TRUE", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetIntParam(BalanceCatalog catalog, string key)
        {
            long value = GetLongParam(catalog, key);
            AssertTrue(value >= int.MinValue && value <= int.MaxValue, "economy param int range: " + key);
            return (int)value;
        }

        private static long GetLongParam(BalanceCatalog catalog, string key)
        {
            AssertTrue(catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param), "economy param exists: " + key);
            AssertTrue(long.TryParse(param.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value), "economy param should be integer: " + key + " / " + param.Value);
            return value;
        }

        private static decimal GetDecimalParam(BalanceCatalog catalog, string key)
        {
            AssertTrue(catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param), "economy param exists: " + key);
            AssertTrue(decimal.TryParse(param.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value), "economy param should be decimal: " + key + " / " + param.Value);
            return value;
        }

        private static bool ConditionContainsStageOne(string condition)
        {
            return !string.IsNullOrEmpty(condition) &&
                   condition.IndexOf("stage>=1", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AssertConditionReferences(BalanceCatalog catalog, string condition, string label, bool ownerEnabled)
        {
            string normalized = string.IsNullOrWhiteSpace(condition) ? string.Empty : condition.Trim();
            if (normalized.Length == 0 ||
                normalized.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("always", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("FALSE", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("bm_placeholder", StringComparison.Ordinal))
            {
                return;
            }

            if (IsKnownDisabledHoldCondition(normalized))
            {
                AssertFalse(ownerEnabled, label + " uses HOLD condition on enabled shop item: " + normalized);
                return;
            }

            string[] parts = normalized.Split(new[] { "&&", ";" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    AssertConditionReferences(catalog, parts[i], label, ownerEnabled);
                }

                return;
            }

            if (AssertStageCondition(normalized, "stage>=", label) ||
                AssertStageCondition(normalized, "currentStage>=", label) ||
                AssertStageCondition(normalized, "farthestStage>=", label))
            {
                return;
            }

            if (normalized.StartsWith("param:", StringComparison.Ordinal))
            {
                string key = normalized.Substring("param:".Length);
                AssertTrue(catalog.EconomyParamsByKey.ContainsKey(key), label + " references missing economy param: " + key);
                return;
            }

            if (TryParseConditionThreshold(normalized, "currency:", out string currency, out long requiredCurrency))
            {
                AssertTrue(FisherCurrencyContract.IsKnownCurrency(currency), label + " references unsupported currency: " + currency);
                AssertTrue(requiredCurrency >= 0, label + " currency threshold must be >= 0");
                return;
            }

            if (TryParseConditionThreshold(normalized, "owned:", out string ownedItemId, out long requiredOwned))
            {
                AssertTrue(catalog.ItemsById.ContainsKey(ownedItemId), label + " references missing owned item: " + ownedItemId);
                AssertTrue(requiredOwned >= 0, label + " owned threshold must be >= 0");
                return;
            }

            if (TryParseConditionThreshold(normalized, "acquired:", out string acquiredItemId, out long requiredAcquired))
            {
                AssertTrue(catalog.ItemsById.ContainsKey(acquiredItemId), label + " references missing acquired item: " + acquiredItemId);
                AssertTrue(requiredAcquired >= 0, label + " acquired threshold must be >= 0");
                return;
            }

            AssertTrue(false, label + " has unsupported condition syntax: " + normalized);
        }

        private static bool IsKnownDisabledHoldCondition(string condition)
        {
            switch (condition)
            {
                case "bag_capacity_placeholder":
                case "cooking_slot_placeholder":
                case "recruit_placeholder":
                case "instant_complete_placeholder":
                case "exchange_placeholder":
                    return true;
                default:
                    return false;
            }
        }

        private static bool AssertStageCondition(string condition, string prefix, string label)
        {
            if (!condition.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string rawStage = condition.Substring(prefix.Length);
            AssertTrue(int.TryParse(rawStage, out int requiredStage), label + " stage threshold must be int: " + condition);
            AssertTrue(requiredStage >= 0, label + " stage threshold must be >= 0");
            return true;
        }

        private static bool TryParseConditionThreshold(string condition, string prefix, out string key, out long required)
        {
            key = string.Empty;
            required = 0;

            if (!condition.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string body = condition.Substring(prefix.Length);
            int separator = body.IndexOf(">=", StringComparison.Ordinal);
            if (separator <= 0 || separator + 2 >= body.Length)
            {
                AssertTrue(false, "Invalid condition threshold syntax: " + condition);
                return true;
            }

            key = body.Substring(0, separator);
            string rawRequired = body.Substring(separator + 2);
            AssertTrue(long.TryParse(rawRequired, out required), "Condition threshold must be numeric: " + condition);
            return true;
        }

        private static void RunGeneratedSpeedupFixtureFlow(BalanceCatalog catalog)
        {
            PlayerRuntimeState speedupState = new PlayerRuntimeState();
            ManualClock speedupClock = new ManualClock(new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
            InventoryService speedupInventory = new InventoryService(catalog, speedupState);
            CookingService speedupCooking = new CookingService(catalog, speedupState, speedupInventory, speedupClock);

            AssertTrue(catalog.EconomyParamsByKey.TryGetValue("speedup_ticket_seconds", out EconomyParam speedupParam), "speedup seconds param exists");
            AssertTrue(speedupParam.IsEnabled, "speedup seconds param enabled");
            AssertTrue(int.TryParse(speedupParam.Value, out int seconds) && seconds > 0, "speedup seconds param is positive int");

            AssertSuccess(speedupInventory.TryAddItem("fish_anchovy", 1), "speedup fixture anchovy add");
            AssertSuccess(speedupInventory.TryAddItem("fish_saury", 1), "speedup fixture saury add");
            AssertSuccess(speedupInventory.TryAddItem("ticket_speedup_10m", 1), "speedup fixture ticket add");
            AssertSuccess(speedupCooking.TryStartRecipe("recipe_grilled_anchovy_g1"), "speedup fixture recipe start");

            long beforeTicks = speedupState.activeRecipeState.completesUtcTicks;
            AssertSuccess(speedupCooking.TryUseSpeedupItem("ticket_speedup_10m", seconds), "generated speedup ticket use");
            AssertEqual(0, speedupInventory.CountItem("ticket_speedup_10m"), "generated speedup ticket consumed");
            AssertTrue(speedupState.activeRecipeState.completesUtcTicks < beforeTicks, "generated speedup should move completion earlier");
            AssertSuccess(speedupCooking.TryCompleteRecipe(), "generated speedup should make starter recipe complete");
            AssertEqual(1, speedupInventory.CountItem("food_grilled_anchovy"), "generated speedup recipe output");
        }

        private static void RunCurrencyDisplayContractTests(TestReport report)
        {
            PlayerRuntimeState state = new PlayerRuntimeState
            {
                softCurrency = 1234,
                prismPearl = 56,
                pirateCoin = 7,
                cash = 999
            };

            AssertEqual("골드 1.2K / 진주 56 / 해적 7", FisherCurrencyContract.FormatWallet(state), "currency wallet display");
            AssertEqual("1.2K G", FisherCurrencyContract.FormatAmount("gold", 1234), "gold price display");
            AssertEqual("56 PP", FisherCurrencyContract.FormatAmount("PrismPearl", 56), "prism price display");
            AssertEqual("7 PC", FisherCurrencyContract.FormatAmount("pirate_coin", 7), "pirate price display");
            AssertEqual(1234L, FisherCurrencyContract.GetBalance(state, "softCurrency"), "gold balance lookup");
            AssertEqual(56L, FisherCurrencyContract.GetBalance(state, "prismPearl"), "prism balance lookup");
            AssertEqual(7L, FisherCurrencyContract.GetBalance(state, "PirateCoin"), "pirate balance lookup");
            AssertTrue(FisherCurrencyContract.IsKnownCurrency("prism_pearl"), "prism currency known");
            AssertTrue(FisherCurrencyContract.IsKnownCurrency("pirateCoin"), "pirate currency known");
            AssertFalse(FisherCurrencyContract.IsGoldCurrency("prismPearl"), "prism is not gold spend path");
            AssertFalse(FisherCurrencyContract.IsKnownCurrency("cash"), "hidden cash should not be display/shop currency");
            AssertEqual(999L, FisherCurrencyContract.GetHiddenCashBalance(state), "hidden cash balance lookup");
            AssertTrue(FisherCurrencyContract.TryConsumeHiddenCash(state, 400, out long cashAfterSpend), "hidden cash spend");
            AssertEqual(599L, cashAfterSpend, "hidden cash spend balance");
            AssertTrue(FisherCurrencyContract.TryGrantHiddenCash(state, 100, out long cashAfterGrant), "hidden cash grant");
            AssertEqual(699L, cashAfterGrant, "hidden cash grant balance");
        }

        private static void RunServerRequestGateContractTests(TestReport report)
        {
            Type gateType = typeof(FisherRuntimeContext).Assembly.GetType("Fisher.PlayerSystems.FisherServerRequestGate", throwOnError: false);
            AssertTrue(gateType != null, "server request gate type should exist");

            object gate = Activator.CreateInstance(gateType, nonPublic: true);
            MethodInfo tryBegin = RequireMethod(gateType, "TryBegin");
            MethodInfo tryComplete = RequireMethod(gateType, "TryComplete");
            MethodInfo tryAbort = RequireMethod(gateType, "TryAbort");
            MethodInfo tryRecoverTimeout = RequireMethod(gateType, "TryRecoverTimeout");
            MethodInfo currentMessage = RequireMethod(gateType, "CurrentMessage");
            PropertyInfo isBusy = RequireProperty(gateType, "IsBusy");
            PropertyInfo token = RequireProperty(gateType, "Token");

            AssertTrue(InvokeBool(tryBegin, gate, "PurchaseShopItem"), "gate should begin first request");
            int firstToken = (int)token.GetValue(gate);
            AssertTrue((bool)isBusy.GetValue(gate), "gate should be busy after begin");
            AssertEqual("서버 구매 요청 중: PurchaseShopItem", (string)currentMessage.Invoke(gate, new object[] { "서버 구매 요청 중" }), "gate busy message");
            AssertFalse(InvokeBool(tryBegin, gate, "CancelCooking"), "gate should reject duplicate begin");
            AssertFalse(InvokeBool(tryComplete, gate, firstToken + 1), "gate should reject stale complete token");
            AssertTrue((bool)isBusy.GetValue(gate), "gate should stay busy after stale complete");
            AssertTrue(InvokeBool(tryComplete, gate, firstToken), "gate should complete current token");
            AssertFalse((bool)isBusy.GetValue(gate), "gate should clear busy after complete");
            AssertFalse(InvokeBool(tryAbort, gate, firstToken), "gate should reject stale abort token");

            AssertTrue(InvokeBool(tryBegin, gate, "CancelCooking"), "gate should begin second request");
            int cancelToken = (int)token.GetValue(gate);
            AssertTrue(InvokeBool(tryAbort, gate, cancelToken), "gate should abort current token");
            AssertFalse((bool)isBusy.GetValue(gate), "gate should clear busy after abort");

            AssertTrue(InvokeBool(tryBegin, gate, "SlowCooking"), "gate should begin timeout request");
            FieldInfo startedAt = gateType.GetField("_startedAt", BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(startedAt != null, "gate startedAt field should exist for timeout simulation");
            startedAt.SetValue(gate, Time.unscaledTime - 20f);
            object[] timeoutArgs = { 1f, "FallbackCooking", null };
            AssertTrue((bool)tryRecoverTimeout.Invoke(gate, timeoutArgs), "gate should recover expired request");
            AssertEqual("SlowCooking", (string)timeoutArgs[2], "gate timeout request name");
            AssertFalse((bool)isBusy.GetValue(gate), "gate should clear busy after timeout recovery");
        }

        private static void RunServerMutationResponseContractTests(TestReport report)
        {
            Type bridgeType = typeof(FisherPlayerDataBridge);
            Type responseType = bridgeType.GetNestedType("CloudScriptMutationResponse", BindingFlags.NonPublic);
            AssertTrue(responseType != null, "CloudScriptMutationResponse nested type should exist");

            MethodInfo hasCookingState = bridgeType.GetMethod("HasCookingState", BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(hasCookingState != null, "HasCookingState should exist");
            MethodInfo tryReadUtcTicks = bridgeType.GetMethod("TryReadUtcTicks", BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(tryReadUtcTicks != null, "TryReadUtcTicks should exist for cooking identity precision");
            MethodInfo tryBuildActiveRecipe = bridgeType.GetMethod(
                "TryBuildActiveRecipeFromServerSlot",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(CookingJobInfo), typeof(ActiveRecipeState).MakeByRefType() },
                null);
            AssertTrue(tryBuildActiveRecipe != null, "TryBuildActiveRecipeFromServerSlot should exist for typed cooking jobs");
            MethodInfo tryBuildActiveRecipeFromRawJob = bridgeType.GetMethod(
                "TryBuildActiveRecipeFromServerSlot",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(JObject), typeof(ActiveRecipeState).MakeByRefType() },
                null);
            AssertTrue(tryBuildActiveRecipeFromRawJob != null, "TryBuildActiveRecipeFromServerSlot should exist for raw CloudScript jobs");
            FieldInfo rawJson = responseType.GetField("rawJson", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            AssertTrue(rawJson != null, "CloudScriptMutationResponse.rawJson should exist");

            long expectedShortFractionTicks = new DateTime(2026, 6, 23, 6, 19, 53, DateTimeKind.Utc).Ticks +
                                             TimeSpan.FromMilliseconds(180).Ticks;
            object[] shortFractionArgs = { "2026-06-23T06:19:53.18Z", 0L };
            AssertTrue((bool)tryReadUtcTicks.Invoke(null, shortFractionArgs), "TryReadUtcTicks should parse short millisecond fraction");
            AssertEqual(expectedShortFractionTicks, (long)shortFractionArgs[1], "short millisecond fraction ticks");

            long expectedLongFractionTicks = new DateTime(2026, 6, 23, 6, 20, 46, DateTimeKind.Utc).Ticks +
                                            TimeSpan.FromMilliseconds(905).Ticks;
            object[] longFractionArgs = { "2026-06-23T06:20:46.9050000Z", 0L };
            AssertTrue((bool)tryReadUtcTicks.Invoke(null, longFractionArgs), "TryReadUtcTicks should parse seven-digit fraction");
            AssertEqual(expectedLongFractionTicks, (long)longFractionArgs[1], "seven-digit fraction ticks");

            CookingJobInfo cookingJob = new CookingJobInfo
            {
                recipeId = "recipe_grilled_anchovy_g1",
                totalCount = 1,
                claimedCount = 0,
                durationSec = 60,
                startedAtUtc = "2026-06-23T06:19:53.18Z"
            };
            object[] buildActiveArgs = { "0", cookingJob, null };
            AssertTrue((bool)tryBuildActiveRecipe.Invoke(null, buildActiveArgs), "server cooking slot should build active state with fractional start");
            ActiveRecipeState builtActive = buildActiveArgs[2] as ActiveRecipeState;
            AssertTrue(builtActive != null, "server cooking slot active state should be returned");
            AssertEqual(expectedShortFractionTicks, builtActive.startedUtcTicks, "server cooking slot preserves fractional startedAtUtc");
            AssertEqual(expectedShortFractionTicks + TimeSpan.FromSeconds(60).Ticks, builtActive.completesUtcTicks, "server cooking slot completion uses fractional start");

            JObject rawCookingJob = ParseRawMutationJsonForTest("{\"recipeId\":\"recipe_grilled_anchovy_g1\",\"totalCount\":1,\"claimedCount\":0,\"durationSec\":60,\"startedAtUtc\":\"2026-06-23T06:20:46.905Z\"}");
            object[] rawBuildActiveArgs = { "0", rawCookingJob, null };
            AssertTrue((bool)tryBuildActiveRecipeFromRawJob.Invoke(null, rawBuildActiveArgs), "raw CloudScript cooking job should build active state");
            ActiveRecipeState rawBuiltActive = rawBuildActiveArgs[2] as ActiveRecipeState;
            AssertTrue(rawBuiltActive != null, "raw CloudScript cooking job active state should be returned");
            AssertEqual(expectedLongFractionTicks, rawBuiltActive.startedUtcTicks, "raw CloudScript cooking job preserves fractional startedAtUtc");
            AssertEqual(expectedLongFractionTicks + TimeSpan.FromSeconds(60).Ticks, rawBuiltActive.completesUtcTicks, "raw CloudScript cooking job completion uses fractional start");

            RunRawCookingMutationApplyContract(bridgeType);

            object cookingFailure = Activator.CreateInstance(responseType, nonPublic: true);
            rawJson.SetValue(cookingFailure, "{\"success\":false,\"error\":\"cooking.no_job\",\"cookingData\":{\"cookSlots\":{}}}");
            AssertTrue((bool)hasCookingState.Invoke(null, new[] { cookingFailure }), "failed cooking response with cookingData should be authoritative");

            object shopFailure = Activator.CreateInstance(responseType, nonPublic: true);
            rawJson.SetValue(shopFailure, "{\"success\":false,\"error\":\"shop.not_enough_currency\"}");
            AssertFalse((bool)hasCookingState.Invoke(null, new[] { shopFailure }), "shop failure without cookingData should not be treated as cooking state");

            string bridgeSource = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/Integration/FisherPlayerDataBridge.cs");
            AssertTrue(bridgeSource.Contains("HandleRejectedMutation(gateway, operation, response"), "rejected mutations should receive parsed FunctionResult");
            AssertTrue(bridgeSource.Contains("ApplyCloudScriptMutationResponse(operation, response, allowDeltaFallback: false)"), "rejected mutation response should apply authoritative snapshots without delta fallback");
            AssertTrue(bridgeSource.Contains("PullCurrenciesFromPlayerData(notify: false)"), "bridge Configure should not notify during bootstrap");
            AssertTrue(bridgeSource.Contains("BuildCookingMutationArgs(slotIndex, recipeId, activeStartedUtcTicks)"), "cooking mutations should send active recipe identity, not slotIndex only");
            AssertTrue(bridgeSource.Contains("args[\"activeStartedAtUtc\"] = activeStartedAtUtc"), "cooking mutation args should include activeStartedAtUtc when available");
            AssertFalse(bridgeSource.Contains("ResolveServerCookingMutationSlot"), "client should not rewrite cooking slotIndex from stale PlayFabDataStore");
            AssertFalse(bridgeSource.Contains("cookSlots.Count == 0"), "empty server cookSlots should clear stale local cooking UI");
            AssertFalse(bridgeSource.Contains("TryApplyClearedCookingSlotSnapshot"), "bridge should not expose no_job string recovery as a UI escape hatch");

            string bootstrapperSource = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/Integration/FisherRuntimeBootstrapper.cs");
            AssertFalse(bootstrapperSource.Contains("bridge.Configure(context)"), "bridge resolver should not configure during panel refresh");

            string cookingSource = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/UI/CookingPanelAdapter.cs");
            AssertTrue(cookingSource.Contains("currentActive.startedUtcTicks"), "CookingPanelAdapter should pass active start time to server mutations");
            AssertFalse(cookingSource.Contains("ApplyNoJobServerReconcile"), "CookingPanelAdapter should not clear UI from no_job strings");
            AssertFalse(cookingSource.Contains("ClearDisplayedCookingSlot"), "CookingPanelAdapter should rely on authoritative cookingData snapshots");
            AssertFalse(cookingSource.Contains("TryRecoverNoJobServerState"), "CookingPanelAdapter should not recover state from no_job strings");
            AssertFalse(cookingSource.Contains("TryRecoverSlotOccupiedServerState"), "CookingPanelAdapter should not recover state from slot_occupied strings");
            AssertFalse(bridgeSource.Contains("IsCookingSlotOccupiedMessage"), "FisherPlayerDataBridge should not recover cooking state from slot_occupied strings");
        }

        private static void RunRawCookingMutationApplyContract(Type bridgeType)
        {
            Type responseType = bridgeType.GetNestedType("CloudScriptMutationResponse", BindingFlags.NonPublic);
            AssertTrue(responseType != null, "CloudScriptMutationResponse should exist for raw mutation apply contract");
            FieldInfo rawJson = responseType.GetField("rawJson", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            AssertTrue(rawJson != null, "CloudScriptMutationResponse.rawJson should exist for raw mutation apply contract");
            MethodInfo applyMutation = bridgeType.GetMethod(
                "ApplyCloudScriptMutationResponse",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), responseType, typeof(bool), typeof(bool), typeof(bool).MakeByRefType() },
                null);
            AssertTrue(applyMutation != null, "ApplyCloudScriptMutationResponse should exist for raw mutation apply contract");

            GameObject host = new GameObject("FisherDataContractSelfTest_RawCookingApply");
            try
            {
                FisherRuntimeContext context = host.AddComponent<FisherRuntimeContext>();
                context.Initialize();
                AssertTrue(context.IsReady, "runtime context should initialize for raw cooking apply contract: " + context.LastStatus);

                FisherPlayerDataBridge bridge = host.AddComponent<FisherPlayerDataBridge>();
                bridge.Configure(context);

                string startJson = "{\"success\":true,\"requestId\":\"selftest-start\",\"slotIndex\":0,\"recipeId\":\"recipe_grilled_anchovy_g1\",\"totalCount\":1,\"durationSec\":60,\"slot\":{\"isOpened\":true,\"job\":{\"recipeId\":\"recipe_grilled_anchovy_g1\",\"totalCount\":1,\"claimedCount\":0,\"durationSec\":60,\"startedAtUtc\":\"2026-06-23T06:56:19.223Z\"}},\"cookingData\":{\"cookSlots\":{\"0\":{\"isOpened\":true,\"job\":{\"recipeId\":\"recipe_grilled_anchovy_g1\",\"totalCount\":1,\"claimedCount\":0,\"durationSec\":60,\"startedAtUtc\":\"2026-06-23T06:56:19.223Z\"}},\"1\":{\"isOpened\":false,\"job\":null},\"2\":{\"isOpened\":false,\"job\":null}}}}";
                AssertTrue(InvokeRawMutationApply(bridge, applyMutation, responseType, rawJson, "StartCooking", startJson, out bool startCookingApplied), "raw StartCooking cookingData should apply");
                AssertTrue(startCookingApplied, "raw StartCooking should report cookingApplied");
                AssertEqual(1, context.CookingService.ActiveCookingSlotCount, "raw StartCooking cookingData should create active slot");
                ActiveRecipeState active = context.CookingService.ActiveRecipeStates[0];
                long expectedTicks = new DateTime(2026, 6, 23, 6, 56, 19, DateTimeKind.Utc).Ticks +
                                     TimeSpan.FromMilliseconds(223).Ticks;
                AssertEqual("recipe_grilled_anchovy_g1", active.recipeId, "raw StartCooking active recipe id");
                AssertEqual(expectedTicks, active.startedUtcTicks, "raw StartCooking preserves fractional startedAtUtc");
                AssertEqual(expectedTicks + TimeSpan.FromSeconds(60).Ticks, active.completesUtcTicks, "raw StartCooking completion uses server start");

                string invalidStartJson = "{\"success\":true,\"requestId\":\"selftest-invalid\",\"slotIndex\":0,\"cookingData\":{\"cookSlots\":{\"0\":{\"isOpened\":true,\"job\":{\"recipeId\":\"recipe_grilled_anchovy_g1\",\"totalCount\":1,\"claimedCount\":0,\"durationSec\":60,\"startedAtUtc\":\"not-a-date\"}},\"1\":{\"isOpened\":false,\"job\":null},\"2\":{\"isOpened\":false,\"job\":null}}}}";
                AssertFalse(InvokeRawMutationApply(bridge, applyMutation, responseType, rawJson, "StartCooking", invalidStartJson, out bool invalidCookingApplied), "invalid raw cooking job should fail apply");
                AssertFalse(invalidCookingApplied, "invalid raw cooking job should not report cookingApplied");
                AssertEqual(1, context.CookingService.ActiveCookingSlotCount, "invalid raw cooking job should preserve previous active slot");
                AssertEqual(expectedTicks, context.CookingService.ActiveRecipeStates[0].startedUtcTicks, "invalid raw cooking job should not overwrite active start");

                string cancelJson = "{\"success\":true,\"requestId\":\"selftest-cancel\",\"slotIndex\":0,\"slot\":{\"isOpened\":true,\"job\":null},\"cookingData\":{\"cookSlots\":{\"0\":{\"isOpened\":true,\"job\":null},\"1\":{\"isOpened\":false,\"job\":null},\"2\":{\"isOpened\":false,\"job\":null}}}}";
                AssertTrue(InvokeRawMutationApply(bridge, applyMutation, responseType, rawJson, "CancelCooking", cancelJson, out bool cancelCookingApplied), "raw CancelCooking cookingData should apply");
                AssertTrue(cancelCookingApplied, "raw CancelCooking should report cookingApplied");
                AssertEqual(0, context.CookingService.ActiveCookingSlotCount, "raw CancelCooking cookingData should clear active slot");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static bool InvokeRawMutationApply(
            FisherPlayerDataBridge bridge,
            MethodInfo applyMutation,
            Type responseType,
            FieldInfo rawJson,
            string operation,
            string json,
            out bool cookingApplied)
        {
            object response = Activator.CreateInstance(responseType, nonPublic: true);
            rawJson.SetValue(response, json);
            object[] args = { operation, response, true, false, false };
            bool changed = (bool)applyMutation.Invoke(bridge, args);
            cookingApplied = (bool)args[4];
            return changed;
        }

        private static void RunCshRuntimeStateBridgeContractTests()
        {
            Type bridgeType = typeof(FisherPlayerDataBridge);
            MethodInfo handleRuntimeState = bridgeType.GetMethod("HandleCshRuntimeStateReceived", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo applyRuntimeState = bridgeType.GetMethod("ApplyCloudScriptRuntimeStateResponse", BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(handleRuntimeState != null, "HandleCshRuntimeStateReceived should exist for PlayFabGateway.cshRuntimeState event");
            AssertTrue(applyRuntimeState != null, "ApplyCloudScriptRuntimeStateResponse should exist for cshRuntimeState payloads");

            string bridgeSource = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/Integration/FisherPlayerDataBridge.cs");
            AssertTrue(bridgeSource.Contains("TryBindCshRuntimeStateGateway();"), "bridge should retry cshRuntimeState gateway binding from lifecycle/update");
            AssertTrue(bridgeSource.Contains("CshRuntimeStateReceived += HandleCshRuntimeStateReceived"), "bridge should subscribe to PlayFabGateway.CshRuntimeStateReceived");
            AssertTrue(bridgeSource.Contains("CshRuntimeStateReceived -= HandleCshRuntimeStateReceived"), "bridge should unsubscribe from PlayFabGateway.CshRuntimeStateReceived");
            AssertTrue(bridgeSource.Contains("LastCshRuntimeState"), "bridge should apply PlayFabGateway.LastCshRuntimeState when it binds late");

            GameObject host = new GameObject("FisherDataContractSelfTest_CshRuntimeStateBridge");
            try
            {
                FisherRuntimeContext context = host.AddComponent<FisherRuntimeContext>();
                context.Initialize();
                AssertTrue(context.IsReady, "runtime context should initialize for cshRuntimeState bridge contract: " + context.LastStatus);

                FisherPlayerDataBridge bridge = host.AddComponent<FisherPlayerDataBridge>();
                bridge.Configure(context);

                int changedEventCount = 0;
                context.RuntimeChanged += () => changedEventCount++;

                JObject eventPayload = JObject.Parse(
                    "{\"cash\":777,\"bagCapacity\":32,\"bagCapacityLevel\":4,\"cookingSlotLimit\":3,\"cookingSlotLevel\":2,\"currentStage\":8,\"farthestStage\":12,\"itemAcquisitionCounts\":{\"fish_anchovy\":5},\"discoveredCollectionItemIds\":[\"fish_anchovy\"],\"claimedCollectionRewards\":[\"collection_reward_anchovy\"]}");
                handleRuntimeState.Invoke(bridge, new object[] { eventPayload });

                AssertEqual(777L, context.State.cash, "event cshRuntimeState cash");
                AssertEqual(32, context.State.bagCapacity, "event cshRuntimeState bagCapacity");
                AssertEqual(4, context.State.bagCapacityLevel, "event cshRuntimeState bagCapacityLevel");
                AssertEqual(3, context.State.cookingSlotLimit, "event cshRuntimeState cookingSlotLimit");
                AssertEqual(2, context.State.cookingSlotLevel, "event cshRuntimeState cookingSlotLevel");
                AssertEqual(8, context.State.currentStage, "event cshRuntimeState currentStage");
                AssertEqual(12, context.State.farthestStage, "event cshRuntimeState farthestStage");
                AssertEqual(5, context.State.itemAcquisitionCounts["fish_anchovy"], "event cshRuntimeState itemAcquisitionCounts");
                AssertTrue(context.State.discoveredCollectionItemIds.Contains("fish_anchovy"), "event cshRuntimeState discoveredCollectionItemIds");
                AssertTrue(context.State.claimedRewardIds.Contains("collection_reward_anchovy"), "event cshRuntimeState claimedCollectionRewards");
                AssertTrue(changedEventCount > 0, "event cshRuntimeState should notify RuntimeChanged when state changes");

                JObject nestedPayload = JObject.Parse("{\"data\":{\"cshRuntimeState\":{\"bagCapacity\":48,\"claimedCollectionRewards\":[\"collection_reward_nested\"]}}}");
                bool nestedChanged = (bool)applyRuntimeState.Invoke(bridge, new object[] { nestedPayload });
                AssertTrue(nestedChanged, "nested data.cshRuntimeState should apply");
                AssertEqual(48, context.State.bagCapacity, "nested cshRuntimeState bagCapacity");
                AssertTrue(context.State.claimedRewardIds.Contains("collection_reward_nested"), "nested cshRuntimeState claimedCollectionRewards");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static JObject ParseRawMutationJsonForTest(string json)
        {
            using (StringReader stringReader = new StringReader(json))
            using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
            {
                jsonReader.DateParseHandling = DateParseHandling.None;
                return JObject.Load(jsonReader);
            }
        }

        private static void RunRuntimeUiNoGenerationContractTests(TestReport report)
        {
            string[] adapterFiles =
            {
                "BagPanelAdapter.cs",
                "CookingPanelAdapter.cs",
                "ShopPanelAdapter.cs",
                "CollectionPanelAdapter.cs"
            };

            for (int i = 0; i < adapterFiles.Length; i++)
            {
                string source = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/UI/" + adapterFiles[i]);
                AssertFalse(source.Contains("FisherStaticViewFactory.Ensure"), adapterFiles[i] + " must not call runtime static view factory Ensure*View");
                AssertFalse(source.Contains(".EnsureSlot("), adapterFiles[i] + " must not ask FisherPanelView to create/repair slots");
                AssertFalse(source.Contains("FisherRuntimeUi.CreateButton("), adapterFiles[i] + " must not create runtime buttons in gameplay render paths");
                AssertFalse(source.Contains("FisherRuntimeUi.CreateItemSlotButton("), adapterFiles[i] + " must not create runtime item slots in gameplay render paths");
                AssertFalse(source.Contains("new GameObject("), adapterFiles[i] + " must not create runtime UI GameObjects in gameplay render paths");
                AssertTrue(source.Contains("FisherPanelViewResolver.TryResolveExistingView"), adapterFiles[i] + " should use read-only ViewRoot resolver");
            }

            string bagSource = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/UI/BagPanelAdapter.cs");
            string cookingSource = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/UI/CookingPanelAdapter.cs");
            string shopSource = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/UI/ShopPanelAdapter.cs");
            string collectionSource = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/UI/CollectionPanelAdapter.cs");
            AssertTrue(
                bagSource.Contains("SetMainSectionsVisible(tabs: true, grid: true, detail: true, actions: true)"),
                "Bag normal render should reactivate sections hidden by status render");
            AssertTrue(
                cookingSource.Contains("SetMainSectionsVisible(tabs: false, grid: true, detail: true, actions: true)"),
                "Cooking normal render should reactivate sections hidden by status render");
            AssertTrue(
                shopSource.Contains("SetMainSectionsVisible(tabs: true, grid: true, detail: false, actions: false)"),
                "Shop normal render should reactivate tabs/grid hidden by status render");
            AssertTrue(
                collectionSource.Contains("SetMainSectionsVisible(tabs: true, grid: true, detail: false, actions: false)"),
                "Collection normal render should reactivate tabs/grid hidden by status render");

            string panelView = ReadProjectText("Assets/01_Scripts/05_CSH/Runtime/UI/FisherPanelView.cs");
            AssertTrue(
                panelView.Contains("SetMainSectionsVisible"),
                "FisherPanelView should centralize static section visibility restore");
            int ensureSlotIndex = panelView.IndexOf("public FisherSlotView EnsureSlot", StringComparison.Ordinal);
            AssertTrue(ensureSlotIndex >= 0, "FisherPanelView.EnsureSlot compatibility method exists");
            string ensureSlotBody = panelView.Substring(ensureSlotIndex);
            AssertFalse(ensureSlotBody.Contains("CreateSlot("), "FisherPanelView.EnsureSlot must not create slots at runtime");
            AssertFalse(ensureSlotBody.Contains("RepairSlot("), "FisherPanelView.EnsureSlot must not repair slots at runtime");
            AssertTrue(ensureSlotBody.Contains("GetExistingSlot"), "FisherPanelView.EnsureSlot should only delegate to existing slot lookup");
        }

        private static void RunFishingAdapterContractTests(TestReport report)
        {
            GameObject contextObject = new GameObject("FisherRuntimeContext_SelfTest");
            GameObject adapterObject = new GameObject("FisherFishingCatchAdapter_SelfTest");
            try
            {
                FisherRuntimeContext context = contextObject.AddComponent<FisherRuntimeContext>();
                if (!context.IsReady)
                {
                    context.Initialize();
                }

                AssertTrue(context.IsReady, "runtime context should be ready for fishing adapter contract");
                BalanceCatalog catalog = context.BuildResult.Catalog;
                string itemId = FirstEnabledGeneratedFishItemId(catalog);
                AssertTrue(!string.IsNullOrEmpty(itemId), "generated fish itemId should exist for fishing adapter contract");
                RecipeDefinition recipe = FirstEnabledRecipeUsingItem(catalog, itemId);
                AssertTrue(recipe != null, "generated fish itemId should feed at least one enabled recipe");

                FisherFishingCatchAdapter adapter = adapterObject.AddComponent<FisherFishingCatchAdapter>();
                adapter.Configure(context);
                AssertFailKey(adapter.ApplyCaughtFishId(string.Empty, 1), "fishing.empty_fish_id", "empty fishing id should fail");
                AssertFailKey(adapter.ApplyCaughtFishId(itemId, 0), "fishing.invalid_count", "zero fishing count should fail");

                int before = context.InventoryService.CountItem(itemId);
                int requiredForRecipe = CountRecipeInputRequirement(recipe, itemId);
                int caughtCount = requiredForRecipe + 1;
                ServiceResult result = adapter.ApplyCaughtFishId(itemId, caughtCount);
                AssertSuccess(result, "generated fishing catch applies to inventory");
                AssertEqual(before + caughtCount, context.InventoryService.CountItem(itemId), "fishing catch should add item count");
                AssertTrue(context.State.discoveredCollectionItemIds.Contains(itemId), "fishing catch should auto register item discovery");

                long goldBeforeSale = context.State.softCurrency;
                ServiceResult sell = context.InventoryService.TrySellItem(itemId, 1);
                AssertSuccess(sell, "caught fish can be sold through inventory service");
                AssertTrue(context.State.softCurrency > goldBeforeSale, "caught fish sale should add gold");

                EnsureRecipeInputs(context, recipe, itemId);
                int beforeCookingInput = context.InventoryService.CountItem(itemId);
                ServiceResult queue = context.CookingService.TryQueueRecipe(recipe.RecipeId, 1);
                AssertSuccess(queue, "caught fish can feed cooking queue");
                AssertEqual(beforeCookingInput - requiredForRecipe, context.InventoryService.CountItem(itemId), "cooking queue should consume caught fish input");
                AssertTrue(context.CookingService.ActiveCookingSlotCount > 0, "cooking queue should create active recipe slot");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(adapterObject);
                UnityEngine.Object.DestroyImmediate(contextObject);
            }
        }

        private static void RunRmsReferenceSeamTests(TestReport report)
        {
            BalanceBuildResult build = BalanceCatalogBuilder.Build(new BalanceCsvSet
            {
                ItemsCsv = ReadGenerated("items.csv"),
                FishCsv = ReadGenerated("fish.csv"),
                ShopItemsCsv = ReadGenerated("shop_items.csv"),
                PremiumCurrencyProductsCsv = ReadGenerated("premium_currency_products.csv", optional: true),
                RecipesCsv = ReadGenerated("recipes.csv"),
                CollectionRewardsCsv = ReadGenerated("collection_rewards.csv"),
                EconomyParamsCsv = ReadGenerated("economy_params.csv"),
                StackRulesCsv = ReadGenerated("stack_rules.csv")
            });
            AssertTrue(build.Success, "catalog build should succeed before RMS seam check. errors=" + string.Join(" | ", build.Errors));

            List<RMS.Data.FishData> fishAssets = LoadAssets<RMS.Data.FishData>("Assets/03_Data/01_RMS");
            List<RMS.Data.BossData> bossAssets = LoadAssets<RMS.Data.BossData>("Assets/03_Data/01_RMS");
            Dictionary<string, RMS.Data.FishData> fishById = new Dictionary<string, RMS.Data.FishData>(StringComparer.Ordinal);

            int mappedFishCount = 0;
            int blankFishCount = 0;
            for (int i = 0; i < fishAssets.Count; i++)
            {
                RMS.Data.FishData fish = fishAssets[i];
                if (fish == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(fish);
                string fishId = NormalizeId(fish.FishId);
                if (string.IsNullOrEmpty(fishId))
                {
                    blankFishCount++;
                    report.Warn(path + " has empty FishId. Treating it as a sample/placeholder asset.");
                    continue;
                }

                if (fishById.ContainsKey(fishId))
                {
                    report.Warn(path + " duplicates RMS FishId: " + fishId);
                    continue;
                }

                fishById.Add(fishId, fish);
                if (!build.Catalog.TryGetItem(fishId, out ItemDefinition item))
                {
                    report.Warn(path + " FishId has no matching generated itemId: " + fishId);
                    continue;
                }

                mappedFishCount++;
                if (item.Category != "Fish")
                {
                    report.Warn(path + " FishId maps to non-Fish item category: " + fishId + " / " + item.Category);
                }

                if (item.SellPrice != fish.SellPriceGold)
                {
                    report.Warn(path + " SellPriceGold differs from items.csv sellPrice: " + fishId + " / RMS=" + fish.SellPriceGold + " CSV=" + item.SellPrice);
                }

                if (!fish.UsableAsIngredient && HasEnabledRecipeInput(build.Catalog, fishId))
                {
                    report.Warn(path + " is not usable as ingredient but recipes.csv uses it: " + fishId);
                }

                if (!string.IsNullOrWhiteSpace(fish.CodexRewardGroupId) &&
                    !HasCollectionRewardForItemOrGroup(build.Catalog, fishId, fish.CodexRewardGroupId))
                {
                    report.Warn(path + " has CodexRewardGroupId but no generated codex reward references itemId or rewardGroupId: " + fishId + " / " + fish.CodexRewardGroupId);
                }
            }

            if (fishAssets.Count == 0)
            {
                report.Warn("No RMS FishData assets found. Generated CSV can still run, but fishing/codex integration is not grounded yet.");
            }

            int bossLinkedCount = 0;
            int blankBossCount = 0;
            for (int i = 0; i < bossAssets.Count; i++)
            {
                RMS.Data.BossData boss = bossAssets[i];
                if (boss == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(boss);
                string bossId = NormalizeId(boss.BossId);
                if (string.IsNullOrEmpty(bossId))
                {
                    blankBossCount++;
                    report.Warn(path + " has empty BossId. Treating it as a sample/placeholder asset.");
                    continue;
                }

                if (boss.MaxHp <= 0)
                {
                    report.Warn(path + " has BossId but MaxHp <= 0: " + bossId);
                }

                string linkedFishId = NormalizeId(boss.LinkedFishId);
                if (string.IsNullOrEmpty(linkedFishId))
                {
                    continue;
                }

                bossLinkedCount++;
                if (!fishById.ContainsKey(linkedFishId) && !build.Catalog.TryGetItem(linkedFishId, out _))
                {
                    report.Warn(path + " LinkedFishId is missing from RMS FishData and generated items.csv: " + linkedFishId);
                }
            }

            if (bossAssets.Count == 0)
            {
                report.Warn("No RMS BossData assets found. Boss reward/codex links are not grounded yet.");
            }

            if (fishById.Count > 0)
            {
                ValidateGeneratedFishReferencesRms(build.Catalog, fishById, report);
            }

            report.Add("RMS FishData assets: " + fishAssets.Count + " / mapped itemIds: " + mappedFishCount + " / empty samples: " + blankFishCount);
            report.Add("RMS BossData assets: " + bossAssets.Count + " / linked fish refs: " + bossLinkedCount + " / empty samples: " + blankBossCount);
        }

        private static void RunServerIdMappingContractTests(TestReport report)
        {
            BalanceBuildResult build = BalanceCatalogBuilder.Build(new BalanceCsvSet
            {
                ItemsCsv = ReadGenerated("items.csv"),
                FishCsv = ReadGenerated("fish.csv"),
                ShopItemsCsv = ReadGenerated("shop_items.csv"),
                PremiumCurrencyProductsCsv = ReadGenerated("premium_currency_products.csv", optional: true),
                RecipesCsv = ReadGenerated("recipes.csv"),
                CollectionRewardsCsv = ReadGenerated("collection_rewards.csv"),
                EconomyParamsCsv = ReadGenerated("economy_params.csv"),
                StackRulesCsv = ReadGenerated("stack_rules.csv")
            });
            AssertTrue(build.Success, "catalog build should succeed before server id mapping check. errors=" + string.Join(" | ", build.Errors));

            BalanceCsvTable inventoryMap = ReadGeneratedContractTable(
                "ywj_inventory_item_map",
                "ywj_inventory_item_map.csv",
                "legacyIntId",
                "itemId",
                "serverId",
                "itemCategory",
                "owner",
                "isRuntimeLinked",
                "isEnabled");
            ValidateYwjInventoryItemMap(inventoryMap, build.Catalog);

            BalanceCsvTable stageMap = ReadGeneratedContractTable(
                "rms_stage_id_map",
                "rms_stage_id_map.csv",
                "stageOrder",
                "stageId",
                "worldId",
                "isBossStage",
                "bossId",
                "nextStageId",
                "isRuntimeLinked",
                "isEnabled");
            ValidateRmsStageIdMap(stageMap, report);

            report.Add("YWJ inventory id map rows: " + inventoryMap.Rows.Count);
            report.Add("RMS stageId map rows: " + stageMap.Rows.Count);
        }

        private static void RunCatalogValidationTests(TestReport report)
        {
            BalanceBuildResult duplicate = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                itemsCsv: string.Join("\n",
                    ItemHeader(),
                    "fish_test,테스트물고기,Fish,Common,Fishing,10,TRUE,2,fish,1,TRUE,OK",
                    "fish_test,중복물고기,Fish,Common,Fishing,10,TRUE,2,fish,2,TRUE,Duplicate")));
            AssertFalse(duplicate.Success, "duplicate item catalog should fail");
            AssertContains(duplicate.Errors, "duplicate itemId", "duplicate item error");

            BalanceBuildResult missingRecipeOutput = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                recipesCsv: string.Join("\n",
                    RecipeHeader(),
                    "recipe_missing_output,fish_test,1,fish_test2,1,30,missing_food,1,1,stage>=1,TRUE,Missing output")));
            AssertFalse(missingRecipeOutput.Success, "missing recipe output catalog should fail");
            AssertContains(missingRecipeOutput.Errors, "missing outputItemId", "missing recipe output error");

            BalanceBuildResult invalidCount = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                recipesCsv: string.Join("\n",
                    RecipeHeader(),
                    "recipe_invalid_count,fish_test,0,fish_test2,1,30,food_test,1,1,stage>=1,TRUE,Invalid input count")));
            AssertFalse(invalidCount.Success, "invalid recipe count catalog should fail");
            AssertContains(invalidCount.Errors, "recipe counts must be > 0", "invalid recipe count error");

            BalanceBuildResult duplicateHeader = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                itemsCsv: string.Join("\n",
                    "itemId,itemId,displayNameKo,category,rarity,sourceType,sellPrice,stackable,maxStack,cookTag,sortOrder,isEnabled,notes",
                    "fish_test,ignored,테스트물고기,Fish,Common,Fishing,10,TRUE,2,fish,1,TRUE,Duplicate header")));
            AssertFalse(duplicateHeader.Success, "duplicate header catalog should fail");
            AssertContains(duplicateHeader.Errors, "duplicate header", "duplicate header error");

            BalanceBuildResult unsupportedPrice = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                shopCsv: string.Join("\n",
                    ShopHeader(),
                    "shop_bad,Fish,gems,1,fish_test,1,stage>=1,1,TRUE,Bad currency")));
            AssertFalse(unsupportedPrice.Success, "unsupported shop price catalog should fail");
            AssertContains(unsupportedPrice.Errors, "unsupported priceType", "unsupported shop price type error");

            BalanceBuildResult unsupportedRewardCurrency = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                collectionRewardsCsv: string.Join("\n",
                    CollectionRewardHeader(),
                    "reward_bad,fish_test,discovery,1,gems,1,,0,claim_bad,1,TRUE,Bad reward currency")));
            AssertFalse(unsupportedRewardCurrency.Success, "unsupported collection currency catalog should fail");
            AssertContains(unsupportedRewardCurrency.Errors, "unsupported rewardCurrency", "unsupported reward currency error");

            BalanceBuildResult missingStackRule = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                itemsCsv: DefaultItemsCsv() + "\n" +
                          "relic_missing_rule,룰없는유물,Relic,Common,Codex,0,TRUE,99,,20,TRUE,Missing stack rule"));
            AssertTrue(missingStackRule.Success, "missing stack rule should warn, not fail");
            AssertContains(missingStackRule.Warnings, "no enabled stack rule", "missing stack rule warning");

            BalanceBuildResult invalidFishGrade = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                fishCsv: string.Join("\n",
                    FishHeader(),
                    "fish_bad_grade,fish_test,4,5,8,100,3,shore_common,FALSE,TRUE,Bad grade")));
            AssertFalse(invalidFishGrade.Success, "invalid fish grade catalog should fail");
            AssertContains(invalidFishGrade.Errors, "fish grade must be between 1 and 3", "invalid fish grade error");

            BalanceBuildResult missingFishGrade = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                fishCsv: string.Join("\n",
                    FishHeader(),
                    "fish_test_g1,fish_test,1,5,8,100,3,shore_common,FALSE,TRUE,Only grade 1")));
            AssertFalse(missingFishGrade.Success, "missing fish grade catalog should fail");
            AssertContains(missingFishGrade.Errors, "missing grade 2", "missing fish grade error");

            BalanceBuildResult duplicateFishRecipeInput = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                recipesCsv: string.Join("\n",
                    RecipeHeader(),
                    "recipe_duplicate_fish,fish_test,1,fish_test,1,30,food_test,1,1,stage>=1,TRUE,Duplicate fish input")));
            AssertFalse(duplicateFishRecipeInput.Success, "duplicate same-fish recipe input should fail");
            AssertContains(duplicateFishRecipeInput.Errors, "two different fish", "duplicate same-fish recipe error");
        }

        private static void RunNumberDisplayContractTests(TestReport report)
        {
            AssertEqual("999", CompactNumberFormatter.Format(999), "compact number below K");
            AssertEqual("1.0K", CompactNumberFormatter.Format(1000), "compact number K");
            AssertEqual("1.5K", CompactNumberFormatter.Format(1500), "compact number K decimal");
            AssertEqual("1.0M", CompactNumberFormatter.Format(1000000), "compact number M");
            AssertEqual("1.0B", CompactNumberFormatter.Format(1000000000), "compact number B");

            BalanceBuildResult overLimit = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                itemsCsv: string.Join("\n",
                    ItemHeader(),
                    "fish_test,테스트물고기,Fish,Common,Fishing,2147483648,TRUE,2,fish,1,TRUE,Over limit",
                    "fish_test2,테스트물고기2,Fish,Common,Fishing,8,TRUE,2,fish,2,TRUE,OK",
                    "food_test,테스트요리,Food,Uncommon,Cooking,50,TRUE,99,meal,2,TRUE,OK",
                    "disabled_item,비활성아이템,Material,Common,Shop,10,TRUE,99,,3,FALSE,Disabled"),
                shopCsv: string.Join("\n",
                    ShopHeader(),
                    "shop_over,Fish,softCurrency,2147483648,fish_test,1,stage>=1,1,TRUE,Over limit"),
                collectionRewardsCsv: string.Join("\n",
                    CollectionRewardHeader(),
                    "reward_over,fish_test,discovery,1,softCurrency,2147483648,,0,claim_over,1,TRUE,Over limit")));
            AssertTrue(overLimit.Success, "int balance limit should warn, not fail");
            AssertContains(overLimit.Warnings, "sellPrice exceeds int balance limit", "sellPrice int limit warning");
            AssertContains(overLimit.Warnings, "priceAmount exceeds int balance limit", "priceAmount int limit warning");
            AssertContains(overLimit.Warnings, "rewardAmount exceeds int balance limit", "rewardAmount int limit warning");
        }

        private static void RunInventoryContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog();
            PlayerRuntimeState state = new PlayerRuntimeState
            {
                softCurrency = 100
            };
            InventoryService inventory = new InventoryService(catalog, state);
            BagQueryService bagQuery = new BagQueryService(catalog, state);

            AssertFailKey(inventory.TryAddItem("missing_item", 1), "inventory.unknown_item", "unknown add item");
            AssertFailKey(inventory.TryAddItem("fish_test", 0), "inventory.invalid_count", "zero add count");
            AssertFailKey(inventory.TryAddItem("disabled_item", 1), "inventory.item_disabled", "disabled add item");
            AssertFailKey(inventory.TryAddItem("unique_test", 1), "inventory.instance_required", "unique add without instance");

            AssertSuccess(inventory.TryAddItem("fish_test", 5), "stack split add");
            AssertEqual(3, inventory.GetInventorySnapshot().Count, "stack split rows");
            AssertEqual(5, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "stack split bag count");
            AssertTrue(state.discoveredCollectionItemIds.Contains("fish_test"), "item add should auto register discovery");
            AssertTrue(state.newItemNoticeIds.Contains("fish_test"), "item add should mark one-shot new notice");

            PlayerRuntimeState returnState = new PlayerRuntimeState();
            InventoryService returnInventory = new InventoryService(catalog, returnState);
            BagQueryService returnBagQuery = new BagQueryService(catalog, returnState);
            AssertSuccess(returnInventory.TryReturnConsumedItem("fish_test2", 1), "return consumed stack item");
            AssertEqual(1, CountBagItem(returnBagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test2"), "returned consumed item count");
            AssertEqual(0, returnInventory.GetAcquiredCount("fish_test2"), "returned consumed item should not record acquisition");
            AssertTrue(!returnState.discoveredCollectionItemIds.Contains("fish_test2"), "returned consumed item should not auto register discovery");
            AssertTrue(!returnState.newItemNoticeIds.Contains("fish_test2"), "returned consumed item should not mark new notice");

            AssertSuccess(inventory.TryAddItem("unique_test", 1, "unique-001", 2), "unique add with instance");
            AssertFailKey(inventory.TryAddItem("unique_test", 1, "unique-001", 2), "inventory.duplicate_instance", "duplicate unique instance should fail");
            AssertFailKey(inventory.TryAddItem("fish_test", 1, "fish-stack-001", 0), "inventory.stack_identity_not_supported", "stack item should not lose runtime identity");
            AssertEqual(5, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "stack identity failure should not add item");
            AssertFailKey(inventory.TryConsumeItem("unique_test", 1), "inventory.instance_required", "unique consume should require instance op");
            AssertSuccess(inventory.SetItemLock("fish_test", true), "lock sellable fish");
            AssertFailKey(inventory.TrySellItem("fish_test", 1), "inventory.item_locked", "locked item should not sell");
            AssertEqual(5, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "locked item sell failure should keep count");
            AssertSuccess(inventory.SetItemLock("fish_test", false), "unlock sellable fish");

            state.inventoryEntries.Add(new InventoryEntry
            {
                itemId = "disabled_item",
                count = 1,
                instanceId = string.Empty,
                levelIndex = -1
            });
            AssertFailKey(inventory.TrySellItem("disabled_item", 1), "inventory.item_disabled", "disabled sell item");
            AssertEqual(100L, state.softCurrency, "currency unchanged after disabled sell");

            AssertSuccess(inventory.TryAddItem("mat_high_test", 1), "high grade material add");
            AssertFailKey(inventory.TrySellItem("mat_high_test", 1), "inventory.not_sellable", "high grade material should not sell even with positive price");
            AssertEqual(1, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "mat_high_test"), "not-sellable high grade material count remains");
            AssertEqual(100L, state.softCurrency, "currency unchanged after high grade material sell block");

            AssertSuccess(
                inventory.ReplaceStackedInventorySnapshot(new Dictionary<string, int>
                {
                    { "fish_test", 3 },
                    { "food_test", 1 }
                }),
                "server stack snapshot replace");
            AssertEqual(3, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "server snapshot fish count");
            AssertEqual(1, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "food_test"), "server snapshot food count");
            AssertTrue(state.discoveredCollectionItemIds.Contains("fish_test"), "server snapshot fish discovery");
        }

        private static void RunBagCapacityContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog();

            PlayerRuntimeState nearFullState = new PlayerRuntimeState
            {
                bagCapacity = 1
            };
            InventoryService nearFullInventory = new InventoryService(catalog, nearFullState);
            BagQueryService nearFullBagQuery = new BagQueryService(catalog, nearFullState);

            AssertSuccess(nearFullInventory.TryAddItem("fish_test", 1), "capacity first stack row");
            AssertEqual(1, nearFullInventory.OccupiedBagRows, "capacity occupied after first add");
            AssertSuccess(nearFullInventory.TryAddItem("fish_test", 1), "capacity existing stack add");
            AssertEqual(1, nearFullInventory.OccupiedBagRows, "existing stack should not add occupied row");
            AssertEqual(2, CountBagItem(nearFullBagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "existing stack count near capacity");
            AssertFailKey(nearFullInventory.TryAddItem("fish_test", 1), "inventory.bag_capacity_full", "capacity full blocks new stack row");
            AssertEqual(1, nearFullInventory.OccupiedBagRows, "capacity failure should not add row");
            AssertEqual(2, CountBagItem(nearFullBagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "capacity failure should keep item count");
            AssertEqual(2, nearFullInventory.GetAcquiredCount("fish_test"), "capacity failure should not record acquisition");

            PlayerRuntimeState multiRowState = new PlayerRuntimeState
            {
                bagCapacity = 2
            };
            InventoryService multiRowInventory = new InventoryService(catalog, multiRowState);
            BagQueryService multiRowBagQuery = new BagQueryService(catalog, multiRowState);
            AssertSuccess(multiRowInventory.TryAddItem("fish_test", 3), "same item can overflow into capacity rows");
            AssertEqual(2, multiRowInventory.OccupiedBagRows, "same item duplicate stack rows occupy capacity");
            AssertEqual(3, CountBagItem(multiRowBagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "duplicate stack rows aggregate in bag query");
            AssertFailKey(multiRowInventory.TryAddItem("unique_test", 1, "unique-capacity-001", 1), "inventory.bag_capacity_full", "capacity full blocks unique row");
            AssertEqual(0, CountBagItem(multiRowBagQuery.BuildSnapshot(new BagQueryOptions()), "unique_test"), "capacity full unique item not added");

            PlayerRuntimeState expandState = new PlayerRuntimeState
            {
                softCurrency = 1000,
                bagCapacity = 1
            };
            InventoryService expandInventory = new InventoryService(catalog, expandState);
            ServiceResult expand = expandInventory.TryPurchaseBagCapacityExpansion();
            AssertSuccess(expand, "bag capacity expansion purchase");
            AssertMessageKey(expand, "inventory.bag_capacity_expand_success", "bag capacity expansion message key");
            AssertEqual(900L, expandState.softCurrency, "bag capacity expansion cost");
            AssertEqual(-100L, expand.CurrencyDelta, "bag capacity expansion currency delta");
            AssertAffected(expand, "bag_capacity", "bag capacity expansion affected id");
            AssertEqual(3, expandState.bagCapacity, "bag capacity expanded by step");
            AssertEqual(1, expandState.bagCapacityLevel, "bag capacity level after first expansion");
            AssertSuccess(expandInventory.TryAddItem("fish_test", 5), "expanded capacity accepts three stack rows");
            AssertEqual(3, expandInventory.OccupiedBagRows, "expanded capacity occupied rows");

            ServiceResult expandSecond = expandInventory.TryPurchaseBagCapacityExpansion();
            AssertSuccess(expandSecond, "bag capacity second expansion");
            AssertEqual(5, expandState.bagCapacity, "bag capacity clamps to max");
            AssertEqual(2, expandState.bagCapacityLevel, "bag capacity level after second expansion");
            long goldAtMax = expandState.softCurrency;
            AssertFailKey(expandInventory.TryPurchaseBagCapacityExpansion(), "inventory.bag_capacity_max", "bag capacity max blocks further purchase");
            AssertEqual(goldAtMax, expandState.softCurrency, "bag capacity max should not spend currency");

            PlayerRuntimeState poorState = new PlayerRuntimeState
            {
                softCurrency = 99,
                bagCapacity = 1
            };
            InventoryService poorInventory = new InventoryService(catalog, poorState);
            AssertFailKey(poorInventory.TryPurchaseBagCapacityExpansion(), "inventory.not_enough_currency", "bag capacity purchase needs gold");
            AssertEqual(99L, poorState.softCurrency, "failed bag capacity purchase keeps gold");
            AssertEqual(1, poorState.bagCapacity, "failed bag capacity purchase keeps capacity");
        }

        private static void RunItemCountOverflowContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog();

            PlayerRuntimeState stackState = new PlayerRuntimeState();
            stackState.itemAcquisitionCounts["fish_test"] = int.MaxValue;
            InventoryService stackInventory = new InventoryService(catalog, stackState);
            AssertFailKey(stackInventory.TryAddItem("fish_test", 1), "inventory.acquisition_overflow", "stack acquisition overflow");
            AssertEqual(0, stackInventory.GetInventorySnapshot().Count, "stack acquisition overflow should not add inventory");
            AssertEqual(int.MaxValue, stackInventory.GetAcquiredCount("fish_test"), "stack acquisition overflow should keep acquired count");

            PlayerRuntimeState uniqueState = new PlayerRuntimeState();
            uniqueState.itemAcquisitionCounts["unique_test"] = int.MaxValue;
            InventoryService uniqueInventory = new InventoryService(catalog, uniqueState);
            AssertFailKey(uniqueInventory.TryAddItem("unique_test", 1, "unique-overflow", 3), "inventory.acquisition_overflow", "unique acquisition overflow");
            AssertEqual(0, uniqueInventory.GetInventorySnapshot().Count, "unique acquisition overflow should not add inventory");
            AssertEqual(int.MaxValue, uniqueInventory.GetAcquiredCount("unique_test"), "unique acquisition overflow should keep acquired count");

            PlayerRuntimeState bagState = new PlayerRuntimeState();
            bagState.inventoryEntries.Add(new InventoryEntry { itemId = "fish_test", count = int.MaxValue, instanceId = string.Empty, levelIndex = -1 });
            bagState.inventoryEntries.Add(new InventoryEntry { itemId = "fish_test", count = 1, instanceId = string.Empty, levelIndex = -1 });
            BagQueryService bagQuery = new BagQueryService(catalog, bagState);
            AssertEqual(int.MaxValue, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "bag query count should cap at max int");
        }

        private static void RunBagQueryContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog(
                itemsCsv: string.Join("\n",
                    ItemHeader(),
                    "fish_common,멸치,Fish,Common,Fishing,10,TRUE,99,fish,1,TRUE,Common fish",
                    "fish_epic,참치,Fish,Epic,Fishing,100,TRUE,99,fish,2,TRUE,Epic fish",
                    "food_uncommon,구이,Food,Uncommon,Cooking,80,TRUE,99,meal,3,TRUE,Food",
                    "mat_rare,진주,UpgradeMaterial,Rare,Fishing,5,TRUE,99,upgrade,4,TRUE,Upgrade material",
                    "mat_high_locked,고급코어,HighGradeMaterial,Common,Reward,999,TRUE,99,upgrade,5,TRUE,Must stay not sellable",
                    "ticket_test,티켓,Ticket,Common,Shop,0,TRUE,99,ticket,5,TRUE,Ticket",
                    "special_test,특수품,Special,Rare,Reward,0,TRUE,99,,6,TRUE,Special"),
                fishCsv: string.Join("\n",
                    FishHeader(),
                    "fish_common_g1,fish_common,1,5,8,100,3,shore_common,FALSE,TRUE,OK",
                    "fish_common_g2,fish_common,2,6,9,50,6,shore_common,FALSE,TRUE,OK",
                    "fish_common_g3,fish_common,3,7,10,20,10,shore_common,FALSE,TRUE,OK",
                    "fish_epic_g1,fish_epic,1,20,30,100,10,shore_rare,FALSE,TRUE,OK",
                    "fish_epic_g2,fish_epic,2,24,34,50,16,shore_rare,FALSE,TRUE,OK",
                    "fish_epic_g3,fish_epic,3,30,40,20,24,shore_rare,FALSE,TRUE,OK"),
                recipesCsv: string.Join("\n",
                    RecipeHeader(),
                    "recipe_common,fish_common,3,fish_epic,1,30,food_uncommon,1,5,stage>=1,TRUE,OK"));
            PlayerRuntimeState state = new PlayerRuntimeState();
            InventoryService inventory = new InventoryService(catalog, state);
            CollectionService collection = new CollectionService(catalog, state, inventory);
            BagQueryService bagQuery = new BagQueryService(catalog, state);

            AssertSuccess(inventory.TryAddItem("fish_common", 2), "bag query common fish add");
            AssertSuccess(inventory.TryAddItem("fish_epic", 1), "bag query epic fish add");
            AssertSuccess(inventory.TryAddItem("food_uncommon", 1), "bag query food add");
            AssertSuccess(inventory.TryAddItem("mat_rare", 1), "bag query material add");
            AssertSuccess(inventory.TryAddItem("mat_high_locked", 1), "bag query high grade material add");
            AssertSuccess(inventory.TryAddItem("ticket_test", 1), "bag query ticket add");
            AssertSuccess(inventory.TryAddItem("special_test", 1), "bag query special add");

            AssertBagOrder(
                bagQuery.BuildSnapshot(new BagQueryOptions()),
                new[] { "fish_epic", "mat_rare", "special_test", "food_uncommon", "mat_high_locked", "fish_common", "ticket_test" },
                "default bag sort order");

            AssertBagOrder(
                bagQuery.BuildSnapshot(new BagQueryOptions { Category = "Fish" }),
                new[] { "fish_epic", "fish_common" },
                "fish category order");

            AssertEqual(4, bagQuery.BuildSnapshot(new BagQueryOptions { Filter = BagFilter.Sellable }).Count, "sellable filter count");
            AssertFalse(FindBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "mat_high_locked").Sellable, "high grade material should not show sellable even with positive price");
            AssertEqual(2, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions { Filter = BagFilter.Cookable }), "fish_common"), "cookable filter count");
            AssertEqual(2, bagQuery.BuildSnapshot(new BagQueryOptions { Category = "Other" }).Count, "other category includes ticket and special");
            AssertEqual(2, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions { Filter = BagFilter.MissingIngredient }), "fish_common"), "missing ingredient filter count");
            AssertEqual(0, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions { Filter = BagFilter.NewDiscovery }), "fish_common"), "fish item discovery is automatic after add");
            AssertTrue(state.newItemNoticeIds.Contains("fish_common"), "fish common new item notice");
            AssertTrue(FindBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_common").NewNotice, "bag row shows fish common new notice before acknowledge");
            AssertTrue(inventory.AcknowledgeNewItemNotice("fish_common"), "single item new notice acknowledge");
            AssertFalse(state.newItemNoticeIds.Contains("fish_common"), "single item new notice removed from state");
            AssertTrue(state.newItemNoticeIds.Contains("fish_epic"), "other item new notice remains after single acknowledge");
            AssertTrue(FindBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_epic").NewNotice, "other bag row keeps new notice after single acknowledge");
            AssertFalse(FindBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_common").NewNotice, "bag row hides fish common new notice after acknowledge");
            AssertFalse(inventory.AcknowledgeNewItemNotice("fish_common"), "duplicate new notice acknowledge should be no-op");
            AssertEqual(2, inventory.AcknowledgeNewItemNotices(new[] { "fish_epic", "food_uncommon" }), "bulk new notice acknowledge count");
            AssertFalse(state.newItemNoticeIds.Contains("fish_epic"), "bulk fish epic new notice removed");
            AssertFalse(state.newItemNoticeIds.Contains("food_uncommon"), "bulk food uncommon new notice removed");
            AssertTrue(state.newItemNoticeIds.Contains("mat_rare"), "not bulk-listed item new notice remains");

            AssertSuccess(collection.TryRegisterFishDiscovery("fish_common_g1"), "bag query fish grade discovery");
            AssertEqual(0, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions { Filter = BagFilter.NewDiscovery }), "fish_common"), "new discovery cleared by fish grade registration");

            AssertSuccess(collection.TryRegisterDiscovery("mat_rare"), "bag query direct item discovery");
            AssertEqual(0, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions { Filter = BagFilter.NewDiscovery }), "mat_rare"), "new discovery cleared by direct item registration");
        }

        private static void RunSaveMapperContractTests(TestReport report)
        {
            PlayerRuntimeState dirtyState = new PlayerRuntimeState
            {
                softCurrency = 999,
                prismPearl = 88,
                pirateCoin = 77,
                cash = 66,
                crewExp = 10,
                bagCapacity = 12,
                bagCapacityLevel = 2,
                cookingSlotLimit = 3,
                cookingSlotLevel = 2,
                currentStage = 3,
                farthestStage = 4,
                activeRecipeState = new ActiveRecipeState
                {
                    recipeId = "dirty_recipe",
                    startedUtcTicks = 1,
                    completesUtcTicks = 2
                }
            };
            dirtyState.inventoryEntries.Add(new InventoryEntry { itemId = "fish_test", count = 3, instanceId = string.Empty, levelIndex = -1 });
            dirtyState.itemAcquisitionCounts["fish_test"] = 3;
            dirtyState.discoveredCollectionItemIds.Add("fish_test_g1");
            dirtyState.claimedRewardIds.Add("claim_old");
            dirtyState.lockedItemIds.Add("fish_test");
            dirtyState.newItemNoticeIds.Add("fish_test");

            PlayerStateSaveMapper.Apply(null, dirtyState);
            AssertEqual(0L, dirtyState.softCurrency, "apply null clears currency");
            AssertEqual(0L, dirtyState.prismPearl, "apply null clears prism pearl");
            AssertEqual(0L, dirtyState.pirateCoin, "apply null clears pirate coin");
            AssertEqual(0L, dirtyState.cash, "apply null clears hidden cash");
            AssertEqual(0L, dirtyState.crewExp, "apply null clears crew exp");
            AssertEqual(0, dirtyState.bagCapacity, "apply null clears bag capacity");
            AssertEqual(0, dirtyState.bagCapacityLevel, "apply null clears bag capacity level");
            AssertEqual(0, dirtyState.cookingSlotLimit, "apply null clears cooking slot limit");
            AssertEqual(0, dirtyState.cookingSlotLevel, "apply null clears cooking slot level");
            AssertEqual(1, dirtyState.currentStage, "apply null resets current stage");
            AssertEqual(1, dirtyState.farthestStage, "apply null resets farthest stage");
            AssertTrue(dirtyState.activeRecipeState == null, "apply null clears active recipe");
            AssertEqual(0, dirtyState.inventoryEntries.Count, "apply null clears inventory");
            AssertEqual(0, dirtyState.itemAcquisitionCounts.Count, "apply null clears acquisition counts");
            AssertEqual(0, dirtyState.discoveredCollectionItemIds.Count, "apply null clears discoveries");
            AssertEqual(0, dirtyState.claimedRewardIds.Count, "apply null clears claims");
            AssertEqual(0, dirtyState.lockedItemIds.Count, "apply null clears locked items");
            AssertEqual(0, dirtyState.newItemNoticeIds.Count, "apply null clears new item notices");

            PlayerStateSaveData nullListSave = new PlayerStateSaveData
            {
                softCurrency = 12,
                prismPearl = 13,
                pirateCoin = 14,
                cash = 15,
                crewExp = 3,
                bagCapacity = 11,
                bagCapacityLevel = 3,
                cookingSlotLimit = 2,
                cookingSlotLevel = 1,
                currentStage = 2,
                farthestStage = 5,
                inventoryEntries = null,
                itemAcquisitionCounts = null,
                discoveredCollectionItemIds = null,
                claimedRewardIds = null,
                lockedItemIds = null,
                newItemNoticeIds = null
            };
            PlayerRuntimeState nullListRestored = PlayerStateSaveMapper.Restore(nullListSave);
            AssertEqual(12L, nullListRestored.softCurrency, "restore null lists currency");
            AssertEqual(13L, nullListRestored.prismPearl, "restore null lists prism pearl");
            AssertEqual(14L, nullListRestored.pirateCoin, "restore null lists pirate coin");
            AssertEqual(15L, nullListRestored.cash, "restore null lists hidden cash");
            AssertEqual(3L, nullListRestored.crewExp, "restore null lists crew exp");
            AssertEqual(11, nullListRestored.bagCapacity, "restore null lists bag capacity");
            AssertEqual(3, nullListRestored.bagCapacityLevel, "restore null lists bag capacity level");
            AssertEqual(2, nullListRestored.cookingSlotLimit, "restore null lists cooking slot limit");
            AssertEqual(1, nullListRestored.cookingSlotLevel, "restore null lists cooking slot level");
            AssertEqual(2, nullListRestored.currentStage, "restore null lists current stage");
            AssertEqual(5, nullListRestored.farthestStage, "restore null lists farthest stage");
            AssertEqual(0, nullListRestored.inventoryEntries.Count, "restore null inventory list");
            AssertEqual(0, nullListRestored.itemAcquisitionCounts.Count, "restore null acquisition list");
            AssertEqual(0, nullListRestored.discoveredCollectionItemIds.Count, "restore null discovery list");
            AssertEqual(0, nullListRestored.claimedRewardIds.Count, "restore null claim list");
            AssertEqual(0, nullListRestored.lockedItemIds.Count, "restore null locked item list");
            AssertEqual(0, nullListRestored.newItemNoticeIds.Count, "restore null new notice list");

            PlayerStateSaveData saveData = new PlayerStateSaveData
            {
                softCurrency = 55,
                prismPearl = 66,
                pirateCoin = 77,
                cash = 88,
                crewExp = 9,
                bagCapacity = 15,
                bagCapacityLevel = 4,
                cookingSlotLimit = 3,
                cookingSlotLevel = 2,
                currentStage = 4,
                farthestStage = 6,
                lastTrustedServerUtcTicks = 777,
                activeRecipeState = new ActiveRecipeSaveData
                {
                    recipeId = "recipe_test",
                    startedUtcTicks = 100,
                    completesUtcTicks = 130
                }
            };
            saveData.inventoryEntries.Add(new InventoryEntrySaveData { itemId = "fish_test", count = 2, instanceId = null, levelIndex = -1 });
            saveData.inventoryEntries.Add(new InventoryEntrySaveData { itemId = string.Empty, count = 99, instanceId = string.Empty, levelIndex = -1 });
            saveData.inventoryEntries.Add(new InventoryEntrySaveData { itemId = "food_test", count = 0, instanceId = string.Empty, levelIndex = -1 });
            saveData.inventoryEntries.Add(null);
            saveData.inventoryEntries.Add(new InventoryEntrySaveData { itemId = "unique_test", count = 1, instanceId = "unique-001", levelIndex = 2 });
            saveData.inventoryEntries.Add(new InventoryEntrySaveData { itemId = "unique_test", count = 1, instanceId = "unique-001", levelIndex = 9 });
            saveData.itemAcquisitionCounts.Add(new ItemAcquisitionSaveData { itemId = "fish_test", acquiredCount = 4 });
            saveData.itemAcquisitionCounts.Add(new ItemAcquisitionSaveData { itemId = string.Empty, acquiredCount = 9 });
            saveData.itemAcquisitionCounts.Add(new ItemAcquisitionSaveData { itemId = "food_test", acquiredCount = 0 });
            saveData.itemAcquisitionCounts.Add(null);
            saveData.discoveredCollectionItemIds.Add("fish_test_g1");
            saveData.discoveredCollectionItemIds.Add("fish_test_g1");
            saveData.discoveredCollectionItemIds.Add(string.Empty);
            saveData.claimedRewardIds.Add("claim_test");
            saveData.claimedRewardIds.Add("claim_test");
            saveData.lockedItemIds.Add("fish_test");
            saveData.lockedItemIds.Add("fish_test");
            saveData.lockedItemIds.Add(string.Empty);
            saveData.newItemNoticeIds.Add("fish_test");
            saveData.newItemNoticeIds.Add("fish_test");
            saveData.newItemNoticeIds.Add(string.Empty);

            PlayerRuntimeState restored = PlayerStateSaveMapper.Restore(saveData);
            AssertEqual(55L, restored.softCurrency, "restored save currency");
            AssertEqual(66L, restored.prismPearl, "restored save prism pearl");
            AssertEqual(77L, restored.pirateCoin, "restored save pirate coin");
            AssertEqual(88L, restored.cash, "restored save hidden cash");
            AssertEqual(9L, restored.crewExp, "restored save crew exp");
            AssertEqual(15, restored.bagCapacity, "restored save bag capacity");
            AssertEqual(4, restored.bagCapacityLevel, "restored save bag capacity level");
            AssertEqual(3, restored.cookingSlotLimit, "restored save cooking slot limit");
            AssertEqual(2, restored.cookingSlotLevel, "restored save cooking slot level");
            AssertEqual(4, restored.currentStage, "restored current stage");
            AssertEqual(6, restored.farthestStage, "restored farthest stage");
            AssertTrue(restored.activeRecipeState != null, "restored save active recipe");
            AssertEqual("recipe_test", restored.activeRecipeState.recipeId, "restored active recipe id");
            AssertEqual(2, restored.inventoryEntries.Count, "restored valid inventory entry count");
            AssertEqual(string.Empty, restored.inventoryEntries[0].instanceId, "restored null instance becomes empty");
            AssertEqual("unique-001", restored.inventoryEntries[1].instanceId, "restored unique instance id");
            AssertEqual(2, restored.inventoryEntries[1].levelIndex, "restored unique level index");
            AssertEqual(1, restored.itemAcquisitionCounts.Count, "restored valid acquisition count rows");
            AssertEqual(4, restored.itemAcquisitionCounts["fish_test"], "restored acquisition count value");
            AssertEqual(1, restored.discoveredCollectionItemIds.Count, "restored duplicate discovery ids deduped");
            AssertEqual(1, restored.claimedRewardIds.Count, "restored duplicate claim ids deduped");
            AssertEqual(1, restored.lockedItemIds.Count, "restored duplicate locked ids deduped");
            AssertEqual(1, restored.newItemNoticeIds.Count, "restored duplicate new notice ids deduped");

            restored.inventoryEntries.Add(new InventoryEntry { itemId = "unique_test", count = 1, instanceId = "unique-001", levelIndex = 9 });
            PlayerStateSaveData captured = PlayerStateSaveMapper.Capture(restored, 888);
            AssertEqual(888L, captured.lastTrustedServerUtcTicks, "captured trusted time");
            AssertEqual(66L, captured.prismPearl, "captured prism pearl");
            AssertEqual(77L, captured.pirateCoin, "captured pirate coin");
            AssertEqual(88L, captured.cash, "captured hidden cash");
            AssertEqual(15, captured.bagCapacity, "captured bag capacity");
            AssertEqual(4, captured.bagCapacityLevel, "captured bag capacity level");
            AssertEqual(3, captured.cookingSlotLimit, "captured cooking slot limit");
            AssertEqual(2, captured.cookingSlotLevel, "captured cooking slot level");
            AssertEqual(2, captured.inventoryEntries.Count, "captured inventory entry count");
            AssertEqual(2, captured.inventoryEntries[1].levelIndex, "capture keeps first duplicate instance entry");
            AssertEqual(1, captured.discoveredCollectionItemIds.Count, "captured discovery id count");
            AssertEqual(1, captured.claimedRewardIds.Count, "captured claim id count");
            AssertEqual(1, captured.lockedItemIds.Count, "captured locked item id count");
            AssertEqual(1, captured.newItemNoticeIds.Count, "captured new notice id count");

            PlayerStateSaveData nullCapture = PlayerStateSaveMapper.Capture(null, 999);
            AssertEqual(999L, nullCapture.lastTrustedServerUtcTicks, "null capture trusted time");
            AssertEqual(0L, nullCapture.softCurrency, "null capture currency");
            AssertEqual(0L, nullCapture.prismPearl, "null capture prism pearl");
            AssertEqual(0L, nullCapture.pirateCoin, "null capture pirate coin");
            AssertEqual(0L, nullCapture.cash, "null capture hidden cash");
            AssertEqual(0, nullCapture.bagCapacity, "null capture bag capacity");
            AssertEqual(0, nullCapture.bagCapacityLevel, "null capture bag capacity level");
            AssertEqual(0, nullCapture.cookingSlotLimit, "null capture cooking slot limit");
            AssertEqual(0, nullCapture.cookingSlotLevel, "null capture cooking slot level");
            AssertEqual(0, nullCapture.lockedItemIds.Count, "null capture locked item ids");
            AssertEqual(0, nullCapture.newItemNoticeIds.Count, "null capture new notice ids");
        }

        private static void RunResultDtoContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog(
                shopCsv: string.Join("\n",
                    ShopHeader(),
                    "shop_success,Material,softCurrency,30,fish_test,2,stage>=1,1,TRUE,Success"),
                collectionRewardsCsv: string.Join("\n",
                    CollectionRewardHeader(),
                    "reward_combo,fish_test,discovery,1,softCurrency,10,food_test,1,claim_combo,1,TRUE,Combo reward"));
            PlayerRuntimeState state = new PlayerRuntimeState
            {
                softCurrency = 100
            };
            ManualClock clock = new ManualClock(new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
            InventoryService inventory = new InventoryService(catalog, state);
            ShopService shop = new ShopService(catalog, state, inventory);
            CookingService cooking = new CookingService(catalog, state, inventory, clock);
            CollectionService collection = new CollectionService(catalog, state, inventory);

            ServiceResult missing = inventory.TryAddItem("missing_item", 1);
            AssertFailKey(missing, "inventory.unknown_item", "result dto failure key");
            AssertTrue(!string.IsNullOrEmpty(missing.FailureReason), "failure result should carry reason");
            AssertEqual(0, missing.ItemDeltas.Count, "failure result item deltas");
            AssertEqual(0L, missing.CurrencyDelta, "failure result currency delta");
            AssertEqual(0, missing.AffectedIds.Count, "failure result affected ids");

            ServiceResult add = inventory.TryAddItem("fish_test", 2);
            AssertSuccess(add, "result dto add");
            AssertMessageKey(add, "inventory.add_success", "add result message key");
            AssertItemDelta(add, "fish_test", 2, "add result item delta");
            AssertEqual(0L, add.CurrencyDelta, "add result currency delta");
            AssertAffected(add, "fish_test", "add result affected id");

            ServiceResult sell = inventory.TrySellItem("fish_test", 1);
            AssertSuccess(sell, "result dto sell");
            AssertMessageKey(sell, "inventory.sell_success", "sell result message key");
            AssertItemDelta(sell, "fish_test", -1, "sell result item delta");
            AssertEqual(10L, sell.CurrencyDelta, "sell result currency delta");
            AssertAffected(sell, "fish_test", "sell result affected id");

            ServiceResult purchase = shop.TryPurchaseShopItem("shop_success");
            AssertSuccess(purchase, "result dto shop purchase");
            AssertMessageKey(purchase, "shop.purchase_success", "shop result message key");
            AssertItemDelta(purchase, "fish_test", 2, "shop result item delta");
            AssertEqual(-30L, purchase.CurrencyDelta, "shop result currency delta");
            AssertAffected(purchase, "shop_success", "shop result affected shop id");
            AssertAffected(purchase, "fish_test", "shop result affected reward id");

            AssertSuccess(inventory.TryAddItem("fish_test2", 1), "result dto second fish add");
            ServiceResult start = cooking.TryStartRecipe("recipe_test");
            AssertSuccess(start, "result dto cooking start");
            AssertMessageKey(start, "cooking.start_success", "cooking start message key");
            AssertItemDelta(start, "fish_test", -1, "cooking start first item delta");
            AssertItemDelta(start, "fish_test2", -1, "cooking start second item delta");
            AssertAffected(start, "recipe_test", "cooking start affected recipe id");
            AssertAffected(start, "fish_test", "cooking start affected input id");
            AssertAffected(start, "fish_test2", "cooking start affected second input id");

            clock.AdvanceSeconds(30);
            ServiceResult complete = cooking.TryCompleteRecipe();
            AssertSuccess(complete, "result dto cooking complete");
            AssertMessageKey(complete, "cooking.complete_success", "cooking complete message key");
            AssertItemDelta(complete, "food_test", 1, "cooking complete item delta");
            AssertAffected(complete, "recipe_test", "cooking complete affected recipe id");
            AssertAffected(complete, "food_test", "cooking complete affected output id");

            AssertSuccess(collection.TryRegisterDiscovery("fish_test"), "result dto collection discovery setup");
            ServiceResult reward = collection.TryClaimCollectionReward("reward_combo");
            AssertSuccess(reward, "result dto collection reward");
            AssertMessageKey(reward, "collection.claim_success", "collection reward message key");
            AssertItemDelta(reward, "food_test", 1, "collection reward item delta");
            AssertEqual(10L, reward.CurrencyDelta, "collection reward currency delta");
            AssertAffected(reward, "reward_combo", "collection reward affected reward id");
            AssertAffected(reward, "claim_combo", "collection reward affected claim id");
            AssertAffected(reward, "food_test", "collection reward affected item id");
        }

        private static void RunCurrencyOverflowContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog(
                itemsCsv: string.Join("\n",
                    ItemHeader(),
                    "overflow_add,오버플로우판매,Fish,Common,Fishing,9223372036854775807,TRUE,99,fish,1,TRUE,Overflow add",
                    "overflow_multiply,오버플로우곱셈,Fish,Common,Fishing,4611686018427387904,TRUE,99,fish,2,TRUE,Overflow multiply"),
                fishCsv: HeaderOnly(FishHeader()),
                recipesCsv: HeaderOnly(RecipeHeader()),
                collectionRewardsCsv: string.Join("\n",
                    CollectionRewardHeader(),
                    "reward_currency_overflow,overflow_add,discovery,1,softCurrency,9223372036854775807,,,claim_currency_overflow,1,TRUE,Overflow reward"));

            PlayerRuntimeState sellAddState = new PlayerRuntimeState
            {
                softCurrency = 1
            };
            InventoryService sellAddInventory = new InventoryService(catalog, sellAddState);
            BagQueryService sellAddBagQuery = new BagQueryService(catalog, sellAddState);
            AssertSuccess(sellAddInventory.TryAddItem("overflow_add", 1), "overflow add item setup");
            AssertFailKey(sellAddInventory.TrySellItem("overflow_add", 1), "inventory.currency_overflow", "sell currency add overflow");
            AssertEqual(1L, sellAddState.softCurrency, "currency unchanged after sell add overflow");
            AssertEqual(1, CountBagItem(sellAddBagQuery.BuildSnapshot(new BagQueryOptions()), "overflow_add"), "item unchanged after sell add overflow");

            PlayerRuntimeState sellMultiplyState = new PlayerRuntimeState
            {
                softCurrency = 0
            };
            InventoryService sellMultiplyInventory = new InventoryService(catalog, sellMultiplyState);
            BagQueryService sellMultiplyBagQuery = new BagQueryService(catalog, sellMultiplyState);
            AssertSuccess(sellMultiplyInventory.TryAddItem("overflow_multiply", 2), "overflow multiply item setup");
            AssertFailKey(sellMultiplyInventory.TrySellItem("overflow_multiply", 2), "inventory.currency_overflow", "sell currency multiply overflow");
            AssertEqual(0L, sellMultiplyState.softCurrency, "currency unchanged after sell multiply overflow");
            AssertEqual(2, CountBagItem(sellMultiplyBagQuery.BuildSnapshot(new BagQueryOptions()), "overflow_multiply"), "item unchanged after sell multiply overflow");

            PlayerRuntimeState rewardState = new PlayerRuntimeState
            {
                softCurrency = 1
            };
            InventoryService rewardInventory = new InventoryService(catalog, rewardState);
            CollectionService collection = new CollectionService(catalog, rewardState, rewardInventory);
            AssertSuccess(collection.TryRegisterDiscovery("overflow_add"), "overflow reward discovery setup");
            AssertFailKey(collection.TryClaimCollectionReward("reward_currency_overflow"), "collection.currency_overflow", "collection reward currency overflow");
            AssertEqual(1L, rewardState.softCurrency, "currency unchanged after collection reward overflow");
            AssertFalse(rewardState.claimedRewardIds.Contains("claim_currency_overflow"), "overflow collection reward should not be claimed");
        }

        private static void RunRewardBundleSeamTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog();

            PlayerRuntimeState successState = new PlayerRuntimeState
            {
                softCurrency = 10,
                bagCapacity = 2
            };
            InventoryService successInventory = new InventoryService(catalog, successState);
            RewardBundleService successRewards = new RewardBundleService(successState, successInventory);
            RewardBundle successBundle = new RewardBundle("bundle_success")
            {
                CurrencyId = "softCurrency",
                CurrencyAmount = 15
            };
            successBundle.ItemGrants.Add(new ItemDelta("fish_test", 2));

            ServiceResult success = successRewards.TryApplyRewardBundle(
                successBundle,
                "reward_bundle.success",
                "reward_bundle.item_failed",
                "reward_bundle.currency_failed");
            AssertSuccess(success, "reward bundle success");
            AssertEqual(25L, successState.softCurrency, "reward bundle currency applied");
            AssertEqual(2, CountBagItem(new BagQueryService(catalog, successState).BuildSnapshot(new BagQueryOptions()), "fish_test"), "reward bundle item applied");
            AssertItemDelta(success, "fish_test", 2, "reward bundle item delta");
            AssertEqual(15L, success.CurrencyDelta, "reward bundle currency delta");
            AssertAffected(success, "bundle_success", "reward bundle source affected");
            AssertAffected(success, "fish_test", "reward bundle item affected");

            PlayerRuntimeState rollbackState = new PlayerRuntimeState
            {
                softCurrency = 50,
                bagCapacity = 1
            };
            InventoryService rollbackInventory = new InventoryService(catalog, rollbackState);
            RewardBundleService rollbackRewards = new RewardBundleService(rollbackState, rollbackInventory);
            AssertSuccess(rollbackInventory.TryAddItem("fish_test", 2), "reward rollback capacity setup");

            RewardBundle blockedBundle = new RewardBundle("bundle_blocked")
            {
                CurrencyId = "softCurrency",
                CurrencyAmount = 40
            };
            blockedBundle.ItemGrants.Add(new ItemDelta("food_test", 1));
            ServiceResult blocked = rollbackRewards.TryApplyRewardBundle(
                blockedBundle,
                "reward_bundle.success",
                "reward_bundle.item_failed",
                "reward_bundle.currency_failed");
            AssertFailKey(blocked, "reward_bundle.item_failed", "reward bundle rolls back on item apply failure");
            AssertEqual(50L, rollbackState.softCurrency, "reward bundle currency rolled back after item failure");
            AssertEqual(2, CountBagItem(new BagQueryService(catalog, rollbackState).BuildSnapshot(new BagQueryOptions()), "fish_test"), "reward bundle existing item preserved");
            AssertEqual(0, CountBagItem(new BagQueryService(catalog, rollbackState).BuildSnapshot(new BagQueryOptions()), "food_test"), "reward bundle failed item not added");
            AssertFalse(rollbackState.newItemNoticeIds.Contains("food_test"), "failed reward item should not leave new marker");

            PlayerRuntimeState overflowState = new PlayerRuntimeState
            {
                softCurrency = long.MaxValue
            };
            RewardBundleService overflowRewards = new RewardBundleService(overflowState, new InventoryService(catalog, overflowState));
            RewardBundle overflowBundle = new RewardBundle("bundle_currency_overflow")
            {
                CurrencyId = "softCurrency",
                CurrencyAmount = 1
            };
            ServiceResult overflow = overflowRewards.TryApplyRewardBundle(
                overflowBundle,
                "reward_bundle.success",
                "reward_bundle.item_failed",
                "reward_bundle.currency_failed");
            AssertFailKey(overflow, "reward_bundle.currency_failed", "reward bundle currency overflow");
            AssertEqual(long.MaxValue, overflowState.softCurrency, "reward bundle overflow leaves currency unchanged");
        }

        private static void RunShopContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog(
                shopCsv: string.Join("\n",
                    ShopHeader(),
                    "shop_success,Material,softCurrency,30,fish_test,2,stage>=1,1,TRUE,Success",
                    "shop_expensive,Material,softCurrency,999,fish_test,1,stage>=1,2,TRUE,Too expensive",
                    "shop_prism,Material,prismPearl,5,fish_test,1,stage>=1,4,TRUE,Prism purchase",
                    "shop_coin,Material,pirateCoin,6,fish_test,1,stage>=1,5,TRUE,Pirate coin purchase",
                    "shop_disabled_reward,Material,softCurrency,20,disabled_item,1,stage>=1,6,TRUE,Refund path",
                    "shop_stage2,Material,softCurrency,1,fish_test,1,stage>=2,7,TRUE,Stage locked",
                    "shop_off,Material,softCurrency,10,fish_test,1,stage>=1,8,FALSE,Disabled shop",
                    "shop_hidden,Material,softCurrency,10,fish_test,1,stage>=1,9,TRUE,Player state hidden,currency:softCurrency>=500;acquired:fish_test>=1"),
                premiumCurrencyProductsCsv: string.Join("\n",
                    PremiumCurrencyProductHeader(),
                    "cash_prism_small_001,1000,100,10,TRUE,Internal test small",
                    "cash_prism_medium_001,5000,550,20,TRUE,Internal test medium",
                    "cash_prism_large_001,10000,1200,30,TRUE,Internal test large",
                    "cash_prism_disabled_001,1000,100,40,FALSE,Disabled test"));
            PlayerRuntimeState state = new PlayerRuntimeState
            {
                softCurrency = 100,
                prismPearl = 50,
                pirateCoin = 60
            };
            InventoryService inventory = new InventoryService(catalog, state);
            ShopService shop = new ShopService(catalog, state, inventory);
            BagQueryService bagQuery = new BagQueryService(catalog, state);

            AssertFailKey(shop.TryPurchaseShopItem("missing_shop"), "shop.unknown_item", "unknown shop item");
            AssertFailKey(shop.TryPurchaseShopItem("shop_off"), "shop.disabled", "disabled shop item");
            AssertFailKey(shop.TryPurchaseShopItem("shop_expensive"), "shop.not_enough_currency", "expensive shop item");
            AssertEqual(100L, state.softCurrency, "currency unchanged after expensive item");
            AssertFalse(shop.IsShopItemVisible("shop_hidden"), "player-sync hidden shop item should start hidden");
            AssertFailKey(shop.TryPurchaseShopItem("shop_hidden"), "shop.not_visible", "hidden shop item cannot be purchased by id");

            PlayerRuntimeState lockedState = new PlayerRuntimeState
            {
                softCurrency = 100,
                currentStage = 1,
                farthestStage = 1
            };
            InventoryService lockedInventory = new InventoryService(catalog, lockedState);
            ShopService lockedShop = new ShopService(catalog, lockedState, lockedInventory);
            AssertFailKey(lockedShop.TryPurchaseShopItem("shop_stage2"), "shop.locked", "stage locked shop item");
            lockedState.farthestStage = 2;
            AssertSuccess(lockedShop.TryPurchaseShopItem("shop_stage2"), "stage unlocked shop item");

            AssertSuccess(shop.TryPurchaseShopItem("shop_success"), "shop success");
            AssertEqual(70L, state.softCurrency, "currency after shop success");
            AssertEqual(2, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "shop reward count");
            AssertSuccess(shop.TryPurchaseShopItem("shop_prism"), "shop prism success");
            AssertEqual(45L, state.prismPearl, "prism after shop success");
            AssertEqual(4, catalog.PremiumCurrencyProductsById.Count, "premium currency product rows");
            state.cash = 1000;
            AssertSuccess(shop.TryPurchasePremiumCurrencyProduct("cash_prism_small_001"), "premium currency product purchase");
            AssertEqual(0L, state.cash, "hidden cash after premium product purchase");
            AssertEqual(145L, state.prismPearl, "prism after premium product purchase");
            AssertFailKey(shop.TryPurchasePremiumCurrencyProduct("cash_prism_medium_001"), "shop.not_enough_cash", "premium product hidden cash shortage");
            AssertFailKey(shop.TryPurchasePremiumCurrencyProduct("cash_prism_disabled_001"), "shop.premium_currency_product_disabled", "disabled premium product");
            AssertFalse(FisherCurrencyContract.IsKnownCurrency("cash"), "cash remains hidden after premium product catalog use");
            state.cash = 1000;
            AssertSuccess(shop.TryPurchasePrismPearlWithCash(300, 30), "hidden cash prism purchase");
            AssertEqual(700L, state.cash, "hidden cash after prism purchase");
            AssertEqual(175L, state.prismPearl, "prism after hidden cash purchase");
            AssertSuccess(shop.TryPurchaseShopItem("shop_coin"), "shop pirate coin success");
            AssertEqual(54L, state.pirateCoin, "pirate coin after shop success");
            AssertEqual(4, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "fish_test"), "multi-currency shop reward count");

            state.softCurrency = 500;
            state.itemAcquisitionCounts["fish_test"] = 1;
            AssertTrue(shop.IsShopItemVisible("shop_hidden"), "player-sync hidden shop item should appear after currency and acquisition sync");
            AssertSuccess(shop.TryPurchaseShopItem("shop_hidden"), "player-sync visible shop purchase");

            AssertFailKey(shop.TryPurchaseShopItem("shop_disabled_reward"), "shop.reward_apply_failed_refunded", "shop refund on reward apply failure");
            AssertEqual(490L, state.softCurrency, "currency refunded after reward failure");
            AssertEqual(0, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "disabled_item"), "disabled reward not added");

            AssertFailKey(shop.TryExchangeCurrency("softCurrency", "prismPearl", 10, 1), "shop.exchange_disabled", "currency exchange disabled by default");

            BalanceCatalog exchangeCatalog = BuildSyntheticCatalog(
                shopCsv: string.Join("\n", ShopHeader()),
                economyParamsCsv: DefaultEconomyParamsCsv() + "\n" +
                                  "currency_exchange_enabled,TRUE,bool,shop,TRUE,Exchange test");
            PlayerRuntimeState exchangeState = new PlayerRuntimeState
            {
                softCurrency = 100,
                prismPearl = 1,
                pirateCoin = long.MaxValue
            };
            ShopService exchangeShop = new ShopService(exchangeCatalog, exchangeState, new InventoryService(exchangeCatalog, exchangeState));
            AssertFailKey(exchangeShop.TryExchangeCurrency("softCurrency", "gold", 10, 1), "shop.invalid_exchange", "same currency aliases cannot exchange");
            AssertFailKey(exchangeShop.TryExchangeCurrency("softCurrency", "prismPearl", 0, 1), "shop.invalid_exchange", "zero source exchange amount");
            AssertFailKey(exchangeShop.TryExchangeCurrency("unknown", "prismPearl", 1, 1), "shop.unknown_currency", "unknown exchange currency");
            AssertFailKey(exchangeShop.TryExchangeCurrency("softCurrency", "prismPearl", 101, 1), "shop.not_enough_currency", "exchange source shortage");
            ServiceResult exchange = exchangeShop.TryExchangeCurrency("softCurrency", "prismPearl", 20, 2);
            AssertSuccess(exchange, "currency exchange success");
            AssertMessageKey(exchange, "shop.exchange_success", "currency exchange success key");
            AssertEqual(80L, exchangeState.softCurrency, "exchange subtracts source currency");
            AssertEqual(3L, exchangeState.prismPearl, "exchange grants target currency");
            AssertFailKey(exchangeShop.TryExchangeCurrency("softCurrency", "pirateCoin", 10, 1), "shop.exchange_target_failed_refunded", "exchange target overflow refunds source");
            AssertEqual(80L, exchangeState.softCurrency, "exchange overflow refunds source");
            AssertEqual(long.MaxValue, exchangeState.pirateCoin, "exchange overflow keeps target currency");
        }

        private static void RunCookingSlotContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog(
                recipesCsv: string.Join("\n",
                    RecipeHeader(),
                    "recipe_test,fish_test,1,fish_test2,1,30,food_test,1,5,stage>=1,TRUE,OK",
                    "recipe_alt,fish_test2,1,fish_test,1,30,food_test,1,5,stage>=1,TRUE,Alt",
                    "recipe_third,fish_test,1,fish_test2,1,30,food_test,1,5,stage>=1,TRUE,Third",
                    "recipe_fourth,fish_test2,1,fish_test,1,30,food_test,1,5,stage>=1,TRUE,Fourth"));

            PlayerRuntimeState queueState = new PlayerRuntimeState
            {
                cookingSlotLimit = 0
            };
            ManualClock queueClock = new ManualClock(new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
            InventoryService queueInventory = new InventoryService(catalog, queueState);
            CookingService queueCooking = new CookingService(catalog, queueState, queueInventory, queueClock);

            AssertEqual(0, queueCooking.ActiveCookingSlotCount, "initial active cooking slot count");
            AssertEqual(3, queueCooking.CookingSlotLimit, "initial cooking slot limit");
            AssertSuccess(queueInventory.TryAddItem("fish_test", CookingService.MaxQueueCount + 1), "slot contract fish setup");
            AssertSuccess(queueInventory.TryAddItem("fish_test2", CookingService.MaxQueueCount + 1), "slot contract fish2 setup");
            AssertSuccess(queueCooking.TryQueueRecipe("recipe_test", CookingService.MaxQueueCount), "same recipe queue max");
            AssertEqual(1, queueInventory.CountItem("fish_test"), "queued recipe consumes first ingredient");
            AssertEqual(1, queueInventory.CountItem("fish_test2"), "queued recipe consumes second ingredient");
            AssertEqual(CookingService.MaxQueueCount, queueState.activeRecipeState.queuedCount, "same recipe queue count max");
            AssertEqual(1, queueCooking.ActiveCookingSlotCount, "active cooking slot count after queue");
            AssertFailKey(queueCooking.TryQueueRecipe("recipe_test", 1), "cooking.queue_full", "same recipe queue beyond max");
            AssertSuccess(queueCooking.TryCancelActiveRecipe(), "active cooking cancel");
            AssertTrue(queueState.activeRecipeState == null, "cancel clears active recipe state");
            AssertEqual(CookingService.MaxQueueCount + 1, queueInventory.CountItem("fish_test"), "cancel refunds first ingredient");
            AssertEqual(CookingService.MaxQueueCount + 1, queueInventory.CountItem("fish_test2"), "cancel refunds second ingredient");
            AssertEqual(CookingService.MaxQueueCount + 1, queueInventory.GetAcquiredCount("fish_test"), "cancel refund should not increase first acquired count");
            AssertEqual(CookingService.MaxQueueCount + 1, queueInventory.GetAcquiredCount("fish_test2"), "cancel refund should not increase second acquired count");
            AssertEqual(0, queueCooking.ActiveCookingSlotCount, "active cooking slot count after cancel");
            AssertFailKey(queueCooking.TryCancelActiveRecipe(), "cooking.no_active_recipe", "cancel without active recipe fails");
            queueState.activeRecipeState = new ActiveRecipeState
            {
                recipeId = "missing_recipe",
                queuedCount = 1
            };
            AssertFailKey(queueCooking.TryCancelActiveRecipe(), "cooking.missing_active_recipe", "cancel with missing recipe should fail");
            AssertTrue(queueState.activeRecipeState != null, "missing recipe cancel should keep active recipe state");
            queueState.activeRecipeState = null;

            PlayerRuntimeState expandState = new PlayerRuntimeState
            {
                softCurrency = 3000
            };
            CookingService expandCooking = new CookingService(catalog, expandState, new InventoryService(catalog, expandState), queueClock);
            AssertEqual(3, expandCooking.CookingSlotLimit, "uninitialized cooking slot limit defaults to three");
            ServiceResult expand = expandCooking.TryPurchaseCookingSlotExpansion();
            AssertFailKey(expand, "cooking.slot_expansion_disabled", "cooking slot expansion disabled");
            AssertEqual(3000L, expandState.softCurrency, "disabled cooking slot expansion keeps gold");
            AssertEqual(3, expandState.cookingSlotLimit, "disabled cooking slot expansion keeps fixed limit");
            AssertEqual(0, expandState.cookingSlotLevel, "disabled cooking slot expansion keeps level zero");

            PlayerRuntimeState poorState = new PlayerRuntimeState
            {
                softCurrency = 999,
                cookingSlotLimit = 3
            };
            CookingService poorCooking = new CookingService(catalog, poorState, new InventoryService(catalog, poorState), queueClock);
            AssertFailKey(poorCooking.TryPurchaseCookingSlotExpansion(), "cooking.slot_expansion_disabled", "cooking slot purchase remains disabled even when poor");
            AssertEqual(999L, poorState.softCurrency, "failed cooking slot purchase keeps gold");
            AssertEqual(3, poorState.cookingSlotLimit, "failed cooking slot purchase keeps limit");

            PlayerRuntimeState fullState = new PlayerRuntimeState
            {
                cookingSlotLimit = 3
            };
            ManualClock fullClock = new ManualClock(new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
            InventoryService fullInventory = new InventoryService(catalog, fullState);
            CookingService fullCooking = new CookingService(catalog, fullState, fullInventory, fullClock);
            AssertSuccess(fullInventory.TryAddItem("fish_test", 4), "full slot first fish setup");
            AssertSuccess(fullInventory.TryAddItem("fish_test2", 4), "full slot second fish setup");
            AssertSuccess(fullCooking.TryStartRecipe("recipe_test"), "full slot first recipe start");
            AssertSuccess(fullCooking.TryStartRecipe("recipe_alt"), "full slot second recipe start");
            AssertSuccess(fullCooking.TryStartRecipe("recipe_third"), "full slot third recipe start");
            AssertFailKey(fullCooking.TryStartRecipe("recipe_fourth"), "cooking.slot_full", "different recipe blocked when three cooking slots are occupied");

            PlayerRuntimeState multiState = new PlayerRuntimeState
            {
                cookingSlotLimit = 3
            };
            ManualClock multiClock = new ManualClock(new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
            InventoryService multiInventory = new InventoryService(catalog, multiState);
            CookingService multiCooking = new CookingService(catalog, multiState, multiInventory, multiClock);
            AssertSuccess(multiInventory.TryAddItem("fish_test", 2), "multi slot first fish setup");
            AssertSuccess(multiInventory.TryAddItem("fish_test2", 2), "multi slot second fish setup");
            AssertSuccess(multiCooking.TryStartRecipe("recipe_test"), "multi slot first recipe start");
            AssertSuccess(multiCooking.TryStartRecipe("recipe_alt"), "multi slot second recipe start");
            AssertEqual(2, multiCooking.ActiveCookingSlotCount, "multi slot active count after two recipes");
            AssertEqual(2, multiState.activeRecipeStates.Count, "multi slot state stores two active recipes");
            multiClock.AdvanceSeconds(30);
            AssertSuccess(multiCooking.TryCompleteRecipe(), "multi slot first completion");
            AssertEqual(1, multiCooking.ActiveCookingSlotCount, "multi slot active count after first completion");
            AssertSuccess(multiCooking.TryCompleteRecipe(), "multi slot second completion");
            AssertEqual(0, multiCooking.ActiveCookingSlotCount, "multi slot active count after second completion");
            AssertEqual(2, CountBagItem(new BagQueryService(catalog, multiState).BuildSnapshot(new BagQueryOptions()), "food_test"), "multi slot outputs both recipes");

            PlayerRuntimeState serverSnapshotState = new PlayerRuntimeState
            {
                cookingSlotLimit = 3
            };
            CookingService serverSnapshotCooking = new CookingService(catalog, serverSnapshotState, new InventoryService(catalog, serverSnapshotState), multiClock);
            long serverStartedTicks = new DateTime(2026, 6, 5, 1, 0, 0, DateTimeKind.Utc).Ticks;
            ServiceResult serverSnapshot = serverSnapshotCooking.ReplaceServerCookingSnapshot(
                new List<ActiveRecipeState>
                {
                    new ActiveRecipeState
                    {
                        slotIndex = 1,
                        recipeId = "recipe_test",
                        startedUtcTicks = serverStartedTicks,
                        completesUtcTicks = serverStartedTicks + TimeSpan.FromSeconds(30).Ticks,
                        queuedCount = 2
                    }
                },
                openedSlotCount: 3);
            AssertSuccess(serverSnapshot, "server cooking snapshot applies");
            AssertEqual(3, serverSnapshotCooking.CookingSlotLimit, "server cooking snapshot opened slots");
            AssertEqual(1, serverSnapshotCooking.ActiveCookingSlotCount, "server cooking snapshot active count");
            AssertEqual("recipe_test", serverSnapshotState.activeRecipeState.recipeId, "server cooking snapshot recipe id");
            AssertEqual(2, serverSnapshotState.activeRecipeState.queuedCount, "server cooking snapshot queue count");
            AssertEqual(0, serverSnapshotState.inventoryEntries.Count, "server cooking snapshot should not consume local inventory");

            PlayerRuntimeState orderedSnapshotState = new PlayerRuntimeState
            {
                cookingSlotLimit = 3,
                activeRecipeState = new ActiveRecipeState
                {
                    slotIndex = 0,
                    recipeId = "recipe_test",
                    startedUtcTicks = serverStartedTicks - TimeSpan.FromMinutes(5).Ticks,
                    completesUtcTicks = serverStartedTicks - TimeSpan.FromMinutes(4).Ticks,
                    queuedCount = 1
                }
            };
            CookingService orderedSnapshotCooking = new CookingService(catalog, orderedSnapshotState, new InventoryService(catalog, orderedSnapshotState), multiClock);
            ServiceResult orderedServerSnapshot = orderedSnapshotCooking.ReplaceServerCookingSnapshot(
                new List<ActiveRecipeState>
                {
                    new ActiveRecipeState
                    {
                        slotIndex = 1,
                        recipeId = "recipe_test",
                        startedUtcTicks = serverStartedTicks,
                        completesUtcTicks = serverStartedTicks + TimeSpan.FromSeconds(30).Ticks,
                        queuedCount = 1
                    },
                    new ActiveRecipeState
                    {
                        slotIndex = 0,
                        recipeId = "recipe_alt",
                        startedUtcTicks = serverStartedTicks,
                        completesUtcTicks = serverStartedTicks + TimeSpan.FromSeconds(45).Ticks,
                        queuedCount = 1
                    }
                },
                openedSlotCount: 3);
            AssertSuccess(orderedServerSnapshot, "ordered server cooking snapshot applies");
            AssertEqual(2, orderedSnapshotCooking.ActiveCookingSlotCount, "ordered server cooking snapshot active count");
            AssertEqual("recipe_alt", orderedSnapshotState.activeRecipeState.recipeId, "ordered server cooking snapshot primary follows first server slot");
            AssertEqual(2, orderedSnapshotCooking.ActiveRecipeStates.Count, "ordered server cooking snapshot keeps server jobs only");

            PlayerRuntimeState noJobState = new PlayerRuntimeState
            {
                cookingSlotLimit = 3
            };
            noJobState.activeRecipeStates.Add(new ActiveRecipeState
            {
                slotIndex = 1,
                recipeId = "recipe_test",
                startedUtcTicks = serverStartedTicks,
                completesUtcTicks = serverStartedTicks + TimeSpan.FromSeconds(30).Ticks,
                queuedCount = 1
            });
            CookingService noJobCooking = new CookingService(catalog, noJobState, new InventoryService(catalog, noJobState), multiClock);
            ServiceResult noJobClear = noJobCooking.ClearServerCookingSlot(0, "recipe_test");
            AssertSuccess(noJobClear, "server no_job clears stale recipe identity");
            AssertEqual(0, noJobCooking.ActiveCookingSlotCount, "server no_job removes stale local active even when slot differs");

            PlayerRuntimeState emptySnapshotState = new PlayerRuntimeState
            {
                cookingSlotLimit = 3
            };
            ActiveRecipeState staleServerSnapshotActive = new ActiveRecipeState
            {
                slotIndex = 0,
                recipeId = "recipe_test",
                startedUtcTicks = serverStartedTicks,
                completesUtcTicks = serverStartedTicks + TimeSpan.FromSeconds(30).Ticks,
                queuedCount = 1
            };
            emptySnapshotState.activeRecipeState = staleServerSnapshotActive;
            emptySnapshotState.activeRecipeStates.Add(staleServerSnapshotActive);
            CookingService emptySnapshotCooking = new CookingService(catalog, emptySnapshotState, new InventoryService(catalog, emptySnapshotState), multiClock);
            ServiceResult emptyServerSnapshot = emptySnapshotCooking.ReplaceServerCookingSnapshot(
                new List<ActiveRecipeState>(),
                openedSlotCount: 0);
            AssertSuccess(emptyServerSnapshot, "empty server cooking snapshot applies");
            AssertEqual(0, emptySnapshotCooking.ActiveCookingSlotCount, "empty server cooking snapshot clears stale local active");
            AssertTrue(emptySnapshotState.activeRecipeState == null, "empty server cooking snapshot clears legacy active singleton");
            AssertEqual(0, emptySnapshotCooking.ActiveRecipeStates.Count, "empty server cooking snapshot stays empty after getter normalization");
            AssertEqual(3, emptySnapshotCooking.CookingSlotLimit, "empty server cooking snapshot preserves known slot limit");
        }

        private static void RunCookingFailureTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog();
            PlayerRuntimeState state = new PlayerRuntimeState();
            ManualClock clock = new ManualClock(new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
            InventoryService inventory = new InventoryService(catalog, state);
            CookingService cooking = new CookingService(catalog, state, inventory, clock);
            BagQueryService bagQuery = new BagQueryService(catalog, state);

            AssertFailKey(cooking.TryStartRecipe("recipe_test"), "inventory.not_enough_item", "recipe without ingredient");
            AssertTrue(state.activeRecipeState == null, "active recipe should stay null after missing ingredient");

            AssertSuccess(inventory.TryAddItem("fish_test", 2), "cooking fish add");
            AssertSuccess(inventory.TryAddItem("fish_test2", 2), "cooking fish2 add");
            AssertSuccess(cooking.TryStartRecipe("recipe_test"), "recipe start synthetic");
            AssertSuccess(cooking.TryStartRecipe("recipe_test"), "same recipe queue add");
            AssertEqual(2, state.activeRecipeState.queuedCount, "same recipe queue count");
            AssertFailKey(cooking.TryCompleteRecipe(), "cooking.not_ready", "recipe early complete");
            AssertEqual(0, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "food_test"), "no output before ready");
            clock.AdvanceSeconds(30);
            AssertSuccess(cooking.TryCompleteRecipe(), "recipe complete synthetic");
            AssertEqual(1, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "food_test"), "recipe output after ready");
            AssertSuccess(cooking.TryAccelerateActiveRecipe(30), "recipe direct acceleration");
            AssertSuccess(cooking.TryCompleteRecipe(), "recipe complete after direct acceleration");
            AssertEqual(2, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "food_test"), "recipe output after accelerated second queue");

            PlayerRuntimeState speedupState = new PlayerRuntimeState();
            ManualClock speedupClock = new ManualClock(new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
            InventoryService speedupInventory = new InventoryService(catalog, speedupState);
            CookingService speedupCooking = new CookingService(catalog, speedupState, speedupInventory, speedupClock);
            AssertSuccess(speedupInventory.TryAddItem("fish_test", 1), "speedup fish add");
            AssertSuccess(speedupInventory.TryAddItem("fish_test2", 1), "speedup fish2 add");
            AssertSuccess(speedupInventory.TryAddItem("ticket_speedup_10m", 1), "speedup ticket add");
            AssertSuccess(speedupCooking.TryStartRecipe("recipe_test"), "speedup recipe start");
            AssertFailKey(speedupCooking.TryUseSpeedupItem("missing_ticket", 30), "cooking.unknown_speedup_item", "missing speedup ticket");
            AssertEqual(1, CountBagItem(new BagQueryService(catalog, speedupState).BuildSnapshot(new BagQueryOptions()), "ticket_speedup_10m"), "missing ticket should not consume speedup item");
            AssertSuccess(speedupCooking.TryUseSpeedupItem("ticket_speedup_10m", 30), "speedup ticket use");
            AssertEqual(0, CountBagItem(new BagQueryService(catalog, speedupState).BuildSnapshot(new BagQueryOptions()), "ticket_speedup_10m"), "speedup ticket consumed");
            AssertSuccess(speedupCooking.TryCompleteRecipe(), "recipe complete after speedup ticket");
            AssertEqual(1, CountBagItem(new BagQueryService(catalog, speedupState).BuildSnapshot(new BagQueryOptions()), "food_test"), "speedup output after ticket");

            PlayerRuntimeState outputFailState = new PlayerRuntimeState();
            InventoryService outputFailInventory = new InventoryService(catalog, outputFailState);
            CookingService outputFailCooking = new CookingService(catalog, outputFailState, outputFailInventory, clock);
            AssertSuccess(outputFailInventory.TryAddItem("fish_test", 1), "output fail fish add");
            AssertSuccess(outputFailInventory.TryAddItem("fish_test2", 1), "output fail fish2 add");
            AssertFailKey(outputFailCooking.TryStartRecipe("recipe_disabled_output"), "cooking.output_item_disabled", "disabled output recipe should fail before consuming ingredient");
            AssertTrue(outputFailState.activeRecipeState == null, "disabled output recipe should not become active");
            AssertEqual(1, CountBagItem(new BagQueryService(catalog, outputFailState).BuildSnapshot(new BagQueryOptions()), "fish_test"), "disabled output recipe should keep ingredient");

            PlayerRuntimeState outputRetryState = new PlayerRuntimeState();
            InventoryService outputRetryInventory = new InventoryService(catalog, outputRetryState);
            CookingService outputRetryCooking = new CookingService(catalog, outputRetryState, outputRetryInventory, clock);
            AssertSuccess(outputRetryInventory.TryAddItem("fish_test", 2), "output retry fish add");
            AssertSuccess(outputRetryInventory.TryAddItem("fish_test2", 2), "output retry fish2 add");
            AssertSuccess(outputRetryCooking.TryStartRecipe("recipe_test"), "output retry recipe start");
            AssertTrue(catalog.ItemsById.TryGetValue("food_test", out ItemDefinition food), "food item should exist for output retry");
            clock.AdvanceSeconds(30);
            food.IsEnabled = false;
            AssertFailKey(outputRetryCooking.TryCompleteRecipe(), "cooking.output_apply_failed", "disabled output after start should fail complete");
            AssertTrue(outputRetryState.activeRecipeState != null, "active recipe should be kept after output failure");
            food.IsEnabled = true;
            AssertSuccess(outputRetryCooking.TryCompleteRecipe(), "output retry recipe complete after config fixed");

            PlayerRuntimeState expOverflowState = new PlayerRuntimeState
            {
                crewExp = long.MaxValue
            };
            InventoryService expOverflowInventory = new InventoryService(catalog, expOverflowState);
            CookingService expOverflowCooking = new CookingService(catalog, expOverflowState, expOverflowInventory, clock);
            AssertSuccess(expOverflowInventory.TryAddItem("fish_test", 2), "crew exp overflow fish add");
            AssertSuccess(expOverflowInventory.TryAddItem("fish_test2", 2), "crew exp overflow fish2 add");
            AssertSuccess(expOverflowCooking.TryStartRecipe("recipe_test"), "crew exp overflow recipe start");
            clock.AdvanceSeconds(30);
            AssertFailKey(expOverflowCooking.TryCompleteRecipe(), "cooking.crew_exp_overflow", "crew exp overflow complete");
            AssertTrue(expOverflowState.activeRecipeState != null, "active recipe should be kept after crew exp overflow");
            AssertEqual(long.MaxValue, expOverflowState.crewExp, "crew exp should stay unchanged after overflow");
        }

        private static void RunCollectionContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog();
            PlayerRuntimeState state = new PlayerRuntimeState();
            InventoryService inventory = new InventoryService(catalog, state);
            CollectionService collection = new CollectionService(catalog, state, inventory);

            AssertFailKey(collection.TryRegisterDiscovery("missing_item"), "collection.unknown_item", "unknown collection item");
            AssertFailKey(collection.TryRegisterDiscovery("disabled_item"), "collection.item_disabled", "disabled collection item");
            AssertFailKey(collection.TryRegisterFishDiscovery("missing_fish"), "collection.unknown_fish", "unknown fish discovery");
            AssertFailKey(collection.TryRegisterFishDiscovery("fish_test_disabled"), "collection.fish_disabled", "disabled fish discovery");
            AssertFailKey(collection.TryRegisterFishDiscovery("fish_disabled_item"), "collection.item_disabled", "fish discovery with disabled item");
            AssertFailKey(collection.TryRegisterCatalogDiscovery("missing_catalog_id"), "collection.unknown_catalog_id", "unknown catalog discovery id");
            AssertEqual(0, state.discoveredCollectionItemIds.Count, "no discovery after failed collection cases");

            AssertSuccess(collection.TryRegisterCatalogDiscovery("fish_test"), "valid fish item catalog discovery");
            AssertTrue(state.discoveredCollectionItemIds.Contains("fish_test"), "catalog item discovery should record itemId");
            AssertSuccess(collection.TryRegisterFishDiscovery("fish_test_g1"), "valid fish discovery");
            AssertSuccess(collection.TryRegisterCatalogDiscovery("fish_test_g1"), "duplicate valid fish catalog discovery");
            AssertEqual(2, state.discoveredCollectionItemIds.Count, "item and grade discovery count");
        }

        private static void RunCollectionRewardSeamTests(TestReport report)
        {
            BalanceCatalog catalog = BuildSyntheticCatalog(
                collectionRewardsCsv: string.Join("\n",
                    CollectionRewardHeader(),
                    "reward_discovery_currency,fish_test,discovery,1,softCurrency,25,,,claim_fish_discovery,1,TRUE,Discovery currency",
                    "reward_count_item,fish_test,count,3,,0,food_test,1,claim_fish_count,2,TRUE,Count item",
                    "reward_disabled,fish_test,discovery,1,softCurrency,10,,,claim_disabled,3,FALSE,Disabled reward",
                    "reward_unsupported_currency,fish_test,discovery,1,prismPearl,10,,,claim_unsupported_currency,4,TRUE,Unsupported currency",
                    "reward_apply_fail,fish_test,discovery,1,softCurrency,40,disabled_item,1,claim_apply_fail,5,TRUE,Rollback path"));
            PlayerRuntimeState state = new PlayerRuntimeState
            {
                softCurrency = 100
            };
            InventoryService inventory = new InventoryService(catalog, state);
            CollectionService collection = new CollectionService(catalog, state, inventory);
            BagQueryService bagQuery = new BagQueryService(catalog, state);

            AssertFailKey(collection.TryClaimCollectionReward("missing_reward"), "collection.unknown_reward", "unknown collection reward");
            AssertFailKey(collection.TryClaimCollectionReward("reward_disabled"), "collection.reward_disabled", "disabled collection reward");
            AssertFailKey(collection.TryClaimCollectionReward("reward_discovery_currency"), "collection.not_discovered", "reward before discovery");
            AssertEqual(0, state.claimedRewardIds.Count, "claim count before valid reward");

            AssertSuccess(inventory.TryAddItem("fish_test", 2), "collection reward fish gain x2");
            AssertSuccess(collection.TryRegisterDiscovery("fish_test"), "collection reward item discovery");
            AssertFailKey(collection.TryClaimCollectionReward("reward_unsupported_currency"), "collection.unsupported_reward_currency", "unsupported collection reward currency");
            AssertEqual(100L, state.softCurrency, "currency unchanged after unsupported collection reward currency");
            AssertFalse(state.claimedRewardIds.Contains("claim_unsupported_currency"), "unsupported currency reward should not be claimed");
            AssertFailKey(collection.TryClaimCollectionReward("reward_count_item"), "collection.condition_not_met", "count reward before required acquisition");
            AssertEqual(0, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "food_test"), "no reward item before count condition");

            ServiceResult discoveryReward = collection.TryClaimCollectionReward("reward_discovery_currency");
            AssertSuccess(discoveryReward, "discovery reward claim");
            AssertEqual(25L, discoveryReward.CurrencyDelta, "discovery reward currency delta");
            AssertEqual(125L, state.softCurrency, "currency after discovery reward");
            AssertTrue(state.claimedRewardIds.Contains("claim_fish_discovery"), "discovery reward claimId recorded");

            AssertFailKey(collection.TryClaimCollectionReward("reward_discovery_currency"), "collection.already_claimed", "duplicate discovery reward claim");
            AssertEqual(125L, state.softCurrency, "currency unchanged after duplicate reward");

            AssertSuccess(inventory.TryAddItem("fish_test", 1), "collection reward fish gain x1");
            ServiceResult countReward = collection.TryClaimCollectionReward("reward_count_item");
            AssertSuccess(countReward, "count reward claim");
            AssertEqual(1, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "food_test"), "count reward item added");
            AssertTrue(state.claimedRewardIds.Contains("claim_fish_count"), "count reward claimId recorded");

            AssertFailKey(collection.TryClaimCollectionReward("reward_apply_fail"), "collection.reward_apply_failed_rolled_back", "collection reward rollback on item apply failure");
            AssertEqual(125L, state.softCurrency, "currency rollback after failed collection reward");
            AssertEqual(0, CountBagItem(bagQuery.BuildSnapshot(new BagQueryOptions()), "disabled_item"), "failed reward item not added");
            AssertFalse(state.claimedRewardIds.Contains("claim_apply_fail"), "failed reward should not be claimed");
        }

        #endregion

        #region Integration Helpers

        private static string FirstEnabledGeneratedFishItemId(BalanceCatalog catalog)
        {
            if (catalog == null)
            {
                return string.Empty;
            }

            foreach (FishDefinition fish in catalog.FishById.Values)
            {
                if (fish == null ||
                    !fish.IsEnabled ||
                    string.IsNullOrEmpty(fish.ItemId) ||
                    !catalog.TryGetItem(fish.ItemId, out ItemDefinition item) ||
                    item == null ||
                    !item.IsEnabled)
                {
                    continue;
                }

                return fish.ItemId;
            }

            return string.Empty;
        }

        private static RecipeDefinition FirstEnabledRecipeUsingItem(BalanceCatalog catalog, string itemId)
        {
            if (catalog == null || string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            foreach (RecipeDefinition recipe in catalog.RecipesById.Values)
            {
                if (recipe == null || !recipe.IsEnabled)
                {
                    continue;
                }

                if (recipe.InputItemId == itemId || recipe.InputItemId2 == itemId)
                {
                    return recipe;
                }
            }

            return null;
        }

        private static int CountRecipeInputRequirement(RecipeDefinition recipe, string itemId)
        {
            if (recipe == null || string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            int required = 0;
            if (recipe.InputItemId == itemId)
            {
                required += recipe.InputCount;
            }

            if (recipe.InputItemId2 == itemId)
            {
                required += recipe.InputCount2;
            }

            return required;
        }

        private static void EnsureRecipeInputs(FisherRuntimeContext context, RecipeDefinition recipe, string caughtItemId)
        {
            EnsureRecipeInput(context, recipe.InputItemId, recipe.InputCount, caughtItemId);
            EnsureRecipeInput(context, recipe.InputItemId2, recipe.InputCount2, caughtItemId);
        }

        private static void EnsureRecipeInput(FisherRuntimeContext context, string inputItemId, int requiredCount, string caughtItemId)
        {
            if (context == null ||
                string.IsNullOrEmpty(inputItemId) ||
                inputItemId == caughtItemId ||
                requiredCount <= 0)
            {
                return;
            }

            int owned = context.InventoryService.CountItem(inputItemId);
            if (owned >= requiredCount)
            {
                return;
            }

            AssertSuccess(
                context.InventoryService.TryAddItem(inputItemId, requiredCount - owned),
                "fixture should supply non-caught recipe input: " + inputItemId);
        }

        #endregion

        #region RMS Reference Helpers

        private static List<T> LoadAssets<T>(string searchRoot) where T : UnityEngine.Object
        {
            string[] guids = string.IsNullOrWhiteSpace(searchRoot)
                ? AssetDatabase.FindAssets("t:" + typeof(T).Name)
                : AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { searchRoot });
            List<T> assets = new List<T>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    continue;
                }

                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            assets.Sort((left, right) => string.CompareOrdinal(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right)));
            return assets;
        }

        private static void ValidateGeneratedFishReferencesRms(
            BalanceCatalog catalog,
            Dictionary<string, RMS.Data.FishData> fishById,
            TestReport report)
        {
            HashSet<string> checkedItemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FishDefinition fish in catalog.FishById.Values)
            {
                if (fish == null || !fish.IsEnabled || string.IsNullOrEmpty(fish.ItemId))
                {
                    continue;
                }

                if (!checkedItemIds.Add(fish.ItemId))
                {
                    continue;
                }

                if (!fishById.ContainsKey(fish.ItemId))
                {
                    report.Warn("generated fish.csv itemId has no matching RMS FishData.FishId yet: " + fish.ItemId);
                }
            }
        }

        private static bool HasEnabledRecipeInput(BalanceCatalog catalog, string itemId)
        {
            foreach (RecipeDefinition recipe in catalog.RecipesById.Values)
            {
                if (recipe != null && recipe.IsEnabled && (recipe.InputItemId == itemId || recipe.InputItemId2 == itemId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCollectionRewardForItemOrGroup(BalanceCatalog catalog, string itemId, string rewardGroupId)
        {
            foreach (CollectionRewardDefinition reward in catalog.CollectionRewardsById.Values)
            {
                if (reward == null || !reward.IsEnabled)
                {
                    continue;
                }

                if (reward.ItemId == itemId || reward.RewardGroupId == rewardGroupId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateYwjInventoryItemMap(BalanceCsvTable table, BalanceCatalog catalog)
        {
            HashSet<int> legacyIntIds = new HashSet<int>();
            HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> serverIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (BalanceCsvRow row in table.Rows)
            {
                int legacyIntId = RequiredInt(row, "legacyIntId");
                string itemId = RequiredCell(row, "itemId");
                string serverId = RequiredCell(row, "serverId");
                string itemCategory = RequiredCell(row, "itemCategory");
                string owner = RequiredCell(row, "owner");
                bool isRuntimeLinked = RequiredBool(row, "isRuntimeLinked");
                bool isEnabled = RequiredBool(row, "isEnabled");

                AssertTrue(legacyIntId > 0, row.Location + " legacyIntId must be positive.");
                AssertTrue(legacyIntIds.Add(legacyIntId), row.Location + " duplicate legacyIntId: " + legacyIntId);
                AssertTrue(itemIds.Add(itemId), row.Location + " duplicate itemId: " + itemId);
                AssertTrue(serverIds.Add(serverId), row.Location + " duplicate serverId: " + serverId);
                AssertEqual("YWJ_PlayerData", owner, row.Location + " owner");
                AssertFalse(isRuntimeLinked, row.Location + " must stay runtime-unlinked until PlayerData bridge contract is finalized.");

                AssertTrue(catalog.ItemsById.TryGetValue(itemId, out ItemDefinition item), row.Location + " itemId missing from generated items.csv: " + itemId);
                AssertEqual(itemId, serverId, row.Location + " serverId should match itemId for current PlayFab/common-id contract.");
                AssertEqual(item.Category, itemCategory, row.Location + " itemCategory");
                AssertEqual(item.IsEnabled, isEnabled, row.Location + " isEnabled should mirror items.csv.");
            }

            foreach (string itemId in catalog.ItemsById.Keys)
            {
                AssertTrue(itemIds.Contains(itemId), "ywj_inventory_item_map is missing generated itemId: " + itemId);
            }

            AssertEqual(catalog.ItemsById.Count, itemIds.Count, "ywj_inventory_item_map coverage count");
        }

        private static void RunCrewFragmentExternalContractTests(TestReport report)
        {
            BalanceCatalog catalog = BuildGeneratedCatalogForTool("crew fragment external contract check");
            BalanceCsvTable inventoryMap = ReadGeneratedContractTable(
                "ywj_inventory_item_map",
                "ywj_inventory_item_map.csv",
                "legacyIntId",
                "itemId",
                "serverId",
                "itemCategory",
                "owner",
                "isRuntimeLinked",
                "isEnabled");
            ValidateYwjInventoryItemMap(inventoryMap, catalog);

            Dictionary<string, BalanceCsvRow> inventoryRowsByItemId = MapRowsByCell(inventoryMap, "itemId");
            JObject fragmentMap = JObject.Parse(ReadProjectText("Assets/Resources/05_CSH/RuntimeCatalog/FragmentList.json"));
            FisherUiArtProfile artProfile = AssetDatabase.LoadAssetAtPath<FisherUiArtProfile>("Assets/Resources/05_CSH/UI/FisherUiArtProfile.asset");
            AssertTrue(artProfile != null, "FisherUiArtProfile asset should exist for crew fragment icons");

            List<string> crewFragmentIds = new List<string>();
            foreach (ItemDefinition item in catalog.ItemsById.Values)
            {
                if (item != null &&
                    item.IsEnabled &&
                    item.ItemId.StartsWith("fragment_Crew_", StringComparison.Ordinal))
                {
                    crewFragmentIds.Add(item.ItemId);
                }
            }

            crewFragmentIds.Sort(StringComparer.Ordinal);
            AssertEqual(10, crewFragmentIds.Count, "enabled crew fragment count");

            for (int i = 0; i < crewFragmentIds.Count; i++)
            {
                string itemId = crewFragmentIds[i];
                AssertTrue(catalog.ItemsById.TryGetValue(itemId, out ItemDefinition item), "crew fragment should exist in generated catalog: " + itemId);
                AssertEqual("Ticket", item.Category, "crew fragment category: " + itemId);
                AssertEqual("Recruit", item.SourceType, "crew fragment source type: " + itemId);
                AssertEqual("crew_fragment", item.CookTag, "crew fragment cookTag: " + itemId);
                AssertFalse(ItemSellPolicy.IsSellable(item), "crew fragment should not be sellable by CSH inventory policy: " + itemId);

                AssertTrue(inventoryRowsByItemId.TryGetValue(itemId, out BalanceCsvRow mapRow), "ywj_inventory_item_map should include crew fragment: " + itemId);
                AssertEqual(itemId, RequiredCell(mapRow, "serverId"), "crew fragment YWJ serverId: " + itemId);
                AssertEqual("Ticket", RequiredCell(mapRow, "itemCategory"), "crew fragment YWJ itemCategory: " + itemId);
                AssertEqual("YWJ_PlayerData", RequiredCell(mapRow, "owner"), "crew fragment YWJ owner: " + itemId);
                AssertFalse(RequiredBool(mapRow, "isRuntimeLinked"), "crew fragment YWJ runtime link should stay false until bridge contract changes: " + itemId);
                AssertTrue(RequiredBool(mapRow, "isEnabled"), "crew fragment YWJ map should be enabled: " + itemId);

                string expectedCrewId = itemId.Substring("fragment_".Length);
                AssertEqual(expectedCrewId, NormalizeId(fragmentMap.Value<string>(itemId)), "FragmentList crew id: " + itemId);
                AssertTrue(artProfile.FindItemIcon(itemId) != null, "FisherUiArtProfile should bind an icon for crew fragment: " + itemId);
            }

            report.Add("선원 조각 외부 계약: " + crewFragmentIds.Count + " fragments OK");
        }

        private static Dictionary<string, BalanceCsvRow> MapRowsByCell(BalanceCsvTable table, string header)
        {
            Dictionary<string, BalanceCsvRow> rows = new Dictionary<string, BalanceCsvRow>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                string value = RequiredCell(row, header);
                AssertTrue(!rows.ContainsKey(value), table.TableName + " duplicate " + header + ": " + value);
                rows.Add(value, row);
            }

            return rows;
        }

        private static void ValidateRmsStageIdMap(BalanceCsvTable table, TestReport report)
        {
            List<RMS.Data.StageData> stageAssets = LoadAssets<RMS.Data.StageData>("Assets/03_Data/01_RMS");
            Dictionary<string, RMS.Data.StageData> stageById = new Dictionary<string, RMS.Data.StageData>(StringComparer.Ordinal);

            for (int i = 0; i < stageAssets.Count; i++)
            {
                RMS.Data.StageData stage = stageAssets[i];
                if (stage == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(stage);
                string stageId = NormalizeId(stage.StageId);
                if (string.IsNullOrEmpty(stageId))
                {
                    report.Warn(path + " has empty StageId. Server stage map cannot reference it yet.");
                    continue;
                }

                if (stageById.ContainsKey(stageId))
                {
                    report.Warn(path + " duplicates RMS StageId: " + stageId);
                    continue;
                }

                stageById.Add(stageId, stage);
            }

            AssertTrue(stageById.Count > 0, "RMS StageData assets should exist for stageId mapping.");

            int lastStageOrder = 0;
            HashSet<int> stageOrders = new HashSet<int>();
            HashSet<string> mappedStageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BalanceCsvRow row in table.Rows)
            {
                int stageOrder = RequiredInt(row, "stageOrder");
                string stageId = RequiredCell(row, "stageId");
                int worldId = RequiredInt(row, "worldId");
                bool isBossStage = RequiredBool(row, "isBossStage");
                string bossId = NormalizeId(row.GetString("bossId"));
                string nextStageId = NormalizeId(row.GetString("nextStageId"));
                bool isRuntimeLinked = RequiredBool(row, "isRuntimeLinked");
                bool isEnabled = RequiredBool(row, "isEnabled");

                AssertTrue(stageOrder > 0, row.Location + " stageOrder must be positive.");
                AssertTrue(stageOrder > lastStageOrder, row.Location + " stageOrder must increase monotonically.");
                AssertTrue(stageOrders.Add(stageOrder), row.Location + " duplicate stageOrder: " + stageOrder);
                AssertTrue(mappedStageIds.Add(stageId), row.Location + " duplicate stageId: " + stageId);
                AssertFalse(isRuntimeLinked, row.Location + " must stay runtime-unlinked until server progression contract is finalized.");
                AssertTrue(isEnabled, row.Location + " RMS StageData has no disabled flag yet so mapped rows should remain enabled.");
                AssertEqual(ExtractWorldId(stageId), worldId, row.Location + " worldId");

                AssertTrue(stageById.TryGetValue(stageId, out RMS.Data.StageData stage), row.Location + " stageId missing from RMS StageData: " + stageId);
                AssertEqual(stage.IsBossStage, isBossStage, row.Location + " isBossStage");

                string expectedBossId = stage.IsBossStage && stage.BossData != null ? NormalizeId(stage.BossData.BossId) : string.Empty;
                string expectedNextStageId = stage.NextStage != null ? NormalizeId(stage.NextStage.StageId) : string.Empty;
                AssertEqual(expectedBossId, bossId, row.Location + " bossId");
                AssertEqual(expectedNextStageId, nextStageId, row.Location + " nextStageId");

                if (!string.IsNullOrEmpty(nextStageId))
                {
                    AssertTrue(stageById.ContainsKey(nextStageId), row.Location + " nextStageId missing from RMS StageData: " + nextStageId);
                }

                if (stage.IsBossStage && stage.BossData != null)
                {
                    string unlockStageId = NormalizeId(stage.BossData.UnlockStageId);
                    if (!string.IsNullOrEmpty(unlockStageId))
                    {
                        AssertTrue(stageById.ContainsKey(unlockStageId), row.Location + " BossData.UnlockStageId missing from RMS StageData: " + unlockStageId);
                    }
                }

                lastStageOrder = stageOrder;
            }

            foreach (string stageId in stageById.Keys)
            {
                AssertTrue(mappedStageIds.Contains(stageId), "rms_stage_id_map is missing RMS StageData.StageId: " + stageId);
            }

            AssertEqual(stageById.Count, mappedStageIds.Count, "rms_stage_id_map coverage count");
        }

        private static BalanceCsvTable ReadGeneratedContractTable(string tableName, string fileName, params string[] requiredHeaders)
        {
            BalanceCsvTable table = BalanceCsvTable.FromText(tableName, ReadGenerated(fileName));
            AssertTrue(table.Headers.Count > 0, tableName + " is empty or has no header row.");
            for (int i = 0; i < requiredHeaders.Length; i++)
            {
                AssertTrue(table.HasHeader(requiredHeaders[i]), tableName + " missing required header: " + requiredHeaders[i]);
            }

            AssertTrue(table.Rows.Count > 0, tableName + " should contain at least one row.");
            return table;
        }

        private static string RequiredCell(BalanceCsvRow row, string header)
        {
            string value = NormalizeId(row.GetString(header));
            AssertTrue(!string.IsNullOrEmpty(value), row.Location + " missing required value: " + header);
            return value;
        }

        private static int RequiredInt(BalanceCsvRow row, string header)
        {
            AssertTrue(row.TryGetInt(header, out int value), row.Location + " invalid int: " + header);
            return value;
        }

        private static bool RequiredBool(BalanceCsvRow row, string header)
        {
            AssertTrue(row.TryGetBool(header, out bool value), row.Location + " invalid bool: " + header);
            return value;
        }

        private static int ExtractWorldId(string stageId)
        {
            int separatorIndex = stageId.IndexOf('-', StringComparison.Ordinal);
            if (separatorIndex < 0 && string.Equals(stageId, "multiplay_arena", StringComparison.Ordinal))
            {
                return 0;
            }

            AssertTrue(separatorIndex > 0, "stageId must include world prefix: " + stageId);
            string rawWorldId = stageId.Substring(0, separatorIndex);
            AssertTrue(int.TryParse(rawWorldId, out int worldId), "stageId world prefix must be int: " + stageId);
            return worldId;
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        #endregion

        #region Fixtures

        private static BalanceCatalog BuildSyntheticCatalog(
            string itemsCsv = null,
            string fishCsv = null,
            string shopCsv = null,
            string premiumCurrencyProductsCsv = null,
            string recipesCsv = null,
            string collectionRewardsCsv = null,
            string economyParamsCsv = null)
        {
            BalanceBuildResult build = BalanceCatalogBuilder.Build(CreateSyntheticCsvSet(
                itemsCsv: itemsCsv,
                fishCsv: fishCsv,
                shopCsv: shopCsv,
                premiumCurrencyProductsCsv: premiumCurrencyProductsCsv,
                recipesCsv: recipesCsv,
                collectionRewardsCsv: collectionRewardsCsv,
                economyParamsCsv: economyParamsCsv));
            AssertTrue(build.Success, "synthetic catalog should build. errors=" + string.Join(" | ", build.Errors));
            return build.Catalog;
        }

        private static BalanceCsvSet CreateSyntheticCsvSet(
            string itemsCsv = null,
            string fishCsv = null,
            string shopCsv = null,
            string premiumCurrencyProductsCsv = null,
            string recipesCsv = null,
            string collectionRewardsCsv = null,
            string economyParamsCsv = null)
        {
            return new BalanceCsvSet
            {
                ItemsCsv = itemsCsv ?? DefaultItemsCsv(),
                FishCsv = fishCsv ?? DefaultFishCsv(),
                ShopItemsCsv = shopCsv ?? HeaderOnly(ShopHeader()),
                PremiumCurrencyProductsCsv = premiumCurrencyProductsCsv ?? HeaderOnly(PremiumCurrencyProductHeader()),
                RecipesCsv = recipesCsv ?? DefaultRecipesCsv(),
                CollectionRewardsCsv = collectionRewardsCsv ?? HeaderOnly(CollectionRewardHeader()),
                EconomyParamsCsv = economyParamsCsv ?? DefaultEconomyParamsCsv(),
                StackRulesCsv = DefaultStackRulesCsv()
            };
        }

        private static string DefaultItemsCsv()
        {
            return string.Join("\n",
                ItemHeader(),
                "fish_test,테스트물고기,Fish,Common,Fishing,10,TRUE,2,fish,1,TRUE,OK",
                "fish_test2,테스트물고기2,Fish,Common,Fishing,8,TRUE,2,fish,2,TRUE,OK",
                "food_test,테스트요리,Food,Uncommon,Cooking,50,TRUE,99,meal,2,TRUE,OK",
                "disabled_item,비활성아이템,Material,Common,Shop,10,TRUE,99,,3,FALSE,Disabled",
                "mat_high_test,고급재료테스트,HighGradeMaterial,Rare,Reward,999,TRUE,99,upgrade,4,TRUE,Must not sell",
                "ticket_speedup_10m,10분가속권,Ticket,Uncommon,BM,0,TRUE,99,ticket,4,TRUE,Speedup",
                "unique_test,고유아이템,Special,Rare,Reward,0,FALSE,1,,4,TRUE,Unique");
        }

        private static string DefaultFishCsv()
        {
            return string.Join("\n",
                FishHeader(),
                "fish_test_g1,fish_test,1,5,8,100,3,shore_common,FALSE,TRUE,OK",
                "fish_test_g2,fish_test,2,6,9,50,6,shore_common,FALSE,TRUE,OK",
                "fish_test_g3,fish_test,3,7,10,20,10,shore_common,FALSE,TRUE,OK",
                "fish_test2_g1,fish_test2,1,5,8,100,3,shore_common,FALSE,TRUE,OK",
                "fish_test2_g2,fish_test2,2,6,9,50,6,shore_common,FALSE,TRUE,OK",
                "fish_test2_g3,fish_test2,3,7,10,20,10,shore_common,FALSE,TRUE,OK",
                "fish_test_disabled,fish_test,1,5,8,100,3,shore_common,FALSE,FALSE,Disabled fish",
                "fish_disabled_item,disabled_item,1,5,8,100,3,shore_common,FALSE,TRUE,Disabled item");
        }

        private static string DefaultRecipesCsv()
        {
            return string.Join("\n",
                RecipeHeader(),
                "recipe_test,fish_test,1,fish_test2,1,30,food_test,1,5,stage>=1,TRUE,OK",
                "recipe_disabled_output,fish_test,1,fish_test2,1,0,disabled_item,1,1,stage>=1,TRUE,Disabled output");
        }

        private static string DefaultEconomyParamsCsv()
        {
            return string.Join("\n",
                "key,value,valueType,scope,isEnabled,notes",
                "startingCurrency,100,long,player,TRUE,Test",
                "initial_bag_capacity,1,int,inventory,TRUE,Test",
                "bag_capacity_step,2,int,inventory,TRUE,Test",
                "bag_capacity_max,5,int,inventory,TRUE,Test",
                "bag_capacity_gold_cost_base,100,int64,inventory,TRUE,Test",
                "speedup_ticket_seconds,600,int,cooking,TRUE,Test");
        }

        private static string DefaultStackRulesCsv()
        {
            return string.Join("\n",
                "category,defaultMaxStack,overflowPolicy,isEnabled,notes",
                "Fish,99,newStack,TRUE,Test",
                "Food,99,newStack,TRUE,Test",
                "Material,99,newStack,TRUE,Test",
                "UpgradeMaterial,99,newStack,TRUE,Test",
                "HighGradeMaterial,99,newStack,TRUE,Test",
                "Ticket,99,newStack,TRUE,Test",
                "Special,1,block,TRUE,Test");
        }

        private static string HeaderOnly(string header)
        {
            return header + "\n";
        }

        private static string ItemHeader()
        {
            return "itemId,displayNameKo,category,rarity,sourceType,sellPrice,stackable,maxStack,cookTag,sortOrder,isEnabled,notes";
        }

        private static string FishHeader()
        {
            return "fishId,itemId,grade,minSizeCm,maxSizeCm,baseWeight,baseExp,dropGroup,isBoss,isEnabled,notes";
        }

        private static string ShopHeader()
        {
            return "shopItemId,category,priceType,priceAmount,rewardItemId,rewardCount,unlockCondition,sortOrder,isEnabled,notes,visibilityCondition";
        }

        private static string PremiumCurrencyProductHeader()
        {
            return "productId,cashAmount,prismPearlAmount,sortOrder,isEnabled,notes";
        }

        private static string RecipeHeader()
        {
            return "recipeId,inputItemId,inputCount,inputItemId2,inputCount2,durationSec,outputItemId,outputCount,crewExp,unlockCondition,isEnabled,notes";
        }

        private static string CollectionRewardHeader()
        {
            return "rewardId,itemId,conditionType,conditionValue,rewardCurrency,rewardAmount,rewardItemId,rewardItemCount,claimId,sortOrder,isEnabled,notes";
        }

        private static int CountBagItem(List<BagItemView> bag, string itemId)
        {
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].ItemId == itemId)
                {
                    return bag[i].Count;
                }
            }

            return 0;
        }

        private static BagItemView FindBagItem(List<BagItemView> bag, string itemId)
        {
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].ItemId == itemId)
                {
                    return bag[i];
                }
            }

            throw new InvalidOperationException("bag item not found: " + itemId);
        }

        #endregion

        #region Assertions

        private static void AssertBagOrder(List<BagItemView> bag, string[] expectedItemIds, string label)
        {
            AssertEqual(expectedItemIds.Length, bag.Count, label + " count");
            for (int i = 0; i < expectedItemIds.Length; i++)
            {
                AssertEqual(expectedItemIds[i], bag[i].ItemId, label + " index " + i);
            }
        }

        private static void AssertSuccess(ServiceResult result, string label)
        {
            if (result.Success)
            {
                return;
            }

            throw new InvalidOperationException(label + " failed: " + result.MessageKey + " / " + result.FailureReason);
        }

        private static void AssertFailKey(ServiceResult result, string expectedMessageKey, string label)
        {
            if (result == null)
            {
                throw new InvalidOperationException(label + " expected failure key=" + expectedMessageKey + " but result was null");
            }

            if (result.Success)
            {
                throw new InvalidOperationException(label + " expected failure key=" + expectedMessageKey + " but succeeded");
            }

            AssertEqual(expectedMessageKey, result.MessageKey, label + " failure key");
        }

        private static void AssertMessageKey(ServiceResult result, string expectedMessageKey, string label)
        {
            if (result == null)
            {
                throw new InvalidOperationException(label + " expected message key=" + expectedMessageKey + " but result was null");
            }

            AssertEqual(expectedMessageKey, result.MessageKey, label);
        }

        private static void AssertItemDelta(ServiceResult result, string itemId, int expectedCountDelta, string label)
        {
            if (result == null)
            {
                throw new InvalidOperationException(label + " result was null");
            }

            int actual = 0;
            for (int i = 0; i < result.ItemDeltas.Count; i++)
            {
                ItemDelta delta = result.ItemDeltas[i];
                if (delta.ItemId == itemId)
                {
                    actual += delta.CountDelta;
                }
            }

            AssertEqual(expectedCountDelta, actual, label);
        }

        private static void AssertAffected(ServiceResult result, string expectedId, string label)
        {
            if (result == null)
            {
                throw new InvalidOperationException(label + " result was null");
            }

            for (int i = 0; i < result.AffectedIds.Count; i++)
            {
                if (result.AffectedIds[i] == expectedId)
                {
                    return;
                }
            }

            throw new InvalidOperationException(label + " expected affected id=" + expectedId + " actual=" + string.Join(" | ", result.AffectedIds));
        }

        private static void AssertContains(List<string> values, string expectedPart, string label)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Contains(expectedPart))
                {
                    return;
                }
            }

            throw new InvalidOperationException(label + " expected to contain=" + expectedPart + " actual=" + string.Join(" | ", values));
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static MethodInfo RequireMethod(Type type, string methodName)
        {
            MethodInfo method = type == null ? null : type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("required method missing: " + methodName);
            }

            return method;
        }

        private static PropertyInfo RequireProperty(Type type, string propertyName)
        {
            PropertyInfo property = type == null ? null : type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                throw new InvalidOperationException("required property missing: " + propertyName);
            }

            return property;
        }

        private static bool InvokeBool(MethodInfo method, object target, params object[] args)
        {
            object value = method.Invoke(target, args);
            return value is bool result && result;
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(label + " expected=" + expected + " actual=" + actual);
            }
        }

        private static string ReadGenerated(string fileName, bool optional = false)
        {
            string path = Path.Combine(GeneratedFolder, fileName).Replace('\\', '/');
            if (!File.Exists(path))
            {
                if (optional)
                {
                    return null;
                }

                throw new FileNotFoundException("Generated CSV is missing.", path);
            }

            return File.ReadAllText(path);
        }

        private static string ReadProjectText(string projectRelativePath)
        {
            string path = projectRelativePath.Replace('\\', '/');
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Project file is missing.", path);
            }

            return File.ReadAllText(path);
        }

        #endregion

        #region Report

        private sealed class TestReport
        {
            private readonly List<string> lines = new List<string>();
            private int passed;
            private int failed;
            private int warned;

            public bool HasFailures => failed > 0;

            public void Add(string line)
            {
                lines.Add(line);
            }

            public void Warn(string line)
            {
                warned++;
                lines.Add("[WARN] " + line);
            }

            public void Run(string label, Action action)
            {
                try
                {
                    action();
                    passed++;
                    lines.Add("[PASS] " + label);
                }
                catch (Exception exception)
                {
                    failed++;
                    lines.Add("[FAIL] " + label);
                    lines.Add("  " + exception.Message);
                }
            }

            public string Build()
            {
                lines.Add("검증 그룹 통과: " + passed);
                lines.Add("검증 그룹 실패: " + failed);
                lines.Add("검증 경고: " + warned);
                lines.Add(failed == 0 ? "전체 결과: OK" : "전체 결과: FAIL");
                return string.Join("\n", lines);
            }
        }

        #endregion
    }
}
