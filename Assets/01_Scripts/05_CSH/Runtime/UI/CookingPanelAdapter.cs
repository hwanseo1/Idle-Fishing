using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 요리 패널에서 레시피/재료/시간 정보를 표시하고 변경 요청을 서버로 보냅니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CookingPanelAdapter : MonoBehaviour
    {
        #region Inspector References

        [SerializeField] private FisherRuntimeContext _context;
        [SerializeField] private GameObject _panel;
        [SerializeField] private FisherPanelView _view;

        [Header("Art Contract")]
        [Tooltip("팀장이 요리 재료/결과물 슬롯과 버튼 스프라이트를 연결하는 공통 UI 아트 프로필입니다.")]
        [SerializeField] private FisherUiArtProfile _artProfile;

        [Header("Server Authority")]
        [Tooltip("StartCooking/ClaimCooking/CancelCooking 서버 mutation을 사용할지 결정합니다.")]
        [SerializeField] private bool _enableServerCookingGatewayRequests = true;
        [Tooltip("SpeedupCooking 서버 mutation을 사용할지 결정합니다. 실패 응답은 UI 잠금을 복구합니다.")]
        [SerializeField] private bool _enableServerCookingSpeedupRequests = true;
        [Tooltip("CloudScript 응답이 돌아오지 않을 때 CSH 버튼 잠금을 복구하는 시간입니다.")]
        [SerializeField] private float _serverCookingRequestTimeoutSeconds = 12f;

        #endregion

        #region View State

        private const string SpeedupItemId = "ticket_speedup_10m";
        private const string ServerCookingBusyMessage = "서버 요리 요청 처리 중";
        private const string ServerCookingBusyLabel = "요청 중";
        private const string ServerCookingReadyLabel = "서버 요청 준비";
        private const int PreparedQueueCellCount = 3;
        private const int ServerCookingQueueCap = 99;
        private const bool ServerCookingQueueCap99Verified = true;
        private static readonly bool CookingMutationDiagnosticsEnabled = true;
        private TextMeshProUGUI _template;
        private string _lastMessage = "준비";
        private string _selectedRecipeId;
        private int _selectedCount = 1;
        private int _selectedQueueSlotIndex = -1;
        private string _selectedQueueRecipeId = string.Empty;
        private string _selectedQueueStartedAtUtc = string.Empty;
        private long _selectedQueueStartedUtcTicks;
        private bool _showCancelConfirm;
        private bool _showCookQuantitySheet;
        private bool _showRecipeList;
        private readonly FisherServerRequestGate _cookingRequest = new FisherServerRequestGate();
        private float _nextAutoRefreshTime;
        private bool _hasPulledInitialPlayFabCookingSnapshot;
        private float _suppressPlayFabDataStorePullUntil;

        #endregion

        #region Configuration

        /// <summary>
        /// 부트스트래퍼가 요리 패널과 Fisher 런타임 컨텍스트를 주입할 때 호출합니다.
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
            global::RuntimeStateEventBus.OnCookingStateEntered += Refresh;
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
                _context.RuntimeChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            global::RuntimeStateEventBus.OnCookingStateEntered -= Refresh;
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
            }

            _cookingRequest.Invalidate();
        }

        private void Update()
        {
            RecoverExpiredServerCookingRequest();

            if (_context == null || !_context.IsReady)
            {
                return;
            }

            if (_context.CookingService.ActiveCookingSlotCount == 0 ||
                _cookingRequest.IsBusy ||
                Time.unscaledTime < _nextAutoRefreshTime)
            {
                return;
            }

            _nextAutoRefreshTime = Time.unscaledTime + 1f;
            Refresh();
        }

        #endregion

        #region Context Binding

        private void SetContext(FisherRuntimeContext context)
        {
            if (!ReferenceEquals(_context, context))
            {
                _hasPulledInitialPlayFabCookingSnapshot = false;
                _suppressPlayFabDataStorePullUntil = 0f;
            }

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

        #region Player Sync

        private void PullCookingSnapshotFromPlayFabDataStore(bool force = false)
        {
            if (!force)
            {
                if (_cookingRequest.IsBusy ||
                    Time.unscaledTime < _suppressPlayFabDataStorePullUntil ||
                    _hasPulledInitialPlayFabCookingSnapshot)
                {
                    return;
                }
            }

            FisherPlayerDataBridge bridge = FisherPlayerDataBridgeResolver.Resolve(_context, this);
            if (bridge == null)
            {
                return;
            }

            if (bridge.PullDisplaySnapshotsFromPlayFabDataStore(notify: false) || force)
            {
                _hasPulledInitialPlayFabCookingSnapshot = true;
            }
        }

        private void SuppressPlayFabDataStorePull()
        {
            _suppressPlayFabDataStorePullUntil = Mathf.Max(
                _suppressPlayFabDataStorePullUntil,
                Time.unscaledTime + FisherServerMutationPolicy.SnapshotPullSuppressSeconds);
        }

        #endregion

        #region Rendering

        /// <summary>
        /// 선택된 레시피 카드와 현재 요리 상태를 다시 그립니다.
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

            PullCookingSnapshotFromPlayFabDataStore();
            EnsureSelectedRecipe();
            SyncSelectedQueueTarget();

            if (_showCancelConfirm)
            {
                if (TryRenderStaticCancelConfirm())
                {
                    return;
                }

                LogStaticViewUnavailable("cancel-confirm");
                return;
            }

            if (!_context.BuildResult.Catalog.TryGetRecipe(_selectedRecipeId, out RecipeDefinition selectedRecipe))
            {
                if (!TryRenderStaticStatus("레시피 없음"))
                {
                LogStaticViewUnavailable("missing-recipe");
                }
                return;
            }

            ClampSelectedCount(selectedRecipe);

            if (_showRecipeList)
            {
                if (TryRenderStaticRecipeSelector())
                {
                    return;
                }

                LogStaticViewUnavailable("recipe-selector");
                return;
            }

            if (TryRenderStaticSelectedRecipe(selectedRecipe))
            {
                return;
            }

            LogStaticViewUnavailable("selected-recipe");
        }

        private bool TryRenderStaticStatus(string status)
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return false;
            }

            HideLegacyRoot("Fisher_CookingRoot");
            SetText(view.TitleText, string.Empty);
            SetText(view.StatusText, string.Empty);
            SetText(view.SubStatusText, string.Empty);
            FisherRuntimeUi.ApplyHeaderStatusParchmentText(view);
            RenderCookingDetailInfoRows(view, null, 0);
            ClearAllSlots(view);
            view.SetCookingSectionsVisible(progress: false, recipes: false, ingredients: false);
            view.SetMainSectionsVisible(tabs: false, grid: false, detail: false, actions: false);
            view.HideActionSheets();
            return true;
        }

        private bool TryRenderStaticCancelConfirm()
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return false;
            }

            HideLegacyRoot("Fisher_CookingRoot");
            view.SetMainSectionsVisible(tabs: false, grid: true, detail: true, actions: true);
            view.SetCookingSectionsVisible(progress: true, recipes: false, ingredients: false);
            ClearAllSlots(view);
            RenderStaticHeader(view);
            RenderCookingProgressSlots(view);
            ActiveRecipeState active = null;
            bool hasQueueTarget = TryResolveSelectedQueueTarget(out active);
            RenderQueueTargetDetail(view, active, "취소 대상");
            view.HideActionSheets();
            if (view.ConfirmSheet != null)
            {
                bool canCancel = hasQueueTarget && !IsServerCookingStartBlocked();
                string cancelTitle = "요리 취소";
                if (hasQueueTarget)
                {
                    string recipeName = active.recipeId;
                    if (_context.BuildResult.Catalog.TryGetRecipe(active.recipeId, out RecipeDefinition recipe))
                    {
                        recipeName = RecipeTitle(recipe);
                    }

                    cancelTitle = recipeName + " 취소";
                }

                string confirmBody = hasQueueTarget
                    ? "이 요리를 취소하시겠습니까?"
                    : "선택된 요리 큐가 없습니다.";
                view.ConfirmSheet.ShowConfirm(
                    cancelTitle,
                    confirmBody,
                    "요리 취소",
                    canCancel,
                    canCancel ? () => RequestServerCookingCancel(active) : null,
                    () =>
                    {
                        _showCancelConfirm = false;
                        Refresh();
                    });
            }

            return true;
        }

        private bool TryRenderStaticRecipeSelector()
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return false;
            }

            HideLegacyRoot("Fisher_CookingRoot");
            view.SetMainSectionsVisible(tabs: false, grid: false, detail: false, actions: true);
            view.SetCookingSectionsVisible(progress: false, recipes: true, ingredients: false);
            ClearAllSlots(view);
            RenderStaticHeader(view);
            view.DetailSlot?.Clear();
            RenderCookingDetailInfoRows(view, null, 0);
            view.HideActionSheets();
            int index = 0;
            foreach (RecipeDefinition recipe in _context.BuildResult.Catalog.RecipesById.Values)
            {
                FisherSlotView slot = view.GetExistingRecipeSlot(index);
                BindRecipeSelectorSlot(slot, recipe);
                index++;
            }

            view.HideUnusedRecipeSlots(index);

            view.SetAction(view.PrimaryAction, "돌아가기", true, () =>
            {
                _showRecipeList = false;
                Refresh();
            });
            view.SetAction(view.SecondaryAction, "수량 " + _selectedCount, false, null);
            view.SetAction(view.TertiaryAction, "레시피 선택", false, null);
            view.SetAction(view.QuaternaryAction, "가속", false, null);
            return true;
        }

        private bool TryRenderStaticSelectedRecipe(RecipeDefinition recipe)
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return false;
            }

            HideLegacyRoot("Fisher_CookingRoot");
            view.SetMainSectionsVisible(tabs: false, grid: true, detail: true, actions: true);
            view.SetCookingSectionsVisible(progress: true, recipes: false, ingredients: true);
            ClearAllSlots(view);
            RenderStaticHeader(view);
            RenderCookingProgressSlots(view);
            BindIngredientOrResultSlots(view, recipe);
            view.HideUnusedIngredientSlots(3);
            RenderStaticRecipeDetail(view, recipe);
            RenderStaticCookingActions(view, recipe);
            RenderStaticCookQuantitySheet(view, recipe);
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
                    "CookingPanel",
                    nameof(CookingPanelAdapter),
                    FisherSlotLayout.CookingProgress,
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
            Debug.LogWarning("[CookingPanelAdapter] CookingPanel/ViewRoot 고정 View를 만들거나 찾지 못했습니다. " +
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

        private void RenderStaticHeader(FisherPanelView view)
        {
            SetText(view.TitleText, string.Empty);
            SetText(view.StatusText, string.Empty);
            SetText(view.SubStatusText, string.Empty);
            FisherRuntimeUi.ApplyHeaderStatusParchmentText(view);
        }

        private void BindIngredientOrResultSlots(FisherPanelView view, RecipeDefinition recipe)
        {
            int displayCount = DetailDisplayCount(recipe);
            FisherSlotView input1 = view.GetExistingIngredientSlot(0);
            input1?.Bind(
                DisplayName(recipe.InputItemId),
                CompactNumberFormatter.Format(CountOwned(recipe.InputItemId)) + "/" + CompactNumberFormatter.Format(recipe.InputCount * displayCount),
                "재료",
                "재료",
                FisherRuntimeUi.ResolveItemIcon(_artProfile, recipe.InputItemId, ItemCategory(recipe.InputItemId)),
                false,
                CountOwned(recipe.InputItemId) < recipe.InputCount * displayCount,
                false,
                false,
                null);

            FisherSlotView input2 = view.GetExistingIngredientSlot(1);
            input2?.Bind(
                DisplayName(recipe.InputItemId2),
                CompactNumberFormatter.Format(CountOwned(recipe.InputItemId2)) + "/" + CompactNumberFormatter.Format(recipe.InputCount2 * displayCount),
                "재료",
                "재료",
                FisherRuntimeUi.ResolveItemIcon(_artProfile, recipe.InputItemId2, ItemCategory(recipe.InputItemId2)),
                false,
                CountOwned(recipe.InputItemId2) < recipe.InputCount2 * displayCount,
                false,
                false,
                null);

            FisherSlotView output = view.GetExistingIngredientSlot(2);
            output?.Bind(
                DisplayName(recipe.OutputItemId),
                "x" + CompactNumberFormatter.Format(recipe.OutputCount * displayCount),
                "결과",
                "결과",
                ResolveRecipeOutputIcon(recipe),
                false,
                false,
                false,
                false,
                null);
        }

        private void RenderStaticRecipeDetail(FisherPanelView view, RecipeDefinition recipe)
        {
            if (view.DetailRoot != null)
            {
                view.DetailRoot.gameObject.SetActive(true);
            }

            int displayCount = DetailDisplayCount(recipe);
            SetText(view.DetailTitleText, RecipeTitle(recipe));
            SetText(view.DetailMetaText, "재료: " + IngredientText(recipe, CountOwned(recipe.InputItemId), CountOwned(recipe.InputItemId2)));
            SetText(view.DetailBodyText, "선택 수량 " + CompactNumberFormatter.FormatCount(displayCount));
            RenderCookingDetailInfoRows(view, recipe, displayCount);
            FisherRuntimeUi.ApplyDetailParchmentText(view);
            view.DetailSlot?.Bind(
                string.Empty,
                "x" + CompactNumberFormatter.Format(recipe.OutputCount * displayCount),
                string.Empty,
                "결",
                ResolveRecipeOutputIcon(recipe),
                false,
                false,
                false,
                false,
                null);
        }

        private void RenderCookingDetailInfoRows(FisherPanelView view, RecipeDefinition recipe, int count)
        {
            if (view == null)
            {
                return;
            }

            bool hasRecipe = recipe != null;
            SetCookingDetailInfoRowsActive(view, hasRecipe);
            if (!hasRecipe)
            {
                SetText(view.CookingRewardExpText, string.Empty);
                SetText(view.CookingSellPriceText, string.Empty);
                return;
            }

            int displayCount = Mathf.Clamp(count <= 0 ? 1 : count, 1, ServerCookingQueueCap);
            long rewardExp = SafeMultiplyForDisplay(Mathf.Max(0, recipe.CrewExp), displayCount);
            SetText(view.CookingRewardExpText, "완료 EXP +" + CompactNumberFormatter.Format(rewardExp));
            SetText(view.CookingSellPriceText, BuildCookingSellPriceText(recipe, displayCount));
        }

        private static void SetCookingDetailInfoRowsActive(FisherPanelView view, bool active)
        {
            if (view == null)
            {
                return;
            }

            SetRectActive(view.CookingDetailInfoRows, active);
            SetRectActive(view.CookingRewardExpPanel, active);
            SetRectActive(view.CookingSellPricePanel, active);
        }

        private static void SetRectActive(RectTransform rect, bool active)
        {
            if (rect != null)
            {
                rect.gameObject.SetActive(active);
            }
        }

        private string BuildCookingSellPriceText(RecipeDefinition recipe, int count)
        {
            if (recipe == null ||
                _context == null ||
                _context.BuildResult == null ||
                _context.BuildResult.Catalog == null ||
                !_context.BuildResult.Catalog.TryGetItem(recipe.OutputItemId, out ItemDefinition outputItem) ||
                !ItemSellPolicy.IsSellable(outputItem))
            {
                return "판매 불가";
            }

            return "개당 " + CompactNumberFormatter.FormatGold(outputItem.SellPrice);
        }

        private static long SafeMultiplyForDisplay(long value, long multiplier)
        {
            try
            {
                return checked(value * multiplier);
            }
            catch (OverflowException)
            {
                return long.MaxValue;
            }
        }

        private void RenderStaticCookingActions(FisherPanelView view, RecipeDefinition recipe)
        {
            bool hasQueueTarget = TryResolveSelectedQueueTarget(out ActiveRecipeState queueActive);
            if (hasQueueTarget)
            {
                bool canAppendToQueue = CanAppendToSelectedQueue(recipe, queueActive);
                string appendLabel = string.Equals(recipe.RecipeId, queueActive.recipeId, StringComparison.Ordinal)
                    ? "요리 추가"
                    : "동일 요리 필요";
                view.SetAction(
                    view.PrimaryAction,
                    appendLabel,
                    canAppendToQueue,
                    canAppendToQueue ? () => QueueSelectedRecipe(recipe) : null);
            }
            else
            {
                view.SetAction(view.PrimaryAction, "레시피 변경", true, () =>
                {
                    _showRecipeList = true;
                    _showCookQuantitySheet = false;
                    Refresh();
                });
            }

            bool canEditQuantity = !_cookingRequest.IsBusy;
            view.SetAction(view.SecondaryAction, "수량 " + _selectedCount, canEditQuantity, () =>
            {
                _showCookQuantitySheet = true;
                Refresh();
            });

            if (!hasQueueTarget)
            {
                string queueLabel = ActiveRecipeFor(recipe.RecipeId) == null ? "요리 시작" : "요리 추가";
                bool canQueue = ServerCookingQueueCap99Verified && CanRequestQueue(recipe) && !IsServerCookingStartBlocked();
                view.SetAction(view.TertiaryAction, queueLabel, canQueue, () => QueueSelectedRecipe(recipe));
                view.SetAction(view.QuaternaryAction, "큐 선택 필요", false, null);
                return;
            }

            bool isReady = IsCookingReady(queueActive);
            if (isReady)
            {
                view.SetAction(
                    view.TertiaryAction,
                    "수령",
                    !IsServerCookingStartBlocked(),
                    () => RequestServerCookingClaim(queueActive));
            }
            else
            {
                int owned = _context == null || _context.InventoryService == null
                    ? 0
                    : _context.InventoryService.CountItem(SpeedupItemId);
                bool canSpeedup = !isReady && owned > 0 && ResolveSpeedupSeconds() > 0 && !IsServerCookingSpeedupBlocked();
                view.SetAction(
                    view.TertiaryAction,
                    "가속 " + CompactNumberFormatter.Format(owned),
                    canSpeedup,
                    canSpeedup ? () => RequestServerCookingSpeedup(queueActive) : null);
            }

            view.SetAction(view.QuaternaryAction, "요리 취소", !IsServerCookingStartBlocked(), () =>
            {
                _showCancelConfirm = true;
                _showCookQuantitySheet = false;
                Refresh();
            });
        }

        private void RenderStaticCookQuantitySheet(FisherPanelView view, RecipeDefinition recipe)
        {
            view.HideActionSheets();
            if (!_showCookQuantitySheet || view.QuantitySheet == null)
            {
                return;
            }

            if (view.ActionSheetsRoot != null)
            {
                view.ActionSheetsRoot.gameObject.SetActive(true);
            }

            view.QuantitySheet.transform.SetAsLastSibling();
            view.QuantitySheet.ShowQuantity(
                RecipeTitle(recipe) + " 요리 수량",
                "최대 " + CompactNumberFormatter.FormatCount(MaxCookCount(recipe)) + " / 선택 " + CompactNumberFormatter.FormatCount(_selectedCount),
                _selectedCount,
                MaxCookCount(recipe),
                count =>
                {
                    _selectedCount = count;
                    ClampSelectedCount(recipe);
                    Refresh();
                },
                "적용",
                () =>
                {
                    _showCookQuantitySheet = false;
                    Refresh();
                },
                () =>
                {
                    _showCookQuantitySheet = false;
                    Refresh();
                });
        }

        private void RenderCookingProgressSlots(FisherPanelView view)
        {
            if (view == null || _context == null || _context.CookingService == null)
            {
                return;
            }

            for (int i = 0; i < PreparedQueueCellCount; i++)
            {
                FisherSlotView slot = view.GetExistingSlot(i, "CookingSlot");
                ActiveRecipeState active = ActiveRecipeAtSlot(i);
                if (active == null)
                {
                    slot?.Bind(
                        "빈 슬롯",
                        string.Empty,
                        "대기",
                        (i + 1).ToString(CultureInfo.InvariantCulture),
                        null,
                        false,
                        true,
                        false,
                        false,
                        () =>
                        {
                            ClearQueueSelection();
                            Refresh();
                        });
                    continue;
                }

                string recipeName = active.recipeId;
                Sprite icon = null;
                if (_context.BuildResult.Catalog.TryGetRecipe(active.recipeId, out RecipeDefinition recipe))
                {
                    recipeName = RecipeTitle(recipe);
                    icon = ResolveRecipeOutputIcon(recipe);
                }

                bool ready = IsCookingReady(active);
                int queuedCount = Mathf.Max(1, active.queuedCount);
                slot?.Bind(
                    recipeName,
                    "x" + queuedCount,
                    ready ? "수령 가능" : RemainingSeconds(active) + "초",
                    ready ? "완" : "진",
                    icon,
                    QueueSelectionMatches(active),
                    false,
                    false,
                    ready,
                    () =>
                    {
                        if (QueueSelectionMatches(active))
                        {
                            ClearQueueSelection();
                        }
                        else
                        {
                            SelectQueueTarget(active);
                        }

                        _showRecipeList = false;
                        _showCookQuantitySheet = false;
                        Refresh();
                    });
            }

            view.HideUnusedSlots(PreparedQueueCellCount);
        }

        private void BindRecipeSelectorSlot(FisherSlotView slot, RecipeDefinition recipe)
        {
            int owned1 = CountOwned(recipe.InputItemId);
            int owned2 = CountOwned(recipe.InputItemId2);
            bool enabled = recipe.IsEnabled;
            bool canCook = enabled && owned1 >= recipe.InputCount && owned2 >= recipe.InputCount2;
            string recipeId = recipe.RecipeId;
            slot?.Bind(
                RecipeTitle(recipe),
                recipe.DurationSec + "초",
                enabled ? (canCook ? "가능" : "재료 부족") : "비활성",
                recipe.RecipeId == _selectedRecipeId ? "선" : enabled ? "요" : "잠",
                ResolveRecipeOutputIcon(recipe),
                recipe.RecipeId == _selectedRecipeId,
                !canCook,
                !enabled,
                false,
                enabled ? () =>
                {
                    _selectedRecipeId = recipeId;
                    _selectedCount = 1;
                    _showRecipeList = false;
                    _showCookQuantitySheet = false;
                    _lastMessage = "레시피 선택";
                    Refresh();
                } : null);
        }

        private void ClearAllSlots(FisherPanelView view)
        {
            if (view == null)
            {
                return;
            }

            ClearSlots(view.Slots);
            ClearSlots(view.RecipeSlots);
            ClearSlots(view.IngredientSlots);
        }

        private static void ClearSlots(IReadOnlyList<FisherSlotView> slots)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i]?.Clear();
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

        private void SetCookCountFromInput(RecipeDefinition recipe, string raw)
        {
            if (!int.TryParse(raw, out int parsed))
            {
                parsed = 1;
            }

            _selectedCount = parsed;
            ClampSelectedCount(recipe);
            Refresh();
        }

        private void QueueSelectedRecipe(RecipeDefinition recipe)
        {
            ClampSelectedCount(recipe);
            RequestServerCookingStart(recipe, _selectedCount);
        }

        private void RequestServerCookingStart(RecipeDefinition recipe, int count)
        {
            if (recipe == null)
            {
                return;
            }

            int safeCount = Mathf.Clamp(count, 1, ServerCookingQueueCap);
            int slotIndex = ResolveCookingSlotIndex(recipe);
            if (slotIndex < 0)
            {
                _lastMessage = "빈 요리 슬롯 없음";
                PullCookingSnapshotFromPlayFabDataStore();
                Refresh();
                return;
            }

            if (!TryBeginServerCookingRequest("StartCooking"))
            {
                Refresh();
                return;
            }

            int requestToken = _cookingRequest.Token;
            FisherPlayerDataBridge bridge = CookingBridge();
            if (bridge == null ||
                !bridge.TryRequestCookingStart(
                    slotIndex,
                    recipe.RecipeId,
                    safeCount,
                    () =>
                    {
                        CompleteServerCookingRequest(requestToken, "요리 요청 완료");
                    },
                    message => HandleServerCookingStartRejected(requestToken, message, recipe, safeCount)))
            {
                AbortServerCookingRequest(requestToken, "StartCooking 요청 실패");
                return;
            }

            _selectedCount = 1;
            _showCookQuantitySheet = false;
            _showCancelConfirm = false;
            _lastMessage = "요리 요청 중";
            Refresh();
        }

        private void RequestServerCookingClaim(ActiveRecipeState active)
        {
            if (!TryResolveCurrentActiveRecipe(active, out ActiveRecipeState currentActive))
            {
                return;
            }

            if (!TryBeginServerCookingRequest("ClaimCooking"))
            {
                return;
            }

            int requestToken = _cookingRequest.Token;
            FisherPlayerDataBridge bridge = CookingBridge();
            LogCookingMutationRequest("ClaimCooking", currentActive);
            string claimName = currentActive.recipeId;
            Sprite claimIcon = null;
            RecipeDefinition claimRecipe = null;
            if (_context.BuildResult.Catalog.TryGetRecipe(currentActive.recipeId, out claimRecipe))
            {
                claimName = RecipeTitle(claimRecipe);
                claimIcon = ResolveRecipeOutputIcon(claimRecipe);
            }

            int fallbackClaimCount = EstimateClaimableCount(currentActive, claimRecipe);
            if (bridge == null ||
                !bridge.TryRequestCookingClaim(
                    Mathf.Max(0, currentActive.slotIndex),
                    currentActive.recipeId,
                    currentActive.startedUtcTicks,
                    () =>
                    {
                        if (TryCompleteServerCookingRequest(requestToken, "요리 완료 갱신"))
                        {
                            int claimCount = bridge.LastCookingClaimedCookCount > 0
                                ? bridge.LastCookingClaimedCookCount
                                : fallbackClaimCount;
                            claimCount = Mathf.Clamp(claimCount, 1, CookingService.MaxQueueCount);
                            ShowCookingResultToast(
                                claimIcon,
                                "요리 완료",
                                claimName + " x" + CompactNumberFormatter.Format(claimCount));
                        }
                    },
                    message => HandleServerCookingClaimRejected(requestToken, message, currentActive)))
            {
                AbortServerCookingRequest(requestToken, "ClaimCooking 요청 실패");
                return;
            }

            _lastMessage = "완료 요청 중";
        }

        private void RequestServerCookingCancel(ActiveRecipeState active)
        {
            if (!TryResolveCurrentActiveRecipe(active, out ActiveRecipeState currentActive))
            {
                return;
            }

            if (!TryBeginServerCookingRequest("CancelCooking"))
            {
                Refresh();
                return;
            }

            int requestToken = _cookingRequest.Token;
            FisherPlayerDataBridge bridge = CookingBridge();
            LogCookingMutationRequest("CancelCooking", currentActive);
            if (bridge == null ||
                !bridge.TryRequestCookingCancel(
                    Mathf.Max(0, currentActive.slotIndex),
                    currentActive.recipeId,
                    currentActive.startedUtcTicks,
                    () =>
                    {
                        CompleteServerCookingRequest(requestToken, "요리 취소 갱신");
                    },
                    message => HandleServerCookingCancelRejected(requestToken, message, currentActive)))
            {
                AbortServerCookingRequest(requestToken, "CancelCooking 요청 실패");
                return;
            }

            _showCancelConfirm = false;
            _lastMessage = "취소 요청 중";
            Refresh();
        }

        private void RequestServerCookingSpeedup(ActiveRecipeState active)
        {
            if (!TryResolveCurrentActiveRecipe(active, out ActiveRecipeState currentActive))
            {
                return;
            }

            int seconds = ResolveSpeedupSeconds();
            if (seconds <= 0)
            {
                _lastMessage = "가속 시간 설정 오류";
                Refresh();
                return;
            }

            if (!TryBeginServerCookingRequest("SpeedupCooking"))
            {
                Refresh();
                return;
            }

            int requestToken = _cookingRequest.Token;
            FisherPlayerDataBridge bridge = CookingBridge();
            LogCookingMutationRequest("SpeedupCooking", currentActive, seconds);
            if (bridge == null ||
                !bridge.TryRequestCookingSpeedup(
                    Mathf.Max(0, currentActive.slotIndex),
                    SpeedupItemId,
                    seconds,
                    currentActive.recipeId,
                    currentActive.startedUtcTicks,
                    () => CompleteServerCookingRequest(requestToken, "요리 가속 갱신"),
                    message => HandleServerCookingSpeedupRejected(requestToken, message, currentActive, seconds)))
            {
                AbortServerCookingRequest(requestToken, "SpeedupCooking 요청 실패");
                return;
            }

            _lastMessage = "가속 요청 중";
            Refresh();
        }

        private void HandleServerCookingStartRejected(int requestToken, string message, RecipeDefinition recipe, int count)
        {
            AbortServerCookingRequest(requestToken, message);
        }

        private void HandleServerCookingClaimRejected(int requestToken, string message, ActiveRecipeState active)
        {
            AbortServerCookingRequest(requestToken, message);
        }

        private void HandleServerCookingCancelRejected(int requestToken, string message, ActiveRecipeState active)
        {
            AbortServerCookingRequest(requestToken, message);
        }

        private void HandleServerCookingSpeedupRejected(int requestToken, string message, ActiveRecipeState active, int seconds)
        {
            AbortServerCookingRequest(requestToken, message);
        }

        private bool TryBeginServerCookingRequest(string requestName)
        {
            if (!_cookingRequest.TryBegin(requestName))
            {
                return false;
            }

            _lastMessage = "서버 요청 중";
            return true;
        }

        private void CompleteServerCookingRequest(int requestToken, string message, bool pullSnapshot = false)
        {
            TryCompleteServerCookingRequest(requestToken, message, pullSnapshot);
        }

        private bool TryCompleteServerCookingRequest(int requestToken, string message, bool pullSnapshot = false)
        {
            if (!_cookingRequest.TryComplete(requestToken))
            {
                return false;
            }

            _lastMessage = string.IsNullOrWhiteSpace(message) ? "서버 갱신 완료" : message;
            SuppressPlayFabDataStorePull();
            if (pullSnapshot)
            {
                PullCookingSnapshotFromPlayFabDataStore(force: true);
            }

            _context?.NotifyRuntimeChanged();
            Refresh();
            return true;
        }

        private void ShowCookingResultToast(Sprite icon, string title, string meta)
        {
            FisherRuntimeUi.ShowCookingCompletePanel(this, _panel == null ? null : _panel.transform, icon, title, meta);
        }

        private void AbortServerCookingRequest(string message)
        {
            int requestToken = _cookingRequest.Token;
            AbortServerCookingRequest(requestToken, message, pullSnapshot: false);
        }

        private void AbortServerCookingRequest(int requestToken, string message)
        {
            AbortServerCookingRequest(requestToken, message, pullSnapshot: false);
        }

        private void AbortServerCookingRequest(int requestToken, string message, bool pullSnapshot)
        {
            if (!_cookingRequest.TryAbort(requestToken))
            {
                return;
            }

            _lastMessage = string.IsNullOrWhiteSpace(message) ? "서버 요청 실패" : message;
            Debug.LogWarning("[CookingPanelAdapter] " + _lastMessage);
            SuppressPlayFabDataStorePull();
            if (pullSnapshot)
            {
                PullCookingSnapshotFromPlayFabDataStore(force: true);
            }

            Refresh();
        }

        private bool TryResolveCurrentActiveRecipe(ActiveRecipeState requestedActive, out ActiveRecipeState currentActive)
        {
            currentActive = null;
            if (requestedActive == null || _context == null || _context.CookingService == null)
            {
                return false;
            }

            IReadOnlyList<ActiveRecipeState> activeRecipes = _context.CookingService.ActiveRecipeStates;
            for (int i = 0; i < activeRecipes.Count; i++)
            {
                ActiveRecipeState active = activeRecipes[i];
                if (active == null)
                {
                    continue;
                }

                if (active.slotIndex == requestedActive.slotIndex &&
                    active.recipeId == requestedActive.recipeId &&
                    active.startedUtcTicks == requestedActive.startedUtcTicks)
                {
                    currentActive = active;
                    return true;
                }
            }

            ClearQueueSelection();
            _showCancelConfirm = false;
            _lastMessage = "요리 상태 갱신";
            Refresh();
            return false;
        }

        private void LogCookingMutationRequest(string operation, ActiveRecipeState active, int seconds = 0)
        {
            if (!CookingMutationDiagnosticsEnabled)
            {
                return;
            }

            string speedup = seconds > 0 ? ", seconds=" + seconds : string.Empty;
            Debug.Log("[FISHER_COOK_DIAG][Panel] " + operation +
                      " request active=" + FormatActiveRecipeForDiagnostics(active) +
                      speedup +
                      ", allActive=" + ActiveRecipesForDiagnostics());
        }

        private string ActiveRecipesForDiagnostics()
        {
            if (_context == null || _context.CookingService == null)
            {
                return "context-or-service-null";
            }

            IReadOnlyList<ActiveRecipeState> activeRecipes = _context.CookingService.ActiveRecipeStates;
            if (activeRecipes == null || activeRecipes.Count == 0)
            {
                return "[]";
            }

            List<string> parts = new List<string>(activeRecipes.Count);
            for (int i = 0; i < activeRecipes.Count; i++)
            {
                parts.Add(FormatActiveRecipeForDiagnostics(activeRecipes[i]));
            }

            return "[" + string.Join("; ", parts) + "]";
        }

        private static string FormatActiveRecipeForDiagnostics(ActiveRecipeState active)
        {
            if (active == null)
            {
                return "null";
            }

            return "{slot=" + active.slotIndex +
                   ", recipe=" + (active.recipeId ?? string.Empty) +
                   ", startedTicks=" + active.startedUtcTicks +
                   ", startedUtc=" + FormatTicksForDiagnostics(active.startedUtcTicks) +
                   ", completesTicks=" + active.completesUtcTicks +
                   ", completesUtc=" + FormatTicksForDiagnostics(active.completesUtcTicks) +
                   ", queued=" + active.queuedCount + "}";
        }

        private static string FormatTicksForDiagnostics(long ticks)
        {
            if (ticks <= 0L)
            {
                return "none";
            }

            try
            {
                DateTime utc = DateTime.SpecifyKind(new DateTime(ticks), DateTimeKind.Utc);
                return utc.ToString("o", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return "invalid(" + ticks + ")";
            }
        }

        private void RecoverExpiredServerCookingRequest()
        {
            if (!_cookingRequest.TryRecoverTimeout(
                    _serverCookingRequestTimeoutSeconds,
                    "Cooking",
                    out string requestName))
            {
                return;
            }

            _lastMessage = "서버 응답 지연";
            Debug.LogWarning("[CookingPanelAdapter] " + requestName + " request timed out; UI lock recovered.");
            SuppressPlayFabDataStorePull();
            Refresh();
        }

        private FisherPlayerDataBridge CookingBridge()
        {
            return FisherPlayerDataBridgeResolver.Resolve(_context, this);
        }

        #endregion

        #region Active Recipe

        private int RemainingSeconds()
        {
            ActiveRecipeState active = FirstActiveRecipe();
            return RemainingSeconds(active);
        }

        private int RemainingSeconds(ActiveRecipeState active)
        {
            if (_context == null || _context.State == null || active == null)
            {
                return 0;
            }

            long ticks = active.completesUtcTicks - _context.Clock.UtcNow.Ticks;
            return Mathf.Max(0, (int)System.TimeSpan.FromTicks(ticks).TotalSeconds);
        }

        #endregion

        #region Recipe Helpers

        private bool IsServerCookingStartBlocked()
        {
            if (_cookingRequest.IsBusy)
            {
                return true;
            }

            if (!_enableServerCookingGatewayRequests)
            {
                return false;
            }

            return false;
        }

        private bool IsServerCookingSpeedupBlocked()
        {
            if (_cookingRequest.IsBusy)
            {
                return true;
            }

            if (!_enableServerCookingSpeedupRequests || !_enableServerCookingGatewayRequests)
            {
                return false;
            }

            return false;
        }

        private int ResolveSpeedupSeconds()
        {
            return ReadEconomyInt("speedup_ticket_seconds", (int)FisherEconomyDefaults.SpeedupTicketSeconds);
        }

        private string CurrentStatusMessage()
        {
            if (_cookingRequest.IsBusy)
            {
                return _cookingRequest.CurrentMessage("서버 요리 요청 중");
            }

            return _lastMessage;
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
                !_context.BuildResult.Catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param) ||
                param == null ||
                !param.IsEnabled ||
                !long.TryParse(param.Value, out long value))
            {
                return resolvedFallback;
            }

            return value;
        }

        private void EnsureSelectedRecipe()
        {
            if (!string.IsNullOrEmpty(_selectedRecipeId) &&
                _context.BuildResult.Catalog.TryGetRecipe(_selectedRecipeId, out RecipeDefinition selected) &&
                selected.IsEnabled)
            {
                return;
            }

            foreach (RecipeDefinition recipe in _context.BuildResult.Catalog.RecipesById.Values)
            {
                if (recipe.IsEnabled)
                {
                    _selectedRecipeId = recipe.RecipeId;
                    return;
                }
            }

            _selectedRecipeId = string.Empty;
        }

        private int CountOwned(string itemId)
        {
            return string.IsNullOrEmpty(itemId) ? 0 : _context.InventoryService.CountItem(itemId);
        }

        private bool CanQueue(RecipeDefinition recipe, int owned1, int owned2)
        {
            if (recipe == null)
            {
                return false;
            }

            ActiveRecipeState active = ActiveRecipeFor(recipe.RecipeId);
            if (active == null && !TryResolveAvailableCookingSlotIndex(out _))
            {
                return false;
            }

            int currentQueue = active == null ? 0 : Mathf.Max(1, active.queuedCount);
            int count = Mathf.Clamp(_selectedCount, 1, ServerCookingQueueCap);
            return currentQueue + count <= ServerCookingQueueCap &&
                   owned1 >= recipe.InputCount * count &&
                   owned2 >= recipe.InputCount2 * count;
        }

        private bool CanRequestQueue(RecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return false;
            }

            return CanQueue(recipe, CountOwned(recipe.InputItemId), CountOwned(recipe.InputItemId2));
        }

        private bool CanAppendToSelectedQueue(RecipeDefinition recipe, ActiveRecipeState queueActive)
        {
            return recipe != null &&
                   queueActive != null &&
                   ServerCookingQueueCap99Verified &&
                   string.Equals(recipe.RecipeId, queueActive.recipeId, StringComparison.Ordinal) &&
                   CanRequestQueue(recipe) &&
                   !IsServerCookingStartBlocked();
        }

        private bool ActiveRecipeMatches(RecipeDefinition recipe)
        {
            return recipe != null && ActiveRecipeFor(recipe.RecipeId) != null;
        }

        private void ClampSelectedCount(RecipeDefinition recipe)
        {
            _selectedCount = Mathf.Clamp(_selectedCount <= 0 ? 1 : _selectedCount, 1, MaxCookCount(recipe));
        }

        private int MaxCookCount(RecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return 1;
            }

            int maxByFirst = recipe.InputCount <= 0 ? 1 : CountOwned(recipe.InputItemId) / recipe.InputCount;
            int maxBySecond = recipe.InputCount2 <= 0 ? 1 : CountOwned(recipe.InputItemId2) / recipe.InputCount2;
            int maxByItems = Mathf.Max(1, Mathf.Min(maxByFirst, maxBySecond));
            if (ActiveRecipeFor(recipe.RecipeId) == null && !TryResolveAvailableCookingSlotIndex(out _))
            {
                return 1;
            }

            ActiveRecipeState active = ActiveRecipeFor(recipe.RecipeId);
            int currentQueue = active == null ? 0 : Mathf.Max(1, active.queuedCount);
            int remainingQueue = Mathf.Max(0, ServerCookingQueueCap - currentQueue);
            int maxByQueue = active == null ? ServerCookingQueueCap : remainingQueue;
            return Mathf.Clamp(Mathf.Min(maxByItems, maxByQueue), 1, ServerCookingQueueCap);
        }

        private int DetailDisplayCount(RecipeDefinition recipe)
        {
            return Mathf.Clamp(_selectedCount <= 0 ? 1 : _selectedCount, 1, ServerCookingQueueCap);
        }

        private void SelectQueueTarget(ActiveRecipeState active)
        {
            if (active == null || string.IsNullOrEmpty(active.recipeId) || active.startedUtcTicks <= 0L)
            {
                ClearQueueSelection();
                return;
            }

            _selectedQueueSlotIndex = Mathf.Max(0, active.slotIndex);
            _selectedQueueRecipeId = active.recipeId ?? string.Empty;
            _selectedQueueStartedUtcTicks = active.startedUtcTicks;
            _selectedQueueStartedAtUtc = FormatTicksForDiagnostics(active.startedUtcTicks);
        }

        private void ClearQueueSelection()
        {
            _selectedQueueSlotIndex = -1;
            _selectedQueueRecipeId = string.Empty;
            _selectedQueueStartedAtUtc = string.Empty;
            _selectedQueueStartedUtcTicks = 0L;
            _showCancelConfirm = false;
        }

        private void SyncSelectedQueueTarget()
        {
            if (!HasSelectedQueueIdentity())
            {
                return;
            }

            if (TryResolveSelectedQueueTarget(out _))
            {
                return;
            }

            ClearQueueSelection();
        }

        private bool HasSelectedQueueIdentity()
        {
            return _selectedQueueSlotIndex >= 0 &&
                   !string.IsNullOrEmpty(_selectedQueueRecipeId) &&
                   !string.IsNullOrEmpty(_selectedQueueStartedAtUtc) &&
                   _selectedQueueStartedUtcTicks > 0L;
        }

        private bool TryResolveSelectedQueueTarget(out ActiveRecipeState active)
        {
            active = null;
            if (!HasSelectedQueueIdentity() || _context == null || _context.CookingService == null)
            {
                return false;
            }

            IReadOnlyList<ActiveRecipeState> activeRecipes = _context.CookingService.ActiveRecipeStates;
            for (int i = 0; i < activeRecipes.Count; i++)
            {
                ActiveRecipeState candidate = activeRecipes[i];
                if (QueueSelectionMatches(candidate))
                {
                    active = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool QueueSelectionMatches(ActiveRecipeState active)
        {
            return HasSelectedQueueIdentity() &&
                   active != null &&
                   active.slotIndex == _selectedQueueSlotIndex &&
                   string.Equals(active.recipeId, _selectedQueueRecipeId, StringComparison.Ordinal) &&
                   active.startedUtcTicks == _selectedQueueStartedUtcTicks;
        }

        private void RenderQueueTargetDetail(FisherPanelView view, ActiveRecipeState active, string title)
        {
            if (view == null)
            {
                return;
            }

            if (view.DetailRoot != null)
            {
                view.DetailRoot.gameObject.SetActive(true);
            }

            Sprite icon = null;
            RecipeDefinition targetRecipe = null;
            int targetCount = 0;
            if (active != null && _context.BuildResult.Catalog.TryGetRecipe(active.recipeId, out RecipeDefinition recipe))
            {
                targetRecipe = recipe;
                targetCount = Mathf.Max(1, active.queuedCount);
                icon = ResolveRecipeOutputIcon(recipe);
            }

            SetText(view.DetailTitleText, title);
            SetText(view.DetailMetaText, active == null ? "선택된 요리 큐 없음" : BuildQueueTargetText(active));
            SetText(view.DetailBodyText, active == null ? "오른쪽 요리 큐에서 진행 중인 요리를 선택하세요." : "선택한 큐에만 요청합니다.");
            RenderCookingDetailInfoRows(view, targetRecipe, targetCount);
            view.DetailSlot?.Bind(
                string.Empty,
                active == null ? string.Empty : "x" + CompactNumberFormatter.Format(Mathf.Max(1, active.queuedCount)),
                string.Empty,
                active == null ? string.Empty : "큐",
                icon,
                false,
                active == null,
                false,
                false,
                null);
        }

        private string BuildQueueTargetText(ActiveRecipeState active)
        {
            if (active == null)
            {
                return "선택된 요리 큐 없음";
            }

            string recipeName = active.recipeId;
            if (_context.BuildResult.Catalog.TryGetRecipe(active.recipeId, out RecipeDefinition recipe))
            {
                recipeName = RecipeTitle(recipe);
            }

            return "슬롯 " + (Mathf.Max(0, active.slotIndex) + 1) +
                   " / " + recipeName +
                   " x" + CompactNumberFormatter.Format(Mathf.Max(1, active.queuedCount)) +
                   " / 시작 " + FormatTicksForDiagnostics(active.startedUtcTicks);
        }

        private ActiveRecipeState FirstActiveRecipe()
        {
            if (_context == null || _context.CookingService == null)
            {
                return null;
            }

            IReadOnlyList<ActiveRecipeState> activeRecipes = _context.CookingService.ActiveRecipeStates;
            for (int i = 0; i < activeRecipes.Count; i++)
            {
                ActiveRecipeState active = activeRecipes[i];
                if (active != null)
                {
                    return active;
                }
            }

            return null;
        }

        private ActiveRecipeState ActiveRecipeAtSlot(int slotIndex)
        {
            if (_context == null || _context.CookingService == null || slotIndex < 0)
            {
                return null;
            }

            IReadOnlyList<ActiveRecipeState> activeRecipes = _context.CookingService.ActiveRecipeStates;
            for (int i = 0; i < activeRecipes.Count; i++)
            {
                ActiveRecipeState active = activeRecipes[i];
                if (active != null && active.slotIndex == slotIndex)
                {
                    return active;
                }
            }

            return null;
        }

        private ActiveRecipeState FirstReadyRecipe()
        {
            if (_context == null || _context.CookingService == null || _context.Clock == null)
            {
                return null;
            }

            long nowTicks = _context.Clock.UtcNow.Ticks;
            IReadOnlyList<ActiveRecipeState> activeRecipes = _context.CookingService.ActiveRecipeStates;
            for (int i = 0; i < activeRecipes.Count; i++)
            {
                ActiveRecipeState active = activeRecipes[i];
                if (active != null &&
                    active.completesUtcTicks <= nowTicks)
                {
                    return active;
                }
            }

            return null;
        }

        private bool IsCookingReady(ActiveRecipeState active)
        {
            return active != null &&
                   _context != null &&
                   _context.Clock != null &&
                   active.completesUtcTicks <= _context.Clock.UtcNow.Ticks;
        }

        private int EstimateClaimableCount(ActiveRecipeState active, RecipeDefinition recipe)
        {
            if (active == null)
            {
                return 1;
            }

            int queuedCount = Mathf.Clamp(active.queuedCount, 1, CookingService.MaxQueueCount);
            if (recipe == null || recipe.DurationSec <= 0)
            {
                return queuedCount;
            }

            long nowTicks = _context != null && _context.Clock != null
                ? _context.Clock.UtcNow.Ticks
                : DateTime.UtcNow.Ticks;
            if (nowTicks < active.completesUtcTicks)
            {
                return 1;
            }

            long durationTicks = TimeSpan.FromSeconds(recipe.DurationSec).Ticks;
            if (durationTicks <= 0L)
            {
                return queuedCount;
            }

            long completedCount = 1L + ((nowTicks - active.completesUtcTicks) / durationTicks);
            return Mathf.Clamp((int)Math.Min(completedCount, queuedCount), 1, queuedCount);
        }

        private ActiveRecipeState ActiveRecipeFor(string recipeId)
        {
            if (_context == null || _context.CookingService == null || string.IsNullOrEmpty(recipeId))
            {
                return null;
            }

            IReadOnlyList<ActiveRecipeState> activeRecipes = _context.CookingService.ActiveRecipeStates;
            for (int i = 0; i < activeRecipes.Count; i++)
            {
                ActiveRecipeState active = activeRecipes[i];
                if (active != null &&
                    IsPreparedQueueSlotIndex(active.slotIndex) &&
                    active.recipeId == recipeId)
                {
                    return active;
                }
            }

            return null;
        }

        private int ResolveCookingSlotIndex(RecipeDefinition recipe)
        {
            ActiveRecipeState active = recipe == null ? null : ActiveRecipeFor(recipe.RecipeId);
            if (active != null)
            {
                return Mathf.Max(0, active.slotIndex);
            }

            return TryResolveAvailableCookingSlotIndex(out int slotIndex) ? slotIndex : -1;
        }

        private bool TryResolveAvailableCookingSlotIndex(out int slotIndex)
        {
            slotIndex = -1;
            if (_context == null || _context.CookingService == null)
            {
                slotIndex = 0;
                return true;
            }

            IReadOnlyList<ActiveRecipeState> activeRecipes = _context.CookingService.ActiveRecipeStates;
            for (int candidateSlotIndex = 0; candidateSlotIndex < PreparedQueueCellCount; candidateSlotIndex++)
            {
                bool used = false;
                for (int i = 0; i < activeRecipes.Count; i++)
                {
                    ActiveRecipeState active = activeRecipes[i];
                    if (active != null &&
                        active.slotIndex == candidateSlotIndex)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    slotIndex = candidateSlotIndex;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        private static bool IsPreparedQueueSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < PreparedQueueCellCount;
        }

        private string RecipeTitle(RecipeDefinition recipe)
        {
            return DisplayName(recipe.OutputItemId);
        }

        private string DisplayName(string itemId)
        {
            if (_context.BuildResult.Catalog.TryGetItem(itemId, out ItemDefinition item) && !string.IsNullOrWhiteSpace(item.DisplayNameKo))
            {
                return item.DisplayNameKo;
            }

            return itemId;
        }

        private string ItemCategory(string itemId)
        {
            if (_context.BuildResult.Catalog.TryGetItem(itemId, out ItemDefinition item))
            {
                return item.Category;
            }

            return string.Empty;
        }

        private Sprite ResolveRecipeOutputIcon(RecipeDefinition recipe)
        {
            if (recipe == null)
            {
                return null;
            }

            Sprite outputIcon = FisherRuntimeUi.ResolveItemIcon(_artProfile, recipe.OutputItemId, "Food");
            if (outputIcon != null)
            {
                return outputIcon;
            }

            Sprite recipeIcon = FisherRuntimeUi.ResolveItemIcon(_artProfile, recipe.RecipeId, "Recipe");
            if (recipeIcon != null)
            {
                return recipeIcon;
            }

            Sprite firstInputIcon = FisherRuntimeUi.ResolveItemIcon(_artProfile, recipe.InputItemId, ItemCategory(recipe.InputItemId));
            if (firstInputIcon != null)
            {
                return firstInputIcon;
            }

            return FisherRuntimeUi.ResolveItemIcon(_artProfile, recipe.InputItemId2, ItemCategory(recipe.InputItemId2));
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

        private string IngredientText(RecipeDefinition recipe, int owned1, int owned2)
        {
            return DisplayName(recipe.InputItemId) + " " +
                   CompactNumberFormatter.Format(owned1) + "/" + CompactNumberFormatter.Format(recipe.InputCount) +
                   " + " +
                   DisplayName(recipe.InputItemId2) + " " +
                   CompactNumberFormatter.Format(owned2) + "/" + CompactNumberFormatter.Format(recipe.InputCount2);
        }

        #endregion
    }
}
