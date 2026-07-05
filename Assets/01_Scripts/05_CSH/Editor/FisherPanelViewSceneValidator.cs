using System;
using System.Collections.Generic;
using Fisher.PlayerSystems;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fisher.PlayerSystems.Editor
{
    /// <summary>
    /// 열린 씬의 CSH Fisher 고정 UI 참조, 부모-자식 관계, 아트 적용 준비도를 읽기 전용으로 검사합니다.
    /// 씬/프리팹 수정, 오브젝트 생성, 에디터 변경 기록, dirty scene 표시를 하지 않습니다.
    /// </summary>
    public static class FisherPanelViewSceneValidator
    {
        private static readonly PanelContract[] PanelContracts =
        {
            new PanelContract("BagPanel", "가방", FisherSlotLayout.Bag, minSlots: 36, maxSlots: 0, requiredTabs: 5, requireHeaderAction: true, requireActionSheets: true, requireSubStatus: false),
            new PanelContract("CookingPanel", "요리", FisherSlotLayout.CookingProgress, minSlots: 3, maxSlots: 3, requiredTabs: 0, requireHeaderAction: false, requireActionSheets: true, recipeSlots: 17, ingredientSlots: 3, requireStatus: false, requireSubStatus: false, requireCookingDetailInfoRows: true),
            new PanelContract("ShopPanel", "상점", FisherSlotLayout.Shop, minSlots: 15, maxSlots: 0, requiredTabs: 4, requireHeaderAction: false, requireActionSheets: false, requireActions: false, requireSubStatus: false, requireStatus: false, requireShopPurchaseSheet: true),
            new PanelContract("CollectionPanel", "도감", FisherSlotLayout.Collection, minSlots: 30, maxSlots: 0, requiredTabs: 4, requireHeaderAction: false, requireActionSheets: false, requireActions: false, requireDetail: false, requireSubStatus: false)
        };

        private const string MainScenePath = "Assets/00_Scenes/00_MainScene.unity";
        private const string CshUiPrefabFolderPath = "Assets/02_Prefabs/05_CSH/UI Panel";

        private static readonly PrefabPanelContract[] PrefabPanelContracts =
        {
            new PrefabPanelContract("Inventory_Panel.prefab", 0),
            new PrefabPanelContract("Cooking_Panel.prefab", 1),
            new PrefabPanelContract("Shop_Panel.prefab", 2),
            new PrefabPanelContract("Collection_Panel.prefab", 3)
        };

        private static readonly string[] ToastPrefabFileNames =
        {
            "CookingCompletePanel.prefab",
            "FisherResultFeedbackToast.prefab"
        };

        [MenuItem("FISHER/UI/CSH View 참조 검사 (읽기 전용)")]
        public static void ValidateOpenScenesMenu()
        {
            string report = ValidateOpenScenes();
            Debug.Log("[FisherPanelViewSceneValidator]\n" + report);
            EditorUtility.DisplayDialog("CSH View 참조 검사", report, "확인");
        }

        /// <summary>
        /// Batchmode에서 MainScene을 열고 같은 validator를 저장 없이 실행합니다.
        /// executeMethod: Fisher.PlayerSystems.Editor.FisherPanelViewSceneValidator.RunMainSceneBatchMode
        /// </summary>
        public static void RunMainSceneBatchMode()
        {
            int exitCode = 1;
            try
            {
                Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    MainScenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Single);
                ValidationResult result = BuildValidationResult();
                Debug.Log("[FisherPanelViewSceneValidator Batch]\n" + result.Report);

                bool noErrors = result.CheckedScenes == 1 && result.Errors == 0;
                bool sceneStayedClean = scene.IsValid() && !scene.isDirty;
                if (!sceneStayedClean)
                {
                    Debug.LogError("[FisherPanelViewSceneValidator Batch] Validator dirtied the scene. Save was not performed.");
                }

                exitCode = noErrors && sceneStayedClean ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError("[FisherPanelViewSceneValidator Batch]\n" + exception);
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// 현재 열린 씬의 CSH UI 계약을 읽기 전용 문자열 보고서로 반환합니다.
        /// </summary>
        public static string ValidateOpenScenes()
        {
            return BuildValidationResult().Report;
        }

        private static ValidationResult BuildValidationResult()
        {
            List<string> lines = new List<string>
            {
                "=== CSH Fisher UI read-only validator ===",
                "Mode: read-only / no scene edit / no prefab edit / no auto repair / CSH prefab asset scan"
            };

            IssueCounts counts = new IssueCounts();
            int checkedScenes = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                checkedScenes++;
                lines.Add(string.Empty);
                lines.Add("Scene: " + scene.path);
                ValidateTopology(scene, counts, lines);
                ValidateGlobalP0ReadabilityHierarchy(scene, counts, lines);
                for (int j = 0; j < PanelContracts.Length; j++)
                {
                    ValidatePanel(scene, PanelContracts[j], counts, lines);
                }
            }

            ValidateCshUiPrefabAssets(counts, lines);

            lines.Add(string.Empty);
            lines.Add("Checked scenes: " + checkedScenes);
            lines.Add("ERROR: " + counts.Errors);
            lines.Add("WARN: " + counts.Warnings);
            lines.Add("INFO: " + counts.Infos);
            return new ValidationResult(string.Join("\n", lines), checkedScenes, counts.Errors, counts.Warnings, counts.Infos);
        }

        #region Scene Topology

        private static void ValidateTopology(Scene scene, IssueCounts counts, List<string> lines)
        {
            Transform cshRoot = FindByName(scene, "CSH");
            FisherRuntimeControlTower controlTower = FindFirstComponentInScene<FisherRuntimeControlTower>(scene);
            int panelRootCount = CountPanelRoots(scene);

            if (panelRootCount == 0 && controlTower == null)
            {
                AddInfo(counts, lines, "Topology: CSH panel roots not found in this scene.");
                return;
            }

            if (cshRoot == null)
            {
                AddWarn(counts, lines, "Topology: root GameObject named CSH not found. Prefab/Inspector ownership may be ambiguous.");
            }

            if (controlTower == null)
            {
                AddError(counts, lines, "Topology: FisherRuntimeControlTower missing. CSH prefab/control root must be pre-wired.");
                return;
            }

            AddInfo(counts, lines, "Topology: FisherRuntimeControlTower=" + GetPath(controlTower.transform));
            if (cshRoot != null && !controlTower.transform.IsChildOf(cshRoot))
            {
                AddWarn(counts, lines, "Topology: FisherRuntimeControlTower is not under CSH root.");
            }

            SerializedObject serializedTower = new SerializedObject(controlTower);
            RequireObjectReference(serializedTower, "_context", "Topology: FisherRuntimeContext", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_playerDataBridge", "Topology: FisherPlayerDataBridge", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_bagAdapter", "Topology: BagPanelAdapter", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_cookingAdapter", "Topology: CookingPanelAdapter", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_shopAdapter", "Topology: ShopPanelAdapter", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_collectionAdapter", "Topology: CollectionPanelAdapter", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_inventoryPanel", "Topology: Inventory Canvas Panel", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_cookingPanel", "Topology: Cooking Canvas Panel", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_shopPanel", "Topology: Shop Canvas Panel", counts, lines, required: true, cshRoot);
            RequireObjectReference(serializedTower, "_collectionPanel", "Topology: Collection Canvas Panel", counts, lines, required: true, cshRoot);

            FisherRuntimeContext context = serializedTower.FindProperty("_context")?.objectReferenceValue as FisherRuntimeContext;
            ValidateContextArtReference(context, counts, lines);
        }

        private static void RequireObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            string label,
            IssueCounts counts,
            List<string> lines,
            bool required,
            Transform expectedRoot)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            UnityEngine.Object value = property == null ? null : property.objectReferenceValue;
            if (value == null)
            {
                if (required)
                {
                    AddError(counts, lines, label + " missing.");
                }
                else
                {
                    AddWarn(counts, lines, label + " missing.");
                }

                return;
            }

            Transform valueTransform = ObjectTransform(value);
            if (expectedRoot != null && valueTransform != null && !valueTransform.IsChildOf(expectedRoot))
            {
                AddWarn(counts, lines, label + " is outside CSH root: " + GetPath(valueTransform));
                return;
            }

            AddInfo(counts, lines, label + "=" + (valueTransform == null ? value.name : GetPath(valueTransform)));
        }

        private static void ValidateContextArtReference(FisherRuntimeContext context, IssueCounts counts, List<string> lines)
        {
            if (context == null)
            {
                return;
            }

            SerializedObject serializedContext = new SerializedObject(context);
            FisherUiArtProfile directProfile = serializedContext.FindProperty("_uiArtProfile")?.objectReferenceValue as FisherUiArtProfile;
            bool autoLoad = serializedContext.FindProperty("_autoLoadUiArtProfile") == null ||
                            serializedContext.FindProperty("_autoLoadUiArtProfile").boolValue;
            string resourcePath = serializedContext.FindProperty("_uiArtProfileResourcePath") == null
                ? string.Empty
                : serializedContext.FindProperty("_uiArtProfileResourcePath").stringValue;

            if (directProfile != null)
            {
                AddInfo(counts, lines, "Art: Context UiArtProfile assigned=" + directProfile.name);
                ValidateArtProfile("Context", directProfile, counts, lines);
                return;
            }

            if (autoLoad)
            {
                AddInfo(counts, lines, "Art: Context UiArtProfile uses Resources fallback path=" + resourcePath);
                return;
            }

            AddWarn(counts, lines, "Art: Context UiArtProfile missing and auto-load is disabled.");
        }

        #endregion

        #region Panel Contract

        private static void ValidatePanel(Scene scene, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            Transform panelRoot = FindByName(scene, contract.PanelRootName);
            if (panelRoot == null)
            {
                AddError(counts, lines, contract.Label + ": " + contract.PanelRootName + " missing.");
                return;
            }

            ValidatePanelRoot(panelRoot, contract, counts, lines);
        }

        private static void ValidatePanelRoot(Transform panelRoot, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            Transform directViewRoot = panelRoot.Find("ViewRoot");
            if (directViewRoot == null)
            {
                AddError(counts, lines, contract.Label + ": " + contract.PanelRootName + "/ViewRoot missing. Runtime resolver expects this exact child.");
                return;
            }

            FisherPanelView view = directViewRoot.GetComponent<FisherPanelView>();
            if (view == null)
            {
                AddError(counts, lines, contract.Label + ": ViewRoot FisherPanelView missing.");
                return;
            }

            AddInfo(counts, lines, contract.Label + ": ViewRoot=" + GetPath(view.transform));
            ValidatePanelReferences(view, contract, counts, lines);
            ValidatePanelP0ReadabilityHierarchy(view, contract, counts, lines);
            ValidateSlotContract(view, contract, counts, lines);
            ValidateOptionalSlotPool(view.RecipeSlots, contract.RecipeSlots, contract.Label + ": RecipeSlots", counts, lines);
            ValidateOptionalSlotPool(view.IngredientSlots, contract.IngredientSlots, contract.Label + ": IngredientSlots", counts, lines);
            if (contract.PanelRootName == "CookingPanel")
            {
                ValidateCookingPanelP0Contract(view, contract, counts, lines);
            }

            ValidateActionSheets(view, contract, counts, lines);
            ValidatePanelArtReadiness(view, contract, counts, lines);
        }

        private static void ValidatePanelReferences(FisherPanelView view, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            Require(view.ViewRoot != null, contract.Label + ": ViewRoot reference", counts, lines);
            Require(view.HeaderRoot != null, contract.Label + ": HeaderRoot", counts, lines);
            if (contract.RequireTitleText)
            {
                Require(view.TitleText != null, contract.Label + ": TitleText", counts, lines);
            }
            else if (view.TitleText == null)
            {
                AddInfo(counts, lines, contract.Label + ": TitleText optional.");
            }

            if (contract.RequireStatus)
            {
                Require(view.StatusText != null, contract.Label + ": StatusText", counts, lines);
            }
            else if (view.StatusText == null)
            {
                AddInfo(counts, lines, contract.Label + ": StatusText optional.");
            }
            if (contract.RequireSubStatus)
            {
                Require(view.SubStatusText != null, contract.Label + ": SubStatusText", counts, lines);
            }
            else if (view.SubStatusText == null)
            {
                AddInfo(counts, lines, contract.Label + ": SubStatusText optional.");
            }
            Require(view.CategoryTabsRoot != null || contract.RequiredTabs == 0, contract.Label + ": CategoryTabsRoot", counts, lines);
            Require(view.GridRoot != null, contract.Label + ": GridRoot", counts, lines);
            Require(view.GridContent != null, contract.Label + ": GridContent", counts, lines);
            Require(view.GridLayout != null, contract.Label + ": GridLayout", counts, lines);
            Require(view.GridScrollRect != null, contract.Label + ": GridScrollRect", counts, lines);
            if (contract.RecipeSlots > 0)
            {
                Require(view.RecipeGridRoot != null, contract.Label + ": RecipeGridRoot", counts, lines);
                Require(view.RecipeGridContent != null, contract.Label + ": RecipeGridContent", counts, lines);
                Require(view.RecipeGridLayout != null, contract.Label + ": RecipeGridLayout", counts, lines);
                Require(view.RecipeGridScrollRect != null, contract.Label + ": RecipeGridScrollRect", counts, lines);
            }

            if (contract.IngredientSlots > 0)
            {
                Require(view.IngredientGridRoot != null, contract.Label + ": IngredientGridRoot", counts, lines);
                Require(view.IngredientGridContent != null, contract.Label + ": IngredientGridContent", counts, lines);
                Require(view.IngredientGridLayout != null, contract.Label + ": IngredientGridLayout", counts, lines);
            }

            if (contract.RequireDetail)
            {
                Require(view.DetailRoot != null, contract.Label + ": DetailRoot", counts, lines);
                Require(view.DetailSlot != null, contract.Label + ": DetailSlot", counts, lines);
                Require(view.DetailTitleText != null, contract.Label + ": DetailTitleText", counts, lines);
                Require(view.DetailMetaText != null, contract.Label + ": DetailMetaText", counts, lines);
                if (contract.PanelRootName == "CookingPanel")
                {
                    AddInfo(counts, lines, contract.Label + ": DetailBodyText optional; action bar carries selected cooking count.");
                }
                else if (contract.PanelRootName == "BagPanel")
                {
                    AddInfo(counts, lines, contract.Label + ": DetailBodyText optional; action buttons carry sale/use intent.");
                }
                else
                {
                    Require(view.DetailBodyText != null, contract.Label + ": DetailBodyText", counts, lines);
                }
            }
            else
            {
                AddInfo(counts, lines, contract.Label + ": Detail section optional.");
            }

            if (contract.RequireCookingDetailInfoRows)
            {
                Require(view.CookingDetailInfoRows != null, contract.Label + ": CookingDetailInfoRows", counts, lines);
                Require(view.CookingRewardExpPanel != null, contract.Label + ": CookingRewardExpPanel", counts, lines);
                Require(view.CookingRewardExpText != null, contract.Label + ": CookingRewardExpText", counts, lines);
                Require(view.CookingSellPricePanel != null, contract.Label + ": CookingSellPricePanel", counts, lines);
                Require(view.CookingSellPriceText != null, contract.Label + ": CookingSellPriceText", counts, lines);
            }

            if (contract.RequireShopPurchaseSheet)
            {
                ValidateShopPurchaseSheet(view, contract, counts, lines);
            }

            if (contract.RequireActions)
            {
                Require(view.ActionsRoot != null, contract.Label + ": ActionsRoot", counts, lines);
                RequireButton(view.PrimaryAction, contract.Label + ": PrimaryAction", counts, lines);
                RequireButton(view.SecondaryAction, contract.Label + ": SecondaryAction", counts, lines);
                RequireButton(view.TertiaryAction, contract.Label + ": TertiaryAction", counts, lines);
                if (contract.PanelRootName == "CookingPanel")
                {
                    RequireButton(view.QuaternaryAction, contract.Label + ": CancelAction(QuaternaryAction)", counts, lines);
                }
            }
            else
            {
                AddInfo(counts, lines, contract.Label + ": Actions optional.");
            }

            if (contract.RequireHeaderAction)
            {
                RequireButton(view.HeaderAction, contract.Label + ": HeaderAction", counts, lines);
            }
            else if (view.HeaderAction == null)
            {
                AddInfo(counts, lines, contract.Label + ": HeaderAction optional.");
            }

            if (view.SlotLayout != contract.ExpectedLayout)
            {
                AddWarn(counts, lines, contract.Label + ": SlotLayout is " + view.SlotLayout + ", expected " + contract.ExpectedLayout + ".");
            }

            if (view.LayoutMode != FisherStaticViewLayoutMode.PreserveInspectorLayout)
            {
                AddWarn(counts, lines, contract.Label + ": LayoutMode is " + view.LayoutMode + ". Art handoff should usually use PreserveInspectorLayout.");
            }

            int tabCount = view.CategoryTabs == null ? 0 : view.CategoryTabs.Length;
            if (contract.RequiredTabs > 0 && tabCount < contract.RequiredTabs)
            {
                AddError(counts, lines, contract.Label + ": CategoryTabs " + tabCount + "/" + contract.RequiredTabs + " 부족.");
            }
        }

        private static void ValidateGlobalP0ReadabilityHierarchy(Scene scene, IssueCounts counts, List<string> lines)
        {
            Transform canvas = FindByName(scene, "Canvas");
            if (canvas == null)
            {
                AddError(counts, lines, "P0 UI: Canvas missing.");
                return;
            }

            ValidateToastOverlay(canvas, "FisherResultFeedbackToast", counts, lines);
            ValidateToastOverlay(canvas, "CookingCompletePanel", counts, lines);
        }

        private static void ValidateToastOverlay(Transform canvas, string overlayName, IssueCounts counts, List<string> lines)
        {
            Transform overlay = canvas.Find(overlayName);
            if (overlay == null)
            {
                AddError(counts, lines, "P0 UI: Canvas/" + overlayName + " missing.");
                return;
            }

            ValidateToastOverlayRoot(overlay, "P0 UI: " + overlayName, counts, lines);
        }

        private static void ValidatePanelP0ReadabilityHierarchy(FisherPanelView view, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            ValidateHeaderPlaceholderPanel(
                view.HeaderRoot,
                view.TitleText,
                "TitlePanel",
                contract.Label + ": Header TitlePanel",
                contract.RequireTitleText,
                counts,
                lines);
            if (contract.RequireStatus || view.StatusText != null)
            {
                ValidateTextPanel(
                    view.HeaderRoot,
                    view.StatusText,
                    "StatusPanel",
                    contract.Label + ": Header StatusPanel",
                    counts,
                    lines);
            }
            else
            {
                AddInfo(counts, lines, contract.Label + ": Header StatusPanel optional.");
            }
            if (contract.RequireSubStatus || view.SubStatusText != null)
            {
                ValidateTextPanel(
                    view.HeaderRoot,
                    view.SubStatusText,
                    "SubStatusPanel",
                    contract.Label + ": Header SubStatusPanel",
                    counts,
                    lines);
            }
            else
            {
                AddInfo(counts, lines, contract.Label + ": Header SubStatusPanel optional.");
            }

            if (contract.RequireDetail)
            {
                ValidateTextPanel(
                    view.DetailRoot,
                    view.DetailTitleText,
                    "DetailTitlePanel",
                    contract.Label + ": DetailTitlePanel",
                    counts,
                    lines);
                ValidateTextPanel(
                    view.DetailRoot,
                    view.DetailMetaText,
                    "DetailMetaPanel",
                    contract.Label + ": DetailMetaPanel",
                    counts,
                    lines);
                if ((contract.PanelRootName != "CookingPanel" && contract.PanelRootName != "BagPanel") || view.DetailBodyText != null)
                {
                    ValidateTextPanel(
                        view.DetailRoot,
                        view.DetailBodyText,
                        "DetailBodyPanel",
                        contract.Label + ": DetailBodyPanel",
                        counts,
                        lines);
                }
            }

            if (contract.RequireCookingDetailInfoRows)
            {
                ValidateCookingDetailInfoRows(view, contract, counts, lines);
            }

            if (contract.PanelRootName == "BagPanel")
            {
                ValidateCurrencyStrip(view.HeaderRoot, "BagCurrencyStrip", contract.Label, counts, lines);
            }
            else if (contract.PanelRootName == "ShopPanel")
            {
                ValidateCurrencyStrip(view.HeaderRoot, "ShopCurrencyStrip", contract.Label, counts, lines);
            }
            else if (contract.PanelRootName == "CookingPanel")
            {
                Transform timePanel = FindByName(view.ViewRoot, "CookingTimePanel");
                if (timePanel != null)
                {
                    AddWarn(counts, lines, contract.Label + ": legacy CookingTimePanel exists. P0 redesigned path must not require or runtime-create it.");
                }
            }
        }

        private static void ValidateTextPanel(
            Transform root,
            TMPro.TextMeshProUGUI text,
            string panelName,
            string label,
            IssueCounts counts,
            List<string> lines)
        {
            if (root == null)
            {
                AddError(counts, lines, label + " root missing.");
                return;
            }

            Transform panel = root.Find(panelName);
            if (panel == null)
            {
                AddError(counts, lines, label + " missing.");
                return;
            }

            Require(panel.GetComponent<UnityEngine.UI.Image>() != null, label + ".Image", counts, lines);
            if (text == null)
            {
                AddError(counts, lines, label + " text reference missing.");
                return;
            }

            if (text.transform.parent != panel)
            {
                AddError(counts, lines, label + " must contain " + text.name + ". currentParent=" + GetPath(text.transform.parent));
            }
        }

        private static void ValidateCookingDetailInfoRows(FisherPanelView view, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            if (view.DetailRoot == null)
            {
                AddError(counts, lines, contract.Label + ": CookingDetailInfoRows requires DetailRoot.");
                return;
            }

            Transform rows = view.DetailRoot.Find("CookingDetailInfoRows");
            if (rows == null)
            {
                AddError(counts, lines, contract.Label + ": CookingDetailInfoRows hierarchy missing.");
                return;
            }

            if (view.CookingDetailInfoRows == null || view.CookingDetailInfoRows.transform != rows)
            {
                AddError(counts, lines, contract.Label + ": CookingDetailInfoRows reference mismatch.");
            }

            ValidateCookingDetailInfoPanel(rows, view.CookingRewardExpPanel, view.CookingRewardExpText, "RewardExpPanel", "RewardExpText", contract.Label + ": Cooking RewardExp", counts, lines);
            ValidateCookingDetailInfoPanel(rows, view.CookingSellPricePanel, view.CookingSellPriceText, "SellPricePanel", "SellPriceText", contract.Label + ": Cooking SellPrice", counts, lines);
        }

        private static void ValidateCookingDetailInfoPanel(
            Transform rows,
            RectTransform panelRef,
            TMPro.TextMeshProUGUI textRef,
            string panelName,
            string textName,
            string label,
            IssueCounts counts,
            List<string> lines)
        {
            Transform panel = rows.Find(panelName);
            if (panel == null)
            {
                AddError(counts, lines, label + " panel missing.");
                return;
            }

            if (panelRef == null || panelRef.transform != panel)
            {
                AddError(counts, lines, label + " panel reference mismatch.");
            }

            Require(panel.GetComponent<UnityEngine.UI.Image>() != null, label + ".Image", counts, lines);
            Transform text = panel.Find(textName);
            if (text == null)
            {
                AddError(counts, lines, label + " text object missing.");
                return;
            }

            if (textRef == null || textRef.transform != text)
            {
                AddError(counts, lines, label + " text reference mismatch.");
            }
        }

        private static void ValidateShopPurchaseSheet(FisherPanelView view, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            if (view.ViewRoot == null)
            {
                AddError(counts, lines, contract.Label + ": ShopPurchaseSheet requires ViewRoot.");
                return;
            }

            Transform root = view.ViewRoot.Find("ShopPurchaseSheet");
            if (root == null)
            {
                AddError(counts, lines, contract.Label + ": ShopPurchaseSheet hierarchy missing.");
                return;
            }

            ShopPurchaseSheetView sheet = root.GetComponent<ShopPurchaseSheetView>();
            if (sheet == null)
            {
                AddError(counts, lines, contract.Label + ": ShopPurchaseSheetView component missing.");
                return;
            }

            Require(root.parent == view.ViewRoot, contract.Label + ": ShopPurchaseSheet direct child under ViewRoot", counts, lines);
            Require(root.Find("Panel") != null, contract.Label + ": ShopPurchaseSheet/Panel", counts, lines);
            Require(root.Find("Panel/IconPanel/IconImage") != null, contract.Label + ": ShopPurchaseSheet IconImage hierarchy", counts, lines);
            Require(root.Find("Panel/TextGroup/TitleText") != null, contract.Label + ": ShopPurchaseSheet TitleText hierarchy", counts, lines);
            Require(root.Find("Panel/TextGroup/DescriptionText") != null, contract.Label + ": ShopPurchaseSheet DescriptionText hierarchy", counts, lines);
            Require(root.Find("Panel/InfoRows/RewardCountText") != null, contract.Label + ": ShopPurchaseSheet RewardCountText hierarchy", counts, lines);
            Require(root.Find("Panel/InfoRows/PriceText") != null, contract.Label + ": ShopPurchaseSheet PriceText hierarchy", counts, lines);
            Require(root.Find("Panel/InfoRows/StatusText") != null, contract.Label + ": ShopPurchaseSheet StatusText hierarchy", counts, lines);
            Require(root.Find("Panel/Buttons/PurchaseButton") != null, contract.Label + ": ShopPurchaseSheet PurchaseButton hierarchy", counts, lines);
            Require(root.Find("Panel/Buttons/CancelButton") != null, contract.Label + ": ShopPurchaseSheet CancelButton hierarchy", counts, lines);
            Transform buttons = root.Find("Panel/Buttons");
            Transform cancelButton = buttons == null ? null : buttons.Find("CancelButton");
            Transform purchaseButton = buttons == null ? null : buttons.Find("PurchaseButton");
            if (cancelButton != null && purchaseButton != null)
            {
                Require(cancelButton.GetSiblingIndex() < purchaseButton.GetSiblingIndex(), contract.Label + ": ShopPurchaseSheet button order CancelButton before PurchaseButton", counts, lines);
            }

            Require(sheet.IconImage != null, contract.Label + ": ShopPurchaseSheet.IconImage", counts, lines);
            Require(sheet.TitleText != null, contract.Label + ": ShopPurchaseSheet.TitleText", counts, lines);
            Require(sheet.DescriptionText != null, contract.Label + ": ShopPurchaseSheet.DescriptionText", counts, lines);
            Require(sheet.RewardCountText != null, contract.Label + ": ShopPurchaseSheet.RewardCountText", counts, lines);
            Require(sheet.PriceText != null, contract.Label + ": ShopPurchaseSheet.PriceText", counts, lines);
            Require(sheet.StatusText != null, contract.Label + ": ShopPurchaseSheet.StatusText", counts, lines);
            RequireButton(sheet.PurchaseButton, contract.Label + ": ShopPurchaseSheet.PurchaseButton", counts, lines);
            RequireButton(sheet.CancelButton, contract.Label + ": ShopPurchaseSheet.CancelButton", counts, lines);
        }

        private static void ValidateHeaderPlaceholderPanel(
            Transform root,
            TMPro.TextMeshProUGUI text,
            string panelName,
            string label,
            bool requireText,
            IssueCounts counts,
            List<string> lines)
        {
            if (root == null)
            {
                AddError(counts, lines, label + " root missing.");
                return;
            }

            Transform panel = root.Find(panelName);
            if (panel == null)
            {
                AddError(counts, lines, label + " missing.");
                return;
            }

            Require(panel.GetComponent<UnityEngine.UI.Image>() != null, label + ".Image", counts, lines);
            if (text == null)
            {
                if (requireText)
                {
                    AddError(counts, lines, label + " text reference missing.");
                }
                else
                {
                    AddInfo(counts, lines, label + " text optional.");
                }

                return;
            }

            if (text.transform.parent != panel)
            {
                AddError(counts, lines, label + " must contain " + text.name + ". currentParent=" + GetPath(text.transform.parent));
            }
        }

        private static void ValidateCurrencyStrip(Transform headerRoot, string stripName, string label, IssueCounts counts, List<string> lines)
        {
            if (headerRoot == null)
            {
                AddError(counts, lines, label + ": HeaderRoot missing for " + stripName + ".");
                return;
            }

            Transform strip = headerRoot.Find(stripName);
            if (strip == null)
            {
                AddError(counts, lines, label + ": " + stripName + " missing.");
                return;
            }

            ValidateCurrencyEntry(strip, "GoldEntry", label + ": " + stripName, counts, lines);
            ValidateCurrencyEntry(strip, "PrismPearlEntry", label + ": " + stripName, counts, lines);

            if (strip.Find("PirateCoinEntry") != null)
            {
                AddError(counts, lines, label + ": " + stripName + "/PirateCoinEntry must not exist in P0 Gold/PP strip.");
            }

            if (strip.Find("PlusButton") != null || strip.Find("AddButton") != null || strip.GetComponentInChildren<UnityEngine.UI.Button>(true) != null)
            {
                AddError(counts, lines, label + ": " + stripName + " must not include charge/add buttons in P0.");
            }
        }

        private static void ValidateCurrencyEntry(Transform strip, string entryName, string label, IssueCounts counts, List<string> lines)
        {
            Transform entry = strip.Find(entryName);
            if (entry == null)
            {
                AddError(counts, lines, label + "/" + entryName + " missing.");
                return;
            }

            Require(entry.Find("Icon") != null, label + "/" + entryName + "/Icon", counts, lines);
            Require(entry.Find("AmountText") != null, label + "/" + entryName + "/AmountText", counts, lines);
        }

        private static void ValidateSlotContract(FisherPanelView view, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            int slotArrayCount = view.Slots == null ? 0 : view.Slots.Length;
            int nonNullSlots = CountNonNullSlots(view.Slots);

            if (slotArrayCount < contract.MinSlots || nonNullSlots < contract.MinSlots)
            {
                AddError(counts, lines, contract.Label + ": Slots " + nonNullSlots + "/" + contract.MinSlots + " 부족. array=" + slotArrayCount);
            }

            if (contract.MaxSlots > 0 && (slotArrayCount != contract.MaxSlots || nonNullSlots != contract.MaxSlots))
            {
                AddError(counts, lines, contract.Label + ": Slots must be exactly " + contract.MaxSlots + ". current nonNull=" + nonNullSlots + ", array=" + slotArrayCount);
            }

            int buttonMissing = 0;
            int backgroundMissing = 0;
            int iconMissing = 0;
            int textMissing = 0;
            int slotsToCheck = Mathf.Min(nonNullSlots, contract.MaxSlots > 0 ? contract.MaxSlots : contract.MinSlots);
            for (int i = 0; i < slotsToCheck; i++)
            {
                FisherSlotView slot = view.Slots == null || i >= view.Slots.Length ? null : view.Slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (slot.Button == null)
                {
                    buttonMissing++;
                }

                if (slot.BackgroundImage == null)
                {
                    backgroundMissing++;
                }

                if (slot.IconImage == null)
                {
                    iconMissing++;
                }

                if (slot.NameText == null && slot.QuantityText == null && slot.MetaText == null)
                {
                    textMissing++;
                }
            }

            if (buttonMissing > 0)
            {
                AddError(counts, lines, contract.Label + ": slot Button missing count=" + buttonMissing);
            }

            if (backgroundMissing > 0 || iconMissing > 0 || textMissing > 0)
            {
                AddWarn(counts, lines, contract.Label + ": slot art/text refs missing. background=" + backgroundMissing + ", icon=" + iconMissing + ", textGroup=" + textMissing);
            }
        }

        private static void ValidateCookingPanelP0Contract(FisherPanelView view, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            AddInfo(counts, lines, contract.Label + ": QueueArea uses fixed 3 prepared QueueCells. Cooking slot expansion is disabled.");
            ValidateRequiredSlotReference(view.Slots, 0, contract.Label + ": Queue cell 0", requireButton: true, counts, lines);
            ValidateRequiredSlotReference(view.Slots, 1, contract.Label + ": Queue cell 1", requireButton: true, counts, lines);
            ValidateRequiredSlotReference(view.Slots, 2, contract.Label + ": Queue cell 2", requireButton: true, counts, lines);

            int nonNullQueueCells = CountNonNullSlots(view.Slots);
            if (nonNullQueueCells > 3)
            {
                AddError(counts, lines, contract.Label + ": QueueArea has " + nonNullQueueCells + " assigned slots. Fixed-3 contract allows only cells 0-2.");
            }

            ValidateRequiredSlotReference(view.IngredientSlots, 0, contract.Label + ": Recipe material slot 0", requireButton: false, counts, lines);
            ValidateRequiredSlotReference(view.IngredientSlots, 1, contract.Label + ": Recipe material slot 1", requireButton: false, counts, lines);
            ValidateRequiredSlotReference(view.IngredientSlots, 2, contract.Label + ": Recipe result slot", requireButton: false, counts, lines);

            if (view.TertiaryAction != null)
            {
                AddInfo(counts, lines, contract.Label + ": TertiaryAction is required and shared by Start/Add and Claim states.");
            }

            if (view.QuaternaryAction != null)
            {
                AddInfo(counts, lines, contract.Label + ": QuaternaryAction is required for Cancel state.");
            }
        }

        private static void ValidateRequiredSlotReference(
            IReadOnlyList<FisherSlotView> slots,
            int index,
            string label,
            bool requireButton,
            IssueCounts counts,
            List<string> lines)
        {
            FisherSlotView slot = slots == null || index < 0 || index >= slots.Count ? null : slots[index];
            if (slot == null)
            {
                AddError(counts, lines, label + " missing.");
                return;
            }

            if (requireButton && slot.Button == null)
            {
                AddError(counts, lines, label + ".Button missing.");
            }
            else if (!requireButton && slot.Button == null)
            {
                AddWarn(counts, lines, label + ".Button missing. This is acceptable for non-clickable material/result display slots.");
            }

            if (slot.BackgroundImage == null)
            {
                AddWarn(counts, lines, label + ".BackgroundImage optional visual missing.");
            }

            if (slot.IconImage == null)
            {
                AddWarn(counts, lines, label + ".IconImage optional visual missing.");
            }

            if (slot.QuantityText == null)
            {
                AddWarn(counts, lines, label + ".QuantityText optional count/timer label missing.");
            }

            if (slot.SelectedFrame == null)
            {
                AddWarn(counts, lines, label + ".SelectedFrame optional highlight visual missing.");
            }

            if (slot.LockedBadge == null)
            {
                AddWarn(counts, lines, label + ".LockedBadge optional lock visual missing.");
            }
        }

        private static void ValidateOptionalSlotPool(
            IReadOnlyList<FisherSlotView> slots,
            int requiredCount,
            string label,
            IssueCounts counts,
            List<string> lines)
        {
            if (requiredCount <= 0)
            {
                return;
            }

            int slotArrayCount = slots == null ? 0 : slots.Count;
            int nonNullSlots = CountNonNullSlots(slots);
            if (slotArrayCount != requiredCount || nonNullSlots != requiredCount)
            {
                AddError(counts, lines, label + " must be exactly " + requiredCount + ". current nonNull=" + nonNullSlots + ", array=" + slotArrayCount);
            }
        }

        private static void ValidateActionSheets(FisherPanelView view, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            if (!contract.RequireActionSheets)
            {
                if (view.ActionSheetsRoot == null)
                {
                    AddInfo(counts, lines, contract.Label + ": ActionSheets optional.");
                }

                return;
            }

            Require(view.ActionSheetsRoot != null, contract.Label + ": ActionSheetsRoot", counts, lines);
            RequireActionSheet(view.QuantitySheet, contract.Label + ": QuantitySheet", requireQuantityControls: true, counts, lines);
            RequireActionSheet(view.ConfirmSheet, contract.Label + ": ConfirmSheet", requireQuantityControls: false, counts, lines);
        }

        private static void ValidatePanelArtReadiness(FisherPanelView view, PanelContract contract, IssueCounts counts, List<string> lines)
        {
            if (view.BackgroundImage != null && view.BackgroundImage.raycastTarget)
            {
                AddWarn(counts, lines, contract.Label + ": BackgroundImage.raycastTarget is true. Check it does not block panel input.");
            }

            FisherUiArtProfile profile = view.ArtProfile;
            if (profile == null)
            {
                AddWarn(counts, lines, contract.Label + ": ArtProfile not assigned on FisherPanelView. Context/Resources fallback is required.");
                return;
            }

            AddInfo(counts, lines, contract.Label + ": ArtProfile=" + profile.name);
            ValidateArtProfile(contract.Label, profile, counts, lines);
        }

        #endregion

        #region Prefab Asset Contract

        private static void ValidateCshUiPrefabAssets(IssueCounts counts, List<string> lines)
        {
            lines.Add(string.Empty);
            lines.Add("Prefab assets: " + CshUiPrefabFolderPath);

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CshUiPrefabFolderPath });
            if (guids == null || guids.Length == 0)
            {
                AddError(counts, lines, "Prefab: no CSH UI prefabs found under " + CshUiPrefabFolderPath + ".");
                return;
            }

            List<string> prefabPaths = new List<string>();
            int checkedPrefabs = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(prefabPath))
                {
                    continue;
                }

                prefabPath = prefabPath.Replace('\\', '/');
                prefabPaths.Add(prefabPath);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    AddError(counts, lines, "Prefab: failed to load " + prefabPath + ".");
                    continue;
                }

                checkedPrefabs++;
                ValidatePrefabMissingScripts(prefab.transform, prefabPath, counts, lines);
            }

            AddInfo(counts, lines, "Prefab: checked assets=" + checkedPrefabs);

            for (int i = 0; i < PrefabPanelContracts.Length; i++)
            {
                ValidateExpectedPanelPrefab(prefabPaths, PrefabPanelContracts[i], counts, lines);
            }

            for (int i = 0; i < ToastPrefabFileNames.Length; i++)
            {
                ValidateToastPrefab(prefabPaths, ToastPrefabFileNames[i], counts, lines);
            }
        }

        private static void ValidateExpectedPanelPrefab(
            IReadOnlyList<string> prefabPaths,
            PrefabPanelContract prefabContract,
            IssueCounts counts,
            List<string> lines)
        {
            string prefabPath = ResolvePrefabPath(prefabPaths, prefabContract.PrefabFileName);
            if (string.IsNullOrEmpty(prefabPath))
            {
                AddError(counts, lines, "Prefab: expected panel prefab missing: " + prefabContract.PrefabFileName + ".");
                return;
            }

            if (prefabContract.ContractIndex < 0 || prefabContract.ContractIndex >= PanelContracts.Length)
            {
                AddError(counts, lines, "Prefab: invalid panel contract index for " + prefabContract.PrefabFileName + ".");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                AddError(counts, lines, "Prefab: failed to load expected panel " + prefabPath + ".");
                return;
            }

            AddInfo(counts, lines, "Prefab: validating panel contract " + prefabPath);
            PanelContract contract = PanelContracts[prefabContract.ContractIndex];
            Transform panelRoot = prefab.transform;
            if (!string.Equals(panelRoot.name, contract.PanelRootName, StringComparison.Ordinal))
            {
                panelRoot = prefab.transform.Find(contract.PanelRootName);
            }

            if (panelRoot == null)
            {
                AddError(counts, lines, "Prefab: " + prefabPath + " missing " + contract.PanelRootName + " root.");
                return;
            }

            ValidatePanelRoot(panelRoot, contract, counts, lines);
        }

        private static void ValidateToastPrefab(
            IReadOnlyList<string> prefabPaths,
            string prefabFileName,
            IssueCounts counts,
            List<string> lines)
        {
            string prefabPath = ResolvePrefabPath(prefabPaths, prefabFileName);
            if (string.IsNullOrEmpty(prefabPath))
            {
                AddError(counts, lines, "Prefab: expected overlay prefab missing: " + prefabFileName + ".");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                AddError(counts, lines, "Prefab: failed to load expected overlay " + prefabPath + ".");
                return;
            }

            AddInfo(counts, lines, "Prefab: validating overlay contract " + prefabPath);
            ValidateToastOverlayRoot(prefab.transform, "Prefab: " + prefabFileName.Replace(".prefab", string.Empty), counts, lines);
        }

        private static void ValidateToastOverlayRoot(Transform overlay, string label, IssueCounts counts, List<string> lines)
        {
            CanvasGroup canvasGroup = overlay.GetComponent<CanvasGroup>();
            Require(canvasGroup != null, label + ".CanvasGroup", counts, lines);
            if (canvasGroup != null && (canvasGroup.blocksRaycasts || canvasGroup.interactable))
            {
                AddError(counts, lines, label + " must be non-blocking. blocksRaycasts=" + canvasGroup.blocksRaycasts + ", interactable=" + canvasGroup.interactable);
            }

            Require(overlay.Find("Icon") != null, label + "/Icon", counts, lines);
            Require(overlay.Find("TextColumn/TitleText") != null, label + "/TextColumn/TitleText", counts, lines);
            Require(overlay.Find("TextColumn/MetaText") != null, label + "/TextColumn/MetaText", counts, lines);
        }

        private static void ValidatePrefabMissingScripts(
            Transform prefabRoot,
            string prefabPath,
            IssueCounts counts,
            List<string> lines)
        {
            int missingTotal = 0;
            Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[i].gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                missingTotal += missingCount;
                AddError(counts, lines, "Prefab: " + prefabPath + " has missing script(s)=" + missingCount + " at " + GetPath(transforms[i]) + ".");
            }

            if (missingTotal == 0)
            {
                AddInfo(counts, lines, "Prefab: " + prefabPath + " missing scripts=0");
            }
        }

        private static string ResolvePrefabPath(IReadOnlyList<string> prefabPaths, string prefabFileName)
        {
            string suffix = "/" + prefabFileName;
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string prefabPath = prefabPaths[i];
                if (prefabPath.EndsWith(suffix, StringComparison.Ordinal) ||
                    string.Equals(prefabPath, CshUiPrefabFolderPath + suffix, StringComparison.Ordinal))
                {
                    return prefabPath;
                }
            }

            return string.Empty;
        }

        #endregion

        #region Art Contract

        private static void ValidateArtProfile(string owner, FisherUiArtProfile profile, IssueCounts counts, List<string> lines)
        {
            if (profile == null)
            {
                AddWarn(counts, lines, "Art: " + owner + " profile missing.");
                return;
            }

            int spriteCount = 0;
            spriteCount += profile.PanelBackground == null ? 0 : 1;
            spriteCount += profile.SectionBackground == null ? 0 : 1;
            spriteCount += profile.DetailBackground == null ? 0 : 1;
            spriteCount += profile.ButtonNormal == null ? 0 : 1;
            spriteCount += profile.ButtonSelected == null ? 0 : 1;
            spriteCount += profile.ButtonDisabled == null ? 0 : 1;
            spriteCount += profile.SlotNormal == null ? 0 : 1;
            spriteCount += profile.SlotSelected == null ? 0 : 1;
            spriteCount += profile.SlotEmpty == null ? 0 : 1;
            spriteCount += profile.IconFrame == null ? 0 : 1;

            if (spriteCount == 0)
            {
                AddWarn(counts, lines, "Art: " + owner + " profile has no sprite refs. Fallback colors only.");
            }
            else
            {
                AddInfo(counts, lines, "Art: " + owner + " sprite refs=" + spriteCount + "/10");
            }

            if (profile.FontAsset == null)
            {
                AddWarn(counts, lines, "Art: " + owner + " TMP FontAsset missing.");
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty icons = serializedProfile.FindProperty("_itemIcons");
            int iconCount = icons == null || !icons.isArray ? 0 : icons.arraySize;
            AddInfo(counts, lines, "Art: " + owner + " item icon bindings=" + iconCount);
        }

        #endregion

        #region Required Field Helpers

        private static void Require(bool condition, string label, IssueCounts counts, List<string> lines)
        {
            if (!condition)
            {
                AddError(counts, lines, label + " missing.");
            }
        }

        private static void RequireButton(FisherButtonView button, string label, IssueCounts counts, List<string> lines)
        {
            if (button == null)
            {
                AddError(counts, lines, label + " missing.");
                return;
            }

            bool missingCore = false;
            if (button.Button == null)
            {
                missingCore = true;
                AddError(counts, lines, label + ".Button missing.");
            }

            if (button.LabelText == null)
            {
                missingCore = true;
                AddError(counts, lines, label + ".LabelText missing.");
            }

            if (!missingCore && button.BackgroundImage == null)
            {
                AddWarn(counts, lines, label + ".BackgroundImage missing. Button can work, but art state cannot be applied.");
            }
        }

        private static void RequireActionSheet(
            FisherActionSheetView sheet,
            string label,
            bool requireQuantityControls,
            IssueCounts counts,
            List<string> lines)
        {
            if (sheet == null)
            {
                AddError(counts, lines, label + " missing.");
                return;
            }

            Require(sheet.TitleText != null, label + ".TitleText", counts, lines);
            Require(sheet.BodyText != null, label + ".BodyText", counts, lines);
            RequireButton(sheet.ConfirmButton, label + ".ConfirmButton", counts, lines);
            RequireButton(sheet.CancelButton, label + ".CancelButton", counts, lines);

            if (!requireQuantityControls)
            {
                return;
            }

            Require(sheet.NumberInput != null, label + ".NumberInput", counts, lines);
            RequireButton(sheet.DecreaseButton, label + ".DecreaseButton", counts, lines);
            RequireButton(sheet.IncreaseButton, label + ".IncreaseButton", counts, lines);
            RequireButton(sheet.MaxButton, label + ".MaxButton", counts, lines);
        }

        #endregion

        #region Scene Search Helpers

        private static int CountPanelRoots(Scene scene)
        {
            int count = 0;
            for (int i = 0; i < PanelContracts.Length; i++)
            {
                if (FindByName(scene, PanelContracts[i].PanelRootName) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static T FindFirstComponentInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static Transform FindByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform match = FindByName(roots[i].transform, name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindByName(root.GetChild(i), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform ObjectTransform(UnityEngine.Object value)
        {
            if (value is Component component)
            {
                return component.transform;
            }

            return value is GameObject gameObject ? gameObject.transform : null;
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static int CountNonNullSlots(IReadOnlyList<FisherSlotView> slots)
        {
            if (slots == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        #endregion

        #region Report Helpers

        private static void AddError(IssueCounts counts, List<string> lines, string message)
        {
            counts.Errors++;
            lines.Add("[ERROR] " + message);
        }

        private static void AddWarn(IssueCounts counts, List<string> lines, string message)
        {
            counts.Warnings++;
            lines.Add("[WARN] " + message);
        }

        private static void AddInfo(IssueCounts counts, List<string> lines, string message)
        {
            counts.Infos++;
            lines.Add("[INFO] " + message);
        }

        private sealed class IssueCounts
        {
            public int Errors;
            public int Warnings;
            public int Infos;
        }

        private sealed class ValidationResult
        {
            public ValidationResult(string report, int checkedScenes, int errors, int warnings, int infos)
            {
                Report = report;
                CheckedScenes = checkedScenes;
                Errors = errors;
                Warnings = warnings;
                Infos = infos;
            }

            public string Report { get; }
            public int CheckedScenes { get; }
            public int Errors { get; }
            public int Warnings { get; }
            public int Infos { get; }
        }

        private sealed class PrefabPanelContract
        {
            public PrefabPanelContract(string prefabFileName, int contractIndex)
            {
                PrefabFileName = prefabFileName;
                ContractIndex = contractIndex;
            }

            public string PrefabFileName { get; }
            public int ContractIndex { get; }
        }

        private sealed class PanelContract
        {
            public PanelContract(
                string panelRootName,
                string label,
                FisherSlotLayout expectedLayout,
                int minSlots,
                int maxSlots,
                int requiredTabs,
                bool requireHeaderAction,
                bool requireActionSheets,
                bool requireActions = true,
                int recipeSlots = 0,
                int ingredientSlots = 0,
                bool requireDetail = true,
                bool requireSubStatus = true,
                bool requireTitleText = false,
                bool requireStatus = true,
                bool requireCookingDetailInfoRows = false,
                bool requireShopPurchaseSheet = false)
            {
                PanelRootName = panelRootName;
                Label = label;
                ExpectedLayout = expectedLayout;
                MinSlots = minSlots;
                MaxSlots = maxSlots;
                RequiredTabs = requiredTabs;
                RequireHeaderAction = requireHeaderAction;
                RequireActionSheets = requireActionSheets;
                RequireActions = requireActions;
                RecipeSlots = recipeSlots;
                IngredientSlots = ingredientSlots;
                RequireDetail = requireDetail;
                RequireSubStatus = requireSubStatus;
                RequireTitleText = requireTitleText;
                RequireStatus = requireStatus;
                RequireCookingDetailInfoRows = requireCookingDetailInfoRows;
                RequireShopPurchaseSheet = requireShopPurchaseSheet;
            }

            public string PanelRootName { get; }
            public string Label { get; }
            public FisherSlotLayout ExpectedLayout { get; }
            public int MinSlots { get; }
            public int MaxSlots { get; }
            public int RequiredTabs { get; }
            public bool RequireHeaderAction { get; }
            public bool RequireActionSheets { get; }
            public bool RequireActions { get; }
            public int RecipeSlots { get; }
            public int IngredientSlots { get; }
            public bool RequireDetail { get; }
            public bool RequireSubStatus { get; }
            public bool RequireTitleText { get; }
            public bool RequireStatus { get; }
            public bool RequireCookingDetailInfoRows { get; }
            public bool RequireShopPurchaseSheet { get; }
        }

        #endregion
    }
}
