using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 05_CSH 씬에 고정 View가 없을 때 명명된 ViewRoot를 1회 보정합니다.
    /// 최종 디자인 책임은 Scene/Prefab 구조와 Inspector 참조에 있습니다.
    /// </summary>
    internal static class FisherStaticViewFactory
    {
        private static T EnsureSingleComponent<T>(GameObject target) where T : Component
        {
            if (target == null)
            {
                return null;
            }

            T[] components = target.GetComponents<T>();
            T keep = components.Length > 0 ? components[0] : target.AddComponent<T>();
            for (int i = 1; i < components.Length; i++)
            {
                RemoveComponent(components[i]);
            }

            return keep;
        }

        private static void RemoveDuplicateViewComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            RemoveDuplicateConcreteViewComponents(root);
            RemoveDuplicateComponentsInChildren<FisherPanelView>(root);
            RemoveDuplicateComponentsInChildren<FisherSlotView>(root);
            RemoveDuplicateComponentsInChildren<FisherButtonView>(root);
            RemoveDuplicateComponentsInChildren<FisherActionSheetView>(root);
            RemoveDuplicateComponentsInChildren<ShopPurchaseSheetView>(root);
        }

        private static void RemoveDuplicateConcreteViewComponents(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null)
                {
                    continue;
                }

#if UNITY_EDITOR
                UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
#endif
                Component[] components = transforms[i].GetComponents<Component>();
                HashSet<string> seen = new HashSet<string>();
                for (int j = 0; j < components.Length; j++)
                {
                    Component component = components[j];
                    if (component == null)
                    {
                        continue;
                    }

                    string typeName = component.GetType().FullName;
                    if (!IsFisherViewComponent(typeName))
                    {
                        continue;
                    }

                    if (!seen.Add(typeName))
                    {
                        RemoveComponent(component);
                    }
                }
            }
        }

        private static bool IsFisherViewComponent(string typeName)
        {
            return typeName == "Fisher.PlayerSystems.FisherPanelView"
                || typeName == "Fisher.PlayerSystems.FisherSlotView"
                || typeName == "Fisher.PlayerSystems.FisherButtonView"
                || typeName == "Fisher.PlayerSystems.FisherActionSheetView"
                || typeName == "Fisher.PlayerSystems.ShopPurchaseSheetView";
        }

        private static void RemoveDuplicateComponentsInChildren<T>(GameObject root) where T : Component
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    T[] components = transforms[i].GetComponents<T>();
                    for (int j = 1; j < components.Length; j++)
                    {
                        RemoveComponent(components[j]);
                    }
                }
            }
        }

        private static void RemoveComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(component);
                return;
            }
#endif
            UnityEngine.Object.Destroy(component);
        }

        /// <summary>
        /// 가방 패널에 고정 ViewRoot와 6열 순번 슬롯 풀을 보장합니다.
        /// 기존 씬 구조가 없을 때만 보정 생성하고, 이후에는 값 바인딩만 수행하게 합니다.
        /// </summary>
        public static FisherPanelView EnsureBagView(GameObject panel, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            FisherPanelView view = EnsurePanelView(panel, "BagPanel", "가방", "BagSlot", FisherUiLayoutContract.BagTabLabels, 36, 6, new Vector2(160f, 160f), FisherSlotLayout.Bag, template, artProfile, autoPreserveExistingViewRoot: true, buildHeaderAction: true, buildSubStatus: false);
            ApplyPanelLayoutIfAllowed(view, FisherSlotLayout.Bag);
            return view;
        }

        /// <summary>
        /// 요리 패널에 레시피 슬롯, 상세 카드, 수량/시작/가속 액션 영역을 가진 ViewRoot를 보장합니다.
        /// </summary>
        public static FisherPanelView EnsureCookingView(GameObject panel, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            FisherPanelView view = EnsurePanelView(panel, "CookingPanel", "요리", "CookingSlot", Array.Empty<string>(), 3, 3, new Vector2(152f, 112f), FisherSlotLayout.CookingProgress, template, artProfile, autoPreserveExistingViewRoot: true, buildHeaderAction: false, buildStatus: false, buildSubStatus: false);
            BuildCookingSupplementalGrids(view, template, artProfile);
            BuildCookingDetailInfoRows(view, template, artProfile);
            ApplyPanelLayoutIfAllowed(view, FisherSlotLayout.Cooking);
            return view;
        }

        /// <summary>
        /// 상점 패널에 상품 슬롯 그리드와 상점 탭 ViewRoot를 보장합니다.
        /// 상품 배치는 상점 전용 레이아웃 계약을 따르며 가방 슬롯 규칙을 그대로 재사용하지 않습니다.
        /// </summary>
        public static FisherPanelView EnsureShopView(GameObject panel, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            FisherPanelView view = EnsurePanelView(panel, "ShopPanel", "상점", "ShopSlot", FisherUiLayoutContract.ShopTabLabels, 15, 4, new Vector2(240f, 240f), FisherSlotLayout.Shop, template, artProfile, autoPreserveExistingViewRoot: true, buildHeaderAction: false, buildStatus: false, buildSubStatus: false, buildActions: false);
            BuildShopPurchaseSheet(view, template, artProfile);
            ApplyPanelLayoutIfAllowed(view, FisherSlotLayout.Shop);
            return view;
        }

        /// <summary>
        /// 도감 패널에 카테고리 탭과 도감 상태 슬롯 그리드를 가진 ViewRoot를 보장합니다.
        /// </summary>
        public static FisherPanelView EnsureCollectionView(GameObject panel, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            FisherPanelView view = EnsurePanelView(panel, "CollectionPanel", "도감", "CollectionSlot", FisherUiLayoutContract.CollectionTabLabels, 30, 4, new Vector2(220f, 220f), FisherSlotLayout.Collection, template, artProfile, autoPreserveExistingViewRoot: true, buildHeaderAction: false, buildSubStatus: false, buildDetail: false, buildActions: false);
            ApplyPanelLayoutIfAllowed(view, FisherSlotLayout.Collection);
            return view;
        }

        private static void ApplyPanelLayoutIfAllowed(FisherPanelView view, FisherSlotLayout layout)
        {
            if (view == null || view.PreserveInspectorLayout)
            {
                return;
            }

            FisherUiLayoutContract.ApplyPanelLayout(view, layout);
        }

        /// <summary>
        /// 정적 ViewRoot 아래에 순번 기반 슬롯 오브젝트를 생성합니다.
        /// itemId를 이름에 넣지 않고 prefix_00 형식을 유지해 Main 이식 후에도 Hierarchy가 예측 가능하게 남습니다.
        /// </summary>
        public static FisherSlotView CreateSlot(RectTransform parent, string name, TextMeshProUGUI template, FisherUiArtProfile artProfile, FisherSlotLayout layout = FisherSlotLayout.Bag)
        {
            GameObject slotObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(FisherSlotView));
            slotObject.transform.SetParent(parent, false);

            RectTransform rect = slotObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image background = slotObject.GetComponent<Image>();
            background.color = FisherRuntimeUi.SlotNormalColor;
            background.raycastTarget = true;
            FisherRuntimeUi.ApplyOptionalSprite(background, artProfile == null ? null : artProfile.SlotNormal);

            Button button = slotObject.GetComponent<Button>();
            FisherSlotView slot = EnsureSingleComponent<FisherSlotView>(slotObject);
            slot.Button = button;
            slot.BackgroundImage = background;
            slot.PreserveInspectorChrome = false;
            slot.NormalSprite = artProfile == null ? null : artProfile.SlotNormal;
            slot.SelectedSprite = artProfile == null ? null : artProfile.SlotSelected;
            slot.EmptySprite = artProfile == null ? null : artProfile.SlotEmpty;

            bool cookingProgressLayout = layout == FisherSlotLayout.Cooking || layout == FisherSlotLayout.CookingProgress;
            bool cookingIngredientLayout = layout == FisherSlotLayout.CookingIngredient;
            bool shopLayout = layout == FisherSlotLayout.Shop;
            bool collectionLayout = layout == FisherSlotLayout.Collection;
            bool useIconPanel = cookingProgressLayout || cookingIngredientLayout || collectionLayout;
            bool useBadgeText = !cookingProgressLayout && !cookingIngredientLayout;
            bool useNameText = !cookingProgressLayout && !collectionLayout;
            bool useQuantityText = !shopLayout && !collectionLayout;
            bool useMetaText = !cookingProgressLayout && !cookingIngredientLayout && !shopLayout;

            Transform iconParent = slotObject.transform;
            if (useIconPanel)
            {
                GameObject iconPanel = CreateRectChild(slotObject.transform, "IconPanel", typeof(Image));
                RectTransform iconPanelRect = iconPanel.GetComponent<RectTransform>();
                iconPanelRect.anchorMin = new Vector2(0.18f, 0.36f);
                iconPanelRect.anchorMax = new Vector2(0.82f, 0.88f);
                iconPanelRect.offsetMin = Vector2.zero;
                iconPanelRect.offsetMax = Vector2.zero;
                Image iconPanelImage = iconPanel.GetComponent<Image>();
                iconPanelImage.color = new Color(1f, 0.92f, 0.78f, 0.34f);
                iconPanelImage.raycastTarget = false;
                iconParent = iconPanel.transform;
            }

            GameObject icon = CreateRectChild(iconParent, "IconImage", typeof(Image));
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = useIconPanel ? Vector2.zero : new Vector2(0.18f, 0.36f);
            iconRect.anchorMax = useIconPanel ? Vector2.one : new Vector2(0.82f, 0.94f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            slot.IconImage = icon.GetComponent<Image>();
            slot.IconImage.enabled = false;
            slot.IconImage.raycastTarget = false;

            if (useBadgeText)
            {
                slot.BadgeText = CreateStaticText(slotObject.transform, "BadgeText", "N", template, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
                FisherRuntimeUi.SetTopStretch(slot.BadgeText.rectTransform, 2f, 2f, 2f, 28f);
            }

            if (useNameText)
            {
                Transform nameParent = slotObject.transform;
                if (cookingIngredientLayout)
                {
                    GameObject namePanel = CreateRectChild(slotObject.transform, "NamePanel", typeof(Image));
                    RectTransform namePanelRect = namePanel.GetComponent<RectTransform>();
                    namePanelRect.anchorMin = new Vector2(0.06f, 0.23f);
                    namePanelRect.anchorMax = new Vector2(0.94f, 0.42f);
                    namePanelRect.offsetMin = Vector2.zero;
                    namePanelRect.offsetMax = Vector2.zero;
                    Image namePanelImage = namePanel.GetComponent<Image>();
                    namePanelImage.color = new Color(1f, 0.92f, 0.78f, 0.34f);
                    namePanelImage.raycastTarget = false;
                    nameParent = namePanel.transform;
                }

                slot.NameText = CreateStaticText(nameParent, "NameText", string.Empty, template, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
                if (cookingIngredientLayout)
                {
                    FisherRuntimeUi.StretchToParent(slot.NameText.rectTransform, 4f);
                }
                else
                {
                    FisherRuntimeUi.SetTopStretch(slot.NameText.rectTransform, 4f, 4f, 50f, 30f);
                }
            }

            if (useQuantityText)
            {
                Transform quantityParent = slotObject.transform;
                if (cookingProgressLayout || cookingIngredientLayout)
                {
                    GameObject quantityPanel = CreateRectChild(slotObject.transform, "QuantityPanel", typeof(Image));
                    RectTransform quantityPanelRect = quantityPanel.GetComponent<RectTransform>();
                    quantityPanelRect.anchorMin = new Vector2(0.12f, 0.06f);
                    quantityPanelRect.anchorMax = new Vector2(0.88f, 0.22f);
                    quantityPanelRect.offsetMin = Vector2.zero;
                    quantityPanelRect.offsetMax = Vector2.zero;
                    Image quantityPanelImage = quantityPanel.GetComponent<Image>();
                    quantityPanelImage.color = new Color(1f, 0.92f, 0.78f, 0.34f);
                    quantityPanelImage.raycastTarget = false;
                    quantityParent = quantityPanel.transform;
                }

                slot.QuantityText = CreateStaticText(quantityParent, "QuantityText", string.Empty, template, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
                if (cookingProgressLayout || cookingIngredientLayout)
                {
                    FisherRuntimeUi.StretchToParent(slot.QuantityText.rectTransform, 4f);
                }
                else
                {
                    FisherRuntimeUi.SetTopStretch(slot.QuantityText.rectTransform, 4f, 4f, 78f, 26f);
                }
            }

            if (useMetaText)
            {
                slot.MetaText = CreateStaticText(slotObject.transform, "MetaText", string.Empty, template, 18f, FontStyles.Normal, TextAlignmentOptions.Center);
                FisherRuntimeUi.SetTopStretch(slot.MetaText.rectTransform, 4f, 4f, 104f, 24f);
            }

            GameObject selectedFrame = CreateRectChild(slotObject.transform, "SelectedFrame", typeof(Image));
            FisherRuntimeUi.StretchToParent(selectedFrame.GetComponent<RectTransform>(), 2f);
            Image selectedImage = selectedFrame.GetComponent<Image>();
            selectedImage.color = FisherRuntimeUi.SelectedFrameColor;
            selectedImage.raycastTarget = false;
            selectedFrame.SetActive(false);
            slot.SelectedFrame = selectedFrame;

            slot.LockedBadge = CreateBadge(slotObject.transform, "LockedBadge", "L", template);
            slot.NewBadge = CreateBadge(slotObject.transform, "NewBadge", "N", template);
            slot.LockedBadge.SetActive(false);
            slot.NewBadge.SetActive(false);

            return RepairSlot(slot, template, artProfile, layout);
        }

        /// <summary>
        /// 기존 슬롯에 남아 있을 수 있는 자동 레이아웃 찌꺼기와 클릭 차단 요소를 정리하고
        /// 현재 <see cref="FisherSlotLayout"/>에 맞는 아이콘/텍스트 배치를 다시 적용합니다.
        /// </summary>
        public static FisherSlotView RepairSlot(
            FisherSlotView slot,
            TextMeshProUGUI template,
            FisherUiArtProfile artProfile,
            FisherSlotLayout layout,
            bool preserveInspectorLayout = false)
        {
            if (slot == null)
            {
                return null;
            }

            bool cookingProgressLayout = layout == FisherSlotLayout.Cooking || layout == FisherSlotLayout.CookingProgress;
            bool cookingIngredientLayout = layout == FisherSlotLayout.CookingIngredient;
            bool shopLayout = layout == FisherSlotLayout.Shop;
            bool collectionLayout = layout == FisherSlotLayout.Collection;
            bool useBadgeText = !cookingProgressLayout && !cookingIngredientLayout;
            bool useNameText = !cookingProgressLayout && !collectionLayout;
            bool useQuantityText = !shopLayout && !collectionLayout;
            bool useMetaText = !cookingProgressLayout && !cookingIngredientLayout && !shopLayout;
            bool iconExisted = FindSlotChild(slot.transform, "IconImage", "IconPanel") != null;
            bool badgeExisted = slot.transform.Find("BadgeText") != null;
            bool nameExisted = FindSlotChild(slot.transform, "NameText", "NamePanel") != null;
            bool quantityExisted = FindSlotChild(slot.transform, "QuantityText", "QuantityPanel") != null;
            bool metaExisted = slot.transform.Find("MetaText") != null;
            slot = EnsureSingleComponent<FisherSlotView>(slot.gameObject);
            if (!preserveInspectorLayout)
            {
                RemoveSlotLayoutDrivers(slot.gameObject);
            }

            slot.Button = slot.GetComponent<Button>();
            if (slot.Button == null)
            {
                slot.Button = slot.gameObject.AddComponent<Button>();
            }

            slot.BackgroundImage = slot.GetComponent<Image>();
            if (slot.BackgroundImage == null)
            {
                slot.BackgroundImage = slot.gameObject.AddComponent<Image>();
            }

            slot.PreserveInspectorChrome = preserveInspectorLayout;
            if (!slot.PreserveInspectorChrome)
            {
                slot.NormalSprite = artProfile == null ? null : artProfile.SlotNormal;
                slot.SelectedSprite = artProfile == null ? null : artProfile.SlotSelected;
                slot.EmptySprite = artProfile == null ? null : artProfile.SlotEmpty;
            }

            Transform iconTransform = FindSlotChild(slot.transform, "IconImage", "IconPanel");
            if (iconTransform == null)
            {
                iconTransform = CreateRectChild(slot.transform, "IconImage", typeof(Image)).transform;
            }

            slot.IconImage = iconTransform.GetComponent<Image>();
            if (slot.IconImage == null)
            {
                slot.IconImage = iconTransform.gameObject.AddComponent<Image>();
            }

            RectTransform iconRect = iconTransform.GetComponent<RectTransform>();
            slot.IconImage.raycastTarget = false;
            slot.IconImage.preserveAspect = true;

            slot.BadgeText = useBadgeText
                ? EnsureText(slot.transform, "BadgeText", "N", template, 22f, FontStyles.Bold, TextAlignmentOptions.Center, preserveInspectorLayout && badgeExisted)
                : null;
            if (slot.BadgeText != null)
            {
                slot.BadgeText.raycastTarget = false;
            }
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(slot.BadgeText == null ? null : slot.BadgeText.rectTransform, 2f, 2f, 2f, 28f);
            }

            slot.NameText = useNameText
                ? EnsureSlotText(slot.transform, "NameText", "NamePanel", string.Empty, template, 22f, FontStyles.Bold, TextAlignmentOptions.Center, preserveInspectorLayout && nameExisted)
                : null;
            if (slot.NameText != null)
            {
                slot.NameText.raycastTarget = false;
            }
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(slot.NameText == null ? null : slot.NameText.rectTransform, 4f, 4f, 50f, 30f);
            }

            slot.QuantityText = useQuantityText
                ? EnsureSlotText(slot.transform, "QuantityText", "QuantityPanel", string.Empty, template, 20f, FontStyles.Normal, TextAlignmentOptions.Center, preserveInspectorLayout && quantityExisted)
                : null;
            if (slot.QuantityText != null)
            {
                slot.QuantityText.raycastTarget = false;
            }
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(slot.QuantityText == null ? null : slot.QuantityText.rectTransform, 4f, 4f, 78f, 26f);
            }

            slot.MetaText = useMetaText
                ? EnsureText(slot.transform, "MetaText", string.Empty, template, 18f, FontStyles.Normal, TextAlignmentOptions.Center, preserveInspectorLayout && metaExisted)
                : null;
            if (slot.MetaText != null)
            {
                slot.MetaText.raycastTarget = false;
            }
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(slot.MetaText == null ? null : slot.MetaText.rectTransform, 4f, 4f, 104f, 24f);
            }

            Transform selected = slot.transform.Find("SelectedFrame");
            bool selectedFrameExisted = selected != null;
            if (selected == null)
            {
                selected = CreateRectChild(slot.transform, "SelectedFrame", typeof(Image)).transform;
            }

            slot.SelectedFrame = selected.gameObject;
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.StretchToParent(selected.GetComponent<RectTransform>(), 2f);
            }

            Image selectedImage = selected.GetComponent<Image>();
            if (selectedImage == null)
            {
                selectedImage = selected.gameObject.AddComponent<Image>();
            }

            if (!preserveInspectorLayout || !selectedFrameExisted)
            {
                selectedImage.color = FisherRuntimeUi.SelectedFrameColor;
            }

            selectedImage.raycastTarget = false;
            slot.SelectedFrame.SetActive(false);
            if (!preserveInspectorLayout || !selectedFrameExisted)
            {
                selected.SetAsFirstSibling();
            }

            Transform locked = slot.transform.Find("LockedBadge");
            slot.LockedBadge = locked == null ? CreateBadge(slot.transform, "LockedBadge", "L", template) : locked.gameObject;
            Transform isNew = slot.transform.Find("NewBadge");
            slot.NewBadge = isNew == null ? CreateBadge(slot.transform, "NewBadge", "N", template) : isNew.gameObject;
            SetBadgeRaycastTarget(slot.LockedBadge, false);
            SetBadgeRaycastTarget(slot.NewBadge, false);
            slot.LockedBadge.SetActive(false);
            slot.NewBadge.SetActive(false);
            if (!preserveInspectorLayout)
            {
                SetTopLevelLastSibling(slot.transform, iconTransform);
                SetTopLevelLastSibling(slot.transform, slot.BadgeText == null ? null : slot.BadgeText.transform);
                SetTopLevelLastSibling(slot.transform, slot.NameText == null ? null : slot.NameText.transform);
                SetTopLevelLastSibling(slot.transform, slot.QuantityText == null ? null : slot.QuantityText.transform);
                SetTopLevelLastSibling(slot.transform, slot.MetaText == null ? null : slot.MetaText.transform);
                slot.LockedBadge.transform.SetAsLastSibling();
                slot.NewBadge.transform.SetAsLastSibling();
            }
            if (FisherStaticViewLayoutPolicy.CanRewriteSlotLayout(preserveInspectorLayout))
            {
                DeactivateChildrenExcept(
                    slot.transform,
                    new HashSet<string>
                    {
                        "IconImage",
                        "IconPanel",
                        "BadgeText",
                        "NameText",
                        "NamePanel",
                        "QuantityText",
                        "QuantityPanel",
                        "MetaText",
                        "SelectedFrame",
                        "LockedBadge",
                        "NewBadge"
                    });
                FisherUiLayoutContract.ApplySlotLayout(slot, layout);
            }

            return slot;
        }

        private static void SetBadgeRaycastTarget(GameObject badge, bool enabled)
        {
            if (badge == null)
            {
                return;
            }

            Graphic[] graphics = badge.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = enabled;
            }
        }

        private static void RemoveSlotLayoutDrivers(GameObject slotObject)
        {
            if (slotObject == null)
            {
                return;
            }

            LayoutGroup[] layoutGroups = slotObject.GetComponents<LayoutGroup>();
            for (int i = 0; i < layoutGroups.Length; i++)
            {
                RemoveComponent(layoutGroups[i]);
            }

            ContentSizeFitter[] fitters = slotObject.GetComponents<ContentSizeFitter>();
            for (int i = 0; i < fitters.Length; i++)
            {
                RemoveComponent(fitters[i]);
            }
        }

        private static FisherPanelView EnsurePanelView(
            GameObject panel,
            string panelName,
            string title,
            string slotPrefix,
            IReadOnlyList<string> tabLabels,
            int initialSlotCount,
            int columns,
            Vector2 cellSize,
            FisherSlotLayout slotLayout,
            TextMeshProUGUI template,
            FisherUiArtProfile artProfile,
            bool autoPreserveExistingViewRoot = false,
            bool buildHeaderAction = false,
            bool buildTitleText = false,
            bool buildStatus = true,
            bool buildSubStatus = true,
            bool buildDetail = true,
            bool buildActions = true)
        {
            if (panel == null)
            {
                return null;
            }

            Transform panelRoot = panel.transform.Find(panelName);
            if (panelRoot == null)
            {
                GameObject panelRootObject = CreateRectChild(panel.transform, panelName, typeof(Image));
                panelRoot = panelRootObject.transform;
                FisherRuntimeUi.StretchToParent(panelRootObject.GetComponent<RectTransform>(), 4f);
            }

            Transform existingViewRoot = panelRoot.Find("ViewRoot");
            bool viewRootExisted = existingViewRoot != null;
            if (autoPreserveExistingViewRoot && viewRootExisted)
            {
                FisherPanelView existingView = existingViewRoot.GetComponent<FisherPanelView>();
                if (existingView == null)
                {
                    Debug.LogWarning("[FisherStaticViewFactory] Existing ViewRoot has no FisherPanelView. " +
                                     "Runtime layout generation skipped to preserve Inspector UI: " + panelName);
                    return null;
                }

                if (existingView.LayoutMode != FisherStaticViewLayoutMode.ForceGenerateOrRepair)
                {
                    existingView.SetRuntimePreserveInspectorLayout(true);
                    if (existingView.ViewRoot == null)
                    {
                        existingView.ViewRoot = existingViewRoot.GetComponent<RectTransform>();
                    }

                    if (existingView.BackgroundImage == null)
                    {
                        existingView.BackgroundImage = existingViewRoot.GetComponent<Image>();
                    }

                    existingView.SlotLayout = slotLayout;
                    return existingView;
                }
            }

            GameObject viewRootObject = existingViewRoot == null
                ? CreateRectChild(panelRoot, "ViewRoot", typeof(Image), typeof(FisherPanelView))
                : existingViewRoot.gameObject;

            RemoveDuplicateViewComponents(viewRootObject);
            FisherPanelView view = EnsureSingleComponent<FisherPanelView>(viewRootObject);
            view.SetRuntimePreserveInspectorLayout(autoPreserveExistingViewRoot && viewRootExisted);
            artProfile = view.ResolveArtProfile(artProfile);
            bool preserveInspectorLayout = view.PreserveInspectorLayout;
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.StretchToParent(viewRootObject.GetComponent<RectTransform>(), 0f);
            }

            view.ViewRoot = viewRootObject.GetComponent<RectTransform>();
            view.BackgroundImage = viewRootObject.GetComponent<Image>();
            if (!preserveInspectorLayout)
            {
                view.BackgroundImage.color = FisherRuntimeUi.PanelColor;
                FisherRuntimeUi.ApplyOptionalSprite(view.BackgroundImage, artProfile == null ? null : artProfile.PanelBackground);
            }

            view.BackgroundImage.raycastTarget = false;
            view.SlotLayout = slotLayout;

            BuildHeader(view, title, template, artProfile, buildHeaderAction, buildTitleText, buildStatus, buildSubStatus);
            BuildTabs(view, tabLabels, template, artProfile);
            BuildGrid(view, slotPrefix, initialSlotCount, columns, cellSize, slotLayout, template, artProfile);
            if (buildDetail)
            {
                BuildDetail(view, template, artProfile, slotLayout);
            }
            else
            {
                view.DetailRoot = null;
                view.DetailSlot = null;
                view.DetailTitleText = null;
                view.DetailMetaText = null;
                view.DetailBodyText = null;
            }

            if (buildActions)
            {
                BuildActions(view, template, artProfile);
                BuildActionSheets(view, template, artProfile);
            }
            else
            {
                view.ActionsRoot = null;
                view.PrimaryAction = null;
                view.SecondaryAction = null;
                view.TertiaryAction = null;
                view.QuaternaryAction = null;
                view.ActionSheetsRoot = null;
                view.QuantitySheet = null;
                view.ConfirmSheet = null;
            }

            RemoveDuplicateViewComponents(viewRootObject);
            return view;
        }

        private static void BuildHeader(FisherPanelView view, string title, TextMeshProUGUI template, FisherUiArtProfile artProfile, bool buildHeaderAction, bool buildTitleText, bool buildStatus, bool buildSubStatus)
        {
            bool preserveInspectorLayout = view != null && view.PreserveInspectorLayout;
            GameObject header = EnsureNamedChild(view.ViewRoot, "Header", typeof(RectTransform));
            view.HeaderRoot = header.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.HeaderRoot, 4f, 4f, 4f, 96f);
            }

            RectTransform titlePanel = EnsureHeaderPanelRoot(header.transform, "TitlePanel", new Vector2(116f, 34f), new Vector2(8f, -4f), preserveInspectorLayout);
            if (buildTitleText)
            {
                view.TitleText = EnsureText(titlePanel.transform, "TitleText", title, template, 34f, FontStyles.Bold, TextAlignmentOptions.Left, preserveInspectorLayout);
                if (!preserveInspectorLayout)
                {
                    FisherRuntimeUi.StretchToParent(view.TitleText.rectTransform, 6f);
                }
            }
            else
            {
                view.TitleText = null;
            }

            if (buildStatus)
            {
                RectTransform statusPanel = EnsureHeaderPanelRoot(header.transform, "StatusPanel", new Vector2(292f, 28f), new Vector2(8f, -42f), preserveInspectorLayout);
                view.StatusText = EnsureText(statusPanel.transform, "StatusText", string.Empty, template, 24f, FontStyles.Bold, TextAlignmentOptions.Left, preserveInspectorLayout);
                if (!preserveInspectorLayout)
                {
                    FisherRuntimeUi.StretchToParent(view.StatusText.rectTransform, 6f);
                }
            }
            else
            {
                view.StatusText = null;
            }

            if (buildSubStatus)
            {
                RectTransform subStatusPanel = EnsureHeaderPanelRoot(header.transform, "SubStatusPanel", new Vector2(330f, 26f), new Vector2(8f, -70f), preserveInspectorLayout);
                view.SubStatusText = EnsureText(subStatusPanel.transform, "SubStatusText", string.Empty, template, 22f, FontStyles.Normal, TextAlignmentOptions.Left, preserveInspectorLayout);
                if (!preserveInspectorLayout)
                {
                    FisherRuntimeUi.StretchToParent(view.SubStatusText.rectTransform, 6f);
                }
            }
            else
            {
                view.SubStatusText = null;
            }

            if (!buildHeaderAction)
            {
                view.HeaderAction = null;
                return;
            }

            view.HeaderAction = EnsureButton(header.transform, "HeaderAction", "확장", template, artProfile, preserveInspectorLayout);
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.HeaderAction.GetComponent<RectTransform>(), 300f, 4f, 2f, 50f);
            }
        }

        private static RectTransform EnsureHeaderPanelRoot(Transform header, string panelName, Vector2 defaultSize, Vector2 defaultPosition, bool preserveInspectorLayout)
        {
            GameObject panel = EnsureNamedChild(header, panelName, typeof(RectTransform), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            Image image = panel.GetComponent<Image>();
            if (!preserveInspectorLayout)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = defaultSize;
                rect.anchoredPosition = defaultPosition;
                image.color = new Color(1f, 0.92f, 0.78f, 0.48f);
            }

            image.raycastTarget = false;
            return rect;
        }

        private static void BuildTabs(FisherPanelView view, IReadOnlyList<string> labels, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            bool preserveInspectorLayout = view != null && view.PreserveInspectorLayout;
            GameObject tabs = EnsureNamedChild(view.ViewRoot, "CategoryTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            view.CategoryTabsRoot = tabs.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.CategoryTabsRoot, 4f, 4f, 104f, 58f);
            }

            HorizontalLayoutGroup layout = tabs.GetComponent<HorizontalLayoutGroup>();
            if (!preserveInspectorLayout)
            {
                layout.spacing = 4f;
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }

            FisherButtonView[] tabViews = new FisherButtonView[labels == null ? 0 : labels.Count];
            HashSet<string> activeTabNames = new HashSet<string>();
            for (int i = 0; i < tabViews.Length; i++)
            {
                string tabName = "Tab_" + i.ToString("00") + "_" + labels[i];
                activeTabNames.Add(tabName);
                tabViews[i] = EnsureButton(tabs.transform, tabName, labels[i], template, artProfile, preserveInspectorLayout);
                if (!preserveInspectorLayout)
                {
                    FisherRuntimeUi.SetFlexible(tabViews[i].gameObject, 1f);
                }
            }

            if (!preserveInspectorLayout)
            {
                DeactivateChildrenExcept(tabs.transform, activeTabNames);
            }

            view.CategoryTabs = tabViews;
        }

        private static void BuildGrid(
            FisherPanelView view,
            string slotPrefix,
            int initialSlotCount,
            int columns,
            Vector2 cellSize,
            FisherSlotLayout slotLayout,
            TextMeshProUGUI template,
            FisherUiArtProfile artProfile)
        {
            bool preserveInspectorLayout = view != null && view.PreserveInspectorLayout;
            bool gridRootExisted = view.ViewRoot.Find("Grid") != null;
            GameObject gridRoot = EnsureNamedChild(view.ViewRoot, "Grid", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            view.GridRoot = gridRoot.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.GridRoot, 4f, 4f, 166f, 400f);
            }

            Image gridImage = gridRoot.GetComponent<Image>();
            if (!preserveInspectorLayout || !gridRootExisted)
            {
                gridImage.color = FisherRuntimeUi.SectionColor;
            }

            gridImage.raycastTarget = false;

            bool viewportExisted = gridRoot.transform.Find("Viewport") != null;
            GameObject viewport = EnsureNamedChild(gridRoot.transform, "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.StretchToParent(viewportRect, 4f);
            }

            Image viewportImage = viewport.GetComponent<Image>();
            if (!preserveInspectorLayout || !viewportExisted)
            {
                viewportImage.color = Color.clear;
            }

            viewportImage.raycastTarget = false;

            GameObject content = EnsureNamedChild(viewport.transform, "Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            view.GridContent = content.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                view.GridContent.anchorMin = new Vector2(0f, 1f);
                view.GridContent.anchorMax = new Vector2(1f, 1f);
                view.GridContent.pivot = new Vector2(0.5f, 1f);
                view.GridContent.offsetMin = Vector2.zero;
                view.GridContent.offsetMax = Vector2.zero;
            }

            view.GridLayout = content.GetComponent<GridLayoutGroup>();
            if (!preserveInspectorLayout)
            {
                view.GridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                view.GridLayout.constraintCount = Mathf.Max(1, columns);
                view.GridLayout.cellSize = cellSize;
                view.GridLayout.spacing = new Vector2(5f, 5f);
                view.GridLayout.padding = new RectOffset(6, 6, 6, 6);
                view.GridLayout.childAlignment = TextAnchor.UpperLeft;
            }

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (!preserveInspectorLayout)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            view.GridScrollRect = gridRoot.GetComponent<ScrollRect>();
            view.GridScrollRect.viewport = viewportRect;
            view.GridScrollRect.content = view.GridContent;
            view.GridScrollRect.horizontal = false;
            view.GridScrollRect.vertical = true;
            view.GridScrollRect.movementType = ScrollRect.MovementType.Clamped;
            view.GridScrollRect.scrollSensitivity = 22f;

            List<FisherSlotView> slots = new List<FisherSlotView>();
            HashSet<string> activeSlotNames = new HashSet<string>();
            for (int i = 0; i < initialSlotCount; i++)
            {
                string slotName = slotPrefix + "_" + i.ToString("00");
                activeSlotNames.Add(slotName);
                Transform existing = content.transform.Find(slotName);
                FisherSlotView slot = existing == null
                    ? CreateSlot(view.GridContent, slotName, template, artProfile, slotLayout)
                    : existing.GetComponent<FisherSlotView>();
                if (slot == null && existing != null)
                {
                    slot = existing.gameObject.AddComponent<FisherSlotView>();
                }

                slots.Add(RepairSlot(slot, template, artProfile, slotLayout, preserveInspectorLayout && existing != null));
            }

            view.Slots = slots.ToArray();
            if (!preserveInspectorLayout)
            {
                DeactivateChildrenExcept(content.transform, activeSlotNames);
            }
        }

        private static void BuildCookingSupplementalGrids(
            FisherPanelView view,
            TextMeshProUGUI template,
            FisherUiArtProfile artProfile)
        {
            if (view == null || view.ViewRoot == null)
            {
                return;
            }

            view.RecipeSlots = BuildSupplementalSlotGrid(
                view,
                "RecipeGrid",
                "RecipeSlot",
                17,
                4,
                new Vector2(112f, 104f),
                FisherSlotLayout.CookingRecipe,
                template,
                artProfile,
                out RectTransform recipeRoot,
                out RectTransform recipeContent,
                out GridLayoutGroup recipeLayout,
                out ScrollRect recipeScrollRect);
            view.RecipeGridRoot = recipeRoot;
            view.RecipeGridContent = recipeContent;
            view.RecipeGridLayout = recipeLayout;
            view.RecipeGridScrollRect = recipeScrollRect;

            view.IngredientSlots = BuildSupplementalSlotGrid(
                view,
                "IngredientGrid",
                "IngredientSlot",
                3,
                3,
                new Vector2(152f, 112f),
                FisherSlotLayout.CookingIngredient,
                template,
                artProfile,
                out RectTransform ingredientRoot,
                out RectTransform ingredientContent,
                out GridLayoutGroup ingredientLayout,
                out _);
            view.IngredientGridRoot = ingredientRoot;
            view.IngredientGridContent = ingredientContent;
            view.IngredientGridLayout = ingredientLayout;
        }

        private static FisherSlotView[] BuildSupplementalSlotGrid(
            FisherPanelView view,
            string gridName,
            string slotPrefix,
            int slotCount,
            int columns,
            Vector2 cellSize,
            FisherSlotLayout slotLayout,
            TextMeshProUGUI template,
            FisherUiArtProfile artProfile,
            out RectTransform gridRoot,
            out RectTransform contentRect,
            out GridLayoutGroup gridLayout,
            out ScrollRect scrollRect)
        {
            bool preserveInspectorLayout = view != null && view.PreserveInspectorLayout;
            bool gridRootExisted = view.ViewRoot.Find(gridName) != null;
            GameObject gridObject = EnsureNamedChild(view.ViewRoot, gridName, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            gridRoot = gridObject.GetComponent<RectTransform>();
            bool initializeGridRoot = !preserveInspectorLayout || !gridRootExisted;
            if (initializeGridRoot)
            {
                ApplyCookingSupplementalGridBand(gridRoot);
            }

            Image gridImage = gridObject.GetComponent<Image>();
            if (initializeGridRoot)
            {
                gridImage.color = Color.clear;
            }

            gridImage.raycastTarget = false;

            bool viewportExisted = gridObject.transform.Find("Viewport") != null;
            GameObject viewport = EnsureNamedChild(gridObject.transform, "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            bool initializeViewport = !preserveInspectorLayout || !viewportExisted;
            if (initializeViewport)
            {
                FisherRuntimeUi.StretchToParent(viewportRect, 4f);
            }

            Image viewportImage = viewport.GetComponent<Image>();
            if (initializeViewport)
            {
                viewportImage.color = Color.clear;
            }

            viewportImage.raycastTarget = false;

            bool contentExisted = viewport.transform.Find("Content") != null;
            GameObject content = EnsureNamedChild(viewport.transform, "Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentRect = content.GetComponent<RectTransform>();
            bool initializeContent = !preserveInspectorLayout || !contentExisted;
            if (initializeContent)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;
            }

            gridLayout = content.GetComponent<GridLayoutGroup>();
            if (initializeContent)
            {
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = Mathf.Max(1, columns);
                gridLayout.cellSize = cellSize;
                gridLayout.spacing = new Vector2(5f, 5f);
                gridLayout.padding = new RectOffset(6, 6, 6, 6);
                gridLayout.childAlignment = TextAnchor.UpperCenter;
            }

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (initializeContent)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            scrollRect = gridObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 22f;

            List<FisherSlotView> slots = new List<FisherSlotView>();
            HashSet<string> activeSlotNames = new HashSet<string>();
            for (int i = 0; i < slotCount; i++)
            {
                string slotName = slotPrefix + "_" + i.ToString("00");
                activeSlotNames.Add(slotName);
                Transform existing = content.transform.Find(slotName);
                FisherSlotView slot = existing == null
                    ? CreateSlot(contentRect, slotName, template, artProfile, slotLayout)
                    : existing.GetComponent<FisherSlotView>();
                if (slot == null && existing != null)
                {
                    slot = existing.gameObject.AddComponent<FisherSlotView>();
                }

                slots.Add(RepairSlot(slot, template, artProfile, slotLayout, preserveInspectorLayout && existing != null));
            }

            if (!preserveInspectorLayout)
            {
                DeactivateChildrenExcept(content.transform, activeSlotNames);
            }

            return slots.ToArray();
        }

        private static void ApplyCookingSupplementalGridBand(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 0.61f);
            rect.anchorMax = new Vector2(1f, 0.845f);
            rect.anchoredPosition = new Vector2(0f, -60.00003f);
            rect.sizeDelta = new Vector2(-12f, 119.99994f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void BuildDetail(FisherPanelView view, TextMeshProUGUI template, FisherUiArtProfile artProfile, FisherSlotLayout slotLayout)
        {
            bool preserveInspectorLayout = view != null && view.PreserveInspectorLayout;
            bool detailExisted = view.ViewRoot.Find("Detail") != null;
            GameObject detail = EnsureNamedChild(view.ViewRoot, "Detail", typeof(RectTransform), typeof(Image));
            view.DetailRoot = detail.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.DetailRoot, 4f, 4f, 574f, 126f);
            }

            Image detailImage = detail.GetComponent<Image>();
            if (!preserveInspectorLayout || !detailExisted)
            {
                detailImage.color = FisherRuntimeUi.CardColor;
                FisherRuntimeUi.ApplyOptionalSprite(detailImage, artProfile == null ? null : artProfile.DetailBackground);
            }

            detailImage.raycastTarget = false;

            Transform slotTransform = detail.transform.Find("SelectedSlot");
            view.DetailSlot = slotTransform == null
                ? CreateSlot(detail.GetComponent<RectTransform>(), "SelectedSlot", template, artProfile, FisherSlotLayout.Detail)
                : slotTransform.GetComponent<FisherSlotView>();
            if (view.DetailSlot == null && slotTransform != null)
            {
                view.DetailSlot = slotTransform.gameObject.AddComponent<FisherSlotView>();
            }

            view.DetailSlot = RepairSlot(view.DetailSlot, template, artProfile, FisherSlotLayout.Detail, preserveInspectorLayout && slotTransform != null);
            if (view.DetailSlot == null)
            {
                return;
            }

            RectTransform detailSlotRect = view.DetailSlot.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                detailSlotRect.anchorMin = new Vector2(0f, 0.5f);
                detailSlotRect.anchorMax = new Vector2(0f, 0.5f);
                detailSlotRect.pivot = new Vector2(0f, 0.5f);
                detailSlotRect.anchoredPosition = new Vector2(12f, 0f);
                detailSlotRect.sizeDelta = new Vector2(96f, 96f);
            }

            view.DetailTitleText = EnsureText(detail.transform, "DetailTitleText", string.Empty, template, 28f, FontStyles.Bold, TextAlignmentOptions.Left, preserveInspectorLayout);
            view.DetailMetaText = EnsureText(detail.transform, "DetailMetaText", string.Empty, template, 23f, FontStyles.Normal, TextAlignmentOptions.Left, preserveInspectorLayout);
            view.DetailBodyText = EnsureText(detail.transform, "DetailBodyText", string.Empty, template, 22f, FontStyles.Normal, TextAlignmentOptions.Left, preserveInspectorLayout);
            if (!preserveInspectorLayout)
            {
                SetAnchoredPercent(view.DetailTitleText.rectTransform, new Vector2(0.25f, 0.66f), new Vector2(0.98f, 0.94f));
                SetAnchoredPercent(view.DetailMetaText.rectTransform, new Vector2(0.25f, 0.42f), new Vector2(0.98f, 0.62f));
                SetAnchoredPercent(view.DetailBodyText.rectTransform, new Vector2(0.25f, 0.08f), new Vector2(0.98f, 0.40f));
                if (slotLayout == FisherSlotLayout.Cooking)
                {
                    detailSlotRect.sizeDelta = new Vector2(104f, 104f);
                    view.DetailTitleText.alignment = TextAlignmentOptions.Center;
                    view.DetailMetaText.alignment = TextAlignmentOptions.Center;
                    view.DetailBodyText.alignment = TextAlignmentOptions.Center;
                    SetAnchoredPercent(view.DetailTitleText.rectTransform, new Vector2(0.26f, 0.71f), new Vector2(0.98f, 0.92f));
                    SetAnchoredPercent(view.DetailMetaText.rectTransform, new Vector2(0.26f, 0.31f), new Vector2(0.98f, 0.48f));
                    SetAnchoredPercent(view.DetailBodyText.rectTransform, new Vector2(0.26f, 0.08f), new Vector2(0.98f, 0.27f));
                }
            }

            view.DetailBodyText.textWrappingMode = TextWrappingModes.Normal;
            view.DetailBodyText.overflowMode = TextOverflowModes.Truncate;
        }

        private static void BuildCookingDetailInfoRows(FisherPanelView view, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            if (view == null || view.DetailRoot == null || view.PreserveInspectorLayout)
            {
                return;
            }

            GameObject rows = EnsureNamedChild(view.DetailRoot, "CookingDetailInfoRows", typeof(RectTransform));
            view.CookingDetailInfoRows = rows.GetComponent<RectTransform>();
            SetAnchoredPercent(view.CookingDetailInfoRows, new Vector2(0.26f, 0.51f), new Vector2(0.98f, 0.68f));

            GameObject rewardPanel = EnsureNamedChild(rows.transform, "RewardExpPanel", typeof(RectTransform), typeof(Image));
            view.CookingRewardExpPanel = rewardPanel.GetComponent<RectTransform>();
            ConfigureCookingDetailInfoPanel(view.CookingRewardExpPanel, rewardPanel.GetComponent<Image>(), 0f, 0.5f);
            view.CookingRewardExpText = EnsureText(rewardPanel.transform, "RewardExpText", "완료 EXP +0", template, 22f, FontStyles.Bold, TextAlignmentOptions.Center, preserveExistingStyle: false);
            FisherRuntimeUi.StretchToParent(view.CookingRewardExpText.rectTransform, 4f);

            GameObject sellPanel = EnsureNamedChild(rows.transform, "SellPricePanel", typeof(RectTransform), typeof(Image));
            view.CookingSellPricePanel = sellPanel.GetComponent<RectTransform>();
            ConfigureCookingDetailInfoPanel(view.CookingSellPricePanel, sellPanel.GetComponent<Image>(), 0.5f, 1f);
            view.CookingSellPriceText = EnsureText(sellPanel.transform, "SellPriceText", "개당 0 G", template, 22f, FontStyles.Bold, TextAlignmentOptions.Center, preserveExistingStyle: false);
            FisherRuntimeUi.StretchToParent(view.CookingSellPriceText.rectTransform, 4f);
        }

        private static void ConfigureCookingDetailInfoPanel(RectTransform rect, Image image, float minX, float maxX)
        {
            if (rect != null)
            {
                rect.anchorMin = new Vector2(minX, 0f);
                rect.anchorMax = new Vector2(maxX, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(minX <= 0f ? 0f : 3f, 0f);
                rect.offsetMax = new Vector2(maxX >= 1f ? 0f : -3f, 0f);
            }

            if (image != null)
            {
                image.color = new Color(1f, 0.92f, 0.78f, 0.34f);
                image.raycastTarget = false;
            }
        }

        private static void SetAnchoredPercent(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void BuildShopPurchaseSheet(FisherPanelView view, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            if (view == null || view.ViewRoot == null)
            {
                return;
            }

            bool preserveInspectorLayout = view.PreserveInspectorLayout;
            bool rootExisted = view.ViewRoot.Find("ShopPurchaseSheet") != null;
            GameObject root = EnsureNamedChild(view.ViewRoot, "ShopPurchaseSheet", typeof(RectTransform), typeof(ShopPurchaseSheetView));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (!preserveInspectorLayout || !rootExisted)
            {
                FisherRuntimeUi.StretchToParent(rootRect, 0f);
            }

            ShopPurchaseSheetView sheet = EnsureSingleComponent<ShopPurchaseSheetView>(root);

            bool panelExisted = root.transform.Find("Panel") != null;
            GameObject panel = EnsureNamedChild(root.transform, "Panel", typeof(RectTransform), typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (!preserveInspectorLayout || !panelExisted)
            {
                SetAnchoredPercent(panelRect, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f));
                Image panelImage = panel.GetComponent<Image>();
                panelImage.color = FisherRuntimeUi.CardColor;
                panelImage.raycastTarget = true;
                FisherRuntimeUi.ApplyOptionalSprite(panelImage, artProfile == null ? null : artProfile.DetailBackground);
            }

            bool iconPanelExisted = panel.transform.Find("IconPanel") != null;
            GameObject iconPanel = EnsureNamedChild(panel.transform, "IconPanel", typeof(RectTransform), typeof(Image));
            RectTransform iconPanelRect = iconPanel.GetComponent<RectTransform>();
            if (!preserveInspectorLayout || !iconPanelExisted)
            {
                SetAnchoredPercent(iconPanelRect, new Vector2(0.06f, 0.54f), new Vector2(0.34f, 0.90f));
                Image iconPanelImage = iconPanel.GetComponent<Image>();
                iconPanelImage.color = FisherRuntimeUi.IconColor;
                iconPanelImage.raycastTarget = false;
            }

            bool iconExisted = iconPanel.transform.Find("IconImage") != null;
            GameObject iconImage = EnsureNamedChild(iconPanel.transform, "IconImage", typeof(RectTransform), typeof(Image));
            sheet.IconImage = iconImage.GetComponent<Image>();
            if (!preserveInspectorLayout || !iconExisted)
            {
                FisherRuntimeUi.StretchToParent(iconImage.GetComponent<RectTransform>(), 8f);
                sheet.IconImage.raycastTarget = false;
                sheet.IconImage.preserveAspect = true;
            }

            bool textGroupExisted = panel.transform.Find("TextGroup") != null;
            GameObject textGroup = EnsureNamedChild(panel.transform, "TextGroup", typeof(RectTransform));
            RectTransform textGroupRect = textGroup.GetComponent<RectTransform>();
            if (!preserveInspectorLayout || !textGroupExisted)
            {
                SetAnchoredPercent(textGroupRect, new Vector2(0.38f, 0.54f), new Vector2(0.94f, 0.92f));
            }

            bool titleExisted = textGroup.transform.Find("TitleText") != null;
            sheet.TitleText = EnsureText(textGroup.transform, "TitleText", "상품명", template, 34f, FontStyles.Bold, TextAlignmentOptions.Left, preserveInspectorLayout && titleExisted);
            if (!preserveInspectorLayout || !titleExisted)
            {
                FisherRuntimeUi.SetTopStretch(sheet.TitleText.rectTransform, 0f, 0f, 0f, 54f);
            }

            bool descriptionExisted = textGroup.transform.Find("DescriptionText") != null;
            sheet.DescriptionText = EnsureText(textGroup.transform, "DescriptionText", "상품 설명", template, 28f, FontStyles.Normal, TextAlignmentOptions.Left, preserveInspectorLayout && descriptionExisted);
            if (!preserveInspectorLayout || !descriptionExisted)
            {
                FisherRuntimeUi.SetTopStretch(sheet.DescriptionText.rectTransform, 0f, 0f, 62f, 128f);
                sheet.DescriptionText.textWrappingMode = TextWrappingModes.Normal;
            }

            bool infoRowsExisted = panel.transform.Find("InfoRows") != null;
            GameObject infoRows = EnsureNamedChild(panel.transform, "InfoRows", typeof(RectTransform));
            RectTransform infoRowsRect = infoRows.GetComponent<RectTransform>();
            if (!preserveInspectorLayout || !infoRowsExisted)
            {
                SetAnchoredPercent(infoRowsRect, new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.50f));
            }

            sheet.RewardCountText = EnsureInfoRowText(infoRows.transform, "RewardCountText", "지급 수량 x1", 0f, 1f / 3f, template, preserveInspectorLayout);
            sheet.PriceText = EnsureInfoRowText(infoRows.transform, "PriceText", "가격 500 G", 1f / 3f, 2f / 3f, template, preserveInspectorLayout);
            sheet.StatusText = EnsureInfoRowText(infoRows.transform, "StatusText", "구매 가능", 2f / 3f, 1f, template, preserveInspectorLayout);

            bool buttonsExisted = panel.transform.Find("Buttons") != null;
            GameObject buttons = EnsureNamedChild(panel.transform, "Buttons", typeof(RectTransform));
            RectTransform buttonsRect = buttons.GetComponent<RectTransform>();
            if (!preserveInspectorLayout || !buttonsExisted)
            {
                SetAnchoredPercent(buttonsRect, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.20f));
            }

            bool cancelExisted = buttons.transform.Find("CancelButton") != null;
            sheet.CancelButton = EnsureButton(buttons.transform, "CancelButton", "취소", template, artProfile, preserveInspectorLayout && cancelExisted);
            if (!preserveInspectorLayout || !cancelExisted)
            {
                SetAnchoredPercent(sheet.CancelButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            }

            bool purchaseExisted = buttons.transform.Find("PurchaseButton") != null;
            sheet.PurchaseButton = EnsureButton(buttons.transform, "PurchaseButton", "구매", template, artProfile, preserveInspectorLayout && purchaseExisted);
            if (!preserveInspectorLayout || !purchaseExisted)
            {
                SetAnchoredPercent(sheet.PurchaseButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0f), new Vector2(1f, 1f));
            }

            root.SetActive(false);
        }

        private static TextMeshProUGUI EnsureInfoRowText(
            Transform parent,
            string name,
            string label,
            float minX,
            float maxX,
            TextMeshProUGUI template,
            bool preserveInspectorLayout)
        {
            bool existed = parent.Find(name) != null;
            TextMeshProUGUI text = EnsureText(parent, name, label, template, 26f, FontStyles.Bold, TextAlignmentOptions.Center, preserveInspectorLayout && existed);
            if (!preserveInspectorLayout || !existed)
            {
                SetAnchoredPercent(text.rectTransform, new Vector2(minX, 0f), new Vector2(maxX, 1f));
                text.textWrappingMode = TextWrappingModes.Normal;
            }

            return text;
        }

        private static void BuildActions(FisherPanelView view, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            bool preserveInspectorLayout = view != null && view.PreserveInspectorLayout;
            GameObject actions = EnsureNamedChild(view.ViewRoot, "Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            view.ActionsRoot = actions.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.ActionsRoot, 4f, 4f, 710f, 62f);
            }

            HorizontalLayoutGroup layout = actions.GetComponent<HorizontalLayoutGroup>();
            if (!preserveInspectorLayout)
            {
                layout.spacing = 4f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }

            view.PrimaryAction = EnsureButton(actions.transform, "PrimaryAction", "확인", template, artProfile, preserveInspectorLayout);
            view.SecondaryAction = EnsureButton(actions.transform, "SecondaryAction", "전체", template, artProfile, preserveInspectorLayout);
            view.TertiaryAction = EnsureButton(actions.transform, "TertiaryAction", "닫기", template, artProfile, preserveInspectorLayout);
            view.QuaternaryAction = EnsureButton(actions.transform, "QuaternaryAction", "추가", template, artProfile, preserveInspectorLayout);
        }

        private static void BuildActionSheets(FisherPanelView view, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            bool preserveInspectorLayout = view != null && view.PreserveInspectorLayout;
            GameObject root = EnsureNamedChild(view.ViewRoot, "ActionSheets", typeof(RectTransform));
            view.ActionSheetsRoot = root.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.StretchToParent(view.ActionSheetsRoot, 0f);
            }

            DisableRootGraphicsRaycast(root);

            view.QuantitySheet = EnsureActionSheet(root.transform, "QuantitySheet", template, artProfile, preserveInspectorLayout);
            view.ConfirmSheet = EnsureActionSheet(root.transform, "ConfirmSheet", template, artProfile, preserveInspectorLayout);
            view.HideActionSheets();
        }

        private static void DisableRootGraphicsRaycast(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Graphic[] graphics = root.GetComponents<Graphic>();
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }

        private static FisherActionSheetView EnsureActionSheet(
            Transform parent,
            string name,
            TextMeshProUGUI template,
            FisherUiArtProfile artProfile,
            bool preserveInspectorLayout)
        {
            bool sheetExisted = parent.Find(name) != null;
            GameObject sheet = EnsureNamedChild(parent, name, typeof(RectTransform), typeof(Image), typeof(FisherActionSheetView));
            RectTransform rect = sheet.GetComponent<RectTransform>();
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(rect, 4f, 4f, 574f, 190f);
            }

            Image image = sheet.GetComponent<Image>();
            if (!preserveInspectorLayout || !sheetExisted)
            {
                image.color = FisherRuntimeUi.CardColor;
                FisherRuntimeUi.ApplyOptionalSprite(image, artProfile == null ? null : artProfile.DetailBackground);
            }

            FisherActionSheetView view = EnsureSingleComponent<FisherActionSheetView>(sheet);
            view.TitleText = EnsureText(sheet.transform, "TitleText", string.Empty, template, 22f, FontStyles.Bold, TextAlignmentOptions.Left, preserveInspectorLayout);
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.TitleText.rectTransform, 12f, 12f, 10f, 30f);
            }

            view.BodyText = EnsureText(sheet.transform, "BodyText", string.Empty, template, 18f, FontStyles.Normal, TextAlignmentOptions.Left, preserveInspectorLayout);
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.BodyText.rectTransform, 12f, 12f, 42f, 34f);
            }

            bool inputExisted = sheet.transform.Find("NumberInput") != null;
            GameObject input = EnsureNamedChild(sheet.transform, "NumberInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(input.GetComponent<RectTransform>(), 126f, 126f, 82f, 50f);
            }

            view.NumberInput = input.GetComponent<TMP_InputField>();
            Image inputImage = input.GetComponent<Image>();
            if (!preserveInspectorLayout || !inputExisted)
            {
                inputImage.color = FisherRuntimeUi.InputColor;
            }

            TextMeshProUGUI inputText = EnsureText(input.transform, "Text", string.Empty, template, 24f, FontStyles.Bold, TextAlignmentOptions.Center, preserveInspectorLayout);
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.StretchToParent(inputText.rectTransform, 8f);
            }

            view.NumberInput.targetGraphic = inputImage;
            view.NumberInput.textViewport = inputText.rectTransform;
            view.NumberInput.textComponent = inputText;
            view.NumberInput.contentType = TMP_InputField.ContentType.IntegerNumber;

            view.DecreaseButton = EnsureButton(sheet.transform, "DecreaseButton", "-1", template, artProfile, preserveInspectorLayout);
            view.IncreaseButton = EnsureButton(sheet.transform, "IncreaseButton", "+1", template, artProfile, preserveInspectorLayout);
            view.MaxButton = EnsureButton(sheet.transform, "MaxButton", "최대", template, artProfile, preserveInspectorLayout);
            view.ConfirmButton = EnsureButton(sheet.transform, "ConfirmButton", "확인", template, artProfile, preserveInspectorLayout);
            view.CancelButton = EnsureButton(sheet.transform, "CancelButton", "취소", template, artProfile, preserveInspectorLayout);
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.SetTopStretch(view.DecreaseButton.GetComponent<RectTransform>(), 12f, 366f, 82f, 50f);
                FisherRuntimeUi.SetTopStretch(view.IncreaseButton.GetComponent<RectTransform>(), 366f, 12f, 82f, 50f);
                FisherRuntimeUi.SetTopStretch(view.MaxButton.GetComponent<RectTransform>(), 166f, 166f, 136f, 48f);
                FisherRuntimeUi.SetTopStretch(view.ConfirmButton.GetComponent<RectTransform>(), 12f, 330f, 136f, 48f);
                FisherRuntimeUi.SetTopStretch(view.CancelButton.GetComponent<RectTransform>(), 330f, 12f, 136f, 48f);
            }

            return view;
        }

        private static FisherButtonView EnsureButton(
            Transform parent,
            string name,
            string label,
            TextMeshProUGUI template,
            FisherUiArtProfile artProfile,
            bool preserveInspectorLayout = false)
        {
            bool buttonExisted = parent.Find(name) != null;
            GameObject buttonObject = EnsureNamedChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(FisherButtonView));
            FisherButtonView view = EnsureSingleComponent<FisherButtonView>(buttonObject);
            view.Button = buttonObject.GetComponent<Button>();
            view.BackgroundImage = buttonObject.GetComponent<Image>();
            view.BackgroundImage.raycastTarget = true;
            view.PreserveInspectorChrome = preserveInspectorLayout && buttonExisted;
            if (!view.PreserveInspectorChrome)
            {
                view.NormalSprite = artProfile == null ? null : artProfile.ButtonNormal;
                view.SelectedSprite = artProfile == null ? null : artProfile.ButtonSelected;
                view.DisabledSprite = artProfile == null ? null : artProfile.ButtonDisabled;
                view.BackgroundImage.color = FisherRuntimeUi.ButtonColor;
                FisherRuntimeUi.ApplyOptionalSprite(view.BackgroundImage, view.NormalSprite);
            }

            view.LabelText = EnsureText(buttonObject.transform, "Text", label, template, 23f, FontStyles.Bold, TextAlignmentOptions.Center, preserveInspectorLayout && buttonExisted);
            if (!preserveInspectorLayout)
            {
                FisherRuntimeUi.StretchToParent(view.LabelText.rectTransform, 4f);
            }

            view.LabelText.enableAutoSizing = true;
            view.LabelText.fontSizeMin = 18f;
            view.LabelText.fontSizeMax = 27f;
            view.LabelText.raycastTarget = false;
            return view;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name, string value, TextMeshProUGUI template, float size, FontStyles style, TextAlignmentOptions alignment, bool preserveExistingStyle = false)
        {
            Transform existing = parent.Find(name);
            bool textExisted = existing != null;
            TextMeshProUGUI text = existing == null
                ? CreateStaticText(parent, name, value, template, size, style, alignment)
                : existing.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = existing.gameObject.AddComponent<TextMeshProUGUI>();
            }

            if (!preserveExistingStyle || !textExisted)
            {
                FisherRuntimeUi.ApplyTextStyle(text, template, size, style, FisherRuntimeUi.TextColor);
                text.alignment = alignment;
                text.enableAutoSizing = true;
                text.fontSizeMin = Mathf.Max(12f, size * 0.7f);
                text.fontSizeMax = size;
                text.overflowMode = TextOverflowModes.Truncate;
            }

            text.text = value ?? string.Empty;
            return text;
        }

        private static TextMeshProUGUI EnsureSlotText(
            Transform parent,
            string name,
            string panelName,
            string value,
            TextMeshProUGUI template,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment,
            bool preserveExistingStyle = false)
        {
            Transform existing = FindSlotChild(parent, name, panelName);
            if (existing == null)
            {
                return EnsureText(parent, name, value, template, size, style, alignment, preserveExistingStyle);
            }

            TextMeshProUGUI text = existing.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = existing.gameObject.AddComponent<TextMeshProUGUI>();
            }

            if (!preserveExistingStyle)
            {
                FisherRuntimeUi.ApplyTextStyle(text, template, size, style, FisherRuntimeUi.TextColor);
                text.alignment = alignment;
                text.enableAutoSizing = true;
                text.fontSizeMin = Mathf.Max(12f, size * 0.7f);
                text.fontSizeMax = size;
                text.overflowMode = TextOverflowModes.Truncate;
            }

            text.text = value ?? string.Empty;
            return text;
        }

        private static Transform FindSlotChild(Transform parent, string childName, string panelName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform direct = parent.Find(childName);
            if (direct != null || string.IsNullOrEmpty(panelName))
            {
                return direct;
            }

            return parent.Find(panelName + "/" + childName);
        }

        private static void SetTopLevelLastSibling(Transform slotRoot, Transform child)
        {
            if (slotRoot == null || child == null)
            {
                return;
            }

            Transform top = child;
            while (top.parent != null && top.parent != slotRoot)
            {
                top = top.parent;
            }

            if (top.parent == slotRoot)
            {
                top.SetAsLastSibling();
            }
        }

        private static TextMeshProUGUI CreateStaticText(Transform parent, string name, string value, TextMeshProUGUI template, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateRectChild(parent, name, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            FisherRuntimeUi.ApplyTextStyle(text, template, size, style, FisherRuntimeUi.TextColor);
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12f, size * 0.7f);
            text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Truncate;
            text.text = value ?? string.Empty;
            return text;
        }

        private static GameObject CreateBadge(Transform parent, string name, string label, TextMeshProUGUI template)
        {
            GameObject badge = CreateRectChild(parent, name, typeof(Image));
            RectTransform rect = badge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(28f, 24f);
            rect.anchoredPosition = name == "NewBadge" ? new Vector2(-2f, -2f) : new Vector2(-32f, -2f);
            Image image = badge.GetComponent<Image>();
            image.color = name == "NewBadge" ? FisherRuntimeUi.NewBadgeColor : FisherRuntimeUi.LockedBadgeColor;
            image.raycastTarget = false;
            TextMeshProUGUI text = CreateStaticText(badge.transform, "Text", label, template, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
            FisherRuntimeUi.StretchToParent(text.rectTransform, 1f);
            text.raycastTarget = false;
            return badge;
        }

        private static GameObject EnsureNamedChild(Transform parent, string name, params Type[] components)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != typeof(RectTransform) && existing.GetComponent(components[i]) == null)
                    {
                        existing.gameObject.AddComponent(components[i]);
                    }
                }

                return existing.gameObject;
            }

            return CreateRectChild(parent, name, components);
        }

        private static void DeactivateChildrenExcept(Transform parent, HashSet<string> activeNames)
        {
            if (parent == null || activeNames == null)
            {
                return;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && !activeNames.Contains(child.name))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static GameObject CreateRectChild(Transform parent, string name, params Type[] extraComponents)
        {
            List<Type> types = new List<Type> { typeof(RectTransform) };
            if (extraComponents != null)
            {
                for (int i = 0; i < extraComponents.Length; i++)
                {
                    Type type = extraComponents[i];
                    if (type != null && type != typeof(RectTransform) && !types.Contains(type))
                    {
                        types.Add(type);
                    }
                }
            }

            GameObject gameObject = new GameObject(name, types.ToArray());
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

#if UNITY_EDITOR
        private const string MainScenePath = "Assets/00_Scenes/00_MainScene.unity";
        private const string P0FeedbackUiRepairRequestFileName = "FisherP0FeedbackUiRepair.request";
        private const string CollectionReceiptRebuildRequestFileName = "FisherCollectionReceiptRebuild.request";

        [UnityEditor.InitializeOnLoadMethod]
        private static void RunPendingP0FeedbackUiRepair()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string markerPath = GetP0FeedbackUiRepairRequestPath();
            if (string.IsNullOrEmpty(markerPath) || !File.Exists(markerPath))
            {
                return;
            }

            try
            {
                File.Delete(markerPath);
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[FisherStaticViewFactory] P0 feedback UI repair marker delete failed: " + exception.Message);
            }

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                bool changed = BuildP0FeedbackUiInOpenScene();
                if (changed)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                }
            };
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void RunPendingCollectionReceiptRebuild()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string markerPath = GetCollectionReceiptRebuildRequestPath();
            if (string.IsNullOrEmpty(markerPath) || !File.Exists(markerPath))
            {
                return;
            }

            try
            {
                File.Delete(markerPath);
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[FisherStaticViewFactory] collection_receipt rebuild marker delete failed: " + exception.Message);
            }

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                UnityEngine.SceneManagement.Scene targetScene = FindLoadedMainScene();
                if (!targetScene.IsValid())
                {
                    targetScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                }

                if (!targetScene.IsValid() || !targetScene.isLoaded || targetScene.path != MainScenePath)
                {
                    Debug.LogWarning("[FisherStaticViewFactory] collection_receipt rebuild skipped because loaded " + MainScenePath + " was not found. target=" + SceneLabel(targetScene));
                    return;
                }

                UnityEngine.SceneManagement.SceneManager.SetActiveScene(targetScene);
                if (RebuildCollectionReceiptInOpenScene())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(targetScene);
                    Debug.Log("[FisherStaticViewFactory] collection_receipt rebuild saved " + MainScenePath + ".");
                }
            };
        }

        [UnityEditor.MenuItem("FISHER/UI/CSH P0 Readability UI 보정 (non-destructive)")]
        public static void BuildP0ReadabilityUiInOpenSceneMenu()
        {
            UnityEngine.SceneManagement.Scene targetScene = FindLoadedMainScene();
            if (!targetScene.IsValid())
            {
                targetScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                Debug.LogWarning("[FisherStaticViewFactory] No loaded scene found for P0 readability UI repair.");
                return;
            }

            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (targetScene.path != MainScenePath)
            {
                Debug.LogWarning(
                    "[FisherStaticViewFactory] CSH P0 readability repair skipped because loaded " +
                    MainScenePath + " was not found. target=" + SceneLabel(targetScene) +
                    ", active=" + SceneLabel(activeScene));
                return;
            }

            if (BuildP0ReadabilityUiInScene(targetScene))
            {
                if (targetScene.path == MainScenePath)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(targetScene);
                    Debug.Log("[FisherStaticViewFactory] CSH P0 readability repair saved " + MainScenePath + ".");
                }
                else
                {
                    Debug.LogWarning(
                        "[FisherStaticViewFactory] CSH P0 readability repair marked a non-main scene dirty, but auto-save was skipped. target=" +
                        SceneLabel(targetScene) + ", active=" + SceneLabel(activeScene));
                }
            }
        }

        public static string GetP0FeedbackUiRepairRequestPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot)
                ? null
                : Path.Combine(projectRoot, "Temp", P0FeedbackUiRepairRequestFileName);
        }

        public static string GetCollectionReceiptRebuildRequestPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot)
                ? null
                : Path.Combine(projectRoot, "Temp", CollectionReceiptRebuildRequestFileName);
        }

        public static void BuildP0FeedbackUiInMainScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/00_Scenes/00_MainScene.unity");
            if (BuildP0FeedbackUiInOpenScene())
            {
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            }
        }

        public static bool BuildP0FeedbackUiInOpenScene()
        {
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogWarning("[FisherStaticViewFactory] No active scene loaded for P0 feedback UI repair.");
                return false;
            }

            TextMeshProUGUI template = null;
            bool touched = false;

            GameObject bag = FindInActiveScene(activeScene, "Inventory_Panel");
            if (bag != null)
            {
                template = FisherRuntimeUi.FindTextTemplate(bag);
                FisherPanelView view = EnsureBagView(bag, template, null);
                FisherRuntimeUi.EnsureBagCurrencyStrip(view, null, template, view == null ? null : view.ResolveArtProfile(null));
                touched = touched || view != null;
            }
            else
            {
                Debug.LogWarning("[FisherStaticViewFactory] Inventory_Panel not found in " + activeScene.name);
            }

            GameObject shop = FindInActiveScene(activeScene, "Shop_Panel");
            if (shop != null)
            {
                TextMeshProUGUI shopTemplate = template ?? FisherRuntimeUi.FindTextTemplate(shop);
                FisherPanelView view = EnsureShopView(shop, shopTemplate, null);
                FisherRuntimeUi.EnsureShopCurrencyStrip(view, null, shopTemplate, view == null ? null : view.ResolveArtProfile(null));
                touched = touched || view != null;
            }
            else
            {
                Debug.LogWarning("[FisherStaticViewFactory] Shop_Panel not found in " + activeScene.name);
            }

            GameObject canvasObject = FindInActiveScene(activeScene, "Canvas");
            Canvas canvas = canvasObject == null ? UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include) : canvasObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                TextMeshProUGUI canvasTemplate = template ?? FisherRuntimeUi.FindTextTemplate(canvas.gameObject);
                FisherRuntimeUi.EnsureResultToastOverlay(canvas, canvasTemplate);
                FisherRuntimeUi.EnsureCollectionReceiptOverlay(canvas, canvasTemplate);
                touched = true;
            }
            else
            {
                Debug.LogWarning("[FisherStaticViewFactory] Canvas not found in " + activeScene.name);
            }

            if (touched)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                Debug.Log("[FisherStaticViewFactory] CSH P0 feedback UI hierarchy built or repaired in open scene.");
            }

            return touched;
        }

        public static void RebuildCollectionReceiptInMainScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            if (RebuildCollectionReceiptInOpenScene())
            {
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            }
        }

        public static bool RebuildCollectionReceiptInOpenScene()
        {
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogWarning("[FisherStaticViewFactory] No active scene loaded for collection_receipt rebuild.");
                return false;
            }

            GameObject canvasObject = FindInActiveScene(activeScene, "Canvas");
            Canvas canvas = canvasObject == null ? UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include) : canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[FisherStaticViewFactory] Canvas not found in " + activeScene.name);
                return false;
            }

            Transform receiptTransform = canvas.transform.Find("collection_receipt");
            if (receiptTransform == null)
            {
                Debug.LogWarning("[FisherStaticViewFactory] collection_receipt rebuild skipped because collection_receipt is missing under Canvas.");
                return false;
            }

            TextMeshProUGUI template = FisherRuntimeUi.FindTextTemplate(receiptTransform.gameObject);
            if (template == null)
            {
                template = FisherRuntimeUi.FindTextTemplate(canvas.gameObject);
            }

            GameObject receipt = FisherRuntimeUi.EnsureCollectionReceiptOverlay(canvas, template);
            if (receipt == null)
            {
                return false;
            }

            receipt.SetActive(false);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[FisherStaticViewFactory] collection_receipt preserved as authored Canvas panel.");
            return true;
        }

        private static void ConfigureLayoutElement(GameObject target, float preferredHeight)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = target.AddComponent<LayoutElement>();
            }

            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;
        }

        public static void BuildP0ReadabilityUiInMainScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/00_Scenes/00_MainScene.unity");
            if (BuildP0ReadabilityUiInOpenScene())
            {
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            }
        }

        public static bool BuildP0ReadabilityUiInOpenScene()
        {
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogWarning("[FisherStaticViewFactory] No active scene loaded for P0 readability UI repair.");
                return false;
            }

            return BuildP0ReadabilityUiInScene(activeScene);
        }

        private static bool BuildP0ReadabilityUiInScene(UnityEngine.SceneManagement.Scene activeScene)
        {
            TextMeshProUGUI template = null;
            bool touched = false;

            GameObject bag = FindInActiveScene(activeScene, "Inventory_Panel");
            FisherPanelView bagView = FindExistingPanelView(bag);
            if (bagView != null)
            {
                template = FisherRuntimeUi.FindTextTemplate(bag);
                FisherRuntimeUi.ApplyHeaderStatusParchmentText(bagView);
                FisherRuntimeUi.ApplyDetailParchmentText(bagView);
                FisherRuntimeUi.EnsureBagCurrencyStrip(bagView, null, template, bagView.ResolveArtProfile(null));
                LogP0ReadabilityViewState("Bag", bagView);
                touched = true;
            }
            else
            {
                LogMissingP0ReadabilityView("Bag", "Inventory_Panel", bag);
            }

            GameObject cooking = FindInActiveScene(activeScene, "Cooking_Panel");
            FisherPanelView cookingView = FindExistingPanelView(cooking);
            if (cookingView != null)
            {
                template ??= FisherRuntimeUi.FindTextTemplate(cooking);
                FisherRuntimeUi.ApplyHeaderStatusParchmentText(cookingView);
                FisherRuntimeUi.ApplyDetailParchmentText(cookingView);
                LogP0ReadabilityViewState("Cooking", cookingView);
                touched = true;
            }
            else
            {
                LogMissingP0ReadabilityView("Cooking", "Cooking_Panel", cooking);
            }

            GameObject shop = FindInActiveScene(activeScene, "Shop_Panel");
            FisherPanelView shopView = FindExistingPanelView(shop);
            if (shopView != null)
            {
                template ??= FisherRuntimeUi.FindTextTemplate(shop);
                FisherRuntimeUi.ApplyHeaderStatusParchmentText(shopView);
                FisherRuntimeUi.ApplyDetailParchmentText(shopView);
                FisherRuntimeUi.EnsureShopCurrencyStrip(shopView, null, template, shopView.ResolveArtProfile(null));
                LogP0ReadabilityViewState("Shop", shopView);
                touched = true;
            }
            else
            {
                LogMissingP0ReadabilityView("Shop", "Shop_Panel", shop);
            }

            GameObject collection = FindInActiveScene(activeScene, "Collection_Panel");
            FisherPanelView collectionView = FindExistingPanelView(collection);
            if (collectionView != null)
            {
                template ??= FisherRuntimeUi.FindTextTemplate(collection);
                FisherRuntimeUi.ApplyHeaderStatusParchmentText(collectionView);
                LogP0ReadabilityViewState("Collection", collectionView, detailExpected: false);
                touched = true;
            }
            else
            {
                LogMissingP0ReadabilityView("Collection", "Collection_Panel", collection);
            }

            GameObject canvasObject = FindInActiveScene(activeScene, "Canvas");
            Canvas canvas = canvasObject == null ? UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include) : canvasObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                template ??= FisherRuntimeUi.FindTextTemplate(canvas.gameObject);
                FisherRuntimeUi.EnsureResultToastOverlay(canvas, template);
                FisherRuntimeUi.EnsureCookingCompleteOverlay(canvas, template);
                FisherRuntimeUi.EnsureCollectionReceiptOverlay(canvas, template);
                touched = true;
            }
            else
            {
                Debug.LogWarning("[FisherStaticViewFactory] Canvas not found in " + activeScene.name);
            }

            if (touched)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                Debug.Log("[FisherStaticViewFactory] CSH P0 readability UI hierarchy repaired in open scene without rebuilding slots or actions.");
                const int expectedHeaderPanelCount = 7;
                int headerPanelCount = CountNamedObjects(activeScene, "TitlePanel", "StatusPanel", "SubStatusPanel");
                int detailPanelCount = CountNamedObjects(activeScene, "DetailTitlePanel", "DetailMetaPanel", "DetailBodyPanel");
                int currencyStripCount = CountNamedObjects(activeScene, "BagCurrencyStrip", "ShopCurrencyStrip");
                int toastOverlayCount = CountNamedObjects(activeScene, "FisherResultFeedbackToast", "CookingCompletePanel", "collection_receipt");
                string countLine =
                    "[FisherStaticViewFactory] CSH P0 readability counts: " +
                    "HeaderPanels=" + headerPanelCount + "/" + expectedHeaderPanelCount + ", " +
                    "DetailPanels=" + detailPanelCount + "/9, " +
                    "CurrencyStrips=" + currencyStripCount + "/2, " +
                    "ToastOverlays=" + toastOverlayCount + "/3";

                if (headerPanelCount < expectedHeaderPanelCount || detailPanelCount < 9 || currencyStripCount < 2 || toastOverlayCount < 3)
                {
                    Debug.LogWarning(countLine + " (incomplete)");
                }
                else
                {
                    Debug.Log(countLine);
                }
            }

            return touched;
        }

        private static UnityEngine.SceneManagement.Scene FindLoadedMainScene()
        {
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && scene.path == MainScenePath)
                {
                    return scene;
                }
            }

            return default;
        }

        private static FisherPanelView FindExistingPanelView(GameObject panel)
        {
            return panel == null ? null : panel.GetComponentInChildren<FisherPanelView>(true);
        }

        private static void LogMissingP0ReadabilityView(string label, string panelName, GameObject panel)
        {
            Debug.LogWarning(
                "[FisherStaticViewFactory] CSH P0 readability " + label +
                " skipped. panel=" + (panel == null ? "missing:" + panelName : GetScenePath(panel.transform)) +
                ", FisherPanelView=" + (panel == null ? "n/a" : "missing"));
        }

        private static void LogP0ReadabilityViewState(string label, FisherPanelView view, bool detailExpected = true)
        {
            if (view == null)
            {
                Debug.LogWarning("[FisherStaticViewFactory] CSH P0 readability " + label + " skipped. FisherPanelView missing.");
                return;
            }

            int headerPanels = CountNamedObjectsInTransform(view.HeaderRoot, "TitlePanel", "StatusPanel", "SubStatusPanel");
            int expectedHeaderPanels = 1 +
                                       (view.StatusText == null ? 0 : 1) +
                                       (view.SubStatusText == null ? 0 : 1);
            int detailPanels = CountNamedObjectsInTransform(view.DetailRoot, "DetailTitlePanel", "DetailMetaPanel", "DetailBodyPanel");
            string detailTarget = detailExpected ? "3" : "0 optional";
            Debug.Log(
                "[FisherStaticViewFactory] CSH P0 readability " + label + ": " +
                "view=" + GetScenePath(view.transform) +
                ", headerRoot=" + GetScenePath(view.HeaderRoot) +
                ", detailRoot=" + GetScenePath(view.DetailRoot) +
                ", titleRef=" + HasRef(view.DetailTitleText) +
                ", titleParent=" + GetScenePath(view.DetailTitleText == null ? null : view.DetailTitleText.transform.parent) +
                ", metaRef=" + HasRef(view.DetailMetaText) +
                ", metaParent=" + GetScenePath(view.DetailMetaText == null ? null : view.DetailMetaText.transform.parent) +
                ", bodyRef=" + HasRef(view.DetailBodyText) +
                ", bodyParent=" + GetScenePath(view.DetailBodyText == null ? null : view.DetailBodyText.transform.parent) +
                ", headerPanels=" + headerPanels + "/" + expectedHeaderPanels +
                ", detailPanels=" + detailPanels + "/" + detailTarget);
        }

        private static string HasRef(UnityEngine.Object target)
        {
            return target == null ? "missing" : "ok";
        }

        private static int CountNamedObjects(UnityEngine.SceneManagement.Scene scene, params string[] names)
        {
            if (!scene.IsValid() || !scene.isLoaded || names == null || names.Length == 0)
            {
                return 0;
            }

            HashSet<string> nameSet = new HashSet<string>(names, StringComparer.Ordinal);
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                count += CountNamedObjectsRecursive(roots[i] == null ? null : roots[i].transform, nameSet);
            }

            return count;
        }

        private static int CountNamedObjectsInTransform(Transform root, params string[] names)
        {
            if (root == null || names == null || names.Length == 0)
            {
                return 0;
            }

            return CountNamedObjectsRecursive(root, new HashSet<string>(names, StringComparer.Ordinal));
        }

        private static int CountNamedObjectsRecursive(Transform root, HashSet<string> names)
        {
            if (root == null || names == null)
            {
                return 0;
            }

            int count = names.Contains(root.name) ? 1 : 0;
            for (int i = 0; i < root.childCount; i++)
            {
                count += CountNamedObjectsRecursive(root.GetChild(i), names);
            }

            return count;
        }

        private static string GetScenePath(Component component)
        {
            return component == null ? "missing" : GetScenePath(component.transform);
        }

        private static string GetScenePath(Transform transform)
        {
            if (transform == null)
            {
                return "missing";
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string SceneLabel(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid())
            {
                return "<invalid>";
            }

            return string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
        }

        public static void BuildCookingViewInMainScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/00_Scenes/00_MainScene.unity");
            BuildCookingViewInOpenScene();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        public static void BuildCookingViewInOpenScene()
        {
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameObject cooking = FindInActiveScene(activeScene, "Cooking_Panel");
            if (cooking == null)
            {
                Debug.LogWarning("[FisherStaticViewFactory] Cooking_Panel not found in " + activeScene.name);
                return;
            }

            TextMeshProUGUI template = FisherRuntimeUi.FindTextTemplate(cooking);
            FisherPanelView existingView = cooking.GetComponentInChildren<FisherPanelView>(true);
            if (existingView != null)
            {
                existingView.SetRuntimePreserveInspectorLayout(true);
                if (existingView.ViewRoot == null)
                {
                    existingView.ViewRoot = existingView.GetComponent<RectTransform>();
                }

                if (existingView.BackgroundImage == null)
                {
                    existingView.BackgroundImage = existingView.GetComponent<Image>();
                }

                existingView.SlotLayout = FisherSlotLayout.CookingProgress;
                BuildGrid(existingView, "CookingSlot", 3, 3, new Vector2(152f, 112f), FisherSlotLayout.CookingProgress, template, null);
                BuildCookingSupplementalGrids(existingView, template, null);
                existingView.LayoutMode = FisherStaticViewLayoutMode.PreserveInspectorLayout;
                existingView.SetRuntimePreserveInspectorLayout(true);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                Debug.Log("[FisherStaticViewFactory] CSH cooking view built or repaired in open scene.");
                return;
            }

            FisherPanelView view = EnsureCookingView(cooking, template, null);
            if (view != null)
            {
                view.LayoutMode = FisherStaticViewLayoutMode.PreserveInspectorLayout;
                view.SetRuntimePreserveInspectorLayout(true);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[FisherStaticViewFactory] CSH cooking view built or repaired in open scene.");
        }

        private static void BuildStaticViewsInOpenScene()
        {
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            TextMeshProUGUI template = null;
            GameObject bag = FindInActiveScene(activeScene, "Inventory_Panel");
            if (bag != null)
            {
                template = FisherRuntimeUi.FindTextTemplate(bag);
                EnsureBagView(bag, template, null);
            }
            else
            {
                Debug.LogWarning("[FisherStaticViewFactory] Inventory_Panel not found in " + activeScene.name);
            }

            GameObject cooking = FindInActiveScene(activeScene, "Cooking_Panel");
            if (cooking != null)
            {
                EnsureCookingView(cooking, template ?? FisherRuntimeUi.FindTextTemplate(cooking), null);
            }
            else
            {
                Debug.LogWarning("[FisherStaticViewFactory] Cooking_Panel not found in " + activeScene.name);
            }

            GameObject shop = FindInActiveScene(activeScene, "Shop_Panel");
            if (shop != null)
            {
                EnsureShopView(shop, template ?? FisherRuntimeUi.FindTextTemplate(shop), null);
            }
            else
            {
                Debug.LogWarning("[FisherStaticViewFactory] Shop_Panel not found in " + activeScene.name);
            }

            GameObject collection = FindInActiveScene(activeScene, "Collection_Panel");
            if (collection != null)
            {
                EnsureCollectionView(collection, template ?? FisherRuntimeUi.FindTextTemplate(collection), null);
            }
            else
            {
                Debug.LogWarning("[FisherStaticViewFactory] Collection_Panel not found in " + activeScene.name);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[FisherStaticViewFactory] CSH static Fisher views built or repaired in open scene.");
        }

        private static GameObject FindInActiveScene(UnityEngine.SceneManagement.Scene scene, string objectName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] children = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < children.Length; j++)
                {
                    if (children[j] != null && children[j].name == objectName)
                    {
                        return children[j].gameObject;
                    }
                }
            }

            return null;
        }
#endif
    }
}
