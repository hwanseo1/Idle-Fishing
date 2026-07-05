using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    public enum FisherStaticViewLayoutMode
    {
        GenerateOrRepair = 0,
        PreserveInspectorLayout = 1,
        ForceGenerateOrRepair = 2
    }

    /// <summary>
    /// Static CSH UI 생성/보정 중 기존 Inspector 배치를 덮을 수 있는 쓰기 작업을 차단합니다.
    /// </summary>
    internal static class FisherStaticViewLayoutPolicy
    {
        public static bool CanWriteGeneratedLayout(FisherPanelView view)
        {
            return view != null && !view.PreserveInspectorLayout;
        }

        public static bool CanRewriteSlotLayout(bool preserveInspectorLayout)
        {
            return !preserveInspectorLayout;
        }
    }

    /// <summary>
    /// Fisher 패널의 고정 ViewRoot 계약입니다.
    /// 레이아웃과 아트는 이 컴포넌트의 Inspector 참조에 두고, 어댑터는 값만 갱신합니다.
    /// 05_CSH에서 검증한 구조를 00_MainScene으로 옮길 때 이 컴포넌트 단위로 참조를 확인합니다.
    /// </summary>
    public sealed class FisherPanelView : MonoBehaviour
    {
        [Header("Root")]
        /// <summary>Header, Tabs, Grid, Detail, Actions, ActionSheets를 포함하는 패널 최상위 루트입니다.</summary>
        public RectTransform ViewRoot;
        /// <summary>패널 전체 배경 이미지입니다. 클릭 차단을 피하기 위해 보통 Raycast를 받지 않습니다.</summary>
        public Image BackgroundImage;

        [Header("Art / Layout Policy")]
        [Tooltip("패널별로 다른 UI 스킨을 쓰고 싶을 때 직접 지정합니다. 비어 있으면 Context/Resources 프로필을 사용합니다.")]
        [SerializeField] private FisherUiArtProfile _artProfile;
        [Tooltip("Inspector에서 직접 배치한 RectTransform/GridLayout/배경 스킨을 런타임 보정으로 덮지 않으려면 PreserveInspectorLayout으로 둡니다. 기본값은 CSH 표준 레이아웃으로 재보정하고, ForceGenerateOrRepair는 명시 강제 보정용입니다.")]
        [SerializeField] private FisherStaticViewLayoutMode _layoutMode = FisherStaticViewLayoutMode.GenerateOrRepair;
        [NonSerialized] private bool _runtimePreserveInspectorLayout;

        [Header("Header")]
        /// <summary>제목, 상태, 확장 버튼이 들어가는 상단 영역입니다.</summary>
        public RectTransform HeaderRoot;
        /// <summary>가방, 요리, 상점, 도감 같은 화면 제목입니다.</summary>
        public TextMeshProUGUI TitleText;
        /// <summary>재화, 진행도, 발견 수 같은 1차 상태 문구입니다.</summary>
        public TextMeshProUGUI StatusText;
        /// <summary>가방 용량, 현재 메시지 같은 2차 상태 문구입니다.</summary>
        public TextMeshProUGUI SubStatusText;
        /// <summary>가방 확장, 요리 슬롯 확장처럼 헤더 오른쪽에 붙는 주요 보조 버튼입니다.</summary>
        public FisherButtonView HeaderAction;

        [Header("Tabs")]
        /// <summary>카테고리 탭 버튼들이 배치되는 영역입니다.</summary>
        public RectTransform CategoryTabsRoot;
        /// <summary>카테고리 전환용 탭 버튼 배열입니다. 화면별 탭 이름은 어댑터가 바인딩합니다.</summary>
        public FisherButtonView[] CategoryTabs = Array.Empty<FisherButtonView>();

        [Header("Grid")]
        /// <summary>스크롤 그리드 전체 영역입니다.</summary>
        public RectTransform GridRoot;
        /// <summary>실제 슬롯들이 자식으로 들어가는 GridLayoutGroup content입니다.</summary>
        public RectTransform GridContent;
        /// <summary>열 수, 셀 크기, 간격을 화면별로 조정하는 GridLayoutGroup입니다.</summary>
        public GridLayoutGroup GridLayout;
        /// <summary>슬롯이 화면을 넘을 때 세로 스크롤을 담당합니다.</summary>
        public ScrollRect GridScrollRect;
        /// <summary>현재 패널이 슬롯을 어떤 방식으로 배치할지 나타내는 레이아웃 타입입니다.</summary>
        public FisherSlotLayout SlotLayout = FisherSlotLayout.Bag;
        /// <summary>itemId별 오브젝트가 아니라 순번 기반으로 재사용되는 슬롯 배열입니다.</summary>
        public FisherSlotView[] Slots = Array.Empty<FisherSlotView>();

        [Header("Cooking Extra Grids")]
        /// <summary>요리 레시피 목록 전용 스크롤 그리드입니다. CookingPanel에서만 사용합니다.</summary>
        public RectTransform RecipeGridRoot;
        public RectTransform RecipeGridContent;
        public GridLayoutGroup RecipeGridLayout;
        public ScrollRect RecipeGridScrollRect;
        public FisherSlotView[] RecipeSlots = Array.Empty<FisherSlotView>();
        /// <summary>선택 레시피의 재료 2개와 결과 1개를 보여주는 전용 그리드입니다. CookingPanel에서만 사용합니다.</summary>
        public RectTransform IngredientGridRoot;
        public RectTransform IngredientGridContent;
        public GridLayoutGroup IngredientGridLayout;
        public FisherSlotView[] IngredientSlots = Array.Empty<FisherSlotView>();

        [Header("Detail")]
        /// <summary>선택된 아이템, 레시피, 상품의 상세 정보를 보여주는 영역입니다.</summary>
        public RectTransform DetailRoot;
        /// <summary>상세 영역 왼쪽의 대표 아이콘 슬롯입니다.</summary>
        public FisherSlotView DetailSlot;
        /// <summary>선택 항목 이름 또는 빈 슬롯 안내 제목입니다.</summary>
        public TextMeshProUGUI DetailTitleText;
        /// <summary>카테고리, 희귀도, 재료 같은 상세 보조 정보입니다.</summary>
        public TextMeshProUGUI DetailMetaText;
        /// <summary>판매가, 조리 시간, 보유량 같은 상세 본문 정보입니다.</summary>
        public TextMeshProUGUI DetailBodyText;

        [Header("Cooking Detail Info")]
        /// <summary>CookingPanel에서만 사용하는 완료 보상/판매가 정보 행 루트입니다.</summary>
        public RectTransform CookingDetailInfoRows;
        /// <summary>CookingPanel 전용 완료 EXP 정보 패널입니다.</summary>
        public RectTransform CookingRewardExpPanel;
        /// <summary>CookingPanel 전용 완료 EXP 표시 텍스트입니다.</summary>
        public TextMeshProUGUI CookingRewardExpText;
        /// <summary>CookingPanel 전용 판매가 정보 패널입니다.</summary>
        public RectTransform CookingSellPricePanel;
        /// <summary>CookingPanel 전용 판매가 표시 텍스트입니다.</summary>
        public TextMeshProUGUI CookingSellPriceText;

        [Header("Actions")]
        /// <summary>판매, 시작, 잠금 같은 하단 주요 액션 버튼들이 들어가는 영역입니다.</summary>
        public RectTransform ActionsRoot;
        /// <summary>첫 번째 주요 액션 버튼입니다.</summary>
        public FisherButtonView PrimaryAction;
        /// <summary>두 번째 주요 액션 버튼입니다.</summary>
        public FisherButtonView SecondaryAction;
        /// <summary>세 번째 주요 액션 버튼입니다.</summary>
        public FisherButtonView TertiaryAction;
        /// <summary>네 번째 선택 액션 버튼입니다. 화면에 따라 숨깁니다.</summary>
        public FisherButtonView QuaternaryAction;

        [Header("Action Sheets")]
        /// <summary>수량 선택/확인 팝업을 담는 전체 루트입니다. 루트 자체는 클릭을 먹지 않아야 합니다.</summary>
        public RectTransform ActionSheetsRoot;
        /// <summary>수량 입력이 필요한 판매/요리 조작용 팝업입니다.</summary>
        public FisherActionSheetView QuantitySheet;
        /// <summary>취소 확인, 탭 전체판매 확인처럼 단일 확인이 필요한 팝업입니다.</summary>
        public FisherActionSheetView ConfirmSheet;

        public FisherStaticViewLayoutMode LayoutMode
        {
            get => _layoutMode;
            set => _layoutMode = value;
        }

        public bool PreserveInspectorLayout =>
            _layoutMode == FisherStaticViewLayoutMode.PreserveInspectorLayout ||
            (_runtimePreserveInspectorLayout && _layoutMode != FisherStaticViewLayoutMode.ForceGenerateOrRepair);

        public void SetRuntimePreserveInspectorLayout(bool preserve)
        {
            _runtimePreserveInspectorLayout = preserve;
            ApplyRuntimePreserveChrome(PreserveInspectorLayout);
        }

        private void ApplyRuntimePreserveChrome(bool preserve)
        {
            Transform root = ViewRoot != null ? ViewRoot : transform;
            FisherSlotView[] slotViews = root.GetComponentsInChildren<FisherSlotView>(true);
            for (int i = 0; i < slotViews.Length; i++)
            {
                slotViews[i].PreserveInspectorChrome = preserve;
            }

            FisherButtonView[] buttonViews = root.GetComponentsInChildren<FisherButtonView>(true);
            for (int i = 0; i < buttonViews.Length; i++)
            {
                buttonViews[i].PreserveInspectorChrome = preserve;
            }
        }

        public FisherUiArtProfile ArtProfile
        {
            get => _artProfile;
            set => _artProfile = value;
        }

        public FisherUiArtProfile ResolveArtProfile(FisherUiArtProfile fallback)
        {
            if (_artProfile != null)
            {
                return _artProfile;
            }

            if (fallback != null)
            {
                _artProfile = fallback;
            }

            return _artProfile;
        }

        public int ExistingSlotCount => Slots == null ? 0 : Slots.Length;

        /// <summary>
        /// 지정 인덱스의 순번 슬롯을 기존 Inspector 풀에서만 반환합니다.
        /// 런타임 생성/보정은 금지하고, 부족분은 validator와 수동 editor builder가 처리합니다.
        /// </summary>
        public FisherSlotView GetExistingSlot(int index, string prefix)
        {
            return GetExistingSlotFrom(Slots, GridContent, index, prefix);
        }

        public FisherSlotView GetExistingRecipeSlot(int index)
        {
            return GetExistingSlotFrom(RecipeSlots, RecipeGridContent, index, "RecipeSlot");
        }

        public FisherSlotView GetExistingIngredientSlot(int index)
        {
            return GetExistingSlotFrom(IngredientSlots, IngredientGridContent, index, "IngredientSlot");
        }

        /// <summary>
        /// Backward-compatible 이름입니다. 런타임에서는 더 이상 슬롯을 생성하지 않습니다.
        /// </summary>
        public FisherSlotView EnsureSlot(int index, string prefix, TextMeshProUGUI template, FisherUiArtProfile artProfile)
        {
            return GetExistingSlot(index, prefix);
        }

        /// <summary>
        /// 데이터보다 남는 슬롯을 비우고 숨깁니다.
        /// 고정 그리드가 필요한 가방은 어댑터에서 필요한 수만큼 다시 활성화합니다.
        /// </summary>
        public void HideUnusedSlots(int firstUnusedIndex)
        {
            HideUnusedSlots(Slots, firstUnusedIndex);
        }

        public void HideUnusedRecipeSlots(int firstUnusedIndex)
        {
            HideUnusedSlots(RecipeSlots, firstUnusedIndex);
        }

        public void HideUnusedIngredientSlots(int firstUnusedIndex)
        {
            HideUnusedSlots(IngredientSlots, firstUnusedIndex);
        }

        public void SetCookingSectionsVisible(bool progress, bool recipes, bool ingredients)
        {
            SetSectionActive(GridRoot, progress);
            SetSectionActive(RecipeGridRoot, recipes);
            SetSectionActive(IngredientGridRoot, ingredients);
        }

        /// <summary>
        /// Status 렌더가 꺼 둔 주요 섹션을 다음 정상 렌더가 명시적으로 복구하게 합니다.
        /// </summary>
        public void SetMainSectionsVisible(bool tabs, bool grid, bool detail, bool actions)
        {
            SetSectionActive(CategoryTabsRoot, tabs);
            SetSectionActive(GridRoot, grid);
            SetSectionActive(DetailRoot, detail);
            SetSectionActive(ActionsRoot, actions);
        }

        private static void SetSectionActive(RectTransform root, bool active)
        {
            if (root != null)
            {
                root.gameObject.SetActive(active);
            }
        }

        private static FisherSlotView GetExistingSlotFrom(FisherSlotView[] slots, RectTransform content, int index, string prefix)
        {
            if (index < 0)
            {
                return null;
            }

            if (slots != null && index < slots.Length && slots[index] != null)
            {
                return slots[index];
            }

            if (content == null || string.IsNullOrWhiteSpace(prefix))
            {
                return null;
            }

            string existingSlotName = prefix + "_" + index.ToString("00");
            Transform existingSlot = content.Find(existingSlotName);
            return existingSlot == null ? null : existingSlot.GetComponent<FisherSlotView>();
        }

        private static void HideUnusedSlots(FisherSlotView[] slots, int firstUnusedIndex)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                if (i >= firstUnusedIndex)
                {
                    slots[i].Clear();
                    slots[i].gameObject.SetActive(false);
                }
                else
                {
                    slots[i].gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 탭 라벨, 선택 상태, 클릭 콜백을 현재 데이터 기준으로 갱신합니다.
        /// </summary>
        public void SetTabs(IReadOnlyList<string> labels, IReadOnlyList<string> keys, string selectedKey, Action<string> onSelected)
        {
            if (CategoryTabs == null)
            {
                return;
            }

            int count = Mathf.Min(CategoryTabs.Length, labels == null ? 0 : labels.Count);
            for (int i = 0; i < count; i++)
            {
                string key = keys == null || i >= keys.Count ? labels[i] : keys[i];
                bool selected = string.Equals(key, selectedKey, StringComparison.Ordinal);
                CategoryTabs[i].gameObject.SetActive(true);
                CategoryTabs[i].Bind(labels[i], selected, true, () => onSelected?.Invoke(key));
            }

            for (int i = count; i < CategoryTabs.Length; i++)
            {
                if (CategoryTabs[i] != null)
                {
                    CategoryTabs[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 하단 액션 버튼 하나의 라벨, 활성 상태, 클릭 콜백을 갱신합니다.
        /// </summary>
        public void SetAction(FisherButtonView action, string label, bool interactable, Action onClick)
        {
            if (action == null)
            {
                return;
            }

            action.Bind(label, false, interactable, onClick);
        }

        /// <summary>
        /// 현재 패널 폭을 기준으로 그리드 열 수와 셀 크기를 다시 계산합니다.
        /// 모바일 세로 화면과 Main 씬 이식 후 폭 차이를 흡수하기 위한 공통 보정입니다.
        /// </summary>
        public void ConfigureGrid(int columns, Vector2 spacing, RectOffset padding, float heightAspect, float fallbackWidth = 480f)
        {
            if (!FisherStaticViewLayoutPolicy.CanWriteGeneratedLayout(this))
            {
                return;
            }

            if (GridLayout == null)
            {
                return;
            }

            int safeColumns = Mathf.Max(1, columns);
            float width = GridRoot == null ? 0f : GridRoot.rect.width;
            if (width <= 0f && ViewRoot != null)
            {
                Canvas.ForceUpdateCanvases();
                width = GridRoot == null ? 0f : GridRoot.rect.width;
                if (width <= 0f)
                {
                    width = ViewRoot.rect.width;
                }
            }

            if (width <= 0f)
            {
                width = fallbackWidth;
            }

            RectOffset safePadding = padding ?? new RectOffset(0, 0, 0, 0);
            float usableWidth = Mathf.Max(120f, width - safePadding.horizontal - spacing.x * (safeColumns - 1));
            float cellWidth = Mathf.Floor(usableWidth / safeColumns);
            GridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            GridLayout.constraintCount = safeColumns;
            GridLayout.spacing = spacing;
            GridLayout.padding = safePadding;
            GridLayout.cellSize = new Vector2(cellWidth, Mathf.Max(56f, cellWidth * Mathf.Max(0.55f, heightAspect)));
        }

        /// <summary>
        /// 수량 입력/확인 팝업을 모두 닫습니다.
        /// 비활성 팝업이 화면 입력을 가로막지 않도록 Refresh 시작점에서 호출합니다.
        /// </summary>
        public void HideActionSheets()
        {
            if (QuantitySheet != null)
            {
                QuantitySheet.gameObject.SetActive(false);
            }

            if (ConfirmSheet != null)
            {
                ConfirmSheet.gameObject.SetActive(false);
            }
        }
    }
}
