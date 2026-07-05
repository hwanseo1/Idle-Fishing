using System.Collections;
using System.Collections.Generic;
using Crew;
using JHS.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 도감 패널에서 자동 발견 상태와 보상 수령 흐름을 Fisher 서비스에 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CollectionPanelAdapter : MonoBehaviour
    {
        private const float ServerCollectionRewardResponseTimeoutSeconds = 12f;
        private const int RequiredCollectionSlotPoolSize = 30;
        private const string DefaultCollectionCategory = "Fish";
        private const string CrewFragmentPrefix = "fragment_";
        private static readonly string[] CollectionTabLabels = { "물고기", "요리", "선원", "배" };
        private static readonly string[] CollectionTabKeys = { "Fish", "Food", "Crew", "Ship" };
        private static readonly ExternalCollectionDefinition[] FallbackBoatEntries =
        {
            new ExternalCollectionDefinition("boat_vacuum", "진공흡입기", "Boat", 1000),
            new ExternalCollectionDefinition("boat_lamp", "야근전등", "Boat", 1010),
            new ExternalCollectionDefinition("boat_penguin", "펭귄", "Boat", 1020)
        };

        #region Inspector References

        [SerializeField] private FisherRuntimeContext _context;
        [SerializeField] private GameObject _panel;
        [SerializeField] private FisherPanelView _view;

        [Header("Art Contract")]
        [Tooltip("팀장이 도감 슬롯과 itemId 아이콘을 연결하는 공통 UI 아트 프로필입니다.")]
        [SerializeField] private FisherUiArtProfile _artProfile;

        #endregion

        #region View State

        private TextMeshProUGUI _template;
        private string _category = DefaultCollectionCategory;
        private string _lastMessage = "확인";
        private string _lastSlotCapacityWarningKey;
        private readonly FisherServerRequestGate _collectionRewardRequest = new FisherServerRequestGate();

        #endregion

        #region View Models

        private sealed class CollectionEntryView
        {
            public string ItemId;
            public string DisplayName;
            public string Category;
            public bool Discovered;
            public int AcquiredCount;
            public int RewardCount;
            public int ClaimedRewardCount;
            public int ClaimableRewardCount;
            public string ClaimableRewardId;
            public int SortOrder;
            public Sprite IconOverride;
        }

        private sealed class ExternalCollectionDefinition
        {
            public readonly string ItemId;
            public readonly string DisplayName;
            public readonly string Category;
            public readonly int SortOrder;

            public ExternalCollectionDefinition(string itemId, string displayName, string category, int sortOrder)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Category = category;
                SortOrder = sortOrder;
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// 부트스트래퍼가 도감 패널과 Fisher 런타임 컨텍스트를 주입할 때 호출합니다.
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
            global::RuntimeStateEventBus.OnCollectionStateEntered += HandleCollectionStateEntered;
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
                _context.RuntimeChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            global::RuntimeStateEventBus.OnCollectionStateEntered -= HandleCollectionStateEntered;
            if (_context != null)
            {
                _context.RuntimeChanged -= Refresh;
            }

            _collectionRewardRequest.Invalidate();
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
        /// 도감 보상 목록과 발견/수령 상태를 현재 카탈로그 기준으로 다시 그립니다.
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

            PullExternalCollectionDiscoveries();

            List<CollectionEntryView> visibleEntries = BuildVisibleEntries();
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

            HideLegacyRoot("Fisher_CollectionRoot");
            SetText(view.TitleText, string.Empty);
            SetText(view.StatusText, status);
            SetText(view.SubStatusText, string.Empty);
            FisherRuntimeUi.ApplyHeaderStatusParchmentText(view);
            ClearAllSlots(view);
            view.SetMainSectionsVisible(tabs: false, grid: false, detail: false, actions: false);
            view.HideActionSheets();
            return true;
        }

        private bool TryRenderStaticView(List<CollectionEntryView> visibleEntries)
        {
            FisherPanelView view = ResolveStaticView();
            if (view == null)
            {
                return false;
            }

            HideLegacyRoot("Fisher_CollectionRoot");
            view.SetMainSectionsVisible(tabs: true, grid: true, detail: false, actions: false);
            SetText(view.TitleText, string.Empty);
            int discoveredVisibleCount = CountDiscoveredEntries(visibleEntries);
            SetText(
                view.StatusText,
                "발견 " + discoveredVisibleCount + "/" + visibleEntries.Count +
                " · 보상 " + _context.State.claimedRewardIds.Count);
            SetText(view.SubStatusText, string.Empty);
            FisherRuntimeUi.ApplyHeaderStatusParchmentText(view);
            if (view.HeaderAction != null)
            {
                view.HeaderAction.gameObject.SetActive(false);
            }

            view.SetTabs(
                CollectionTabLabels,
                CollectionTabKeys,
                _category,
                key =>
                {
                    _category = key;
                    _lastMessage = "확인";
                    Refresh();
                });

            view.HideActionSheets();
            ClearAllSlots(view);
            int slotCount = visibleEntries.Count;
            WarnIfCollectionSlotPoolIsShort(view, visibleEntries.Count);
            for (int i = 0; i < slotCount; i++)
            {
                FisherSlotView slot = view.GetExistingSlot(i, "CollectionSlot");
                if (slot == null)
                {
                    continue;
                }

                if (i < visibleEntries.Count)
                {
                    BindCollectionSlot(slot, visibleEntries[i]);
                }
            }

            HideUnusedSlots(view, slotCount);
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
                    "CollectionPanel",
                    nameof(CollectionPanelAdapter),
                    FisherSlotLayout.Collection,
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
            Debug.LogWarning("[CollectionPanelAdapter] CollectionPanel/ViewRoot 고정 View를 만들거나 찾지 못했습니다. " +
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

        private void BindCollectionSlot(FisherSlotView slot, CollectionEntryView entry)
        {
            string rewardId = entry.ClaimableRewardId;
            bool claimable = !string.IsNullOrEmpty(rewardId);
            bool requestBusy = _collectionRewardRequest.IsBusy;
            bool requestPendingForThisReward = claimable && _collectionRewardRequest.IsBusyFor(rewardId);
            bool canClaim = claimable && !requestBusy;
            slot.Bind(
                entry.DisplayName,
                string.Empty,
                string.Empty,
                EntryStatusLabel(entry),
                entry.IconOverride != null
                    ? entry.IconOverride
                    : FisherRuntimeUi.ResolveItemIcon(_artProfile, entry.ItemId, entry.Category),
                requestPendingForThisReward,
                !entry.Discovered || (requestBusy && !requestPendingForThisReward),
                false,
                claimable && !requestBusy,
                canClaim
                    ? () => RequestServerCollectionRewardClaim(rewardId)
                    : null);
        }

        private void RequestServerCollectionRewardClaim(string rewardId)
        {
            if (string.IsNullOrEmpty(rewardId) || _collectionRewardRequest.IsBusy)
            {
                return;
            }

            int requestToken = BeginCollectionRewardRequestTimeout(rewardId);
            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            if (bridge == null ||
                !bridge.TryRequestCollectionRewardClaim(
                    rewardId,
                    () =>
                    {
                        if (!TryCompleteCollectionRewardRequest(requestToken))
                        {
                            return;
                        }

                        _lastMessage = "도감 보상 요청 반영";
                        _context?.NotifyRuntimeChanged();
                        ShowCollectionRewardReceipt(rewardId);
                        Refresh();
                    },
                    message =>
                    {
                        if (!TryCompleteCollectionRewardRequest(requestToken))
                        {
                            return;
                        }

                        _lastMessage = string.IsNullOrWhiteSpace(message) ? "도감 보상 실패" : message;
                        Debug.LogWarning("[CollectionPanelAdapter] Server collection reward rejected: " + _lastMessage);
                        Refresh();
                    }))
            {
                _collectionRewardRequest.TryAbort(requestToken);
                _lastMessage = "도감 보상 요청 실패";
                Debug.LogWarning("[CollectionPanelAdapter] Server collection reward request failed before CloudScript call: " + rewardId);
                Refresh();
                return;
            }

            _lastMessage = "도감 보상 요청 중";
            Debug.Log("[CollectionPanelAdapter] Server collection reward requested: " + rewardId);
            Refresh();
        }

        private int BeginCollectionRewardRequestTimeout(string rewardId)
        {
            if (!_collectionRewardRequest.TryBegin(rewardId))
            {
                return -1;
            }

            int token = _collectionRewardRequest.Token;
            StartCoroutine(ReleaseCollectionRewardRequestOnTimeout(token, rewardId));
            return token;
        }

        private bool TryCompleteCollectionRewardRequest(int token)
        {
            return _collectionRewardRequest.TryComplete(token);
        }

        private void ShowCollectionRewardReceipt(string rewardId)
        {
            Sprite icon = null;
            string itemName = CollectionRewardFallbackMeta(rewardId);
            string quantityText = string.Empty;
            if (_context != null &&
                _context.BuildResult != null &&
                _context.BuildResult.Catalog != null &&
                _context.BuildResult.Catalog.TryGetCollectionReward(rewardId, out CollectionRewardDefinition reward) &&
                reward != null)
            {
                if (!string.IsNullOrWhiteSpace(reward.RewardItemId) && reward.RewardItemCount > 0)
                {
                    itemName = CollectionItemName(reward.RewardItemId);
                    quantityText = CompactNumberFormatter.Format(reward.RewardItemCount) + "개";
                    icon = FisherRuntimeUi.ResolveItemIcon(_artProfile, reward.RewardItemId, string.Empty);
                }
                else if (reward.RewardAmount > 0)
                {
                    itemName = CollectionCurrencyName(reward.RewardCurrency);
                    quantityText = CompactNumberFormatter.Format(reward.RewardAmount);
                    icon = FisherRuntimeUi.ResolveItemIcon(_artProfile, CollectionCurrencyIconItemId(reward.RewardCurrency), string.Empty);
                }
            }

            FisherRuntimeUi.ShowCollectionReceipt(this, _panel == null ? null : _panel.transform, icon, itemName, quantityText);
        }

        private static string CollectionRewardFallbackMeta(string rewardId)
        {
            return string.IsNullOrWhiteSpace(rewardId)
                ? "보상 정보 확인 필요"
                : "보상 ID " + rewardId;
        }

        private static string CollectionCurrencyName(string currency)
        {
            if (FisherCurrencyContract.IsGoldCurrency(currency))
            {
                return "골드";
            }

            if (FisherCurrencyContract.IsPrismPearl(currency))
            {
                return "진주";
            }

            if (FisherCurrencyContract.IsPirateCoin(currency))
            {
                return "해적 주화";
            }

            return string.IsNullOrWhiteSpace(currency) ? "보상" : currency;
        }

        private static string CollectionCurrencyIconItemId(string currency)
        {
            if (FisherCurrencyContract.IsGoldCurrency(currency))
            {
                return "gold";
            }

            if (FisherCurrencyContract.IsPrismPearl(currency))
            {
                return "prismPearl";
            }

            if (FisherCurrencyContract.IsPirateCoin(currency))
            {
                return "pirateCoin";
            }

            return currency;
        }

        private string CollectionItemName(string itemId)
        {
            if (!string.IsNullOrWhiteSpace(itemId) &&
                _context != null &&
                _context.BuildResult != null &&
                _context.BuildResult.Catalog != null &&
                _context.BuildResult.Catalog.TryGetItem(itemId, out ItemDefinition item) &&
                item != null &&
                !string.IsNullOrWhiteSpace(item.DisplayNameKo))
            {
                return item.DisplayNameKo;
            }

            return string.IsNullOrWhiteSpace(itemId) ? "아이템" : itemId;
        }

        private IEnumerator ReleaseCollectionRewardRequestOnTimeout(int token, string rewardId)
        {
            yield return new WaitForSecondsRealtime(ServerCollectionRewardResponseTimeoutSeconds);

            if (!_collectionRewardRequest.TryRecoverTimeout(ServerCollectionRewardResponseTimeoutSeconds, rewardId, out string requestName))
            {
                yield break;
            }

            _lastMessage = "도감 보상 응답 지연/실패";
            Debug.LogWarning("[CollectionPanelAdapter] Server collection reward request timed out before callback: " + requestName);
            Refresh();
        }

        private void ClearAllSlots(FisherPanelView view)
        {
            if (view == null)
            {
                return;
            }

            HashSet<FisherSlotView> clearedSlots = new HashSet<FisherSlotView>();
            if (view.Slots != null)
            {
                for (int i = 0; i < view.Slots.Length; i++)
                {
                    if (view.Slots[i] == null || !clearedSlots.Add(view.Slots[i]))
                    {
                        continue;
                    }

                    view.Slots[i].Clear();
                }
            }

            if (view.GridContent == null)
            {
                return;
            }

            FisherSlotView[] childSlots = view.GridContent.GetComponentsInChildren<FisherSlotView>(true);
            for (int i = 0; i < childSlots.Length; i++)
            {
                FisherSlotView slot = childSlots[i];
                if (slot == null || !clearedSlots.Add(slot))
                {
                    continue;
                }

                slot.Clear();
            }
        }

        private void HideUnusedSlots(FisherPanelView view, int firstUnusedIndex)
        {
            if (view == null)
            {
                return;
            }

            view.HideUnusedSlots(firstUnusedIndex);
            if (view.GridContent == null)
            {
                return;
            }

            FisherSlotView[] childSlots = view.GridContent.GetComponentsInChildren<FisherSlotView>(true);
            for (int i = 0; i < childSlots.Length; i++)
            {
                FisherSlotView slot = childSlots[i];
                if (slot == null || !TryParseCollectionSlotIndex(slot.name, out int slotIndex))
                {
                    continue;
                }

                if (slotIndex >= firstUnusedIndex)
                {
                    slot.Clear();
                    slot.gameObject.SetActive(false);
                }
                else
                {
                    slot.gameObject.SetActive(true);
                }
            }
        }

        private void WarnIfCollectionSlotPoolIsShort(FisherPanelView view, int visibleEntryCount)
        {
            int slotCapacity = CountExistingCollectionSlots(view);
            if (slotCapacity < RequiredCollectionSlotPoolSize)
            {
                string poolWarningKey = _category + ":pool:" + slotCapacity;
                if (_lastSlotCapacityWarningKey != poolWarningKey)
                {
                    _lastSlotCapacityWarningKey = poolWarningKey;
                    Debug.LogWarning(
                        "[CollectionPanelAdapter] 도감 CollectionSlot 정적 풀은 " +
                        RequiredCollectionSlotPoolSize + "개가 필요합니다. 현재 slots=" + slotCapacity);
                }
            }

            if (visibleEntryCount <= slotCapacity)
            {
                return;
            }

            string warningKey = _category + ":" + visibleEntryCount + ":" + slotCapacity;
            if (_lastSlotCapacityWarningKey == warningKey)
            {
                return;
            }

            _lastSlotCapacityWarningKey = warningKey;
            Debug.LogWarning(
                "[CollectionPanelAdapter] 도감 표시 항목 수가 기존 CollectionSlot 풀보다 많습니다. " +
                "런타임 생성은 하지 않으므로 부족분은 표시되지 않습니다. category=" + _category +
                ", entries=" + visibleEntryCount +
                ", slots=" + slotCapacity);
        }

        private static int CountExistingCollectionSlots(FisherPanelView view)
        {
            int capacity = 0;
            if (view == null)
            {
                return capacity;
            }

            if (view.Slots != null)
            {
                for (int i = 0; i < view.Slots.Length; i++)
                {
                    if (view.Slots[i] != null)
                    {
                        capacity = Mathf.Max(capacity, i + 1);
                    }
                }
            }

            if (view.GridContent == null)
            {
                return capacity;
            }

            FisherSlotView[] childSlots = view.GridContent.GetComponentsInChildren<FisherSlotView>(true);
            for (int i = 0; i < childSlots.Length; i++)
            {
                FisherSlotView slot = childSlots[i];
                if (slot != null && TryParseCollectionSlotIndex(slot.name, out int slotIndex))
                {
                    capacity = Mathf.Max(capacity, slotIndex + 1);
                }
            }

            return capacity;
        }

        private static bool TryParseCollectionSlotIndex(string slotName, out int slotIndex)
        {
            slotIndex = -1;
            const string prefix = "CollectionSlot_";
            if (string.IsNullOrWhiteSpace(slotName) ||
                !slotName.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            string suffix = slotName.Substring(prefix.Length);
            return int.TryParse(suffix, out slotIndex) && slotIndex >= 0;
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

        private void HandleCollectionStateEntered()
        {
            _category = DefaultCollectionCategory;
            _lastMessage = "확인";
            Refresh();
        }

        #endregion

        #region External Discovery Sync

        private void PullExternalCollectionDiscoveries()
        {
            if (_context == null)
            {
                return;
            }

            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            if (bridge != null)
            {
                bridge.PullBoatCollectionDiscoveries(notify: false);
            }
        }

        #endregion

        #region Server Helpers

        private FisherPlayerDataBridge ResolvePlayerDataBridge()
        {
            return FisherPlayerDataBridgeResolver.Resolve(_context, this);
        }

        #endregion

        #region Collection Data

        private List<CollectionEntryView> BuildVisibleEntries()
        {
            List<CollectionEntryView> entries = new List<CollectionEntryView>();
            HashSet<string> includedIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (ItemDefinition item in _context.BuildResult.Catalog.ItemsById.Values)
            {
                if (item == null || !item.IsEnabled || !MatchesCategory(item.Category))
                {
                    continue;
                }

                entries.Add(BuildEntry(item));
                includedIds.Add(item.ItemId);
            }

            if (_category == "Crew")
            {
                AddCrewEntries(entries, includedIds);
            }
            else if (_category == "Ship")
            {
                AddBoatEntries(entries, includedIds);
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private CollectionEntryView BuildEntry(ItemDefinition item)
        {
            bool discovered = HasDiscovery(item.ItemId);
            int acquiredCount = _context.InventoryService.GetAcquiredCount(item.ItemId);
            if (IsCrewCategory(item.Category) && TryGetCrewOwnership(item.ItemId, out int ownedCrewCount))
            {
                discovered = true;
                acquiredCount = ownedCrewCount;
            }
            else if (IsBoatCategory(item.Category))
            {
                discovered = IsBoatDiscovered(item.ItemId, unlockedFromEquipment: false);
                acquiredCount = discovered ? Mathf.Max(1, acquiredCount) : acquiredCount;
            }

            CollectionEntryView entry = new CollectionEntryView
            {
                ItemId = item.ItemId,
                DisplayName = string.IsNullOrEmpty(item.DisplayNameKo) ? item.ItemId : item.DisplayNameKo,
                Category = item.Category,
                Discovered = discovered,
                AcquiredCount = acquiredCount,
                SortOrder = item.SortOrder,
                IconOverride = IsBoatCategory(item.Category) ? ResolveBoatSkillIcon(item.ItemId) : null
            };

            foreach (CollectionRewardDefinition reward in _context.BuildResult.Catalog.CollectionRewardsById.Values)
            {
                if (reward == null || !reward.IsEnabled || reward.ItemId != item.ItemId)
                {
                    continue;
                }

                entry.RewardCount++;
                if (_context.State.claimedRewardIds.Contains(reward.ClaimId))
                {
                    entry.ClaimedRewardCount++;
                    continue;
                }

                if (IsConditionMet(reward))
                {
                    entry.ClaimableRewardCount++;
                    if (string.IsNullOrEmpty(entry.ClaimableRewardId))
                    {
                        entry.ClaimableRewardId = reward.RewardId;
                    }
                }
            }

            return entry;
        }

        private void AddCrewEntries(List<CollectionEntryView> entries, HashSet<string> includedIds)
        {
            CrewManager crewManager = ResolveCrewManager();
            CrewDatabase crewDatabase = crewManager == null ? null : crewManager.CrewDataBase;
            if (crewDatabase != null && crewDatabase.Crews != null)
            {
                for (int i = 0; i < crewDatabase.Crews.Count; i++)
                {
                    CrewData crewData = crewDatabase.Crews[i];
                    if (crewData == null || string.IsNullOrWhiteSpace(crewData.CrewId))
                    {
                        continue;
                    }

                    TryAddCrewEntry(
                        entries,
                        includedIds,
                        crewData.CrewId,
                        string.IsNullOrWhiteSpace(crewData.CrewName) ? crewData.CrewId : crewData.CrewName,
                        2000 + i,
                        crewData.CrewSprite);
                }
            }

            foreach (ItemDefinition item in _context.BuildResult.Catalog.ItemsById.Values)
            {
                if (!IsCrewFragmentItem(item) || !TryGetCrewIdFromFragment(item.ItemId, out string crewId))
                {
                    continue;
                }

                TryAddCrewEntry(
                    entries,
                    includedIds,
                    crewId,
                    CrewDisplayNameFromFragment(item),
                    item.SortOrder);
            }
        }

        private void TryAddCrewEntry(
            List<CollectionEntryView> entries,
            HashSet<string> includedIds,
            string crewId,
            string displayName,
            int sortOrder,
            Sprite iconOverride = null)
        {
            if (string.IsNullOrWhiteSpace(crewId) || !includedIds.Add(crewId))
            {
                return;
            }

            bool discovered = TryGetCrewOwnership(crewId, out int acquiredCount);
            entries.Add(BuildExternalEntry(
                crewId,
                string.IsNullOrWhiteSpace(displayName) ? crewId : displayName,
                "Crew",
                discovered,
                acquiredCount,
                sortOrder,
                iconOverride));
        }

        private void AddBoatEntries(List<CollectionEntryView> entries, HashSet<string> includedIds)
        {
            EquipmentManager equipmentManager = FindFirstObjectByType<EquipmentManager>();
            int addedFromEquipment = 0;
            if (equipmentManager != null)
            {
                IReadOnlyList<BoatSkillEntry> boatSkills = equipmentManager.GetBoatSkills();
                if (boatSkills != null)
                {
                    for (int i = 0; i < boatSkills.Count; i++)
                    {
                        BoatSkillEntry boatSkill = boatSkills[i];
                        if (boatSkill == null || string.IsNullOrWhiteSpace(boatSkill.id))
                        {
                            continue;
                        }

                        TryAddBoatEntry(
                            entries,
                            includedIds,
                            boatSkill.id,
                            string.IsNullOrWhiteSpace(boatSkill.displayName) ? boatSkill.id : boatSkill.displayName,
                            boatSkill.unlocked,
                            1000 + i * 10,
                            boatSkill.icon);
                        addedFromEquipment++;
                    }
                }
            }

            if (addedFromEquipment > 0)
            {
                return;
            }

            for (int i = 0; i < FallbackBoatEntries.Length; i++)
            {
                ExternalCollectionDefinition fallback = FallbackBoatEntries[i];
                TryAddBoatEntry(
                    entries,
                    includedIds,
                    fallback.ItemId,
                    fallback.DisplayName,
                    unlockedFromEquipment: false,
                    fallback.SortOrder);
            }
        }

        private void TryAddBoatEntry(
            List<CollectionEntryView> entries,
            HashSet<string> includedIds,
            string boatId,
            string displayName,
            bool unlockedFromEquipment,
            int sortOrder,
            Sprite iconOverride = null)
        {
            if (string.IsNullOrWhiteSpace(boatId) || !includedIds.Add(boatId))
            {
                return;
            }

            bool discovered = IsBoatDiscovered(boatId, unlockedFromEquipment);
            entries.Add(BuildExternalEntry(
                boatId,
                string.IsNullOrWhiteSpace(displayName) ? boatId : displayName,
                "Boat",
                discovered,
                discovered ? 1 : 0,
                sortOrder,
                iconOverride));
        }

        private CollectionEntryView BuildExternalEntry(
            string itemId,
            string displayName,
            string category,
            bool discovered,
            int acquiredCount,
            int sortOrder,
            Sprite iconOverride = null)
        {
            return new CollectionEntryView
            {
                ItemId = itemId,
                DisplayName = displayName,
                Category = category,
                Discovered = discovered,
                AcquiredCount = acquiredCount,
                SortOrder = sortOrder,
                IconOverride = iconOverride
            };
        }

        private static int CompareEntries(CollectionEntryView left, CollectionEntryView right)
        {
            int category = CategorySortRank(left.Category).CompareTo(CategorySortRank(right.Category));
            if (category != 0)
            {
                return category;
            }

            int sort = left.SortOrder.CompareTo(right.SortOrder);
            if (sort != 0)
            {
                return sort;
            }

            return string.Compare(left.ItemId, right.ItemId, System.StringComparison.Ordinal);
        }

        private static int CategorySortRank(string category)
        {
            switch (category)
            {
                case "Fish":
                    return 0;
                case "Food":
                    return 1;
                case "Crew":
                case "Sailor":
                    return 2;
                case "Ship":
                case "Boat":
                    return 3;
                default:
                    return 4;
            }
        }

        #endregion

        #region Conditions

        private bool IsConditionMet(CollectionRewardDefinition reward)
        {
            if (!HasDiscovery(reward.ItemId))
            {
                return false;
            }

            if (reward.ConditionType == "discovery")
            {
                return true;
            }

            if (reward.ConditionType == "count")
            {
                int required = reward.ConditionValue > 0 ? reward.ConditionValue : 1;
                return _context.InventoryService.GetAcquiredCount(reward.ItemId) >= required;
            }

            return false;
        }

        private bool HasDiscovery(string itemId)
        {
            if (_context.State.discoveredCollectionItemIds.Contains(itemId))
            {
                return true;
            }

            foreach (FishDefinition fish in _context.BuildResult.Catalog.FishById.Values)
            {
                if (fish.IsEnabled && fish.ItemId == itemId && _context.State.discoveredCollectionItemIds.Contains(fish.FishId))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountDiscoveredEntries(List<CollectionEntryView> entries)
        {
            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Discovered)
                {
                    count++;
                }
            }

            return count;
        }

        private CrewManager ResolveCrewManager()
        {
            if (CrewManager.Instance != null)
            {
                return CrewManager.Instance;
            }

            return FindFirstObjectByType<CrewManager>();
        }

        private bool TryGetCrewOwnership(string crewId, out int acquiredCount)
        {
            acquiredCount = 0;
            if (string.IsNullOrWhiteSpace(crewId))
            {
                return false;
            }

            if (HasDiscovery(crewId))
            {
                acquiredCount = 1;
                return true;
            }

            CrewManager crewManager = ResolveCrewManager();
            List<CrewInstanceData> ownedCrews = crewManager == null ? null : crewManager.GetOwnedCrews();
            if (ownedCrews != null)
            {
                for (int i = 0; i < ownedCrews.Count; i++)
                {
                    CrewInstanceData crew = ownedCrews[i];
                    if (crew == null || crew.CrewId != crewId)
                    {
                        continue;
                    }

                    acquiredCount = Mathf.Max(1, 1 + crew.DuplicateCount);
                    return true;
                }
            }

            PlayerInfo playerInfo = TryGetPlayerInfo();
            if (playerInfo?.crew?.crews != null &&
                playerInfo.crew.crews.TryGetValue(crewId, out CrewInfo crewInfo))
            {
                acquiredCount = Mathf.Max(1, 1 + (crewInfo == null ? 0 : crewInfo.duplicateCount));
                return true;
            }

            return false;
        }

        private bool IsBoatDiscovered(string boatId, bool unlockedFromEquipment)
        {
            if (unlockedFromEquipment || HasDiscovery(boatId))
            {
                return true;
            }

            PlayerInfo playerInfo = TryGetPlayerInfo();
            if (playerInfo?.ship?.ships != null &&
                playerInfo.ship.ships.TryGetValue(boatId, out ShipInfo shipInfo))
            {
                return shipInfo != null && (shipInfo.isOpened || shipInfo.equipped);
            }

            return false;
        }

        private static Sprite ResolveBoatSkillIcon(string boatId)
        {
            if (string.IsNullOrWhiteSpace(boatId))
            {
                return null;
            }

            EquipmentManager equipmentManager = FindFirstObjectByType<EquipmentManager>();
            if (equipmentManager == null)
            {
                return null;
            }

            IReadOnlyList<BoatSkillEntry> boatSkills = equipmentManager.GetBoatSkills();
            if (boatSkills == null)
            {
                return null;
            }

            for (int i = 0; i < boatSkills.Count; i++)
            {
                BoatSkillEntry boatSkill = boatSkills[i];
                if (boatSkill != null &&
                    string.Equals(boatSkill.id, boatId, System.StringComparison.Ordinal) &&
                    boatSkill.icon != null)
                {
                    return boatSkill.icon;
                }
            }

            return null;
        }

        private static PlayerInfo TryGetPlayerInfo()
        {
            PlayFabDataStore dataStore = PlayFabDataStore.Instance;
            if (dataStore == null)
            {
                return null;
            }

            try
            {
                return dataStore.GetPlayerInfo();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsCrewFragmentItem(ItemDefinition item)
        {
            return item != null &&
                   item.IsEnabled &&
                   item.CookTag == "crew_fragment" &&
                   TryGetCrewIdFromFragment(item.ItemId, out _);
        }

        private static bool TryGetCrewIdFromFragment(string itemId, out string crewId)
        {
            crewId = null;
            if (string.IsNullOrWhiteSpace(itemId) ||
                !itemId.StartsWith(CrewFragmentPrefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            crewId = itemId.Substring(CrewFragmentPrefix.Length);
            return !string.IsNullOrWhiteSpace(crewId);
        }

        private static string CrewDisplayNameFromFragment(ItemDefinition item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string displayName = string.IsNullOrWhiteSpace(item.DisplayNameKo) ? item.ItemId : item.DisplayNameKo;
            const string suffix = " 조각";
            return displayName.EndsWith(suffix, System.StringComparison.Ordinal)
                ? displayName.Substring(0, displayName.Length - suffix.Length)
                : displayName;
        }

        private void SyncRewardToPlayerData(ServiceResult result)
        {
            if (result == null || !result.Success || _context == null)
            {
                return;
            }

            FisherPlayerDataBridge bridge = ResolvePlayerDataBridge();
            if (bridge == null)
            {
                return;
            }

            if (result.CurrencyDelta > 0 && !bridge.TryAddGoldToPlayerData(result.CurrencyDelta))
            {
                Debug.LogWarning("[CollectionPanelAdapter] PlayerData gold sync failed after reward: +" + result.CurrencyDelta);
            }

            if (!bridge.SyncItemDeltasToPlayerData(result))
            {
                Debug.LogWarning("[CollectionPanelAdapter] PlayerData item sync failed after reward.");
            }
        }

        #endregion

        #region Text Helpers

        private static string EntryStatusLabel(CollectionEntryView entry)
        {
            if (entry == null || entry.RewardCount <= 0)
            {
                return string.Empty;
            }

            if (entry.ClaimableRewardCount > 0)
            {
                return "수령 가능";
            }

            if (entry.ClaimedRewardCount >= entry.RewardCount)
            {
                return "수령 완료";
            }

            return "조건 미달성";
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

        private bool MatchesCategory(string itemCategory)
        {
            if (_category == "Crew")
            {
                return IsCrewCategory(itemCategory);
            }

            if (_category == "Ship")
            {
                return IsBoatCategory(itemCategory);
            }

            return itemCategory == _category;
        }

        private static bool IsCrewCategory(string itemCategory)
        {
            return itemCategory == "Crew" ||
                   itemCategory == "Sailor";
        }

        private static bool IsBoatCategory(string itemCategory)
        {
            return itemCategory == "Ship" ||
                   itemCategory == "Boat";
        }

        #endregion
    }
}
