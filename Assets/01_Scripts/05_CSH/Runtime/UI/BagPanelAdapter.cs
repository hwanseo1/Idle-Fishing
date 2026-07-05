using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// WJ RuntimeStateEventBus의 가방 진입 이벤트를 받아 Fisher 가방 snapshot을 표시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BagPanelAdapter : MonoBehaviour
    {
        #region Layout Constants

        private const int GridColumnCount = 6;
        private const int MinimumVisibleBagSlots = 36;
        private const string ServerSellPendingMessage = "판매 요청 중";
        private const string ServerBoxUsePendingMessage = "개봉 요청 중";
        private const float ServerSellResponseTimeoutSeconds = 12f;
        private const float ServerBagExpansionResponseTimeoutSeconds = 12f;
        private const bool BagExpansionUiEnabled = false;
        private static readonly Vector2 BagGridCellSize = new Vector2(90f, 90f);
        private static readonly Vector2 BagGridSpacing = new Vector2(8f, 8f);
        #endregion

        #region Inspector References

        [Header("Runtime")]
        [SerializeField] private FisherRuntimeContext _context;
        [SerializeField] private GameObject _inventoryPanel;

        [Header("View")]
        [SerializeField] private FisherPanelView _view;

        [Header("Art Contract")]
        [Tooltip("팀장이 가방 슬롯, 버튼, itemId 아이콘을 연결하는 공통 UI 아트 프로필입니다.")]
        [SerializeField] private FisherUiArtProfile _artProfile;

        #endregion

        #region View State

        private string _selectedItemId;
        private string _category = "All";
        private string _lastMessage = "준비";
        private TextMeshProUGUI _template;
        private int _sellCount = 1;
        private bool _showSellPopup;
        private bool _showSellAllConfirm;
        private bool _isInventoryOpen;
        private readonly FisherServerRequestGate _saleRequest = new FisherServerRequestGate();
        private readonly FisherServerRequestGate _boxUseRequest = new FisherServerRequestGate();
        private readonly FisherServerRequestGate _bagExpansionRequest = new FisherServerRequestGate();
        private TMP_InputField _sellCountInput;
        private FisherPlayerDataBridge _playerDataBridge;
        private bool _pendingBagSnapshotPullOnReady;
        private bool _pulledBagSnapshotThisOpen;
        private bool _missingContextWarningLogged;
        private readonly HashSet<string> _newNoticeIdsShownForNextOpen = new HashSet<string>();

        #endregion

        #region Configuration

        /// <summary>
        /// 부트스트래퍼가 씬 패널과 Fisher 런타임 컨텍스트를 주입할 때 호출합니다.
        /// </summary>
        public void Configure(FisherRuntimeContext context, GameObject inventoryPanel)
        {
            SetContext(context);
            _inventoryPanel = inventoryPanel;
            _playerDataBridge = ResolvePlayerDataBridge();
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            global::RuntimeStateEventBus.OnInventoryStateEntered += HandleInventoryStateEntered;
            global::RuntimeStateEventBus.OnInventoryStateExited += HandleInventoryStateExited;
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
                _context.RuntimeChanged += Refresh;
            }
            Refresh();
        }

        private void OnDisable()
        {
            global::RuntimeStateEventBus.OnInventoryStateEntered -= HandleInventoryStateEntered;
            global::RuntimeStateEventBus.OnInventoryStateExited -= HandleInventoryStateExited;
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
            }
            _saleRequest.Invalidate();
            _boxUseRequest.Invalidate();
            _bagExpansionRequest.Invalidate();
        }

        #endregion

        #region Context Binding

        private void SetContext(FisherRuntimeContext context)
        {
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
            }

            _context = context;
            _playerDataBridge = null;
            ResolveArtProfile();

            if (isActiveAndEnabled && _context != null)
            {
                _context.RuntimeChanged -= Refresh;
                _context.RuntimeChanged += Refresh;
            }

        }

        #endregion

        #region Rendering

        /// <summary>
        /// 현재 필터 조건으로 가방 목록과 선택 상세 영역을 다시 그립니다.
        /// </summary>
        public void Refresh()
        {
            ResolveReferences();
            ResolveArtProfile();

            if (_inventoryPanel == null)
            {
                return;
            }

            if (!TryGetReadyInventoryContext(out string waitStatus))
            {
                RenderStatus("가방 데이터 준비 중", waitStatus);
                return;
            }

            if (!TryEnsureBagSnapshotHydratedForRender(
                    allowPull: _isInventoryOpen && _pendingBagSnapshotPullOnReady && !_pulledBagSnapshotThisOpen,
                    out string hydrationStatus))
            {
                RenderStatus("가방 데이터 준비 중", hydrationStatus);
                return;
            }

            if (_isInventoryOpen && _pendingBagSnapshotPullOnReady && !_pulledBagSnapshotThisOpen)
            {
                _pendingBagSnapshotPullOnReady = false;
                _pulledBagSnapshotThisOpen = true;
                PullInventorySnapshotFromPlayFabDataStore(force: true);
            }

            List<BagItemView> rows = _context.BagQueryService.BuildSnapshot(new BagQueryOptions
            {
                Category = _category,
                Filter = BagFilter.None
            });
            TrackNewNoticesShownThisOpen(rows);

            if (rows.Count > 0 && string.IsNullOrEmpty(_selectedItemId))
            {
                _selectedItemId = rows[0].ItemId;
            }

            if (rows.Count == 0)
            {
                _selectedItemId = string.Empty;
                _showSellPopup = false;
                _showSellAllConfirm = false;
            }

            BagItemView selectedForLayout = FindSelectedRow(rows) ?? (rows.Count > 0 ? rows[0] : null);
            if (_showSellPopup && (selectedForLayout == null || !selectedForLayout.Sellable || IsItemLocked(selectedForLayout)))
            {
                _showSellPopup = false;
            }

            if (TryRenderStaticView(rows, selectedForLayout))
            {
                return;
            }

            LogStaticViewUnavailable("refresh");
        }

        private bool TryRenderStaticView(List<BagItemView> rows, BagItemView selectedForLayout)
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return false;
            }

            HideLegacyRoot("Fisher_BagRoot");
            view.SetMainSectionsVisible(tabs: true, grid: true, detail: true, actions: true);

            if (view.TitleText != null)
            {
                view.TitleText.text = string.Empty;
            }

            if (view.StatusText != null)
            {
                view.StatusText.text = FormatBagCapacity();
            }

            if (view.SubStatusText != null)
            {
                view.SubStatusText.text = string.Empty;
            }

            FisherRuntimeUi.ApplyHeaderStatusParchmentText(view);
            FisherRuntimeUi.EnsureBagCurrencyStrip(view, _context.State, _template, _artProfile);

            if (view.HeaderAction != null)
            {
                if (IsServerBagExpansionBlocked())
                {
                    view.HeaderAction.gameObject.SetActive(false);
                }
                else
                {
                    bool canExpand = CanPurchaseBagExpansion(out long nextCost);
                    view.HeaderAction.gameObject.SetActive(true);
                    view.HeaderAction.Bind(
                        _bagExpansionRequest.IsBusy ? "확장 요청 중" : nextCost > 0 ? "확장 " + CompactNumberFormatter.FormatGold(nextCost) : "확장",
                        false,
                        canExpand && !_bagExpansionRequest.IsBusy,
                        PurchaseBagExpansion);
                }
            }

            view.SetTabs(
                new[] { "전체", "물고기", "요리", "재료", "기타" },
                new[] { "All", "Fish", "Food", "Material", "Other" },
                _category,
                key =>
                {
                    _category = key;
                    _selectedItemId = string.Empty;
                    _sellCount = 1;
                    _showSellPopup = false;
                    _showSellAllConfirm = false;
                    _lastMessage = CategoryLabel(key);
                    Refresh();
                });

            BagItemView selected = RenderStaticGrid(view, rows);
            if (selected == null)
            {
                selected = selectedForLayout;
            }

            RenderStaticDetail(view, selected);
            RenderStaticActions(view, selected);
            RenderStaticActionSheet(view, selected);
            return true;
        }

        private FisherPanelView ResolveStaticView()
        {
            if (_inventoryPanel == null)
            {
                return null;
            }

            if (!FisherPanelViewResolver.TryResolveExistingView(
                    _inventoryPanel,
                    _view,
                    "BagPanel",
                    nameof(BagPanelAdapter),
                    FisherSlotLayout.Bag,
                    _artProfile,
                    out FisherPanelView resolvedView,
                    out FisherUiArtProfile resolvedArtProfile))
            {
                LogStaticViewUnavailable("resolve");
                return null;
            }

            _view = resolvedView;
            _artProfile = resolvedArtProfile;
            return _view;
        }

        private static void LogStaticViewUnavailable(string state)
        {
            Debug.LogWarning("[BagPanelAdapter] BagPanel/ViewRoot 고정 View를 만들거나 찾지 못했습니다. " +
                             "기존 Inspector UI 보호를 위해 레거시 자동 렌더링을 건너뜁니다. state=" + state);
        }

        private BagItemView RenderStaticGrid(FisherPanelView view, List<BagItemView> rows)
        {
            int slotCount = CalculateGridSlotCount(rows);
            BagItemView selected = null;
            for (int i = 0; i < slotCount; i++)
            {
                FisherSlotView slot = view.GetExistingSlot(i, "BagSlot");
                if (slot == null)
                {
                    continue;
                }

                BagItemView row = i < rows.Count ? rows[i] : null;
                if (row == null)
                {
                    slot.Clear();
                    continue;
                }

                bool isSelected = row.ItemId == _selectedItemId;
                if (isSelected)
                {
                    selected = row;
                }

                string itemId = row.ItemId;
                Sprite icon = FisherRuntimeUi.ResolveItemIcon(_artProfile, row.ItemId, row.Category);
                bool locked = IsItemLocked(row);
                slot.Bind(
                    icon == null ? row.DisplayNameKo : string.Empty,
                    "x" + CompactNumberFormatter.Format(row.Count),
                    string.Empty,
                    string.Empty,
                    icon,
                    isSelected,
                    false,
                    locked,
                    row.NewNotice,
                    () =>
                    {
                        SelectBagItem(itemId);
                    });
            }

            if (selected == null && rows.Count > 0)
            {
                selected = rows[0];
                _selectedItemId = selected.ItemId;
            }

            return selected;
        }

        private void RenderStaticDetail(FisherPanelView view, BagItemView selected)
        {
            if (view.DetailRoot != null)
            {
                view.DetailRoot.gameObject.SetActive(true);
            }

            if (selected == null)
            {
                view.DetailSlot?.Clear();
                SetText(view.DetailTitleText, "빈 슬롯");
                SetText(view.DetailMetaText, "아이템 없음");
                SetText(view.DetailBodyText, "아이템을 선택하면 판매/잠금 조작이 활성화됩니다.");
                RenderBagDetailSaleRows(view, null, false);
                FisherRuntimeUi.ApplyDetailParchmentText(view);
                return;
            }

            bool locked = IsItemLocked(selected);
            ClampSellCount(selected);
            view.DetailSlot?.Bind(
                string.Empty,
                "x" + CompactNumberFormatter.Format(selected.Count),
                string.Empty,
                string.Empty,
                FisherRuntimeUi.ResolveItemIcon(_artProfile, selected.ItemId, selected.Category),
                false,
                false,
                locked,
                selected.NewNotice,
                null);
            SetText(view.DetailTitleText, selected.DisplayNameKo + (locked ? " / 잠금" : string.Empty));
            SetText(view.DetailMetaText, CategoryLabel(selected.Category) + " / " + selected.Rarity);
            SetText(view.DetailBodyText, "판매 상태 " + SellStatusLabel(selected, locked) + "\n" +
                                     "요리 " + (selected.Cookable ? "가능" : "불가") +
                                     (IsBoxItem(selected) ? "\n상자 " + BoxUseStatusLabel(selected, locked) : string.Empty));
            RenderBagDetailSaleRows(view, selected, locked);
            FisherRuntimeUi.ApplyDetailParchmentText(view);
        }

        private void RenderStaticActions(FisherPanelView view, BagItemView selected)
        {
            bool hasSelected = selected != null;
            bool locked = hasSelected && IsItemLocked(selected);
            bool isBox = IsBoxItem(selected);
            bool boxNeedsChoice = isBox && RequiresBoxChoice(selected);
            bool canUseBox = hasSelected && isBox && !boxNeedsChoice && !locked && selected.Count > 0 &&
                             !_saleRequest.IsBusy && !_boxUseRequest.IsBusy;
            bool canSell = hasSelected && !isBox && selected.Sellable && !locked && _sellCount > 0 &&
                           !_saleRequest.IsBusy && !_boxUseRequest.IsBusy;
            string primaryLabel = isBox
                ? (boxNeedsChoice ? "선택 대기" : "개봉")
                : "판매";
            bool primaryEnabled = isBox ? canUseBox : canSell;
            view.SetAction(view.PrimaryAction, primaryLabel, primaryEnabled, () =>
            {
                if (isBox)
                {
                    UseSelectedBox(selected);
                }
                else
                {
                    _showSellPopup = true;
                    _showSellAllConfirm = false;
                    Refresh();
                }
            });
            bool sellAllBlocked = _saleRequest.IsBusy || _boxUseRequest.IsBusy;
            view.SetAction(view.SecondaryAction, sellAllBlocked ? "전체판매 요청 중" : "탭 전체판매", !sellAllBlocked, () =>
            {
                _showSellPopup = false;
                _showSellAllConfirm = true;
                Refresh();
            });
            view.SetAction(view.TertiaryAction, locked ? "잠금 해제" : "잠금", hasSelected, () => ToggleItemLock(selected, !locked));
            if (view.QuaternaryAction != null)
            {
                view.QuaternaryAction.gameObject.SetActive(false);
            }
        }

        private void RenderStaticActionSheet(FisherPanelView view, BagItemView selected)
        {
            view.HideActionSheets();
            _sellCountInput = null;

            if (_showSellPopup && selected != null && selected.Sellable && !IsItemLocked(selected))
            {
                ClampSellCount(selected);
                if (view.QuantitySheet != null)
                {
                    view.QuantitySheet.ShowQuantity(
                        selected.DisplayNameKo + " 판매 수량",
                        string.Empty,
                        _sellCount,
                        selected.Count,
                        count =>
                        {
                            _sellCount = count;
                            Refresh();
                        },
                        "선택 수량 판매",
                        () => SellSelectedCount(selected),
                        () =>
                        {
                            _showSellPopup = false;
                            Refresh();
                        });
                    RenderSaleUnitRows(view.QuantitySheet, selected);
                    _sellCountInput = view.QuantitySheet.NumberInput;
                }

                return;
            }

            if (!_showSellAllConfirm || view.ConfirmSheet == null)
            {
                return;
            }

            bool previewAvailable = TryBuildSellAllPreview(out int sellableKinds, out long totalCount, out long totalGain);
            string body = previewAvailable
                ? CategoryLabel(_category) + " 탭\n아이템 " + sellableKinds + "종\n" + CompactNumberFormatter.FormatCount(totalCount) + "\n" + CompactNumberFormatter.FormatGold(totalGain)
                : CategoryLabel(_category) + " 탭 전체판매 금액 계산 실패";
            view.ConfirmSheet.ShowConfirm(
                "탭 전체판매 확인",
                HasSaleSummaryRows(view.ConfirmSheet) ? string.Empty : body,
                "판매 확정",
                previewAvailable && sellableKinds > 0,
                SellAllUnlockedSellable,
                () =>
                {
                    _showSellAllConfirm = false;
                    Refresh();
                });
            RenderSaleSummaryRows(view.ConfirmSheet, previewAvailable, sellableKinds, totalCount, totalGain);
        }

        private static bool HasSaleUnitRows(FisherActionSheetView sheet)
        {
            return FindSheetText(sheet, "SaleUnitRows/OwnedCountPanel/OwnedCountText") != null &&
                   FindSheetText(sheet, "SaleUnitRows/UnitPricePanel/UnitPriceText") != null;
        }

        private static void RenderSaleUnitRows(FisherActionSheetView sheet, BagItemView selected)
        {
            Transform rows = FindSheetChild(sheet, "SaleUnitRows");
            if (rows == null || selected == null)
            {
                return;
            }

            rows.gameObject.SetActive(true);
            SetSheetText(sheet, "SaleUnitRows/OwnedCountPanel/OwnedCountText", "보유 " + CompactNumberFormatter.FormatCount(selected.Count));
            SetSheetText(sheet, "SaleUnitRows/UnitPricePanel/UnitPriceText", "단가 " + CompactNumberFormatter.FormatGold(selected.SellPrice));
        }

        private static bool HasBagDetailSaleRows(FisherPanelView view)
        {
            return FindDetailText(view, "BagDetailSaleRows/OwnedCountPanel/OwnedCountText") != null &&
                   FindDetailText(view, "BagDetailSaleRows/UnitPricePanel/UnitPriceText") != null;
        }

        private static void RenderBagDetailSaleRows(FisherPanelView view, BagItemView selected, bool locked)
        {
            Transform rows = FindDetailChild(view, "BagDetailSaleRows");
            if (rows == null)
            {
                return;
            }

            bool visible = selected != null;
            rows.gameObject.SetActive(visible);
            if (!visible)
            {
                SetDetailText(view, "BagDetailSaleRows/OwnedCountPanel/OwnedCountText", string.Empty);
                SetDetailText(view, "BagDetailSaleRows/UnitPricePanel/UnitPriceText", string.Empty);
                return;
            }

            SetDetailText(view, "BagDetailSaleRows/OwnedCountPanel/OwnedCountText", "보유 " + CompactNumberFormatter.FormatCount(selected.Count));
            string priceText = selected.Sellable && !locked && selected.SellPrice > 0
                ? "개당 " + CompactNumberFormatter.FormatGold(selected.SellPrice)
                : "판매 불가";
            SetDetailText(view, "BagDetailSaleRows/UnitPricePanel/UnitPriceText", priceText);
        }

        private static bool HasSaleSummaryRows(FisherActionSheetView sheet)
        {
            return FindSheetText(sheet, "SaleSummaryRows/ScopePanel/ScopeText") != null &&
                   FindSheetText(sheet, "SaleSummaryRows/KindCountPanel/KindCountText") != null &&
                   FindSheetText(sheet, "SaleSummaryRows/TotalCountPanel/TotalCountText") != null &&
                   FindSheetText(sheet, "SaleSummaryRows/TotalGoldPanel/TotalGoldText") != null;
        }

        private string SaleScopeText()
        {
            return CategoryLabel(_category) + " 탭";
        }

        private void RenderSaleSummaryRows(FisherActionSheetView sheet, bool previewAvailable, int sellableKinds, long totalCount, long totalGain)
        {
            Transform rows = FindSheetChild(sheet, "SaleSummaryRows");
            if (rows == null)
            {
                return;
            }

            rows.gameObject.SetActive(previewAvailable);
            if (!previewAvailable)
            {
                return;
            }

            SetSheetText(sheet, "SaleSummaryRows/ScopePanel/ScopeText", SaleScopeText());
            SetSheetText(sheet, "SaleSummaryRows/KindCountPanel/KindCountText", "아이템 " + sellableKinds + "종");
            SetSheetText(sheet, "SaleSummaryRows/TotalCountPanel/TotalCountText", CompactNumberFormatter.FormatCount(totalCount));
            SetSheetText(sheet, "SaleSummaryRows/TotalGoldPanel/TotalGoldText", CompactNumberFormatter.FormatGold(totalGain));
        }

        private static Transform FindSheetChild(FisherActionSheetView sheet, string path)
        {
            return sheet == null || sheet.transform == null ? null : sheet.transform.Find(path);
        }

        private static TextMeshProUGUI FindSheetText(FisherActionSheetView sheet, string path)
        {
            Transform child = FindSheetChild(sheet, path);
            return child == null ? null : child.GetComponent<TextMeshProUGUI>();
        }

        private static void SetSheetText(FisherActionSheetView sheet, string path, string value)
        {
            TextMeshProUGUI text = FindSheetText(sheet, path);
            SetText(text, value);
            FisherRuntimeUi.ApplyParchmentText(text);
        }

        private static Transform FindDetailChild(FisherPanelView view, string path)
        {
            return view == null || view.DetailRoot == null ? null : view.DetailRoot.transform.Find(path);
        }

        private static TextMeshProUGUI FindDetailText(FisherPanelView view, string path)
        {
            Transform child = FindDetailChild(view, path);
            return child == null ? null : child.GetComponent<TextMeshProUGUI>();
        }

        private static void SetDetailText(FisherPanelView view, string path, string value)
        {
            TextMeshProUGUI text = FindDetailText(view, path);
            SetText(text, value);
            FisherRuntimeUi.ApplyParchmentText(text);
        }

        private void HideLegacyRoot(string rootName)
        {
            Transform legacyRoot = _inventoryPanel == null ? null : _inventoryPanel.transform.Find(rootName);
            if (legacyRoot != null)
            {
                legacyRoot.gameObject.SetActive(false);
            }
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
                FisherRuntimeUi.RefreshWrappedTextPanel(text);
            }
        }

        private void ClearAllSlots(FisherPanelView view)
        {
            if (view == null || view.Slots == null)
            {
                return;
            }

            for (int i = 0; i < view.Slots.Length; i++)
            {
                view.Slots[i]?.Clear();
            }
        }

        private int CalculateGridSlotCount(List<BagItemView> rows)
        {
            int rowCount = rows == null ? 0 : rows.Count;
            int capacity = Mathf.Max(CurrentBagCapacity(), rowCount);
            int desired = Mathf.Max(MinimumVisibleBagSlots, capacity);
            int remainder = desired % GridColumnCount;
            return remainder == 0 ? desired : desired + GridColumnCount - remainder;
        }

        private BagItemView FindSelectedRow(List<BagItemView> rows)
        {
            if (rows == null || string.IsNullOrEmpty(_selectedItemId))
            {
                return null;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                BagItemView row = rows[i];
                if (row != null && row.ItemId == _selectedItemId)
                {
                    return row;
                }
            }

            return null;
        }

        private Sprite ButtonSprite(bool selected, bool disabled)
        {
            if (_artProfile == null)
            {
                return null;
            }

            if (disabled && _artProfile.ButtonDisabled != null)
            {
                return _artProfile.ButtonDisabled;
            }

            if (selected && _artProfile.ButtonSelected != null)
            {
                return _artProfile.ButtonSelected;
            }

            return _artProfile.ButtonNormal;
        }

        #endregion

        #region Entry State

        private void HandleInventoryStateEntered()
        {
            ClearPreviouslyShownNewNotices();
            _isInventoryOpen = true;
            _category = "All";
            _selectedItemId = string.Empty;
            _sellCount = 1;
            _sellCountInput = null;
            _showSellPopup = false;
            _showSellAllConfirm = false;
            _lastMessage = "전체";
            _pendingBagSnapshotPullOnReady = false;
            _pulledBagSnapshotThisOpen = false;
            if (!TryGetReadyInventoryContext(out string waitStatus))
            {
                _pendingBagSnapshotPullOnReady = true;
                RenderStatus("가방 데이터 준비 중", waitStatus);
                return;
            }

            _pendingBagSnapshotPullOnReady = true;
            if (!TryEnsureBagSnapshotHydratedForRender(allowPull: true, out string hydrationStatus))
            {
                RenderStatus("가방 데이터 준비 중", hydrationStatus);
                return;
            }

            Refresh();
        }

        private void HandleInventoryStateExited()
        {
            _isInventoryOpen = false;
            _pendingBagSnapshotPullOnReady = false;
            _pulledBagSnapshotThisOpen = false;
        }

        private void TrackNewNoticesShownThisOpen(List<BagItemView> rows)
        {
            if (!_isInventoryOpen || rows == null)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                BagItemView row = rows[i];
                if (row != null && row.NewNotice)
                {
                    _newNoticeIdsShownForNextOpen.Add(row.ItemId);
                }
            }
        }

        private void ClearPreviouslyShownNewNotices()
        {
            if (_context == null || _context.InventoryService == null || _newNoticeIdsShownForNextOpen.Count == 0)
            {
                return;
            }

            _context.InventoryService.AcknowledgeNewItemNotices(_newNoticeIdsShownForNextOpen);
            _newNoticeIdsShownForNextOpen.Clear();
        }

        private void SelectBagItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return;
            }

            if (_context != null && _context.InventoryService != null && _context.InventoryService.AcknowledgeNewItemNotice(itemId))
            {
                _newNoticeIdsShownForNextOpen.Remove(itemId);
            }

            _selectedItemId = itemId;
            _sellCount = 1;
            _showSellPopup = false;
            _showSellAllConfirm = false;
            Refresh();
        }

        #endregion

        #region Player Sync

        private bool PullInventorySnapshotFromPlayFabDataStore(bool force = false)
        {
            if (!TryGetReadyInventoryContext(out string waitStatus))
            {
                return false;
            }

            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            bool currencyApplied = false;
            bool inventoryApplied = false;
            string blockedReason = "FisherPlayerDataBridge 준비 중";
            bool applied = bridge != null &&
                           bridge.TryHydrateBagSnapshotsFromPlayFabDataStore(
                               forceInventory: force,
                               notify: false,
                               out currencyApplied,
                               out inventoryApplied,
                               out blockedReason);
            return applied;
        }

        private bool TryEnsureBagSnapshotHydratedForRender(bool allowPull, out string waitStatus)
        {
            waitStatus = string.Empty;
            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            if (bridge == null)
            {
                waitStatus = "FisherPlayerDataBridge 준비 중";
                return false;
            }

            if (!bridge.IsBoundToContext(_context))
            {
                waitStatus = "FisherPlayerDataBridge context binding 준비 중";
                return false;
            }

            if (!bridge.RequiresBagSnapshotHydration())
            {
                return true;
            }

            if (_context != null && _context.PlayerSnapshotHydrated)
            {
                return true;
            }

            if (!allowPull)
            {
                waitStatus = "PlayFabDataStore snapshot hydration 준비 중";
                return false;
            }

            _pendingBagSnapshotPullOnReady = false;
            _pulledBagSnapshotThisOpen = true;
            bool hydrated = PullInventorySnapshotFromPlayFabDataStore(force: true);
            if (hydrated && _context != null && _context.PlayerSnapshotHydrated)
            {
                return true;
            }

            waitStatus = "PlayFabDataStore snapshot hydration 미완료";
            return false;
        }

        #endregion

        #region Reference Resolution

        private void ResolveReferences()
        {
            if (_context == null && !TryResolveRuntimeContext() && !_missingContextWarningLogged)
            {
                _missingContextWarningLogged = true;
                Debug.LogWarning("[BagPanelAdapter] FisherRuntimeContext is not assigned. Wire it under the CSH control tower.", this);
            }

            if (_inventoryPanel == null)
            {
                return;
            }

            _template ??= FisherRuntimeUi.FindTextTemplate(_inventoryPanel);
        }

        private bool TryResolveRuntimeContext()
        {
            if (_context != null)
            {
                return true;
            }

            FisherRuntimeContext[] contexts = FindObjectsByType<FisherRuntimeContext>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (contexts == null || contexts.Length == 0 || contexts[0] == null)
            {
                return false;
            }

            SetContext(contexts[0]);
            return true;
        }

        private bool TryGetReadyInventoryContext(out string waitStatus)
        {
            TryResolveRuntimeContext();
            if (_context == null)
            {
                waitStatus = "FisherRuntimeContext 준비 중";
                return false;
            }

            if (!_context.IsReady)
            {
                waitStatus = string.IsNullOrEmpty(_context.LastStatus)
                    ? "FisherRuntimeContext 준비 중"
                    : _context.LastStatus;
                return false;
            }

            if (_context.State == null)
            {
                waitStatus = "PlayerRuntimeState 준비 중";
                return false;
            }

            if (_context.InventoryService == null)
            {
                waitStatus = "InventoryService 준비 중";
                return false;
            }

            if (_context.BagQueryService == null)
            {
                waitStatus = "BagQueryService 준비 중";
                return false;
            }

            waitStatus = string.Empty;
            return true;
        }

        private FisherUiArtProfile ResolveArtProfile()
        {
            if (_artProfile == null && _view != null)
            {
                _artProfile = _view.ResolveArtProfile(null);
            }

            if (_artProfile == null && _context != null)
            {
                _artProfile = _context.UiArtProfile;
            }

            if (_artProfile == null)
            {
                _artProfile = FisherUiArtProfile.LoadFromResources();
            }

            FisherRuntimeUi.SetActiveProfile(_artProfile);
            return _artProfile;
        }

        #endregion

        #region Status

        private string FormatBagCapacity()
        {
            InventoryService inventory = _context == null ? null : _context.InventoryService;
            int occupied = inventory == null ? 0 : inventory.OccupiedBagRows;
            int capacity = inventory == null ? 0 : inventory.BagCapacity;
            if (capacity <= 0)
            {
                return occupied.ToString();
            }

            return occupied + "/" + capacity;
        }

        private bool CanPurchaseBagExpansion(out long nextCost)
        {
            nextCost = 0;
            if (IsServerBagExpansionBlocked())
            {
                return false;
            }

            int current = CurrentBagCapacity();
            int max = ReadEconomyInt("bag_capacity_max", 0);
            if (current <= 0 || max <= 0 || current >= max)
            {
                return false;
            }

            if (!TryGetNextBagExpansionCost(out nextCost))
            {
                return false;
            }

            return true;
        }

        private void PurchaseBagExpansion()
        {
            if (IsServerBagExpansionBlocked())
            {
                _bagExpansionRequest.Invalidate();
                _lastMessage = "가방 확장 준비중";
                Refresh();
                return;
            }

            if (_bagExpansionRequest.IsBusy)
            {
                return;
            }

            int requestToken = BeginBagExpansionRequestTimeout();
            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            if (bridge == null ||
                !bridge.TryRequestBagCapacityExpansion(
                    () =>
                    {
                        if (!TryCompleteBagExpansionRequest(requestToken))
                        {
                            return;
                        }

                        _lastMessage = "가방 확장 반영";
                        _context.NotifyRuntimeChanged();
                        Refresh();
                    },
                    message =>
                    {
                        if (!TryCompleteBagExpansionRequest(requestToken))
                        {
                            return;
                        }

                        _lastMessage = string.IsNullOrWhiteSpace(message) ? "가방 확장 실패" : message;
                        Debug.LogWarning("[BagPanelAdapter] Server bag expansion rejected: " + _lastMessage);
                        _context.NotifyRuntimeChanged();
                        Refresh();
                    }))
            {
                _bagExpansionRequest.TryAbort(requestToken);
                _lastMessage = "가방 확장 요청 실패";
                Debug.LogWarning("[BagPanelAdapter] Server bag expansion request failed before CloudScript call.");
                _context.NotifyRuntimeChanged();
                Refresh();
                return;
            }

            _lastMessage = "가방 확장 요청 중";
            Debug.Log("[BagPanelAdapter] Server bag expansion requested.");
            _context.NotifyRuntimeChanged();
            Refresh();
        }

        private int CurrentBagCapacity()
        {
            int current = _context.InventoryService == null ? 0 : _context.InventoryService.BagCapacity;
            return current > 0 ? current : ReadEconomyInt("initial_bag_capacity", 0);
        }

        private bool TryGetNextBagExpansionCost(out long cost)
        {
            cost = 0;
            long baseCost = ReadEconomyLong("bag_capacity_gold_cost_base", 0);
            if (baseCost <= 0)
            {
                return false;
            }

            int levelMultiplier = Mathf.Max(1, _context.State.bagCapacityLevel + 1);
            return CurrencyMath.TryMultiply(baseCost, levelMultiplier, out cost);
        }

        private int ReadEconomyInt(string key, int fallback)
        {
            long value = ReadEconomyLong(key, fallback);
            if (value <= 0)
            {
                return fallback;
            }

            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private long ReadEconomyLong(string key, long fallback)
        {
            long resolvedFallback = FisherEconomyDefaults.ResolveLong(key, fallback);
            if (_context == null ||
                _context.BuildResult == null ||
                _context.BuildResult.Catalog == null ||
                string.IsNullOrEmpty(key) ||
                !_context.BuildResult.Catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param) ||
                param == null ||
                !param.IsEnabled ||
                !long.TryParse(param.Value, out long value))
            {
                return resolvedFallback;
            }

            return value;
        }

        private void RenderStatus(string title, string body)
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return;
            }

            view.SetMainSectionsVisible(tabs: false, grid: false, detail: false, actions: false);
            SetText(view.TitleText, string.Empty);
            SetText(view.StatusText, body);
            SetText(view.SubStatusText, string.Empty);
            FisherRuntimeUi.ApplyHeaderStatusParchmentText(view);
        }

        #endregion

        #region Detail Actions

        private void SetSellCountFromInput(BagItemView selected, string raw)
        {
            if (!int.TryParse(raw, out int parsed))
            {
                parsed = 1;
            }

            _sellCount = parsed;
            ClampSellCount(selected);
            UpdateSellCountInput();
        }

        private void ClampSellCount(BagItemView selected)
        {
            int max = selected == null ? 1 : Mathf.Max(1, selected.Count);
            _sellCount = Mathf.Clamp(_sellCount <= 0 ? 1 : _sellCount, 1, max);
        }

        private void UpdateSellCountInput()
        {
            if (_sellCountInput == null)
            {
                return;
            }

            _sellCountInput.SetTextWithoutNotify(_sellCount.ToString());
            _sellCountInput.caretPosition = _sellCountInput.text.Length;
        }

        private void SellSelectedCount(BagItemView selected)
        {
            if (_saleRequest.IsBusy)
            {
                return;
            }

            string targetItemId = selected == null || string.IsNullOrEmpty(selected.ItemId) ? _selectedItemId : selected.ItemId;
            BagItemView current = FindCurrentBagRow(targetItemId);
            if (current == null)
            {
                _lastMessage = "판매 실패";
                Debug.LogWarning("[BagPanelAdapter] Sell failed: selected item is no longer available. itemId=" + targetItemId);
                CloseSaleUiAndRefresh();
                return;
            }

            if (!current.Sellable || IsItemLocked(current))
            {
                _lastMessage = current.Sellable ? "잠금 판매 불가" : "판매 불가";
                Debug.LogWarning("[BagPanelAdapter] Sell blocked before service call: " + current.ItemId);
                CloseSaleUiAndRefresh();
                return;
            }

            ClampSellCount(current);
            int sellCount = _sellCount;
            CurrencyMath.TryMultiply(current.SellPrice, sellCount, out long sellGain);
            Sprite soldIcon = FisherRuntimeUi.ResolveItemIcon(_artProfile, current.ItemId, current.Category);
            int saleRequestToken = BeginSaleRequestTimeout(current.ItemId);
            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            if (bridge == null ||
                !bridge.TryRequestInventorySell(current.ItemId, sellCount, () =>
                {
                    if (!TryCompleteSaleRequest(saleRequestToken))
                    {
                        return;
                    }

                    _lastMessage = "판매 반영";
                    ShowBagResultToast(
                        soldIcon,
                        "판매 완료",
                        current.DisplayNameKo + " " + CompactNumberFormatter.FormatCount(sellCount) +
                        "\n총 판매가 " + CompactNumberFormatter.FormatGold(sellGain));
                    if (current.Count <= sellCount)
                    {
                        _selectedItemId = string.Empty;
                    }

                    CloseSaleUiAndRefresh();
                },
                message =>
                {
                    if (!TryCompleteSaleRequest(saleRequestToken))
                    {
                        return;
                    }

                    _lastMessage = string.IsNullOrWhiteSpace(message) ? "판매 실패" : message;
                    Debug.LogWarning("[BagPanelAdapter] Server sell rejected: " + _lastMessage);
                    CloseSaleUiAndRefresh();
                }))
            {
                _saleRequest.TryAbort(saleRequestToken);
                _lastMessage = "판매 요청 실패";
                Debug.LogWarning("[BagPanelAdapter] Server sell request failed before CloudScript call: " + current.ItemId);
                CloseSaleUiAndRefresh();
                return;
            }

            _lastMessage = "판매 요청 " + current.DisplayNameKo + " x" + sellCount;
            Debug.Log("[BagPanelAdapter] Server sell requested: " + current.ItemId + " x" + sellCount);
            CloseSaleUiAndRefresh();
        }

        private void UseSelectedBox(BagItemView selected)
        {
            if (_boxUseRequest.IsBusy)
            {
                return;
            }

            string targetItemId = selected == null || string.IsNullOrEmpty(selected.ItemId) ? _selectedItemId : selected.ItemId;
            BagItemView current = FindCurrentBagRow(targetItemId);
            if (current == null || !IsBoxItem(current))
            {
                _lastMessage = "상자 개봉 실패";
                Debug.LogWarning("[BagPanelAdapter] Box use failed: selected item is not a box. itemId=" + targetItemId);
                CloseBoxUseUiAndRefresh();
                return;
            }

            if (IsItemLocked(current))
            {
                _lastMessage = "잠금 상자 개봉 불가";
                CloseBoxUseUiAndRefresh();
                return;
            }

            if (RequiresBoxChoice(current))
            {
                _lastMessage = "선택 보상 UI 필요";
                Debug.LogWarning("[BagPanelAdapter] Box use blocked: choice reward UI is not connected yet. itemId=" + current.ItemId);
                CloseBoxUseUiAndRefresh();
                return;
            }

            int requestToken = BeginBoxUseRequestTimeout(current.ItemId);
            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            if (bridge == null ||
                !bridge.TryRequestBoxUse(current.ItemId, 1, null, null, () =>
                {
                    if (!TryCompleteBoxUseRequest(requestToken))
                    {
                        return;
                    }

                    _lastMessage = "상자 개봉 반영";
                    if (current.Count <= 1)
                    {
                        _selectedItemId = string.Empty;
                    }

                    CloseBoxUseUiAndRefresh();
                },
                message =>
                {
                    if (!TryCompleteBoxUseRequest(requestToken))
                    {
                        return;
                    }

                    _lastMessage = string.IsNullOrWhiteSpace(message) ? "상자 개봉 실패" : message;
                    Debug.LogWarning("[BagPanelAdapter] Server box use rejected: " + _lastMessage);
                    CloseBoxUseUiAndRefresh();
                }))
            {
                _boxUseRequest.TryAbort(requestToken);
                _lastMessage = "상자 개봉 요청 실패";
                Debug.LogWarning("[BagPanelAdapter] Server box use request failed before CloudScript call: " + current.ItemId);
                CloseBoxUseUiAndRefresh();
                return;
            }

            _lastMessage = "상자 개봉 요청 " + current.DisplayNameKo;
            Debug.Log("[BagPanelAdapter] Server box use requested: " + current.ItemId);
            CloseBoxUseUiAndRefresh();
        }

        private void ToggleItemLock(BagItemView selected, bool locked)
        {
            ServiceResult result = _context.InventoryService.SetItemLock(selected.ItemId, locked);
            _lastMessage = result.Success ? (locked ? "잠금" : "잠금 해제") : "잠금 실패";
            Debug.Log("[BagPanelAdapter] Lock " + selected.ItemId + " = " + locked + ": " + result.MessageKey);
            _showSellPopup = false;
            _showSellAllConfirm = false;
            _context.NotifyRuntimeChanged();
        }

        private void SellAllUnlockedSellable()
        {
            List<BagItemView> sellableRows = _context.BagQueryService.BuildSnapshot(new BagQueryOptions
            {
                Category = _category,
                Filter = BagFilter.Sellable
            });

            Dictionary<string, int> sellCounts = new Dictionary<string, int>();
            long totalGain = 0;
            int soldKinds = 0;
            for (int i = 0; i < sellableRows.Count; i++)
            {
                BagItemView row = sellableRows[i];
                if (row == null || !row.Sellable || IsItemLocked(row) || row.Count <= 0)
                {
                    continue;
                }

                if (!CurrencyMath.TryMultiply(row.SellPrice, row.Count, out long rowGain) ||
                    !CurrencyMath.TryAdd(totalGain, rowGain, out totalGain))
                {
                    _lastMessage = "전체 판매 overflow";
                    _showSellPopup = false;
                    _showSellAllConfirm = false;
                    _context.NotifyRuntimeChanged();
                    return;
                }

                sellCounts[row.ItemId] = row.Count;
                soldKinds++;
            }

            if (soldKinds == 0)
            {
                _lastMessage = "판매 없음";
                _showSellPopup = false;
                _showSellAllConfirm = false;
                _context.NotifyRuntimeChanged();
                return;
            }

            int saleRequestToken = BeginSaleRequestTimeout("tab_all");
            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            if (bridge == null ||
                !bridge.TryRequestInventorySellBatch(sellCounts, () =>
                {
                    if (!TryCompleteSaleRequest(saleRequestToken))
                    {
                        return;
                    }

                    _selectedItemId = string.Empty;
                    _lastMessage = "전체 판매 반영";
                    ShowBagResultToast(null, "전체 판매 완료", "아이템 " + soldKinds + "종\n총 판매가 " + CompactNumberFormatter.FormatGold(totalGain));
                    CloseSaleUiAndRefresh();
                },
                message =>
                {
                    if (!TryCompleteSaleRequest(saleRequestToken))
                    {
                        return;
                    }

                    _lastMessage = string.IsNullOrWhiteSpace(message) ? "전체 판매 실패" : message;
                    Debug.LogWarning("[BagPanelAdapter] Server sell-all rejected: " + _lastMessage);
                    CloseSaleUiAndRefresh();
                }))
            {
                _saleRequest.TryAbort(saleRequestToken);
                _lastMessage = "전체 판매 요청 실패";
                _showSellPopup = false;
                _showSellAllConfirm = false;
                _context.NotifyRuntimeChanged();
                return;
            }

            _showSellPopup = false;
            _showSellAllConfirm = false;
            _sellCount = 1;
            _lastMessage = "전체 판매 요청 " + soldKinds + "종 · 총 판매가 " + CompactNumberFormatter.FormatGold(totalGain);
            _context.NotifyRuntimeChanged();
        }

        private void ShowBagResultToast(Sprite icon, string title, string meta)
        {
            FisherRuntimeUi.ShowResultToast(this, _inventoryPanel == null ? null : _inventoryPanel.transform, icon, title, meta);
        }

        private int BeginSaleRequestTimeout(string requestLabel)
        {
            if (!_saleRequest.TryBegin(requestLabel))
            {
                return -1;
            }

            int token = _saleRequest.Token;
            StartCoroutine(ReleaseSaleRequestOnTimeout(token, requestLabel));
            return token;
        }

        private bool TryCompleteSaleRequest(int token)
        {
            return _saleRequest.TryComplete(token);
        }

        private IEnumerator ReleaseSaleRequestOnTimeout(int token, string requestLabel)
        {
            yield return new WaitForSecondsRealtime(ServerSellResponseTimeoutSeconds);

            if (!_saleRequest.TryRecoverTimeout(ServerSellResponseTimeoutSeconds, requestLabel, out string requestName))
            {
                yield break;
            }

            _lastMessage = "판매 응답 지연/실패";
            Debug.LogWarning("[BagPanelAdapter] Server sell request timed out before callback: " + requestName);
            CloseSaleUiAndRefresh();
        }

        private int BeginBagExpansionRequestTimeout()
        {
            if (!_bagExpansionRequest.TryBegin("BagCapacityExpansion"))
            {
                return -1;
            }

            int token = _bagExpansionRequest.Token;
            StartCoroutine(ReleaseBagExpansionRequestOnTimeout(token));
            return token;
        }

        private bool TryCompleteBagExpansionRequest(int token)
        {
            return _bagExpansionRequest.TryComplete(token);
        }

        private IEnumerator ReleaseBagExpansionRequestOnTimeout(int token)
        {
            yield return new WaitForSecondsRealtime(ServerBagExpansionResponseTimeoutSeconds);

            if (!_bagExpansionRequest.TryRecoverTimeout(ServerBagExpansionResponseTimeoutSeconds, "BagCapacityExpansion", out string requestName))
            {
                yield break;
            }

            _lastMessage = "가방 확장 응답 지연/실패";
            Debug.LogWarning("[BagPanelAdapter] Server bag expansion request timed out before callback: " + requestName);
            _context.NotifyRuntimeChanged();
            Refresh();
        }

        private int BeginBoxUseRequestTimeout(string requestLabel)
        {
            if (!_boxUseRequest.TryBegin(requestLabel))
            {
                return -1;
            }

            int token = _boxUseRequest.Token;
            StartCoroutine(ReleaseBoxUseRequestOnTimeout(token, requestLabel));
            return token;
        }

        private bool TryCompleteBoxUseRequest(int token)
        {
            return _boxUseRequest.TryComplete(token);
        }

        private IEnumerator ReleaseBoxUseRequestOnTimeout(int token, string requestLabel)
        {
            yield return new WaitForSecondsRealtime(ServerSellResponseTimeoutSeconds);

            if (!_boxUseRequest.TryRecoverTimeout(ServerSellResponseTimeoutSeconds, requestLabel, out string requestName))
            {
                yield break;
            }

            _lastMessage = "상자 개봉 응답 지연/실패";
            Debug.LogWarning("[BagPanelAdapter] Server box use request timed out before callback: " + requestName);
            CloseBoxUseUiAndRefresh();
        }

        private bool TryBuildSellAllPreview(out int sellableKinds, out long totalCount, out long totalGain)
        {
            sellableKinds = 0;
            totalCount = 0;
            totalGain = 0;

            List<BagItemView> sellableRows = _context.BagQueryService.BuildSnapshot(new BagQueryOptions
            {
                Category = _category,
                Filter = BagFilter.Sellable
            });

            for (int i = 0; i < sellableRows.Count; i++)
            {
                BagItemView row = sellableRows[i];
                if (row == null || !row.Sellable || IsItemLocked(row) || row.Count <= 0)
                {
                    continue;
                }

                if (!CurrencyMath.TryMultiply(row.SellPrice, row.Count, out long rowGain) ||
                    !CurrencyMath.TryAdd(totalGain, rowGain, out totalGain) ||
                    !CurrencyMath.TryAdd(totalCount, row.Count, out totalCount))
                {
                    return false;
                }

                sellableKinds++;
            }

            return true;
        }

        private BagItemView FindCurrentBagRow(string itemId)
        {
            if (_context == null || _context.BagQueryService == null || string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            List<BagItemView> rows = _context.BagQueryService.BuildSnapshot(new BagQueryOptions
            {
                Category = "All",
                Filter = BagFilter.None
            });
            for (int i = 0; i < rows.Count; i++)
            {
                BagItemView row = rows[i];
                if (row != null && row.ItemId == itemId)
                {
                    return row;
                }
            }

            return null;
        }

        private static bool IsServerSellBlocked()
        {
            return false;
        }

        private static bool IsServerBoxUseBlocked()
        {
            return false;
        }

        private static bool IsServerBagExpansionBlocked()
        {
            return !BagExpansionUiEnabled;
        }

        private string SellStatusLabel(BagItemView selected, bool locked)
        {
            if (selected == null)
            {
                return "선택 없음";
            }

            if (locked)
            {
                return "잠금";
            }

            if (!selected.Sellable)
            {
                return "판매 불가";
            }

            return _saleRequest.IsBusy ? ServerSellPendingMessage : "판매 가능";
        }

        private string BoxUseStatusLabel(BagItemView selected, bool locked)
        {
            if (!IsBoxItem(selected))
            {
                return "대상 아님";
            }

            if (locked)
            {
                return "잠금";
            }

            if (RequiresBoxChoice(selected))
            {
                return "선택 보상 UI 필요";
            }

            return _boxUseRequest.IsBusy ? ServerBoxUsePendingMessage : "개봉 가능";
        }

        private static bool IsBoxItem(BagItemView item)
        {
            return item != null &&
                   (string.Equals(item.Category, "Box", System.StringComparison.Ordinal) ||
                    (!string.IsNullOrEmpty(item.ItemId) && item.ItemId.StartsWith("box_", System.StringComparison.Ordinal)));
        }

        private static bool RequiresBoxChoice(BagItemView item)
        {
            return item != null && string.Equals(item.ItemId, "box_basic_reward", System.StringComparison.Ordinal);
        }

        private void CloseSaleUiAndRefresh(bool pullSnapshot = false)
        {
            _showSellPopup = false;
            _showSellAllConfirm = false;
            _sellCount = 1;
            if (pullSnapshot)
            {
                PullInventorySnapshotFromPlayFabDataStore();
            }

            if (_context != null)
            {
                _context.NotifyRuntimeChanged();
            }

            Refresh();
        }

        private void CloseBoxUseUiAndRefresh(bool pullSnapshot = false)
        {
            _showSellPopup = false;
            _showSellAllConfirm = false;
            if (pullSnapshot)
            {
                PullInventorySnapshotFromPlayFabDataStore();
            }

            if (_context != null)
            {
                _context.NotifyRuntimeChanged();
            }

            Refresh();
        }

        private FisherPlayerDataBridge ResolvePlayerDataBridge()
        {
            if (_playerDataBridge != null &&
                (_context == null || _playerDataBridge.IsBoundToContext(_context)))
            {
                return _playerDataBridge;
            }

            if (_playerDataBridge != null)
            {
                _playerDataBridge = null;
            }

            _playerDataBridge = FisherPlayerDataBridgeResolver.Resolve(_context, this);
            if (_playerDataBridge != null &&
                _context != null &&
                !_playerDataBridge.IsBoundToContext(_context))
            {
                _playerDataBridge.Configure(_context);
            }

            return _playerDataBridge;
        }

        #endregion

        private static string CategoryLabel(string category)
        {
            switch (category)
            {
                case "Fish":
                    return "물고기";
                case "Food":
                    return "요리";
                case "Material":
                case "UpgradeMaterial":
                case "HighGradeMaterial":
                    return "재료";
                case "Other":
                case "Ticket":
                case "Special":
                case "Box":
                case "ChoiceTicket":
                    return "기타";
                default:
                    return "전체";
            }
        }

        private static string CategoryBadge(string category)
        {
            switch (category)
            {
                case "Fish":
                    return "물";
                case "Food":
                    return "요";
                case "Material":
                case "UpgradeMaterial":
                case "HighGradeMaterial":
                    return "재";
                default:
                    return "기";
            }
        }

        private bool IsItemLocked(BagItemView item)
        {
            if (item == null)
            {
                return false;
            }

            return item.Locked ||
                (_context != null &&
                 _context.InventoryService != null &&
                 _context.InventoryService.IsItemLocked(item.ItemId));
        }

        private string BuildItemSlotBadge(BagItemView item)
        {
            if (item == null)
            {
                return "I";
            }

            if (item.NewNotice)
            {
                return "N";
            }

            return IsItemLocked(item) ? "잠" : CategoryBadge(item.Category);
        }

    }
}
