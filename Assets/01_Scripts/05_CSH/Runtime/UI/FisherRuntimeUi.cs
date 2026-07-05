using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 같은 item slot 컴포넌트를 화면 목적별로 다르게 배치하기 위한 View 모드입니다.
    /// </summary>
    public enum FisherSlotLayout
    {
        Bag,
        Cooking,
        Shop,
        Collection,
        Detail,
        CookingProgress,
        CookingRecipe,
        CookingIngredient
    }

    /// <summary>
    /// Fisher 런타임 패널이 공통으로 쓰는 모바일 UI 생성 유틸리티입니다.
    /// </summary>
    internal static class FisherRuntimeUi
    {
        #region Palette

        private static readonly Color DefaultPanelColor = new Color(0.08f, 0.1f, 0.12f, 0.96f);
        private static readonly Color DefaultSectionColor = new Color(0.06f, 0.08f, 0.09f, 0.5f);
        private static readonly Color DefaultCardColor = new Color(0.12f, 0.18f, 0.22f, 0.92f);
        private static readonly Color DefaultButtonColor = new Color(0.2f, 0.32f, 0.42f, 1f);
        private static readonly Color DefaultButtonSelectedColor = new Color(0.36f, 0.52f, 0.64f, 1f);
        private static readonly Color DefaultButtonDisabledColor = new Color(0.28f, 0.28f, 0.28f, 1f);
        private static readonly Color DefaultTextColor = Color.white;
        private static readonly Color DefaultParchmentTextColor = new Color(0.18f, 0.11f, 0.06f, 1f);
        private static readonly Color DefaultCurrencyStripColor = new Color(0.05f, 0.11f, 0.16f, 0.94f);
        private static readonly Color DefaultToastColor = new Color(0.05f, 0.11f, 0.16f, 0.96f);
        private static readonly Color DefaultInputColor = new Color(0.08f, 0.13f, 0.16f, 1f);
        private static readonly Color DefaultIconColor = new Color(0.16f, 0.23f, 0.27f, 1f);
        private static readonly Color DefaultSlotNormalColor = new Color(0.12f, 0.18f, 0.2f, 0.95f);
        private static readonly Color DefaultSlotSelectedColor = new Color(0.36f, 0.52f, 0.64f, 0.9f);
        private static readonly Color DefaultSlotEmptyColor = new Color(0.12f, 0.18f, 0.2f, 0.88f);
        private static readonly Color DefaultSlotDimmedColor = new Color(0.08f, 0.1f, 0.11f, 0.35f);
        private static readonly Color DefaultSelectedFrameColor = new Color(0.55f, 0.78f, 1f, 0.55f);
        private static readonly Color DefaultNewBadgeColor = new Color(0.82f, 0.16f, 0.1f, 1f);
        private static readonly Color DefaultLockedBadgeColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color DefaultOutlineColor = new Color(0f, 0f, 0f, 0.95f);
        private static FisherUiArtProfile _activeProfile;

        public static FisherUiArtProfile ActiveProfile => _activeProfile;
        public static Color PanelColor => _activeProfile == null ? DefaultPanelColor : _activeProfile.PanelColor;
        public static Color SectionColor => _activeProfile == null ? DefaultSectionColor : _activeProfile.SectionColor;
        public static Color CardColor => _activeProfile == null ? DefaultCardColor : _activeProfile.DetailColor;
        public static Color ButtonColor => _activeProfile == null ? DefaultButtonColor : _activeProfile.ButtonNormalColor;
        public static Color ButtonSelectedColor => _activeProfile == null ? DefaultButtonSelectedColor : _activeProfile.ButtonSelectedColor;
        public static Color ButtonDisabledColor => _activeProfile == null ? DefaultButtonDisabledColor : _activeProfile.ButtonDisabledColor;
        public static Color TextColor => _activeProfile == null ? DefaultTextColor : _activeProfile.TextPrimary;
        public static Color ParchmentTextColor => DefaultParchmentTextColor;
        public static Color InputColor => _activeProfile == null ? DefaultInputColor : _activeProfile.InputColor;
        public static Color IconColor => _activeProfile == null ? DefaultIconColor : _activeProfile.IconFrameColor;
        public static Color SlotNormalColor => _activeProfile == null ? DefaultSlotNormalColor : _activeProfile.SlotNormalColor;
        public static Color SlotSelectedColor => _activeProfile == null ? DefaultSlotSelectedColor : _activeProfile.SlotSelectedColor;
        public static Color SlotEmptyColor => _activeProfile == null ? DefaultSlotEmptyColor : _activeProfile.SlotEmptyColor;
        public static Color SlotDimmedColor => _activeProfile == null ? DefaultSlotDimmedColor : _activeProfile.SlotDimmedColor;
        public static Color SelectedFrameColor => _activeProfile == null ? DefaultSelectedFrameColor : _activeProfile.SelectedFrameColor;
        public static Color NewBadgeColor => _activeProfile == null ? DefaultNewBadgeColor : _activeProfile.NewBadgeColor;
        public static Color LockedBadgeColor => _activeProfile == null ? DefaultLockedBadgeColor : _activeProfile.LockedBadgeColor;
        public static Color OutlineColor => _activeProfile == null ? DefaultOutlineColor : _activeProfile.OutlineColor;

        public static void SetActiveProfile(FisherUiArtProfile profile)
        {
            _activeProfile = profile;
        }

        #endregion

        #region Containers

        /// <summary>
        /// 기존 패널 안에 Fisher 전용 루트를 새로 만들고 이전 루트는 제거합니다.
        /// </summary>
        public static GameObject CreatePanelRoot(GameObject panel, string rootName, string title, TextMeshProUGUI template, Sprite backgroundSprite = null)
        {
            if (panel == null)
            {
                return null;
            }

            Transform oldRoot = panel.transform.Find(rootName);
            if (oldRoot != null)
            {
                oldRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(oldRoot.gameObject);
            }

            GameObject root = new GameObject(rootName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(panel.transform, false);
            root.transform.SetAsLastSibling();

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(10f, 10f);
            rect.offsetMax = new Vector2(-10f, -10f);

            Image image = root.GetComponent<Image>();
            image.color = PanelColor;
            ApplyOptionalSprite(image, backgroundSprite);

            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI titleText = CreateText(root.transform, title, template, 32f, FontStyles.Bold, TextColor);
            SetHeight(titleText.gameObject, 46f);
            return root;
        }

        /// <summary>
        /// 고정 구획 UI용 Fisher 전용 루트를 만듭니다. 자식 배치는 각 패널 어댑터가 RectTransform으로 직접 잡습니다.
        /// </summary>
        public static GameObject CreateAbsolutePanelRoot(GameObject panel, string rootName, Sprite backgroundSprite = null)
        {
            if (panel == null)
            {
                return null;
            }

            Transform oldRoot = panel.transform.Find(rootName);
            if (oldRoot != null)
            {
                oldRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(oldRoot.gameObject);
            }

            GameObject root = new GameObject(rootName, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(panel.transform, false);
            root.transform.SetAsLastSibling();

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(10f, 10f);
            rect.offsetMax = new Vector2(-10f, -10f);

            Image image = root.GetComponent<Image>();
            image.color = PanelColor;
            ApplyOptionalSprite(image, backgroundSprite);
            return root;
        }

        /// <summary>
        /// 부모 상단 기준으로 높이가 고정된 stretch 영역을 만듭니다.
        /// </summary>
        public static GameObject CreateTopStretchBox(Transform parent, string name, float left, float right, float top, float height, Color? color = null, Sprite sprite = null)
        {
            GameObject box = new GameObject(name, typeof(RectTransform));
            box.transform.SetParent(parent, false);
            RectTransform rect = box.GetComponent<RectTransform>();
            SetTopStretch(rect, left, right, top, height);

            if (color.HasValue || sprite != null)
            {
                Image image = box.AddComponent<Image>();
                image.color = color ?? Color.white;
                ApplyOptionalSprite(image, sprite);
                image.raycastTarget = false;
            }

            return box;
        }

        /// <summary>
        /// RectTransform을 부모 상단 기준 stretch 박스로 배치합니다.
        /// </summary>
        public static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// 이미 생성된 자식을 부모 전체에 맞춥니다.
        /// </summary>
        public static void StretchToParent(RectTransform rect, float inset = 0f)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>
        /// 버튼, 리스트 항목처럼 가로로 배치되는 한 줄 컨테이너를 만듭니다.
        /// </summary>
        public static GameObject CreateRow(Transform parent, string name, float height = 96f)
        {
            GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            SetHeight(row, height);
            return row;
        }

        /// <summary>
        /// 세부 정보나 리스트처럼 세로로 쌓이는 컨테이너를 만듭니다.
        /// </summary>
        public static GameObject CreateColumn(Transform parent, string name)
        {
            GameObject column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            column.transform.SetParent(parent, false);
            VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return column;
        }

        /// <summary>
        /// 강조 배경이 필요한 상세 정보 카드 영역을 만듭니다.
        /// </summary>
        public static GameObject CreateCard(Transform parent, string name)
        {
            GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(parent, false);
            Image image = card.GetComponent<Image>();
            image.color = CardColor;
            image.raycastTarget = false;

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return card;
        }

        /// <summary>
        /// 아이콘 기반 목록을 위한 고정 셀 스크롤 그리드를 만듭니다.
        /// </summary>
        public static Transform CreateScrollGrid(
            Transform parent,
            string name,
            float height,
            int columns,
            Vector2 cellSize,
            Vector2 spacing,
            RectOffset padding)
        {
            GameObject scrollObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            if (parent.GetComponent<LayoutGroup>() == null)
            {
                StretchToParent(scrollRectTransform);
            }

            Image background = scrollObject.GetComponent<Image>();
            background.color = SectionColor;
            background.raycastTarget = false;

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(4f, 4f);
            viewportRect.offsetMax = new Vector2(-4f, -4f);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = padding ?? new RectOffset(0, 0, 0, 0);
            grid.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            SetHeight(scrollObject, height);
            return contentObject.transform;
        }

        #endregion

        #region Controls

        /// <summary>
        /// 모바일 세로 화면에서 읽을 수 있는 크기의 TextMeshProUGUI를 생성합니다.
        /// </summary>
        public static TextMeshProUGUI CreateText(Transform parent, string text, TextMeshProUGUI template, float size = 30f, FontStyles style = FontStyles.Normal, Color? color = null)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(tmp, template, size, style, color ?? TextColor);
            EnsureGlyphs(tmp.font, text);
            tmp.text = text;
            SetHeight(textObject, Mathf.Ceil(size * 1.45f));
            return tmp;
        }

        /// <summary>
        /// 터치 입력용 최소 높이를 가진 텍스트 버튼을 생성합니다.
        /// </summary>
        public static Button CreateButton(Transform parent, string label, TextMeshProUGUI template, Color? color = null, Sprite sprite = null)
        {
            GameObject buttonObject = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = color ?? ButtonColor;
            image.raycastTarget = true;
            ApplyOptionalSprite(image, sprite);

            Button button = buttonObject.GetComponent<Button>();
            HorizontalLayoutGroup layout = buttonObject.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 5);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            TextMeshProUGUI text = CreateText(buttonObject.transform, label, template, 28f, FontStyles.Bold, TextColor);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 28f;
            SetHeight(buttonObject, 60f);
            SetFlexible(buttonObject, 1f);
            return button;
        }

        /// <summary>
        /// 숫자를 직접 입력할 수 있는 TMP input field를 생성합니다.
        /// </summary>
        public static TMP_InputField CreateNumberInput(Transform parent, string value, TextMeshProUGUI template, Action<string> onEndEdit, int maxValue = int.MaxValue)
        {
            GameObject inputObject = new GameObject("Input_Number", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputObject.transform.SetParent(parent, false);

            Image image = inputObject.GetComponent<Image>();
            image.color = InputColor;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(inputObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(text, template, 28f, FontStyles.Bold, TextColor);
            text.alignment = TextAlignmentOptions.Center;
            text.text = value;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.textViewport = textRect;
            input.textComponent = text;
            input.text = value;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            int clampedMax = Mathf.Max(1, maxValue);
            input.characterLimit = clampedMax.ToString().Length;
            input.onValueChanged.AddListener(raw =>
            {
                if (int.TryParse(raw, out int parsed) && parsed > clampedMax)
                {
                    input.SetTextWithoutNotify(clampedMax.ToString());
                    input.caretPosition = input.text.Length;
                }
            });
            input.onEndEdit.AddListener(raw => onEndEdit?.Invoke(raw));

            SetHeight(inputObject, 60f);
            SetFlexible(inputObject, 1.4f);
            return input;
        }

        /// <summary>
        /// 나중에 아이콘 에셋을 붙일 수 있는 고정 크기 슬롯을 만듭니다.
        /// </summary>
        public static GameObject CreateIconSlot(Transform parent, string label, TextMeshProUGUI template, float size = 64f, Sprite iconSprite = null, Sprite frameSprite = null)
        {
            GameObject slot = new GameObject("IconSlot", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(parent, false);
            Image image = slot.GetComponent<Image>();
            image.color = IconColor;
            image.raycastTarget = false;
            ApplyOptionalSprite(image, frameSprite);

            if (iconSprite != null)
            {
                GameObject iconObject = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(slot.transform, false);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(6f, 6f);
                iconRect.offsetMax = new Vector2(-6f, -6f);

                Image iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = iconSprite;
                iconImage.color = TextColor;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            LayoutElement layout = slot.AddComponent<LayoutElement>();
            layout.minWidth = size;
            layout.preferredWidth = size;
            layout.minHeight = size;
            layout.preferredHeight = size;
            layout.flexibleWidth = 0f;
            return slot;
        }

        /// <summary>
        /// 32x32 아이콘 에셋을 나중에 끼울 수 있는 공통 아이템 슬롯 버튼을 만듭니다.
        /// </summary>
        public static Button CreateItemSlotButton(
            Transform parent,
            string key,
            string title,
            string quantity,
            string badge,
            TextMeshProUGUI template,
            bool selected = false,
            bool dimmed = false,
            float height = 178f,
            Sprite iconSprite = null,
            Sprite slotSprite = null)
        {
            GameObject buttonObject = new GameObject("ItemSlot_" + SafeObjectName(key), typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = dimmed
                ? SlotDimmedColor
                : selected
                    ? SlotSelectedColor
                    : SlotNormalColor;
            image.raycastTarget = true;
            ApplyOptionalSprite(image, slotSprite);

            VerticalLayoutGroup layout = buttonObject.GetComponent<VerticalLayoutGroup>();
            int horizontalPadding = Mathf.RoundToInt(Mathf.Clamp(height * 0.06f, 6f, 10f));
            int verticalPadding = Mathf.RoundToInt(Mathf.Clamp(height * 0.05f, 5f, 8f));
            layout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
            layout.spacing = Mathf.Clamp(height * 0.03f, 3f, 6f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            float iconSize = Mathf.Clamp(height * 0.42f, 56f, 70f);
            float titleSize = Mathf.Clamp(height * 0.16f, 22f, 26f);
            float quantitySize = Mathf.Clamp(height * 0.14f, 18f, 22f);
            CreateIconSlot(buttonObject.transform, string.IsNullOrEmpty(badge) ? "I" : badge, template, iconSize, iconSprite);
            TextMeshProUGUI titleText = CreateText(buttonObject.transform, title, template, titleSize, FontStyles.Bold, TextColor);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.textWrappingMode = TextWrappingModes.Normal;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 18f;
            titleText.fontSizeMax = titleSize;
            TextMeshProUGUI quantityText = CreateText(buttonObject.transform, quantity, template, quantitySize, FontStyles.Normal, TextColor);
            quantityText.alignment = TextAlignmentOptions.Center;
            quantityText.textWrappingMode = TextWrappingModes.Normal;
            quantityText.enableAutoSizing = true;
            quantityText.fontSizeMin = 17f;
            quantityText.fontSizeMax = quantitySize;

            SetHeight(buttonObject, height);
            SetFlexible(buttonObject, 1f);
            return buttonObject.GetComponent<Button>();
        }

        #endregion

        #region Layout Helpers

        /// <summary>
        /// 자동 레이아웃 안에서 높이를 고정해 목록이 흔들리지 않게 합니다.
        /// </summary>
        public static void SetHeight(GameObject gameObject, float height)
        {
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }

            LayoutElement layout = gameObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
            }

            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        /// <summary>
        /// 같은 줄의 형제 요소 사이에서 차지할 가로 비율을 설정합니다.
        /// </summary>
        public static void SetFlexible(GameObject gameObject, float flexibleWidth = 1f)
        {
            LayoutElement layout = gameObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
            }

            layout.flexibleWidth = flexibleWidth;
        }

        /// <summary>
        /// 스프라이트가 지정된 경우 Image에 안전하게 적용합니다.
        /// </summary>
        public static bool ApplyOptionalSprite(Image image, Sprite sprite)
        {
            if (image == null || sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            return true;
        }

        /// <summary>
        /// itemId에 직접 연결된 아트 프로필 또는 데이터 에셋 아이콘을 사용합니다.
        /// </summary>
        public static Sprite ResolveItemIcon(FisherUiArtProfile profile, string itemId, string category)
        {
            if (profile != null)
            {
                Sprite itemIcon = profile.FindItemIcon(itemId);
                if (itemIcon != null)
                {
                    return itemIcon;
                }
            }

            Sprite dataIcon = ResolveDataIcon(itemId);
            if (dataIcon != null)
            {
                return dataIcon;
            }

            if (profile != null)
            {
                return null;
            }

            return null;
        }

#if UNITY_EDITOR
        private static readonly Dictionary<string, Sprite> EditorDataIconCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static bool editorDataIconCacheBuilt;
#endif

        private static Sprite ResolveDataIcon(string itemId)
        {
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            EnsureEditorDataIconCache();
            return EditorDataIconCache.TryGetValue(itemId, out Sprite icon) ? icon : null;
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        private static void EnsureEditorDataIconCache()
        {
            if (editorDataIconCacheBuilt)
            {
                return;
            }

            editorDataIconCacheBuilt = true;
            EditorDataIconCache.Clear();
            CacheRmsFishIcons();
            CacheCshItemIcons();
            CacheCshRecipeIcons();
        }

        private static void CacheRmsFishIcons()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:FishData", new[] { "Assets/03_Data/01_RMS" });
            for (int i = 0; i < guids.Length; i++)
            {
                RMS.Data.FishData fish = UnityEditor.AssetDatabase.LoadAssetAtPath<RMS.Data.FishData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]));
                if (fish != null && !string.IsNullOrWhiteSpace(fish.FishId) && fish.Icon != null)
                {
                    EditorDataIconCache[fish.FishId] = fish.Icon;
                }
            }
        }

        private static void CacheCshItemIcons()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/03_Data/05_CSH" });
            for (int i = 0; i < guids.Length; i++)
            {
                Fisher.Data.ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<Fisher.Data.ItemData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]));
                if (item != null && !string.IsNullOrWhiteSpace(item.ItemId) && item.Icon != null)
                {
                    EditorDataIconCache[item.ItemId] = item.Icon;
                }
            }
        }

        private static void CacheCshRecipeIcons()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:RecipeData", new[] { "Assets/03_Data/05_CSH" });
            for (int i = 0; i < guids.Length; i++)
            {
                Fisher.Data.RecipeData recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<Fisher.Data.RecipeData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]));
                if (recipe == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(recipe.RecipeId) && recipe.Icon != null)
                {
                    EditorDataIconCache[recipe.RecipeId] = recipe.Icon;
                }

                Fisher.Data.ItemData output = recipe.OutputItem;
                if (output != null && !string.IsNullOrWhiteSpace(output.ItemId) && output.Icon != null)
                {
                    EditorDataIconCache[output.ItemId] = output.Icon;
                }
            }
        }
#endif

        /// <summary>
        /// 슬롯 상태에 맞는 공통 슬롯 스프라이트를 반환합니다.
        /// </summary>
        public static Sprite ResolveSlotSprite(FisherUiArtProfile profile, bool selected, bool empty = false)
        {
            if (profile == null)
            {
                return null;
            }

            if (empty && profile.SlotEmpty != null)
            {
                return profile.SlotEmpty;
            }

            if (selected && profile.SlotSelected != null)
            {
                return profile.SlotSelected;
            }

            return profile.SlotNormal;
        }

        /// <summary>
        /// 슬롯 그리드 행의 빈 칸을 같은 footprint로 채웁니다.
        /// </summary>
        public static GameObject CreateFlexibleSpacer(Transform parent, string name, float height = 60f)
        {
            GameObject spacer = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            SetHeight(spacer, height);
            SetFlexible(spacer, 1f);
            return spacer;
        }

        #endregion

        #region P0 Feedback UI

        private const string BagCurrencyStripName = "BagCurrencyStrip";
        private const string ShopCurrencyStripName = "ShopCurrencyStrip";
        private const string ResultToastName = "FisherResultFeedbackToast";
        private const string CookingCompletePanelName = "CookingCompletePanel";
        private const string CollectionReceiptName = "collection_receipt";
        private const string CookingTimePanelName = "CookingTimePanel";
        private const float ToastPanelWidth = 420f;
        private const float ToastPanelHeight = 170f;
        private const float ToastIconSize = 72f;
        private const float ToastTextColumnMinWidth = 270f;
        private const float ToastTextColumnPreferredWidth = 288f;
        private const float ToastTitleFontSize = 34f;
        private const float ToastTitleMinFontSize = 32f;
        private const float ToastTitleMaxFontSize = 36f;
        private const float ToastMetaFontSize = 30f;
        private const float ToastMetaMinFontSize = 28f;
        private const float ToastMetaMaxFontSize = 32f;

        public static void ApplyParchmentText(params TextMeshProUGUI[] texts)
        {
            if (texts == null)
            {
                return;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    texts[i].color = ParchmentTextColor;
                }
            }
        }

        public static void RefreshWrappedTextPanel(TextMeshProUGUI text)
        {
            if (text == null || text.transform == null)
            {
                return;
            }

            Transform parent = text.transform.parent;
            if (parent == null || !parent.name.EndsWith("Panel", StringComparison.Ordinal))
            {
                return;
            }

            if (parent.GetComponent<Image>() == null)
            {
                return;
            }

            parent.gameObject.SetActive(!string.IsNullOrWhiteSpace(text.text));
        }

        public static void ApplyHeaderStatusParchmentText(FisherPanelView view)
        {
            if (view == null)
            {
                return;
            }

            ApplyParchmentText(view.TitleText, view.StatusText, view.SubStatusText);
            EnsureHeaderInfoPanels(view);
        }

        public static void ApplyDetailParchmentText(FisherPanelView view)
        {
            if (view == null)
            {
                return;
            }

            ApplyParchmentText(view.DetailTitleText, view.DetailMetaText, view.DetailBodyText);
            EnsureDetailInfoPanels(view);
        }

        public static void EnsureBagCurrencyStrip(
            FisherPanelView view,
            PlayerRuntimeState state,
            TextMeshProUGUI template,
            FisherUiArtProfile profile)
        {
            EnsureCurrencyStrip(view, BagCurrencyStripName, state, template, profile);
        }

        public static void EnsureShopCurrencyStrip(
            FisherPanelView view,
            PlayerRuntimeState state,
            TextMeshProUGUI template,
            FisherUiArtProfile profile)
        {
            EnsureCurrencyStrip(view, ShopCurrencyStripName, state, template, profile);
        }

        public static void ShowResultToast(MonoBehaviour owner, Transform scope, Sprite icon, string title, string meta)
        {
            ShowToast(owner, scope, ResultToastName, icon, title, meta);
        }

        public static void ShowCookingCompletePanel(MonoBehaviour owner, Transform scope, Sprite icon, string title, string meta)
        {
            ShowToast(owner, scope, CookingCompletePanelName, icon, title, meta);
        }

        public static void ShowCollectionReceipt(MonoBehaviour owner, Transform scope, Sprite icon, string itemName, string quantityText)
        {
            Canvas canvas = ResolveFeedbackCanvas(owner, scope);
            if (canvas == null)
            {
                return;
            }

            HideFeedbackOverlay(canvas, ResultToastName);
            HideFeedbackOverlay(canvas, CookingCompletePanelName);
            GameObject receipt = EnsureCollectionReceiptOverlay(canvas, FindTextTemplate(canvas.gameObject), false);
            if (receipt == null)
            {
                return;
            }

            receipt.transform.SetAsLastSibling();
            receipt.SetActive(true);

            FisherFeedbackOverlayRuntime runtime = receipt.GetComponent<FisherFeedbackOverlayRuntime>();
            if (runtime == null)
            {
                runtime = receipt.AddComponent<FisherFeedbackOverlayRuntime>();
            }

            runtime.Show(icon, itemName, quantityText);
        }

        private static void ShowToast(MonoBehaviour owner, Transform scope, string toastName, Sprite icon, string title, string meta)
        {
            Canvas canvas = ResolveFeedbackCanvas(owner, scope);
            if (canvas == null)
            {
                return;
            }

            HideFeedbackOverlay(canvas, CollectionReceiptName);
            GameObject toast = EnsureToastOverlay(canvas, toastName, FindTextTemplate(canvas.gameObject), false, allowCreate: false);
            if (toast == null)
            {
                return;
            }

            toast.transform.SetAsLastSibling();
            toast.SetActive(true);

            FisherFeedbackOverlayRuntime runtime = toast.GetComponent<FisherFeedbackOverlayRuntime>();
            if (runtime == null)
            {
                runtime = toast.AddComponent<FisherFeedbackOverlayRuntime>();
            }

            runtime.Show(icon, title, meta);
        }

        public static GameObject EnsureResultToastOverlay(Canvas canvas, TextMeshProUGUI template, bool startHidden = true)
        {
            return EnsureToastOverlay(canvas, ResultToastName, template, startHidden, allowCreate: !Application.isPlaying);
        }

        public static GameObject EnsureCookingCompleteOverlay(Canvas canvas, TextMeshProUGUI template, bool startHidden = true)
        {
            return EnsureToastOverlay(canvas, CookingCompletePanelName, template, startHidden, allowCreate: !Application.isPlaying);
        }

        public static GameObject EnsureCollectionReceiptOverlay(Canvas canvas, TextMeshProUGUI template, bool startHidden = true)
        {
            if (canvas == null)
            {
                return null;
            }

            Transform existing = canvas.transform.Find(CollectionReceiptName);
            if (existing == null)
            {
                Debug.LogWarning("[FisherRuntimeUi] collection_receipt is missing under Canvas. Collection receipt uses its authored panel only.");
                return null;
            }

            GameObject receipt = existing.gameObject;
            CanvasGroup canvasGroup = receipt.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = receipt.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            if (startHidden)
            {
                if (Application.isPlaying)
                {
                    canvasGroup.alpha = 0f;
                }

                receipt.SetActive(false);
            }

            return receipt;
        }

        private static Canvas ResolveFeedbackCanvas(MonoBehaviour owner, Transform scope)
        {
            Transform feedbackScope = scope != null ? scope : owner == null ? null : owner.transform;
            Canvas canvas = feedbackScope == null ? FindAnyCanvas() : feedbackScope.GetComponentInParent<Canvas>(true);
            return canvas == null ? FindAnyCanvas() : canvas;
        }

        private static void HideFeedbackOverlay(Canvas canvas, string overlayName)
        {
            if (canvas == null || string.IsNullOrWhiteSpace(overlayName))
            {
                return;
            }

            Transform existing = canvas.transform.Find(overlayName);
            if (existing == null)
            {
                return;
            }

            CanvasGroup canvasGroup = existing.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            existing.gameObject.SetActive(false);
        }

        private static GameObject EnsureToastOverlay(Canvas canvas, string toastName, TextMeshProUGUI template, bool startHidden, bool allowCreate = true)
        {
            if (canvas == null)
            {
                return null;
            }

            string safeName = string.IsNullOrWhiteSpace(toastName) ? ResultToastName : toastName;
            Transform existing = canvas.transform.Find(safeName);
            bool createdToast = existing == null;
            if (createdToast && !allowCreate)
            {
                Debug.LogWarning("[FisherRuntimeUi] " + safeName + " is missing under Canvas. Runtime fallback UI creation was skipped.");
                return null;
            }

            GameObject toast = createdToast
                ? CreateResultToast(canvas.transform, safeName, template)
                : existing.gameObject;
            EnsureResultToastShape(toast, template, createdToast);

            CanvasGroup canvasGroup = toast.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = toast.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            if (startHidden)
            {
                if (Application.isPlaying)
                {
                    canvasGroup.alpha = 0f;
                }

                toast.SetActive(false);
            }

            return toast;
        }

        public static GameObject EnsureCookingTimePanel(FisherPanelView view, TextMeshProUGUI template)
        {
            if (view == null)
            {
                return null;
            }

            Transform searchRoot = view.ViewRoot != null ? view.ViewRoot : view.DetailRoot;
            Transform existing = FindDescendant(searchRoot, CookingTimePanelName);
            if (existing == null && view.DetailRoot == null)
            {
                return null;
            }

            bool createdPanel = existing == null;
            GameObject panel = createdPanel
                ? CreateCookingTimePanel(view.DetailRoot, template)
                : existing.gameObject;

            EnsureCookingTimePanelChildren(panel, template, createdPanel);
            return panel;
        }

        public static void SetCookingTimePanel(FisherPanelView view, TextMeshProUGUI template, int durationSeconds, int totalSeconds)
        {
            GameObject panel = EnsureCookingTimePanel(view, template);
            if (panel == null)
            {
                return;
            }

            panel.SetActive(true);
            TextMeshProUGUI labelText = FindTextChild(panel.transform, "LabelText", "TitleText");
            TextMeshProUGUI valueText = FindTextChild(panel.transform, "ValueText", "TimeText", "AmountText");
            if (labelText != null)
            {
                labelText.text = "조리 시간";
            }

            if (valueText != null)
            {
                valueText.text = Mathf.Max(0, durationSeconds) + "초 · 총 " + Mathf.Max(0, totalSeconds) + "초";
            }
        }

        public static void HideCookingTimePanel(FisherPanelView view)
        {
            if (view == null)
            {
                return;
            }

            Transform searchRoot = view.ViewRoot != null ? view.ViewRoot : view.DetailRoot;
            Transform panel = FindDescendant(searchRoot, CookingTimePanelName);
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }

        private static void EnsureCurrencyStrip(
            FisherPanelView view,
            string stripName,
            PlayerRuntimeState state,
            TextMeshProUGUI template,
            FisherUiArtProfile profile)
        {
            if (view == null || view.HeaderRoot == null)
            {
                return;
            }

            Transform existing = view.HeaderRoot.Find(stripName);
            GameObject strip = existing == null
                ? CreateCurrencyStrip(view.HeaderRoot, stripName, template, profile)
                : existing.gameObject;
            EnsureCurrencyStripChildren(strip, template, profile);
            strip.SetActive(true);
            strip.transform.SetAsLastSibling();

            long gold = state == null ? 0 : state.softCurrency;
            long prismPearl = state == null ? 0 : state.prismPearl;
            bool feedbackEnabled = state != null;
            UpdateCurrencyEntry(strip.transform.Find("GoldEntry"), ResolveItemIcon(profile, "gold", "gold"), "G", gold, feedbackEnabled);
            UpdateCurrencyEntry(strip.transform.Find("PrismPearlEntry"), ResolveItemIcon(profile, "prismPearl", "pearl"), "PP", prismPearl, feedbackEnabled);
        }

        private static GameObject CreateCurrencyStrip(
            RectTransform parent,
            string stripName,
            TextMeshProUGUI template,
            FisherUiArtProfile profile)
        {
            GameObject strip = new GameObject(stripName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            strip.transform.SetParent(parent, false);
            RectTransform rect = strip.GetComponent<RectTransform>();
            SetTopStretch(rect, 140f, 150f, 6f, 54f);

            Image background = strip.GetComponent<Image>();
            background.color = DefaultCurrencyStripColor;
            background.raycastTarget = false;

            HorizontalLayoutGroup layout = strip.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateCurrencyEntry(strip.transform, "GoldEntry", ResolveItemIcon(profile, "gold", "gold"), "G", template);
            CreateCurrencyEntry(strip.transform, "PrismPearlEntry", ResolveItemIcon(profile, "prismPearl", "pearl"), "PP", template);
            return strip;
        }

        private static void EnsureCurrencyStripChildren(GameObject strip, TextMeshProUGUI template, FisherUiArtProfile profile)
        {
            if (strip == null)
            {
                return;
            }

            if (strip.GetComponent<Image>() == null)
            {
                Image background = strip.AddComponent<Image>();
                background.color = DefaultCurrencyStripColor;
                background.raycastTarget = false;
            }

            if (strip.GetComponent<HorizontalLayoutGroup>() == null)
            {
                HorizontalLayoutGroup layout = strip.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(8, 8, 6, 6);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }

            EnsureCurrencyEntry(strip.transform, "GoldEntry", ResolveItemIcon(profile, "gold", "gold"), "G", template);
            EnsureCurrencyEntry(strip.transform, "PrismPearlEntry", ResolveItemIcon(profile, "prismPearl", "pearl"), "PP", template);
        }

        private static void EnsureCurrencyEntry(Transform parent, string name, Sprite icon, string fallbackLabel, TextMeshProUGUI template)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find(name);
            if (existing == null)
            {
                CreateCurrencyEntry(parent, name, icon, fallbackLabel, template);
                return;
            }

            if (existing.GetComponent<HorizontalLayoutGroup>() == null)
            {
                HorizontalLayoutGroup layout = existing.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 4f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
            }

            if (existing.GetComponent<LayoutElement>() == null)
            {
                LayoutElement entryLayout = existing.gameObject.AddComponent<LayoutElement>();
                entryLayout.flexibleWidth = 1f;
            }

            Transform iconTransform = existing.Find("Icon");
            if (iconTransform == null)
            {
                GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconObject.transform.SetParent(existing, false);
                Image iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.color = icon == null ? IconColor : Color.white;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
                iconLayout.minWidth = 32f;
                iconLayout.preferredWidth = 32f;
                iconLayout.minHeight = 32f;
                iconLayout.preferredHeight = 32f;
                TextMeshProUGUI iconFallback = CreateText(iconObject.transform, fallbackLabel, template, 14f, FontStyles.Bold, TextColor);
                iconFallback.name = "FallbackText";
                iconFallback.alignment = TextAlignmentOptions.Center;
                StretchToParent(iconFallback.GetComponent<RectTransform>(), 0f);
                iconFallback.gameObject.SetActive(icon == null);
            }

            if (existing.Find("AmountText") == null)
            {
                TextMeshProUGUI amountText = CreateText(existing, "0", template, 20f, FontStyles.Bold, TextColor);
                amountText.name = "AmountText";
                amountText.alignment = TextAlignmentOptions.MidlineLeft;
                amountText.enableAutoSizing = true;
                amountText.fontSizeMin = 14f;
                amountText.fontSizeMax = 20f;
                SetFlexible(amountText.gameObject, 1f);
            }
        }

        private static void CreateCurrencyEntry(Transform parent, string name, Sprite icon, string fallbackLabel, TextMeshProUGUI template)
        {
            GameObject entry = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            entry.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = entry.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            LayoutElement entryLayout = entry.GetComponent<LayoutElement>();
            entryLayout.flexibleWidth = 1f;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(entry.transform, false);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.color = icon == null ? IconColor : Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
            iconLayout.minWidth = 32f;
            iconLayout.preferredWidth = 32f;
            iconLayout.minHeight = 32f;
            iconLayout.preferredHeight = 32f;

            TextMeshProUGUI iconFallback = CreateText(iconObject.transform, fallbackLabel, template, 14f, FontStyles.Bold, TextColor);
            iconFallback.name = "FallbackText";
            iconFallback.alignment = TextAlignmentOptions.Center;
            StretchToParent(iconFallback.GetComponent<RectTransform>(), 0f);
            iconFallback.gameObject.SetActive(icon == null);

            TextMeshProUGUI amountText = CreateText(entry.transform, "0", template, 20f, FontStyles.Bold, TextColor);
            amountText.name = "AmountText";
            amountText.alignment = TextAlignmentOptions.MidlineLeft;
            amountText.enableAutoSizing = true;
            amountText.fontSizeMin = 14f;
            amountText.fontSizeMax = 20f;
            SetFlexible(amountText.gameObject, 1f);
        }

        private static void UpdateCurrencyEntry(Transform entry, Sprite icon, string fallbackLabel, long amount, bool feedbackEnabled)
        {
            if (entry == null)
            {
                return;
            }

            Transform iconTransform = entry.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent(out Image iconImage))
            {
                if (iconImage.sprite == null && icon != null)
                {
                    iconImage.sprite = icon;
                }

                iconImage.color = iconImage.sprite == null ? IconColor : Color.white;
                TextMeshProUGUI fallback = iconTransform.Find("FallbackText")?.GetComponent<TextMeshProUGUI>();
                if (fallback != null)
                {
                    fallback.text = fallbackLabel;
                    fallback.gameObject.SetActive(iconImage.sprite == null);
                }
            }

            TextMeshProUGUI amountText = entry.Find("AmountText")?.GetComponent<TextMeshProUGUI>();
            if (amountText != null)
            {
                amountText.text = CompactNumberFormatter.Format(amount);
                amountText.color = TextColor;
            }

            FisherCurrencyEntryFeedback feedback = entry.GetComponent<FisherCurrencyEntryFeedback>();
            if (feedback == null)
            {
                feedback = entry.gameObject.AddComponent<FisherCurrencyEntryFeedback>();
            }

            feedback.Bind(amountText, TextColor);
            feedback.SetAmount(amount, feedbackEnabled);
        }

        private static void EnsureHeaderInfoPanels(FisherPanelView view)
        {
            if (view == null || view.HeaderRoot == null)
            {
                return;
            }

            EnsureHeaderInfoPanel(view.HeaderRoot, view.TitleText, "TitlePanel", new Vector2(116f, 34f), new Vector2(8f, -4f));
            EnsureHeaderInfoPanel(view.HeaderRoot, view.StatusText, "StatusPanel", new Vector2(292f, 28f), new Vector2(8f, -42f));
            EnsureHeaderInfoPanel(view.HeaderRoot, view.SubStatusText, "SubStatusPanel", new Vector2(330f, 26f), new Vector2(8f, -70f));
        }

        private static void EnsureDetailInfoPanels(FisherPanelView view)
        {
            if (view == null || view.DetailRoot == null)
            {
                return;
            }

            EnsureDetailInfoPanel(view.DetailRoot, view.DetailTitleText, "DetailTitlePanel", 4f);
            EnsureDetailInfoPanel(view.DetailRoot, view.DetailMetaText, "DetailMetaPanel", 4f);
            EnsureDetailInfoPanel(view.DetailRoot, view.DetailBodyText, "DetailBodyPanel", 6f);
        }

        private static void EnsureDetailInfoPanel(
            RectTransform detailRoot,
            TextMeshProUGUI text,
            string panelName,
            float textInset)
        {
            if (detailRoot == null || text == null)
            {
                return;
            }

            RectTransform textRect = text.rectTransform;
            Transform currentParent = text.transform.parent;
            GameObject panel;
            bool created = false;
            if (currentParent != null && currentParent.name == panelName)
            {
                panel = currentParent.gameObject;
            }
            else
            {
                Transform existing = detailRoot.Find(panelName);
                if (existing == null)
                {
                    panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
                    panel.transform.SetParent(detailRoot, false);
                    created = true;
                }
                else
                {
                    panel = existing.gameObject;
                }

                if (created)
                {
                    RectTransform panelRect = panel.GetComponent<RectTransform>();
                    CopyRectTransform(textRect, panelRect);
                    int siblingIndex = text.transform.GetSiblingIndex();
                    panel.transform.SetSiblingIndex(siblingIndex);
                }

                text.transform.SetParent(panel.transform, false);
                StretchToParent(textRect, textInset);
            }

            Image image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
            }

            if (created || image.color.a <= 0.01f)
            {
                image.color = new Color(1f, 0.92f, 0.78f, 0.34f);
            }

            image.raycastTarget = false;
            text.color = ParchmentTextColor;
            text.raycastTarget = false;
            panel.SetActive(!string.IsNullOrWhiteSpace(text.text));
        }

        private static void EnsureHeaderInfoPanel(
            RectTransform headerRoot,
            TextMeshProUGUI text,
            string panelName,
            Vector2 defaultSize,
            Vector2 defaultPosition)
        {
            if (headerRoot == null || text == null)
            {
                return;
            }

            RectTransform textRect = text.rectTransform;
            Transform currentParent = text.transform.parent;
            GameObject panel;
            bool created = false;
            if (currentParent != null && currentParent.name == panelName)
            {
                panel = currentParent.gameObject;
            }
            else
            {
                Transform existing = headerRoot.Find(panelName);
                if (existing == null)
                {
                    panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
                    panel.transform.SetParent(headerRoot, false);
                    created = true;
                }
                else
                {
                    panel = existing.gameObject;
                }

                int siblingIndex = text.transform.GetSiblingIndex();
                panel.transform.SetSiblingIndex(siblingIndex);
                text.transform.SetParent(panel.transform, false);
                StretchToParent(textRect, 6f);
            }

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (created)
            {
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.sizeDelta = defaultSize;
                panelRect.anchoredPosition = defaultPosition;
            }

            Image image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
            }

            if (created || image.color.a <= 0.01f)
            {
                image.color = new Color(1f, 0.92f, 0.78f, 0.48f);
            }

            image.raycastTarget = false;
            text.color = ParchmentTextColor;
            text.raycastTarget = false;
            panel.SetActive(!string.IsNullOrWhiteSpace(text.text));
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
        }

        private static GameObject CreateCookingTimePanel(RectTransform detailRoot, TextMeshProUGUI template)
        {
            GameObject panel = new GameObject(CookingTimePanelName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            panel.transform.SetParent(detailRoot, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.54f, 0.06f);
            rect.anchorMax = new Vector2(0.96f, 0.30f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = panel.GetComponent<Image>();
            image.color = new Color(1f, 0.92f, 0.78f, 0.54f);
            image.raycastTarget = false;

            HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            EnsureCookingTimePanelChildren(panel, template, true);
            return panel;
        }

        private static void EnsureCookingTimePanelChildren(GameObject panel, TextMeshProUGUI template, bool repairGeneratedShape)
        {
            if (panel == null)
            {
                return;
            }

            Image image = panel.GetComponent<Image>();
            if (image == null && repairGeneratedShape)
            {
                image = panel.AddComponent<Image>();
                image.color = new Color(1f, 0.92f, 0.78f, 0.54f);
            }

            if (image != null)
            {
                image.raycastTarget = false;
            }

            if (repairGeneratedShape && panel.GetComponent<HorizontalLayoutGroup>() == null)
            {
                HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(10, 10, 4, 4);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
            }

            if (repairGeneratedShape && panel.transform.Find("LabelText") == null)
            {
                TextMeshProUGUI labelText = CreateText(panel.transform, "조리 시간", template, 16f, FontStyles.Bold, ParchmentTextColor);
                labelText.name = "LabelText";
                labelText.alignment = TextAlignmentOptions.MidlineLeft;
                SetFlexible(labelText.gameObject, 0f);
            }

            if (repairGeneratedShape && panel.transform.Find("ValueText") == null)
            {
                TextMeshProUGUI valueText = CreateText(panel.transform, "0초", template, 18f, FontStyles.Bold, ParchmentTextColor);
                valueText.name = "ValueText";
                valueText.alignment = TextAlignmentOptions.MidlineLeft;
                valueText.enableAutoSizing = true;
                valueText.fontSizeMin = 14f;
                valueText.fontSizeMax = 18f;
                SetFlexible(valueText.gameObject, 1f);
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindTextChild(Transform root, params string[] names)
        {
            if (root == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                Transform child = FindDescendant(root, names[i]);
                TextMeshProUGUI text = child == null ? null : child.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }

        private static GameObject CreateResultToast(Transform canvasTransform, string toastName, TextMeshProUGUI template)
        {
            GameObject toast = new GameObject(toastName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(HorizontalLayoutGroup));
            toast.transform.SetParent(canvasTransform, false);
            EnsureResultToastShape(toast, template, true);
            return toast;
        }

        private static T EnsureExclusiveLayoutGroup<T>(GameObject gameObject) where T : LayoutGroup
        {
            if (gameObject == null)
            {
                return null;
            }

            LayoutGroup[] groups = gameObject.GetComponents<LayoutGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && !(groups[i] is T))
                {
                    DestroyUiComponent(groups[i]);
                }
            }

            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        private static void DestroyUiComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(component);
        }

        private static void HideDirectChild(Transform parent, string name)
        {
            Transform child = parent == null || string.IsNullOrWhiteSpace(name) ? null : parent.Find(name);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static void EnsureResultToastShape(GameObject toast, TextMeshProUGUI template, bool forceDefaultLayout)
        {
            if (toast == null)
            {
                return;
            }

            RectTransform rect = toast.GetComponent<RectTransform>();
            bool applyDefaultLayout = forceDefaultLayout;
            if (rect != null && applyDefaultLayout)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(ToastPanelWidth, ToastPanelHeight);
                rect.anchoredPosition = Vector2.zero;
            }

            CanvasGroup canvasGroup = toast.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = toast.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            Image background = toast.GetComponent<Image>();
            if (background == null)
            {
                background = toast.AddComponent<Image>();
            }

            if (forceDefaultLayout || background.color.a <= 0.01f)
            {
                background.color = DefaultToastColor;
            }

            background.raycastTarget = false;

            HorizontalLayoutGroup layout = toast.GetComponent<HorizontalLayoutGroup>();
            bool createdLayout = false;
            if (layout == null)
            {
                layout = toast.AddComponent<HorizontalLayoutGroup>();
                createdLayout = true;
            }

            if (applyDefaultLayout || createdLayout)
            {
                layout.padding = new RectOffset(22, 22, 18, 18);
                layout.spacing = 16f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            Transform iconTransform = toast.transform.Find("Icon");
            GameObject iconObject = iconTransform == null
                ? new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement))
                : iconTransform.gameObject;
            if (iconTransform == null)
            {
                iconObject.transform.SetParent(toast.transform, false);
            }

            Image iconImage = iconObject.GetComponent<Image>();
            if (iconImage == null)
            {
                iconImage = iconObject.AddComponent<Image>();
            }

            iconImage.color = IconColor;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
            bool createdIconLayout = false;
            if (iconLayout == null)
            {
                iconLayout = iconObject.AddComponent<LayoutElement>();
                createdIconLayout = true;
            }

            if (applyDefaultLayout || createdIconLayout)
            {
                iconLayout.minWidth = ToastIconSize;
                iconLayout.preferredWidth = ToastIconSize;
                iconLayout.minHeight = ToastIconSize;
                iconLayout.preferredHeight = ToastIconSize;
            }

            Transform textColumnTransform = toast.transform.Find("TextColumn");
            GameObject textColumn = textColumnTransform == null
                ? new GameObject("TextColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement))
                : textColumnTransform.gameObject;
            if (textColumnTransform == null)
            {
                textColumn.transform.SetParent(toast.transform, false);
            }

            VerticalLayoutGroup columnLayout = textColumn.GetComponent<VerticalLayoutGroup>();
            bool createdColumnLayout = false;
            if (columnLayout == null)
            {
                columnLayout = textColumn.AddComponent<VerticalLayoutGroup>();
                createdColumnLayout = true;
            }

            if (applyDefaultLayout || createdColumnLayout)
            {
                columnLayout.spacing = 4f;
                columnLayout.childControlWidth = true;
                columnLayout.childControlHeight = false;
                columnLayout.childForceExpandWidth = true;
                columnLayout.childForceExpandHeight = false;
            }

            LayoutElement columnElement = textColumn.GetComponent<LayoutElement>();
            bool createdColumnElement = false;
            if (columnElement == null)
            {
                columnElement = textColumn.AddComponent<LayoutElement>();
                createdColumnElement = true;
            }

            if (applyDefaultLayout || createdColumnElement)
            {
                columnElement.minWidth = ToastTextColumnMinWidth;
                columnElement.preferredWidth = ToastTextColumnPreferredWidth;
                columnElement.flexibleWidth = 1f;
            }

            TextMeshProUGUI titleText = textColumn.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            bool createdTitleText = false;
            if (titleText == null)
            {
                titleText = CreateText(textColumn.transform, string.Empty, template, ToastTitleFontSize, FontStyles.Bold, TextColor);
                titleText.name = "TitleText";
                createdTitleText = true;
            }

            if (applyDefaultLayout || createdTitleText)
            {
                titleText.alignment = TextAlignmentOptions.MidlineLeft;
                titleText.enableAutoSizing = true;
                titleText.fontSize = ToastTitleFontSize;
                titleText.fontSizeMin = ToastTitleMinFontSize;
                titleText.fontSizeMax = ToastTitleMaxFontSize;
            }

            TextMeshProUGUI metaText = textColumn.transform.Find("MetaText")?.GetComponent<TextMeshProUGUI>();
            bool createdMetaText = false;
            if (metaText == null)
            {
                metaText = CreateText(textColumn.transform, string.Empty, template, ToastMetaFontSize, FontStyles.Normal, TextColor);
                metaText.name = "MetaText";
                createdMetaText = true;
            }

            if (applyDefaultLayout || createdMetaText)
            {
                metaText.alignment = TextAlignmentOptions.MidlineLeft;
                metaText.enableAutoSizing = true;
                metaText.fontSize = ToastMetaFontSize;
                metaText.fontSizeMin = ToastMetaMinFontSize;
                metaText.fontSizeMax = ToastMetaMaxFontSize;
            }
        }

        private static Canvas FindAnyCanvas()
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return canvases == null || canvases.Length == 0 ? null : canvases[0];
        }

        private sealed class FisherFeedbackOverlayRuntime : MonoBehaviour
        {
            private const float VisibleSeconds = 1.9f;
            private const float FadeSeconds = 0.24f;

            private CanvasGroup _canvasGroup;
            private Image _iconImage;
            private TextMeshProUGUI _titleText;
            private TextMeshProUGUI _metaText;
            private Coroutine _hideRoutine;

            public void Show(Sprite icon, string title, string meta)
            {
                Resolve();
                if (_canvasGroup == null)
                {
                    return;
                }

                if (_iconImage != null)
                {
                    _iconImage.sprite = icon;
                    _iconImage.color = icon == null ? IconColor : Color.white;
                    _iconImage.gameObject.SetActive(true);
                }

                if (_titleText != null)
                {
                    _titleText.text = string.IsNullOrWhiteSpace(title) ? "완료" : title;
                }

                if (_metaText != null)
                {
                    _metaText.text = meta ?? string.Empty;
                    _metaText.gameObject.SetActive(!string.IsNullOrWhiteSpace(meta));
                }

                _canvasGroup.alpha = 1f;
                gameObject.SetActive(true);
                if (_hideRoutine != null)
                {
                    StopCoroutine(_hideRoutine);
                }

                _hideRoutine = StartCoroutine(HideAfterDelay());
            }

            private void Resolve()
            {
                _canvasGroup ??= GetComponent<CanvasGroup>();
                Transform iconTransform = transform.Find("Icon");
                _iconImage ??= iconTransform == null ? null : iconTransform.GetComponent<Image>();
                _titleText ??= transform.Find("TextColumn/TitleText")?.GetComponent<TextMeshProUGUI>();
                _metaText ??= transform.Find("TextColumn/MetaText")?.GetComponent<TextMeshProUGUI>();
            }

            private IEnumerator HideAfterDelay()
            {
                yield return new WaitForSecondsRealtime(VisibleSeconds);
                float elapsed = 0f;
                while (elapsed < FadeSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (_canvasGroup != null)
                    {
                        _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / FadeSeconds);
                    }

                    yield return null;
                }

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                }

                gameObject.SetActive(false);
                _hideRoutine = null;
            }
        }

        #endregion

        #region Text Style

        /// <summary>
        /// 기존 패널의 TMP 폰트 설정을 재사용하기 위한 템플릿 텍스트를 찾습니다.
        /// </summary>
        public static TextMeshProUGUI FindTextTemplate(GameObject panel)
        {
            if (panel == null)
            {
                return null;
            }

            TextMeshProUGUI[] texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null)
                {
                    return texts[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 템플릿의 폰트와 재질을 가져오고 동적 TMP 폰트는 한글 글리프를 추가할 수 있게 준비합니다.
        /// </summary>
        public static void ApplyTextStyle(TextMeshProUGUI text, TextMeshProUGUI template, float size, FontStyles style, Color color)
        {
            if (_activeProfile != null && _activeProfile.FontAsset != null)
            {
                text.font = _activeProfile.FontAsset;
                if (_activeProfile.FontMaterial != null)
                {
                    text.fontSharedMaterial = _activeProfile.FontMaterial;
                }

                PrepareDynamicFont(text.font);
            }
            else if (template != null)
            {
                text.font = template.font;
                text.fontSharedMaterial = template.fontSharedMaterial;
                PrepareDynamicFont(text.font);
            }

            text.fontSize = size;
            text.fontStyle = style;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.color = color;
        }

        private static void PrepareDynamicFont(TMP_FontAsset font)
        {
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                return;
            }

            font.isMultiAtlasTexturesEnabled = true;
        }

        private static void EnsureGlyphs(TMP_FontAsset font, string text)
        {
            if (font == null ||
                string.IsNullOrEmpty(text) ||
                font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                return;
            }

            font.TryAddCharacters(text, out _);
        }

        private static string SafeObjectName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Empty";
            }

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        #endregion
    }
}
