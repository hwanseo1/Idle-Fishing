using TMPro;
using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// Fisher fixed View의 패널별 배치와 슬롯별 텍스트/아이콘 영역 계약입니다.
    /// 화면별 수치 조정은 이 파일에서 먼저 처리합니다.
    /// </summary>
    internal static class FisherUiLayoutContract
    {
        public static readonly string[] BagTabLabels = { "전체", "물고기", "요리", "재료", "기타" };
        public static readonly string[] ShopTabLabels = { "골드상점", "펄상점", "주화상점", "특수" };
        public static readonly string[] CollectionTabLabels = { "물고기", "요리", "선원", "배" };

        /// <summary>
        /// 패널의 Header, Tabs, Grid, Detail, Actions 영역을 화면 목적에 맞게 재배치합니다.
        /// 가방/요리/상점/도감은 같은 ViewRoot 계약을 쓰지만 사용 흐름이 다르므로
        /// 이 메서드에서 화면별 비율을 분리해 적용합니다.
        /// </summary>
        public static void ApplyPanelLayout(FisherPanelView view, FisherSlotLayout layout)
        {
            if (view == null)
            {
                return;
            }

            switch (layout)
            {
                case FisherSlotLayout.Bag:
                    ApplyBagLayout(view);
                    break;
                case FisherSlotLayout.Cooking:
                    ApplyCookingLayout(view);
                    break;
                case FisherSlotLayout.Shop:
                    ApplyShopLayout(view);
                    break;
                case FisherSlotLayout.Collection:
                    ApplyCollectionLayout(view);
                    break;
            }
        }

        /// <summary>
        /// 단일 슬롯 내부의 아이콘, 이름, 수량, 보조 텍스트 위치와 폰트 크기를 화면별로 적용합니다.
        /// 32x32 원본 아이콘은 Image preserveAspect로 확대 표시하고, 텍스트는 아이콘 영역을 침범하지 않게 분리합니다.
        /// </summary>
        public static void ApplySlotLayout(FisherSlotView slot, FisherSlotLayout layout)
        {
            if (slot == null)
            {
                return;
            }

            RectTransform iconRect = SlotFrameRect(slot.IconImage == null ? null : slot.IconImage.rectTransform);
            switch (layout)
            {
                case FisherSlotLayout.Bag:
                    SetAnchoredPercent(iconRect, new Vector2(0.22f, 0.30f), new Vector2(0.78f, 0.82f));
                    ConfigureSlotText(slot.BadgeText, false, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.BadgeText), new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.98f));
                    ConfigureSlotText(slot.NameText, false, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
                    ConfigureSlotText(slot.QuantityText, true, 23f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.QuantityText), new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.24f));
                    ConfigureSlotText(slot.MetaText, false, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    break;

                case FisherSlotLayout.Cooking:
                case FisherSlotLayout.CookingProgress:
                    SetAnchoredPercent(iconRect, new Vector2(0.22f, 0.46f), new Vector2(0.78f, 0.88f));
                    ConfigureSlotText(slot.BadgeText, false, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.BadgeText), new Vector2(0.08f, 0.85f), new Vector2(0.92f, 0.97f));
                    ConfigureSlotText(slot.NameText, false, 20f, FontStyles.Bold, TextAlignmentOptions.Center, true);
                    SetAnchoredPercent(TextRect(slot.NameText), new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.44f));
                    ConfigureSlotText(slot.QuantityText, true, 19f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.QuantityText), new Vector2(0.08f, 0.15f), new Vector2(0.92f, 0.27f));
                    ConfigureSlotText(slot.MetaText, false, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.MetaText), new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.14f));
                    break;

                case FisherSlotLayout.CookingRecipe:
                    SetAnchoredPercent(iconRect, new Vector2(0.24f, 0.48f), new Vector2(0.76f, 0.88f));
                    ConfigureSlotText(slot.BadgeText, true, 13f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.BadgeText), new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.98f));
                    ConfigureSlotText(slot.NameText, true, 15f, FontStyles.Bold, TextAlignmentOptions.Center, true);
                    SetAnchoredPercent(TextRect(slot.NameText), new Vector2(0.06f, 0.27f), new Vector2(0.94f, 0.46f));
                    ConfigureSlotText(slot.QuantityText, true, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.QuantityText), new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.26f));
                    ConfigureSlotText(slot.MetaText, true, 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.MetaText), new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.13f));
                    break;

                case FisherSlotLayout.CookingIngredient:
                    SetAnchoredPercent(iconRect, new Vector2(0.24f, 0.42f), new Vector2(0.76f, 0.86f));
                    ConfigureSlotText(slot.BadgeText, false, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.BadgeText), new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.97f));
                    ConfigureSlotText(slot.NameText, true, 16f, FontStyles.Bold, TextAlignmentOptions.Center, true);
                    SetAnchoredPercent(TextRect(slot.NameText), new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.42f));
                    ConfigureSlotText(slot.QuantityText, true, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.QuantityText), new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.25f));
                    ConfigureSlotText(slot.MetaText, false, 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.MetaText), new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.12f));
                    break;

                case FisherSlotLayout.Shop:
                    SetAnchoredPercent(iconRect, new Vector2(0.30f, 0.50f), new Vector2(0.70f, 0.84f));
                    ConfigureSlotText(slot.BadgeText, false, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.BadgeText), new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.98f));
                    ConfigureSlotText(slot.NameText, true, 27f, FontStyles.Bold, TextAlignmentOptions.Center, true);
                    SetAnchoredPercent(TextRect(slot.NameText), new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.34f));
                    ConfigureSlotText(slot.QuantityText, false, 28f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.QuantityText), new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.30f));
                    ConfigureSlotText(slot.MetaText, false, 25f, FontStyles.Normal, TextAlignmentOptions.Center, true);
                    SetAnchoredPercent(TextRect(slot.MetaText), new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.16f));
                    break;

                case FisherSlotLayout.Collection:
                    SetAnchoredPercent(iconRect, new Vector2(0.27f, 0.50f), new Vector2(0.73f, 0.82f));
                    ConfigureSlotText(slot.BadgeText, true, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.BadgeText), new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.98f));
                    ConfigureSlotText(slot.NameText, false, 19f, FontStyles.Bold, TextAlignmentOptions.Center, true);
                    SetAnchoredPercent(TextRect(slot.NameText), new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.46f));
                    ConfigureSlotText(slot.QuantityText, false, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.QuantityText), new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.24f));
                    ConfigureSlotText(slot.MetaText, true, 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    SetAnchoredPercent(TextRect(slot.MetaText), new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.22f));
                    break;

                case FisherSlotLayout.Detail:
                    SetAnchoredPercent(iconRect, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
                    ConfigureSlotText(slot.BadgeText, false, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
                    ConfigureSlotText(slot.NameText, false, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
                    ConfigureSlotText(slot.QuantityText, false, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
                    ConfigureSlotText(slot.MetaText, false, 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    break;
            }
        }

        private static RectTransform TextRect(TextMeshProUGUI text)
        {
            return SlotFrameRect(text == null ? null : text.rectTransform);
        }

        private static RectTransform SlotFrameRect(RectTransform rect)
        {
            if (rect == null)
            {
                return null;
            }

            return rect.parent is RectTransform parent && parent.name.EndsWith("Panel")
                ? parent
                : rect;
        }

        private static void ApplyBagLayout(FisherPanelView view)
        {
            SetPercentBand(view.HeaderRoot, 6f, 6f, 0.86f, 0.995f);
            SetPercentBand(view.CategoryTabsRoot, 6f, 6f, 0.78f, 0.855f);
            SetPercentBand(view.GridRoot, 6f, 6f, 0.36f, 0.775f);
            SetPercentBand(view.DetailRoot, 6f, 6f, 0.13f, 0.35f);
            SetPercentBand(view.ActionsRoot, 6f, 6f, 0.035f, 0.12f);
            SetPercentBand(view.QuantitySheet == null ? null : view.QuantitySheet.GetComponent<RectTransform>(), 6f, 6f, 0.12f, 0.34f);
            SetPercentBand(view.ConfirmSheet == null ? null : view.ConfirmSheet.GetComponent<RectTransform>(), 6f, 6f, 0.12f, 0.34f);
        }

        private static void ApplyCookingLayout(FisherPanelView view)
        {
            SetPercentBand(view.HeaderRoot, 6f, 6f, 0.855f, 0.995f);
            SetPercentBand(view.CategoryTabsRoot, 6f, 6f, 0.84f, 0.84f);
            if (view.CategoryTabsRoot != null)
            {
                view.CategoryTabsRoot.gameObject.SetActive(false);
            }

            SetPercentBand(view.GridRoot, 6f, 6f, 0.700f, 0.845f);
            SetPercentBand(view.RecipeGridRoot, 6f, 6f, 0.245f, 0.690f);
            SetPercentBand(view.IngredientGridRoot, 6f, 6f, 0.505f, 0.690f);
            SetPercentBand(view.DetailRoot, 6f, 6f, 0.245f, 0.495f);
            SetPercentBand(view.ActionsRoot, 6f, 6f, 0.035f, 0.225f);
            SetPercentBand(view.QuantitySheet == null ? null : view.QuantitySheet.GetComponent<RectTransform>(), 6f, 6f, 0.225f, 0.50f);
            SetPercentBand(view.ConfirmSheet == null ? null : view.ConfirmSheet.GetComponent<RectTransform>(), 6f, 6f, 0.225f, 0.50f);
        }

        private static void ApplyShopLayout(FisherPanelView view)
        {
            SetPercentBand(view.HeaderRoot, 6f, 6f, 0.86f, 0.995f);
            SetPercentBand(view.CategoryTabsRoot, 6f, 6f, 0.78f, 0.855f);
            SetPercentBand(view.GridRoot, 8f, 8f, 0.08f, 0.785f);
            SetPercentBand(view.DetailRoot, 8f, 8f, 0.095f, 0.17f);
            SetPercentBand(view.ActionsRoot, 8f, 8f, 0.035f, 0.12f);
        }

        private static void ApplyCollectionLayout(FisherPanelView view)
        {
            SetPercentBand(view.HeaderRoot, 6f, 6f, 0.855f, 0.995f);
            SetPercentBand(view.CategoryTabsRoot, 6f, 6f, 0.775f, 0.852f);
            SetPercentBand(view.GridRoot, 8f, 8f, 0.22f, 0.765f);
            SetPercentBand(view.DetailRoot, 8f, 8f, 0.12f, 0.21f);
            SetPercentBand(view.ActionsRoot, 8f, 8f, 0.035f, 0.11f);
        }

        private static void SetPercentBand(RectTransform rect, float left, float right, float yMin, float yMax)
        {
            if (rect == null)
            {
                return;
            }

            float safeMin = Mathf.Clamp01(Mathf.Min(yMin, yMax));
            float safeMax = Mathf.Clamp01(Mathf.Max(yMin, yMax));
            rect.anchorMin = new Vector2(0f, safeMin);
            rect.anchorMax = new Vector2(1f, safeMax);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, 0f);
        }

        private static void SetAnchoredPercent(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ConfigureSlotText(TextMeshProUGUI text, bool active, float maxSize, FontStyles style, TextAlignmentOptions alignment, bool wrap = false)
        {
            if (text == null)
            {
                return;
            }

            text.gameObject.SetActive(active);
            text.fontSizeMax = maxSize;
            text.fontSizeMin = Mathf.Max(12f, maxSize - 5f);
            text.fontStyle = style;
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
        }
    }
}
