using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 상점 패널에서 런타임 SO/JSON 상품 목록을 표시하고 구매 요청을 서버로 보냅니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopPanelAdapter : MonoBehaviour
    {
        private const int MinimumVisibleShopSlots = 15;

        #region Inspector References

        [SerializeField] private FisherRuntimeContext _context;
        [SerializeField] private GameObject _panel;
        [SerializeField] private FisherPanelView _view;

        [Header("Art Contract")]
        [Tooltip("팀장이 상점 슬롯, 버튼, 상품 itemId 아이콘을 연결하는 공통 UI 아트 프로필입니다.")]
        [SerializeField] private FisherUiArtProfile _artProfile;

        [Header("Server Authority")]
        [Tooltip("PurchaseShopItem 서버 mutation을 사용할지 결정합니다. 실패 응답은 UI 잠금을 복구합니다.")]
        [SerializeField] private bool _enableServerShopPurchaseRequests = true;
        [Tooltip("CloudScript 응답이 돌아오지 않을 때 CSH 상점 버튼 잠금을 복구하는 시간입니다.")]
        [SerializeField] private float _serverShopPurchaseRequestTimeoutSeconds = 12f;

        #endregion

        #region View State

        private TextMeshProUGUI _template;
        private string _category = "gold";
        private string _lastMessage = "골드상점";
        private readonly FisherServerRequestGate _purchaseRequest = new FisherServerRequestGate();
        private ShopPurchaseSheetView _purchaseSheet;
        private ShopSlotEntry _selectedPurchaseEntry;
        private string _purchaseSheetStatusOverride;
        private float _suppressPlayerDataSnapshotPullUntil;

        #endregion

        #region View Models

        private sealed class ShopSlotEntry
        {
            public ShopItemDefinition ShopItem;
            public PremiumCurrencyProductDefinition PremiumProduct;
            public string Category;
            public int SortOrder;
            public string Id;

            public bool IsPremiumCurrencyProduct => PremiumProduct != null;
        }

        #endregion

        #region Configuration

        /// <summary>
        /// 부트스트래퍼가 상점 패널과 Fisher 런타임 컨텍스트를 주입할 때 호출합니다.
        /// </summary>
        public void Configure(FisherRuntimeContext context, GameObject panel)
        {
            SetContext(context);
            _panel = panel;
            ResolveArtProfile();
            _template = FisherRuntimeUi.FindTextTemplate(panel);
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            global::RuntimeStateEventBus.OnShopStateEntered += HandleShopStateEntered;
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
                _context.RuntimeChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            global::RuntimeStateEventBus.OnShopStateEntered -= HandleShopStateEntered;
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
            }

            _purchaseRequest.Invalidate();
            HidePurchaseSheet(clearSelection: true);
        }

        private void Update()
        {
            RecoverExpiredServerShopPurchaseRequest();
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
        /// 현재 분류 조건에 맞는 상점 상품을 다시 그리고 구매 버튼 상태를 갱신합니다.
        /// </summary>
        public void Refresh()
        {
            if (_panel == null || _context == null)
            {
                return;
            }

            _template ??= FisherRuntimeUi.FindTextTemplate(_panel);
            ResolveArtProfile();

            if (!_context.IsReady)
            {
                if (!TryRenderStaticStatus(_context.LastStatus))
                {
                    LogStaticViewUnavailable("status");
                }
                return;
            }

            PullPlayerDataSnapshotForShop();
            List<ShopSlotEntry> visibleEntries = BuildVisibleShopEntries();
            if (TryRenderStaticView(visibleEntries))
            {
                return;
            }

            LogStaticViewUnavailable("refresh");
        }

        private bool TryRenderStaticStatus(string status)
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return false;
            }

            HideLegacyRoot("Fisher_ShopRoot");
            SetText(view.TitleText, string.Empty);
            SetText(view.StatusText, string.Empty);
            SetText(view.SubStatusText, string.Empty);
            FisherRuntimeUi.ApplyHeaderStatusParchmentText(view);
            FisherRuntimeUi.ApplyDetailParchmentText(view);
            ClearAllSlots(view);
            view.SetMainSectionsVisible(tabs: false, grid: false, detail: false, actions: false);
            view.HideActionSheets();
            HidePurchaseSheet(clearSelection: true);
            return true;
        }

        private bool TryRenderStaticView(List<ShopSlotEntry> visibleEntries)
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return false;
            }

            HideLegacyRoot("Fisher_ShopRoot");
            ResolvePurchaseSheet(view);
            view.SetMainSectionsVisible(tabs: true, grid: true, detail: false, actions: false);
            SetText(view.TitleText, string.Empty);
            SetText(view.StatusText, string.Empty);
            SetText(view.SubStatusText, string.Empty);
            FisherRuntimeUi.ApplyHeaderStatusParchmentText(view);
            FisherRuntimeUi.ApplyDetailParchmentText(view);
            FisherRuntimeUi.EnsureShopCurrencyStrip(view, _context.State, _template, _artProfile);
            if (view.HeaderAction != null)
            {
                view.HeaderAction.gameObject.SetActive(false);
            }

            view.SetTabs(
                new[] { "골드상점", "펄상점", "주화상점", "특수" },
                new[] { "gold", "pearl", "coin", "special" },
                _category,
                key =>
                {
                    _category = key;
                    _lastMessage = CategoryLabel(key);
                    HidePurchaseSheet(clearSelection: true);
                    Refresh();
                });

            view.HideActionSheets();
            ClearAllSlots(view);
            int slotCount = Mathf.Max(visibleEntries.Count, MinimumVisibleShopSlots);
            for (int i = 0; i < slotCount; i++)
            {
                FisherSlotView slot = view.GetExistingSlot(i, "ShopSlot");
                if (slot == null)
                {
                    continue;
                }

                if (i < visibleEntries.Count)
                {
                    BindShopSlot(slot, visibleEntries[i]);
                }
            }

            view.HideUnusedSlots(slotCount);
            RefreshPurchaseSheetSelection(visibleEntries);
            return true;
        }

        private FisherPanelView ResolveStaticView()
        {
            if (_panel == null)
            {
                return null;
            }

            if (!FisherPanelViewResolver.TryResolveExistingView(
                    _panel,
                    _view,
                    "ShopPanel",
                    nameof(ShopPanelAdapter),
                    FisherSlotLayout.Shop,
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
            Debug.LogWarning("[ShopPanelAdapter] ShopPanel/ViewRoot 고정 View를 만들거나 찾지 못했습니다. " +
                             "기존 Inspector UI 보호를 위해 레거시 자동 렌더링을 건너뜁니다. state=" + state);
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

        private void BindShopSlot(FisherSlotView slot, ShopSlotEntry entry)
        {
            if (entry == null)
            {
                slot?.Clear();
                return;
            }

            if (entry.IsPremiumCurrencyProduct)
            {
                BindPremiumCurrencySlot(slot, entry);
                return;
            }

            BindStandardShopSlot(slot, entry);
        }

        private void BindStandardShopSlot(FisherSlotView slot, ShopSlotEntry entry)
        {
            ShopItemDefinition shopItem = entry?.ShopItem;
            if (slot == null || shopItem == null)
            {
                return;
            }

            string rewardName = DisplayName(shopItem.RewardItemId);
            bool supportedPrice = FisherCurrencyContract.IsKnownCurrency(shopItem.PriceType);
            bool visible = _context.ShopService.IsShopItemVisible(shopItem);
            bool unlocked = UnlockConditionEvaluator.IsUnlocked(shopItem.UnlockCondition, _context.BuildResult.Catalog, _context.State);
            bool hasEnoughCurrency = FisherCurrencyContract.GetBalance(_context.State, shopItem.PriceType) >= shopItem.PriceAmount;
            bool requestBlocked = IsServerShopPurchaseBlocked();
            bool canBuy = shopItem.IsEnabled &&
                          visible &&
                          supportedPrice &&
                          unlocked &&
                          hasEnoughCurrency &&
                          !requestBlocked;
            string status = StatusLabel(shopItem, supportedPrice, visible, unlocked, hasEnoughCurrency, requestBlocked);
            Sprite rewardIcon = FisherRuntimeUi.ResolveItemIcon(_artProfile, shopItem.RewardItemId, shopItem.Category);
            slot.Bind(
                rewardName + " x" + CompactNumberFormatter.Format(shopItem.RewardCount),
                FisherCurrencyContract.FormatAmount(shopItem.PriceType, shopItem.PriceAmount),
                status,
                string.Empty,
                rewardIcon,
                IsSelectedPurchaseEntry(entry),
                !canBuy,
                false,
                false,
                () => ShowPurchaseSheet(entry));
        }

        private void BindPremiumCurrencySlot(FisherSlotView slot, ShopSlotEntry entry)
        {
            PremiumCurrencyProductDefinition product = entry?.PremiumProduct;
            if (slot == null || product == null)
            {
                return;
            }

            bool hasHiddenCash = _context != null &&
                                 _context.State != null &&
                                 _context.State.cash >= product.CashAmount;
            bool requestBlocked = IsServerShopPurchaseBlocked();
            bool canBuy = product.IsEnabled && hasHiddenCash && !requestBlocked;
            string productId = product.ProductId;
            Sprite productIcon = FisherRuntimeUi.ResolveItemIcon(_artProfile, productId, "pearl");
            slot.Bind(
                "진주 " + CompactNumberFormatter.Format(product.PrismPearlAmount),
                "프리미엄 상품",
                PremiumPurchaseStatus(product, hasHiddenCash, requestBlocked),
                "PP",
                productIcon,
                IsSelectedPurchaseEntry(entry),
                !canBuy,
                false,
                false,
                () => ShowPurchaseSheet(entry));
        }

        private void ShowPurchaseSheet(ShopSlotEntry entry)
        {
            if (entry == null)
            {
                HidePurchaseSheet(clearSelection: true);
                return;
            }

            _selectedPurchaseEntry = entry;
            _purchaseSheetStatusOverride = null;
            RenderPurchaseSheet();
        }

        private void RefreshPurchaseSheetSelection(IReadOnlyList<ShopSlotEntry> visibleEntries)
        {
            if (_selectedPurchaseEntry == null)
            {
                HideResolvedPurchaseSheet();
                return;
            }

            ShopSlotEntry refreshedEntry = FindMatchingVisibleEntry(visibleEntries, _selectedPurchaseEntry);
            if (refreshedEntry == null)
            {
                HidePurchaseSheet(clearSelection: true);
                return;
            }

            _selectedPurchaseEntry = refreshedEntry;
            RenderPurchaseSheet();
        }

        private ShopSlotEntry FindMatchingVisibleEntry(IReadOnlyList<ShopSlotEntry> visibleEntries, ShopSlotEntry selected)
        {
            if (visibleEntries == null || selected == null)
            {
                return null;
            }

            for (int i = 0; i < visibleEntries.Count; i++)
            {
                ShopSlotEntry candidate = visibleEntries[i];
                if (IsSamePurchaseEntry(candidate, selected))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RenderPurchaseSheet()
        {
            if (_selectedPurchaseEntry == null)
            {
                HidePurchaseSheet(clearSelection: true);
                return;
            }

            ShopPurchaseSheetView sheet = ResolvePurchaseSheet(_view);
            if (sheet == null)
            {
                return;
            }

            ConfigurePurchaseSheetChrome(sheet);

            if (_selectedPurchaseEntry.IsPremiumCurrencyProduct)
            {
                RenderPremiumPurchaseSheet(sheet, _selectedPurchaseEntry.PremiumProduct);
                return;
            }

            RenderStandardPurchaseSheet(sheet, _selectedPurchaseEntry.ShopItem);
        }

        private void RenderStandardPurchaseSheet(ShopPurchaseSheetView sheet, ShopItemDefinition shopItem)
        {
            if (sheet == null || shopItem == null)
            {
                HidePurchaseSheet(clearSelection: true);
                return;
            }

            _context.BuildResult.Catalog.TryGetItem(shopItem.RewardItemId, out ItemDefinition rewardItem);
            string rewardName = string.IsNullOrWhiteSpace(rewardItem?.DisplayNameKo)
                ? DisplayName(shopItem.RewardItemId)
                : rewardItem.DisplayNameKo;
            bool supportedPrice = FisherCurrencyContract.IsKnownCurrency(shopItem.PriceType);
            bool visible = _context.ShopService.IsShopItemVisible(shopItem);
            bool unlocked = UnlockConditionEvaluator.IsUnlocked(shopItem.UnlockCondition, _context.BuildResult.Catalog, _context.State);
            long balance = FisherCurrencyContract.GetBalance(_context.State, shopItem.PriceType);
            bool hasEnoughCurrency = balance >= shopItem.PriceAmount;
            bool requestBlocked = IsServerShopPurchaseBlocked();
            bool canBuy = shopItem.IsEnabled &&
                          visible &&
                          supportedPrice &&
                          unlocked &&
                          hasEnoughCurrency &&
                          !requestBlocked;
            string status = PurchaseSheetStatusOrDefault(
                StatusLabel(shopItem, supportedPrice, visible, unlocked, hasEnoughCurrency, requestBlocked));
            string rewardMeta = rewardName + " x" + CompactNumberFormatter.Format(shopItem.RewardCount);
            Sprite rewardIcon = FisherRuntimeUi.ResolveItemIcon(_artProfile, shopItem.RewardItemId, shopItem.Category);

            sheet.Show(
                rewardIcon,
                rewardName,
                BuildShopItemDescription(shopItem, rewardItem),
                "지급 수량 x" + CompactNumberFormatter.Format(shopItem.RewardCount),
                "가격 " + FisherCurrencyContract.FormatAmount(shopItem.PriceType, shopItem.PriceAmount),
                status,
                canBuy,
                requestBlocked,
                () => PurchaseAndRefresh(shopItem.ShopItemId, rewardIcon, "구매 완료", rewardMeta),
                () => HidePurchaseSheet(clearSelection: true));
        }

        private void RenderPremiumPurchaseSheet(ShopPurchaseSheetView sheet, PremiumCurrencyProductDefinition product)
        {
            if (sheet == null || product == null)
            {
                HidePurchaseSheet(clearSelection: true);
                return;
            }

            bool hasHiddenCash = _context != null &&
                                 _context.State != null &&
                                 _context.State.cash >= product.CashAmount;
            bool requestBlocked = IsServerShopPurchaseBlocked();
            bool canBuy = product.IsEnabled && hasHiddenCash && !requestBlocked;
            string productTitle = "진주 " + CompactNumberFormatter.Format(product.PrismPearlAmount);
            string status = PurchaseSheetStatusOrDefault(
                PremiumPurchaseStatus(product, hasHiddenCash, requestBlocked));
            Sprite productIcon = FisherRuntimeUi.ResolveItemIcon(_artProfile, product.ProductId, "pearl");

            sheet.Show(
                productIcon,
                productTitle,
                "구매 후 프리즘 진주가 지급됩니다.",
                "지급 진주 x" + CompactNumberFormatter.Format(product.PrismPearlAmount),
                "가격 " + CompactNumberFormatter.Format(product.CashAmount) + " 캐시",
                status,
                canBuy,
                requestBlocked,
                () => PurchasePremiumCurrencyProduct(product.ProductId, productIcon, product.PrismPearlAmount),
                () => HidePurchaseSheet(clearSelection: true));
        }

        private ShopPurchaseSheetView ResolvePurchaseSheet(FisherPanelView view = null)
        {
            if (_purchaseSheet != null)
            {
                return _purchaseSheet;
            }

            _purchaseSheet = null;

            if (view != null)
            {
                _purchaseSheet = view.GetComponentInChildren<ShopPurchaseSheetView>(true);
            }

            if (_purchaseSheet == null && _panel != null)
            {
                _purchaseSheet = _panel.GetComponentInChildren<ShopPurchaseSheetView>(true);
            }

            return _purchaseSheet;
        }

        private void ConfigurePurchaseSheetChrome(ShopPurchaseSheetView sheet)
        {
            if (sheet == null)
            {
                return;
            }

            ResolveArtProfile();
            ConfigurePurchaseSheetButtonChrome(sheet.CancelButton);
            ConfigurePurchaseSheetButtonChrome(sheet.PurchaseButton);
        }

        private void ConfigurePurchaseSheetButtonChrome(FisherButtonView button)
        {
            if (button == null)
            {
                return;
            }

            button.PreserveInspectorChrome = false;
            button.NormalSprite = _artProfile == null ? null : _artProfile.ButtonNormal;
            button.SelectedSprite = _artProfile == null ? null : _artProfile.ButtonSelected;
            button.DisabledSprite = _artProfile == null ? null : _artProfile.ButtonDisabled;
        }

        private void HidePurchaseSheet(bool clearSelection)
        {
            _purchaseSheetStatusOverride = null;
            if (clearSelection)
            {
                _selectedPurchaseEntry = null;
            }

            HideResolvedPurchaseSheet();
        }

        private void HideResolvedPurchaseSheet()
        {
            ShopPurchaseSheetView sheet = ResolvePurchaseSheet(_view);
            if (sheet != null)
            {
                sheet.Hide();
            }
        }

        private bool IsSelectedPurchaseEntry(ShopSlotEntry entry)
        {
            return IsSamePurchaseEntry(entry, _selectedPurchaseEntry);
        }

        private static bool IsSamePurchaseEntry(ShopSlotEntry left, ShopSlotEntry right)
        {
            if (left == null || right == null || left.IsPremiumCurrencyProduct != right.IsPremiumCurrencyProduct)
            {
                return false;
            }

            return string.Equals(left.Id, right.Id, System.StringComparison.Ordinal) &&
                   string.Equals(left.Category, right.Category, System.StringComparison.Ordinal);
        }

        private string PurchaseSheetStatusOrDefault(string fallback)
        {
            return string.IsNullOrWhiteSpace(_purchaseSheetStatusOverride)
                ? fallback
                : _purchaseSheetStatusOverride;
        }

        private static string BuildShopItemDescription(ShopItemDefinition shopItem, ItemDefinition rewardItem)
        {
            if (!string.IsNullOrWhiteSpace(shopItem?.Notes))
            {
                return shopItem.Notes;
            }

            if (!string.IsNullOrWhiteSpace(rewardItem?.Notes))
            {
                return rewardItem.Notes;
            }

            return "구매 후 가방에 지급됩니다.";
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

        private void HideLegacyRoot(string rootName)
        {
            Transform legacyRoot = _panel == null ? null : _panel.transform.Find(rootName);
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

        #endregion

        #region Entry State

        private void HandleShopStateEntered()
        {
            _category = "gold";
            _lastMessage = "골드상점";
            HidePurchaseSheet(clearSelection: true);
            Refresh();
        }

        #endregion

        #region Shop Data

        private List<ShopSlotEntry> BuildVisibleShopEntries()
        {
            List<ShopSlotEntry> entries = new List<ShopSlotEntry>();
            foreach (ShopItemDefinition shopItem in _context.BuildResult.Catalog.ShopItemsById.Values)
            {
                if (shopItem == null)
                {
                    continue;
                }

                if (!shopItem.IsEnabled)
                {
                    continue;
                }

                if (!_context.ShopService.IsShopItemVisible(shopItem))
                {
                    continue;
                }

                if (!UnlockConditionEvaluator.IsUnlocked(shopItem.UnlockCondition, _context.BuildResult.Catalog, _context.State))
                {
                    continue;
                }

                if (!MatchesCategory(shopItem))
                {
                    continue;
                }

                entries.Add(new ShopSlotEntry
                {
                    ShopItem = shopItem,
                    Category = shopItem.Category,
                    SortOrder = shopItem.SortOrder,
                    Id = shopItem.ShopItemId
                });
            }

            if (_category == "pearl")
            {
                foreach (PremiumCurrencyProductDefinition product in _context.BuildResult.Catalog.PremiumCurrencyProductsById.Values)
                {
                    if (product == null || !product.IsEnabled)
                    {
                        continue;
                    }

                    entries.Add(new ShopSlotEntry
                    {
                        PremiumProduct = product,
                        Category = "pearl",
                        SortOrder = product.SortOrder,
                        Id = product.ProductId
                    });
                }
            }

            entries.Sort(CompareShopEntries);
            return entries;
        }

        private bool MatchesCategory(ShopItemDefinition shopItem)
        {
            return shopItem.Category == _category;
        }

        private static int CompareShopEntries(ShopSlotEntry left, ShopSlotEntry right)
        {
            int category = string.Compare(left.Category, right.Category, System.StringComparison.Ordinal);
            if (category != 0)
            {
                return category;
            }

            int entryType = EntryTypeRank(left).CompareTo(EntryTypeRank(right));
            if (entryType != 0)
            {
                return entryType;
            }

            int sort = left.SortOrder.CompareTo(right.SortOrder);
            if (sort != 0)
            {
                return sort;
            }

            return string.Compare(left.Id, right.Id, System.StringComparison.Ordinal);
        }

        private static int EntryTypeRank(ShopSlotEntry entry)
        {
            return entry != null && entry.IsPremiumCurrencyProduct ? 1 : 0;
        }

        #endregion

        #region Player Sync

        private void PullPlayerDataSnapshotForShop(bool force = false)
        {
            if (!force && Time.unscaledTime < _suppressPlayerDataSnapshotPullUntil)
            {
                return;
            }

            FisherPlayerDataBridge bridge = FisherPlayerDataBridgeResolver.Resolve(_context, this);
            bridge?.PullCurrenciesFromPlayerData(notify: false);
            bridge?.PullCurrenciesFromPlayFabDataStore(notify: false);
        }

        private void SuppressPlayerDataSnapshotPull()
        {
            _suppressPlayerDataSnapshotPullUntil = Mathf.Max(
                _suppressPlayerDataSnapshotPullUntil,
                Time.unscaledTime + FisherServerMutationPolicy.SnapshotPullSuppressSeconds);
        }

        private void PurchaseAndRefresh(string shopItemId, Sprite toastIcon, string toastTitle, string toastMeta)
        {
            if (!TryBeginServerShopPurchaseRequest(shopItemId))
            {
                Refresh();
                return;
            }

            int requestToken = _purchaseRequest.Token;
            FisherPlayerDataBridge bridge = ShopBridge();
            if (bridge == null ||
                !bridge.TryRequestShopPurchase(
                    shopItemId,
                    () => CompleteServerShopPurchaseRequest(requestToken, "구매 반영", toastIcon, toastTitle, toastMeta),
                    message => HandleServerShopPurchaseRejected(requestToken, message, shopItemId)))
            {
                AbortServerShopPurchaseRequest(requestToken, "PurchaseShopItem 요청 실패");
                return;
            }

            _lastMessage = "구매 요청 중";
            _purchaseSheetStatusOverride = "요청 중";
            Refresh();
        }

        private void PurchasePremiumCurrencyProduct(string productId, Sprite toastIcon, long prismPearlAmount)
        {
            if (string.IsNullOrEmpty(productId))
            {
                _lastMessage = "프리미엄 상품 준비 실패";
                Refresh();
                return;
            }

            if (!TryBeginServerShopPurchaseRequest(productId))
            {
                Refresh();
                return;
            }

            int requestToken = _purchaseRequest.Token;
            FisherPlayerDataBridge bridge = ShopBridge();
            if (bridge == null ||
                !bridge.TryRequestPremiumCurrencyProductPurchase(
                    productId,
                    () => CompleteServerShopPurchaseRequest(
                        requestToken,
                        "프리미엄 상품 구매 반영",
                        toastIcon,
                        "구매 완료",
                        "진주 +" + CompactNumberFormatter.Format(prismPearlAmount)),
                    message => AbortServerShopPurchaseRequest(requestToken, message)))
            {
                AbortServerShopPurchaseRequest(requestToken, "PurchasePremiumCurrencyProduct 요청 실패");
                return;
            }

            _lastMessage = "프리미엄 상품 요청 중";
            _purchaseSheetStatusOverride = "요청 중";
            Refresh();
        }

        private void HandleServerShopPurchaseRejected(int requestToken, string message, string shopItemId)
        {
            AbortServerShopPurchaseRequest(requestToken, message);
        }

        private bool TryBeginServerShopPurchaseRequest(string shopItemId)
        {
            if (!_purchaseRequest.TryBegin(shopItemId))
            {
                return false;
            }

            _lastMessage = "서버 구매 요청 중";
            return true;
        }

        private void CompleteServerShopPurchaseRequest(
            int requestToken,
            string message,
            Sprite toastIcon = null,
            string toastTitle = null,
            string toastMeta = null)
        {
            if (!_purchaseRequest.TryComplete(requestToken))
            {
                return;
            }

            _lastMessage = string.IsNullOrWhiteSpace(message) ? "구매 갱신 완료" : message;
            HidePurchaseSheet(clearSelection: true);
            SuppressPlayerDataSnapshotPull();
            _context?.NotifyRuntimeChanged();
            ShowShopResultToast(toastIcon, toastTitle, toastMeta);
            Refresh();
        }

        private void ShowShopResultToast(Sprite icon, string title, string meta)
        {
            FisherRuntimeUi.ShowResultToast(this, _panel == null ? null : _panel.transform, icon, title, meta);
        }

        private void AbortServerShopPurchaseRequest(int requestToken, string message)
        {
            if (!_purchaseRequest.TryAbort(requestToken))
            {
                return;
            }

            _lastMessage = string.IsNullOrWhiteSpace(message) ? "구매 요청 실패" : message;
            _purchaseSheetStatusOverride = null;
            Debug.LogWarning("[ShopPanelAdapter] " + _lastMessage);
            PullPlayerDataSnapshotForShop(force: true);
            Refresh();
        }

        private void RecoverExpiredServerShopPurchaseRequest()
        {
            if (!_purchaseRequest.TryRecoverTimeout(
                    _serverShopPurchaseRequestTimeoutSeconds,
                    "PurchaseShopItem",
                    out string requestName))
            {
                return;
            }

            _lastMessage = "서버 응답 지연";
            _purchaseSheetStatusOverride = null;
            Debug.LogWarning("[ShopPanelAdapter] " + requestName + " request timed out; UI lock recovered.");
            PullPlayerDataSnapshotForShop(force: true);
            Refresh();
        }

        private FisherPlayerDataBridge ShopBridge()
        {
            return FisherPlayerDataBridgeResolver.Resolve(_context, this);
        }

        #endregion

        #region Text Helpers

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

        private static string CategoryLabel(string category)
        {
            switch (category)
            {
                case "gold":
                    return "골드상점";
                case "pearl":
                    return "펄상점";
                case "coin":
                    return "주화상점";
                case "special":
                    return "특수";
                default:
                    return "미분류";
            }
        }

        private static string CategoryBadge(string category)
        {
            switch (category)
            {
                case "gold":
                    return "G";
                case "pearl":
                    return "P";
                case "coin":
                    return "C";
                case "special":
                    return "S";
                default:
                    return "?";
            }
        }

        private string StatusLabel(
            ShopItemDefinition shopItem,
            bool supportedPrice,
            bool visible,
            bool unlocked,
            bool hasEnoughCurrency,
            bool requestBlocked)
        {
            if (shopItem == null || !shopItem.IsEnabled)
            {
                return "구매 불가";
            }

            if (!supportedPrice)
            {
                return "구매 불가";
            }

            if (!visible || !unlocked)
            {
                return "구매 불가";
            }

            if (!hasEnoughCurrency)
            {
                return "재화 부족";
            }

            if (requestBlocked)
            {
                return "요청 중";
            }

            return "구매 가능";
        }

        private static string PremiumPurchaseStatus(PremiumCurrencyProductDefinition product, bool hasHiddenCash, bool requestBlocked)
        {
            if (product == null || !product.IsEnabled)
            {
                return "구매 불가";
            }

            if (requestBlocked)
            {
                return "요청 중";
            }

            return hasHiddenCash ? "구매 가능" : "재화 부족";
        }

        private bool IsServerShopPurchaseBlocked()
        {
            if (_purchaseRequest.IsBusy)
            {
                return true;
            }

            if (!_enableServerShopPurchaseRequests)
            {
                return false;
            }

            return false;
        }

        private string CurrentStatusMessage()
        {
            if (_purchaseRequest.IsBusy)
            {
                return _purchaseRequest.CurrentMessage("서버 구매 요청 중");
            }

            return _lastMessage;
        }

        private string DisplayName(string itemId)
        {
            if (_context.BuildResult.Catalog.TryGetItem(itemId, out ItemDefinition item) &&
                !string.IsNullOrEmpty(item.DisplayNameKo))
            {
                return item.DisplayNameKo;
            }

            return itemId;
        }

        #endregion
    }
}
